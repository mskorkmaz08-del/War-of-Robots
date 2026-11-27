"""
LANCER - tall, slim long-range walker (about 32 t), second-generation model.

Built for reach and a steady aim: long legs with overlapping armadillo shin plates
and two-toed feet, a narrow prow chest of steep angled plates, a long-lens sensor
head carried forward on an actuated neck, swept shoulder fins, a sensor mast and
slim thruster pods. Working cylinders at hips, knees, ankles, shoulders and neck.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mechlib import reset
from mathutils import Vector, Matrix
from hardsurf import *
from parts import *

reset()
STAND = 7.6
HIP_X, HIP_Z = 2.0, 7.35
THIGH, SHIN, ANKLE_Z, FOOT_OUT = 3.8, 4.0, 1.0, 0.45
UPPER = Vector((0, 0, 8.7))
ARMS = Vector((0, 0.1, 11.6))
K = 0.82
R = Rig('Lancer', STAND)

ring_top = pelvis(R, HIP_X, HIP_Z, k=0.76, ring_r=1.45)

T = 'Torso'
lathe(T, 'Metal', (0, 0, ring_top), (0, 0, 1), [(0, 0), (1.35, 0), (1.4, 0.06), (1.4, 0.28), (1.25, 0.34), (0, 0.34)], segs=40, name='RingTop')
armour(T, 'Dark', ngon(ring_top + 0.3, 1.2, 8, 22.5) + ngon(ring_top + 1.0, 1.3, 8, 22.5), bevel=0.05, seams=False, groove=None, name='Waist')
# Prow chest: a tall narrow hull, keel-shaped at the front
core = sec(9.5, 1.3, 1.1, 0.35) + sec(10.6, 1.85, 1.45, 0.5, 0, 0.1) + sec(11.9, 2.0, 1.5, 0.55, 0, 0.15) + sec(12.6, 1.55, 1.2, 0.45, 0, 0.0)
armour(T, 'Dark', core, bevel=0.06, seams=False, groove=None, name='ChestCore')
for s in (-1, 1):
    armour(T, 'Secondary', [(s * x, y, z) for x, y, z in ((1.65, -1.1, 9.8), (1.65, 1.0, 9.8), (2.05, -1.3, 11.6), (2.05, 1.2, 11.6), (1.6, -1.1, 12.5), (1.6, 0.9, 12.5),
                                                       (1.45, -1.1, 9.8), (1.45, 1.0, 9.8), (1.85, -1.3, 11.6), (1.85, 1.2, 11.6))], bevel=0.05, seam_min=0.6, name='ChestSide')
    armour(T, 'Dark', boxpts((0.5, 1.7, 1.4), ch=0.3), Mx((s * 1.95, 0.1, 11.6)), bevel=0.05, seams=False, groove=None, name='ShoulderSocket')
    gear(T, (s * 2.2, 0.1, 11.6), (1, 0, 0), 0.45, 0.12)
    armour(T, 'Dark', boxpts((0.4, 0.55, 0.55), ch=0.1), Mx((s * 1.75, -1.15, 9.95)), bevel=0.04, seams=False, groove=None, name='ShLug')
# Spine with radiator fins
armour(T, 'Primary', [(x, y, z) for x, y, z in ((0.5, -1.3, 9.7), (-0.5, -1.3, 9.7), (0.55, -1.8, 11.0), (-0.55, -1.8, 11.0), (0.45, -1.5, 12.5), (-0.45, -1.5, 12.5),
                                                (0.5, -0.9, 9.7), (-0.5, -0.9, 9.7), (0.45, -0.9, 12.5), (-0.45, -0.9, 12.5))], bevel=0.05, seam_min=0.6, name='Spine')
for i in range(7):
    armour(T, 'Dark', boxpts((0.06, 0.7, 0.26)), Mx((0, -2.05, 9.9 + i * 0.38), (-15, 0, 0)), bevel=0, seams=False, groove=None, name='Fin')
# Neck: a short armoured column on a working pitch cylinder pair
armour(T, 'Dark', boxpts((0.8, 0.9, 1.0), ch=0.2, taper=(0.8, 0.8)), Mx((0, 0.5, 12.85), (25, 0, 0)), bevel=0.04, seams=False, groove=None, name='Neck')
for s in (-1, 1):
    piston(T, (s * 0.55, -0.1, 12.45), (s * 0.45, 0.75, 13.25), 0.1)
# Long-lens sensor head
H = Vector((0, 1.0, 13.45))
armour(T, 'Primary', sec(-0.35, 0.72, 1.1, 0.25, 0, 0) + sec(0.35, 0.65, 1.0, 0.25, 0, -0.1) + [(0, 1.45, 0.05), (0.4, 1.3, -0.15), (-0.4, 1.3, -0.15)],
       Mx(H), bevel=0.05, seam_min=0.4, name='Head')
armour(T, 'Secondary', sec(0, 0.82, 0.95, 0.2, 0, 0.15) + sec(0.18, 0.7, 0.9, 0.2, 0, 0.1) + [(0, 1.55, 0.05)], Mx(H + Vector((0, 0, 0.35))), bevel=0.04, seam_min=0.4, name='Hood')
lens = H + Vector((0, 1.3, -0.05))
lathe(T, 'Dark', lens, (0, 1, 0), [(0, -0.3), (0.38, -0.3), (0.42, 0.0), (0.42, 0.14), (0.34, 0.18), (0, 0.18)], segs=28, name='LensBarrel')
lathe(T, 'Glow', lens, (0, 1, 0), [(0, 0.12), (0.28, 0.12), (0.22, 0.2), (0, 0.22)], segs=28, name='Lens')
for s in (-1, 1):
    lathe(T, 'Glow', H + Vector((s * 0.5, 1.15, -0.25)), (0, 1, 0), [(0, 0.0), (0.07, 0.0), (0.07, 0.08), (0, 0.08)], segs=12, name='Aux')
    cyl(T, 'Dark', H + Vector((s * 0.75, 0.1, -0.05)), (1, 0, 0), 0.22, 0.22, segs=14, name='HeadPivot')
# Slim thruster pods
for s in (-1, 1):
    pod = Vector((s * 1.0, -1.8, 10.5))
    armour(T, 'Primary', boxpts((0.9, 0.9, 2.2), ch=0.25, taper=(0.85, 0.85), endch=0.08), Mx(pod + Vector((0, 0, 0.5))), bevel=0.05, seam_min=0.5, name='Pod')
    n = jet(T, pod + Vector((0, -0.1, -0.75)), (0, -0.3, -1), 0.75)
    R.anchor('Jet' + ('L' if s < 0 else 'R'), n, 'Upper', (0, -0.3, -1))
    hose(T, [(s * 0.5, -1.5, 12.2), (s * 1.1, -1.9, 12.0), (s * 1.0, -1.9, 11.6)], 0.08)
R.rigid('Torso', UPPER, 'Upper')

# Prow plates (detachable)
for s, nm in ((-1, 'ChestL'), (1, 'ChestR')):
    c = Vector((s * 0.9, 1.6, 11.0))
    M = Mx(c, (-10, 0, -s * 34))
    armour(nm, 'Primary', [(x, y, z) for x, z in ((-0.9, -1.3), (0.9, -1.0), (0.95, 0.9), (0.3, 1.4), (-0.9, 1.1)) for y in (-0.18, 0.18)], M, bevel=0.05, seam_min=0.6, name='Prow')
    stripe(nm, M @ Mx((s * 0.2, 0.2, 0.9)), 0.8, 0.13)
    R.rigid(nm, c, 'Upper')

for s in (-1, 1):
    arm(R, s, (s * 2.55, 0.1, 11.6), (s * 2.7, 0.3, 9.95), (s * 2.75, 1.4, 9.45), k=0.74, gauntlet=(1.0, 1.0, 1.0))
    armour('Arms', 'Dark', boxpts((0.4, 0.55, 0.55), ch=0.1), Mx((s * 2.2, -1.25, 11.05)), bevel=0.04, seams=False, groove=None, name='ArmLug')
R.rigid('Arms', ARMS, 'Arms')
shoulder_rams(R, [((s * 1.75, -1.15, 9.95), (s * 2.2, -1.25, 11.05)) for s in (-1, 1)], k=0.7)

# Swept shoulder fins (detachable)
for s, nm in ((-1, 'PauldronL'), (1, 'PauldronR')):
    c = Vector((s * 2.75, -0.1, 12.45))
    M = Mx(c)
    armour(nm, 'Primary', sec(-0.4, 0.85, 1.2, 0.35, 0, 0.2) + sec(0.25, 0.8, 1.3, 0.35, 0, -0.1) + [(s * 0.15, -2.2, 0.55), (-s * 0.25, -2.0, 0.4), (s * 0.3, -1.9, 0.0)],
           M, bevel=0.05, seam_min=0.5, name='Fin')
    side_plate(nm, 'Secondary', [(-1.3, 0.2), (1.2, 0.2), (0.9, -0.7), (-1.0, -0.8)], 0.15, (s * 0.95, -0.2, -0.45), s, 12, M=M,
               stripe_at=0.0, stripe_h=0.13, bevel=0.04, seam_min=0.4, name='Plate')
    R.rigid(nm, c, 'Upper')

# Sensor mast (detachable)
mast = Vector((0.7, -1.1, 12.6))
armour('Antenna', 'Dark', boxpts((0.35, 0.35, 0.5), ch=0.08), Mx(mast + Vector((0, 0, 0.25))), bevel=0.03, seams=False, groove=None, name='MastBase')
lathe('Antenna', 'Dark', mast, (0, 0, 1), [(0.07, 0.5), (0.05, 2.6), (0.0, 2.7)], segs=8, name='Mast')
for kk in range(3):
    armour('Antenna', 'Metal', boxpts((0.6 - kk * 0.12, 0.05, 0.05)), Mx(mast + Vector((0, 0, 1.2 + kk * 0.45))), bevel=0, seams=False, groove=None, name='Spar')
lathe('Antenna', 'Glow', mast + Vector((0, 0, 2.6)), (0, 0, 1), [(0, 0), (0.07, 0), (0, 0.12)], segs=10, name='Tip')
R.rigid('Antenna', mast, 'Upper')

R.anchor('BackL', (-1.5, -1.9, 12.65), 'Upper')
R.anchor('BackR', (1.5, -1.9, 12.65), 'Upper')
R.anchor('Shoulder', (-2.75, -0.2, 13.2), 'Arms')
R.anchor('Smoke', (0, -1.0, 11.3), 'Upper')

for s in (-1, 1):
    hip = (s * HIP_X, 0, HIP_Z)
    knee, Mt, Ms = walker_leg(R, s, hip, (s * (HIP_X + FOOT_OUT), 0, ANKLE_Z), THIGH, SHIN, ANKLE_Z, k=K,
                              st=dict(shin='layered', toes=2, foot_len=1.05, foot_w=0.9, knee_rams=2, ankle_rams=1))
    hip_ram(R, s, hip, Mt, THIGH, k=0.78)

R.extra.update({'thigh': THIGH, 'shin': SHIN, 'ankle': ANKLE_Z, 'footOut': FOOT_OUT, 'upper': list(U(UPPER)), 'arms': list(U(ARMS))})
run(R, os.path.dirname(os.path.abspath(__file__)), 7.2, 44)
