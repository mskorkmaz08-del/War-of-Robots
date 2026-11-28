"""
STRIDER - light reverse-jointed scout walker (about 22 t).

A fast, jumpy hunter on two long reverse-jointed legs: the knees fold backwards, so the body rides low and forward over the feet and every step is a long,
springy stride. The body is one armoured pod: a blunt glacis nose with a slit canopy and a
square sensor box, flanked by two stub weapon pylons on geared trunnions, a rotating
radar dome on the spine, and a twin-nozzle jump pack under the tail.
Working cylinders at hips, knees, ankles and pylons; gears at every bearing.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mechlib import reset, lathe
from mathutils import Vector, Matrix
from hardsurf import *
from parts import *

reset()
STAND = 6.6
HIP_X, HIP_Z = 1.75, 6.3
THIGH, SHIN, ANKLE_Z, FOOT_OUT = 3.4, 4.0, 1.0, 0.45
UPPER = Vector((0, 0.2, 7.55))
ARMS = Vector((0, 0.9, 8.75))
K = 0.8
R = Rig('Strider', STAND)

ring_top = pelvis(R, HIP_X, HIP_Z, k=0.72, ring_r=1.25)

# ---------------------------------------------------------------------------
# Body pod (turns with the upper body)
# ---------------------------------------------------------------------------
T = 'Torso'
lathe(T, 'Metal', (0, 0, ring_top), (0, 0, 1), [(0, 0), (1.2, 0), (1.25, 0.05), (1.25, 0.24), (1.12, 0.3), (0, 0.3)], segs=40, name='RingTop')
gear(T, (0, 0, ring_top + 0.42), (0, 0, 1), 0.95, 0.12, teeth=36, mat='Metal')

# Keel frame under the pod: an exposed girder with a slung engine block
armour(T, 'Dark', [(x, y, z) for x in (-0.7, 0.7) for (y, z) in ((-2.0, 7.95), (2.1, 8.05), (-1.6, 7.55), (1.6, 7.6))], bevel=0.05, seams=False, groove=None, name='Keel')
# Main pod shell: wedge nose, broad mid-body, tapered tail
pod = [(x * w, y, z) for (y, w, zb, zt) in ((3.15, 1.1, 8.15, 9.0), (2.4, 1.4, 7.95, 9.45), (0.4, 1.65, 7.85, 9.9), (-1.6, 1.5, 7.9, 9.75), (-2.65, 1.0, 8.15, 9.3))
       for x in (-1, 1) for z in (zb, zt)]
armour(T, 'Secondary', pod, bevel=0.07, seam_min=1.0, seam_w=0.2, name='Pod')
# Nose armour: a blunt sloped glacis plate with a centre seam
armour(T, 'Primary', [(x * w, y, z) for (y, w, z) in ((3.3, 1.15, 8.95), (2.3, 1.4, 9.55), (3.2, 1.1, 8.75), (2.2, 1.4, 9.3)) for x in (-1, 1)],
       bevel=0.05, seam_min=0.6, name='Glacis')
# Lower front: a square sensor box with one wide lens and a rangefinder, and a bumper bar
armour(T, 'Primary', [(x * w, y, z) for (y, w, z) in ((3.25, 1.1, 8.15), (2.3, 1.3, 7.95), (3.25, 1.1, 8.7), (2.2, 1.35, 8.25)) for x in (-1, 1)],
       bevel=0.04, seam_min=0.6, name='Chin')
armour(T, 'Dark', boxpts((1.3, 0.5, 0.45), ch=0.08), Mx((0, 3.35, 8.4)), bevel=0.03, seams=False, groove=None, name='SensorBox')
armour(T, 'Black', boxpts((1.05, 0.06, 0.25)), Mx((0, 3.6, 8.42)), bevel=0, seams=False, groove=None, name='SensorWindow')
armour(T, 'Glow', boxpts((0.9, 0.04, 0.07)), Mx((0, 3.63, 8.42)), bevel=0, seams=False, groove=None, name='SensorSlit')
lathe(T, 'Dark', Vector((0.85, 3.2, 8.75)), (0, 1, 0), [(0, -0.2), (0.17, -0.2), (0.19, 0.0), (0.17, 0.3), (0, 0.3)], segs=16, name='RangeHousing')
lathe(T, 'Glow', Vector((0.85, 3.5, 8.75)), (0, 1, 0), [(0, 0), (0.12, 0), (0, 0.04)], segs=14, name='RangeLens')
# Canopy: a dark slit band wrapping the upper nose, with glowing pane edges
armour(T, 'Black', [(x * w, y, z) for (y, w, z) in ((2.45, 1.05, 9.4), (1.55, 1.3, 9.75), (2.55, 1.0, 9.2), (1.5, 1.32, 9.55)) for x in (-1, 1)],
       bevel=0.02, seams=False, groove=None, name='Canopy')
for s in (-1, 1):
    armour(T, 'Glow', boxpts((0.05, 0.9, 0.06)), Mx((s * 1.12, 2.05, 9.5), (0, 0, s * 18)), bevel=0, seams=False, groove=None, name='PaneEdge')
# Spine: armoured ridge with a hazard band and a hatch
armour(T, 'Primary', sec(9.85, 0.75, 1.4, 0.3, 0, -0.4) + sec(10.15, 0.55, 1.2, 0.25, 0, -0.5), bevel=0.05, seam_min=0.7, name='Spine')
hazard(T, Mx((0, -1.6, 10.02), (90, 0, 0)), 1.2, 0.24, n=5)
armour(T, 'Dark', boxpts((0.8, 0.8, 0.12), ch=0.14), Mx((0, 0.55, 10.18)), bevel=0.03, seams=False, groove=None, name='Hatch')
# Flank details: intakes, vents, step rungs, hoses
for s in (-1, 1):
    grille(T, Mx((s * 1.62, -0.6, 8.85), (0, 0, -s * 90)), 1.6, 0.8, slats=5, depth=0.1)
    for k in range(4):
        armour(T, 'Dark', boxpts((0.06, 0.06, 0.5)), Mx((s * 1.55, -2.0 + k * 0.08, 8.3 + k * 0.32), (0, 90, 0)), bevel=0, seams=False, groove=None, name='Rung')
    hose(T, [(s * 0.9, -2.3, 9.6), (s * 1.55, -1.4, 9.75), (s * 1.75, 0.3, 9.55)], 0.08)
    bolts_line(T, (s * 1.66, 1.6, 9.35), (s * 1.66, -1.4, 9.35), 7, (s, 0, 0), 0.05)
    lamp(T, Mx((s * 0.75, 3.05, 8.95), (0, 0, s * 12)), 0.3, 0.1)
    # Pylon trunnion housings
    armour(T, 'Dark', boxpts((0.55, 1.2, 1.1), ch=0.25), Mx((s * 1.75, ARMS.y, ARMS.z)), bevel=0.04, seams=False, groove=None, name='Trunnion')
    armour(T, 'Dark', boxpts((0.4, 0.55, 0.55), ch=0.1), Mx((s * 1.4, ARMS.y - 1.25, 8.1)), bevel=0.04, seams=False, groove=None, name='PylonLug')

# Tail: engine block with cooling fins and the jump pack
armour(T, 'Dark', boxpts((2.0, 1.4, 1.3), ch=0.3, taper=(0.85, 0.85)), Mx((0, -2.95, 8.55)), bevel=0.05, seams=False, groove=None, name='Engine')
for k in range(7):
    armour(T, 'Metal', boxpts((2.1, 0.06, 1.05)), Mx((0, -2.45 - k * 0.14, 8.55)), bevel=0, seams=False, groove=None, name='Fin')
armour(T, 'Primary', boxpts((2.3, 0.6, 1.45), ch=0.25, endch=0.08), Mx((0, -3.75, 8.65), (-8, 0, 0)), bevel=0.05, seam_min=0.6, name='TailPlate')
hazard(T, Mx((0, -4.07, 8.65), (-8, 0, 180)), 1.8, 0.24, n=6)
for s in (-1, 1):
    n = jet(T, Vector((s * 0.75, -3.1, 7.75)), (0, -0.25, -1), 0.7)
    R.anchor('Jet' + ('L' if s < 0 else 'R'), n, 'Upper', (0, -0.25, -1))
    lathe(T, 'Dark', (s * 0.75, -3.55, 9.3), (0, 0, 1), [(0, 0), (0.22, 0), (0.22, 0.75), (0.28, 0.8), (0.28, 0.95), (0, 0.95)], segs=10, name='Exhaust')
    lathe(T, 'Hot', (s * 0.75, -3.55, 10.25), (0, 0, 1), [(0, 0), (0.2, 0), (0, 0.01)], segs=10, name='ExhaustGlow')
R.rigid('Torso', UPPER, 'Upper')

# Rotating radar dome on the spine (keeps sweeping in the game)
RAD = Vector((0, -1.05, 10.2))
lathe('SpinRadar', 'Dark', RAD, (0, 0, 1), [(0, 0), (0.42, 0), (0.42, 0.12), (0.3, 0.2), (0.3, 0.55), (0, 0.55)], segs=20, name='RadarPost')
for s in (-1, 1):
    armour('SpinRadar', 'Secondary', boxpts((1.55, 0.18, 0.42), ch=0.06, endch=0.04), Mx(RAD + Vector((s * 0.72, 0, 0.72))), bevel=0.03, seams=False, groove=None, name='RadarVane')
    armour('SpinRadar', 'Black', boxpts((1.3, 0.04, 0.3)), Mx(RAD + Vector((s * 0.72, 0.1, 0.72))), bevel=0, seams=False, groove=None, name='RadarFace')
lathe('SpinRadar', 'Glow', RAD + Vector((0, 0, 0.92)), (0, 0, 1), [(0, 0), (0.1, 0), (0.06, 0.12), (0, 0.14)], segs=10, name='RadarTip')
R.spin('SpinRadar', RAD, 'Upper', (0, 0, 1), 110.0)

# Detachable flank armour (blows off as the robot takes damage)
for s, nm in ((-1, 'ChestL'), (1, 'ChestR')):
    c = Vector((s * 1.72, 1.0, 9.05))
    side_plate(nm, 'Primary', [(-1.25, 0.55), (1.35, 0.55), (1.1, -0.6), (-1.1, -0.6)], 0.16, (0, 0, 0), s, 8, M=Mx(c),
               stripe_at=0.15, stripe_h=0.13, bevel=0.04, seam_min=0.5, name='FlankPlate')
    R.rigid(nm, c, 'Upper')

# ---------------------------------------------------------------------------
# Weapon pylons (pitch with the 'Arms' group)
# ---------------------------------------------------------------------------
A = 'Arms'
for s in (-1, 1):
    tr = Vector((s * 2.1, ARMS.y, ARMS.z))
    drum(A, tr, (1, 0, 0), 0.62, 0.6, bolts=8)
    gear(A, tr + Vector((-s * 0.42, 0, 0)), (1, 0, 0), 0.55, 0.12)
    # Pylon beam from the trunnion out and forward to the cradle
    m = Vector((s * 2.6, 2.0, 8.55))
    armour(A, 'Dark', [(tr.x + s * x, y, z) for x in (0.1, 0.7) for (y, z) in ((ARMS.y - 0.6, ARMS.z + 0.35), (ARMS.y - 0.6, ARMS.z - 0.5), (m.y - 0.6, m.z + 0.45), (m.y - 0.6, m.z - 0.45))],
           bevel=0.04, seams=False, groove=None, name='PylonBeam')
    armour(A, 'Primary', [(tr.x + s * x, y, z) for x in (0.0, 0.85) for (y, z) in ((ARMS.y - 0.9, ARMS.z + 0.55), (m.y - 0.3, m.z + 0.65), (m.y - 0.5, m.z + 0.85))] +
           [(tr.x + s * 0.42, ARMS.y - 0.2, ARMS.z + 0.95)], bevel=0.04, seam_min=0.5, name='PylonCover')
    stripe(A, Mx((tr.x + s * 0.88, (ARMS.y + m.y) / 2 - 0.3, m.z + 0.35), (0, 0, -s * 90)), 1.0, 0.1)
    # Ammunition feed: a segmented chute from the pod to the cradle
    hose(A, [(s * 1.95, ARMS.y - 0.9, ARMS.z - 0.5), (s * 2.45, ARMS.y - 0.4, m.z - 0.7), (s * 2.6, m.y - 0.7, m.z - 0.55)], 0.13, mat='Dark')
    lathe(A, 'Dark', m + Vector((0, -0.3, 0)), (0, 1, 0), [(0.42, 0), (0.72, 0), (0.72, 0.35), (0.42, 0.35)], segs=28, name='Cradle')
    armour(A, 'Dark', boxpts((0.4, 0.5, 0.5), ch=0.1), Mx((s * 2.15, ARMS.y - 1.15, ARMS.z - 0.55)), bevel=0.04, seams=False, groove=None, name='ArmLug')
    R.anchor('Mount' + ('L' if s < 0 else 'R'), m, 'Arms')
R.rigid('Arms', ARMS, 'Arms')
shoulder_rams(R, [((s * 1.4, ARMS.y - 1.25, 8.1), (s * 2.15, ARMS.y - 1.15, ARMS.z - 0.55)) for s in (-1, 1)], k=0.6)

# Antenna whip (detachable)
mast = Vector((0.95, -2.2, 9.7))
armour('Antenna', 'Dark', boxpts((0.3, 0.3, 0.35), ch=0.06), Mx(mast + Vector((0, 0, 0.17))), bevel=0.03, seams=False, groove=None, name='MastBase')
lathe('Antenna', 'Dark', mast, (0, 0, 1), [(0.05, 0.35), (0.035, 2.4), (0.0, 2.5)], segs=8, name='Mast')
lathe('Antenna', 'Glow', mast + Vector((0, 0, 2.4)), (0, 0, 1), [(0, 0), (0.06, 0), (0, 0.1)], segs=10, name='Tip')
R.rigid('Antenna', mast, 'Upper')

R.anchor('BackL', (-1.0, -1.9, 10.1), 'Upper')
R.anchor('BackR', (1.0, -1.9, 10.1), 'Upper')
R.anchor('Shoulder', (1.4, 0.4, 10.35), 'Arms')
R.anchor('Smoke', (0, -2.8, 9.4), 'Upper')

# ---------------------------------------------------------------------------
# Reverse-jointed legs
# ---------------------------------------------------------------------------
for s in (-1, 1):
    hip = (s * HIP_X, 0, HIP_Z)
    ank = (s * (HIP_X + FOOT_OUT), 0.35, ANKLE_Z)
    knee, Mt, Ms = walker_leg(R, s, hip, ank, THIGH, SHIN, ANKLE_Z, k=K,
                              st=dict(shin='layered', toes=3, foot_len=1.25, foot_w=0.8, bend=(0, -1, 0), knee_rams=2, ankle_rams=2))
    hip_ram(R, s, hip, Mt, THIGH, k=0.8)

R.extra.update({'thigh': THIGH, 'shin': SHIN, 'ankle': ANKLE_Z, 'footOut': FOOT_OUT, 'upper': list(U(UPPER)), 'arms': list(U(ARMS)),
                'detach': ['ChestL', 'Antenna', 'ChestR']})
run(R, os.path.dirname(os.path.abspath(__file__)), 6.0, 36)
