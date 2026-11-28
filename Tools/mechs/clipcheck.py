"""
Finds parts of a robot that pass through each other (not used by the game).

Usage: python clipcheck.py <robot> [yaw degrees,...]
  robot: titan|lancer|vanguard|arachne|mammoth|strider|colossus|scarab|Raptor|Hornet|Juggernaut|Bastion

Builds the robot with its default weapons (as showcase.py does) and reports:
  1. Hydraulic cylinders (barrel and rod) whose body runs through armour, a leg or
     another cylinder. The pinned ends are allowed to sit in their lugs.
  2. Turret clearance: the upper body, arms and weapons turned to each yaw angle
     (default every 45 degrees) against the hull and legs, which do not turn.
A point counts as inside a part when it is more than DEPTH (default 0.04 m) under
that part's surface. Every pair is listed with the number of points and the deepest one.
"""
import sys, os, runpy, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
robot = sys.argv[1]
yaws = [float(a) for a in sys.argv[2].split(',')] if len(sys.argv) > 2 else [0, 45, 90, 135, 180, -135, -90, -45]
DEPTH = float(os.environ.get('DEPTH', '0.04'))
WHERE = os.environ.get('WHERE') == '1'

import mechlib as ml
ml.render = lambda *a, **k: None
sys.argv = ['showcase', robot, os.devnull]
ns = runpy.run_path(os.path.join(HERE, 'showcase.py'))
R = ns['R']
import bpy, bmesh
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

PARTS = dict(ml.PARTS)
rig_parts = set(R.frames)


def _B(p):
    return Vector((p[0], p[2], p[1]))


# All fitted weapons share part names ('Static', 'Spin'), so split them by the anchor each
# piece sits nearest to: one pseudo-part per weapon, e.g. 'Static@MountL'.
WANCH = {nm: an for nm, an in R.anchors.items() if nm.startswith(('Mount', 'Back', 'Shoulder'))}
ANCHOR_OF = {}
for p in [p for p in PARTS if p not in rig_parts]:
    split = {}
    for ob in PARTS.pop(p):
        c = ob.matrix_world @ (sum((Vector(b) for b in ob.bound_box), Vector()) / 8)
        nm = min(WANCH, key=lambda n: (_B(WANCH[n]['p']) - c).length) if WANCH else 'X'
        split.setdefault(nm, []).append(ob)
    for nm, objs in split.items():
        PARTS[p + '@' + nm] = objs; ANCHOR_OF[p + '@' + nm] = nm
weapon_parts = [p for p in PARTS if p not in rig_parts]
ram_names = set()
for ram in R.rams:
    ram_names.add(ram['sleeve']); ram_names.add(ram['rod'])

UPPER_BONES = ('Upper', 'Arms')


def bone_of(part):
    seen = 0
    p = part
    while p in R.parents and seen < 10:
        par = R.parents[p]
        if par in UPPER_BONES or par in ('Root', 'Hull') or par not in R.parents:
            return par
        p = par; seen += 1
    return R.parents.get(part, 'Root')


def turns(part):
    if part in weapon_parts:
        return True
    if part in ram_names:
        return False
    return bone_of(part) in UPPER_BONES


def mesh_of(objs, M=Matrix.Identity(4)):
    bm = bmesh.new()
    for ob in objs:
        tmp = bmesh.new(); tmp.from_mesh(ob.data); tmp.transform(M @ ob.matrix_world)
        me = bpy.data.meshes.new('t'); tmp.to_mesh(me); tmp.free()
        bm.from_mesh(me); bpy.data.meshes.remove(me)
    bm.verts.ensure_lookup_table()
    return bm


RAYS = [Vector(v).normalized() for v in ((0.31, 0.53, 0.79), (-0.72, 0.21, -0.37), (0.12, -0.91, 0.28))]


class Shell:
    """One closed piece of a part (each modelling call makes one)."""
    def __init__(self, ob, M):
        bm = bmesh.new(); bm.from_mesh(ob.data); bm.transform(M @ ob.matrix_world)
        bm.verts.ensure_lookup_table()
        self.verts = [v.co.copy() for v in bm.verts]
        self.bvh = BVHTree.FromBMesh(bm)
        xs = [v.x for v in self.verts] or [0]; ys = [v.y for v in self.verts] or [0]; zs = [v.z for v in self.verts] or [0]
        self.lo = Vector((min(xs), min(ys), min(zs))); self.hi = Vector((max(xs), max(ys), max(zs)))
        bm.free()

    def depth(self, p):
        if any(p[i] < self.lo[i] or p[i] > self.hi[i] for i in range(3)):
            return 0.0
        hit = self.bvh.find_nearest(p)
        if hit[0] is None:
            return 0.0
        loc, n, _, d = hit
        if (p - loc).dot(n) >= 0:
            return 0.0
        # Inside a closed shell a ray leaves through the surface an odd number of times.
        # Three rays vote, so one that grazes an edge cannot fake an inside point.
        odd = 0
        for dirv in RAYS:
            cnt, o = 0, p.copy()
            for _ in range(16):
                h = self.bvh.ray_cast(o, dirv)
                if h[0] is None:
                    break
                cnt += 1
                o = h[0] + dirv * 1e-4
            odd += cnt % 2
        return d if odd >= 2 else 0.0


