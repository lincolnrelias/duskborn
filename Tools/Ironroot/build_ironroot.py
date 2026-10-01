"""Original Ironroot player. Run through the connected Blender MCP session.
Z up, -Y forward, meters, T-pose; FBX exports +Y up/+Z forward for Unity.
No third-party geometry or skeleton is included in the deliverable.
"""
import bpy, math, json, os, bmesh
from mathutils import Vector
from pathlib import Path

OUT = Path('C:/Users/linco/Mugg/Artifacts/Ironroot/v002')
OUT.mkdir(parents=True, exist_ok=True)
old = bpy.data.scenes.get('Ironroot_Production')
if old:
    # Only replace this generator's own scene; preserve the user's other scenes.
    for ob in list(old.objects):
        if len(ob.users_scene)==1: bpy.data.objects.remove(ob,do_unlink=True)
    bpy.data.scenes.remove(old)
scene = bpy.data.scenes.new('Ironroot_Production')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
collection = bpy.data.collections.new('Ironroot_Character')
scene.collection.children.link(collection)
parts = []
palette = {
    'Skin':(.58,.365,.235,1), 'Shirt':(.115,.145,.18,1),
    'Trousers':(.245,.222,.177,1), 'Leather':(.135,.077,.043,1),
    'Hair':(.075,.047,.03,1), 'Iron':(.28,.30,.32,1),
    'Ochre':(.52,.34,.13,1), 'Eyes':(.68,.65,.54,1),
    'Pupil':(.038,.042,.038,1), 'Lip':(.31,.155,.105,1),
}
mats = {}
for name, color in palette.items():
    color = tuple(c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4 for c in color[:3])+(1,)
    m = bpy.data.materials.get('IR_'+name) or bpy.data.materials.new('IR_'+name)
    m.diffuse_color = color
    m.use_nodes = True
    bs = m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = color
    bs.inputs['Roughness'].default_value = .88
    bs.inputs['Metallic'].default_value = .65 if name=='Iron' else 0
    mats[name] = m

arm = bpy.data.armatures.new('IronrootSkeleton')
rig = bpy.data.objects.new('IronrootRig',arm)
collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
def bone(name, head, tail, parent=None):
    b = arm.edit_bones.new(name); b.head=head; b.tail=tail
    if parent: b.parent = arm.edit_bones[parent]
    return b
bone('Root',(0,0,0),(0,0,.15))
bone('Hips',(0,0,.93),(0,0,1.05),'Root')
bone('Spine',(0,0,1.05),(0,0,1.22),'Hips')
bone('Chest',(0,0,1.22),(0,0,1.40),'Spine')
bone('UpperChest',(0,0,1.40),(0,0,1.48),'Chest')
bone('Neck',(0,0,1.48),(0,0,1.57),'UpperChest')
bone('Head',(0,0,1.57),(0,0,1.78),'Neck')
for side,s in [('Left',1),('Right',-1)]:
    bone(side+'Shoulder',(s*.035,0,1.44),(s*.225,0,1.435),'UpperChest')
    bone(side+'UpperArm',(s*.225,0,1.435),(s*.505,0,1.435),side+'Shoulder')
    bone(side+'LowerArm',(s*.505,0,1.435),(s*.755,0,1.435),side+'UpperArm')
    bone(side+'Hand',(s*.755,0,1.435),(s*.855,0,1.435),side+'LowerArm')
    for fn,y,ln in [('Index',-.042,.082),('Middle',-.014,.092),('Ring',.014,.083),('Little',.040,.064)]:
        start=Vector((s*.84,y,1.43))
        for k,label in enumerate(['Proximal','Intermediate','Distal']):
            h=start+Vector((s*ln*k/3,0,0)); t=start+Vector((s*ln*(k+1)/3,0,0))
            bone(side+fn+label,h,t,side+'Hand' if k==0 else side+fn+['Proximal','Intermediate'][k-1])
    pts=[(.785,-.035,1.43),(.80,-.07,1.426),(.825,-.092,1.422),(.849,-.108,1.418)]
    for k,label in enumerate(['Proximal','Intermediate','Distal']):
        h=pts[k]; t=pts[k+1]
        bone(side+'Thumb'+label,(s*h[0],h[1],h[2]),(s*t[0],t[1],t[2]),side+'Hand' if k==0 else side+'Thumb'+['Proximal','Intermediate'][k-1])
    bone(side+'UpperLeg',(s*.102,0,.95),(s*.13,-.018,.525),'Hips')
    bone(side+'LowerLeg',(s*.13,-.018,.525),(s*.135,0,.105),side+'UpperLeg')
    bone(side+'Foot',(s*.135,0,.105),(s*.135,-.12,.05),side+'LowerLeg')
    bone(side+'Toes',(s*.135,-.12,.05),(s*.135,-.21,.05),side+'Foot')
