"""Casting metrics for the grain scaffold: venting and feeding."""

import numpy as np

AREA_EPS_MULT = 1.5     # eps = AREA_EPS_MULT * h; validated on a sphere


# ------------------------------------------------------------- reachability

def flood2d(mask, seed):
    """4-connected flood of `seed` within `mask`, both 2-D bool arrays."""
    out = seed & mask
    while True:
        grown = out.copy()
        grown[1:, :] |= out[:-1, :]
        grown[:-1, :] |= out[1:, :]
        grown[:, 1:] |= out[:, :-1]
        grown[:, :-1] |= out[:, 1:]
        grown &= mask
        if grown.sum() == out.sum():
            return out
        out = grown


def reachable_monotone(void, openings):
    """Which void cells reach an opening by a path that never reverses in z."""
    nz = void.shape[2]
    out = np.zeros_like(void)
    prev = None
    for k in range(nz - 1, -1, -1):
        v = void[:, :, k]
        seed = openings[:, :, k] & v
        if prev is not None:
            seed |= prev & v
        out[:, :, k] = flood2d(v, seed)
        prev = out[:, :, k]
    return out


def geodesic2d(mask, seed, maxit=10000):
    """Unit-cost geodesic distance within `mask` from a seeded array."""
    d = np.where(mask, seed, np.inf)
    for _ in range(maxit):
        nd = d.copy()
        nd[1:, :] = np.minimum(nd[1:, :], d[:-1, :] + 1.0)
        nd[:-1, :] = np.minimum(nd[:-1, :], d[1:, :] + 1.0)
        nd[:, 1:] = np.minimum(nd[:, 1:], d[:, :-1] + 1.0)
        nd[:, :-1] = np.minimum(nd[:, :-1], d[:, 1:] + 1.0)
        nd = np.where(mask, nd, np.inf)
        if np.array_equal(nd, d):
            return d
        d = nd
    return d


def escape_path(field, openings=None):
    """V4. Distance (mm) a bubble travels to an opening without going down."""
    void = field.void
    op = openings_of(field) if openings is None else openings
    nz = void.shape[2]
    dist = np.full(void.shape, np.inf)
    prev = None
    for k in range(nz - 1, -1, -1):
        v = void[:, :, k]
        seed = np.where(op[:, :, k] & v, 0.0, np.inf)
        if prev is not None:
            seed = np.minimum(seed, np.where(v, prev + 1.0, np.inf))
        dist[:, :, k] = geodesic2d(v, seed)
        prev = dist[:, :, k]

    reached = void & np.isfinite(dist)
    n_void = int(void.sum())
    if not reached.any():
        return {"mean_mm": 0.0, "p50_mm": 0.0, "p95_mm": 0.0, "max_mm": 0.0,
                "unreachable_frac": 1.0 if n_void else 0.0}
    mm = dist[reached] * field.h
    return {
        "mean_mm": float(mm.mean()),
        "p50_mm": float(np.percentile(mm, 50)),
        "p95_mm": float(np.percentile(mm, 95)),
        "max_mm": float(mm.max()),
        "unreachable_frac": float((void & ~reached).sum()) / max(n_void, 1),
    }


def erode(mask, radius, h):
    """Shrink `mask` by `radius`. Used to drop throats a bubble cannot pass."""
    n = int(round(radius / h))
    if n <= 0:
        return mask.copy()
    out = mask.copy()
    for _ in range(n):
        e = out.copy()
        e[1:, :, :] &= out[:-1, :, :]
        e[:-1, :, :] &= out[1:, :, :]
        e[:, 1:, :] &= out[:, :-1, :]
        e[:, :-1, :] &= out[:, 1:, :]
        e[:, :, 1:] &= out[:, :, :-1]
        e[:, :, :-1] &= out[:, :, 1:]
        out = e
    return out


# ------------------------------------------------------------------- areas

