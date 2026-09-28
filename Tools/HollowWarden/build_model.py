"""Run through the official Blender MCP. Builds a new isolated prototype scene.

Never deletes existing data or overwrites a previous production pass. The first pass
uses v001; subsequent executions choose the next free version directory.
"""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector

ROOT = Path('C:/Users/linco/Mugg/Artifacts/HollowWarden')
if any(a.name.startswith('HW_') for a in bpy.data.actions):
    raise RuntimeError('Build in a fresh Blender file: existing HW actions are preserved; do not export unrelated actions.')
version = 1
while (ROOT / f'v{version:03}').exists():
    version += 1
OUT = ROOT / f'v{version:03}'
OUT.mkdir(parents=True)
scene = bpy.data.scenes.new(f'HollowWarden_v{version:03}')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
scene.render.fps = 30
collection = bpy.data.collections.new('HW_Character')
scene.collection.children.link(collection)

# Compact palette: four wood values, stone, moss, dark cavity, warm cut wood.
colors = [(0.25, .16, .09, 1), (.34, .23, .14, 1), (.43, .30, .19, 1),
          (.51, .37, .24, 1), (.39, .40, .36, 1), (.30, .35, .13, 1),
          (.095, .065, .04, 1), (.58, .41, .22, 1)]
palette = bpy.data.images.new('HW_Palette', width=64, height=8, alpha=True)
palette.pixels = [c for y in range(8) for x in range(64) for c in colors[x // 8]]
palette.filepath_raw = str(OUT / 'HW_Palette.png')
palette.file_format = 'PNG'
palette.save()
palette.pack()
wood = bpy.data.materials.new('HW_Palette_Matte')
wood.use_nodes = True
shader = wood.node_tree.nodes.get('Principled BSDF')
shader.inputs['Roughness'].default_value = .95
tex = wood.node_tree.nodes.new('ShaderNodeTexImage')
tex.image = palette
tex.interpolation = 'Closest'
wood.node_tree.links.new(tex.outputs['Color'], shader.inputs['Base Color'])
glow = bpy.data.materials.new('HW_Amber_Emission')
glow.use_nodes = True
gs = glow.node_tree.nodes.get('Principled BSDF')
gs.inputs['Base Color'].default_value = (1, .40, .025, 1)
gs.inputs['Emission Color'].default_value = (1, .25, .006, 1)
gs.inputs['Emission Strength'].default_value = 2
gs.inputs['Roughness'].default_value = .65

verts, faces, swatches, materials, weights = [], [], [], [], []
foot_rings = []

def segment(name, a, b, widths, bone, color=1, sides=5, depth=1, emissive=False):
    """Closed faceted tapered limb, baked at real scale, one rigid bone assignment."""
    a, b = Vector(a), Vector(b)
    direction = (b-a).normalized()
    ref = Vector((0, 1, 0)) if abs(direction.y) < .9 else Vector((1, 0, 0))
    u = direction.cross(ref).normalized()
    v = direction.cross(u).normalized()
    start = len(verts)
    if bone.startswith('Foot.'):
        foot_rings.extend([list(range(start+r*sides, start+(r+1)*sides)) for r in range(3)])
    for t, radius in zip((0, .5, 1), widths):
        center = a.lerp(b, t)
        for i in range(sides):
            angle = 2 * math.pi * i / sides + math.pi/4
            p = center + radius * (u * math.cos(angle) + v * math.sin(angle) * depth)
            verts.append(tuple(p))
            weights.append(bone)
    new_faces = [tuple(start+i for i in reversed(range(sides)))]
    for ring in range(2):
        for i in range(sides):
            j = (i+1) % sides
            new_faces.append((start+ring*sides+i, start+ring*sides+j,
                              start+(ring+1)*sides+j, start+(ring+1)*sides+i))
    new_faces.append(tuple(start+2*sides+i for i in range(sides)))
    for i, face in enumerate(new_faces):
        faces.append(face)
        swatches.append(min(3, color+(1 if i % 5 == 2 else 0)) if color < 4 else color)
        materials.append(1 if emissive else 0)

bones = [
    ('Root', (0,0,0), (0,0,.4), None),
    ('Hips', (0,0,1.35), (0,0,1.85), 'Root'),
    ('Chest', (0,0,1.85), (0,0,2.9), 'Hips'),
    ('Head', (0,0,2.95), (0,0,3.4), 'Chest'),
    ('UpperArm.R', (-1,0,2.85), (-1.38,-.06,2), 'Chest'),
    ('Forearm.R', (-1.38,-.06,2), (-1.62,-.12,1.25), 'UpperArm.R'),
    ('Hand.R', (-1.62,-.12,1.25), (-1.7,-.18,.6), 'Forearm.R'),
    ('UpperArm.L', (1,0,2.85), (1.3,-.02,2.1), 'Chest'),
    ('Forearm.L', (1.3,-.02,2.1), (1.5,-.1,1.48), 'UpperArm.L'),
    ('Hand.L', (1.5,-.1,1.48), (1.5,-.12,1.03), 'Forearm.L'),
    ('Thigh.R', (-.48,0,1.5), (-.64,0,.82), 'Hips'),
    ('Shin.R', (-.64,0,.82), (-.69,0,.25), 'Thigh.R'),
    ('Foot.R', (-.69,0,.25), (-.69,-.48,.15), 'Shin.R'),
    ('Thigh.L', (.48,0,1.5), (.64,0,.82), 'Hips'),
    ('Shin.L', (.64,0,.82), (.69,0,.25), 'Thigh.L'),
    ('Foot.L', (.69,0,.25), (.69,-.48,.15), 'Shin.L'),
    ('Plate.R', (-.55,-.3,2.7), (-.24,-.5,1.8), 'Chest'),
    ('Plate.L', (.55,-.3,2.7), (.24,-.5,1.8), 'Chest'),
    ('Heart', (0,-.30,2.25), (0,-.30,2.65), 'Chest'),
    ('Stone.R', (-1,.03,2.95), (-1.17,.03,3.8), 'Chest'),
    ('Stone.L', (1,.03,2.95), (1.12,.03,3.6), 'Chest'),
]

# Main mass stays behind the core, leaving a real dark recess around it.
segment('Pelvis', (0,.05,1.32), (0,.08,1.9), (.38,.53,.43), 'Hips', 0, 6, .8)
segment('Back', (0,.28,1.85), (0,.3,3), (.48,.77,.68), 'Chest', 0, 6, .57)
segment('Cavity', (0,-.04,1.95), (0,-.04,2.98), (.34,.55,.40), 'Chest', 6, 6, .40)
for side, sign in [('R',-1), ('L',1)]:
    segment('Rib'+side, (sign*.4,-.15,1.82), (sign*.73,-.12,3.1),
            (.20,.36,.25), 'Chest', 1, 5, .75)
    segment('Plate'+side, (sign*.18,-.49,1.73), (sign*.57,-.52,2.94),
            (.045,.25,.24), 'Plate.'+side, 2, 4, .55)
    segment('Collar'+side, (sign*.35,.02,3.4), (sign*.94,-.02,2.97),
            (.12,.22,.30), 'Chest', 2, 5, .8)
segment('Head', (0,-.1,2.99), (0,-.1,3.4), (.18,.23,.16), 'Head', 0, 5, .8)
for x in [-.08,.08]:
    segment('Eye', (x,-.294,3.15), (x,-.294,3.29), (.018,.025,.018), 'Head', 7, 4, .5, True)
segment('Heart', (0,-.51,2.14), (0,-.51,2.77), (.018,.20,.018), 'Heart', 7, 5, .60, True)

for side, sign in [('R',-1), ('L',1)]:
    big = side == 'R'
    segment('Shoulder', (sign*.87,0,2.85), (sign*1.08,.03,3.1),
            (.43,.47,.33), 'UpperArm.'+side, 1, 5, .85)
    a = next(b for b in bones if b[0] == 'UpperArm.'+side)
    segment('Upper', a[1], a[2], (.33,.37,.25) if big else (.28,.29,.20), a[0], 1, 5)
    a = next(b for b in bones if b[0] == 'Forearm.'+side)
    segment('Forearm', a[1], a[2], (.26,.39,.39) if big else (.22,.26,.17), a[0], 2, 5)
    if big:
        segment('RootFist', (-1.6,-.1,1.45), (-1.74,-.20,.25), (.40,.60,.34), 'Hand.R', 1, 7, .9)
        segment('FistMoss', (-1.64,-.12,1.35), (-1.58,-.08,1.52), (.37,.35,.26), 'Hand.R', 5, 5)
    else:
        segment('Palm', (1.5,-.10,1.55), (1.53,-.14,1.18), (.18,.23,.18), 'Hand.L', 1, 5)
        for dx, dy, dz in [(-.18,-.08,.12), (0,-.10,0), (.18,.015,.06)]:
            segment('Finger', (1.53+dx,-.15+dy,1.23), (1.55+dx,-.24+dy,.76+dz),
                    (.075,.09,.025), 'Hand.L', 2, 4)
    for prefix, widths in [('Thigh',(.30,.39,.26)),('Shin',(.24,.29,.23))]:
        b = next(b for b in bones if b[0] == prefix+'.'+side)
        segment(prefix, b[1], b[2], widths, b[0], 1, 5, 1.05)
    segment('Heel', (sign*.69,.1,.24), (sign*.69,-.3,.22), (.23,.32,.26), 'Foot.'+side, 1, 5, .7)
    for dx, length in [(-.22,.40),(0,.60),(.22,.42)]:
        segment('Toe', (sign*.69+dx,-.10,.18), (sign*.69+dx,-length,.09),
                (.13,.14,.055), 'Foot.'+side, 2, 4, .65)
    h = 3.95 if big else 3.73
    segment('Stone', (sign*.95,.12,3.10), (sign*1.13,.12,h), (.27,.28,.15), 'Stone.'+side, 4, 5, .60)
    segment('Moss', (sign*.98,.10,3.06), (sign*1.0,.10,3.19), (.36,.35,.27), 'Stone.'+side, 5, 5, .8)

# Rest pose feet touch Z=0 exactly. Keep the locomotion root at the origin.
ground_offset = -min(v[2] for v in verts)
verts = [(x,y,z+ground_offset) for x,y,z in verts]
for ring in foot_rings:
    # A broad sole along every ring, including every toe tip, not a single heel point.
    for index in sorted(ring, key=lambda i: verts[i][2])[:2]:
        x,y,_ = verts[index]
        verts[index] = (x,y,0)
bones = [(name, tuple(Vector(head)+Vector((0,0,ground_offset))) if name != 'Root' else head,
          tuple(Vector(tail)+Vector((0,0,ground_offset))) if name != 'Root' else tail, parent)
         for name,head,tail,parent in bones]
mesh = bpy.data.meshes.new('HW_BodyMesh')
mesh.from_pydata(verts, [], faces)
mesh.update()
body = bpy.data.objects.new('HW_Body', mesh)
collection.objects.link(body)
mesh.materials.append(wood)
mesh.materials.append(glow)
uv = mesh.uv_layers.new(name='PaletteUV')
for face, swatch, material in zip(mesh.polygons, swatches, materials):
    face.material_index = material
    face.use_smooth = False
    # Tiny nondegenerate islands entirely inside each solid palette cell.
    count = len(face.loop_indices)
    for i, loop in enumerate(face.loop_indices):
        theta = 2*math.pi*i/count
        uv.data[loop].uv = ((swatch+.5)/8 + .025*math.cos(theta), .5+.2*math.sin(theta))

arm = bpy.data.armatures.new('HW_Armature')
rig = bpy.data.objects.new('HW_Rig', arm)
collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name, head, tail, parent in bones:
    bone = arm.edit_bones.new(name)
    bone.head, bone.tail = head, tail
    if parent:
        bone.parent = arm.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
for name, *_ in bones:
    group = body.vertex_groups.new(name=name)
    indices = [i for i,bone in enumerate(weights) if bone == name]
    if indices:
        group.add(indices, 1, 'REPLACE')
body.parent = rig
modifier = body.modifiers.new('HW_Skin', 'ARMATURE')
modifier.object = rig
rig.show_in_front = True

def pose(rotations=None, locations=None):
    for bone in rig.pose.bones:
        bone.rotation_mode = 'XYZ'
        bone.rotation_euler = (0,0,0)
        bone.location = (0,0,0)
        bone.scale = (1,1,1)
    for name, rotation in (rotations or {}).items():
        rig.pose.bones[name].rotation_euler = tuple(math.radians(a) for a in rotation)
    for name, location in (locations or {}).items():
        rig.pose.bones[name].location = location

clips = []
def action(name, duration, keys, loop=False, markers=None):
    act = bpy.data.actions.new('HW_'+name)
    act.use_fake_user = True
    rig.animation_data_create().action = act
    for t, rotations, locations in keys:
        pose(rotations, locations)
        frame = 1 + round(t*30)
        for bone in rig.pose.bones:
            bone.keyframe_insert('rotation_euler', frame=frame, group=bone.name)
            bone.keyframe_insert('location', frame=frame, group=bone.name)
    for label, t in (markers or {}).items():
        act.pose_markers.new(label).frame = 1+round(t*30)
    act['loop'] = loop
    act['duration_seconds'] = duration
    clips.append({'name':'HW_'+name, 'duration':duration, 'loop':loop, 'markers':markers or {}})
    return act

idle = action('Idle',2,[(0,{},{}),(1,{'Chest':(2,0,0),'Head':(-2,0,0)},{}),(2,{},{})],True)
action('Walk',1.2,[(0,{'Thigh.R':(18,0,0),'Thigh.L':(-18,0,0),'UpperArm.R':(-10,0,0),'UpperArm.L':(12,0,0)},{}),
    (.3,{'Shin.R':(-15,0,0),'Chest':(0,0,3)}, {'Hips':(0,.035,0)}),
    (.6,{'Thigh.R':(-18,0,0),'Thigh.L':(18,0,0),'UpperArm.R':(10,0,0),'UpperArm.L':(-12,0,0)},{}),
    (.9,{'Shin.L':(-15,0,0),'Chest':(0,0,-3)},{'Hips':(0,.035,0)}),
    (1.2,{'Thigh.R':(18,0,0),'Thigh.L':(-18,0,0),'UpperArm.R':(-10,0,0),'UpperArm.L':(12,0,0)}, {})],True)
action('Spawn',2,[(0,{'Chest':(25,0,0),'Head':(20,0,0)},{'Hips':(0,-.25,0)}),
    (1.4,{'Chest':(-8,0,0),'UpperArm.R':(-20,0,-8)},{}),(2,{},{})])
action('Rootbreaker',2.4,[(0,{},{}),(.6,{'UpperArm.R':(-105,0,-15),'Forearm.R':(-20,0,0),'Chest':(-12,0,-8)},{}),
    (.9,{'UpperArm.R':(-115,0,-15),'Chest':(-15,0,-8)},{}),
    (1,{'UpperArm.R':(-32,0,0),'Chest':(25,0,8)},{}),
    (1.35,{'UpperArm.R':(-32,0,0),'Chest':(25,0,8)},{}),(2.4,{},{})],markers={'Lock':.6,'Impact':1})
action('HarvestSweep',2.2,[(0,{},{}),(.6,{'Chest':(0,0,-38),'UpperArm.R':(-50,0,-45)},{}),
    (.9,{'Chest':(0,0,38),'UpperArm.R':(-60,0,38)},{}),
    (1.2,{'Chest':(5,0,38),'UpperArm.R':(-45,0,35)},{}),(2.2,{},{})],markers={'Lock':.6,'Impact':.9})
root_pose = {'Chest':(12,0,0),'Head':(-10,0,0),'UpperArm.R':(-12,0,0),'UpperArm.L':(-20,0,0)}
action('RootPlant',1.4,[(0,{},{}),(.8,root_pose,{'Hips':(0,-.1,0)}),(1.4,root_pose,{'Hips':(0,-.1,0)})],markers={'RootsAppear':1.4})
action('Rooted',2,[(0,root_pose,{'Hips':(0,-.1,0)}),(1,{**root_pose,'Head':(-6,0,0)},{'Hips':(0,-.1,0)}),(2,root_pose,{'Hips':(0,-.1,0)})],True)
open_plates = {'Plate.R':(0,0,35),'Plate.L':(0,0,-35)}
exposed_pose = {**open_plates,'Chest':(-12,0,0),'Head':(15,0,0),'UpperArm.R':(8,0,-10),'UpperArm.L':(8,0,10)}
action('Stagger',1,[(0,root_pose,{'Hips':(0,-.1,0)}),(.3,{**exposed_pose,'Chest':(-20,0,0)},{}),(1,exposed_pose,{})])
action('Exposed',2,[(0,exposed_pose,{}),(1,{**exposed_pose,'Head':(18,0,0)},{}),(2,exposed_pose,{})],True)
action('Recover',1,[(0,exposed_pose,{}),(1,{},{})])
action('PhaseBreak',2,[(0,{},{}),(.6,{'Chest':(15,0,0),'Head':(15,0,0)},{}),
    (1,{**open_plates,'Chest':(-15,0,0),'UpperArm.R':(-30,0,-20),'UpperArm.L':(-30,0,20)},{}),(2,open_plates,{})],markers={'ChestCrack':1})
action('Death',3,[(0,{},{}),(.5,{'Chest':(18,0,0),'Head':(25,0,0)},{}),
    (1.4,{'Chest':(60,0,0),'Thigh.R':(-35,0,0),'Thigh.L':(-35,0,0),'Shin.R':(55,0,0),'Shin.L':(55,0,0)}, {'Hips':(0,-.6,0)}),
    (2.1,{'Root':(75,0,0),'Chest':(15,0,0)}, {'Root':(0,.52,0)}),
    (3,{'Root':(75,0,0),'Chest':(15,0,0)}, {'Root':(0,.52,0)})])

rig.animation_data.action = idle
pose()
scene.frame_start, scene.frame_end = 1, 61
scene.frame_set(1)
bpy.context.view_layer.update()
mesh.calc_loop_triangles()
manifest = {'stage':'prototype; Unity import and gameplay unverified', 'version':version,
    'triangles':len(mesh.loop_triangles),'vertices':len(mesh.vertices), 'bones':len(bones),
    'materials':2, 'height_m':body.dimensions.z, 'clips':clips,
    'forward':'-Y in Blender; exported -Z forward / Y up', 'root_motion':False,
    'phase_2_note':'Unity presentation should maintain open chest plates as an additive/masked layer after PhaseBreak.'}
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2))

