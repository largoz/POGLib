using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using POGLib.Core;
using Xunit;

namespace POGLib.Shops.Tests
{
    /// <summary>The GLB reader against files Blender's own exporter wrote (fixtures/make_glb.py): the numbers below are the
    /// ones planted there, in Blender's axes.</summary>
    public class GlbTests
    {
        private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);
        private static GlbModel Skinned() => Glb.ReadFile(Fixture("skinned.glb"));
        private static GlbModel Static() => Glb.ReadFile(Fixture("static.glb"));

        private static float[] P(GlbModel m, int v) => new[] { m.Positions[v * 3], m.Positions[v * 3 + 1], m.Positions[v * 3 + 2] };
        private static float[] FromBlender(double x, double y, double z) => new[] { (float)x, (float)z, (float)-y };
        private static bool Near(float[] a, float[] b, float eps = 1e-4f) => a.Zip(b, (x, y) => Math.Abs(x - y)).All(d => d < eps);

        private static readonly double[][] Places =
        {
            new[] { 0.10, -0.05, 0.40 }, new[] { -0.10, -0.05, 0.40 }, new[] { 0.10, -0.05, 0.60 }, new[] { -0.10, -0.05, 0.60 }, new[] { 0.0, 0.07, 0.75 },
        };

        private static readonly Dictionary<string, float>[] PlantedWeights =
        {
            new Dictionary<string, float> { ["mixamorig:Hips"] = 1f },
            new Dictionary<string, float> { ["mixamorig:Hips"] = .5f, ["mixamorig:Spine"] = .5f },
            new Dictionary<string, float> { ["mixamorig:Spine"] = .75f, ["mixamorig:LeftArm"] = .25f },
            new Dictionary<string, float> { ["mixamorig:Spine"] = 1f },
            new Dictionary<string, float> { ["mixamorig:Head"] = .6f, ["mixamorig:Spine"] = .4f },
        };

        private static int PlaceOf(GlbModel m, int v)
        {
            for (int i = 0; i < Places.Length; i++) if (Near(P(m, v), FromBlender(Places[i][0], Places[i][1], Places[i][2]))) return i;
            return -1;
        }

        [Fact]
        public void EveryVertexStandsWhereTheFileShowsIt_ThoughTheArmatureIsMovedTurnedAndScaled()
        {
            var m = Skinned();
            Assert.True(m.VertexCount >= Places.Length);
            var seen = new HashSet<int>();
            for (int v = 0; v < m.VertexCount; v++) { int i = PlaceOf(m, v); Assert.True(i >= 0, $"vertex {v} at {string.Join(",", P(m, v))}"); seen.Add(i); }
            Assert.Equal(Places.Length, seen.Count);
        }

        [Fact]
        public void WeightsGoToTheBonesByName()
        {
            var m = Skinned();
            for (int v = 0; v < m.VertexCount; v++)
            {
                var got = new Dictionary<string, float>();
                for (int k = 0; k < 4; k++) if (m.Weights[v * 4 + k] > 0) got[m.JointNames[m.Joints[v * 4 + k]]] = m.Weights[v * 4 + k];
                var want = PlantedWeights[PlaceOf(m, v)];
                Assert.Equal(want.Keys.OrderBy(x => x), got.Keys.OrderBy(x => x));
                foreach (var k in want.Keys) Assert.Equal(want[k], got[k], 2);
            }
            Assert.Equal(0, m.Unweighted);
        }

        [Fact]
        public void TwoSkinsWhoseBonesComeInAnotherOrder_MeetByName()
        {
            var m = Glb.ReadFile(Fixture("twoskins.glb"));
            Assert.Equal(6, m.VertexCount);
            Assert.Equal(new[] { "mixamorig:Arm", "mixamorig:Hips", "mixamorig:Spine" }, m.JointNames.OrderBy(x => x));
            for (int v = 0; v < m.VertexCount; v++)
                for (int k = 0; k < 4; k++)
                    if (m.Weights[v * 4 + k] > 0) Assert.Equal("mixamorig:Hips", m.JointNames[m.Joints[v * 4 + k]]);
            Assert.Equal(0, m.Unweighted);
        }

        [Fact]
        public void MaterialsKeepTheirOrderColoursAndPicture()
        {
            var m = Skinned();
            Assert.Equal(new[] { "Cloth", "Brass" }, m.Parts.Select(p => p.MaterialName));
            Assert.Equal(new[] { 2, 1 }, m.Parts.Select(p => p.Triangles.Count / 3));
            var brass = m.Parts[1];
            Assert.True(Near(brass.BaseColor, new[] { .8f, .6f, .1f, 1f }));
            Assert.Equal(.7f, brass.Metallic, 3);
            Assert.Equal(.3f, brass.Roughness, 3);
            Assert.True(Near(brass.Emissive, new[] { 1.5f, .75f, 0f }), string.Join(",", brass.Emissive));   // colour x strength 3
            Assert.Null(brass.BaseColorImage);
            var png = m.Parts[0].BaseColorImage;
            Assert.NotNull(png);
            Assert.Equal(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, png.Take(4));
            Assert.Equal(4, png[19]); Assert.Equal(2, png[23]);      // the picture made in Blender is 4 by 2
        }

        [Fact]
        public void UvIsAsGltfHasIt_VDown()
        {
            var m = Skinned();
            for (int v = 0; v < m.VertexCount; v++)
            {
                Assert.Equal(0.25f * PlaceOf(m, v), m.Uv[v * 2], 3);
                Assert.Equal(0.8f, m.Uv[v * 2 + 1], 3);
            }
        }

