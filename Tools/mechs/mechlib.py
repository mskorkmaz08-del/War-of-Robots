"""
Hard-surface modelling toolkit for the War of Robots mechs, run inside Blender's
Python module (bpy). Robots are modelled in Blender world space (metres, Z up,
front = +Y) and exported per part in the frame of the game bone that moves them.

Building blocks:
  loft()   - sculpted volumes from superellipse cross-sections along a path
             (thighs, shins, torsos, armour shells), smoothly interpolated
  lathe()  - turned parts from a radius profile (joints, rams, barrels, hubs)
  block()  - machined blocks with real chamfers (bmesh bevel)
  plate()  - curved armour plates that wrap around a volume
  tube()   - hoses and cables along a curve
Normals are split by angle on export, so bevels and sharp breaks catch the light.
"""
import bpy, bmesh, math, struct, json
from mathutils import Vector, Matrix

MATS = ['Primary', 'Secondary', 'Dark', 'Metal', 'Chrome', 'Accent', 'Glow', 'Black', 'Rubber', 'Hot']
PREVIEW = {
    'Primary': ((0.11, 0.17, 0.25), 0.55, 0.42), 'Secondary': ((0.2, 0.21, 0.22), 0.6, 0.45),
    'Dark': ((0.035, 0.037, 0.04), 0.7, 0.5), 'Metal': ((0.4, 0.4, 0.41), 0.95, 0.35),
    'Chrome': ((0.8, 0.8, 0.82), 1.0, 0.12), 'Accent': ((0.75, 0.42, 0.06), 0.3, 0.5),
    'Glow': ((0.3, 0.85, 1.0), 0.0, 0.3), 'Black': ((0.012, 0.012, 0.014), 0.5, 0.55),
    'Rubber': ((0.02, 0.02, 0.02), 0.0, 0.85), 'Hot': ((1.0, 0.4, 0.1), 0.0, 0.3),
}

# ---------------------------------------------------------------------------
# Scene and materials
# ---------------------------------------------------------------------------

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for m in MATS:
        mat = bpy.data.materials.new(m)
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get('Principled BSDF')
        col, met, rough = PREVIEW[m]
        bsdf.inputs['Base Color'].default_value = (*col, 1)
        bsdf.inputs['Metallic'].default_value = met
        bsdf.inputs['Roughness'].default_value = rough
        if m in ('Glow', 'Hot'):
            bsdf.inputs['Emission Color'].default_value = (*col, 1)
            bsdf.inputs['Emission Strength'].default_value = 3.0


PARTS = {}  # part name -> list of objects


def _obj(name, bm, mat, part):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(bpy.data.materials[mat])
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for p in me.polygons:
        p.use_smooth = True
    PARTS.setdefault(part, []).append(ob)
    return ob


def V(*a):
    return Vector(a if len(a) == 3 else a[0])


# ---------------------------------------------------------------------------
# Loft: superellipse sections along a path
# ---------------------------------------------------------------------------

def _se(t, n):
    c, s = math.cos(t), math.sin(t)
    return (math.copysign(abs(c) ** (2.0 / n), c), math.copysign(abs(s) ** (2.0 / n), s))


def _catmull(p0, p1, p2, p3, u):
    return 0.5 * (2 * p1 + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u * u * u)


