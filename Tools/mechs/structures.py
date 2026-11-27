"""
Scenery and objective models (2026-11-27): the power core and the landmark buildings
of the Highland Dam, Launch Site and Airbase maps. One file per model in
Assets/Resources/RobotModels/Struct<Name>.bytes, placed by SceneryModels.Place (Objectives2.cs).

Modelled in metres at real size: origin on the ground at the model's centre,
Blender +Y = Unity +Z (the model's front), +Z = up. The same ten material slots as the
robots; the game fills them with scenery paints (Primary = main paint, Secondary = team or
second colour, Accent = hazard paint, Glow = lamps and screens in the team colour).

Parts:
  'Body'      everything that is drawn (no collider of its own when a Col part exists)
  'NoColXxx'  drawn, never collides (cables, lamps, thin rails)
  'ColXxx'    simple invisible hull for physics, so robots and shots get a cheap collider

Usage: python structures.py [Names...] [--render] [-- out_dir]
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mechlib as ml
from mechlib import reset, lathe, tube, loft, finish, frame_identity, render
from mathutils import Vector, Matrix
from hardsurf import *

OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Resources', 'RobotModels')
RENDER = '--render' in sys.argv
B = 'Body'
D = 'NoColDetail'
C = 'ColHull'


# ------------------------------------------------------------------ helpers

def beam(part, mat, a, b, w, h=None, up=(0, 0, 1), name='Beam'):
    """A square-section member from a to b (w wide, h deep), its depth along `up` where possible."""
    h = w if h is None else h
    A, Bb = Vector(a), Vector(b)
    ax = (Bb - A).normalized()
    u = Vector(up)
    if abs(ax.dot(u)) > 0.95:
        u = Vector((1, 0, 0)) if abs(ax.x) < 0.9 else Vector((0, 1, 0))
    side = ax.cross(u).normalized()
    up2 = side.cross(ax).normalized()
    pts = [p + side * sx * w / 2 + up2 * sz * h / 2 for p in (A, Bb) for sx in (-1, 1) for sz in (-1, 1)]
    return armour(part, mat, pts, bevel=0.0, seams=False, groove=None, name=name)


def ibeam(part, mat, a, b, w, h, up=(0, 0, 1)):
    """An I-section girder: two flanges and a web."""
    A, Bb = Vector(a), Vector(b)
    ax = (Bb - A).normalized()
    u = Vector(up)
    if abs(ax.dot(u)) > 0.95:
        u = Vector((1, 0, 0)) if abs(ax.x) < 0.9 else Vector((0, 1, 0))
    side = ax.cross(u).normalized()
    up2 = side.cross(ax).normalized()
    t = max(0.04, h * 0.12)
    for s in (-1, 1):
        o = up2 * s * (h / 2 - t / 2)
        beam(part, mat, A + o, Bb + o, w, t, up=up2, name='Flange')
    beam(part, mat, A, Bb, t, h - 2 * t, up=up2, name='Web')


def lattice_mast(part, mat, base_w, top_w, z0, z1, bays, chord=0.3, brace=0.14, cx=0.0, cy=0.0):
    """A four-legged lattice tower tapering from base_w to top_w, with X bracing on every face."""
    def corner(z, sx, sy):
        t = (z - z0) / (z1 - z0)
        w = base_w + (top_w - base_w) * t
        return Vector((cx + sx * w / 2, cy + sy * w / 2, z))
    cs = ((1, 1), (-1, 1), (-1, -1), (1, -1))
    for sx, sy in cs:
        beam(part, mat, corner(z0, sx, sy), corner(z1, sx, sy), chord, name='Chord')
    for k in range(bays):
        za = z0 + (z1 - z0) * k / bays
        zb = z0 + (z1 - z0) * (k + 1) / bays
        for i in range(4):
            a, b = cs[i], cs[(i + 1) % 4]
            beam(part, mat, corner(zb, *a), corner(zb, *b), brace, name='Strut')
            beam(part, mat, corner(za, *a), corner(zb, *b), brace * 0.8, name='Brace')
            beam(part, mat, corner(za, *b), corner(zb, *a), brace * 0.8, name='Brace')


def railing(part, pts, h=1.1, post=1.6, r=0.05, mat='Accent'):
    """Hand rail along a polyline: posts, a top rail and a knee rail."""
    P = [Vector(p) for p in pts]
    for i in range(len(P) - 1):
        a, b = P[i], P[i + 1]
        L = (b - a).length
        n = max(1, int(L / post))
        for k in range(n + 1):
            p = a.lerp(b, k / n)
            beam(part, mat, p, p + Vector((0, 0, h)), r * 1.6, name='Post')
        for z in (h, h * 0.5):
            beam(part, mat, a + Vector((0, 0, z)), b + Vector((0, 0, z)), r * 1.4, name='Rail')


def ladder(part, a, h, facing, w=0.7, mat='Metal', cage=True):
    """A vertical ladder at a (bottom centre) climbing h, rungs spanning across `facing`'s side axis."""
    A = Vector(a)
    f = Vector(facing).normalized()
    s = f.cross(Vector((0, 0, 1))).normalized()
    for sx in (-1, 1):
        beam(part, mat, A + s * sx * w / 2, A + s * sx * w / 2 + Vector((0, 0, h)), 0.08, name='Stile')
    n = int(h / 0.35)
    for k in range(1, n):
        z = k * h / n
        beam(part, mat, A - s * w / 2 + Vector((0, 0, z)), A + s * w / 2 + Vector((0, 0, z)), 0.05, name='Rung')
    if cage:
        for k in range(int((h - 2.4) / 1.2)):
            z = 2.4 + k * 1.2
            pts = [A + Vector((0, 0, z)) + s * math.cos(t) * w * 0.75 + f * (0.4 + math.sin(t) * 0.6) for t in [math.pi * i / 6 for i in range(7)]]
            for i in range(6):
                beam(part, mat, pts[i], pts[i + 1], 0.05, name='Hoop')


def concrete(part, mat, size, M, ch=0.15, bevel=0.08, seams=True, seam_min=6.0):
    """A cast block with chamfered edges and big panel seams."""
    return armour(part, mat, boxpts(size, ch=ch), M, bevel=bevel, seams=seams, seam_min=seam_min, seam_w=0.35, name='Concrete')


def col_box(size, M):
    armour(C, 'Dark', boxpts(size), M, bevel=0.0, seams=False, groove=None, name='Col')


def col_cyl(centre, axis, r, length, segs=10):
    cyl(C, 'Dark', centre, axis, r, length, ch=0.0, segs=segs, name='Col')


def save(name, view_target=(0, 0, 6), dist=45, lens=50):
    frames = {n: frame_identity(Vector((0, 0, 0))) for n in ml.PARTS}
    finish(os.path.join(OUT, 'Struct%s.bytes' % name), frames, {}, {}, [], {'stand': 0})
    if RENDER:
        import bpy
        # Hide the collider hulls from the preview
        for ob in ml.PARTS.get(C, []):
            ob.hide_render = True
        render(os.path.join(OUT, 's_%s.png' % name.lower()), views=tuple(tuple(float(x) for x in v.split(',')) for v in os.environ.get('WVIEWS', '35,18').split(';')),
               target=view_target, dist=dist, res=(1000, 680), samples=20, lens=lens)


def build(name, fn, **kw):
    reset()
    ml.PARTS.clear()
    fn()
    save(name, **kw)


# ------------------------------------------------------------------ power core

