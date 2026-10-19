import bpy, math, random, os, json
from mathutils import Vector
from math import sin, cos, pi
random.seed(18)
ROOT=r'D:\UNITY\3D伴生範例'
OUT=os.path.join(ROOT,'Artifacts','BunnyRoom'); os.makedirs(OUT,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc=bpy.context.scene
def mat(name,color,rough=.65):
 m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough
 return m
cream=mat('Ivory porcelain',(1,.86,.72)); pink=mat('Strawberry milk',(.79,.36,.39)); pale=mat('Blush upholstery',(.91,.59,.57)); white=mat('Warm cream fabric',(.98,.90,.78)); wall=mat('Rose plaster',(.73,.43,.43)); wood=mat('Honey birch',(.69,.45,.25)); lightwood=mat('Pale maple',(.87,.66,.43)); leafmat=mat('Sage leaves',(.25,.36,.12)); leaflight=mat('Leaf highlights',(.4,.49,.21)); lavender=mat('Lilac accents',(.58,.40,.61)); dark=mat('Cocoa eyes',(.12,.045,.04),.3); iris=mat('Rose eyes',(.45,.12,.17),.3); skin=mat('Peach skin',(1,.72,.60)); hair=mat('Rose hair',(.85,.31,.37)); hairlight=mat('Hair highlights',(.98,.49,.53)); blush=mat('Cheeks',(.96,.32,.37)); gold=mat('Warm brass',(.65,.41,.19),.35)
def cloth(m,scale=130,strength=.16):
 n=m.node_tree.nodes;l=m.node_tree.links;t=n.new('ShaderNodeTexNoise');t.inputs['Scale'].default_value=scale;t.inputs['Detail'].default_value=2;b=n.new('ShaderNodeBump');b.inputs['Strength'].default_value=strength;b.inputs['Distance'].default_value=.025;l.new(t.outputs['Fac'],b.inputs['Height']);l.new(b.outputs[0],n.get('Principled BSDF').inputs['Normal'])
for m in [pink,pale,white]:cloth(m)
def group(name):
 c=bpy.data.collections.new(name);sc.collection.children.link(c);return c
C=group('01 Architecture')
def put(o,name,m):
 o.name=name
 for c in list(o.users_collection):c.objects.unlink(o)
 C.objects.link(o)
 if m:o.data.materials.append(m)
 return o
def box(name,loc,size,m,bevel=.08):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);put(o,name,m)
 if bevel:
  b=o.modifiers.new('Soft rounded edges','BEVEL');b.width=min(bevel,min(size)*.48);b.segments=4
  o.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL')
 return o
def uv(name,loc,size,m):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=20,location=loc);o=bpy.context.object;o.scale=size;put(o,name,m)
 for p in o.data.polygons:p.use_smooth=True
 return o
def cyl(name,loc,r,depth,m):
 bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=r,depth=depth,location=loc);o=bpy.context.object;put(o,name,m);b=o.modifiers.new('Rounded rim','BEVEL');b.width=min(.03,depth*.2);b.segments=3;o.modifiers.new('Normals','WEIGHTED_NORMAL');return o
def line(name,pts,r,m):
 d=bpy.data.curves.new(name,'CURVE');d.dimensions='3D';d.resolution_u=12;d.bevel_depth=r;d.bevel_resolution=3;s=d.splines.new('BEZIER');s.bezier_points.add(len(pts)-1)
 for p,co in zip(s.bezier_points,pts):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
 o=bpy.data.objects.new(name,d);C.objects.link(o);o.data.materials.append(m);return o
def rod(name,a,b,r,m):
 o=cyl(name,(Vector(a)+Vector(b))/2,r,(Vector(a)-Vector(b)).length,m);o.rotation_euler=(Vector(b)-Vector(a)).to_track_quat('Z','Y').to_euler();return o
def bunny(name,x,y,z,s,m=white,face=True):
 uv(name+' head',(x,y,z),(s,s*.57,s*.88),m)
 for side in [-1,1]:
  uv(name+' ear',(x+side*s*.43,y,z+s*1.05),(s*.24,s*.32,s*.70),m)
  uv(name+' inner ear',(x+side*s*.43,y-s*.28,z+s*1.09),(s*.12,s*.045,s*.44),pale)
 if face:
  for side in [-1,1]:uv(name+' eye',(x+side*s*.32,y-s*.555,z+s*.09),(s*.065,s*.035,s*.078),dark)
  uv(name+' nose',(x,y-s*.59,z-s*.10),(s*.09,s*.035,s*.06),pink)
