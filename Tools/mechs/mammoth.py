"""
MAMMOTH - heavy four-legged assault walker (about 58 t), second-generation model.

A long faceted armoured hull like a tank's, on four thick plated legs (front knees
forward, rear knees back) so the weight sits inside a wide, stable footprint.
Every leg has hip, knee and ankle cylinders. A crewed cab with twin armoured weapon
arms turns at the front; engine deck, radiators and exhaust stacks at the rear.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mechlib import reset
from mathutils import Vector, Matrix
from hardsurf import *
from parts import *

reset()
STAND = 7.2
THIGH, SHIN, ANKLE_Z = 4.4, 4.6, 1.1
UPPER = Vector((0, 2.6, 9.4))
ARMS = Vector((0, 3.0, 10.65))
K = 1.0
R = Rig('Mammoth', STAND)

B = 'Hull'
# Hull core: a long faceted box, sloped front and rear
core = [(x, y, z) for (y, w, zb, zt) in ((5.6, 1.6, 6.6, 7.6), (4.6, 2.5, 6.2, 8.4), (-3.6, 2.6, 6.2, 8.6), (-5.4, 2.0, 6.6, 8.2))
        for x in (-w, w) for z in (zb, zt)]
armour(B, 'Dark', core, bevel=0.07, seams=False, groove=None, name='HullCore')
# Upper hull armour shell: front glacis, roof, rear deck
armour(B, 'Secondary', [(x, y, z) for (y, w, z) in ((5.75, 1.5, 7.65), (4.5, 2.4, 8.55), (-3.4, 2.5, 8.75), (-5.3, 1.9, 8.35)) for x in (-w, w)] +
       [(x, y, z - 0.35) for (y, w, z) in ((5.75, 1.5, 7.65), (4.5, 2.4, 8.55), (-3.4, 2.5, 8.75), (-5.3, 1.9, 8.35)) for x in (-w, w)], bevel=0.06, seam_min=1.5, seam_w=0.25, name='HullTop')
armour(B, 'Primary', [(x, y, z) for (y, w, z) in ((5.95, 1.55, 6.6), (5.85, 1.55, 7.55), (4.7, 2.3, 8.4)) for x in (-w, w)] +
       [(x, y - 0.3, z) for (y, w, z) in ((5.95, 1.55, 6.6), (5.85, 1.55, 7.55), (4.7, 2.3, 8.4)) for x in (-w, w)], bevel=0.06, seam_min=0.8, name='Glacis')
for s in (-1, 1):
    lamp(B, Mx((s * 1.0, 5.95, 7.0), (-10, 0, 0)), 0.38, 0.16)
    # Side skirts: three overlapping flared plates
    for i, y in enumerate((3.0, 0.0, -3.0)):
        side_plate(B, 'Primary' if i != 1 else 'Secondary', [(-1.75, 0.8), (1.75, 0.8), (1.5, -0.95), (-1.5, -0.95)], 0.26, (s * 2.85, y, 7.4), s, 10,
                   stripe_at=0.45, stripe_h=0.14, bevel=0.05, seam_min=0.8, name='Skirt')
    hose(B, [(s * 1.4, -4.2, 9.0), (s * 2.2, -2.0, 8.95), (s * 2.3, 1.5, 8.9), (s * 1.6, 3.4, 9.0)], 0.14)
# Engine deck: radiator grilles, stacks, hazard band
grille(B, Mx((0, -3.0, 8.75), (90, 0, 0)), 3.0, 1.8, slats=7, depth=0.12)
hazard(B, Mx((0, -1.75, 8.78), (90, 0, 0)), 3.2, 0.3, n=8)
for s in (-1, 1):
    lathe(B, 'Dark', (s * 1.75, -4.3, 8.4), (0, 0, 1), [(0, 0), (0.45, 0), (0.45, 0.2), (0.38, 0.28), (0.38, 1.9), (0.48, 1.95), (0.48, 2.2), (0.32, 2.2), (0.32, 2.0), (0, 2.0)], segs=8, name='Stack')
    lathe(B, 'Hot', (s * 1.75, -4.3, 10.4), (0, 0, 1), [(0, 0), (0.32, 0), (0, 0.01)], segs=8, name='StackGlow')
    n = jet(B, (s * 1.6, -1.0, 6.3), (0, 0, -1), 0.75)
    R.anchor('Jet' + ('L' if s < 0 else 'R'), n, 'Root', (0, 0, -1))
armour(B, 'Primary', [(x, y, z) for (y, w, z) in ((-5.5, 1.9, 8.2), (-5.75, 1.7, 6.8)) for x in (-w, w)] +
       [(x, y + 0.3, z) for (y, w, z) in ((-5.5, 1.9, 8.2), (-5.75, 1.7, 6.8)) for x in (-w, w)], bevel=0.05, seam_min=0.8, name='RearPlate')
grille(B, Mx((0, -5.95, 7.5), (8, 0, 180)), 2.2, 0.9, slats=4, depth=0.1)
corners = [(-1, 1), (1, -1), (1, 1), (-1, -1)]
for sx, sy in corners:
    hp = Vector((sx * 2.55, sy * 3.4, 6.8))
    armour(B, 'Secondary', sec(-1.05, 0.45, 1.2, 0.4) + sec(1.15, 0.45, 1.3, 0.5), Mx(hp - Vector((sx * 0.95, 0, 0))), bevel=0.05, seam_min=0.6, name='HipHousing')
    armour(B, 'Primary', sec(0, 0.95, 1.35, 0.45, sx * 0.3, 0) + sec(0.45, 0.65, 1.05, 0.35, sx * 0.2, 0), Mx(hp + Vector((0, 0, 1.1))), bevel=0.05, seam_min=0.6, name='HipCowl')
lathe(B, 'Dark', (0, UPPER.y, 8.95), (0, 0, 1), [(0, 0), (1.85, 0), (1.9, 0.08), (1.9, 0.42), (0, 0.42)], segs=44, name='RingBase')
gear(B, (0, UPPER.y, 9.4), (0, 0, 1), 1.55, 0.12, teeth=44)
R.rigid('Hull', (0, 0, STAND), 'Root')

# ---- Cab ----
T = 'Torso'
c0 = UPPER
lathe(T, 'Metal', c0, (0, 0, 1), [(0, 0), (1.75, 0), (1.8, 0.06), (1.8, 0.3), (0, 0.3)], segs=44, name='RingTop')
armour(T, 'Secondary', sec(0.25, 1.65, 1.75, 0.6, 0, -0.1) + sec(1.3, 1.8, 1.9, 0.65, 0, 0.0) + sec(1.9, 1.35, 1.45, 0.5, 0, -0.25) + [(0, 2.15, 0.9)],
       Mx(c0), bevel=0.06, seam_min=0.8, name='Cab')
armour(T, 'Primary', sec(1.85, 1.2, 1.35, 0.45, 0, -0.25) + sec(2.2, 0.95, 1.05, 0.35, 0, -0.4), Mx(c0), bevel=0.05, seam_min=0.6, name='CabRoof')
armour(T, 'Black', boxpts((2.2, 0.3, 0.3), ch=0.1), Mx(c0 + Vector((0, 1.85, 1.25)), (-20, 0, 0)), bevel=0.03, seams=False, groove=None, name='SlitFrame')
armour(T, 'Glow', boxpts((1.9, 0.1, 0.14)), Mx(c0 + Vector((0, 1.98, 1.27)), (-20, 0, 0)), bevel=0, seams=False, groove=None, name='Slit')
for s in (-1, 1):
    grille(T, Mx(c0 + Vector((s * 1.7, -0.8, 1.0)), (0, 0, -s * 90)), 1.3, 0.55, slats=3, depth=0.1)
    armour(T, 'Dark', boxpts((0.5, 0.6, 0.6), ch=0.12), Mx(c0 + Vector((s * 1.95, -0.4, 0.6))), bevel=0.04, seams=False, groove=None, name='ShLug')
armour(T, 'Dark', boxpts((0.9, 0.9, 0.14), ch=0.15), Mx(c0 + Vector((-0.5, -1.0, 2.25))), bevel=0.03, seams=False, groove=None, name='Hatch')
R.rigid('Torso', UPPER, 'Upper')

for s in (-1, 1):
    arm(R, s, (s * 2.2, ARMS.y, ARMS.z), (s * 2.35, ARMS.y + 0.4, ARMS.z - 0.6), (s * 2.4, 4.45, ARMS.z - 0.1), k=0.72, gauntlet=(1.05, 1.0, 1.0))
    armour('Arms', 'Dark', boxpts((0.45, 0.55, 0.55), ch=0.1), Mx((s * 2.05, ARMS.y - 1.2, ARMS.z - 0.3)), bevel=0.04, seams=False, groove=None, name='ArmLug')
R.rigid('Arms', ARMS, 'Arms')
shoulder_rams(R, [(c0 + Vector((s * 1.95, -0.4, 0.6)), (s * 2.05, ARMS.y - 1.2, ARMS.z - 0.3)) for s in (-1, 1)], k=0.7)

# ---- Detachable deck plates and antenna ----
for s, nm in ((-1, 'ChestL'), (1, 'ChestR')):
    cc = Vector((s * 1.35, -0.6, 9.1))
    armour(nm, 'Primary', [(x, y, z) for x in (-0.95, 0.95) for y in (-1.6, 1.6) for z in (0, 0.28)] + [(s * 0.0, 0, 0.45)], Mx(cc, (0, -s * 6, 0)), bevel=0.05, seam_min=0.6, name='DeckPlate')
    stripe(nm, Mx(cc + Vector((s * 0.55, 0, 0.36)), (90, 0, 0)), 0.3, 2.6)
    R.rigid(nm, cc, 'Root')
mast = Vector((-1.6, -2.6, 9.0))
armour('Antenna', 'Dark', boxpts((0.4, 0.4, 0.5), ch=0.08), Mx(mast + Vector((0, 0, 0.25))), bevel=0.03, seams=False, groove=None, name='MastBase')
lathe('Antenna', 'Dark', mast, (0, 0, 1), [(0.07, 0.5), (0.05, 2.8), (0.0, 2.9)], segs=8, name='Mast')
lathe('Antenna', 'Glow', mast + Vector((0, 0, 2.8)), (0, 0, 1), [(0, 0), (0.07, 0), (0, 0.12)], segs=10, name='Tip')
R.rigid('Antenna', mast, 'Root')

R.anchor('BackL', (-1.3, -2.2, 9.5), 'Root')
R.anchor('BackR', (1.3, -2.2, 9.5), 'Root')
R.anchor('Shoulder', (0, 2.4, ARMS.z + 1.55), 'Arms')
R.anchor('Smoke', (0, -2.8, 9.6), 'Root')

for sx, sy in corners:
    tag = ('F' if sy > 0 else 'B') + ('L' if sx < 0 else 'R')
    hp = Vector((sx * 2.55, sy * 3.4, 6.8))
    ank = hp + Vector((sx * 3.0, sy * 3.6, 0)); ank.z = ANKLE_Z
    knee, Mt, Ms = walker_leg(R, sx, hp, ank, THIGH, SHIN, ANKLE_Z, k=K, tag=tag,
                              st=dict(shin='box' if sy > 0 else 'crest', toes=3, foot_len=1.0, foot_w=1.0, bend=(0, 1 if sy > 0 else -1, 0)))
    # Hip cylinder under the hull: from a hanger on the belly, behind the hip, to a hanger
    # under the thigh. The thighs run nearly level, so the cylinder works below them.
    pa = hp + Vector((sx * 0.35, -sy * 1.7, -0.95))
    pb = Mt @ Vector((0, -1.25, 0.3))
    ax = (pb - pa).normalized()
    hanger('Hull', pa + Vector((0, 0, 0.45)), pa, ax, 1.0)
    hanger('Thigh' + tag, Mt @ Vector((0, -0.6, 0.3)), pb, ax, 1.0)
    live_ram(R, 'RamHip' + tag, Matrix.Identity(4), pa, 'Hull', Matrix.Identity(4), pb, 'Thigh' + tag, 0.17)

R.extra.update({'thigh': THIGH, 'shin': SHIN, 'ankle': ANKLE_Z, 'footOut': 0, 'upper': list(U(UPPER)), 'arms': list(U(ARMS))})
run(R, os.path.dirname(os.path.abspath(__file__)), 6.0, 46)
