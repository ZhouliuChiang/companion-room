"""Convert the user-supplied .blend props to self-contained Unity FBX + texture files.
The source files are read only; no character or old generated room is exported.
"""
import bpy, os, json, shutil, time, sys
from mathutils import Vector
SRC=r'C:\Users\PowerMan\Documents\Codex\2026-10-15\blender\outputs'
ROOT=r'D:\UNITY\3D伴生範例'
OUT=os.path.join(ROOT,'Assets','CompanionRoom','Art','UserModels')
REPORT=os.path.join(ROOT,'Artifacts','UserModelsImport')
os.makedirs(OUT,exist_ok=True);os.makedirs(REPORT,exist_ok=True)
entries=[('Room','房間.blend','房間',5.4),('Bed','床.blend','床',1.6),('Rug','地毯.blend','地毯',2.2),('FlowerPlant','盆栽(花).blend','盆栽（花）',.32),('GreenPlant','盆栽(草).blend','盆栽（草）',.36),('GamingDesk','粉兔桌.blend','粉兔桌',1.65),('GamingChair','粉兔椅.blend','粉兔椅',.68),('Computer','粉兔電腦.blend','粉兔電腦',.80),('Window','窗戶.blend','窗戶',1.25),('Bookcase','矮書架.blend','矮書架',1.25),('WallLamp','壁燈.blend','壁燈',.24),('WallShelf','牆架.blend','牆架',.80)]
curtain='--curtain' in sys.argv
extra='--extra' in sys.argv or curtain
if extra:entries=[('SmallTable','小木桌.blend','小木桌',.85),('TarotCards','塔羅牌.blend','塔羅牌',.30),('Pendulum','靈擺.blend','靈擺',.18)]
if curtain:entries=[('Curtain','窗簾.blend','窗簾',1.65)]
def trace_image(socket,seen=None):
 seen=set() if seen is None else seen
 for link in socket.links:
  node=link.from_node
  if node in seen:continue
  seen.add(node)
  if node.type=='TEX_IMAGE' and node.image:return node.image
  for i in node.inputs:
   image=trace_image(i,seen)
   if image:return image
 return None
report=[]
for key,file,label,width in entries:
 print('EXPORT_START',key,flush=True)
 bpy.ops.wm.open_mainfile(filepath=os.path.join(SRC,file))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and not o.hide_render]
 if not meshes:raise RuntimeError(file+' contains no visible mesh')
 # Evaluate full transforms; normalize only origin, leaving all proportions intact.
 pts=[o.matrix_world@Vector(p) for o in meshes for p in o.bound_box]
 lo=Vector([min(v[i] for v in pts) for i in range(3)]);hi=Vector([max(v[i] for v in pts) for i in range(3)])
 offset=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
 for o in meshes:
  o.parent=None;o.location-=offset
 folder=os.path.join(OUT,key);texdir=os.path.join(folder,'Textures');os.makedirs(texdir,exist_ok=True)
 mats=[];images={}
 for o in meshes:
  for m in o.data.materials:
   if m and m not in mats:mats.append(m)
 for m in mats:
  if m.use_nodes:
   for n in m.node_tree.nodes:
    if n.type=='TEX_IMAGE' and n.image:images[n.image.name]=n.image
 paths={}
 for j,(name,im) in enumerate(images.items()):
  data=bytes(im.packed_file.data) if im.packed_file else None
  if data:ext='.png' if data[:8]==b'\x89PNG\r\n\x1a\n' else '.jpg' if data[:2]==b'\xff\xd8' else os.path.splitext(im.filepath)[1]
  else:ext=os.path.splitext(im.filepath)[1] or '.png'
  dest=os.path.join(texdir,key+'_Texture_%02d'%j+ext)
  if data:
   with open(dest,'wb') as f:f.write(data)
  else:
   src=bpy.path.abspath(im.filepath)
   if not os.path.isfile(src):raise RuntimeError('Missing source texture: '+src)
   shutil.copyfile(src,dest)
  paths[name]=dest.replace(ROOT+os.sep,'').replace('\\','/')
  # Repoint export texture references to extracted project-local textures.
  im.filepath=dest
 metadata=[]
 for m in mats:
  p=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) if m.use_nodes else None
  row={'name':m.name,'baseColor':list(m.diffuse_color),'roughness':.7,'textures':{}}
  if p:
   row['baseColor']=list(p.inputs['Base Color'].default_value);row['roughness']=p.inputs['Roughness'].default_value
   for source,target in [('Base Color','baseColor'),('Normal','normal'),('Roughness','roughness'),('Metallic','metallic'),('Emission Color','emission')]:
    if source in p.inputs:
     im=trace_image(p.inputs[source])
     if im and im.name in paths:row['textures'][target]=paths[im.name]
  metadata.append(row)
 bpy.ops.object.select_all(action='DESELECT')
 for o in meshes:o.hide_set(False);o.select_set(True)
 bpy.context.view_layer.objects.active=meshes[0]
 fbx=os.path.join(folder,key+'.fbx')
 bpy.ops.export_scene.fbx(filepath=fbx,use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE',embed_textures=False)
 item={'key':key,'label':label,'source':os.path.join(SRC,file),'fbx':fbx.replace(ROOT+os.sep,'').replace('\\','/'),'size':[float(v) for v in hi-lo],'targetWidth':width,'meshes':len(meshes),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),'materials':metadata}
 with open(os.path.join(folder,'asset_info.json'),'w',encoding='utf-8') as f:json.dump(item,f,ensure_ascii=False,indent=2)
 report.append(item)
 print('EXPORT_DONE',key,item['triangles'],'triangles',len(images),'textures',flush=True)
manifest_path=os.path.join(OUT,'additional_manifest.json' if extra else 'import_manifest.json')
if extra and os.path.isfile(manifest_path):
 with open(manifest_path,encoding='utf-8') as f:previous=json.load(f)['assets']
 report=[a for a in previous if a['key'] not in {r['key'] for r in report}]+report
with open(manifest_path,'w',encoding='utf-8') as f:json.dump({'assets':report},f,ensure_ascii=False,indent=2)
with open(os.path.join(REPORT,'additional_export_report.json' if extra else 'export_report.json'),'w',encoding='utf-8') as f:json.dump(report,f,ensure_ascii=False,indent=2)
print('ALL_ASSETS_EXPORTED',len(report),flush=True)