def power_core():
    """
    A base fusion generator: an octagonal stepped plinth, eight armoured buttresses with
    radiator fins round a glowing containment column, a catwalk, and a heavy cap with
    vent stacks and an emitter dome. About 21 m across and 19 m tall.
    """
    # Stepped plinth
    armour(B, 'Dark', ngon(0, 10.6, 8, 22.5) + ngon(0.7, 10.6, 8, 22.5), bevel=0.12, seams=False, groove=None, name='Plinth')
    armour(B, 'Secondary', ngon(0.7, 9.8, 8, 22.5) + ngon(1.6, 9.3, 8, 22.5), bevel=0.1, seam_min=3.0, seam_w=0.3, name='Plinth2')
    for k in range(8):
        t = math.radians(k * 45 + 22.5)
        d = Vector((math.cos(t), math.sin(t), 0))
        n = Vector((math.cos(math.radians(k * 45)), math.sin(math.radians(k * 45)), 0))
        # Hazard band round the plinth edge
        hazard(D, Mx(tuple(d * 9.55 * math.cos(math.radians(22.5)) * 1.0 + Vector((0, 0, 1.15))), (0, 0, math.degrees(math.atan2(d.y, d.x)) - 90)), 6.2, 0.55, n=7)
        # Lamps on the plinth corners
        c = n * 10.0
        lathe(D, 'Dark', (c.x, c.y, 0.7), (0, 0, 1), [(0, 0), (0.35, 0), (0.35, 0.3), (0.25, 0.35), (0, 0.35)], segs=12, name='LampBase')
        lathe(D, 'Glow', (c.x, c.y, 1.05), (0, 0, 1), [(0, 0), (0.22, 0), (0.22, 0.25), (0, 0.4)], segs=12, name='Lamp')

    # Fusion column and its containment cage
    lathe(B, 'Glow', (0, 0, 1.6), (0, 0, 1), [(0, 0), (2.3, 0), (2.3, 13.6), (0, 13.6)], segs=40, name='Column')
    lathe(B, 'Dark', (0, 0, 1.6), (0, 0, 1), [(0, 0), (3.6, 0), (3.6, 1.0), (3.0, 1.4), (2.4, 1.4), (2.4, 0)], segs=40, name='ColumnFoot')
    for k in range(16):
        t = 2 * math.pi * k / 16
        p = Vector((math.cos(t), math.sin(t), 0)) * 2.75
        beam(B, 'Chrome', p + Vector((0, 0, 2.8)), p + Vector((0, 0, 15.0)), 0.22, name='CageBar')
    for z in (4.6, 8.4, 12.2):
        lathe(B, 'Metal', (0, 0, z), (0, 0, 1), [(2.3, -0.5), (3.2, -0.45), (3.35, -0.3), (3.35, 0.3), (3.2, 0.45), (2.3, 0.5)], segs=40, name='Ring')
        lathe(B, 'Glow', (0, 0, z), (0, 0, 1), [(3.36, -0.1), (3.4, -0.1), (3.4, 0.1), (3.36, 0.1)], segs=40, name='RingGlow')
        for k in range(8):
            t = 2 * math.pi * (k + 0.5) / 8
            p = Vector((math.cos(t), math.sin(t), 0)) * 3.35
            lathe(D, 'Dark', (p.x, p.y, z), (math.cos(t), math.sin(t), 0), [(0, 0), (0.22, 0), (0.22, 0.25), (0, 0.3)], segs=8, name='Bolt')

    # Eight armoured buttresses, open between them so the column glows through
    for k in range(8):
        a = k * 45
        M = Mx((0, 0, 1.6), (0, 0, a))
        # Profile in local X (radius) / Z (height): wide foot, sloped outer face, narrow top
        pts = []
        for y in (-1.25, 1.25):
            for x, z in ((4.0, 0), (8.6, 0), (8.6, 1.4), (6.4, 8.6), (4.0, 10.4), (4.0, 0)):
                pts.append((x, y, z))
        armour(B, 'Primary', pts, M, bevel=0.1, seam_min=2.0, seam_w=0.3, name='Buttress')
        # Team-paint cheek plates on the sloped face
        slope = Vector((8.6 - 6.4, 0, 1.4 - 8.6)).normalized()
        nrm = Vector((-slope.z, 0, slope.x))
        if nrm.x < 0:
            nrm = -nrm
        mid = Vector((7.5, 0, 5.0)) + nrm * 0.14
        Mp = M @ Matrix.Translation(mid) @ Matrix.Rotation(math.atan2(-slope.x, -slope.z), 4, 'Y')
        armour(B, 'Secondary', boxpts((0.24, 2.7, 6.6), ch=0.3), Mp, bevel=0.05, seam_min=2.0, seam_w=0.2, name='Cheek')
        stripe(D, Mp @ Mx((0.13, 0, 2.2), (0, 0, -90)), 2.0, 0.45)
        # Radiator fin stack on top of the buttress, leaning inward
        for f in range(7):
            x = 4.4 + f * 0.32
            zt = 10.2 - (x - 4.0) * 0.75
            armour(B, 'Metal', boxpts((0.09, 2.2, 2.3)), M @ Mx((x, 0, zt + 0.6)), bevel=0.0, seams=False, groove=None, name='Fin')
        # Coolant pipes running down the buttress side
        for s in (-1, 1):
            pts2 = [M @ Vector((4.3, s * 1.45, 9.8)), M @ Vector((6.4, s * 1.45, 8.0)), M @ Vector((8.5, s * 1.45, 1.6)), M @ Vector((9.3, s * 1.45, 0.4))]
            tube(D, 'Metal', pts2, 0.2, segs=10, name='Pipe')
            for z in (3.0, 6.0):
                pz = M @ Vector((7.0 + (z == 3.0) * 1.1, s * 1.45, z + 0.3))
                lathe(D, 'Dark', pz, (0, 0, 1), [(0, -0.15), (0.32, -0.12), (0.32, 0.12), (0, 0.15)], segs=10, name='Clamp')
        # A maintenance hatch and lamp on the foot
        armour(B, 'Dark', boxpts((0.2, 1.4, 1.8), ch=0.15), M @ Mx((8.65, 0, 0.95)), bevel=0.04, seams=False, groove=None, name='Hatch')
        lamp(D, M @ Mx((8.8, 0, 2.0), (0, 0, -90)), 0.6, 0.18)

    # Catwalk ring at mid height, between the buttresses, with railings
    lathe(B, 'Dark', (0, 0, 8.8), (0, 0, 1), [(3.4, 0), (5.4, 0), (5.4, 0.18), (3.4, 0.18)], segs=40, name='Catwalk')
    for k in range(8):
        a0 = math.radians(k * 45 + 9)
        a1 = math.radians(k * 45 + 36)
        pts3 = [(math.cos(a0 + (a1 - a0) * i / 3) * 5.3, math.sin(a0 + (a1 - a0) * i / 3) * 5.3, 8.98) for i in range(4)]
        railing(D, pts3, h=1.0, post=1.2)
    ladder(D, (0, -8.95, 1.6), 7.3, (0, -1, 0), cage=False)
    # Ladder lands on the catwalk through a gap in the front buttress pair: it runs up the plinth face
    beam(D, 'Dark', (-0.5, -5.4, 8.9), (-0.5, -8.9, 8.9), 0.12, name='Landing')
    beam(D, 'Dark', (0.5, -5.4, 8.9), (0.5, -8.9, 8.9), 0.12, name='Landing')

    # Heavy cap
    armour(B, 'Dark', ngon(15.0, 4.6, 8, 22.5) + ngon(15.6, 6.4, 8, 22.5) + ngon(17.0, 6.4, 8, 22.5) + ngon(17.5, 5.6, 8, 22.5), bevel=0.1, seam_min=2.0, seam_w=0.3, name='Cap')
    armour(B, 'Primary', ngon(17.5, 5.2, 8, 22.5) + ngon(17.9, 4.4, 8, 22.5), bevel=0.08, seam_min=1.0, name='CapTop')
    for k in range(8):
        t = math.radians(k * 45)
        d = Vector((math.cos(t), math.sin(t), 0))
        stripe(D, Mx(tuple(d * 6.35 * math.cos(math.radians(22.5)) + Vector((0, 0, 16.3))), (0, 0, math.degrees(t) - 90)), 3.6, 0.35)
    # Vent stacks
    for k in range(4):
        t = math.radians(k * 90 + 45)
        p = Vector((math.cos(t), math.sin(t), 0)) * 3.6
        lathe(B, 'Metal', (p.x, p.y, 17.8), (0, 0, 1), [(0, 0), (0.75, 0), (0.75, 1.6), (0.9, 1.7), (0.9, 2.1), (0.6, 2.1), (0.6, 1.9), (0, 1.9)], segs=16, name='Stack')
        lathe(D, 'Hot', (p.x, p.y, 19.75), (0, 0, 1), [(0, 0), (0.6, 0), (0, 0.02)], segs=16, name='StackGlow')
    # Emitter dome and antenna
    lathe(B, 'Metal', (0, 0, 17.9), (0, 0, 1), [(0, 0), (2.6, 0), (2.6, 0.5), (2.2, 0.7), (0, 0.7)], segs=32, name='DomeBase')
    lathe(B, 'Glow', (0, 0, 18.6), (0, 0, 1), [(2.0, 0)] + [(2.0 * math.cos(math.radians(a)), 1.6 * math.sin(math.radians(a))) for a in range(15, 91, 15)], segs=32, name='Dome')
    for k in range(6):
        t = 2 * math.pi * k / 6
        p = Vector((math.cos(t), math.sin(t), 0))
        tube(D, 'Dark', [p * 2.05 + Vector((0, 0, 18.6)), p * 1.6 + Vector((0, 0, 19.7)), p * 0.3 + Vector((0, 0, 20.25))], 0.08, segs=6, name='DomeRib')
    lathe(D, 'Metal', (0, 0, 20.1), (0, 0, 1), [(0, 0), (0.12, 0), (0.06, 3.0), (0, 3.0)], segs=8, name='Antenna')
    lathe(D, 'Glow', (0, 0, 23.1), (0, 0, 1), [(0, -0.2), (0.2, 0), (0, 0.2)], segs=8, name='Beacon')

    # Cable trunks out to junction boxes on four sides
    for k in range(4):
        t = math.radians(k * 90 + 22.5)
        d = Vector((math.cos(t), math.sin(t), 0))
        s = Vector((-d.y, d.x, 0))
        for o in (-0.5, 0, 0.5):
            tube(D, 'Black', [d * 9.0 + s * o + Vector((0, 0, 1.0)), d * 10.6 + s * o + Vector((0, 0, 0.5)), d * 12.0 + s * o + Vector((0, 0, 0.25)), d * 13.2 + s * o * 0.6 + Vector((0, 0, 0.6))], 0.18, segs=8, name='Cable')
        jb = d * 14.0
        armour(B, 'Secondary', boxpts((1.8, 1.4, 2.0), ch=0.15, endch=0.08), Mx((jb.x, jb.y, 1.0), (0, 0, math.degrees(t) - 90)), bevel=0.04, seam_min=0.8, name='Junction')
        grille(D, Mx(tuple(jb + d * 0.72 + Vector((0, 0, 1.1))), (0, 0, math.degrees(t) - 90)), 1.2, 0.8, slats=4, depth=0.08)
        lamp(D, Mx(tuple(jb + Vector((0, 0, 2.05))), (90, 0, math.degrees(t) - 90)), 0.4, 0.4)

    # Physics hull: plinth, buttress ring, cap, dome
    col_cyl((0, 0, 0.8), (0, 0, 1), 10.4, 1.6, segs=8)
    col_cyl((0, 0, 6.2), (0, 0, 1), 7.6, 9.2, segs=8)
    col_cyl((0, 0, 13.0), (0, 0, 1), 4.6, 4.4, segs=8)
    col_cyl((0, 0, 16.6), (0, 0, 1), 6.3, 2.2, segs=8)
    col_cyl((0, 0, 19.2), (0, 0, 1), 2.4, 1.6, segs=8)
    for k in range(4):
        t = math.radians(k * 90 + 22.5)
        jb = Vector((math.cos(t), math.sin(t), 0)) * 14.0
        col_box((1.8, 1.4, 2.0), Mx((jb.x, jb.y, 1.0), (0, 0, math.degrees(t) - 90)))


def box(part, mat, centre, size, rot=(0, 0, 0), ch=0.0, bevel=0.05, seams=False, seam_min=4.0, taper=(1.0, 1.0), name='Box'):
    """A chamfered box at centre (size = full x, y, z), rotated by rot (degrees)."""
    return armour(part, mat, boxpts(size, ch=ch, taper=taper), Mx(centre, rot), bevel=bevel, seams=seams, seam_min=seam_min, seam_w=0.3,
                  groove='Black' if seams else None, name=name)


# ------------------------------------------------------------------ highland dam

def dam():
    """
    A concrete arch dam about 500 m long and 112 m high, seen from downstream (+Y):
    buttress ribs, a gated spillway with water running down its chute, penstocks
    into powerhouses at the foot, a crest road with lamps and the reservoir behind.
    """
    Rr, half = 420.0, math.radians(35.0)
    C = Vector((0, Rr, 0))
    n = 40
    H = 112.0
    prof = [(-6, H), (6, H), (6, H - 12), (52, 0), (-11, 0)]   # (d downstream, z)

    def at(th, d, z):
        u = Vector((math.sin(th), -math.cos(th), 0))     # upstream direction
        return C + u * (Rr - d) + Vector((0, 0, z))

    spill = math.radians(5.0)
    for i in range(n):
        a = -half + 2 * half * i / n
        b = -half + 2 * half * (i + 1) / n
        mid = (a + b) / 2
        mat = 'Primary'
        pts = [at(t, d, z) for t in (a, b) for d, z in prof]
        armour(B, mat, pts, bevel=0.4, seams=False, groove=None, name='DamBlock')
        # Contraction joint: a dark rib up the downstream face
        if i > 0:
            rib = [at(a, d + o, z) for d, z in ((6, H - 12), (52, 0)) for o in (0, 1.6)]
            rib += [at(a + 0.004 * s, d + 1.6, z) for d, z in ((6, H - 12), (52, 0)) for s in (-1, 1)]
            armour(B, 'Dark', rib, bevel=0.0, seams=False, groove=None, name='Rib')
        # Crest parapets and lamp posts
        if abs(mid) > spill:
            for d0 in (-6.0, 5.2):
                armour(B, 'Secondary', [at(t, d, z) for t in (a, b) for d in (d0, d0 + 0.8) for z in (H, H + 1.4)], bevel=0.05, seams=False, groove=None, name='Parapet')
            if i % 2 == 0:
                p = at(mid, 5.6, H + 1.4)
                beam(D, 'Dark', p, p + Vector((0, 0, 7)), 0.3, name='LampPost')
                q = at(mid, 3.6, H + 8.2)
                beam(D, 'Dark', p + Vector((0, 0, 7)), q, 0.25, name='LampArm')
                box(D, 'Glow', q - Vector((0, 0, 0.25)), (1.2, 0.6, 0.3), name='Lamp')
        # Inspection galleries: dark slots across the face
        for z in (30.0, 62.0, 88.0):
            d = 52 - (52 - 6) * z / (H - 12) + 0.15
            if abs(mid) > spill and i % 3 == 1:
                armour(D, 'Black', [at(mid + o, d + e, z + h) for o in (-0.006, 0.006) for e in (0, 0.2) for h in (0, 2.4)], bevel=0, seams=False, groove=None, name='Gallery')

    # Crest road surface
    for i in range(n):
        a = -half + 2 * half * i / n
        b = -half + 2 * half * (i + 1) / n
        armour(B, 'Dark', [at(t, d, z) for t in (a, b) for d in (-5.2, 5.2) for z in (H, H + 0.25)], bevel=0, seams=False, groove=None, name='Road')

    # Spillway: five gate bays between piers, gate hoists above, water down the chute
    bays = 5
    for k in range(bays + 1):
        t = -spill + 2 * spill * k / bays
        armour(B, 'Primary', [at(t + o, d, z) for o in (-0.0035, 0.0035) for d, z in ((-8, H + 14), (8, H + 14), (8, H - 10), (20, H - 40), (-8, H - 40))], bevel=0.25, seams=False, groove=None, name='Pier')
        if k < bays:
            m = t + spill / bays
            # Hoist house on top
            hp = at(m, 0, H + 14)
            armour(B, 'Secondary', [at(m + o, d, H + 14 + h) for o in (-0.012, 0.012) for d in (-5, 5) for h in (0, 6)], bevel=0.15, seams=False, groove=None, name='Hoist')
            armour(D, 'Glow', [at(m + o, 5.05, H + 17 + h) for o in (-0.004, 0.004) for h in (0, 1.0)] + [at(m + o, 5.2, H + 17 + h) for o in (-0.004, 0.004) for h in (0, 1.0)], bevel=0, seams=False, groove=None, name='Window')
            # Radial gate, partly raised
            armour(B, 'Metal', [at(m + o, d, z) for o in (-0.0135, 0.0135) for d, z in ((5.5, H - 2), (7.5, H - 2), (8.5, H + 6), (6.5, H + 6))], bevel=0.05, seams=False, groove=None, name='Gate')
            # Water sheet: down the chute from the gate lip to the flip bucket
            ws = []
            for o in (-0.0135, 0.0135):
                ws += [at(m + o, 8.6, H - 3), at(m + o, 9.4, H - 3), at(m + o, 52.4, 0.6), at(m + o, 53.4, 1.4)]
            armour(D, 'Chrome', ws, bevel=0, seams=False, groove=None, name='Water')
    # Chute training walls and the flip bucket at the foot
    for t in (-spill, spill):
        armour(B, 'Primary', [at(t + o, d, z + h) for o in (-0.003, 0.003) for d, z in ((8, H - 6), (52, 0), (62, 0)) for h in (0, 4)], bevel=0.15, seams=False, groove=None, name='TrainWall')
    armour(B, 'Primary', [at(t, d, z) for t in (-spill, spill) for d, z in ((52, 0), (62, 0), (62, 6), (56, 3))], bevel=0.2, seams=False, groove=None, name='Bucket')
    for k in range(10):
        t = -spill + 2 * spill * (k + 0.5) / 10
        p = at(t, 63 + (k % 3) * 2, 6 + (k % 4) * 2)
        lathe(D, 'Chrome', p, (0, 0, 1), [(0, -3)] + [(4 * math.cos(math.radians(a)), 4 * math.sin(math.radians(a))) for a in range(-60, 91, 30)], segs=10, name='Spray')

    # Penstocks and powerhouses on each side of the spillway
    for s in (-1, 1):
        for j in range(3):
            t = s * math.radians(11 + j * 2.2)
            pts = [at(t, 3 + 26 * 0, 70) + Vector((0, 0, 0))]
            pts = [at(t, 6 + (52 - 6) * (H - 12 - z) / (H - 12) + 2.6, z + 2.6) for z in (76, 50, 22)] + [at(t, 66, 6)]
            tube(B, 'Metal', pts, 2.4, segs=16, name='Penstock')
            for z in (68, 52, 36, 20):
                c = at(t, 6 + (52 - 6) * (H - 12 - z) / (H - 12) + 1.0, z)
                armour(B, 'Secondary', [at(t + o, 6 + (52 - 6) * (H - 12 - zz) / (H - 12) + e, zz) for o in (-0.007, 0.007) for zz in (z - 1.5, z + 1.5) for e in (0, 2.4)], bevel=0.1, seams=False, groove=None, name='Anchor')
        tm = s * math.radians(13.2)
        armour(B, 'Secondary', [at(tm + o, d, z) for o in (-0.055, 0.055) for d in (58, 82) for z in (0, 22)], bevel=0.3, seams=False, groove=None, name='Powerhouse')
        armour(B, 'Dark', [at(tm + o, d, z) for o in (-0.057, 0.057) for d in (57, 84) for z in (22, 24)], bevel=0.1, seams=False, groove=None, name='PHRoof')
        for w in range(8):
            o = -0.045 + 0.09 * w / 7
            armour(D, 'Glow' if w % 3 else 'Black', [at(tm + o + e, 82.2 + f, z) for e in (-0.003, 0.003) for f in (0, 0.2) for z in (12, 18)], bevel=0, seams=False, groove=None, name='PHWindow')
        for k in range(4):
            p = at(tm - 0.04 + 0.027 * k, 86, 0)
            transformer_at(p, s)

    # Reservoir behind the dam
    armour(D, 'Chrome', [at(t, -6, H - 4) for t in (-half - 0.02, half + 0.02)] + [at(t, -500, H - 4) for t in (-half - 0.2, half + 0.2)] +
           [at(t, -6, H - 4.5) for t in (-half - 0.02, half + 0.02)] + [at(t, -500, H - 4.5) for t in (-half - 0.2, half + 0.2)], bevel=0, seams=False, groove=None, name='Reservoir')


