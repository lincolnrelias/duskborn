"""Create v008 through official Blender MCP; keep the previous asset intact."""
import bpy, bmesh, math, json, shutil
from pathlib import Path
from mathutils import Vector

ROOT = Path('C:/Users/linco/Mugg')
OUT = ROOT / 'Artifacts/Bramblekin/v008'
assert not (OUT / 'Bramblekin.blend').exists(), 'Preserve the saved source.'
OUT.mkdir(parents=True, exist_ok=True)
source = bpy.data.scenes['Bramblekin_v007']
scene = bpy.data.scenes.new('Bramblekin_v008')
bpy.context.window.scene = scene
scene.world = source.world.copy()
meshes = []
for old in source.objects:
    obj = old.copy()
    obj.data = old.data.copy()
    scene.collection.objects.link(obj)
    if old == source.camera: scene.camera = obj
    if obj.type == 'MESH': meshes.append(obj)
arm = next(o for o in meshes if o.get('export_name') == 'Arm.R')
old = arm.data
skin = [p for p in old.polygons if old.uv_layers.active.data[p.loop_indices[0]].uv.x < .3]
indices = sorted({i for p in skin for i in p.vertices})
mapping = {v:i for i,v in enumerate(indices)}
mesh = bpy.data.meshes.new('BK_RightArm_NoWeapon')
mesh.from_pydata([old.vertices[i].co for i in indices], [], [[mapping[i] for i in p.vertices] for p in skin])
mesh.materials.append(old.materials[0])
uv = mesh.uv_layers.new(name='PaletteUV')
for p, original in zip(mesh.polygons, skin):
    for a,b in zip(p.loop_indices, original.loop_indices): uv.data[a].uv = old.uv_layers.active.data[b].uv
arm.data = mesh

# Single continuous, slightly bent wooden form. Pivot is inside the fist.
grip = Vector((.43, -.12, .49))
axis = Vector((.20, -.53, .40)).normalized()
side = axis.cross(Vector((1,0,0))).normalized()
across = axis.cross(side).normalized()
rings = [(-.10,.021),(-.075,.033),(.0,.034),(.14,.043),(.30,.064),
         (.48,.095),(.66,.135),(.81,.155),(.91,.143),(.98,.108),(1.02,.055)]
verts, faces = [], []
N = 12
for distance, radius in rings:
    center = axis * distance + side * (.018 * (max(distance,0) ** 2))
    for j in range(N):
        angle = math.tau*j/N
        r = radius*(1+.055*math.sin(j*2.1)+.025*math.cos(j*3.2+distance*4))
        verts.append(center + side*(math.cos(angle)*r) + across*(math.sin(angle)*r*.90))
for i in range(len(rings)-1):
    for j in range(N): faces.append((i*N+j,i*N+(j+1)%N,(i+1)*N+(j+1)%N,(i+1)*N+j))
faces.extend([tuple(reversed(range(N))),tuple((len(rings)-1)*N+j for j in range(N))])
wood = bpy.data.meshes.new('BK_CrudeClub')
wood.from_pydata(verts,[],faces)
wood.materials.append(old.materials[0])
uv = wood.uv_layers.new(name='PaletteUV')
for p in wood.polygons:
    color = 3 if p.index % N in (0,1,5,9) else 4
    for loop in p.loop_indices: uv.data[loop].uv = ((color+.5)/10,.5)
club = bpy.data.objects.new('CrudeClub_v008', wood)
club['export_name'] = 'Club'
scene.collection.objects.link(club)
club.location = grip
meshes.append(club)
for obj in meshes:
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    assert all(e.is_manifold for e in bm.edges), obj.name
    bm.to_mesh(obj.data); bm.free()
    obj.data.calc_loop_triangles()
    assert all(t.area > 1e-10 for t in obj.data.loop_triangles), obj.name
bpy.ops.object.select_all(action='DESELECT')
renamed = []
for obj in meshes:
    desired = obj['export_name']
    occupied = bpy.data.objects.get(desired)
    if occupied and occupied != obj:
        renamed.append((occupied,occupied.name)); occupied.name = 'Preserved_v008_' + desired
    obj.name = desired; obj.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=str(OUT/'Bramblekin.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
for obj in meshes: obj.name = 'Crude_v008_' + obj['export_name']
for obj,name in renamed: obj.name = name
shutil.copyfile(ROOT/'Artifacts/Bramblekin/v007/BK_Palette.png',OUT/'BK_Palette.png')
scene.render.engine = 'CYCLES'; scene.cycles.samples = 24
scene.camera.data.ortho_scale = 2.65
scene.render.resolution_x = 768; scene.render.resolution_y = 768
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = str(OUT/'preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Bramblekin.blend'))
bpy.ops.render.render(write_still=True)
result = {'parts':len(meshes),'triangles':sum(len(o.data.loop_triangles) for o in meshes),
          'club_triangles':len(wood.loop_triangles),'club':'Single crude tapered wood club; separate hand-pivot mesh for death detachment.',
          'length_m':1.12,'closed_manifold':True}
(OUT/'manifest.json').write_text(json.dumps(result,indent=2))