def pot(name,x,y,z,s=.25):
 uv(name+' ceramic',(x,y,z+s*.7),(s,s,s*.8),cream);cyl(name+' soil',(x,y,z+s*1.36),s*.80,.03,wood)
 for j in range(7):
  a=j*2.4;tip=Vector((x+sin(a)*s*1.35,y+cos(a)*s*1.25,z+s*(2.2+random.random()*.8)));start=(x,y,z+s*1.2);rod(name+' stem',start,tip,.012,leafmat)
  o=uv(name+' leaf',tip,(s*.45,s*.15,s*.9),leafmat if j%2 else leaflight);o.rotation_euler=Vector((sin(a),cos(a),.7)).to_track_quat('Z','Y').to_euler()
def vine(x,y,z):
 for j in range(12):
  t=j/11;xx=x+.12*sin(t*7);yy=y-.06*cos(t*5);zz=z-t*1.15
  uv('Trailing leaf',(xx+(.09 if j%2 else -.09),yy-.03,zz),(.13,.065,.09),leafmat if j%2 else leaflight)
 line('Vine stem',[(x+.12*sin(t*7),y-.06*cos(t*5),z-t*1.15) for t in [i/11 for i in range(12)]],.014,leafmat)
# rounded floor platform and actual individual planks
box('Rounded diorama foundation',(0,0,-.17),(6.25,5.95,.34),lightwood,.22)
for row in range(18):
 yy=-2.8+row*.32
 for col in range(3):
  xx=-2.02+col*2.02
  box('Floor plank %02d %d'%(row,col),(xx,yy,.012),(2.008,.313,.025),lightwood,.008)
box('Back wall',(0,2.86,1.85),(6.15,.19,3.7),wall,.11)
# left wall leaves a genuine opening for window
box('Left wall front pier',(-3.04,-2.42,1.85),(.19,.9,3.7),wall,.08)
box('Left wall back pier',(-3.04,1.83,1.85),(.19,2.05,3.7),wall,.08)
box('Window wall below',(-3.04,-.61,.48),(.19,2.72,.96),wall,.03)
box('Window wall above',(-3.04,-.61,3.48),(.19,2.72,.44),wall,.03)
box('Rear skirting',(0,2.73,.13),(6,.07,.22),pale,.025)
# window on left, vertical z and horizontal y
C=group('02 Window and curtains')
glass=mat('Soft blue window',(.65,.82,.88),.2)
box('Window light opening',(-3.035,-.61,2.1),(.025,2.62,2.22),glass,.06)
for y in [-1.9,.68]:box('Window upright',(-2.91,y,2.05),(.16,.10,2.30),lightwood,.04)
for z in [.95,2,3.18]:box('Window cross rail',(-2.89,-.61,z),(.17,2.68,.09),lightwood,.03)
box('Window middle mullion',(-2.88,-.61,2.09),(.17,.075,2.16),lightwood,.025)
box('Deep window sill',(-2.78,-.61,.95),(.48,2.85,.12),cream,.05)
rod('Curtain pole',(-2.63,-2.05,3.35),(-2.63,.85,3.35),.045,lightwood)
for y in [-2.08,.88]:uv('Curtain finial',(-2.63,y,3.35),(.10,.12,.10),lightwood)
# swept pleated curtain surfaces
for side,cy in [(-1,-1.64),(1,.43)]:
 verts=[];faces=[];nu=20;nv=26
 for j in range(nv+1):
  t=j/nv;z=3.26-t*2.39;width=.63*(1-.54*math.exp(-((t-.62)/.20)**2));center=cy+side*.10*sin(t*pi)
  for i in range(nu+1):
   u=i/nu;verts.append((-2.61+.065*cos(u*8*pi),center+(u-.5)*width,z))
 for j in range(nv):
  for i in range(nu):a=j*(nu+1)+i;faces.append((a,a+1,a+nu+2,a+nu+1))
 me=bpy.data.meshes.new('Pleated drape');me.from_pydata(verts,[],faces);o=bpy.data.objects.new('Gathered curtain',me);C.objects.link(o);me.materials.append(pale if side<0 else white)
 sol=o.modifiers.new('Fabric thickness','SOLIDIFY');sol.thickness=.012
 for p in me.polygons:p.use_smooth=True
 for i in range(5):
  bpy.ops.mesh.primitive_torus_add(major_radius=.065,minor_radius=.014,major_segments=16,minor_segments=8,location=(-2.63,cy+(i-2)*.13,3.30),rotation=(pi/2,0,0));put(bpy.context.object,'Curtain ring',cream)
 # face toward camera with small rabbit tieback
 bunny('Curtain rabbit tie',-2.48,cy-.04,1.72,.11)
