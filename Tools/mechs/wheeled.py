"""
The wheeled robots, third generation: four machines that share only the drive
technology (sprung wheels on independent arms, a slewing column, working shoulder
cylinders) and are otherwise designed from scratch, each for its own job.

  RAPTOR      assault tank: a low shark-nosed raid hull with a bull bar, a raised
              engine deck with twin exhaust stacks and big rocket boosters, chunky
              off-road tyres. A gunner torso with a visor head.
  HORNET      light raider: a slim pod on four wheels hung far out on exposed
              wishbones like a racer, a segmented wasp tail carrying the boosters,
              turbine-fan rims. An insect torso with compound eyes, wing cases and
              feelers.
  JUGGERNAUT  siege crawler: a towering eight-wheeled fortress with a toothed V
              plough, slab skirts, fuel tanks and smoking stacks, steel disc wheels.
              A hunched brute torso with the head sunk between huge shoulders and a
              glowing furnace grille.
  BASTION     artillery tank: a long flat gun deck behind a dozer blade, four fold-down
              stabiliser jacks, a rack of shells, star-ribbed wheels. A tall observation
              tower torso with an optical rangefinder for a head.

Usage: wheeled.py <Raptor|Hornet|Juggernaut|Bastion> [-- out] [--render]
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mechlib import reset
from mathutils import Vector, Matrix
from hardsurf import *
from parts import *

NAME = [a for a in sys.argv[1:] if a in ('Raptor', 'Hornet', 'Juggernaut', 'Bastion')][0]

# Unity-space numbers shared with MechModels.Spec (waist, hull box, wheel radius).
SPECS = {
    'Raptor': dict(stand=2.6, k=0.85, waist=(0, 2.35, -0.6), hull=(5.4, 2.0, 10.4), hull_c=(0, 0.8, 0.2), wx=3.3, wz=(3.2, 0, -3.2), r=1.4, w=1.15,
                   deck=1.55, wheel='offroad'),
    'Hornet': dict(stand=2.2, k=0.68, waist=(0, 2.05, -0.5), hull=(4.4, 1.6, 8.4), hull_c=(0, 0.7, 0), wx=3.35, wz=(2.7, -2.5), r=1.2, w=0.9,
                   deck=1.3, wheel='fan'),
    'Juggernaut': dict(stand=3.0, k=1.05, waist=(0, 2.85, -0.9), hull=(6.4, 2.6, 13.4), hull_c=(0, 1.0, 0.4), wx=3.4, wz=(4.6, 1.6, -1.4, -4.4), r=1.5, w=1.35,
                       deck=2.05, wheel='disc'),
    'Bastion': dict(stand=2.8, k=0.95, waist=(0, 2.7, -1.0), hull=(6.0, 2.4, 11.6), hull_c=(0, 1.0, 0), wx=2.9, wz=(3.6, 0, -3.6), r=1.35, w=1.2,
                    deck=1.92, wheel='star'),
}
S = SPECS[NAME]
STAND, k = S['stand'], S['k']

reset()
R = Rig(NAME, STAND)


def B(x, y, z):
    """Unity root-local -> Blender world."""
    return Vector((x, z, y + STAND))


W = B(*S['waist'])


def Wp(x, y, z):
    """Unity offsets from the waist, scaled by k -> Blender world."""
    return W + Vector((x, z, y)) * k


def K(pts):
    """Scale local torso points by k."""
    return [(x * k, y * k, z * k) for x, y, z in pts]


H = 'Hull'
hw, hh, hl = S['hull'][0] / 2, S['hull'][1] / 2, S['hull'][2] / 2
hc = B(*S['hull_c'])
inner = hw * 0.64
MH = Matrix.Translation(hc)


def hsec(y, w, zb, zt, bw):
    """A hexagonal hull cross-section at y: narrow keel, wide waist, top."""
    return [(x, y, z) for x, z in ((bw, zb), (-bw, zb), (w, (zb + zt) * 0.5 - hh * 0.1), (-w, (zb + zt) * 0.5 - hh * 0.1), (w * 0.92, zt), (-w * 0.92, zt))]


def ring_xz(y, cx, cz, r, n=8, rot=22.5, sx=1.0):
    """An n-gon section in the XZ plane at depth y."""
    return [(cx + math.cos(math.radians(rot) + 2 * math.pi * i / n) * r * sx, y, cz + math.sin(math.radians(rot) + 2 * math.pi * i / n) * r) for i in range(n)]


def bar(part, mat, a, b, t, name='Bar'):
    """A straight structural bar of square section t between two points."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    u = d.orthogonal().normalized() * t / 2
    v = d.cross(u).normalized() * t / 2
    armour(part, mat, [tuple(p + du + dv) for p in (a, b) for du in (u, -u) for dv in (v, -v)], bevel=min(0.03, t * 0.15), seams=False, groove=None, name=name)


def booster(p, d, i, housing=True):
    """A rocket booster nozzle at p firing along -Y, with its housing and glow."""
    if housing:
        armour(H, 'Secondary', boxpts((d * 1.35, 0.9, d * 1.25), ch=0.25, endch=0.05), Mx(p + Vector((0, 0.6, 0)), (90, 0, 0)), bevel=0.04, seam_min=0.4, name='BoosterHousing')
    lathe(H, 'Metal', p + Vector((0, 0.35, 0)), (0, -1, 0), [(0, 0), (d * 0.5, 0), (d * 0.55, 0.35), (d * 0.46, 0.4), (d * 0.62, 0.9), (d * 0.54, 0.93), (d * 0.36, 0.4), (0, 0.4)], segs=24, name='Booster')
    lathe(H, 'Hot', p + Vector((0, -0.07, 0)), (0, -1, 0), [(0, 0), (d * 0.36, 0), (0, 0.01)], segs=20, name='BoosterGlow')
    R.anchor('Booster%d' % i, p + Vector((0, -0.6, 0)), 'Root', (0, -1, 0))


def stack(part, base, height, r, glow=True):
    """An upright exhaust stack: flanged pipe, heat shield, glowing rim."""
    lathe(part, 'Dark', base, (0, 0, 1), [(0, 0), (r * 1.5, 0), (r * 1.5, 0.15), (r, 0.25), (r, height - 0.2), (r * 1.12, height - 0.15), (r * 1.12, height), (r * 0.8, height), (r * 0.8, height - 0.3), (0, height - 0.3)], segs=20, name='Stack')
    armour(part, 'Metal', [tuple(base + Vector((x, y, z))) for x in (-r * 1.25, r * 1.25) for y in (r * 1.05, r * 1.35) for z in (height * 0.3, height * 0.75)], bevel=0.02, seams=False, groove=None, name='HeatShield')
    if glow:
        lathe(part, 'Hot', base + Vector((0, 0, height - 0.28)), (0, 0, 1), [(0, 0), (r * 0.78, 0), (0, 0.02)], segs=16, name='StackGlow')


# ===========================================================================
# Wheels: the same sprung-wheel technology, four different designs
# ===========================================================================
def tyre(P, hub, r, w, lugs, lug_h, chevron=True):
    prof = [(r * 0.55, -w / 2), (r * 0.88, -w / 2), (r * 0.96, -w * 0.42), (r * 0.99 - lug_h, -w * 0.28), (r * 0.99 - lug_h, w * 0.28), (r * 0.96, w * 0.42), (r * 0.88, w / 2), (r * 0.55, w / 2)]
    lathe(P, 'Rubber', hub, (1, 0, 0), prof, segs=40, name='Tyre')
    for t in range(lugs):
        ang = 2 * math.pi * t / lugs
        rad = Vector((0, math.sin(ang), math.cos(ang)))
        tan = Vector((0, math.cos(ang), -math.sin(ang)))
        if chevron:
            for sd in (-1, 1):
                c = hub + Vector((sd * w * 0.24, 0, 0)) + rad * (r * 0.99 - lug_h)
                pts = [tuple(c + Vector((dx, 0, 0)) + tan * (dt + dx * 0.4 * sd) + rad * dr) for dx in (-w * 0.2, w * 0.2) for dt in (-0.12, 0.12) for dr in (-0.04, lug_h)]
                armour(P, 'Rubber', pts, bevel=0.02, seams=False, groove=None, name='Lug')
        else:
            # Straight blocks, staggered side to side.
            sd = 1 if t % 2 else -1
            c = hub + Vector((sd * w * 0.14, 0, 0)) + rad * (r * 0.99 - lug_h)
            pts = [tuple(c + Vector((dx, 0, 0)) + tan * dt + rad * dr) for dx in (-w * 0.33, w * 0.33) for dt in (-0.16, 0.16) for dr in (-0.04, lug_h)]
            armour(P, 'Rubber', pts, bevel=0.025, seams=False, groove=None, name='Block')


