"""Acceptance checks for a gyroid grain scaffold STL."""

import argparse
import struct
import sys

import numpy as np


# ----------------------------------------------------------------- reading

def read_stl(path):
    """Return (T, 3, 3) float64 vertex array. Handles binary and ascii."""
    with open(path, "rb") as f:
        head = f.read(84)
        if len(head) < 84:
            raise ValueError(f"{path}: too short to be an STL")
        n = struct.unpack("<I", head[80:84])[0]
        body = f.read()

    if len(body) == n * 50:
        raw = np.frombuffer(body, dtype=np.uint8).reshape(n, 50)
        # bytes 12..48 are the three vertices, 3 floats each
        v = raw[:, 12:48].copy().view("<f4").reshape(n, 3, 3)
        return v.astype(np.float64)

    # ascii fallback
    with open(path, "r", errors="ignore") as f:
        vals = [
            [float(x) for x in ln.split()[1:4]]
            for ln in f
            if ln.strip().startswith("vertex")
        ]
    if not vals or len(vals) % 3:
        raise ValueError(f"{path}: not a readable binary or ascii STL")
    return np.asarray(vals, dtype=np.float64).reshape(-1, 3, 3)


# ------------------------------------------------------------- topology

def weld(tris, decimals=4):
    """Weld vertices on a grid and return (verts, faces)."""
    flat = tris.reshape(-1, 3)
    keys = np.round(flat, decimals)
    _, inv = np.unique(keys, axis=0, return_inverse=True)
    return flat, inv.reshape(-1, 3)


def drop_degenerate(faces):
    """Split off zero-area faces (a repeated vertex index)."""
    deg = ((faces[:, 0] == faces[:, 1])
           | (faces[:, 1] == faces[:, 2])
           | (faces[:, 2] == faces[:, 0]))
    return faces[~deg], int(deg.sum())


def check_manifold(faces):
    """Every undirected edge used exactly twice, each direction exactly once."""
    faces, degenerate = drop_degenerate(faces)
    e = np.concatenate(
        [faces[:, [0, 1]], faces[:, [1, 2]], faces[:, [2, 0]]], axis=0
    )

    und = np.sort(e, axis=1)
    _, counts = np.unique(und, axis=0, return_counts=True)
    non_two = int((counts != 2).sum())

    _, dcounts = np.unique(e, axis=0, return_counts=True)
    flipped = int((dcounts != 1).sum())

    return {
        "zero_area_faces": degenerate,
        "edges_not_shared_by_2": non_two,
        "inconsistently_wound_edges": flipped,
        "watertight": non_two == 0,
        "manifold": non_two == 0 and flipped == 0,
    }


# -------------------------------------------------------------- geometry

def volume_mm3(tris):
    """Signed volume by the divergence theorem."""
    a, b, c = tris[:, 0], tris[:, 1], tris[:, 2]
    return float(np.einsum("ij,ij->i", a, np.cross(b, c)).sum() / 6.0)


def normals_areas(tris):
    a, b, c = tris[:, 0], tris[:, 1], tris[:, 2]
    n = np.cross(b - a, c - a)
    m = np.linalg.norm(n, axis=1)
    ok = m > 1e-12
    return n[ok] / m[ok, None], 0.5 * m[ok]


def overhang(tris):
    """Area-weighted tilt-from-horizontal of the DOWNWARD-facing surface."""
    n, area = normals_areas(tris)
    down = n[:, 2] < 0
    n, area = n[down], area[down]
    beta = np.degrees(np.arccos(np.clip(np.abs(n[:, 2]), 0, 1)))
    tot = area.sum()
    return {
        "down_area_mm2": float(tot),
        "frac_below_45deg": float(area[beta < 45].sum() / tot),
        "frac_below_30deg": float(area[beta < 30].sum() / tot),
        "frac_below_20deg": float(area[beta < 20].sum() / tot),
        "area_below_45deg_mm2": float(area[beta < 45].sum()),
        "area_below_20deg_mm2": float(area[beta < 20].sum()),
    }


def ray_crossings(tris, x0, y0, z_floor=None):
    """Count triangles a vertical ray at (x0,y0) passes through."""
    a, b, c = tris[:, 0], tris[:, 1], tris[:, 2]
    # barycentric containment in the xy projection
    v0 = b[:, :2] - a[:, :2]
    v1 = c[:, :2] - a[:, :2]
    v2 = np.array([x0, y0]) - a[:, :2]
    den = v0[:, 0] * v1[:, 1] - v1[:, 0] * v0[:, 1]
    ok = np.abs(den) > 1e-12
    u = np.full(len(tris), -1.0)
    v = np.full(len(tris), -1.0)
    u[ok] = (v2[ok, 0] * v1[ok, 1] - v1[ok, 0] * v2[ok, 1]) / den[ok]
    v[ok] = (v0[ok, 0] * v2[ok, 1] - v2[ok, 0] * v0[ok, 1]) / den[ok]
    hit = ok & (u >= 0) & (v >= 0) & (u + v <= 1)
    if z_floor is None:
        return int(hit.sum())
    w = 1.0 - u - v
    z = w * a[:, 2] + u * b[:, 2] + v * c[:, 2]
    return int(np.sum(hit & (z > z_floor + 1e-6)))


