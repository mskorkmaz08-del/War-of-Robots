"""
Preview a robot with its default weapons fitted (not used by the game).
Usage: python showcase.py <titan|lancer|vanguard|arachne|mammoth|Raptor|Hornet|Juggernaut|Bastion> out.png [yaw,pitch;...]
"""
import sys, os, runpy, tempfile, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
robot, out = sys.argv[1], sys.argv[2]
views = [tuple(float(x) for x in v.split(',')) for v in (sys.argv[3] if len(sys.argv) > 3 else '35,10').split(';')]
wheeled = robot in ('Raptor', 'Hornet', 'Juggernaut', 'Bastion')
tmp = tempfile.mkdtemp()
sys.argv = ['x'] + ([robot] if wheeled else []) + ['--', tmp]
ns = runpy.run_path(os.path.join(HERE, 'wheeled.py' if wheeled else robot + '.py'))
R = ns['R']
import bpy
import mechlib as ml
from mathutils import Vector, Matrix
import weapons as W

LOAD = {'titan': ('heavy_cannon', 'swarm'), 'raptor': ('pulse_laser', 'railgun'), 'arachne': ('gatling', 'mortar'), 'hornet': ('rocket_pods', 'tesla'),
        'juggernaut': ('flamethrower', 'swarm'), 'lancer': ('rail_rifle', 'swarm'), 'bastion': ('heavy_cannon', 'mortar'), 'vanguard': ('pulse_laser', 'swarm'),
        'mammoth': ('gatling', 'railgun'), 'strider': ('gatling', 'swarm'), 'colossus': ('heavy_cannon', 'railgun'), 'scarab': ('pulse_laser', 'mortar')}
SCALE = {'titan': (1, 1, 1), 'lancer': (0.85, 0.85, 0.8), 'vanguard': (0.92, 0.9, 0.85), 'arachne': (0.85, 0.8, 0.9), 'mammoth': (1, 1, 1),
         'strider': (0.72, 0.75, 0.7), 'colossus': (1.15, 1.1, 1.1), 'scarab': (0.85, 0.85, 0.95)}
WK = {'Raptor': 0.85, 'Hornet': 0.68, 'Juggernaut': 1.05, 'Bastion': 0.95}
if wheeled:
    kk = WK[robot]; sc = (0.82 * kk, 0.82 * kk, 0.8 * kk)
else:
    sc = SCALE[robot]
prim, spec = LOAD[robot.lower()]


def B(p):
    return Vector((p[0], p[2], p[1]))


def place(fn, anchor, k, side):
    before = set(bpy.context.scene.objects)
    fn()
    M = Matrix.Translation(B(R.anchors[anchor]['p'])) @ Matrix.Diagonal((k * (-1 if side < 0 else 1), k, k, 1))
    for ob in set(bpy.context.scene.objects) - before:
        ob.data.transform(M)
        if side < 0:
            ob.data.flip_normals()


for a in ('MountL', 'MountR'):
    if a in R.anchors:
        place(getattr(W, prim), a, sc[0], -1 if a.endswith('L') else 1)
backs = [a for a in ('BackL', 'BackR', 'Back') if a in R.anchors]
if spec == 'railgun':
    place(W.railgun, 'Shoulder', sc[1], 1)
else:
    for i, a in enumerate(backs):
        side = -1 if a.endswith('L') else 1
        if spec == 'swarm':
            place(lambda: W.swarm(4 if len(backs) == 1 else 3), a, sc[2], side)
        elif spec == 'mortar':
            place(lambda: W.mortar(3 if len(backs) == 1 else 2), a, sc[2], side)
        else:
            place(W.tesla_coil if i == 0 else W.tesla_bank, a, sc[2], side)

stand = R.stand if not wheeled else ns['STAND']
tz = {'arachne': 5.0, 'mammoth': 6.5}.get(robot.lower(), stand + (1.8 if wheeled else 0.3))
dist = float(os.environ.get('DIST', '40' if not wheeled else '32'))
tgt = tuple(float(v) for v in os.environ['TARGET'].split(',')) if os.environ.get('TARGET') else (0, 0, tz)
ml.render(out, hdri=os.environ.get('HDRI'), views=views, target=tgt, dist=dist, res=(1280, 900), samples=int(os.environ.get('SAMPLES', '24')), lens=45)
