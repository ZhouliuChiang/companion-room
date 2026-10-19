import bpy, math, os
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=r'C:\Users\PowerMan\Documents\Codex\2026-10-15\blender\outputs\粉色女巫.blend')
s=bpy.context.scene
m=next(o for o in s.objects if o.type=='MESH'); a=next(o for o in s.objects if o.type=='ARMATURE')
print('matrix',m.matrix_world,a.matrix_world)
for t in bpy.data.images:
 print('IMAGE',t.name,t.filepath,t.size[:])
c=next(o for o in s.objects if o.type=='CAMERA'); c.location=(1.5,-2.5,1.2); c.rotation_euler=(Vector((0,0,.5))-c.location).to_track_quat('-Z','Y').to_euler();c.data.type='ORTHO';c.data.ortho_scale=1.25;s.camera=c
s.render.engine='BLENDER_WORKBENCH';s.display.shading.color_type='MATERIAL';s.render.resolution_x=700;s.render.resolution_y=700;s.render.resolution_percentage=100;s.render.filepath=r'D:\UNITY\3D伴生範例\Artifacts\Witch\source.png';bpy.ops.render.render(write_still=True)
