"""
Weapon models (hard-surface, 2026-11-27). One file per weapon in
Assets/Resources/RobotModels/Weapon<Name>.bytes, loaded by WeaponModels.cs.

Built in the mount's frame: origin at the mount, Blender +Y = Unity +Z (the way the
weapon points), +Z = up, at weapon scale 1 (the game scales by the robot's arm,
shoulder or back scale). Muzzle points are fixed by WeaponKit.cs, so every barrel
ends exactly where the game spawns its shots. The right-hand version is modelled;
the game mirrors it for left mounts.

Parts: 'Static' (everything rigid); the Gatling also has 'Spin' (the rotating
barrel cluster, frame at its spin axis origin).
Usage: python weapons.py [-- out_dir] [--render]
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mechlib as ml
from mechlib import reset, lathe, finish, frame_identity, render, to_unity
from mathutils import Vector, Matrix
from hardsurf import *
from parts import gear, loft_y, sec_xz

OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Resources', 'RobotModels')
RENDER = '--render' in sys.argv
S = 'Static'


def barrel(part, y0, y1, r, mat='Dark', segs=20, z=0.0, x=0.0):
    lathe(part, mat, (x, y0, z), (0, 1, 0), [(0, 0), (r, 0), (r, y1 - y0), (0, y1 - y0)], segs=segs, name='Barrel')


def ring(part, y, r0, r1, w, mat='Metal', segs=20, z=0.0, x=0.0):
    lathe(part, mat, (x, y, z), (0, 1, 0), [(r0, -w / 2), (r1, -w / 2), (r1, w / 2), (r0, w / 2)], segs=segs, name='Ring')


def housing(part, mat, y0, y1, w, h, ch, z=0.0, taper=0.9, seam=0.6):
    """Faceted receiver box along Y from y0 to y1, half width w, half height h."""
    loft_y(part, mat, [(y0, w * taper, h * taper, ch, 0, z), (y0 + 0.3, w, h, ch, 0, z), (y1 - 0.3, w, h, ch, 0, z), (y1, w * taper, h * taper, ch, 0, z)],
           bevel=0.05, seam_min=seam, name='Housing')


def cradle_cover(part, r=0.95):
    """A collar that covers the arm's weapon cradle ring."""
    armour(part, 'Dark', ngon(-0.35, r, 8, 22.5) + ngon(0.15, r, 8, 22.5), Mx((0, 0, 0), (-90, 0, 0)), bevel=0.04, seams=False, groove=None, name='Collar')


def save(name, spin_origin=None, view_target=(0, 2, 0), dist=12):
    frames = {'Static': frame_identity(Vector((0, 0, 0)))}
    if spin_origin is not None:
        frames['Spin'] = frame_identity(to_unity(Vector(spin_origin)))
    finish(os.path.join(OUT, 'Weapon%s.bytes' % name), frames, {}, {}, [], {'stand': 0})
    if RENDER:
        import bpy
        for ob in list(bpy.context.scene.objects):
            ob.location.z += 2.5
        view_target = (view_target[0], view_target[1], view_target[2] + 2.5)
        render(os.path.join(OUT, 'w_%s.png' % name.lower()), views=((35, 15),), target=view_target, dist=dist, res=(800, 560), samples=16, lens=50)


def build(name, fn, **kw):
    reset()
    ml.PARTS.clear()
    fn()
    save(name, **kw)


# ---------------------------------------------------------------- primaries

