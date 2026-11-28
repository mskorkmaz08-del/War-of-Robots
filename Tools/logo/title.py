# Blender: 3D game title "WAR OF ROBOTS". python title.py out.png  (env RES, SAMPLES, VARIANT)
import bpy, bmesh, math, os, sys, random
from mathutils import Vector, Euler
S=os.environ.get('LOGO_WORK', os.path.dirname(os.path.abspath(__file__)))
FONT=S+'/fonts/'
out=sys.argv[-1]
E=lambda k,d: os.environ.get(k,d)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc=bpy.context.scene
def nodes(mat): mat.use_nodes=True; return mat.node_tree.nodes, mat.node_tree.links
# ---------------- materials ----------------
def steel(name, col=(0.8,0.82,0.85), rough=(0.18,0.42), brushed=True, wear=True):
    m=bpy.data.materials.new(name); N,L=nodes(m); b=N['Principled BSDF']
    b.inputs['Metallic'].default_value=1; b.inputs['Base Color'].default_value=(*col,1)
    tc=N.new('ShaderNodeTexCoord'); mp=N.new('ShaderNodeMapping'); L.new(tc.outputs['Object'],mp.inputs['Vector'])
    mp.inputs['Scale'].default_value=(2,120 if brushed else 2,2) if brushed else (3,3,3)
    nz=N.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value=6; nz.inputs['Detail'].default_value=8; L.new(mp.outputs['Vector'],nz.inputs['Vector'])
    mr=N.new('ShaderNodeMapRange'); mr.inputs['To Min'].default_value=rough[0]; mr.inputs['To Max'].default_value=rough[1]
    L.new(nz.outputs['Fac'],mr.inputs['Value'])
    if wear:  # scratches: thin bright random lines lower the roughness, grime raises it
        vo=N.new('ShaderNodeTexWave'); vo.wave_type='BANDS'; vo.inputs['Scale'].default_value=40; vo.inputs['Distortion'].default_value=30; vo.inputs['Detail'].default_value=4
        L.new(tc.outputs['Object'],vo.inputs['Vector'])
        cr=N.new('ShaderNodeValToRGB'); cr.color_ramp.elements[0].position=0.93; cr.color_ramp.elements[1].position=0.97
        L.new(vo.outputs['Fac'],cr.inputs['Fac'])
        mx=N.new('ShaderNodeMix'); mx.data_type='FLOAT'; L.new(cr.outputs['Color'],mx.inputs['Factor']); L.new(mr.outputs['Result'],mx.inputs['A']); mx.inputs['B'].default_value=0.08
        g=N.new('ShaderNodeTexNoise'); g.inputs['Scale'].default_value=3; g.inputs['Detail'].default_value=10; L.new(tc.outputs['Object'],g.inputs['Vector'])
        gr=N.new('ShaderNodeValToRGB'); gr.color_ramp.elements[0].position=0.5; gr.color_ramp.elements[0].color=(1,1,1,1); gr.color_ramp.elements[1].position=0.75; gr.color_ramp.elements[1].color=(0.35,0.36,0.38,1)
        L.new(g.outputs['Fac'],gr.inputs['Fac'])
        mc=N.new('ShaderNodeMix'); mc.data_type='RGBA'; mc.blend_type='MULTIPLY'; mc.inputs['Factor'].default_value=1
        mc.inputs['A'].default_value=(*col,1); L.new(gr.outputs['Color'],mc.inputs['B']); L.new(mc.outputs['Result'],b.inputs['Base Color'])
        L.new(mx.outputs['Result'],b.inputs['Roughness'])
    else: L.new(mr.outputs['Result'],b.inputs['Roughness'])
    bv=N.new('ShaderNodeBevel'); bv.inputs['Radius'].default_value=0.012; L.new(bv.outputs['Normal'],b.inputs['Normal'])
    return m
def emit(name,col,strength):
    m=bpy.data.materials.new(name); N,L=nodes(m); b=N['Principled BSDF']
    b.inputs['Base Color'].default_value=(*col,1); b.inputs['Emission Color'].default_value=(*col,1); b.inputs['Emission Strength'].default_value=strength
    return m
