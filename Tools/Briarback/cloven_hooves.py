"""Revise the inspected v002 character into v003, preserving previous sources.
Replaces only the rigid hoof islands, preserving the rig, UV palette and actions.
Run through the official Blender MCP with the Briarback source active.
"""
import bpy, bmesh, math, json, shutil
from pathlib import Path
from mathutils import Vector

ROOT=Path('C:/Users/linco/Mugg/Artifacts/Briarback')
OUT=ROOT/'v003'
if (OUT/'Briarback.blend').exists(): raise RuntimeError('v003 exists; preserve this revision.')
source=bpy.data.objects.get('BB_Body');rig=bpy.data.objects.get('BB_Rig');scene=bpy.context.scene
assert source and rig and source.type=='MESH'
assert bpy.context.mode=='OBJECT'
OUT.mkdir(parents=True,exist_ok=True)
# Keep the original mesh datablock recoverable inside the new file, too.
old=source.data;old.use_fake_user=True
group_names=[g.name for g in source.vertex_groups]
verts=[];faces=[];palette=[];slots=[];weights=[];mapping={}
hoof_groups={g.index:g.name for g in source.vertex_groups if g.name.startswith('Hoof')}
for vertex in old.vertices:
    if any(g.group in hoof_groups and g.weight>.5 for g in vertex.groups): continue
    mapping[vertex.index]=len(verts);verts.append(tuple(vertex.co))
    weights.append([(g.group,g.weight) for g in vertex.groups])
old_uv=old.uv_layers.active
for poly in old.polygons:
    if not all(i in mapping for i in poly.vertices):continue
    faces.append(tuple(mapping[i] for i in poly.vertices))
    palette.append([tuple(old_uv.data[i].uv) for i in poly.loop_indices]);slots.append(poly.material_index)

def claw(x,y,bone,side):
    # Two separate rounded, tapering keratin claws. Toe points forward (-Y),
    # heel is narrow, coronet tapers into the ankle. The center cleft is geometry.
    start=len(verts)
    profile=[(-.040,-.190),(.040,-.190),(.071,-.150),(.076,.076),
             (.041,.115),(-.041,.115),(-.076,.076),(-.071,-.150)]
    for z,scale,shift in [(0,1,0),(.155,.99,.006),(.225,.72,.025)]:
        for dx,dy in profile:
            verts.append((x+dx*scale,y+dy*scale+shift,z))
            weights.append([(source.vertex_groups[bone].index,1)])
    new=[tuple(start+i for i in reversed(range(8)))]
    for ring in range(2):
        for i in range(8):
            j=(i+1)%8
            new.append((start+ring*8+i,start+ring*8+j,start+(ring+1)*8+j,start+(ring+1)*8+i))
    new.append(tuple(start+16+i for i in range(8)))
    for n,f in enumerate(new):
        faces.append(f);slots.append(0)
        # Soot-dark keratin with a subdued worn upper lip; no square boot band.
        swatch=0 if n>=9 else 7
        palette.append([((swatch+.5)/8+.022*math.cos(2*math.pi*i/len(f)),
                         .5+.18*math.sin(2*math.pi*i/len(f))) for i in range(len(f))])

for name in [g.name for g in source.vertex_groups if g.name.startswith('Hoof')]:
    indices=[v for v in old.vertices if any(g.group==source.vertex_groups[name].index for g in v.groups)]
    low=Vector((min(v.co.x for v in indices),min(v.co.y for v in indices),min(v.co.z for v in indices)))
    high=Vector((max(v.co.x for v in indices),max(v.co.y for v in indices),max(v.co.z for v in indices)))
    x=(low.x+high.x)/2;y=(low.y+high.y)/2
    claw(x-.086,y,name,-1);claw(x+.086,y,name,1)

mesh=bpy.data.meshes.new('BB_ClovenBodyMesh');mesh.from_pydata(verts,[],faces);mesh.update()
for material in old.materials:mesh.materials.append(material)
uv=mesh.uv_layers.new(name='PaletteUV')
for poly,coordinates,slot in zip(mesh.polygons,palette,slots):
    poly.use_smooth=False;poly.material_index=slot
    for loop,coordinate in zip(poly.loop_indices,coordinates):uv.data[loop].uv=coordinate
