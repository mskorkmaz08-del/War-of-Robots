"""
TITAN - 15 m heavy assault walker, modelled from scratch.

Design notes (engineering research, see Tools/mechs/README.md):
  * Square-cube law: a ~60 t machine needs short, thick legs, a low hip and a
    wide stance; the knees stay bent so the hip rams never lock straight.
  * Hip torque is the hardest load, so the hips are huge drum joints braced
    into a cast pelvis. Paired hydraulic rams cross the knee and ankle.
  * Heel strike is 3-4x body weight: big three-toed feet with rubber pads
    and a shock ram from shin to foot.
  * Upper body turns on a slewing ring like a crane; reactor, radiators and
    exhausts sit at the back; the crew sits in a low armoured head with a
    lit visor, sunk between the shoulders.
"""
import sys, os, math, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mechlib import *
import mechlib as ml
from mathutils import Vector, Matrix


reset()

# ---------------------------------------------------------------------------
# Game skeleton (Blender: X right, Y forward, Z up, ground z = 0)
# ---------------------------------------------------------------------------
STAND = 8.5                       # root height (Unity transform)
ROOT = Vector((0, 0, STAND))
HIP_X, HIP_Z = 2.75, 8.0
THIGH, SHIN = 4.0, 4.1
ANKLE_Z = 1.3
FOOT_OUT = 0.8                    # feet rest this far outside the hips
UPPER = Vector((0, 0, 9.9))
ARMS = Vector((0, 0.2, 13.4))
MOUNT = lambda s: Vector((s * 4.0, 1.8, 10.65))

from rig import Rig, U, biped_leg, bolts_ring, stripe, run
R = Rig('Titan', STAND)
frames, anchors, parents = R.frames, R.anchors, R.parents


# ===========================================================================
# PELVIS (root frame)
# ===========================================================================
P = 'Hull'
loft(P, 'Dark', [((-2.15, 0, 8.25), 0.9, 0.75, 3), ((-1.3, 0, 8.3), 1.35, 1.0, 3.2), ((0, 0, 8.35), 1.5, 1.1, 3.4),
                 ((1.3, 0, 8.3), 1.35, 1.0, 3.2), ((2.15, 0, 8.25), 0.9, 0.75, 3)], axis_up=(0, 0, 1), segs=32, name='PelvisCore')
# Cast pelvis shell: wraps the core, swept forward like a cuirass skirt
loft(P, 'Secondary', [((-1.7, 0.05, 8.6), 1.25, 0.6, 2.6), ((0, 0.15, 8.75), 1.55, 0.75, 2.8), ((1.7, 0.05, 8.6), 1.25, 0.6, 2.6)],
     axis_up=(0, 0, 1), segs=32, name='PelvisCast')
for s in (-1, 1):
    hip = Vector((s * HIP_X, 0, HIP_Z))
    # Hip yoke: a forged bracket from the pelvis to the hip drum
    block(P, 'Secondary', (s * 2.05, 0, 8.15), (0.7, 1.9, 1.5), bevel=0.22, segs=3, name='Yoke')
    disc_joint(P, hip + Vector((s * 0.15, 0, 0)), (s, 0, 0), 1.3, 1.0, bolts=12)
    # Hip actuators: a pair of rams from the pelvis into the hip drum
    for zf in (-1, 1):
        ram(P, (s * 1.2, zf * 1.05, 9.05), (s * 2.2, zf * 0.75, 8.6), 0.17)
    # Armoured hip cowl over the drum (cast, swept back)
    loft(P, 'Primary', [((s * 2.85, 1.15, 8.65), 0.85, 0.55, 2.4), ((s * 2.95, 0.0, 9.0), 1.0, 0.7, 2.6),
                        ((s * 2.85, -1.25, 8.7), 0.8, 0.5, 2.4)], axis_up=(0, 0, 1), segs=28, name='HipCowl')
    stripe(P, (s * 3.6, 0.1, 9.05), (0.06, 1.4, 0.16), rot=(0, s * 35, 0))
    tube(P, 'Black', [(s * 0.9, -1.1, 8.0), (s * 1.6, -1.5, 7.7), (s * 2.4, -1.2, 7.4), (s * 2.9, -0.6, 7.5)], 0.11)
