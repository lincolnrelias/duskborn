"""Compare exported baseline/fix in deterministic poses; Blender only, not Unity playback.
Checks real triangle crossings between the shirt's hip region and trousers, plus
the connected trouser surface's topology. Captures the stride that exposed v001.
"""
import bpy, bmesh, math, json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root=Path('C:/Users/linco/Mugg/Artifacts/Ironroot')
out=root/'v002'
production=bpy.data.scenes['Ironroot_Production']
poses=[('rest',0,0,0,0,0,0)]
for i in range(16):
    a=2*math.pi*i/16
    poses.append(('stride_%02d'%i,65*math.sin(a),-65*math.sin(a),max(0,-math.sin(a))*65,max(0,math.sin(a))*65,0,0))
poses.extend([('high_step',-90,20,100,0,0,0),('crouch',-85,-85,115,115,0,0),('wide_stance',0,0,25,25,30,-30)])

def set_pose(rig,pose):
    for b in rig.pose.bones: b.rotation_mode='XYZ'; b.rotation_euler=(0,0,0)
    for side in ['Left','Right']: rig.pose.bones[side+'UpperArm'].rotation_euler.x=math.radians(-58)
    for name,angle in zip(['LeftUpperLeg','RightUpperLeg','LeftLowerLeg','RightLowerLeg'],pose[1:5]):
        rig.pose.bones[name].rotation_euler.x=math.radians(angle)
    rig.pose.bones['LeftUpperLeg'].rotation_euler.z=math.radians(pose[5])
    rig.pose.bones['RightUpperLeg'].rotation_euler.z=math.radians(pose[6])
    bpy.context.view_layer.update()

def hip_triangles(obj,shirt=False):
    original=obj.data
    ev=obj.evaluated_get(bpy.context.evaluated_depsgraph_get()); data=ev.to_mesh()
    try:
        data.calc_loop_triangles()
        vertices=[obj.matrix_world@v.co for v in data.vertices]
        faces=[]
        for tri in data.loop_triangles:
            indices=tuple(tri.vertices)
            if min(original.vertices[j].co.z for j in indices)>1.11: continue
            if max(original.vertices[j].co.z for j in indices)<.70: continue
            if shirt and not original.materials[tri.material_index].name.startswith('IR_Shirt'): continue
            faces.append(indices)
        return vertices,faces
    finally: ev.to_mesh_clear()

reports={}
for version in ['v001','v002']:
    scene=bpy.data.scenes.new('Ironroot_HipCheck_'+version); bpy.context.window.scene=scene
    bpy.ops.import_scene.fbx(filepath=str(root/version/'Ironroot.fbx'))
    rig=next(o for o in scene.objects if o.type=='ARMATURE')
    legs=next(o for o in scene.objects if o.name.startswith('IR_Legs'))
    torso=next(o for o in scene.objects if o.name.startswith('IR_Torso'))
    bm=bmesh.new(); bm.from_mesh(legs.data)
    edges_bad=sum(not e.is_manifold for e in bm.edges)
    pending=set(bm.verts); islands=0
    while pending:
        todo=[pending.pop()]; islands+=1
        while todo:
            v=todo.pop()
            for e in v.link_edges:
                other=e.other_vert(v)
                if other in pending: pending.remove(other); todo.append(other)
    bm.free()
    samples=[]
    for pose in poses:
        set_pose(rig,pose)
        lv,lf=hip_triangles(legs); tv,tf=hip_triangles(torso,True)
        crossing=BVHTree.FromPolygons(lv,lf,all_triangles=True).overlap(BVHTree.FromPolygons(tv,tf,all_triangles=True))
        samples.append({'pose':pose[0],'shirtTrouserTriangleCrossings':len(crossing)})
    reports[version]={'nonManifoldTrouserEdges':edges_bad,'trouserIslands':islands,'samples':samples}
    scene.world=production.world; scene.render.engine='CYCLES'; scene.cycles.samples=24; scene.view_settings.view_transform='AgX'
    for light in [o for o in production.objects if o.type=='LIGHT']: scene.collection.objects.link(light)
    cam=production.camera.copy(); cam.data=cam.data.copy(); scene.collection.objects.link(cam); scene.camera=cam
    cam.data.ortho_scale=.83; cam.location=(1.8,-4,1.6); cam.rotation_euler=(Vector((0,0,.93))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.resolution_x=850; scene.render.resolution_y=850; scene.render.resolution_percentage=100
    for pose_name in ['stride_04','high_step','crouch']:
        set_pose(rig,next(p for p in poses if p[0]==pose_name))
        scene.render.filepath=str(out/(version+'-'+pose_name+'.png')); bpy.ops.render.render(write_still=True)

(out/'hip-deformation-report.json').write_text(json.dumps(reports,indent=2))
assert reports['v002']['trouserIslands']==1 and reports['v002']['nonManifoldTrouserEdges']==0, reports['v002']
assert any(s['shirtTrouserTriangleCrossings']>0 for s in reports['v001']['samples']), 'Baseline did not reproduce.'
assert all(s['shirtTrouserTriangleCrossings']==0 for s in reports['v002']['samples']), reports['v002']['samples']
bpy.context.window.scene=production
result=reports
