"""
Hard-surface armour kit (second generation, 2026-11-27).

The first sculpted models were lofted superellipses: smooth, inflated volumes that
read as soft plastic toys. Real large machines (excavators, tank hulls, cranes,
the hero mechs in the reference art) are built from flat, cut and welded plate:
crisp facets, chamfered edges, panel seams, bolted covers, with the dark
mechanical frame and the hydraulics showing in the gaps between armour pieces.

Everything here builds convex faceted pieces from point sets (convex hull), so a
handful of numbers describes an angular armour piece. Pieces get:
  * chamfered edges (one bevel segment: a bright edge highlight, never a blob)
  * engraved panel seams on their big faces (a dark V groove around an inset panel)
  * optional painted stripe faces
Concave shapes come from combining several convex pieces, the way real armour is
assembled from separate plates.

Coordinates: Blender metres, Z up, front = +Y. Most helpers take a 4x4 matrix M
that places local coordinates in the world (or in the bone space of a leg part).
"""
import bpy, bmesh, math
from mathutils import Vector, Matrix
import mechlib as ml
from mechlib import V, lathe, tube, MATS

I4 = Matrix.Identity(4)


def Mx(t=(0, 0, 0), r=(0, 0, 0), s=None):
    """Matrix: translate t, rotate r (degrees, applied X then Y then Z)."""
    R = (Matrix.Rotation(math.radians(r[2]), 4, 'Z') @ Matrix.Rotation(math.radians(r[1]), 4, 'Y')
         @ Matrix.Rotation(math.radians(r[0]), 4, 'X'))
    M = Matrix.Translation(V(t)) @ R
    if s is not None:
        M = M @ Matrix.Diagonal((s[0], s[1], s[2], 1))
    return M


def _new_obj(name, bm, mats, part):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(bpy.data.materials[m])
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for p in me.polygons:
        p.use_smooth = False
    ml.PARTS.setdefault(part, []).append(ob)
    return ob


# ---------------------------------------------------------------------------
# Section point sets
# ---------------------------------------------------------------------------

def sec(z, w, d, ch=0.0, cx=0.0, cy=0.0):
    """
    A chamfered rectangle cross-section at height z (local), half width w (X),
    half depth d (Y), centred at (cx, cy). ch = corner chamfer, a number or a
    tuple (front-right, front-left, back-left, back-right).
    """
    if not isinstance(ch, (tuple, list)):
        ch = (ch, ch, ch, ch)
    out = []
    corners = ((1, 1), (-1, 1), (-1, -1), (1, -1))
    for (sx, sy), c in zip(corners, ch):
        c = min(c, w * 0.95, d * 0.95)
        if c <= 1e-4:
            out.append((cx + sx * w, cy + sy * d, z))
        else:
            out.append((cx + sx * w, cy + sy * (d - c), z))
            out.append((cx + sx * (w - c), cy + sy * d, z))
    return out


def ngon(z, r, n, rot=0.0, sx=1.0, sy=1.0, cx=0.0, cy=0.0):
    return [(cx + math.cos(math.radians(rot) + 2 * math.pi * k / n) * r * sx,
             cy + math.sin(math.radians(rot) + 2 * math.pi * k / n) * r * sy, z) for k in range(n)]


def boxpts(size, ch=0.0, taper=(1.0, 1.0), shift=(0.0, 0.0), endch=0.0):
    """
    Points of a box (full sizes x, y, z) centred at the origin, edges along Z chamfered
    by ch; the top face is scaled by taper and shifted by shift (x, y). endch chamfers the
    top and bottom edges.
    """
    w, d, h = size[0] / 2, size[1] / 2, size[2] / 2
    pts = []
    for z, k, (ox, oy) in ((-h, 1.0, (0, 0)), (h, 1.0, shift)):
        tw = w * (taper[0] if z > 0 else 1.0)
        td = d * (taper[1] if z > 0 else 1.0)
        if endch > 0:
            zz = z - math.copysign(endch, z)
            pts += sec(zz, tw, td, ch, ox, oy)
            pts += sec(z, max(0.02, tw - endch), max(0.02, td - endch), max(0.0, ch - endch * 0.4), ox, oy)
        else:
            pts += sec(z, tw, td, ch, ox, oy)
    return pts