# Groin armour and rear skirt
plate(P, 'Primary', (0, 1.25, 7.55), (2.1, 1.7, 0.28), rot=(-14, 0, 0), curve=(0.12, 0.05), taper=0.7)
plate(P, 'Primary', (0, -1.25, 7.7), (2.3, 1.4, 0.26), rot=(14, 0, 180), curve=(0.12, 0.05), taper=0.75)
block(P, 'Black', (0, 1.42, 7.75), (1.1, 0.05, 0.06), rot=(-14, 0, 0), bevel=0, name='Groove')
# Fixed half of the slewing ring
lathe(P, 'Dark', (0, 0, 9.05), (0, 0, 1), [(0, 0), (1.95, 0), (2.0, 0.08), (2.0, 0.4), (1.85, 0.48), (0, 0.48)], segs=48, name='RingBase')
bolts_ring(P, (0, 0, 9.53), (0, 0, 1), 1.75, 20)

frames['Hull'] = frame_identity(U(ROOT))

# ===========================================================================
# TORSO (turns on the ring; Upper frame)
# ===========================================================================
T = 'Torso'
lathe(T, 'Metal', (0, 0, 9.55), (0, 0, 1), [(0, 0), (1.8, 0), (1.85, 0.06), (1.85, 0.3), (1.7, 0.36), (0, 0.36)], segs=48, name='RingTop')
# Armoured abdomen: two banded segments that let the chest flex
for i, z in enumerate((10.05, 10.75)):
    loft(T, 'Dark', [((0, 0.05, z), 1.75 + i * 0.25, 1.4 + i * 0.15, 3.5), ((0, 0.1, z + 0.62), 1.9 + i * 0.3, 1.5 + i * 0.2, 3.5)],
         axis_up=(0, 1, 0), segs=36, sub=1, name='Abdomen')
    loft(T, 'Secondary', [((0, 0.1, z + 0.18), 1.95 + i * 0.3, 1.55 + i * 0.2, 3.5), ((0, 0.12, z + 0.5), 2.0 + i * 0.3, 1.6 + i * 0.2, 3.5)],
         axis_up=(0, 1, 0), segs=36, sub=1, name='Band')
# The chest: one big cast hull, broad at the shoulders, deep and rounded
loft(T, 'Secondary', [((0, 0.05, 11.35), 2.25, 1.65, 3.0), ((0, 0.12, 12.3), 2.95, 2.05, 3.1), ((0, 0.2, 13.5), 3.3, 2.25, 3.2),
                      ((0, 0.15, 14.6), 3.05, 2.1, 3.3), ((0, 0.0, 15.25), 2.3, 1.7, 3.4)], axis_up=(0, 1, 0), segs=40, name='Chest')
# Collar: a raised armoured ring the head sits down into
loft(T, 'Primary', [((0, 0.6, 15.0), 1.75, 1.35, 3.0), ((0, 0.6, 15.45), 1.85, 1.45, 3.0), ((0, 0.55, 15.65), 1.6, 1.25, 3.0)],
     axis_up=(0, 1, 0), segs=36, sub=2, cap=(False, True), name='Collar')
# Sternum keel and a hazard band
loft(T, 'Primary', [((0, 2.05, 11.6), 0.55, 0.3, 3), ((0, 2.35, 12.6), 0.75, 0.38, 3), ((0, 2.42, 13.8), 0.8, 0.4, 3), ((0, 2.15, 14.8), 0.6, 0.3, 3)],
     axis_up=(0, 1, 0), segs=24, name='Keel')
for z in (12.2, 13.0, 13.8):
    block(T, 'Black', (0, 2.78, z), (0.9, 0.06, 0.05), bevel=0, name='Groove')
vent(T, (0, 1.75, 11.15), (1.6, 0.55, 0.3), rot=(-25, 0, 0), slats=4)
# Head: low armoured cockpit with a wrap-around lit visor
H = Vector((0, 1.5, 15.9))
HS = [(-1.0, 1.35, 0.9, 3.2, 0.05), (0.0, 1.45, 0.95, 3.0, 0.1), (0.95, 1.25, 0.72, 2.8, -0.05), (1.45, 0.85, 0.42, 2.6, -0.15)]
loft(T, 'Primary', [(H + Vector((0, y, z)), w, d, e) for y, w, d, e, z in HS], axis_up=(0, 0, 1), segs=36, name='Head')
VS = [(0.25, 1.43), (1.0, 1.23), (1.42, 0.9)]
def hz(y):
    return 0.08 - 0.15 * max(0.0, y - 0.3)