def wheel(P, hub, s, r, w, style):
    if style == 'offroad':
        # Raptor: chunky chevron off-road tyre, dished six-spoke rim, bolted hub.
        tyre(P, hub, r, w, 20, 0.14)
        lathe(P, 'Secondary', hub, (s, 0, 0), [(0, -w * 0.45), (r * 0.6, -w * 0.45), (r * 0.6, w * 0.3), (r * 0.52, w * 0.36), (r * 0.48, w * 0.2), (0, w * 0.2)], segs=32, name='Rim')
        for i in range(6):
            ang = 2 * math.pi * i / 6
            rad = Vector((0, math.sin(ang), math.cos(ang))); tan = Vector((0, math.cos(ang), -math.sin(ang)))
            armour(P, 'Dark', [tuple(hub + Vector((s * x, 0, 0)) + rad * rr + tan * tt) for x in (w * 0.22, w * 0.36) for rr in (r * 0.2, r * 0.5) for tt in (-0.08, 0.08)], bevel=0.0, seams=False, groove=None, name='Spoke')
        lathe(P, 'Metal', hub + Vector((s * w * 0.36, 0, 0)), (s, 0, 0), [(0, 0), (r * 0.22, 0), (r * 0.22, 0.1), (r * 0.15, 0.2), (0, 0.22)], segs=16, name='Hub')
        for b in range(8):
            ang = 2 * math.pi * b / 8
            lathe(P, 'Metal', hub + Vector((s * w * 0.46, math.sin(ang) * r * 0.32, math.cos(ang) * r * 0.32)), (s, 0, 0), [(0, 0), (0.07, 0), (0.07, 0.06), (0, 0.07)], segs=6, name='Bolt')
    elif style == 'fan':
        # Hornet: low-profile racing tyre with fine rib tread, a turbine-fan rim with a glowing core.
        prof = [(r * 0.62, -w / 2), (r * 0.92, -w / 2), (r * 0.99, -w * 0.3), (r, 0), (r * 0.99, w * 0.3), (r * 0.92, w / 2), (r * 0.62, w / 2)]
        lathe(P, 'Rubber', hub, (1, 0, 0), prof, segs=44, name='Tyre')
        for x in (-w * 0.3, -w * 0.1, w * 0.1, w * 0.3):
            lathe(P, 'Black', hub + Vector((x, 0, 0)), (1, 0, 0), [(r * 1.0, -0.03), (r * 1.005, -0.03), (r * 1.005, 0.03), (r * 1.0, 0.03)], segs=44, name='Rib')
        lathe(P, 'Dark', hub, (s, 0, 0), [(r * 0.5, -w * 0.4), (r * 0.64, -w * 0.4), (r * 0.64, w * 0.32), (r * 0.55, w * 0.38), (r * 0.5, w * 0.38)], segs=36, name='RimRing')
        for i in range(12):
            ang = 2 * math.pi * i / 12
            rad = Vector((0, math.sin(ang), math.cos(ang))); tan = Vector((0, math.cos(ang), -math.sin(ang)))
            # Curved turbine blades: twisted thin plates
            armour(P, 'Metal', [tuple(hub + Vector((s * x, 0, 0)) + rad * rr + tan * (tt + rr * 0.35)) for x in (w * 0.12, w * 0.3) for rr in (r * 0.18, r * 0.52) for tt in (-0.035, 0.035)],
                   bevel=0.0, seams=False, groove=None, name='Blade')
        lathe(P, 'Secondary', hub + Vector((s * w * 0.25, 0, 0)), (s, 0, 0), [(0, 0), (r * 0.2, 0), (r * 0.2, 0.12), (r * 0.12, 0.22), (0, 0.24)], segs=20, name='Spinner')
        lathe(P, 'Glow', hub + Vector((s * w * 0.12, 0, 0)), (s, 0, 0), [(r * 0.53, 0), (r * 0.56, 0), (r * 0.56, 0.04), (r * 0.53, 0.04)], segs=36, name='GlowRing')
    elif style == 'disc':
        # Juggernaut: huge block-tread tyre, flat steel disc with a bolt ring and a hub cone.
        tyre(P, hub, r, w, 16, 0.2, chevron=False)
        lathe(P, 'Secondary', hub, (s, 0, 0), [(0, -w * 0.45), (r * 0.6, -w * 0.45), (r * 0.6, w * 0.38), (r * 0.55, w * 0.44), (0, w * 0.44)], segs=36, name='Disc')
        lathe(P, 'Dark', hub + Vector((s * w * 0.44, 0, 0)), (s, 0, 0), [(r * 0.36, 0), (r * 0.5, 0), (r * 0.5, 0.05), (r * 0.36, 0.05)], segs=36, name='DiscRing')
        for b in range(12):
            ang = 2 * math.pi * b / 12
            lathe(P, 'Metal', hub + Vector((s * w * 0.47, math.sin(ang) * r * 0.43, math.cos(ang) * r * 0.43)), (s, 0, 0), [(0, 0), (0.08, 0), (0.08, 0.07), (0, 0.08)], segs=6, name='Bolt')
        lathe(P, 'Metal', hub + Vector((s * w * 0.44, 0, 0)), (s, 0, 0), [(0, 0), (r * 0.28, 0), (r * 0.28, 0.08), (r * 0.12, 0.38), (0, 0.42)], segs=20, name='HubCone')
    else:
        # Bastion: road tyre with block tread, armoured wheel with a five-rib star and a deep hub.
        tyre(P, hub, r, w, 18, 0.12, chevron=False)
        lathe(P, 'Dark', hub, (s, 0, 0), [(0, -w * 0.45), (r * 0.6, -w * 0.45), (r * 0.6, w * 0.25), (r * 0.5, w * 0.3), (0, w * 0.3)], segs=32, name='Rim')
        for i in range(5):
            ang = 2 * math.pi * i / 5
            rad = Vector((0, math.sin(ang), math.cos(ang))); tan = Vector((0, math.cos(ang), -math.sin(ang)))
            armour(P, 'Secondary', [tuple(hub + Vector((s * x, 0, 0)) + rad * rr + tan * tt) for x in (w * 0.28, w * 0.42) for rr in (r * 0.2, r * 0.56) for tt in (-0.13, 0.13)]
                   + [tuple(hub + Vector((s * w * 0.28, 0, 0)) + rad * r * 0.58)], bevel=0.03, seams=False, groove=None, name='Rib')
        lathe(P, 'Metal', hub + Vector((s * w * 0.3, 0, 0)), (s, 0, 0), [(0, 0), (r * 0.25, 0), (r * 0.25, 0.22), (r * 0.18, 0.3), (0, 0.3)], segs=18, name='Hub')
        for b in range(6):
            ang = 2 * math.pi * b / 6
            lathe(P, 'Metal', hub + Vector((s * (w * 0.3 + 0.22), math.sin(ang) * r * 0.16, math.cos(ang) * r * 0.16)), (s, 0, 0), [(0, 0), (0.05, 0), (0.05, 0.05), (0, 0.06)], segs=6, name='Bolt')


def trailing_arm(hub, s, w):
    top = Vector((s * (S['wx'] - 1.05), hub.y + 0.25, hub.z + 0.95))
    armour(H, 'Dark', boxpts((0.7, 1.1, 0.7), ch=0.2), Mx(top), bevel=0.04, seams=False, groove=None, name='ArmMount')
    arm_end = hub + Vector((-s * (w * 0.5 + 0.22), 0.0, 0.0))
    armour(H, 'Secondary', [tuple(p + Vector((0, dy, dz))) for p in (top + Vector((s * 0.1, 0, -0.1)), arm_end) for dy in (-0.28, 0.28) for dz in (-0.25, 0.25)],
           bevel=0.04, seam_min=10, name='TrailingArm')
    piston(H, top + Vector((0, -0.55, 0.2)), arm_end + Vector((0, -0.45, 0.35)), 0.13)


def wishbones(hub, s, w, pod_x):
    """Hornet: exposed double wishbones out to the wheel, a coil-over and an upright."""
    inner_x = s * pod_x
    out_x = s * (S['wx'] - w * 0.5 - 0.18)
    for dz, mat in ((0.38, 'Secondary'), (-0.32, 'Dark')):
        for dy in (-0.55, 0.55):
            bar(H, mat, (inner_x, hub.y + dy, hc.z + dz * 0.7), (out_x, hub.y + dy * 0.25, hub.z + dz), 0.16, name='Wishbone')
    # Upright on the hub
    armour(H, 'Dark', [tuple(Vector((out_x, hub.y, hub.z)) + Vector((x, y, z))) for x in (-0.08, 0.12 * s) for y in (-0.22, 0.22) for z in (-0.5, 0.5)], bevel=0.03, seams=False, groove=None, name='Upright')
    # Coil-over: spring rings round a damper
    a = Vector((s * (pod_x - 0.15), hub.y - 0.1, hc.z + 0.95))
    b = Vector((out_x - s * 0.25, hub.y - 0.1, hub.z + 0.4))
    piston(H, a, b, 0.09)
    ax = (b - a).normalized()
    for i in range(7):
        p = a.lerp(b, 0.22 + i * 0.075)
        lathe(H, 'Accent', p, ax, [(0.15, -0.025), (0.19, -0.025), (0.19, 0.025), (0.15, 0.025)], segs=12, name='Coil')


# ===========================================================================
# Hulls
# ===========================================================================
detach_fenders = []
wheels = []


def build_wheels(arms='trailing', pod_x=1.0):
    for wz in S['wz']:
        for s in (-1, 1):
            i = len(wheels)
            hub = B(s * S['wx'], 0.2, wz)
            r, w = S['r'], S['w']
            if arms == 'trailing':
                trailing_arm(hub, s, w)
            else:
                wishbones(hub, s, w, pod_x)
            P = 'Wheel%d' % i
            wheel(P, hub, s, r, w, S['wheel'])
            R.rigid(P, hub, 'Root')
            R.anchor('WheelAnchor%d' % i, hub + Vector((0, 0, 1.0)), 'Root')
            wheels.append((i, s, wz, hub))


