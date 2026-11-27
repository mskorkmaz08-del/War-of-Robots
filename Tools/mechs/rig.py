"""
Shared rigging for the mechs: the game skeleton (hip, knee, ankle from the same
two-bone IK the game uses) and parts exported in their bone frames. The armour,
legs, arms and working hydraulics are in hardsurf.py and parts.py.
"""
import os, sys, math
from mathutils import Vector, Matrix
from mechlib import *
import mechlib as ml


def U(v):
    return to_unity(v)


class Rig:
    def __init__(self, name, stand):
        self.name = name
        self.stand = stand
        self.frames, self.parents, self.anchors, self.rams = {}, {}, {}, []
        self.extra = {'stand': stand}

    def leg(self, tag, hip, ank, bend):
        """Where this leg's foot rests relative to its hip, and which way its knee bends (Unity space)."""
        off = U(Vector(ank) - Vector(hip)); off.y = 0
        self.extra.setdefault('legs', []).append({'name': tag, 'footOffset': list(off), 'bend': list(U(Vector(bend)))})

    # ---- frames -----------------------------------------------------------
    def rigid(self, part, origin, parent):
        self.frames[part] = frame_identity(U(Vector(origin)))
        self.parents[part] = parent

    def spin(self, part, origin, parent, axis=(0, 0, 1), speed=90.0):
        """A part that keeps turning in the game (radar, fan, turbine) about an axis through origin."""
        self.rigid(part, origin, parent)
        self.extra.setdefault('spin', []).append({'name': part, 'axis': list(U(Vector(axis).normalized())), 'speed': speed})

    def anchor(self, name, p, parent, f=None):
        a = {'p': list(U(Vector(p))), 'parent': parent}
        if f is not None:
            a['f'] = list(U(Vector(f).normalized()))
        self.anchors[name] = a

    def out(self, out_dir):
        os.makedirs(out_dir, exist_ok=True)
        return finish(os.path.join(out_dir, self.name + '.bytes'), self.frames, self.parents, self.anchors, self.rams, self.extra)


def leg_pose(hip, ank, thigh, shin, bend_dir=(0, 1, 0)):
    hip, ank = Vector(hip), Vector(ank)
    v = ank - hip; d = v.length; dr = v / d
    a = (thigh ** 2 - shin ** 2 + d * d) / (2 * d)
    h = math.sqrt(max(0.0, thigh ** 2 - a * a))
    fwd = Vector(bend_dir)
    bend = (fwd - dr * fwd.dot(dr)).normalized()
    return hip + dr * a + bend * h


def bone_matrix(a, b):
    """Bone space: X right, Y forward, Z from b back to a (the bone runs down -Z), origin at the midpoint."""
    d = (b - a).normalized()
    fwd = Vector((0, 1, 0))
    y = (fwd - d * fwd.dot(d)).normalized()
    z = -d
    x = y.cross(z).normalized()
    M = Matrix.Identity(4)
    for r in range(3):
        M[r][0], M[r][1], M[r][2] = x[r], y[r], z[r]
    M.translation = (a + b) / 2
    return M


def run(R, out_default, target_z, dist=50):
    out = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else out_default
    R.out(out)
    if '--render' in sys.argv:
        views = [tuple(float(x) for x in v.split(',')) for v in os.environ.get('VIEWS', '35,12;-140,15;90,4;0,4').split(';')]
        render(os.path.join(out, R.name.lower() + '.png'), hdri=os.environ.get('HDRI'), views=views, target=(0, 0, target_z),
               dist=float(os.environ.get('DIST', str(dist))), res=(1280, 900), samples=int(os.environ.get('SAMPLES', '24')), lens=45)