# furniture bed
C=group('03 Bunny bed')
bx=-1.55;by=.91
box('Bed frame',(bx,by,.30),(1.84,2.63,.43),pale,.17)
box('Mattress',(bx,by,.59),(1.72,2.48,.31),white,.15)
box('Upholstered headboard',(bx,2.21,.97),(1.92,.20,1.2),pale,.30)
box('Headboard inner panel',(bx,2.08,1.03),(1.63,.13,.74),pink,.28)
for dx in [-.49,.49]:
 uv('Bed bunny ear',(bx+dx,2.22,1.75),(.30,.12,.54),pale)
 uv('Bed bunny ear inset',(bx+dx,2.085,1.78),(.18,.035,.34),pink)
box('Footboard',(bx,-.43,.33),(1.87,.15,.46),pale,.11)
for dx in [-.44,.44]:
 o=box('Soft cream pillow',(bx+dx,1.76,.88),(.78,.52,.22),white,.12);o.rotation_euler=(.22,0,(-.12 if dx<0 else .11))
bunny('Bunny cushion',bx,1.57,1.05,.25,white)
uv('Heart pillow left',(bx-.67,1.61,.93),(.20,.12,.22),pink)
# quilt with genuine puffed grid geometry
verts=[];faces=[];nx=64;ny=80
for j in range(ny+1):
 v=j/ny;y=-.39+v*1.97
 for i in range(nx+1):
  u=i/nx;x=bx+(u-.5)*1.79;edge=abs((u-.5)*2);base=.80-.37*max(0,(edge-.82)/.18)**.6
  puff=.052*(sin(u*5*pi)**2)*(sin(v*7*pi)**2)
  verts.append((x,y,base+puff+.015*sin(v*pi)))
for j in range(ny):
 for i in range(nx):a=j*(nx+1)+i;faces.append((a,a+1,a+nx+2,a+nx+1))
me=bpy.data.meshes.new('Quilted surface');me.from_pydata(verts,[],faces);o=bpy.data.objects.new('Puffy stitched pink duvet',me);C.objects.link(o);me.materials.append(pink)
for p in me.polygons:p.use_smooth=True
o.modifiers.new('Quilt thickness','SOLIDIFY').thickness=.08
box('Folded cream blanket',(bx,1.40,.89),(1.80,.29,.18),white,.085)
for xx,yy in [(-1.94,-.05),(-1.25,.18),(-1.82,.60),(-1.14,.93),(-1.63,1.12)]:
 uv('Quilt rabbit applique',(xx,yy,.862),(.095,.074,.008),cream)
 for dx in [-.038,.038]:uv('Quilt rabbit applique ear',(xx+dx,yy+.078,.86),(.024,.055,.008),cream)
# cupboard
C=group('04 Bunny cabinet and decor')
cx=.96;cy=2.37
for dx in [-1.05,1.05]:uv('Cabinet foot',(cx+dx,cy,.16),(.09,.10,.16),pale)
box('Cabinet lower body',(cx,cy,.65),(2.55,.62,.93),pale,.13)
for dx in [-.63,.63]:
 box('Inset cabinet door',(cx+dx,cy-.334,.66),(1.20,.045,.76),pink,.07)
 bunny('Rabbit door knob',cx+dx,cy-.40,.65,.105,cream,False)
box('Cabinet countertop',(cx,cy,1.15),(2.65,.72,.13),pale,.06)
box('Shelf backing',(cx,cy+.24,1.60),(2.58,.10,.91),pale,.12)
for dx in [-1.25,1.25]:box('Upper shelf side',(cx+dx,cy,1.60),(.12,.64,.91),pale,.06)
box('Upper shelf top',(cx,cy,2.04),(2.62,.69,.17),pale,.08)
bookm=[lavender,pale,cream,pink,white]
for j in range(6):
 h=.47+random.random()*.12;x=cx-1.05+j*.155
 o=box('Pastel book',(x,cy-.05,1.23+h/2),(.13,.40,h),bookm[j%5],.014);o.rotation_euler[1]=random.uniform(-.08,.08)
 for z in [1.28,1.23+h-.05]:box('Book spine gold rule',(x,cy-.255,z),(.07,.008,.014),cream,.002)