def arch_fender(hub, s, r, w, span=1.15, lift=0.3, mat='Primary', flare=0.0):
    nm = 'Fender%d' % len(detach_fenders)
    c = hub + Vector((0, 0, r + lift))
    pts = [(s * x, y, z) for (y, z) in ((r * span, -0.6), (r * 0.6, 0.0), (-r * 0.6, 0.0), (-r * span, -0.6)) for x in (-w * 0.62, w * 0.62 + flare)]
    pts += [(x, y, z + 0.22) for (x, y, z) in pts]
    armour(nm, mat, pts, Mx(c), bevel=0.04, seam_min=0.6, name='Fender')
    stripe(nm, Mx(c + Vector((s * (w * 0.63 + flare * 0.5), 0, 0.05)), (0, 0, 90)), r * 1.2, 0.14)
    R.rigid(nm, c, 'Root')
    detach_fenders.append(nm)


def column():
    """Slewing column carrying the torso, braced by four cylinders, with the slewing ring."""
    deck = B(0, S['deck'], S['waist'][2])
    armour(H, 'Dark', ngon(deck.z, 1.55 * k, 8, 22.5, cx=deck.x, cy=deck.y) + ngon(deck.z + 0.35, 1.35 * k, 8, 22.5, cx=deck.x, cy=deck.y), bevel=0.04, seams=False, groove=None, name='ColumnBase')
    armour(H, 'Secondary', ngon(deck.z, 0.85 * k, 8, 22.5, cx=deck.x, cy=deck.y) + ngon(W.z - 0.08, 0.8 * k, 8, 22.5, cx=deck.x, cy=deck.y), bevel=0.03, seams=False, groove=None, name='Column')
    for i in range(4):
        a = math.radians(45 + i * 90)
        o = Vector((math.cos(a), math.sin(a), 0)) * 1.1 * k
        piston(H, deck + o * 1.1 + Vector((0, 0, 0.35)), W + o * 0.75 - Vector((0, 0, 0.12)), 0.1 * k)
    lathe(H, 'Dark', W - Vector((0, 0, 0.1)), (0, 0, 1), [(0, 0), (1.35 * k, 0), (1.35 * k, 0.12), (0, 0.12)], segs=40, name='RingBase')
    gear(H, W, (0, 0, 1), 1.2 * k, 0.1, teeth=36)


def hull_raptor():
    """A low raid hull: shark nose, bull bar, open wheels under flared fenders, a raised engine deck."""
    tub = hsec(hl * 1.05, inner * 0.7, -hh * 0.4, -hh * 0.05, inner * 0.5) + hsec(hl * 0.55, inner, -hh * 0.9, hh * 0.55, inner * 0.7) + \
          hsec(-hl * 0.7, inner, -hh * 0.9, hh * 0.7, inner * 0.7) + hsec(-hl * 1.0, inner * 0.85, -hh * 0.5, hh * 0.5, inner * 0.6)
    armour(H, 'Dark', tub, MH, bevel=0.06, seams=False, groove=None, name='Tub')
    # Shark nose: a long low wedge running to a point over the bull bar
    nose = [(0, hl * 1.22, -hh * 0.15), (0, hl * 1.22, -hh * 0.42)] + [(x, y, z) for (y, w, z) in ((hl * 1.08, inner * 0.55, -hh * 0.05), (hl * 0.45, hw * 0.84, hh * 0.78)) for x in (-w, w)]
    nose += [(x, y, z - 0.32) for (x, y, z) in nose[2:]]
    armour(H, 'Primary', nose, MH, bevel=0.06, seam_min=0.8, name='SharkNose')
    stripe(H, Mx(hc + Vector((0, hl * 0.78, hh * 0.47)), (-62, 0, 0)), 0.35, 1.6)
    # Chin splitter
    armour(H, 'Dark', [(x, y, -hh * 0.55 + dz) for x, y in ((-inner * 0.75, hl * 1.15), (inner * 0.75, hl * 1.15), (-inner * 0.95, hl * 0.75), (inner * 0.95, hl * 0.75), (0, hl * 1.3)) for dz in (0, 0.12)],
           MH, bevel=0.03, seams=False, groove=None, name='Splitter')
    # Bull bar with four lamps
    bb = [hc + Vector(p) for p in ((-inner * 0.95, hl * 1.0, -hh * 0.45), (-inner * 0.95, hl * 1.2, hh * 0.05), (-inner * 0.4, hl * 1.3, hh * 0.15),
                                    (inner * 0.4, hl * 1.3, hh * 0.15), (inner * 0.95, hl * 1.2, hh * 0.05), (inner * 0.95, hl * 1.0, -hh * 0.45))]
    tube(H, 'Metal', bb, 0.11, segs=10, name='BullBar')
    bar(H, 'Metal', hc + Vector((-inner * 0.9, hl * 1.22, -hh * 0.3)), hc + Vector((inner * 0.9, hl * 1.22, -hh * 0.3)), 0.12, name='BullBarLow')
    for x in (-0.85, -0.3, 0.3, 0.85):
        lamp(H, Mx(hc + Vector((x * inner, hl * 1.29, hh * 0.32)), (-10, 0, 0)), 0.26, 0.16)
    # Roof and sponsons
    roof = [(x, y, z) for (y, w, z) in ((hl * 0.45, hw * 0.82, hh * 0.8), (-hl * 0.3, hw * 0.84, hh * 0.88)) for x in (-w, w)]
    armour(H, 'Secondary', roof + [(x, y, z - 0.4) for x, y, z in roof], MH, bevel=0.06, seam_min=1.2, seam_w=0.22, name='Roof')
    for s in (-1, 1):
        sp = [(s * x, y, z) for (x, y, z) in ((hw * 0.8, hl * 0.6, hh * 0.85), (hw * 0.8, -hl * 0.9, hh * 0.9), (hw * 1.02, hl * 0.55, hh * 0.4), (hw * 1.02, -hl * 0.88, hh * 0.45),
                                             (hw * 0.6, hl * 0.6, hh * 0.85), (hw * 0.6, -hl * 0.9, hh * 0.9), (hw * 0.6, hl * 0.55, hh * 0.3), (hw * 0.6, -hl * 0.88, hh * 0.3))]
        armour(H, 'Primary', sp, MH, bevel=0.05, seam_min=0.8, stripe=((s, 0, 0.6), 0.75), name='Sponson')
        hose(H, [hc + Vector((s * inner * 0.5, -hl * 0.6, hh * 0.95)), hc + Vector((s * inner * 0.7, -hl * 0.1, hh * 1.0)), hc + Vector((s * inner * 0.5, hl * 0.3, hh * 0.85))], 0.1)
    # Raised engine deck at the back: intake grilles, twin exhaust stacks
    eng = [(x, y, z) for (y, w, z) in ((-hl * 0.35, hw * 0.72, hh * 1.45), (-hl * 1.02, hw * 0.7, hh * 1.3)) for x in (-w, w)]
    eng += [(x, y, hh * 0.6) for (x, y, z) in eng]
    armour(H, 'Secondary', eng, MH, bevel=0.06, seam_min=1.0, name='EngineDeck')
    for s in (-1, 1):
        grille(H, Mx(hc + Vector((s * hw * 0.36, -hl * 0.68, hh * 1.42)), (90, 0, 0)), hw * 0.55, hl * 0.42, slats=5, depth=0.08)
        stack(H, hc + Vector((s * hw * 0.82, -hl * 0.5, hh * 0.9)), 2.1, 0.2)
    grille(H, Mx(hc + Vector((0, -hl * 1.03, hh * 0.95)), (0, 0, 180)), hw * 0.9, hh * 0.55, slats=4, depth=0.1)
    hazard(H, Mx(hc + Vector((0, hl * 0.75, hh * 0.55)), (-60, 0, 0)), inner * 1.1, 0.26, n=6)
    for i, x in enumerate((-1.2, 1.2)):
        booster(hc + Vector((x, -hl * 1.08, hh * 0.15)), 1.25, i)
    build_wheels('trailing')
    for i, s, wz, hub in wheels:
        if wz != 0:
            arch_fender(hub, s, S['r'], S['w'], span=1.25, lift=0.32, flare=0.25)