def level_set_area(d, h, eps=None):
    """Surface area of {d = 0} as volume of a thin shell over its thickness."""
    eps = eps if eps is not None else AREA_EPS_MULT * h
    return float((np.abs(d) < eps).sum()) * h**3 / (2.0 * eps)


def down_tilts(d, h, eps):
    """Tilt from horizontal (deg) of each downward-facing cell of {d = 0}."""
    gx, gy, gz = np.gradient(d, h)
    mag = np.maximum(np.sqrt(gx * gx + gy * gy + gz * gz), 1e-9)
    nz = gz / mag
    down = (np.abs(d) < eps) & (nz < 0)
    return np.degrees(np.arccos(np.clip(np.abs(nz[down]), 0.0, 1.0)))


def solid_faces(field):
    """The sheet's two bounding surfaces, as two SEPARATE level sets."""
    half = 0.5 * field.thickness
    return field.dist - half, field.dist + half


def orientation_fractions(d, h, angles=(20, 30, 45), eps=None,
                          mid_surface=True):
    """Fraction of downward-facing area within each angle of horizontal."""
    eps = eps if eps is not None else AREA_EPS_MULT * h
    beta = down_tilts(d, h, eps)
    n_down = len(beta)
    cell_to_cm2 = h**3 / (2.0 * eps) / 100.0
    # a mid-surface only sees one of the sheet's two faces, so double it
    scale = 2.0 if mid_surface else 1.0
    out = {"total_down_cm2": scale * n_down * cell_to_cm2}
    if n_down == 0:
        for a in angles:
            out[f"within_{a}_frac"] = 0.0
        return out
    for a in angles:
        out[f"within_{a}_frac"] = float((beta < a).mean())
    return out


# ----------------------------------------------------------------- venting

def openings_of(field, include_id=True):
    """Where a bubble can leave: top face, ID bore, chimneys, slits."""
    p = field.params
    op = np.zeros_like(field.solid)
    void = field.void
    op[:, :, -1] |= void[:, :, -1]                       # top face
    if include_id:
        op |= void & (field.r <= p.R_port + field.h)     # ID bore
    op |= void & (field.r >= p.R_grain - field.h)        # through a slit
    return op


def venting(field, throat_r=0.0):
    """V1 and V2. `throat_r` > 0 restricts to throats a bubble can pass."""
    void = field.void
    passable = erode(void, throat_r, field.h) if throat_r > 0 else void
    op = openings_of(field) & passable
    reach = reachable_monotone(passable, op)
    trapped = passable & ~reach
    cell = field.h**3
    total = float(void.sum()) * cell
    trapped_mL = float(trapped.sum()) * cell * 1e-3
    per_z = trapped.sum(axis=(0, 1))
    return {
        "trapped_frac": float(trapped.sum()) / max(void.sum(), 1),
        "trapped_mL": trapped_mL,
        "vented_frac": float(reach.sum()) / max(passable.sum(), 1),
        "void_mL": total * 1e-3,
        "worst_z": float(field.z[int(np.argmax(per_z))]) if per_z.any() else 0.0,
        "throat_r": throat_r,
    }


def _lattice_only(d, field):
    """Push everything that isn't lattice out of the shell."""
    p = field.params
    d = d.copy()
    d[(field.r >= p.R_skin) | (field.r <= p.R_port)] = 1e3
    return d


def ceiling_area(field, angles=(20, 30, 45)):
    """V3, on the sheet MID-SURFACE."""
    d = _lattice_only(field.dist, field)
    out = orientation_fractions(d, field.h, angles)
    out["area_cm2"] = level_set_area(d, field.h) / 100.0
    return out


