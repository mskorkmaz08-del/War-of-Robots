"""
VANGUARD - medium all-round walker (38 t). A broad V-shaped chest with big
intakes, a twin-eyed head under a heavy brow, rounded shoulder armour and a
jump pack with two large thrusters: the one that leaps into the fight.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mechlib import *
from mathutils import Vector, Matrix
from rig import Rig, U, biped_leg, bolts_ring, stripe, run, pelvis, arm, jet_nozzle

reset()
STAND = 7.8
HIP_X, HIP_Z = 2.35, 7.45
THIGH, SHIN, ANKLE_Z, FOOT_OUT = 3.85, 4.05, 1.15, 0.6
UPPER = Vector((0, 0, 9.0))
ARMS = Vector((0, 0.1, 12.1))
K = 0.86
R = Rig('Vanguard', STAND)

pelvis(R, HIP_X, HIP_Z, k=K, ring_z=8.4, ring_r=1.7)

T = 'Torso'
lathe(T, 'Metal', (0, 0, 8.9), (0, 0, 1), [(0, 0), (1.6, 0), (1.65, 0.06), (1.65, 0.3), (1.5, 0.36), (0, 0.36)], segs=44, name='RingTop')
for i, z in enumerate((9.25, 9.85)):
    loft(T, 'Dark', [((0, 0.05, z), 1.45 + i * 0.2, 1.2 + i * 0.12, 3.5), ((0, 0.1, z + 0.55), 1.6 + i * 0.25, 1.3 + i * 0.15, 3.5)], axis_up=(0, 1, 0), segs=36, sub=1, name='Abdomen')
    loft(T, 'Secondary', [((0, 0.1, z + 0.15), 1.65 + i * 0.25, 1.35 + i * 0.15, 3.5), ((0, 0.1, z + 0.43), 1.7 + i * 0.25, 1.38 + i * 0.15, 3.5)], axis_up=(0, 1, 0), segs=36, sub=1, name='Band')
# A V-shaped chest: narrow waist flaring to broad shoulders
loft(T, 'Secondary', [((0, 0.05, 10.3), 1.75, 1.45, 3.0), ((0, 0.2, 11.2), 2.55, 1.85, 3.0), ((0, 0.3, 12.3), 3.0, 2.0, 3.2),
                      ((0, 0.2, 13.2), 2.75, 1.85, 3.3), ((0, 0.0, 13.65), 2.0, 1.45, 3.4)], axis_up=(0, 1, 0), segs=40, name='Chest')
loft(T, 'Primary', [((0, 0.45, 13.3), 1.5, 1.15, 3.0), ((0, 0.45, 13.75), 1.6, 1.25, 3.0), ((0, 0.4, 13.95), 1.35, 1.05, 3.0)],
     axis_up=(0, 1, 0), segs=36, sub=2, cap=(False, True), name='Collar')
# Side intakes
for s in (-1, 1):
    loft(T, 'Dark', [((s * 2.5, 0.9, 11.0), 0.45, 0.6, 4), ((s * 2.55, 1.5, 11.0), 0.5, 0.65, 4)], axis_up=(0, 0, 1), segs=24, sub=1, name='Intake')
    vent(T, (s * 2.55, 1.55, 11.0), (0.85, 0.12, 1.1), rot=(0, 0, 0), slats=5)
    vent(T, (s * 2.75, -0.6, 11.6), (1.6, 0.3, 1.1), rot=(0, 0, s * 90), slats=5)
# Head: twin eyes under a heavy brow
H = Vector((0, 1.4, 14.55))
loft(T, 'Primary', [(H + Vector((0, -0.9, 0.05)), 1.05, 0.7, 3.2), (H + Vector((0, 0.0, 0.1)), 1.15, 0.75, 3.0),
                    (H + Vector((0, 0.85, -0.05)), 0.95, 0.58, 2.8), (H + Vector((0, 1.25, -0.15)), 0.65, 0.35, 2.6)], axis_up=(0, 0, 1), segs=36, name='Head')
loft(T, 'Secondary', [(H + Vector((0, -0.4, 0.62)), 0.95, 0.2, 2.6), (H + Vector((0, 0.7, 0.55)), 1.05, 0.22, 2.6), (H + Vector((0, 1.45, 0.32)), 0.8, 0.14, 2.6)],
     axis_up=(0, 0, 1), segs=32, name='Brow')
for s in (-1, 1):
    e = H + Vector((s * 0.42, 1.12, 0.1))
    lathe(T, 'Dark', e, (0, 1, 0), [(0, -0.1), (0.3, -0.1), (0.32, 0.12), (0.24, 0.16), (0, 0.16)], segs=28, name='EyeRing')
    lathe(T, 'Glow', e, (0, 1, 0), [(0, 0.1), (0.2, 0.1), (0.15, 0.17), (0, 0.18)], segs=28, name='Eye')
    plate(T, 'Secondary', H + Vector((s * 1.12, 0.1, -0.35)), (1.5, 0.75, 0.16), rot=(0, 0, s * 96), curve=(0.15, 0.1))
block(T, 'Dark', H + Vector((0, 1.2, -0.35)), (0.7, 0.3, 0.3), bevel=0.08, name='Jaw')
vent(T, H + Vector((0, 1.35, -0.35)), (0.55, 0.1, 0.22), slats=3)
# Jump pack: two big thrusters on a saddle
loft(T, 'Primary', [((0, -1.5, 12.8), 1.9, 0.9, 3.5), ((0, -2.2, 12.3), 1.8, 0.85, 3.5), ((0, -2.4, 11.4), 1.4, 0.7, 3.5)], axis_up=(0, 1, 0), segs=36, name='Pack')
for s in (-1, 1):
    tc = Vector((s * 1.35, -2.55, 10.8))
    loft(T, 'Secondary', [(tc + Vector((0, 0, 2.1)), 0.55, 0.55, 2.2), (tc + Vector((0, 0, 0.9)), 0.72, 0.72, 2.2), (tc + Vector((0, -0.05, -0.2)), 0.62, 0.62, 2.2)],
         axis_up=(0, 1, 0), segs=32, name='Thruster')
    for kb in range(3):
        lathe(T, 'Metal', tc + Vector((0, 0, 0.35 + kb * 0.45)), (0, 0, 1), [(0.7, 0), (0.75, 0.02), (0.75, 0.12), (0.7, 0.14)], segs=32, name='Band')
    n = jet_nozzle(T, tc + Vector((0, -0.05, -0.5)), (0, -0.2, -1), 0.9)
    R.anchor('Jet' + ('L' if s < 0 else 'R'), n, 'Upper', (0, -0.2, -1))
    tube(T, 'Black', [(s * 0.6, -2.3, 12.9), (s * 1.3, -2.7, 13.0), (s * 1.35, -2.6, 12.9)], 0.1)
lamp(T, (0, -2.6, 12.6), 0.25, rot=(0, 0, 180))
R.rigid('Torso', UPPER, 'Upper')

for s, nm in ((-1, 'ChestL'), (1, 'ChestR')):
    c = Vector((s * 1.3, 2.05, 12.05))
    Rm = Matrix.Rotation(math.radians(-s * 24), 3, 'Z') @ Matrix.Rotation(math.radians(-6), 3, 'X')
    plate(nm, 'Primary', c, (2.4, 2.9, 0.38), rot=(-6, 0, -s * 24), curve=(0.12, 0.06), bevel=0.09, nx=12, ny=12, taper=0.72)
    block(nm, 'Black', c + Rm @ Vector((0, 0.22, -0.55)), (1.7, 0.05, 0.05), rot=(-6, 0, -s * 24), bevel=0, name='Groove')
    stripe(nm, c + Rm @ Vector((s * 0.4, 0.22, 1.0)), (1.0, 0.05, 0.14), rot=(-6, 0, -s * 24))
    lamp(nm, c + Rm @ Vector((-s * 0.55, 0.25, 0.35)), 0.2, rot=(-6, 0, -s * 24))
    R.rigid(nm, c, 'Upper')

for s in (-1, 1):
    arm(R, s, (s * 3.25, 0.1, 12.1), (s * 3.3, 0.35, 10.2), (s * 3.3, 1.55, 9.75), k=0.82, shoulder_r=1.35)
R.rigid('Arms', ARMS, 'Arms')

for s, nm in ((-1, 'PauldronL'), (1, 'PauldronR')):
    c = Vector((s * 3.4, 0.1, 13.3))
    loft(nm, 'Primary', [(c + Vector((s * 0.1, -1.6, -0.3)), 1.15, 0.5, 2.2), (c + Vector((s * 0.15, -0.7, 0.05)), 1.4, 0.78, 2.2),
                         (c + Vector((s * 0.15, 0.5, 0.05)), 1.42, 0.8, 2.2), (c + Vector((s * 0.1, 1.55, -0.3)), 1.1, 0.5, 2.2)], axis_up=(0, 0, 1), segs=36, name='Pauldron')
    plate(nm, 'Secondary', c + Vector((s * 1.45, 0.0, -0.85)), (2.9, 1.5, 0.22), rot=(0, s * 12, s * 90), curve=(0.07, 0.07), taper=1.1)
    stripe(nm, c + Vector((s * 1.62, 0.0, -0.4)), (0.05, 2.3, 0.15), rot=(0, s * 12, 0))
    for kk in range(3):
        block(nm, 'Black', c + Vector((s * 0.6, -0.8 + kk * 0.55, 0.72)), (0.8, 0.05, 0.05), rot=(0, s * 30, 0), bevel=0, name='Groove')
    R.rigid(nm, c, 'Upper')

R.anchor('BackL', (-1.7, -1.6, 13.45), 'Upper')
R.anchor('BackR', (1.7, -1.6, 13.45), 'Upper')
R.anchor('Shoulder', (3.3, -0.2, 14.2), 'Arms')
R.anchor('Smoke', (0, -1.2, 12.0), 'Upper')

for s in (-1, 1):
    biped_leg(R, s, (s * HIP_X, 0, HIP_Z), (s * (HIP_X + FOOT_OUT), 0, ANKLE_Z), THIGH, SHIN, ANKLE_Z, k=K,
              style=dict(guard='blade', toes=3, toe_len=2.2, rams=(0.18, 0.14)))

R.extra.update({'thigh': THIGH, 'shin': SHIN, 'ankle': ANKLE_Z, 'footOut': FOOT_OUT, 'upper': list(U(UPPER)), 'arms': list(U(ARMS))})
run(R, os.path.dirname(os.path.abspath(__file__)), 7.5, 44)
