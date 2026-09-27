"""Build the Moonwell Shrine in the connected Blender session; additive only."""
import bpy, math, random, os
import numpy as np
from mathutils import Vector
from math import sin, cos, pi

OUT = r'C:/Users/linco/Mugg/Artifacts/MoonwellShrine'
assert not bpy.data.collections.get('Moonwell_Shrine'), 'Shrine already exists; do not duplicate.'
scene = bpy.context.scene
asset = bpy.data.collections.new('Moonwell_Shrine')
scene.collection.children.link(asset)
studio = bpy.data.collections.new('Moonwell_Preview_Studio')
scene.collection.children.link(studio)
root = bpy.data.objects.new('Moonwell_ROOT', None)
asset.objects.link(root)
root['purpose'] = 'Arcane crafting station; meters; front faces -Y; pivot at floor center'

# A compact painted atlas: broad gradients, sparse strokes, no photographic noise.
S, T = 1024, 256
pix = np.ones((S, S, 4), dtype=np.float32)
palette = [(0.49,.51,.46),(.28,.32,.22),(.40,.26,.14),(.16,.17,.19),
           (.24,.13,.12),(.83,.74,.54),(.57,.40,.18),(.31,.20,.45),
           (.29,.33,.23),(.83,.74,.54),(.76,.67,.47),(.59,.59,.51),
           (.21,.22,.19),(.43,.29,.16),(.64,.50,.27),(.42,.27,.60)]
Y,X = np.mgrid[0:T,0:T]
for i,c in enumerate(palette):
    grad = .87 + .18*(Y/T) + .025*np.sin(X/51 + Y/80)
    if i in (2,13):
        grad += .045*np.sin(X/19 + .7*np.sin(Y/55))
    rgb = np.clip(np.asarray(c)[None,None,:]*grad[:,:,None],0,1)
    ty,tx = divmod(i,4)
    pix[ty*T:(ty+1)*T,tx*T:(tx+1)*T,:3] = rgb

def stroke(tile, points, color, width=2):
    ty,tx=divmod(tile,4)
    for a,b in zip(points[:-1],points[1:]):
        ax,ay=a; bx,by=b
        n=max(2,int(math.hypot(bx-ax,by-ay)*1.5))
        for t in np.linspace(0,1,n):
            x,y=int(ax+(bx-ax)*t),int(ay+(by-ay)*t)
            x0,x1=max(0,x-width),min(T,x+width+1)
            y0,y1=max(0,y-width),min(T,y+width+1)
            pix[ty*T+y0:ty*T+y1,tx*T+x0:tx*T+x1,:3]=color

def crescent_paint(tile,cx,cy,r,color):
    outer=(X-cx)**2+(Y-cy)**2<r*r
    cut=(X-(cx+r*.47))**2+(Y-(cy+r*.18))**2<(r*.90)**2
    ty,tx=divmod(tile,4)
    patch=pix[ty*T:(ty+1)*T,tx*T:(tx+1)*T,:3]
    patch[outer & ~cut]=color

ochre=(.68,.49,.24); ink=(.44,.34,.21)
crescent_paint(8,127,171,42,ochre)
for x in (69,128,184):
    stroke(8,[(x,41),(x,89),(x+13,75),(x,66),(x-10,55)],ochre,3)
for tile in (5,9):
    stroke(tile,[(20,20),(20,236),(236,236),(236,20),(20,20)],ink,1)
    if tile==5:
        crescent_paint(tile,126,167,42,ink)
    else:
        stroke(tile,[(128+57*cos(a),159+57*sin(a)) for a in np.linspace(0,2*pi,17)],ink,1)
        for a in np.arange(0,2*pi,pi/4):
            stroke(tile,[(128+18*cos(a),159+18*sin(a)),(128+68*cos(a),159+68*sin(a))],ink,1)
        stroke(tile,[(128,220),(112,159),(128,98),(144,159),(128,220)],ink,1)
    for j in range(4):
        yy=43+j*12
        for k in range(6):
            xx=38+k*29
            stroke(tile,[(xx,yy),(xx+8,yy+3),(xx+17,yy+1)],ink,1)
