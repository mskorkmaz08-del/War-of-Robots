"""
ARACHNE - four-legged spider walker (36 t). A low armoured thorax and a
segmented abdomen slung between four long legs with high knees, so the body
stays level on any ground. The weapons ride a domed sensor turret at the front
with a cluster of lit lenses.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mechlib import *
from mathutils import Vector, Matrix
from rig import Rig, U, spider_leg, bolts_ring, stripe, run, jet_nozzle

reset()
STAND = 6.5
THIGH, SHIN, ANKLE_Z = 6.0, 6.8, 0.9
UPPER = Vector((0, 1.0, 8.1))     # the head turret (yaw)
ARMS = Vector((0, 1.6, 8.8))      # the gun cradle (pitch)
R = Rig('Arachne', STAND)

# ---- Body ------------------------------------------------------------------
B = 'Hull'
loft(B, 'Secondary', [((0, 3.1, 6.9), 1.5, 0.95, 3.0), ((0, 1.8, 7.0), 2.45, 1.35, 3.2), ((0, 0.0, 7.05), 2.75, 1.45, 3.2),
                      ((0, -1.5, 7.1), 2.45, 1.4, 3.0)], axis_up=(0, 0, 1), segs=40, name='Thorax')
# Segmented abdomen: overlapping cast bands
for i in range(4):
    y = -1.9 - i * 0.78
    w = 2.55 - i * 0.32; d = 1.6 - i * 0.16
    loft(B, 'Primary' if i % 2 == 0 else 'Secondary', [((0, y + 0.05, 7.25 + i * 0.05), w, d, 2.6), ((0, y - 0.62, 7.25 + i * 0.05), w * 0.93, d * 0.94, 2.6)],
         axis_up=(0, 0, 1), segs=36, sub=1, name='Segment')
loft(B, 'Dark', [((0, -4.6, 7.4), 1.15, 0.85, 2.6), ((0, -5.05, 7.4), 0.6, 0.45, 2.6)], axis_up=(0, 0, 1), segs=28, sub=2, name='Tail')
lamp(B, (0, -5.1, 7.6), 0.25, rot=(0, 0, 180))
# Top deck plates over the thorax
for s in (-1, 1):
    plate(B, 'Primary', (s * 1.05, 0.6, 8.35), (2.0, 3.4, 0.24), rot=(90, -s * 14, 0), curve=(0.12, 0.02), taper=0.85)
    vent(B, (s * 2.6, 0.2, 6.6), (1.8, 0.3, 0.6), rot=(0, 0, s * 90), slats=3)
    tube(B, 'Black', [(s * 1.6, -1.2, 8.1), (s * 2.3, -0.4, 7.8), (s * 2.6, 0.9, 7.0)], 0.12)
# Belly: armour keel and lamps
loft(B, 'Dark', [((0, 2.6, 6.2), 1.0, 0.4, 3), ((0, -1.6, 6.0), 1.5, 0.5, 3), ((0, -3.6, 6.3), 1.0, 0.4, 3)], axis_up=(0, 0, 1), segs=28, name='Keel')
for s in (-1, 1):
    n = jet_nozzle(B, (s * 1.2, -3.0, 6.1), (0, 0, -1), 0.65)
    R.anchor('Jet' + ('L' if s < 0 else 'R'), n, 'Root', (0, 0, -1))
# Hip sockets
corners = [(-1, 1), (1, -1), (1, 1), (-1, -1)]
for sx, sy in corners:
    hp = Vector((sx * 2.6, sy * 1.8 - 0.4, 6.5))
    lathe(B, 'Dark', hp - Vector((sx * 0.4, 0, 0)), (sx, 0, 0), [(0, 0), (1.05, 0), (1.1, 0.2), (0.95, 0.5), (0, 0.5)], segs=32, name='Socket')
    bolts_ring(B, hp + Vector((sx * 0.12, 0, 0)), (sx, 0, 0), 0.85, 10, 0.06)
    loft(B, 'Primary', [(hp + Vector((sx * -0.1, 0.9, 0.85)), 0.75, 0.25, 2.4), (hp + Vector((sx * 0.2, 0.0, 1.05)), 0.95, 0.35, 2.4), (hp + Vector((sx * -0.1, -0.9, 0.85)), 0.75, 0.25, 2.4)],
         axis_up=(0, 0, 1), segs=24, name='SocketCowl')
lathe(B, 'Dark', (0, 1.0, 7.85), (0, 0, 1), [(0, 0), (1.75, 0), (1.8, 0.08), (1.8, 0.3), (0, 0.3)], segs=44, name='RingBase')
R.rigid('Hull', (0, 0, STAND), 'Root')

# ---- Head turret --------------------------------------------------------------
T = 'Torso'
lathe(T, 'Metal', UPPER + Vector((0, 0, 0.05)), (0, 0, 1), [(0, 0), (1.65, 0), (1.7, 0.06), (1.7, 0.25), (0, 0.25)], segs=44, name='RingTop')
loft(T, 'Secondary', [(UPPER + Vector((0, -1.5, 0.75)), 1.3, 0.75, 2.4), (UPPER + Vector((0, -0.3, 0.85)), 1.75, 0.95, 2.4),
                      (UPPER + Vector((0, 0.9, 0.7)), 1.55, 0.85, 2.3), (UPPER + Vector((0, 1.7, 0.45)), 0.95, 0.55, 2.2)], axis_up=(0, 0, 1), segs=40, name='Dome')
loft(T, 'Primary', [(UPPER + Vector((0, -1.2, 1.45)), 0.85, 0.2, 2.4), (UPPER + Vector((0, 0.2, 1.62)), 1.05, 0.25, 2.4), (UPPER + Vector((0, 1.2, 1.35)), 0.8, 0.18, 2.4)],
     axis_up=(0, 0, 1), segs=32, name='DomeTop')
# Lens cluster: two big main eyes, four small
eyes = [(-0.42, 0.62, 0.22), (0.42, 0.62, 0.22), (-0.85, 0.35, 0.12), (0.85, 0.35, 0.12), (-0.25, 0.25, 0.1), (0.25, 0.25, 0.1)]
for x, z, r in eyes:
    e = UPPER + Vector((x, 1.62 - abs(x) * 0.35, z))
    lathe(T, 'Dark', e, (x * 0.4, 1, 0), [(0, -0.08), (r * 1.3, -0.08), (r * 1.35, 0.08), (0, 0.1)], segs=20, name='EyeRing')
    lathe(T, 'Glow', e, (x * 0.4, 1, 0), [(0, 0.06), (r, 0.06), (r * 0.7, 0.13), (0, 0.14)], segs=20, name='Eye')
for s in (-1, 1):
    vent(T, UPPER + Vector((s * 1.55, -0.4, 0.55)), (1.2, 0.25, 0.4), rot=(0, 0, s * 90), slats=3)
R.rigid('Torso', UPPER, 'Upper')

# ---- Gun cradle (pitch) ----------------------------------------------------------
A = 'Arms'
lathe(A, 'Dark', ARMS + Vector((-1.95, 0.4, 0)), (1, 0, 0), [(0, 0), (0.42, 0), (0.42, 3.9), (0, 3.9)], segs=24, name='Axle')
for s in (-1, 1):
    m = Vector((s * 1.75, 2.1, 8.8))
    disc_joint(A, Vector((s * 1.75, ARMS.y + 0.4, ARMS.z)), (s, 0, 0), 0.7, 0.8, bolts=8)
    loft(A, 'Secondary', [(m + Vector((0, -1.0, 0)), 0.6, 0.65, 3.2), (m + Vector((0, -0.1, 0)), 0.7, 0.72, 3.4), (m + Vector((0, 0.25, 0)), 0.6, 0.6, 3.2)],
         axis_up=(0, 0, 1), segs=28, name='Cradle')
    lathe(A, 'Dark', m + Vector((0, 0.15, 0)), (0, 1, 0), [(0.45, 0), (0.72, 0), (0.72, 0.3), (0.45, 0.3)], segs=28, name='CradleRing')
    R.anchor('Mount' + ('L' if s < 0 else 'R'), m, 'Arms')
R.rigid('Arms', ARMS, 'Arms')

# ---- Detachable parts -----------------------------------------------------------
for s, nm in ((-1, 'ChestL'), (1, 'ChestR')):
    c = Vector((s * 2.85, 1.0, 7.45))
    plate(nm, 'Primary', c, (3.0, 1.6, 0.3), rot=(0, s * -20, s * 90), curve=(0.06, 0.12), bevel=0.08, taper=0.85)
    stripe(nm, c + Vector((s * 0.18, 0, 0.35)), (0.05, 2.2, 0.14), rot=(0, s * -20, 0))
    R.rigid(nm, c, 'Root')
mast = Vector((1.4, -1.2, 8.35))
lathe('Antenna', 'Dark', mast, (0, 0, 1), [(0, 0), (0.2, 0), (0.2, 0.35), (0.07, 0.45), (0.05, 2.4), (0.0, 2.5)], segs=12, name='Mast')
lathe('Antenna', 'Glow', mast + Vector((0, 0, 2.4)), (0, 0, 1), [(0, 0), (0.07, 0), (0, 0.12)], segs=10, name='Tip')
R.rigid('Antenna', mast, 'Root')

R.anchor('Back', (0, -3.0, 8.95), 'Root')
R.anchor('Shoulder', (0, 1.4, 9.65), 'Arms')
R.anchor('Smoke', (0, -2.4, 8.7), 'Root')

for sx, sy in corners:
    tag = ('F' if sy > 0 else 'B') + ('L' if sx < 0 else 'R')
    hp = Vector((sx * 2.6, sy * 1.8 - 0.4, 6.5))
    ank = hp + Vector((sx * 4.6, sy * 3.0, 0)); ank.z = ANKLE_Z
    spider_leg(R, tag, hp, ank, THIGH, SHIN, ANKLE_Z, (sx * 0.5, sy * 0.3, 1), k=1.0, claws=3)

R.extra.update({'thigh': THIGH, 'shin': SHIN, 'ankle': ANKLE_Z, 'footOut': 0, 'upper': list(U(UPPER)), 'arms': list(U(ARMS))})
run(R, os.path.dirname(os.path.abspath(__file__)), 5.0, 44)
