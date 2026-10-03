"""Split existing closed leg components for knee/ankle animation. Official Blender MCP.
Preserves the v001 source and creates a separate v002 scene and editable source.
"""
import bpy, json
from pathlib import Path
from mathutils import Vector
OUT=Path('C:/Users/linco/Mugg/Artifacts/Bramblekin/v003')
if (OUT/'Bramblekin.blend').exists(): raise RuntimeError('v002 already exists; preserve it.')
OUT.mkdir(parents=True,exist_ok=True)
source=bpy.data.scenes.get('Bramblekin_v001')
if source is None:
    with bpy.data.libraries.load('C:/Users/linco/Mugg/Artifacts/Bramblekin/v001/Bramblekin.blend',link=False) as (available,loaded):
        loaded.scenes=['Bramblekin_v001']
    source=loaded.scenes[0]
scene=bpy.data.scenes.new('Bramblekin_v003');bpy.context.window.scene=scene
scene.world=source.world.copy()
meshes=[]
for old in source.objects:
    obj=old.copy();obj.data=old.data.copy();scene.collection.objects.link(obj)
    obj.name=old.name # Blender suffixes are normalized at export using a separate temporary source name.
    obj['export_name']=old.name.replace('Source_v001_','').replace('Preserved_v001_','').split('.00')[0]
    if old.type=='MESH': meshes.append(obj)
    if old==source.camera:scene.camera=obj
for side in ('L','R'):
    obj=next(o for o in meshes if o['export_name']=='Leg.'+side);mesh=obj.data
    adjacent={v.index:set() for v in mesh.vertices}
    for edge in mesh.edges:
        a,b=edge.vertices;adjacent[a].add(b);adjacent[b].add(a)
    unseen=set(adjacent);groups=[]
    while unseen:
        seed=unseen.pop();component={seed};stack=[seed]
        while stack:
            for other in adjacent[stack.pop()]:
                if other in unseen:unseen.remove(other);component.add(other);stack.append(other)
        groups.append(component)
    partitions={'Leg.'+side:set(),'Shin.'+side:set(),'Foot.'+side:set()}
    for group in groups:
        height=max((obj.matrix_world@mesh.vertices[v].co).z for v in group)
        mean_height=sum((obj.matrix_world@mesh.vertices[v].co).z for v in group)/len(group)
        name=('Leg.' if height>.3 else 'Shin.' if mean_height>.12 else 'Foot.')+side
        partitions[name].update(group)
    sign=-1 if side=='L' else 1
    pivots={'Leg.'+side:Vector((sign*.145,.015,.43)),
            'Shin.'+side:Vector((sign*.18,.03,.20)), 'Foot.'+side:Vector((sign*.19,-.04,.065))}
    for name,indices in partitions.items():
        assert indices,name
        ordered=sorted(indices);mapping={v:i for i,v in enumerate(ordered)}
        polys=[p for p in mesh.polygons if set(p.vertices)<=indices]
        new=bpy.data.meshes.new('BK2_'+name)
        new.from_pydata([tuple(obj.matrix_world@mesh.vertices[v].co-pivots[name]) for v in ordered],[],
            [[mapping[v] for v in p.vertices] for p in polys]);new.update()
        uv=new.uv_layers.new(name='PaletteUV')
        for polygon,original in zip(new.polygons,polys):
            for loop,old_loop in zip(polygon.loop_indices,original.loop_indices):uv.data[loop].uv=mesh.uv_layers.active.data[old_loop].uv
        new.materials.append(mesh.materials[0]);part=bpy.data.objects.new('BK2_'+name,new)
        scene.collection.objects.link(part);part.location=pivots[name];part['export_name']=name;meshes.append(part)
    meshes.remove(obj);scene.collection.objects.unlink(obj)
# The original scene remains intact; export names are assigned temporarily without deleting data.
renamed=[]
for obj in meshes:
    desired=obj['export_name'];occupied=bpy.data.objects.get(desired)
    if occupied is not None and occupied!=obj:renamed.append((occupied,occupied.name));occupied.name='Preserved_v001_'+desired
for obj in meshes:obj.name=obj['export_name'];obj.select_set(True)
bpy.context.view_layer.objects.active=meshes[0]
bpy.ops.export_scene.fbx(filepath=str(OUT/'Bramblekin.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
for obj,name in renamed:obj.name='Source_v001_'+name
palette=bpy.data.images.get('BK_Palette');palette.filepath_raw=str(OUT/'BK_Palette.png');palette.save()
scene.render.engine='CYCLES';scene.cycles.samples=32
scene.render.resolution_x=768;scene.render.resolution_y=768;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(OUT/'preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Bramblekin.blend'))
bpy.ops.render.render(write_still=True)
for obj in meshes:obj.data.calc_loop_triangles()
result={'parts':len(meshes),'triangles':sum(len(o.data.loop_triangles) for o in meshes),'change':'Existing anatomy split at knees and ankles; silhouette and palette retained.'}
(OUT/'manifest.json').write_text(json.dumps(result,indent=2))