def chimney_open(tris, r, theta0, n_chim, d_chim, samples=64, seed=0,
                 z_floor=None):
    """Fraction of each chimney bore that is clear from end to end."""
    rng = np.random.default_rng(seed)
    # disc sample of the bore, inset so we test the bore not its wall
    rad = (d_chim / 2.0) * 0.85 * np.sqrt(rng.random(samples))
    ang = rng.random(samples) * 2 * np.pi
    out = []
    for i in range(n_chim):
        th = np.radians(theta0) + i * 2 * np.pi / n_chim
        cx, cy = r * np.cos(th), r * np.sin(th)
        clear = sum(
            ray_crossings(tris, cx + rr * np.cos(aa), cy + rr * np.sin(aa),
                          z_floor) == 0
            for rr, aa in zip(rad, ang)
        )
        out.append(clear / samples)
    return out


def check_slits(tris, n_expected, width, r_grain, tol=0.15):
    """Count the angular gaps in the OD surface and measure their width."""
    cen = tris.mean(axis=1)
    r = np.hypot(cen[:, 0], cen[:, 1])
    nrm = np.cross(tris[:, 1] - tris[:, 0], tris[:, 2] - tris[:, 0])
    nrm = nrm / np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-12)
    outward = (nrm[:, 0] * cen[:, 0] + nrm[:, 1] * cen[:, 1]) / np.maximum(r, 1e-9)

    z = cen[:, 2]
    zlo, zhi = z.min(), z.max()
    mid_lo = zlo + 0.25 * (zhi - zlo)
    mid_hi = zlo + 0.75 * (zhi - zlo)

    on_od = ((np.abs(r - r_grain) < 0.5) & (outward > 0.8)
             & (z > mid_lo) & (z < mid_hi))
    if not on_od.any():
        return False, "no outward-facing OD surface found in the mid-height band"
    th = np.degrees(np.arctan2(cen[on_od, 1], cen[on_od, 0])) % 360.0
    hist, edges = np.histogram(th, bins=720, range=(0, 360))
    empty = hist == 0
    # count runs of empty bins
    runs, in_run, this = [], False, 0
    for e in np.concatenate([empty, [False]]):
        if e:
            in_run, this = True, this + 1
        elif in_run:
            runs.append(this)
            in_run, this = False, 0
    # a slit centred on 0 deg is split across the wrap, appearing as one run at
    # each end of the histogram -- rejoin them before counting
    if len(runs) > 1 and empty[0] and empty[-1]:
        runs[0] += runs.pop()
    n_found = len(runs)
    if n_found != int(n_expected):
        return False, f"found {n_found} slits, expected {int(n_expected)}"
    got_w = np.mean(runs) * (360.0 / 720) * np.radians(1.0) * r_grain
    if abs(got_w / width - 1) > tol:
        return False, f"slit width {got_w:.2f} mm, expected {width:.2f} mm"
    return True, f"{n_found} slits, mean width {got_w:.2f} mm"


# ------------------------------------------------------------------ main