def transformer_at(p, s=1, k=1.0):
    """A small step-up transformer block (used beside the dam's powerhouses)."""
    box(B, 'Dark', p + Vector((0, 0, 0.5 * k)), (7 * k, 5 * k, 1 * k), ch=0.2 * k)
    box(B, 'Secondary', p + Vector((0, 0, 3.5 * k)), (5 * k, 3.4 * k, 5 * k), ch=0.3 * k, seams=True)
    for sx in (-1, 1):
        for f in range(6):
            box(B, 'Metal', p + Vector((sx * 3.0 * k, (-1.2 + f * 0.48) * k, 3.4 * k)), (1.0 * k, 0.12 * k, 4.2 * k), bevel=0)
    for b in range(3):
        c = p + Vector(((b - 1) * 1.4 * k, 0, 6.0 * k))
        lathe(D, 'Rubber', c, (0, 0, 1), [(0, 0)] + [(0.35 * k if q % 2 == 0 else 0.22 * k, q * 0.22 * k) for q in range(9)] + [(0, 8 * 0.22 * k)], segs=10, name='Bushing')


def transformer():
    """A substation transformer: oil tank, radiator banks, conservator tank and three bushings."""
    box(B, 'Dark', (0, 0, 0.4), (9.0, 6.4, 0.8), ch=0.2, seams=True, seam_min=8)
    box(B, 'Secondary', (0, 0, 3.4), (5.6, 3.8, 5.2), ch=0.35, seams=True, seam_min=4)
    box(B, 'Secondary', (0, 0, 6.05), (6.0, 4.2, 0.3), ch=0.4)
    for sx in (-1, 1):
        for f in range(9):
            box(B, 'Metal', (sx * 3.6, -1.6 + f * 0.4, 3.3), (1.4, 0.1, 4.4), bevel=0)
        for z in (1.3, 5.3):
            cyl(B, 'Dark', (sx * 3.6, 0, z), (0, 1, 0), 0.18, 3.6, segs=10, name='Header')
        cyl(D, 'Metal', (sx * 2.9, 0, 1.3), (1, 0, 0), 0.16, 1.4, segs=10, name='Pipe')
        cyl(D, 'Metal', (sx * 2.9, 0, 5.3), (1, 0, 0), 0.16, 1.4, segs=10, name='Pipe')
    # Conservator tank on stilts
    cyl(B, 'Secondary', (0, -1.0, 7.6), (1, 0, 0), 0.75, 4.6, ch=0.25, segs=20, name='Conservator')
    for sx in (-1.6, 1.6):
        beam(D, 'Dark', (sx, -1.0, 6.2), (sx, -1.0, 7.0), 0.18)
    tube(D, 'Metal', [(-2.1, -1.0, 7.6), (-2.5, -0.6, 7.0), (-2.5, 0.2, 6.3)], 0.1, segs=8, name='Breather')
    # Bushings: ribbed porcelain on metal turrets
    for b in range(3):
        c = Vector(((b - 1) * 1.5, 0.8, 6.2))
        lathe(B, 'Metal', c, (0, 0, 1), [(0, 0), (0.4, 0), (0.4, 0.6), (0.3, 0.7), (0, 0.7)], segs=12, name='Turret')
        prof = [(0, 0)] + [((0.42 if q % 2 == 0 else 0.26), 0.7 + q * 0.25) for q in range(13)] + [(0.15, 0.7 + 12 * 0.25 + 0.2), (0, 0.7 + 12 * 0.25 + 0.2)]
        lathe(B, 'Rubber', c, (0, 0, 1), prof, segs=14, name='Bushing')
        lathe(D, 'Metal', c + Vector((0, 0, 4.0)), (0, 0, 1), [(0, 0), (0.2, 0), (0.2, 0.5), (0, 0.6)], segs=10, name='Terminal')
    # Valves, nameplate, earth bars and a hazard sign
    cyl(D, 'Dark', (1.8, -1.95, 1.4), (0, 1, 0), 0.3, 0.3, segs=12, name='Valve')
    box(D, 'Chrome', (-1.2, -1.92, 3.6), (1.2, 0.05, 0.8), bevel=0)
    hazard(D, Mx((1.0, -1.93, 4.6), (0, 0, 180)), 1.4, 0.5, n=3)
    col_box((9.0, 6.4, 6.4), Mx((0, 0, 3.2)))


def pylon():
    """A 44 m steel lattice transmission tower with two cross-arms and insulator strings."""
    for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1)):
        lathe(B, 'Secondary', (sx * 5.2, sy * 5.2, 0), (0, 0, 1), [(0, 0), (1.1, 0), (1.1, 0.8), (0.8, 1.1), (0, 1.1)], segs=8, name='Footing')
        col_box((1.6, 1.6, 6.0), Mx((sx * 4.8, sy * 4.8, 3.0)))
    lattice_mast(B, 'Metal', 10.4, 3.6, 1.0, 22.0, 5, chord=0.34, brace=0.16)
    lattice_mast(B, 'Metal', 3.6, 2.2, 22.0, 40.0, 5, chord=0.28, brace=0.13)
    lattice_mast(B, 'Metal', 2.2, 0.3, 40.0, 44.0, 1, chord=0.22, brace=0.1)
    # Cross-arms: triangular lattice outriggers along X
    for z, L in ((26.0, 9.5), (34.0, 7.5)):
        for sx in (-1, 1):
            root_w = 3.6 - (3.6 - 2.2) * (z - 22) / 18
            r = root_w / 2
            tip = Vector((sx * (r + L), 0, z))
            for sy in (-1, 1):
                beam(B, 'Metal', (sx * r, sy * r, z), tip, 0.24, name='ArmChord')
                beam(B, 'Metal', (sx * r, sy * r, z + 2.2), tip + Vector((0, 0, 0.1)), 0.2, name='ArmTop')
            nb = 4
            for k in range(1, nb):
                f = k / nb
                p0 = Vector((sx * (r + L * f), 0, z))
                w = r * (1 - f)
                beam(B, 'Metal', p0 + Vector((0, -w, 0)), p0 + Vector((0, w, 0)), 0.12)
                beam(B, 'Metal', p0 + Vector((0, -w, 0)), p0 + Vector((0, 0, 2.2 * (1 - f))), 0.1)
                beam(B, 'Metal', p0 + Vector((0, w, 0)), p0 + Vector((0, 0, 2.2 * (1 - f))), 0.1)
            # Insulator string and conductor clamp
            for q in range(12):
                lathe(D, 'Rubber', tip + Vector((0, 0, -0.5 - q * 0.32)), (0, 0, 1), [(0, -0.05), (0.26, -0.02), (0.1, 0.06), (0, 0.06)], segs=10, name='Disc')
            beam(D, 'Metal', tip, tip + Vector((0, 0, -4.4)), 0.06)
            box(D, 'Dark', tip + Vector((0, 0, -4.6)), (0.3, 0.9, 0.3), bevel=0)
    # Earth-wire peak and aircraft warning light
    lathe(D, 'Hot', (0, 0, 44.2), (0, 0, 1), [(0, 0), (0.25, 0), (0, 0.4)], segs=8, name='Beacon')
    # Anti-climb band, danger plates, step bolts up one leg
    for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1)):
        p = Vector((sx * 4.3, sy * 4.3, 4.2))
        box(D, 'Accent', p, (0.9, 0.9, 0.6), bevel=0)
    for q in range(30):
        z = 2 + q * 0.6
        t = (z - 1.0) / 21
        w = (10.4 + (3.6 - 10.4) * t) / 2
        cyl(D, 'Dark', (w + 0.2, w, z), (1, 0, 0), 0.04, 0.4, segs=6, name='Step')


