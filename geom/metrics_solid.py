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
    skin = np.pi * (p.R_grain**2 - p.R_skin**2) * p.L_print
    gyr_env = np.pi * (p.R_skin**2 - p.R_port**2) * p.L_print
    chim = 0.0
    if p.no_chim > 0 and p.D_chim > 0:
        chim = p.no_chim * np.pi * (0.5 * p.D_chim) ** 2 * p.L_print
    # analytic skin, so cut slits read as lattice. fine while W_perf = 0
    sheet = solid_mm3 - skin
    return {
        "whole_part": solid_mm3 / env,
        "gyroid_only_chim_in": sheet / gyr_env,
        "gyroid_only_chim_out": sheet / max(gyr_env - chim, 1e-9),
        "skin_frac": skin / env,
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
    """S4. Gyroid-paraffin contact area in cm2."""
    return mf.level_set_area(_lattice_distance(field), field.h) / 100.0


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

    def edge_len(mask):
        """Edge cells form a t x h tube along the cut, so L = count*h**2/t."""
        t = max(p.th_gyr, 1e-9)
        return float(mask.sum()) * h**2 / t

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
    n_ew = p.th_gyr / ew
    out = {
        "wall_mm": p.th_gyr,
        "wall_in_ew": float(n_ew),
        "wall_is_integer": bool(abs(n_ew - round(n_ew)) < 0.02),
        "wall_below_two_perimeters": bool(p.th_gyr < 2 * ew),
        "min_feature_mm": float(min(p.th_gyr, p.th_skin)),
    }
    out.update(over)
    return out
