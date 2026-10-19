import bpy, math, json
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.stl_import(filepath=r'D:\TRIPO模型\Q版紫毛羅莉_已移除後方球體.stl')
o=bpy.context.object
print('BOUNDS',[(min(v.co[i] for v in o.data.vertices),max(v.co[i] for v in o.data.vertices)) for i in range(3)])
print('VERTICES',len(o.data.vertices))
center=sum((Vector(v) for v in o.bound_box),Vector())/8
size=max(o.dimensions)
mat=bpy.data.materials.new('Clay'); mat.diffuse_color=(.55,.35,.72,1); o.data.materials.append(mat)
world=bpy.data.worlds.new('World'); bpy.context.scene.world=world; world.color=(.22,.22,.22)
for name,direction in [('front',(0,-1,.12)),('back',(0,1,.12)),('side',(1,0,.12))]:
 bpy.ops.object.camera_add(location=center+Vector(direction)*size*2)
 cam=bpy.context.object; cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=size*1.2
 sc=bpy.context.scene; sc.camera=cam; sc.render.engine='BLENDER_WORKBENCH'; sc.display.shading.light='STUDIO'; sc.display.shading.color_type='MATERIAL'; sc.display.shading.show_shadows=True; sc.display.shading.show_cavity=True; sc.render.resolution_x=900; sc.render.resolution_y=900; sc.render.resolution_percentage=100; sc.render.filepath=r'D:\UNITY\3D伴生範例\Artifacts\inspect_'+name+'.png'; bpy.ops.render.render(write_still=True)
 bpy.data.objects.remove(cam,do_unlink=True)