# ---------------------------------------------------------------------------
# Faceted armour piece from points
# ---------------------------------------------------------------------------

def armour(part, mat, pts, M=I4, bevel=0.06, seams=True, seam_min=0.9, seam_w=0.16, groove='Black',
           stripe=None, name='Armour'):
    """
    Convex faceted piece through pts (local), placed by M.
      bevel:   edge chamfer width (m)
      seams:   engrave a panel seam on every face bigger than seam_min m^2
      stripe:  (axis vector, min dot) paints faces whose normal points that way with Accent
    """
    bm = bmesh.new()
    for p in pts:
        bm.verts.new(V(p))
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-4)
    r = bmesh.ops.convex_hull(bm, input=list(bm.verts), use_existing_faces=False)
    junk = list({g for g in r['geom_interior'] + r['geom_unused'] if isinstance(g, bmesh.types.BMVert)})
    if junk:
        bmesh.ops.delete(bm, geom=junk, context='VERTS')
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(1.5), use_dissolve_boundaries=False,
                             verts=list(bm.verts), edges=list(bm.edges))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    mats = [mat]
    gi = None
    if groove and seams:
        mats.append(groove); gi = 1
    si = None
    if stripe:
        mats.append('Accent'); si = len(mats) - 1
    chamfers = set()
    if bevel > 0:
        rb = bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, profile=0.5, affect='EDGES', clamp_overlap=True)
        chamfers = set(rb['faces'])
    orig = [f for f in bm.faces if f not in chamfers]
    big = [f for f in orig if f.calc_area() > seam_min and len(f.verts) >= 4]
    if stripe:
        ax = V(stripe[0]).normalized()
        for f in orig:
            if f.is_valid and f.normal.dot(ax) > stripe[1]:
                f.material_index = si
    if seams and gi is not None:
        for f in big:
            if not f.is_valid:
                continue
            # Border width relative to the face: about a sixth of its smaller span
            cen = f.calc_center_median()
            span = min((v.co - cen).length for v in f.verts)
            t = min(seam_w, span * 0.28)
            if t < 0.05:
                continue
            g = min(0.028, t * 0.25)
            dpt = min(0.045, t * 0.3)
            keep_mat = f.material_index
            r1 = bmesh.ops.inset_individual(bm, faces=[f], thickness=t, depth=0.0, use_even_offset=True)
            r2 = bmesh.ops.inset_individual(bm, faces=[f], thickness=g, depth=-dpt, use_even_offset=True)
            for ff in r2['faces']:
                ff.material_index = gi
            r3 = bmesh.ops.inset_individual(bm, faces=[f], thickness=g, depth=dpt * 0.7, use_even_offset=True)
            for ff in r3['faces']:
                ff.material_index = gi
            f.material_index = keep_mat
    bmesh.ops.transform(bm, matrix=M, verts=list(bm.verts))
    if M.determinant() < 0:
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    return _new_obj(name, bm, mats, part)


def slab(part, mat, outline, thick, M=I4, bevel=0.05, seams=True, **kw):
    """
    A flat plate cut to a convex 2D outline [(x, z), ...] in the XZ plane, thickness along Y
    (front face at +thick/2). Use M to place and tilt it.
    """
    pts = [(x, y, z) for x, z in outline for y in (-thick / 2, thick / 2)]
    return armour(part, mat, pts, M, bevel=bevel, seams=seams, **kw)


def wedge(part, mat, size, M=I4, front=0.5, top=0.7, ch=0.0, bevel=0.06, **kw):
    """A box whose top face is pulled back (front < 1 shrinks the top's depth towards the back)."""
    w, d, h = size[0] / 2, size[1] / 2, size[2] / 2
    pts = sec(-h, w, d, ch) + sec(h, w * top, d * front, ch * front, 0, -d * (1 - front))
    return armour(part, mat, pts, M, bevel=bevel, **kw)


