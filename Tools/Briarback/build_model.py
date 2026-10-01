"""Original Briarback model and sounds. Execute via the official Blender MCP.
Creates a separate scene, preserves existing data, refuses to overwrite v001.
"""
import bpy, math, json, random, struct, wave
from pathlib import Path
from mathutils import Vector

OUT = Path('C:/Users/linco/Mugg/Artifacts/Briarback/v001')
if (OUT / 'Briarback.blend').exists() or any(a.name.startswith('BB_') for a in bpy.data.actions):
    raise RuntimeError('Existing Briarback source preserved; choose a fresh version before rebuilding.')
OUT.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes.new('Briarback_v001')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
scene.render.fps = 30
collection = bpy.data.collections.new('BB_Character')
scene.collection.children.link(collection)
colors = [(.19,.12,.075,1),(.31,.20,.12,1),(.43,.30,.18,1),(.57,.41,.25,1),
          (.22,.29,.12,1),(.37,.43,.20,1),(.81,.73,.51,1),(.075,.065,.05,1)]
image = bpy.data.images.new('BB_Palette', width=64, height=8, alpha=True)
image.pixels = [c for y in range(8) for x in range(64) for c in colors[x//8]]
image.filepath_raw = str(OUT / 'BB_Palette.png')
image.file_format = 'PNG'
image.save(); image.pack()
palette = bpy.data.materials.new('BB_Palette_Matte'); palette.use_nodes = True
shader = palette.node_tree.nodes.get('Principled BSDF')
shader.inputs['Roughness'].default_value = .93
tex = palette.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = image; tex.interpolation = 'Closest'
palette.node_tree.links.new(tex.outputs['Color'], shader.inputs['Base Color'])
amber = bpy.data.materials.new('BB_Amber'); amber.use_nodes = True
shader = amber.node_tree.nodes.get('Principled BSDF')
shader.inputs['Base Color'].default_value = (1,.47,.035,1)
shader.inputs['Emission Color'].default_value = (1,.22,.005,1)
shader.inputs['Emission Strength'].default_value = 1.4

verts, faces, swatches, weights, slots = [], [], [], [], []
def face(indices, color, glow=False):
    faces.append(tuple(indices)); swatches.append(color); slots.append(int(glow))

def segment(a, b, radii, bone, color=1, sides=6, depth=1, glow=False):
    a,b = Vector(a),Vector(b)
    direction=(b-a).normalized()
    ref=Vector((0,1,0)) if abs(direction.y)<.9 else Vector((1,0,0))
    u=direction.cross(ref).normalized(); v=direction.cross(u).normalized(); start=len(verts)
    for t,r in zip((0,.5,1),radii):
        for i in range(sides):
            angle=2*math.pi*i/sides+math.pi/4
            verts.append(tuple(a.lerp(b,t)+r*(u*math.cos(angle)+v*math.sin(angle)*depth)))
            weights.append(bone)
    face([start+i for i in reversed(range(sides))],color,glow)
    for ring in range(2):
        for i in range(sides):
            j=(i+1)%sides
            face([start+ring*sides+i,start+ring*sides+j,start+(ring+1)*sides+j,start+(ring+1)*sides+i],
                 min(3,color+(i%3==1)) if color<4 else color,glow)
    face([start+2*sides+i for i in range(sides)],color,glow)

def mass(center, scale, bone, color=1, sides=10):
    # Closed latitude rings with pole fans, no degenerate duplicated pole vertices.
    cx,cy,cz=center; sx,sy,sz=scale; start=len(verts)
    for lat in (-60,-30,0,30,60):
        a=math.radians(lat)
        for i in range(sides):
            t=2*math.pi*i/sides
            verts.append((cx+sx*math.cos(a)*math.cos(t),cy+sy*math.cos(a)*math.sin(t),cz+sz*math.sin(a)))
            weights.append(bone)
    bottom=len(verts);verts.append((cx,cy,cz-sz));weights.append(bone)
    top=len(verts);verts.append((cx,cy,cz+sz));weights.append(bone)
    for i in range(sides):
        j=(i+1)%sides
        face([bottom,start+j,start+i],color)
        for ring in range(4):
            face([start+ring*sides+i,start+ring*sides+j,start+(ring+1)*sides+j,start+(ring+1)*sides+i],
                 min(3,color+(ring>=2)) if color<4 else color)
        face([start+4*sides+i,start+4*sides+j,top],min(3,color+1) if color<4 else color)

def box(center, scale, bone, color):
    start=len(verts)
    for z in (-1,1):
        for y in (-1,1):
            for x in (-1,1):
                verts.append(tuple(center[i]+scale[i]*p for i,p in enumerate((x,y,z))));weights.append(bone)
    for f in ((0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)):
        face([start+i for i in f],color)

bones=[('Root',(0,0,0),(0,0,.25),None),('Body',(0,.2,.85),(0,.2,1.15),'Root'),
       ('Head',(0,-.62,1.05),(0,-.62,1.42),'Body'),('Snout',(0,-1.1,.88),(0,-1.1,1.1),'Head'),
       ('Tail',(0,.88,1.1),(0,1.22,1.2),'Body')]
mass((0,.15,1.04),(.61,1.02,.63),'Body',1,12)
mass((0,-.69,1.02),(.51,.55,.49),'Head',2,10)
mass((0,-1.18,.81),(.39,.37,.27),'Snout',1,8)
mass((0,-1.45,.83),(.29,.07,.17),'Snout',7,8)
# Broad asymmetrical shingle plates along the shoulder and flanks.
for side in (-1,1):
    for j in range(3):
        segment((side*.48,-.38+j*.42,1.05),(side*.46,-.23+j*.42,1.42),(.22,.28,.17),'Body',4+(j%2),5,.45)
    # Ears, amber inset eyes, two nostrils and curved ivory tusks.
    segment((side*.34,-.64,1.3),(side*.58,-.42,1.77),(.16,.16,.025),'Head',1,5,.6)
    mass((side*.425,-.97,1.18),(.105,.075,.08),'Head',7,6)
    segment((side*.463,-1.012,1.18),(side*.472,-1.035,1.18),(.057,.061,.055),'Head',6,6,1,True)
    mass((side*.13,-1.507,.88),(.047,.026,.04),'Snout',7,6)
    segment((side*.29,-1.12,.69),(side*.48,-1.42,.75),(.13,.12,.09),'Head',6,6)
    segment((side*.48,-1.42,.75),(side*.45,-1.57,1.1),(.09,.07,.009),'Head',6,6)
    for front in (True,False):
        label=('Fore' if front else 'Hind')+('.R' if side<0 else '.L')
        y=-.48 if front else .7
        x=side*(.41 if front else .43)
        hip=(x,y,.94); knee=(x*1.09,y+(.09 if front else -.08),.43); ankle=(x*1.12,y-.04,.16)
        bones.extend([(label,hip,knee,'Body'),('Shin'+label,knee,ankle,label),
                      ('Hoof'+label,ankle,(ankle[0],y-.20,.11),'Shin'+label)])
        segment(hip,knee,(.21,.19,.12),label,1,6)
        segment(knee,ankle,(.12,.115,.095),'Shin'+label,0,6)
        box((ankle[0],y-.07,.11),(.145,.20,.11),'Hoof'+label,7)
        # Ivory cleft bands keep grounded hooves readable.
        box((ankle[0],y-.253,.105),(.09,.012,.07),'Hoof'+label,3)
for j in range(7):
    y=-.55+j*.225
    basez=1.50+.12*math.sin(j*math.pi/6)
    segment((0,y,basez-.16),((.055 if j%2 else -.04),y+.12,basez+.35-(j*.024)),(.14,.1,.008),'Body',2,5,.72)
segment((0,.92,1.15),(0,1.30,1.23),(.13,.095,.025),'Tail',0,6)

mesh=bpy.data.meshes.new('BB_BodyMesh');mesh.from_pydata(verts,[],faces);mesh.update()
body=bpy.data.objects.new('BB_Body',mesh);collection.objects.link(body)
mesh.materials.append(palette);mesh.materials.append(amber)
uv=mesh.uv_layers.new(name='PaletteUV')
for poly,color,slot in zip(mesh.polygons,swatches,slots):
    poly.material_index=slot;poly.use_smooth=False
    for i,loop in enumerate(poly.loop_indices):
        t=2*math.pi*i/len(poly.loop_indices)
        uv.data[loop].uv=((color+.5)/8+.025*math.cos(t),.5+.2*math.sin(t))
arm=bpy.data.armatures.new('BB_Armature');rig=bpy.data.objects.new('BB_Rig',arm);collection.objects.link(rig)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
for name,head,tail,parent in bones:
    bone=arm.edit_bones.new(name);bone.head=head;bone.tail=tail
    if parent:bone.parent=arm.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
for name,*_ in bones:
    group=body.vertex_groups.new(name=name);indices=[i for i,b in enumerate(weights) if b==name]
    if indices:group.add(indices,1,'REPLACE')
body.parent=rig;body.modifiers.new('BB_Skin','ARMATURE').object=rig;rig.show_in_front=True
clips=[]
def action(name,duration,keys,loop=False):
    act=bpy.data.actions.new('BB_'+name);act.use_fake_user=True;rig.animation_data_create().action=act
    for seconds,rotations,locations in keys:
        for bone in rig.pose.bones:
            bone.rotation_mode='XYZ';bone.rotation_euler=tuple(math.radians(a) for a in rotations.get(bone.name,(0,0,0)))
            bone.location=locations.get(bone.name,(0,0,0));bone.scale=(1,1,1)
            bone.keyframe_insert('rotation_euler',frame=1+round(seconds*30),group=bone.name)
            bone.keyframe_insert('location',frame=1+round(seconds*30),group=bone.name)
    clips.append({'name':act.name,'duration':duration,'loop':loop})
    return act
idle=action('Idle',2,[(0,{},{}),(1,{'Head':(3,0,0),'Tail':(0,0,8)},{}),(2,{},{})],True)
def gait(t,charge=False):
    amp=32 if charge else 20
    rots={}
    for label,offset in [('Fore.R',0),('Hind.L',0),('Fore.L',math.pi),('Hind.R',math.pi)]:
        swing=math.sin(t*2*math.pi+offset)
        rots[label]=(amp*swing,0,0)
        rots['Shin'+label]=(-max(0,swing)*22,0,0)
    rots['Head']=(12 if charge else -3,0,0)
    return rots
action('Walk',.8,[(i*.1,gait(i/8),{}) for i in range(9)],True)
action('Windup',.9,[(0,{},{}),(.3,{'Head':(18,0,0),'Fore.R':(-8,0,0),'Fore.L':(-8,0,0)},{}),
    (.65,{'Head':(22,0,0),'Fore.R':(-8,0,0),'Fore.L':(-8,0,0),'Tail':(0,0,15)},{}),
    (.9,{'Head':(22,0,0),'Fore.R':(-8,0,0),'Fore.L':(-8,0,0)}, {})])
action('Charge',.4,[(i*.05,gait(i/8,True),{}) for i in range(9)],True)
action('Recover',1.4,[(0,{'Head':(22,0,0)},{}),(.3,{'Head':(-10,0,0),'Tail':(0,0,-20)},{}),
    (.9,{'Head':(-6,0,10)},{}),(1.4,{},{})])
action('Hurt',.3,[(0,{},{}),(.1,{'Head':(-12,0,0),'Body':(0,0,6)},{}),(.3,{},{})])
action('Death',2,[(0,{},{}),(.4,{'Head':(15,0,0)},{}),
    (1,{'Body':(0,0,70),'Head':(10,0,0)},{'Body':(0,-.50,0)}),
    (2,{'Body':(0,0,70),'Head':(10,0,0)},{'Body':(0,-.50,0)})])
rig.animation_data.action=idle;scene.frame_set(1);bpy.context.view_layer.update()
mesh.calc_loop_triangles()
# Validate original topology, full skin assignment, UVs, export budget and bounds.
import bmesh
bm=bmesh.new();bm.from_mesh(mesh)
bad_edges=sum(not e.is_manifold for e in bm.edges);bm.free()
assert bad_edges==0, f'{bad_edges} non-manifold edges'
assert len(mesh.loop_triangles)<3000
assert all(len(v.groups)==1 and abs(v.groups[0].weight-1)<1e-5 for v in mesh.vertices)
assert min(v.co.z for v in mesh.vertices)==0
manifest={'name':'Briarback','provenance':'Original locally authored procedural model and synthesized sounds; no external assets',
    'triangles':len(mesh.loop_triangles),'vertices':len(mesh.vertices),'bones':len(bones),'materials':2,
    'dimensions_m':list(body.dimensions),'non_manifold_edges':bad_edges,'clips':clips,'root_motion':False,
    'forward':'-Y Blender; FBX -Z forward/Y up, Unity +Z','uv':'one compact eight-swatch palette',
    'visual_status':'Blender preview required; Unity gameplay unverified'}
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2))
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
# NLA export scopes clips to this rig, even when unrelated assets exist in the open file.
rig.animation_data.action=None
for info in clips:
    track=rig.animation_data.nla_tracks.new();track.name=info['name']
    track.strips.new(info['name'],1,bpy.data.actions[info['name']])
