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
        render(os.path.join(OUT, 'w_%s.png' % name.lower()), views=tuple(tuple(float(x) for x in v.split(',')) for v in os.environ.get('WVIEWS', '35,15').split(';')), target=view_target, dist=dist, res=(800, 560), samples=16, lens=50)


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



# ---------------------------------------------------------------- added 2026-11-28: new weapon kinds

def autocannon():
    """40 mm autocannon: long receiver, belt-fed from a drum box, cooling jacket and a ported brake."""
    cradle_cover(S, 0.85)
    housing(S, 'Primary', -0.5, 2.5, 0.68, 0.72, 0.22)
    armour(S, 'Secondary', [(x, y, z) for y in (-0.3, 2.2) for x, z in ((-0.5, 0.72), (0.5, 0.72), (-0.36, 0.95), (0.36, 0.95))], bevel=0.03, seam_min=0.35, name='TopPlate')
    stripe(S, Mx((0.69, 1.2, 0.35), (0, 0, -90)), 1.8, 0.12)
    bolts_line(S, (0.7, -0.2, -0.3), (0.7, 2.2, -0.3), 7, (1, 0, 0), r=0.05)
    # Ammunition: a drum box on the inner side, a flexible chute into the receiver
    armour(S, 'Dark', boxpts((0.7, 1.5, 1.1), ch=0.1, endch=0.05), Mx((-1.05, 0.5, -0.2)), bevel=0.03, seam_min=0.25, name='AmmoBox')
    for kk in range(4):
        armour(S, 'Metal', boxpts((0.74, 0.06, 1.14)), Mx((-1.05, -0.05 + kk * 0.36, -0.2)), bevel=0, seams=False, groove=None, name='Rib')
    hazard(S, Mx((-1.05, 1.26, 0.1)), 0.6, 0.22, n=3)
    for kk in range(5):
        t = kk / 4.0
        armour(S, 'Metal', boxpts((0.34, 0.14, 0.22)), Mx((-0.75 + t * 0.4, 0.9 - t * 0.1, 0.38 + math.sin(t * math.pi) * 0.25), (0, -30 + t * 30, 0)),
               bevel=0, seams=False, groove=None, name='Link')
    # Barrel: breech ring, perforated cooling jacket, recoil sleeve, long tube and brake
    lathe(S, 'Dark', (0, 2.4, 0.05), (0, 1, 0), [(0, 0), (0.5, 0), (0.5, 0.35), (0.38, 0.45), (0, 0.45)], segs=24, name='Breech')
    lathe(S, 'Metal', (0, 2.85, 0.05), (0, 1, 0), [(0, 0), (0.34, 0), (0.34, 1.3), (0, 1.3)], segs=22, name='Jacket')
    for kk in range(6):
        lathe(S, 'Black', (0, 2.98 + kk * 0.2, 0.05), (0, 1, 0), [(0.3, -0.05), (0.35, -0.05), (0.35, 0.05), (0.3, 0.05)], segs=22, name='Vent')
    lathe(S, 'Dark', (0, 4.15, 0.05), (0, 1, 0), [(0, 0), (0.2, 0), (0.2, 1.45), (0, 1.45)], segs=18, name='Tube')
    ring(S, 4.2, 0.2, 0.3, 0.12, 'Dark', z=0.05)
    lathe(S, 'Metal', (0, 5.5, 0.05), (0, 1, 0), [(0.18, 0), (0.33, 0.06), (0.33, 0.48), (0.24, 0.52), (0.18, 0.52)], segs=20, name='Brake')
    for s in (-1, 1):
        armour(S, 'Black', boxpts((0.08, 0.1, 0.24)), Mx((s * 0.32, 5.72, 0.05)), bevel=0, seams=False, groove=None, name='Port')
        armour(S, 'Black', boxpts((0.08, 0.1, 0.24)), Mx((s * 0.32, 5.88, 0.05)), bevel=0, seams=False, groove=None, name='Port')
    lathe(S, 'Black', (0, 6.0, 0.05), (0, 1, 0), [(0, 0), (0.15, 0), (0.15, 0.02), (0, 0.02)], segs=14, name='Bore')
    # Recoil buffers under the barrel and an optics box on top
    for s in (-1, 1):
        piston(S, (s * 0.28, 2.3, -0.42), (s * 0.24, 4.1, -0.3), 0.1)
    armour(S, 'Dark', boxpts((0.45, 0.6, 0.18)), Mx((0, 4.05, -0.26)), bevel=0.02, seams=False, groove=None, name='Clamp')
    armour(S, 'Dark', boxpts((0.42, 0.9, 0.38), ch=0.06), Mx((0.1, 1.6, 1.15)), bevel=0.03, seams=False, groove=None, name='Optic')
    lathe(S, 'Glow', (0.1, 2.06, 1.15), (0, 1, 0), [(0, 0), (0.12, 0), (0, 0.02)], segs=12, name='OpticLens')
    hose(S, [(0.65, 0.3, -0.5), (0.75, 1.3, -0.65), (0.4, 2.3, -0.55)], 0.08)


