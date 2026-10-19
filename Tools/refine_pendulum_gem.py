import bpy, os, math
folder=r'D:\UNITY\3D伴生範例\Artifacts\Pendulum'
path=os.path.join(folder,'靈擺_金屬紫水晶.blend')
bpy.ops.wm.open_mainfile(filepath=path)
gem=bpy.data.materials['AmethystGem']
principled=next(n for n in gem.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
principled.inputs['Base Color'].default_value=(.84,.70,1,1)
principled.inputs['Transmission Weight'].default_value=1
for name,value in [('Thin Film Thickness',380),('Thin Film IOR',1.38)]:
 if name in principled.inputs:principled.inputs[name].default_value=value
absorb=next(n for n in gem.node_tree.nodes if n.type=='VOLUME_ABSORPTION')
absorb.inputs['Color'].default_value=(.78,.60,.96,1)
absorb.inputs['Density'].default_value=.12
scene=bpy.context.scene;scene.frame_set(1)
pose=bpy.data.objects['PendulumRig'].pose.bones['SwingingAssembly']
for frame,label in [(1,'0'),(16,'14')]:
 scene.frame_set(frame);scene.render.filepath=os.path.join(folder,'rig_'+label+'.png');bpy.ops.render.render(write_still=True)
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=path)
print('TRANSMISSION',principled.inputs['Transmission Weight'].default_value,'THIN_FILM',[(i.name,i.default_value) for i in principled.inputs if 'Thin Film' in i.name],flush=True)