def main():
    p = argparse.ArgumentParser(description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("stl")
    p.add_argument("--baseline", help="STL to compare overhangs against")
    p.add_argument("--od", type=float, default=95.0)
    p.add_argument("--id", type=float, default=59.0, dest="idia")
    p.add_argument("--length", type=float, default=96.25)
    p.add_argument("--env-tol", type=float, default=0.5,
                   help="envelope tolerance, mm")
    p.add_argument("--rho", type=float, default=1.04, help="g/cm3")
    p.add_argument("--mass-target", type=float, default=None, help="grams")
    p.add_argument("--mass-band", type=float, default=0.03)
    p.add_argument("--fill-lo", type=float, default=0.25)
    p.add_argument("--fill-hi", type=float, default=0.27)
    p.add_argument("--chim-n", type=int, default=3)
    p.add_argument("--chim-r", type=float, default=38.0)
    p.add_argument("--chim-d", type=float, default=8.0)
    p.add_argument("--chim-theta0", type=float, default=0.0, help="degrees")
    p.add_argument("--shelf", type=float, default=0.0,
                   help="base shelf thickness; chimneys terminate on it")
    p.add_argument("--shelf-tol", type=float, default=0.5,
                   help="ignore crossings within this of the shelf top face, "
                        "which is the shelf itself rather than a blockage")
    p.add_argument("--slits", nargs=2, type=float, metavar=("N", "W"),
                   default=None,
                   help="expect N slits of width W mm through the OD skin")
    args = p.parse_args()

    tris = read_stl(args.stl)
    _, faces = weld(tris)
    fails = []

    def check(ok, label, detail=""):
        fails.append(label) if not ok else None
        print(f"  [{'PASS' if ok else 'FAIL'}] {label}{'  ' + detail if detail else ''}")

    print(f"\n{args.stl}\n  triangles: {len(tris)}")

    print("\ntopology")
    topo = check_manifold(faces)
    if topo["zero_area_faces"]:
        print(f"  note: {topo['zero_area_faces']} zero-area faces "
              f"({100 * topo['zero_area_faces'] / len(faces):.4f}%), ignored "
              "- PicoGK mesher artefact, harmless")
    check(topo["watertight"], "watertight",
          f"({topo['edges_not_shared_by_2']} bad edges)")
    check(topo["manifold"], "manifold + consistently wound",
          f"({topo['inconsistently_wound_edges']} bad directed edges)")

    print("\nenvelope")
    lo, hi = tris.reshape(-1, 3).min(0), tris.reshape(-1, 3).max(0)
    size = hi - lo
    rad = np.hypot(tris.reshape(-1, 3)[:, 0], tris.reshape(-1, 3)[:, 1])
    od_meas, id_meas = 2 * rad.max(), 2 * rad.min()
    print(f"  bounds  {size[0]:.2f} x {size[1]:.2f} x {size[2]:.2f} mm")
    check(abs(od_meas - args.od) <= args.env_tol, "OD",
          f"{od_meas:.2f} vs {args.od} mm")
    check(abs(id_meas - args.idia) <= args.env_tol, "ID",
          f"{id_meas:.2f} vs {args.idia} mm")
    check(abs(size[2] - args.length) <= args.env_tol, "length",
          f"{size[2]:.2f} vs {args.length} mm")

    print("\nvolume, fill, mass")
    vol = volume_mm3(tris)
    env = args.length * np.pi * ((args.od / 2) ** 2 - (args.idia / 2) ** 2)
    fill = vol / env
    mass = vol * 1e-3 * args.rho
    print(f"  closed volume   {vol * 1e-3:.2f} mL")
    print(f"  envelope        {env * 1e-3:.2f} mL")
    print(f"  predicted mass  {mass:.1f} g  (rho {args.rho} g/cm3)")
    check(args.fill_lo <= fill <= args.fill_hi, "fill fraction",
          f"{fill * 100:.2f}% (want {args.fill_lo * 100:.0f}-{args.fill_hi * 100:.0f}%)")
    if args.mass_target:
        err = abs(mass - args.mass_target) / args.mass_target
        check(err <= args.mass_band, "mass band",
              f"{mass:.1f} g vs {args.mass_target:.1f} g ({err * 100:.1f}%)")

    if args.chim_n:
        print("\nchimneys")
        # a ray that misses the part reads clear, so check it's in the annulus
        inside = (args.idia / 2 < args.chim_r - args.chim_d / 2
                  and args.chim_r + args.chim_d / 2 < args.od / 2)
        check(inside, "chimney bores lie within the annulus",
              f"r={args.chim_r} +/- {args.chim_d / 2}")
        if inside:
            # shelf top sits at z=shelf, give it some slack
            floor = lo[2] + args.shelf + args.shelf_tol if args.shelf else None
            span = "top face to shelf" if args.shelf else "end to end"
            opens = chimney_open(tris, args.chim_r, args.chim_theta0,
                                 args.chim_n, args.chim_d, z_floor=floor)
            for i, o in enumerate(opens):
                check(o > 0.98, f"chimney {i} continuous {span}",
                      f"{o * 100:.0f}% of bore clear")

    if args.slits:
        print("\nslits")
        n_exp, width = args.slits
        ok, detail = check_slits(tris, n_exp, width, args.od / 2.0)
        check(ok, f"{int(n_exp)} slits of width {width:.2f} mm through the OD",
              detail)

    print("\noverhang (downward-facing surface, area-weighted)")
    oh = overhang(tris)
    print(f"  downward area   {oh['down_area_mm2'] / 100:.1f} cm2")
    for key, lbl in (("frac_below_45deg", "<45"), ("frac_below_30deg", "<30"),
                     ("frac_below_20deg", "<20")):
        print(f"  {lbl} deg from horizontal: {oh[key] * 100:5.2f}%")
    if args.baseline:
        base = overhang(read_stl(args.baseline))
        print(f"  baseline: {args.baseline}")
        for key, lbl in (("frac_below_45deg", "<45"),
                         ("frac_below_30deg", "<30"),
                         ("frac_below_20deg", "<20")):
            print(f"    {lbl} deg  {base[key] * 100:5.2f}% -> {oh[key] * 100:5.2f}%")
        check(oh["frac_below_45deg"] <= base["frac_below_45deg"] + 1e-4,
              "overhangs no worse than baseline",
              f"{oh['frac_below_45deg'] * 100:.2f}% vs "
              f"{base['frac_below_45deg'] * 100:.2f}%")

    print()
    if fails:
        print(f"FAILED {len(fails)}: {', '.join(fails)}\n")
        return 1
    print("all checks passed\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
