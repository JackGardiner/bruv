"""Reproduces every measured number behind the gyroid grain design."""

import json
import os
import sys

import numpy as np

TWOPI = 2.0 * np.pi
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)


def load_grain():
    with open(os.path.join(ROOT, "config", "all.json")) as f:
        cfg = json.load(f)["grain"]
    amend = os.path.join(ROOT, "config", "ammendments.json")
    if os.path.exists(amend):
        with open(amend) as f:
            cfg.update(json.load(f).get("grain", {}))
    return cfg


G = load_grain()
L_CELL, LZ_CELL, TH_GYR = G["L_cell"], G["Lz_cell"], G["th_gyr"]
X0, Y0, Z0 = G["x0_gyr"], G["y0_gyr"], G["z0_gyr"]
L_PUCK, TH_SKIN = G["L_puck"], G["th_skin"]
R_GRAIN, R_PORT = 0.5 * G["D_grain"], 0.5 * G["D_port"]
R_SKIN = R_GRAIN - TH_SKIN
EW = 0.2  # extrusion width, mm


def level_set(x, y, z, L=None, Lz=None, x0=None, y0=None, z0=None):
    """Gyroid f and |grad f| at world points, in the part's frame."""
    L = L_CELL if L is None else L
    Lz = LZ_CELL if Lz is None else Lz
    x0 = X0 if x0 is None else x0
    y0 = Y0 if y0 is None else y0
    z0 = Z0 if z0 is None else z0
    wx = wy = TWOPI / L
    wz = TWOPI / Lz
    qx, qy, qz = (x - x0) * wx, (y - y0) * wy, (z - z0) * wz
    sx, sy, sz = np.sin(qx), np.sin(qy), np.sin(qz)
    cx, cy, cz = np.cos(qx), np.cos(qy), np.cos(qz)
    f = sx * cy + sy * cz + sz * cx
    gx = wx * (cx * cy - sz * sx)
    gy = wy * (cy * cz - sx * sy)
    gz = wz * (cz * cx - sy * sz)
    return f, np.sqrt(gx * gx + gy * gy + gz * gz)


def distance(x, y, z, **kw):
    f, g = level_set(x, y, z, **kw)
    return f / np.maximum(g, 1e-9)


def rule(title):
    print("=" * 74)
    print(title)
    print("=" * 74)


# --------------------------------------------------------------- accounting
def probe_accounting():
    rule("ACCOUNTING -- fill fraction on both bases")
    env = np.pi * (R_GRAIN**2 - R_PORT**2) * L_PUCK
    skin = np.pi * (R_GRAIN**2 - R_SKIN**2) * L_PUCK
    gyr_env = np.pi * (R_SKIN**2 - R_PORT**2) * L_PUCK
    chim = G["no_chim"] * np.pi * (0.5 * G["D_chim"]) ** 2 * L_PUCK

    # newest by mtime, not filename (filenames are hashes)
    rec = None
    cfgdir = os.path.join(ROOT, "config", "grain")
    if os.path.isdir(cfgdir):
        names = [n for n in os.listdir(cfgdir)
                  if n.startswith("grain-std-") and n.endswith(".json")]
        if names:
            names.sort(key=lambda n: os.path.getmtime(os.path.join(cfgdir, n)))
            name = names[-1]
            with open(os.path.join(cfgdir, name)) as f:
                rec = json.load(f)
            print(f"  using emitted record {name} (newest by mtime)")
    if rec is None:
        print("  no grain-std record in config/grain -- skipping")
        return

    solid = rec["fin_volume_mL"] * 1e3
    sheet = solid - skin
    print(f"  annular envelope        {env/1e3:8.2f} mL")
    print(f"  OD skin                 {skin/1e3:8.2f} mL "
          f"({skin/env:6.2%} of envelope)")
    print(f"  gyroid-region envelope  {gyr_env/1e3:8.2f} mL")
    print(f"  chimney bores           {chim/1e3:8.2f} mL")
    print(f"  sheet solid             {sheet/1e3:8.2f} mL")
    print()
    print(f"  whole-part fill (ODY basis)        {solid/env:7.2%}")
    print(f"  gyroid-only, chimneys in envelope  {sheet/gyr_env:7.2%}")
    print(f"  gyroid-only, chimneys out          {sheet/(gyr_env-chim):7.2%}")
    print()
    w = R_SKIN - R_PORT
    circ = TWOPI * 0.5 * (R_SKIN + R_PORT)
    print(f"  cells across: radial {w/L_CELL:.2f}, hoop {circ/L_CELL:.1f}, "
          f"axial {L_PUCK/LZ_CELL:.1f}")
    print(f"  th_gyr {TH_GYR} mm = {TH_GYR/EW:.2f} extrusion widths")
    print(f"  interface area ~ 3.09*V/L = {3.09*gyr_env/L_CELL/100:.0f} cm2/puck")
    print()


# ------------------------------------------------------------------ density
def probe_density():
    rule("DENSITY -- phi vs t/L, and the error in the 3.09 coefficient (2.3)")
    n = 220
    u = (np.arange(n) + 0.5) / n
    print(f"  {'Lz/L':>6} {'t=1.00':>9} {'t=1.86':>9} {'t=2.50':>9}   "
          f"(values are phi/(t/L))")
    for k in (1.0, LZ_CELL / L_CELL, 1.5, 2.0):
        L = L_CELL
        Lz = L * k
        X, Y, Z = np.meshgrid(u * L, u * L, u * Lz, indexing="ij")
        d = np.abs(distance(X, Y, Z, L=L, Lz=Lz, x0=0, y0=0, z0=0))
        row = []
        for t in (1.0, TH_GYR, 2.5):
            phi = float((d < 0.5 * t).mean())
            row.append(phi / (t / L))
        mark = "  <- current" if abs(k - LZ_CELL / L_CELL) < 1e-9 else ""
        print(f"  {k:6.3f} {row[0]:9.3f} {row[1]:9.3f} {row[2]:9.3f}{mark}")
    print()
    print("  Literature 3.09 is the isotropic thin-sheet limit only.")
    print()


