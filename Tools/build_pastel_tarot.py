"""Bake the 78 illustrated tarot cards as textured, solid rounded Blender/FBX assets.
Run with the installed Blender in background mode after artwork generation finishes.
"""
import bpy
import bmesh
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'Assets/CompanionRoom/Resources/CompanionRoom/Tarot'
OUT = ROOT / 'Assets/CompanionRoom/Art/PastelTarot'
PREVIEW = ROOT / 'Artifacts/PastelTarot'


def material(name, texture=None, color=(.94, .82, .73, 1)):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    shader = next((n for n in mat.node_tree.nodes if n.bl_idname == 'ShaderNodeBsdfPrincipled'), None)
    if shader is None:
        shader = mat.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
    output = next((n for n in mat.node_tree.nodes if n.bl_idname == 'ShaderNodeOutputMaterial'), None)
    if output is None:
        output = mat.node_tree.nodes.new('ShaderNodeOutputMaterial')
    mat.node_tree.links.new(shader.outputs['BSDF'], output.inputs['Surface'])
    shader.inputs['Base Color'].default_value = color
    shader.inputs['Roughness'].default_value = .68
    if texture:
        image = bpy.data.images.load(str(texture), check_existing=True)
        node = mat.node_tree.nodes.new('ShaderNodeTexImage')
        node.image = image
        mat.node_tree.links.new(node.outputs['Color'], shader.inputs['Base Color'])
    return mat


def rounded_mesh():
    w, h, radius, half_z = .07, .115, .004, .0008
    ring = []
    for corner in range(4):
        cx = w / 2 - radius if corner in (0, 3) else -w / 2 + radius
        cy = h / 2 - radius if corner < 2 else -h / 2 + radius
        for step in range(9):
            angle = math.radians(corner * 90 + step * 90 / 8)
            ring.append((cx + radius * math.cos(angle), cy + radius * math.sin(angle)))
    count = len(ring)
    vertices = [(x, y, z) for z in (half_z, -half_z) for x, y in ring]
    faces = [tuple(range(count)), tuple(range(2 * count - 1, count - 1, -1))]
    faces += [(i, i + count, (i + 1) % count + count, (i + 1) % count) for i in range(count)]
    mesh = bpy.data.meshes.new('RoundedCard_70x115x1.6mm')
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    uv = mesh.uv_layers.new(name='CardUV')
    for polygon in mesh.polygons:
        polygon.material_index = min(polygon.index, 2)
        for loop_index in polygon.loop_indices:
            vertex = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = (.5 + vertex.x / w * (-1 if polygon.index == 1 else 1), .5 + vertex.y / h)
    assert abs(mesh.vertices[0].co.z * 2 - .0016) < 1e-7
    check = bmesh.new()
    check.from_mesh(mesh)
    assert all(e.is_manifold for e in check.edges), 'Card must be a closed solid.'
    expected_volume = (w * h - (4 - math.pi) * radius ** 2) * half_z * 2
    assert abs(check.calc_volume() - expected_volume) < 1e-9, 'Unexpected card thickness or winding.'
    check.free()
    return mesh


def main():
    cards = json.loads((ART / 'art_manifest.json').read_text(encoding='utf-8'))['cards']
    missing = [c['id'] for c in cards if not (ART / 'Cards' / (c['id'] + '.png')).exists()]
    if missing:
        raise RuntimeError('Missing artwork: ' + ', '.join(missing))
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'Models').mkdir(exist_ok=True)
    PREVIEW.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    back = material('Tarot_MoonRabbit_Back', ART / 'Art/card_back.png')
    edge = material('Tarot_Cream_Edge')
    base = rounded_mesh()
    objects = []
    for i, card in enumerate(cards):
        mesh = base.copy()
        face = material('Tarot_' + card['id'], ART / 'Cards' / (card['id'] + '.png'))
        for mat in (face, back, edge):
            mesh.materials.append(mat)
        obj = bpy.data.objects.new(card['id'], mesh)
        bpy.context.collection.objects.link(obj)
        obj['english'] = card['english']
        obj['card_id'] = card['id']
        obj['thickness_mm'] = 1.6
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.export_scene.fbx(filepath=str(OUT / 'Models' / (card['id'] + '.fbx')),
            use_selection=True, object_types={'MESH'}, global_scale=1.0, apply_unit_scale=True,
            axis_forward='-Z', axis_up='Y', path_mode='RELATIVE', bake_anim=False,
            add_leaf_bones=False, use_mesh_modifiers=True)
        obj.location = ((i % 10 - 4.5) * .078, (3.5 - i // 10) * .125, 0)
        objects.append(obj)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.world.color = (.3, .3, .3)
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    # Emission preview materials display the source artwork without illumination shifts.
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            continue
        shader = next((n for n in mat.node_tree.nodes if n.bl_idname == 'ShaderNodeBsdfPrincipled'), None)
        if shader:
            texture = next((n for n in mat.node_tree.nodes if n.bl_idname == 'ShaderNodeTexImage'), None)
            if texture:
                mat.node_tree.links.new(texture.outputs['Color'], shader.inputs['Emission Color'])
                shader.inputs['Emission Strength'].default_value = 1
                shader.inputs['Base Color'].default_value = (0, 0, 0, 1)
                for link in list(mat.node_tree.links):
                    if link.to_socket == shader.inputs['Base Color']:
                        mat.node_tree.links.remove(link)
    bpy.ops.object.camera_add(location=(0, 0, 1))
    camera = bpy.context.object
    camera.name = 'Tarot_Gallery_Camera'
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 1.02
    scene.camera = camera
    scene.render.resolution_x, scene.render.resolution_y = 3200, 4000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = str(PREVIEW / '78-cards-preview.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / 'Concepts/PastelTarot78.blend'))
    bpy.ops.render.render(write_still=True)
    # A separate oblique front/back preview demonstrates actual solid sidewalls.
    for obj in objects:
        obj.hide_render = True
    objects[19].hide_render = False
    objects[19].location = (-.045, 0, 0)
    objects[19].rotation_euler = (math.radians(15), math.radians(28), math.radians(-8))
    back_card = objects[19].copy()
    back_card.data = objects[19].data
    bpy.context.collection.objects.link(back_card)
    back_card.location = (.045, 0, 0)
    back_card.rotation_euler = (math.radians(15), math.radians(155), math.radians(8))
    back_card.hide_render = False
    camera.data.ortho_scale = .225
    scene.render.resolution_x, scene.render.resolution_y = 1600, 1200
    scene.render.filepath = str(PREVIEW / 'card-thickness-preview.png')
    bpy.ops.render.render(write_still=True)
    (PREVIEW / 'model-validation.json').write_text(json.dumps({
        'cards': len(cards), 'fbx_files': len(list((OUT / 'Models').glob('*.fbx'))),
        'dimensions_mm': [70, 115, 1.6], 'rounded_radius_mm': 4,
        'material_slots': ['front', 'back', 'edge'], 'pivot': 'center',
        'missing_art': missing,
    }, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
