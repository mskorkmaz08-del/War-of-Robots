"""
Assemblies for the second-generation (hard-surface) mechs: legs, pelvis, arms,
jets, with working hydraulics at every joint.

Every moving joint is driven by at least one live hydraulic cylinder whose barrel
rides one bone and whose chrome rod rides the other, so in the game the rods slide
in and out as hips, knees, ankles and shoulders move (HydraulicRam.cs). Joints also
carry geared drive housings (JointGear.cs turns them with the joint angle).
"""
import math
from mathutils import Vector, Matrix
import mechlib as ml
from mechlib import V, lathe, frame_look, frame_identity
from hardsurf import *
from rig import Rig, U, leg_pose, bone_matrix, run


# ---------------------------------------------------------------------------
# Live actuators and gears
# ---------------------------------------------------------------------------

def live_ram(R, name, Ma, la, bone_a, Mb, lb, bone_b, r, frac=0.58):
    """A working cylinder: barrel on bone_a at la, rod eye on bone_b at lb (bone-local points)."""
    a = Ma @ V(la); b = Mb @ V(lb)
    d = b - a; L0 = d.length; ax = d / L0
    sl, rl = L0 * frac, L0 * 0.62
    side = ax.orthogonal().normalized()
    if abs(ax.x) < 0.9:
        side = Vector((1, 0, 0)) - ax * ax.x; side.normalize()
    up = ax.cross(side).normalized()
    S, Rd = name + 'S', name + 'R'
    lathe(S, 'Dark', a, ax, [(0, -r * 0.2), (r * 1.3, -r * 0.2), (r * 1.3, r * 0.7), (r * 1.08, r * 0.95), (r * 1.08, sl - r * 0.8),
                              (r * 1.32, sl - r * 0.6), (r * 1.32, sl), (r * 0.6, sl + r * 0.2), (0, sl + r * 0.2)], segs=20, name='Barrel')
    lathe(S, 'Metal', a + ax * sl * 0.45, ax, [(r * 1.1, 0), (r * 1.22, 0.02), (r * 1.22, r * 0.5), (r * 1.1, r * 0.52)], segs=20, name='Band')
    cyl(S, 'Dark', a, side, r * 1.0, r * 1.7, ch=r * 0.2, segs=14, name='Clevis')
    cyl(S, 'Metal', a, side, r * 0.42, r * 2.3, ch=r * 0.1, segs=10, name='Pin')
    cyl(S, 'Metal', a + ax * sl * 0.75 + up * r * 1.15, up, r * 0.3, r * 0.6, ch=0.01, segs=8, name='Port')
    lathe(Rd, 'Chrome', b, -ax, [(0, r * 0.6), (r * 0.56, r * 0.6), (r * 0.56, rl), (0, rl)], segs=16, name='Rod')
    lathe(Rd, 'Dark', b, -ax, [(0, r * 0.3), (r * 0.8, r * 0.3), (r * 0.8, r * 0.75), (0, r * 0.75)], segs=16, name='RodEnd')
    cyl(Rd, 'Dark', b, side, r * 0.95, r * 1.5, ch=r * 0.2, segs=14, name='Eye')
    cyl(Rd, 'Metal', b, side, r * 0.4, r * 2.1, ch=r * 0.1, segs=10, name='Pin')
    fa = R.frames[bone_a]
    upU = U(ax).cross(fa[1])
    if upU.length < 1e-3:
        upU = U(up)
    R.frames[S] = frame_look(U(a), U(ax), upU)
    R.frames[Rd] = frame_look(U(b), U(ax), upU)
    fb = R.frames[bone_b]
    loc = lambda f, p: [(U(p) - f[0]).dot(f[1]), (U(p) - f[0]).dot(f[2]), (U(p) - f[0]).dot(f[3])]
    R.rams.append({'sleeve': S, 'rod': Rd, 'a': bone_a, 'pa': loc(fa, a), 'b': bone_b, 'pb': loc(fb, b)})


def gear(part, centre, axis, r, width, teeth=None, mat='Metal', hub='Dark'):
    """A spur gear: toothed rim, lightening holes, hub."""
    c = V(centre); ax = V(axis).normalized()
    n = teeth or max(12, int(r * 22))
    a = ax.orthogonal().normalized(); b = ax.cross(a)
    w = width / 2
    prof = []
    lathe(part, mat, c, ax, [(0, -w), (r * 0.86, -w), (r * 0.86, w), (0, w)], segs=n, name='GearBody')
    for k in range(n):
        t = 2 * math.pi * (k + 0.5) / n
        d = a * math.cos(t) + b * math.sin(t)
        tan = ax.cross(d)
        tw = math.pi * r / n * 0.55
        pts = []
        for rr, ww in ((r * 0.84, tw * 1.25), (r * 1.0, tw * 0.7)):
            for sgn in (-1, 1):
                for z in (-w * 0.9, w * 0.9):
                    pts.append(c + d * rr + tan * sgn * ww + ax * z)
        armour(part, mat, [tuple(p) for p in pts], bevel=0.0, seams=False, groove=None, name='Tooth')
    lathe(part, hub, c, ax, [(0, -w - 0.06), (r * 0.32, -w - 0.06), (r * 0.32, w + 0.06), (0, w + 0.06)], segs=16, name='GearHub')
    for k in range(5):
        t = 2 * math.pi * k / 5
        p = c + (a * math.cos(t) + b * math.sin(t)) * r * 0.58
        lathe(part, 'Black', p, ax, [(0, -w - 0.01), (r * 0.15, -w - 0.01), (r * 0.15, w + 0.01), (0, w + 0.01)], segs=10, name='Hole')