        [Theory]
        [InlineData("skinned.glb")]
        [InlineData("static.glb")]
        public void TrianglesGoRoundTheirNormals_AlsoInAMirroredObject(string file)
        {
            var m = Glb.ReadFile(Fixture(file));
            Assert.True(m.HasNormals);
            foreach (var part in m.Parts)
                for (int i = 0; i < part.Triangles.Count; i += 3)
                {
                    var a = P(m, part.Triangles[i]); var b = P(m, part.Triangles[i + 1]); var c = P(m, part.Triangles[i + 2]);
                    float ux = b[0] - a[0], uy = b[1] - a[1], uz = b[2] - a[2], wx = c[0] - a[0], wy = c[1] - a[1], wz = c[2] - a[2];
                    int n = part.Triangles[i] * 3;
                    float dot = (uy * wz - uz * wy) * m.Normals[n] + (uz * wx - ux * wz) * m.Normals[n + 1] + (ux * wy - uy * wx) * m.Normals[n + 2];
                    Assert.True(dot > 0, $"{file} triangle {i / 3}");
                }
        }

        [Fact]
        public void ObjectsWithoutASkin_ArePutWhereTheyStand()
        {
            var m = Static();
            var got = Enumerable.Range(0, m.VertexCount).Select(v => P(m, v)).ToList();
            var want = new[] { FromBlender(0, 0, 0), FromBlender(1, 0, 0), FromBlender(0, 0, 1), FromBlender(0, 0, 2), FromBlender(0, -2, 2), FromBlender(0, 0, 2.5) };
            Assert.Equal(want.Length, got.Count);
            foreach (var w in want) Assert.Contains(got, g => Near(g, w));
            Assert.Equal(m.VertexCount, m.Unweighted);
            Assert.Empty(m.JointNames);
            Assert.Equal(2, m.Parts.Count);
            Assert.Contains(m.Parts, p => p.MaterialName == "Felt");
        }

        [Fact]
        public void MirroredObject_NormalAsBlenderShowsIt()
        {
            var m = Static();
            var felt = m.Parts.Single(p => p.MaterialName == "Felt");
            int v = felt.Triangles[0];
            Assert.True(Near(new[] { m.Normals[v * 3], m.Normals[v * 3 + 1], m.Normals[v * 3 + 2] }, FromBlender(1, 0, 0)));
        }

        // ---- files that must be refused, each with a message and without reading past the end ----

        [Fact]
        public void EveryCutOfAGoodFileIsRefused()
        {
            foreach (var name in new[] { "skinned.glb", "static.glb", "twoskins.glb" })
            {
                var whole = File.ReadAllBytes(Fixture(name));
                for (int n = 0; n < whole.Length; n++)
                    Assert.Throws<GlbException>(() => Glb.Read(whole.Take(n).ToArray()));
            }
        }

        [Fact]
        public void EveryChangedByteEitherReadsOrIsRefused_NothingElse()
        {
            foreach (var name in new[] { "skinned.glb", "static.glb", "twoskins.glb" })
            {
                var whole = File.ReadAllBytes(Fixture(name));
                for (int i = 0; i < whole.Length; i++)
                    foreach (byte x in new byte[] { 0xFF, 0x00, (byte)'9', (byte)(whole[i] ^ 1) })
                    {
                        var bad = (byte[])whole.Clone();
                        bad[i] = x;
                        try
                        {
                            var m = Glb.Read(bad);
                            Assert.True(m.VertexCount * 12L <= whole.Length * 4L);
                            Assert.All(m.Parts.SelectMany(p => p.Triangles), t => Assert.InRange(t, 0, m.VertexCount - 1));
                            Assert.All(m.Joints, j => Assert.True(j == 0 || j < m.JointNames.Count));
                        }
                        catch (GlbException) { }
                    }
            }
        }

        private static byte[] With(string file, string from, string to)
        {
            // Same length, so that the chunk's own length stays right.
            Assert.Equal(from.Length, to.Length);
            var b = File.ReadAllBytes(Fixture(file));
            var text = System.Text.Encoding.Latin1.GetString(b);
            int at = text.IndexOf(from, StringComparison.Ordinal);
            Assert.True(at >= 0, from);
            System.Text.Encoding.Latin1.GetBytes(to).CopyTo(b, at);
            return b;
        }

        [Theory]
        [InlineData("\"type\":\"VEC3\"", "\"type\":\"VEC2\"")]
        [InlineData("\"mesh\":0", "\"mesh\":7")]
        [InlineData("\"POSITION\"", "\"POSITIOM\"")]
        public void AFileThatContradictsItself_IsRefused(string from, string to) =>
            Assert.Throws<GlbException>(() => Glb.Read(With("static.glb", from, to)));

        [Fact]
        public void MoreThanFourBones_IsRefused_AndSaysSo()
        {
            var ex = Assert.Throws<GlbException>(() => Glb.Read(With("skinned.glb", "\"TEXCOORD_0\"", "\"JOINTS_1\"  ")));
            Assert.Contains("four bones", ex.Message);
        }

        [Fact]
        public void AnObjIsNotAGlb() => Assert.Contains("glTF Binary", Assert.Throws<GlbException>(() => Glb.Read(System.Text.Encoding.ASCII.GetBytes("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n"))).Message);
    }
}