# ---------------------------------------------------------------------------
# Mechanical parts: actuators, joints, fasteners, cables, vents, lights
# ---------------------------------------------------------------------------

def cyl(part, mat, centre, axis, r, length, ch=0.05, segs=24, name='Cyl'):
    c = V(centre); ax = V(axis).normalized()
    h = length / 2
    return lathe(part, mat, c, ax, [(0, -h), (r - ch, -h), (r, -h + ch), (r, h - ch), (r - ch, h), (0, h)], segs=segs, name=name)


def drum(part, centre, axis, r, width, body='Dark', face='Secondary', hub='Metal', bolts=8, segs=32):
    """
    A heavy rotary joint: a machined drum with stepped faces, a recessed ring, a hub
    cap and a bolt circle on each side. Reads as a real bearing housing.
    """
    c = V(centre); ax = V(axis).normalized()
    w = width / 2
    lathe(part, body, c, ax, [(0, -w), (r * 0.92, -w), (r, -w + 0.06), (r, w - 0.06), (r * 0.92, w), (0, w)], segs=segs, name='Drum')
    a = ax.orthogonal().normalized(); b = ax.cross(a)
    for s in (-1, 1):
        o = c + ax * s * w
        # stepped machined face: outer ring, groove, raised inner boss
        lathe(part, face, o, ax * s, [(r * 0.98, -0.02), (r * 0.98, 0.04), (r * 0.9, 0.08), (r * 0.72, 0.08), (r * 0.7, 0.03),
                                       (r * 0.6, 0.03), (r * 0.58, 0.12), (r * 0.4, 0.14), (0, 0.14)], segs=segs, name='DrumFace')
        lathe(part, hub, o + ax * s * 0.12, ax * s, [(0, 0), (r * 0.3, 0), (r * 0.3, 0.08), (r * 0.18, 0.14), (0, 0.15)], segs=16, name='Hub')
        for k in range(bolts):
            t = 2 * math.pi * k / bolts
            p = o + ax * s * 0.06 + (a * math.cos(t) + b * math.sin(t)) * r * 0.82
            lathe(part, 'Metal', p, ax * s, [(0, 0), (r * 0.055, 0), (r * 0.055, 0.05), (0, 0.06)], segs=6, name='Bolt')


def piston(part, a, b, r, frac=0.55, boot=False):
    """
    A static hydraulic cylinder from a to b: barrel with end caps and port, chrome rod,
    clevis lugs and pins at both ends.
    """
    A, B = V(a), V(b)
    d = B - A; L = d.length; ax = d.normalized()
    sl = L * frac
    lathe(part, 'Dark', A, ax, [(0, 0), (r * 1.25, 0), (r * 1.25, r * 0.9), (r * 1.05, r * 1.1), (r * 1.05, sl - r * 0.9),
                                 (r * 1.3, sl - r * 0.7), (r * 1.3, sl), (r * 0.55, sl + r * 0.2), (0, sl + r * 0.2)], segs=18, name='Barrel')
    lathe(part, 'Chrome', A, ax, [(0, sl), (r * 0.55, sl), (r * 0.55, L - r * 0.9), (0, L - r * 0.9)], segs=14, name='Rod')
    side = ax.orthogonal().normalized()
    for p, mt in ((A, 'Dark'), (B - ax * r * 0.4, 'Dark')):
        cyl(part, mt, p, side, r * 0.95, r * 1.5, ch=r * 0.2, segs=14, name='Clevis')
        cyl(part, 'Metal', p, side, r * 0.4, r * 2.1, ch=r * 0.1, segs=10, name='Pin')
    # hydraulic port and feed line stub
    up = ax.cross(side).normalized()
    cyl(part, 'Metal', A + ax * sl * 0.8 + up * r * 1.15, up, r * 0.3, r * 0.6, ch=0.01, segs=8, name='Port')


