using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace POGLib.Core
{
    /// <summary>A GLB file that cannot be read; the message is for the modder.</summary>
    internal sealed class GlbException : Exception
    {
        public GlbException(string message) : base(message) { }
    }

    /// <summary>One material of the file and the triangles that wear it.</summary>
    internal sealed class GlbPart
    {
        public string MaterialName = "default";
        /// <summary>Linear, as glTF writes it.</summary>
        public float[] BaseColor = { 1f, 1f, 1f, 1f };
        public float Metallic = 1f, Roughness = 1f;
        public float[] Emissive = { 0f, 0f, 0f };
        /// <summary>Drawn from behind too (Blender writes it unless "Backface Culling" is on).</summary>
        public bool DoubleSided;
        /// <summary>The base colour picture's own bytes (PNG or JPEG); null when there is none.</summary>
        public byte[] BaseColorImage;
        public readonly List<int> Triangles = new List<int>();
        /// <summary>Per triangle: which object of the file it belongs to (index into <see cref="GlbModel.Objects"/>).</summary>
        public readonly List<int> ObjectOf = new List<int>();
    }

    /// <summary>
    /// A model from a GLB file (binary glTF 2.0): every mesh of its scene put into one, in the pose the file is saved in,
    /// in glTF's axes (metres, Y up, facing +Z, right-handed). Triangles are grouped by material, in the order the file
    /// names them first.
    /// </summary>
    internal sealed class GlbModel
    {
        public readonly List<float> Positions = new List<float>();   // x, y, z
        public readonly List<float> Normals = new List<float>();     // as Positions
        public readonly List<float> Uv = new List<float>();          // u, v (v down, as glTF has it); zeros where the file has none
        public readonly List<int> Joints = new List<int>();          // four per vertex: index into JointNames
        public readonly List<float> Weights = new List<float>();     // four per vertex, adding up to 1; all zero for a vertex of a mesh without a skin
        public readonly List<string> JointNames = new List<string>();
        public readonly List<GlbPart> Parts = new List<GlbPart>();
        /// <summary>The names of the objects that have a mesh (Blender's object names).</summary>
        public readonly List<string> Objects = new List<string>();
        /// <summary>False when a mesh of the file has no normals (they are then worked out from the triangles).</summary>
        public bool HasNormals = true;
        /// <summary>What the file holds that is not read, for the log.</summary>
        public readonly List<string> Notes = new List<string>();

        public int VertexCount => Positions.Count / 3;
        public int Unweighted
        {
            get
            {
                int n = 0;
                for (int v = 0; v < VertexCount; v++)
                    if (Weights[v * 4] + Weights[v * 4 + 1] + Weights[v * 4 + 2] + Weights[v * 4 + 3] <= 0f) n++;
                return n;
            }
        }
    }

    /// <summary>
    /// Reads a GLB as any modelling tool exports it: triangles, normals, one set of UVs, up to four bones per vertex, the base
    /// material (colour, its picture, metallic, roughness, emission). What it cannot read it refuses or lists in
    /// <see cref="GlbModel.Notes"/>, never guesses. No Unity types, so the unit tests compile it. Every length in the file is
    /// checked against the bytes that are there before anything is read or made.
    /// </summary>
    internal static class Glb
    {
        private const uint Magic = 0x46546C67, ChunkJson = 0x4E4F534A, ChunkBin = 0x004E4942;
        private const int MaxDepth = 64;

        public static GlbModel Read(byte[] file)
        {
            if (file == null || file.Length < 20) throw new GlbException("the file is too short to be a GLB");
            if (U32(file, 0) != Magic) throw new GlbException("not a GLB file (export it as \"glTF Binary (.glb)\")");
            if (U32(file, 4) != 2) throw new GlbException($"glTF version {U32(file, 4)}; only 2 is read");
            byte[] json = null, bin = null;
            long at = 12;
            while (at + 8 <= file.Length)
            {
                long length = U32(file, (int)at);
                uint type = U32(file, (int)at + 4);
                if (at + 8 + length > file.Length) throw new GlbException("the file is cut short");
                if (type == ChunkJson && json == null) json = file.AsSpan((int)at + 8, (int)length).ToArray();
                else if (type == ChunkBin && bin == null) bin = file.AsSpan((int)at + 8, (int)length).ToArray();
                at += 8 + length;
            }
            if (json == null) throw new GlbException("the file has no JSON part");
            try
            {
                using var doc = JsonDocument.Parse(json);
                return new Reader(doc.RootElement, bin ?? Array.Empty<byte>()).Read();
            }
            catch (JsonException ex) { throw new GlbException("its JSON part cannot be read: " + ex.Message); }
            catch (Exception ex) when (ex is InvalidOperationException || ex is FormatException || ex is KeyNotFoundException)
            { throw new GlbException("its JSON part is not what glTF 2.0 says: " + ex.Message); }
        }

        private static uint U32(byte[] b, int at) => (uint)(b[at] | b[at + 1] << 8 | b[at + 2] << 16 | b[at + 3] << 24);

        // ---- 4x4 matrices, column by column, as glTF writes them ----

        private static readonly double[] Identity = { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };

        private static double[] Mul(double[] a, double[] b)
        {
            var r = new double[16];
            for (int c = 0; c < 4; c++)
                for (int row = 0; row < 4; row++)
                    for (int k = 0; k < 4; k++) r[c * 4 + row] += a[k * 4 + row] * b[c * 4 + k];
            return r;
        }

        private static double Det3(double[] m) =>
            m[0] * (m[5] * m[10] - m[9] * m[6]) - m[4] * (m[1] * m[10] - m[9] * m[2]) + m[8] * (m[1] * m[6] - m[5] * m[2]);

        // The cofactor matrix: what a normal goes through (its length is put right afterwards).
        private static double[] Cofactor3(double[] m) => new[]
        {
            m[5] * m[10] - m[9] * m[6], m[8] * m[6] - m[4] * m[10], m[4] * m[9] - m[8] * m[5],
            m[9] * m[2] - m[1] * m[10], m[0] * m[10] - m[8] * m[2], m[8] * m[1] - m[0] * m[9],
            m[1] * m[6] - m[5] * m[2], m[4] * m[2] - m[0] * m[6], m[0] * m[5] - m[4] * m[1],
        };

        private sealed class Reader
        {
            private readonly JsonElement _root;
            private readonly byte[] _bin;
            private readonly GlbModel _model = new GlbModel();
            private readonly Dictionary<int, GlbPart> _partOf = new Dictionary<int, GlbPart>();   // by material index, -1 = none
            private readonly Dictionary<int, double[]> _global = new Dictionary<int, double[]>();  // node -> its place in the scene
            private readonly Dictionary<string, int> _joint = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly HashSet<string> _noted = new HashSet<string>(StringComparer.Ordinal);

            public Reader(JsonElement root, byte[] bin) { _root = root; _bin = bin; }

            public GlbModel Read()
            {
                if (_root.TryGetProperty("extensionsRequired", out var required) && required.GetArrayLength() > 0)
                    throw new GlbException("it needs the glTF extension(s) " + string.Join(", ", required.EnumerateArray().Select(e => e.GetString())) + " (export without compression)");
                var nodes = List("nodes");
                var order = new List<int>();
                foreach (int n in SceneRoots()) Walk(n, Identity, 0, order);
                int meshes = 0;
                foreach (int n in order)
                    if (nodes[n].TryGetProperty("mesh", out var mesh))
                    {
                        meshes++;
                        _model.Objects.Add(nodes[n].TryGetProperty("name", out var name) ? name.GetString() ?? "" : "");
                        AddMesh(nodes[n], mesh.GetInt32(), _global[n]);
                    }
                if (meshes == 0) throw new GlbException("its scene has no mesh");
                if (_model.VertexCount == 0 || _model.Parts.All(p => p.Triangles.Count == 0)) throw new GlbException("its meshes have no triangles");
                if (!_model.HasNormals) for (int i = 0; i < _model.Normals.Count; i++) _model.Normals[i] = 0f;
                return _model;
            }

            private void Note(string text) { if (_noted.Add(text)) _model.Notes.Add(text); }

            private readonly Dictionary<string, List<JsonElement>> _lists = new Dictionary<string, List<JsonElement>>(StringComparer.Ordinal);

            private List<JsonElement> List(string name)
            {
                if (!_lists.TryGetValue(name, out var list))
                    _lists[name] = list = _root.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().ToList() : new List<JsonElement>();
                return list;
            }

            private JsonElement Item(string list, int index)
            {
                var all = List(list);
                if (index < 0 || index >= all.Count) throw new GlbException($"{list}[{index}] does not exist (there are {all.Count})");
                return all[index];
            }

            private static int Int(JsonElement e, string name, int otherwise) => e.TryGetProperty(name, out var v) ? v.GetInt32() : otherwise;

            // ---- nodes ----

            private IEnumerable<int> SceneRoots()
            {
                var scenes = List("scenes");
                if (scenes.Count > 0)
                {
                    int s = Int(_root, "scene", 0);
                    if (s < 0 || s >= scenes.Count) throw new GlbException($"its scene {s} does not exist");
                    return scenes[s].TryGetProperty("nodes", out var roots) ? roots.EnumerateArray().Select(x => x.GetInt32()).ToList() : new List<int>();
                }
                var children = new HashSet<int>();
                var nodes = List("nodes");
                foreach (var n in nodes) if (n.TryGetProperty("children", out var c)) foreach (var x in c.EnumerateArray()) children.Add(x.GetInt32());
                return Enumerable.Range(0, nodes.Count).Where(i => !children.Contains(i)).ToList();
            }

            private void Walk(int index, double[] parent, int depth, List<int> order)
            {
                if (depth > MaxDepth || _global.ContainsKey(index)) throw new GlbException("its nodes go round in a circle or too deep");
                var n = Item("nodes", index);
                var world = Mul(parent, Local(n));
                _global[index] = world;
                order.Add(index);
                if (n.TryGetProperty("children", out var c)) foreach (var x in c.EnumerateArray()) Walk(x.GetInt32(), world, depth + 1, order);
            }

            private static double[] Local(JsonElement n)
            {
                if (n.TryGetProperty("matrix", out var m)) return Numbers(m, 16);
                var t = n.TryGetProperty("translation", out var tt) ? Numbers(tt, 3) : new double[] { 0, 0, 0 };
                var q = n.TryGetProperty("rotation", out var qq) ? Numbers(qq, 4) : new double[] { 0, 0, 0, 1 };
                var s = n.TryGetProperty("scale", out var ss) ? Numbers(ss, 3) : new double[] { 1, 1, 1 };
                double x = q[0], y = q[1], z = q[2], w = q[3];
                return new[]
                {
                    (1 - 2 * (y * y + z * z)) * s[0], 2 * (x * y + z * w) * s[0], 2 * (x * z - y * w) * s[0], 0,
                    2 * (x * y - z * w) * s[1], (1 - 2 * (x * x + z * z)) * s[1], 2 * (y * z + x * w) * s[1], 0,
                    2 * (x * z + y * w) * s[2], 2 * (y * z - x * w) * s[2], (1 - 2 * (x * x + y * y)) * s[2], 0,
                    t[0], t[1], t[2], 1,
                };
            }

            private static double[] Numbers(JsonElement e, int count)
            {
                var a = e.EnumerateArray().Select(v => v.GetDouble()).ToArray();
                if (a.Length != count || a.Any(v => double.IsNaN(v) || double.IsInfinity(v))) throw new GlbException($"a list of {count} numbers was expected");
                return a;
            }

            // ---- meshes ----

            private void AddMesh(JsonElement node, int meshIndex, double[] world)
            {
                double[][] skin = null;       // per joint of this skin: where a point of the mesh goes in the file's pose
                int[] names = null;
                if (node.TryGetProperty("skin", out var s))
                {
                    var sk = Item("skins", s.GetInt32());
                    var joints = sk.GetProperty("joints").EnumerateArray().Select(j => j.GetInt32()).ToArray();
                    float[] inverse = null;
                    if (sk.TryGetProperty("inverseBindMatrices", out var ibm))
                    {
                        inverse = Floats(ibm.GetInt32(), "MAT4", out int n);
                        if (n < joints.Length) throw new GlbException("a skin has fewer matrices than bones");
                    }
                    skin = new double[joints.Length][];
                    names = new int[joints.Length];
                    for (int j = 0; j < joints.Length; j++)
                    {
                        var jn = Item("nodes", joints[j]);
                        if (!_global.TryGetValue(joints[j], out var g)) throw new GlbException("a bone is not in the scene");
                        skin[j] = inverse == null ? g : Mul(g, inverse.Skip(j * 16).Take(16).Select(v => (double)v).ToArray());
                        names[j] = JointIndex(jn.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "");
                    }
                }
                foreach (var p in Item("meshes", meshIndex).GetProperty("primitives").EnumerateArray()) AddPrimitive(p, world, skin, names);
            }

            private int JointIndex(string name)
            {
                if (!_joint.TryGetValue(name, out int i)) { i = _model.JointNames.Count; _model.JointNames.Add(name); _joint[name] = i; }
                return i;
            }

            private void AddPrimitive(JsonElement p, double[] world, double[][] skin, int[] names)
            {
                if (Int(p, "mode", 4) != 4) { Note("lines or points"); return; }
                if (p.TryGetProperty("targets", out _)) Note("shape keys");
                var attributes = p.GetProperty("attributes");
                if (!attributes.TryGetProperty("POSITION", out var pa)) throw new GlbException("a mesh has no positions");
                var pos = Floats(pa.GetInt32(), "VEC3", out int count);
                float[] nor = Optional(attributes, "NORMAL", "VEC3", count), uv = Optional(attributes, "TEXCOORD_0", "VEC2", count);
                if (attributes.TryGetProperty("COLOR_0", out _)) Note("vertex colours");
                if (attributes.TryGetProperty("JOINTS_1", out _)) throw new GlbException("a vertex has more than four bones (export with at most 4 bone influences)");
                int[] joints = null; float[] weights = null;
                if (skin != null && attributes.TryGetProperty("JOINTS_0", out var ja))
                {
                    if (!attributes.TryGetProperty("WEIGHTS_0", out _)) throw new GlbException("a mesh has bones but no weights");
                    joints = Ints(ja.GetInt32(), "VEC4", out int jn);
                    if (jn != count) throw new GlbException("JOINTS_0 and POSITION differ in length");
                    weights = Optional(attributes, "WEIGHTS_0", "VEC4", count);
                }

                int first = _model.VertexCount;
                if (nor == null) _model.HasNormals = false;
                bool mirrored = false;
                for (int v = 0; v < count; v++)
                {
                    var m = world;
                    double sum = 0;
                    if (weights != null)
                    {
                        var blend = new double[16];
                        for (int k = 0; k < 4; k++)
                        {
                            double w = weights[v * 4 + k];
                            if (w <= 0) continue;
                            int j = joints[v * 4 + k];
                            if (j < 0 || j >= skin.Length) throw new GlbException($"a vertex names bone {j} of {skin.Length}");
                            for (int i = 0; i < 16; i++) blend[i] += w * skin[j][i];
                            sum += w;
                        }
                        if (sum > 0) { for (int i = 0; i < 16; i++) blend[i] /= sum; m = blend; }
                    }
                    if (v == 0) mirrored = Det3(m) < 0;
                    double x = pos[v * 3], y = pos[v * 3 + 1], z = pos[v * 3 + 2];
                    _model.Positions.Add((float)(m[0] * x + m[4] * y + m[8] * z + m[12]));
                    _model.Positions.Add((float)(m[1] * x + m[5] * y + m[9] * z + m[13]));
                    _model.Positions.Add((float)(m[2] * x + m[6] * y + m[10] * z + m[14]));
                    if (nor == null) { _model.Normals.Add(0); _model.Normals.Add(0); _model.Normals.Add(0); }
                    else
                    {
                        var c = Cofactor3(m);
                        double sign = Det3(m) < 0 ? -1 : 1;
                        double nx = nor[v * 3], ny = nor[v * 3 + 1], nz = nor[v * 3 + 2];
                        double ox = sign * (c[0] * nx + c[3] * ny + c[6] * nz), oy = sign * (c[1] * nx + c[4] * ny + c[7] * nz), oz = sign * (c[2] * nx + c[5] * ny + c[8] * nz);
                        double len = Math.Sqrt(ox * ox + oy * oy + oz * oz);
                        if (len < 1e-12) { ox = 0; oy = 1; oz = 0; len = 1; }
                        _model.Normals.Add((float)(ox / len)); _model.Normals.Add((float)(oy / len)); _model.Normals.Add((float)(oz / len));
                    }
                    _model.Uv.Add(uv == null ? 0f : uv[v * 2]);
                    _model.Uv.Add(uv == null ? 0f : uv[v * 2 + 1]);
                    for (int k = 0; k < 4; k++)
                    {
                        float w = weights == null || sum <= 0 ? 0f : (float)Math.Max(0, weights[v * 4 + k] / sum);
                        _model.Weights.Add(w);
                        _model.Joints.Add(w > 0f ? names[joints[v * 4 + k]] : 0);
                    }
                }

                int[] index;
                if (p.TryGetProperty("indices", out var ia)) index = Ints(ia.GetInt32(), "SCALAR", out _);
                else index = Enumerable.Range(0, count).ToArray();
                if (index.Length % 3 != 0) throw new GlbException("a mesh's triangles are not threes of indices");
                var part = PartFor(Int(p, "material", -1));
                for (int i = 0; i < index.Length; i += 3)
                {
                    int a = index[i], b = index[i + 1], d = index[i + 2];
                    if (a < 0 || b < 0 || d < 0 || a >= count || b >= count || d >= count) throw new GlbException("a triangle names a vertex that is not there");
                    part.Triangles.Add(first + a);
                    part.Triangles.Add(first + (mirrored ? d : b));
                    part.Triangles.Add(first + (mirrored ? b : d));
                    part.ObjectOf.Add(_model.Objects.Count - 1);
                }
            }

            private float[] Optional(JsonElement attributes, string name, string type, int count)
            {
                if (!attributes.TryGetProperty(name, out var a)) return null;
                var f = Floats(a.GetInt32(), type, out int n);
                if (n != count) throw new GlbException($"{name} and POSITION differ in length");
                return f;
            }

            // ---- materials ----

            private GlbPart PartFor(int material)
            {
                if (_partOf.TryGetValue(material, out var part)) return part;
                part = new GlbPart();
                _partOf[material] = part;
                _model.Parts.Add(part);
                if (material < 0) return part;
                var m = Item("materials", material);
                part.MaterialName = m.TryGetProperty("name", out var nm) ? nm.GetString() ?? "material" : "material" + material;
                if (m.TryGetProperty("pbrMetallicRoughness", out var pbr))
                {
                    if (pbr.TryGetProperty("baseColorFactor", out var bc)) part.BaseColor = Numbers(bc, 4).Select(v => (float)v).ToArray();
                    if (pbr.TryGetProperty("metallicFactor", out var mf)) part.Metallic = mf.GetSingle();
                    if (pbr.TryGetProperty("roughnessFactor", out var rf)) part.Roughness = rf.GetSingle();
                    if (pbr.TryGetProperty("baseColorTexture", out var bt)) part.BaseColorImage = Image(bt, part.MaterialName);
                    if (pbr.TryGetProperty("metallicRoughnessTexture", out _)) Note($"{part.MaterialName}: the metallic/roughness picture");
                }
                if (m.TryGetProperty("emissiveFactor", out var ef)) part.Emissive = Numbers(ef, 3).Select(v => (float)v).ToArray();
                if (m.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("KHR_materials_emissive_strength", out var es) && es.TryGetProperty("emissiveStrength", out var st))
                    part.Emissive = part.Emissive.Select(v => v * st.GetSingle()).ToArray();
                if (m.TryGetProperty("doubleSided", out var ds)) part.DoubleSided = ds.GetBoolean();
                if (m.TryGetProperty("normalTexture", out _)) Note($"{part.MaterialName}: the normal map");
                if (m.TryGetProperty("emissiveTexture", out _)) Note($"{part.MaterialName}: the emission picture");
                if (m.TryGetProperty("occlusionTexture", out _)) Note($"{part.MaterialName}: the occlusion picture");
                if (m.TryGetProperty("alphaMode", out var am) && am.GetString() != "OPAQUE") Note($"{part.MaterialName}: transparency ({am.GetString()}), drawn opaque");
                return part;
            }

            private byte[] Image(JsonElement textureInfo, string material)
            {
                if (Int(textureInfo, "texCoord", 0) != 0) { Note($"{material}: a picture on a second UV map"); return null; }
                if (textureInfo.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("KHR_texture_transform", out _)) Note($"{material}: the picture's own offset and scale");
                var texture = Item("textures", textureInfo.GetProperty("index").GetInt32());
                if (!texture.TryGetProperty("source", out var src)) { Note($"{material}: a picture that is neither PNG nor JPEG"); return null; }
                var image = Item("images", src.GetInt32());
                if (!image.TryGetProperty("bufferView", out var bv)) throw new GlbException($"{material}: its picture is in another file (export it inside the GLB)");
                var (start, length, _) = View(bv.GetInt32());
                return _bin.AsSpan(start, length).ToArray();
            }

            // ---- numbers out of the binary part ----

            private (int start, int length, int stride) View(int index)
            {
                var v = Item("bufferViews", index);
                if (Int(v, "buffer", 0) != 0) throw new GlbException("its numbers are in another file (export as a single GLB)");
                long start = Int(v, "byteOffset", 0), length = v.GetProperty("byteLength").GetInt64();
                int stride = Int(v, "byteStride", 0);
                if (start < 0 || length < 0 || length > _bin.Length || stride < 0 || start + length > _bin.Length) throw new GlbException("the file is cut short");
                return ((int)start, (int)length, stride);
            }

            private static int Components(string type) => type switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, "MAT4" => 16, _ => 0 };

            private static int Size(int componentType) => componentType switch { 5120 => 1, 5121 => 1, 5122 => 2, 5123 => 2, 5125 => 4, 5126 => 4, _ => 0 };

            private (JsonElement a, int start, int step, int count, int kind, bool normalized) Accessor(int index, string type)
            {
                var a = Item("accessors", index);
                if (a.GetProperty("type").GetString() != type) throw new GlbException($"a list of {type} was expected, {a.GetProperty("type").GetString()} found");
                if (a.TryGetProperty("sparse", out _)) throw new GlbException("sparse lists are not read");
                if (!a.TryGetProperty("bufferView", out var bv)) throw new GlbException("a list has no numbers");
                int kind = a.GetProperty("componentType").GetInt32(), size = Size(kind);
                if (size == 0) throw new GlbException($"numbers of kind {kind} are not read");
                long count = a.GetProperty("count").GetInt64(), offset = Int(a, "byteOffset", 0), one = (long)size * Components(type);
                var (start, length, stride) = View(bv.GetInt32());
                long step = stride > 0 ? stride : one;
                if (count < 0 || count > _bin.Length || offset < 0 || step < one || (count > 0 && offset + (count - 1) * step + one > length)) throw new GlbException("a list is longer than its bytes");
                return (a, (int)(start + offset), (int)step, (int)count, kind, a.TryGetProperty("normalized", out var n) && n.GetBoolean());
            }

            private float[] Floats(int index, string type, out int count)
            {
                var (_, start, step, n, kind, normalized) = Accessor(index, type);
                int per = Components(type);
                count = n;
                var r = new float[n * per];
                for (int i = 0; i < n; i++)
                    for (int k = 0; k < per; k++)
                    {
                        int at = start + i * step + k * Size(kind);
                        float f = kind == 5126 ? BitConverter.ToSingle(_bin, at) : Whole(at, kind);
                        if (kind != 5126)
                        {
                            if (!normalized) throw new GlbException("whole numbers where fractions were expected");
                            f = kind switch { 5121 => f / 255f, 5123 => f / 65535f, 5120 => Math.Max(f / 127f, -1f), 5122 => Math.Max(f / 32767f, -1f), _ => throw new GlbException($"numbers of kind {kind} are not read") };
                        }
                        if (float.IsNaN(f) || float.IsInfinity(f)) throw new GlbException("a number in it is not a number");
                        r[i * per + k] = f;
                    }
                return r;
            }

            private int[] Ints(int index, string type, out int count)
            {
                var (_, start, step, n, kind, _) = Accessor(index, type);
                if (kind != 5121 && kind != 5123 && kind != 5125) throw new GlbException("whole numbers were expected");
                int per = Components(type);
                count = n;
                var r = new int[n * per];
                for (int i = 0; i < n; i++)
                    for (int k = 0; k < per; k++)
                    {
                        long whole = kind == 5125 ? U32(_bin, start + i * step + k * 4) : (long)Whole(start + i * step + k * Size(kind), kind);
                        if (whole > int.MaxValue) throw new GlbException("an index is too large");
                        r[i * per + k] = (int)whole;
                    }
                return r;
            }

            private float Whole(int at, int kind) => kind switch
            {
                5120 => (sbyte)_bin[at],
                5121 => _bin[at],
                5122 => BitConverter.ToInt16(_bin, at),
                5123 => BitConverter.ToUInt16(_bin, at),
                _ => throw new GlbException($"numbers of kind {kind} are not read"),
            };
        }

        public static GlbModel ReadFile(string path) => Read(File.ReadAllBytes(path));
    }
}