def gear_link(R, part, a, b, axis_unity, ratio):
    """JointGear: turn part about its local axis by ratio x the relative angle between bones a and b."""
    R.extra.setdefault('gears', []).append({'part': part, 'a': a, 'b': b, 'axis': list(axis_unity), 'ratio': ratio})


# ---------------------------------------------------------------------------
# Leg
# ---------------------------------------------------------------------------

def _flipY(flip):
    M = Matrix.Identity(4)
    if flip:
        M[1][1] = -1
    return M


def in_space(part, M, fn):
    n0 = count(part)
    fn()
    transform_new(part, n0, M)


def walker_leg(R, s, hip, ank, thigh_len, shin_len, ankle_z, k=1.0, st=None, tag=None):
    """
    A heavy armoured walker leg in four bones (Thigh, Knee, Shin, Foot), all hard-surface.
      st keys: shin ('crest' | 'box' | 'layered'), toes (2|3), foot_len, foot_w,
               knee_rams (count), ankle_rams (count), digi (False), thigh_armour (True)
    """
    o = dict(shin='crest', toes=3, foot_len=1.0, foot_w=1.0, knee_rams=2, ankle_rams=2, thigh_armour=True, heel=True, bend=(0, 1, 0))
    o.update(st or {})
    L = tag or ('L' if s < 0 else 'R')
    hip, ank = V(hip), V(ank)
    flip = o['bend'][1] < 0
    knee = leg_pose(hip, ank, thigh_len, shin_len, o['bend'])
    Mt = bone_matrix(hip, knee) @ _flipY(flip)
    Ms = bone_matrix(knee, ank) @ _flipY(flip)
    ht, hs = thigh_len / 2, shin_len / 2
    T, K, S_, F = 'Thigh' + L, 'Knee' + L, 'Shin' + L, 'Foot' + L

    # ---------------- THIGH (bone space: Z along the bone, +Y front, +X right) ----------------
    def thigh():
        # Hip bearing (rotates with the thigh inside the pelvis housing)
        drum(T, (s * 0.1 * k, 0, ht), (1, 0, 0), 1.0 * k, 1.5 * k, bolts=10)
        # Structural frame: two cut side cheeks joined by a spine web
        for xs in (-1, 1):
            slab(T, 'Dark', [(-0.95 * k, ht + 0.1), (0.9 * k, ht + 0.15), (0.75 * k, -ht + 0.3), (0.0, -ht - 0.25 * k), (-0.7 * k, -ht + 0.25)],
                 0.22 * k, Mx((xs * 0.62 * k, 0, 0), (0, 0, 90)), bevel=0.04, seams=False, groove=None, name='Cheek')
        armour(T, 'Dark', boxpts((1.0 * k, 0.9 * k, thigh_len * 0.75), ch=0.15 * k), Mx((0, -0.1 * k, -0.1)), bevel=0.05, seams=False, groove=None, name='Web')
        # Front armour: a faceted quad plate, wider at the hip, with a centre ridge
        if o['thigh_armour']:
            pts = sec(ht - 0.2, 0.95 * k, 0.32 * k, (0.25 * k, 0.25 * k, 0.05, 0.05), 0, 1.05 * k) + \
                  sec(-ht + 0.75 * k, 0.7 * k, 0.28 * k, (0.25 * k, 0.25 * k, 0.05, 0.05), 0, 0.95 * k) + \
                  [(0, 1.5 * k, ht * 0.35), (0, 1.42 * k, -ht * 0.45)]
            armour(T, 'Primary', pts, bevel=0.06, seam_min=0.6, name='ThighPlate')
            stripe(T, Mx((s * 0.55 * k, 1.36 * k, ht * 0.1), (0, 0, s * 20)), 0.12, thigh_len * 0.45)
        # Outer side armour with a cut lower corner and a bolted edge
        slab(T, 'Secondary', [(-0.8 * k, ht - 0.3), (0.85 * k, ht - 0.1), (0.75 * k, -ht + 0.9 * k), (0.2 * k, -ht + 0.55 * k), (-0.6 * k, -ht + 0.9 * k)],
             0.2 * k, Mx((s * 0.95 * k, 0.15 * k, 0), (0, 0, 90)), bevel=0.05, seam_min=0.5, name='ThighSide')
        bolts_line(T, (s * 1.06 * k, 0.9 * k, ht - 0.3), (s * 1.06 * k, 0.8 * k, -ht + 1.1 * k), 5, (s, 0, 0), 0.06 * k)
        # Lower fork that carries the knee axle
        for xs in (-1, 1):
            armour(T, 'Secondary', boxpts((0.28 * k, 1.3 * k, 1.2 * k), ch=0.3 * k), Mx((xs * 0.72 * k, 0, -ht + 0.2 * k)), bevel=0.04, seams=False, name='Fork')
        # Hoses down the back
        hose(T, [(-s * 0.35 * k, -0.6 * k, ht - 0.2), (-s * 0.45 * k, -0.75 * k, 0), (-s * 0.4 * k, -0.7 * k, -ht + 0.6)], 0.08 * k)
        hose(T, [(-s * 0.1 * k, -0.62 * k, ht - 0.4), (-s * 0.2 * k, -0.8 * k, 0.2), (-s * 0.15 * k, -0.72 * k, -ht + 0.5)], 0.07 * k)
        # Knee drive gear (turns with the thigh against the shin's pinion)
        gear(T, (-s * 0.95 * k, 0, -ht), (1, 0, 0), 0.75 * k, 0.18 * k)
    in_space(T, Mt, thigh)
    R.frames[T] = frame_look(U((hip + knee) / 2), U(knee - hip), (0, 0, 1)); R.parents[T] = T

    # ---------------- KNEE (world-aligned; follows the root rotation) ----------------
    def kneef():
        drum(K, (0, 0, 0), (1, 0, 0), 0.82 * k, 1.25 * k, bolts=8)
        # Knee guard: an angled faceted cap in front of the joint
        f = -1 if flip else 1
        pts = sec(0.9 * k, 0.7 * k, 0.15 * k, 0.1 * k, 0, f * 0.85 * k) + sec(-0.55 * k, 0.6 * k, 0.12 * k, 0.1 * k, 0, f * 0.95 * k) + \
              [(0, f * 1.3 * k, 0.25 * k), (0.45 * k, f * 1.2 * k, 0.2 * k), (-0.45 * k, f * 1.2 * k, 0.2 * k)]
        armour(K, 'Primary', pts, bevel=0.05, seam_min=0.4, name='KneeCap')
        lamp(K, Mx((0, f * 1.22 * k, -0.2 * k), (0, 0, 0 if f > 0 else 180)), 0.35 * k, 0.1 * k, 'Glow')
    kneef()
    transform_new(K, 0, Matrix.Translation(knee))
    R.frames[K] = frame_identity(U(knee)); R.parents[K] = K

    # ---------------- SHIN ----------------
    def shin():
        # Core frame
        armour(S_, 'Dark', boxpts((1.15 * k, 1.1 * k, shin_len * 0.85), ch=0.25 * k, taper=(0.8, 0.85)), Mx((0, -0.1 * k, 0), (180, 0, 0)), bevel=0.05, seams=False, groove=None, name='ShinCore')
        # Knee pinion and the sliding gear housing
        gear(S_, (-s * 0.95 * k, -0.95 * k, hs - 0.1), (1, 0, 0), 0.42 * k, 0.16 * k)
        armour(S_, 'Secondary', boxpts((0.22 * k, 1.6 * k, 1.4 * k), ch=0.35 * k), Mx((-s * 0.72 * k, -0.4 * k, hs - 0.35)), bevel=0.04, seams=False, name='GearCase')
        if o['shin'] == 'crest':
            # Big shin guard: pentagonal section with a sharp front crest, tapering to the ankle
            def pent(z, w, d, y0, cr):
                return [(w, y0, z), (-w, y0, z), (-w, y0 + d, z), (w, y0 + d, z), (0, y0 + d + cr, z)]
            pts = pent(hs - 0.25, 0.9 * k, 0.55 * k, 0.15 * k, 0.45 * k) + pent(-hs + 0.95 * k, 0.72 * k, 0.45 * k, 0.15 * k, 0.32 * k) + \
                  pent(-hs + 0.55 * k, 0.6 * k, 0.3 * k, 0.15 * k, 0.15 * k)
            armour(S_, 'Primary', pts, bevel=0.06, seam_min=0.7, name='ShinGuard')
            stripe(S_, Mx((s * 0.42 * k, 1.03 * k, hs * 0.35), (0, 0, s * 29)), 0.14, shin_len * 0.3)
        elif o['shin'] == 'box':
            pts = sec(hs - 0.2, 0.92 * k, 0.5 * k, (0.35 * k, 0.35 * k, 0.05, 0.05), 0, 0.55 * k) + \
                  sec(-hs + 0.6 * k, 0.75 * k, 0.4 * k, (0.25 * k, 0.25 * k, 0.05, 0.05), 0, 0.5 * k)
            armour(S_, 'Primary', pts, bevel=0.07, seam_min=0.7, name='ShinGuard')
            hazard(S_, Mx((0, 0.92 * k, -hs + 1.3 * k), (8, 0, 0)), 1.1 * k, 0.28 * k)
        else:  # layered: overlapping plates like an armadillo
            n = 4
            for i in range(n):
                z = hs - 0.6 - i * (shin_len - 1.2) / (n - 0.5)
                w = (0.92 - i * 0.06) * k
                pts = sec(z + 0.55 * k, w, 0.2 * k, (0.2 * k, 0.2 * k, 0, 0), 0, 0.8 * k) + sec(z - 0.45 * k, w * 0.96, 0.2 * k, (0.2 * k, 0.2 * k, 0, 0), 0, 1.0 * k)
                armour(S_, 'Primary' if i % 2 == 0 else 'Secondary', pts, bevel=0.05, seam_min=0.5, name='ShinLayer')
        # Side plates
        for xs in (-1, 1):
            slab(S_, 'Secondary', [(-0.75 * k, hs - 0.5), (0.7 * k, hs - 0.3), (0.55 * k, -hs + 0.9 * k), (-0.5 * k, -hs + 1.1 * k)],
                 0.18 * k, Mx((xs * 0.72 * k, 0.05, 0), (0, 0, 90)), bevel=0.04, seam_min=0.5, name='ShinSide')
        # Rear: hoses along the exposed frame
        for xs in (-1, 1):
            hose(S_, [(xs * 0.3 * k, -0.75 * k, hs - 0.3), (xs * 0.35 * k, -0.95 * k, 0), (xs * 0.3 * k, -0.8 * k, -hs + 0.8 * k)], 0.075 * k)
        # Ankle fork
        for xs in (-1, 1):
            armour(S_, 'Dark', boxpts((0.24 * k, 1.0 * k, 1.1 * k), ch=0.3 * k), Mx((xs * 0.62 * k, 0, -hs + 0.3 * k)), bevel=0.04, seams=False, groove=None, name='AnkleFork')
    in_space(S_, Ms, shin)
    R.frames[S_] = frame_look(U((knee + ank) / 2), U(ank - knee), (0, 0, 1)); R.parents[S_] = S_

    # ---------------- FOOT (at the ankle, yaw only) ----------------
    def foot():
        g = -ankle_z
        fl, fw = o['foot_len'] * k, o['foot_w'] * k
        drum(F, (0, 0, 0), (1, 0, 0), 0.6 * k, 1.0 * k, bolts=6)
        # Ankle block: a tapered pyramid from the axle to the sole plate
        armour(F, 'Dark', boxpts((1.1 * k, 1.5 * k, ankle_z), ch=0.2 * k, taper=(0.7, 0.6)), Mx((0, 0, g / 2 - 0.05)), bevel=0.05, seams=False, groove=None, name='AnkleBlock')
        # Main foot: a long faceted block, sloped toward the toe
        pts = sec(g + 0.12, 1.05 * fw, 1.1 * fl, 0.3 * fw, 0, 0.35 * fl) + sec(g + 0.75 * k, 0.9 * fw, 0.8 * fl, 0.25 * fw, 0, 0.1 * fl)
        armour(F, 'Secondary', pts, bevel=0.06, seam_min=0.8, name='FootBody')
        # Top cover
        pts = sec(g + 0.72 * k, 0.75 * fw, 0.65 * fl, 0.2 * fw, 0, 0.35 * fl) + sec(g + 1.0 * k, 0.6 * fw, 0.45 * fl, 0.15 * fw, 0, 0.2 * fl)
        armour(F, 'Primary', pts, bevel=0.05, seam_min=0.6, name='FootTop')
        # Toes: separate hinged blocks with tread
        toes = {3: (-1, 0, 1), 2: (-0.6, 0.6)}[o['toes']]
        for i in toes:
            ang = i * 22
            Mt_ = Mx((i * 0.55 * fw, 1.4 * fl, g), (0, 0, -ang))
            pts = sec(0.08, 0.36 * fw, 0.85 * fl, 0.12 * fw, 0, 0.45 * fl) + sec(0.62 * k, 0.32 * fw, 0.55 * fl, 0.12 * fw, 0, 0.1 * fl)
            armour(F, 'Secondary', pts, Mt_, bevel=0.05, seam_min=0.4, name='Toe')
            armour(F, 'Rubber', sec(0.0, 0.34 * fw, 0.8 * fl, 0.1 * fw, 0, 0.45 * fl) + sec(0.1, 0.34 * fw, 0.8 * fl, 0.1 * fw, 0, 0.45 * fl), Mt_, bevel=0.02, seams=False, groove=None, name='Tread')
            cyl(F, 'Metal', Mt_ @ Vector((0, 0.0, 0.35 * k)), Mt_.to_3x3() @ Vector((1, 0, 0)), 0.16 * k, 0.8 * fw, segs=12, name='ToePin')
        if o['heel']:
            Mh = Mx((0, -1.0 * fl, g))
            pts = sec(0.08, 0.6 * fw, 0.55 * fl, 0.2 * fw, 0, -0.25 * fl) + sec(0.6 * k, 0.5 * fw, 0.4 * fl, 0.15 * fw, 0, 0.0)
            armour(F, 'Secondary', pts, Mh, bevel=0.05, seam_min=0.4, name='Heel')
        armour(F, 'Rubber', sec(g, 1.0 * fw, 1.05 * fl, 0.3 * fw, 0, 0.35 * fl) + sec(g + 0.13, 1.0 * fw, 1.05 * fl, 0.3 * fw, 0, 0.35 * fl), bevel=0.02, seams=False, groove=None, name='Sole')
        stripe(F, Mx((s * 1.0 * fw, 0.3 * fl, g + 0.45 * k), (0, 0, s * -90)), 1.2 * fl, 0.12)
    foot()
    transform_new(F, 0, Matrix.Translation(ank))
    R.frames[F] = frame_identity(U(ank)); R.parents[F] = F

    # ---------------- Working hydraulics ----------------
    fy = -1 if flip else 1
    nk = o['knee_rams']
    xs_k = (0,) if nk == 1 else (-0.4 * k, 0.4 * k)
    for i, xo in enumerate(xs_k):
        live_ram(R, 'RamKnee%s%d' % (L, i), Mt, (xo, -0.75 * k, ht - 0.7), T, Ms, (xo, -0.75 * k, hs - 1.1 * k), S_, 0.2 * k)
    na = o['ankle_rams']
    xs_a = (0,) if na == 1 else (-0.5 * k, 0.5 * k)
    for i, xo in enumerate(xs_a):
        live_ram(R, 'RamAnkle%s%d' % (L, i), Ms, (xo, -0.85 * k, 0.1 * hs), S_,
                 Matrix.Translation(ank), (xo, -0.95 * k * o['foot_len'] * fy, -ankle_z + 1.0 * k), F, 0.15 * k)
    R.anchor('Hip' + L, hip, 'Root')
    R.leg(L, hip, ank, o['bend'])
    return knee, Mt, Ms


