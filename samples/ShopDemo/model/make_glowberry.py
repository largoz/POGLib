# make_glowberry.py <out dir>: writes GlowBerry.obj + GlowBerry.mtl, the ShopDemo sample's own model.
# A cluster of three glowing berries on a short stem with one leaf, about 8 cm tall, in metres, Y up, the origin
# at the bottom centre (the same convention as the Ember Lantern model POGLib's OBJ loader was built on).
import math, os, sys

out = sys.argv[1] if len(sys.argv) > 1 else '.'
V, VN, F = [], [], []          # positions, normals, faces as (material, [(v, vn)...])

def add_v(p, n):
    V.append(p); VN.append(n)
    return len(V)              # OBJ indices are 1-based

def sphere(cx, cy, cz, r, mat, rings=8, segs=12):
    idx = {}
    for i in range(rings + 1):
        th = math.pi * i / rings
        for j in range(segs):
            ph = 2 * math.pi * j / segs
            n = (math.sin(th) * math.cos(ph), math.cos(th), math.sin(th) * math.sin(ph))
            idx[i, j] = add_v((cx + r * n[0], cy + r * n[1], cz + r * n[2]), n)
    for i in range(rings):
        for j in range(segs):
            a, b = idx[i, j], idx[i, (j + 1) % segs]
            c, d = idx[i + 1, (j + 1) % segs], idx[i + 1, j]
            if i > 0: F.append((mat, [a, b, c]))           # counter-clockwise seen from outside (OBJ, right-handed)
            if i < rings - 1: F.append((mat, [a, c, d]))

def cylinder(x0, y0, z0, x1, y1, z1, r, mat, segs=6):
    # along a straight line; good enough for a stem
    ax = (x1 - x0, y1 - y0, z1 - z0); L = math.sqrt(sum(c * c for c in ax)); ax = tuple(c / L for c in ax)
    up = (0, 0, 1) if abs(ax[1]) > 0.9 else (0, 1, 0)
    u = (ax[1] * up[2] - ax[2] * up[1], ax[2] * up[0] - ax[0] * up[2], ax[0] * up[1] - ax[1] * up[0])
    lu = math.sqrt(sum(c * c for c in u)); u = tuple(c / lu for c in u)
    w = (ax[1] * u[2] - ax[2] * u[1], ax[2] * u[0] - ax[0] * u[2], ax[0] * u[1] - ax[1] * u[0])
    ring0, ring1 = [], []
    for j in range(segs):
        a = 2 * math.pi * j / segs
        n = tuple(math.cos(a) * u[k] + math.sin(a) * w[k] for k in range(3))
        ring0.append(add_v((x0 + r * n[0], y0 + r * n[1], z0 + r * n[2]), n))
        ring1.append(add_v((x1 + r * n[0], y1 + r * n[1], z1 + r * n[2]), n))
    for j in range(segs):
        k = (j + 1) % segs
        F.append((mat, [ring0[j], ring0[k], ring1[k]]))
        F.append((mat, [ring0[j], ring1[k], ring1[j]]))

def leaf(mat):
    # a flat diamond-shaped leaf near the top of the stem, both sides
    pts = [(0.0, 0.070, 0.0), (0.018, 0.078, 0.006), (0.040, 0.083, 0.0), (0.018, 0.074, -0.008)]
    n = (0.0, 1.0, 0.0)
    top = [add_v(p, n) for p in pts]
    bot = [add_v(p, (0.0, -1.0, 0.0)) for p in pts]
    F.append((mat, [top[0], top[1], top[2]])); F.append((mat, [top[0], top[2], top[3]]))
    F.append((mat, [bot[0], bot[2], bot[1]])); F.append((mat, [bot[0], bot[3], bot[2]]))

r = 0.019
sphere(-0.014, r, 0.008, r, 'Berry_Glow')
sphere(0.015, r, 0.006, r, 'Berry_Glow')
sphere(0.0, r * 2.35, -0.004, r * 0.95, 'Berry_Glow')
cylinder(0.0, 0.055, -0.004, 0.003, 0.080, 0.0, 0.0028, 'Stem')
leaf('Leaf')

os.makedirs(out, exist_ok=True)
with open(os.path.join(out, 'GlowBerry.obj'), 'w') as f:
    f.write('# GlowBerry: ShopDemo sample model (POGLib). Metres, Y up, origin at the bottom centre.\n')
    f.write('mtllib GlowBerry.mtl\no GlowBerry\n')
    for p in V: f.write('v %.5f %.5f %.5f\n' % p)
    for n in VN: f.write('vn %.5f %.5f %.5f\n' % n)
    cur = None
    for mat, idx in F:
        if mat != cur: f.write('usemtl %s\n' % mat); cur = mat
        f.write('f ' + ' '.join('%d//%d' % (i, i) for i in idx) + '\n')
with open(os.path.join(out, 'GlowBerry.mtl'), 'w') as f:
    f.write('# GlowBerry materials\n')
    f.write('newmtl Berry_Glow\nKd 0.30 0.95 0.45\nKe 0.10 0.55 0.22\nNs 300\nd 1.0\n\n')
    f.write('newmtl Stem\nKd 0.32 0.20 0.10\nKe 0 0 0\nNs 50\nd 1.0\n\n')
    f.write('newmtl Leaf\nKd 0.20 0.55 0.18\nKe 0 0 0\nNs 80\nd 1.0\n')
ys = [p[1] for p in V]; xs = [p[0] for p in V]
print(f'{len(V)} vertices, {len(F)} triangles; x {min(xs):.3f}..{max(xs):.3f} m, y {min(ys):.3f}..{max(ys):.3f} m')
