# Test clothes for Cosmetics.cs, one part per slot: blender -b -P make_parts.py -- <out dir>
# Cut to the goblin: the body's own surface where the garment covers it, pushed outwards, with the body's own bone weights. The body is read from
# files on this machine (tests/RuntimeDriver/RigDump.cs, /run/media/tim/Games/pog-tools/export_body.py); it is the game's, so neither it nor what is
# made here goes into a repository or a package. (The first version was plain tubes and boxes; the user took them for the mod's clothes.)
import bpy, bmesh, json, math, os, sys, zlib, struct
from mathutils import Vector
OUT = sys.argv[sys.argv.index("--") + 1]; os.makedirs(OUT, exist_ok=True)
R = "/run/media/tim/Games/pog-runs/goblin-3/"
body = json.load(open(R + "body.json"))["CH_Goblin_Default#1"]
NAMES = [m for m in json.load(open(R + "rig.json"))["meshes"] if m["mesh"] == "CH_Goblin_Default"][0]["bones"]
V = [Vector((-v[0] * 100, v[1] * 100, v[2] * 100)) for v in body["v"]]        # Blender axes, metres
T = [t for t in body["t"]]
W = [[(NAMES[b], w) for b, w in zip(bi, ws) if w > 1e-3 and NAMES[b].startswith("mixamorig:")] for bi, ws in zip(body["bi"], body["w"])]
MAIN = [max(w, key=lambda t: t[1])[0][10:] if w else "" for w in W]
M = "mixamorig:"

def clear(): bpy.ops.wm.read_factory_settings(use_empty=True)
def mat(name, rgb, cull=True, image=None):
    m = bpy.data.materials.new(name); m.use_nodes = True; b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1); b.inputs["Roughness"].default_value = .9; m.use_backface_culling = cull
    if image:
        t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = image; t.interpolation = "Closest"
        m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
    return m
def obj(name, verts, faces, material, weights=None, uvs=None, smooth=True):
    me = bpy.data.meshes.new(name); me.from_pydata([Vector(v) for v in verts], [], faces); me.update(); me.materials.append(material)
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    if smooth:
        for p in me.polygons: p.use_smooth = True
        me.set_sharp_from_angle(angle=math.radians(55))
    if uvs:
        uv = me.uv_layers.new(name="UVMap")
        for l in me.loops: uv.data[l.index].uv = uvs[l.vertex_index]
    if weights:
        bones = sorted({b for ws in weights for b, _ in ws})
        arm = bpy.data.objects.new("Goblin", bpy.data.armatures.new("Goblin")); bpy.context.scene.collection.objects.link(arm)
        bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode="EDIT")
        for i, b in enumerate(bones): e = arm.data.edit_bones.new(b); e.head = (0, 0, .1 * i); e.tail = (0, 0, .1 * i + .05)
        bpy.ops.object.mode_set(mode="OBJECT")
        for i, ws in enumerate(weights):
            for b, w in ws: (o.vertex_groups.get(b) or o.vertex_groups.new(name=b)).add([i], w, "REPLACE")
        o.parent = arm; o.modifiers.new("Armature", "ARMATURE").object = arm
    return o
def export(name): bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, name + ".glb"), export_format="GLB", export_yup=True, export_animations=False)
def png(name, rgb, size=64):
    row = b"\0" + bytes(rgb + (255,)) * size
    raw = zlib.compress(row * size)
    def chunk(t, d): return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d))
    open(os.path.join(OUT, name + ".png"), "wb").write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)) + chunk(b"IDAT", raw) + chunk(b"IEND", b""))

# one vertex per place, and its normal over all its faces
place = {}; weld = []
for v in V: weld.append(place.setdefault(tuple(round(c, 5) for c in v), len(place)))
first = {}
for i, k in enumerate(weld): first.setdefault(k, i)
normal = [Vector() for _ in place]
for t in T:
    a, b, c = (V[i] for i in t); n = (b - a).cross(c - a)
    for i in t: normal[weld[i]] += n
vol = sum(V[t[0]].cross(V[t[1]]).dot(V[t[2]]) for t in T)
SIGN = 1 if vol > 0 else -1

def garment(covers, out):
    """The body's faces all of whose corners are covered, pushed out by `out` metres (a number, or a function of the place)."""
    vs, fs, ws, ix = [], [], [], {}
    for t in T:
        k = [weld[i] for i in t]
        if len(set(k)) < 3 or not all(covers(V[first[q]], MAIN[first[q]]) for q in k): continue
        f = []
        for q in k:
            if q not in ix:
                ix[q] = len(vs); p = V[first[q]]; n = normal[q] * SIGN
                vs.append(p + (n.normalized() if n.length > 1e-12 else Vector()) * (out(p) if callable(out) else out)); ws.append(W[first[q]])
            f.append(ix[q])
        fs.append(tuple(f) if SIGN > 0 else tuple(reversed(f)))
    return vs, fs, ws

def make(name, rgb, covers, out, colour=None, weights=True, cull=False):
    clear(); vs, fs, ws = garment(covers, out); obj(name, vs, fs, mat(colour or name, rgb, cull), ws if weights else None); export(name)
    png(name, tuple(int(255 * c ** (1 / 2.2)) for c in rgb)); print(name, len(vs), "verts", len(fs), "faces")

