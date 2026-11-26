"""
Shared rigging and assemblies for the walkers: the game skeleton (hip, knee,
ankle from the same two-bone IK the game uses), parts exported in their bone
frames, live hydraulic rams, and a parametric armoured biped leg.
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

    def anchor(self, name, p, parent, f=None):
        a = {'p': list(U(Vector(p))), 'parent': parent}
        if f is not None:
            a['f'] = list(U(Vector(f).normalized()))
        self.anchors[name] = a

    def out(self, out_dir):
        os.makedirs(out_dir, exist_ok=True)
        return finish(os.path.join(out_dir, self.name + '.bytes'), self.frames, self.parents, self.anchors, self.rams, self.extra)

    # ---- live rams ---------------------------------------------------------
    def live_ram(self, name, Ma, la, bone_a, Mb, lb, bone_b, r):
        """A ram whose sleeve rides bone_a and rod rides bone_b; HydraulicRam slides them in the game."""
        a = Ma @ Vector(la); b = Mb @ Vector(lb)
        d = b - a; L0 = d.length; ax = d / L0
        sl, rl = L0 * 0.6, L0 * 0.55
        lathe(name + 'S', 'Dark', a, ax, [(0, -r), (r * 1.2, -r), (r * 1.2, sl * 0.1), (r, sl * 0.14), (r, sl * 0.86), (r * 1.22, sl * 0.9),
                                          (r * 1.22, sl), (r * 0.6, sl + 0.04), (0, sl + 0.04)], segs=20, name='Sleeve')
        lathe(name + 'S', 'Dark', a - Vector((r * 0.75, 0, 0)), (1, 0, 0), [(0, 0), (r * 0.95, 0), (r * 0.95, r * 1.5), (0, r * 1.5)], segs=16, name='Eye')
        lathe(name + 'R', 'Chrome', b, -ax, [(0, r * 0.5), (r * 0.5, r * 0.5), (r * 0.5, rl), (0, rl)], segs=16, name='Rod')
        lathe(name + 'R', 'Dark', b - Vector((r * 0.75, 0, 0)), (1, 0, 0), [(0, 0), (r * 0.95, 0), (r * 0.95, r * 1.5), (0, r * 1.5)], segs=16, name='Eye')
        fa = self.frames[bone_a]
        up = U(ax).cross(fa[1])
        self.frames[name + 'S'] = frame_look(U(a), U(ax), up)
        self.frames[name + 'R'] = frame_look(U(b), U(ax), up)
        fb = self.frames[bone_b]
        loc = lambda f, p: [(U(p) - f[0]).dot(f[1]), (U(p) - f[0]).dot(f[2]), (U(p) - f[0]).dot(f[3])]
        self.rams.append({'sleeve': name + 'S', 'rod': name + 'R', 'a': bone_a, 'pa': loc(fa, a), 'b': bone_b, 'pb': loc(fb, b)})


def _flipY(flip):
    M = Matrix.Identity(4)
    if flip:
        M[1][1] = -1
    return M


def _fixflip(part, flip):
    if not flip:
        return
    for ob in ml.PARTS.get(part, []):
        if not ob.get('flipped'):
            ob.data.flip_normals()
            ob['flipped'] = True


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


def space(part, M, fn):
    before = len(ml.PARTS.get(part, []))
    fn()
    for ob in ml.PARTS.get(part, [])[before:]:
        ob.data.transform(M)


def bolts_ring(part, centre, axis, radius, n, size=0.07):
    ax = Vector(axis).normalized(); a = ax.orthogonal().normalized(); b = ax.cross(a)
    for k in range(n):
        t = 2 * math.pi * k / n
        p = Vector(centre) + (a * math.cos(t) + b * math.sin(t)) * radius
        lathe(part, 'Metal', p, ax, [(0, -0.02), (size, -0.02), (size, 0.05), (size * 0.7, 0.08), (0, 0.08)], segs=6, name='Bolt')


def stripe(part, centre, size, rot=(0, 0, 0)):
    block(part, 'Accent', centre, size, rot=rot, bevel=0.015, segs=1, name='Stripe')


def biped_leg(R, s, hip, ank, thigh_len, shin_len, ankle_z, k=1.0, style=None, tag=None, flip=False):
    """
    One armoured leg, built in bone space and exported in the game's bone frames.
    k scales the thickness; style tweaks the look:
      guard: 'blade' (cast shin blade) or 'ribbed' (stacked plates)
      toes:  number of toes (2 or 3), toe_len, heel
      cap:   knee cap size
      rams:  (knee ram radius, ankle ram radius)
    """
    st = dict(guard='blade', toes=3, toe_len=2.4, foot_w=1.0, cap=1.0, rams=(0.2, 0.15), vent=True, bend=(0, 1, 0))
    st.update(style or {})
    L = tag or ('L' if s < 0 else 'R')
    hip, ank = Vector(hip), Vector(ank)
    if flip:
        st['bend'] = (0, -1, 0)
    knee = leg_pose(hip, ank, thigh_len, shin_len, st['bend'])
    Mt = bone_matrix(hip, knee)
    Ms = bone_matrix(knee, ank)
    ht, hs = thigh_len / 2, shin_len / 2
    zt, zs = thigh_len / 4.0, shin_len / 4.1   # scale the Titan's proportions along the bone

    def thigh():
        p = 'Thigh' + L
        lathe(p, 'Dark', (s * 0.15 * k, 0, ht), (s, 0, 0), [(0, -0.6 * k), (1.0 * k, -0.6 * k), (1.0 * k, 0.45 * k), (0, 0.45 * k)], segs=32, name='HipBoss')
        loft(p, 'Dark', [((0, 0, 1.35 * zt), 0.95 * k, 1.1 * k, 3.2), ((0, 0.1 * k, 0.4 * zt), 1.02 * k, 1.2 * k, 3.4),
                         ((0, 0.05 * k, -0.9 * zt), 0.88 * k, 0.98 * k, 3.2), ((0, 0, -1.55 * zt), 0.72 * k, 0.8 * k, 3)],
             axis_up=(0, 1, 0), segs=32, name='ThighCore')
        plate(p, 'Primary', (0, 1.2 * k, 0.1 * zt), (1.9 * k, 3.0 * zt, 0.3), rot=(4, 0, 0), curve=(0.2 / k, 0.03), bevel=0.07, taper=0.75)
        plate(p, 'Secondary', (s * 1.08 * k, 0.05, 0.35 * zt), (2.0 * k, 2.2 * zt, 0.2), rot=(0, 0, s * 90), curve=(0.14 / k, 0.02), taper=0.85)
        stripe(p, (s * 1.12 * k, 0.1, 1.0 * zt), (0.05, 1.6 * zt, 0.16))
        block(p, 'Black', (0, 1.42 * k, -0.2 * zt), (1.2 * k, 0.05, 0.05), rot=(4, 0, 0), bevel=0, name='Groove')
        lathe(p, 'Dark', (0, 0, -ht), (1, 0, 0), [(0, -0.85 * k), (0.8 * k, -0.85 * k), (0.8 * k, 0.85 * k), (0, 0.85 * k)], segs=28, name='KneeBoss')
        tube(p, 'Black', [(-s * 0.85 * k, -0.6 * k, 1.4 * zt), (-s * 0.95 * k, -0.9 * k, 0.2 * zt), (-s * 0.9 * k, -0.8 * k, -1.2 * zt), (-s * 0.7 * k, -0.3 * k, -1.9 * zt)], 0.11 * k)
    space('Thigh' + L, Mt @ _flipY(flip), thigh)
    _fixflip('Thigh' + L, flip)
    R.frames['Thigh' + L] = frame_look(U((hip + knee) / 2), U(knee - hip), (0, 0, 1))
    R.parents['Thigh' + L] = 'Thigh' + L

    def kneef():
        p = 'Knee' + L
        c = st['cap']
        disc_joint(p, (0, 0, 0), (1, 0, 0), 1.1 * k, 1.7 * k, bolts=10)
        loft(p, 'Primary', [((0, 0.55 * k, 0.85 * k * c), 0.95 * k, 0.25 * k, 2.4), ((0, 1.05 * k, 0.2 * k), 1.05 * k, 0.35 * k, 2.4),
                            ((0, 1.1 * k, -0.55 * k * c), 0.85 * k, 0.3 * k, 2.4)], axis_up=(0, 1, 0), segs=28, name='KneeCap')
        stripe(p, (0, 1.36 * k, 0.6 * k), (1.0 * k, 0.05, 0.12))
    kneef()
    for ob in ml.PARTS['Knee' + L]:
        ob.data.transform(Matrix.Translation(knee) @ _flipY(flip))
    _fixflip('Knee' + L, flip)
    R.frames['Knee' + L] = frame_identity(U(knee))
    R.parents['Knee' + L] = 'Knee' + L

    def shin():
        p = 'Shin' + L
        loft(p, 'Dark', [((0, -0.1 * k, 1.55 * zs), 0.82 * k, 0.85 * k, 3.2), ((0, -0.15 * k, 0.6 * zs), 0.9 * k, 0.95 * k, 3.4),
                         ((0, -0.05 * k, -0.8 * zs), 0.78 * k, 0.82 * k, 3.2), ((0, 0, -1.7 * zs), 0.62 * k, 0.66 * k, 3)],
             axis_up=(0, 1, 0), segs=32, name='ShinCore')
        if st['guard'] == 'blade':
            loft(p, 'Primary', [((0, 0.85 * k, 1.6 * zs), 0.95 * k, 0.3 * k, 2.4), ((0, 1.05 * k, 0.5 * zs), 1.08 * k, 0.36 * k, 2.5),
                                ((0, 0.95 * k, -0.7 * zs), 0.95 * k, 0.32 * k, 2.5), ((0, 0.75 * k, -1.6 * zs), 0.75 * k, 0.26 * k, 2.4)],
                 axis_up=(0, 1, 0), segs=28, name='ShinGuard')
            block(p, 'Black', (0, 1.36 * k, 0.3 * zs), (1.2 * k, 0.05, 0.05), bevel=0, name='Groove')
            block(p, 'Black', (0, 1.33 * k, -0.5 * zs), (1.1 * k, 0.05, 0.05), bevel=0, name='Groove')
        else:
            # Stacked overlapping plates down the shin, each tilted out at the bottom
            n = 4
            for i in range(n):
                z = (1.5 - i * 0.95) * zs
                w = (1.0 - i * 0.07) * k
                plate(p, 'Primary' if i % 2 == 0 else 'Secondary', (0, 0.95 * k + i * 0.02, z), (1.9 * w, 1.15 * zs, 0.22), rot=(-8, 0, 0), curve=(0.25 / k, 0.0), bevel=0.05, taper=0.88)
        stripe(p, (0, 1.36 * k, -1.1 * zs), (0.8 * k, 0.05, 0.14))
        if st['vent']:
            vent(p, (0, -1.0 * k, 0.5 * zs), (0.9 * k, 0.25, 1.4 * zs), rot=(0, 0, 180), slats=5)
        plate(p, 'Secondary', (s * 0.88 * k, -0.1 * k, 0.0), (1.7 * k, 2.6 * zs, 0.16), rot=(0, 0, s * 90), curve=(0.12 / k, 0.02), taper=0.8)
        lathe(p, 'Dark', (0, 0, -hs), (1, 0, 0), [(0, -0.75 * k), (0.7 * k, -0.75 * k), (0.7 * k, 0.75 * k), (0, 0.75 * k)], segs=28, name='AnkleBoss')
        tube(p, 'Black', [(s * 0.7 * k, -0.7 * k, 1.6 * zs), (s * 0.85 * k, -0.85 * k, 0.3 * zs), (s * 0.75 * k, -0.6 * k, -1.4 * zs)], 0.1 * k)
    space('Shin' + L, Ms @ _flipY(flip), shin)
    _fixflip('Shin' + L, flip)
    R.frames['Shin' + L] = frame_look(U((knee + ank) / 2), U(ank - knee), (0, 0, 1))
    R.parents['Shin' + L] = 'Shin' + L

    def foot():
        p = 'Foot' + L
        g = -ankle_z
        fw = st['foot_w'] * k
        disc_joint(p, (0, 0, 0), (1, 0, 0), 0.85 * k, 1.4 * k, bolts=8)
        loft(p, 'Dark', [((0, -0.9 * fw, g + 0.55 * k), 1.0 * fw, 0.55 * k, 3.5), ((0, 0.2 * fw, g + 0.62 * k), 1.15 * fw, 0.6 * k, 3.5),
                         ((0, 1.2 * fw, g + 0.5 * k), 1.0 * fw, 0.45 * k, 3.5)], axis_up=(0, 0, 1), segs=28, name='FootBody')
        loft(p, 'Primary', [((0, -0.6 * fw, g + 1.0 * k), 0.85 * fw, 0.3 * k, 2.6), ((0, 0.6 * fw, g + 0.98 * k), 0.95 * fw, 0.3 * k, 2.6),
                            ((0, 1.6 * fw, g + 0.7 * k), 0.75 * fw, 0.22 * k, 2.6)], axis_up=(0, 0, 1), segs=24, name='FootTop')
        toes = {3: ((-28, 0.83), (0, 1.0), (28, 0.83)), 2: ((-16, 1.0), (16, 1.0)), 1: ((0, 1.0),)}[st['toes']]
        for ang, f in toes:
            ln = st['toe_len'] * f * fw
            d = Vector((math.sin(math.radians(ang)), math.cos(math.radians(ang)), 0))
            base = Vector((0, 0.8 * fw, g + 0.42 * k))
            tip = base + d * ln
            tw = 0.5 * fw * (1.25 if st['toes'] < 3 else 1.0)
            loft(p, 'Secondary', [(base, tw, 0.36 * k, 3), (base + d * ln * 0.55, tw * 0.96, 0.32 * k, 3), (tip + Vector((0, 0, -0.08 * k)), tw * 0.72, 0.2 * k, 2.6)],
                 axis_up=(0, 0, 1), segs=24, name='Toe')
            loft(p, 'Rubber', [(base + d * 0.2 + Vector((0, 0, -0.33 * k)), tw, 0.1 * k, 4), (tip + Vector((0, 0, -0.3 * k)) - d * 0.1, tw * 0.76, 0.09 * k, 4)],
                 axis_up=(0, 0, 1), segs=20, sub=1, name='ToePad')
        heel = Vector((0, -1.0 * fw, g + 0.4 * k))
        loft(p, 'Secondary', [(heel, 0.75 * fw, 0.35 * k, 3), (heel + Vector((0, -0.9 * fw, -0.08 * k)), 0.55 * fw, 0.22 * k, 2.6)], axis_up=(0, 0, 1), segs=24, sub=2, name='Heel')
        loft(p, 'Rubber', [((0, -1.6 * fw, g + 0.1 * k), 0.6 * fw, 0.1 * k, 4), ((0, 1.4 * fw, g + 0.1 * k), 1.05 * fw, 0.1 * k, 4)], axis_up=(0, 0, 1), segs=24, sub=1, name='Sole')
        stripe(p, (s * 1.0 * fw, 0.1, g + 0.75 * k), (0.05, 1.4 * fw, 0.14))
    foot()
    for ob in ml.PARTS['Foot' + L]:
        ob.data.transform(Matrix.Translation(ank))
    R.frames['Foot' + L] = frame_identity(U(ank))
    R.parents['Foot' + L] = 'Foot' + L

    rk, ra = st['rams']
    for i, xo in enumerate((-0.55 * k, 0.55 * k)):
        fy = -1 if flip else 1
        R.live_ram('RamKnee%s%d' % (L, i), Mt, (xo, -1.15 * k * fy, 0.9 * zt), 'Thigh' + L, Ms, (xo, -0.95 * k * fy, 0.2 * zs), 'Shin' + L, rk)
        R.live_ram('RamAnkle%s%d' % (L, i), Ms, (xo * 1.75, 0.35 * k * fy, -0.2 * zs), 'Shin' + L, Matrix.Translation(ank), (xo * 1.8, 0.85 * k, -0.55 * k), 'Foot' + L, ra)

    R.anchor('Hip' + L, hip, 'Root')
    R.leg(L, hip, ank, st['bend'])
    return knee


def run(R, out_default, target_z, dist=50):
    out = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else out_default
    R.out(out)
    if '--render' in sys.argv:
        views = [tuple(float(x) for x in v.split(',')) for v in os.environ.get('VIEWS', '35,12;-140,15;90,4;0,4').split(';')]
        render(os.path.join(out, R.name.lower() + '.png'), hdri=os.environ.get('HDRI'), views=views, target=(0, 0, target_z),
               dist=float(os.environ.get('DIST', str(dist))), res=(1280, 900), samples=int(os.environ.get('SAMPLES', '24')), lens=45)


# ---------------------------------------------------------------------------
# Upper-body assemblies shared by the bipeds
# ---------------------------------------------------------------------------

def pelvis(R, hip_x, hip_z, k=1.0, ring_z=None, ring_r=None):
    """Cast pelvis, hip drums with their rams and cowls, groin plates and the fixed half of the waist ring."""
    P = 'Hull'
    w = hip_x - 0.6 * k
    loft(P, 'Dark', [((-w, 0, hip_z + 0.25 * k), 0.9 * k, 0.75 * k, 3), ((-w * 0.55, 0, hip_z + 0.3 * k), 1.35 * k, 1.0 * k, 3.2), ((0, 0, hip_z + 0.35 * k), 1.5 * k, 1.1 * k, 3.4),
                     ((w * 0.55, 0, hip_z + 0.3 * k), 1.35 * k, 1.0 * k, 3.2), ((w, 0, hip_z + 0.25 * k), 0.9 * k, 0.75 * k, 3)], axis_up=(0, 0, 1), segs=32, name='PelvisCore')
    loft(P, 'Secondary', [((-w * 0.8, 0.05, hip_z + 0.6 * k), 1.25 * k, 0.6 * k, 2.6), ((0, 0.15 * k, hip_z + 0.75 * k), 1.55 * k, 0.75 * k, 2.8),
                          ((w * 0.8, 0.05, hip_z + 0.6 * k), 1.25 * k, 0.6 * k, 2.6)], axis_up=(0, 0, 1), segs=32, name='PelvisCast')
    for s in (-1, 1):
        hip = Vector((s * hip_x, 0, hip_z))
        block(P, 'Secondary', (s * (hip_x - 0.7 * k), 0, hip_z + 0.15 * k), (0.7 * k, 1.9 * k, 1.5 * k), bevel=0.22 * k, segs=3, name='Yoke')
        disc_joint(P, hip + Vector((s * 0.15 * k, 0, 0)), (s, 0, 0), 1.3 * k, 1.0 * k, bolts=12)
        for zf in (-1, 1):
            ram(P, (s * (hip_x - 1.55 * k), zf * 1.05 * k, hip_z + 1.05 * k), (s * (hip_x - 0.55 * k), zf * 0.75 * k, hip_z + 0.6 * k), 0.17 * k)
        loft(P, 'Primary', [((s * (hip_x + 0.1 * k), 1.15 * k, hip_z + 0.65 * k), 0.85 * k, 0.55 * k, 2.4), ((s * (hip_x + 0.2 * k), 0.0, hip_z + 1.0 * k), 1.0 * k, 0.7 * k, 2.6),
                            ((s * (hip_x + 0.1 * k), -1.25 * k, hip_z + 0.7 * k), 0.8 * k, 0.5 * k, 2.4)], axis_up=(0, 0, 1), segs=28, name='HipCowl')
        stripe(P, (s * (hip_x + 0.85 * k), 0.1, hip_z + 1.05 * k), (0.06, 1.4 * k, 0.16), rot=(0, s * 35, 0))
        tube(P, 'Black', [(s * 0.9 * k, -1.1 * k, hip_z), (s * 1.6 * k, -1.5 * k, hip_z - 0.3 * k), (s * (hip_x - 0.35 * k), -1.2 * k, hip_z - 0.6 * k), (s * (hip_x + 0.15 * k), -0.6 * k, hip_z - 0.5 * k)], 0.11 * k)
    plate(P, 'Primary', (0, 1.25 * k, hip_z - 0.45 * k), (2.1 * k, 1.7 * k, 0.28), rot=(-14, 0, 0), curve=(0.12 / k, 0.05), taper=0.7)
    plate(P, 'Primary', (0, -1.25 * k, hip_z - 0.3 * k), (2.3 * k, 1.4 * k, 0.26), rot=(14, 0, 180), curve=(0.12 / k, 0.05), taper=0.75)
    block(P, 'Black', (0, 1.42 * k, hip_z - 0.25 * k), (1.1 * k, 0.05, 0.06), rot=(-14, 0, 0), bevel=0, name='Groove')
    rz = ring_z if ring_z is not None else hip_z + 1.05 * k
    rr = ring_r if ring_r is not None else 2.0 * k
    lathe(P, 'Dark', (0, 0, rz), (0, 0, 1), [(0, 0), (rr * 0.97, 0), (rr, 0.08), (rr, 0.4), (rr * 0.92, 0.48), (0, 0.48)], segs=48, name='RingBase')
    bolts_ring(P, (0, 0, rz + 0.48), (0, 0, 1), rr * 0.87, 20)
    R.rigid('Hull', (0, 0, R.stand), 'Root')
    return rz + 0.48


def arm(R, s, sh, el, mount, k=1.0, part='Arms', fore_len=1.4, shoulder_r=1.4):
    """Shoulder drum, upper arm with paired rams, elbow drum and an armoured forearm ending in the weapon cradle."""
    A = part
    sh, el, m = Vector(sh), Vector(el), Vector(mount)
    disc_joint(A, sh, (s, 0, 0), shoulder_r * k, 1.3 * k, bolts=12)
    lathe(A, 'Dark', sh - Vector((s * 0.6 * k, 0, 0)), (-s, 0, 0), [(0, 0), (1.0 * k, 0), (1.0 * k, 0.55 * k), (0, 0.55 * k)], segs=32, name='ShoulderBoss')
    up = sh - el
    loft(A, 'Dark', [(sh - up.normalized() * 0.6 * k, 0.85 * k, 0.9 * k, 3), (sh.lerp(el, 0.5), 0.8 * k, 0.85 * k, 3), (el + up.normalized() * 0.45 * k, 0.7 * k, 0.75 * k, 3)],
         axis_up=(0, 1, 0), segs=28, name='UpperArm')
    plate(A, 'Secondary', sh.lerp(el, 0.5) + Vector((s * 0.66 * k, 0.05, 0.1)), (1.3 * k, up.length * 0.7, 0.2), rot=(0, 0, s * 90), curve=(0.2 / k, 0.02))
    for yf in (-1, 1):
        ram(A, sh + Vector((0, yf * 0.72 * k, -0.35 * k)), el + Vector((0, yf * 0.7 * k, 0.35 * k)), 0.16 * k)
    disc_joint(A, el, (s, 0, 0), 0.95 * k, 1.15 * k, bolts=10)
    zc = m.z
    f0, f1 = el.y - 0.75 * k, m.y - 0.4 * k
    fl = f1 - f0
    loft(A, 'Secondary', [(Vector((el.x, f0, zc + 0.15 * k)), 0.95 * k, 1.0 * k, 3.2), (Vector((el.x, f0 + fl * 0.4, zc)), 1.15 * k, 1.15 * k, 3.4),
                          (Vector((el.x, f0 + fl * 0.85, zc)), 1.1 * k, 1.08 * k, 3.4), (Vector((el.x, f1, zc)), 0.9 * k, 0.88 * k, 3.2)],
         axis_up=(0, 0, 1), segs=32, name='Forearm')
    loft(A, 'Primary', [(Vector((el.x, f0 + 0.25 * k, zc + 1.0 * k)), 0.9 * k, 0.26 * k, 2.6), (Vector((el.x, f0 + fl * 0.55, zc + 1.12 * k)), 1.0 * k, 0.3 * k, 2.6),
                        (Vector((el.x, f1 - 0.1, zc + 0.92 * k)), 0.8 * k, 0.22 * k, 2.6)], axis_up=(0, 0, 1), segs=24, name='ForearmTop')
    plate(A, 'Primary', Vector((el.x + s * 1.18 * k, f0 + fl * 0.55, zc)), (fl * 0.85, 1.7 * k, 0.22), rot=(0, 0, s * 90), curve=(0.12 / k, 0.08))
    stripe(A, Vector((el.x + s * 1.32 * k, f0 + fl * 0.55, zc + 0.5 * k)), (0.05, fl * 0.7, 0.14))
    lathe(A, 'Dark', m + Vector((0, -0.25 * k, 0)), (0, 1, 0), [(0.55 * k, 0), (0.95 * k, 0), (0.95 * k, 0.45 * k), (0.55 * k, 0.45 * k)], segs=32, name='Cradle')
    bolts_ring(A, m + Vector((0, 0.15 * k, 0)), (0, 1, 0), 0.68 * k, 8, 0.05)
    tube(A, 'Black', [sh + Vector((-s * 0.5 * k, -0.8 * k, -0.2)), sh + Vector((-s * 0.75 * k, -0.9 * k, -1.4 * k)), el + Vector((-s * 0.7 * k, -0.75 * k, 0.1)), el + Vector((-s * 0.6 * k, -0.1, -0.6 * k))], 0.12 * k)
    R.anchor('Mount' + ('L' if s < 0 else 'R'), m, 'Arms')


def jet_nozzle(part, p, d, k=1.0):
    d = Vector(d).normalized()
    lathe(part, 'Metal', p, d, [(0, -0.3 * k), (0.55 * k, -0.3 * k), (0.6 * k, 0.1 * k), (0.5 * k, 0.15 * k), (0.75 * k, 0.75 * k), (0.68 * k, 0.8 * k), (0.42 * k, 0.25 * k), (0, 0.25 * k)], segs=28, name='Nozzle')
    return Vector(p) + d * 0.8 * k


def _up_in(M):
    """World up expressed in bone space, flattened across the bone."""
    u = M.to_3x3().inverted() @ Vector((0, 0, 1))
    u.z = 0
    if u.length < 1e-3:
        u = Vector((0, 1, 0))
    return u.normalized()


def spider_leg(R, tag, hip, ank, thigh_len, shin_len, ankle_z, bend, k=1.0, claws=3):
    """A long jointed leg with a high knee: armoured spine plates on top, a ram over the knee and a clawed foot."""
    hip, ank = Vector(hip), Vector(ank)
    knee = leg_pose(hip, ank, thigh_len, shin_len, bend)
    Mt = bone_matrix(hip, knee)
    Ms = bone_matrix(knee, ank)
    ht, hs = thigh_len / 2, shin_len / 2
    ut, us = _up_in(Mt), _up_in(Ms)
    side = 1 if hip.x > 0 else -1

    def thigh():
        p = 'Thigh' + tag
        lathe(p, 'Dark', (0, 0, ht - 0.1), (0, 0, 1), [(0, -0.6 * k), (0.62 * k, -0.45 * k), (0.72 * k, 0), (0.62 * k, 0.45 * k), (0, 0.6 * k)], segs=28, name='HipBall')
        loft(p, 'Dark', [((0, 0, ht - 0.5 * k), 0.58 * k, 0.62 * k, 3), ((0, 0, 0), 0.5 * k, 0.55 * k, 3), ((0, 0, -ht + 0.5 * k), 0.42 * k, 0.48 * k, 3)],
             axis_up=tuple(ut), segs=28, name='ThighCore')
        loft(p, 'Primary', [(ut * 0.42 * k + Vector((0, 0, ht - 0.6 * k)), 0.72 * k, 0.32 * k, 2.4), (ut * 0.5 * k + Vector((0, 0, 0.2)), 0.68 * k, 0.36 * k, 2.4),
                            (ut * 0.42 * k + Vector((0, 0, -ht + 0.7 * k)), 0.5 * k, 0.26 * k, 2.4)], axis_up=tuple(ut), segs=28, name='ThighShell')
        for zz in (0.35, -0.35):
            lathe(p, 'Secondary', (0, 0, zz * thigh_len), (0, 0, 1), [(0.5 * k, -0.12), (0.6 * k, -0.08), (0.6 * k, 0.08), (0.5 * k, 0.12)], segs=28, name='Collar')
        lathe(p, 'Dark', (0, 0, -ht), (1, 0, 0), [(0, -0.55 * k), (0.48 * k, -0.55 * k), (0.48 * k, 0.55 * k), (0, 0.55 * k)], segs=24, name='KneeBoss')
    space('Thigh' + tag, Mt, thigh)
    R.frames['Thigh' + tag] = frame_look(U((hip + knee) / 2), U(knee - hip), (0, 0, 1))
    R.parents['Thigh' + tag] = 'Thigh' + tag

    disc_joint('Knee' + tag, knee, (1, 0, 0), 0.7 * k, 1.2 * k, bolts=8)
    lathe('Knee' + tag, 'Primary', knee + Vector((0, 0, 0.45 * k)), (0, 0, 1), [(0, 0), (0.7 * k, 0), (0.5 * k, 0.35 * k), (0, 0.45 * k)], segs=24, name='KneeDome')
    R.frames['Knee' + tag] = frame_identity(U(knee))
    R.parents['Knee' + tag] = 'Knee' + tag

    def shin():
        p = 'Shin' + tag
        loft(p, 'Dark', [((0, 0, hs - 0.3 * k), 0.5 * k, 0.55 * k, 3), ((0, 0, hs * 0.3), 0.46 * k, 0.5 * k, 3), ((0, 0, -hs * 0.6), 0.32 * k, 0.36 * k, 3), ((0, 0, -hs + 0.2), 0.24 * k, 0.26 * k, 3)],
             axis_up=tuple(us), segs=28, name='ShinCore')
        loft(p, 'Primary', [(us * 0.38 * k + Vector((0, 0, hs - 0.35 * k)), 0.62 * k, 0.3 * k, 2.4), (us * 0.45 * k + Vector((0, 0, hs * 0.25)), 0.66 * k, 0.34 * k, 2.4),
                            (us * 0.3 * k + Vector((0, 0, -hs * 0.65)), 0.4 * k, 0.22 * k, 2.4)], axis_up=tuple(us), segs=28, name='ShinShell')
        for i in range(3):
            block(p, 'Black', us * (0.78 - i * 0.05) * k + Vector((0, 0, hs * (0.55 - i * 0.35))), (0.5 * k, 0.05, 0.05), bevel=0, name='Groove')
        lathe(p, 'Secondary', (0, 0, -hs * 0.2), (0, 0, 1), [(0.42 * k, -0.12), (0.52 * k, -0.08), (0.52 * k, 0.08), (0.42 * k, 0.12)], segs=24, name='Collar')
        lathe(p, 'Dark', (0, 0, -hs), (1, 0, 0), [(0, -0.4 * k), (0.32 * k, -0.4 * k), (0.32 * k, 0.4 * k), (0, 0.4 * k)], segs=20, name='AnkleBoss')
    space('Shin' + tag, Ms, shin)
    R.frames['Shin' + tag] = frame_look(U((knee + ank) / 2), U(ank - knee), (0, 0, 1))
    R.parents['Shin' + tag] = 'Shin' + tag

    p = 'Foot' + tag
    g = -ankle_z
    lathe(p, 'Dark', (0, 0, 0), (1, 0, 0), [(0, -0.45 * k), (0.42 * k, -0.45 * k), (0.42 * k, 0.45 * k), (0, 0.45 * k)], segs=20, name='Ankle')
    loft(p, 'Secondary', [((0, 0, 0.1), 0.55 * k, 0.5 * k, 3), ((0, 0, g + 0.45 * k), 0.7 * k, 0.6 * k, 3)], axis_up=(0, 1, 0), segs=24, sub=1, name='Pad')
    angs = {3: (-60, 60, 180), 4: (45, 135, 225, 315), 2: (-90, 90)}[claws]
    for a in angs:
        d = Vector((math.sin(math.radians(a + (0 if side > 0 else 0))), math.cos(math.radians(a)), 0))
        b0 = Vector((0, 0, g + 0.5 * k))
        loft(p, 'Primary', [(b0, 0.32 * k, 0.28 * k, 2.6), (b0 + d * 0.8 * k + Vector((0, 0, 0.1 * k)), 0.26 * k, 0.24 * k, 2.6), (b0 + d * 1.35 * k + Vector((0, 0, -0.42 * k)), 0.12 * k, 0.1 * k, 2.4)],
             axis_up=(0, 0, 1), segs=20, name='Claw')
    loft(p, 'Rubber', [((0, 0, g + 0.12), 0.75 * k, 0.75 * k, 2.2), ((0, 0, g + 0.02), 0.7 * k, 0.7 * k, 2.2)], axis_up=(0, 1, 0), segs=24, sub=1, name='Sole')
    for ob in ml.PARTS[p]:
        ob.data.transform(Matrix.Translation(ank))
    R.frames[p] = frame_identity(U(ank))
    R.parents[p] = p

    R.live_ram('RamKnee%s' % tag, Mt, ut * 0.75 * k + Vector((0, 0, ht * 0.15)), 'Thigh' + tag, Ms, us * 0.62 * k + Vector((0, 0, hs * 0.55)), 'Shin' + tag, 0.16 * k)
    R.anchor('Hip' + tag, hip, 'Root')
    R.leg(tag, hip, ank, bend)
    return knee