def heavy_cannon():
    cradle_cover(S)
    housing(S, 'Primary', -0.6, 2.7, 0.85, 0.85, 0.3)
    armour(S, 'Secondary', [(x, y, z) for y in (-0.3, 2.4) for x, z in ((-0.6, 0.85), (0.6, 0.85), (-0.45, 1.15), (0.45, 1.15))], bevel=0.04, seam_min=0.4, name='TopPlate')
    stripe(S, Mx((0.87, 1.0, 0.3), (0, 0, -90)), 2.0, 0.14)
    grille(S, Mx((0.86, 1.0, -0.35), (0, 0, -90)), 1.6, 0.5, slats=3, depth=0.06)
    # Recoil cylinders either side of the barrel
    for s in (-1, 1):
        piston(S, (s * 0.55, 2.4, 0.55), (s * 0.55, 4.6, 0.45), 0.16)
        armour(S, 'Dark', boxpts((0.4, 0.5, 0.45), ch=0.1), Mx((s * 0.55, 4.7, 0.45)), bevel=0.03, seams=False, groove=None, name='Clamp')
    # Barrel: thick breech, bore evacuator, muzzle brake
    lathe(S, 'Dark', (0, 2.6, 0), (0, 1, 0), [(0, 0), (0.62, 0), (0.62, 0.5), (0.45, 0.65), (0.42, 4.2), (0, 4.2)], segs=24, name='Barrel')
    lathe(S, 'Metal', (0, 4.6, 0), (0, 1, 0), [(0.42, 0), (0.55, 0.12), (0.55, 0.75), (0.42, 0.9)], segs=24, name='Evacuator')
    ring(S, 3.3, 0.42, 0.5, 0.15)
    brake = boxpts((1.3, 0.9, 0.85), ch=0.12, endch=0.05)
    armour(S, 'Metal', brake, Mx((0, 6.9, 0), (-90, 0, 0)), bevel=0.04, seams=False, groove=None, name='Brake')
    for s in (-1, 1):
        for kk in range(3):
            armour(S, 'Black', boxpts((0.08, 0.16, 0.55)), Mx((s * 0.66, 6.65 + kk * 0.25, 0)), bevel=0, seams=False, groove=None, name='Port')
    lathe(S, 'Black', (0, 7.36, 0), (0, 1, 0), [(0, 0), (0.3, 0), (0.3, 0.02), (0, 0.02)], segs=16, name='Bore')
    hose(S, [(-0.9, 0.2, -0.3), (-1.0, 1.0, -0.6), (-0.7, 2.0, -0.7)], 0.12)


def pulse_laser():
    cradle_cover(S, 0.8)
    housing(S, 'Secondary', -0.4, 1.6, 0.55, 0.55, 0.18)
    for kk in range(5):
        armour(S, 'Dark', boxpts((1.35, 0.06, 0.5)), Mx((0, 0.0 + kk * 0.3, 0.25)), bevel=0, seams=False, groove=None, name='HeatSink')
    for s in (-1, 1):
        armour(S, 'Dark', [(s * x, y, z) for x in (0.24, 0.44) for y in (1.3, 4.0) for z in (-0.3, 0.3)] + [(s * 0.34, 4.15, 0)], bevel=0.03, seam_min=0.3, name='Rail')
    barrel(S, 1.4, 4.0, 0.18, 'Metal')
    for kk in range(3):
        lathe(S, 'Dark', (0, 1.9 + kk * 0.7, 0), (0, 1, 0), [(0.18, -0.12), (0.4, -0.1), (0.4, 0.1), (0.18, 0.12)], segs=20, name='CoilCase')
        ring(S, 1.9 + kk * 0.7, 0.18, 0.37, 0.12, 'Glow')
    lathe(S, 'Glow', (0, 4.0, 0), (0, 1, 0), [(0, 0), (0.3, 0), (0.25, 0.12), (0, 0.18)], segs=20, name='Lens')
    stripe(S, Mx((0.56, 0.6, 0.0), (0, 0, -90)), 1.2, 0.12)