bpy.ops.object.mode_set(mode='OBJECT')
rig.select_set(False)

def mesh(name, verts, faces, material, weights):
    data=bpy.data.meshes.new(name); data.from_pydata(verts,[],faces); data.update()
    obj=bpy.data.objects.new(name,data); collection.objects.link(obj)
    data.materials.append(mats[material]); parts.append(obj)
    # Palette UV regions are deliberate and can be replaced with a painted atlas later.
    uv=data.uv_layers.new(name='UVMap')
    for poly in data.polygons:
        for li in poly.loop_indices:
            v=data.vertices[data.loops[li].vertex_index].co
            uv.data[li].uv=(v.x*.45+.5,v.z/1.85)
    for i,w in enumerate(weights):
        for bn,value in w.items():
            if value<=0: continue
            vg=obj.vertex_groups.get(bn) or obj.vertex_groups.new(name=bn)
            vg.add([i],value,'REPLACE')
    obj.parent=rig
    mod=obj.modifiers.new('Humanoid skin','ARMATURE'); mod.object=rig
    return obj

def rings(name, rows, material, axis='z', n=12, caps=True):
    # row = center, two radii, bone weights. Wound counterclockwise when viewed from outside.
    verts=[]; weights=[]; faces=[]
    for center,r1,r2,w in rows:
        for j in range(n):
            a=2*math.pi*j/n
            p=Vector(center)
            if axis=='z': p+=Vector((r1*math.cos(a),r2*math.sin(a),0))
            else: p+=Vector((0,r1*math.cos(a),r2*math.sin(a)))
            verts.append(tuple(p)); weights.append(w)
    for k in range(len(rows)-1):
        for j in range(n):
            a=k*n+j; b=k*n+(j+1)%n; c=(k+1)*n+(j+1)%n; d=(k+1)*n+j
            faces.append((a,b,c,d))
    if caps:
        faces.extend([tuple(reversed(range(n))),tuple((len(rows)-1)*n+j for j in range(n))])
    obj=mesh(name,verts,faces,material,weights)
    return obj

def ellipsoid(name,center,scale,mat,bn,segments=12,rings_count=6):
    rows=[]
    for k in range(rings_count+1):
        a=-math.pi/2+math.pi*k/rings_count
        rows.append(((center[0],center[1],center[2]+scale[2]*math.sin(a)),max(.0008,scale[0]*math.cos(a)),max(.0008,scale[1]*math.cos(a)),{bn:1}))
    return rings(name,rows,mat,n=segments)

def box(name,center,scale,mat,bn,bevel=.008):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj=bpy.context.object; obj.name=name; obj.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        m=obj.modifiers.new('Broad edges','BEVEL'); m.width=bevel; m.segments=1
        bpy.ops.object.modifier_apply(modifier=m.name)
    coords=[tuple(obj.matrix_world@v.co) for v in obj.data.vertices]
    faces=[tuple(p.vertices) for p in obj.data.polygons]
    bpy.data.objects.remove(obj,do_unlink=True)
    return mesh(name,coords,faces,mat,[{bn:1}]*len(coords))

def strap(name,a,b,width,depth,mat,bn):
    mid=(Vector(a)+Vector(b))/2; direction=Vector(b)-Vector(a)
    o=box(name,(0,0,0),(width,depth,direction.length),mat,bn,.0015)
    rot=direction.to_track_quat('Z','Y').to_matrix()
    for v in o.data.vertices: v.co=rot@v.co+mid
    return o

# One continuous trouser surface: two leg openings join at a shared crotch seam,
# then widen into the waist. No capped thigh/pelvis islands can emerge during a stride.
pv=[]; pf=[]; pw=[]
def pants_vertex(co, weights):
    pv.append(co); pw.append(weights); return len(pv)-1
