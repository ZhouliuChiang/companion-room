import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.stl_import(filepath=r'D:\TRIPO模型\Q版紫毛羅莉_已移除後方球體.stl')
o=bpy.context.object
adj=[[] for v in o.data.vertices]
for e in o.data.edges:
 a,b=e.vertices; adj[a].append(b); adj[b].append(a)
seen=set()
for v in o.data.vertices:
 if v.index in seen: continue
 stack=[v.index]; seen.add(v.index); ids=[]
 while stack:
  i=stack.pop(); ids.append(i)
  for j in adj[i]:
   if j not in seen: seen.add(j); stack.append(j)
 if len(ids)>15: print('COMP',len(ids),[(round(min(o.data.vertices[i].co[a] for i in ids),3),round(max(o.data.vertices[i].co[a] for i in ids),3)) for a in range(3)])
