"""Read-only source mesh checks plus FBX import into a separate QA scene.

Run through Blender MCP with the prototype scene active. The source .blend is not
saved after the QA import. No existing objects or actions are deleted.
"""
import bpy
import bmesh
import json
from pathlib import Path

source = bpy.context.scene
out = Path(globals().get('WARDEN_VALIDATION_DIR', Path(bpy.data.filepath).parent))
rig = next(o for o in source.objects if o.type == 'ARMATURE')
body = next(o for o in source.objects if o.type == 'MESH' and o.parent == rig)
mesh = body.data
mesh.calc_loop_triangles()
bm = bmesh.new()
bm.from_mesh(mesh)
nonmanifold = sum(not edge.is_manifold for edge in bm.edges)
zero_area = sum(face.calc_area() < 1e-10 for face in bm.faces)
volume = bm.calc_volume(signed=True)
bm.free()
bad_weights = sum(abs(sum(g.weight for g in vertex.groups)-1) > 1e-5 for vertex in mesh.vertices)
manifest = json.loads((out/'manifest.json').read_text())
checks = {
    'source_triangles':len(mesh.loop_triangles),
    'source_bones':len(rig.data.bones),
    'source_materials':len(mesh.materials),
    'uv_layers':len(mesh.uv_layers),
    'nonmanifold_edges':nonmanifold,
    'zero_area_faces':zero_area,
    'invalid_weight_vertices':bad_weights,
    'signed_volume':volume,
    'rest_min_z':min(v.co.z for v in mesh.vertices),
    'rest_max_z':max(v.co.z for v in mesh.vertices),
}
assert nonmanifold == 0 and zero_area == 0 and bad_weights == 0 and volume > 0
assert len(mesh.uv_layers) == 1 and len(mesh.materials) == 2
assert abs(checks['rest_min_z']) < .001
source_animation = rig.animation_data.action
source_frame = source.frame_current
loops = {}
for clip in manifest['clips']:
    if not clip['loop']: continue
    rig.animation_data.action = bpy.data.actions[clip['name']]
    snapshots = []
    for frame in [1,1+round(clip['duration']*30)]:
        source.frame_set(frame)
        snapshots.append([tuple(value for row in b.matrix for value in row) for b in rig.pose.bones])
    error = max(abs(a-b) for first,last in zip(*snapshots) for a,b in zip(first,last))
    loops[clip['name']] = error
    assert error < 1e-5, clip['name']+' has a loop seam'
rig.animation_data.action = source_animation
source.frame_set(source_frame)
checks['loop_matrix_max_error'] = loops

actions_before = set(bpy.data.actions)
qa = bpy.data.scenes.new('HW_FBX_Roundtrip_QA')
bpy.context.window.scene = qa
try:
    bpy.ops.import_scene.fbx(filepath=str(out/'HollowWarden.fbx'))
    imported_actions = [a for a in bpy.data.actions if a not in actions_before]
    imported_rigs = [o for o in qa.objects if o.type == 'ARMATURE']
    imported_meshes = [o for o in qa.objects if o.type == 'MESH']
    assert len(imported_rigs) == 1 and len(imported_meshes) == 1
    assert len(imported_actions) == len(manifest['clips'])
    assert len(imported_rigs[0].data.bones) == len(rig.data.bones)
    assert imported_meshes[0].modifiers[0].object == imported_rigs[0]
    imported = {a.name:float(a.frame_range[1]-a.frame_range[0]) for a in imported_actions}
    for clip in manifest['clips']:
        matches = [(name,frames) for name,frames in imported.items() if name.endswith(clip['name'])]
        assert len(matches) == 1, 'Missing/ambiguous imported clip: '+clip['name']
        assert abs(matches[0][1]-clip['duration']*30) < .1, 'Wrong clip duration'
    checks['fbx_clips_frames'] = imported
    checks['fbx_rigs'] = len(imported_rigs)
    checks['fbx_meshes'] = len(imported_meshes)
    checks['fbx_bones'] = len(imported_rigs[0].data.bones)
    checks['status'] = 'PASS; Blender round-trip only, Unity import unverified'
finally:
    bpy.context.window.scene = source
(out/'validation.json').write_text(json.dumps(checks,indent=2))
result = checks
