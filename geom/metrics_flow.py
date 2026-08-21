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


def orientation_fractions(d, h, angles=(20, 30, 45), eps=None):
    """Fraction of downward-facing area within each angle of horizontal."""
    eps = eps if eps is not None else AREA_EPS_MULT * h
    gx, gy, gz = np.gradient(d, h)
    mag = np.maximum(np.sqrt(gx * gx + gy * gy + gz * gz), 1e-9)
    nz = (gz / mag)
    shell = np.abs(d) < eps
    down = shell & (nz < 0)
    n_down = int(down.sum())
    out = {"total_down_cm2": n_down * h**3 / (2.0 * eps) / 100.0}
    if n_down == 0:
        for a in angles:
            out[f"within_{a}_frac"] = 0.0
        return out
    nzd = np.abs(nz[down])
    for a in angles:
        out[f"within_{a}_frac"] = float((nzd < np.sin(np.radians(a))).mean())
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


def ceiling_area(field, angles=(20, 30, 45)):
    """V3. Restricted to the lattice, so the OD skin does not swamp it."""
    p = field.params
    d = field.dist.copy()
    outside = (field.r >= p.R_skin) | (field.r <= p.R_port)
    d[outside] = 1e3                       # push non-lattice out of the shell
    out = orientation_fractions(d, field.h, angles)
    out["area_cm2"] = level_set_area(d, field.h) / 100.0
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
    """F4. Shrinkage volume against the reservoir that has to supply it."""
    p = field.params
    cell = field.h**3
    wax_mm3 = float(field.void.sum()) * cell
    shrink = beta * wax_mm3
    chim = 0.0
    if p.no_chim > 0 and p.D_chim > 0:
        chim = p.no_chim * np.pi * (0.5 * p.D_chim) ** 2 * p.L_print
    return {
        "wax_mL": wax_mm3 * 1e-3,
        "shrink_mL": shrink * 1e-3,
        "reservoir_mL": chim * 1e-3,
        "margin": (chim / shrink) if shrink > 0 else float("inf"),
        "beta": beta,
    }
