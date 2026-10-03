"""Render the exact imported Unity pose deltas through Blender MCP, preserving source."""
import bpy,json
from mathutils import Matrix
from pathlib import Path
OUT=Path('C:/Users/linco/Mugg/Artifacts/Bramblekin/v003')
sequence=json.loads((OUT/'gait-poses.json').read_text())
scene=bpy.data.scenes['Bramblekin_v003'];bpy.context.window.scene=scene
objects={o['export_name']:o for o in scene.objects if o.type=='MESH' and 'export_name' in o}
rest={name:o.matrix_world.copy() for name,o in objects.items()}
conversion=Matrix(((-1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)))
scene.render.resolution_x=320;scene.render.resolution_y=320;scene.render.resolution_percentage=100
scene.cycles.samples=12;scene.render.image_settings.file_format='PNG'
try:
    for frame,pose in enumerate(sequence['frames']):
        for part in pose['parts']:
            numbers=part['delta'];delta=Matrix([numbers[row*4:row*4+4] for row in range(4)])
            objects[part['name']].matrix_world=conversion.inverted()@delta@conversion@rest[part['name']]
        bpy.context.view_layer.update()
        scene.render.filepath=str(OUT/f'gait-{frame:02d}.png');bpy.ops.render.render(write_still=True)
finally:
    for name,o in objects.items():o.matrix_world=rest[name]
result={'frames':len(sequence['frames']),'source':'actual imported Unity pose deltas','output':str(OUT)}
