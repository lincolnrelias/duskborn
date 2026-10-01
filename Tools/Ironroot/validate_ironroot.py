"""Blender render/FBX round-trip checks; does not launch Unity."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector
out=Path('C:/Users/linco/Mugg/Artifacts/Ironroot/v002')
s=bpy.data.scenes['Ironroot_Production']
bpy.context.window.scene=s
rig=next(o for o in s.objects if o.type=='ARMATURE')
for p in rig.pose.bones: p.rotation_mode='XYZ'; p.rotation_euler=(0,0,0)
for side in ['Left','Right']: rig.pose.bones[side+'UpperArm'].rotation_euler.x=math.radians(-58)
cam=s.camera
s.render.resolution_x=800; s.render.resolution_y=1000
for name,loc in [('front',(0,-5,1.2)),('rear',(0,5,1.2)),('side',(5,0,1.2))]:
    cam.location=loc; cam.rotation_euler=(Vector((0,0,.93))-cam.location).to_track_quat('-Z','Y').to_euler()
    s.render.filepath=str(out/(name+'.png')); bpy.ops.render.render(write_still=True)
for name,angle in [('LeftLowerArm',-90),('RightLowerArm',-70),('LeftUpperLeg',-35),('LeftLowerLeg',75),('RightUpperLeg',20),('RightLowerLeg',20)]:
    rig.pose.bones[name].rotation_euler.x=math.radians(angle)
cam.location=(2.5,-5,2.4); cam.rotation_euler=(Vector((0,0,.93))-cam.location).to_track_quat('-Z','Y').to_euler()
s.render.filepath=str(out/'pose-check.png'); bpy.ops.render.render(write_still=True)
rt=bpy.data.scenes.new('Ironroot_RoundTrip'); bpy.context.window.scene=rt
bpy.ops.import_scene.fbx(filepath=str(out/'Ironroot.fbx'))
meshes=[o for o in rt.objects if o.type=='MESH']; rigs=[o for o in rt.objects if o.type=='ARMATURE']
points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
checks={
    'meshes':len(meshes),'rigs':len(rigs),'bones':len(rigs[0].data.bones),
    'height':max(v.z for v in points)-min(v.z for v in points),
    'vertices':sum(len(o.data.vertices) for o in meshes),
    'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),
    'unweighted':sum(sum(not v.groups for v in o.data.vertices) for o in meshes),
    'nonNormalized':sum(sum(abs(sum(g.weight for g in v.groups)-1)>.001 for v in o.data.vertices) for o in meshes),
    'zeroAreaFaces':sum(sum(p.area<1e-10 for p in o.data.polygons) for o in meshes),
    'maxInfluences':max(len(v.groups) for o in meshes for v in o.data.vertices),
    'missingUV':sum(len(o.data.uv_layers)==0 for o in meshes)
}
assert checks['meshes']==6 and checks['rigs']==1 and checks['bones']==53, checks
assert 1.8<checks['height']<1.9 and checks['unweighted']==0 and checks['nonNormalized']==0, checks
assert checks['missingUV']==0 and checks['zeroAreaFaces']==0 and checks['maxInfluences']<=4, checks
(out/'roundtrip-report.json').write_text(json.dumps(checks,indent=2))
bpy.context.window.scene=s
for p in rig.pose.bones: p.rotation_euler=(0,0,0)
for side in ['Left','Right']: rig.pose.bones[side+'UpperArm'].rotation_euler.x=math.radians(-58)
result=checks
