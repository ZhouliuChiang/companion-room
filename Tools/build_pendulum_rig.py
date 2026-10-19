"""Preserve the complete source mesh and animate the suspended centre with skin weights."""
import bpy, os, json, math, numpy as np
from mathutils import Vector
ROOT=r'D:\UNITY\3D伴生範例'
OUT=os.path.join(ROOT,'Artifacts','Pendulum')
DEST=os.path.join(ROOT,'Assets','CompanionRoom','Resources','Pendulum')
os.makedirs(DEST,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=r'C:\Users\PowerMan\Documents\Codex\2026-10-15\blender\outputs\靈擺.blend')
source=next(o for o in bpy.context.scene.objects if o.type=='MESH' and not o.hide_render)
source.name='PendulumBody'
original_vertices=len(source.data.vertices);original_faces=len(source.data.polygons)
metal=source.data.materials[0].copy();metal.name='FrameMetal'
metal_bsdf=next(n for n in metal.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
metal_bsdf.inputs['Metallic'].default_value=.88
metal_bsdf.inputs['Roughness'].default_value=.22
metal_bsdf.inputs['Coat Weight'].default_value=.35
metal_bsdf.inputs['Coat Roughness'].default_value=.12
gem=bpy.data.materials.new('AmethystGem');gem.use_nodes=True
gem_bsdf=next(n for n in gem.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
gem_bsdf.inputs['Base Color'].default_value=(.84,.70,1,1)
gem_bsdf.inputs['Metallic'].default_value=0
gem_bsdf.inputs['Roughness'].default_value=.055
gem_bsdf.inputs['IOR'].default_value=1.54
gem_bsdf.inputs['Transmission Weight'].default_value=1
if 'Thin Film Thickness' in gem_bsdf.inputs:gem_bsdf.inputs['Thin Film Thickness'].default_value=380
if 'Thin Film IOR' in gem_bsdf.inputs:gem_bsdf.inputs['Thin Film IOR'].default_value=1.38
gem_bsdf.inputs['Coat Weight'].default_value=.65
gem_bsdf.inputs['Coat Roughness'].default_value=.04
absorb=gem.node_tree.nodes.new('ShaderNodeVolumeAbsorption');absorb.inputs['Color'].default_value=(.78,.60,.96,1);absorb.inputs['Density'].default_value=.12
gem.node_tree.links.new(absorb.outputs['Volume'],next(n for n in gem.node_tree.nodes if n.type=='OUTPUT_MATERIAL').inputs['Volume'])
texture=next(n.image for n in metal.node_tree.nodes if n.type=='TEX_IMAGE' and n.image)
pixels=np.empty(len(texture.pixels),dtype=np.float32);texture.pixels.foreach_get(pixels);pixels=pixels.reshape(texture.size[1],texture.size[0],4)
uvs=source.data.uv_layers.active.data
source.data.materials.clear();source.data.materials.append(metal);source.data.materials.append(gem)
gem_faces=0
for face in source.data.polygons:
 p=source.matrix_world@face.center
 if .23<p.z<.67 and abs(p.x)<.18:
  uv=sum((uvs[i].uv for i in face.loop_indices),Vector((0,0)))/len(face.loop_indices)
  color=pixels[int((uv.y%1)*(texture.size[1]-1)),int((uv.x%1)*(texture.size[0]-1))]
  if color[2]>color[1]*1.12 and color[0]>color[1]*1.04:
   face.material_index=1;gem_faces+=1
fixed=source.vertex_groups.new(name='FixedFrame')
moving=source.vertex_groups.new(name='SwingingAssembly')
pivot=Vector((0,0,.875))
full_fixed=[];full_moving=[];blend_count=0
for vertex in source.data.vertices:
 p=source.matrix_world@vertex.co
 inside=.235<p.z<.875 and abs(p.x)<(.17 if p.z<.72 else .055)
 weight=0
 if inside:
  t=max(0,min(1,(.875-p.z)/.04))
  weight=t*t*(3-2*t)
 if weight==0:full_fixed.append(vertex.index)
 elif weight==1:full_moving.append(vertex.index)
 else:
  fixed.add([vertex.index],1-weight,'REPLACE');moving.add([vertex.index],weight,'REPLACE');blend_count+=1
 vertex.co=p-pivot
fixed.add(full_fixed,1,'REPLACE');moving.add(full_moving,1,'REPLACE')
source.matrix_world.identity()
# Face the same way as the original front view in the Unity preview.
for vertex in source.data.vertices:vertex.co.x=-vertex.co.x;vertex.co.y=-vertex.co.y
arm_data=bpy.data.armatures.new('PendulumSkeleton')
arm=bpy.data.objects.new('PendulumRig',arm_data);bpy.context.collection.objects.link(arm)
bpy.context.view_layer.objects.active=arm;arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
base=arm_data.edit_bones.new('FixedFrame');base.head=(0,0,0);base.tail=(0,.1,0)
bone=arm_data.edit_bones.new('SwingingAssembly');bone.head=(0,0,0);bone.tail=(0,.08,0);bone.parent=base
bpy.ops.object.mode_set(mode='OBJECT')
modifier=source.modifiers.new('ContinuousJoint','ARMATURE');modifier.object=arm
source.parent=arm
bpy.ops.object.select_all(action='DESELECT');source.select_set(True);arm.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=os.path.join(DEST,'PendulumRig.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=False,use_armature_deform_only=True)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.cycles.max_bounces=12;scene.cycles.transmission_bounces=10
world=scene.world or bpy.data.worlds.new('GemStudio');scene.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs['Color'].default_value=(.20,.16,.28,1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value=.5
scene.render.film_transparent=False
for obj in list(scene.objects):
 if obj.type in {'LIGHT','CAMERA'}:bpy.data.objects.remove(obj,do_unlink=True)
for name,position,power,color,size in [('Key',(-1.4,1.5,1),450,(1,.86,.70),1.3),('Strip',(1.2,.8,.4),350,(.75,.83,1),.65),('Rim',(.5,-1,.5),550,(.80,.60,1),.8)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.color=color;data.shape='RECTANGLE';data.size=size;data.size_y=size*.25
 obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=position;obj.rotation_euler=(Vector((0,0,-.35))-obj.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=850;scene.render.resolution_y=850;scene.render.resolution_percentage=100
bpy.ops.object.camera_add(location=(0,2,-.375))
camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,-.375))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=1.18;scene.camera=camera
pose=arm.pose.bones['SwingingAssembly'];pose.rotation_mode='XYZ'
for angle in [0,14]:
 pose.rotation_euler.y=math.radians(angle)
 scene.render.filepath=os.path.join(OUT,'rig_%s.png'%angle);bpy.ops.render.render(write_still=True)
pose.rotation_euler.y=0
# A ready-to-preview sway is included in the project copy of the Blender file.
for frame,angle in [(1,0),(16,14),(31,0),(46,-14),(61,0)]:
 pose.rotation_euler.y=math.radians(angle);pose.keyframe_insert(data_path='rotation_euler',frame=frame)
scene.frame_start=1;scene.frame_end=61;scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'靈擺_金屬紫水晶.blend'))
assert len(source.data.vertices)==original_vertices and len(source.data.polygons)==original_faces
with open(os.path.join(OUT,'rig_report.json'),'w',encoding='utf8') as f:json.dump({'method':'continuous original mesh with two-bone skinning; no geometry cut','vertices':original_vertices,'faces':original_faces,'gem_faces':gem_faces,'fixed_vertices':len(full_fixed),'moving_vertices':len(full_moving),'blended_joint_vertices':blend_count,'pivot':list(pivot)},f,indent=2)