def scatter_cannon():
    """A giant's shotgun: a fat smoothbore with a slotted heat shield, a slide-action forend and a magazine tube."""
    cradle_cover(S, 0.9)
    housing(S, 'Primary', -0.5, 1.8, 0.8, 0.82, 0.26)
    armour(S, 'Secondary', [(x, y, z) for y in (-0.3, 1.6) for x, z in ((-0.6, 0.82), (0.6, 0.82), (-0.45, 1.05), (0.45, 1.05))], bevel=0.03, seam_min=0.3, name='TopPlate')
    hazard(S, Mx((0, 0.7, 1.06), (90, 0, 0)), 1.0, 0.25, n=4)
    for s in (-1, 1):
        side_plate(S, 'Dark', [(-0.9, 0.55), (0.8, 0.55), (0.9, -0.5), (-0.9, -0.55)], 0.1, (s * 0.84, 0.7, -0.05), s, 6, bevel=0.02, seam_min=0.3, name='Cheek')
    # The barrel and its slotted shield
    lathe(S, 'Dark', (0, 1.7, 0.15), (0, 1, 0), [(0, 0), (0.5, 0), (0.5, 2.6), (0, 2.6)], segs=24, name='Barrel')
    shield = boxpts((1.18, 2.2, 0.62), ch=0.2)
    armour(S, 'Secondary', shield, Mx((0, 2.85, 0.42)), bevel=0.03, seams=False, groove=None, name='Shield')
    for kk in range(6):
        armour(S, 'Black', boxpts((0.9, 0.12, 0.05)), Mx((0, 2.05 + kk * 0.32, 0.74)), bevel=0, seams=False, groove=None, name='Slot')
    # Choke: a flared, reinforced muzzle with a big dark bore
    lathe(S, 'Metal', (0, 4.15, 0.15), (0, 1, 0), [(0.46, 0), (0.66, 0.12), (0.7, 0.4), (0.6, 0.45), (0.46, 0.45)], segs=26, name='Choke')
    lathe(S, 'Black', (0, 4.58, 0.15), (0, 1, 0), [(0, 0), (0.42, 0), (0.42, 0.02), (0, 0.02)], segs=22, name='Bore')
    # Under-barrel magazine tube and the slide (forend) riding on it
    lathe(S, 'Dark', (0, 1.6, -0.55), (0, 1, 0), [(0, 0), (0.28, 0), (0.28, 2.3), (0.2, 2.4), (0, 2.4)], segs=18, name='MagTube')
    armour(S, 'Rubber', boxpts((0.9, 1.0, 0.5), ch=0.14), Mx((0, 2.6, -0.5)), bevel=0.03, seams=False, groove=None, name='Forend')
    for kk in range(5):
        armour(S, 'Black', boxpts((0.94, 0.05, 0.54)), Mx((0, 2.2 + kk * 0.2, -0.5)), bevel=0, seams=False, groove=None, name='Grip')
    for y in (1.8, 3.9):
        armour(S, 'Metal', boxpts((0.4, 0.2, 1.0), ch=0.08), Mx((0, y, -0.2)), bevel=0.02, seams=False, groove=None, name='Band')
    # Shell feed box under the receiver
    armour(S, 'Dark', boxpts((0.75, 0.9, 0.7), ch=0.1), Mx((0, 0.5, -0.95)), bevel=0.03, seam_min=0.2, name='Feed')
    stripe(S, Mx((0.38, 0.5, -0.95), (0, 0, -90)), 0.7, 0.1)