# ---------------------------------------------------------------------------
# Pelvis
# ---------------------------------------------------------------------------

def pelvis(R, hip_x, hip_z, k=1.0, ring_r=None, legs=None):
    """
    Cast pelvis: a broad faceted girder between two hip housings, the fixed half of
    the slewing ring on top and a working hip-pitch cylinder to each thigh.
    """
    P = 'Hull'
    w = hip_x - 0.85 * k
    pts = sec(hip_z - 0.7 * k, w * 0.75, 0.9 * k, 0.35 * k) + sec(hip_z + 0.5 * k, w + 0.1, 1.25 * k, 0.45 * k) + sec(hip_z + 1.0 * k, w * 0.8, 1.05 * k, 0.4 * k)
    armour(P, 'Dark', pts, bevel=0.06, seams=False, groove=None, name='Girder')
    # Groin and rear armour
    armour(P, 'Primary', sec(hip_z - 0.9 * k, 0.6 * k, 0.12 * k, 0.1, 0, 1.1 * k) + sec(hip_z + 0.6 * k, 1.1 * k, 0.15 * k, 0.15, 0, 1.35 * k) +
           [(0, 1.7 * k, hip_z), (0, 1.55 * k, hip_z - 0.8 * k)], bevel=0.05, seam_min=0.5, name='Groin')
    armour(P, 'Secondary', sec(hip_z - 0.4 * k, 1.0 * k, 0.12 * k, 0.1, 0, -1.25 * k) + sec(hip_z + 0.7 * k, 1.3 * k, 0.12 * k, 0.15, 0, -1.3 * k),
           bevel=0.05, seam_min=0.5, name='RearPlate')
    for s in (-1, 1):
        hx = s * (hip_x - 0.95 * k)
        # Hip housing: a heavy cast block around the hip bearing
        Mh = Mx((hx, 0, hip_z))
        armour(P, 'Secondary', sec(-1.0 * k, 0.4 * k, 1.1 * k, 0.4 * k) + sec(1.1 * k, 0.4 * k, 1.2 * k, 0.5 * k), Mh, bevel=0.06, seam_min=0.6, name='HipHousing')
        # Top armour over the hip (cowl)
        armour(P, 'Primary', sec(0, 0.9 * k, 1.25 * k, 0.45 * k, s * 0.35 * k, 0) + sec(0.45 * k, 0.6 * k, 0.95 * k, 0.35 * k, s * 0.25 * k, 0),
               Mx((hx, 0, hip_z + 1.05 * k)), bevel=0.05, seam_min=0.6, stripe=((s, 0, 0.3), 0.85), name='HipCowl')
        hose(P, [(s * 0.6 * k, -1.2 * k, hip_z + 0.6 * k), (s * (hip_x - 1.2 * k), -1.5 * k, hip_z), (s * (hip_x - 0.3 * k), -0.9 * k, hip_z - 0.5 * k)], 0.09 * k)
    rr = ring_r or 1.9 * k
    rz = hip_z + 1.0 * k
    lathe(P, 'Dark', (0, 0, rz), (0, 0, 1), [(0, 0), (rr * 0.97, 0), (rr, 0.08), (rr, 0.35), (rr * 0.92, 0.42), (0, 0.42)], segs=48, name='RingBase')
    gear(P, (0, 0, rz + 0.5), (0, 0, 1), rr * 0.82, 0.16, teeth=48, mat='Metal')
    R.rigid('Hull', (0, 0, R.stand), 'Root')
    return rz + 0.42