loft(T, 'Black', [(H + Vector((0, y, hz(y))), w + 0.04, 0.3, 3.0) for y, w in VS], axis_up=(0, 0, 1), segs=36, sub=2, cap=(False, True), name='VisorFrame')
loft(T, 'Glow', [(H + Vector((0, y + 0.02, hz(y))), w + 0.07, 0.13, 3.0) for y, w in VS], axis_up=(0, 0, 1), segs=36, sub=2, cap=(False, True), name='Visor')
# Brow crest and cheek armour
loft(T, 'Secondary', [(H + Vector((0, -0.9, 0.85)), 0.28, 0.14, 2.5), (H + Vector((0, 0.4, 0.95)), 0.34, 0.18, 2.5), (H + Vector((0, 1.25, 0.6)), 0.22, 0.1, 2.5)],
     axis_up=(0, 0, 1), segs=16, name='Crest')
for s in (-1, 1):
    plate(T, 'Secondary', H + Vector((s * 1.38, 0.1, -0.45)), (1.7, 0.85, 0.2), rot=(0, 0, s * 90 + s * 8), curve=(0.15, 0.1))
    lathe(T, 'Dark', H + Vector((s * 1.5, -0.3, 0.1)), (s, 0, 0), [(0, 0), (0.35, 0), (0.35, 0.18), (0.2, 0.25), (0, 0.25)], segs=20, name='EarCap')

# Back: reactor housing, radiator, exhaust stacks
loft(T, 'Primary', [((0, -1.6, 13.2), 2.0, 1.75, 4.0), ((0, -2.35, 13.25), 1.9, 1.7, 4.0), ((0, -2.85, 13.2), 1.6, 1.45, 4.0)],
     axis_up=(0, 0, 1), segs=36, name='Reactor')
lathe(T, 'Hot', (0, -2.85, 13.6), (0, -1, 0), [(0, 0.04), (0.8, 0.04), (0.8, 0.08), (0, 0.08)], segs=40, name='Core')
lathe(T, 'Metal', (0, -2.85, 13.6), (0, -1, 0), [(0, 0.1), (0.3, 0.1), (0.3, 0.32), (0.2, 0.4), (0, 0.42)], segs=24, name='FanHub')
for k in range(9):
    a = 2 * math.pi * k / 9
    block(T, 'Dark', (math.cos(a) * 0.55, -2.99, 13.6 + math.sin(a) * 0.55), (0.5, 0.06, 0.16), rot=(0, -math.degrees(a) + 25, 0), bevel=0.01, segs=1, name='Blade')
lathe(T, 'Dark', (0, -2.85, 13.6), (0, -1, 0), [(0.82, 0.0), (1.08, 0.0), (1.08, 0.3), (0.82, 0.3)], segs=40, name='FanGuard')
vent(T, (0, -2.95, 12.25), (2.4, 0.3, 0.9), rot=(0, 0, 180), slats=6)
for s in (-1, 1):
    # Exhaust stacks
    lathe(T, 'Dark', (s * 1.35, -2.4, 14.55), (0, 0, 1), [(0, 0), (0.5, 0), (0.5, 0.25), (0.42, 0.3), (0.42, 2.0), (0.5, 2.05), (0.5, 2.25), (0.36, 2.25), (0.36, 2.0), (0, 2.0)], segs=24, name='Stack')
    lathe(T, 'Hot', (s * 1.35, -2.4, 16.55), (0, 0, 1), [(0, 0), (0.36, 0), (0, 0.01)], segs=24, name='StackGlow')
    for k in range(3):
        lathe(T, 'Metal', (s * 1.35, -2.4, 14.95 + k * 0.45), (0, 0, 1), [(0.43, 0), (0.47, 0.02), (0.47, 0.1), (0.43, 0.12)], segs=24, name='StackBand')
    # Side radiators
    vent(T, (s * 3.2, -0.6, 12.6), (2.0, 0.35, 1.3), rot=(0, 0, s * 90), slats=6)
    # Jump-jet nozzles low on the back
    lathe(T, 'Metal', (s * 1.6, -2.15, 10.9), Vector((0, -0.35, -1)).normalized(),
          [(0, -0.3), (0.55, -0.3), (0.6, 0.1), (0.5, 0.15), (0.75, 0.75), (0.68, 0.8), (0.42, 0.25), (0, 0.25)], segs=28, name='Nozzle')
    block(T, 'Dark', (s * 1.6, -1.9, 11.35), (0.9, 0.8, 0.8), bevel=0.15, segs=2, name='NozzleMount')
    tube(T, 'Black', [(s * 0.8, -2.9, 14.2), (s * 1.9, -2.6, 14.7), (s * 2.9, -1.6, 14.6), (s * 3.4, -0.6, 14.1)], 0.15)
    tube(T, 'Black', [(s * 1.2, -2.6, 12.0), (s * 2.3, -2.2, 11.6), (s * 2.9, -1.4, 11.9)], 0.12)