for tile in (2,13):
    for x in (39,105,187,229):
        stroke(tile,[(x+4*sin(y/45),y) for y in range(10,248,8)],(.25,.16,.085),1)
for tile in (0,11):
    stroke(tile,[(25,233),(101,237),(165,230)],(.64,.65,.57),2)
img=bpy.data.images.new('Moonwell_PaintedAtlas_1024',width=S,height=S,alpha=True)
img.pixels.foreach_set(pix.ravel())
img.filepath_raw=OUT+'/Moonwell_PaintedAtlas.png'; img.file_format='PNG'; img.save()
mat=bpy.data.materials.new('Moonwell_Painted_Atlas'); mat.use_nodes=True
bs=mat.node_tree.nodes.get('Principled BSDF'); bs.inputs['Roughness'].default_value=.82
tex=mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=img; tex.interpolation='Linear'
mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
magic=bpy.data.materials.new('Moonwell_Crystal_Emission'); magic.use_nodes=True
mb=magic.node_tree.nodes.get('Principled BSDF')
mb.inputs['Base Color'].default_value=(.30,.09,.56,1)
mb.inputs['Roughness'].default_value=.38
mb.inputs['Emission Color'].default_value=(.35,.075,.8,1)
mb.inputs['Emission Strength'].default_value=.85

def mesh(name,verts,faces,tile=0,parent=root,uvs=None,material=mat):
    me=bpy.data.meshes.new(name+'_Mesh'); me.from_pydata(verts,[],faces); me.update()
    ob=bpy.data.objects.new(name,me); asset.objects.link(ob); ob.parent=parent
    me.materials.append(material)
    uv=me.uv_layers.new(name='AtlasUV')
    for p in me.polygons:
        coords=[Vector(verts[i]) for i in p.vertices]
        ax=max(range(3),key=lambda d:abs(p.normal[d]))
        axes=[d for d in range(3) if d!=ax]
        lo=[min(v[d] for v in coords) for d in axes]
        hi=[max(v[d] for v in coords) for d in axes]
        tid=tile[p.index] if isinstance(tile,list) else tile
        ty,tx=divmod(tid,4)
        for j,li in enumerate(p.loop_indices):
            if uvs is not None: u,v=uvs[p.vertices[j]]
            else:
                u=(coords[j][axes[0]]-lo[0])/max(hi[0]-lo[0],.00001)
                v=(coords[j][axes[1]]-lo[1])/max(hi[1]-lo[1],.00001)
            uv.data[li].uv=((tx+.045+.91*u)/4,(ty+.045+.91*v)/4)
    return ob

def bevel(ob,w=.015):
    mod=ob.modifiers.new('Handmade edge bevel','BEVEL'); mod.width=w; mod.segments=1
    mod.affect='EDGES'
    return ob

def box(name,loc,size,tile=0,parent=root,b=.01):
    x,y,z=[v/2 for v in size]
    vs=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
    ob=mesh(name,vs,[(3,2,1,0),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)],tile,parent)
    ob.location=loc
    if b: bevel(ob,b)
    return ob

def cylinder(name,r,z0,z1,n=8,tile=0,center=(0,0),r2=None):
    r2=r if r2 is None else r2
    vs=[(center[0]+rr*cos(2*pi*i/n+pi/8),center[1]+rr*sin(2*pi*i/n+pi/8),z) for rr,z in [(r,z0),(r2,z1)] for i in range(n)]
    fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,vs,fs,tile)

