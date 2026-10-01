"""Ground all animation samples and preserve v001. Run with original BB rig active."""
import bpy, json, shutil
from pathlib import Path
from mathutils import Vector
OUT=Path('C:/Users/linco/Mugg/Artifacts/Briarback/v002')
if (OUT/'Briarback.blend').exists(): raise RuntimeError('v002 already exists; preserve it.')
OUT.mkdir(parents=True,exist_ok=True)
scene=bpy.context.scene;rig=bpy.data.objects['BB_Rig'];body=bpy.data.objects['BB_Body']
actions=[a for a in bpy.data.actions if a.name.startswith('BB_')]
assert len(actions)==7
up=rig.pose.bones['Body'].bone.matrix_local.to_3x3().inverted()@Vector((0,0,1))
validation=[]
for action in actions:
    rig.animation_data.action=action
    samples=[]
    for half in range(round(action.frame_range[0]*2),round(action.frame_range[1]*2)+1):
        frame=half/2
        scene.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
        obj=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=obj.to_mesh()
        low=min((obj.matrix_world@v.co).z for v in mesh.vertices);obj.to_mesh_clear()
        samples.append((frame,rig.pose.bones['Body'].location.copy()+up*max(0,-low)))
    for frame,location in samples:
        rig.pose.bones['Body'].location=location
        rig.pose.bones['Body'].keyframe_insert('location',frame=frame,group='Body')
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    if curve.data_path=='pose.bones["Body"].location':
                        for key in curve.keyframe_points:key.interpolation='LINEAR'
    low=99;high=-99
    # Include subframes; all clips are checked at 60Hz and the final endpoint.
    start,end=action.frame_range
    for half in range(round(start*2),round(end*2)+1):
        frame=half/2;scene.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
        obj=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=obj.to_mesh()
        zs=[(obj.matrix_world@v.co).z for v in mesh.vertices]
        low=min(low,min(zs));high=max(high,max(zs));obj.to_mesh_clear()
    assert low>-.003, (action.name,low)
    validation.append({'clip':action.name,'lowest_z':low,'highest_z':high,'sample_hz':60})

manifest=json.loads((OUT.parent/'v001/manifest.json').read_text())
manifest['version']=2;manifest['animation_grounding']='All seven actions corrected and sampled at 60Hz, lowest Z above -0.003m'
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2))
(OUT/'animation-validation.json').write_text(json.dumps(validation,indent=2))
for file in ['BB_Palette.png','BB_Windup.wav','BB_Charge.wav','BB_Hurt.wav','BB_Death.wav']:
    shutil.copyfile(OUT.parent/'v001'/file,OUT/file)
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
rig.animation_data.action=None
for action in actions:
    track=rig.animation_data.nla_tracks.new();track.name=action.name;track.strips.new(action.name,1,action)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Briarback.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},
    axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,
    bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0,
    path_mode='COPY',embed_textures=True)
for track in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(track)
rig.animation_data.action=bpy.data.actions['BB_Idle'];scene.frame_set(1)
scene.render.filepath=str(OUT/'preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Briarback.blend'))
bpy.ops.render.render(write_still=True)
rig.animation_data.action=bpy.data.actions['BB_Death'];scene.frame_set(61)
scene.render.filepath=str(OUT/'death.png');bpy.ops.render.render(write_still=True)
rig.animation_data.action=bpy.data.actions['BB_Idle'];scene.frame_set(1)
scene.render.filepath=str(OUT/'preview.png')
result={'output':str(OUT),'animation_grounding':validation}