def hip_ram(R, s, hip, Mt, thigh_len, k=1.0, tag=None):
    """Working hip-pitch cylinder: a lug above and behind the hip to the back of the thigh."""
    L = tag or ('L' if s < 0 else 'R')
    hip = V(hip)
    f = -1 if Mt.determinant() < 0 else 1
    a = hip + Vector((0, -1.25 * k * f, 1.05 * k))
    armour('Hull', 'Dark', boxpts((0.5 * k, 0.7 * k, 0.8 * k), ch=0.15 * k), Mx(a + Vector((0, 0.15 * k * f, 0.25 * k))), bevel=0.04, seams=False, groove=None, name='HipLug')
    live_ram(R, 'RamHip' + L, Matrix.Identity(4), a, 'Hull', Mt, (0, -0.95 * k, thigh_len / 2 - 1.9 * k), 'Thigh' + L, 0.17 * k)


# ---------------------------------------------------------------------------
# Arm (rigid, pitches with the 'Arms' group) and shoulder actuators
# ---------------------------------------------------------------------------

def arm(R, s, sh, el, mount, k=1.0, gauntlet=(1.0, 1.0, 1.0)):
    """
    Shoulder bearing, an open upper-arm frame with twin cylinders, an elbow drum and an
    armoured gauntlet ending at the weapon cradle (the Mount anchor).
    """
    A = 'Arms'
    sh, el, m = V(sh), V(el), V(mount)
    drum(A, sh, (1, 0, 0), 1.05 * k, 1.1 * k, bolts=10)
    gear(A, sh + Vector((-s * 0.7 * k, 0, 0)), (1, 0, 0), 0.85 * k, 0.16 * k)
    # Upper arm: two side plates and a web; cylinders down the front and back
    up = (sh - el).normalized()
    Mu = Matrix.Translation(sh.lerp(el, 0.5)) @ Vector((0, 0, 1)).rotation_difference(up).to_matrix().to_4x4()
    ln = (sh - el).length
    for xs in (-1, 1):
        armour(A, 'Dark', boxpts((0.2 * k, 1.1 * k, ln), ch=0.3 * k), Mu @ Mx((xs * 0.42 * k, 0, 0)), bevel=0.04, seams=False, groove=None, name='ArmCheek')
    armour(A, 'Secondary', boxpts((0.75 * k, 0.7 * k, ln * 0.7), ch=0.2 * k), Mu, bevel=0.04, seam_min=0.5, name='ArmWeb')
    for yf in (-1, 1):
        piston(A, sh + Vector((0, yf * 0.62 * k, -0.4 * k)), el + Vector((0, yf * 0.6 * k, 0.4 * k)), 0.15 * k)
    drum(A, el, (1, 0, 0), 0.8 * k, 1.15 * k, bolts=8)
    # Gauntlet: faceted housing from the elbow forward to the cradle
    gx, gy, gz = gauntlet
    y0, y1 = el.y - 0.8 * k, m.y - 0.35 * k
    Mg = Mx((el.x, 0, m.z))
    pts = [(x, y, z) for y, sc in ((y0, 0.85), (y0 + 0.5 * k, 1.0), (y1 - 0.4 * k, 1.0), (y1, 0.82))
           for (x, z) in sec_xz(1.0 * k * gx * sc, 1.0 * k * gz * sc, 0.35 * k * sc)]
    armour(A, 'Secondary', pts, Mg, bevel=0.06, seam_min=0.8, name='Gauntlet')
    # Top armour plate with a ridge
    pts = [(x, y, z) for y, sc in ((y0 + 0.2 * k, 0.9), (y1 - 0.1 * k, 0.8)) for (x, z) in ((-0.85 * k * gx * sc, 0.85 * k * gz), (0.85 * k * gx * sc, 0.85 * k * gz), (0, 1.3 * k * gz),
                                                                                            (-0.7 * k * gx * sc, 1.12 * k * gz), (0.7 * k * gx * sc, 1.12 * k * gz))]
    armour(A, 'Primary', pts, Mg, bevel=0.05, seam_min=0.5, name='GauntletTop')
    stripe(A, Mx((el.x + s * 1.02 * k * gx, (y0 + y1) / 2, m.z + 0.3 * k), (0, 0, -s * 90)), (y1 - y0) * 0.6, 0.14)
    grille(A, Mx((el.x + s * 1.0 * k * gx, (y0 + y1) / 2, m.z - 0.35 * k), (0, 0, -s * 90)), (y1 - y0) * 0.5, 0.45 * k, slats=4, depth=0.08)
    # Cradle ring
    lathe(A, 'Dark', m + Vector((0, -0.3 * k, 0)), (0, 1, 0), [(0.5 * k, 0), (0.9 * k, 0), (0.9 * k, 0.4 * k), (0.5 * k, 0.4 * k)], segs=32, name='Cradle')
    hose(A, [sh + Vector((-s * 0.5 * k, -0.7 * k, -0.2)), el + Vector((-s * 0.6 * k, -0.8 * k, 0.2)), el + Vector((-s * 0.5 * k, -0.3 * k, -0.6 * k))], 0.1 * k)
    R.anchor('Mount' + ('L' if s < 0 else 'R'), m, 'Arms')


