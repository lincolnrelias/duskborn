"""Original Bramblekin, built in a separate scene through the official Blender MCP.
No existing scene/data is removed. Versioned source and export refuse overwrite.
Rigid mesh parts support repository-native procedural animation and jointed death.
"""
import bpy, math, json
from pathlib import Path
from mathutils import Vector

OUT = Path('C:/Users/linco/Mugg/Artifacts/Bramblekin/v001')
if (OUT / 'Bramblekin.blend').exists():
    raise RuntimeError('Source already exists; choose a fresh version before rebuilding.')
OUT.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes.new('Bramblekin_v001')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
palette = [(.22,.33,.13,1),(.36,.46,.20,1),(.49,.57,.28,1),
           (.20,.12,.065,1),(.36,.23,.11,1),(.52,.36,.17,1),
           (.65,.50,.28,1),(.11,.105,.065,1),(.96,.68,.18,1),(.80,.73,.51,1)]
image = bpy.data.images.new('BK_Palette', width=100, height=10)
image.pixels = [c for y in range(10) for x in range(100) for c in palette[x//10]]
image.filepath_raw = str(OUT/'BK_Palette.png'); image.file_format = 'PNG'; image.save(); image.pack()
material = bpy.data.materials.new('BK_Palette_Matte'); material.use_nodes = True
shader = material.node_tree.nodes.get('Principled BSDF'); shader.inputs['Roughness'].default_value = .95
tex = material.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = image; tex.interpolation = 'Closest'
material.node_tree.links.new(tex.outputs['Color'], shader.inputs['Base Color'])
parts = {}

def part(name, pivot):
    parts[name] = {'pivot':Vector(pivot), 'verts':[], 'faces':[], 'colors':[]}
    return parts[name]

def face(p, indices, color):
    p['faces'].append(indices); p['colors'].append(color)

def mass(p, center, scale, color, sides=8):
    start = len(p['verts']); center = Vector(center)
    for angle in (-60,-30,0,30,60):
        a = math.radians(angle)
        for i in range(sides):
            t = 2*math.pi*i/sides
            p['verts'].append(center+Vector((scale[0]*math.cos(a)*math.cos(t),scale[1]*math.cos(a)*math.sin(t),scale[2]*math.sin(a))))
    bottom=len(p['verts']); p['verts'].append(center-Vector((0,0,scale[2])))
    top=len(p['verts']); p['verts'].append(center+Vector((0,0,scale[2])))
    for i in range(sides):
        j=(i+1)%sides
        face(p,[bottom,start+j,start+i],color)
        for ring in range(4):
            face(p,[start+ring*sides+i,start+ring*sides+j,start+(ring+1)*sides+j,start+(ring+1)*sides+i],color+(1 if ring>=2 and color in (0,3) else 0))
        face(p,[start+4*sides+i,start+4*sides+j,top],color)

def segment(p,a,b,r0,r1,color,sides=6):
    a,b=Vector(a),Vector(b); direction=(b-a).normalized()
    ref=Vector((0,1,0)) if abs(direction.y)<.9 else Vector((1,0,0))
    u=direction.cross(ref).normalized(); v=direction.cross(u).normalized(); start=len(p['verts'])
    for center,radius in ((a,r0),(b,r1)):
        for i in range(sides):
            t=2*math.pi*i/sides
            p['verts'].append(center+radius*(u*math.cos(t)+v*math.sin(t)))
    face(p,[start+i for i in reversed(range(sides))],color)
    for i in range(sides):
        j=(i+1)%sides
        face(p,[start+i,start+j,start+sides+j,start+sides+i],color+(1 if i%3==1 and color in (0,3,4) else 0))
    face(p,[start+sides+i for i in range(sides)],color)

body=part('Body',(0,0,.65)); head=part('Head',(0,-.06,1.02))
mass(body,(0,.03,.72),(.27,.21,.32),0,10)
# Woven leather apron, collar and diagonal bark strap: large readable shapes.
mass(body,(0,.02,.54),(.31,.23,.19),3,10)
segment(body,(-.21,-.195,.98),(.20,-.21,.56),.042,.038,6)
mass(head,(0,-.08,1.17),(.31,.25,.26),1,10)
mass(head,(0,-.30,1.10),(.21,.13,.12),1,8)
mass(head,(0,-.37,1.20),(.065,.09,.06),0,6)
# Oversized pointed ears with a warm inset. No generic rectangular feet.
for side in (-1,1):
    segment(head,(side*.23,-.03,1.23),(side*.58,.02,1.39),.14,.008,0,5)
    segment(head,(side*.31,-.115,1.25),(side*.52,-.035,1.36),.062,.005,6,5)
    mass(head,(side*.125,-.300,1.24),(.10,.035,.068),7,6)
    mass(head,(side*.125,-.332,1.24),(.055,.012,.038),8,6)
    segment(head,(side*.065,-.405,1.04),(side*.066,-.412,1.11),.028,.002,9,5)
# Acorn cap: ochre rim and domed dark bark, with one offset woody stalk.
mass(head,(0,.015,1.40),(.33,.265,.12),3,10)
segment(head,(0,0,1.41),(-.07,.03,1.59),.05,.024,4,5)
for i in range(8):
    t=2*math.pi*i/8
    mass(head,(math.cos(t)*.30,math.sin(t)*.245,1.37),(.055,.05,.045),5,5)
for side,suffix in ((-1,'L'),(1,'R')):
    arm=part('Arm.'+suffix,(side*.26,0,.94))
    segment(arm,(side*.27,0,.94),(side*.40,-.02,.71),.115,.085,0)
    segment(arm,(side*.40,-.02,.71),(side*.43,-.12,.51),.085,.07,0)
    mass(arm,(side*.43,-.12,.49),(.10,.095,.105),1,6)
    for finger in range(3):
        segment(arm,(side*(.40+finger*.026),-.185,.49),(side*(.40+finger*.026),-.20,.42),.02,.015,1,5)
    leg=part('Leg.'+suffix,(side*.145,.015,.43))
    segment(leg,(side*.145,.015,.43),(side*.18,.03,.20),.13,.08,0)
    segment(leg,(side*.18,.03,.20),(side*.19,-.04,.065),.08,.065,0)
    mass(leg,(side*.19,-.08,.06),(.11,.15,.06),0,8)
    for toe in range(3):
        segment(leg,(side*(.14+toe*.048),-.15,.06),(side*(.14+toe*.048),-.24,.035),.027,.015,1,5)
    if suffix=='R':
        # One short knotted cudgel, held in the right fist, incorporated into the mesh.
        segment(arm,(.43,-.12,.38),(.43,-.12,.98),.035,.07,4,7)
        mass(arm,(.43,-.12,.91),(.095,.085,.14),3,7)
        segment(arm,(.43,-.12,.84),(.53,-.10,.95),.038,.018,4,5)

objects=[]
for name,p in parts.items():
    mesh=bpy.data.meshes.new('BK_'+name)
    mesh.from_pydata([tuple(v-p['pivot']) for v in p['verts']],[],p['faces']); mesh.update()
    obj=bpy.data.objects.new(name,mesh); scene.collection.objects.link(obj); obj.location=p['pivot']; objects.append(obj)
    mesh.materials.append(material); uv=mesh.uv_layers.new(name='PaletteUV')
    for polygon,color in zip(mesh.polygons,p['colors']):
        for loop in polygon.loop_indices: uv.data[loop].uv=((color+.5)/10,.5)
    # Recalculate outward normals for every closed component.
    import bmesh
    bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
    mesh.calc_loop_triangles()
    assert all(tri.area>1e-10 for tri in mesh.loop_triangles)
    assert len(mesh.uv_layers)==1 and all(len(poly.vertices)>=3 for poly in mesh.polygons)

for obj in objects: obj.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.export_scene.fbx(filepath=str(OUT/'Bramblekin.fbx'),use_selection=True,object_types={'MESH'},
    axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,bake_anim=False)
# A studio scene is retained with the editable parts for future posing and art passes.
world=bpy.data.worlds.new('BK_Studio'); scene.world=world; world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.085,.105,.12,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.6
camera_data=bpy.data.cameras.new('BK_Camera'); camera=bpy.data.objects.new('BK_Camera',camera_data); scene.collection.objects.link(camera)
camera.location=(2.4,-4,2.15); camera.rotation_euler=(Vector((0,0,.8))-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.type='ORTHO'; camera_data.ortho_scale=2.25; scene.camera=camera
for name,location,power,size in [('Key',(-3,-4,5),550,4),('Fill',(3,-1,3),300,3),('Rim',(1,3,4),450,3)]:
    data=bpy.data.lights.new('BK_'+name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size
    light=bpy.data.objects.new('BK_'+name,data); scene.collection.objects.link(light); light.location=location
    light.rotation_euler=(Vector((0,0,.8))-light.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES'; scene.cycles.samples=32
scene.render.resolution_x=768; scene.render.resolution_y=768; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'; scene.render.filepath=str(OUT/'preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Bramblekin.blend'))
bpy.ops.render.render(write_still=True)
manifest={'name':'Bramblekin','height_m':1.615,'parts':len(objects),'triangles':sum(len(o.data.loop_triangles) for o in objects),
          'materials':1,'uv':'ten swatch atlas with per-face value grouping','facing':'Blender -Y, Unity +Z',
          'animation':'runtime rigid-part procedural idle, walk, windup, swing, recovery; jointed ragdoll death',
          'engine_grounding':'awaiting imported pose sampling and gameplay slope checks'}
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2))
result=manifest