torso = ("Spine", "Spine1", "Spine2", "LeftShoulder", "RightShoulder")
make("vest", (.02, .05, .8), lambda p, b: .425 < p.z < .615 and abs(p.x) < .125 and (b in torso or b.endswith("Arm") or b == "Hips"), .010, "Blue")
make("trousers", (.9, .3, .01), lambda p, b: .095 < p.z < .425 and (b in ("Hips", "Spine") or b.endswith("UpLeg") or b.endswith("Leg")), .009, "Orange")
make("boots", (.3, .02, .5), lambda p, b: p.z < .135 and (b.endswith("Foot") or b.endswith("ToeBase") or b.endswith("Toe_End") or b.endswith("Leg")), .009, "Purple")
make("cap", (.5, .5, .5), lambda p, b: p.z > .838 and abs(p.x) < .14, .016, "Grey", weights=False)
make("hood", (.02, .6, .02), lambda p, b: p.z > .70 and abs(p.x) < .145 and p.y > -.03, .014, "Green", weights=False)

# the belt: measured round the waist
clear()
def ring(z, room, n=20):
    P = []
    for t in T:
        p = [V[i] for i in t]
        for a, b in ((0, 1), (1, 2), (2, 0)):
            d0, d1 = p[a].z - z, p[b].z - z
            if d0 * d1 < 0: P.append(p[a] + (p[b] - p[a]) * (d0 / (d0 - d1)))
    P = [p for p in P if abs(p.x) < .16]; cy = (min(p.y for p in P) + max(p.y for p in P)) / 2; out = []
    for i in range(n):
        a = 2 * math.pi * i / n; d = Vector((math.cos(a), math.sin(a), 0))
        out.append((Vector((0, cy, z)) + d * (max((p - Vector((0, cy, z))).dot(d) for p in P) + room)))
    return out
L = [ring(z, r) for z, r in ((.392, .011), (.392, .019), (.424, .019), (.424, .011))]; n = len(L[0]); VS = [v for l in L for v in l]
FS = [(k * n + i, k * n + (i + 1) % n, ((k + 1) % 4) * n + (i + 1) % n, ((k + 1) % 4) * n + i) for k in range(4) for i in range(n)]
o = obj("belt", VS, FS, mat("Yellow", (.9, .8, .02)), [[(M + "Hips", .5), (M + "Spine", .5)]] * len(VS))
bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(o.data); bm.free(); export("belt"); png("belt", (230, 210, 20))

# horns: curved, out of the skull
clear(); VS, FS = [], []
for s in (-1, 1):
    base = len(VS); path = [(.070, .00, .885, .024), (.098, .00, .925, .020), (.118, .005, .965, .015), (.122, .012, 1.005, .009), (.114, .02, 1.04, .002)]
    for x, y, z, r in path: VS += [(s * x + r * math.cos(2 * math.pi * i / 12), y + r * math.sin(2 * math.pi * i / 12), z) for i in range(12)]
    FS += [(base + k * 12 + i, base + k * 12 + (i + 1) % 12, base + (k + 1) * 12 + (i + 1) % 12, base + (k + 1) * 12 + i) for k in range(4) for i in range(12)]
obj("horns", VS, FS, mat("Red", (.8, .02, .02), cull=False)); export("horns"); png("horns", (200, 20, 20))

# a mane down the back: the back's own line
clear(); VS, WS = [], []
for z, bone in ((.90, "Head"), (.84, "Head"), (.77, "Head"), (.70, "Head"), (.62, "Neck"), (.56, "Spine2"), (.50, "Spine2"), (.45, "Spine1"), (.40, "Spine")):
    near = [p.y for p in V if abs(p.z - z) < .03 and abs(p.x) < .05]; y = max(near) if near else .08
    for x in (-.035, 0, .035): VS.append((x, y + (.022 if x == 0 else .010), z)); WS.append([(M + bone, 1)])
FS = [(3 * k + j + 1, 3 * k + j, 3 * (k + 1) + j, 3 * (k + 1) + j + 1) for k in range(8) for j in range(2)]
obj("mane", VS, FS, mat("White", (.9, .9, .9), cull=False), WS); export("mane"); png("mane", (230, 230, 230))

# A little sign over the head, seen from both sides, with a picture that shows which way round it is:
# as Blender's front view has it, the upper row is yellow, magenta, cyan, black, the lower one red, green, blue, white.
clear()
img = bpy.data.images.new("board", 4, 2); px = []
for c in ((1, 0, 0), (0, 1, 0), (0, 0, 1), (1, 1, 1), (1, 1, 0), (1, 0, 1), (0, 1, 1), (0, 0, 0)): px += [*c, 1]
img.pixels = px; img.pack()
obj("board", [(-.08, 0, 1.08), (.08, 0, 1.08), (.08, 0, 1.16), (-.08, 0, 1.16)], [(0, 1, 2, 3)], mat("Picture", (1, 1, 1), cull=False, image=img), uvs=[(0, 0), (1, 0), (1, 1), (0, 1)], smooth=False)
export("board"); png("board", (255, 255, 255))

# weighted to a bone the game does not find
clear(); vs, fs, _ = garment(lambda p, b: p.z < .74 and p.y < -.15, .006)
obj("mouth", vs, fs, mat("Pink", (.9, .2, .4)), [[("UpperMouth", 1)]] * len(vs)); export("mouth"); png("mouth", (230, 90, 140))
print("PARTS WRITTEN")