def ion_beam():
    """Ion projector: capacitor banks, a focusing column of glowing coils and a crystal lens."""
    cradle_cover(S, 0.8)
    housing(S, 'Secondary', -0.4, 1.7, 0.6, 0.62, 0.2)
    for kk in range(6):
        armour(S, 'Dark', boxpts((1.5, 0.07, 0.36)), Mx((0, -0.1 + kk * 0.3, 0.75)), bevel=0, seams=False, groove=None, name='Fin')
    for s in (-1, 1):
        # Capacitor cans either side, strapped on
        lathe(S, 'Primary', (s * 0.78, -0.2, -0.15), (0, 1, 0), [(0, 0), (0.24, 0), (0.3, 0.08), (0.3, 1.5), (0.24, 1.58), (0, 1.58)], segs=18, name='Capacitor')
        for y in (0.2, 0.95):
            ring(S, y, 0.3, 0.33, 0.07, 'Dark', x=s * 0.78, z=-0.15)
        lathe(S, 'Glow', (s * 0.78, 1.38, -0.15), (0, 1, 0), [(0, 0), (0.18, 0), (0, 0.04)], segs=12, name='CapGlow')
        hose(S, [(s * 0.78, 1.4, 0.15), (s * 0.6, 1.9, 0.3), (s * 0.3, 2.3, 0.3)], 0.07)
    # Focusing column: stepped coils over a ceramic tube, each with a glowing core ring
    lathe(S, 'Metal', (0, 1.6, 0), (0, 1, 0), [(0, 0), (0.22, 0), (0.22, 3.0), (0, 3.0)], segs=18, name='Tube')
    for kk in range(4):
        y = 2.0 + kk * 0.62
        rr = 0.58 - kk * 0.06
        lathe(S, 'Dark', (0, y, 0), (0, 1, 0), [(0.22, -0.16), (rr, -0.13), (rr, 0.13), (0.22, 0.16)], segs=24, name='Coil')
        ring(S, y, 0.22, rr - 0.04, 0.08, 'Glow')
    for a in range(3):
        t = math.radians(90 + a * 120)
        armour(S, 'Dark', [(math.cos(t) * 0.45 + dx, y, math.sin(t) * 0.45 + dz) for y in (1.8, 4.3) for dx, dz in ((0.07, 0.07), (-0.07, -0.07), (0.07, -0.07), (-0.07, 0.07))],
               bevel=0.02, seams=False, groove=None, name='Strut')
    # Lens housing and the crystal
    lathe(S, 'Secondary', (0, 4.3, 0), (0, 1, 0), [(0.2, 0), (0.42, 0.05), (0.46, 0.3), (0.36, 0.42), (0.2, 0.42)], segs=24, name='LensRing')
    lathe(S, 'Glow', (0, 4.45, 0), (0, 1, 0), [(0, 0), (0.3, 0), (0.24, 0.18), (0, 0.28)], segs=20, name='Lens')
    stripe(S, Mx((0.61, 0.6, 0.0), (0, 0, -90)), 1.2, 0.12)