bunny('Shelf rabbit figurine',cx+.12,cy-.08,1.45,.13)
box('Lilac storage box',(cx+.84,cy-.05,1.44),(.61,.44,.41),lavender,.05);box('Storage box lid',(cx+.84,cy-.05,1.66),(.64,.46,.07),pale,.025);bunny('Box emblem',cx+.84,cy-.282,1.44,.065,cream,False)
pot('Top trailing plant',cx-.99,cy,2.13,.17);vine(cx-.99,cy-.29,2.39)
bunny('Top rabbit toy',cx-.24,cy,2.23,.14)
pot('Tulip vase',cx+1.02,cy,2.13,.13)
for j in range(3):
 xx=cx+.96+j*.07;rod('Tulip stem',(cx+1.02,cy,2.35),(xx,cy,2.68+j*.08),.012,leafmat);uv('Tulip blossom',(xx,cy,2.70+j*.08),(.065,.06,.11),pale)
def picture(name,x,y,z,w=.42,h=.54):
 box(name+' frame',(x,y,z),(w,.06,h),lightwood,.035);box(name+' paper',(x,y-.035,z),(w-.065,.012,h-.065),cream,.008)
 # raised tulip relief
 rod(name+' stem',(x,y-.05,z-h*.22),(x,y-.05,z+h*.10),.009,leafmat);uv(name+' tulip',(x,y-.065,z+h*.13),(w*.15,.018,h*.16),pink)
picture('Cabinet framed rabbit',cx+.43,cy-.03,2.37,.39,.53)
# wall art and shelf
C=group('04b Fixed wall decor')
picture('Tulip wall art',-.90,2.73,3.03,.45,.59);picture('Heart wall art',-.26,2.73,2.95,.38,.46);picture('Right tulip art',2.52,2.73,2.72,.43,.58)
box('Floating plant shelf',(-2.18,2.52,2.95),(.88,.49,.12),lightwood,.04);pot('Shelf greenery',-2.35,2.49,3.01,.15);vine(-2.41,2.27,3.14);bunny('Shelf rabbit',-1.98,2.48,3.12,.12)
# rabbit wall light
em=mat('Warm glowing lamp',(1,.72,.29));p=em.node_tree.nodes.get('Principled BSDF');p.inputs['Emission Color'].default_value=(1,.48,.12,1);p.inputs['Emission Strength'].default_value=2
bunny('Rabbit wall sconce',.22,2.64,2.66,.17,em,False)
# desk and chair as pictured, intentionally no computer
C=group('05 Writing desk and bunny chair')
dx=-2.02;dy=-1.77
box('Writing desktop',(dx,dy,.91),(1.74,.83,.14),lightwood,.18)
box('Pink desk pedestal',(dx-.43,dy,.45),(.57,.69,.87),pale,.10)
for y in [dy-.28,dy+.28]:rod('Desk tapered leg',(dx+.65,y,.03),(dx+.65,y,.86),.055,lightwood)
box('Desk raised rounded back',(dx,dy+.35,1.17),(1.65,.11,.53),pale,.15)
box('Desk small shelf',(dx-.40,dy+.25,1.20),(.69,.31,.075),lightwood,.04)
pot('Desk flower pot',dx-.51,dy+.20,1.25,.12)
for j in range(4):
 xx=dx-.57+j*.05;uv('Desk daisy center',(xx,dy+.15,1.73+sin(j)*.04),(.025,.025,.026),gold)
 for k in range(5):uv('Daisy petal',(xx+.045*sin(k*2*pi/5),dy+.15,1.73+sin(j)*.04+.045*cos(k*2*pi/5)),(.025,.013,.032),white)
box('Pink notebook',(dx+.05,dy-.15,1.005),(.45,.32,.045),pink,.022)
bunny('Notebook rabbit',dx+.05,dy-.18,1.034,.065,white,False)
cyl('Pencil cup',(dx-.65,dy-.26,1.11),.09,.23,cream)
for j in range(4):rod('Pastel pencil',(dx-.70+j*.03,dy-.26,1.12),(dx-.71+j*.04,dy-.26,1.40+(j%2)*.06),.012,bookm[j])
qx=-1.38;qy=-2.34
for x in [-.22,.22]:
 for y in [-.20,.20]:rod('Chair wooden leg',(qx+x*1.15,qy+y*1.15,.025),(qx+x,qy+y,.48),.045,lightwood)
