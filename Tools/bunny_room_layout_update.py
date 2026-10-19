# Runs inside build_bunny_room.py, reusing its modeling helpers.
from mathutils import Matrix
# Move cabinet, including all shelf contents, into former bunny-nook zone.
old=Vector((.96,2.37,0));new=Vector((2.54,.45,0));R=Matrix.Rotation(-pi/2,4,'Z')
for o in bpy.data.collections['04 Bunny cabinet and decor'].objects:
 o.matrix_world=Matrix.Translation(new)@R@Matrix.Translation(-old)@o.matrix_world
# Remove rabbit nook, pet, bowl, mat and every related part.
for o in list(bpy.data.collections['06 Rabbit nook and pet'].objects):bpy.data.objects.remove(o,do_unlink=True)
bpy.data.collections.remove(bpy.data.collections['06 Rabbit nook and pet'])
# Clear the notebook and its relief rabbit from the existing front writing desk.
for o in list(bpy.data.collections['05 Writing desk and bunny chair'].objects):
 if o.name.startswith(('Pink notebook','Notebook rabbit')):bpy.data.objects.remove(o,do_unlink=True)
# Real supplied Blender assets with original embedded texture materials.
C=group('10 Supplied gaming furniture')
assetroot=r'C:\Users\PowerMan\Documents\Codex\2026-10-15\blender\outputs'
def append_asset(file,name,width,loc,angle=0):
 with bpy.data.libraries.load(os.path.join(assetroot,file),link=False) as (src,dst):dst.objects=src.objects
 mesh=[]
 for o in dst.objects:
  if o and o.type=='MESH':
   C.objects.link(o);mesh.append(o)
  elif o:bpy.data.objects.remove(o)
 # Model has no parent in all three verified files.
 pts=[o.matrix_world@Vector(p) for o in mesh for p in o.bound_box];lo=Vector([min(p[i] for p in pts) for i in range(3)]);hi=Vector([max(p[i] for p in pts) for i in range(3)])
 scale=width/(hi.x-lo.x);center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
 T=Matrix.Translation(Vector(loc))@Matrix.Rotation(angle,4,'Z')@Matrix.Scale(scale,4)@Matrix.Translation(-center)
 for o in mesh:
  o.matrix_world=T@o.matrix_world;o.name=name
  o['source_blend']=file
  for m in o.data.materials:
   if m and m.use_nodes:
    for n in m.node_tree.nodes:
     if n.type=='BSDF_PRINCIPLED':n.inputs['Metallic'].default_value=0
 return mesh,scale
# Desk tabletop source horizontal area peaks at z=.38-.39.
gamingdesk,desk_scale=append_asset('粉兔桌.blend','Provided bunny gaming desk',2.16,(.43,2.12,.035))
desk_top=.035+.390*desk_scale
monitor,monitor_scale=append_asset('粉兔電腦.blend','Provided bunny computer',1.03,(.43,1.96,desk_top+.006))
chair,chair_scale=append_asset('粉兔椅.blend','Provided bunny gaming chair',.80,(.64,1.07,.035),pi+.22)
# Keyboard, separate from monitor and positioned on actual desk surface.
C=group('11 Gaming accessories')
box('Pink keyboard base',(.40,1.76,desk_top+.025),(.64,.22,.035),pale,.025)
for row in range(4):
 for col in range(12):box('Cream keycap',(.11+col*.049,1.69+row*.045,desk_top+.049),(.040,.034,.013),white,.004)
