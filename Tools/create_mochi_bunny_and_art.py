"""Original editable decorations, modeled directly rather than reconstructed from an image."""
import bpy, math, os, json
from mathutils import Vector
from mathutils.geometry import tessellate_polygon
ROOT=r'D:\UNITY\3D伴生範例'
OUT=os.path.join(ROOT,'Assets','CompanionRoom','Art','UserModels')
SOURCE=os.path.join(ROOT,'Artifacts','HandmadeDecor');os.makedirs(SOURCE,exist_ok=True)
records=[]
def material(name,color):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=.80
 return m
def palette():
 global white,pink,rose,cocoa,wood,cream,green,lilac,gold
 white=material('MochiIvory',(.96,.89,.79));pink=material('MarshmallowPink',(.91,.61,.65));rose=material('CheekPink',(.92,.40,.48));cocoa=material('CocoaFace',(.19,.085,.063));wood=material('PaleMaple',(.80,.61,.40));cream=material('CreamCanvas',(.99,.94,.84));green=material('SoftSage',(.40,.55,.32));lilac=material('PastelLilac',(.64,.51,.76));gold=material('ButterYellow',(.96,.74,.37))
def finish(o,name,m):
 o.name=name;o.data.materials.append(m)
 for p in o.data.polygons:p.use_smooth=True
 return o
def uv(name,loc,scale,m,seg=40,rings=24):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=seg,ring_count=rings,location=loc);o=bpy.context.object;o.scale=scale
 return finish(o,name,m)
def box(name,loc,size,m,b=.025):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);finish(o,name,m)
 mod=o.modifiers.new('Soft rounded edge','BEVEL');mod.width=min(b,min(size)*.47);mod.segments=5;o.modifiers.new('Normals','WEIGHTED_NORMAL');return o
def line(name,pts,r,m):
 c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.bevel_depth=r;c.bevel_resolution=4;c.resolution_u=16;s=c.splines.new('BEZIER');s.bezier_points.add(len(pts)-1)
 for p,v in zip(s.bezier_points,pts):p.co=v;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
 o=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(o);c.materials.append(m);return o
def star(x,y,z,r,m):
 pts=[]
 for j in range(10):
  a=math.pi/2+j*math.pi/5;rr=r if j%2==0 else r*.47;pts.append(Vector((x+rr*math.cos(a),y,z+rr*math.sin(a))))
 return polygon('Puffy star',pts,.007,m)
def polygon(name,pts,thick,m):
 faces=tessellate_polygon([pts]);verts=[tuple(p) for p in pts]
 indices=[[v if isinstance(v,int) else min(range(len(pts)),key=lambda i:(pts[i]-v).length_squared) for v in f] for f in faces]
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],indices);me.update();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);me.materials.append(m)
 s=o.modifiers.new('Relief depth','SOLIDIFY');s.thickness=thick;b=o.modifiers.new('Relief rounded edge','BEVEL');b.width=.003;b.segments=3;return o