def gatling():
    cradle_cover(S, 0.85)
    housing(S, 'Secondary', -0.4, 1.7, 0.58, 0.58, 0.2)
    armour(S, 'Primary', [(x, y, z) for y in (-0.2, 1.5) for x, z in ((-0.4, 0.58), (0.4, 0.58), (-0.3, 0.75), (0.3, 0.75))], bevel=0.03, seam_min=0.3, name='TopPlate')
    stripe(S, Mx((0, 0.65, 0.76), (90, 0, 0)), 0.3, 1.2)
    # Ammo box and feed chute on the inner side
    armour(S, 'Dark', boxpts((0.55, 1.3, 0.95), ch=0.08, endch=0.04), Mx((-0.85, 0.4, -0.25)), bevel=0.03, seam_min=0.2, name='AmmoBox')
    for kk in range(4):
        armour(S, 'Metal', boxpts((0.3, 0.12, 0.2)), Mx((-0.62, 0.95 - kk * 0.16, 0.25 + kk * 0.05)), bevel=0, seams=False, groove=None, name='Link')
    # Front bearing and drive motor
    lathe(S, 'Dark', (0, 1.6, 0), (0, 1, 0), [(0, 0), (0.5, 0), (0.5, 0.25), (0, 0.25)], segs=24, name='Bearing')
    cyl(S, 'Metal', (0.42, 1.2, -0.45), (0, 1, 0), 0.18, 0.6, segs=12, name='Motor')
    P = 'Spin'
    c = 1.9
    lathe(P, 'Dark', (0, c, 0), (0, 1, 0), [(0, 0), (0.12, 0), (0.12, 1.75), (0, 1.75)], segs=12, name='Shaft')
    for n in range(6):
        a = math.radians(n * 60)
        barrel(P, c, c + 1.75, 0.09, 'Black', segs=10, x=math.cos(a) * 0.26, z=math.sin(a) * 0.26)
    for y in (c + 0.3, c + 1.0, c + 1.62):
        armour(P, 'Metal', ngon(y - 0.07, 0.45, 6, 0) + ngon(y + 0.07, 0.45, 6, 0), Mx((0, 0, 0), (-90, 0, 0)) @ Mx((0, 0, 0)), bevel=0.02, seams=False, groove=None, name='Clamp')
    ring(P, c + 1.85, 0.3, 0.42, 0.2, 'Dark')


def _fix_clamps():
    # ngon builds in XY at height z; the Mx(-90 X) turns that into XZ at depth -z, so rebuild by Y directly
    pass


def rocket_pods():
    cradle_cover(S, 0.9)
    housing(S, 'Primary', -0.4, 1.62, 0.72, 0.72, 0.22)
    armour(S, 'Secondary', [(x, y, z) for y in (-0.2, 1.4) for x, z in ((-0.55, 0.72), (0.55, 0.72), (-0.45, 0.88), (0.45, 0.88))], bevel=0.03, seam_min=0.3, name='TopPlate')
    hazard(S, Mx((0, 1.0, 0.89), (90, 0, 0)), 1.0, 0.25, n=4)
    for r in (-1, 1):
        for cl in (-1, 1):
            x, z = cl * 0.3, r * 0.3
            lathe(S, 'Black', (x, 1.62, z), (0, 1, 0), [(0, -0.6), (0.21, -0.6), (0.21, 0.0), (0, 0.0)], segs=14, name='Tube')
            lathe(S, 'Dark', (x, 1.62, z), (0, 1, 0), [(0.21, -0.05), (0.27, -0.05), (0.27, 0.06), (0.21, 0.06)], segs=14, name='TubeLip')
            lathe(S, 'Hot', (x, 1.3, z), (0, 1, 0), [(0, 0), (0.14, 0), (0.1, 0.12), (0, 0.18)], segs=10, name='Warhead')
    for s in (-1, 1):
        side_plate(S, 'Dark', [(-0.8, 0.6), (0.9, 0.6), (0.9, -0.6), (-0.8, -0.6)], 0.1, (s * 0.76, 0.6, 0), s, 0, bevel=0.02, seam_min=0.3, name='Shutter')


