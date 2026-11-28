"""
TITAN - 15 m heavy assault walker (about 50 t), second-generation hard-surface model.

Built like heavy plant, not a toy: a cast pelvis girder with hip housings, legs of
cut and welded plate over an exposed dark frame, a chest hull of faceted armour with
a low sensor head sunk into the glacis, and a reactor pack at the back. Every joint
is driven: hip, knee and ankle cylinders on each leg and a shoulder cylinder on
each side slide in and out as the robot walks and aims. Gear trains turn at the
knees, shoulders and the slewing ring.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mechlib import reset
import mechlib as ml
from mathutils import Vector, Matrix
from hardsurf import *
from parts import *

reset()
STAND = 8.5
HIP_X, HIP_Z = 2.75, 8.0
THIGH, SHIN = 4.0, 4.1
ANKLE_Z = 1.3
FOOT_OUT = 0.8
UPPER = Vector((0, 0, 9.9))
ARMS = Vector((0, 0.2, 13.4))
MOUNT = lambda s: Vector((s * 4.0, 1.8, 10.65))
R = Rig('Titan', STAND)

# ---------------------------------------------------------------- pelvis
ring_top = pelvis(R, HIP_X, HIP_Z, k=1.0, ring_r=1.95)

# ---------------------------------------------------------------- torso
T = 'Torso'
lathe(T, 'Metal', (0, 0, ring_top), (0, 0, 1), [(0, 0), (1.85, 0), (1.9, 0.06), (1.9, 0.28), (1.75, 0.34), (0, 0.34)], segs=48, name='RingTop')
# Waist: a faceted turret base
armour(T, 'Dark', ngon(ring_top + 0.3, 1.75, 8, 22.5) + ngon(ring_top + 1.2, 1.95, 8, 22.5), bevel=0.05, seams=False, groove=None, name='Waist')
# Chest hull: wide, deep, faceted, leaning forward at the top
chest = sec(10.6, 2.0, 1.55, 0.5) + sec(11.8, 2.75, 1.95, 0.7, 0, 0.15) + sec(13.6, 3.0, 2.1, 0.8, 0, 0.25) + \
        sec(14.9, 2.6, 1.85, 0.7, 0, 0.1) + sec(15.4, 1.9, 1.35, 0.5, 0, -0.1)
armour(T, 'Dark', chest, bevel=0.08, seams=False, groove=None, name='ChestCore')
# Chest armour: side plates, upper deck and a collar, each a separate cut plate
for s in (-1, 1):
    armour(T, 'Secondary', [(s * x, y, z) for x, y, z in ((2.55, -1.6, 11.3), (2.55, 1.6, 11.3), (2.95, -1.8, 13.6), (2.95, 1.75, 13.6), (2.6, -1.6, 14.9), (2.6, 1.5, 14.9),
                                                       (2.3, -1.6, 11.3), (2.3, 1.6, 11.3), (2.7, -1.8, 13.6), (2.7, 1.75, 13.6), (2.35, -1.6, 14.9), (2.35, 1.5, 14.9))],
           bevel=0.06, seam_min=1.0, name='ChestSide')
armour(T, 'Secondary', sec(15.0, 2.35, 1.6, 0.55, 0, 0.0) + sec(15.55, 1.95, 1.3, 0.45, 0, -0.05), bevel=0.06, seam_min=1.0, name='Deck')
armour(T, 'Primary', sec(13.2, 2.2, 0.25, 0.1, 0, 1.95) + sec(15.0, 2.0, 0.25, 0.1, 0, 1.75) + [(0, 2.55, 14.0)], bevel=0.06, seam_min=0.8, name='UpperGlacis')
# Belly armour: three stepped plates under the chest
for i, z in enumerate((10.4, 10.95)):
    armour(T, 'Primary', sec(z - 0.25, 1.4 - i * 0.05 + i * 0.35, 0.12, 0.08, 0, 1.75 + i * 0.25) + sec(z + 0.3, 1.6 + i * 0.35, 0.12, 0.08, 0, 1.95 + i * 0.25),
           bevel=0.04, seam_min=10, name='BellyPlate')
# Sensor head sunk in the upper glacis
H = Vector((0, 1.45, 15.95))
armour(T, 'Primary', sec(-0.55, 1.15, 0.75, 0.3, 0, 0) + sec(0.45, 1.05, 0.6, 0.3, 0, -0.1) + [(0, 0.95, -0.05), (0.7, 0.85, 0.0), (-0.7, 0.85, 0.0)],
       Mx(H), bevel=0.05, seam_min=0.4, name='Head')
armour(T, 'Black', boxpts((1.5, 0.3, 0.26), ch=0.08), Mx(H + Vector((0, 0.62, 0.05)), (-8, 0, 0)), bevel=0.03, seams=False, groove=None, name='VisorSlot')
armour(T, 'Glow', boxpts((1.3, 0.1, 0.12)), Mx(H + Vector((0, 0.72, 0.05)), (-8, 0, 0)), bevel=0.0, seams=False, groove=None, name='Visor')
armour(T, 'Secondary', sec(0.0, 1.25, 0.6, 0.25, 0, 0.2) + sec(0.2, 1.1, 0.75, 0.25, 0, 0.35), Mx(H + Vector((0, 0, 0.45))), bevel=0.04, seam_min=0.5, name='Brow')
for s in (-1, 1):
    lamp(T, Mx(H + Vector((s * 0.85, 0.7, -0.35))), 0.22, 0.12)
    cyl(T, 'Dark', H + Vector((s * 1.25, 0.1, 0.0)), (1, 0, 0), 0.32, 0.3, segs=16, name='HeadPivot')
# Shoulder sockets
for s in (-1, 1):
    armour(T, 'Dark', sec(-0.95, 0.55, 1.2, 0.35) + sec(0.95, 0.55, 1.2, 0.35), Mx((s * 3.05, 0.15, 13.4)), bevel=0.05, seams=False, groove=None, name='ShoulderSocket')
    gear(T, (s * 3.25, 0.15, 13.4), (1, 0, 0), 0.55, 0.14)
    # Side intake grille
    grille(T, Mx((s * 2.95, 0.4, 11.9), (0, 0, -s * 90)), 1.6, 0.9, slats=5, depth=0.12)
    # torso lug for the shoulder cylinder
    armour(T, 'Dark', boxpts((0.5, 0.7, 0.7), ch=0.15), Mx((s * 3.0, -1.3, 11.35)), bevel=0.04, seams=False, groove=None, name='ShLug')
# Back: reactor pack with radiators and exhaust stacks
pack = sec(10.9, 1.9, 0.9, 0.3, 0, -2.2) + sec(14.6, 2.1, 1.0, 0.35, 0, -2.3) + sec(15.2, 1.7, 0.8, 0.3, 0, -2.15)
armour(T, 'Primary', pack, bevel=0.07, seam_min=1.0, name='Reactor')
grille(T, Mx((0, -3.25, 13.3), (0, 0, 180)), 2.6, 1.6, slats=7, depth=0.14)
grille(T, Mx((0, -3.25, 11.6), (0, 0, 180)), 2.6, 0.9, slats=4, depth=0.14)
hazard(T, Mx((0, -3.32, 14.55), (0, 0, 180)), 2.4, 0.3, n=7)
for s in (-1, 1):
    lathe(T, 'Dark', (s * 1.3, -2.6, 15.0), (0, 0, 1), [(0, 0), (0.45, 0), (0.45, 0.2), (0.38, 0.28), (0.38, 1.6), (0.48, 1.65), (0.48, 1.9), (0.32, 1.9), (0.32, 1.7), (0, 1.7)], segs=8, name='Stack')
    lathe(T, 'Hot', (s * 1.3, -2.6, 16.7), (0, 0, 1), [(0, 0), (0.32, 0), (0, 0.01)], segs=8, name='StackGlow')
    for kk in range(3):
        armour(T, 'Metal', ngon(15.4 + kk * 0.4, 0.43, 8, 22.5, cx=s * 1.3, cy=-2.6) + ngon(15.48 + kk * 0.4, 0.43, 8, 22.5, cx=s * 1.3, cy=-2.6), bevel=0, seams=False, groove=None, name='StackBand')
    # cooling fins down the sides of the pack
    for kk in range(6):
        armour(T, 'Dark', boxpts((0.08, 0.9, 0.5)), Mx((s * 2.05, -2.3, 11.3 + kk * 0.55)), bevel=0, seams=False, groove=None, name='Fin')
    hose(T, [(s * 0.9, -3.2, 14.3), (s * 1.9, -3.0, 13.5), (s * 2.55, -1.8, 12.6), (s * 2.9, -0.6, 12.2)], 0.14)
    hose(T, [(s * 1.4, -3.2, 11.0), (s * 2.3, -2.6, 10.6), (s * 2.45, -1.2, 10.9)], 0.11)
    n = jet(T, (s * 1.45, -2.6, 10.4), (0, -0.35, -1), 0.95)
    R.anchor('Jet' + ('L' if s < 0 else 'R'), n, 'Upper', (0, -0.35, -1))
# roof: hatch, rails, sensor mast base
armour(T, 'Dark', boxpts((1.0, 1.0, 0.16), ch=0.15), Mx((-0.7, -0.6, 15.45)), bevel=0.03, seams=False, groove=None, name='Hatch')
for s in (-1, 1):
    tube(T, 'Metal', [(s * 1.5, -1.6, 15.35), (s * 1.5, -1.6, 15.85), (s * 1.5, 0.4, 15.85), (s * 1.5, 0.4, 15.35)], 0.05, segs=6)
# Fortress roof: a parapet of merlons round the deck and two searchlights
for x in (-1.75, -1.15, 1.15, 1.75):
    armour(T, 'Secondary', boxpts((0.42, 0.42, 0.62), ch=0.06, endch=0.04), Mx((x, 1.05, 15.85)), bevel=0.03, seam_min=10, name='Merlon')
for s in (-1, 1):
    for y in (-1.1, -0.3, 0.45):
        armour(T, 'Secondary', boxpts((0.42, 0.42, 0.62), ch=0.06, endch=0.04), Mx((s * 1.95, y, 15.85)), bevel=0.03, seam_min=10, name='Merlon')
    lp = Vector((s * 1.2, -0.15, 16.0))
    armour(T, 'Dark', boxpts((0.5, 0.5, 0.35), ch=0.08), Mx(lp + Vector((0, 0, -0.18))), bevel=0.03, seams=False, groove=None, name='LightMount')
    lathe(T, 'Secondary', lp + Vector((0, -0.35, 0.25)), (0, 1, 0), [(0, 0), (0.28, 0), (0.36, 0.25), (0.36, 0.7), (0.3, 0.72), (0, 0.72)], segs=18, name='Searchlight')
    lathe(T, 'Glow', lp + Vector((0, 0.36, 0.25)), (0, 1, 0), [(0, 0), (0.3, 0), (0, 0.03)], segs=18, name='SearchLens')
R.rigid('Torso', UPPER, 'Upper')

# Detachable glacis plates
for s, nm in ((-1, 'ChestL'), (1, 'ChestR')):
    c = Vector((s * 1.35, 2.05, 12.4))
    M = Mx(c, (-10, 0, -s * 24))
    armour(nm, 'Primary', [(x, y, z) for x, z in ((-1.25, -1.35), (1.25, -1.35), (1.3, 0.9), (0.6, 1.45), (-1.3, 1.0)) for y in (-0.22, 0.22)] +
           [], M, bevel=0.06, seam_min=0.8, name="Glacis")
    stripe(nm, M @ Mx((s * 0.55, 0.36, -0.95), (0, 0, 0)), 1.0, 0.16)
    bolts_line(nm, M @ Vector((-1.1, 0.25, 1.0)), M @ Vector((-1.1, 0.25, -1.1)), 5, M.to_3x3() @ Vector((0, 1, 0)), 0.06)
    R.rigid(nm, c, 'Upper')

# ---------------------------------------------------------------- arms
for s in (-1, 1):
    arm(R, s, (s * 4.05, 0.2, 13.4), (s * 4.15, 0.45, 10.95), MOUNT(s), k=1.1, gauntlet=(1.15, 1.0, 1.1))
    armour('Arms', 'Dark', boxpts((0.5, 0.7, 0.7), ch=0.15), Mx((s * 3.55, -1.5, 12.75)), bevel=0.04, seams=False, groove=None, name='ArmLug')
R.rigid('Arms', ARMS, 'Arms')
shoulder_rams(R, [((s * 3.0, -1.3, 11.35), (s * 3.55, -1.5, 12.75)) for s in (-1, 1)], k=1.0)

# Detachable pauldrons: heavy faceted shoulder armour wrapping the shoulder bearing
for s, nm in ((-1, 'PauldronL'), (1, 'PauldronR')):
    c = Vector((s * 4.35, 0.1, 14.6))
    M = Mx(c)
    armour(nm, 'Primary', sec(-1.7, 1.15, 1.55, 0.45, s * 0.25, 0.05) + sec(-0.2, 1.45, 1.95, 0.6, s * 0.15, 0) + sec(0.75, 1.3, 1.8, 0.6, 0, 0)
           + sec(1.15, 0.85, 1.35, 0.4, -s * 0.15, -0.05), M, bevel=0.08, seam_min=0.9, seam_w=0.2, name='Pauldron')
    # outer layered plates
    side_plate(nm, 'Secondary', [(-1.75, 0.5), (1.75, 0.5), (1.55, -1.3), (-1.55, -1.3)], 0.2, (s * 1.62, 0, -0.45), s, 6, M=M,
               stripe_at=0.25, stripe_h=0.2, hazard_at=-0.95, bevel=0.05, seam_min=0.6, name='Plate')
    lamp(nm, M @ Mx((s * 0.7, 1.95, 0.2)), 0.36, 0.14)
    grille(nm, M @ Mx((s * 0.2, -1.95, -0.3), (0, 0, 180)), 1.4, 0.9, slats=4, depth=0.1)
    bolts_line(nm, M @ Vector((s * 0.0, -1.5, 1.17)), M @ Vector((s * 0.0, 1.4, 1.17)), 8, (0, 0, 1), 0.06)
    R.rigid(nm, c, 'Upper')

R.anchor('BackL', (-4.0, -0.7, 16.0), 'Upper')
R.anchor('BackR', (4.0, -0.7, 16.0), 'Upper')
R.anchor('Shoulder', (-4.0, 0.0, 15.6), 'Arms')
R.anchor('Smoke', (0.6, -1.4, 14.6), 'Upper')

# ---------------------------------------------------------------- legs
for s in (-1, 1):
    hip = (s * HIP_X, 0, HIP_Z)
    knee, Mt, Ms = walker_leg(R, s, hip, (s * (HIP_X + FOOT_OUT), 0, ANKLE_Z), THIGH, SHIN, ANKLE_Z, k=1.2,
                              st=dict(shin='crest', toes=3, foot_len=1.15, foot_w=1.05))
    hip_ram(R, s, hip, Mt, THIGH, k=1.1)

R.extra.update({'thigh': THIGH, 'shin': SHIN, 'ankle': ANKLE_Z, 'footOut': FOOT_OUT, 'upper': list(U(UPPER)), 'arms': list(U(ARMS))})
run(R, os.path.dirname(os.path.abspath(__file__)), 8.6)