def turbine_hall():
    """
    The hydro station's turbine hall: 64 x 26 m, a steel-framed shed on a concrete plinth
    with clad bays, a clerestory window band, a roof monitor with fans, a roller door, a
    control annexe and three penstocks entering the back wall. An outdoor gantry crane
    stands over the laydown yard at one end.
    """
    L, Wd, Hh = 64.0, 26.0, 21.0
    box(B, 'Primary', (0, 0, 1.6), (L + 2, Wd + 2, 3.2), ch=0.3, seams=True, seam_min=30)
    nb = 8
    for side in (-1, 1):
        y = side * Wd / 2
        for k in range(nb + 1):
            x = -L / 2 + L * k / nb
            box(B, 'Dark', (x, y, 3.2 + (Hh - 3.2) / 2), (0.9, 1.1, Hh - 3.2), bevel=0.04, name='Column')
        for k in range(nb):
            x0 = -L / 2 + L * k / nb
            cx = x0 + L / nb / 2
            # Cladding panel with vertical ribs
            box(B, 'Secondary', (cx, y, 3.2 + 5.5), (L / nb - 0.9, 0.4, 11.0), bevel=0.03, name='Clad')
            for r in range(6):
                box(B, 'Secondary', (x0 + 0.9 + r * 1.2, y + side * 0.25, 3.2 + 5.5), (0.22, 0.2, 10.8), bevel=0, name='Rib')
            # Window band
            box(B, 'Black', (cx, y, 16.6), (L / nb - 0.9, 0.25, 4.0), bevel=0, name='Glass')
            lit = (k * 7 + (side > 0) * 3) % 5 < 2
            if lit:
                box(D, 'Glow', (cx, y + side * 0.13, 16.0), (L / nb - 1.6, 0.05, 1.6), bevel=0, name='Light')
            for m in range(4):
                box(B, 'Dark', (x0 + 0.45 + (L / nb - 0.9) * m / 3, y + side * 0.15, 16.6), (0.18, 0.2, 4.0), bevel=0, name='Mullion')
            box(B, 'Dark', (cx, y + side * 0.2, 14.4), (L / nb, 0.5, 0.4), bevel=0, name='Sill')
            box(B, 'Secondary', (cx, y, 19.8), (L / nb - 0.9, 0.4, 2.4), bevel=0.03, name='Clad')
        # Eaves gutter
        box(B, 'Dark', (0, y + side * 0.6, Hh + 0.1), (L + 1.2, 0.8, 0.6), bevel=0.05, name='Gutter')
    # Gable end walls
    for sx in (-1, 1):
        x = sx * L / 2
        box(B, 'Secondary', (x, 0, 3.2 + (Hh - 3.2) / 2), (0.5, Wd, Hh - 3.2), bevel=0.03, name='Gable')
        armour(B, 'Secondary', [(x + e, y, z) for e in (-0.25, 0.25) for y, z in ((-Wd / 2, Hh), (Wd / 2, Hh), (0, Hh + 3.4))], bevel=0.03, seams=False, groove=None, name='GableTop')
        for r in range(9):
            box(B, 'Secondary', (x + sx * 0.3, -Wd / 2 + 1.5 + r * 2.9, 3.2 + (Hh - 3.2) / 2), (0.2, 0.22, Hh - 3.4), bevel=0, name='Rib')
    # Roller door on the +X end with a hazard frame and lamps
    box(B, 'Dark', (L / 2 + 0.45, 0, 3.2 + 7.0), (0.4, 13.0, 14.0), bevel=0.03, name='Door')
    for z in range(16):
        box(B, 'Metal', (L / 2 + 0.7, 0, 3.6 + z * 0.85), (0.1, 12.6, 0.3), bevel=0, name='Slat')
    for s in (-1, 1):
        hazard(D, Mx((L / 2 + 0.75, s * 6.8, 3.2 + 7.0), (0, 0, -90), (1, 1, 1)) @ Mx((0, 0, 0), (0, 90, 0)), 14.0, 0.6, n=12)
        lamp(D, Mx((L / 2 + 0.6, s * 7.6, 18.4), (0, 0, -90)), 1.0, 0.4)
    box(B, 'Dark', (L / 2 + 1.2, 0, 17.8), (1.6, 14.0, 1.0), bevel=0.05, name='DoorHead')
    # Roof: shallow gable in panels, ridge monitor with louvres and fans
    for side in (-1, 1):
        for k in range(nb):
            x = -L / 2 + L * (k + 0.5) / nb
            pts = [(x + e * L / nb / 2, y, z) for e in (-1, 1) for y, z in ((side * (Wd / 2 + 0.8), Hh + 0.2), (side * 3.2, Hh + 3.0))]
            pts += [(px, py, pz - 0.35) for px, py, pz in pts]
            armour(B, 'Metal', pts, bevel=0.03, seams=False, groove=None, name='Roof')
    box(B, 'Dark', (0, 0, Hh + 3.6), (L - 4, 6.6, 1.6), ch=0.2, name='MonitorBase')
    for side in (-1, 1):
        for k in range(24):
            box(B, 'Black', (-L / 2 + 3 + (L - 6) * (k + 0.5) / 24, side * 3.0, Hh + 5.2), (2.0, 0.12, 0.6), rot=(side * 35, 0, 0), bevel=0, name='Louvre')
    box(B, 'Metal', (0, 0, Hh + 6.4), (L - 3, 8.0, 0.4), ch=0.2, name='MonitorRoof')
    for k in range(4):
        x = -L / 2 + 10 + k * 14.7
        lathe(B, 'Metal', (x, 0, Hh + 6.6), (0, 0, 1), [(0, 0), (1.5, 0), (1.5, 1.0), (1.7, 1.2), (1.7, 1.6), (0, 1.6)], segs=20, name='Fan')
        lathe(D, 'Black', (x, 0, Hh + 8.21), (0, 0, 1), [(0, 0), (1.5, 0), (0, 0.01)], segs=20, name='FanHole')
    # Control annexe on the front (+Y) with a stair tower
    ax, ay = -14.0, Wd / 2 + 5.0
    box(B, 'Primary', (ax, ay, 6.0), (22.0, 10.0, 12.0), ch=0.3, seams=True, seam_min=10)
    box(B, 'Dark', (ax, ay, 12.3), (22.6, 10.6, 0.6), ch=0.2)
    for k in range(6):
        box(B, 'Black', (ax - 9 + k * 3.6, ay + 5.05, 8.6), (2.8, 0.15, 2.4), bevel=0, name='Window')
        if k % 2 == 0:
            box(D, 'Glow', (ax - 9 + k * 3.6, ay + 5.14, 8.2), (2.4, 0.05, 1.0), bevel=0, name='Light')
    box(B, 'Dark', (ax + 5, ay + 5.1, 2.0), (3.0, 0.3, 4.0), bevel=0.04, name='Entrance')
    box(D, 'Dark', (ax + 5, ay + 6.2, 4.3), (4.4, 2.6, 0.3), bevel=0.04, name='Canopy')
    lamp(D, Mx((ax + 5, ay + 6.2, 4.0), (-90, 0, 0)), 1.2, 0.3)
    # External stair up the annexe end
    sx0 = ax - 11.0 - 1.4
    for st in range(20):
        box(D, 'Metal', (sx0, ay - 4.0 + st * 0.45, 0.3 + st * 0.6), (1.6, 0.5, 0.12), bevel=0, name='Step')
    railing(D, [(sx0 - 0.85, ay - 4.0, 0.3), (sx0 - 0.85, ay + 4.55, 11.7)], h=1.0, post=2.0, mat='Accent')
    box(D, 'Dark', (ax, ay, 12.9), (6.0, 4.0, 1.0), bevel=0.05, name='AC')
    for k in range(3):
        lathe(D, 'Metal', (ax - 6 + k * 6, ay - 3.0, 12.6), (0, 0, 1), [(0, 0), (0.15, 0), (0.15, 6.0 + k * 1.5), (0, 6.0 + k * 1.5)], segs=6, name='Antenna')
    # Penstocks entering the back wall, with valve houses
    for k in range(3):
        x = -18 + k * 18
        tube(B, 'Metal', [(x, -Wd / 2 + 0.5, 7.5), (x, -Wd / 2 - 8, 7.5), (x, -Wd / 2 - 16, 5.5), (x, -Wd / 2 - 24, 0.5), (x, -Wd / 2 - 30, -3.0)], 2.2, segs=20, name='Penstock')
        for yy in (-Wd / 2 - 3.0, -Wd / 2 - 9.0):
            lathe(B, 'Dark', (x, yy, 7.5), (0, 1, 0), [(2.2, -0.4), (2.7, -0.3), (2.7, 0.3), (2.2, 0.4)], segs=20, name='Flange')
        box(B, 'Primary', (x, -Wd / 2 - 6.0, 2.6), (5.6, 3.0, 5.2), ch=0.2, name='Saddle')
        box(D, 'Accent', (x, -Wd / 2 - 3.0, 10.4), (1.2, 1.2, 1.6), ch=0.1, name='Actuator')
    # Outdoor gantry crane over the laydown yard at -X
    gx = -L / 2 - 14
    for sy in (-1, 1):
        for sx in (-1, 1):
            beam(B, 'Accent', (gx + sx * 9, sy * 11, 0.5), (gx + sx * 9, sy * 11, 17.0), 0.9, name='CraneLeg')
            box(D, 'Dark', (gx + sx * 9, sy * 11, 0.5), (2.0, 1.6, 1.0), bevel=0.05, name='Bogie')
        ibeam(B, 'Accent', (gx - 10, sy * 11, 17.5), (gx + 10, sy * 11, 17.5), 0.9, 1.6)
    for sx in (-1, 1):
        ibeam(B, 'Accent', (gx + sx * 9, -12, 19.0), (gx + sx * 9, 12, 19.0), 1.0, 1.8)
    box(B, 'Dark', (gx, 3.0, 20.6), (20.0, 2.6, 1.4), ch=0.1, name='Trolley')
    for c in (-0.5, 0.5):
        beam(D, 'Metal', (gx + c, 3.0, 20.0), (gx + c, 3.0, 9.0), 0.08)
    box(D, 'Accent', (gx, 3.0, 8.6), (1.4, 0.6, 0.8), bevel=0.05, name='Hook')
    box(B, 'Black', (gx + 9.6, -9, 15.0), (1.8, 2.0, 2.4), ch=0.2, name='Cab')
    for k in range(5):
        box(B, 'Dark', (gx - 6 + k * 3.2, -2, 1.3), (2.4, 6.0, 2.6), ch=0.2, name='Crate')
    # Physics hull
    col_box((L + 2, Wd + 2, Hh + 2.2), Mx((0, 0, (Hh + 2.2) / 2)))
    col_box((22.0, 10.0, 13.0), Mx((ax, ay, 6.5)))
    for k in range(3):
        col_box((5.0, 26.0, 9.0), Mx((-18 + k * 18, -Wd / 2 - 13, 4.5)))
    for sy in (-1, 1):
        for sx in (-1, 1):
            col_box((1.2, 1.2, 18.0), Mx((gx + sx * 9, sy * 11, 9.0)))


# ------------------------------------------------------------------ launch site

def floodlight_mast(x, y, h, face):
    """A slim lattice mast with a bank of floodlights aimed along `face` (degrees about Z)."""
    lattice_mast(B, 'Metal', 1.6, 1.0, 0.0, h, max(3, int(h / 6)), chord=0.18, brace=0.08, cx=x, cy=y)
    M = Mx((x, y, h + 0.6), (0, 0, face))
    armour(B, 'Dark', boxpts((4.0, 1.2, 0.25)), M, bevel=0.03, seams=False, groove=None, name='LampDeck')
    for r in range(2):
        for c in range(4):
            lamp(D, M @ Mx((-1.5 + c, 0.4, 0.6 + r * 0.8), (-15, 0, 0)), 0.7, 0.55)
    lathe(D, 'Hot', (x, y, h + 2.4), (0, 0, 1), [(0, 0), (0.15, 0), (0, 0.3)], segs=8, name='Beacon')


def rocket(cx=0.0, cy=0.0, z0=10.0):
    """A two-stage launcher with two strap-on boosters, standing on its engines at z0."""
    R = 3.2
    c = Vector((cx, cy, z0))

    def band(z, h, mat, r=R + 0.03):
        lathe(B, mat, c + Vector((0, 0, z)), (0, 0, 1), [(r, 0), (r, h)], segs=40, name='Band')

    lathe(B, 'Primary', c, (0, 0, 1), [(0, 0), (R - 0.2, 0), (R, 0.3), (R, 47.5), (0, 47.5)], segs=40, name='Core')
    for z, h, m in ((2.0, 3.0, 'Secondary'), (14.0, 0.6, 'Black'), (24.0, 4.5, 'Secondary'), (38.0, 0.6, 'Black'), (44.0, 3.5, 'Secondary')):
        band(z, h, m)
    # Interstage (dark, slightly flared) and upper stage
    lathe(B, 'Dark', c + Vector((0, 0, 47.5)), (0, 0, 1), [(R, 0), (R + 0.05, 3.0), (0, 3.0)], segs=40, name='Interstage')
    lathe(B, 'Primary', c + Vector((0, 0, 50.5)), (0, 0, 1), [(0, 0), (R, 0), (R, 14.5), (0, 14.5)], segs=40, name='Upper')
    band(56.0, 0.5, 'Accent')
    band(60.0, 2.0, 'Secondary')
    # Payload fairing: an ogive
    prof = [(R + 0.08, 0.0), (R + 0.08, 4.0)]
    for k in range(1, 13):
        t = k / 12
        prof.append(((R + 0.08) * math.cos(t * math.pi / 2) ** 0.75, 4.0 + 12.0 * math.sin(t * math.pi / 2)))
    prof.append((0, 16.0))
    lathe(B, 'Primary', c + Vector((0, 0, 65.0)), (0, 0, 1), prof, segs=40, name='Fairing')
    lathe(B, 'Black', c + Vector((0, 0, 69.0)), (0, 0, 1), [(R + 0.1, 0), (R + 0.1, 0.25)], segs=40, name='FairingSeam')
    # Raceway and cable tunnel up the core
    beam(B, 'Secondary', c + Vector((0, -R - 0.15, 1.0)), c + Vector((0, -R - 0.15, 64.0)), 0.5, 0.35, name='Raceway')
    # Core engines: four bells and a heat shield
    lathe(B, 'Dark', c, (0, 0, 1), [(0, 0.4), (R, 0.4), (R, 0)], segs=40, name='HeatShield')
    for k in range(4):
        t = math.radians(45 + 90 * k)
        p = c + Vector((math.cos(t) * 1.6, math.sin(t) * 1.6, 0))
        lathe(B, 'Metal', p, (0, 0, 1), [(1.15, -2.6), (0.9, -1.8), (0.55, -0.6), (0.6, 0), (0, 0)], segs=20, name='Bell')
        lathe(D, 'Black', p + Vector((0, 0, -2.58)), (0, 0, 1), [(0, 0), (1.1, 0), (0, 0.01)], segs=20, name='BellIn')
    # Strap-on boosters
    for sx in (-1, 1):
        b = c + Vector((sx * (R + 2.0), 0, 0))
        r = 1.8
        lathe(B, 'Primary', b, (0, 0, 1), [(0, 0), (r, 0), (r, 32.0), (r * 0.98, 33.0)] + [(r * math.cos(t * math.pi / 2) ** 0.8, 33.0 + 6.0 * math.sin(t * math.pi / 2)) for t in [k / 8 for k in range(1, 9)]], segs=28, name='Booster')
        for z, h, m in ((1.0, 2.0, 'Secondary'), (16.0, 0.5, 'Black'), (28.0, 2.5, 'Accent')):
            lathe(B, m, b + Vector((0, 0, z)), (0, 0, 1), [(r + 0.03, 0), (r + 0.03, h)], segs=28, name='BBand')
        lathe(B, 'Metal', b, (0, 0, 1), [(1.3, -2.8), (0.9, -1.6), (0.7, 0), (0, 0)], segs=20, name='BBell')
        lathe(D, 'Black', b + Vector((0, 0, -2.78)), (0, 0, 1), [(0, 0), (1.25, 0), (0, 0.01)], segs=20, name='BBellIn')
        # Attach struts
        for z in (3.0, 29.0):
            beam(B, 'Dark', c + Vector((sx * (R - 0.2), 0, z)), b + Vector((-sx * (r - 0.2), 0, z)), 0.4, name='Strut')
            beam(B, 'Dark', c + Vector((sx * (R - 0.2), 0, z + 2.0)), b + Vector((-sx * (r - 0.2), 0, z)), 0.25, name='Strut')
        # Fins at the booster base
        for f in (90, -90):
            fy = 1 if f > 0 else -1
            armour(B, 'Secondary', [b + Vector((0, fy * r * 0.9, 0.5)), b + Vector((0, fy * (r + 2.2), 0.0)), b + Vector((0, fy * (r + 2.2), 2.0)), b + Vector((0, fy * r * 0.9, 5.5))] +
                   [p + Vector((e, 0, 0)) for e in (-0.12, 0.12) for p in (b + Vector((0, fy * r * 0.9, 0.5)), b + Vector((0, fy * (r + 2.2), 0.0)), b + Vector((0, fy * (r + 2.2), 2.0)), b + Vector((0, fy * r * 0.9, 5.5)))],
                   bevel=0.03, seams=False, groove=None, name='Fin')


