"""Ground the eight rigid foot islands in the saved prototype; preserve v001."""
import bpy
import json
import shutil
from pathlib import Path
from mathutils import Vector

source = bpy.context.scene
assert source.name == 'HollowWarden_v001', 'Select the source Warden scene first.'
body = source.objects['HW_Body']
rig = source.objects['HW_Rig']
out = Path('C:/Users/linco/Mugg/Artifacts/HollowWarden/v002')
out.mkdir(exist_ok=True)
mesh = body.data
groups = {g.index:g.name for g in body.vertex_groups}
foot_ids = {v.index for v in mesh.vertices if any(groups[g.group].startswith('Foot.') for g in v.groups)}
adj = {i:set() for i in foot_ids}
for edge in mesh.edges:
    a,b = edge.vertices
    if a in adj and b in adj: adj[a].add(b);adj[b].add(a)
islands = []
unseen = set(foot_ids)
while unseen:
    stack = [unseen.pop()]; component = []
    while stack:
        current = stack.pop();component.append(current)
        for neighbor in adj[current] & unseen:
            unseen.remove(neighbor);stack.append(neighbor)
    islands.append(sorted(component))
assert len(islands) == 8, 'Expected one heel and three toes on each foot.'
for island in islands:
    sides = len(island)//3
    assert sides in (4,5) and sides*3 == len(island)
    for r in range(3):
        ring = island[r*sides:(r+1)*sides]
        for index in sorted(ring,key=lambda i:mesh.vertices[i].co.z)[:2]:
            mesh.vertices[index].co.z = 0
mesh.update()
manifest = json.loads(Path('C:/Users/linco/Mugg/Artifacts/HollowWarden/v001/manifest.json').read_text())
manifest['version'] = 2
manifest['changes'] = 'Grounded all toe and heel sole rings at Z=0; original rig and clips retained.'
(out/'manifest.json').write_text(json.dumps(manifest,indent=2))
shutil.copyfile('C:/Users/linco/Mugg/Artifacts/HollowWarden/v001/HW_Palette.png',out/'HW_Palette.png')

# Export only the twelve source actions, excluding the prior round-trip QA actions.
tracks = []
for clip in manifest['clips']:
    track = rig.animation_data.nla_tracks.new()
    track.name = 'Export_'+clip['name']
    strip = track.strips.new(clip['name'],1,bpy.data.actions[clip['name']])
    strip.name = clip['name']
    strip.action_slot = bpy.data.actions[clip['name']].slots[0]
    tracks.append(track)
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True);body.select_set(True);bpy.context.view_layer.objects.active=rig
try:
    bpy.ops.export_scene.fbx(filepath=str(out/'HollowWarden.fbx'),use_selection=True,
        object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',
        add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0,path_mode='COPY',embed_textures=True)
finally:
    for track in tracks: rig.animation_data.nla_tracks.remove(track)
rig.animation_data.action=bpy.data.actions['HW_Idle']
source.frame_set(1)
# Save only source dependencies, not the earlier imported QA scene.
bpy.data.libraries.write(str(out/'HollowWarden.blend'),
    {source, *[bpy.data.actions[c['name']] for c in manifest['clips']]}, fake_user=True, compress=True)
for name, location, target, scale in [('preview',(6,-10,5),(0,0,2),5.4),('feet',(3,-6,1.2),(0,-.2,.4),2.5)]:
    source.camera.location=location
    source.camera.rotation_euler=(Vector(target)-source.camera.location).to_track_quat('-Z','Y').to_euler()
    source.camera.data.ortho_scale=scale
    source.render.filepath=str(out/(name+'.png'))
    bpy.ops.render.render(write_still=True)
source.camera.location=(6,-10,5)
source.camera.rotation_euler=(Vector((0,0,2))-source.camera.location).to_track_quat('-Z','Y').to_euler()
source.camera.data.ortho_scale=5.4
source.render.filepath=str(out/'preview.png')
(out/'feet-validation.json').write_text(json.dumps({
    'grounded_islands':len(islands),
    'sole_heights':[min(mesh.vertices[i].co.z for i in island) for island in islands],
    'sole_vertices_per_island':[sum(abs(mesh.vertices[i].co.z)<1e-6 for i in island) for island in islands]
},indent=2))
result={'output':str(out),'grounded_islands':len(islands)}
