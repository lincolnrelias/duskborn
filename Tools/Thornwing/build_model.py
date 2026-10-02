"""Execute through official Blender MCP; create a separate scene and preserve existing work."""
import bpy, math, json, hashlib
from pathlib import Path
from mathutils import Vector

OUT = Path('C:/Users/linco/Mugg/Artifacts/Thornwing/v002')
OUT.mkdir(parents=True, exist_ok=True)
previous = bpy.context.window.scene
scene = bpy.data.scenes.new('Thornwing_v002')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
collection = bpy.data.collections.new('TW_Character')
scene.collection.children.link(collection)
palette = [(0.19,.23,.16,1),(.31,.38,.20,1),(.48,.53,.28,1),(.68,.67,.39,1),
           (.22,.16,.28,1),(.40,.29,.43,1),(.62,.43,.51,1),(.88,.56,.15,1)]
image = bpy.data.images.new('TW_Palette', width=8, height=1)
image.pixels = [v for color in palette for v in color]
image.filepath_raw = str(OUT/'TW_Palette.png'); image.file_format = 'PNG'; image.save(); image.pack()
material = bpy.data.materials.new('TW_Palette'); material.use_nodes = True
bsdf = material.node_tree.nodes.get('Principled BSDF'); bsdf.inputs['Roughness'].default_value = .86
texture = material.node_tree.nodes.new('ShaderNodeTexImage'); texture.image = image; texture.interpolation = 'Closest'
material.node_tree.links.new(texture.outputs['Color'], bsdf.inputs['Base Color'])
amber = bpy.data.materials.new('TW_Amber'); amber.diffuse_color = (.95,.48,.06,1); amber.use_nodes = True
ab = amber.node_tree.nodes.get('Principled BSDF'); ab.inputs['Base Color'].default_value = (.95,.48,.06,1)
ab.inputs['Emission Color'].default_value = (1,.25,.015,1); ab.inputs['Emission Strength'].default_value = .5
objects = []

def admit(obj, name, color, glow=False):
    obj.name = name
    for c in list(obj.users_collection): c.objects.unlink(obj)
    collection.objects.link(obj); objects.append(obj)
    obj.data.materials.clear(); obj.data.materials.append(amber if glow else material)
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name='PaletteUV')
    for face in obj.data.polygons:
        # Broad authored highlight and underside shading, no noise.
        idx = max(0,min(7,color + (1 if face.normal.z > .45 and color < 6 else 0)))
        for loop in face.loop_indices: uv.data[loop].uv = ((idx+.5)/8,.5)
        face.use_smooth = False
    return obj

def ellipsoid(name, location, scale, color, glow=False, subdivisions=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=location)
    obj = bpy.context.object; obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return admit(obj,name,color,glow)

def rod(name, start, end, radius, color):
    start,end = Vector(start),Vector(end); delta=end-start
    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=radius, radius2=radius*.42, depth=delta.length,
                                   location=(start+end)/2)
    obj = bpy.context.object; obj.rotation_euler = delta.to_track_quat('Z','Y').to_euler()
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return admit(obj,name,color)

# Asset origin sits at body center; Unity supplies a bounded chest-height hover.
ellipsoid('Thorax',(0,0,0),(.20,.28,.20),1,subdivisions=2)
ellipsoid('Abdomen',(0,.34,-.04),(.17,.34,.16),4,subdivisions=2)
ellipsoid('Head',(0,-.29,.07),(.20,.17,.19),1,subdivisions=2)
for sign,label in [(-1,'L'),(1,'R')]:
    ellipsoid('Eye.'+label,(sign*.145,-.40,.13),(.095,.075,.10),7,True)
    rod('Antenna.'+label,(sign*.10,-.32,.20),(sign*.23,-.41,.49),.025,0)
    ellipsoid('AntennaTip.'+label,(sign*.23,-.41,.49),(.037,.036,.044),3)
    rod('Mandible.'+label,(sign*.08,-.43,-.04),(sign*.12,-.55,-.12),.042,3)
    rod('MandibleHook.'+label,(sign*.12,-.55,-.12),(sign*.03,-.59,-.07),.025,3)
    for i in range(3):
        y=-.15+i*.19
        rod('Leg%d.%s'%(i,label),(sign*.13,y,-.10),(sign*.28,y-.05,-.27),.024,0)
        rod('Claw%d.%s'%(i,label),(sign*.28,y-.05,-.27),(sign*.20,y-.15,-.34),.018,3)
    # Four wings per creature as thick, closed faceted leaf-like membranes.
    wingparts=[]
    for rear in [False,True]:
        anchor = Vector((sign*.16, .07 if rear else -.05,.08))
        outline=[(.02,-.02),(.35,-.28),(.72,-.42),(.95,-.27),(.78,.04),(.56,.22),(.22,.17)]
        if rear: outline=[(x*.78,y*.75+.35) for x,y in outline]
        verts=[tuple(anchor)]
        for x,y in outline: verts.append(tuple(Vector((sign*(.16+x),y,.07+(.06 if x>.65 else 0)))))
        verts += [(x,y,z-.028) for x,y,z in verts]
        n=8; faces=[]
        for i in range(1,n):
            j=1 if i==n-1 else i+1
            faces.extend([(0,i,j),(n,n+j,n+i),(i,n+i,n+j,j)])
        if sign < 0: faces=[tuple(reversed(face)) for face in faces]
        mesh=bpy.data.meshes.new('WingMembrane'); mesh.from_pydata(verts,[],faces); mesh.update()
        obj=bpy.data.objects.new('WingPart',mesh); collection.objects.link(obj)
        obj=admit(obj,'WingPart',4 if rear else 1)
        uv=obj.data.uv_layers.active
        for fi,face in enumerate(obj.data.polygons):
            idx=(5 if rear else 2) if fi%3==0 else (6 if rear else 3) if fi%3==2 else (4 if rear else 0)
            for loop in face.loop_indices: uv.data[loop].uv=((idx+.5)/8,.5)
        wingparts.append(obj)
    # Joining each side gives two cheap articulated transforms, no armature.
    bpy.ops.object.select_all(action='DESELECT')
    for obj in wingparts: obj.select_set(True)
    objects.remove(wingparts[1])
    bpy.context.view_layer.objects.active=wingparts[0]; bpy.ops.object.join()
    wing=wingparts[0]; wing.name='Wing.'+label
    bpy.context.scene.cursor.location=(sign*.16,0,.08); bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