def sec_xz(w, h, ch):
    """Chamfered rectangle in the XZ plane (for sections along Y)."""
    return [(w, h - ch), (w - ch, h), (-w + ch, h), (-w, h - ch), (-w, -h + ch), (-w + ch, -h), (w - ch, -h), (w, -h + ch)]


def loft_y(part, mat, secs, M=I4, **kw):
    """Convex piece through XZ sections at given Y: secs = [(y, w, h, ch, cx, cz)]."""
    pts = []
    for sct in secs:
        y, w, h, ch = sct[:4]
        cx, cz = (sct[4], sct[5]) if len(sct) > 4 else (0, 0)
        pts += [(cx + x, y, cz + z) for x, z in sec_xz(w, h, ch)]
    return armour(part, mat, pts, M, **kw)


def shoulder_rams(R, pairs, k=1.0):
    """Working arm-pitch cylinders from the torso to the arms: pairs = [(torso point, arm point), ...]."""
    for i, (a, b) in enumerate(pairs):
        live_ram(R, 'RamShoulder%d' % i, Matrix.Identity(4), a, 'Torso', Matrix.Identity(4), b, 'Arms', 0.16 * k)


def jet(part, p, d, k=1.0):
    d = V(d).normalized()
    lathe(part, 'Dark', p, d, [(0, -0.35 * k), (0.6 * k, -0.35 * k), (0.62 * k, 0.05 * k), (0.5 * k, 0.12 * k)], segs=24, name='JetMount')
    lathe(part, 'Metal', p, d, [(0.45 * k, 0.1 * k), (0.52 * k, 0.1 * k), (0.78 * k, 0.8 * k), (0.72 * k, 0.85 * k), (0.42 * k, 0.25 * k)], segs=28, name='Nozzle')
    lathe(part, 'Hot', p, d, [(0, 0.2 * k), (0.42 * k, 0.24 * k), (0, 0.26 * k)], segs=20, name='NozzleCore')
    for kk in range(10):
        t = 2 * math.pi * kk / 10
        a = d.orthogonal().normalized(); b = d.cross(a)
        q = V(p) + d * 0.45 * k + (a * math.cos(t) + b * math.sin(t)) * 0.66 * k
        armour(part, 'Dark', [tuple(q + d * z + (a * math.cos(t) + b * math.sin(t)) * rr + (b * math.cos(t) - a * math.sin(t)) * ww)
                               for z in (-0.3 * k, 0.3 * k) for rr in (0, 0.08 * k) for ww in (-0.05 * k, 0.05 * k)],
               bevel=0, seams=False, groove=None, name='Petal')
    return V(p) + d * 0.85 * k


