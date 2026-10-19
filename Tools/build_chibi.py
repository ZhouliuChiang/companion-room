import bpy, bmesh, math, json, os
from mathutils import Vector, Quaternion
ROOT=r'D:\UNITY\3D伴生範例'
OUT=ROOT+r'\Assets\ChibiCompanion'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.stl_import(filepath=r'D:\TRIPO模型\Q版紫毛羅莉_已移除後方球體.stl')
mesh=bpy.context.object; mesh.name='ChibiMesh'
# Move the separate braid tip and ribbon together about the braid attachment.
# The attachment stays fixed, while the low tip moves backwards away from the hand.
adj=[[] for v in mesh.data.vertices]
for e in mesh.data.edges:
 a,b=e.vertices; adj[a].append(b); adj[b].append(a)
seen=set(); repaired_tail=0
for vertex in mesh.data.vertices:
 if vertex.index in seen: continue
 stack=[vertex.index]; seen.add(vertex.index); ids=[]
 while stack:
  i=stack.pop(); ids.append(i)
  for j in adj[i]:
   if j not in seen: seen.add(j); stack.append(j)
 coords=[mesh.data.vertices[i].co for i in ids]
 if max(v.x for v in coords)<-.15 and min(v.y for v in coords)>.05 and max(v.z for v in coords)<.38:
  pivot=Vector((-.228,.115,.355)); q=Quaternion((1,0,0),math.radians(19))
  for i in ids: mesh.data.vertices[i].co=pivot+q@(mesh.data.vertices[i].co-pivot)
  repaired_tail+=len(ids)
# Exact plane cuts subdivide intersected triangles at the sleeve/body boundary.
bm=bmesh.new(); bm.from_mesh(mesh.data)
for side in [-1,1]:
 bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=(side*.125,0,.30),plane_no=(side,0,.35),clear_inner=False,clear_outer=False)
bm.to_mesh(mesh.data); bm.free(); mesh.data.update()
# The print mesh joins sleeves to the torso. Split the deformation islands along
# existing triangle edges so raising an arm cannot drag the skirt into a web.
source=mesh.data; verts=[]; faces=[]; regions=[]; lookup={}
for poly in source.polygons:
 c=sum((source.vertices[i].co for i in poly.vertices),Vector())/len(poly.vertices)
 x,y,z=c; arm=abs(x)>(.09+max(0,.31-z)*.70) and .215<z<.445 and y<.12
 region=('ArmR' if x>0 else 'ArmL') if arm else 'Body'
 ids=[]
 for i in poly.vertices:
  key=(region,i)
  if key not in lookup:
   lookup[key]=len(verts); verts.append(tuple(source.vertices[i].co)); regions.append(key[0])
  ids.append(lookup[key])
 faces.append(ids)
data=bpy.data.meshes.new('SeparatedSleeves'); data.from_pydata(verts,[],faces); data.update(); mesh.data=data
# Cap the new cut boundaries on both islands; retain the original surfaces.
bm=bmesh.new(); bm.from_mesh(data)
boundary=[e for e in bm.edges if e.is_boundary and all(.21<v.co.z<.45 and v.co.y<.125 for v in e.verts)]
if boundary: bmesh.ops.holes_fill(bm,edges=boundary,sides=0)
bmesh.ops.triangulate(bm,faces=list(bm.faces))
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(data); bm.free(); data.update()
# Give the separated lower sleeves a visible rest-pose clearance from the skirt.
for v in mesh.data.vertices:
 if regions[v.index].startswith('Arm') and v.co.z<.325:
  amount=max(0,min(1,(.325-v.co.z)/.07))
  v.co.x+=(1 if v.co.x>0 else -1)*.009*amount
  v.co.y-=.004*amount
# Preserve the original geometry. The original front is -Y and height is one metre.
for p in mesh.data.polygons: p.use_smooth=True
mat=bpy.data.materials.new('LavenderClay'); mat.diffuse_color=(.55,.36,.72,1); mesh.data.materials.append(mat)
rigdata=bpy.data.armatures.new('ChibiSkeleton'); rig=bpy.data.objects.new('ChibiRig',rigdata); bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig; mesh.select_set(False); rig.select_set(True); bpy.ops.object.mode_set(mode='EDIT')
defs={
 'Root':((0,0,0),(0,0,.08),None),
 'Hips':((0,-.065,.225),(0,-.065,.31),'Root'),
 'Spine':((0,-.065,.31),(0,-.065,.405),'Hips'),
 'Chest':((0,-.065,.405),(0,-.065,.455),'Spine'),
 'Head':((0,-.025,.455),(0,-.025,.82),'Chest')}
