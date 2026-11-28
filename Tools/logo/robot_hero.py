# python keyart.py <robot> out.png   env: YAW CAMZ DIST TZ LENS ROLL SAMPLES RES
import sys, os, math, runpy
M=os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','mechs'); sys.path.insert(0,M)
import bpy, mechlib as ml
from mathutils import Vector, Euler
E=lambda k,d: float(os.environ.get(k,d))
def keyart(path, hdri=None, views=None, target=None, dist=None, res=None, samples=None, lens=None):
    sc=bpy.context.scene
    sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=int(E('SAMPLES',48)); sc.cycles.use_denoising=True
    rx,ry=[int(v) for v in os.environ.get('RES','900,1200').split(',')]
    sc.render.resolution_x, sc.render.resolution_y = rx, ry
    sc.render.film_transparent=True
    sc.view_settings.view_transform='AgX'; sc.view_settings.look='AgX - Medium High Contrast'
    w=bpy.data.worlds.new('W'); sc.world=w; w.use_nodes=True
    w.node_tree.nodes['Background'].inputs['Color'].default_value=(0.05,0.075,0.09,1)
    w.node_tree.nodes['Background'].inputs['Strength'].default_value=0.6
    # shadow-catcher ground so the feet sit on something
    bpy.ops.mesh.primitive_plane_add(size=400); g=bpy.context.object; g.is_shadow_catcher=True
    def light(name,kind,energy,loc,col,size=1.0,rot=None):
        L=bpy.data.lights.new(name,kind); L.energy=energy; L.color=col
        if kind=='AREA': L.size=size
        if kind=='SUN': L.angle=0.03
        o=bpy.data.objects.new(name,L); sc.collection.objects.link(o); o.location=loc
        if rot is None:
            o.rotation_euler=(Vector((0,0,TZ))-Vector(loc)).to_track_quat('-Z','Y').to_euler()
        else: o.rotation_euler=rot
        return o
    TZ=E('TZ',6)
    # cool sky key from high front-left, teal rim lights behind both sides, warm low bounce
    light('Key','SUN',2.6,(0,0,0),(0.85,0.92,1.0),rot=Euler((math.radians(40),0,math.radians(-150))))
    light('RimL','AREA',18000,(-14,-12,TZ+6),(0.2,0.95,0.85),size=6)
    light('RimR','AREA',18000,(14,-12,TZ+4),(0.25,0.85,1.0),size=6)
    light('Fill','AREA',1500,(6,16,1.0),(1.0,0.75,0.5),size=8)
    cam=bpy.data.cameras.new('Cam'); cam.lens=E('LENS',24)
    co=bpy.data.objects.new('Cam',cam); sc.collection.objects.link(co); sc.camera=co
    yaw=math.radians(E('YAW',28)); d=E('DIST',16)
    co.location=Vector((math.sin(yaw)*d, math.cos(yaw)*d, E('CAMZ',1.2)))
    q=(Vector((0,0,TZ))-co.location).to_track_quat('-Z','Y').to_euler()
    co.rotation_euler=q
    co.rotation_euler.rotate_axis('Z', math.radians(E('ROLL',-5)))
    sc.render.filepath=path; bpy.ops.render.render(write_still=True)
    return [path]
ml.render=keyart
robot,out=sys.argv[1],sys.argv[2]
sys.argv=['showcase.py',robot,out]
runpy.run_path(M+'/showcase.py',run_name='__main__')