def loft(part, mat, secs, axis_up=(0, 0, 1), segs=28, sub=3, cap=(True, True), name='Loft'):
    """
    secs: list of (centre, half_width, half_depth, exponent[, tilt]) along a path.
    The section plane is perpendicular to the path; 'width' is measured along the
    axis perpendicular to both the path and axis_up, 'depth' along axis_up's projection.
    exponent 2 = ellipse, 3-5 = rounded rectangle. Sections are interpolated with a
    Catmull-Rom spline (sub steps between each pair) for smooth sculpted forms.
    """
    up = V(axis_up)
    raw = [(V(s[0]), float(s[1]), float(s[2]), float(s[3]), float(s[4]) if len(s) > 4 else 0.0) for s in secs]
    # Interpolate all parameters
    pts = []
    for i in range(len(raw) - 1):
        a = raw[max(0, i - 1)]; b = raw[i]; c = raw[i + 1]; d = raw[min(len(raw) - 1, i + 2)]
        for k in range(sub):
            u = k / sub
            cen = _catmull(a[0], b[0], c[0], d[0], u)
            vals = [max(0.01, _catmull(a[j], b[j], c[j], d[j], u)) if j < 4 else _catmull(a[j], b[j], c[j], d[j], u) for j in (1, 2, 3, 4)]
            pts.append((cen, *vals))
    pts.append(raw[-1])
    bm = bmesh.new()
    rings = []
    n = len(pts)
    for i, (c, w, dpt, ex, tilt) in enumerate(pts):
        t = (pts[min(i + 1, n - 1)][0] - pts[max(i - 1, 0)][0]).normalized()
        side = t.cross(up)
        if side.length < 1e-4:
            side = t.cross(Vector((0, 1, 0)))
        side.normalize()
        fwd = side.cross(t).normalized()
        if tilt:
            rot = Matrix.Rotation(math.radians(tilt), 3, t)
            side = rot @ side; fwd = rot @ fwd
        ring = []
        for k in range(segs):
            sx, sy = _se(2 * math.pi * k / segs, ex)
            ring.append(bm.verts.new(c + side * (sx * w) + fwd * (sy * dpt)))
        rings.append(ring)
    for i in range(n - 1):
        r0, r1 = rings[i], rings[i + 1]
        for k in range(segs):
            bm.faces.new((r0[k], r0[(k + 1) % segs], r1[(k + 1) % segs], r1[k]))
    for end, ring in ((0, rings[0]), (1, rings[-1])):
        if not cap[end]:
            continue
        # Chamfered cap: a smaller inset ring, then a flat face.
        c = sum((v.co for v in ring), Vector()) / len(ring)
        t = (rings[1][0].co - rings[0][0].co) if end == 0 else (rings[-1][0].co - rings[-2][0].co)
        t = t.normalized() * (-1 if end == 0 else 1)
        rad = max((v.co - c).length for v in ring)
        ch = min(0.12, rad * 0.12)
        inner = [bm.verts.new(c + (v.co - c) * (1 - ch / max(rad, 1e-3) * 1.2) + t * ch) for v in ring]
        for k in range(segs):
            q = (ring[k], ring[(k + 1) % segs], inner[(k + 1) % segs], inner[k])
            bm.faces.new(q if end == 1 else q[::-1])
        f = bm.faces.new(inner if end == 1 else inner[::-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _obj(name, bm, mat, part)


# ---------------------------------------------------------------------------
# Lathe: revolve a (radius, height) profile around an axis
# ---------------------------------------------------------------------------

def lathe(part, mat, centre, axis, profile, segs=32, name='Lathe'):
    """profile: list of (r, h) from one end of the axis to the other (r may be 0 at the ends)."""
    c = V(centre); ax = V(axis).normalized()
    a = ax.orthogonal().normalized(); b = ax.cross(a).normalized()
    bm = bmesh.new()
    rings = []
    for r, h in profile:
        if r <= 1e-4:
            rings.append([bm.verts.new(c + ax * h)])
            continue
        rings.append([bm.verts.new(c + ax * h + (a * math.cos(2 * math.pi * k / segs) + b * math.sin(2 * math.pi * k / segs)) * r) for k in range(segs)])
    for i in range(len(rings) - 1):
        r0, r1 = rings[i], rings[i + 1]
        if len(r0) == 1 and len(r1) == 1:
            continue
        if len(r0) == 1:
            for k in range(segs):
                bm.faces.new((r0[0], r1[k], r1[(k + 1) % segs]))
        elif len(r1) == 1:
            for k in range(segs):
                bm.faces.new((r0[k], r1[0], r0[(k + 1) % segs]))
        else:
            for k in range(segs):
                bm.faces.new((r0[k], r1[k], r1[(k + 1) % segs], r0[(k + 1) % segs]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _obj(name, bm, mat, part)


def disc_joint(part, centre, axis, radius, width, body='Dark', rim='Secondary', hub='Chrome', bolts=10):
    """A big pin joint: drum, two machined side discs with recessed ring, chrome hub cap and bolts."""
    c = V(centre); ax = V(axis).normalized()
    w = width / 2
    lathe(part, body, c, ax, [(0, -w), (radius * 0.8, -w), (radius * 0.8, w), (0, w)], name='Drum')
    for s in (-1, 1):
        o = c + ax * s * (w - 0.02)
        R = radius
        prof = [(0, 0), (R * 0.36, 0), (R * 0.36, 0.1), (R * 0.42, 0.14), (R * 0.5, 0.08), (R * 0.78, 0.08),
                (R * 0.82, 0.18), (R * 0.95, 0.2), (R, 0.12), (R, -0.12), (R * 0.9, -0.2), (0, -0.2)]
        lathe(part, rim, o, ax * s, prof, segs=40, name='JointDisc')
        lathe(part, hub, o + ax * s * 0.1, ax * s, [(0, 0), (R * 0.3, 0), (R * 0.3, 0.1), (R * 0.22, 0.2), (0, 0.22)], segs=24, name='Hub')
        a = ax.orthogonal().normalized(); b = ax.cross(a)
        for k in range(bolts):
            t = 2 * math.pi * k / bolts
            p = o + ax * s * 0.15 + (a * math.cos(t) + b * math.sin(t)) * R * 0.64
            lathe(part, 'Metal', p, ax * s, [(0, -0.02), (R * 0.06, -0.02), (R * 0.06, 0.06), (R * 0.045, 0.09), (0, 0.09)], segs=6, name='Bolt')


def ram(part, a, b, radius, sleeve_frac=0.58):
    """A fixed hydraulic ram from a to b: banded sleeve, gland, chrome rod and clevis eyes."""
    A, B = V(a), V(b)
    d = B - A; L = d.length; ax = d.normalized()
    sl = L * sleeve_frac
    r = radius
    lathe(part, 'Dark', A, ax, [(0, -r * 0.2), (r * 1.15, -r * 0.2), (r * 1.15, sl * 0.12), (r, sl * 0.15), (r, sl * 0.85),
                                  (r * 1.2, sl * 0.88), (r * 1.2, sl), (r * 0.6, sl + 0.05), (0, sl + 0.05)], segs=20, name='Sleeve')
    lathe(part, 'Chrome', A, ax, [(0, sl - 0.1), (r * 0.5, sl - 0.1), (r * 0.5, L - r * 0.6), (0, L - r * 0.6)], segs=16, name='Rod')
    for p in (A, B):
        side = ax.orthogonal().normalized()
        lathe(part, 'Dark', p - side * r * 0.7, side, [(0, 0), (r * 0.9, 0), (r * 0.9, r * 1.4), (0, r * 1.4)], segs=16, name='Eye')


# ---------------------------------------------------------------------------
# Blocks and plates
# ---------------------------------------------------------------------------

def block(part, mat, centre, size, rot=(0, 0, 0), bevel=0.08, segs=2, taper=None, name='Block'):
    """A machined block with chamfered edges. taper=(sx, sy) scales the top face (+Z)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        if taper and v.co.z > 0:
            v.co.x *= taper[0]; v.co.y *= taper[1]
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, min(size) * 0.45), segments=segs, profile=0.5, affect='EDGES')
    R = (Matrix.Rotation(math.radians(rot[2]), 4, 'Z') @ Matrix.Rotation(math.radians(rot[1]), 4, 'Y') @ Matrix.Rotation(math.radians(rot[0]), 4, 'X'))
    bmesh.ops.transform(bm, matrix=Matrix.Translation(V(centre)) @ R, verts=bm.verts)
    return _obj(name, bm, mat, part)


def plate(part, mat, centre, size, rot=(0, 0, 0), curve=(0.0, 0.0), bevel=0.05, nx=10, ny=10, taper=None, name='Plate'):
    """
    A curved armour plate: size = (width X, height Z, thickness Y), facing +Y before rot.
    curve=(cx, cz) bends it back by cx*x^2 + cz*z^2 so it wraps round a volume.
    """
    w, h, t = size
    bm = bmesh.new()
    grid = {}
    for side in (0, 1):
        for i in range(nx + 1):
            for j in range(ny + 1):
                x = -w / 2 + w * i / nx; z = -h / 2 + h * j / ny
                if taper:
                    x *= 1 + (taper - 1) * (j / ny)
                y = (t / 2 if side else -t / 2) - curve[0] * x * x - curve[1] * z * z
                grid[side, i, j] = bm.verts.new((x, y, z))
    for side in (0, 1):
        for i in range(nx):
            for j in range(ny):
                q = [grid[side, i, j], grid[side, i + 1, j], grid[side, i + 1, j + 1], grid[side, i, j + 1]]
                bm.faces.new(q if side == 0 else q[::-1])
    def edge(path):
        for k in range(len(path) - 1):
            (i0, j0), (i1, j1) = path[k], path[k + 1]
            bm.faces.new((grid[0, i0, j0], grid[0, i1, j1], grid[1, i1, j1], grid[1, i0, j0]))
    edge([(i, 0) for i in range(nx + 1)][::-1])
    edge([(i, ny) for i in range(nx + 1)])
    edge([(0, j) for j in range(ny + 1)])
    edge([(nx, j) for j in range(ny + 1)][::-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel > 0:
        boundary = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0) > 0.6]
        bmesh.ops.bevel(bm, geom=boundary, offset=min(bevel, t * 0.45), segments=2, profile=0.5, affect='EDGES')
    R = (Matrix.Rotation(math.radians(rot[2]), 4, 'Z') @ Matrix.Rotation(math.radians(rot[1]), 4, 'Y') @ Matrix.Rotation(math.radians(rot[0]), 4, 'X'))
    bmesh.ops.transform(bm, matrix=Matrix.Translation(V(centre)) @ R, verts=bm.verts)
    return _obj(name, bm, mat, part)


def tube(part, mat, points, radius, segs=10, name='Hose'):
    pts = [V(p) for p in points]
    path = []
    for i in range(len(pts) - 1):
        a = pts[max(0, i - 1)]; b = pts[i]; c = pts[i + 1]; d = pts[min(len(pts) - 1, i + 2)]
        for k in range(8):
            path.append(_catmull(a, b, c, d, k / 8))
    path.append(pts[-1])
    bm = bmesh.new()
    rings = []
    prev = None
    for i, p in enumerate(path):
        t = (path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)]).normalized()
        if prev is None:
            prev = t.orthogonal().normalized()
        side = t.cross(prev).cross(t).normalized()
        prev = side
        up = t.cross(side)
        rings.append([bm.verts.new(p + (side * math.cos(2 * math.pi * k / segs) + up * math.sin(2 * math.pi * k / segs)) * radius) for k in range(segs)])
    for i in range(len(rings) - 1):
        for k in range(segs):
            bm.faces.new((rings[i][k], rings[i][(k + 1) % segs], rings[i + 1][(k + 1) % segs], rings[i + 1][k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _obj(name, bm, mat, part)


def vent(part, centre, size, rot=(0, 0, 0), slats=5, name='Vent'):
    """A recessed louvre: dark frame and angled black slats, facing +Y before rot."""
    w, h, d = size
    block(part, 'Dark', centre, (w, d, h), rot=rot, bevel=0.04, name=name)
    R = Matrix.Rotation(math.radians(rot[2]), 3, 'Z') @ Matrix.Rotation(math.radians(rot[1]), 3, 'Y') @ Matrix.Rotation(math.radians(rot[0]), 3, 'X')
    for k in range(slats):
        z = -h / 2 + h * (k + 0.5) / slats
        block(part, 'Black', V(centre) + R @ Vector((0, d * 0.5, z)), (w * 0.86, 0.06, h / slats * 0.55),
              rot=(rot[0] - 30, rot[1], rot[2]), bevel=0.0, name='Slat')


def lamp(part, centre, size, rot=(0, 0, 0)):
    block(part, 'Dark', centre, (size * 1.4, size * 0.7, size), rot=rot, bevel=0.04, name='LampBody')
    R = Matrix.Rotation(math.radians(rot[2]), 3, 'Z') @ Matrix.Rotation(math.radians(rot[1]), 3, 'Y') @ Matrix.Rotation(math.radians(rot[0]), 3, 'X')
    block(part, 'Glow', V(centre) + R @ Vector((0, size * 0.36, 0)), (size * 1.1, 0.05, size * 0.65), rot=rot, bevel=0.0, name='Lens')


def mirror_x(fn):
    """Call fn(s) for s = -1 (left) and +1 (right)."""
    for s in (-1, 1):
        fn(s)


# ---------------------------------------------------------------------------
# Export: one binary file per robot, parts in their game bone frames
# ---------------------------------------------------------------------------

def to_unity(p):
    """Blender (x, y fwd, z up) -> Unity (x, y up, z fwd)."""
    return Vector((p[0], p[2], p[1]))


def frame_identity(origin_unity):
    return (Vector(origin_unity), Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))


def frame_look(origin_unity, fwd_unity, up_unity):
    """Same basis as Unity's Quaternion.LookRotation(fwd, up)."""
    z = Vector(fwd_unity).normalized()
    x = Vector(up_unity).cross(z).normalized()
    y = z.cross(x)
    return (Vector(origin_unity), x, y, z)


def _split_normals(verts, faces, angle=38.0):
    """Per-corner normals averaged over neighbouring faces within the smoothing angle."""
    cos_t = math.cos(math.radians(angle))
    fn = []
    for f in faces:
        a, b, c = verts[f[0]], verts[f[1]], verts[f[2]]
        n = (b - a).cross(c - a)
        fn.append(n)
    adj = {}
    for fi, f in enumerate(faces):
        for v in f:
            adj.setdefault(v, []).append(fi)
    out_v, out_n, remap, tris = [], [], {}, []
    for fi, f in enumerate(faces):
        nf = fn[fi].normalized() if fn[fi].length > 1e-12 else Vector((0, 1, 0))
        idx = []
        for v in f:
            acc = Vector()
            for gi in adj[v]:
                g = fn[gi]
                if g.length > 1e-12 and g.normalized().dot(nf) >= cos_t:
                    acc += g
            n = acc.normalized() if acc.length > 1e-12 else nf
            key = (v, round(n.x, 2), round(n.y, 2), round(n.z, 2))
            if key not in remap:
                remap[key] = len(out_v)
                out_v.append(verts[v]); out_n.append(n)
            idx.append(remap[key])
        tris.append(idx)
    return out_v, out_n, tris


def export(path, frames, anchors=None, meta=None):
    """
    frames: part name -> (origin, x, y, z) in Unity coordinates (from frame_identity / frame_look).
    anchors: name -> {'p': [x,y,z], 'f': [x,y,z] (optional forward)} in Unity root space.
    Writes RBM2: parts with submeshes per material, plus a JSON block of anchors/meta.
    """
    deps = bpy.context.evaluated_depsgraph_get()
    out = bytearray(b'RBM3')
    names = [n for n in PARTS if n in frames]
    out += struct.pack('<H', len(names))
    stats = {}
    for name in names:
        o, X, Y, Z = frames[name]
        per_mat = {}
        for ob in PARTS[name]:
            ev = ob.evaluated_get(deps)
            me = ev.to_mesh()
            me.calc_loop_triangles()
            mw = ob.matrix_world
            vs = [to_unity(mw @ v.co) for v in me.vertices]
            vs = [Vector(((p - o).dot(X), (p - o).dot(Y), (p - o).dot(Z))) for p in vs]
            # Unity is left-handed: after the axis swap Blender's CCW triangles are already CW.
            # Faces may carry their own material slot (panel grooves, painted stripes).
            by_mat = {}
            for t in me.loop_triangles:
                mi = me.polygons[t.polygon_index].material_index
                mat = me.materials[mi].name if mi < len(me.materials) and me.materials[mi] else (me.materials[0].name if me.materials else 'Primary')
                by_mat.setdefault(mat, []).append(tuple(t.vertices))
            for mat, fl in by_mat.items():
                used = sorted({i for f in fl for i in f})
                rm = {v: k for k, v in enumerate(used)}
                lst = per_mat.setdefault(mat, ([], []))
                base = len(lst[0])
                lst[0].extend(vs[i] for i in used)
                lst[1].extend([(rm[a] + base, rm[b] + base, rm[c] + base) for a, b, c in fl])
            ev.to_mesh_clear()
        # One vertex buffer per part, one submesh per material
        allv, alln, subs = [], [], []
        for mat, (vs, fs) in per_mat.items():
            v2, n2, t2 = _split_normals(vs, fs)
            base = len(allv)
            allv += v2; alln += n2
            subs.append((MATS.index(mat) if mat in MATS else 0, [i + base for t in t2 for i in t]))
        nb = name.encode()
        out += struct.pack('<B', len(nb)) + nb
        out += struct.pack('<I', len(allv))
        # Positions quantised to 16 bits inside the part's bounds (sub-millimetre at robot scale)
        lo = [min(v[i] for v in allv) for i in range(3)] if allv else [0, 0, 0]
        hi = [max(v[i] for v in allv) for i in range(3)] if allv else [0, 0, 0]
        sz = [max(hi[i] - lo[i], 1e-4) for i in range(3)]
        out += struct.pack('<6f', *lo, *sz)
        for v in allv:
            out += struct.pack('<3H', *(int(round((v[i] - lo[i]) / sz[i] * 65535)) for i in range(3)))
        for n in alln:
            out += struct.pack('<3b', *(max(-127, min(127, int(round(c * 127)))) for c in n))
        wide = len(allv) > 65535
        out += struct.pack('<BB', len(subs), 1 if wide else 0)
        for m, idx in subs:
            out += struct.pack('<BI', m, len(idx))
            out += struct.pack('<%d%s' % (len(idx), 'I' if wide else 'H'), *idx)
        stats[name] = (len(allv), sum(len(i) for _, i in subs) // 3)
    js = json.dumps(meta if anchors is None else {'anchors': anchors, 'meta': meta or {}}).encode()
    out += struct.pack('<I', len(js)) + js
    with open(path, 'wb') as f:
        f.write(out)
    return stats


def finish(path, frames, parents, anchors, rams, extra):
    """Export with a JSON block Unity's JsonUtility can read (lists, not dictionaries)."""
    info = dict(extra)
    info['frames'] = [{'name': n, 'parent': parents.get(n, 'Root'), 'o': list(f[0]), 'x': list(f[1]), 'y': list(f[2]), 'z': list(f[3])}
                      for n, f in frames.items()]
    info['anchors'] = [{'name': n, 'parent': a.get('parent', 'Root'), 'p': list(a['p']), 'f': list(a.get('f', (0, 0, 1)))}
                       for n, a in anchors.items()]
    info['rams'] = rams
    stats = export(path, frames, None, info)
    tv = sum(v for v, t in stats.values()); tt = sum(t for v, t in stats.values())
    print('parts', len(stats), 'verts', tv, 'tris', tt)
    return stats


# ---------------------------------------------------------------------------
# Preview render (Cycles, HDRI lighting)
# ---------------------------------------------------------------------------

def render(path, hdri=None, views=((35, 18), ), target=(0, 0, 7), dist=30, res=(1280, 720), samples=48, lens=50):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Medium High Contrast'
    world = bpy.data.worlds.new('W'); sc.world = world; world.use_nodes = True
    nt = world.node_tree
    bg = nt.nodes.get('Background')
    if hdri:
        env = nt.nodes.new('ShaderNodeTexEnvironment'); env.image = bpy.data.images.load(hdri)
        nt.links.new(env.outputs['Color'], bg.inputs['Color']); bg.inputs['Strength'].default_value = 0.55
    else:
        bg.inputs['Color'].default_value = (0.4, 0.45, 0.5, 1)
    # Ground
    bpy.ops.mesh.primitive_plane_add(size=400, location=(0, 0, 0))
    g = bpy.context.object
    gm = bpy.data.materials.new('Ground'); gm.use_nodes = True
    gb = gm.node_tree.nodes.get('Principled BSDF'); gb.inputs['Base Color'].default_value = (0.09, 0.085, 0.08, 1); gb.inputs['Roughness'].default_value = 0.9
    g.data.materials.append(gm)
    sun = bpy.data.lights.new('Sun', 'SUN'); sun.energy = 4.5; sun.angle = 0.05
    so = bpy.data.objects.new('Sun', sun); sc.collection.objects.link(so); so.rotation_euler = (math.radians(50), 0, math.radians(-35))
    cam = bpy.data.cameras.new('Cam'); cam.lens = lens
    co = bpy.data.objects.new('Cam', cam); sc.collection.objects.link(co); sc.camera = co
    tgt = Vector(target)
    files = []
    for i, (yaw, pitch) in enumerate(views):
        d = Vector((math.sin(math.radians(yaw)) * math.cos(math.radians(pitch)), math.cos(math.radians(yaw)) * math.cos(math.radians(pitch)), math.sin(math.radians(pitch))))
        co.location = tgt + d * dist
        co.rotation_euler = (tgt - co.location).to_track_quat('-Z', 'Y').to_euler()
        f = path.replace('.png', '_%d.png' % i)
        sc.render.filepath = f
        bpy.ops.render.render(write_still=True)
        files.append(f)
    return files
