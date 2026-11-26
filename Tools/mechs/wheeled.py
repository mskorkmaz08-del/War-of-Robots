"""
The wheeled robots: an armoured drive base on big independently sprung wheels,
carrying a real robot upper body (chest, head, shoulders, gun arms) on a
hydraulic column. Usage: wheeled.py <Raptor|Hornet|Juggernaut|Bastion> [-- out] [--render]

Engineering notes: wheels spread the weight far better than feet, so these are
the fast ones. Each wheel hangs on a trailing fork with its own ram; the hull
is a sloped cast tub (glacis up front); boosters sit low at the back.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mechlib import *
from mathutils import Vector, Matrix
from rig import Rig, U, bolts_ring, stripe, run, arm

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
# Drive base
# ===========================================================================
H = 'Hull'
hw, hh, hl = S['hull'][0] / 2, S['hull'][1] / 2, S['hull'][2] / 2
hc = B(*S['hull_c'])
inner = hw * 0.62
# Lower tub, sloped glacis at the front, rounded flanks
loft(H, 'Dark', [(hc + Vector((0, hl * 0.98, -hh * 0.35)), inner * 0.7, hh * 0.45, 3.4), (hc + Vector((0, hl * 0.7, -hh * 0.2)), inner, hh * 0.75, 3.6),
                 (hc + Vector((0, -hl * 0.75, -hh * 0.15)), inner, hh * 0.8, 3.6), (hc + Vector((0, -hl * 0.95, 0)), inner * 0.85, hh * 0.65, 3.4)],
     axis_up=(0, 0, 1), segs=36, name='Tub')
loft(H, 'Secondary', [(hc + Vector((0, hl * 0.92, hh * 0.15)), inner * 0.8, hh * 0.35, 3.0), (hc + Vector((0, hl * 0.45, hh * 0.45)), hw * 0.8, hh * 0.5, 3.2),
                      (hc + Vector((0, -hl * 0.6, hh * 0.5)), hw * 0.82, hh * 0.5, 3.2), (hc + Vector((0, -hl * 0.92, hh * 0.35)), hw * 0.7, hh * 0.4, 3.0)],
     axis_up=(0, 0, 1), segs=36, name='Deck')
plate(H, 'Primary', hc + Vector((0, hl * 0.93, hh * 0.15)), (hw * 1.4, hh * 1.4, 0.3), rot=(-58, 0, 0), curve=(0.06, 0.04), taper=0.85)
for s in (-1, 1):
    lamp(H, hc + Vector((s * inner * 0.7, hl * 1.0, hh * 0.05)), 0.32, rot=(-25, 0, 0))
    plate(H, 'Primary', hc + Vector((s * hw * 0.86, -hl * 0.05, hh * 0.35)), (hl * 1.4, hh * 0.9, 0.24), rot=(0, s * -8, s * 90), curve=(0.02, 0.1), taper=0.95)
    stripe(H, hc + Vector((s * hw * 0.55, hl * 0.35, hh * 0.96)), (0.22, hl * 0.6, 0.05))
vent(H, hc + Vector((0, -hl * 0.97, hh * 0.1)), (hw * 1.0, 0.25, hh * 0.8), rot=(0, 0, 180), slats=4)
# Boosters
for i, bp in enumerate(S['boost']):
    p = B(*bp)
    d = S['bd']
    lathe(H, 'Metal', p + Vector((0, 0.35, 0)), (0, -1, 0), [(0, 0), (d * 0.5, 0), (d * 0.55, 0.35), (d * 0.46, 0.4), (d * 0.6, 0.85), (d * 0.52, 0.88), (d * 0.36, 0.4), (0, 0.4)], segs=28, name='Booster')
    lathe(H, 'Hot', p + Vector((0, -0.07, 0)), (0, -1, 0), [(0, 0), (d * 0.36, 0), (0, 0.01)], segs=24, name='BoosterGlow')
    R.anchor('Booster%d' % i, p + Vector((0, -0.55, 0)), 'Root', (0, -1, 0))
# Pedestal: the hydraulic column carrying the torso
deck = B(0, S['deck'], S['waist'][2])
colh = W.z - deck.z
lathe(H, 'Dark', deck, (0, 0, 1), [(0, 0), (1.45 * k, 0), (1.5 * k, 0.1), (1.5 * k, 0.3), (1.2 * k, 0.35), (0, 0.35)], segs=40, name='ColumnBase')
lathe(H, 'Metal', deck, (0, 0, 1), [(0, 0), (0.85 * k, 0), (0.85 * k, colh - 0.05), (0, colh - 0.05)], segs=32, name='Column')
for i in range(4):
    a = math.radians(45 + i * 90)
    o = Vector((math.cos(a), math.sin(a), 0)) * 1.1 * k
    ram(H, deck + o * 1.1 + Vector((0, 0, 0.3)), W + o * 0.75 - Vector((0, 0, 0.12)), 0.1 * k)
lathe(H, 'Dark', W - Vector((0, 0, 0.1)), (0, 0, 1), [(0, 0), (1.35 * k, 0), (1.35 * k, 0.12), (0, 0.12)], segs=40, name='RingBase')
bolts_ring(H, W + Vector((0, 0, 0.02)), (0, 0, 1), 1.18 * k, 16, 0.06)

# Wheels and their forks
wheels = []
for wz in S['wz']:
    for s in (-1, 1):
        i = len(wheels)
        hub = B(s * S['wx'], 0.2, wz)
        r, w = S['r'], S['w']
        # Fork arm from the hull side to the hub, with its spring ram
        top = Vector((s * (S['wx'] - 1.05), hub.y + 0.2, hub.z + 0.95))
        block(H, 'Dark', top, (0.7, 1.0, 0.6), bevel=0.12, name='ForkMount')
        loft(H, 'Secondary', [(top + Vector((s * 0.1, 0.0, -0.05)), 0.32, 0.28, 3), (hub + Vector((-s * (w * 0.5 + 0.25), 0.25, 0.1)), 0.26, 0.24, 3)],
             axis_up=(0, 1, 0), segs=20, sub=1, name='Fork')
        ram(H, top + Vector((0, -0.5, 0.15)), hub + Vector((-s * (w * 0.5 + 0.25), -0.35, 0.35)), 0.14)
        # The wheel (spins about X in the game)
        P = 'Wheel%d' % i
        prof = [(r * 0.55, -w / 2), (r * 0.9, -w / 2), (r * 0.98, -w * 0.4), (r, -w * 0.25), (r, w * 0.25), (r * 0.98, w * 0.4), (r * 0.9, w / 2), (r * 0.55, w / 2)]
        lathe(P, 'Rubber', hub, (1, 0, 0), prof, segs=40, name='Tyre')
        for t in range(18):
            ang = 2 * math.pi * t / 18
            for sd in (-1, 1):
                c = hub + Vector((sd * w * 0.22, math.sin(ang) * r * 1.0, math.cos(ang) * r * 1.0))
                block(P, 'Rubber', c, (w * 0.38, r * 0.17, 0.16), rot=(-math.degrees(ang), 0, sd * 20), bevel=0.03, segs=1, name='Lug')
        lathe(P, 'Secondary', hub, (s, 0, 0), [(0, -w * 0.45), (r * 0.6, -w * 0.45), (r * 0.6, w * 0.3), (r * 0.5, w * 0.38), (r * 0.25, w * 0.42), (0, w * 0.42)], segs=36, name='Rim')
        lathe(P, 'Chrome', hub + Vector((s * w * 0.42, 0, 0)), (s, 0, 0), [(0, 0), (r * 0.18, 0), (r * 0.15, 0.12), (0, 0.15)], segs=20, name='Hub')
        bolts_ring(P, hub + Vector((s * w * 0.44, 0, 0)), (s, 0, 0), r * 0.33, 8, 0.07)
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
        c = hub + Vector((0, 0, S['r'] + 0.32))
        loft(nm, 'Primary', [(c + Vector((0, S['r'] * 1.05, -0.45)), S['w'] * 0.75, 0.18, 2.4), (c + Vector((0, 0.0, 0.0)), S['w'] * 0.82, 0.22, 2.4),
                             (c + Vector((0, -S['r'] * 1.05, -0.45)), S['w'] * 0.75, 0.18, 2.4)], axis_up=(0, 0, 1), segs=24, name='Fender')
        stripe(nm, c + Vector((s * S['w'] * 0.84, 0, -0.05)), (0.05, S['r'] * 1.2, 0.14))
        R.rigid(nm, c, 'Root')
        detach_fenders.append(nm)
elif S['fenders'] == 'skirts':
    for s in (-1, 1):
        for yy in (3.1, -2.9):
            nm = 'Fender%d' % len(detach_fenders)
            c = B(s * 3.45, 2.05, yy)
            plate(nm, 'Primary', c, (5.4, 1.6, 0.3), rot=(0, s * -10, s * 90), curve=(0.02, 0.1), bevel=0.08, taper=0.92)
            stripe(nm, c + Vector((s * 0.2, 0, 0.45)), (0.05, 4.0, 0.15), rot=(0, s * -10, 0))
            for kk in range(4):
                lathe(nm, 'Metal', c + Vector((s * 0.18, -2.0 + kk * 1.3, -0.5)), (s, 0, 0), [(0, 0), (0.09, 0), (0.06, 0.07), (0, 0.08)], segs=8, name='Bolt')
            R.rigid(nm, c, 'Root')
            detach_fenders.append(nm)

if S.get('wing'):
    loft(H, 'Primary', [(hc + Vector((-hw * 0.95, -hl * 0.95, hh * 1.45)), 0.08, 0.45, 2.2), (hc + Vector((0, -hl * 0.98, hh * 1.5)), 0.12, 0.55, 2.2),
                        (hc + Vector((hw * 0.95, -hl * 0.95, hh * 1.45)), 0.08, 0.45, 2.2)], axis_up=(0, 1, 0), segs=16, name='Wing')
    for s in (-1, 1):
        block(H, 'Dark', hc + Vector((s * hw * 0.55, -hl * 0.9, hh * 1.05)), (0.15, 0.4, 0.8), bevel=0.04, name='WingPost')

shield_name = None
if S.get('shield'):
    shield_name = 'Shield'
    c = B(0, 1.2, 5.6)
    loft(shield_name, 'Primary', [(c + Vector((-3.0, -0.2, 0)), 1.05, 0.3, 2.6), (c + Vector((-1.5, 0.15, 0)), 1.12, 0.32, 2.6), (c + Vector((0, 0.25, 0)), 1.15, 0.34, 2.6),
                                  (c + Vector((1.5, 0.15, 0)), 1.12, 0.32, 2.6), (c + Vector((3.0, -0.2, 0)), 1.05, 0.3, 2.6)], axis_up=(0, 1, 0), segs=24, name='Blade')
    lathe(shield_name, 'Metal', c + Vector((-3.1, 0.35, -1.0)), (1, 0, 0), [(0, 0), (0.2, 0), (0.2, 6.2), (0, 6.2)], segs=16, name='Edge')
    for s in (-1, 1):
        stripe(shield_name, c + Vector((s * 1.4, 0.45, 0.75)), (1.2, 0.05, 0.14))
        ram(H, B(s * 1.6, 0.9, 3.6), c + Vector((s * 1.6, -0.35, -0.2)), 0.16)
    for kk in range(6):
        block(shield_name, 'Dark', c + Vector((-2.5 + kk, 0.42, -0.2)), (0.1, 0.06, 1.3), bevel=0, name='Rib')
    R.rigid(shield_name, c, 'Root')

R.rigid('Hull', (0, 0, STAND), 'Root')

# ===========================================================================
# Upper body
# ===========================================================================
T = 'Torso'
lathe(T, 'Metal', W, (0, 0, 1), [(0, 0), (1.3 * k, 0), (1.32 * k, 0.05), (1.32 * k, 0.3), (0, 0.33)], segs=40, name='RingTop')
loft(T, 'Dark', [(Wp(0, 0.4, 0), 1.05 * k, 0.95 * k, 3.4), (Wp(0, 1.4, 0.05), 1.15 * k, 1.0 * k, 3.4)], axis_up=(0, 1, 0), segs=32, sub=1, name='Abdomen')
loft(T, 'Secondary', [(Wp(0, 0.7, 0.05), 1.2 * k, 1.05 * k, 3.4), (Wp(0, 1.05, 0.05), 1.22 * k, 1.07 * k, 3.4)], axis_up=(0, 1, 0), segs=32, sub=1, name='Band')
for s in (-1, 1):
    ram(T, Wp(s * 0.8, 0.3, 0.8), Wp(s * 1.1, 1.5, 1.0), 0.11 * k)
loft(T, 'Secondary', [(Wp(0, 1.3, -0.1), 1.3 * k, 1.1 * k, 3.0), (Wp(0, 2.0, 0.0), 1.95 * k, 1.45 * k, 3.1), (Wp(0, 2.9, 0.05), 2.15 * k, 1.55 * k, 3.2),
                      (Wp(0, 3.55, -0.05), 1.75 * k, 1.3 * k, 3.3), (Wp(0, 3.85, -0.15), 1.2 * k, 0.95 * k, 3.4)], axis_up=(0, 1, 0), segs=40, name='Chest')
plate(T, 'Primary', Wp(0, 2.5, 1.55), (3.0 * k, 2.0 * k, 0.32), rot=(-10, 0, 0), curve=(0.12 / k, 0.05), bevel=0.08, taper=0.75)
block(T, 'Black', Wp(0, 2.2, 1.72), (2.0 * k, 0.05, 0.05), rot=(-10, 0, 0), bevel=0, name='Groove')
stripe(T, Wp(0, 3.15, 1.6), (1.6 * k, 0.05, 0.14), rot=(-10, 0, 0))
for s in (-1, 1):
    vent(T, Wp(s * 2.05, 2.3, -0.3), (1.6 * k, 0.25, 1.0 * k), rot=(0, 0, s * 90), slats=3)
# Backpack with a glowing reactor port
loft(T, 'Primary', [(Wp(0, 3.2, -1.2), 1.15 * k, 0.65 * k, 3.6), (Wp(0, 2.2, -1.95), 1.1 * k, 0.7 * k, 3.6), (Wp(0, 1.3, -1.4), 0.9 * k, 0.55 * k, 3.6)], axis_up=(1, 0, 0), segs=32, name='Pack')
lathe(T, 'Dark', Wp(0, 2.2, -2.55), (0, -1, 0), [(0, 0), (0.62 * k, 0), (0.62 * k, 0.12), (0, 0.12)], segs=28, name='PortRim')
lathe(T, 'Glow', Wp(0, 2.2, -2.55), (0, -1, 0), [(0, 0.06), (0.45 * k, 0.06), (0.45 * k, 0.16), (0, 0.16)], segs=28, name='Port')
# Head
hp = Wp(0, 4.25, 0.35)
hs = S['head']
if hs == 0:
    # Crew canopy: a bubble cockpit in an armoured collar
    loft(T, 'Primary', [(hp + Vector((0, -0.95, -0.3)) * 1, 1.05 * k, 0.55 * k, 3), (hp + Vector((0, 0.1, -0.25)), 1.15 * k, 0.6 * k, 3), (hp + Vector((0, 0.95, -0.4)), 0.9 * k, 0.45 * k, 3)],
         axis_up=(0, 0, 1), segs=32, name='Collar')
    loft(T, 'Glow', [(hp + Vector((0, -0.6, 0.15)), 0.7 * k, 0.45 * k, 2.2), (hp + Vector((0, 0.2, 0.25)), 0.85 * k, 0.6 * k, 2.2), (hp + Vector((0, 0.85, 0.0)), 0.55 * k, 0.35 * k, 2.2)],
         axis_up=(0, 0, 1), segs=32, name='Canopy')
    for j in range(3):
        lathe(T, 'Dark', hp + Vector((0, -0.4 + j * 0.45, 0.2)), (0, 1, 0), [(0.62 * k, -0.04), (0.9 * k, -0.04), (0.9 * k, 0.04), (0.62 * k, 0.04)], segs=28, name='Frame')
else:
    loft(T, 'Primary', [(hp + Vector((0, -0.75 * k, 0.05)), 0.75 * k, 0.55 * k, 3.0), (hp + Vector((0, 0.1, 0.1 * k)), 0.85 * k, 0.6 * k, 2.8),
                        (hp + Vector((0, 0.75 * k, 0.0)), 0.7 * k, 0.45 * k, 2.6), (hp + Vector((0, 1.0 * k, -0.08 * k)), 0.5 * k, 0.3 * k, 2.4)], axis_up=(0, 0, 1), segs=32, name='Head')
    loft(T, 'Secondary', [(hp + Vector((0, -0.5 * k, 0.55 * k)), 0.55 * k, 0.15 * k, 2.4), (hp + Vector((0, 0.5 * k, 0.5 * k)), 0.72 * k, 0.17 * k, 2.4),
                          (hp + Vector((0, 1.1 * k, 0.3 * k)), 0.55 * k, 0.1 * k, 2.4)], axis_up=(0, 0, 1), segs=28, name='Brow')
    if hs == 1:
        loft(T, 'Glow', [(hp + Vector((0, 0.3 * k, 0.05 * k)), 0.86 * k, 0.1 * k, 3), (hp + Vector((0, 0.8 * k, 0.0)), 0.74 * k, 0.08 * k, 3),
                         (hp + Vector((0, 1.02 * k, -0.04 * k)), 0.53 * k, 0.06 * k, 2.6)], axis_up=(0, 0, 1), segs=32, sub=2, cap=(False, True), name='Visor')
    else:
        e = hp + Vector((0, 0.95 * k, 0.0))
        lathe(T, 'Dark', e, (0, 1, 0), [(0, -0.1), (0.38 * k, -0.1), (0.4 * k, 0.12), (0.3 * k, 0.16), (0, 0.16)], segs=28, name='EyeRing')
        lathe(T, 'Glow', e, (0, 1, 0), [(0, 0.1), (0.26 * k, 0.1), (0.2 * k, 0.17), (0, 0.18)], segs=28, name='Eye')
R.rigid('Torso', W, 'Upper')

ARMS = Wp(0, 2.8, 0.1)
for s in (-1, 1):
    arm(R, s, Wp(s * 2.7, 2.8, 0.1), Wp(s * 2.8, 0.9, 0.25), Wp(s * 2.8, 0.75, 1.35), k=0.72 * k, shoulder_r=1.3)
R.rigid('Arms', ARMS, 'Arms')

for s, nm in ((-1, 'PauldronL'), (1, 'PauldronR')):
    c = Wp(s * 2.85, 3.5, 0)
    loft(nm, 'Primary', [(c + Vector((s * 0.05, -1.2 * k, -0.2 * k)), 0.95 * k, 0.45 * k, 2.2), (c + Vector((s * 0.1, -0.4 * k, 0.05)), 1.15 * k, 0.65 * k, 2.2),
                         (c + Vector((s * 0.1, 0.5 * k, 0.05)), 1.17 * k, 0.66 * k, 2.2), (c + Vector((s * 0.05, 1.25 * k, -0.25 * k)), 0.9 * k, 0.42 * k, 2.2)],
         axis_up=(0, 0, 1), segs=32, name='Pauldron')
    plate(nm, 'Secondary', c + Vector((s * 1.15 * k, 0, -0.75 * k)), (2.3 * k, 1.2 * k, 0.2), rot=(0, s * 12, s * 90), curve=(0.08, 0.08), taper=1.1)
    stripe(nm, c + Vector((s * 1.3 * k, 0, -0.4 * k)), (0.05, 1.8 * k, 0.13), rot=(0, s * 12, 0))
    R.rigid(nm, c, 'Upper')

mast = Wp(0.7, 4.0, -0.6)
lathe('Antenna', 'Dark', mast, (0, 0, 1), [(0, 0), (0.16, 0), (0.16, 0.3), (0.06, 0.4), (0.045, 1.9 * k), (0.0, 2.0 * k)], segs=12, name='Mast')
lathe('Antenna', 'Glow', mast + Vector((0, 0, 1.9 * k)), (0, 0, 1), [(0, 0), (0.06, 0), (0, 0.1)], segs=10, name='Tip')
R.rigid('Antenna', mast, 'Upper')

R.anchor('BackL', Wp(-1.45, 3.85, -1.8), 'Upper')
R.anchor('BackR', Wp(1.45, 3.85, -1.8), 'Upper')
R.anchor('Shoulder', Wp(-2.8, 4.25, -0.2), 'Arms')
R.anchor('Smoke', Wp(0, 2.4, -1.2), 'Upper')

# Detach order matches the old builders: fenders/plates alternate so damage shows all round.
f = detach_fenders
order = {'Raptor': [ 'PauldronL', f[0], 'Antenna', f[3], 'PauldronR', f[1], f[2]],
         'Hornet': [f[0], 'PauldronL', f[3], 'Antenna', f[1], 'PauldronR', f[2]],
         'Juggernaut': [f[0], 'PauldronL', f[2], 'Antenna', f[1], 'PauldronR', f[3]],
         'Bastion': [f[0], 'PauldronL', 'Shield', f[3], 'Antenna', 'PauldronR', f[1], f[2]]}[NAME]
R.extra.update({'upper': list(U(W)), 'arms': list(U(ARMS)), 'detach': order, 'wheels': len(wheels), 'wheelRadius': S['r']})
run(R, os.path.dirname(os.path.abspath(__file__)), STAND + 1.5, 30)