def hull_hornet():
    """A slim rally-raid pod on outrigger wishbones: splitter nose, engine deck, a strut-mounted rear wing."""
    pod_x = 1.15
    pod = hsec(hl * 1.1, 0.85, -hh * 0.35, hh * 0.25, 0.5) + hsec(hl * 0.6, pod_x, -hh * 0.75, hh * 0.75, 0.75) + \
          hsec(-hl * 0.55, pod_x, -hh * 0.8, hh * 0.8, 0.75) + hsec(-hl * 1.05, 0.95, -hh * 0.6, hh * 0.7, 0.6)
    armour(H, 'Dark', pod, MH, bevel=0.06, seams=False, groove=None, name='Pod')
    # Top shell: a blunt, faceted bonnet sloping down to the nose
    shell = [(x, y, z) for (y, w, z) in ((hl * 1.16, 0.78, hh * 0.22), (hl * 0.65, pod_x * 1.05, hh * 0.95), (-hl * 0.6, pod_x * 1.05, hh * 1.05), (-hl * 0.95, 0.9, hh * 0.95)) for x in (-w, w)]
    shell += [(x * 0.95, y, z - 0.42) for (x, y, z) in shell]
    armour(H, 'Primary', shell, MH, bevel=0.05, seam_min=0.7, name='PodShell')
    for x in (-0.3, 0.3):
        stripe(H, Mx(hc + Vector((x, -hl * 0.1, hh * 1.06)), (90, 0, 0)), 0.16, hl * 1.2)
    # Front splitter: a wide flat blade under the nose, with a light bar above it
    split = [(x, y, -hh * 0.5) for (x, y) in ((-1.35, hl * 0.95), (1.35, hl * 0.95), (-1.15, hl * 1.32), (1.15, hl * 1.32), (-1.25, hl * 0.7), (1.25, hl * 0.7))]
    split += [(x, y, z + 0.14) for (x, y, z) in split]
    armour(H, 'Secondary', split, MH, bevel=0.03, seam_min=0.5, name='Splitter')
    hazard(H, Mx(hc + Vector((0, hl * 1.33, -hh * 0.43))), 2.2, 0.12, n=7)
    lamp(H, Mx(hc + Vector((0, hl * 1.14, hh * 0.02)), (-10, 0, 0)), 0.95, 0.07)
    for s in (-1, 1):
        # Tow hooks either side of the nose
        armour(H, 'Metal', boxpts((0.14, 0.3, 0.14), ch=0.04), Mx(hc + Vector((s * 0.9, hl * 1.1, -hh * 0.2))), bevel=0.02, seams=False, groove=None, name='TowHook')
    # Side intakes
    for s in (-1, 1):
        grille(H, Mx(hc + Vector((s * pod_x * 1.0, -hl * 0.1, hh * 0.2)), (0, 0, -s * 90)), hl * 0.6, hh * 0.7, slats=4, depth=0.06)
    # Engine deck at the back: a raised block with a top grille and two exhausts
    deck = [(x, y, z) for (y, w) in ((-hl * 0.5, 0.95), (-hl * 1.22, 0.85)) for x in (-w, w) for z in (hh * 0.4, hh * 1.25)]
    deck += [(x * 0.8, y, hh * 1.4) for (x, y, z) in deck if z > hh]
    armour(H, 'Secondary', deck, MH, bevel=0.05, seam_min=0.5, name='EngineDeck')
    grille(H, Mx(hc + Vector((0, -hl * 0.86, hh * 1.41)), (90, 0, 0)), 1.1, hl * 0.42, slats=5, depth=0.06)
    for s in (-1, 1):
        lathe(H, 'Dark', hc + Vector((s * 0.6, -hl * 1.2, hh * 1.0)), (0, -1, 0), [(0, 0), (0.17, 0), (0.17, 0.55), (0.2, 0.6), (0.2, 0.75), (0.14, 0.75), (0, 0.6)], segs=16, name='Exhaust')
    # Twin boosters under the deck
    for i, x in enumerate((-0.42, 0.42)):
        booster(hc + Vector((x, -hl * 1.28, hh * 0.05)), 0.62, i, housing=False)
    # Rear wing on two swan-neck struts, with endplates and a stripe
    wy, wz = -hl * 1.12, hh * 2.35
    for s in (-1, 1):
        bar(H, 'Dark', hc + Vector((s * 0.55, -hl * 0.98, hh * 1.3)), hc + Vector((s * 0.55, wy, wz - 0.05)), 0.14, name='WingStrut')
    wing = [(x, y, z) for x in (-1.7, 1.7) for (y, z) in ((wy + 0.55, wz + 0.08), (wy - 0.5, wz + 0.22), (wy - 0.45, wz + 0.02), (wy + 0.5, wz - 0.06))]
    armour(H, 'Primary', [tuple(hc + Vector(q)) for q in wing], bevel=0.02, seam_min=0.5, name='RearWing')
    stripe(H, Mx(hc + Vector((0, wy, wz + 0.16)), (82, 0, 0)), 2.6, 0.18, t=0.03)
    for s in (-1, 1):
        armour(H, 'Secondary', [tuple(hc + Vector(q)) for x in (s * 1.7, s * 1.78) for q in ((x, wy + 0.65, wz - 0.25), (x, wy - 0.65, wz - 0.15), (x, wy - 0.6, wz + 0.45), (x, wy + 0.5, wz + 0.3))],
               bevel=0.02, seams=False, groove=None, name='Endplate')
    build_wheels('wishbone', pod_x)
    # Aero wheel pods over each tyre (detachable)
    for i, s, wz, hub in wheels:
        nm = 'Fender%d' % len(detach_fenders)
        r, w = S['r'], S['w']
        c = hub + Vector((0, 0, r + 0.18))
        pts = [(s * x, y, z) for (y, z) in ((r * 1.1, -0.55), (r * 0.75, -0.05), (r * 0.1, 0.12), (-r * 0.55, 0.0), (-r * 0.95, -0.4)) for x in (-w * 0.58, w * 0.58)]
        pts += [(x, y, z + 0.16) for (x, y, z) in pts]
        armour(nm, 'Primary', pts, Mx(c), bevel=0.03, seam_min=0.5, name='WheelPod')
        stripe(nm, Mx(c + Vector((s * w * 0.59, r * 0.1, 0.05)), (0, 0, 90)), r * 0.9, 0.1)
        R.rigid(nm, c, 'Root')
        detach_fenders.append(nm)


def hull_juggernaut():
    """A towering fortress: V plough with teeth, casemate, stepped engine block, fuel tanks, stacks."""
    tub = hsec(hl * 1.0, inner * 0.9, -hh * 0.5, hh * 0.4, inner * 0.6) + hsec(hl * 0.6, inner * 1.05, -hh * 0.9, hh * 0.85, inner * 0.75) + \
          hsec(-hl * 0.7, inner * 1.05, -hh * 0.9, hh * 0.9, inner * 0.75) + hsec(-hl * 1.0, inner, -hh * 0.6, hh * 0.8, inner * 0.7)
    armour(H, 'Dark', tub, MH, bevel=0.06, seams=False, groove=None, name='Tub')
    # Front casemate: a tall slab-sided box with a steep glacis
    cm = [(x, y, z) for (y, w, z) in ((hl * 0.92, hw * 0.78, hh * 0.35), (hl * 0.62, hw * 0.82, hh * 1.25), (hl * 0.15, hw * 0.84, hh * 1.3)) for x in (-w, w)]
    cm += [(x, y, -hh * 0.1) for (x, y, z) in cm]
    armour(H, 'Primary', cm, MH, bevel=0.07, seam_min=1.0, seam_w=0.2, name='Casemate')
    stripe(H, Mx(hc + Vector((0, hl * 0.38, hh * 1.33)), (90, 0, 0)), hw * 1.3, 0.35)
    for x in (-0.55, 0.0, 0.55):
        armour(H, 'Glow', boxpts((0.5, 0.08, 0.12)), Mx(hc + Vector((x * hw, hl * 0.775, hh * 0.86)), (-34, 0, 0)), bevel=0, seams=False, groove=None, name='Slit')
    # Stepped engine block at the back with intakes
    eng = [(x, y, z) for (y, w, z) in ((-hl * 0.25, hw * 0.8, hh * 1.55), (-hl * 0.98, hw * 0.78, hh * 1.5)) for x in (-w, w)]
    eng += [(x, y, hh * 0.5) for (x, y, z) in eng]
    armour(H, 'Secondary', eng, MH, bevel=0.07, seam_min=1.2, name='EngineBlock')
    for s in (-1, 1):
        grille(H, Mx(hc + Vector((s * hw * 0.38, -hl * 0.6, hh * 1.55)), (90, 0, 0)), hw * 0.6, hl * 0.5, slats=6, depth=0.1)
        for yy in (-0.42, -0.72):
            stack(H, hc + Vector((s * hw * 0.88, hl * yy, hh * 1.2)), 2.4, 0.26)
        # Fuel tanks with straps along the flanks
        tc = hc + Vector((s * hw * 0.8, hl * 0.02, hh * 1.0))
        lathe(H, 'Secondary', tc, (0, 1, 0), [(0, -1.6), (0.3, -1.6), (0.5, -1.45), (0.5, 1.45), (0.3, 1.6), (0, 1.6)], segs=20, name='FuelTank')
        for yy in (-1.0, 0.0, 1.0):
            lathe(H, 'Dark', tc + Vector((0, yy, 0)), (0, 1, 0), [(0.51, -0.08), (0.54, -0.08), (0.54, 0.08), (0.51, 0.08)], segs=20, name='Strap')
        hazard(H, Mx(tc + Vector((s * 0.52, 0.55, 0)), (0, 0, -s * 90)), 0.8, 0.22, n=4)
    grille(H, Mx(hc + Vector((0, -hl * 1.0, hh * 0.6)), (0, 0, 180)), hw * 1.0, hh * 0.9, slats=6, depth=0.12)
    for i, x in enumerate((-1.4, 1.4)):
        booster(hc + Vector((x, -hl * 1.06, hh * 0.05)), 1.2, i)
    # The plough: two angled blades meeting at a prow, teeth along the bottom edge, rams behind
    py = hl * 1.0
    for s in (-1, 1):
        blade = [(0, py + 2.1, z) for z in (-hh * 0.95, hh * 0.55)] + [(s * hw * 1.08, py + 0.25, z) for z in (-hh * 0.95, hh * 0.65)]
        blade += [(x - s * 0.05, y - 0.38, z) for (x, y, z) in blade]
        armour(H, 'Primary', blade, MH, bevel=0.05, seam_min=0.6, name='Plough')
        for t in range(3):
            f = (t + 0.5) / 3
            tp = Vector((s * hw * 1.0 * f, py + 2.1 - 1.85 * f, -hh * 0.95))
            fwd = Vector((s * 0.42, 1, 0)).normalized()
            armour(H, 'Metal', [tuple(tp + Vector((dx, 0, dz))) for dx in (-0.22, 0.22) for dz in (0, 0.35)] + [tuple(tp + fwd * 0.75 + Vector((0, 0, 0.08)))], MH, bevel=0.02, seams=False, groove=None, name='Tooth')
        piston(H, hc + Vector((s * hw * 0.55, hl * 0.82, -hh * 0.25)), hc + Vector((s * hw * 0.55, py + 0.4, hh * 0.05)), 0.18)
    hazard(H, Mx(hc + Vector((0, py + 1.0, hh * 0.25)), (0, 0, 0)), hw * 0.6, 0.3, n=6)
    build_wheels('trailing')
    # Slab skirts (detachable)
    for s in (-1, 1):
        for yy in (3.1, -2.9):
            nm = 'Fender%d' % len(detach_fenders)
            c = B(s * 3.5, 2.0, yy)
            side_plate(nm, 'Primary', [(-2.8, 0.9), (2.8, 0.9), (2.5, -0.9), (-2.5, -0.9)], 0.3, c, s, 10, stripe_at=0.5, stripe_h=0.15,
                       hazard_at=-0.5, bevel=0.05, seam_min=0.8, name='Skirt')
            bolts_line(nm, c + Vector((s * 0.2, -2.3, 0.7)), c + Vector((s * 0.2, 2.3, 0.7)), 8, (s, 0, 0), 0.08)
            R.rigid(nm, c, 'Root')
            detach_fenders.append(nm)