# ----------------------------------------------------------------- symmetry
def probe_symmetry():
    rule("SYMMETRY -- 4_1 screw about (L_cell/4, 0)")
    rng = np.random.default_rng(0)
    n = 200_000
    ax = L_CELL / 4.0
    p = rng.uniform(-2 * L_CELL, 2 * L_CELL, size=(n, 3))
    p[:, 2] = rng.uniform(-2 * LZ_CELL, 2 * LZ_CELL, size=n)
    dx, dy = p[:, 0] - ax, p[:, 1]
    qx, qy, qz = ax - dy, dx, p[:, 2] + LZ_CELL / 4.0
    f0, _ = level_set(p[:, 0], p[:, 1], p[:, 2], x0=0, y0=0, z0=0)
    f1, _ = level_set(qx, qy, qz, x0=0, y0=0, z0=0)
    r = f1 - f0
    print(f"  max|residual| {np.abs(r).max():.3e}   rms {np.sqrt((r**2).mean()):.3e}")
    print(f"  part axis sits on the screw axis when x0_gyr = -L_cell/4 = "
          f"{-L_CELL/4:.2f}  (config: {X0})")
    print()


# --------------------------------------------------------------------- slit
def slot_clearance(theta0, width, r, nz=4000, nw=25):
    z = np.linspace(0.0, L_PUCK, nz)
    half = 0.5 * width / r
    worst = np.full(nz, np.inf)
    for o in np.linspace(-half, half, nw):
        th = theta0 + o
        d = np.abs(distance(r * np.cos(th), r * np.sin(th), z))
        worst = np.minimum(worst, d - 0.5 * TH_GYR)
    return worst


def probe_slit():
    rule("SLIT -- straight constant-width axial slot through the skin")
    print("  1. four slits at 90 deg -- equivalent?")
    for k in range(4):
        c = slot_clearance(k * np.pi / 2, 2.0, R_SKIN)
        print(f"     {k*90:3d} deg: min {c.min():+8.4f}  mean {c.mean():+8.4f}"
              f"  blocked {100*(c<0).mean():5.2f}%")
    print()

    print("  2. how wide can it be (at r = R_skin)?")
    A_skin = TWOPI * R_GRAIN * L_PUCK
    print(f"     {'width':>6} {'min clr':>9} {'blocked':>9} {'open area':>10}")
    for w in (1.0, 2.0, 2.5, 3.0, 4.0):
        c = slot_clearance(0.0, w, R_SKIN)
        print(f"     {w:6.1f} {c.min():+9.3f} {100*(c<0).mean():8.2f}% "
              f"{4*w*L_PUCK/A_skin:9.2%}")
    print()

    print("  3. radial dependence -- the cut must stop at the skin")
    for rr in (R_SKIN - 2.0, R_SKIN - 1.0, R_SKIN, R_GRAIN):
        c = slot_clearance(0.0, 2.0, rr)
        print(f"     r={rr:5.2f}: min {c.min():+7.3f}  "
              f"blocked {100*(c<0).mean():5.2f}%")
    print()

    print("  4. how many slits keep exact equivalence?")
    for n in (2, 3, 4, 6, 8):
        mins = [slot_clearance(k * TWOPI / n, 2.0, R_SKIN).min()
                for k in range(n)]
        spread = max(mins) - min(mins)
        print(f"     n={n}: spread {spread:7.4f} mm  -> "
              f"{'equivalent' if spread < 1e-3 else 'NOT equivalent'}")
    print()


# ----------------------------------------------------------------- chimneys
def probe_chimneys():
    rule("CHIMNEYS -- bore openness vs count")
    r_c, D = G["r_chim"], G["D_chim"]
    nz, nr, nth = 800, 13, 25
    z = np.linspace(0, L_PUCK, nz)
    rr = np.linspace(0, 0.5 * D, nr)
    tt = np.linspace(0, TWOPI, nth, endpoint=False)
    R, T, Z = np.meshgrid(rr, tt, z, indexing="ij")

    def bore(theta0):
        cx, cy = r_c * np.cos(theta0), r_c * np.sin(theta0)
        d = np.abs(distance(cx + R * np.cos(T), cy + R * np.sin(T), Z))
        w = np.broadcast_to(R, d.shape)
        return (((d > 0.5 * TH_GYR) * w).sum(axis=(0, 1))
                / w.sum(axis=(0, 1))).min()

    print(f"  r_chim = {r_c}, D_chim = {D}")
    for n in (2, 3, 4, 6):
        mins = [bore(k * TWOPI / n) for k in range(n)]
        mark = "  <- current" if n == G["no_chim"] else ""
        print(f"    n={n}: " + " ".join(f"{m:6.3f}" for m in mins).ljust(46)
              + f" spread {max(mins)-min(mins):6.4f}{mark}")
    print()
    print("  spread ~0 means every bore meets an identical lattice section.")
    print()


PROBES = {
    "accounting": probe_accounting,
    "density": probe_density,
    "symmetry": probe_symmetry,
    "slit": probe_slit,
    "chimneys": probe_chimneys,
}

if __name__ == "__main__":
    wanted = sys.argv[1:] or list(PROBES)
    for name in wanted:
        if name not in PROBES:
            print(f"unknown probe '{name}'; have: {', '.join(PROBES)}")
            sys.exit(1)
        PROBES[name]()