uv('Chair seat cushion',(qx,qy,.53),(.39,.34,.12),pale)
box('Chair bunny back',(qx,qy-.25,.86),(.67,.15,.64),pale,.23)
for s in [-1,1]:uv('Chair bunny ears',(qx+s*.18,qy-.25,1.28),(.105,.08,.30),pale)
uv('Chair bunny tail',(qx,qy-.36,.65),(.095,.075,.095),white)
# bunny igloo, arch cross section mesh open front
C=group('06 Rabbit nook and pet')
hx=2.17;hy=1.15
cyl('Lilac bunny mat',(hx,hy,.065),.76,.10,lavender)
# arch shell 0 to pi with thickness and front/rear surfaces
verts=[];faces=[];N=40
for y in [hy-.49,hy+.49]:
 for r in [.61,.46]:
  for j in range(N+1):a=j*pi/N;verts.append((hx+r*cos(a),y,.39+r*sin(a)))
for j in range(N):
 a=j;b=N+1+j;c=2*(N+1)+j;d=3*(N+1)+j
 faces.extend([(a,a+1,c+1,c),(b,d,d+1,b+1),(a,b,b+1,a+1),(c,c+1,d+1,d)])
me=bpy.data.meshes.new('Open rabbit igloo arch');me.from_pydata(verts,[],faces);o=bpy.data.objects.new('Hollow plush bunny igloo',me);C.objects.link(o);me.materials.append(pale)
for p in me.polygons:p.use_smooth=True
for s in [-1,1]:box('Igloo side bolster',(hx+s*.535,hy,.27),(.16,.99,.35),pale,.075)
box('Igloo padded back',(hx,hy+.45,.50),(1.08,.13,.89),pink,.28)
uv('Igloo quilt cushion',(hx,hy,.19),(.49,.45,.11),pale)
for side in [-1,1]:
 uv('Igloo bunny ear',(hx+side*.27,hy,1.20),(.16,.11,.38),pale);uv('Igloo inner ear',(hx+side*.27,hy-.10,1.22),(.085,.025,.24),pink)
line('Igloo hanging toy string',[(hx,hy-.26,.91),(hx,hy-.26,.64)],.008,cream);uv('Igloo toy ball',(hx,hy-.26,.61),(.06,.06,.065),white)
cyl('Rabbit bowl',(hx+.31,hy-.80,.11),.18,.18,pale);cyl('Bowl recess',(hx+.31,hy-.80,.205),.12,.008,pink)
rx=1.36;ry=.56
uv('Pet bunny body',(rx,ry,.27),(.24,.29,.25),white);bunny('Pet bunny',rx,ry-.17,.46,.17);uv('Rabbit fluffy tail',(rx+.19,ry+.18,.25),(.11,.11,.11),white)
for s in [-1,1]:uv('Rabbit paw',(rx+s*.13,ry-.22,.075),(.09,.11,.07),white)
# round rug
C=group('07 Rug lamp and plant')
rug=mat('Cream fluffy rug',(.95,.82,.70));cloth(rug,210,.65)
cyl('Cream circular rug',(0,-1.04,.053),1.26,.075,rug)
for j in range(12):
 a=j*2*pi/12;x=1.02*cos(a);y=-1.04+1.02*sin(a)
 for k in range(5):uv('Rug woven flower petal',(x+.04*cos(k*2*pi/5),y+.04*sin(k*2*pi/5),.094),(.035,.035,.003),pale)
lx=2.35;ly=-1.83
cyl('Lamp pink foot',(lx,ly,.07),.27,.11,pink);rod('Lamp wooden stem',(lx,ly,.10),(lx,ly,1.37),.035,lightwood)
uv('Lamp scalloped shade',(lx,ly,1.48),(.35,.35,.27),em)
for k in range(8):
 a=k*2*pi/8;uv('Shade scallop',(lx+.25*cos(a),ly+.25*sin(a),1.32),(.13,.13,.07),pale)
