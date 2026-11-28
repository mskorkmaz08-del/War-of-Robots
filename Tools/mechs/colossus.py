"""
COLOSSUS - super-heavy siege walker (about 68 t), the biggest machine on the field.

Built like a walking bunker: short, massively thick legs set wide apart, a hunched
armoured chest that leans out over them with the crew canopy buried in the glacis,
enormous stepped shoulder armour, and long heavy arms carrying the main guns low and
forward. On its back sits a reactor block with two open turbine ducts whose fans keep
spinning, flanked by radiator banks and exhaust stacks. Every hip, knee, ankle and
shoulder is driven by working cylinders; gear trains at the knees, shoulders and the
slewing ring; a rotating sensor head on the left shoulder deck.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mechlib import reset, lathe, tube
from mathutils import Vector, Matrix
from hardsurf import *
from parts import *

reset()
STAND = 8.0
HIP_X, HIP_Z = 3.1, 7.55
THIGH, SHIN = 3.75, 3.85
ANKLE_Z = 1.4
FOOT_OUT = 0.9
UPPER = Vector((0, 0, 9.5))
ARMS = Vector((0, 0.3, 14.0))
MOUNT = lambda s: Vector((s * 4.95, 2.6, 10.7))
R = Rig('Colossus', STAND)

# ---------------------------------------------------------------- pelvis
ring_top = pelvis(R, HIP_X, HIP_Z, k=1.15, ring_r=2.2)
# extra skirt armour hanging off the pelvis front and back
for yy, nm in ((1.75, 'SkirtF'), (-1.75, 'SkirtB')):
    f = 1 if yy > 0 else -1
    armour('Hull', 'Secondary', [(x, yy + f * dy, z) for x in (-1.6, 1.6) for (dy, z) in ((0.0, 8.5), (0.25, 6.6), (0.18, 8.5), (0.42, 6.6))],
           bevel=0.05, seam_min=0.8, name=nm)
    hazard('Hull', Mx((0, yy + f * 0.45, 6.9), (0, 0, 0 if f > 0 else 180)), 2.6, 0.26, n=7)

# ---------------------------------------------------------------- torso
T = 'Torso'
lathe(T, 'Metal', (0, 0, ring_top), (0, 0, 1), [(0, 0), (2.15, 0), (2.2, 0.06), (2.2, 0.3), (2.05, 0.36), (0, 0.36)], segs=48, name='RingTop')
armour(T, 'Dark', ngon(ring_top + 0.3, 2.0, 10, 18) + ngon(ring_top + 1.3, 2.3, 10, 18), bevel=0.05, seams=False, groove=None, name='Waist')
for kk in range(10):
    a = math.radians(18 + kk * 36)
    armour(T, 'Secondary', boxpts((0.5, 0.12, 0.8), ch=0.04), Mx((math.cos(a) * 2.25, math.sin(a) * 2.25, ring_top + 0.8), (0, 0, math.degrees(a) - 90)),
           bevel=0.02, seams=False, groove=None, name='WaistRib')
# Chest core: broad and deep, hunched forward at the top
chest = sec(10.4, 2.4, 1.8, 0.6) + sec(11.8, 3.2, 2.25, 0.8, 0, 0.3) + sec(13.7, 3.55, 2.4, 0.9, 0, 0.55) + \
        sec(15.0, 3.2, 2.1, 0.8, 0, 0.6) + sec(15.6, 2.4, 1.6, 0.6, 0, 0.4)
armour(T, 'Dark', chest, bevel=0.09, seams=False, groove=None, name='ChestCore')
# Glacis: two layers of thick sloped plate with the canopy band between them
armour(T, 'Primary', sec(10.3, 2.3, 0.3, 0.12, 0, 1.95) + sec(12.6, 2.9, 0.3, 0.15, 0, 2.75) + [(0, 3.15, 11.6)], bevel=0.07, seam_min=1.0, name='LowerGlacis')
# Reactor core showing through the lower glacis: a ring housing, a hot core, vanes
RC = Vector((0, 2.95, 11.45))
lathe(T, 'Dark', RC, (0, 1, 0.25), [(0, -0.4), (1.05, -0.4), (1.15, -0.2), (1.15, 0.25), (1.0, 0.32), (0.8, 0.32), (0.78, 0.12), (0, 0.12)], segs=36, name='CoreHousing')
lathe(T, 'Hot', RC, (0, 1, 0.25), [(0, 0.12), (0.78, 0.12), (0.6, 0.2), (0, 0.24)], segs=36, name='Core')
lathe(T, 'Accent', RC, (0, 1, 0.25), [(1.0, 0.3), (1.08, 0.3), (1.08, 0.34), (1.0, 0.34)], segs=36, name='CoreRing')
cax = Vector((0, 1, 0.25)).normalized(); ca = Vector((1, 0, 0)); cb = cax.cross(ca)
for i in range(8):
    t = 2 * math.pi * i / 8
    d = ca * math.cos(t) + cb * math.sin(t)
    tn = cax.cross(d)
    armour(T, 'Metal', [tuple(RC + cax * h + d * rr + tn * w) for h in (0.22, 0.32) for rr in (0.15, 0.85) for w in (-0.05, 0.05)], bevel=0.0, seams=False, groove=None, name='CoreVane')
armour(T, 'Black', boxpts((3.4, 0.5, 0.7), ch=0.15), Mx((0, 2.75, 13.25), (-22, 0, 0)), bevel=0.03, seams=False, groove=None, name='CanopyFrame')
for i, x in enumerate((-1.1, 0.0, 1.1)):
    armour(T, 'Glow', boxpts((0.95, 0.1, 0.42)), Mx((x, 2.98, 13.27), (-22, 0, 0)), bevel=0.0, seams=False, groove=None, name='CanopyPane')
armour(T, 'Secondary', sec(13.65, 3.0, 0.3, 0.15, 0, 2.6) + sec(15.2, 2.5, 0.3, 0.12, 0, 2.15) + [(0, 3.05, 14.3)], bevel=0.06, seam_min=0.9, name='UpperGlacis')
# Brow guard over the canopy
armour(T, 'Primary', [(x, y, z) for x in (-1.9, 1.9) for (y, z) in ((3.2, 13.75), (2.6, 14.05), (3.05, 13.55), (2.5, 13.85))], bevel=0.04, seam_min=10, name='Brow')
for s in (-1, 1):
    lamp(T, Mx((s * 2.2, 3.05, 12.55), (-20, 0, s * 10)), 0.34, 0.16)
    lamp(T, Mx((s * 1.65, 3.15, 11.9), (-20, 0, s * 8)), 0.22, 0.12, 'Hot')
    # chest side armour, three overlapping plates
    for i, z in enumerate((11.4, 12.6, 13.8)):
        side_plate(T, 'Secondary' if i == 1 else 'Primary', [(-1.9, 0.55), (1.9, 0.55), (1.7, -0.55), (-1.7, -0.55)], 0.2, (s * (3.05 + i * 0.12), 0.2, z), s, 7,
                   stripe_at=0.2 if i == 0 else None, stripe_h=0.14, bevel=0.04, seam_min=0.7, name='ChestSide')
    # shoulder sockets with drive gears
    armour(T, 'Dark', sec(-1.1, 0.6, 1.35, 0.4) + sec(1.1, 0.6, 1.35, 0.4), Mx((s * 3.55, 0.3, 14.0)), bevel=0.05, seams=False, groove=None, name='ShoulderSocket')
    gear(T, (s * 3.75, 0.3, 14.0), (1, 0, 0), 0.7, 0.16)
    gear(T, (s * 3.75, -0.75, 13.35), (1, 0, 0), 0.38, 0.14)
    armour(T, 'Dark', boxpts((0.6, 0.8, 0.8), ch=0.15), Mx((s * 3.45, -1.55, 11.7)), bevel=0.04, seams=False, groove=None, name='ShLug')
    hose(T, [(s * 2.4, 2.0, 10.6), (s * 3.0, 1.6, 10.4), (s * 3.3, 0.6, 10.9)], 0.12)
# Belly: stepped plates and a hazard band
armour(T, 'Primary', sec(9.95, 1.7, 0.14, 0.08, 0, 1.85) + sec(10.45, 2.0, 0.14, 0.08, 0, 2.05), bevel=0.04, seam_min=10, name='Belly')
hazard(T, Mx((0, 2.2, 10.2), (-10, 0, 0)), 3.0, 0.28, n=8)

# Reactor block on the back with two open turbine ducts
armour(T, 'Primary', sec(10.6, 2.6, 1.1, 0.35, 0, -3.3) + sec(15.0, 2.8, 1.2, 0.4, 0, -3.4) + sec(15.4, 2.5, 1.05, 0.35, 0, -3.3),
       bevel=0.07, seam_min=1.0, name='Reactor')
grille(T, Mx((0, -4.62, 12.0), (0, 0, 180)), 3.4, 1.6, slats=7, depth=0.14)
hazard(T, Mx((0, -4.66, 14.6), (0, 0, 180)), 3.6, 0.32, n=9)
for s in (-1, 1):
    dc = Vector((s * 1.35, -3.35, 15.4))
    lathe(T, 'Dark', dc, (0, 0, 1), [(0.95, 0), (1.15, 0), (1.15, 0.9), (1.25, 0.95), (1.25, 1.15), (0.95, 1.15)], segs=32, name='Duct')
    lathe(T, 'Black', dc, (0, 0, 1), [(0, 0.0), (0.96, 0.0), (0.96, 0.05), (0, 0.05)], segs=32, name='DuctFloor')
    lathe(T, 'Metal', dc + Vector((0, 0, 0.05)), (0, 0, 1), [(0, 0), (0.3, 0), (0.3, 0.3), (0.2, 0.45), (0, 0.45)], segs=16, name='FanMotor')
    for kk in range(4):
        a = math.radians(45 + kk * 90)
        armour(T, 'Dark', boxpts((0.9, 0.08, 0.12)), Mx(dc + Vector((math.cos(a) * 0.5, math.sin(a) * 0.5, 1.05)), (0, 0, math.degrees(a))), bevel=0, seams=False, groove=None, name='Strut')
    # Spinning turbine fan inside the duct
    F_ = 'SpinFan' + ('L' if s < 0 else 'R')
    fc = dc + Vector((0, 0, 0.62))
    lathe(F_, 'Metal', fc, (0, 0, 1), [(0, -0.12), (0.26, -0.12), (0.26, 0.12), (0.12, 0.2), (0, 0.2)], segs=16, name='FanHub')
    for kk in range(9):
        a = 2 * math.pi * kk / 9
        d = Vector((math.cos(a), math.sin(a), 0))
        tng = Vector((-d.y, d.x, 0))
        blade = []
        for r_, w_ in ((0.24, 0.17), (0.9, 0.22)):
            for sg in (-1, 1):
                for h in (-0.03, 0.03):
                    blade.append(tuple(fc + d * r_ + tng * sg * w_ + Vector((0, 0, sg * 0.08 + h))))
        armour(F_, 'Metal', blade, bevel=0.0, seams=False, groove=None, name='Blade')
    R.spin(F_, fc, 'Upper', (0, 0, 1), 520.0 * s)
    # exhaust stacks and radiator fins down the reactor sides
    lathe(T, 'Dark', (s * 2.45, -3.0, 14.6), (0, 0, 1), [(0, 0), (0.36, 0), (0.36, 0.2), (0.3, 0.26), (0.3, 1.9), (0.4, 1.95), (0.4, 2.15), (0.26, 2.15), (0.26, 2.0), (0, 2.0)], segs=8, name='Stack')
    lathe(T, 'Hot', (s * 2.45, -3.0, 16.6), (0, 0, 1), [(0, 0), (0.26, 0), (0, 0.01)], segs=8, name='StackGlow')
    for kk in range(7):
        armour(T, 'Metal', boxpts((0.1, 1.6, 0.42)), Mx((s * 2.75, -3.3, 11.0 + kk * 0.5)), bevel=0, seams=False, groove=None, name='Radiator')
    for kk in range(4):
        tube(T, 'Black', [(s * 0.4 * kk, -4.45, 15.3 - kk * 0.2), (s * (0.6 + 0.4 * kk), -4.7, 13.0), (s * (0.5 + 0.4 * kk), -4.5, 11.0)], 0.06, segs=6)
    n = jet(T, (s * 1.7, -3.4, 10.2), (0, -0.3, -1), 1.05)
    R.anchor('Jet' + ('L' if s < 0 else 'R'), n, 'Upper', (0, -0.3, -1))
# Roof: hatch, grab rails, deck plating
armour(T, 'Secondary', sec(15.55, 2.3, 1.5, 0.5, 0, 0.35) + sec(15.95, 1.9, 1.2, 0.4, 0, 0.3), bevel=0.05, seam_min=0.9, name='Deck')
armour(T, 'Dark', boxpts((1.1, 1.1, 0.16), ch=0.15), Mx((0.7, 0.1, 16.02)), bevel=0.03, seams=False, groove=None, name='Hatch')
for s in (-1, 1):
    tube(T, 'Metal', [(s * 1.75, -1.3, 15.9), (s * 1.75, -1.3, 16.35), (s * 1.75, 1.2, 16.35), (s * 1.75, 1.2, 15.9)], 0.05, segs=6)
R.rigid('Torso', UPPER, 'Upper')

# Rotating sensor head on the deck (slow scan)
SH = Vector((-1.0, 0.6, 16.0))
lathe('SpinSensor', 'Dark', SH, (0, 0, 1), [(0, 0), (0.55, 0), (0.55, 0.15), (0.42, 0.25), (0.42, 0.45), (0, 0.45)], segs=20, name='SensorBase')
armour('SpinSensor', 'Secondary', boxpts((1.2, 0.9, 0.6), ch=0.2, taper=(0.8, 0.8)), Mx(SH + Vector((0, 0.1, 0.75))), bevel=0.04, seam_min=0.5, name='SensorHead')
for x in (-0.3, 0.0, 0.3):
    lathe('SpinSensor', 'Glow', SH + Vector((x, 0.58, 0.78)), (0, 1, 0), [(0, 0), (0.11, 0), (0.08, 0.05), (0, 0.06)], segs=12, name='Lens')
R.spin('SpinSensor', SH, 'Upper', (0, 0, 1), 35.0)

# Detachable glacis armour (the outer layer of the chest)
for s, nm in ((-1, 'ChestL'), (1, 'ChestR')):
    c = Vector((s * 1.6, 2.95, 11.35))
    M = Mx(c, (-18, 0, -s * 18))
    armour(nm, 'Primary', [(x, y, z) for x, z in ((-1.45, -1.0), (1.45, -1.0), (1.5, 0.75), (0.7, 1.15), (-1.5, 0.8)) for y in (-0.24, 0.24)],
           M, bevel=0.06, seam_min=0.8, name='Glacis')
    stripe(nm, M @ Mx((s * 0.65, 0.38, -0.6)), 1.1, 0.16)
    bolts_line(nm, M @ Vector((-1.25, 0.27, 0.75)), M @ Vector((-1.25, 0.27, -0.8)), 5, M.to_3x3() @ Vector((0, 1, 0)), 0.07)
    bolts_line(nm, M @ Vector((1.25, 0.27, 0.75)), M @ Vector((1.25, 0.27, -0.8)), 5, M.to_3x3() @ Vector((0, 1, 0)), 0.07)
    R.rigid(nm, c, 'Upper')

# ---------------------------------------------------------------- arms
for s in (-1, 1):
    arm(R, s, (s * 4.6, 0.3, 14.0), (s * 4.8, 0.7, 11.4), MOUNT(s), k=1.25, gauntlet=(1.2, 1.05, 1.15))
    armour('Arms', 'Dark', boxpts((0.6, 0.8, 0.8), ch=0.15), Mx((s * 4.0, -1.75, 13.3)), bevel=0.04, seams=False, groove=None, name='ArmLug')
    # elbow guard: a big faceted cop over the elbow drum
    armour('Arms', 'Primary', sec(-0.6, 0.7, 0.9, 0.3, 0, -0.2) + sec(0.5, 0.55, 0.75, 0.25, 0, -0.35) + [(0, -1.35, -0.1)],
           Mx((s * 4.8, 0.7, 11.1)), bevel=0.05, seam_min=0.5, name='ElbowGuard')
R.rigid('Arms', ARMS, 'Arms')
shoulder_rams(R, [((s * 3.45, -1.55, 11.7), (s * 4.0, -1.75, 13.3)) for s in (-1, 1)], k=1.15)

# Detachable pauldrons: three stepped layers of plate over each shoulder
for s, nm in ((-1, 'PauldronL'), (1, 'PauldronR')):
    c = Vector((s * 4.95, 0.2, 15.3))
    M = Mx(c)
    armour(nm, 'Primary', sec(-1.6, 1.25, 1.75, 0.5, s * 0.25, 0.1) + sec(-0.1, 1.6, 2.2, 0.65, s * 0.15, 0) + sec(0.85, 1.4, 2.0, 0.6, 0, 0)
           + sec(1.25, 0.9, 1.45, 0.4, -s * 0.2, -0.05), M, bevel=0.09, seam_min=1.0, seam_w=0.22, name='Pauldron')
    for i in range(3):
        side_plate(nm, 'Secondary' if i % 2 else 'Primary', [(-2.0 + i * 0.1, 0.45), (2.0 - i * 0.1, 0.45), (1.8 - i * 0.1, -0.5), (-1.8 + i * 0.1, -0.5)], 0.2,
                   (s * (1.78 + i * 0.1), 0, -0.25 - i * 0.75), s, 5 + i * 3, M=M, stripe_at=0.0 if i == 0 else None, stripe_h=0.18,
                   bevel=0.05, seam_min=0.6, name='Layer')
    lamp(nm, M @ Mx((s * 0.6, 2.2, 0.15)), 0.4, 0.15)
    grille(nm, M @ Mx((s * 0.25, -2.2, -0.3), (0, 0, 180)), 1.6, 0.9, slats=4, depth=0.1)
    bolts_line(nm, M @ Vector((0, -1.7, 1.27)), M @ Vector((0, 1.6, 1.27)), 9, (0, 0, 1), 0.07)
    R.rigid(nm, c, 'Upper')

# Antenna mast (detachable)
mast = Vector((1.7, -2.0, 15.4))
armour('Antenna', 'Dark', boxpts((0.4, 0.4, 0.5), ch=0.08), Mx(mast + Vector((0, 0, 0.25))), bevel=0.03, seams=False, groove=None, name='MastBase')
lathe('Antenna', 'Dark', mast, (0, 0, 1), [(0.07, 0.5), (0.05, 3.2), (0.0, 3.3)], segs=8, name='Mast')
for z in (1.4, 2.2):
    armour('Antenna', 'Dark', boxpts((0.7, 0.06, 0.06)), Mx(mast + Vector((0, 0, z))), bevel=0, seams=False, groove=None, name='Spar')
lathe('Antenna', 'Glow', mast + Vector((0, 0, 3.2)), (0, 0, 1), [(0, 0), (0.08, 0), (0, 0.12)], segs=10, name='Tip')
R.rigid('Antenna', mast, 'Upper')

R.anchor('BackL', (-4.6, -0.8, 16.7), 'Upper')
R.anchor('BackR', (4.6, -0.8, 16.7), 'Upper')
R.anchor('Shoulder', (4.6, 0.1, 16.6), 'Arms')
R.anchor('Smoke', (0.6, -2.8, 15.4), 'Upper')

# ---------------------------------------------------------------- legs
for s in (-1, 1):
    hip = (s * HIP_X, 0, HIP_Z)
    knee, Mt, Ms = walker_leg(R, s, hip, (s * (HIP_X + FOOT_OUT), 0, ANKLE_Z), THIGH, SHIN, ANKLE_Z, k=1.35,
                              st=dict(shin='layered', toes=3, foot_len=1.25, foot_w=1.2))
    hip_ram(R, s, hip, Mt, THIGH, k=1.2)

R.extra.update({'thigh': THIGH, 'shin': SHIN, 'ankle': ANKLE_Z, 'footOut': FOOT_OUT, 'upper': list(U(UPPER)), 'arms': list(U(ARMS)),
                'detach': ['PauldronL', 'ChestL', 'Antenna', 'PauldronR', 'ChestR']})
run(R, os.path.dirname(os.path.abspath(__file__)), 8.8, 50)
