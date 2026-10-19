import bpy, json, os
from mathutils import Vector
ROOT=r'D:\UNITY\3D伴生範例'
OUT=os.path.join(ROOT,'Artifacts','Pendulum')
os.makedirs(OUT,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=r'C:\Users\PowerMan\Documents\Codex\2026-10-15\blender\outputs\靈擺.blend')
objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and not o.hide_render]
rows=[]
for o in objects:
 m=o.data
 parent=list(range(len(m.vertices)))
 def find(a):
  while parent[a]!=a:
   parent[a]=parent[parent[a]]; a=parent[a]
  return a
 for e in m.edges:
  a,b=map(find,e.vertices)
  if a!=b:parent[b]=a
 groups={}
 for v in m.vertices:groups.setdefault(find(v.index),[]).append(v.index)
 for ids in sorted(groups.values(),key=len,reverse=True):
  if len(ids)<30:continue
  pts=[o.matrix_world@m.vertices[i].co for i in ids]
  rows.append(dict(object=o.name,component=find(ids[0]),count=len(ids),min=[min(v[i] for v in pts) for i in range(3)],max=[max(v[i] for v in pts) for i in range(3)]))
with open(os.path.join(OUT,'components.json'),'w',encoding='utf8') as f:json.dump(rows,f,indent=2)
pts=[o.matrix_world@Vector(p) for o in objects for p in o.bound_box]
lo=Vector([min(v[i] for v in pts) for i in range(3)]);hi=Vector([max(v[i] for v in pts) for i in range(3)])
center=(lo+hi)/2;size=max(hi-lo)
sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';sc.display.shading.light='STUDIO';sc.display.shading.color_type='TEXTURE';sc.display.shading.show_shadows=True;sc.display.shading.show_cavity=True
sc.render.resolution_x=850;sc.render.resolution_y=850;sc.render.resolution_percentage=100
for name,direction in [('front',(0,-1,0)),('back',(0,1,0)),('side',(1,0,0))]:
 bpy.ops.object.camera_add(location=center+Vector(direction)*size*2)
 cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=size*1.18;sc.camera=cam
 sc.render.filepath=os.path.join(OUT,name+'.png');bpy.ops.render.render(write_still=True)
print('COMPONENTS',len(rows),'BOUNDS',list(lo),list(hi),flush=True)