# ---------------------------------------------------------------------------
# Spider leg (long, high knee) for the four-legged Arachne
# ---------------------------------------------------------------------------

def _up_in(M):
    u = M.to_3x3().inverted() @ Vector((0, 0, 1))
    u.z = 0
    if u.length < 1e-3:
        u = Vector((0, 1, 0))
    return u.normalized()


def spider_leg(R, tag, hip, ank, thigh_len, shin_len, ankle_z, bend, k=1.0, claws=3):
    """
    A long hard-surface leg: box-section beams with armour spines on top, exposed
    frame and hoses underneath, a geared knee, a working knee cylinder over the top
    and a hip cylinder from the body, and a clawed foot.
    """
    hip, ank = V(hip), V(ank)
    knee = leg_pose(hip, ank, thigh_len, shin_len, bend)
    Mt = bone_matrix(hip, knee)
    Ms = bone_matrix(knee, ank)
    ht, hs = thigh_len / 2, shin_len / 2
    # local frames with +Y = the leg's top side
    def topframe(M):
        u = _up_in(M)
        ang = math.atan2(-u.x, u.y)
        return M @ Matrix.Rotation(ang, 4, 'Z')
    Mt2, Ms2 = topframe(Mt), topframe(Ms)
    T, K, S_, F = 'Thigh' + tag, 'Knee' + tag, 'Shin' + tag, 'Foot' + tag

    def thigh():
        armour(T, 'Dark', ngon(ht - 0.2, 0.75 * k, 8, 22.5) + ngon(ht + 0.5 * k, 0.6 * k, 8, 22.5), bevel=0.04, seams=False, groove=None, name='HipBall')
        drum(T, (0, 0, ht), (1, 0, 0), 0.7 * k, 1.3 * k, bolts=8)
        # Box beam, tapering toward the knee
        armour(T, 'Dark', sec(ht - 0.4, 0.5 * k, 0.55 * k, 0.15 * k) + sec(-ht + 0.4, 0.38 * k, 0.42 * k, 0.12 * k), bevel=0.04, seams=False, groove=None, name='Beam')
        # Top armour spine: faceted, with a ridge
        armour(T, 'Primary', sec(ht - 0.7, 0.72 * k, 0.18 * k, 0.1 * k, 0, 0.62 * k) + sec(-ht + 0.9, 0.5 * k, 0.15 * k, 0.08 * k, 0, 0.5 * k) +
               [(0, 1.0 * k, ht * 0.35), (0, 0.85 * k, -ht * 0.5)], bevel=0.05, seam_min=0.5, name='Spine')
        stripe(T, Mx((0, 0.93 * k, 0.1), (-4, 0, 0)), 0.16, thigh_len * 0.35)
        for xs in (-1, 1):
            slab(T, 'Secondary', [(-0.55 * k, ht - 0.8), (0.45 * k, ht - 0.6), (0.3 * k, -ht + 1.0), (-0.4 * k, -ht + 1.2)], 0.14 * k,
                 Mx((xs * 0.52 * k, 0.05, 0), (0, 0, 90)), bevel=0.03, seam_min=0.4, name='Side')
            hose(T, [(xs * 0.25 * k, -0.6 * k, ht - 0.6), (xs * 0.3 * k, -0.75 * k, 0), (xs * 0.25 * k, -0.55 * k, -ht + 0.6)], 0.07 * k)
        for z in (ht * 0.4, -ht * 0.3):
            armour(T, 'Metal', sec(z - 0.08, 0.54 * k, 0.6 * k, 0.15 * k) + sec(z + 0.08, 0.54 * k, 0.6 * k, 0.15 * k), bevel=0, seams=False, groove=None, name='Collar')
        gear(T, (0.45 * k, 0, -ht), (1, 0, 0), 0.6 * k, 0.15 * k)
    in_space(T, Mt2, thigh)
    R.frames[T] = frame_look(U((hip + knee) / 2), U(knee - hip), (0, 0, 1)); R.parents[T] = T

    drum(K, knee, (1, 0, 0), 0.65 * k, 1.05 * k, bolts=8)
    armour(K, 'Primary', sec(0.4 * k, 0.55 * k, 0.55 * k, 0.2 * k) + sec(0.75 * k, 0.4 * k, 0.4 * k, 0.15 * k) + [(0, 0, 1.25 * k)],
           Mx(knee), bevel=0.04, seam_min=0.3, name='KneeSpike')
    R.frames[K] = frame_identity(U(knee)); R.parents[K] = K

    def shin():
        armour(S_, 'Dark', sec(hs - 0.3, 0.45 * k, 0.5 * k, 0.12 * k) + sec(-hs * 0.4, 0.32 * k, 0.36 * k, 0.1 * k) + sec(-hs + 0.25, 0.2 * k, 0.22 * k, 0.06 * k),
               bevel=0.03, seams=False, groove=None, name='Beam')
        armour(S_, 'Primary', sec(hs - 0.4, 0.62 * k, 0.16 * k, 0.08 * k, 0, 0.5 * k) + sec(-hs * 0.55, 0.38 * k, 0.12 * k, 0.06 * k, 0, 0.32 * k) +
               [(0, 0.9 * k, hs * 0.3)], bevel=0.04, seam_min=0.4, name='Spine')
        for xs in (-1, 1):
            slab(S_, 'Secondary', [(-0.45 * k, hs - 0.6), (0.4 * k, hs - 0.5), (0.25 * k, -hs * 0.35), (-0.3 * k, -hs * 0.45)], 0.12 * k,
                 Mx((xs * 0.45 * k, 0.0, 0), (0, 0, 90)), bevel=0.03, seam_min=0.4, name='Side')
        hazard(S_, Mx((0, 0.58 * k, hs * 0.05), (-6, 0, 0)), 0.5 * k, 0.6, n=3)
        gear(S_, (0.45 * k, 0.55 * k, hs - 0.1), (1, 0, 0), 0.35 * k, 0.14 * k)
        hose(S_, [(0.15 * k, -0.55 * k, hs - 0.4), (0.2 * k, -0.45 * k, 0), (0.1 * k, -0.3 * k, -hs * 0.6)], 0.06 * k)
        for z in (hs * 0.3, -hs * 0.6):
            armour(S_, 'Metal', sec(z - 0.07, 0.44 * k - z * 0.02, 0.48 * k - z * 0.02, 0.1 * k) + sec(z + 0.07, 0.44 * k - z * 0.02, 0.48 * k - z * 0.02, 0.1 * k),
                   bevel=0, seams=False, groove=None, name='Collar')
    in_space(S_, Ms2, shin)
    R.frames[S_] = frame_look(U((knee + ank) / 2), U(ank - knee), (0, 0, 1)); R.parents[S_] = S_

    g = -ankle_z
    drum(F, (0, 0, 0), (1, 0, 0), 0.38 * k, 0.75 * k, bolts=6)
    armour(F, 'Dark', ngon(0, 0.5 * k, 6) + ngon(g + 0.4 * k, 0.75 * k, 6), bevel=0.04, seams=False, groove=None, name='Pad')
    angs = {3: (-60, 60, 180), 4: (45, 135, 225, 315), 2: (-90, 90)}[claws]
    for a in angs:
        d = Vector((math.sin(math.radians(a)), math.cos(math.radians(a)), 0))
        sd = Vector((d.y, -d.x, 0))
        b0 = Vector((0, 0, g + 0.45 * k))
        pts = []
        for p, w, h in ((b0, 0.32 * k, 0.3 * k), (b0 + d * 0.85 * k + Vector((0, 0, 0.12 * k)), 0.26 * k, 0.26 * k), (b0 + d * 1.5 * k + Vector((0, 0, -0.45 * k)), 0.06 * k, 0.05 * k)):
            for sx in (-1, 1):
                for sz in (-1, 1):
                    pts.append(tuple(p + sd * sx * w + Vector((0, 0, sz * h))))
        armour(F, 'Primary', pts, bevel=0.03, seam_min=10, name='Claw')
    armour(F, 'Rubber', ngon(g, 0.72 * k, 8) + ngon(g + 0.12, 0.72 * k, 8), bevel=0.02, seams=False, groove=None, name='Sole')
    transform_new(F, 0, Matrix.Translation(ank))
    R.frames[F] = frame_identity(U(ank)); R.parents[F] = F

    live_ram(R, 'RamKnee%s' % tag, Mt2, (0, 1.15 * k, ht * 0.1), T, Ms2, (0, 0.95 * k, hs * 0.6), S_, 0.17 * k)
    R.anchor('Hip' + tag, hip, 'Root')
    R.leg(tag, hip, ank, bend)
    return knee, Mt2, Ms2