for s in [-1,1]:uv('Lamp rabbit ear',(lx+s*.12,ly,1.85),(.08,.065,.23),cream)
line('Lamp pull cord',[(lx+.15,ly-.13,1.31),(lx+.15,ly-.13,1.09)],.008,gold);uv('Lamp pull bead',(lx+.15,ly-.13,1.06),(.04,.04,.05),pale)
pot('Foreground plant',1.74,-2.32,.025,.24);bunny('Flowerpot relief',1.74,-2.555,.20,.075,pale,False)
# stylized original pink-haired character, separate editable parts
C=group('08 Pink chibi girl - stylized approximation')
gx=0;gy=-.92
def P(x,y,z):return(gx+x,gy+y,z)
# shoes socks and legs
for s in [-1,1]:
 uv('Girl leg',P(s*.115,0,.35),(.065,.067,.21),skin)
 cyl('Cream sock',P(s*.115,-.012,.20),.071,.20,white)
 uv('Bunny slipper',P(s*.12,-.055,.095),(.105,.15,.08),pink)
 bunny('Slipper face',gx+s*.12,gy-.174,.10,.047,pale)
# tapered flared dress using radial mesh with pleats
def skirt():
 verts=[];faces=[];N=64
 for z,r in [(.37,.34),(.43,.35),(.75,.20),(.94,.15)]:
  for i in range(N):a=2*pi*i/N;rr=r+.013*cos(a*12);verts.append((gx+rr*cos(a),gy+rr*sin(a),z))
 for j in range(3):
  for i in range(N):a=j*N+i;b=j*N+(i+1)%N;faces.append((a,b,b+N,a+N))
 me=bpy.data.meshes.new('Pleated dress');me.from_pydata(verts,[],faces);o=bpy.data.objects.new('Girl pink pleated skirt',me);C.objects.link(o);me.materials.append(pale);o.modifiers.new('Dress fabric thickness','SOLIDIFY').thickness=.015
 for p in me.polygons:p.use_smooth=True
skirt();uv('Girl cream blouse',P(0,0,.84),(.19,.12,.23),white)
for s in [-1,1]:
 rod('Girl arm',P(s*.16,0,.92),P(s*.33,-.02,.73),.068,white);uv('Girl sleeve cuff',P(s*.29,-.02,.76),(.09,.085,.07),white);uv('Girl hand',P(s*.35,-.04,.72),(.065,.042,.052),skin)
 for j in range(3):uv('Girl finger',P(s*(.38+j*.012),-.045,.73+j*.018),(.029,.023,.012),skin)
uv('Girl neck',P(0,0,1.03),(.075,.065,.12),skin)
# large head hair shell with face forward negative Y
uv('Girl bob hair volume',P(0,.04,1.39),(.46,.32,.48),hair)
uv('Girl face',P(0,-.095,1.38),(.365,.265,.355),skin)
for s in [-1,1]:uv('Girl ear',P(s*.35,-.08,1.36),(.065,.05,.09),skin)
# back hair curved locks and face framing ends
for j in range(13):
 a=pi*j/12;x=.42*cos(a);yy=.04+.23*sin(a)
 uv('Bob rounded hair lock',P(x,yy,1.36),(.105,.10,.34),hair if j%3 else hairlight)
for s in [-1,1]:
 for j in range(2):
  o=uv('Side hair lock',P(s*(.35+.05*j),-.09,1.31-j*.06),(.09,.10,.27),hair);o.rotation_euler[1]=s*-.15
# pointed bangs built as tapering ellipsoid locks
for j in range(7):
 x=(j-3)*.093;z=1.62+.06*abs(j-3)/3
 o=uv('Girl swept fringe',P(x,-.305,z),(.087,.065,.20),hairlight if j%3==0 else hair);o.rotation_euler[1]=(j-3)*-.11
# anime eyes, face glints, eyebrows, tiny smiling mouth
for s in [-1,1]:
 x=s*.145
 uv('Eye cocoa outline',P(x,-.342,1.37),(.087,.025,.105),dark)
 uv('Eye rose iris',P(x,-.365,1.362),(.064,.012,.081),iris)
 uv('Eye pupil',P(x,-.376,1.38),(.028,.007,.05),dark)
 uv('Eye large catchlight',P(x-.021,-.386,1.413),(.024,.009,.028),white)
 uv('Eye tiny catchlight',P(x+.022,-.385,1.345),(.011,.007,.013),white)
 uv('Girl blush',P(s*.25,-.313,1.27),(.058,.008,.026),blush)
 line('Upper lash',[P(x-s*.08,-.36,1.41),P(x,-.37,1.465),P(x+s*.077,-.36,1.42)],.009,dark)
 uv('Eyebrow',P(x,-.325,1.505),(.058,.012,.012),hair)
