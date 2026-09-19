"""Structure, printability and ballistics metrics for the grain scaffold."""

import numpy as np

import metrics_flow as mf

# Bisin et al., gyroid alone, compression (their Table 3)
BISIN_PHI = (0.10, 0.15)
BISIN_E_MPA = (16.0, 26.0)
# Bisin et al., W1 wax armoured, static firing (their Table 4)
BISIN_RF = (2.23, 1.75)
BARE_WAX_RF = 1.21          # W1, no armour
EXTRUSION_WIDTH = 0.2       # mm
MIN_PERIMETERS = 2          # printable floor: two perimeters of extrusion

# phi where the linear r_f fit through Bisin's two points crosses zero
RF_ZERO_PHI = BISIN_PHI[0] + BISIN_RF[0] * (BISIN_PHI[1] - BISIN_PHI[0]) / (
    BISIN_RF[0] - BISIN_RF[1])


def cells_across(p):
    """S1. Unit cells across the section, below ~3 it's all edge effect."""
    radial = (p.R_skin - p.R_port) / p.L_cell
    hoop = np.pi * (p.R_skin + p.R_port) / p.L_cell
    axial = p.L_puck / p.Lz_cell
    return {"radial": float(radial), "hoop": float(hoop),
            "axial": float(axial)}


def fill_fractions(field):
    """Both accounting bases, always reported together."""
    p = field.params
    cell = field.h**3
    solid_mm3 = float(field.solid.sum()) * cell
    env = np.pi * (p.R_grain**2 - p.R_port**2) * p.L_print
    skin_env = np.pi * (p.R_grain**2 - p.R_skin**2) * p.L_print
    gyr_env = np.pi * (p.R_skin**2 - p.R_port**2) * p.L_print
    chim = 0.0
    if p.no_chim > 0 and p.D_chim > 0:
        chim = p.no_chim * np.pi * (0.5 * p.D_chim) ** 2 * p.L_print
    # skin is measured, so perforations aren't charged to the lattice
    skin = float((field.solid & (field.r >= p.R_skin)).sum()) * cell
    sheet = solid_mm3 - skin
    return {
        "whole_part": solid_mm3 / env,
        "gyroid_only_chim_in": sheet / gyr_env,
        "gyroid_only_chim_out": sheet / max(gyr_env - chim, 1e-9),
        "skin_frac": skin_env / env,
        "solid_mL": solid_mm3 * 1e-3,
        "mass_g": solid_mm3 * 1e-6 * p.rho,
    }


def stiffness(phi):
    """S2. Gibson-Ashby E = C phi^n fitted through Bisin's two points."""
    n = np.log(BISIN_E_MPA[1] / BISIN_E_MPA[0]) / np.log(
        BISIN_PHI[1] / BISIN_PHI[0])
    C = BISIN_E_MPA[0] / BISIN_PHI[0] ** n
    return {
        "E_MPa": float(C * phi ** n),
        "exponent": float(n),
        "basis": "Bisin EUCASS 2022 Table 3, gyroid alone, 2 points",
        "extrapolated": not (BISIN_PHI[0] <= phi <= BISIN_PHI[1]),
    }


def regression_rate(phi):
    """Ballistics. Linear in phi through Bisin's two W1-armoured points."""
    slope = (BISIN_RF[1] - BISIN_RF[0]) / (BISIN_PHI[1] - BISIN_PHI[0])
    r_f = BISIN_RF[0] + slope * (phi - BISIN_PHI[0])
    return {
        "r_f_mm_s": float(r_f),
        "vs_bare_wax": float(r_f / BARE_WAX_RF),
        "slope_per_phi": float(slope),
        "extrapolated": not (BISIN_PHI[0] <= phi <= BISIN_PHI[1]),
        "caveat": "linear extrapolation past Bisin's tested 10-15% range",
    }


def _lattice_distance(field):
    """Sheet distance field with everything that isn't lattice pushed away."""
    p = field.params
    d = field.dist.copy()
    d[(field.r >= p.R_skin) | (field.r <= p.R_port)] = 1e3
    return d


def interface_area(field):
    """S4, interface area on the mid-surface. Thickness-blind."""
    return mf.level_set_area(_lattice_distance(field), field.h) / 100.0


def interface_area_solid(field):
    """S4 on the SOLID BOUNDARY -- the area the paraffin actually wets."""
    return sum(mf.level_set_area(mf._lattice_only(face, field), field.h)
               for face in mf.solid_faces(field)) / 100.0


# ------------------------------------------------ radial profiles (graded)

def phi_profile(field, nbins=10):
    """Fill fraction of the LATTICE per annular bin, ID to OD."""
    p = field.params
    edges = np.linspace(p.R_port, p.R_skin, int(nbins) + 1)
    lattice = field.solid & (field.r < p.R_skin) & (field.r >= p.R_port)
    phi, centres = [], []
    for a, b in zip(edges[:-1], edges[1:]):
        shell = (field.r >= a) & (field.r < b)
        n = int(shell.sum())
        phi.append(float(lattice[shell].sum()) / n if n else 0.0)
        centres.append(0.5 * (a + b))
    return {"r_mm": np.array(centres), "phi": np.array(phi),
            "edges_mm": edges}