# Crew access: hatch and rails on the roof deck
block(T, 'Dark', (-0.9, -1.2, 15.35), (1.0, 1.0, 0.12), bevel=0.04, name='Hatch')
for s in (-1, 1):
    tube(T, 'Metal', [(s * 1.5, -2.4, 15.25), (s * 1.5, -2.4, 15.85), (s * 1.5, -0.4, 15.85), (s * 1.5, -0.4, 15.4)], 0.05, segs=6)

frames['Torso'] = frame_identity(U(UPPER))
parents['Torso'] = 'Upper'

# Detachable chest plates (blow off when the hull is badly damaged)
for s, nm in ((-1, 'ChestL'), (1, 'ChestR')):
    c = Vector((s * 1.45, 2.2, 13.25))
    Rm = Matrix.Rotation(math.radians(-s * 27), 3, 'Z') @ Matrix.Rotation(math.radians(-8), 3, 'X')
    plate(nm, 'Primary', c, (2.7, 3.4, 0.42), rot=(-8, 0, -s * 27), curve=(0.12, 0.05), bevel=0.1, nx=12, ny=12, taper=0.8)
    block(nm, 'Black', c + Rm @ Vector((0, 0.24, -0.7)), (1.9, 0.06, 0.05), rot=(-8, 0, -s * 27), bevel=0, name='Groove')
    stripe(nm, c + Rm @ Vector((s * 0.6, 0.24, -1.25)), (0.9, 0.05, 0.16), rot=(-8, 0, -s * 27))
    frames[nm] = frame_identity(U(c)); parents[nm] = 'Upper'

# ===========================================================================
# ARMS (pitch to aim; Arms frame)
# ===========================================================================
A = 'Arms'
for s in (-1, 1):
    sh = Vector((s * 4.05, 0.2, 13.4))
    el = Vector((s * 4.15, 0.45, 10.95))
    disc_joint(A, sh, (s, 0, 0), 1.4, 1.3, bolts=12)
    lathe(A, 'Dark', sh - Vector((s * 0.6, 0, 0)), (-s, 0, 0), [(0, 0), (1.0, 0), (1.0, 0.55), (0, 0.55)], segs=32, name='ShoulderBoss')
    # Upper arm: a tapered cast member, rams down both sides
    loft(A, 'Dark', [(sh + Vector((0, 0, -0.6)), 0.85, 0.9, 3), (sh + Vector((0, 0.05, -1.5)), 0.8, 0.85, 3), (el + Vector((0, 0, 0.45)), 0.7, 0.75, 3)],
         axis_up=(0, 1, 0), segs=28, name='UpperArm')
    plate(A, 'Secondary', sh + Vector((s * 0.66, 0.05, -1.35)), (1.3, 1.8, 0.2), rot=(0, 0, s * 90), curve=(0.2, 0.02))
    for yf in (-1, 1):
        ram(A, sh + Vector((0, yf * 0.72, -0.35)), el + Vector((0, yf * 0.7, 0.35)), 0.16)
    disc_joint(A, el, (s, 0, 0), 0.95, 1.15, bolts=10)
    # Forearm: a heavy armoured housing that carries the weapon cradle
    loft(A, 'Secondary', [(el + Vector((0, -0.75, -0.2)), 0.95, 1.0, 3.2), (el + Vector((0, 0.2, -0.3)), 1.15, 1.15, 3.4),
                          (el + Vector((0, 1.0, -0.3)), 1.1, 1.08, 3.4), (el + Vector((0, 1.4, -0.3)), 0.9, 0.88, 3.2)],
         axis_up=(0, 0, 1), segs=32, name='Forearm')
    loft(A, 'Primary', [(el + Vector((0, -0.5, 0.7)), 0.9, 0.26, 2.6), (el + Vector((0, 0.5, 0.82)), 1.0, 0.3, 2.6), (el + Vector((0, 1.3, 0.62)), 0.8, 0.22, 2.6)],
         axis_up=(0, 0, 1), segs=24, name='ForearmTop')
    plate(A, 'Primary', el + Vector((s * 1.18, 0.35, -0.3)), (1.9, 1.7, 0.22), rot=(0, 0, s * 90), curve=(0.12, 0.08))
    stripe(A, el + Vector((s * 1.32, 0.35, 0.2)), (0.05, 1.4, 0.14))
    # Cradle ring the weapon locks into
    m = MOUNT(s)
    lathe(A, 'Dark', m + Vector((0, -0.25, 0)), (0, 1, 0), [(0.55, 0), (0.95, 0), (0.95, 0.45), (0.55, 0.45)], segs=32, name='Cradle')
    bolts_ring(A, m + Vector((0, 0.15, 0)), (0, 1, 0), 0.68, 8, 0.05)
    tube(A, 'Black', [sh + Vector((-s * 0.5, -0.8, -0.2)), sh + Vector((-s * 0.75, -0.9, -1.4)), el + Vector((-s * 0.7, -0.75, 0.1)), el + Vector((-s * 0.6, -0.1, -0.6))], 0.12)
    anchors['Mount' + ('L' if s < 0 else 'R')] = {'p': list(U(m)), 'parent': 'Arms'}