def launch_pad():
    """
    The launch complex: a raised concrete pad split by a flame trench with a steel
    deflector, a launch table with hold-down arms, the rocket with its boosters, an 92 m
    service gantry with swing arms and a hammerhead crane, an access ramp and four
    floodlight masts.
    """
    P, Ht, T = 46.0, 7.0, 6.0      # pad size, deck height, half trench width
    for sy in (-1, 1):
        yc = sy * (T + (P / 2 - T) / 2)
        armour(B, 'Primary', [(x * (1 if z == 0 else 0.94), y, z) for x in (-P / 2, P / 2) for y in (sy * T, sy * (P / 2 + (2.0 if 0 else 0))) for z in (0,)] +
               [(x * 0.94, y, Ht) for x in (-P / 2, P / 2) for y in (sy * T, sy * (P / 2 - 1.6))], bevel=0.25, seams=True, seam_min=40, seam_w=0.4, name='PadHalf')
        # Deck edge hazard band and kerb
        hazard(D, Mx((0, sy * (P / 2 - 1.62), Ht - 0.4), (0, 0, 0 if sy < 0 else 180), (1, 1, 1)) @ Mx((0, 0, 0), (-12 * 0, 0, 0)), P * 0.9, 0.8, n=24)
        for x in (-P / 2 * 0.94 + 0.5, P / 2 * 0.94 - 0.5):
            pass
        railing(D, [(-P / 2 * 0.94 + 0.6, sy * (T + 0.4), Ht), (P / 2 * 0.94 - 0.6, sy * (T + 0.4), Ht)], h=1.1, post=2.4)
        box(B, 'Dark', (0, sy * (T + 0.15), Ht - 0.3), (P * 0.94, 0.3, 0.6), bevel=0, name='TrenchLip')
    # Trench floor and the wedge deflector under the rocket
    box(B, 'Dark', (0, 0, 0.2), (P + 6, 2 * T, 0.4), bevel=0.05, name='TrenchFloor')
    armour(B, 'Metal', [(x, y, z) for y in (-T + 0.4, T - 0.4) for x, z in ((-9, 0.4), (9, 0.4), (0, 5.2))], bevel=0.1, seams=False, groove=None, name='Deflector')
    for x in (-6, -3, 0, 3, 6):
        box(D, 'Black', (x, 0, 0.45), (0.3, 2 * T - 1.0, 0.1), bevel=0, name='Scorch')
    # Launch table bridging the trench
    for sx in (-1, 1):
        ibeam(B, 'Dark', (sx * 5.5, -T - 1.0, Ht + 0.9), (sx * 5.5, T + 1.0, Ht + 0.9), 1.4, 1.8)
    for sy in (-1, 1):
        ibeam(B, 'Dark', (-7.5, sy * 4.0, Ht + 2.4), (7.5, sy * 4.0, Ht + 2.4), 1.2, 1.2)
    for k in range(4):
        t = math.radians(45 + 90 * k)
        d = Vector((math.cos(t), math.sin(t), 0))
        a = d * 6.2 + Vector((0, 0, Ht + 2.6))
        b = d * 3.4 + Vector((0, 0, Ht + 3.4))
        armour(B, 'Accent', [a + Vector((0, 0, -1)), a + Vector((0, 0, 1)), b] + [p + Vector((d.y, -d.x, 0)) * 0.5 for p in (a + Vector((0, 0, -1)), a + Vector((0, 0, 1)), b)] +
               [p - Vector((d.y, -d.x, 0)) * 0.5 for p in (a + Vector((0, 0, -1)), a + Vector((0, 0, 1)), b)], bevel=0.05, seams=False, groove=None, name='HoldDown')
    rocket(0, 0, Ht + 3.0 + 2.6)
    # Service gantry behind the rocket (-Y side)
    gy = -14.5
    lattice_mast(B, 'Accent', 11.0, 9.0, Ht, Ht + 85.0, 14, chord=0.6, brace=0.28, cy=gy)
    box(B, 'Dark', (0, gy, Ht + 42.5), (3.2, 3.2, 85.0), ch=0.2, name='Lift')
    for k in range(1, 15):
        z = Ht + k * 85.0 / 14
        w = 11.0 + (9.0 - 11.0) * k / 14
        box(B, 'Dark', (0, gy, z + 0.15), (w + 1.2, w + 1.2, 0.3), bevel=0.03, name='Level')
        if k % 3 == 0:
            railing(D, [(-w / 2 - 0.5, gy - w / 2 - 0.5, z + 0.3), (w / 2 + 0.5, gy - w / 2 - 0.5, z + 0.3), (w / 2 + 0.5, gy + w / 2 + 0.5, z + 0.3)], h=1.1, post=2.5)
    # Swing arms to the rocket: truss arms with umbilical hoses
    for z, reach in ((Ht + 5.6 + 18.0, 3.2), (Ht + 5.6 + 44.0, 3.2), (Ht + 5.6 + 58.0, 3.2), (Ht + 5.6 + 70.0, 3.4)):
        y0, y1 = gy + 5.0, -reach - 0.4
        for sx in (-1, 1):
            beam(B, 'Accent', (sx * 1.2, y0, z), (sx * 1.0, y1, z), 0.3, name='ArmChord')
            beam(B, 'Accent', (sx * 1.2, y0, z + 2.6), (sx * 1.0, y1 - 0.4, z + 2.2), 0.3, name='ArmChord')
            for q in range(5):
                f = q / 4
                yy = y0 + (y1 - y0) * f
                beam(B, 'Accent', (sx * 1.15, yy, z), (sx * 1.15, yy, z + 2.6 - 0.4 * f), 0.18)
                if q < 4:
                    yy2 = y0 + (y1 - y0) * (q + 1) / 4
                    beam(B, 'Accent', (sx * 1.15, yy, z), (sx * 1.15, yy2, z + 2.6 - 0.4 * (q + 1) / 4), 0.14)
        box(B, 'Dark', (0, (y0 + y1) / 2, z + 0.1), (2.2, abs(y1 - y0), 0.25), bevel=0, name='ArmDeck')
        box(B, 'Dark', (0, y1 + 0.3, z + 1.2), (2.8, 0.8, 2.4), ch=0.2, name='ArmHead')
        tube(D, 'Black', [(0.6, y0, z - 0.3), (0.7, (y0 + y1) / 2, z - 1.6), (0.6, y1 + 0.4, z + 0.4)], 0.22, segs=8, name='Umbilical')
        tube(D, 'Black', [(-0.6, y0, z - 0.3), (-0.7, (y0 + y1) / 2, z - 1.2), (-0.6, y1 + 0.4, z + 0.8)], 0.16, segs=8, name='Umbilical')
    # Hammerhead crane and lightning mast on top
    top = Ht + 85.0
    ibeam(B, 'Accent', (-12.0, gy, top + 2.0), (8.0, gy, top + 2.0), 1.2, 2.0)
    box(B, 'Dark', (-10.0, gy, top + 4.2), (3.0, 3.0, 2.4), ch=0.2, name='Counterweight')
    box(B, 'Black', (2.0, gy - 1.5, top + 1.2), (2.0, 1.4, 1.8), ch=0.2, name='CraneCab')
    beam(D, 'Metal', (7.0, gy, top + 1.0), (7.0, gy, top - 12.0), 0.08)
    lathe(B, 'Metal', (0, gy, top + 0.3), (0, 0, 1), [(0, 0), (0.5, 0), (0.25, 14.0), (0, 14.0)], segs=10, name='LightningMast')
    lathe(D, 'Hot', (0, gy, top + 14.2), (0, 0, 1), [(0, 0), (0.35, 0), (0, 0.5)], segs=10, name='Beacon')
    for z in (30.0, 60.0):
        for sx in (-1, 1):
            lathe(D, 'Hot', (sx * 5.0, gy - 5.0, Ht + z), (0, 0, 1), [(0, 0), (0.25, 0), (0, 0.35)], segs=8, name='Beacon')
    # Access ramp up to the deck on the +Y side
    ry0, ry1 = P / 2 - 1.6, P / 2 + 24.0
    armour(B, 'Primary', [(x, y, z) for x in (-7.0, 7.0) for y, z in ((ry0, 0), (ry0, Ht), (ry1, 0))], bevel=0.15, seams=False, groove=None, name='Ramp')
    for sx in (-1, 1):
        railing(D, [(sx * 6.7, ry1 - 1.0, 0.3), (sx * 6.7, ry0 + 0.5, Ht)], h=1.1, post=2.4)
    for q in range(10):
        f = (q + 0.5) / 10
        box(D, 'Black', (0, ry0 + (ry1 - ry0) * f, Ht * (1 - f) + 0.05), (12.0, 0.25, 0.12), rot=(math.degrees(math.atan2(Ht, ry1 - ry0)), 0, 0), bevel=0, name='Grip')
    # Floodlight masts at the corners, aimed at the rocket
    for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
        floodlight_mast(sx * 34.0, sy * 34.0, 30.0, math.degrees(math.atan2(-sy, -sx)) - 90 + 180)
    # Physics hull
    for sy in (-1, 1):
        col_box((P, P / 2 - T, Ht), Mx((0, sy * (T + (P / 2 - T) / 2), Ht / 2)))
    col_box((15.0, 2 * T + 2.0, 3.4), Mx((0, 0, Ht + 1.7)))
    col_cyl((0, 0, Ht + 5.6 + 40.0), (0, 0, 1), 3.4, 80.0, segs=10)
    for sx in (-1, 1):
        col_cyl((sx * 5.2, 0, Ht + 5.6 + 19.0), (0, 0, 1), 1.9, 40.0, segs=8)
    col_box((11.0, 11.0, 86.0), Mx((0, gy, Ht + 43.0)))
    armour(C, 'Dark', [(x, y, z) for x in (-7.0, 7.0) for y, z in ((ry0, 0), (ry0, Ht), (ry1, 0))], bevel=0, seams=False, groove=None, name='Col')
    for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
        col_box((1.8, 1.8, 30.0), Mx((sx * 34.0, sy * 34.0, 15.0)))