for side,s in [('Left',1),('Right',-1)]:
    ul=side+'UpperLeg'; ll=side+'LowerLeg'
    rows=[(.27,.135,.064,.065,{ll:1}),(.37,.134,.080,.074,{ll:1}),
          (.475,.131,.071,.072,{ll:.85,ul:.15}),(.525,.13,.072,.073,{ll:.5,ul:.5}),
          (.58,.128,.080,.081,{ul:.85,ll:.15}),(.72,.117,.091,.091,{ul:.95,'Hips':.05}),
          (.80,.106,.085,.096,{ul:.6,'Hips':.4})]
    loops=[]
    for z,x,rx,ry,w in rows:
        loops.append([pants_vertex((s*(x+rx*math.cos(2*math.pi*j/12)),ry*math.sin(2*math.pi*j/12)-(.014 if z<.6 else 0),z),w) for j in range(12)])
    crotch=[]
    for j in range(12):
        a=2*math.pi*j/12
        co=(s*(.104+.098*math.cos(a)),.105*math.sin(a),.874)
        w={ul:.28,'Hips':.72}
        if j==5: co=(0,.074,.905); w={'Hips':.8,'LeftUpperLeg':.1,'RightUpperLeg':.1}
        elif j==6: co=(0,0,.82); w={'Hips':.3,'LeftUpperLeg':.35,'RightUpperLeg':.35}
        elif j==7: co=(0,-.074,.905); w={'Hips':.8,'LeftUpperLeg':.1,'RightUpperLeg':.1}
        crotch.append(pants_vertex(co,w))
    loops.append(crotch)
    faces=[]
    for lower,upper in zip(loops,loops[1:]):
        for j in range(12): faces.append((lower[j],lower[(j+1)%12],upper[(j+1)%12],upper[j]))
    faces.append(tuple(reversed(loops[0])))
    # Inner 5-6-7 edges weld to the opposite leg. The remaining arc continues up
    # the front/side/back of one pelvis half, sharing the center seams with its mirror.
    arc=[crotch[j] for j in [7,8,9,10,11,0,1,2,3,4,5]]
    for z,rx,ry in [(.945,.180,.118),(1.015,.143,.093),(1.045,.137,.088)]:
        upper=[]
        for j in range(11):
            a=-math.pi/2+j*math.pi/10
            upper.append(pants_vertex((s*rx*math.cos(a),ry*math.sin(a),z),{'Hips':1}))
        for j in range(10): faces.append((arc[j],arc[j+1],upper[j+1],upper[j]))
        arc=upper
    faces.append(tuple(arc)) # Two half caps close the waist beneath the shirt.
    pf.extend(faces if s>0 else [tuple(reversed(f)) for f in faces])
mesh('TrousersConnected',pv,pf,'Trousers',pw)
for side,s in [('Left',1),('Right',-1)]:
    ul=side+'UpperLeg'; ll=side+'LowerLeg'; foot=side+'Foot'
    rings('Boot_'+side,[((s*.135,-.064,.035),.083,.151,{foot:1}),((s*.135,-.066,.076),.086,.151,{foot:1}),((s*.135,-.052,.12),.078,.13,{foot:1}),((s*.135,.006,.16),.065,.071,{foot:.6,ll:.4}),((s*.135,.008,.265),.069,.075,{ll:1}),((s*.135,.008,.32),.076,.077,{ll:1})],'Leather',n=12)
    rings('BootCuff_'+side,[((s*.135,.008,.285),.077,.081,{ll:1}),((s*.135,.008,.315),.081,.085,{ll:1}),((s*.135,.008,.329),.080,.084,{ll:1})],'Leather',n=10)
    rings('Sole_'+side,[((s*.135,-.064,.015),.085,.153,{foot:1}),((s*.135,-.064,.039),.087,.154,{foot:1})],'Hair',n=12)
    box('BootBuckle_'+side,(s*.197,-.012,.26),(.018,.05,.04),'Iron',ll,.003)

# Tucked shirt ends inside the belt, above the articulated hips; waist layers share Hips weights.
rings('Tunic',[
    ((0,0,1.016),.156,.103,{'Hips':1}),((0,0,1.035),.156,.103,{'Hips':1}),
    ((0,0,1.075),.161,.108,{'Hips':.35,'Spine':.65}),((0,0,1.13),.171,.116,{'Spine':.7,'Chest':.3}),
    ((0,0,1.26),.206,.132,{'Chest':1}),((0,0,1.38),.235,.128,{'Chest':.4,'UpperChest':.6}),
    ((0,0,1.435),.232,.108,{'UpperChest':1}),((0,0,1.48),.079,.067,{'UpperChest':1})],'Shirt',n=12,caps=False)
