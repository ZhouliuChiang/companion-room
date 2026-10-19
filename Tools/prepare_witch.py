import bpy,math,os
from mathutils import Vector,Quaternion
root=r'D:\UNITY\3D伴生範例'; out=root+r'\Assets\CompanionRoom\Art\Witch';os.makedirs(out,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=r'C:\Users\PowerMan\Documents\Codex\2026-10-15\blender\outputs\粉色女巫.blend')
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
a=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');m=next(o for o in bpy.context.scene.objects if o.type=='MESH')
# Keep the skirt attached to the pelvis instead of letting the automatic leg weights split it.
hip=m.vertex_groups['Hips']; fixed=0
for v in m.data.vertices:
 p=v.co
 if .205<p.z<.345 and math.hypot(p.x-.002,p.y+.022)>.095:
  for g in list(v.groups):m.vertex_groups[g.group].remove([v.index])
  hip.add([v.index],1,'REPLACE');fixed+=1
for im in bpy.data.images:
 if 'witch' in im.name and im.size[0]: im.filepath_raw=out+r'\WitchBase.png';im.file_format='PNG';im.save()
a.animation_data_create()
def rot(name,axis,deg):
 b=a.pose.bones.get(name)
 if b:b.rotation_mode='QUATERNION';b.rotation_quaternion=b.bone.matrix_local.to_quaternion().inverted()@Quaternion(Vector(axis),math.radians(deg))@b.bone.matrix_local.to_quaternion()
for name,frames in [('Idle',120),('Walk',32)]:
 act=bpy.data.actions.new(name);act.use_fake_user=True;a.animation_data.action=act
 for f in range(1,frames+2):
  p=(f-1)/frames*math.tau; w=math.sin(p);breath=math.sin(p)
  for b in a.pose.bones:b.rotation_mode='QUATERNION';b.rotation_quaternion=Quaternion();b.location=(0,0,0)
  moving=name=='Walk'
  rot('Hips',(0,0,1),2*w if moving else .35*w)
  rot('Spine',(0,1,0),1.4*w if moving else .35*w)
  rot('Chest',(1,0,0),1.3+(.6*math.cos(p*2) if moving else .65*breath))
  rot('Head',(0,0,1),-.8*w)
  for side,sign in [('Left',1),('Right',-1)]:
   step=w*sign
   rot(side+'_UpperArm',(0,1,0),sign*48)
   # Small forward/back swing stays outside the protected skirt volume.
   b=a.pose.bones[side+'_UpperArm'];b.rotation_quaternion=b.rotation_quaternion@Quaternion((1,0,0),math.radians(4*step if moving else .5*w))
   rot(side+'_LowerArm',(0,0,1),-sign*8)
   rot(side+'_UpperLeg',(1,0,0),14*step if moving else 0)
   rot(side+'_LowerLeg',(1,0,0),-max(0,-step)*20 if moving else 0)
   rot(side+'_Foot',(1,0,0),max(0,-step)*7 if moving else 0)
  a.pose.bones['Hips'].location.z=(.002*(1-math.cos(p*2)) if moving else .00065*breath)
  for b in a.pose.bones:b.keyframe_insert('rotation_quaternion',frame=f);b.keyframe_insert('location',frame=f)
 a.animation_data.action=None
 track=a.animation_data.nla_tracks.new();track.name=name;strip=track.strips.new(name,1,act);track.mute=True
# Export actions as separate takes, preserving generic skeleton.
bpy.ops.object.select_all(action='DESELECT');a.select_set(True);m.select_set(True);bpy.context.view_layer.objects.active=a
bpy.context.scene.render.fps=30
bpy.ops.export_scene.fbx(filepath=out+r'\PinkWitch.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=False)
a.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=root+r'\Artifacts\Witch\粉色女巫_動作版.blend')
print('SKIRT_WEIGHT_FIXED',fixed)

