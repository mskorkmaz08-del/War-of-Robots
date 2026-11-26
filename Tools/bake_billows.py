# Bakes 4x4 atlases of volumetric billows (smoke: lit from above; fire: self-glowing).
import sys, numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter, zoom
out = sys.argv[1]
R = 96            # volume resolution per sprite
T = 256           # sprite size in the atlas
rng = np.random.default_rng(7)
ax = np.linspace(-1, 1, R, dtype=np.float32)
Z, Y, X = np.meshgrid(ax, ax, ax, indexing='ij')   # Z = depth (towards camera), Y = up (row 0 = top)

def noise3(scale, seed):
    r = np.random.default_rng(seed)
    n = r.standard_normal((R, R, R)).astype(np.float32)
    n = gaussian_filter(n, scale)
    return n / (n.std() + 1e-6)

def billow(seed):
    r = np.random.default_rng(seed)
    d = np.full((R, R, R), -1.0, np.float32)
    nb = r.integers(9, 16)
    for _ in range(nb):
        c = r.normal(0, 0.2, 3).clip(-0.4, 0.4)
        rad = r.uniform(0.2, 0.42)
        dist = np.sqrt((X - c[0])**2 + (Y - c[1])**2 + (Z - c[2])**2)
        d = np.maximum(d, 1 - dist / rad)
    # Cauliflower edges: displace the field with two octaves of noise.
    d = d + 0.16 * noise3(4, seed + 1) + 0.08 * noise3(1.6, seed + 2)
    d = np.clip((d - 0.05) * 4.0, 0, None)
    # Keep inside the sprite: round fade near the borders.
    edge = np.clip((0.97 - np.sqrt(X**2 + Y**2 + Z**2)) * 5, 0, 1)
    return d * edge

def render(d, fire):
    k = 9.0 / R
    # Light from above and a bit in front: transmittance by cumulative density down the Y axis
    # (rows go top to bottom) mixed with along Z.
    tl_y = np.exp(-np.cumsum(d, axis=1) * k * 1.4)
    tl_z = np.exp(-np.cumsum(d[::-1], axis=0)[::-1] * k * 0.8)
    light = 0.75 * tl_y + 0.25 * tl_z
    # Front-to-back compositing along Z (index R-1 is nearest the camera).
    dd = d[::-1]; ll = light[::-1]
    alpha_v = 1 - np.exp(-dd * k * 2.2)
    trans = np.cumprod(np.concatenate([np.ones((1, R, R), np.float32), 1 - alpha_v[:-1]]), axis=0)
    w = trans * alpha_v
    A = w.sum(0)
    if fire:
        # Hot where the cloud is thick, cooler and darker at the wispy edges, with
        # the self-shadowed turbulence kept for detail.
        thick = dd.sum(0) * k
        thick = thick / (thick.max() + 1e-4)
        shade = (w * (0.22 + 0.9 * ll)).sum(0) / (A + 1e-4)
        C = np.clip(thick ** 0.6 * (0.45 + 0.55 * shade) * 1.25, 0, 1)
    else:
        C = (w * (0.22 + 0.9 * ll)).sum(0) / (A + 1e-4)
    return np.clip(C, 0, 1), np.clip(A, 0, 1)

def atlas(fire, seed0):
    img = np.zeros((T * 4, T * 4, 4), np.float32)
    for i in range(16):
        d = billow(seed0 + i * 13)
        C, A = render(d, fire)
        C = zoom(C, T / R, order=1); A = zoom(A, T / R, order=1)
        y, x = (i // 4) * T, (i % 4) * T
        img[y:y + T, x:x + T, 0] = C; img[y:y + T, x:x + T, 1] = C; img[y:y + T, x:x + T, 2] = C
        img[y:y + T, x:x + T, 3] = A
        print('sprite', i, flush=True)
    # Unity's texture V runs bottom-up but the sheet animation counts rows from the top, so keep top-down.
    return Image.fromarray((img * 255).astype(np.uint8), 'RGBA')

atlas(False, 100).save(out + '/SmokeBillows.png')
atlas(True, 500).save(out + '/FireBillows.png')