source.data=mesh
for name in group_names:source.vertex_groups.new(name=name)
for group in source.vertex_groups:
    for index,groups in enumerate(weights):
        for number,weight in groups:
            if number==group.index:group.add([index],weight,'REPLACE')
bm=bmesh.new();bm.from_mesh(mesh);bad=sum(not e.is_manifold for e in bm.edges);bm.free()
assert bad==0
assert all(len(v.groups)==1 and abs(v.groups[0].weight-1)<1e-5 for v in mesh.vertices)
mesh.calc_loop_triangles()
actions=[a for a in bpy.data.actions if a.name.startswith('BB_')];assert len(actions)==7
# Re-ground changed toe geometry, retaining every original animation pose.
up=rig.pose.bones['Body'].bone.matrix_local.to_3x3().inverted()@Vector((0,0,1))
validation=[]
for action in actions:
    rig.animation_data.action=action;samples=[]
    for half in range(round(action.frame_range[0]*2),round(action.frame_range[1]*2)+1):
        frame=half/2;scene.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
        obj=source.evaluated_get(bpy.context.evaluated_depsgraph_get());evaluated=obj.to_mesh()
        low=min((obj.matrix_world@v.co).z for v in evaluated.vertices);obj.to_mesh_clear()
        samples.append((frame,rig.pose.bones['Body'].location.copy()+up*max(0,-low)))
    for frame,location in samples:
        rig.pose.bones['Body'].location=location;rig.pose.bones['Body'].keyframe_insert('location',frame=frame,group='Body')
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    if curve.data_path=='pose.bones["Body"].location':
                        for key in curve.keyframe_points:key.interpolation='LINEAR'
    lows=[]
    for half in range(round(action.frame_range[0]*2),round(action.frame_range[1]*2)+1):
        frame=half/2;scene.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
        obj=source.evaluated_get(bpy.context.evaluated_depsgraph_get());evaluated=obj.to_mesh()
        lows.append(min((obj.matrix_world@v.co).z for v in evaluated.vertices));obj.to_mesh_clear()
    assert min(lows)>-.003,(action.name,min(lows))
    validation.append({'clip':action.name,'lowest_z':min(lows),'sample_hz':60})

manifest=json.loads((ROOT/'v002/manifest.json').read_text())
manifest.update(version=3,triangles=len(mesh.loop_triangles),vertices=len(mesh.vertices),
    hooves='Four grounded cloven hooves, each with two separate tapered eight-sided claws',
    visual_status='Blender render and revised Unity import pending')
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2))
(OUT/'animation-validation.json').write_text(json.dumps(validation,indent=2))
for file in ['BB_Palette.png','BB_Windup.wav','BB_Charge.wav','BB_Hurt.wav','BB_Death.wav']:
    shutil.copyfile(ROOT/'v002'/file,OUT/file)
bpy.ops.object.select_all(action='DESELECT');source.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
rig.animation_data.action=None
for action in actions:
    track=rig.animation_data.nla_tracks.new();track.name=action.name;track.strips.new(action.name,1,action)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Briarback.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},
    axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,
    bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_step=.5,bake_anim_simplify_factor=0,
    path_mode='COPY',embed_textures=True)
for track in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(track)
rig.animation_data.action=bpy.data.actions['BB_Idle'];scene.frame_set(1)
scene.name='Briarback_v003';scene.render.filepath=str(OUT/'preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Briarback.blend'))
bpy.ops.render.render(write_still=True)
camera=scene.camera;old_location=camera.location.copy();old_rotation=camera.rotation_euler.copy();old_scale=camera.data.ortho_scale
camera.location=(2.8,-4,1.25);camera.rotation_euler=(Vector((0,-.1,.27))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.ortho_scale=2.9;scene.render.filepath=str(OUT/'hooves.png');bpy.ops.render.render(write_still=True)
camera.location=old_location;camera.rotation_euler=old_rotation;camera.data.ortho_scale=old_scale;scene.render.filepath=str(OUT/'preview.png')
result={'output':str(OUT),'triangles':manifest['triangles'],'vertices':manifest['vertices'],'non_manifold_edges':bad,'animation_grounding':validation}
