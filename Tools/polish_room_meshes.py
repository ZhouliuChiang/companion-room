import bpy,struct,os,json,math,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
R=r'D:\UNITY\3D伴生範例';D=os.path.join(R,'Artifacts','RoomPolish')
def read(name):
 with open(os.path.join(D,name),'rb') as f:
  count=struct.unpack('<I',f.read(4))[0];assert count==1
  nv,ni=struct.unpack('<II',f.read(8));v=np.fromfile(f,'<f4',nv*8).reshape(nv,8);t=np.fromfile(f,'<i4',ni).reshape(-1,3)
 return v,t
def object_from(name,v,t):
 me=bpy.data.meshes.new(name);me.vertices.add(len(v));me.vertices.foreach_set('co',v[:,:3].ravel());me.loops.add(t.size);me.loops.foreach_set('vertex_index',t.ravel());me.polygons.add(len(t));me.polygons.foreach_set('loop_start',np.arange(len(t),dtype=np.int32)*3);me.polygons.foreach_set('loop_total',np.full(len(t),3,dtype=np.int32));me.update()
 uv=me.uv_layers.new(name='UVMap');uv.data.foreach_set('uv',v[t,6:8].ravel());me.polygons.foreach_set('use_smooth',np.ones(len(t),dtype=np.bool_));me.normals_split_custom_set_from_vertices(v[:,3:6].tolist())
 o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);return o
def color_sample(key,uv):
 # Read the texture through Blender's decoder; bottom-left UV origin matches image pixels.
 import glob
 path=glob.glob(os.path.join(R,'Assets','CompanionRoom','Art','UserModels',key,'Textures','*.jpg'))[0]
 im=bpy.data.images.load(path);a=np.empty(im.size[0]*im.size[1]*4,dtype=np.float32);im.pixels.foreach_get(a);a=a.reshape(im.size[1],im.size[0],4)
 return a[np.clip((uv[:,1]*im.size[1]).astype(int),0,im.size[1]-1),np.clip((uv[:,0]*im.size[0]).astype(int),0,im.size[0]-1),:3]
def write_mesh(o,path,group=None):
 me=o.data;me.calc_loop_triangles();tris=np.empty(len(me.loop_triangles)*3,dtype=np.int32);me.loop_triangles.foreach_get('loops',tris);tri_poly=np.empty(len(me.loop_triangles),dtype=np.int32);me.loop_triangles.foreach_get('polygon_index',tri_poly)
 co=np.empty(len(me.vertices)*3,dtype=np.float32);me.vertices.foreach_get('co',co);co=co.reshape(-1,3)
 li=np.empty(len(me.loops),dtype=np.int32);me.loops.foreach_get('vertex_index',li)
 ns=np.empty(len(me.loops)*3,dtype=np.float32);me.corner_normals.foreach_get('vector',ns);ns=ns.reshape(-1,3)
 uv=np.empty(len(me.loops)*2,dtype=np.float32);me.uv_layers.active.data.foreach_get('uv',uv);uv=uv.reshape(-1,2)
 rows=np.concatenate([co[li[tris]],ns[tris],uv[tris]],axis=1)
 # Keep normals and UV seams, but avoid exporting three fully duplicated vertices per triangle.
 unique,indices=np.unique(rows,axis=0,return_inverse=True)
 groups=np.zeros(len(tri_poly),dtype=np.int32) if group is None else group
 with open(os.path.join(D,path),'wb') as f:
  f.write(struct.pack('<II',len(unique),len(indices)));unique.astype('<f4').tofile(f);indices.astype('<i4').tofile(f);groups.astype('<i4').tofile(f)
 return {'vertices':len(unique),'triangles':len(indices)//3,'special_triangles':int(np.count_nonzero(groups))}
bpy.ops.wm.read_factory_settings(use_empty=True)
v,t=read('Room_13.bin');room=object_from('Room with arched window',v,t)
# Closed, arched prism; lower edge is hidden behind the existing sill.
yc=.054;half=.46;shoulder=1.81;bottom=1.22
cross=[(yc-half,bottom),(yc+half,bottom),(yc+half,shoulder)]+[(yc+half*math.cos(math.pi*j/48),shoulder+half*math.sin(math.pi*j/48)) for j in range(1,49)]
verts=[(x,y,z) for x in [-2.86,-2.10] for y,z in cross];n=len(cross);faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(j,(j+1)%n,(j+1)%n+n,j+n) for j in range(n)]
me=bpy.data.meshes.new('Arched aperture cutter');me.from_pydata(verts,[],faces);me.update();cut=bpy.data.objects.new('Window aperture',me);bpy.context.collection.objects.link(cut)
# Recalculate prism normals before robust boolean on the original detailed shell.
import bmesh
bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(me);bm.free()
# Clip only triangles intersecting the convex arched prism. Preserve every untouched
# triangle, UV and normal; avoid a global self-intersection solve on the million-face scan.
planes=[(np.array([1.,0,0]),2.86),(np.array([-1.,0,0]),-2.10)]
for j in range(n):
 a=np.array(cross[j]);b=np.array(cross[(j+1)%n]);edge=b-a
 normal=np.array([0.,-edge[1],edge[0]])
 # Select the halfspace containing the aperture's center.
 d=-float(normal@np.array([0.,a[0],a[1]]))
 if normal@np.array([-2.5,yc,1.65])+d<0:normal=-normal;d=-d
 planes.append((normal,d))
