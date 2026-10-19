import bpy,os,json
from mathutils import Vector
root=r'C:\Users\PowerMan\Documents\Codex\2026-10-15\blender\outputs'
report=[]
for file in ['粉兔桌.blend','粉兔椅.blend','粉兔電腦.blend']:
 bpy.ops.wm.open_mainfile(filepath=os.path.join(root,file))
 obs=[o for o in bpy.context.scene.objects if o.type=='MESH']
 pts=[o.matrix_world@Vector(v) for o in obs for v in o.bound_box]
 item={'file':file,'bounds':[[min(v[i] for v in pts) for i in range(3)],[max(v[i] for v in pts) for i in range(3)]], 'objects':[{'name':o.name,'size':list(o.dimensions),'location':list(o.location),'materials':[m.name for m in o.data.materials if m]} for o in obs], 'cameras':[{'name':o.name,'loc':list(o.location)} for o in bpy.context.scene.objects if o.type=='CAMERA']}
 ob=obs[0];hist={}
 for p in ob.data.polygons:
  if p.normal.z>.85:
   z=round(p.center.z,2);hist[z]=hist.get(z,0)+p.area
 item['horizontal_surfaces']=sorted(hist.items(),key=lambda x:-x[1])[:15]
 item['images']=[{'name':im.name,'path':im.filepath,'packed':bool(im.packed_file),'size':list(im.size)} for im in bpy.data.images if im.source=='FILE']
 report.append(item)
with open(r'D:\UNITY\3D伴生範例\Artifacts\BunnyRoom\asset_inspection.json','w',encoding='utf-8') as f:json.dump(report,f,ensure_ascii=False,indent=2)
print('ASSET_INSPECTION_DONE',flush=True)
