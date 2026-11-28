"""
VANGUARD - medium all-round assault walker (about 38 t), second-generation model.

A jump-capable line mech: a V-shaped chest of stacked angled plates over a narrow
armoured waist, two big intake ducts either side of the sternum, a twin-eyed head
under a heavy brow, swept shoulder armour, and a jump pack with two large lift
thrusters. Working cylinders at hips, knees, ankles and shoulders.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mechlib import reset
from mathutils import Vector, Matrix
from hardsurf import *
from parts import *

reset()
STAND = 7.8
HIP_X, HIP_Z = 2.35, 7.45
THIGH, SHIN, ANKLE_Z, FOOT_OUT = 3.85, 4.05, 1.15, 0.6
UPPER = Vector((0, 0, 9.0))
ARMS = Vector((0, 0.1, 12.1))
K = 0.95
R = Rig('Vanguard', STAND)

ring_top = pelvis(R, HIP_X, HIP_Z, k=0.88, ring_r=1.7)

T = 'Torso'
lathe(T, 'Metal', (0, 0, ring_top), (0, 0, 1), [(0, 0), (1.6, 0), (1.65, 0.06), (1.65, 0.28), (1.5, 0.34), (0, 0.34)], segs=44, name='RingTop')
# Armoured waist: two stacked octagonal segments
armour(T, 'Dark', ngon(ring_top + 0.3, 1.45, 8, 22.5) + ngon(ring_top + 1.0, 1.55, 8, 22.5), bevel=0.05, seams=False, groove=None, name='Waist')
armour(T, 'Secondary', ngon(ring_top + 0.45, 1.6, 8, 22.5) + ngon(ring_top + 0.8, 1.62, 8, 22.5), bevel=0.04, seams=False, name='WaistBand')
# Chest core: a V - narrow at the waist, broad at the shoulders
core = sec(9.9, 1.55, 1.3, 0.4) + sec(11.2, 2.45, 1.7, 0.6, 0, 0.15) + sec(12.9, 2.75, 1.75, 0.65, 0, 0.1) + sec(13.7, 2.0, 1.35, 0.5, 0, -0.1)
armour(T, 'Dark', core, bevel=0.07, seams=False, groove=None, name='ChestCore')
# Sternum keel
armour(T, 'Secondary', [(x, y, z) for x, y, z in ((0.55, 1.6, 10.0), (-0.55, 1.6, 10.0), (0.75, 1.9, 12.8), (-0.75, 1.9, 12.8), (0, 2.35, 11.6), (0, 2.25, 12.6),
                                                  (0.4, 1.3, 13.4), (-0.4, 1.3, 13.4), (0, 1.2, 9.85))], bevel=0.05, seam_min=0.5, name='Keel')
# Intake ducts either side of the keel
for s in (-1, 1):
    Mi = Mx((s * 1.35, 1.75, 11.2), (-12, 0, -s * 18))
    armour(T, 'Secondary', boxpts((1.3, 0.7, 1.6), ch=0.25, endch=0.08), Mi, bevel=0.05, seam_min=0.5, name='Intake')
    grille(T, Mi @ Mx((0, 0.25, 0)), 1.0, 1.25, slats=6, depth=0.12)
    # Side plates
    armour(T, 'Secondary', [(s * x, y, z) for x, y, z in ((2.2, -1.3, 10.6), (2.2, 1.3, 10.6), (2.75, -1.5, 12.7), (2.75, 1.4, 12.7), (2.05, -1.2, 13.6), (2.05, 1.0, 13.6),
                                                       (1.95, -1.3, 10.6), (1.95, 1.3, 10.6), (2.5, -1.5, 12.7), (2.5, 1.4, 12.7))], bevel=0.05, seam_min=0.8, name='ChestSide')
    armour(T, 'Dark', boxpts((0.6, 2.0, 1.6), ch=0.3), Mx((s * 2.55, 0.1, 12.1)), bevel=0.05, seams=False, groove=None, name='ShoulderSocket')
    gear(T, (s * 2.85, 0.1, 12.1), (1, 0, 0), 0.5, 0.12)
    armour(T, 'Dark', boxpts((0.45, 0.6, 0.6), ch=0.12), Mx((s * 2.4, -1.25, 10.3)), bevel=0.04, seams=False, groove=None, name='ShLug')
# Upper chest deck
armour(T, 'Secondary', sec(13.4, 2.1, 1.45, 0.5) + sec(13.85, 1.7, 1.15, 0.4, 0, -0.05), bevel=0.05, seam_min=0.8, name='Deck')
# Head: low wedge under a heavy brow, twin round eyes
H = Vector((0, 1.15, 14.1))
# Knight's helm: a tall faceted helm with a ridged face, a T-visor and a crest fin
armour(T, 'Primary', sec(-0.55, 0.85, 0.78, 0.32) + sec(0.5, 0.8, 0.72, 0.32, 0, -0.05) + sec(0.85, 0.55, 0.55, 0.22, 0, -0.1) + [(0, 1.08, -0.15), (0, 1.0, 0.45)],
       Mx(H), bevel=0.05, seam_min=0.35, name='Helm')
armour(T, 'Secondary', [(x, y, z) for x in (-0.08, 0.08) for (y, z) in ((0.75, 0.75), (-0.95, 0.7), (-0.75, 1.45), (0.35, 1.25))], Mx(H), bevel=0.02, seams=False, groove=None, name='Crest')
armour(T, 'Accent', [(x, y, z) for x in (-0.1, 0.1) for (y, z) in ((0.45, 1.2), (-0.7, 1.38), (-0.6, 1.5), (0.35, 1.31))], Mx(H), bevel=0.0, seams=False, groove=None, name='CrestStripe')
armour(T, 'Black', boxpts((1.25, 0.34, 0.2), ch=0.05), Mx(H + Vector((0, 0.82, 0.2))), bevel=0.02, seams=False, groove=None, name='VisorBar')
armour(T, 'Black', boxpts((0.24, 0.34, 0.7), ch=0.05), Mx(H + Vector((0, 0.9, -0.15))), bevel=0.02, seams=False, groove=None, name='VisorStem')
armour(T, 'Glow', boxpts((1.1, 0.1, 0.08)), Mx(H + Vector((0, 0.98, 0.2))), bevel=0.0, seams=False, groove=None, name='Visor')
armour(T, 'Glow', boxpts((0.08, 0.1, 0.5)), Mx(H + Vector((0, 1.05, -0.15))), bevel=0.0, seams=False, groove=None, name='VisorStemGlow')
for s in (-1, 1):
    for kk in range(3):
        armour(T, 'Dark', boxpts((0.05, 0.3, 0.06)), Mx(H + Vector((s * 0.55, 0.85, -0.35 - kk * 0.13)), (0, 0, s * 25)), bevel=0, seams=False, groove=None, name='Breather')
    cyl(T, 'Dark', H + Vector((s * 0.95, 0.0, -0.1)), (1, 0, 0), 0.25, 0.25, segs=14, name='HeadPivot')
# Jump pack: a tall faceted housing with two big lift thrusters and radiators
pack = sec(10.0, 1.55, 0.75, 0.3, 0, -1.85) + sec(13.4, 1.75, 0.85, 0.3, 0, -1.95) + sec(13.9, 1.4, 0.7, 0.25, 0, -1.8)
armour(T, 'Primary', pack, bevel=0.06, seam_min=0.9, name='JumpPack')
grille(T, Mx((0, -2.82, 12.4), (0, 0, 180)), 1.8, 1.1, slats=5, depth=0.12)
hazard(T, Mx((0, -2.84, 13.4), (0, 0, 180)), 2.2, 0.26, n=6)
for s in (-1, 1):
    tc = Vector((s * 1.35, -2.55, 10.9))
    armour(T, 'Secondary', boxpts((1.2, 1.2, 2.4), ch=0.35, endch=0.1), Mx(tc + Vector((0, 0, 0.4))), bevel=0.05, seam_min=0.6, name='ThrusterHousing')
    n = jet(T, tc + Vector((0, -0.1, -0.85)), (0, -0.2, -1), 1.15)
    R.anchor('Jet' + ('L' if s < 0 else 'R'), n, 'Upper', (0, -0.2, -1))
    for kk in range(5):
        armour(T, 'Dark', boxpts((0.07, 0.75, 0.4)), Mx((s * 1.85, -1.95, 10.6 + kk * 0.5)), bevel=0, seams=False, groove=None, name='Fin')
    hose(T, [(s * 0.6, -2.7, 13.1), (s * 1.5, -2.7, 12.6), (s * 1.6, -2.9, 11.7)], 0.1)
R.rigid('Torso', UPPER, 'Upper')

# Detachable upper chest plates (stacked, angled)
for s, nm in ((-1, 'ChestL'), (1, 'ChestR')):
    c = Vector((s * 1.35, 1.65, 12.9))
    M = Mx(c, (-14, 0, -s * 22))
    armour(nm, 'Primary', [(x, y, z) for x, z in ((-1.1, -0.7), (1.1, -0.7), (1.25, 0.45), (0.5, 0.75), (-1.15, 0.5)) for y in (-0.2, 0.2)], M, bevel=0.06, seam_min=0.6, name='ChestPlate')
    stripe(nm, M @ Mx((s * 0.4, 0.22, 0.3)), 1.0, 0.14)
    bolts_line(nm, M @ Vector((-0.95, 0.22, -0.55)), M @ Vector((0.95, 0.22, -0.55)), 6, M.to_3x3() @ Vector((0, 1, 0)), 0.05)
    R.rigid(nm, c, 'Upper')

for s in (-1, 1):
    arm(R, s, (s * 3.25, 0.1, 12.1), (s * 3.3, 0.35, 10.2), (s * 3.3, 1.55, 9.75), k=0.88, gauntlet=(1.05, 1.0, 1.0))
    armour('Arms', 'Dark', boxpts((0.45, 0.6, 0.6), ch=0.12), Mx((s * 2.85, -1.35, 11.45)), bevel=0.04, seams=False, groove=None, name='ArmLug')
# Kite shield on the left forearm, braced to the gauntlet
SX = -4.55
kite = [(-0.6, 11.35), (2.0, 11.35), (2.35, 10.3), (0.9, 7.3), (-0.85, 10.3)]
armour('Arms', 'Dark', [(SX + 0.12, y * 1.0, z) for y, z in kite] + [(SX + 0.02, y * 1.04 + 0.02, (z - 10.0) * 1.05 + 10.0) for y, z in kite], bevel=0.04, seams=False, groove=None, name='ShieldBack')
armour('Arms', 'Primary', [(SX - 0.02, y, z) for y, z in kite] + [(SX - 0.2, y * 0.92 + 0.05, (z - 10.0) * 0.92 + 10.0) for y, z in kite] + [(SX - 0.34, 0.75, 10.15)],
       bevel=0.05, seam_min=10, name='Shield')
armour('Arms', 'Metal', [(x, y, z) for x in (SX - 0.04, SX + 0.04) for (y, z) in ((-0.68, 11.42), (2.08, 11.42), (2.08, 11.28), (-0.68, 11.28))], bevel=0.0, seams=False, groove=None, name='ShieldRim')
# A chevron emblem (V for Vanguard) on the shield face
for (ya, za) in ((-0.2, 10.95), (1.7, 10.95)):
    armour('Arms', 'Accent', [(x, y, z) for x in (SX - 0.36, SX - 0.3) for (y, z) in ((ya - 0.17, za), (ya + 0.17, za), (0.75 + 0.17, 8.65), (0.75 - 0.17, 8.65))], bevel=0.0, seams=False, groove=None, name='Chevron')
for yy in (0.15, 1.25):
    armour('Arms', 'Dark', [(x, y, z) for x in (-4.05, SX + 0.12) for y in (yy - 0.15, yy + 0.15) for z in (9.6, 9.9)], bevel=0.02, seams=False, groove=None, name='ShieldStrut')
    lathe('Arms', 'Metal', (SX - 0.24, yy, 9.75), (-1, 0, 0), [(0, 0), (0.12, 0), (0.1, 0.06), (0, 0.08)], segs=8, name='Rivet')
R.rigid('Arms', ARMS, 'Arms')
shoulder_rams(R, [((s * 2.4, -1.25, 10.3), (s * 2.85, -1.35, 11.45)) for s in (-1, 1)], k=0.85)

# Detachable swept shoulder armour
for s, nm in ((-1, 'PauldronL'), (1, 'PauldronR')):
    c = Vector((s * 3.45, 0.0, 13.2))
    M = Mx(c)
    armour(nm, 'Primary', sec(-0.5, 1.05, 1.45, 0.45, s * 0.1, 0.1) + sec(0.35, 1.0, 1.6, 0.5, 0, -0.1) + sec(0.7, 0.6, 1.25, 0.3, -s * 0.2, -0.4)
           + [(s * 0.3, -2.3, 0.6), (s * 0.6, -2.1, 0.1)], M, bevel=0.06, seam_min=0.7, name='Pauldron')
    side_plate(nm, 'Secondary', [(-1.5, 0.3), (1.6, 0.3), (1.2, -0.9), (-1.2, -0.9)], 0.18, (s * 1.18, 0, -0.55), s, 10, M=M,
               stripe_at=0.0, stripe_h=0.16, bevel=0.04, seam_min=0.5, name='Plate')
    lamp(nm, M @ Mx((s * 0.4, 1.55, 0.1)), 0.28, 0.12)
    R.rigid(nm, c, 'Upper')

R.anchor('BackL', (-1.7, -1.6, 14.0), 'Upper')
R.anchor('BackR', (1.7, -1.6, 14.0), 'Upper')
R.anchor('Shoulder', (3.3, -0.2, 14.2), 'Arms')
R.anchor('Smoke', (0, -1.2, 12.0), 'Upper')

for s in (-1, 1):
    hip = (s * HIP_X, 0, HIP_Z)
    knee, Mt, Ms = walker_leg(R, s, hip, (s * (HIP_X + FOOT_OUT), 0, ANKLE_Z), THIGH, SHIN, ANKLE_Z, k=K,
                              st=dict(shin='box', toes=3, foot_len=1.0, foot_w=0.95))
    hip_ram(R, s, hip, Mt, THIGH, k=0.9)

R.extra.update({'thigh': THIGH, 'shin': SHIN, 'ankle': ANKLE_Z, 'footOut': FOOT_OUT, 'upper': list(U(UPPER)), 'arms': list(U(ARMS))})
run(R, os.path.dirname(os.path.abspath(__file__)), 7.6, 44)