class Body:
    def __init__(self, name, objs, M=Matrix.Identity(4)):
        self.name = name
        self.shells = [Shell(ob, M) for ob in objs if len(ob.data.vertices) > 3]
        self.verts = [v for s in self.shells for v in s.verts]
        if self.verts:
            self.lo = Vector(tuple(min(s.lo[i] for s in self.shells) for i in range(3)))
            self.hi = Vector(tuple(max(s.hi[i] for s in self.shells) for i in range(3)))

    def overlaps(self, o, pad=0.05):
        if not self.verts or not o.verts:
            return False
        return all(self.lo[i] - pad <= o.hi[i] and o.lo[i] - pad <= self.hi[i] for i in range(3))

    def depth(self, p):
        """How far p is under this part's surface (0 when outside)."""
        return max((s.depth(p) for s in self.shells), default=0.0)


def report(title, rows):
    print('\n== ' + title)
    if not rows:
        print('   clear')
    for (a, b), (n, dmax) in sorted(rows.items(), key=lambda kv: -kv[1][1]):
        print('   %-28s in %-28s points %4d  deepest %.2f m' % (a, b, n, dmax))


def pins_of(name):
    return [ob for ob in PARTS.get(name, []) if ob.name.startswith('Pin')]


# ---- 1. Hydraulic cylinders ----
statics = {p: Body(p, PARTS[p]) for p in PARTS if p not in weapon_parts}
rows = {}
for ram in R.rams:
    S, Rd = ram['sleeve'], ram['rod']
    ends = []
    rr = 0.2
    for nm in (S, Rd):
        for pin in pins_of(nm):
            c = pin.matrix_world @ (sum((Vector(b) for b in pin.bound_box), Vector()) / 8)
            ends.append(c)
            rr = max(dim for dim in pin.dimensions) / 2.3
    for nm in (S, Rd):
        body = statics[nm]
        for other, ob in statics.items():
            if other in (S, Rd) or not body.overlaps(ob):
                continue
            n, dmax = 0, 0.0
            for p in body.verts:
                if any((p - e).length < rr * 2.4 for e in ends):
                    continue
                d = ob.depth(p)
                if d > DEPTH:
                    n += 1
                    if d > dmax:
                        dmax = d; wp = p
            if n:
                if WHERE: print('      deepest %s>%s at (%.2f, %.2f, %.2f)' % (nm, other, wp.x, wp.y, wp.z))
                key = (S[:-1], other)
                m = rows.get(key, (0, 0.0)); rows[key] = (m[0] + n, max(m[1], dmax))
report('Cylinders running through other parts', rows)

# ---- 2. Turret clearance: yaw the upper body, pitch the arms ----
def B(p):
    return Vector((p[0], p[2], p[1]))


up = R.extra.get('upper')
pivot = B(up) if up else Vector((0, 0, 0))
ar = R.extra.get('arms')
arms_pivot = B(ar) if ar else pivot


def weapon_bone(part):
    par = R.anchors[ANCHOR_OF[part]]['parent'] if ANCHOR_OF.get(part) in R.anchors else 'Upper'
    if par in ('Root', 'Hull'):
        return None  # a weapon fixed to the hull (a back mortar) does not turn
    return 'Arms' if par == 'Arms' else 'Upper'


def group(part):
    if part in ram_names:
        return None
    if part in weapon_parts:
        return weapon_bone(part)
    b = bone_of(part)
    return b if b in UPPER_BONES else None


turning = {p: group(p) for p in PARTS if group(p)}
fixed = [p for p in PARTS if p not in turning and p not in ram_names]
fixed_bodies = {p: statics[p] if p in statics else Body(p, PARTS[p]) for p in fixed}
pitches = [float(a) for a in os.environ.get('PITCH', '25,0,-30').split(',')]  # degrees down (+) / up (-)
for yaw in yaws:
    Y = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(yaw), 4, 'Z') @ Matrix.Translation(-pivot)
    for pitch in pitches:
        P = Matrix.Translation(arms_pivot) @ Matrix.Rotation(math.radians(-pitch), 4, 'X') @ Matrix.Translation(-arms_pivot)
        rows = {}; where = {}
        for p, g in turning.items():
            if g == 'Upper' and pitch != pitches[0]:
                continue  # the body does not pitch: checked once per yaw
            b = Body(p, PARTS[p], Y @ P if g == 'Arms' else Y)
            for q, fb in fixed_bodies.items():
                if not b.overlaps(fb):
                    continue
                n, dmax = 0, 0.0
                for v in b.verts:
                    d = fb.depth(v)
                    if d > DEPTH:
                        n += 1
                        if d > dmax:
                            dmax = d
                            if WHERE: where[(p, q)] = Y.inverted() @ v if g == 'Upper' else (Y @ P).inverted() @ v
                for v in fb.verts:
                    d = b.depth(v)
                    if d > DEPTH:
                        n += 1; dmax = max(dmax, d)
                if n:
                    rows[(p, q)] = (n, dmax)
        if WHERE:
            for kk, vv in where.items():
                if kk in rows: print('      deepest %s>%s at part-rest (%.2f, %.2f, %.2f)' % (kk[0], kk[1], vv.x, vv.y, vv.z))
        report('Turret %+d deg, arms %+d deg down: turning parts inside fixed parts' % (yaw, pitch), rows)