def fuel_sphere():
    """A spherical propellant tank on eight columns, with bracing, an equator walkway, a stair and valves."""
    Rs, zc = 7.5, 11.0
    prof = [(0, -Rs)] + [(Rs * math.sin(math.radians(a)), -Rs * math.cos(math.radians(a))) for a in range(6, 175, 6)] + [(0, Rs)]
    lathe(B, 'Primary', (0, 0, zc), (0, 0, 1), prof, segs=48, name='Sphere')
    for z0, h, m in ((-0.6, 1.2, 'Secondary'), (3.2, 0.25, 'Black'), (-3.45, 0.25, 'Black')):
        r0 = math.sqrt(max(0.0, Rs * Rs - (z0) ** 2)) + 0.04
        r1 = math.sqrt(max(0.0, Rs * Rs - (z0 + h) ** 2)) + 0.04
        lathe(B, m, (0, 0, zc + z0), (0, 0, 1), [(r0, 0), (r1, h)], segs=48, name='Band')
    for k in range(8):
        t = 2 * math.pi * k / 8
        d = Vector((math.cos(t), math.sin(t), 0))
        p = d * (Rs + 0.25)
        lathe(B, 'Secondary', (p.x, p.y, 0), (0, 0, 1), [(0, 0), (0.95, 0), (0.95, 0.9), (0.6, 1.1), (0, 1.1)], segs=10, name='Footing')
        beam(B, 'Dark', p + Vector((0, 0, 1.0)), p + Vector((0, 0, zc + 0.4)), 0.55, name='Column')
        box(B, 'Dark', p + Vector((0, 0, zc)) - d * 0.3, (0.9, 0.9, 1.8), rot=(0, 0, math.degrees(t)), bevel=0.04, name='Gusset')
        # Tie-rod X bracing between columns
        q = Vector((math.cos(t + math.pi / 4), math.sin(t + math.pi / 4), 0)) * (Rs + 0.25)
        beam(B, 'Metal', p + Vector((0, 0, 1.6)), q + Vector((0, 0, zc - 2.5)), 0.12, name='Tie')
        beam(B, 'Metal', q + Vector((0, 0, 1.6)), p + Vector((0, 0, zc - 2.5)), 0.12, name='Tie')
    # Equator walkway with railing
    lathe(B, 'Dark', (0, 0, zc - 0.8), (0, 0, 1), [(Rs + 0.1, 0), (Rs + 1.4, 0), (Rs + 1.4, 0.2), (Rs + 0.1, 0.2)], segs=48, name='Walkway')
    railing(D, [((Rs + 1.3) * math.cos(2 * math.pi * k / 32), (Rs + 1.3) * math.sin(2 * math.pi * k / 32), zc - 0.6) for k in range(33)], h=1.1, post=3.0)
    # Stair flights up to the walkway, zig-zagging on the +X side
    for f in range(3):
        y0, y1 = (-4.0, 4.0) if f % 2 == 0 else (4.0, -4.0)
        z0 = f * (zc - 0.8) / 3
        z1 = (f + 1) * (zc - 0.8) / 3
        for st in range(12):
            g = (st + 0.5) / 12
            box(D, 'Metal', (Rs + 3.2, y0 + (y1 - y0) * g, z0 + (z1 - z0) * g), (1.4, 0.6, 0.1), bevel=0, name='Step')
        beam(D, 'Dark', (Rs + 2.45, y0, z0), (Rs + 2.45, y1, z1), 0.18)
        beam(D, 'Dark', (Rs + 3.95, y0, z0), (Rs + 3.95, y1, z1), 0.18)
        railing(D, [(Rs + 4.0, y0, z0), (Rs + 4.0, y1, z1)], h=1.0, post=2.0)
        box(D, 'Dark', (Rs + 3.2, y1 + (0.9 if y1 > 0 else -0.9), z1), (1.6, 1.8, 0.2), bevel=0, name='Landing')
        beam(D, 'Dark', (Rs + 3.2, y1 + (0.9 if y1 > 0 else -0.9), 0), (Rs + 3.2, y1 + (0.9 if y1 > 0 else -0.9), z1), 0.2)
    # Top platform, relief valves and a ladder over the top
    lathe(B, 'Dark', (0, 0, zc + Rs - 0.3), (0, 0, 1), [(0, 0), (2.2, 0), (2.2, 0.3), (0, 0.3)], segs=20, name='TopDeck')
    railing(D, [(2.1 * math.cos(2 * math.pi * k / 12), 2.1 * math.sin(2 * math.pi * k / 12), zc + Rs) for k in range(13)], h=1.0, post=1.2)
    for k in range(3):
        t = 2 * math.pi * k / 3
        lathe(D, 'Accent', (math.cos(t) * 1.0, math.sin(t) * 1.0, zc + Rs), (0, 0, 1), [(0, 0), (0.22, 0), (0.22, 0.6), (0.32, 0.7), (0.32, 0.9), (0, 0.9)], segs=10, name='Valve')
    for q in range(16):
        a0 = math.radians(5 + q * 5.2)
        a1 = math.radians(5 + (q + 1) * 5.2)
        for sy in (-0.35, 0.35):
            beam(D, 'Metal', (math.sin(a0) * (Rs + 0.3), sy, zc + math.cos(a0) * (Rs + 0.3)), (math.sin(a1) * (Rs + 0.3), sy, zc + math.cos(a1) * (Rs + 0.3)), 0.07)
    # Fill pipe and manifold at the bottom
    tube(B, 'Metal', [(0, 0, zc - Rs), (0, 0, zc - Rs - 1.2), (0, 3.0, 1.2), (0, 10.0, 0.9)], 0.35, segs=12, name='FillPipe')
    box(B, 'Secondary', (0, 10.5, 0.9), (2.6, 1.4, 1.8), ch=0.2, name='Manifold')
    for k in range(3):
        cyl(D, 'Accent', (-0.8 + k * 0.8, 10.5, 2.0), (0, 0, 1), 0.2, 0.4, segs=10, name='Wheel')
    hazard(D, Mx((0, 11.22, 0.9)), 2.2, 0.5, n=4)
    col_cyl((0, 0, zc), (0, 0, 1), Rs, Rs * 1.6, segs=10)
    for k in range(8):
        t = 2 * math.pi * k / 8
        p = Vector((math.cos(t), math.sin(t), 0)) * (Rs + 0.25)
        col_box((1.0, 1.0, zc), Mx((p.x, p.y, zc / 2)))


def radar_dish():
    """A tracking radar: equipment building, pedestal, yoke and a 18 m dish with a feed tripod and back truss."""
    box(B, 'Primary', (0, 0, 2.5), (12.0, 12.0, 5.0), ch=0.3, seams=True, seam_min=10)
    box(B, 'Dark', (0, 0, 5.15), (12.6, 12.6, 0.3), ch=0.3)
    for k in range(3):
        grille(D, Mx((-3.5 + k * 3.5, -6.02, 2.2), (0, 0, 180)), 2.4, 1.6, slats=4, depth=0.1)
    box(B, 'Dark', (4.5, 6.05, 1.6), (2.0, 0.3, 3.2), bevel=0.03, name='Door')
    lamp(D, Mx((4.5, 6.2, 3.7)), 0.8, 0.25)
    railing(D, [(-6.1, -6.1, 5.3), (6.1, -6.1, 5.3), (6.1, 6.1, 5.3), (-6.1, 6.1, 5.3), (-6.1, -6.1, 5.3)], h=1.0, post=2.0)
    ladder(D, (-6.4, 0, 0), 5.3, (-1, 0, 0), cage=False)
    # Pedestal and azimuth turret
    lathe(B, 'Dark', (0, 0, 5.3), (0, 0, 1), [(0, 0), (2.6, 0), (2.6, 0.6), (1.9, 0.9), (1.7, 3.6), (2.2, 3.8), (2.2, 4.4), (0, 4.4)], segs=32, name='Pedestal')
    box(B, 'Secondary', (0, 0, 10.6), (5.0, 3.4, 2.4), ch=0.4, seams=True, seam_min=2, name='Turret')
    # Yoke arms and elevation axle
    for sx in (-1, 1):
        armour(B, 'Secondary', [(sx * x, y, z) for x in (2.3, 2.9) for y, z in ((-1.4, 9.6), (1.4, 9.6), (0.9, 14.2), (-0.9, 14.2))], bevel=0.06, seams=False, groove=None, name='Yoke')
    cyl(B, 'Metal', (0, 0, 13.6), (1, 0, 0), 0.5, 6.6, segs=16, name='Axle')
    for sx in (-1, 1):
        drum(B, (sx * 3.15, 0, 13.6), (1, 0, 0), 0.9, 0.5, body='Dark', face='Metal', hub='Chrome', bolts=6, segs=20)
    # Dish: parabolic shell tilted up 35 degrees, facing +Y
    f, Rd = 8.1, 9.0
    tilt = 35.0
    centre = Vector((0, 1.2, 13.6))
    Md = Matrix.Translation(centre) @ Matrix.Rotation(math.radians(-(90 - tilt)), 4, 'X')   # local +Z -> dish axis (forward and up)
    axis = (Md.to_3x3() @ Vector((0, 0, 1))).normalized()
    front = [(r, r * r / (4 * f)) for r in [Rd * k / 12 for k in range(13)]]
    shell = [(0, 0.35)] + [(r, z + 0.35 + 0.05) for r, z in front[1:]] + [(Rd + 0.15, front[-1][1] + 0.15), (Rd + 0.15, front[-1][1] - 0.1)] + [(r, z) for r, z in reversed(front[1:])] + [(0, 0)]
    # lathe() takes (radius, height) and builds faces between rings; the closed profile gives a shell with thickness
    prof_front = [(0, 0.36)] + [(r, z + 0.36) for r, z in front[1:]] + [(Rd + 0.1, front[-1][1] + 0.36)]
    prof_back = [(0, 0)] + [(r, z) for r, z in front[1:]] + [(Rd + 0.1, front[-1][1])]
    lathe(B, 'Primary', Md @ Vector((0, 0, 0)), axis, [(p[0], p[1]) for p in reversed(prof_front)], segs=48, name='DishFace')
    lathe(B, 'Dark', Md @ Vector((0, 0, 0)), axis, prof_back, segs=48, name='DishBack')
    lathe(B, 'Dark', Md @ Vector((0, 0, front[-1][1])), axis, [(Rd + 0.1, 0.0), (Rd + 0.25, 0.1), (Rd + 0.25, 0.45), (Rd + 0.1, 0.5)], segs=48, name='Rim')
    # Back truss: radial ribs to a hub
    hub = Md @ Vector((0, 0, -1.6))
    lathe(B, 'Dark', hub, axis, [(0, 0), (1.3, 0), (1.3, 1.8), (0, 1.8)], segs=16, name='Hub')
    for k in range(12):
        t = 2 * math.pi * k / 12
        for rr in (Rd * 0.55, Rd * 0.95):
            p = Md @ Vector((math.cos(t) * rr, math.sin(t) * rr, rr * rr / (4 * f) - 0.25))
            beam(B, 'Metal', hub + axis * 0.9, p, 0.16, name='BackRib')
    # Feed tripod and horn at the focus
    focus = Md @ Vector((0, 0, f * 0.8))
    for k in range(3):
        t = 2 * math.pi * k / 3 + math.pi / 2
        p = Md @ Vector((math.cos(t) * Rd * 0.92, math.sin(t) * Rd * 0.92, (Rd * 0.92) ** 2 / (4 * f) + 0.4))
        beam(B, 'Metal', p, focus, 0.18, name='FeedStrut')
    lathe(B, 'Secondary', focus, axis, [(0, 0.3), (0.6, 0.3), (0.9, -0.8), (0.5, -1.4), (0, -1.4)], segs=16, name='FeedHorn')
    lathe(D, 'Glow', focus + axis * 0.31, axis, [(0, 0), (0.5, 0), (0, 0.01)], segs=16, name='FeedLens')
    lathe(D, 'Hot', (0, 0, 12.0), (0, 0, 1), [(0, 0), (0.2, 0), (0, 0.3)], segs=8, name='Beacon')
    # Cable trays down to the building
    tube(D, 'Black', [(1.0, -1.6, 10.0), (1.4, -2.4, 8.4), (1.4, -2.4, 5.4)], 0.18, segs=8, name='Cable')
    col_box((12.0, 12.0, 5.4), Mx((0, 0, 2.7)))
    col_cyl((0, 0, 9.0), (0, 0, 1), 2.4, 7.0, segs=8)
    col_cyl(tuple(Md @ Vector((0, 0, 1.2))), tuple(axis), Rd, 2.6, segs=12)