def grenade_launcher():
    """Revolver grenade launcher: a six-chamber drum below a short heavy barrel, on a hinged frame."""
    cradle_cover(S, 0.85)
    housing(S, 'Primary', -0.5, 0.75, 0.7, 0.72, 0.22)
    armour(S, 'Secondary', [(x, y, z) for y in (-0.3, 0.6) for x, z in ((-0.5, 0.72), (0.5, 0.72), (-0.38, 0.92), (0.38, 0.92))], bevel=0.03, seam_min=0.3, name='TopPlate')
    # Frame plates either side of the drum, joined front and back
    for s in (-1, 1):
        # Open side frame: top and bottom rails, so the turning drum shows between them
        for z0, z1 in ((0.3, 0.5), (-1.02, -0.84)):
            armour(S, 'Dark', [(s * x, y, z) for x in (0.5, 0.74) for y in (0.5, 2.05) for z in (z0, z1)], bevel=0.02, seams=False, groove=None, name='Rail')
        bolts_line(S, (s * 0.75, 0.7, -0.93), (s * 0.75, 1.9, -0.93), 4, (s, 0, 0), r=0.05)
    armour(S, 'Dark', boxpts((1.4, 0.25, 1.4), ch=0.15), Mx((0, 2.05, -0.25)), bevel=0.03, seams=False, groove=None, name='FrontPlate')
    # Barrel above the drum's axis, with a heat sleeve and a flared mouth
    lathe(S, 'Dark', (0, 2.0, 0.25), (0, 1, 0), [(0, 0), (0.42, 0), (0.42, 1.55), (0, 1.55)], segs=22, name='Barrel')
    for kk in range(3):
        ring(S, 2.35 + kk * 0.42, 0.42, 0.48, 0.12, 'Metal', z=0.25)
    lathe(S, 'Metal', (0, 3.55, 0.25), (0, 1, 0), [(0.4, 0), (0.52, 0.1), (0.52, 0.42), (0.4, 0.45)], segs=22, name='Mouth')
    lathe(S, 'Black', (0, 4.0, 0.25), (0, 1, 0), [(0, 0), (0.32, 0), (0.32, 0.02), (0, 0.02)], segs=18, name='Bore')
    # Ladder sight
    armour(S, 'Dark', boxpts((0.1, 0.5, 0.5)), Mx((0.3, 2.6, 0.85)), bevel=0.01, seams=False, groove=None, name='Sight')
    armour(S, 'Glow', boxpts((0.11, 0.05, 0.08)), Mx((0.3, 2.85, 1.05)), bevel=0, seams=False, groove=None, name='SightDot')
    # Drum spring housing behind the drum (static)
    lathe(S, 'Metal', (0, 0.45, -0.38), (0, 1, 0), [(0, 0), (0.3, 0), (0.3, 0.2), (0, 0.2)], segs=16, name='Spring')
    # The drum itself (the Spin part): six chambers round a hub, turns 60 degrees per shot
    P = 'Spin'
    c = Vector((0, 1.25, -0.38))
    lathe(P, 'Dark', c - Vector((0, 0.62, 0)), (0, 1, 0), [(0, 0), (0.55, 0), (0.62, 0.08), (0.62, 1.16), (0.55, 1.24), (0, 1.24)], segs=30, name='Drum')
    for n in range(6):
        a = math.radians(90 + n * 60)
        cx, cz = math.cos(a) * 0.36, math.sin(a) * 0.36
        lathe(P, 'Black', (c.x + cx, c.y + 0.6, c.z + cz), (0, 1, 0), [(0, 0), (0.17, 0), (0.17, 0.03), (0, 0.03)], segs=12, name='Chamber')
        lathe(P, 'Hot', (c.x + cx, c.y + 0.56, c.z + cz), (0, 1, 0), [(0, 0), (0.12, 0), (0, 0.05)], segs=10, name='Round')
        # Flutes between the chambers
        a2 = a + math.radians(30)
        armour(P, 'Black', boxpts((0.08, 1.0, 0.08)), Mx((c.x + math.cos(a2) * 0.6, c.y, c.z + math.sin(a2) * 0.6), (0, -math.degrees(a2), 0)),
               bevel=0, seams=False, groove=None, name='Flute')
    lathe(P, 'Metal', c + Vector((0, 0.62, 0)), (0, 1, 0), [(0, 0), (0.16, 0), (0.16, 0.08), (0, 0.1)], segs=12, name='Hub')