def paint(name,col,rough=0.5):
    m=bpy.data.materials.new(name); N,L=nodes(m); b=N['Principled BSDF']
    b.inputs['Base Color'].default_value=(*col,1); b.inputs['Metallic'].default_value=0.15; b.inputs['Roughness'].default_value=rough
    tc=N.new('ShaderNodeTexCoord'); g=N.new('ShaderNodeTexNoise'); g.inputs['Scale'].default_value=2.5; g.inputs['Detail'].default_value=12
    L.new(tc.outputs['Object'],g.inputs['Vector'])
    gr=N.new('ShaderNodeValToRGB'); gr.color_ramp.elements[0].position=0.45; gr.color_ramp.elements[0].color=(*col,1); gr.color_ramp.elements[1].position=0.7; gr.color_ramp.elements[1].color=(col[0]*0.45,col[1]*0.45,col[2]*0.45,1)
    L.new(g.outputs['Fac'],gr.inputs['Fac']); L.new(gr.outputs['Color'],b.inputs['Base Color'])
    bv=N.new('ShaderNodeBevel'); bv.inputs['Radius'].default_value=0.015; L.new(bv.outputs['Normal'],b.inputs['Normal'])
    return m
def hazard():
    m=bpy.data.materials.new('Hazard'); N,L=nodes(m); b=N['Principled BSDF']
    tc=N.new('ShaderNodeTexCoord'); w=N.new('ShaderNodeTexWave'); w.wave_profile='SAW'; w.inputs['Scale'].default_value=3.5; w.bands_direction='DIAGONAL'
    L.new(tc.outputs['Object'],w.inputs['Vector'])
    cr=N.new('ShaderNodeValToRGB'); cr.color_ramp.interpolation='CONSTANT'; cr.color_ramp.elements[0].color=(0.02,0.02,0.02,1); cr.color_ramp.elements[1].position=0.5; cr.color_ramp.elements[1].color=(0.9,0.55,0.02,1)
    L.new(w.outputs['Fac'],cr.inputs['Fac']); L.new(cr.outputs['Color'],b.inputs['Base Color']); b.inputs['Roughness'].default_value=0.6
    return m
STEEL=steel('Steel',(0.85,0.87,0.9),(0.22,0.4))
def chrome(st):
    N,L=st.node_tree.nodes,st.node_tree.links; b=N['Principled BSDF']
    tc=N.new('ShaderNodeTexCoord'); sep=N.new('ShaderNodeSeparateXYZ'); L.new(tc.outputs['Object'],sep.inputs['Vector'])
    mr=N.new('ShaderNodeMapRange'); mr.inputs['From Min'].default_value=float(E('ZMIN','0')); mr.inputs['From Max'].default_value=float(E('ZMAX','0.72'))
    L.new(sep.outputs['Z'],mr.inputs['Value'])
    cr=N.new('ShaderNodeValToRGB'); r=cr.color_ramp
    r.elements[0].position=0.0; r.elements[0].color=(0.55,0.62,0.66,1)
    r.elements[1].position=0.47; r.elements[1].color=(0.14,0.17,0.2,1)
    e=r.elements.new(0.5); e.color=(0.95,0.97,1.0,1)
    e=r.elements.new(1.0); e.color=(0.6,0.66,0.72,1)
    L.new(mr.outputs['Result'],cr.inputs['Fac'])
    mul=N.new('ShaderNodeMix'); mul.data_type='RGBA'; mul.blend_type='MULTIPLY'; mul.inputs['Factor'].default_value=1
    old=b.inputs['Base Color'].links[0].from_socket if b.inputs['Base Color'].links else None
    L.new(cr.outputs['Color'],mul.inputs['A'])
    if old: L.new(old,mul.inputs['B'])
    else: mul.inputs['B'].default_value=(1,1,1,1)
    L.new(mul.outputs['Result'],b.inputs['Base Color'])
chrome(STEEL); STEEL.node_tree.nodes['Principled BSDF'].inputs['Metallic'].default_value=0.75; GUN=steel('Gun',(0.06,0.07,0.085),(0.38,0.6),brushed=False)
TEAL=emit('Teal',(0.05,0.9,0.75),5); TEALD=emit('TealDim',(0.12,0.95,0.82),4)
PLATE=paint('Plate',(0.02,0.03,0.04),0.55); FRAME=steel('Frame',(0.32,0.35,0.39),(0.3,0.55),brushed=False); HAZ=hazard()
DARK=paint('Dark',(0.015,0.018,0.022),0.6)

def link(o): sc.collection.objects.link(o); return o
def text(s, font, size, extrude, bevel, spacing=1.0):
    cu=bpy.data.curves.new(s,'FONT'); cu.body=s; cu.font=bpy.data.fonts.load(FONT+font)
    cu.size=size; cu.extrude=extrude; cu.bevel_depth=bevel; cu.bevel_resolution=3; cu.align_x='CENTER'; cu.space_character=spacing
    o=link(bpy.data.objects.new(s,cu)); o.rotation_euler=(math.radians(90),0,0)
    bpy.context.view_layer.objects.active=o; o.select_set(True)
    bpy.ops.object.convert(target='MESH'); o.select_set(False)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.ops.object.transform_apply(rotation=True); o.select_set(False)
    return o
