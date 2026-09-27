"""Run in an isolated Blender process against the saved shrine, never the live edits."""
import bpy, json, os, shutil, traceback
from mathutils import Matrix, Vector

BASE=r'C:/Users/linco/Mugg/Artifacts/MoonwellShrine'
PACKAGE=BASE+'/UnityExport/MoonwellShrine'
REPORT=BASE+'/unity_export_validation.json'
try:
    scene=bpy.context.scene
    scene.name='Moonwell_Idle'
    col=bpy.data.collections['Moonwell_Shrine']
    objects=list(col.all_objects)
    assert len(objects)==78, 'Saved source differs from the verified 78-object shrine.'
    root=bpy.data.objects['Moonwell_ROOT']
    book=bpy.data.objects['Spellbook_Hover']
    page=bpy.data.objects['Spellbook_Turning_Page']
    source_samples={}
    sample_frames=list(range(1,122))
    for frame in sample_frames:
        scene.frame_set(frame)
        source_samples[frame]={'book_world':list(book.matrix_world.translation),
            'weights':{k.name:k.value for k in page.data.shape_keys.key_blocks[1:]}}
    scene.frame_set(1)
    # Bake only the static bevels. Applying modifiers to a shape-key mesh would
    # destroy its animated targets, so that mesh is deliberately left separate.
    for ob in objects:
        if ob.type!='MESH' or ob.data.shape_keys: continue
        bpy.ops.object.select_all(action='DESELECT')
        ob.select_set(True);bpy.context.view_layer.objects.active=ob
        for mod in list(ob.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)

    def merge(group,name,parent):
        bpy.ops.object.select_all(action='DESELECT')
        for ob in group:ob.select_set(True)
        bpy.context.view_layer.objects.active=group[0]
        bpy.ops.object.join()
        ob=bpy.context.object;ob.name=name;ob.data.name=name+'_Mesh'
        # Bake the merged mesh into its parent's coordinate space, keeping pivots
        # and animated hierarchy clean without changing visible geometry.
        ob.data.transform(parent.matrix_world.inverted()@ob.matrix_world)
        ob.parent=parent;ob.matrix_parent_inverse=Matrix.Identity(4);ob.matrix_basis=Matrix.Identity(4)
        return ob

    body=[o for o in objects if o.type=='MESH' and o.parent==root and o.data.materials[0].name=='Moonwell_Painted_Atlas']
    glow=[o for o in objects if o.type=='MESH' and o.parent==root and o.data.materials[0].name=='Moonwell_Crystal_Emission']
    covers=[o for o in objects if o.type=='MESH' and o.parent==book and o!=page]
    body=merge(body,'Moonwell_Body',root)
    glow=merge(glow,'Moonwell_Crystals',root)
    covers=merge(covers,'Moonwell_Book',book)
    page.name='Moonwell_Page';page.data.name='Moonwell_Page_Mesh'
    final=[root,book,body,glow,covers,page]
    bpy.context.view_layer.update()
    image=bpy.data.images['Moonwell_PaintedAtlas_1024']
    texture_path=PACKAGE+'/Textures/Moonwell_PaintedAtlas.png'
    shutil.copy2(bpy.path.abspath(image.filepath),texture_path)
    image.filepath=texture_path
    bpy.ops.object.select_all(action='DESELECT')
    for ob in final:ob.select_set(True)
    bpy.context.view_layer.objects.active=root
    scene.frame_start=1;scene.frame_end=121;scene.render.fps=30;scene.frame_set(1)
    fbx=PACKAGE+'/Models/Moonwell_Shrine.fbx'
    bpy.ops.export_scene.fbx(filepath=fbx,check_existing=False,use_selection=True,
        object_types={'MESH','EMPTY'},global_scale=1.0,apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
        use_space_transform=True,bake_space_transform=False,
        use_mesh_modifiers=False,mesh_smooth_type='OFF',use_tspace=False,
        use_triangles=False,add_leaf_bones=False,bake_anim=True,
        bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,path_mode='RELATIVE',embed_textures=False)
    triangles=0
    for ob in final:
        if ob.type=='MESH':ob.data.calc_loop_triangles();triangles+=len(ob.data.loop_triangles)
    # Import the exported file into an isolated scene and compare its animation.
    imported_scene=bpy.data.scenes.new('Moonwell_Export_Verification')
    bpy.context.window.scene=imported_scene
    imported_scene.render.fps=30
    bpy.ops.import_scene.fbx(filepath=fbx,use_anim=True,anim_offset=0.0)
    imp=list(imported_scene.objects)
    imp_book=next(o for o in imp if o.name.startswith('Spellbook_Hover'))
    imp_page=next(o for o in imp if o.type=='MESH' and o.data.shape_keys)
    max_position_error=0.0;max_weight_error=0.0
    weight_samples=[]
    for frame in sample_frames:
        imported_scene.frame_set(frame)
        max_position_error=max(max_position_error,(imp_book.matrix_world.translation-Vector(source_samples[frame]['book_world'])).length)
        for key in imp_page.data.shape_keys.key_blocks[1:]:
            max_weight_error=max(max_weight_error,abs(key.value-source_samples[frame]['weights'][key.name]))
        if frame in (1,29,57,85,113,115,117,119,121):
            weight_samples.append({'frame':frame,'imported':{k.name:k.value for k in imp_page.data.shape_keys.key_blocks[1:] if k.value>.0001},'source':{k:v for k,v in source_samples[frame]['weights'].items() if v>.0001}})
    imported_scene.frame_set(1)
    corners=[o.matrix_world@Vector(v) for o in imp if o.type=='MESH' for v in o.bound_box]
    bounds=[max(v[i] for v in corners)-min(v[i] for v in corners) for i in range(3)]
    report={'status':'ok','source':bpy.data.filepath,'fbx':fbx,'mesh_count':sum(o.type=='MESH' for o in imp),
        'triangles':triangles,'materials':2,'blend_shapes':len(imp_page.data.shape_keys.key_blocks)-1,
        'fps':30,'duration_seconds':4,'animation_samples':sample_frames,
        'roundtrip_max_book_position_error_m':max_position_error,
        'roundtrip_max_blendshape_weight_error':max_weight_error,'weight_samples':weight_samples,
        'roundtrip_dimensions_blender_xyz_m':bounds,
        'uv_present':all(o.data.uv_layers for o in imp if o.type=='MESH'),
        'unity_editor_validation':'Not run: project open in Unity.'}
    assert max_position_error<.0001,report
    assert max_weight_error<.0001,report
    assert report['mesh_count']==4 and report['blend_shapes']==63,report
    assert all(.9<x<3 for x in bounds),report
    # Neutral verification render uses only the reimported geometry.
    for ob in scene.collection.children['Moonwell_Preview_Studio'].objects:
        imported_scene.collection.objects.link(ob)
    imported_scene.camera=scene.camera;imported_scene.world=scene.world
    imported_scene.render.engine='CYCLES';imported_scene.cycles.samples=16
    imported_scene.cycles.use_denoising=True
    imported_scene.render.resolution_x=640;imported_scene.render.resolution_y=640;imported_scene.render.resolution_percentage=100
    imported_scene.view_settings.view_transform='AgX';imported_scene.frame_set(57)
    imported_scene.render.filepath=BASE+'/Unity_Export_Roundtrip.png'
    bpy.ops.render.render(write_still=True)
    with open(REPORT,'w') as f:json.dump(report,f,indent=2)
except Exception:
    with open(REPORT,'w') as f:json.dump({'status':'error','traceback':traceback.format_exc()},f,indent=2)
    raise