def cluster_rack():
    """A short heavy launch tube on trunnions, elevated 50 degrees, fed from a canister hopper."""
    armour(S, 'Dark', boxpts((1.9, 2.1, 0.45), ch=0.18), Mx((0, 0, 0.1)), bevel=0.04, seams=False, groove=None, name='Base')
    armour(S, 'Secondary', boxpts((1.6, 1.8, 0.22), ch=0.14, taper=(0.95, 0.92)), Mx((0, 0, 0.42)), bevel=0.03, seam_min=0.3, name='Deck')
    pivot = Vector((0, -0.05, 0.85))
    ax = Vector((0, math.cos(math.radians(50)), math.sin(math.radians(50))))
    for s in (-1, 1):
        armour(S, 'Primary', [(s * x, y, z) for x in (0.6, 0.78) for y in (-0.6, 0.5) for z in (0.4, 0.95)] + [(s * 0.69, -0.05, 1.25)], bevel=0.02, seam_min=0.3, name='Cheek')
        cyl(S, 'Metal', pivot + Vector((s * 0.69, 0, 0)), (1, 0, 0), 0.2, 0.3, ch=0.03, segs=14, name='Trunnion')
    # The tube: from just behind the pivot out to the muzzle at (0, 1.25, 1.75)
    start = pivot - ax * 0.35
    lathe(S, 'Secondary', start, ax, [(0, 0), (0.55, 0), (0.55, 1.85), (0.62, 1.9), (0.62, 2.05), (0.47, 2.05), (0.47, 1.95), (0, 1.95)], segs=26, name='Tube')
    for t in (0.5, 1.2):
        lathe(S, 'Dark', start + ax * t, ax, [(0.55, -0.09), (0.6, -0.09), (0.6, 0.09), (0.55, 0.09)], segs=26, name='Band')
    lathe(S, 'Hot', start + ax * 1.6, ax, [(0, 0), (0.4, 0), (0.3, 0.2), (0, 0.32)], segs=18, name='Canister')
    stripe(S, Mx((0.56, 0.25, 1.3), (-40, 0, -90)), 0.9, 0.12)
    # Elevation ram under the tube and a hopper of spare canisters behind
    piston(S, (0, -0.7, 0.45), start + ax * 0.9 + Vector((0, 0, -0.5)), 0.11)
    armour(S, 'Dark', boxpts((1.2, 0.8, 0.9), ch=0.12, endch=0.04), Mx((0, -0.9, 0.85)), bevel=0.03, seam_min=0.2, name='Hopper')
    for s in (-1, 1):
        lathe(S, 'Hot', (s * 0.3, -0.9, 1.3), (0, 0, 1), [(0, 0), (0.22, 0), (0.15, 0.12), (0, 0.18)], segs=12, name='SpareNose')
    hazard(S, Mx((0, -1.31, 0.85), (0, 0, 180)), 1.0, 0.25, n=4)


def hellfire_cells():
    """Vertical launch box: two big cells with their armoured hatches blown open, side exhaust vents."""
    armour(S, 'Secondary', boxpts((1.9, 1.35, 1.3), ch=0.16, endch=0.05), Mx((0, 0, 0.62)), bevel=0.04, seam_min=0.35, name='Box')
    armour(S, 'Dark', boxpts((2.0, 1.45, 0.25), ch=0.18), Mx((0, 0, 0.05)), bevel=0.03, seams=False, groove=None, name='Base')
    for s in (-1, 1):
        x = s * 0.45
        # Cell mouth, a rocket nose waiting inside, and the hatch flipped open outward
        armour(S, 'Black', boxpts((0.72, 0.72, 0.06)), Mx((x, 0, 1.29)), bevel=0, seams=False, groove=None, name='Mouth')
        lathe(S, 'Hot', (x, 0, 1.0), (0, 0, 1), [(0, 0), (0.24, 0), (0.24, 0.12), (0.14, 0.26), (0, 0.32)], segs=16, name='Nose')
        armour(S, 'Primary', boxpts((0.74, 0.74, 0.08), ch=0.1), Mx((x + s * 0.4, 0, 1.62), (0, s * 72, 0)) @ Mx((s * 0.37, 0, 0)), bevel=0.02, seams=False, groove=None, name='Hatch')
        cyl(S, 'Metal', (x + s * 0.37, 0, 1.32), (0, 1, 0), 0.06, 0.8, ch=0.01, segs=8, name='Hinge')
        grille(S, Mx((s * 0.96, 0, 0.6), (0, 0, s * 90)), 1.0, 0.6, slats=4, depth=0.06)
    hazard(S, Mx((0, 0.68, 0.95)), 1.6, 0.22, n=6)
    stripe(S, Mx((0, -0.68, 0.95), (0, 0, 180)), 1.6, 0.14)
    bolts_line(S, (-0.85, 0.68, 0.25), (0.85, 0.68, 0.25), 6, (0, 1, 0), r=0.05)
    hose(S, [(-0.8, -0.7, 0.4), (-1.0, -0.8, 0.8), (-0.9, -0.6, 1.2)], 0.07)