# Selected-character-only FBX. All actions are intentionally baked, no leaf bones.
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(OUT/'HollowWarden.fbx'), use_selection=True,
    object_types={'MESH','ARMATURE'}, axis_forward='-Z', axis_up='Y',
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', add_leaf_bones=False, bake_anim=True,
    bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_simplify_factor=0, path_mode='COPY', embed_textures=True)

# A neutral preview scene; these objects are never part of the FBX export.
world = bpy.data.worlds.new('HW_PreviewWorld')
world.use_nodes = True
world.node_tree.nodes['Background'].inputs[0].default_value = (.35,.36,.38,1)
world.node_tree.nodes['Background'].inputs[1].default_value = .45
scene.world = world
ground = bpy.data.materials.new('HW_PreviewGround')
ground.diffuse_color = (.28,.29,.30,1)
bpy.ops.mesh.primitive_plane_add(size=200, location=(0,0,-.005))
plane = bpy.context.object
plane.name = 'HW_PreviewGround'
plane.data.materials.append(ground)
for name, position, energy, size in [('Key',(-4,-6,8),1200,5),('Fill',(4,-2,5),700,4),('Rim',(1,4,6),1000,3)]:
    data = bpy.data.lights.new('HW_'+name,'AREA')
    data.energy, data.shape, data.size = energy, 'DISK', size
    light = bpy.data.objects.new('HW_'+name,data)
    scene.collection.objects.link(light)
    light.location = position
    light.rotation_euler = (Vector((0,0,2))-light.location).to_track_quat('-Z','Y').to_euler()
camera_data = bpy.data.cameras.new('HW_PreviewCamera')
camera = bpy.data.objects.new('HW_PreviewCamera',camera_data)
scene.collection.objects.link(camera)
camera.location = (6,-10,5)
camera.rotation_euler = (Vector((0,0,2))-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.type, camera_data.ortho_scale = 'ORTHO', 5.4
scene.camera = camera
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.render.resolution_x = 900
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = str(OUT/'preview.png')
scene.view_settings.view_transform = 'AgX'
rig.animation_data.action = idle
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'HollowWarden.blend'))
result = {'output':str(OUT), **manifest}