def blockhouse():
    """A reinforced firing-room bunker: battered concrete walls, a thick roof slab, vision slits, blast door and periscopes."""
    Lb, Wb, Hb = 24.0, 16.0, 6.5
    armour(B, 'Primary', boxpts((Lb, Wb, Hb), ch=1.8, taper=(0.82, 0.76)), Mx((0, 0, Hb / 2)), bevel=0.25, seams=True, seam_min=12, seam_w=0.45, name='Bunker')
    armour(B, 'Secondary', boxpts((Lb * 0.86, Wb * 0.82, 1.4), ch=1.6, taper=(0.96, 0.94)), Mx((0, 0, Hb + 0.7)), bevel=0.2, seams=False, groove=None, name='Roof')
    # Vision slits on the front (+Y) with lit glass
    for k in range(5):
        x = -8 + k * 4
        y = Wb * 0.76 / 2 + (Wb - Wb * 0.76) / 2 * (1 - 4.4 / Hb) + 0.02
        box(B, 'Black', (x, y + 0.2, 4.4), (2.4, 1.4, 0.6), rot=(-18, 0, 0), bevel=0.02, name='Slit')
        box(D, 'Glow', (x, y + 0.55, 4.35), (2.0, 0.05, 0.35), rot=(-18, 0, 0), bevel=0, name='SlitGlow')
        box(B, 'Dark', (x, y + 0.85, 4.85), (3.0, 1.2, 0.2), rot=(-18, 0, 0), bevel=0.02, name='Visor')
    # Blast door at the back (-Y) in a recessed concrete portal
    by = -Wb / 2 - 1.6
    box(B, 'Primary', (-4.2, by + 0.2, 2.6), (1.6, 4.0, 5.2), ch=0.2, name='Portal')
    box(B, 'Primary', (4.2, by + 0.2, 2.6), (1.6, 4.0, 5.2), ch=0.2, name='Portal')
    box(B, 'Primary', (0, by + 0.2, 5.6), (10.0, 4.0, 1.0), ch=0.2, name='Lintel')
    box(B, 'Dark', (0, by + 1.4, 2.5), (6.6, 0.6, 5.0), bevel=0.05, name='BlastDoor')
    for z in (1.0, 2.5, 4.0):
        box(D, 'Metal', (0, by + 1.05, z), (6.0, 0.2, 0.3), bevel=0, name='DoorBand')
    hazard(D, Mx((0, by + 1.08, 0.4), (0, 0, 180)), 6.4, 0.6, n=8)
    lamp(D, Mx((0, by - 1.9, 6.2), (0, 0, 180)), 1.2, 0.3)
    # Periscopes, vents and an antenna mast on the roof
    for x in (-6, 6):
        box(B, 'Dark', (x, 3.0, Hb + 1.9), (1.0, 1.0, 1.4), ch=0.15, name='Periscope')
        box(B, 'Black', (x, 3.5, Hb + 2.3), (0.8, 0.1, 0.35), bevel=0, name='PeriGlass')
    for x in (-2, 2):
        lathe(B, 'Metal', (x, -3.0, Hb + 1.4), (0, 0, 1), [(0, 0), (0.6, 0), (0.6, 1.2), (0.8, 1.4), (0.8, 1.7), (0, 1.7)], segs=14, name='Vent')
    lattice_mast(B, 'Metal', 1.0, 0.5, Hb + 1.4, Hb + 16.0, 6, chord=0.12, brace=0.06, cx=9.0, cy=-4.0)
    lathe(D, 'Hot', (9.0, -4.0, Hb + 16.2), (0, 0, 1), [(0, 0), (0.18, 0), (0, 0.3)], segs=8, name='Beacon')
    for k in range(3):
        beam(D, 'Metal', (9.0, -4.0, Hb + 13.0 + k), (9.0 + (k - 1) * 1.2, -4.0 + 1.4, Hb + 13.2 + k), 0.06)
    # Sandbag walls flanking the front
    for sx in (-1, 1):
        for q in range(7):
            for row in range(3):
                lathe(D, 'Secondary', (sx * (Lb / 2 + 1.0 + (q % 2) * 0.1), Wb / 2 - 1 - q * 1.1 - (row % 2) * 0.5, 0.35 + row * 0.55), (0, 1, 0), [(0, -0.5), (0.3, -0.45), (0.38, 0), (0.3, 0.45), (0, 0.5)], segs=8, name='Sandbag')
    col_box((Lb, Wb, Hb + 1.4), Mx((0, 0, (Hb + 1.4) / 2)))
    col_box((10.0, 4.0, 6.2), Mx((0, by + 0.2, 3.1)))


# ------------------------------------------------------------------ airbase

def hangar():
    """
    An arched aircraft shelter, 50 m wide and 60 m deep: ribbed barrel-vault roof,
    lean-to workshops down both sides, a back wall and front gable with the big door
    leaves rolled open, door lights and an apron slab.
    """
    W, Lh, Hh = 50.0, 60.0, 20.0
    n = 16
    y0, y1 = -Lh / 2, Lh / 2
    def arch(t, grow=0.0):
        return ((W / 2 + grow) * math.cos(t), (Hh + grow) * math.sin(t))
    for k in range(n):
        ta = math.pi * k / n
        tb = math.pi * (k + 1) / n
        pts = []
        for t in (ta, tb):
            for g in (0.0, 0.5):
                x, z = arch(t, g)
                pts += [(x, y0, z), (x, y1, z)]
        armour(B, 'Primary', pts, bevel=0.04, seams=False, groove=None, name='Vault')
        armour(C, 'Dark', pts, bevel=0.0, seams=False, groove=None, name='Col')
        # Ribs every 7.5 m
        for r in range(9):
            y = y0 + 0.4 + (Lh - 0.8) * r / 8
            pr = []
            for t in (ta, tb):
                for g in (0.5, 0.85):
                    x, z = arch(t, g)
                    pr += [(x, y - 0.3, z), (x, y + 0.3, z)]
            armour(B, 'Dark', pr, bevel=0.0, seams=False, groove=None, name='Rib')
        # Skylight strip along the crown
        if k in (7, 8):
            x0, z0 = arch(ta, 0.52); x1, z1 = arch(tb, 0.52)
            for r in range(8):
                y = y0 + 4 + (Lh - 8) * (r + 0.5) / 8
                armour(D, 'Black', [(x, y + e, z + 0.02) for x, z in ((x0, z0), (x1, z1)) for e in (-2.2, 2.2)] + [(x, y + e, z + 0.12) for x, z in ((x0, z0), (x1, z1)) for e in (-2.2, 2.2)], bevel=0, seams=False, groove=None, name='Skylight')
        # Roof vents along the flanks
        if k in (3, 12):
            for r in range(4):
                y = y0 + 8 + (Lh - 16) * r / 3
                x, z = arch((ta + tb) / 2, 0.5)
                lathe(D, 'Metal', (x, y, z), (math.cos((ta + tb) / 2) * W / 2 / Hh, 0, math.sin((ta + tb) / 2)), [(0, 0), (0.7, 0), (0.7, 0.8), (0.9, 1.0), (0, 1.1)], segs=12, name='Vent')
    # Back wall: a half-ellipse slab with buttress pilasters
    back = [(x, z) for x, z in (arch(math.pi * k / 24) for k in range(25))]
    slab(B, 'Secondary', back, 0.6, Mx((0, y0 + 0.3, 0)), bevel=0.05, seams=False, groove=None)
    slab(C, 'Dark', back, 0.6, Mx((0, y0 + 0.3, 0)), bevel=0, seams=False, groove=None)
    for x in (-15, -5, 5, 15):
        hz = Hh * math.sqrt(max(0.0, 1 - (x / (W / 2)) ** 2))
        box(B, 'Dark', (x, y0 - 0.2, (hz - 0.6) / 2), (1.2, 0.9, hz - 0.6), bevel=0.04, name='Pilaster')
    # Front gable: side jambs and the lintel above the door opening
    Do, Dh = 38.0, 14.0
    for sx in (-1, 1):
        jamb = [(sx * Do / 2, 0), (sx * W / 2, 0)] + [(x, z) for x, z in (arch(math.pi * k / 24) for k in range(25)) if sx * x >= Do / 2 and z <= Dh + 0.5] + [(sx * Do / 2, Dh)]
        jamb = sorted(set(jamb))
        # hull of the jamb points is convex enough (a corner of the ellipse)
        slab(B, 'Secondary', jamb, 0.8, Mx((0, y1 - 0.4, 0)), bevel=0.05, seams=False, groove=None)
        slab(C, 'Dark', jamb, 0.8, Mx((0, y1 - 0.4, 0)), bevel=0, seams=False, groove=None)
    lint = [(x, z) for x, z in (arch(math.pi * k / 32) for k in range(33)) if z >= Dh] + [(-Do / 2 - 2, Dh), (Do / 2 + 2, Dh)]
    slab(B, 'Secondary', lint, 0.8, Mx((0, y1 - 0.4, 0)), bevel=0.05, seams=False, groove=None)
    slab(C, 'Dark', lint, 0.8, Mx((0, y1 - 0.4, 0)), bevel=0, seams=False, groove=None)
    box(B, 'Dark', (0, y1 + 0.2, Dh + 0.5), (Do + 1.0, 1.0, 1.2), bevel=0.05, name='DoorTrack')
    hazard(D, Mx((0, y1 + 0.72, Dh - 0.15)), Do, 0.6, n=20)
    # Door leaves rolled open, stacked behind the jambs
    for sx in (-1, 1):
        for q in range(2):
            x = sx * (Do / 2 - 4.6 - q * 0.0) + sx * 0.0
            yy = y1 - 1.3 - q * 0.8
            box(B, 'Metal', (sx * (Do / 2 - 4.6), yy, Dh / 2), (9.2, 0.5, Dh), bevel=0.04, name='Leaf')
            for r in range(7):
                box(B, 'Dark', (sx * (Do / 2 - 4.6), yy + 0.27, 1.0 + r * 2.0), (9.0, 0.08, 0.25), bevel=0, name='LeafRib')
    for k in range(6):
        lamp(D, Mx((-Do / 2 + 3 + k * (Do - 6) / 5, y1 + 0.8, Dh + 1.4)), 1.4, 0.3)
    # Inside: a dark floor and back lights so the opening reads as deep
    box(B, 'Black', (0, 0, 0.05), (W - 1, Lh - 1, 0.1), bevel=0, name='InnerFloor')
    for k in range(5):
        box(D, 'Glow', (-14 + k * 7, y0 + 0.65, Hh * 0.55), (3.0, 0.08, 0.6), bevel=0, name='WallLight')
    for k in range(3):
        box(D, 'Dark', (-12 + k * 12, 0, 13.5), (1.6, Lh - 6, 0.4), bevel=0, name='Gantry')
        for q in range(4):
            lamp(D, Mx((-12 + k * 12, -18 + q * 12, 13.2), (90, 0, 0)), 1.2, 1.2)
    # Lean-to workshops along both sides
    for sx in (-1, 1):
        xw = sx * (W / 2 + 2.8)
        box(B, 'Secondary', (xw, -4.0, 2.6), (6.0, 36.0, 5.2), ch=0.2, seams=True, seam_min=10, name='Workshop')
        box(B, 'Dark', (xw, -4.0, 5.35), (6.6, 36.6, 0.3), bevel=0.04, name='WsRoof')
        for q in range(6):
            box(B, 'Black', (xw + sx * 3.02, -18 + q * 5.6, 3.0), (0.1, 2.4, 1.2), bevel=0, name='Window')
            if q % 2:
                box(D, 'Glow', (xw + sx * 3.06, -18 + q * 5.6, 2.8), (0.05, 2.0, 0.5), bevel=0, name='Light')
        box(B, 'Dark', (xw + sx * 3.05, 12.0, 1.4), (0.2, 2.0, 2.8), bevel=0.03, name='Door')
        col_box((6.0, 36.0, 5.4), Mx((xw, -4.0, 2.7)))
    # Apron slab in front
    box(B, 'Secondary', (0, y1 + 12.0, 0.1), (W + 6, 24.0, 0.2), bevel=0.05, seams=True, seam_min=60, name='Apron')
    for k in range(3):
        box(D, 'Accent', (0, y1 + 4 + k * 8, 0.22), (0.4, 5.0, 0.02), bevel=0, name='Line')