uv('Little nose',P(0,-.367,1.29),(.020,.018,.024),skin)
uv('Open smile',P(0,-.35,1.22),(.037,.012,.031),dark);uv('Pink tongue',P(0,-.363,1.207),(.025,.007,.012),blush)
# blouse bow and hair accessory
for s in [-1,1]:
 o=uv('Blouse ribbon',P(s*.06,-.139,.96),(.063,.027,.035),pink);o.rotation_euler[1]=s*.4
uv('Ribbon knot',P(0,-.16,.96),(.025,.018,.025),pink)
line('Ribbon tail left',[P(-.01,-.145,.94),P(-.04,-.15,.85)],.016,pink);line('Ribbon tail right',[P(.01,-.145,.94),P(.035,-.15,.85)],.016,pink)
bunny('Girl bunny hairclip',gx+.35,gy-.19,1.65,.073,white)
line('Girl ahoge',[P(.06,.0,1.80),P(.05,.01,1.99),P(.19,.01,1.98)],.022,hair)
# soft studio and named cameras
C=group('09 Lighting and cameras')
ground=mat('Backdrop blush',(0.91,.72,.73));box('Studio ground',(0,0,-.43),(200,200,.1),ground,.0)
world=bpy.data.worlds.new('Pastel studio');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.72,.61,.62,1);world.node_tree.nodes['Background'].inputs[1].default_value=.5;sc.world=world
def area(name,loc,power,size,color):
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;put(o,name,None);o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.data.color=color;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
area('Large warm key',(-3,-4,8),1100,5,(1,.84,.76));area('Soft front fill',(5,-2,5),850,5,(1,.86,.87));area('Window daylight',(-4,1,4),650,3,(.83,.9,1));area('High back rim',(0,5,6),900,4,(1,.78,.69))
def camera(name,loc,target,scale):
 bpy.ops.object.camera_add(location=loc);o=bpy.context.object;put(o,name,None);o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();o.data.type='ORTHO';o.data.ortho_scale=scale;return o
sc.camera=camera('Reference composition camera',(8,-12,9),(0,0,1.40),9.5)
camera('Front inspection camera',(0,-12,7),(0,0,1.2),8.7)
camera('Layout overhead camera',(0,0,12),(0,0,0),8)
sc.render.engine='CYCLES';sc.cycles.samples=40;sc.cycles.use_denoising=True
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='OPTIX';prefs.get_devices()
 gpu=False
 for d in prefs.devices:d.use=d.type!='CPU';gpu|=d.use
 if gpu:sc.cycles.device='GPU'
except Exception as e:print('CPU rendering',str(e))
sc.render.resolution_x=1400;sc.render.resolution_y=1200;sc.render.resolution_percentage=100
sc.view_settings.view_transform='AgX';sc.render.image_settings.file_format='PNG';sc.render.filepath=os.path.join(OUT,'BunnyRoom_preview.png')
exec(compile(open(os.path.join(ROOT,'Tools','bunny_room_layout_update.py'),encoding='utf-8').read(),'bunny_room_layout_update.py','exec'))
sc['reference_note']='Modeled from supplied concept image with requested layout changes: supplied gaming desk/chair/computer at rear; cabinet on right; no pet or igloo; tarot on foreground writing desk. Hidden surfaces and character are approximate.'
sc['units_note']='Meters. Separate furniture collections; model objects remain editable.'
sc.unit_settings.system='METRIC'
# keep viewport immediately useful when file is opened
for screen in bpy.data.screens:
 for a in screen.areas:
  if a.type=='VIEW_3D':a.spaces.active.region_3d.view_perspective='CAMERA';a.spaces.active.shading.type='MATERIAL'
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'PinkBunnyRoom.blend'))
print('SCENE_SAVED',len(bpy.data.objects),'objects',flush=True)
bpy.ops.render.render(write_still=True)
report={'objects':len(bpy.data.objects),'collections':[c.name for c in sc.collection.children],'blend':os.path.join(OUT,'PinkBunnyRoom.blend'),'render':sc.render.filepath,'character':'Editable stylized approximation, no rig','reference':'Latest user supplied pink room image'}
with open(os.path.join(OUT,'scene_report.json'),'w',encoding='utf-8') as f:json.dump(report,f,ensure_ascii=False,indent=2)
print('ROOM_BUILD_COMPLETE',flush=True)