# Main pedestal and timber outriggers.
bevel(cylinder('Foundation_Stone',.59,.055,.19,tile=0),.02)
ped=cylinder('Olive_Fresco_Pedestal',.54,.18,.95,tile=1,r2=.57)
bevel(ped,.013)
bevel(cylinder('Upper_Pedestal_Collar',.65,.86,1.015,tile=1),.025)
for idx,(sx,sy) in enumerate([(-1,-1),(1,-1),(-1,1),(1,1)]):
    ob=box('Timber_Foot_%02d'%idx,(sx*.49,sy*.40,.14),(.57,.28,.25),2,b=.035)
    ob.rotation_euler[2]=sx*sy*-.27
    strap=box('Forged_Upright_%02d'%idx,(sx*.415,sy*.405,.55),(.105,.09,.78),3,b=.013)
    for z in (.27,.83):
        cuff=box('Iron_Cuff_%02d_%.2f'%(idx,z),(sx*.415,sy*.415,z),(.18,.125,.16),3,b=.018)
        riv=box('Rivet_%02d_%.2f'%(idx,z),(sx*.415,sy*.489,z),(.052,.025,.052),11,b=.005)
        riv.rotation_euler[1]=pi/4
# Front painted panel; follows the flat front of the octagonal pedestal.
panel=mesh('Painted_Crescent_and_Runes',[(-.24,-.527,.24),(.24,-.527,.24),(.24,-.527,.86),(-.24,-.527,.86)],[(0,1,2,3)],8,uvs=[(0,0),(1,0),(1,1),(0,1)])

# A genuine recessed well with eight distinct rim stones.
cylinder('Well_Basin_Outside',.69,.93,1.035,tile=12)
well=cylinder('Violet_Well_Surface',.47,1.025,1.037,n=16,tile=7)
well.data.materials.clear();well.data.materials.append(magic)
for i in range(8):
    a0=pi/8+i*pi/4+.009; a1=pi/8+(i+1)*pi/4-.009
    vs=[(r*cos(a),r*sin(a),z) for z in (.985,1.15) for r,a in ((.49,a0),(.90,a0),(.90,a1),(.49,a1))]
    ob=mesh('Rim_Stone_%02d'%i,vs,[(3,2,1,0),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)],0 if i%3 else 11)
    bevel(ob,.015)
for i in range(4):
    a=pi/4+i*pi/2
    band=box('Ochre_Rim_Inlay_%02d'%i,(.69*cos(a),.69*sin(a),1.153),(.39,.032,.004),6,b=0)
    band.rotation_euler[2]=a

# Stone crescent: a tapered, segmented arch in the XZ plane, supported at back-left.
angles=np.linspace(math.radians(224),math.radians(79),10)
for i in range(9):
    aa,bb=angles[i]-.004,angles[i+1]+.004
    def point(a,outer,y):
        t=(math.radians(224)-a)/math.radians(145)
        width=.225*(1-t)+.063*t
        rad=.73+(width*.5 if outer else -width*.5)
        return (.04+rad*cos(a),y,1.57+rad*sin(a))
    vs=[point(a,o,y) for y in (.29,.49) for a,o in ((aa,False),(aa,True),(bb,True),(bb,False))]
    ob=mesh('Crescent_Stone_%02d'%i,vs,[(3,2,1,0),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)],11 if i%3 else 0)
    bevel(ob,.012)
socket=box('Crescent_Iron_Socket',(-.475,.385,1.16),(.28,.27,.23),3,b=.018)
socket.rotation_euler[1]=-.42
for x,z in [(-.49,1.19),(-.41,1.13)]:
    stud=box('Socket_Rivet',(x,.237,z),(.06,.025,.06),11,b=.005);stud.rotation_euler[1]=pi/4
for i in (3,7):
    a=angles[i]
    w=.225*(1-i/9)+.063*i/9
    band=box('Crescent_Ochre_Band_%d'%i,(.04+.73*cos(a),.282,1.57+.73*sin(a)),(w+.025,.012,.038),6,b=.002)
    band.rotation_euler[1]=-a

def crystal(name,loc,r,h,lean):
    n=5
    vs=[(r*cos(2*pi*i/n),r*sin(2*pi*i/n),.035) for i in range(n)]
    vs +=[(r*.82*cos(2*pi*i/n+.12),r*.82*sin(2*pi*i/n+.12),h*.68) for i in range(n)]
    vs +=[(0,0,0),(.02,-.01,h)]
    fs=[(10,(i+1)%n,i) for i in range(n)]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]+[(i+n,(i+1)%n+n,11) for i in range(n)]
    ob=mesh(name,vs,fs,15,material=magic);ob.location=loc;ob.rotation_euler=lean
    return ob