def hull_bastion():
    """A long flat gun deck: dozer blade, stabiliser jacks, a rack of shells, stowage bins."""
    tub = hsec(hl * 1.0, inner * 0.85, -hh * 0.45, hh * 0.2, inner * 0.55) + hsec(hl * 0.6, inner, -hh * 0.9, hh * 0.6, inner * 0.7) + \
          hsec(-hl * 0.75, inner, -hh * 0.9, hh * 0.6, inner * 0.7) + hsec(-hl * 1.0, inner * 0.9, -hh * 0.55, hh * 0.5, inner * 0.6)
    armour(H, 'Dark', tub, MH, bevel=0.06, seams=False, groove=None, name='Tub')
    deck = [(x, y, z) for (y, w, z) in ((hl * 0.95, hw * 0.8, hh * 0.45), (hl * 0.7, hw * 0.92, hh * 0.72), (-hl * 1.02, hw * 0.92, hh * 0.72)) for x in (-w, w)]
    deck += [(x, y, z - 0.38) for x, y, z in deck]
    armour(H, 'Secondary', deck, MH, bevel=0.05, seam_min=1.4, seam_w=0.22, name='GunDeck')
    hazard(H, Mx(hc + Vector((0, hl * 0.83, hh * 0.62)), (-45, 0, 0)), hw * 1.2, 0.25, n=8)
    # Stowage bins along both sides with latches
    for s in (-1, 1):
        for yy in (0.35, -0.35):
            bc = hc + Vector((s * hw * 0.98, hl * yy, hh * 0.35))
            armour(H, 'Primary', boxpts((0.55, hl * 0.6, 0.9), ch=0.08, endch=0.04), Mx(bc), bevel=0.04, seam_min=0.3, stripe=((s, 0, 0), 0.9), name='Bin')
            bolts_line(H, bc + Vector((s * 0.28, -hl * 0.22, 0.3)), bc + Vector((s * 0.28, hl * 0.22, 0.3)), 3, (s, 0, 0), 0.07)
    # Shell rack behind the torso: two rows of rounds in an open frame
    rc = hc + Vector((0, -hl * 0.72, hh * 0.72))
    for s in (-1, 1):
        armour(H, 'Dark', boxpts((0.14, 1.5, 1.2), ch=0.03), Mx(rc + Vector((s * 1.55, 0, 0.55))), bevel=0.02, seams=False, groove=None, name='RackSide')
    bar(H, 'Dark', rc + Vector((-1.55, 0.6, 1.1)), rc + Vector((1.55, 0.6, 1.1)), 0.1, name='RackBar')
    bar(H, 'Dark', rc + Vector((-1.55, -0.6, 1.1)), rc + Vector((1.55, -0.6, 1.1)), 0.1, name='RackBar')
    for row in (-0.38, 0.38):
        for i in range(6):
            p = rc + Vector((-1.25 + i * 0.5, row, 0))
            lathe(H, 'Metal', p, (0, 0, 1), [(0, 0), (0.17, 0), (0.17, 0.08), (0.15, 0.1), (0.15, 0.75), (0.1, 0.95), (0.04, 1.05), (0, 1.07)], segs=12, name='Shell')
            lathe(H, 'Accent', p + Vector((0, 0, 0.6)), (0, 0, 1), [(0.152, 0), (0.155, 0), (0.155, 0.1), (0.152, 0.1)], segs=12, name='ShellBand')
    for i, x in enumerate((-1.5, 1.5)):
        booster(hc + Vector((x, -hl * 1.06, hh * 0.1)), 1.1, i)
    grille(H, Mx(hc + Vector((0, -hl * 1.03, hh * 0.1)), (0, 0, 180)), hw * 0.6, hh * 0.55, slats=4, depth=0.1)
    # Stabiliser jacks: a hinged beam folded down and out to a pad at each corner
    for s in (-1, 1):
        for yy in (0.82, -0.86):
            root = hc + Vector((s * hw * 0.88, hl * yy, hh * 0.3))
            foot = Vector((s * (S['wx'] + S['w'] * 0.5 + 1.25), root.y + (0.35 if yy > 0 else -0.35), STAND + 0.2 - S['r'] + 0.35))
            knee = root.lerp(foot, 0.55) + Vector((0, 0, 0.9))
            armour(H, 'Dark', boxpts((0.6, 0.8, 0.7), ch=0.15), Mx(root), bevel=0.03, seams=False, groove=None, name='JackMount')
            bar(H, 'Secondary', root, knee, 0.42, name='JackBeam')
            bar(H, 'Secondary', knee, foot + Vector((0, 0, 0.35)), 0.34, name='JackLeg')
            drum(H, knee, (0, 1, 0), 0.28, 0.5, bolts=6, segs=20)
            piston(H, root + Vector((0, 0, 0.55)), knee.lerp(foot, 0.45) + Vector((0, 0, 0.18)), 0.12)
            lathe(H, 'Dark', foot, (0, 0, 1), [(0, 0), (0.62, 0), (0.62, 0.1), (0.5, 0.18), (0.22, 0.2), (0.22, 0.36), (0, 0.36)], segs=24, name='JackPad')
            stripe(H, Mx(knee.lerp(root, 0.5) + Vector((0, 0.22 if yy > 0 else -0.22, 0.05)), (0, 0, 0 if yy > 0 else 180)), 0.6, 0.12)
    build_wheels('trailing')
    for i, s, wz, hub in wheels:
        if wz != 0:
            arch_fender(hub, s, S['r'], S['w'], span=1.1, lift=0.3)
    # Dozer blade
    c = B(0, 1.2, 5.6)
    pts = [(x, y, z) for (x, y) in ((-3.05, -0.25), (-1.5, 0.15), (0, 0.3), (1.5, 0.15), (3.05, -0.25)) for z in (-1.05, 1.05)]
    pts += [(x, y - 0.55, z * 0.92) for (x, y, z) in pts]
    armour('Shield', 'Primary', pts, Mx(c), bevel=0.06, seam_min=0.8, name='Blade')
    armour('Shield', 'Metal', [(x, y, z) for x in (-3.1, 3.1) for y in (0.15, 0.35) for z in (-1.15, -0.95)], Mx(c), bevel=0.02, seams=False, groove=None, name='Edge')
    hazard('Shield', Mx(c + Vector((0, 0.33, 0.7))), 4.0, 0.3, n=10)
    for x in (-2.2, 2.2):
        armour('Shield', 'Dark', boxpts((0.3, 0.5, 1.9), ch=0.06), Mx(c + Vector((x, -0.45, 0))), bevel=0.03, seams=False, groove=None, name='BladeRib')
    for s in (-1, 1):
        piston(H, B(s * 1.6, 0.9, 3.6), c + Vector((s * 1.6, -0.45, -0.2)), 0.16)
    R.rigid('Shield', c, 'Root')


# ===========================================================================
# Upper bodies
# ===========================================================================
T = 'Torso'


def slew_top(waist_r):
    lathe(T, 'Metal', W, (0, 0, 1), [(0, 0), (1.3 * k, 0), (1.32 * k, 0.05), (1.32 * k, 0.3), (0, 0.33)], segs=40, name='RingTop')
    armour(T, 'Dark', ngon(W.z + 0.3, waist_r * k, 8, 22.5, cx=W.x, cy=W.y) + ngon(W.z + 1.4 * k, (waist_r + 0.1) * k, 8, 22.5, cx=W.x, cy=W.y), bevel=0.04, seams=False, groove=None, name='Waist')