def ceiling_area_solid(field, angles=(20, 30, 45)):
    """V3 on the SOLID BOUNDARY -- the thickness-aware version."""
    h = field.h
    eps = AREA_EPS_MULT * h
    betas, area = [], 0.0
    for face in solid_faces(field):
        d = _lattice_only(face, field)
        betas.append(down_tilts(d, h, eps))
        area += level_set_area(d, h)
    beta = np.concatenate(betas)
    cell_to_cm2 = h**3 / (2.0 * eps) / 100.0
    out = {"total_down_cm2": len(beta) * cell_to_cm2,
           "area_cm2": area / 100.0}
    for a in angles:
        out[f"within_{a}_frac"] = float((beta < a).mean()) if len(beta) else 0.0
    return out


# ----------------------------------------------------------------- feeding

def riser_openings(field):
    """Liquid feeds from above, so the riser is the top face only."""
    op = np.zeros_like(field.solid)
    op[:, :, -1] |= field.void[:, :, -1]
    return op


def channel_area_profile(field):
    """F2. Open cross-sectional area connected to the riser, at each height."""
    reach = reachable_monotone(field.void, riser_openings(field))
    area = reach.sum(axis=(0, 1)) * field.h**2
    return field.z, area


def feeding(field):
    """F1. Void the riser can't feed by a non-ascending path."""
    void = field.void
    reach = reachable_monotone(void, riser_openings(field))
    unfed = void & ~reach
    cell = field.h**3
    per_z = unfed.sum(axis=(0, 1))
    area = reach.sum(axis=(0, 1)) * field.h**2
    k = int(np.argmin(area))
    return {
        "unfed_frac": float(unfed.sum()) / max(void.sum(), 1),
        "unfed_mL": float(unfed.sum()) * cell * 1e-3,
        "worst_z": float(field.z[int(np.argmax(per_z))]) if per_z.any() else 0.0,
        "min_channel_mm2": float(area[k]),
        "min_channel_z": float(field.z[k]),
    }


def directional_ok(mod, tol=1e-6):
    """True if the modulus profile never drops going up to the riser."""
    mod = np.asarray(mod, dtype=np.float64)
    if len(mod) < 2:
        return True
    return bool(np.all(np.diff(mod) >= -tol))


def solidification_modulus(field):
    """F3. Chvorinov modulus V/A of the liquid region, per height."""
    h = field.h
    reach = reachable_monotone(field.void, riser_openings(field))
    area = reach.sum(axis=(0, 1)) * h**2
    # wetted perimeter per slice, from the in-plane boundary of the liquid
    per = np.zeros(reach.shape[2])
    for k in range(reach.shape[2]):
        s = reach[:, :, k]
        if not s.any():
            continue
        edge = np.zeros_like(s)
        edge[1:, :] |= s[1:, :] & ~s[:-1, :]
        edge[:-1, :] |= s[:-1, :] & ~s[1:, :]
        edge[:, 1:] |= s[:, 1:] & ~s[:, :-1]
        edge[:, :-1] |= s[:, :-1] & ~s[:, 1:]
        per[k] = edge.sum() * h
    mod = np.divide(area, np.maximum(per, 1e-9))
    top = mod[-1] if len(mod) else 0.0
    below = mod[:-1]
    return {
        "riser_modulus_mm": float(top),
        "worst_pocket_modulus_mm": float(below.min()) if len(below) else 0.0,
        "directional_ok": directional_ok(mod),
        "modulus_profile": mod,
    }


def feed_demand(field, beta):
    """F4. How much wax the riser has to push in, and how wide the path is."""
    p = field.params
    cell = field.h**3
    wax_mm3 = float(field.void.sum()) * cell
    bore_area = 0.0
    bore_vol = 0.0
    if p.no_chim > 0 and p.D_chim > 0:
        bore_area = p.no_chim * np.pi * (0.5 * p.D_chim) ** 2
        bore_vol = bore_area * p.L_print
    return {
        "wax_mL": wax_mm3 * 1e-3,
        # what the riser above this puck must supply, the actionable number
        "shrink_mL": beta * wax_mm3 * 1e-3,
        # conduit, not reservoir
        "bore_volume_mL": bore_vol * 1e-3,
        "bore_area_mm2": bore_area,
        "beta": beta,
    }