def flamethrower():
    cradle_cover(S, 0.8)
    housing(S, 'Secondary', -0.4, 1.3, 0.52, 0.52, 0.18)
    # Fuel tank on the outer side, with straps
    lathe(S, 'Accent', (0.66, -0.3, -0.12), (0, 1, 0), [(0, 0), (0.25, 0), (0.33, 0.1), (0.33, 1.4), (0.25, 1.5), (0, 1.5)], segs=20, name='Tank')
    for y in (0.1, 0.9):
        ring(S, y, 0.33, 0.36, 0.08, 'Dark', x=0.66, z=-0.12)
    hazard(S, Mx((0.66, 0.5, 0.25), (90, 0, 0)), 0.3, 0.25, n=2)
    # Feed pipe and the flame nozzle
    lathe(S, 'Dark', (0, 1.2, 0), (0, 1, 0), [(0, 0), (0.32, 0), (0.3, 1.6), (0, 1.6)], segs=18, name='Pipe')
    for y in (1.6, 2.1, 2.5):
        ring(S, y, 0.3, 0.36, 0.1)
    lathe(S, 'Metal', (0, 2.75, 0), (0, 1, 0), [(0.3, 0), (0.48, 0.1), (0.52, 0.45), (0.44, 0.5), (0.36, 0.2), (0.3, 0.2)], segs=22, name='Shroud')
    ring(S, 3.12, 0.12, 0.34, 0.05, 'Hot')
    hose(S, [(0.66, 1.2, -0.12), (0.5, 1.6, -0.45), (0.15, 2.0, -0.4), (0.05, 2.3, -0.3)], 0.08)
    lathe(S, 'Black', (0, 0.9, -0.48), (0, 1, 0), [(0, 0), (0.09, 0), (0.09, 1.5), (0, 1.5)], segs=8, name='PilotLine')


def rail_rifle():
    cradle_cover(S, 0.8)
    housing(S, 'Primary', -0.5, 1.6, 0.5, 0.6, 0.18)
    for s in (-1, 1):
        armour(S, 'Dark', [(s * x, y, z) for x in (0.22, 0.42) for y in (1.0, 6.5) for z in (-0.18, 0.38)] + [(s * 0.32, 6.65, 0.1)], bevel=0.03, seam_min=0.5, name='Rail')
        for kk in range(6):
            armour(S, 'Metal', boxpts((0.05, 0.18, 0.5)), Mx((s * 0.44, 1.4 + kk * 0.9, 0.1)), bevel=0, seams=False, groove=None, name='Rivet')
    for r in range(5):
        y = 1.9 + r * 0.95
        lathe(S, 'Dark', (0, y, 0.1), (0, 1, 0), [(0.1, -0.1), (0.5, -0.08), (0.5, 0.08), (0.1, 0.1)], segs=20, name='CoilCase')
        ring(S, y, 0.1, 0.46, 0.1, 'Glow', z=0.1)
    # Scope on top
    lathe(S, 'Dark', (0, 0.4, 0.85), (0, 1, 0), [(0, 0), (0.2, 0), (0.2, 1.5), (0.24, 1.6), (0, 1.65)], segs=14, name='Scope')
    lathe(S, 'Glow', (0, 2.03, 0.85), (0, 1, 0), [(0, 0), (0.15, 0), (0, 0.03)], segs=12, name='ScopeLens')
    armour(S, 'Dark', boxpts((0.2, 0.9, 0.3)), Mx((0, 0.9, 0.65)), bevel=0.02, seams=False, groove=None, name='ScopeMount')
    stripe(S, Mx((0.51, 0.5, 0.1), (0, 0, -90)), 1.2, 0.12)


# ---------------------------------------------------------------- specials