def torso_gunner():
    """Raptor: an angular gunner torso with keel breastplates, a backpack reactor and a visor head."""
    slew_top(1.1)
    c = Wp(0, 0, 0)
    armour(T, 'Dark', K(sec(1.3, 1.3, 1.1, 0.35) + sec(2.1, 2.0, 1.45, 0.55, 0, 0.1) + sec(3.1, 2.15, 1.5, 0.6, 0, 0.1) + sec(3.9, 1.5, 1.1, 0.45, 0, -0.1)), Mx(c), bevel=0.06, seams=False, groove=None, name='ChestCore')
    for s in (-1, 1):
        armour(T, 'Primary', K([(s * x, y, z) for x, y, z in ((0.05, 1.75, 1.6), (1.6, 1.3, 1.9), (1.75, 1.35, 3.4), (0.05, 1.85, 3.5), (0.05, 1.4, 3.95), (1.3, 1.1, 3.9),
                                                         (0.05, 1.45, 1.5), (1.5, 1.0, 1.8), (1.6, 1.0, 3.3))]), Mx(c), bevel=0.05, seam_min=0.5, name='Breast')
        armour(T, 'Secondary', K([(s * x, y, z) for x, y, z in ((1.95, -1.2, 1.9), (1.95, 1.2, 1.9), (2.2, -1.3, 3.2), (2.2, 1.25, 3.2), (1.7, -1.0, 3.9), (1.7, 0.9, 3.9),
                                                           (1.75, -1.2, 1.9), (1.75, 1.2, 1.9), (2.0, -1.3, 3.2), (2.0, 1.25, 3.2))]), Mx(c), bevel=0.05, seam_min=0.6, name='ChestSide')
    stripe(T, Mx(Wp(0.55, 3.2, 1.72), (0, 0, -18)), 0.8 * k, 0.14)
    armour(T, 'Primary', K(sec(1.4, 1.2, 0.65, 0.25, 0, -1.75) + sec(3.6, 1.3, 0.7, 0.25, 0, -1.85)), Mx(c), bevel=0.05, seam_min=0.6, name='Pack')
    grille(T, Mx(Wp(0, 2.9, -2.6), (0, 0, 180)), 1.6 * k, 0.8 * k, slats=4, depth=0.08)
    lathe(T, 'Dark', Wp(0, 1.95, -2.6), (0, -1, 0), [(0, 0), (0.55 * k, 0), (0.55 * k, 0.12), (0, 0.12)], segs=24, name='PortRim')
    lathe(T, 'Glow', Wp(0, 1.95, -2.6), (0, -1, 0), [(0, 0.06), (0.4 * k, 0.06), (0.4 * k, 0.16), (0, 0.16)], segs=24, name='Port')
    # Head: a sloped helm with a shark-fin crest and a visor slot
    hp = Wp(0, 4.2, 0.35)
    armour(T, 'Primary', K(sec(-0.4, 0.8, 0.85, 0.28) + sec(0.35, 0.72, 0.75, 0.25, 0, -0.05) + [(0, 1.05, -0.1), (0.45, 0.95, -0.12), (-0.45, 0.95, -0.12)]), Mx(hp), bevel=0.04, seam_min=0.4, name='Head')
    armour(T, 'Secondary', K([(x, y, z) for x in (-0.09, 0.09) for (y, z) in ((0.75, 0.45), (-0.9, 0.5), (-0.6, 1.15), (0.2, 0.75)) if True]), Mx(hp), bevel=0.02, seams=False, groove=None, name='Crest')
    armour(T, 'Black', boxpts((1.2 * k, 0.3, 0.22 * k), ch=0.06), Mx(hp + Vector((0, 0.8 * k, 0.02)), (-6, 0, 0)), bevel=0.02, seams=False, groove=None, name='VisorSlot')
    armour(T, 'Glow', boxpts((1.05 * k, 0.1, 0.1 * k)), Mx(hp + Vector((0, 0.92 * k, 0.02)), (-6, 0, 0)), bevel=0, seams=False, groove=None, name='Visor')
    R.rigid('Torso', W, 'Upper')
    for s in (-1, 1):
        arm(R, s, Wp(s * 2.7, 2.8, 0.1), Wp(s * 2.8, 0.9, 0.25), Wp(s * 2.8, 0.75, 1.35), k=0.78 * k, gauntlet=(1.0, 1.0, 1.0))
    rams = [(Wp(s * 1.95, 1.7, -1.1), Wp(s * 2.3, 2.5, -1.45)) for s in (-1, 1)]
    for s, nm in ((-1, 'PauldronL'), (1, 'PauldronR')):
        cc = Wp(s * 2.85, 3.5, 0)
        M = Mx(cc)
        armour(nm, 'Primary', K(sec(-0.45, 1.0, 1.3, 0.4, s * 0.1, 0) + sec(0.3, 0.95, 1.35, 0.42) + sec(0.6, 0.6, 1.0, 0.3, -s * 0.1, -0.05)), M, bevel=0.05, seam_min=0.5, name='Pauldron')
        side_plate(nm, 'Secondary', [(-1.3 * k, 0.25 * k), (1.3 * k, 0.25 * k), (1.05 * k, -0.85 * k), (-1.05 * k, -0.85 * k)], 0.16, (s * 1.05 * k, 0, -0.5 * k), s, 10, M=M,
                   stripe_at=0.0, stripe_h=0.13, bevel=0.04, seam_min=0.4, name='Plate')
        R.rigid(nm, cc, 'Upper')
    mast = Wp(0.7, 4.0, -0.6)
    antenna(mast, 2.0)
    return dict(arms=Wp(0, 2.8, 0.1), rams=rams, lug=lambda s: Wp(s * 1.95, 1.7, -1.1), armlug=lambda s: Wp(s * 2.3, 2.5, -1.45),
                back=(Wp(-1.45, 3.85, -1.8), Wp(1.45, 3.85, -1.8)), shoulder=Wp(-2.8, 4.25, -0.2), smoke=Wp(0, 2.4, -1.2))