crystal('Crystal_Left',(-.29,.01,1.034),.10,.33,(.06,-.23,.2))
crystal('Crystal_Right',(.29,.055,1.034),.086,.29,(-.12,.22,0))

# Floating book: covers, page blocks and the turning leaf remain independent.
book=bpy.data.objects.new('Spellbook_Hover',None);asset.objects.link(book);book.parent=root
book.location=(.055,-.14,1.59);book.rotation_euler=(math.radians(16),math.radians(-4),math.radians(-5))
book['animation']='120-frame loop at 30 fps; page turns autonomously; floor-root static'
def wing(name,side,width,length,z,thick,tile):
    vs=[]
    for dz in (0,thick):
        for x,y in [(0,-length/2),(width,-length/2),(width,length/2),(0,length/2)]:
            vs.append((side*x,y,z+dz+.13*x/.54))
    fs=[(3,2,1,0),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)]
    if side<0:fs=[tuple(reversed(f)) for f in fs]
    ob=mesh(name,vs,fs,tile,parent=book)
    bevel(ob,.008)
    return ob
for side in (-1,1):
    wing('Leather_Cover_'+str(side),side,.56,.73,-.066,.031,4)
    wing('Parchment_Block_'+str(side),side,.515,.66,-.028,.071,10)
    # Printed page plane with full atlas tile assigned explicitly.
    vs=[(side*.018,-.317,.052),(side*.505,-.317,.052+.13*.505/.54),(side*.505,.317,.052+.13*.505/.54),(side*.018,.317,.052)]
    fs=[(0,1,2,3)] if side>0 else [(3,2,1,0)]
    ob=mesh('Inscribed_Page_'+str(side),vs,fs,9 if side>0 else 5,book,[(0,0),(1,0),(1,1),(0,1)])
    for y in (-.31,.31):
        clasp=box('Book_Brass_Corner',(side*.50,y,.076),(.09,.095,.025),6,book,.007)
        clasp.rotation_euler[1]=-side*.235
    for j in range(3):
        line=box('Page_Edge_Line',(side*.264,-.332,-.013+j*.018+.064),(.485,.003,.002),5,book,0)
        line.rotation_euler[1]=-side*.235
spine=box('Leather_Spine',(0,0,-.052),(.064,.735,.064),4,book,.018)

# Closed thin leaf with 8 lengthwise divisions; shape keys carry the page turn.
NU,NV=8,4
def page_coords(theta,sink=0):
    vv=[]
    for layer in (0,1):
        for j in range(NV+1):
            y=-.311+.622*j/NV
            for i in range(NU+1):
                u=i/NU
                curl=.075*sin(pi*u)*sin(theta)
                x=.497*u*cos(theta)-curl*sin(theta)
                z=.063+.13*.497*u/.54+.497*u*sin(theta)+curl*cos(theta)-sink+.0015*layer
                vv.append((x,y+.017*sin(pi*u)*sin(theta)*sin(j*pi/NV),z))
    return vv
NN=(NU+1)*(NV+1)
fs=[]
for layer in (0,1):
    for j in range(NV):
        for i in range(NU):
            a=layer*NN+j*(NU+1)+i;f=(a,a+1,a+NU+2,a+NU+1)
            fs.append(f if layer else tuple(reversed(f)))
boundary=list(range(NU+1))+[j*(NU+1)+NU for j in range(1,NV+1)]+[NV*(NU+1)+i for i in range(NU-1,-1,-1)]+[j*(NU+1) for j in range(NV-1,0,-1)]
for a,b in zip(boundary,boundary[1:]+boundary[:1]): fs.append((a,b,b+NN,a+NN))
uvs=[(i/NU,j/NV) for layer in (0,1) for j in range(NV+1) for i in range(NU+1)]
leaf=mesh('Spellbook_Turning_Page',page_coords(0),fs,9,book,uvs)
leaf.shape_key_add(name='Basis')
poses=[('Lift',.22*pi,0),('Upright',.5*pi,0),('Fall',.79*pi,0),('Left',pi,0),('Reset_Left',pi,.025),('Reset_Right',0,.025)]
keys=[]
for name,a,sink in poses:
    key=leaf.shape_key_add(name=name)
    for v,co in zip(key.data,page_coords(a,sink)):v.co=co
    keys.append(key)