def railgun():
    housing(S, 'Primary', -0.6, 1.4, 0.65, 0.55, 0.2)
    armour(S, 'Dark', boxpts((0.6, 1.6, 0.3), ch=0.08), Mx((0, 0.6, -0.62)), bevel=0.03, seams=False, groove=None, name='Mount')
    for s in (-1, 1):
        armour(S, 'Dark', [(s * x, y, z) for x in (0.25, 0.48) for y in (0.7, 5.7) for z in (-0.3, 0.3)] + [(s * 0.36, 5.9, 0)], bevel=0.03, seam_min=0.5, name='Rail')
    for r in range(4):
        y = 1.6 + r * 1.05
        lathe(S, 'Dark', (0, y, 0), (0, 1, 0), [(0.1, -0.12), (0.56, -0.1), (0.56, 0.1), (0.1, 0.12)], segs=20, name='CoilCase')
        ring(S, y, 0.1, 0.52, 0.12, 'Glow')
    hose(S, [(-0.5, -0.4, 0.5), (-0.6, 0.6, 0.7), (-0.3, 1.5, 0.5)], 0.1)
    hazard(S, Mx((0, 0.4, 0.56), (90, 0, 0)), 0.8, 0.25, n=3)


def mortar(tubes):
    w = tubes * 0.85 + 0.4
    armour(S, 'Dark', boxpts((w, 1.9, 0.5), ch=0.15), bevel=0.04, seams=False, groove=None, name='Base')
    armour(S, 'Secondary', boxpts((w - 0.2, 1.6, 0.25), ch=0.12, taper=(0.95, 0.92)), Mx((0, 0, 0.35)), bevel=0.03, seam_min=0.4, name='Deck')
    ax = Vector((0, math.sin(math.radians(20)), math.cos(math.radians(20))))
    for n in range(tubes):
        x = (n - (tubes - 1) * 0.5) * 0.85
        c = Vector((x, 0, 0.6))
        lathe(S, 'Secondary', c - ax * 0.5, ax, [(0, 0), (0.33, 0), (0.33, 0.85), (0.38, 0.9), (0.38, 1.05), (0.24, 1.05), (0.24, 0.95), (0, 0.95)], segs=18, name='Tube')
        lathe(S, 'Hot', c + ax * 0.35, ax, [(0, 0), (0.22, 0), (0, 0.05)], segs=12, name='Shell')
        ring_y = c - ax * 0.1
        lathe(S, 'Dark', ring_y, ax, [(0.33, -0.08), (0.38, -0.08), (0.38, 0.08), (0.33, 0.08)], segs=18, name='Band')
        piston(S, Vector((x + 0.3, -0.7, 0.3)), c - ax * 0.2 + Vector((0.3, 0, 0)), 0.08)
    stripe(S, Mx((w / 2 + 0.01, 0, 0.1), (0, 0, -90)), 1.5, 0.14)


def tesla_coil():
    armour(S, 'Dark', ngon(-0.5, 0.8, 8, 22.5) + ngon(0.5, 0.72, 8, 22.5), bevel=0.04, seams=False, groove=None, name='Base')
    for r in range(4):
        z = 0.5 + r * 0.38
        rr = 0.62 - r * 0.085
        lathe(S, 'Metal' if r % 2 == 0 else 'Glow', (0, 0, z), (0, 0, 1), [(0.16, -0.07), (rr, -0.06), (rr, 0.06), (0.16, 0.07)], segs=24, name='Coil')
    lathe(S, 'Dark', (0, 0, 0.5), (0, 0, 1), [(0, 0), (0.18, 0), (0.15, 1.5), (0, 1.5)], segs=12, name='Mast')
    lathe(S, 'Metal', (0, 0, 2.2), (0, 0, 1), [(0.0, -0.25), (0.5, -0.15), (0.6, 0), (0.5, 0.15), (0, 0.25)], segs=24, name='Toroid')
    lathe(S, 'Glow', (0, 0, 2.2), (0, 0, 1), [(0, -0.32), (0.3, -0.25), (0.36, 0), (0.3, 0.25), (0, 0.32)], segs=20, name='Orb')
    for a in range(4):
        t = math.radians(45 + a * 90)
        piston(S, (math.cos(t) * 0.7, math.sin(t) * 0.7, 0.4), (math.cos(t) * 0.25, math.sin(t) * 0.25, 1.3), 0.06)