def torso_raider():
    """Hornet: a compact armoured cab with a wraparound visor helmet, side radiators and boxy lamp pods."""
    slew_top(0.85)
    c = Wp(0, 0, 0)
    armour(T, 'Dark', K(sec(1.0, 0.95, 0.85, 0.28) + sec(2.0, 1.25, 1.05, 0.35) + sec(3.1, 1.2, 1.0, 0.35) + sec(3.45, 0.9, 0.78, 0.25)), Mx(c), bevel=0.05, seams=False, groove=None, name='Cab')
    # Breastplate: two faceted halves meeting on a centre keel
    for s in (-1, 1):
        armour(T, 'Primary', K([(s * x, y, z) for x, y, z in ((0.04, 1.3, 1.35), (1.2, 1.0, 1.55), (1.32, 1.1, 2.95), (0.04, 1.42, 3.2),
                                                         (0.04, 0.95, 1.35), (1.15, 0.7, 1.55), (1.25, 0.75, 2.95), (0.04, 1.0, 3.2))]), Mx(c), bevel=0.05, seam_min=0.5, name='Breast')
        armour(T, 'Secondary', K([(s * x, y, z) for x in (1.2, 1.42) for y in (-0.95, 0.85) for z in (1.6, 3.0)]), Mx(c), bevel=0.04, seam_min=0.6, name='CabSide')
        grille(T, Mx(Wp(s * 1.43, 2.3, -0.05), (0, 0, -s * 90)), 1.15 * k, 0.85 * k, slats=5, depth=0.06)
    stripe(T, Mx(Wp(0.55, 2.2, 1.43), (0, 0, -12)), 0.14 * k, 1.1 * k)
    armour(T, 'Metal', boxpts((0.12 * k, 0.12, 1.45 * k)), Mx(Wp(0, 2.25, 1.47)), bevel=0.02, seams=False, groove=None, name='Keel')
    # Back: a power pack with a vented cover
    armour(T, 'Secondary', K([(x, y, z) for x in (-0.95, 0.95) for y in (-0.9, -1.45) for z in (1.4, 3.15)]), Mx(c), bevel=0.04, seam_min=0.5, name='PowerPack')
    grille(T, Mx(Wp(0, 2.25, -0.99), (0, 0, 180)), 1.3 * k, 1.2 * k, slats=5, depth=0.06)
    # Helmet head: rounded crown, a wraparound glowing visor, a chin guard and cheek vents
    hp = Wp(0, 3.55, 0.5)
    armour(T, 'Dark', K(sec(-0.45, 0.42, 0.42, 0.12) + sec(-0.1, 0.45, 0.45, 0.12)), Mx(hp), bevel=0.03, seams=False, groove=None, name='Neck')
    armour(T, 'Primary', K(sec(-0.15, 0.6, 0.62, 0.2, 0, 0.05) + sec(0.3, 0.62, 0.6, 0.22) + sec(0.6, 0.5, 0.48, 0.18, 0, -0.04) + sec(0.72, 0.3, 0.3, 0.1, 0, -0.06)),
           Mx(hp), bevel=0.04, seam_min=0.3, name='Helmet')
    armour(T, 'Black', K([(x, y, z) for x in (-0.58, 0.58) for y in (0.3, 0.66) for z in (0.12, 0.34)] + [(x, 0.05, z) for x in (-0.64, 0.64) for z in (0.14, 0.32)]), Mx(hp), bevel=0.02, seams=False, groove=None, name='VisorBand')
    armour(T, 'Glow', K([(x, y, z) for x in (-0.5, 0.5) for y in (0.62, 0.69) for z in (0.19, 0.27)] + [(x, 0.3, z) for x in (-0.6, 0.6) for z in (0.19, 0.27)]), Mx(hp), bevel=0, seams=False, groove=None, name='Visor')
    armour(T, 'Secondary', K([(x, y, z) for x in (-0.38, 0.38) for (y, z) in ((0.68, -0.05), (0.55, -0.3), (0.2, -0.3), (0.3, 0.08))]), Mx(hp), bevel=0.03, seam_min=0.3, name='ChinGuard')
    for s in (-1, 1):
        grille(T, Mx(hp + Vector((s * 0.62, 0.0, 0.0)) * 1.0, (0, 0, -s * 90)), 0.45 * k, 0.32 * k, slats=3, depth=0.04)
    R.rigid('Torso', W, 'Upper')
    # Shoulder pods: boxy, a sloped top, a lamp on the front and a stripe
    for s, nm in ((-1, 'PauldronL'), (1, 'PauldronR')):
        cc = Wp(s * 1.75, 3.2, 0.25)
        M = Mx(cc)
        armour(nm, 'Primary', K([(x, y, z) for x in (-0.55, 0.6) for y in (-0.95, 1.05) for z in (-0.55, 0.3)] + [(-s * 0.4, y, 0.55) for y in (-0.85, 0.95)] + [(s * 0.7, y, -0.75) for y in (-0.85, 0.9)]),
               M, bevel=0.05, seam_min=0.5, name='Pod')
        lamp(nm, M @ Mx((s * 0.05 * k, 1.06 * k, -0.12 * k)), 0.38 * k, 0.16 * k)
        stripe(nm, M @ Mx((s * 0.61 * k, 0.0, -0.1 * k), (0, 0, s * 90)), 1.5 * k, 0.13)
        R.rigid(nm, cc, 'Upper')
    for s in (-1, 1):
        arm(R, s, Wp(s * 2.15, 2.4, 0.3), Wp(s * 2.25, 0.85, 0.4), Wp(s * 2.25, 0.7, 1.45), k=0.7 * k, gauntlet=(0.75, 1.0, 0.8))
    rams = [(Wp(s * 1.45, 1.5, -0.8), Wp(s * 1.9, 2.15, -1.2)) for s in (-1, 1)]
    antenna(Wp(-0.75, 3.3, -1.25), 2.0)
    return dict(arms=Wp(0, 2.4, 0.3), rams=rams, lug=lambda s: Wp(s * 1.45, 1.5, -0.8), armlug=lambda s: Wp(s * 1.9, 2.15, -1.2),
                back=(Wp(-1.15, 3.5, -1.6), Wp(1.15, 3.5, -1.6)), shoulder=Wp(-2.2, 3.7, -0.2), smoke=Wp(0, 2.2, -1.2))


def torso_brute():
    """Juggernaut: a hunched, very wide chest, the head sunk low, huge shoulders, a furnace grille, smokestacks."""
    slew_top(1.35)
    c = Wp(0, 0, 0)
    armour(T, 'Dark', K(sec(1.0, 1.8, 1.3, 0.4) + sec(1.9, 2.75, 1.6, 0.6, 0, 0.1) + sec(3.0, 2.95, 1.65, 0.6) + sec(3.6, 2.35, 1.35, 0.5, 0, -0.1)), Mx(c), bevel=0.06, seams=False, groove=None, name='ChestCore')
    # Front: two massive slanted plates and a belly plate, the furnace between them
    for s in (-1, 1):
        armour(T, 'Primary', K([(s * x, y, z) for x, y, z in ((0.55, 1.75, 1.6), (2.6, 1.45, 1.8), (2.75, 1.5, 3.3), (0.55, 1.85, 3.45), (0.9, 1.35, 3.85), (2.3, 1.2, 3.75),
                                                         (0.55, 1.4, 1.5), (2.5, 1.1, 1.7), (2.6, 1.1, 3.2))]), Mx(c), bevel=0.06, seam_min=0.6, name='Breast')
        armour(T, 'Secondary', K([(s * x, y, z) for x, y, z in ((2.75, -1.4, 1.6), (2.75, 1.3, 1.6), (3.0, -1.45, 3.1), (3.0, 1.35, 3.1), (2.5, -1.1, 3.7), (2.5, 1.0, 3.7),
                                                           (2.5, -1.4, 1.6), (2.5, 1.3, 1.6), (2.75, -1.45, 3.1), (2.75, 1.35, 3.1))]), Mx(c), bevel=0.05, seam_min=0.6, name='ChestSide')
    armour(T, 'Secondary', K(sec(0.95, 1.6, 1.25, 0.4, 0, 0.35) + sec(1.5, 1.75, 1.35, 0.45, 0, 0.4)), Mx(c), bevel=0.05, seam_min=0.5, name='Belly')
    armour(T, 'Hot', boxpts((0.9 * k, 0.12, 1.3 * k)), Mx(Wp(0, 2.45, 1.62)), bevel=0, seams=False, groove=None, name='FurnaceGlow')
    grille(T, Mx(Wp(0, 2.45, 1.72)), 1.0 * k, 1.45 * k, slats=6, depth=0.1)
    # Sunken head: an armoured cupola set low between the shoulders, vision blocks
    hp = Wp(0, 3.55, 0.85)
    armour(T, 'Primary', K(sec(-0.35, 1.0, 0.9, 0.35) + sec(0.2, 0.92, 0.82, 0.3) + sec(0.48, 0.65, 0.55, 0.22)), Mx(hp), bevel=0.05, seam_min=0.4, name='Cupola')
    for a in (-45, -15, 15, 45):
        armour(T, 'Glow', boxpts((0.32 * k, 0.1, 0.14 * k)), Mx(hp, (0, 0, a)) @ Mx((0, 0.86 * k, 0.0), (-15, 0, 0)), bevel=0, seams=False, groove=None, name='VisionBlock')
    # Back: twin smokestacks close together, a heavy radiator
    for s in (-1, 1):
        stack(T, Wp(s * 0.55, 2.3, -1.75), 2.4 * k, 0.26 * k)
    grille(T, Mx(Wp(0, 2.0, -1.7), (0, 0, 180)), 2.2 * k, 1.0 * k, slats=5, depth=0.1)
    R.rigid('Torso', W, 'Upper')
    for s in (-1, 1):
        arm(R, s, Wp(s * 3.25, 2.7, 0.1), Wp(s * 3.35, 0.8, 0.3), Wp(s * 3.35, 0.65, 1.5), k=0.9 * k, gauntlet=(1.25, 1.1, 1.2))
    rams = [(Wp(s * 2.35, 1.6, -1.1), Wp(s * 2.8, 2.4, -1.5)) for s in (-1, 1)]
    # Huge shoulders: rounded layered pads with studs
    for s, nm in ((-1, 'PauldronL'), (1, 'PauldronR')):
        cc = Wp(s * 3.35, 3.45, 0)
        M = Mx(cc)
        armour(nm, 'Primary', K(sec(-0.7, 1.35, 1.6, 0.55, s * 0.12, 0) + sec(0.35, 1.35, 1.7, 0.6) + sec(0.85, 0.95, 1.25, 0.4, -s * 0.1, 0)), M, bevel=0.06, seam_min=0.6, name='Pauldron')
        armour(nm, 'Secondary', K(sec(-1.25, 1.2, 1.45, 0.45, s * 0.25, 0) + sec(-0.6, 1.25, 1.55, 0.5, s * 0.2, 0)), M, bevel=0.05, seam_min=0.5, name='PauldronSkirt')
        for j in range(3):
            p = cc + Vector((s * 0.55 * k, (j - 1) * 0.8 * k, 0.88 * k))
            lathe(nm, 'Metal', p, (0, 0, 1), [(0, 0), (0.16 * k, 0), (0.14 * k, 0.12 * k), (0, 0.3 * k)], segs=10, name='Stud')
        stripe(nm, M @ Mx((s * 1.36 * k, 0, 0.0), (0, 0, s * 90)), 1.8 * k, 0.16)
        R.rigid(nm, cc, 'Upper')
    antenna(Wp(1.6, 3.6, -1.0), 2.2)
    return dict(arms=Wp(0, 2.7, 0.1), rams=rams, lug=lambda s: Wp(s * 2.35, 1.6, -1.1), armlug=lambda s: Wp(s * 2.8, 2.4, -1.5),
                back=(Wp(-1.85, 3.75, -1.9), Wp(1.85, 3.75, -1.9)), shoulder=Wp(-3.3, 4.3, -0.2), smoke=Wp(0, 2.6, -1.5))