for s,sgn in [('L',-1),('R',1)]:
 defs.update({
 'UpperArm.'+s:((sgn*.105,-.068,.407),(sgn*.14,-.079,.337),'Chest'),
 'Forearm.'+s:((sgn*.14,-.079,.337),(sgn*.165,-.09,.267),'UpperArm.'+s),
 'Hand.'+s:((sgn*.165,-.09,.267),(sgn*.178,-.095,.243),'Forearm.'+s),
 'Thigh.'+s:((sgn*.059,-.072,.226),(sgn*.059,-.073,.139),'Hips'),
 'Shin.'+s:((sgn*.059,-.073,.139),(sgn*.059,-.078,.055),'Thigh.'+s),
 'Foot.'+s:((sgn*.059,-.078,.055),(sgn*.059,-.153,.033),'Shin.'+s)})
for n,(h,t,parent) in defs.items():
 b=rigdata.edit_bones.new(n); b.head=h; b.tail=t
 if parent: b.parent=rigdata.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
groups={n:mesh.vertex_groups.new(name=n) for n in defs}
def smooth(t):
 t=max(0,min(1,t)); return t*t*(3-2*t)
def pair(a,b,t): return {a:1-t,b:t}
for v in mesh.data.vertices:
 x,y,z=v.co; ax=abs(x); s='R' if x>=0 else 'L'
 # Hair and braid are kept with the large rigid head, including the low hanging tail.
 hair=(x<-.155 and y>.065) or (z>.455) or (z>.37 and y>.025 and ax>.18)
 if hair: w={'Head':1}
 elif regions[v.index].startswith('Arm'):
  if z>.335:
   blend=1-smooth((z-.365)/.065)
   w=pair('Chest','UpperArm.'+s,blend)
  elif z>.285: w=pair('Forearm.'+s,'UpperArm.'+s,smooth((z-.295)/.055))
  else: w=pair('Hand.'+s,'Forearm.'+s,smooth((z-.254)/.042))
 elif z<.213:
  if z<.067: w={'Foot.'+s:1}
  elif z<.105: w=pair('Foot.'+s,'Shin.'+s,smooth((z-.067)/.038))
  elif z<.167: w=pair('Shin.'+s,'Thigh.'+s,smooth((z-.116)/.043))
  else: w={'Thigh.'+s:1}
 elif z<.305: w={'Hips':1}
 elif z<.39: w=pair('Spine','Chest',smooth((z-.33)/.06))
 else: w=pair('Chest','Head',smooth((z-.435)/.025))
 for n,weight in w.items():
  if weight>0: groups[n].add([v.index],weight,'REPLACE')
# Discard cap triangles that would reconnect a separated sleeve to the skirt.
arm_indices={g.index for n,g in groups.items() if n.startswith(('UpperArm','Forearm','Hand'))}
arm_weight=[sum(g.weight for g in v.groups if g.group in arm_indices) for v in mesh.data.vertices]
remove=[]
for p in mesh.data.polygons:
 weights=[arm_weight[i] for i in p.vertices]
 if min(mesh.data.vertices[i].co.z for i in p.vertices)<.31 and max(weights)-min(weights)>.65:
  remove.append(p.index)
bm=bmesh.new(); bm.from_mesh(mesh.data); bm.faces.ensure_lookup_table()
bmesh.ops.delete(bm,geom=[bm.faces[i] for i in remove],context='FACES_ONLY'); bm.to_mesh(mesh.data); bm.free()
mod=mesh.modifiers.new('ChibiSkin','ARMATURE'); mod.object=rig; mesh.parent=rig
# Interior linings close the exposed print-mesh cut without bridging two moving
# parts. Each sleeve lining follows its own arm; the torso lining follows spine.
patches=[('TorsoLining',(0,-.065,.337),(.098,.072,.083),'Spine',None)]
for name,pos,scale,bone,direction in patches:
 bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=12,location=pos)
 patch=bpy.context.object; patch.name=name; patch.scale=scale
 if direction is not None: patch.rotation_euler=direction.to_track_quat('Z','Y').to_euler()
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 patch.data.materials.append(mat)
 for p in patch.data.polygons: p.use_smooth=True
 group=patch.vertex_groups.new(name=bone); group.add(list(range(len(patch.data.vertices))),1,'REPLACE')
 bpy.ops.object.select_all(action='DESELECT'); patch.select_set(True); mesh.select_set(True); bpy.context.view_layer.objects.active=mesh; bpy.ops.object.join()
rig.show_in_front=True
fps=30; scene=bpy.context.scene; scene.render.fps=fps
rest={n:rigdata.bones[n].matrix_local.to_quaternion() for n in defs}
def rot(n,xyz):
 # Angles are authored in model space to make the deliberately small chibi ranges explicit.
 q=Quaternion((1,0,0),math.radians(xyz[0])) @ Quaternion((0,1,0),math.radians(xyz[1])) @ Quaternion((0,0,1),math.radians(xyz[2]))
 p=rig.pose.bones[n]; p.rotation_mode='QUATERNION'; p.rotation_quaternion=rest[n].inverted() @ q @ rest[n]
