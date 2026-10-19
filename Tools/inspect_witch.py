import bpy,json
bpy.ops.wm.open_mainfile(filepath=r'C:\Users\PowerMan\Documents\Codex\2026-10-15\blender\outputs\粉色女巫.blend')
r=[]
for o in bpy.data.objects:
 d={'name':o.name,'type':o.type,'dim':list(o.dimensions),'loc':list(o.location),'mods':[(m.name,m.type) for m in o.modifiers]}
 if o.type=='ARMATURE': d['bones']=[(b.name,list(b.head_local),list(b.tail_local)) for b in o.data.bones]
 if o.type=='MESH': d['verts']=len(o.data.vertices);d['groups']=[g.name for g in o.vertex_groups]
 r.append(d)
open(r'D:\UNITY\3D伴生範例\Artifacts\Witch\inspect.json','w',encoding='utf8').write(json.dumps(r,ensure_ascii=False,indent=2))
print('ACTIONS',[(a.name) for a in bpy.data.actions])
