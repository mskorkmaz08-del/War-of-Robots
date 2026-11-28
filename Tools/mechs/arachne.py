"""
ARACHNE - four-legged artillery walker (about 36 t), second-generation model.

A low armoured hull of faceted plate with a squared rear power module, slung between four long box-beam legs with high geared knees. Each
leg has a working knee cylinder and a hip cylinder from the body. The weapons ride
a faceted sensor turret at the front with a visor slit and a rangefinder.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mechlib import reset
from mathutils import Vector, Matrix
from hardsurf import *
from parts import *

reset()
STAND = 6.5
THIGH, SHIN, ANKLE_Z = 6.0, 6.8, 0.9
UPPER = Vector((0, 1.0, 8.1))
ARMS = Vector((0, 1.6, 9.25))
R = Rig('Arachne', STAND)

B = 'Hull'
# Thorax: a wide faceted hull
armour(B, 'Dark', sec(6.1, 1.9, 2.6, 0.8, 0, 0.3) + sec(7.0, 2.7, 3.2, 1.1, 0, 0.2) + sec(7.9, 2.2, 2.7, 0.9, 0, 0.3), bevel=0.06, seams=False, groove=None, name='Thorax')
for s in (-1, 1):
    armour(B, 'Primary', [(s * x, y, z) for x, y, z in ((0.15, 2.9, 8.0), (2.1, 2.4, 7.95), (2.3, -1.9, 7.95), (0.15, -2.3, 8.05),
                                                       (0.15, 2.5, 8.35), (1.8, 2.0, 8.3), (1.9, -1.6, 8.3), (0.15, -1.9, 8.4))], bevel=0.05, seam_min=0.8, name='DeckPlate')
    side_plate(B, 'Secondary', [(-2.4, 0.5), (2.6, 0.5), (2.2, -0.6), (-2.0, -0.6)], 0.2, (s * 2.75, 0.2, 7.2), s, 10, stripe_at=0.25, stripe_h=0.14, bevel=0.04, seam_min=0.6, name='SideArmour')
    grille(B, Mx((s * 2.55, -1.4, 6.45), (0, 0, -s * 90)), 1.2, 0.45, slats=3, depth=0.1)
    hose(B, [(s * 1.6, -1.6, 8.2), (s * 2.3, -0.6, 7.9), (s * 2.6, 0.9, 7.2)], 0.12)
# Rear power module: a squared engine bay with radiators, exhaust stacks and a tow frame
armour(B, 'Secondary', [(x, y, z) for x in (-2.1, 2.1) for y in (-2.2, -5.2) for z in (6.55, 8.0)] + [(x, y, 8.3) for x in (-1.7, 1.7) for y in (-2.4, -4.9)],
       bevel=0.06, seam_min=0.8, name='PowerModule')
grille(B, Mx((0, -3.75, 8.31), (90, 0, 0)), 2.6, 2.0, slats=7, depth=0.08)
for s in (-1, 1):
    grille(B, Mx((s * 2.11, -3.7, 7.25), (0, 0, -s * 90)), 2.2, 0.9, slats=5, depth=0.08)
    lathe(B, 'Dark', (s * 1.35, -4.75, 8.25), (0, 0, 1), [(0, 0), (0.24, 0), (0.24, 1.1), (0.3, 1.15), (0.3, 1.3), (0, 1.3)], segs=12, name='Exhaust')
armour(B, 'Dark', [(x, y, z) for x in (-1.6, 1.6) for y in (-5.2, -5.7) for z in (6.7, 7.7)], bevel=0.04, seams=False, groove=None, name='RearBumper')
lamp(B, Mx((0, -5.72, 7.45), (0, 0, 180)), 0.9, 0.1)
hazard(B, Mx((0, -5.71, 6.95), (0, 0, 180)), 2.8, 0.26, n=7)
# Front armour: a sloped glacis under the turret with lamps
armour(B, 'Primary', sec(6.0, 1.5, 0.15, 0.1, 0, 2.95) + sec(7.6, 1.9, 0.15, 0.1, 0, 3.25) + [(0, 3.6, 6.8)], bevel=0.05, seam_min=0.6, name='Glacis')
lamp(B, Mx((0, 3.35, 6.6), (-20, 0, 0)), 1.6, 0.1)
hazard(B, Mx((0, -3.2, 8.62), (90, 0, 0)), 2.6, 0.3, n=6)
# Belly keel and jets
armour(B, 'Dark', sec(5.8, 1.0, 2.4, 0.4, 0, 0.0) + sec(6.3, 1.5, 3.0, 0.5, 0, 0.0), bevel=0.04, seams=False, groove=None, name='Keel')
for s in (-1, 1):
    n = jet(B, (s * 1.2, -3.0, 6.1), (0, 0, -1), 0.65)
    R.anchor('Jet' + ('L' if s < 0 else 'R'), n, 'Root', (0, 0, -1))
corners = [(-1, 1), (1, -1), (1, 1), (-1, -1)]
for sx, sy in corners:
    hp = Vector((sx * 2.6, sy * 1.8 - 0.4, 6.5))
    armour(B, 'Secondary', sec(-0.95, 0.5, 1.0, 0.35, 0, 0) + sec(0.95, 0.5, 1.0, 0.35, 0, 0), Mx(hp - Vector((sx * 0.75, 0, 0))), bevel=0.05, seam_min=0.5, name='Socket')
    # hip cylinder lug on the deck above the socket
    armour(B, 'Dark', boxpts((0.6, 0.6, 0.6), ch=0.15), Mx(hp + Vector((-sx * 0.6, 0, 1.55))), bevel=0.04, seams=False, groove=None, name='HipLug')
lathe(B, 'Dark', (0, 1.0, 7.85), (0, 0, 1), [(0, 0), (1.75, 0), (1.8, 0.08), (1.8, 0.3), (0, 0.3)], segs=44, name='RingBase')
gear(B, (0, 1.0, 8.15), (0, 0, 1), 1.45, 0.12, teeth=40)
R.rigid('Hull', (0, 0, STAND), 'Root')

# ---- Sensor turret ----
T = 'Torso'
lathe(T, 'Metal', UPPER + Vector((0, 0, 0.05)), (0, 0, 1), [(0, 0), (1.65, 0), (1.7, 0.06), (1.7, 0.25), (0, 0.25)], segs=44, name='RingTop')
c = UPPER
armour(T, 'Secondary', sec(0.25, 1.5, 1.6, 0.6, 0, 0.0) + sec(1.0, 1.75, 1.75, 0.7, 0, 0.0) + sec(1.55, 1.2, 1.3, 0.5, 0, -0.2) + [(0, 2.05, 0.6)], Mx(c), bevel=0.06, seam_min=0.7, name='Turret')
armour(T, 'Primary', sec(1.5, 1.05, 1.2, 0.45, 0, -0.2) + sec(1.85, 0.8, 0.95, 0.35, 0, -0.35), Mx(c), bevel=0.05, seam_min=0.5, name='TurretTop')
# Sensor head: a recessed visor band with a single glowing slit and a rangefinder lens
armour(T, 'Black', [(x, y, z) for x in (-1.25, 1.25) for (y, z) in ((1.55, 0.25), (1.55, 0.95), (1.95, 0.35), (1.95, 0.85))], Mx(c), bevel=0.02, seams=False, groove=None, name='VisorBand')
armour(T, 'Glow', boxpts((2.1, 0.06, 0.12)), Mx(c + Vector((0, 1.97, 0.62))), bevel=0, seams=False, groove=None, name='Visor')
lathe(T, 'Dark', c + Vector((0.85, 1.9, 1.25)), (0, 1, 0), [(0, -0.3), (0.3, -0.3), (0.32, 0.0), (0.3, 0.25), (0, 0.25)], segs=20, name='RangeHousing')
lathe(T, 'Glow', c + Vector((0.85, 2.16, 1.25)), (0, 1, 0), [(0, 0), (0.2, 0), (0, 0.05)], segs=18, name='RangeLens')
for s in (-1, 1):
    grille(T, Mx(c + Vector((s * 1.62, -0.5, 0.75)), (0, 0, -s * 90)), 1.0, 0.45, slats=3, depth=0.08)
R.rigid('Torso', UPPER, 'Upper')

# ---- Gun cradle (pitch) ----
A = 'Arms'
cyl(A, 'Dark', ARMS + Vector((0, 0.4, 0)), (1, 0, 0), 0.42, 3.9, segs=20, name='Axle')
for s in (-1, 1):
    m = Vector((s * 1.75, 2.1, ARMS.z))
    drum(A, Vector((s * 1.75, ARMS.y + 0.4, ARMS.z)), (1, 0, 0), 0.7, 0.8, bolts=8)
    loft_y(A, 'Secondary', [(m.y - 1.1, 0.6, 0.62, 0.2, m.x, m.z - ARMS.z + ARMS.z), (m.y + 0.25, 0.55, 0.58, 0.18, m.x, m.z)], bevel=0.05, seam_min=0.4, name='Cradle')
    lathe(A, 'Dark', m + Vector((0, 0.15, 0)), (0, 1, 0), [(0.45, 0), (0.72, 0), (0.72, 0.3), (0.45, 0.3)], segs=28, name='CradleRing')
    R.anchor('Mount' + ('L' if s < 0 else 'R'), m, 'Arms')
armour(A, 'Dark', boxpts((0.45, 0.6, 0.5), ch=0.1), Mx((0, 2.2, ARMS.z - 0.55)), bevel=0.03, seams=False, groove=None, name='ArmLug')
armour(T, 'Dark', boxpts((0.45, 0.6, 0.5), ch=0.1), Mx((0, 1.9, UPPER.z + 0.35)), bevel=0.03, seams=False, groove=None, name='ShLug')
R.rigid('Arms', ARMS, 'Arms')
shoulder_rams(R, [((0, 1.9, UPPER.z + 0.35), (0, 2.2, ARMS.z - 0.55))], k=0.6)

# ---- Detachables ----
# Side skirts between the hips, low on the flanks under the guns' sweep
for s, nm in ((-1, 'ChestL'), (1, 'ChestR')):
    cc = Vector((s * 2.2, -0.35, 7.75))
    armour(nm, 'Primary', [(s * x, y, z) for x, y, z in ((0.0, -0.95, 0.05), (0.0, 0.95, 0.05), (0.95, -0.85, -0.2), (0.95, 0.85, -0.2),
                                                       (0.0, -0.9, 0.35), (0.0, 0.9, 0.35), (0.8, -0.75, 0.05), (0.8, 0.75, 0.05))], Mx(cc), bevel=0.04, seam_min=0.4, name='Plate')
    stripe(nm, Mx(cc + Vector((s * 0.42, 0, 0.23)), (90, 0, 0)), 0.45, 1.2)
    R.rigid(nm, cc, 'Root')
mast = Vector((0, -4.55, 8.3))  # on the power module, behind the guns' reach
armour('Antenna', 'Dark', boxpts((0.35, 0.35, 0.45), ch=0.08), Mx(mast + Vector((0, 0, 0.22))), bevel=0.03, seams=False, groove=None, name='MastBase')
lathe('Antenna', 'Dark', mast, (0, 0, 1), [(0.07, 0.45), (0.05, 2.4), (0.0, 2.5)], segs=8, name='Mast')
lathe('Antenna', 'Glow', mast + Vector((0, 0, 2.4)), (0, 0, 1), [(0, 0), (0.07, 0), (0, 0.12)], segs=10, name='Tip')
R.rigid('Antenna', mast, 'Root')

R.anchor('Back', (0, -3.0, 8.95), 'Root')
R.anchor("Shoulder", (0, 1.4, ARMS.z + 0.85), "Arms")
R.anchor('Smoke', (0, -2.4, 8.7), 'Root')

for sx, sy in corners:
    tag = ('F' if sy > 0 else 'B') + ('L' if sx < 0 else 'R')
    hp = Vector((sx * 2.6, sy * 1.8 - 0.4, 6.5))
    ank = hp + Vector((sx * 4.6, sy * 3.0, 0)); ank.z = ANKLE_Z
    knee, Mt2, Ms2 = spider_leg(R, tag, hp, ank, THIGH, SHIN, ANKLE_Z, (sx * 0.5, sy * 0.3, 1), k=1.6, claws=3)
    for xs in (-1, 1):
        armour('Thigh' + tag, 'Dark', boxpts((0.1, 0.75, 0.5), ch=0.03), Mt2 @ Mx((xs * 0.22, 1.05, THIGH / 2 - 1.6)), bevel=0.02, seams=False, groove=None, name='RamPost')
    live_ram(R, 'RamHip' + tag, Matrix.Identity(4), hp + Vector((-sx * 0.6, 0, 1.55)), 'Hull', Mt2, (0, 1.4, THIGH / 2 - 1.6), 'Thigh' + tag, 0.16)

R.extra.update({'thigh': THIGH, 'shin': SHIN, 'ankle': ANKLE_Z, 'footOut': 0, 'upper': list(U(UPPER)), 'arms': list(U(ARMS))})
run(R, os.path.dirname(os.path.abspath(__file__)), 5.0, 44)
