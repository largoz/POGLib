# The GLB files GlbTests reads, written by Blender's own glTF exporter (blender -b -P make_glb.py): the values the test
# expects are planted here, in Blender's axes (Z up, facing -Y, metres), and repeated in GlbTests.cs.
import bpy, math, os
from mathutils import Matrix, Vector
HERE = os.path.dirname(os.path.abspath(__file__))

def clear(): bpy.ops.wm.read_factory_settings(use_empty=True)

def material(name, rgba, metallic, rough, emit=None, image=None):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = rgba
    b.inputs["Metallic"].default_value = metallic
    b.inputs["Roughness"].default_value = rough
    if emit:
        b.inputs["Emission Color"].default_value = (*emit, 1); b.inputs["Emission Strength"].default_value = 3
    if image:
        t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = image
        m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
    return m

def export(name, **kw):
    bpy.ops.export_scene.gltf(filepath=os.path.join(HERE, name), export_format="GLB", export_yup=True, export_animations=False, **kw)

def link(o): bpy.context.scene.collection.objects.link(o); return o

# skinned.glb: an armature that is moved, turned and scaled; a mesh weighted to three of its bones, two materials, one picture.
clear()
PLACES = [(0.10, -0.05, 0.40), (-0.10, -0.05, 0.40), (0.10, -0.05, 0.60), (-0.10, -0.05, 0.60), (0.0, 0.07, 0.75)]
arm = link(bpy.data.objects.new("Armature", bpy.data.armatures.new("Armature")))
arm.matrix_world = Matrix.Translation((0.3, -0.2, 0.1)) @ Matrix.Rotation(math.radians(30), 4, "Z") @ Matrix.Scale(0.5, 4)
bpy.context.view_layer.update()
inv = arm.matrix_world.inverted()
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="EDIT")
for bone, head in (("mixamorig:Hips", (0, 0.02, 0.35)), ("mixamorig:Spine", (0, 0.02, 0.45)), ("mixamorig:Head", (0, 0.01, 0.70)), ("mixamorig:LeftArm", (0.1, 0.02, 0.58))):
    b = arm.data.edit_bones.new(bone); b.head = inv @ Vector(head); b.tail = inv @ (Vector(head) + Vector((0, 0, 0.05)))
bpy.ops.object.mode_set(mode="OBJECT")
img = bpy.data.images.new("cloth", 4, 2); img.generated_color = (1, 0, 0, 1); img.pack()
me = bpy.data.meshes.new("coat")
me.from_pydata([inv @ Vector(p) for p in PLACES], [], [(0, 2, 1), (1, 2, 3), (2, 4, 3)])
me.materials.append(material("Cloth", (0.25, 0.5, 0.75, 1), 0.2, 0.9, image=img))
me.materials.append(material("Brass", (0.8, 0.6, 0.1, 1), 0.7, 0.3, emit=(0.5, 0.25, 0.0)))
for poly, mi in zip(me.polygons, (0, 0, 1)): poly.material_index = mi
uv = me.uv_layers.new(name="UVMap")
for loop in me.loops: uv.data[loop.index].uv = (0.25 * loop.vertex_index, 0.2)   # Blender's v goes up, glTF's down
o = link(bpy.data.objects.new("coat", me)); o.parent = arm
W = {0: {"mixamorig:Hips": 1.0}, 1: {"mixamorig:Hips": 0.5, "mixamorig:Spine": 0.5}, 2: {"mixamorig:Spine": 0.75, "mixamorig:LeftArm": 0.25},
     3: {"mixamorig:Spine": 1.0}, 4: {"mixamorig:Head": 0.6, "mixamorig:Spine": 0.4}}
for v, ws in W.items():
    for bone, w in ws.items():
        g = o.vertex_groups.get(bone) or o.vertex_groups.new(name=bone); g.add([v], w, "REPLACE")
o.modifiers.new("Armature", "ARMATURE").object = arm
export("skinned.glb")

# static.glb: two objects, one of them turned, stretched and mirrored; no skin; one without a material.
clear()
def tri(name, pts, mat=None):
    me = bpy.data.meshes.new(name); me.from_pydata(pts, [], [(0, 1, 2)])
    if mat: me.materials.append(mat)
    return link(bpy.data.objects.new(name, me))
a = tri("plain", [(0, 0, 0), (1, 0, 0), (0, 0, 1)])                                        # faces -Y
b = tri("mirrored", [(0, 0, 0), (1, 0, 0), (0, 0, 1)], material("Felt", (0.1, 0.1, 0.1, 1), 0.0, 1.0))
b.matrix_world = Matrix.Translation((0, 0, 2)) @ Matrix.Rotation(math.radians(90), 4, "Z") @ Matrix.Diagonal((-2, 1, 0.5, 1))
export("static.glb")

# twoskins.glb: two meshes on two armatures; Hips is the first bone of one and the second of the other.
clear()
for k, bones in enumerate((("mixamorig:Hips", "mixamorig:Spine"), ("mixamorig:Arm", "mixamorig:Hips"))):
    arm = link(bpy.data.objects.new(f"Armature{k}", bpy.data.armatures.new(f"Armature{k}")))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for i, bone in enumerate(bones):
        b = arm.data.edit_bones.new(bone); b.head = (0, 0, i); b.tail = (0, 0, i + .5)
    bpy.ops.object.mode_set(mode="OBJECT")
    o = tri(f"part{k}", [(k, 0, 0), (k + 1, 0, 0), (k, 0, 1)]); o.parent = arm
    o.vertex_groups.new(name="mixamorig:Hips").add([0, 1, 2], 1.0, "REPLACE")     # every vertex to Hips, in both
    o.modifiers.new("Armature", "ARMATURE").object = arm
export("twoskins.glb")
print("FIXTURES WRITTEN")