def tesla_bank():
    armour(S, 'Secondary', boxpts((1.5, 0.8, 1.6), ch=0.15, endch=0.05), Mx((0, 0, 0.4)), bevel=0.04, seam_min=0.4, name='Bank')
    for r in range(3):
        x = (r - 1) * 0.42
        lathe(S, 'Dark', (x, 0, 1.2), (0, 0, 1), [(0, 0), (0.18, 0), (0.18, 0.1), (0, 0.1)], segs=14, name='Cap')
        lathe(S, 'Glow', (x, 0, 1.25), (0, 0, 1), [(0, 0), (0.14, 0), (0.14, 0.45), (0.08, 0.52), (0, 0.52)], segs=14, name='Cell')
    hazard(S, Mx((0, 0.41, 0.4)), 1.2, 0.25, n=5)
    hose(S, [(-0.75, 0, 0.7), (-1.0, -0.2, 0.9), (-1.2, -0.1, 1.2)], 0.08)


def swarm(cols):
    w = cols * 0.62 + 0.3
    armour(S, 'Secondary', boxpts((w, 2.4, 1.3), ch=0.15, endch=0.06), bevel=0.05, seam_min=0.6, name='Box')
    for s in (-1, 1):
        stripe(S, Mx((s * (w / 2 + 0.01), 0, 0.2), (0, 0, s * 90)), 2.0, 0.14)
    armour(S, 'Dark', boxpts((w - 0.15, 2.2, 0.12)), Mx((0, 0, 0.66)), bevel=0.02, seams=False, groove=None, name='Lid')
    for r in range(2):
        for col in range(cols):
            x = (col - (cols - 1) * 0.5) * 0.62
            y = 0.5 - r * 0.95
            lathe(S, 'Black', (x, y, 0.72), (0, 0, 1), [(0, -0.5), (0.24, -0.5), (0.24, 0.0), (0, 0.0)], segs=12, name='Cell')
            lathe(S, 'Metal', (x, y, 0.72), (0, 0, 1), [(0.24, -0.02), (0.28, -0.02), (0.28, 0.04), (0.24, 0.04)], segs=12, name='Lip')
            lathe(S, 'Hot', (x, y, 0.55), (0, 0, 1), [(0, 0), (0.15, 0), (0, 0.12)], segs=10, name='Tip')
    piston(S, (0, -1.2, -0.6), (0, -1.0, 0.3), 0.1)


ALL = [('HeavyCannon', heavy_cannon, {}), ('PulseLaser', pulse_laser, {}), ('Gatling', gatling, {'spin_origin': (0, 1.9, 0)}),
       ('RocketPods', rocket_pods, {}), ('Flamethrower', flamethrower, {}), ('RailRifle', rail_rifle, {'dist': 14}),
       ('Railgun', railgun, {}), ('Mortar2', lambda: mortar(2), {'view_target': (0, 0, 0.5)}), ('Mortar3', lambda: mortar(3), {'view_target': (0, 0, 0.5)}),
       ('TeslaCoil', tesla_coil, {'view_target': (0, 0, 1)}), ('TeslaBank', tesla_bank, {'view_target': (0, 0, 0.6)}),
       ('Swarm3', lambda: swarm(3), {'view_target': (0, 0, 0.3)}), ('Swarm4', lambda: swarm(4), {'view_target': (0, 0, 0.3)})]

if __name__ == '__main__':
    only = [a for a in sys.argv[1:] if a in [n for n, _, _ in ALL]]
    for name, fn, kw in ALL:
        if only and name not in only:
            continue
        build(name, fn, **kw)
        print('built', name)
