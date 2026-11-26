"""
MAMMOTH - heavy four-legged assault walker (58 t). A long armoured hull
like a tank's, carried on four thick legs: front knees forward, rear knees
back, so the weight sits inside a wide, stable footprint. A crewed cab with
twin weapon arms turns at the front; engine deck and exhausts at the rear.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mechlib import *
from mathutils import Vector, Matrix
from rig import Rig, U, biped_leg, bolts_ring, stripe, run, jet_nozzle, arm

reset()
STAND = 7.2
THIGH, SHIN, ANKLE_Z = 4.4, 4.6, 1.1
UPPER = Vector((0, 2.6, 9.4))
ARMS = Vector((0, 3.0, 10.3))
K = 0.92
R = Rig('Mammoth', STAND)

# ---- Hull -----------------------------------------------------------------
B = 'Hull'
loft(B, 'Dark', [((0, 5.0, 7.2), 1.8, 1.0, 3.6), ((0, 3.8, 7.3), 2.55, 1.45, 3.8), ((0, -2.5, 7.4), 2.65, 1.5, 3.8), ((0, -4.9, 7.5), 2.2, 1.3, 3.6)],
     axis_up=(0, 0, 1), segs=40, name='HullCore')
# Upper hull armour: a long sloped shell
loft(B, 'Secondary', [((0, 5.15, 7.7), 1.6, 0.55, 3), ((0, 3.4, 8.45), 2.45, 0.75, 3.2), ((0, -2.8, 8.55), 2.5, 0.75, 3.2), ((0, -5.0, 8.25), 2.0, 0.6, 3)],
     axis_up=(0, 0, 1), segs=40, name='HullTop')
# Side skirts: three overlapping plates each side
for s in (-1, 1):
    for i, y in enumerate((3.2, 0.0, -3.2)):
        plate(B, 'Primary', (s * 2.75, y, 7.35), (3.4, 1.9, 0.3), rot=(0, s * -12, s * 90), curve=(0.05, 0.12), bevel=0.08, taper=0.92)
        stripe(B, (s * 2.95, y, 8.0), (0.05, 2.6, 0.14), rot=(0, s * -12, 0))
    tube(B, 'Black', [(s * 1.4, -4.2, 9.0), (s * 2.2, -2.0, 8.9), (s * 2.3, 1.5, 8.85), (s * 1.6, 3.4, 9.0)], 0.13)
# Front glacis with lamps
plate(B, 'Primary', (0, 5.25, 7.4), (3.6, 1.8, 0.32), rot=(-35, 0, 0), curve=(0.1, 0.05), taper=0.8)
for s in (-1, 1):
    lamp(B, (s * 1.3, 5.6, 7.0), 0.3, rot=(-20, 0, 0))
# Engine deck: louvres and two exhaust stacks
vent(B, (0, -3.4, 9.05), (2.8, 0.25, 1.6), rot=(90, 0, 0), slats=6)
for s in (-1, 1):
    lathe(B, 'Dark', (s * 1.6, -4.0, 8.8), (0, 0, 1), [(0, 0), (0.42, 0), (0.42, 0.2), (0.34, 0.25), (0.34, 1.8), (0.42, 1.85), (0.42, 2.0), (0.3, 2.0), (0, 1.9)], segs=24, name='Stack')
    lathe(B, 'Hot', (s * 1.6, -4.0, 10.75), (0, 0, 1), [(0, 0), (0.3, 0), (0, 0.01)], segs=20, name='StackGlow')
    n = jet_nozzle(B, (s * 1.6, -1.0, 6.3), (0, 0, -1), 0.75)
    R.anchor('Jet' + ('L' if s < 0 else 'R'), n, 'Root', (0, 0, -1))
plate(B, 'Primary', (0, -5.2, 7.6), (4.0, 2.0, 0.34), rot=(25, 0, 180), curve=(0.08, 0.05), taper=0.85)
vent(B, (0, -5.5, 7.6), (2.4, 0.2, 1.0), rot=(25, 0, 180), slats=4)
# Hip sockets
corners = [(-1, 1), (1, -1), (1, 1), (-1, -1)]
for sx, sy in corners:
    hp = Vector((sx * 2.55, sy * 3.4, 6.8))
    loft(B, 'Primary', [(hp + Vector((sx * 0.2, -1.2, 1.0)), 0.8, 0.45, 2.4), (hp + Vector((sx * 0.35, 0.0, 1.25)), 1.05, 0.6, 2.5), (hp + Vector((sx * 0.2, 1.2, 1.0)), 0.8, 0.45, 2.4)],
         axis_up=(0, 0, 1), segs=28, name='HipCowl')
    disc_joint(B, hp + Vector((sx * 0.1, 0, 0)), (sx, 0, 0), 1.25 * K, 0.95, bolts=10)
lathe(B, 'Dark', (0, UPPER.y, 9.0), (0, 0, 1), [(0, 0), (1.85, 0), (1.9, 0.08), (1.9, 0.38), (0, 0.38)], segs=44, name='RingBase')
bolts_ring(B, (0, UPPER.y, 9.38), (0, 0, 1), 1.65, 18)
R.rigid('Hull', (0, 0, STAND), 'Root')

# ---- Cab ------------------------------------------------------------------
T = 'Torso'
lathe(T, 'Metal', UPPER, (0, 0, 1), [(0, 0), (1.75, 0), (1.8, 0.06), (1.8, 0.3), (0, 0.3)], segs=44, name='RingTop')
c0 = UPPER
loft(T, 'Secondary', [(c0 + Vector((0, -1.7, 0.95)), 1.6, 0.8, 3.4), (c0 + Vector((0, -0.4, 1.05)), 1.8, 0.95, 3.4), (c0 + Vector((0, 1.0, 0.95)), 1.6, 0.85, 3.2),
                      (c0 + Vector((0, 1.75, 0.7)), 1.2, 0.55, 3.0)], axis_up=(0, 0, 1), segs=40, name='Cab')
loft(T, 'Primary', [(c0 + Vector((0, -1.5, 1.85)), 1.4, 0.25, 2.8), (c0 + Vector((0, 0.3, 1.95)), 1.55, 0.3, 2.8), (c0 + Vector((0, 1.5, 1.6)), 1.15, 0.22, 2.6)],
     axis_up=(0, 0, 1), segs=36, name='CabRoof')
# Lit vision slit wrapping the front of the cab
loft(T, 'Black', [(c0 + Vector((0, 0.6, 1.15)), 1.72, 0.25, 3.2), (c0 + Vector((0, 1.3, 1.0)), 1.45, 0.2, 3.0), (c0 + Vector((0, 1.82, 0.82)), 1.05, 0.14, 2.8)],
     axis_up=(0, 0, 1), segs=36, sub=2, cap=(False, True), name='SlitFrame')
loft(T, 'Glow', [(c0 + Vector((0, 0.7, 1.15)), 1.75, 0.1, 3.2), (c0 + Vector((0, 1.32, 1.0)), 1.48, 0.08, 3.0), (c0 + Vector((0, 1.85, 0.82)), 1.08, 0.06, 2.8)],
     axis_up=(0, 0, 1), segs=36, sub=2, cap=(False, True), name='Slit')
for s in (-1, 1):
    vent(T, c0 + Vector((s * 1.75, -1.0, 0.95)), (1.4, 0.25, 0.55), rot=(0, 0, s * 90), slats=3)
block(T, 'Dark', c0 + Vector((-0.5, -1.0, 2.2)), (0.9, 0.9, 0.12), bevel=0.04, name='Hatch')
R.rigid('Torso', UPPER, 'Upper')

# ---- Weapon arms ------------------------------------------------------------
for s in (-1, 1):
    arm(R, s, (s * 2.2, ARMS.y, ARMS.z), (s * 2.35, ARMS.y + 0.4, ARMS.z - 0.6), (s * 2.4, 4.45, 10.2), k=0.7, shoulder_r=1.2)
R.rigid('Arms', ARMS, 'Arms')

# ---- Detachable deck plates and antenna ---------------------------------------
for s, nm in ((-1, 'ChestL'), (1, 'ChestR')):
    c = Vector((s * 1.4, -0.4, 9.25))
    plate(nm, 'Primary', c, (2.2, 3.6, 0.3), rot=(90, -s * 10, 0), curve=(0.1, 0.02), bevel=0.08, taper=0.9)
    stripe(nm, c + Vector((s * 0.4, 0, 0.18)), (0.6, 2.6, 0.05), rot=(0, -s * 10, 0))
    R.rigid(nm, c, 'Root')
mast = Vector((-1.6, -2.4, 9.1))
lathe('Antenna', 'Dark', mast, (0, 0, 1), [(0, 0), (0.2, 0), (0.2, 0.35), (0.07, 0.45), (0.05, 2.8), (0.0, 2.9)], segs=12, name='Mast')
lathe('Antenna', 'Glow', mast + Vector((0, 0, 2.8)), (0, 0, 1), [(0, 0), (0.07, 0), (0, 0.12)], segs=10, name='Tip')
R.rigid('Antenna', mast, 'Root')

R.anchor('BackL', (-1.3, -2.2, 9.5), 'Root')
R.anchor('BackR', (1.3, -2.2, 9.5), 'Root')
R.anchor('Shoulder', (0, 2.4, 11.85), 'Arms')
R.anchor('Smoke', (0, -2.8, 9.6), 'Root')

for sx, sy in corners:
    tag = ('F' if sy > 0 else 'B') + ('L' if sx < 0 else 'R')
    hp = Vector((sx * 2.55, sy * 3.4, 6.8))
    ank = hp + Vector((sx * 3.0, sy * 3.6, 0)); ank.z = ANKLE_Z
    biped_leg(R, sx, hp, ank, THIGH, SHIN, ANKLE_Z, k=K, style=dict(guard='blade', toes=3, toe_len=2.2, rams=(0.19, 0.15)), tag=tag, flip=sy < 0)

R.extra.update({'thigh': THIGH, 'shin': SHIN, 'ankle': ANKLE_Z, 'footOut': 0, 'upper': list(U(UPPER)), 'arms': list(U(ARMS))})
run(R, os.path.dirname(os.path.abspath(__file__)), 6.0, 46)