box('Mouse pad',(.99,1.76,desk_top+.007),(.28,.23,.012),pale,.025);uv('Pink mouse',(.99,1.76,desk_top+.044),(.054,.078,.035),cream)
# Foreground desk is now a dedicated tarot station.
C=group('12 Tarot divination desk')
velvet=mat('Dusty mauve tarot velvet',(.43,.20,.35));cloth(velvet,180,.22)
clothob=box('Tarot velvet reading cloth',(-1.98,-1.82,1.000),(1.10,.53,.014),velvet,.045)
tarotback=mat('Tarot card blush back',(.72,.38,.48));tarotink=mat('Tarot card gold ink',(.76,.48,.20));navy=mat('Tarot illustration lavender',(.40,.28,.49))
def card(name,x,y,z,ang=0,face=True,kind=0):
 root=bpy.data.objects.new(name+' assembly',None);C.objects.link(root);root.location=(x,y,z);root.rotation_euler.z=ang
 def parent(o):o.parent=root;return o
 parent(box(name+' cardstock',(0,0,0),(.145,.245,.009),cream,.012))
 parent(box(name+' gold border',(0,0,.0052),(.128,.226,.002),tarotink,.008))
 parent(box(name+' inner print',(0,0,.0065),(.115,.211,.002),cream if face else tarotback,.007))
 if face:
  # Original geometric tarot emblems: moon, sun, star. No copyrighted deck art.
  if kind==0:
   parent(cyl(name+' moon disc',(0,.025,.009),.036,.002,navy));parent(cyl(name+' crescent cutout',(.014,.036,.0105),.031,.002,cream))
  elif kind==1:
   parent(cyl(name+' sun disc',(0,.025,.009),.026,.002,tarotink))
   for j in range(8):
    a=j*pi/4;parent(line(name+' sun ray',[(.036*cos(a),.025+.036*sin(a),.009),(.046*cos(a),.025+.046*sin(a),.009)],.002,tarotink))
  else:
   pts=[]
   for j in range(11):
    a=pi/2+j*pi/5;r=.043 if j%2==0 else .018;pts.append((r*cos(a),.025+r*sin(a),.009))
   parent(line(name+' star',pts,.0025,tarotink))
  parent(line(name+' lower line',[(-.032,-.067,.009),(.032,-.067,.009)],.002,navy))
  for j in range(3):parent(cyl(name+' small constellation',(-.031+j*.031,.080,.009),.003,.002,tarotink))
 else:
  # A fine diamond marking on all card backs.
  parent(line(name+' back diamond',[(0,.076,.009),(.040,0,.009),(0,-.076,.009),(-.040,0,.009),(0,.076,.009)],.002,tarotink))
 return root
for j in range(3):card('Face-up tarot '+str(j+1),-2.20+j*.205,-1.86,1.018,(j-1)*-.10,True,j)
for j in range(8):card('Stacked tarot '+str(j),-1.56,-1.80,1.018+j*.009,.1,False)
for j in range(5):card('Fanned tarot '+str(j),-2.36+j*.047,-1.65,1.018+j*.002,(j-2)*.15,False)
# Tiny crystal and candle at cloth corners, avoiding cards.
crystal=mat('Rose quartz crystal',(.78,.47,.64),.25)
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.06,location=(-1.60,-1.64,1.075));put(bpy.context.object,'Rose quartz reading crystal',crystal);bpy.context.object.scale=(.65,.7,1.2)
cyl('Small tarot candle holder',(-2.43,-1.95,1.026),.045,.025,lightwood);cyl('Cream tarot candle',(-2.43,-1.95,1.075),.031,.08,cream)
uv('Tarot candle flame',(-2.43,-1.95,1.134),(.010,.010,.025),em)
# Character slightly larger, keeping the same floor contact.
for o in bpy.data.collections['08 Pink chibi girl - stylized approximation'].objects:
 o.matrix_world=Matrix.Translation(Vector((0,-.92,0)))@Matrix.Scale(1.10,4)@Matrix.Translation(Vector((0,.92,0)))@o.matrix_world
# Improve contrast, make the pink read without overly bright fill.
sc.view_settings.exposure=-.45
for o in bpy.data.objects:
 if o.type=='LIGHT' and o.data.type=='AREA':o.data.energy*=.82
sc.world.node_tree.nodes['Background'].inputs[1].default_value=.36
sc.render.resolution_x=1600;sc.render.resolution_y=1400;sc.cycles.samples=56
sc['layout']='Rear gaming station; right-side cabinet; front tarot table; no rabbit or bunny igloo.'
# Keep supplied textures self-contained.
bpy.ops.file.pack_all()
validation={'desktop_height':desk_top,'monitor_bottom':min((o.matrix_world@Vector(p)).z for o in monitor for p in o.bound_box),'monitor_width':1.03,'desk_width':2.16,'source_files':['粉兔桌.blend','粉兔椅.blend','粉兔電腦.blend'],'tarot_face_up':3,'pet_and_igloo_removed':True}
with open(os.path.join(OUT,'layout_validation.json'),'w',encoding='utf-8') as f:json.dump(validation,f,ensure_ascii=False,indent=2)