def emp_emitter():
    """EMP projector: a stacked coil tower over a capacitor ring, crowned by a four-prong emitter."""
    armour(S, 'Dark', ngon(-0.3, 0.85, 8, 22.5) + ngon(0.3, 0.78, 8, 22.5), bevel=0.04, seams=False, groove=None, name='Base')
    for a in range(6):
        t = math.radians(a * 60)
        p = (math.cos(t) * 0.6, math.sin(t) * 0.6, 0.3)
        lathe(S, 'Primary', p, (0, 0, 1), [(0, 0), (0.16, 0), (0.18, 0.05), (0.18, 0.55), (0.12, 0.6), (0, 0.6)], segs=12, name='Cap')
        lathe(S, 'Glow', (p[0], p[1], 0.86), (0, 0, 1), [(0, 0), (0.1, 0), (0, 0.04)], segs=8, name='CapGlow')
    lathe(S, 'Metal', (0, 0, 0.3), (0, 0, 1), [(0, 0), (0.22, 0), (0.2, 1.4), (0, 1.4)], segs=14, name='Mast')
    for r in range(3):
        z = 0.75 + r * 0.32
        rr = 0.5 - r * 0.08
        lathe(S, 'Dark', (0, 0, z), (0, 0, 1), [(0.2, -0.08), (rr, -0.06), (rr, 0.06), (0.2, 0.08)], segs=24, name='Coil')
        ring_pts = [(0.2, -0.03), (rr - 0.03, -0.03), (rr - 0.03, 0.03), (0.2, 0.03)]
        lathe(S, 'Glow', (0, 0, z), (0, 0, 1), ring_pts, segs=24, name='CoilGlow')
    # Emitter head: a toroid with four prongs angled outward and the glowing core at (0, 0, 2.0)
    lathe(S, 'Metal', (0, 0, 1.75), (0, 0, 1), [(0.0, -0.15), (0.42, -0.1), (0.5, 0), (0.42, 0.1), (0, 0.15)], segs=24, name='Toroid')
    for a in range(4):
        t = math.radians(45 + a * 90)
        d = Vector((math.cos(t), math.sin(t), 0))
        lathe(S, 'Dark', Vector((0, 0, 1.75)) + d * 0.35, (d * 0.6 + Vector((0, 0, 1))).normalized(), [(0, 0), (0.06, 0), (0.04, 0.6), (0, 0.62)], segs=8, name='Prong')
        lathe(S, 'Glow', Vector((0, 0, 1.75)) + d * 0.35 + (d * 0.6 + Vector((0, 0, 1))).normalized() * 0.6, (0, 0, 1), [(0, -0.06), (0.07, 0), (0, 0.06)], segs=8, name='ProngTip')
    lathe(S, 'Glow', (0, 0, 2.0), (0, 0, 1), [(0, -0.28), (0.24, -0.2), (0.28, 0), (0.24, 0.2), (0, 0.28)], segs=20, name='Core')
    hazard(S, Mx((0, 0.82, 0)), 0.9, 0.2, n=4)

ALL = [('HeavyCannon', heavy_cannon, {}), ('PulseLaser', pulse_laser, {}), ('Gatling', gatling, {'spin_origin': (0, 1.9, 0)}),
       ('RocketPods', rocket_pods, {}), ('Flamethrower', flamethrower, {}), ('RailRifle', rail_rifle, {'dist': 14}),
       ('Railgun', railgun, {}), ('Mortar2', lambda: mortar(2), {'view_target': (0, 0, 0.5)}), ('Mortar3', lambda: mortar(3), {'view_target': (0, 0, 0.5)}),
       ('TeslaCoil', tesla_coil, {'view_target': (0, 0, 1)}), ('TeslaBank', tesla_bank, {'view_target': (0, 0, 0.6)}),
       ('Swarm3', lambda: swarm(3), {'view_target': (0, 0, 0.3)}), ('Swarm4', lambda: swarm(4), {'view_target': (0, 0, 0.3)}),
       ('Autocannon', autocannon, {'dist': 13}), ('ScatterCannon', scatter_cannon, {}), ('IonBeam', ion_beam, {}),
       ('GrenadeLauncher', grenade_launcher, {'spin_origin': (0, 1.25, -0.38)}),
       ('ClusterRack', cluster_rack, {'view_target': (0, 0, 0.8)}), ('HellfireCells', hellfire_cells, {'view_target': (0, 0, 0.7)}),
       ('EmpEmitter', emp_emitter, {'view_target': (0, 0, 1.0)})]

if __name__ == '__main__':
    only = [a for a in sys.argv[1:] if a in [n for n, _, _ in ALL]]
    for name, fn, kw in ALL:
        if only and name not in only:
            continue
        build(name, fn, **kw)
        print('built', name)