def assign_front(o, front, side):
    o.data.materials.clear(); o.data.materials.append(side); o.data.materials.append(front)
    for p in o.data.polygons: p.material_index = 1 if abs(p.normal.y) > 0.95 else 0
def box(name,c,s,mat,bev=0.0):
    me=bpy.data.meshes.new(name); bm=bmesh.new(); bmesh.ops.create_cube(bm,size=1); bm.to_mesh(me); bm.free()
    o=link(bpy.data.objects.new(name,me)); o.location=c; o.scale=s; o.data.materials.append(mat)
    if bev: md=o.modifiers.new('b','BEVEL'); md.width=bev; md.segments=3; md.limit_method='ANGLE'
    return o
def prism(name, pts, y0, y1, mat, bev=0.02):   # polygon in XZ extruded along Y
    me=bpy.data.meshes.new(name); bm=bmesh.new()
    a=[bm.verts.new((x,y0,z)) for x,z in pts]; b=[bm.verts.new((x,y1,z)) for x,z in pts]
    bm.faces.new(a[::-1]); bm.faces.new(b); n=len(pts)
    for i in range(n): bm.faces.new((a[i],a[(i+1)%n],b[(i+1)%n],b[i]))
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces); bm.to_mesh(me); bm.free()
    o=link(bpy.data.objects.new(name,me)); o.data.materials.append(mat)
    if bev: md=o.modifiers.new('b','BEVEL'); md.width=bev; md.segments=3; md.limit_method='ANGLE'
    return o
def cut(o, cutter):
    md=o.modifiers.new('cut','BOOLEAN'); md.operation='DIFFERENCE'; md.object=cutter; md.solver='EXACT'
    bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier='cut'); cutter.hide_render=True; cutter.hide_viewport=True

# ---------------- title ----------------
R=text('ROBOTS','Oxanium_wght_800.ttf',1.0,0.16,0.022,1.06)
bb=[Vector(v) for v in R.bound_box]; minx=min(v.x for v in bb); maxx=max(v.x for v in bb); minz=min(v.z for v in bb); maxz=max(v.z for v in bb)
W=maxx-minx; Hh=maxz-minz
# visor slit across the letters, with a glowing core inside
Rpre=R.copy(); Rpre.data=R.data.copy(); link(Rpre); Rpre.hide_render=True
slot=box('slot',((minx+maxx)/2, -0.2, minz+Hh*0.47),(W+1,0.18,0.075),DARK)
cut(R,slot)
assign_front(R,STEEL,GUN)
glow=box('glow',((minx+maxx)/2,-0.125,minz+Hh*0.47),(W*1.02,0.03,0.05),TEAL)
# keep the glow only inside letters: intersect with a copy of the letters
Rc=Rpre
md=glow.modifiers.new('i','BOOLEAN'); md.operation='INTERSECT'; md.object=Rc; md.solver='EXACT'
bpy.context.view_layer.objects.active=glow; bpy.ops.object.modifier_apply(modifier='i'); Rc.hide_render=True
# top line: WAR OF
WO=text('WAR OF','Chakra_Petch_wght_700.ttf',0.36,0.06,0.008,1.35)
WO.location=(0,-0.02,maxz+0.22)
assign_front(WO,TEAL,GUN)
# armoured back plate with cut corners
px=W/2+0.42; pz0=minz-0.32; pz1=maxz+0.12; c=0.28
plate=[(-px,pz0+c),(-px+c,pz0),(px-c,pz0),(px,pz0+c),(px,pz1-c),(px-c,pz1),(-px+c,pz1),(-px,pz1-c)]
prism('Plate',plate,0.10,0.34,PLATE,0.03)
fr=[(x*1.03 if abs(x)>0 else x, z) for x,z in plate]
prism('Frame',[(x*1.0+ (0.06 if x>0 else -0.06), z+(0.06 if z>(pz0+pz1)/2 else -0.06)) for x,z in plate],0.2,0.30,FRAME,0.02)
# WAR OF tab rising from the top edge
tw=0.95; tz=maxz+0.12
tab=[(-tw-0.2,tz),( -tw,tz+0.42),(tw,tz+0.42),(tw+0.2,tz)]
prism('Tab',tab,0.04,0.30,PLATE,0.025)
# teal light strip along the bottom, hazard end caps, bolts
box('Strip',(0,0.05,pz0+0.12),(W*0.6,0.06,0.035),TEAL)
box('StripHousing',(0,0.09,pz0+0.12),(W*0.64,0.06,0.09),GUN,0.01)
for sgn in (-1,1):
    box('Haz',(sgn*(px-0.25),0.05,(pz0+pz1)/2),(0.22,0.06,(pz1-pz0)-0.9),HAZ,0.01)
    for z in (pz0+0.2,pz1-0.2):
        bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=0.045,depth=0.06,location=(sgn*(px-0.12),0.07,z),rotation=(math.radians(90),0,0))
        bo=bpy.context.object; bo.data.materials.append(FRAME)