def key_all(frame):
 for p in rig.pose.bones:
  p.keyframe_insert('rotation_quaternion',frame=frame,group=p.name); p.keyframe_insert('location',frame=frame,group=p.name)
actions={}
for name,length in [('Idle',90),('Wave',90),('Walk',36),('Run',24)]:
 action=bpy.data.actions.new(name); action.use_fake_user=True; rig.animation_data_create(); rig.animation_data.action=action; actions[name]=action
 for f in range(1,length+2):
  t=(f-1)/length; phase=t*math.tau
  for p in rig.pose.bones: p.rotation_mode='QUATERNION'; p.rotation_quaternion=Quaternion(); p.location=(0,0,0)
  # Subtle breathing; the heavy head does not swing with every step.
  rig.pose.bones['Hips'].location.y=.0015*math.sin(phase)
  if name=='Wave':
   envelope=smooth(t/.2)*smooth((1-t)/.2)
   rot('UpperArm.R',(42*envelope,-60*envelope,0))
   rot('Forearm.R',(12*envelope,-45*envelope,0))
   rot('Hand.R',(0,0,18*math.sin(phase*3)*envelope))
   # Forward/outward clearance avoids the oversized head and sleeve.
   rig.pose.bones['UpperArm.R'].location=rest['UpperArm.R'].inverted() @ Vector((.008*envelope,-.012*envelope,0))
  if name in ('Walk','Run'):
   run=name=='Run'; stride=19 if run else 12; knee=28 if run else 17
   rig.pose.bones['Hips'].location.y=(.006 if run else .003)*(1-math.cos(phase*2))*.5
   for s,sign in [('L',1),('R',-1)]:
    v=math.sin(phase)*sign; lift=max(0,-v)
    rot('Thigh.'+s,(stride*v,0,0)); rot('Shin.'+s,(-knee*lift,0,0)); rot('Foot.'+s,(-stride*v+knee*lift*.65,0,0))
    rot('UpperArm.'+s,(-v*(11 if run else 6),-5 if s=='R' else 5,0)); rot('Forearm.'+s,(8 if run else 3,0,0))
   rot('Spine',(-2 if run else 0,0,0)); rot('Head',(2 if run else 0,0,0))
  key_all(f)
 for curve in action.layers[0].strips[0].channelbags[0].fcurves:
  for key in curve.keyframe_points: key.interpolation='LINEAR'
rig.animation_data.action=actions['Idle']; scene.frame_set(1); scene.frame_end=90
bpy.ops.wm.save_as_mainfile(filepath=ROOT+r'\Artifacts\ChibiCompanion.blend')
bpy.ops.object.select_all(action='DESELECT'); mesh.select_set(True); bpy.context.view_layer.objects.active=mesh
bpy.ops.wm.stl_export(filepath=ROOT+r'\Artifacts\Chibi_Repaired.stl',export_selected_objects=True)
bpy.ops.object.select_all(action='DESELECT'); mesh.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=OUT+r'\ChibiCompanion.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,apply_unit_scale=True)
# Render the strongest poses from front and side for review.
scene.render.engine='BLENDER_WORKBENCH'; scene.display.shading.light='STUDIO'; scene.display.shading.color_type='MATERIAL'; scene.display.shading.show_cavity=True
scene.world=bpy.data.worlds.new('World'); scene.world.color=(.16,.16,.16)
scene.render.resolution_x=700; scene.render.resolution_y=700; scene.render.resolution_percentage=100
for name,frame in [('Idle',1),('Wave',36),('Walk',10),('Run',7)]:
 rig.animation_data.action=actions[name]; scene.frame_set(frame)
 for view,pos in [('front',(0,-2,.7)),('side',(2,-.1,.7))]:
  bpy.ops.object.camera_add(location=pos); camera=bpy.context.object; camera.rotation_euler=(Vector((0,0,.5))-camera.location).to_track_quat('-Z','Y').to_euler(); camera.data.type='ORTHO'; camera.data.ortho_scale=1.17; scene.camera=camera
  scene.render.filepath=ROOT+'\\Artifacts\\'+name+'_'+view+'.png'; bpy.ops.render.render(write_still=True); bpy.data.objects.remove(camera,do_unlink=True)
report={'vertices':len(mesh.data.vertices),'bones':len(defs),'actions':{n:len for n,len in [('Idle',90),('Wave',90),('Walk',36),('Run',24)]},'fps':30,'rig':'Generic, custom chibi proportions','weights_unassigned':sum(not v.groups for v in mesh.data.vertices)}
with open(ROOT+r'\Artifacts\rig_report.json','w') as f: json.dump(report,f,indent=2)