def torso_tower():
    """Bastion: a tall narrow observation tower with a rangefinder head and a periscope."""
    slew_top(1.0)
    c = Wp(0, 0, 0)
    armour(T, 'Dark', K(sec(1.1, 1.25, 1.1, 0.3) + sec(4.6, 1.05, 0.95, 0.3)), Mx(c), bevel=0.05, seams=False, groove=None, name='TowerCore')
    armour(T, 'Primary', K([(x, y, z) for x in (-1.35, 1.35) for (y, z) in ((1.15, 1.3), (1.4, 1.6), (1.3, 4.3), (1.05, 4.6))] + [(x, 0.85, z) for x in (-1.35, 1.35) for z in (1.3, 4.6)]),
           Mx(c), bevel=0.05, seam_min=0.7, name='FrontSlab')
    stripe(T, Mx(Wp(0.9, 3.0, 1.43)), 0.18 * k, 2.4 * k)
    for s in (-1, 1):
        armour(T, 'Secondary', K([(s * x, y, z) for x in (1.2, 1.45) for y in (-1.15, 1.0) for z in (1.5, 4.3)]), Mx(c), bevel=0.04, seam_min=0.7, name='SideSlab')
        for zz in (2.2, 3.4):
            armour(T, 'Black', boxpts((0.06, 1.6 * k, 0.08 * k)), Mx(Wp(s * 1.46, zz, -0.1)), bevel=0, seams=False, groove=None, name='Groove')
    hazard(T, Mx(Wp(0, 1.8, 1.43), (-8, 0, 0)), 2.2 * k, 0.24, n=6)
    # Back: equipment cabinet with a cooling grille
    armour(T, 'Primary', K(sec(1.6, 1.0, 0.55, 0.2, 0, -1.4) + sec(3.9, 1.0, 0.55, 0.2, 0, -1.4)), Mx(c), bevel=0.04, seam_min=0.5, name='Cabinet')
    grille(T, Mx(Wp(0, 2.8, -1.98), (0, 0, 180)), 1.5 * k, 1.6 * k, slats=6, depth=0.08)
    # Rangefinder head: a long bar with lens drums at both ends and a slit visor in the middle
    hp = Wp(0, 5.0, 0.15)
    armour(T, 'Primary', K(sec(-0.38, 1.55, 0.42, 0.15) + sec(0.36, 1.5, 0.38, 0.14)), Mx(hp), bevel=0.04, seam_min=0.3, name='RangeBar')
    armour(T, 'Dark', K(sec(-0.75, 0.45, 0.45, 0.12) + sec(-0.35, 0.5, 0.5, 0.12)), Mx(hp), bevel=0.03, seams=False, groove=None, name='HeadNeck')
    for s in (-1, 1):
        e = hp + Vector((s * 1.72, 0.0, 0.0)) * k
        lathe(T, 'Secondary', e + Vector((0, -0.5 * k, 0)), (0, 1, 0), [(0, 0), (0.42 * k, 0), (0.45 * k, 0.1 * k), (0.45 * k, 0.95 * k), (0.36 * k, 1.05 * k), (0, 1.05 * k)], segs=24, name='LensDrum')
        lathe(T, 'Black', e + Vector((0, 0.55 * k, 0)), (0, 1, 0), [(0, 0), (0.33 * k, 0), (0.33 * k, 0.04), (0, 0.04)], segs=24, name='LensRim')
        lathe(T, 'Glow', e + Vector((0, 0.56 * k, 0)), (0, 1, 0), [(0, 0), (0.25 * k, 0), (0, 0.06 * k)], segs=20, name='Lens')
    armour(T, 'Black', boxpts((1.3 * k, 0.12, 0.16 * k)), Mx(hp + Vector((0, 0.4 * k, 0.03))), bevel=0, seams=False, groove=None, name='SlitBack')
    armour(T, 'Glow', boxpts((1.15 * k, 0.06, 0.07 * k)), Mx(hp + Vector((0, 0.44 * k, 0.03))), bevel=0, seams=False, groove=None, name='Slit')
    R.rigid('Torso', W, 'Upper')
    for s in (-1, 1):
        arm(R, s, Wp(s * 2.35, 3.3, 0.1), Wp(s * 2.45, 1.35, 0.25), Wp(s * 2.45, 1.2, 1.4), k=0.78 * k, gauntlet=(1.05, 1.0, 1.0))
    rams = [(Wp(s * 1.25, 2.3, -0.9), Wp(s * 1.95, 3.05, -1.35)) for s in (-1, 1)]
    # Square shoulder shields
    for s, nm in ((-1, 'PauldronL'), (1, 'PauldronR')):
        cc = Wp(s * 2.45, 4.05, 0)
        M = Mx(cc)
        armour(nm, 'Primary', K([(x, y, z) for x in (-0.75, 0.75) for y in (-1.2, 1.25) for z in (-0.2, 0.45)] + [(s * 0.95, y, -0.9) for y in (-1.1, 1.1)] + [(-s * 0.6, y, -0.5) for y in (-1.1, 1.1)]),
               M, bevel=0.05, seam_min=0.5, name='Pauldron')
        stripe(nm, M @ Mx((0, 1.26 * k, 0.1 * k)), 1.1 * k, 0.15)
        R.rigid(nm, cc, 'Upper')
    # Periscope mast with a sensor head (the detachable antenna)
    mast = Wp(-0.7, 5.4, -0.55)
    lathe('Antenna', 'Dark', mast, (0, 0, 1), [(0, 0), (0.16, 0), (0.14, 1.6 * k), (0, 1.6 * k)], segs=12, name='Periscope')
    armour('Antenna', 'Secondary', boxpts((0.5 * k, 0.55 * k, 0.4 * k), ch=0.08), Mx(mast + Vector((0, 0.08, 1.7 * k))), bevel=0.03, seams=False, groove=None, name='SensorHead')
    armour('Antenna', 'Glow', boxpts((0.34 * k, 0.05, 0.14 * k)), Mx(mast + Vector((0, 0.36 * k, 1.72 * k))), bevel=0, seams=False, groove=None, name='SensorLens')
    lathe('Antenna', 'Dark', mast + Vector((0.3, 0, 0)), (0, 0, 1), [(0.05, 0), (0.035, 2.4 * k), (0, 2.5 * k)], segs=8, name='Whip')
    R.rigid('Antenna', mast, 'Upper')
    return dict(arms=Wp(0, 3.3, 0.1), rams=rams, lug=lambda s: Wp(s * 1.25, 2.3, -0.9), armlug=lambda s: Wp(s * 1.95, 3.05, -1.35),
                back=(Wp(-1.35, 4.75, -1.4), Wp(1.35, 4.75, -1.4)), shoulder=Wp(-2.45, 4.7, -0.2), smoke=Wp(0, 3.0, -1.2))


def antenna(mast, h):
    armour('Antenna', 'Dark', boxpts((0.3, 0.3, 0.4), ch=0.06), Mx(mast + Vector((0, 0, 0.2))), bevel=0.03, seams=False, groove=None, name='MastBase')
    lathe('Antenna', 'Dark', mast, (0, 0, 1), [(0.06, 0.4), (0.045, h * k), (0.0, h * k + 0.1)], segs=8, name='Mast')
    lathe('Antenna', 'Glow', mast + Vector((0, 0, h * k - 0.1)), (0, 0, 1), [(0, 0), (0.06, 0), (0, 0.1)], segs=10, name='Tip')
    R.rigid('Antenna', mast, 'Upper')


# ===========================================================================
# Build
# ===========================================================================
{'Raptor': hull_raptor, 'Hornet': hull_hornet, 'Juggernaut': hull_juggernaut, 'Bastion': hull_bastion}[NAME]()
column()
R.rigid('Hull', (0, 0, STAND), 'Root')

tor = {'Raptor': torso_gunner, 'Hornet': torso_raider, 'Juggernaut': torso_brute, 'Bastion': torso_tower}[NAME]()
for s in (-1, 1):
    armour(T, 'Dark', boxpts((0.45 * k, 0.6 * k, 0.6 * k), ch=0.12 * k), Mx(tor['lug'](s)), bevel=0.04, seams=False, groove=None, name='ShLug')
    armour('Arms', 'Dark', boxpts((0.45 * k, 0.6 * k, 0.6 * k), ch=0.12 * k), Mx(tor['armlug'](s)), bevel=0.04, seams=False, groove=None, name='ArmLug')
ARMS = tor['arms']
R.rigid('Arms', ARMS, 'Arms')
shoulder_rams(R, tor['rams'], k=0.6 * k)

R.anchor('BackL', tor['back'][0], 'Upper')
R.anchor('BackR', tor['back'][1], 'Upper')
R.anchor('Shoulder', tor['shoulder'], 'Arms')
R.anchor('Smoke', tor['smoke'], 'Upper')

f = detach_fenders
order = {'Raptor': ['PauldronL', f[0], 'Antenna', f[3], 'PauldronR', f[1], f[2]],
         'Hornet': [f[0], 'PauldronL', f[3], 'Antenna', f[1], 'PauldronR', f[2]],
         'Juggernaut': [f[0], 'PauldronL', f[2], 'Antenna', f[1], 'PauldronR', f[3]],
         'Bastion': [f[0], 'PauldronL', 'Shield', f[3], 'Antenna', 'PauldronR', f[1], f[2]]}[NAME]
R.extra.update({'upper': list(U(W)), 'arms': list(U(ARMS)), 'detach': order, 'wheels': len(wheels), 'wheelRadius': S['r']})
run(R, os.path.dirname(os.path.abspath(__file__)), STAND + 1.5, 30)