rings('Belt',[((0,0,1.004),.166,.113,{'Hips':1}),((0,0,1.051),.165,.112,{'Hips':1})],'Leather',n=12,caps=False)
box('Buckle',(0,-.118,1.03),(.062,.018,.047),'Iron','Hips',.005)
box('BuckleInset',(0,-.129,1.03),(.038,.005,.025),'Leather','Hips',.001)
strap('BeltTail',(.115,-.095,1.035),(.121,-.105,.980),.028,.015,'Leather','Hips')
for side,s in [('Left',1),('Right',-1)]:
    ua=side+'UpperArm'; la=side+'LowerArm'; hand=side+'Hand'
    # Longitudinal arm rings ensure continuous elbow bends, sleeves finish above elbow.
    rows=[(.166,.047,.049,{'UpperChest':.85,ua:.15}),(.225,.079,.079,{'UpperChest':.4,ua:.6}),(.29,.082,.083,{ua:1}),(.407,.071,.074,{ua:1}),(.441,.070,.072,{ua:1})]
    ob=rings('Sleeve_'+side,[((s*x,0,1.435),ry,rz,w) for x,ry,rz,w in (rows if s>0 else reversed(rows))],'Shirt',axis='x',n=10)
    rows=[(.419,.074,.076,{ua:1}),(.438,.078,.080,{ua:1}),(.459,.073,.075,{ua:.95,la:.05})]
    rings('RolledCuff_'+side,[((s*x,0,1.435),ry,rz,w) for x,ry,rz,w in (rows if s>0 else reversed(rows))],'Shirt',axis='x',n=10)
    rows=[(.436,.061,.062,{ua:1}),(.479,.060,.060,{ua:.8,la:.2}),(.505,.058,.058,{ua:.5,la:.5}),(.535,.06,.06,{ua:.15,la:.85}),(.584,.061,.064,{la:1}),(.665,.050,.052,{la:1}),(.753,.033,.034,{la:1}),(.775,.035,.033,{la:.4,hand:.6})]
    rings('Forearm_'+side,[((s*x,0,1.435),ry,rz,w) for x,ry,rz,w in (rows if s>0 else reversed(rows))],'Skin',axis='x',n=10)
    rows=[(.755,.032,.029,{la:.35,hand:.65}),(.79,.049,.030,{hand:1}),(.833,.054,.027,{hand:1}),(.849,.051,.024,{hand:1})]
    rings('Palm_'+side,[((s*x,0,1.435),ry,rz,w) for x,ry,rz,w in (rows if s>0 else reversed(rows))],'Skin',axis='x',n=8)
    for fn,y,ln in [('Index',-.042,.082),('Middle',-.014,.092),('Ring',.014,.083),('Little',.040,.064)]:
        rows=[]
        for k in range(7):
            t=k/6; idx=min(2,k//2); bn=side+fn+['Proximal','Intermediate','Distal'][idx]
            r=.0125*(1-.32*t)
            rows.append(((s*(.838+ln*t),y,1.43),r,r,{bn:1}))
        rings(side+fn,rows if s>0 else list(reversed(rows)),'Skin',axis='x',n=6)
    # Thumb segments follow the actual humanoid thumb chain.
    for label in ['Proximal','Intermediate','Distal']:
        b=arm.bones[side+'Thumb'+label]
        mid=(b.head_local+b.tail_local)/2
        ob=ellipsoid(side+'ThumbMesh'+label,mid,(.014,.014,(b.tail_local-b.head_local).length*.7),'Skin',b.name,8,4)
        q=(b.tail_local-b.head_local).to_track_quat('Z','Y')
        for v in ob.data.vertices: v.co=q@(v.co-mid)+mid

rings('Neck',[((0,0,1.465),.065,.061,{'Neck':.4,'UpperChest':.6}),((0,0,1.535),.060,.058,{'Neck':1}),((0,-.006,1.585),.064,.065,{'Head':.65,'Neck':.35})],'Skin',n=10)
rings('Collar',[((0,0,1.467),.086,.073,{'UpperChest':1}),((0,0,1.502),.079,.071,{'UpperChest':.6,'Neck':.4})],'Shirt',n=12)
for z in [1.407,1.43,1.453]:
    def lace_y(height):
        radius = .128+(height-1.38)*(-.020/.055) if height<=1.435 else .108+(height-1.435)*(-.041/.045)
        return -radius-.002
    strap('CollarLace',(-.018,lace_y(z+.007),z+.007),(.018,lace_y(z-.007),z-.007),.005,.004,'Ochre','UpperChest')
    strap('CollarLace',(.018,lace_y(z+.007),z+.007),(-.018,lace_y(z-.007),z-.007),.005,.004,'Ochre','UpperChest')

# Deliberately sculpted head rings: square jaw, cheekbone, brow, tapered forehead.
# Front points have a broad face plane; nose/eyes/brows provide actual readable facial structure.
outline=[(1,0),(.88,.66),(.45,1),(-.45,1),(-.88,.66),(-1,0),(-.88,-.62),(-.52,-.93),(0,-1),(.52,-.93),(.88,-.62)]
headrows=[(1.55,.055,.062,.006),(1.574,.078,.077,.006),(1.62,.090,.085,.001),(1.672,.097,.094,0),(1.706,.095,.092,.004),(1.745,.092,.090,.008),(1.783,.080,.077,.012),(1.808,.044,.044,.013)]
vs=[]; fs=[]
for z,rx,ry,cy in headrows:
    vs.extend([(x*rx,y*ry+cy,z) for x,y in outline])
n=len(outline)
for k in range(len(headrows)-1):
    for j in range(n): fs.append((k*n+j,k*n+(j+1)%n,(k+1)*n+(j+1)%n,(k+1)*n+j))
fs += [tuple(reversed(range(n))),tuple((len(headrows)-1)*n+j for j in range(n))]
mesh('Head',vs,fs,'Skin',[{'Head':1}]*len(vs))
for s in [-1,1]:
    ellipsoid('Ear',(s*.097,.0,1.665),(.018,.022,.038),'Skin','Head',8,4)
    # Narrow ivory eye plane, dark iris and angled brow; no huge cartoon eyeballs.
    eye=box('Eye',(s*.043,-.090,1.691),(.030,.009,.010),'Eyes','Head',.003)
    box('Iris',(s*.040,-.096,1.691),(.010,.003,.010),'Pupil','Head',.002)
    strap('Brow',(s*.022,-.094,1.709),(s*.067,-.078,1.714),.013,.011,'Hair','Head')
    strap('LowerLid',(s*.024,-.094,1.680),(s*.063,-.084,1.682),.004,.004,'Skin','Head')
# Wedge nose with narrow bridge and strong tip.
vs=[(-.012,-.089,1.711),(.012,-.089,1.711),(-.016,-.092,1.657),(.016,-.092,1.657),(-.011,-.12,1.666),(.011,-.12,1.666),(0,-.129,1.664),(0,-.101,1.649)]
mesh('Nose',vs,[(0,1,5,4),(0,4,2),(1,3,5),(2,4,6,7),(4,5,6),(3,7,6,5),(0,2,3,1),(2,7,3)],'Skin',[{'Head':1}]*8)
strap('Mouth',(-.025,-.084,1.628),(.025,-.084,1.628),.004,.005,'Lip','Head')
box('LowerLip',(0,-.087,1.623),(.036,.006,.006),'Skin','Head',.002)

# Low-poly scalp and designed swept locks, removable as one hair module.
hairparts=[]
before=len(parts)
cap=rings('HairCap',[((0,.011,1.731),.115,.119,{'Head':1}),((0,.010,1.781),.106,.111,{'Head':1}),((0,.011,1.819),.060,.063,{'Head':1}),((-.013,.012,1.834),.015,.022,{'Head':1})],'Hair',n=11)
for v in cap.data.vertices:
    if v.index<11:
        if v.co.y>.03: v.co.z=1.64+abs(v.co.x)*.24
        elif v.co.y>-.025: v.co.z=1.705
        else: v.co.z=1.758; v.co.y*=.90
for i in range(6):
    x=-.079+i*.029
    vs=[(x-.021,-.065,1.78),(x+.021,-.067,1.784),(x+.023,-.016,1.813),(x-.018,-.01,1.819),(x-.012,-.093,1.719+(i%3)*.012),(x+.019,-.082,1.766)]
    mesh('SweptLock',vs,[(0,1,2,3),(0,4,5,1),(1,5,2),(0,3,4),(3,2,5,4)],'Hair',[{'Head':1}]*6)
for s in [-1,1]:
    box('Sideburn',(s*.090,-.021,1.709),(.016,.045,.053),'Hair','Head',.004)
hairparts=parts[before:]

# Consolidate by equipment/body region and material. Retains swappable head/hair and tint slots.
groups={}
for ob in parts:
    if ob in hairparts: region='Hair'
    elif ob.name.startswith(('Head','Ear','Eye','Iris','Brow','LowerLid','Nose','Mouth','LowerLip')): region='Head'
    elif ob.name.startswith(('Boot','Sole')): region='Boots'
    elif ob.name.startswith(('Pelvis','Trousers')): region='Legs'
    elif ob.name.startswith(('Forearm','Palm','LeftIndex','LeftMiddle','LeftRing','LeftLittle','LeftThumb','RightIndex','RightMiddle','RightRing','RightLittle','RightThumb')): region='ArmsHands'
    else: region='Torso'
    groups.setdefault(region,[]).append(ob)
export_meshes=[]
for name,objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    if len(objects)>1: bpy.ops.object.join()
    ob=bpy.context.object; ob.name='IR_'+name
    bm=bmesh.new(); bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-8)
    bm.to_mesh(ob.data); bm.free(); ob.data.update()
    # Recalculate outward normals on every closed island.
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
    export_meshes.append(ob)