def regression_schedule(field, nbins=10):
    """Ballistics as the burn front sweeps outward: r_f at each radius."""
    prof = phi_profile(field, nbins)
    over = prof["phi"] >= RF_ZERO_PHI
    if over.any():
        bad = ", ".join(f"r={r:.1f}mm phi={v:.3f}"
                        for r, v in zip(prof["r_mm"][over], prof["phi"][over]))
        raise ValueError(
            "regression_schedule is undefined at these radii: the linear fit "
            f"through Bisin's two points crosses zero at phi={RF_ZERO_PHI:.3f} "
            f"and returns a negative regression rate beyond it. Got {bad}. "
            "Thin the sheet at the OD or score ballistics some other way.")
    rf = np.array([regression_rate(v)["r_f_mm_s"] for v in prof["phi"]])
    inband = [(BISIN_PHI[0] <= v <= BISIN_PHI[1]) for v in prof["phi"]]
    return {
        "r_mm": prof["r_mm"],
        "phi": prof["phi"],
        "r_f_mm_s": rf,
        "spread_mm_s": float(rf.max() - rf.min()),
        "in_band_frac": float(np.mean(inband)),
        "caveat": "linear extrapolation past Bisin's tested 10-15% range",
    }


def stiffness_profile(field, nbins=10):
    """S2 per annular bin, for a graded sheet."""
    prof = phi_profile(field, nbins)
    E = np.array([stiffness(v)["E_MPa"] for v in prof["phi"]])
    inband = [(BISIN_PHI[0] <= v <= BISIN_PHI[1]) for v in prof["phi"]]
    return {
        "r_mm": prof["r_mm"],
        "phi": prof["phi"],
        "E_MPa": E,
        "ratio_od_to_id": float(E[-1] / max(E[0], 1e-9)),
        "in_band_frac": float(np.mean(inband)),
        "basis": "Bisin EUCASS 2022 Table 3, gyroid alone, 2 points",
    }


def directional_area(field):
    """S3. Sheet area projected normal to each axis, as a stiffness proxy."""
    d = _lattice_distance(field)
    h = field.h
    eps = mf.AREA_EPS_MULT * h
    gx, gy, gz = np.gradient(d, h)
    mag = np.maximum(np.sqrt(gx * gx + gy * gy + gz * gz), 1e-9)
    shell = np.abs(d) < eps
    scale = h**3 / (2.0 * eps) / 100.0
    return {ax: float(np.abs(g[shell] / mag[shell]).sum() * scale)
            for ax, g in (("x", gx), ("y", gy), ("z", gz))}


def free_edge_length(field):
    """S5. Length of sheet edge ending in air, by where it's cut."""
    p, h = field.params, field.h
    lat = field.solid & (field.r < p.R_skin)
    out = {}

    t_local = field.thickness

    def edge_len(mask):
        """Edge cells form a t x h tube along the cut, so L = count*h**2/t."""
        return float((h**2 / np.maximum(t_local[mask], 1e-9)).sum())

    id_edge = lat & (field.r <= p.R_port + h)
    out["id_mm"] = edge_len(id_edge)
    bot = np.zeros_like(lat)
    bot[:, :, 0] = lat[:, :, 0]
    top = np.zeros_like(lat)
    top[:, :, -1] = lat[:, :, -1]
    ends = bot | top
    out["ends_mm"] = edge_len(ends)

    bore = np.zeros_like(lat)
    if p.no_chim > 0 and p.D_chim > 0:
        X, Y = np.meshgrid(field.x, field.y, indexing="ij")
        for i in range(p.no_chim):
            th = p.theta0_chim + i * 2 * np.pi / p.no_chim
            cx, cy = p.r_chim * np.cos(th), p.r_chim * np.sin(th)
            rr = np.sqrt((X - cx) ** 2 + (Y - cy) ** 2)
            ring = (np.abs(rr - 0.5 * p.D_chim) < h)[:, :, None]
            bore |= lat & np.broadcast_to(ring, lat.shape)
    out["chimney_mm"] = edge_len(bore)

    # total is the union of the three masks, not their sum
    out["total_mm"] = edge_len(id_edge | ends | bore)
    return out


def printability(field, ew=EXTRUSION_WIDTH):
    """Wall in extrusion widths, overhang area, minimum feature."""
    p = field.params
    d = _lattice_distance(field)
    over = mf.orientation_fractions(d, field.h, angles=(20, 30, 45))
    lattice = field.solid & (field.r < p.R_skin) & (field.r >= p.R_port)
    th = field.thickness
    if lattice.any():
        t_min = float(th[lattice].min())
        t_max = float(th[lattice].max())
    else:
        t_min = t_max = float(p.th_gyr)
    n_ew = t_min / ew
    out = {
        "wall_mm": t_min,
        "wall_min_mm": t_min,
        "wall_max_mm": t_max,
        "wall_graded": bool(t_max - t_min > 1e-6),
        "wall_in_ew": float(n_ew),
        "wall_is_integer": bool(abs(n_ew - round(n_ew)) < 0.02),
        "wall_below_two_perimeters": bool(t_min < MIN_PERIMETERS * ew),
        "min_feature_mm": float(min(t_min, p.th_skin)),
    }
    out.update(over)
    return out