def bunny():
 # Fused soft silhouette; no detached spherical head, no seams on the mochi body.
 parts=[]
 parts.append(uv('Mochi body',(0,0,.30),(.47,.39,.33),white))
 for side in [-1,1]:
  parts.append(uv('Short soft ear',(side*.165,.035,.675),(.105,.085,.225),white))
  parts[-1].rotation_euler.y=side*-.09
  parts.append(uv('Squishy foot',(side*.235,-.225,.067),(.14,.18,.085),white))
 parts.append(uv('Small cotton tail',(0,.34,.22),(.13,.13,.13),white))
 bpy.ops.object.select_all(action='DESELECT')
 for o in parts:o.select_set(True)
 bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.convert(target='MESH');bpy.ops.object.join();o=bpy.context.object;o.name='MochiRabbit_SoftBody'
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 rem=o.modifiers.new('Fused mochi skin','REMESH');rem.mode='VOXEL';rem.voxel_size=.010;rem.use_smooth_shade=True;bpy.ops.object.modifier_apply(modifier=rem.name)
 smooth=o.modifiers.new('Soft mochi smoothing','SMOOTH');smooth.factor=1.0;smooth.iterations=5;bpy.ops.object.modifier_apply(modifier=smooth.name)
 # Flat concealed underside makes it sit properly on a table or floor.
 for v in o.data.vertices:
  if v.co.z+o.location.z<.012:v.co.z=.012-o.location.z
 for p in o.data.polygons:p.use_smooth=True
 for side in [-1,1]:
  inner=uv('Pink inner ear',(side*.165,-.047,.70),(.058,.016,.145),pink);inner.rotation_euler.y=side*-.09
  uv('Little cocoa eye',(side*.14,-.367,.355),(.024,.011,.029),cocoa)
  uv('Eye highlight',(side*.14-.006,-.378,.365),(.006,.003,.007),cream,20,12)
  uv('Soft blush',(side*.25,-.329,.276),(.051,.010,.026),pink)
 uv('Pink nose',(0,-.391,.293),(.018,.009,.013),rose)
 line('Tiny nose line',[(0,-.393,.285),(0,-.393,.267)],.004,cocoa)
 line('Little smile left',[(0,-.392,.266),(-.014,-.390,.256),(-.027,-.388,.266)],.004,cocoa)
 line('Little smile right',[(0,-.392,.266),(.014,-.390,.256),(.027,-.388,.266)],.004,cocoa)
def frame():
 box('Rounded maple frame',(0,0,.31),(.50,.060,.62),wood,.055)
 box('Inset ivory canvas',(0,-.034,.31),(.427,.016,.547),cream,.025)
 # Back fitting is modeled as well; the painting remains a separate wall prop.
 box('Back panel',(0,.034,.31),(.44,.016,.56),wood,.015)
def bunny_art():
 frame();uv('Flat bunny head',(0,-.051,.29),(.125,.012,.103),pink)
 for side in [-1,1]:
  uv('Flat bunny ear',(side*.058,-.051,.411),(.037,.012,.10),pink)
  uv('Bunny printed eye',(side*.046,-.066,.29),(.010,.004,.012),cocoa,20,12)
 uv('Bunny printed nose',(0,-.066,.265),(.010,.004,.007),rose,20,12)
 for x in [-.15,.15]:star(x,-.054,.45,.022,gold)
def tulip_art():
 frame();line('Tulip thick stem',[(0,-.055,.13),(0,-.055,.36)],.008,green)
 for side in [-1,1]:
  o=uv('Tulip leaf',(side*.048,-.055,.235),(.031,.010,.071),green);o.rotation_euler.y=side*.75
 uv('Tulip center',(0,-.055,.40),(.064,.014,.079),pink)
 for side in [-1,1]:
  o=uv('Tulip outer petal',(side*.045,-.059,.411),(.035,.014,.073),rose);o.rotation_euler.y=side*-.25
def moon_art():
 frame();pts=[]
 for j in range(33):
  a=math.radians(60+240*j/32);pts.append(Vector((.115*math.cos(a),-.058,.337+.115*math.sin(a))))
 for j in range(1,25):
  t=j/24;x=.0575*(1-t)**2+2*(-.105)*t*(1-t)+.0575*t*t;z=-.0996*(1-t)**2+.0996*t*t;pts.append(Vector((x,-.058,.337+z)))
 polygon('Crescent moon',pts,.009,gold)
 star(.112,-.058,.457,.037,lilac);star(.14,-.058,.251,.026,pink);star(-.12,-.058,.162,.021,gold)