timeline=[(1,None),(18,None),(32,0),(47,1),(62,2),(77,3),(82,4),(83,5),(89,None),(121,None)]
for frame,index in timeline:
    for i,key in enumerate(keys):
        key.value=1 if i==index else 0;key.keyframe_insert('value',frame=frame)
reset=leaf.shape_key_add(name='Reset_Inside_Spine')
for idx,(v,co) in enumerate(zip(reset.data,page_coords(0))):
    v.co=(co[0]*.002,co[1],-.01+.0015*(idx//NN))
for frame,index in timeline:
    reset.value=0;reset.keyframe_insert('value',frame=frame)
for key in keys:
    key.value=0;key.keyframe_insert('value',frame=82.5)
reset.value=1;reset.keyframe_insert('value',frame=82.5)
for f,z in [(1,1.59),(31,1.62),(61,1.59),(91,1.565),(121,1.59)]:
    book.location.z=z;book.keyframe_insert('location',frame=f)
scene.render.fps=30;scene.frame_start=1;scene.frame_end=120;scene.frame_set(47)
for ob in asset.objects:
    if ob.parent==root and ob!=book: ob.location.z-=.015

# Neutral studio is separate from the exportable asset collection.
def studio_obj(name,data):
    ob=bpy.data.objects.new(name,data);studio.objects.link(ob);return ob
groundmat=bpy.data.materials.new('Preview_Backdrop');groundmat.diffuse_color=(.17,.185,.19,1)
groundmat.use_nodes=True;groundmat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.17,.185,.19,1)
me=bpy.data.meshes.new('Preview_Ground_Mesh');me.from_pydata([(-200,-200,-.004),(200,-200,-.004),(200,200,-.004),(-200,200,-.004)],[],[(0,1,2,3)])
ground=studio_obj('Preview_Ground',me);me.materials.append(groundmat)
def aim(ob,point):ob.rotation_euler=(Vector(point)-ob.location).to_track_quat('-Z','Y').to_euler()
camd=bpy.data.cameras.new('Moonwell_Camera');cam=studio_obj('Moonwell_Camera',camd)
cam.location=(3.25,-5.6,3.05);aim(cam,(0,0,1.16));camd.type='ORTHO';camd.ortho_scale=3.15;scene.camera=cam
for name,loc,power,color,size in [('Key',(-3,-4,6),650,(1,.86,.69),4),('Fill',(4,-1,3.5),380,(.70,.80,1),3),('Rim',(0,3.5,4.5),750,(.83,.77,1),3)]:
    ld=bpy.data.lights.new('Moonwell_'+name,'AREA');ld.energy=power;ld.color=color;ld.shape='DISK';ld.size=size
    ob=studio_obj('Moonwell_'+name,ld);ob.location=loc;aim(ob,(0,0,1))
world=bpy.data.worlds.new('Moonwell_Studio_World');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.20,.23,.29,1);world.node_tree.nodes['Background'].inputs[1].default_value=.4;scene.world=world
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=OUT+'/Moonwell_Preview.png'
scene.view_settings.view_transform='AgX'
for ob in bpy.context.selected_objects:ob.select_set(False)
root.select_set(True);bpy.context.view_layer.objects.active=root
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_perspective='CAMERA'
        area.spaces.active.shading.type='MATERIAL'
scene['Moonwell_Model_Notes']='Only Moonwell_Shrine collection is asset geometry. Preview studio excluded from game export. Frame 47 shows page mid-turn. Shared 1024 atlas plus crystal material.'
exec(compile(open(OUT+'/smooth_animation.py', encoding='utf-8').read(), 'smooth_animation.py', 'exec'))
bpy.context.view_layer.update()
result={'created':len(asset.objects),'scene':scene.name,'atlas':img.filepath_raw,'animation_frames':[1,120],'render':scene.render.filepath}