scene.render.engine='CYCLES'; scene.cycles.samples=32
scene.world=bpy.data.worlds.new('IronrootStudio'); scene.world.color=(.25,.25,.25)
scene.view_settings.view_transform='AgX'
def area(name,loc,energy,size):
    d=bpy.data.lights.new(name,'AREA'); d.energy=energy; d.shape='DISK'; d.size=size
    o=bpy.data.objects.new(name,d); scene.collection.objects.link(o); o.location=loc; o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
area('Key',(-3,-4,5),450,4); area('Fill',(3,-2,3),250,3); area('Rim',(1,3,4),400,3)
bpy.ops.mesh.primitive_plane_add(size=200)
floor=bpy.context.object; floor.name='StudioFloor'; floor.location.z=.014
floor_mat=bpy.data.materials.new('StudioClay'); floor_mat.diffuse_color=(.24,.255,.27,1); floor.data.materials.append(floor_mat)
camdata=bpy.data.cameras.new('ReviewCamera'); cam=bpy.data.objects.new('ReviewCamera',camdata); scene.collection.objects.link(cam); scene.camera=cam; camdata.type='ORTHO'; camdata.ortho_scale=2.13
scene.render.resolution_x=1000; scene.render.resolution_y=1100; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'

# Export neutral T-pose with applied unit scale. Root is not named Armature to avoid Unity stripping it.
bpy.ops.object.select_all(action='DESELECT')
for ob in export_meshes+[rig]: ob.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(OUT/'Ironroot.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',use_armature_deform_only=True,mesh_smooth_type='FACE',use_mesh_modifiers=True)
bpy.ops.export_scene.gltf(filepath=str(OUT/'Ironroot.glb'),export_format='GLB',use_selection=True,use_active_scene=True,export_animations=False,export_yup=True)

def camera_at(loc,target=(0,0,.95)):
    cam.location=loc; cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
camera_at((2.5,-5,2.5))
# Save editable neutral source before posing; render poses are not exported.
bpy.data.libraries.write(str(OUT/'Ironroot.blend'),{scene},fake_user=True)
for side,angle in [('Left',-58),('Right',-58)]:
    p=rig.pose.bones[side+'UpperArm']; p.rotation_mode='XYZ'; p.rotation_euler.x=math.radians(angle)
bpy.context.view_layer.update()
scene.render.filepath=str(OUT/'preview.png'); bpy.ops.render.render(write_still=True)
report={'heightMeters':1.819,'soleHeight':.015,'bones':len(arm.bones),'meshes':[{'name':o.name,'vertices':len(o.data.vertices),'triangles':sum(len(p.vertices)-2 for p in o.data.polygons),'materials':[m.name for m in o.data.materials],'uv':len(o.data.uv_layers),'unweightedVertices':sum(not v.groups for v in o.data.vertices)} for o in export_meshes]}
(OUT/'model-report.json').write_text(json.dumps(report,indent=2))
result=report
