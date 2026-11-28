"""
SCARAB - six-legged armoured artillery crawler (about 46 t).

A low, wide armoured crawler: a stepped hull and engine deck over a dark frame, carried on six geared legs that always keep three feet planted,
so it is the steadiest firing platform on the field. A driver's vision band and push
bar at the front, a squat two-gun turret on the back of the thorax,
ammunition drums along the flanks, and a radar that keeps
sweeping. Every leg has a working knee cylinder and a hip cylinder from the body.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mechlib import reset, lathe, tube
from mathutils import Vector, Matrix
from hardsurf import *
from parts import *

reset()
STAND = 5.6
THIGH, SHIN, ANKLE_Z = 4.3, 5.0, 0.85
UPPER = Vector((0, 0.4, 7.35))
ARMS = Vector((0, 0.9, 8.25))
R = Rig('Scarab', STAND)

B = 'Hull'
# ---------------------------------------------------------------- frame and carapace
# Keel frame: a long dark box girder that every hip hangs from
armour(B, 'Dark', sec(4.7, 1.6, 4.6, 0.6) + sec(6.6, 2.0, 4.9, 0.7), bevel=0.06, seams=False, groove=None, name='Keel')
# Thorax shell (middle): broad faceted dome
armour(B, 'Secondary', sec(6.3, 2.75, 2.2, 0.9, 0, 0.3) + sec(7.1, 2.45, 2.0, 0.85, 0, 0.3) + sec(7.4, 1.7, 1.5, 0.6, 0, 0.3),
       bevel=0.07, seam_min=1.0, seam_w=0.2, name='Thorax')
# Head section: a heavy wedge with a sensor face
armour(B, 'Primary', [(x * w, y, z) for (y, w, zb, zt) in ((5.6, 1.1, 5.2, 6.3), (4.6, 2.0, 4.9, 6.9), (2.7, 2.3, 5.0, 7.0)) for x in (-1, 1) for z in (zb, zt)],
       bevel=0.07, seam_min=0.9, name='Head')
# Driver's vision band across the front: armoured glass, a glowing slit and two periscope blocks
armour(B, 'Black', [(x * w, y, z) for (y, w, z) in ((5.7, 1.05, 6.0), (5.7, 1.05, 6.3), (4.9, 1.75, 6.55), (4.9, 1.75, 6.85)) for x in (-1, 1)],
       bevel=0.02, seams=False, groove=None, name='VisionBand')
armour(B, 'Glow', boxpts((1.9, 0.06, 0.1)), Mx((0, 5.69, 6.15), (-32, 0, 0)), bevel=0, seams=False, groove=None, name='VisionSlit')
for s in (-1, 1):
    armour(B, 'Dark', boxpts((0.5, 0.45, 0.35), ch=0.08), Mx((s * 0.7, 4.5, 7.15)), bevel=0.03, seams=False, groove=None, name='Periscope')
    armour(B, 'Glow', boxpts((0.36, 0.05, 0.12)), Mx((s * 0.7, 4.74, 7.17)), bevel=0, seams=False, groove=None, name='PeriscopeGlass')
# Front bumper: a heavy flat push bar with tow eyes, and corner guards
armour(B, 'Secondary', [(x, y, z) for x in (-2.1, 2.1) for (y, z) in ((5.9, 4.75), (6.15, 5.15), (5.5, 4.7), (5.6, 5.25))], bevel=0.04, seam_min=0.6, name='Bumper')
hazard(B, Mx((0, 6.16, 4.95)), 3.6, 0.22, n=9)
for s in (-1, 1):
    armour(B, 'Metal', boxpts((0.22, 0.3, 0.22), ch=0.05), Mx((s * 1.2, 6.25, 4.95)), bevel=0.02, seams=False, groove=None, name='TowEye')
    armour(B, 'Secondary', [(s * x, y, z) for (x, y, z) in ((2.05, 4.3, 5.0), (2.3, 3.4, 5.1), (2.1, 4.25, 6.3), (2.35, 3.3, 6.4), (1.8, 4.6, 5.1), (1.8, 4.55, 6.3))],
           bevel=0.04, seam_min=0.6, name='CornerGuard')
    lamp(B, Mx((s * 1.7, 4.95, 6.6), (-15, 0, s * 25)), 0.3, 0.12)
    armour(B, 'Dark', boxpts((0.7, 0.5, 0.3), ch=0.1), Mx((s * 1.6, 4.0, 7.05)), bevel=0.03, seams=False, groove=None, name='HeadVent')
# Rear deck: a long armoured engine deck in two stepped boxes, bolted hatches and a top grille
armour(B, 'Primary', [(x, y, z) for x in (-2.55, 2.55) for (y, z) in ((-1.4, 7.25), (-3.7, 7.05))] + [(x, y, 5.5) for x in (-2.4, 2.4) for y in (-1.4, -3.7)], bevel=0.06, seam_min=0.9, name='DeckFront')
armour(B, 'Secondary', [(x, y, z) for x in (-2.35, 2.35) for (y, z) in ((-3.6, 6.75), (-5.8, 6.45))] + [(x, y, 5.3) for x in (-2.2, 2.2) for y in (-3.6, -5.8)], bevel=0.06, seam_min=0.9, name='DeckRear')
grille(B, Mx((0, -2.55, 7.2), (90, 0, 0)), 2.6, 1.6, slats=6, depth=0.08)
for s in (-1, 1):
    armour(B, 'Dark', boxpts((0.9, 0.9, 0.1), ch=0.12), Mx((s * 1.2, -4.7, 6.62)), bevel=0.03, seams=False, groove=None, name='Hatch')
    bolts_line(B, (s * 2.2, -1.6, 7.24), (s * 2.2, -3.5, 7.07), 5, (0, 0, 1), 0.05)
hazard(B, Mx((0, -6.15, 5.8), (0, 0, 180)), 2.6, 0.3, n=7)
# Tail block: engine and exhaust
armour(B, 'Dark', boxpts((2.6, 1.0, 1.5), ch=0.3), Mx((0, -6.2, 5.3)), bevel=0.05, seams=False, groove=None, name='Tail')
grille(B, Mx((0, -6.72, 5.3), (0, 0, 180)), 2.0, 1.0, slats=5, depth=0.1)
for s in (-1, 1):
    lathe(B, 'Dark', (s * 0.85, -6.4, 6.0), (0, -0.4, 1), [(0, 0), (0.25, 0), (0.25, 0.95), (0.32, 1.0), (0.32, 1.15), (0, 1.15)], segs=10, name='Exhaust')
    n = jet(B, (s * 1.5, -4.2, 4.4), (0, 0, -1), 0.7)
    R.anchor('Jet' + ('L' if s < 0 else 'R'), n, 'Root', (0, 0, -1))
# Ammunition drums slung along the flanks, fed to the turret
for s in (-1, 1):
    for i, y in enumerate((-1.3, -3.1)):
        lathe(B, 'Dark', (s * 2.45, y, 5.75), (0, 1, 0), [(0, -0.8), (0.55, -0.8), (0.62, -0.7), (0.62, 0.7), (0.55, 0.8), (0, 0.8)], segs=16, name='Drum')
        for yy in (-0.45, 0.0, 0.45):
            lathe(B, 'Metal', (s * 2.45, y + yy, 5.75), (0, 1, 0), [(0.63, -0.05), (0.68, -0.05), (0.68, 0.05), (0.63, 0.05)], segs=16, name='DrumBand')
    tube(B, 'Dark', [(s * 2.3, -0.6, 6.1), (s * 1.9, 0.1, 6.9), (s * 1.3, 0.4, 7.3)], 0.12, segs=8)
    hose(B, [(s * 2.0, 3.6, 6.8), (s * 2.5, 1.8, 6.6), (s * 2.6, -0.2, 6.4), (s * 2.2, -4.0, 6.2)], 0.09)
# Hip housings: six cast bosses on the keel
HIPS = []
for sy, yy in ((1, 2.6), (0, 0.0), (-1, -2.6)):
    for sx in (-1, 1):
        hp = Vector((sx * 2.2, yy, 5.55))
        HIPS.append((sx, sy, hp))
        armour(B, 'Secondary', sec(-0.85, 0.5, 0.75, 0.3) + sec(0.95, 0.5, 0.85, 0.35), Mx(hp - Vector((sx * 0.6, 0, 0))), bevel=0.05, seam_min=0.6, name='HipBoss')
        armour(B, 'Dark', boxpts((0.5, 0.55, 0.55), ch=0.1), Mx(hp + Vector((-sx * 0.55, 0, 1.4))), bevel=0.03, seams=False, groove=None, name='HipLug')
# Slewing ring for the turret
lathe(B, 'Dark', (0, UPPER.y, 7.0), (0, 0, 1), [(0, 0), (1.55, 0), (1.6, 0.08), (1.6, 0.35), (0, 0.35)], segs=44, name='RingBase')
gear(B, (0, UPPER.y, 7.45), (0, 0, 1), 1.3, 0.12, teeth=40)
R.rigid('Hull', (0, 0, STAND), 'Root')

# Sweeping radar on the abdomen
RAD = Vector((0, -5.3, 6.25))
lathe('SpinRadar', 'Dark', RAD, (0, 0, 1), [(0, 0), (0.4, 0), (0.4, 0.12), (0.22, 0.2), (0.22, 0.75), (0, 0.75)], segs=16, name='RadarPost')
dish_c = RAD + Vector((0, 0.15, 1.1))
lathe('SpinRadar', 'Secondary', dish_c, (0, 1, 0), [(0, -0.05), (0.9, 0.25), (0.92, 0.32), (0, 0.06)], segs=24, name='Dish')
lathe('SpinRadar', 'Metal', dish_c, (0, 1, 0), [(0, 0), (0.08, 0), (0.05, 0.6), (0, 0.62)], segs=8, name='Feed')
R.spin('SpinRadar', RAD, 'Root', (0, 0, 1), 60.0)

# ---------------------------------------------------------------- turret
T = 'Torso'
c0 = UPPER
lathe(T, 'Metal', c0, (0, 0, 1), [(0, 0), (1.5, 0), (1.55, 0.06), (1.55, 0.28), (0, 0.28)], segs=40, name='RingTop')
armour(T, 'Secondary', sec(0.25, 1.55, 1.7, 0.6, 0, 0.1) + sec(1.1, 1.65, 1.8, 0.65, 0, 0.2) + sec(1.6, 1.2, 1.3, 0.45, 0, 0.0) + [(0, 2.1, 0.8)],
       Mx(c0), bevel=0.06, seam_min=0.8, name='Turret')
armour(T, 'Primary', sec(1.55, 1.1, 1.2, 0.4, 0, 0.0) + sec(1.9, 0.85, 0.95, 0.3, 0, -0.15), Mx(c0), bevel=0.05, seam_min=0.6, name='TurretRoof')
armour(T, 'Black', boxpts((1.6, 0.25, 0.25), ch=0.08), Mx(c0 + Vector((0, 1.85, 1.15)), (-25, 0, 0)), bevel=0.02, seams=False, groove=None, name='SlitFrame')
armour(T, 'Glow', boxpts((1.35, 0.08, 0.1)), Mx(c0 + Vector((0, 1.95, 1.16)), (-25, 0, 0)), bevel=0, seams=False, groove=None, name='Slit')
for s in (-1, 1):
    armour(T, 'Dark', boxpts((0.5, 1.0, 0.9), ch=0.2), Mx(c0 + Vector((s * 1.6, 0.5, 0.9))), bevel=0.04, seams=False, groove=None, name='Trunnion')
    armour(T, 'Dark', boxpts((0.4, 0.5, 0.5), ch=0.1), Mx(c0 + Vector((s * 1.35, -0.9, 0.45))), bevel=0.03, seams=False, groove=None, name='ShLug')
    grille(T, Mx(c0 + Vector((s * 1.62, -0.7, 1.0)), (0, 0, -s * 90)), 0.9, 0.5, slats=3, depth=0.08)
armour(T, 'Dark', boxpts((0.8, 0.8, 0.14), ch=0.14), Mx(c0 + Vector((0.4, -0.5, 1.95))), bevel=0.03, seams=False, groove=None, name='Hatch')
R.rigid('Torso', UPPER, 'Upper')

A = 'Arms'
for s in (-1, 1):
    tr = Vector((s * 1.95, ARMS.y, ARMS.z))
    drum(A, tr, (1, 0, 0), 0.6, 0.55, bolts=8)
    gear(A, tr + Vector((-s * 0.4, 0, 0)), (1, 0, 0), 0.5, 0.12)
    m = Vector((s * 2.35, 2.6, 8.2))
    loft_y(A, 'Secondary', [(ARMS.y - 0.6, 0.55, 0.55, 0.18, s * 2.3, ARMS.z), (m.y - 0.4, 0.5, 0.5, 0.16, m.x, m.z)], bevel=0.04, seam_min=0.5, name='GunArm')
    armour(A, 'Primary', [(m.x + x, y, z) for x in (-0.5, 0.5) for (y, z) in ((ARMS.y - 0.3, ARMS.z + 0.6), (m.y - 0.5, m.z + 0.55))] + [(m.x, ARMS.y + 0.4, ARMS.z + 0.85)],
           bevel=0.04, seam_min=0.5, name='GunArmTop')
    lathe(A, 'Dark', m + Vector((0, -0.3, 0)), (0, 1, 0), [(0.4, 0), (0.66, 0), (0.66, 0.32), (0.4, 0.32)], segs=24, name='Cradle')
    hose(A, [(s * 1.7, ARMS.y - 0.6, ARMS.z - 0.5), (s * 2.2, ARMS.y + 0.4, m.z - 0.55), (m.x, m.y - 0.6, m.z - 0.5)], 0.1, mat='Dark')
    armour(A, 'Dark', boxpts((0.4, 0.5, 0.5), ch=0.1), Mx((s * 2.0, ARMS.y - 1.0, ARMS.z - 0.6)), bevel=0.03, seams=False, groove=None, name='ArmLug')
    R.anchor('Mount' + ('L' if s < 0 else 'R'), m, 'Arms')
R.rigid('Arms', ARMS, 'Arms')
shoulder_rams(R, [(c0 + Vector((s * 1.35, -0.9, 0.45)), (s * 2.0, ARMS.y - 1.0, ARMS.z - 0.6)) for s in (-1, 1)], k=0.55)

# ---------------------------------------------------------------- detachable armour
for s, nm in ((-1, 'ChestL'), (1, 'ChestR')):
    cc = Vector((s * 1.55, 0.9, 7.15))
    armour(nm, 'Primary', [(s * x, y, z) for x, y, z in ((0.0, -1.3, 0.0), (0.0, 1.3, 0.0), (1.15, -1.2, -0.3), (1.15, 1.2, -0.3),
                                                       (0.0, -1.2, 0.28), (0.0, 1.2, 0.28), (0.95, -1.05, 0.0), (0.95, 1.05, 0.0))], Mx(cc), bevel=0.04, seam_min=0.4, name='Plate')
    stripe(nm, Mx(cc + Vector((s * 0.45, 0, 0.25)), (90, 0, 0)), 0.45, 1.6)
    R.rigid(nm, cc, 'Root')
mast = Vector((-1.3, -4.6, 6.6))
armour('Antenna', 'Dark', boxpts((0.35, 0.35, 0.45), ch=0.08), Mx(mast + Vector((0, 0, 0.22))), bevel=0.03, seams=False, groove=None, name='MastBase')
lathe('Antenna', 'Dark', mast, (0, 0, 1), [(0.07, 0.45), (0.05, 2.6), (0.0, 2.7)], segs=8, name='Mast')
lathe('Antenna', 'Glow', mast + Vector((0, 0, 2.6)), (0, 0, 1), [(0, 0), (0.07, 0), (0, 0.12)], segs=10, name='Tip')
R.rigid('Antenna', mast, 'Root')

R.anchor('Back', (0, -3.15, 7.2), 'Root')
R.anchor('Shoulder', (0, 0.6, 9.55), 'Arms')
R.anchor('Smoke', (0, -4.4, 6.9), 'Root')

# ---------------------------------------------------------------- six legs
for sx, sy, hp in HIPS:
    tag = {1: 'F', 0: 'M', -1: 'B'}[sy] + ('L' if sx < 0 else 'R')
    ank = hp + Vector((sx * 3.7, sy * 2.2, 0)); ank.z = ANKLE_Z
    knee, Mt2, Ms2 = spider_leg(R, tag, hp, ank, THIGH, SHIN, ANKLE_Z, (sx * 0.45, sy * 0.25, 1), k=1.3, claws=3)
    live_ram(R, 'RamHip' + tag, Matrix.Identity(4), hp + Vector((-sx * 0.55, 0, 1.4)), 'Hull', Mt2, (0, 0.75, THIGH / 2 - 1.3), 'Thigh' + tag, 0.14)

R.extra.update({'thigh': THIGH, 'shin': SHIN, 'ankle': ANKLE_Z, 'footOut': 0, 'upper': list(U(UPPER)), 'arms': list(U(ARMS)),
                'detach': ['ChestL', 'Antenna', 'ChestR']})
run(R, os.path.dirname(os.path.abspath(__file__)), 4.8, 40)