def bolts_line(part, a, b, n, normal, r=0.06):
    A, B = V(a), V(b); nn = V(normal).normalized()
    for k in range(n):
        p = A.lerp(B, (k + 0.5) / n)
        lathe(part, 'Metal', p, nn, [(0, -0.01), (r, -0.01), (r, 0.04), (r * 0.7, 0.06), (0, 0.06)], segs=6, name='Bolt')


def grille(part, M, w, h, slats=5, depth=0.12, frame='Dark'):
    """A recessed intake: frame box and angled slats, facing +Y in local space."""
    armour(part, frame, boxpts((w, depth * 2, h), ch=0.04), M, bevel=0.03, seams=False, groove=None, name='GrilleFrame')
    for k in range(slats):
        z = -h / 2 + h * (k + 0.5) / slats
        armour(part, 'Black', boxpts((w * 0.88, 0.05, h / slats * 0.55)), M @ Mx((0, depth * 0.85, z), (35, 0, 0)),
               bevel=0.0, seams=False, groove=None, name='Slat')


def lamp(part, M, w, h, mat='Glow'):
    armour(part, 'Dark', boxpts((w * 1.3, 0.3, h * 1.5), ch=0.04), M, bevel=0.03, seams=False, groove=None, name='LampBox')
    armour(part, mat, boxpts((w, 0.06, h)), M @ Mx((0, 0.15, 0)), bevel=0.0, seams=False, groove=None, name='Lens')


def stripe(part, M, w, h, t=0.04):
    """A painted hazard band: a thin Accent plate laid on a surface (local +Y out of it)."""
    armour(part, 'Accent', boxpts((w, t, h)), M, bevel=0.0, seams=False, groove=None, name='Stripe')


def hose(part, pts, r, mat='Black'):
    return tube(part, mat, pts, r, segs=10, name='Hose')


def hazard(part, M, w, h, n=5):
    """Diagonal hazard stripes (Accent bars on a Black band), facing +Y."""
    armour(part, 'Black', boxpts((w, 0.03, h)), M, bevel=0.0, seams=False, groove=None, name='HazBand')
    bw = w / (n * 2)
    for k in range(n):
        x = -w / 2 + bw * (2 * k + 1)
        armour(part, 'Accent', [(x - bw / 2 - h * 0.35, 0.03, -h / 2), (x + bw / 2 - h * 0.35, 0.03, -h / 2),
                                 (x + bw / 2 + h * 0.35, 0.03, h / 2), (x - bw / 2 + h * 0.35, 0.03, h / 2),
                                 (x - bw / 2 - h * 0.35, 0.0, -h / 2), (x + bw / 2 - h * 0.35, 0.0, -h / 2),
                                 (x + bw / 2 + h * 0.35, 0.0, h / 2), (x - bw / 2 + h * 0.35, 0.0, h / 2)],
               M, bevel=0.0, seams=False, groove=None, name='Haz')


def transform_new(part, start, M):
    """Apply M to the objects added to part since index start."""
    for ob in ml.PARTS.get(part, [])[start:]:
        ob.data.transform(M)
        if M.determinant() < 0:
            ob.data.flip_normals()


def count(part):
    return len(ml.PARTS.get(part, []))


def side_plate(part, mat, outline, thick, centre, s, flare=8.0, M=I4, stripe_at=None, stripe_w=None, stripe_h=0.16, hazard_at=None, **kw):
    """
    A plate hung on the side of a body (s = -1 left, +1 right), its bottom flared
    outward by `flare` degrees. outline is [(along, up)] with 'along' running front-back.
    Optional painted stripe / hazard band laid on the outer face at local height z.
    """
    Mp = M @ Mx(centre, (flare, 0, s * 90))
    slab(part, mat, outline, thick, Mp, **kw)
    span = max(x for x, z in outline) - min(x for x, z in outline)
    if stripe_at is not None:
        stripe(part, Mp @ Mx((0, -(thick / 2 + 0.012), stripe_at)), stripe_w or span * 0.75, stripe_h)
    if hazard_at is not None:
        hazard(part, Mp @ Mx((0, -(thick / 2 + 0.01), hazard_at), (0, 0, 180)), span * 0.7, stripe_h * 1.3, n=6)
    return Mp