def control_tower():
    """An airfield control tower: operations block, a 30 m shaft with a lit stair window, and a flared glass cab with a radar on top."""
    box(B, 'Primary', (0, 0, 4.0), (22.0, 14.0, 8.0), ch=0.3, seams=True, seam_min=12, name='Ops')
    box(B, 'Dark', (0, 0, 8.2), (22.6, 14.6, 0.4), ch=0.3)
    for k in range(7):
        box(B, 'Black', (-9 + k * 3, 7.02, 4.8), (2.2, 0.1, 2.2), bevel=0, name='Window')
        if k % 3 != 1:
            box(D, 'Glow', (-9 + k * 3, 7.08, 4.4), (1.8, 0.05, 0.9), bevel=0, name='Light')
    box(B, 'Black', (7.0, 7.05, 1.6), (4.0, 0.2, 3.2), bevel=0.02, name='Entrance')
    box(D, 'Dark', (7.0, 8.4, 3.5), (6.0, 3.0, 0.3), bevel=0.04, name='Canopy')
    for x in (4.4, 9.6):
        beam(D, 'Metal', (x, 9.6, 0), (x, 9.6, 3.4), 0.18)
    lamp(D, Mx((7.0, 8.4, 3.25), (-90, 0, 0)), 1.6, 0.3)
    for k in range(3):
        box(D, 'Metal', (-6 + k * 3.4, -3.0, 9.0), (2.6, 2.6, 1.4), ch=0.2, name='AC')
        lathe(D, 'Black', (-6 + k * 3.4, -3.0, 9.71), (0, 0, 1), [(0, 0), (0.9, 0), (0, 0.01)], segs=14, name='Fan')
    # Shaft with a vertical lit stair window
    S0, S1 = 8.4, 31.0
    armour(B, 'Secondary', boxpts((7.4, 7.4, S1 - S0), ch=1.2, taper=(0.9, 0.9)), Mx((-2.0, 0, (S0 + S1) / 2)), bevel=0.1, seams=True, seam_min=8, name='Shaft')
    box(B, 'Black', (-2.0, 3.5, (S0 + S1) / 2), (1.4, 0.3, S1 - S0 - 2.0), bevel=0, name='StairWindow')
    for q in range(8):
        box(D, 'Glow', (-2.0, 3.62, S0 + 2 + q * 2.6), (1.0, 0.05, 0.9), bevel=0, name='StairLight')
    # Balcony under the cab
    lathe(B, 'Dark', (-2.0, 0, S1), (0, 0, 1), [(0, 0), (7.0, 0), (7.0, 0.5), (0, 0.5)], segs=8, name='Balcony')
    railing(D, [(-2.0 + 6.9 * math.cos(math.radians(22.5 + 45 * k)), 6.9 * math.sin(math.radians(22.5 + 45 * k)), S1 + 0.5) for k in range(9)], h=1.1, post=1.4)
    # Flared glass cab: octagonal frustum, wider at the top, with mullions and a roof cap
    c0, c1 = S1 + 0.5, S1 + 5.6
    r0, r1 = 5.0, 6.2
    armour(B, 'Black', ngon(c0, r0, 8, 22.5, cx=-2.0) + ngon(c1, r1, 8, 22.5, cx=-2.0), bevel=0.0, seams=False, groove=None, name='CabGlass')
    armour(D, 'Glow', ngon(c0 + 0.3, r0 - 0.4, 8, 22.5, cx=-2.0) + ngon(c0 + 1.2, r0 - 0.3, 8, 22.5, cx=-2.0), bevel=0.0, seams=False, groove=None, name='Consoles')
    for k in range(8):
        t = math.radians(22.5 + 45 * k)
        a = Vector((-2.0 + math.cos(t) * (r0 + 0.05), math.sin(t) * (r0 + 0.05), c0))
        b = Vector((-2.0 + math.cos(t) * (r1 + 0.05), math.sin(t) * (r1 + 0.05), c1))
        beam(B, 'Dark', a, b, 0.22, name='Mullion')
    armour(B, 'Dark', ngon(c1, r1 + 0.2, 8, 22.5, cx=-2.0) + ngon(c1 + 0.9, r1 + 0.9, 8, 22.5, cx=-2.0) + ngon(c1 + 1.5, r1 + 0.6, 8, 22.5, cx=-2.0), bevel=0.06, seams=False, groove=None, name='CabRoof')
    armour(B, 'Primary', ngon(c1 + 1.5, 3.0, 8, 22.5, cx=-2.0) + ngon(c1 + 3.0, 2.6, 8, 22.5, cx=-2.0), bevel=0.06, seams=False, groove=None, name='RoofHouse')
    # Rotating radar antenna on a mast, antennas, wind sock pole and beacon
    rt = c1 + 3.0
    lathe(B, 'Dark', (-2.0, 0, rt), (0, 0, 1), [(0, 0), (0.6, 0), (0.4, 2.2), (0, 2.2)], segs=14, name='RadarMast')
    armour(B, 'Secondary', [(-2.0 + x, y, rt + 2.4 + z) for x in (-3.4, 3.4) for y, z in ((-0.5, -0.5), (0.5, -0.4), (0.4, 0.6), (-0.5, 0.5))], bevel=0.04, seams=False, groove=None, name='RadarBar')
    for k in range(3):
        lathe(D, 'Metal', (-2.0 + 2.2 * math.cos(2 * math.pi * k / 3 + 1), 2.2 * math.sin(2 * math.pi * k / 3 + 1), c1 + 1.5), (0, 0, 1), [(0, 0), (0.08, 0), (0.04, 5.0 + k), (0, 5.0 + k)], segs=6, name='Whip')
    lathe(D, 'Hot', (-2.0, 0, rt + 3.0), (0, 0, 1), [(0, 0), (0.25, 0), (0, 0.4)], segs=8, name='Beacon')
    pole = Vector((9.0, -5.0, 8.4))
    lathe(D, 'Metal', pole, (0, 0, 1), [(0, 0), (0.08, 0), (0.06, 6.0), (0, 6.0)], segs=6, name='SockPole')
    lathe(D, 'Accent', pole + Vector((0.2, 0, 5.6)), (1, 0, 0), [(0.45, 0), (0.25, 2.6), (0, 2.6)], segs=10, name='Sock')
    col_box((22.0, 14.0, 8.4), Mx((0, 0, 4.2)))
    col_box((7.4, 7.4, S1 - S0), Mx((-2.0, 0, (S0 + S1) / 2)))
    col_cyl((-2.0, 0, (c0 + c1 + 1.5) / 2), (0, 0, 1), r1, c1 + 1.5 - c0 + 0.5, segs=8)


def jet():
    """A parked twin-tail fighter about 17 m long, nose to +Y, sitting on its landing gear."""
    g = 2.1    # fuselage centreline height
    loft(B, 'Primary', [((0, 9.0, g - 0.1), 0.02, 0.02, 2.2), ((0, 8.4, g - 0.05), 0.22, 0.2, 2.2), ((0, 6.8, g), 0.55, 0.5, 2.4), ((0, 4.6, g + 0.1), 0.8, 0.7, 2.6),
                         ((0, 2.0, g + 0.05), 1.15, 0.72, 3.0), ((0, -1.5, g), 1.5, 0.66, 3.2), ((0, -5.0, g), 1.45, 0.6, 3.2), ((0, -7.2, g), 1.3, 0.55, 3.0)],
         axis_up=(0, 0, 1), segs=24, sub=4, cap=(True, True), name='Fuselage')
    # Canopy
    loft(B, 'Black', [((0, 5.6, g + 0.55), 0.05, 0.05, 2.0), ((0, 4.8, g + 0.75), 0.42, 0.32, 2.0), ((0, 3.4, g + 0.85), 0.48, 0.42, 2.0), ((0, 2.0, g + 0.75), 0.4, 0.3, 2.0), ((0, 1.2, g + 0.62), 0.05, 0.05, 2.0)],
         segs=18, sub=3, name='Canopy')
    beam(D, 'Dark', (0, 3.6, g + 1.18), (0, 3.6, g + 1.28), 0.86, 0.08, up=(0, 1, 0), name='Bow')
    # Intakes on the flanks
    for sx in (-1, 1):
        armour(B, 'Secondary', [(sx * x, y, g + z) for y in (2.6, -1.0) for x, z in ((0.9, 0.3), (1.6, 0.3), (1.6, -0.6), (0.9, -0.6))] + [(sx * 1.05, 3.0, g - 0.15)], bevel=0.04, seams=False, groove=None, name='Intake')
        box(B, 'Black', (sx * 1.25, 2.62, g - 0.15), (0.6, 0.05, 0.8), bevel=0, name='IntakeMouth')
    # Wings: thin cranked-delta slabs
    for sx in (-1, 1):
        wing = [(sx * 1.3, 2.0), (sx * 1.3, -5.8), (sx * 6.0, -4.8), (sx * 6.0, -3.9), (sx * 2.4, 1.0)]
        armour(B, 'Primary', [(x, y, g - 0.15 + e) for x, y in wing for e in (-0.1, 0.1)], bevel=0.03, seams=False, groove=None, name='Wing')
        armour(B, 'Secondary', [(sx * x, y, g - 0.15 + e) for x, y in ((5.9, -3.9), (5.9, -4.8), (6.15, -4.8), (6.15, -3.9)) for e in (-0.16, 0.16)], bevel=0.02, seams=False, groove=None, name='Rail')
        box(D, 'Glow', (sx * 6.1, -3.7, g - 0.15), (0.1, 0.25, 0.1), bevel=0, name='NavLight')
        # Stabilator
        armour(B, 'Primary', [(x, y, g - 0.05 + e) for x, y in ((sx * 1.4, -6.0), (sx * 1.4, -8.4), (sx * 3.9, -8.6), (sx * 3.9, -7.9)) for e in (-0.07, 0.07)], bevel=0.02, seams=False, groove=None, name='Stab')
        # Canted twin fins
        base = [(sx * 1.0, -4.6), (sx * 1.0, -7.6)]
        top = [(sx * 2.1, -7.0), (sx * 2.1, -8.4)]
        armour(B, 'Primary', [(x + e, y, g + 0.5) for x, y in base for e in (-0.08, 0.08)] + [(x + e, y, g + 3.4) for x, y in top for e in (-0.06, 0.06)], bevel=0.02, seams=False, groove=None, name='Fin')
        armour(D, 'Accent', [(sx * 1.95 + e, y, g + z) for y, z in ((-7.05, 3.0), (-8.25, 3.0), (-8.4, 3.38), (-7.1, 3.38)) for e in (-0.09, 0.09)], bevel=0, seams=False, groove=None, name='FinTip')
        # Engine nozzle
        lathe(B, 'Dark', (sx * 0.62, -7.0, g - 0.05), (0, -1, 0), [(0.62, 0), (0.66, 0.4), (0.56, 1.4), (0.48, 1.4), (0.48, 1.3)], segs=20, name='Nozzle')
        lathe(D, 'Black', (sx * 0.62, -8.3, g - 0.05), (0, -1, 0), [(0, 0), (0.48, 0), (0, 0.01)], segs=16, name='NozzleIn')
        # Main gear
        beam(B, 'Metal', (sx * 1.7, -1.6, g - 0.4), (sx * 2.0, -1.6, 0.5), 0.16, name='Strut')
        cyl(B, 'Rubber', (sx * 2.05, -1.6, 0.48), (1, 0, 0), 0.48, 0.32, ch=0.08, segs=16, name='Wheel')
        cyl(D, 'Metal', (sx * 2.05, -1.6, 0.48), (1, 0, 0), 0.24, 0.36, ch=0.04, segs=12, name='Hub')
        armour(B, 'Secondary', [(sx * x, y, g + z) for x in (1.3, 1.35) for y, z in ((-0.6, -0.6), (-2.6, -0.6), (-2.6, -1.5), (-0.6, -1.5))], bevel=0, seams=False, groove=None, name='GearDoor')
        # Underwing store
        lathe(B, 'Secondary', (sx * 3.8, -1.2, g - 0.7), (0, 1, 0), [(0, -1.6), (0.28, -1.3), (0.28, 1.4), (0, 2.0)], segs=12, name='Tank')
        beam(D, 'Dark', (sx * 3.8, -1.2, g - 0.4), (sx * 3.8, -1.2, g - 0.25), 0.2, 1.0, name='Pylon')
    # Nose gear
    beam(B, 'Metal', (0, 6.0, g - 0.4), (0, 6.1, 0.35), 0.14, name='Strut')
    cyl(B, 'Rubber', (0, 6.1, 0.34), (1, 0, 0), 0.34, 0.24, ch=0.06, segs=14, name='Wheel')
    # Markings: a band behind the cockpit and a pitot boom
    loft(D, 'Accent', [((0, 0.9, g + 0.02), 1.21, 0.75, 3.0), ((0, 0.6, g + 0.02), 1.24, 0.76, 3.0)], segs=24, sub=1, cap=(False, False), name='Band')
    lathe(D, 'Metal', (0, 9.0, g - 0.1), (0, 1, 0), [(0, 0), (0.04, 0), (0.02, 1.2), (0, 1.2)], segs=6, name='Pitot')
    lathe(D, 'Hot', (0, -1.0, g + 0.68), (0, 0, 1), [(0, 0), (0.1, 0), (0, 0.12)], segs=8, name='Beacon')
    # Wheel chocks and a ground power cart
    for sx in (-1, 1):
        box(D, 'Accent', (sx * 2.05, -0.95, 0.15), (0.3, 0.3, 0.3), bevel=0.02, name='Chock')
    box(B, 'Accent', (3.4, 5.0, 0.7), (1.4, 2.4, 1.0), ch=0.1, name='Cart')
    for sx in (-1, 1):
        for sy in (-1, 1):
            cyl(D, 'Rubber', (3.4 + sx * 0.72, 5.0 + sy * 0.8, 0.22), (1, 0, 0), 0.22, 0.14, segs=10, name='CartWheel')
    tube(D, 'Black', [(3.0, 4.0, 0.9), (2.0, 3.4, 0.3), (1.0, 2.6, g - 0.4)], 0.06, segs=6, name='Cable')
    col_box((2.8, 16.5, 2.2), Mx((0, 0.4, g)))
    col_box((12.0, 6.0, 0.6), Mx((0, -2.0, g - 0.15)))
    col_box((4.0, 3.0, 3.4), Mx((0, -7.0, g + 1.7)))


ALL = [('PowerCore', power_core, {'view_target': (0, 0, 9), 'dist': 52}),
       ('Dam', dam, {'view_target': (0, 120, 50), 'dist': 620, 'lens': 40}),
       ('TurbineHall', turbine_hall, {'view_target': (-8, 0, 10), 'dist': 120}),
       ('Transformer', transformer, {'view_target': (0, 0, 4), 'dist': 22}),
       ('Pylon', pylon, {'view_target': (0, 0, 22), 'dist': 80}),
       ('LaunchPad', launch_pad, {'view_target': (0, 0, 45), 'dist': 300}),
       ('FuelSphere', fuel_sphere, {'view_target': (0, 0, 9), 'dist': 50}),
       ('RadarDish', radar_dish, {'view_target': (0, 0, 10), 'dist': 52}),
       ('Blockhouse', blockhouse, {'view_target': (0, 0, 5), 'dist': 58}),
       ('Hangar', hangar, {'view_target': (0, 0, 9), 'dist': 140}),
       ('ControlTower', control_tower, {'view_target': (0, 0, 19), 'dist': 110}),
       ('Jet', jet, {'view_target': (0, 0, 2), 'dist': 30})]

if __name__ == '__main__':
    only = [a for a in sys.argv[1:] if a in [n for n, _, _ in ALL]]
    for name, fn, kw in ALL:
        if only and name not in only:
            continue
        build(name, fn, **kw)
        print('built', name)