# panel seams and rivets on the plate, teal edge lights on the tab
for sgn in (-1,1):
    box('SeamV',(sgn*(W/2+0.06),0.085,(pz0+pz1)/2),(0.018,0.03,(pz1-pz0)-0.25),DARK)
    box('TabLight',(sgn*(tw+0.08),0.02,tz+0.2),(0.03,0.03,0.3),TEALD)
box('SeamH',(0,0.085,pz1-0.07),(2*px-0.7,0.03,0.016),DARK)
for i in range(18):
    x=-px+0.55+i*(2*px-1.1)/17
    for z in (pz0+0.04,):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=10,ring_count=6,radius=0.022,location=(x,0.1,z)); r=bpy.context.object; r.data.materials.append(FRAME)
box('TabSlit',(0,0.03,tz+0.36),(tw*1.2,0.03,0.02),TEALD)

# sparks
random.seed(7)
for i in range(int(E('SPARKS','28'))):
    sg=random.choice((-1,1)); x=sg*random.uniform(px*0.85,px*1.25); z=random.uniform(pz0-0.3,pz0+0.5)
    l=random.uniform(0.04,0.22)
    bpy.ops.mesh.primitive_cylinder_add(vertices=4,radius=0.004,depth=l,location=(x,-0.4-random.random()*0.6,z),rotation=(0,math.radians(random.uniform(20,70)),0))
    sp=bpy.context.object; sp.data.materials.append(emit('Spark%d'%i,(1.0,0.35,0.05),random.uniform(3,8)))
# ---------------- render ----------------
sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=int(E('SAMPLES','64')); sc.cycles.use_denoising=True
rx,ry=[int(v) for v in E('RES','1600,800').split(',')]; sc.render.resolution_x=rx; sc.render.resolution_y=ry
sc.render.film_transparent=True; sc.view_settings.view_transform='AgX'; sc.view_settings.look='AgX - Medium High Contrast'
w=bpy.data.worlds.new('W'); sc.world=w; w.use_nodes=True; nt=w.node_tree
env=nt.nodes.new('ShaderNodeTexEnvironment'); env.image=bpy.data.images.load(S+'/hdri/'+E('HDRI','industrial_workshop_foundry')+'.hdr')
nt.links.new(env.outputs['Color'],nt.nodes['Background'].inputs['Color']); nt.nodes['Background'].inputs['Strength'].default_value=float(E('ENV','0.8'))
def light(n,loc,energy,col,size):
    L=bpy.data.lights.new(n,'AREA'); L.energy=energy; L.color=col; L.size=size
    o=link(bpy.data.objects.new(n,L)); o.location=loc; o.rotation_euler=(Vector((0,0,0.4))-Vector(loc)).to_track_quat('-Z','Y').to_euler()
light('Key',(-3,-5,4),500,(1,0.95,0.9),3)
light('Softbox',(0,-12,6),700,(0.9,0.95,1.0),14)
light('Floor',(0,-10,-5),120,(0.3,0.9,0.85),10)
light('RimT',(4,3,3),1500,(0.2,0.95,0.85),3)
light('RimL',(-5,3,1),900,(0.25,0.8,1.0),2)
light('Under',(0,-3,-3),60,(0.2,0.9,0.8),4)
cam=bpy.data.cameras.new('C'); cam.lens=float(E('LENS','50')); co=link(bpy.data.objects.new('C',cam)); sc.camera=co
yaw=math.radians(float(E('YAW','-10'))); d=float(E('DIST','9.5'))
co.location=(math.sin(yaw)*d, -math.cos(yaw)*d, float(E('CAMZ','-1.2')))
co.rotation_euler=(Vector((0,0,0.55))-co.location).to_track_quat('-Z','Y').to_euler()
sc.render.filepath=out; bpy.ops.render.render(write_still=True)