def prepare_render():
 sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=32;sc.cycles.use_denoising=True
 try:
  prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='OPTIX';prefs.get_devices()
  for d in prefs.devices:d.use=d.type!='CPU'
  if any(d.use for d in prefs.devices):sc.cycles.device='GPU'
 except Exception:pass
 world=bpy.data.worlds.new('Soft studio');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.85,.80,.77,1);world.node_tree.nodes['Background'].inputs[1].default_value=.65;sc.world=world
 for name,loc,energy,size in [('Key',(-3,-4,5),350,4),('Fill',(3,-1,3),180,3)]:
  bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name=name;o.data.energy=energy;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,.35))-o.location).to_track_quat('-Z','Y').to_euler()
 bpy.ops.object.camera_add(location=(1.1,-3,1.55));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,.4))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=1.2;sc.camera=cam
 sc.render.resolution_x=800;sc.render.resolution_y=800;sc.render.resolution_percentage=100;sc.render.film_transparent=True;sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGBA';sc.view_settings.view_transform='AgX'
 return sc
for key,label,width,build in [('MochiBunny','麻糬兔',.42,bunny),('WallArtBunny','掛畫（兔兔）',.42,bunny_art),('WallArtTulip','掛畫（鬱金香）',.38,tulip_art),('WallArtMoon','掛畫（星月）',.36,moon_art)]:
 bpy.ops.wm.read_factory_settings(use_empty=True);palette();build()
 # Convert curves and apply bevels so every engine sees the same shapes.
 bpy.ops.object.select_all(action='SELECT');bpy.context.view_layer.objects.active=bpy.context.selected_objects[0];bpy.ops.object.convert(target='MESH')
 meshes=list(bpy.context.selected_objects);pts=[o.matrix_world@Vector(p) for o in meshes for p in o.bound_box];lo=Vector([min(p[i] for p in pts) for i in range(3)]);hi=Vector([max(p[i] for p in pts) for i in range(3)])
 offset=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
 for o in meshes:o.location-=offset
 folder=os.path.join(OUT,key);texdir=os.path.join(folder,'Textures');os.makedirs(texdir,exist_ok=True)
 mats=list({m for o in meshes for m in o.data.materials if m});metadata=[]
 for m in mats:
  color=list(m.diffuse_color);im=bpy.data.images.new(key+'_'+m.name,width=8,height=8,alpha=False);im.pixels=list(color)*64;im.file_format='PNG';im.filepath_raw=os.path.join(texdir,m.name+'.png');im.save()
  tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im;m.node_tree.links.new(tex.outputs['Color'],m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
  metadata.append({'name':m.name,'baseColor':color,'roughness':.8,'textures':{'baseColor':os.path.relpath(im.filepath_raw,ROOT).replace('\\','/')}})
 fbx=os.path.join(folder,key+'.fbx')
 bpy.ops.export_scene.fbx(filepath=fbx,use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=True,mesh_smooth_type='FACE',bake_anim=False,path_mode='RELATIVE')
 sc=prepare_render();sc.render.filepath=os.path.join(SOURCE,key+'_preview.png')
 blend=os.path.join(SOURCE,key+'.blend');bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=blend);bpy.ops.render.render(write_still=True)
 item={'key':key,'label':label,'source':blend,'fbx':os.path.relpath(fbx,ROOT).replace('\\','/'),'size':list(hi-lo),'targetWidth':width,'meshes':len(meshes),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),'materials':metadata}
 with open(os.path.join(folder,'asset_info.json'),'w',encoding='utf-8') as f:json.dump(item,f,ensure_ascii=False,indent=2)
 records.append(item);print('DECOR_DONE',key,item['triangles'],flush=True)
manifest=os.path.join(OUT,'additional_manifest.json')
with open(manifest,encoding='utf-8') as f:previous=json.load(f)['assets']
previous=[a for a in previous if a['key'] not in {r['key'] for r in records}]
with open(manifest,'w',encoding='utf-8') as f:json.dump({'assets':previous+records},f,ensure_ascii=False,indent=2)
print('HANDMADE_DECOR_COMPLETE',flush=True)
