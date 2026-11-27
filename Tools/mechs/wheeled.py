"""
The wheeled robots, second-generation hard-surface models: an armoured drive hull
of cut, sloped plate (like a modern wheeled APC) on big independently sprung
wheels, carrying a real robot upper body on a slewing column.
Usage: wheeled.py <Raptor|Hornet|Juggernaut|Bastion> [-- out] [--render]

Every wheel hangs on a trailing arm with its own suspension cylinder; the torso
turns on a geared ring above a column braced by four cylinders; the arms pitch on
working shoulder cylinders.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mechlib import reset
from mathutils import Vector, Matrix
from hardsurf import *
from parts import *

NAME = [a for a in sys.argv[1:] if a in ('Raptor', 'Hornet', 'Juggernaut', 'Bastion')][0]

SPECS = {
    'Raptor': dict(stand=2.6, k=0.85, waist=(0, 2.35, -0.6), hull=(5.4, 2.0, 10.4), hull_c=(0, 0.8, 0.2), wx=3.3, wz=(3.2, 0, -3.2), r=1.4, w=1.1,
                   boost=[(-1.05, 0.9, -3.9), (1.05, 0.9, -3.9)], bd=1.0, deck=1.55, head=1, fenders='ends'),
    'Hornet': dict(stand=2.2, k=0.68, waist=(0, 2.05, -0.5), hull=(4.4, 1.6, 8.4), hull_c=(0, 0.7, 0), wx=2.55, wz=(2.6, -2.4), r=1.2, w=1.0,
                   boost=[(-0.9, 0.8, -3.9), (0.9, 0.8, -3.9)], bd=1.0, deck=1.3, head=2, fenders='all', wing=True),
    'Juggernaut': dict(stand=3.0, k=1.05, waist=(0, 2.85, -0.9), hull=(6.4, 2.6, 13.4), hull_c=(0, 1.0, 0.4), wx=3.4, wz=(4.6, 1.6, -1.4, -4.4), r=1.5, w=1.3,
                       boost=[(-1.3, 1.2, -4.9), (1.3, 1.2, -4.9)], bd=1.2, deck=2.05, head=0, fenders='skirts'),
    'Bastion': dict(stand=2.8, k=0.95, waist=(0, 2.7, -1.0), hull=(6.0, 2.4, 11.6), hull_c=(0, 1.0, 0), wx=2.9, wz=(3.6, 0, -3.6), r=1.35, w=1.2,
                    boost=[(-1.5, 1.1, -4.5), (1.5, 1.1, -4.5)], bd=1.1, deck=1.92, head=1, fenders='ends', shield=True),
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


# ===========================================================================
# Drive hull
# ===========================================================================
H = 'Hull'
hw, hh, hl = S['hull'][0] / 2, S['hull'][1] / 2, S['hull'][2] / 2
hc = B(*S['hull_c'])
inner = hw * 0.64
# Lower tub: a hexagonal-section hull with a long sloped glacis and rear plate
def hsec(y, w, zb, zt, bw):
    return [(x, y, z) for x, z in ((bw, zb), (-bw, zb), (w, (zb + zt) * 0.5 - hh * 0.1), (-w, (zb + zt) * 0.5 - hh * 0.1), (w * 0.92, zt), (-w * 0.92, zt))]
tub = hsec(hl * 1.0, inner * 0.8, -hh * 0.45, hh * 0.05, inner * 0.55) + hsec(hl * 0.55, inner, -hh * 0.9, hh * 0.65, inner * 0.7) + \
      hsec(-hl * 0.7, inner, -hh * 0.9, hh * 0.7, inner * 0.7) + hsec(-hl * 1.0, inner * 0.85, -hh * 0.5, hh * 0.5, inner * 0.6)
armour(H, 'Dark', tub, M=Matrix.Translation(hc), bevel=0.06, seams=False, groove=None, name='Tub')
# Upper hull armour: glacis, roof and sponsons over the wheels
gl = [(x, y, z) for (y, w, z) in ((hl * 1.03, inner * 0.82, 0.0), (hl * 0.5, hw * 0.82, hh * 0.85)) for x in (-w, w)]
armour(H, 'Primary', gl + [(x, y - 0.35, z) for x, y, z in gl], M=Matrix.Translation(hc), bevel=0.06, seam_min=0.8, name='Glacis')
roof = [(x, y, z) for (y, w, z) in ((hl * 0.52, hw * 0.82, hh * 0.85), (-hl * 0.85, hw * 0.84, hh * 0.9), (-hl * 1.0, hw * 0.7, hh * 0.5)) for x in (-w, w)]
armour(H, 'Secondary', roof + [(x, y, z - 0.4) for x, y, z in roof], M=Matrix.Translation(hc), bevel=0.06, seam_min=1.2, seam_w=0.22, name='Roof')
for s in (-1, 1):
    lamp(H, Mx(hc + Vector((s * inner * 0.62, hl * 1.0, -hh * 0.15)), (-20, 0, 0)), 0.34, 0.14)
    # Sponson armour along the side above the wheels
    sp = [(s * x, y, z) for (x, y, z) in ((hw * 0.8, hl * 0.6, hh * 0.85), (hw * 0.8, -hl * 0.9, hh * 0.9), (hw * 1.02, hl * 0.55, hh * 0.45), (hw * 1.02, -hl * 0.88, hh * 0.5),
                                         (hw * 0.6, hl * 0.6, hh * 0.85), (hw * 0.6, -hl * 0.9, hh * 0.9), (hw * 0.6, hl * 0.55, hh * 0.3), (hw * 0.6, -hl * 0.88, hh * 0.3))]
    armour(H, 'Primary', sp, M=Matrix.Translation(hc), bevel=0.05, seam_min=0.8, stripe=((s, 0, 0.6), 0.75), name='Sponson')
    grille(H, Mx(hc + Vector((s * hw * 0.62, -hl * 0.55, hh * 0.95)), (90, 0, 0)), hw * 0.45, 1.6 * k, slats=5, depth=0.08)
    hose(H, [hc + Vector((s * inner * 0.5, -hl * 0.8, hh * 0.95)), hc + Vector((s * inner * 0.7, -hl * 0.2, hh * 1.0)), hc + Vector((s * inner * 0.5, hl * 0.3, hh * 0.95))], 0.1)
hazard(H, Mx(hc + Vector((0, hl * 0.78, hh * 0.47)), (-58, 0, 0)), inner * 1.4, 0.28, n=7)
grille(H, Mx(hc + Vector((0, -hl * 1.0, 0.0)), (0, 0, 180)), hw * 0.9, hh * 0.6, slats=4, depth=0.1)
# Boosters
for i, bp in enumerate(S['boost']):
    p = B(*bp)
    d = S['bd']
    armour(H, 'Secondary', boxpts((d * 1.3, 0.8, d * 1.2), ch=0.25, endch=0.05), Mx(p + Vector((0, 0.55, 0)), (90, 0, 0)), bevel=0.04, seam_min=0.4, name='BoosterHousing')
    lathe(H, 'Metal', p + Vector((0, 0.35, 0)), (0, -1, 0), [(0, 0), (d * 0.5, 0), (d * 0.55, 0.35), (d * 0.46, 0.4), (d * 0.6, 0.85), (d * 0.52, 0.88), (d * 0.36, 0.4), (0, 0.4)], segs=24, name='Booster')
    lathe(H, 'Hot', p + Vector((0, -0.07, 0)), (0, -1, 0), [(0, 0), (d * 0.36, 0), (0, 0.01)], segs=20, name='BoosterGlow')
    R.anchor('Booster%d' % i, p + Vector((0, -0.55, 0)), 'Root', (0, -1, 0))
# Slewing column carrying the torso, braced by four cylinders
deck = B(0, S['deck'], S['waist'][2])
colh = W.z - deck.z
armour(H, 'Dark', ngon(deck.z, 1.55 * k, 8, 22.5, cx=deck.x, cy=deck.y) + ngon(deck.z + 0.35, 1.35 * k, 8, 22.5, cx=deck.x, cy=deck.y), bevel=0.04, seams=False, groove=None, name='ColumnBase')
armour(H, 'Secondary', ngon(deck.z, 0.85 * k, 8, 22.5, cx=deck.x, cy=deck.y) + ngon(W.z - 0.08, 0.8 * k, 8, 22.5, cx=deck.x, cy=deck.y), bevel=0.03, seams=False, groove=None, name='Column')
for i in range(4):
    a = math.radians(45 + i * 90)
    o = Vector((math.cos(a), math.sin(a), 0)) * 1.1 * k
    piston(H, deck + o * 1.1 + Vector((0, 0, 0.35)), W + o * 0.75 - Vector((0, 0, 0.12)), 0.1 * k)
lathe(H, 'Dark', W - Vector((0, 0, 0.1)), (0, 0, 1), [(0, 0), (1.35 * k, 0), (1.35 * k, 0.12), (0, 0.12)], segs=40, name='RingBase')
gear(H, W + Vector((0, 0, 0.0)), (0, 0, 1), 1.2 * k, 0.1, teeth=36)

# Wheels and their trailing arms
wheels = []
for wz in S['wz']:
    for s in (-1, 1):
        i = len(wheels)
        hub = B(s * S['wx'], 0.2, wz)
        r, w = S['r'], S['w']
        top = Vector((s * (S['wx'] - 1.05), hub.y + 0.25, hub.z + 0.95))
        armour(H, 'Dark', boxpts((0.7, 1.1, 0.7), ch=0.2), Mx(top), bevel=0.04, seams=False, groove=None, name='ArmMount')
        arm_end = hub + Vector((-s * (w * 0.5 + 0.22), 0.0, 0.0))
        armour(H, 'Secondary', [tuple(p + Vector((0, dy, dz))) for p in (top + Vector((s * 0.1, 0, -0.1)), arm_end) for dy in (-0.28, 0.28) for dz in (-0.25, 0.25)],
               bevel=0.04, seam_min=10, name='TrailingArm')
        piston(H, top + Vector((0, -0.55, 0.2)), arm_end + Vector((0, -0.45, 0.35)), 0.13)
        P = 'Wheel%d' % i
        prof = [(r * 0.55, -w / 2), (r * 0.9, -w / 2), (r * 0.97, -w * 0.42), (r * 0.99, -w * 0.25), (r * 0.99, w * 0.25), (r * 0.97, w * 0.42), (r * 0.9, w / 2), (r * 0.55, w / 2)]
        lathe(P, 'Rubber', hub, (1, 0, 0), prof, segs=36, name='Tyre')
        # Chevron tread blocks
        for t in range(20):
            ang = 2 * math.pi * t / 20
            rad = Vector((0, math.sin(ang), math.cos(ang)))
            tan = Vector((0, math.cos(ang), -math.sin(ang)))
            for sd in (-1, 1):
                c = hub + Vector((sd * w * 0.24, 0, 0)) + rad * (r * 0.99)
                pts = [tuple(c + Vector((dx, 0, 0)) + tan * (dt + dx * 0.4 * sd) + rad * dr) for dx in (-w * 0.2, w * 0.2) for dt in (-0.12, 0.12) for dr in (-0.04, 0.12)]
                armour(P, 'Rubber', pts, bevel=0.02, seams=False, groove=None, name='Lug')
        # Rim: dished wheel with spokes and a bolted hub
        lathe(P, 'Secondary', hub, (s, 0, 0), [(0, -w * 0.45), (r * 0.6, -w * 0.45), (r * 0.6, w * 0.3), (r * 0.52, w * 0.36), (r * 0.48, w * 0.2), (0, w * 0.2)], segs=32, name='Rim')
        for sp_ in range(6):
            ang = 2 * math.pi * sp_ / 6
            rad = Vector((0, math.sin(ang), math.cos(ang))); tan = Vector((0, math.cos(ang), -math.sin(ang)))
            pts = [tuple(hub + Vector((s * x, 0, 0)) + rad * rr + tan * tt) for x in (w * 0.22, w * 0.36) for rr in (r * 0.2, r * 0.5) for tt in (-0.08, 0.08)]
            armour(P, 'Dark', pts, bevel=0.0, seams=False, groove=None, name='Spoke')
        lathe(P, 'Metal', hub + Vector((s * w * 0.36, 0, 0)), (s, 0, 0), [(0, 0), (r * 0.22, 0), (r * 0.22, 0.1), (r * 0.15, 0.2), (0, 0.22)], segs=16, name='Hub')
        for b in range(8):
            ang = 2 * math.pi * b / 8
            p = hub + Vector((s * (w * 0.46), math.sin(ang) * r * 0.32, math.cos(ang) * r * 0.32))
            lathe(P, 'Metal', p, (s, 0, 0), [(0, 0), (0.07, 0), (0.07, 0.06), (0, 0.07)], segs=6, name='Bolt')
        R.rigid(P, hub, 'Root')
        R.anchor('WheelAnchor%d' % i, hub + Vector((0, 0, 1.0)), 'Root')
        wheels.append((i, s, wz, hub))

# Fenders / skirts (detachable)
detach_fenders = []
if S['fenders'] in ('ends', 'all'):
    for i, s, wz, hub in wheels:
        if S['fenders'] == 'ends' and wz == 0:
            continue
        nm = 'Fender%d' % len(detach_fenders)
        c = hub + Vector((0, 0, S['r'] + 0.3))
        r, w = S['r'], S['w']
        pts = [(s * x, y, z) for (y, z) in ((r * 1.15, -0.55), (r * 0.55, 0.0), (-r * 0.55, 0.0), (-r * 1.15, -0.55)) for x in (-w * 0.62, w * 0.62)]
        pts += [(x, y, z + 0.22) for (x, y, z) in pts]
        armour(nm, 'Primary', pts, Mx(c), bevel=0.04, seam_min=0.6, name='Fender')
        stripe(nm, Mx(c + Vector((s * w * 0.63, 0, 0.05)), (0, 0, 90)), r * 1.2, 0.14)
        R.rigid(nm, c, 'Root')
        detach_fenders.append(nm)
elif S['fenders'] == 'skirts':
    for s in (-1, 1):
        for yy in (3.1, -2.9):
            nm = 'Fender%d' % len(detach_fenders)
            c = B(s * 3.45, 2.05, yy)
            side_plate(nm, 'Primary', [(-2.7, 0.8), (2.7, 0.8), (2.4, -0.8), (-2.4, -0.8)], 0.28, c, s, 10, stripe_at=0.45, stripe_h=0.15,
                       hazard_at=-0.45, bevel=0.05, seam_min=0.8, name='Skirt')
            bolts_line(nm, c + Vector((s * 0.2, -2.2, 0.62)), c + Vector((s * 0.2, 2.2, 0.62)), 7, (s, 0, 0), 0.07)
            R.rigid(nm, c, 'Root')
            detach_fenders.append(nm)

if S.get('wing'):
    wc = hc + Vector((0, -hl * 0.97, hh * 1.5))
    armour(H, 'Primary', [(x, y, z) for x in (-hw * 0.95, hw * 0.95) for y in (-0.5, 0.45) for z in (-0.05, 0.08)] + [(0, 0.55, 0.15)], Mx(wc, (-8, 0, 0)), bevel=0.03, seam_min=0.6, name='Wing')
    for s in (-1, 1):
        armour(H, 'Dark', boxpts((0.15, 0.45, 0.85), ch=0.05), Mx(hc + Vector((s * hw * 0.55, -hl * 0.92, hh * 1.05))), bevel=0.03, seams=False, groove=None, name='WingPost')

if S.get('shield'):
    c = B(0, 1.2, 5.6)
    pts = [(x, y, z) for (x, y) in ((-3.05, -0.25), (-1.5, 0.15), (0, 0.3), (1.5, 0.15), (3.05, -0.25)) for z in (-1.05, 1.05)]
    pts += [(x, y - 0.55, z * 0.92) for (x, y, z) in pts]
    armour('Shield', 'Primary', pts, Mx(c), bevel=0.06, seam_min=0.8, name='Blade')
    armour('Shield', 'Metal', [(x, y, z) for x in (-3.1, 3.1) for y in (0.15, 0.35) for z in (-1.15, -0.95)], Mx(c), bevel=0.02, seams=False, groove=None, name='Edge')
    hazard('Shield', Mx(c + Vector((0, 0.33, 0.7))), 4.0, 0.3, n=10)
    for s in (-1, 1):
        piston(H, B(s * 1.6, 0.9, 3.6), c + Vector((s * 1.6, -0.45, -0.2)), 0.16)
    R.rigid('Shield', c, 'Root')

R.rigid('Hull', (0, 0, STAND), 'Root')

# ===========================================================================
# Upper body
# ===========================================================================
T = 'Torso'
lathe(T, 'Metal', W, (0, 0, 1), [(0, 0), (1.3 * k, 0), (1.32 * k, 0.05), (1.32 * k, 0.3), (0, 0.33)], segs=40, name='RingTop')
armour(T, 'Dark', ngon(W.z + 0.3, 1.1 * k, 8, 22.5, cx=W.x, cy=W.y) + ngon(W.z + 1.4 * k, 1.2 * k, 8, 22.5, cx=W.x, cy=W.y), bevel=0.04, seams=False, groove=None, name='Waist')
c = Wp(0, 0, 0)
chest = [(x * k, y * k, z * k) for x, y, z in
         sec(1.3, 1.3, 1.1, 0.35) + sec(2.1, 2.0, 1.45, 0.55, 0, 0.1) + sec(3.1, 2.15, 1.5, 0.6, 0, 0.1) + sec(3.9, 1.5, 1.1, 0.45, 0, -0.1)]
armour(T, 'Dark', chest, Mx(c), bevel=0.06, seams=False, groove=None, name='ChestCore')
# Front armour: two angled breast plates meeting at a keel
for s in (-1, 1):
    pts = [(s * x * k, y * k, z * k) for x, y, z in ((0.05, 1.75, 1.6), (1.6, 1.3, 1.9), (1.75, 1.35, 3.4), (0.05, 1.85, 3.5), (0.05, 1.4, 3.95), (1.3, 1.1, 3.9),
                                                      (0.05, 1.45, 1.5), (1.5, 1.0, 1.8), (1.6, 1.0, 3.3))]
    armour(T, 'Primary', pts, Mx(c), bevel=0.05, seam_min=0.5, name='Breast')
    side = [(s * x * k, y * k, z * k) for x, y, z in ((1.95, -1.2, 1.9), (1.95, 1.2, 1.9), (2.2, -1.3, 3.2), (2.2, 1.25, 3.2), (1.7, -1.0, 3.9), (1.7, 0.9, 3.9),
                                                       (1.75, -1.2, 1.9), (1.75, 1.2, 1.9), (2.0, -1.3, 3.2), (2.0, 1.25, 3.2))]
    armour(T, 'Secondary', side, Mx(c), bevel=0.05, seam_min=0.6, name='ChestSide')
    armour(T, 'Dark', boxpts((0.45 * k, 0.6 * k, 0.6 * k), ch=0.12 * k), Mx(Wp(s * 1.95, 1.7, -1.1)), bevel=0.04, seams=False, groove=None, name='ShLug')
stripe(T, Mx(Wp(0.55, 3.2, 1.72), (0, 0, -18)), 0.8 * k, 0.14)
# Backpack with radiator and a glowing reactor port
pk = [(x * k, y * k, z * k) for x, y, z in sec(1.4, 1.2, 0.65, 0.25, 0, -1.75) + sec(3.6, 1.3, 0.7, 0.25, 0, -1.85)]
armour(T, 'Primary', pk, Mx(c), bevel=0.05, seam_min=0.6, name='Pack')
grille(T, Mx(Wp(0, 2.9, -2.6), (0, 0, 180)), 1.6 * k, 0.8 * k, slats=4, depth=0.08)
lathe(T, 'Dark', Wp(0, 1.95, -2.6), (0, -1, 0), [(0, 0), (0.55 * k, 0), (0.55 * k, 0.12), (0, 0.12)], segs=24, name='PortRim')
lathe(T, 'Glow', Wp(0, 1.95, -2.6), (0, -1, 0), [(0, 0.06), (0.4 * k, 0.06), (0.4 * k, 0.16), (0, 0.16)], segs=24, name='Port')
# Head
hp = Wp(0, 4.2, 0.35)
hs = S['head']
if hs == 0:
    # Crew cab: an armoured cupola with vision blocks
    armour(T, 'Primary', [(x * k, y * k, z * k) for x, y, z in sec(-0.35, 1.05, 0.95, 0.35) + sec(0.25, 0.95, 0.85, 0.3) + sec(0.55, 0.7, 0.6, 0.25)], Mx(hp), bevel=0.05, seam_min=0.4, name='Cupola')
    for a in (-40, 0, 40):
        Mv = Mx(hp, (0, 0, a)) @ Mx((0, 0.9 * k, 0.05 * k), (-15, 0, 0))
        armour(T, 'Glow', boxpts((0.45 * k, 0.12, 0.16 * k)), Mv, bevel=0, seams=False, groove=None, name='VisionBlock')
else:
    armour(T, 'Primary', [(x * k, y * k, z * k) for x, y, z in sec(-0.4, 0.8, 0.85, 0.28) + sec(0.35, 0.72, 0.75, 0.25, 0, -0.05) + [(0, 1.05, -0.1), (0.45, 0.95, -0.12), (-0.45, 0.95, -0.12)]],
           Mx(hp), bevel=0.04, seam_min=0.4, name='Head')
    armour(T, 'Secondary', [(x * k, y * k, z * k) for x, y, z in sec(0.0, 0.9, 0.5, 0.2, 0, 0.35) + sec(0.18, 0.78, 0.7, 0.2, 0, 0.3)], Mx(hp + Vector((0, 0, 0.35 * k))), bevel=0.04, seam_min=0.4, name='Brow')
    if hs == 1:
        armour(T, 'Black', boxpts((1.2 * k, 0.3, 0.22 * k), ch=0.06), Mx(hp + Vector((0, 0.8 * k, 0.02)), (-6, 0, 0)), bevel=0.02, seams=False, groove=None, name='VisorSlot')
        armour(T, 'Glow', boxpts((1.05 * k, 0.1, 0.1 * k)), Mx(hp + Vector((0, 0.92 * k, 0.02)), (-6, 0, 0)), bevel=0, seams=False, groove=None, name='Visor')
    else:
        e = hp + Vector((0, 0.95 * k, 0.0))
        lathe(T, 'Dark', e, (0, 1, 0), [(0, -0.2), (0.38 * k, -0.2), (0.4 * k, 0.12), (0.3 * k, 0.16), (0, 0.16)], segs=24, name='EyeRing')
        lathe(T, 'Glow', e, (0, 1, 0), [(0, 0.1), (0.26 * k, 0.1), (0.2 * k, 0.17), (0, 0.18)], segs=24, name='Eye')
R.rigid('Torso', W, 'Upper')

ARMS = Wp(0, 2.8, 0.1)
for s in (-1, 1):
    arm(R, s, Wp(s * 2.7, 2.8, 0.1), Wp(s * 2.8, 0.9, 0.25), Wp(s * 2.8, 0.75, 1.35), k=0.78 * k, gauntlet=(1.0, 1.0, 1.0))
    armour('Arms', 'Dark', boxpts((0.45 * k, 0.6 * k, 0.6 * k), ch=0.12 * k), Mx(Wp(s * 2.3, 2.5, -1.45)), bevel=0.04, seams=False, groove=None, name='ArmLug')
R.rigid('Arms', ARMS, 'Arms')
shoulder_rams(R, [(Wp(s * 1.95, 1.7, -1.1), Wp(s * 2.3, 2.5, -1.45)) for s in (-1, 1)], k=0.6 * k)

for s, nm in ((-1, 'PauldronL'), (1, 'PauldronR')):
    cc = Wp(s * 2.85, 3.5, 0)
    M = Mx(cc)
    armour(nm, 'Primary', [(x * k, y * k, z * k) for x, y, z in sec(-0.45, 1.0, 1.3, 0.4, s * 0.1, 0) + sec(0.3, 0.95, 1.35, 0.42) + sec(0.6, 0.6, 1.0, 0.3, -s * 0.1, -0.05)],
           M, bevel=0.05, seam_min=0.5, name='Pauldron')
    side_plate(nm, 'Secondary', [(-1.3 * k, 0.25 * k), (1.3 * k, 0.25 * k), (1.05 * k, -0.85 * k), (-1.05 * k, -0.85 * k)], 0.16, (s * 1.05 * k, 0, -0.5 * k), s, 10, M=M,
               stripe_at=0.0, stripe_h=0.13, bevel=0.04, seam_min=0.4, name='Plate')
    R.rigid(nm, cc, 'Upper')

mast = Wp(0.7, 4.0, -0.6)
armour('Antenna', 'Dark', boxpts((0.3, 0.3, 0.4), ch=0.06), Mx(mast + Vector((0, 0, 0.2))), bevel=0.03, seams=False, groove=None, name='MastBase')
lathe('Antenna', 'Dark', mast, (0, 0, 1), [(0.06, 0.4), (0.045, 1.9 * k), (0.0, 2.0 * k)], segs=8, name='Mast')
lathe('Antenna', 'Glow', mast + Vector((0, 0, 1.9 * k)), (0, 0, 1), [(0, 0), (0.06, 0), (0, 0.1)], segs=10, name='Tip')
R.rigid('Antenna', mast, 'Upper')

R.anchor('BackL', Wp(-1.45, 3.85, -1.8), 'Upper')
R.anchor('BackR', Wp(1.45, 3.85, -1.8), 'Upper')
R.anchor('Shoulder', Wp(-2.8, 4.25, -0.2), 'Arms')
R.anchor('Smoke', Wp(0, 2.4, -1.2), 'Upper')

f = detach_fenders
order = {'Raptor': ['PauldronL', f[0], 'Antenna', f[3], 'PauldronR', f[1], f[2]],
         'Hornet': [f[0], 'PauldronL', f[3], 'Antenna', f[1], 'PauldronR', f[2]],
         'Juggernaut': [f[0], 'PauldronL', f[2], 'Antenna', f[1], 'PauldronR', f[3]],
         'Bastion': [f[0], 'PauldronL', 'Shield', f[3], 'Antenna', 'PauldronR', f[1], f[2]]}[NAME]
R.extra.update({'upper': list(U(W)), 'arms': list(U(ARMS)), 'detach': order, 'wheels': len(wheels), 'wheelRadius': S['r']})
run(R, os.path.dirname(os.path.abspath(__file__)), STAND + 1.5, 30)