frames['Arms'] = frame_identity(U(ARMS))
parents['Arms'] = 'Arms'

# Pauldrons (detachable; ride on the torso, not the arm pitch)
for s, nm in ((-1, 'PauldronL'), (1, 'PauldronR')):
    c = Vector((s * 4.25, 0.1, 14.85))
    # Two overlapping cast layers, the lower one flaring out over the arm
    loft(nm, 'Primary', [(c + Vector((s * 0.1, -2.0, -0.35)), 1.4, 0.6, 2.3), (c + Vector((s * 0.15, -0.9, 0.05)), 1.75, 0.95, 2.3),
                         (c + Vector((s * 0.15, 0.6, 0.05)), 1.78, 0.97, 2.3), (c + Vector((s * 0.1, 1.95, -0.35)), 1.35, 0.6, 2.3)],
         axis_up=(0, 0, 1), segs=40, name='Pauldron')
    loft(nm, 'Secondary', [(c + Vector((-s * 0.25, -1.5, 0.55)), 0.95, 0.4, 2.3), (c + Vector((-s * 0.25, -0.3, 0.78)), 1.1, 0.5, 2.3),
                           (c + Vector((-s * 0.25, 1.0, 0.7)), 1.05, 0.46, 2.3), (c + Vector((-s * 0.25, 1.75, 0.4)), 0.8, 0.3, 2.3)],
         axis_up=(0, 0, 1), segs=32, name='PauldronTop')
    plate(nm, 'Primary', c + Vector((s * 1.75, 0.0, -1.05)), (3.6, 1.8, 0.26), rot=(0, s * 12, s * 90), curve=(0.06, 0.06), taper=1.1)
    plate(nm, 'Secondary', c + Vector((s * 1.95, 0.0, -2.0)), (3.2, 0.7, 0.2), rot=(0, s * 16, s * 90), curve=(0.06, 0.0))
    stripe(nm, c + Vector((s * 1.93, 0.0, -0.55)), (0.05, 2.8, 0.18), rot=(0, s * 12, 0))
    lamp(nm, c + Vector((s * 0.6, 2.05, -0.25)), 0.3)
    for k in range(4):
        block(nm, 'Black', c + Vector((s * 0.85, -1.1 + k * 0.6, 0.86)), (0.9, 0.06, 0.05), rot=(0, s * 30, 0), bevel=0, name='Groove')
    frames[nm] = frame_identity(U(c)); parents[nm] = 'Upper'

anchors['BackL'] = {'p': list(U((-4.0, -0.7, 16.0))), 'parent': 'Upper'}
anchors['BackR'] = {'p': list(U((4.0, -0.7, 16.0))), 'parent': 'Upper'}
anchors['Shoulder'] = {'p': list(U((-4.0, 0.0, 15.6))), 'parent': 'Arms'}
for s in (-1, 1):
    n = Vector((s * 1.6, -2.15, 10.9)) + Vector((0, -0.35, -1)).normalized() * 0.8
    anchors['Jet' + ('L' if s < 0 else 'R')] = {'p': list(U(n)), 'f': list(U(Vector((0, -0.35, -1)).normalized())), 'parent': 'Upper'}
anchors['Smoke'] = {'p': list(U((0.6, -1.4, 14.6))), 'parent': 'Upper'}

# ===========================================================================
# LEGS
# ===========================================================================
for s_ in (-1, 1):
    biped_leg(R, s_, (s_ * HIP_X, 0, HIP_Z), (s_ * (HIP_X + FOOT_OUT), 0, ANKLE_Z), THIGH, SHIN, ANKLE_Z)

R.extra.update({'thigh': THIGH, 'shin': SHIN, 'ankle': ANKLE_Z, 'footOut': FOOT_OUT, 'upper': list(U(UPPER)), 'arms': list(U(ARMS))})
run(R, os.path.dirname(os.path.abspath(__file__)), 8.6)