bpy.ops.export_scene.fbx(filepath=str(OUT/'Briarback.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},
    axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,
    bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0,
    path_mode='COPY',embed_textures=True)
for track in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(track)
rig.animation_data.action=idle;scene.frame_set(1)

# Small original dry, woody creature sounds; deterministic, no provider dependencies.
for name,duration,f0 in [('Windup',.85,88),('Charge',.5,65),('Hurt',.25,145),('Death',1.4,62)]:
    rng=random.Random(401+len(name));rate=22050;data=[];phase=0;filtered=0
    for i in range(round(rate*duration)):
        t=i/rate;p=t/duration
        pitch=f0*(1.25-.55*p);phase+=2*math.pi*pitch/rate
        noise=rng.uniform(-1,1);filtered=.82*filtered+.18*noise
        envelope=min(1,t/.025)*((1-p)**1.5)
        if name=='Windup':envelope*=.55+.45*math.sin(2*math.pi*7*t)**2
        sample=(.40*math.sin(phase)+.16*math.sin(phase*2.03)+.36*filtered)*envelope
        data.append(struct.pack('<h',int(max(-1,min(1,sample))*.78*32767)))
    with wave.open(str(OUT/f'BB_{name}.wav'),'wb') as sound:
        sound.setnchannels(1);sound.setsampwidth(2);sound.setframerate(rate);sound.writeframes(b''.join(data))

world=bpy.data.worlds.new('BB_PreviewWorld');world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.32,.35,.39,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.4;scene.world=world
ground=bpy.data.materials.new('BB_PreviewGround');ground.diffuse_color=(.22,.24,.23,1)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.008));bpy.context.object.name='BB_PreviewGround';bpy.context.object.data.materials.append(ground)
for name,pos,energy,size in [('Key',(-3,-4,6),700,4),('Fill',(4,-1,3),400,3),('Rim',(0,4,5),900,3)]:
    data=bpy.data.lights.new('BB_'+name,'AREA');data.energy=energy;data.shape='DISK';data.size=size
    obj=bpy.data.objects.new('BB_'+name,data);scene.collection.objects.link(obj);obj.location=pos
    obj.rotation_euler=(Vector((0,0,.9))-obj.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('BB_PreviewCamera');camera=bpy.data.objects.new('BB_PreviewCamera',data);scene.collection.objects.link(camera)
camera.location=(4,-6,3.2);camera.rotation_euler=(Vector((0,-.1,.9))-camera.location).to_track_quat('-Z','Y').to_euler()
data.type='ORTHO';data.ortho_scale=3.8;scene.camera=camera
scene.render.engine='CYCLES';scene.cycles.samples=24
scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(OUT/'preview.png');scene.view_settings.view_transform='AgX'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Briarback.blend'))
result={'output':str(OUT),**manifest}