points=v[t,:3];candidate=(points[:,:,0].min(1)<-2.10)&(points[:,:,0].max(1)>-2.86)&(points[:,:,1].min(1)<yc+half)&(points[:,:,1].max(1)>yc-half)&(points[:,:,2].min(1)<shoulder+half)&(points[:,:,2].max(1)>bottom)
keep=t[~candidate];out=[]
def split(poly,normal,d):
 inside=[];outside=[]
 for j in range(len(poly)):
  a=poly[j];b=poly[(j+1)%len(poly)];da=normal@a[:3]+d;db=normal@b[:3]+d
  (inside if da>=-1e-8 else outside).append(a)
  if (da>1e-8 and db< -1e-8) or (da< -1e-8 and db>1e-8):
   p=a+(b-a)*(da/(da-db));inside.append(p);outside.append(p)
 return inside,outside
def triangles(poly):
 return [np.array([poly[0],poly[j],poly[j+1]]) for j in range(1,len(poly)-1)]
for face in t[candidate]:
 poly=[a.copy() for a in v[face]]
 for normal,d in planes:
  ds=np.array([normal@p[:3]+d for p in poly])
  if np.all(ds>=-1e-8):continue
  if np.all(ds<1e-8):out.extend(triangles(poly));poly=[];break
  poly,exterior=split(poly,normal,d);out.extend(triangles(exterior))
  if len(poly)<3:break
 # What remains inside every prism plane is the wall material to remove.
# Reveal faces through the wall thickness are covered by the window frame but close the shell.
mid=v[t,:3].mean(1);target=np.array([-2.335,yc+.65,1.6]);sample=np.argmin(np.linalg.norm(mid-target,axis=1));revealuv=v[t[sample],6:8].mean(0)
for j in range(n):
 a=cross[j];b=cross[(j+1)%n]
 pos=np.array([[-2.695,*a],[-2.326,*a],[-2.326,*b],[-2.695,*b]])
 norm=np.cross(pos[1]-pos[0],pos[2]-pos[0]);norm/=max(np.linalg.norm(norm),1e-12)
 attrs=np.concatenate([pos,np.tile(norm,(4,1)),np.tile(revealuv,(4,1))],axis=1)
 out.extend([attrs[[0,1,2]],attrs[[0,2,3]]])
rows=np.concatenate([v[keep].reshape(-1,8),np.asarray(out,dtype=np.float32).reshape(-1,8)],axis=0)
rows[:,3:6]/=np.maximum(np.linalg.norm(rows[:,3:6],axis=1)[:,None],1e-12)
unique,ix=np.unique(rows,axis=0,return_inverse=True)
bpy.data.objects.remove(room,do_unlink=True);bpy.data.objects.remove(cut,do_unlink=True)
room=object_from('Room with real arched aperture',unique,ix.reshape(-1,3));print('WINDOW_CLIP_COMPLETE',len(t),len(ix)//3,flush=True)
room.data.calc_loop_triangles();lt=room.data.loop_triangles
ids=np.empty(len(lt)*3,dtype=np.int32);lt.foreach_get('vertices',ids);ids=ids.reshape(-1,3)
co=np.empty(len(room.data.vertices)*3,dtype=np.float32);room.data.vertices.foreach_get('co',co);co=co.reshape(-1,3)
loops=np.empty(len(lt)*3,dtype=np.int32);lt.foreach_get('loops',loops);uvs=np.empty(len(room.data.loops)*2,dtype=np.float32);room.data.uv_layers.active.data.foreach_get('uv',uvs);uvs=uvs.reshape(-1,2)
center=co[ids].mean(axis=1);uvcenter=uvs[loops.reshape(-1,3)].mean(axis=1);c=color_sample('Room',uvcenter)
# Warm yellow roof decorations only; pink walls and wooden beams do not emit.
stars=(center[:,2]>3.15)&(c[:,0]>.67)&(c[:,1]>.32)&(c[:,1]>c[:,0]*.53)&(c[:,2]<c[:,1]*.63)
report={'room':write_mesh(room,'RoomCut_processed.bin',stars.astype(np.int32))}
bvh=BVHTree.FromPolygons(co.tolist(),ids.tolist(),all_triangles=True)
checks=[]
for yy,zz in [(yc-.23,1.48),(yc+.23,1.48),(yc-.23,1.97),(yc+.23,1.97),(yc+.75,1.7)]:
 hit=bvh.ray_cast(Vector((-2.9,yy,zz)),Vector((1,0,0)),1.0);checks.append({'along_wall':yy,'height':zz,'blocked':hit[0] is not None})
report['aperture_rays']=checks
if any(c['blocked'] for c in checks[:4]) or not checks[-1]['blocked']:raise RuntimeError('Window aperture validation failed: '+str(checks))
v,t=read('Window_24.bin');window=object_from('Window with transparent panes',v,t);center=v[t,:3].mean(axis=1);normal=v[t,3:6].mean(axis=1);uv=v[t,6:8].mean(axis=1);c=color_sample('Window',uv)
blue=(c[:,2]>c[:,0]*1.18)&(c[:,2]>c[:,1]*.93)&(c[:,0]<.82)
# Include pale highlights on the same pane slab, keeping the projecting wooden mullions opaque.
inside=(np.abs(center[:,1]-yc)<half-.01)&(center[:,2]>bottom)&(center[:,2]<shoulder+np.sqrt(np.maximum(0,half**2-(center[:,1]-yc)**2)))
pane=blue|((center[:,0]>-2.445)&(center[:,0]<-2.376)&inside&(np.abs(normal[:,0])>.6))
report['window']=write_mesh(window,'WindowGlass_processed.bin',pane.astype(np.int32))
if report['window']['special_triangles']<1000:raise RuntimeError('Glass pane selection empty')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(D,'EditedRoomAndWindow.blend'))
with open(os.path.join(D,'mesh_processing_report.json'),'w') as f:json.dump(report,f,indent=2)
print(json.dumps(report),flush=True)