scene.cursor.location=(0,0,0)
for name, parts in [('Body',[o for o in objects if not o.name.startswith(('Wing.','Eye.')) and o.name not in ['Head','Abdomen']]),
                    ('Eyes',[o for o in objects if o.name.startswith('Eye.')])]:
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts: obj.select_set(True)
    for obj in parts[1:]: objects.remove(obj)
    bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join()
    parts[0].name=name; bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
bpy.ops.object.select_all(action='DESELECT')
for obj in objects: obj.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.export_scene.fbx(filepath=str(OUT/'Thornwing.fbx'),use_selection=True,object_types={'MESH'},
                        apply_unit_scale=True,axis_forward='-Z',axis_up='Y',bake_anim=False,
                        path_mode='STRIP',use_mesh_modifiers=True)

# Separate neutral presentation stage; only character meshes are exported.
world=bpy.data.worlds.new('TW_Studio'); scene.world=world; world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.11,.13,.16,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.6
def light(name,location,power,size):
    data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size
    obj=bpy.data.objects.new(name,data); scene.collection.objects.link(obj); obj.location=location
    obj.rotation_euler=(-obj.location).to_track_quat('-Z','Y').to_euler()
light('TW_Key',(2,-3,4),350,4); light('TW_Fill',(-3,-1,2),180,3); light('TW_Rim',(1,3,2),240,3)
data=bpy.data.cameras.new('TW_Camera'); cam=bpy.data.objects.new('TW_Camera',data); scene.collection.objects.link(cam)
cam.location=(2.1,-3.2,2.8); cam.rotation_euler=(-cam.location).to_track_quat('-Z','Y').to_euler()
data.type='ORTHO'; data.ortho_scale=2.8; scene.camera=cam
scene.render.engine='BLENDER_EEVEE'; scene.render.resolution_x=900; scene.render.resolution_y=700
scene.render.resolution_percentage=100; scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(OUT/'preview.png'); scene.view_settings.view_transform='Standard'
bpy.ops.render.render(write_still=True)
bpy.data.libraries.write(str(OUT/'Thornwing.blend'),{scene},fake_user=True)
triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects)
issues=[]
for obj in objects:
    if not obj.data.uv_layers: issues.append(obj.name+': no UV')
    if any(p.area<1e-10 for p in obj.data.polygons): issues.append(obj.name+': degenerate face')
    import bmesh
    bm=bmesh.new(); bm.from_mesh(obj.data)
    if any(not e.is_manifold for e in bm.edges): issues.append(obj.name+': non-manifold')
    if bm.calc_volume(signed=True) <= 0: issues.append(obj.name+': inward-facing normals')
    bm.free()
manifest={'enemy':'Thornwing','triangles':triangles,'meshes':len(objects),'materials':2,'armature':False,
          'animation':'Runtime procedural living flight; jointed body and independent wings on death; no root motion',
          'coordinates':'Blender -Y facing, Z up; FBX -Z forward/Y up export','issues':issues,
          'objects':[{'name':o.name,'dimensions':list(o.dimensions),'uv':bool(o.data.uv_layers)} for o in objects]}
manifest['sha256']={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in OUT.iterdir() if p.suffix in ['.fbx','.png','.blend']}
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2))
bpy.context.window.scene=previous
result=manifest
