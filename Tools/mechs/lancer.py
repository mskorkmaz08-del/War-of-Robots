"""
LANCER - tall, slim long-range walker (32 t). Built for reach and a steady aim:
long legs with stacked shin plates and two-toed feet, a narrow prow-shaped
chest, a hooded single-lens sensor head on a raised neck, swept shoulder
fins and slim thruster pods on the back.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mechlib import *
from mathutils import Vector, Matrix
from rig import Rig, U, biped_leg, bolts_ring, stripe, run, pelvis, arm, jet_nozzle

reset()
STAND = 7.6
HIP_X, HIP_Z = 2.0, 7.35
THIGH, SHIN, ANKLE_Z, FOOT_OUT = 3.8, 4.0, 1.0, 0.45
UPPER = Vector((0, 0, 8.7))
ARMS = Vector((0, 0.1, 11.6))
K = 0.74
R = Rig('Lancer', STAND)

ring_top = pelvis(R, HIP_X, HIP_Z, k=K, ring_z=8.15, ring_r=1.45)

# ---- Torso: a narrow, angular prow ---------------------------------------
T = 'Torso'
lathe(T, 'Metal', (0, 0, 8.63), (0, 0, 1), [(0, 0), (1.35, 0), (1.4, 0.06), (1.4, 0.28), (1.25, 0.34), (0, 0.34)], segs=40, name='RingTop')
loft(T, 'Dark', [((0, 0, 8.9), 1.15, 0.95, 3.2), ((0, 0.05, 9.55), 1.3, 1.05, 3.2)], axis_up=(0, 1, 0), segs=32, sub=1, name='Abdomen')
loft(T, 'Secondary', [((0, 0.05, 9.05), 1.35, 1.1, 3.2), ((0, 0.05, 9.4), 1.38, 1.12, 3.2)], axis_up=(0, 1, 0), segs=32, sub=1, name='Band')
loft(T, 'Secondary', [((0, 0.0, 9.45), 1.45, 1.2, 2.6), ((0, 0.25, 10.35), 1.95, 1.6, 2.4), ((0, 0.35, 11.35), 2.1, 1.7, 2.4),
                      ((0, 0.2, 12.2), 1.75, 1.45, 2.6), ((0, 0.0, 12.7), 1.15, 1.0, 2.8)], axis_up=(0, 1, 0), segs=40, name='Chest')
# Spine ridge with radiator fins down the back
loft(T, 'Primary', [((0, -1.2, 9.8), 0.45, 0.4, 3), ((0, -1.75, 11.0), 0.55, 0.5, 3), ((0, -1.45, 12.5), 0.4, 0.4, 3)], axis_up=(0, 1, 0), segs=24, name='Spine')
for i in range(6):
    z = 10.0 + i * 0.42
    block(T, 'Dark', (0, -2.15, z), (0.08, 0.7, 0.3), rot=(-15, 0, 0), bevel=0.02, segs=1, name='Fin')
vent(T, (0, 1.45, 9.6), (1.2, 0.5, 0.25), rot=(-28, 0, 0), slats=4)
# Neck and the hooded sensor head
loft(T, 'Dark', [((0, 0.3, 12.5), 0.55, 0.55, 3), ((0, 0.6, 13.3), 0.5, 0.5, 3)], axis_up=(0, 1, 0), segs=24, sub=2, name='Neck')
for yf in (-1, 1):
    ram(T, (yf * 0.75, 0.0, 12.2), (yf * 0.5, 0.55, 13.2), 0.1)
H = Vector((0, 0.9, 13.55))
loft(T, 'Primary', [(H + Vector((0, -1.0, 0.1)), 0.7, 0.55, 2.8), (H + Vector((0, 0.0, 0.15)), 0.88, 0.62, 2.6),
                    (H + Vector((0, 0.9, 0.05)), 0.8, 0.52, 2.5), (H + Vector((0, 1.35, 0.0)), 0.62, 0.42, 2.4)], axis_up=(0, 0, 1), segs=36, name='Head')
# Hood that overhangs the lens
loft(T, 'Secondary', [(H + Vector((0, -0.6, 0.55)), 0.6, 0.18, 2.4), (H + Vector((0, 0.6, 0.55)), 0.85, 0.2, 2.4), (H + Vector((0, 1.55, 0.42)), 0.7, 0.14, 2.4)],
     axis_up=(0, 0, 1), segs=28, name='Hood')
lens = H + Vector((0, 1.36, -0.02))
lathe(T, 'Dark', lens, (0, 1, 0), [(0, -0.05), (0.42, -0.05), (0.45, 0.08), (0.36, 0.14), (0, 0.14)], segs=36, name='LensRing')
lathe(T, 'Glow', lens, (0, 1, 0), [(0, 0.1), (0.3, 0.1), (0.24, 0.17), (0, 0.19)], segs=36, name='Lens')
for s in (-1, 1):
    lathe(T, 'Glow', H + Vector((s * 0.45, 1.3, -0.25)), (0, 1, 0), [(0, 0.0), (0.08, 0.0), (0.08, 0.08), (0, 0.08)], segs=12, name='Aux')
    plate(T, 'Secondary', H + Vector((s * 0.85, 0.2, -0.05)), (1.4, 0.7, 0.14), rot=(0, 0, s * 96), curve=(0.15, 0.1))
# Thruster pods on the back
for s in (-1, 1):
    pod = Vector((s * 1.0, -1.85, 10.6))
    loft(T, 'Primary', [(pod + Vector((0, 0, 1.4)), 0.42, 0.42, 2.4), (pod + Vector((0, 0, 0.3)), 0.55, 0.55, 2.4), (pod + Vector((0, -0.1, -0.7)), 0.45, 0.45, 2.4)],
         axis_up=(0, 1, 0), segs=28, name='Pod')
    n = jet_nozzle(T, pod + Vector((0, -0.25, -0.95)), (0, -0.3, -1), 0.7)
    R.anchor('Jet' + ('L' if s < 0 else 'R'), n, 'Upper', (0, -0.3, -1))
    tube(T, 'Black', [(s * 0.5, -1.5, 12.2), (s * 1.1, -1.9, 12.0), (s * 1.0, -1.9, 11.9)], 0.09)
R.rigid('Torso', UPPER, 'Upper')

# Chest plates: a steep V prow
for s, nm in ((-1, 'ChestL'), (1, 'ChestR')):
    c = Vector((s * 0.95, 1.75, 10.95))
    Rm = Matrix.Rotation(math.radians(-s * 36), 3, 'Z') @ Matrix.Rotation(math.radians(-10), 3, 'X')
    plate(nm, 'Primary', c, (1.9, 2.8, 0.32), rot=(-10, 0, -s * 36), curve=(0.1, 0.05), bevel=0.08, nx=12, ny=12, taper=0.7)
    block(nm, 'Black', c + Rm @ Vector((0, 0.2, -0.5)), (1.3, 0.05, 0.05), rot=(-10, 0, -s * 36), bevel=0, name='Groove')
    stripe(nm, c + Rm @ Vector((s * 0.35, 0.2, 0.9)), (0.8, 0.05, 0.13), rot=(-10, 0, -s * 36))
    R.rigid(nm, c, 'Upper')

# ---- Arms ----------------------------------------------------------------
for s in (-1, 1):
    arm(R, s, (s * 2.55, 0.1, 11.6), (s * 2.7, 0.3, 9.95), (s * 2.75, 1.4, 9.45), k=0.72, shoulder_r=1.35)
R.rigid('Arms', ARMS, 'Arms')

# Swept shoulder fins (detachable)
for s, nm in ((-1, 'PauldronL'), (1, 'PauldronR')):
    c = Vector((s * 2.75, -0.1, 12.45))
    loft(nm, 'Primary', [(c + Vector((0, 1.3, -0.15)), 0.95, 0.4, 2.2), (c + Vector((s * 0.05, 0.2, 0.1)), 1.15, 0.58, 2.2),
                         (c + Vector((s * 0.1, -1.0, 0.25)), 1.0, 0.5, 2.2), (c + Vector((s * 0.15, -2.0, 0.55)), 0.55, 0.25, 2.2)],
         axis_up=(0, 0, 1), segs=32, name='Fin')
    plate(nm, 'Secondary', c + Vector((s * 1.05, -0.3, -0.5)), (2.6, 1.1, 0.18), rot=(0, s * 14, s * 90), curve=(0.06, 0.06))
    stripe(nm, c + Vector((s * 1.2, -0.3, -0.15)), (0.05, 2.0, 0.13), rot=(0, s * 14, 0))
    R.rigid(nm, c, 'Upper')

# Antenna mast (detachable)
mast = Vector((0.7, -1.1, 12.6))
lathe('Antenna', 'Dark', mast, (0, 0, 1), [(0, 0), (0.18, 0), (0.18, 0.4), (0.07, 0.5), (0.05, 2.6), (0.0, 2.7)], segs=12, name='Mast')
lathe('Antenna', 'Glow', mast + Vector((0, 0, 2.6)), (0, 0, 1), [(0, 0), (0.07, 0), (0, 0.12)], segs=10, name='Tip')
R.rigid('Antenna', mast, 'Upper')

R.anchor('BackL', (-1.5, -1.9, 12.65), 'Upper')
R.anchor('BackR', (1.5, -1.9, 12.65), 'Upper')
R.anchor('Shoulder', (-2.75, -0.2, 13.2), 'Arms')
R.anchor('Smoke', (0, -1.0, 11.3), 'Upper')

for s in (-1, 1):
    biped_leg(R, s, (s * HIP_X, 0, HIP_Z), (s * (HIP_X + FOOT_OUT), 0, ANKLE_Z), THIGH, SHIN, ANKLE_Z, k=K,
              style=dict(guard='ribbed', toes=2, toe_len=2.3, foot_w=0.95, rams=(0.17, 0.13)))

R.extra.update({'thigh': THIGH, 'shin': SHIN, 'ankle': ANKLE_Z, 'footOut': FOOT_OUT, 'upper': list(U(UPPER)), 'arms': list(U(ARMS))})
run(R, os.path.dirname(os.path.abspath(__file__)), 7.2, 44)
