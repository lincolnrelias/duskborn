"""Replace the small integrated cudgel with a readable knotted oak club through Blender MCP."""
import bpy, bmesh, math, json, ast
from pathlib import Path
from mathutils import Vector
OUT=Path('C:/Users/linco/Mugg/Artifacts/Bramblekin/v005')
if (OUT/'Bramblekin.blend').exists(): raise RuntimeError('Preserve the existing v005 source.')
OUT.mkdir(parents=True,exist_ok=True)
source=bpy.data.scenes['Bramblekin_v003']
scene=bpy.data.scenes.new('Bramblekin_v005'); bpy.context.window.scene=scene
scene.world=source.world.copy(); meshes=[]
for old in source.objects:
    obj=old.copy(); obj.data=old.data.copy(); scene.collection.objects.link(obj)
    if old==source.camera: scene.camera=obj
    if obj.type=='MESH': meshes.append(obj)
arm=next(o for o in meshes if o.get('export_name')=='Arm.R')
mesh=arm.data; uv=mesh.uv_layers.active.data
skin=[p for p in mesh.polygons if uv[p.loop_indices[0]].uv.x<.3]
indices=sorted({v for p in skin for v in p.vertices}); mapping={v:i for i,v in enumerate(indices)}
verts=[tuple(mesh.vertices[v].co) for v in indices]
faces=[[mapping[v] for v in p.vertices] for p in skin]
colors=[int(uv[p.loop_indices[0]].uv.x*10) for p in skin]
# Reuse the established closed low-poly primitives and palette conventions.
tree=ast.parse(Path('C:/Users/linco/Mugg/Tools/Bramblekin/build_model.py').read_text())
definitions=ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ('face','mass','segment')],type_ignores=[])
exec(compile(definitions,'club_primitives','exec'))
club={'verts':[],'faces':[],'colors':[]}
segment(club,(.43,-.04,.44),(.43,-.57,.84),.035,.075,4,8)
mass(club,(.43,-.54,.82),(.125,.135,.175),3,8)
segment(club,(.43,-.49,.80),(.57,-.49,.85),.05,.025,4,5)
# Leather grip and a blunt knot emphasize the material and strike mass.
segment(club,(.43,-.08,.47),(.43,-.20,.56),.043,.047,6,8)
offset=len(verts)
verts.extend(tuple(v-arm.location) for v in club['verts'])
faces.extend([i+offset for i in f] for f in club['faces']); colors.extend(club['colors'])
new=bpy.data.meshes.new('BK_ClubArm'); new.from_pydata(verts,[],faces); new.update()
new.materials.append(mesh.materials[0]); paletteUV=new.uv_layers.new(name='PaletteUV')
for polygon,color in zip(new.polygons,colors):
    for loop in polygon.loop_indices: paletteUV.data[loop].uv=((color+.5)/10,.5)
bm=bmesh.new(); bm.from_mesh(new); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
assert all(e.is_manifold for e in bm.edges), 'Closed parts required'
bm.to_mesh(new); bm.free(); arm.data=new
bpy.ops.object.select_all(action='DESELECT'); renamed=[]; exportNames=[]
try:
    for obj in meshes:
        desired=obj['export_name']; occupied=bpy.data.objects.get(desired)
        if occupied and occupied!=obj:
            renamed.append((occupied,occupied.name)); occupied.name='Preserved_club_'+desired
        exportNames.append((obj,obj.name)); obj.name=desired; obj.select_set(True)
    bpy.context.view_layer.objects.active=arm
    bpy.ops.export_scene.fbx(filepath=str(OUT/'Bramblekin.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
finally:
    for obj,name in exportNames: obj.name='Club_v005_'+obj['export_name']
    for obj,name in renamed: obj.name=name
palette=bpy.data.images.get('BK_Palette'); oldPath=palette.filepath_raw
palette.filepath_raw=str(OUT/'BK_Palette.png'); palette.save(); palette.filepath_raw=oldPath
scene.render.engine='CYCLES'; scene.cycles.samples=16
scene.render.resolution_x=512; scene.render.resolution_y=512; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'; scene.render.filepath=str(OUT/'preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Bramblekin.blend'))
bpy.ops.render.render(write_still=True)
for obj in meshes:
    obj.data.calc_loop_triangles()
    assert all(t.area>1e-10 for t in obj.data.loop_triangles)
result={'parts':len(meshes),'triangles':sum(len(o.data.loop_triangles) for o in meshes),'club':'Heavy knotted wooden club, leather grip, held forward of the fist; attached to right-arm animation and corpse.'}
(OUT/'manifest.json').write_text(json.dumps(result,indent=2))
