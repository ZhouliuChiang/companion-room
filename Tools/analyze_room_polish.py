import struct,pathlib,json,numpy as np
R=pathlib.Path(r'D:\UNITY\3D伴生範例\Artifacts\RoomPolish')
def read(path):
 with open(path,'rb') as f:
  count=struct.unpack('<I',f.read(4))[0];out=[]
  for i in range(count):
   nv,ni=struct.unpack('<II',f.read(8));v=np.fromfile(f,dtype='<f4',count=nv*8).reshape(nv,8);t=np.fromfile(f,dtype='<i4',count=ni).reshape(-1,3);out.append((v,t))
 return out
result={}
for p in R.glob('*.bin'):
 vv,tt=read(p)[0];pts=vv[tt,:3];center=pts.mean(axis=1);ns=vv[tt,3:6].mean(axis=1);area=np.linalg.norm(np.cross(pts[:,1]-pts[:,0],pts[:,2]-pts[:,0]),axis=1)/2
 def peaks(axis,normal):
  valid=ns[:,axis]*normal>.85;keys=np.round(center[valid,axis]/.01).astype(int);h={}
  for k,w in zip(keys,area[valid]):h[int(k)]=h.get(int(k),0)+float(w)
  return sorted([(k*.01,round(w,3)) for k,w in h.items()],key=lambda x:-x[1])[:14]
 result[p.stem]={'vertices':len(vv),'faces':len(tt),'bounds':[vv[:,:3].min(axis=0).tolist(),vv[:,:3].max(axis=0).tolist()],'horizontal_up':peaks(2,1),'wall_x_positive':peaks(0,1),'wall_y_negative':peaks(1,-1)}
with open(R/'geometry_analysis.json','w',encoding='utf-8') as f:json.dump(result,f,indent=2)
for k,r in result.items():print(k,'UP',r['horizontal_up'][:8], 'X+',r['wall_x_positive'][:5], 'Y-',r['wall_y_negative'][:5])
