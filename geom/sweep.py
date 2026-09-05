"""Parameter sweep to a recorded Pareto front."""

import argparse
import csv
import functools
import itertools
import json
import os
import time

import grainfield
import metrics_flow as mf
import metrics_solid as ms
import numpy as np
import sigma_sensitivity as ss

# central values of each band
BETA_DEFAULT = 0.19            # shrinkage, band 0.15-0.25
SIGMA_MNM_DEFAULT = 25.0       # surface tension mN/m, band 20-30
OVERHANG_LIMIT_DEG_DEFAULT = 42  # ABS FDM, band 40-45
# nominal bubble size for the throat-restricted (V2) venting metric
BUBBLE_H_MM_DEFAULT = 2.0
# S1: fewer cells than this across the annulus is edge effect, reported
# but not gated
S1_MIN_CELLS_RADIAL = 3.0

# direction of improvement for all six objectives
OBJECTIVES = {
    "trapped_frac": "min",      # venting
    "unfed_frac": "min",        # feeding
    "E_MPa": "max",             # stiffness
    "r_f_mm_s": "max",          # ballistics
    "interface_cm2": "min",     # debonding
    "within_45_frac": "min",    # printability / bubble pinning
}

# phi is a stratum, so each phi level's front uses the other four and
# stiffness and ballistics go in the phi trade curve
OBJECTIVES_STRATIFIED = {
    "trapped_frac": "min",
    "unfed_frac": "min",
    "interface_cm2": "min",
    "within_45_frac": "min",
}


def venting_id_closed(field, throat_r=0.0):
    """V1 recomputed with the ID bore excluded from the opening set."""
    void = field.void
    passable = mf.erode(void, throat_r, field.h) if throat_r > 0 else void
    op = mf.openings_of(field, include_id=False) & passable
    reach = mf.reachable_monotone(passable, op)
    trapped = passable & ~reach
    cell = field.h ** 3
    return {
        "trapped_frac": float(trapped.sum()) / max(void.sum(), 1),
        "trapped_mL": float(trapped.sum()) * cell * 1e-3,
    }


def evaluate(p, h=0.8, beta=BETA_DEFAULT, sigma_mNm=SIGMA_MNM_DEFAULT,
             bubble_h_mm=BUBBLE_H_MM_DEFAULT,
             overhang_limit_deg=OVERHANG_LIMIT_DEG_DEFAULT):
    f = grainfield.sample(p, h=h)
    fills = ms.fill_fractions(f)
    phi = fills["gyroid_only_chim_out"]
    vent = mf.venting(f)
    vent_id_closed = venting_id_closed(f)
    throat_r = ss.min_throat_mm(sigma_mNm, bubble_h_mm)
    vent_throat = mf.venting(f, throat_r=throat_r)
    feed = mf.feeding(f)
    angles = tuple(sorted({20, 30, 45, overhang_limit_deg}))
    ceil = mf.ceiling_area(f, angles=angles)
    prn = ms.printability(f)
    cells = ms.cells_across(p)
    row = {
        "L_cell": p.L_cell,
        "Lz_cell": p.Lz_cell,
        "stretch_k": p.Lz_cell / p.L_cell,
        "th_gyr": p.th_gyr,
        "th_anchor": p.th_anchor,
        "W_perf": p.W_perf,
        "no_chim": p.no_chim,
        "phi_whole_part": fills["whole_part"],
        "phi_gyroid_only": phi,
        "mass_g": fills["mass_g"],
        "trapped_frac": vent["trapped_frac"],
        "trapped_mL": vent["trapped_mL"],
        "trapped_frac_id_closed": vent_id_closed["trapped_frac"],
        "trapped_mL_id_closed": vent_id_closed["trapped_mL"],
        "sigma_mNm": sigma_mNm,
        "bubble_h_mm": bubble_h_mm,
        "throat_r_mm": throat_r,
        "trapped_frac_throat": vent_throat["trapped_frac"],
        "trapped_mL_throat": vent_throat["trapped_mL"],
        "vented_frac_throat": vent_throat["vented_frac"],
        "unfed_frac": feed["unfed_frac"],
        "interface_cm2": ms.interface_area(f),
        "cells_radial": cells["radial"],
        "cells_hoop": cells["hoop"],
        "cells_axial": cells["axial"],
        "meets_S1": bool(cells["radial"] >= S1_MIN_CELLS_RADIAL),
        "wall_in_ew": prn["wall_in_ew"],
        "wall_is_integer": prn["wall_is_integer"],
        "overhang_limit_deg": overhang_limit_deg,
        "within_45_frac": ceil["within_45_frac"],
        "within_20_frac": ceil["within_20_frac"],
    }
    limit_key = f"within_{overhang_limit_deg}_frac"
    if limit_key in ceil:
        row[limit_key] = ceil[limit_key]
    row.update({"E_MPa": ms.stiffness(phi)["E_MPa"],
                "E_extrapolated": ms.stiffness(phi)["extrapolated"]})
    rf = ms.regression_rate(phi)
    row.update({"r_f_mm_s": rf["r_f_mm_s"],
                "r_f_extrapolated": rf["extrapolated"],
                "r_f_vs_bare_wax": rf["vs_bare_wax"]})
    row.update(mf.feed_demand(f, beta))
    return row


def dominates(a, b, objectives):
    better_anywhere = False
    for key, sense in objectives.items():
        av, bv = a[key], b[key]
        if sense == "min":
            if av > bv:
                return False
            if av < bv:
                better_anywhere = True
        else:
            if av < bv:
                return False
            if av > bv:
                better_anywhere = True
    return better_anywhere


def pareto(rows, objectives):
    """Indices of the non-dominated rows."""
    keep = []
    for i, a in enumerate(rows):
        if not any(dominates(b, a, objectives)
                   for j, b in enumerate(rows) if j != i):
            keep.append(i)
    return keep


def dedupe_by_geometry(rows):
    """Drop rows that share (L_cell, Lz_cell, th_gyr) with an earlier row."""
    seen = set()
    out = []
    for r in rows:
        key = (r["L_cell"], r["Lz_cell"], r["th_gyr"])
        if key in seen:
            continue
        seen.add(key)
        out.append(r)
    return out


def phi_trade_curve(distinct_rows):
    """phi's own effect on stiffness and ballistics, as a trade curve."""
    by_target = {}
    for r in distinct_rows:
        by_target.setdefault(r["phi_target"], []).append(r)
    curve = []
    for phi_t in sorted(by_target):
        grp = by_target[phi_t]
        phis = [r["phi_gyroid_only"] for r in grp]
        Es = [r["E_MPa"] for r in grp]
        rfs = [r["r_f_mm_s"] for r in grp]
        curve.append({
            "phi_target": phi_t,
            "n_designs": len(grp),
            "phi_gyroid_only_min": min(phis),
            "phi_gyroid_only_mean": sum(phis) / len(phis),
            "phi_gyroid_only_max": max(phis),
            "E_MPa_min": min(Es), "E_MPa_mean": sum(Es) / len(Es),
            "E_MPa_max": max(Es),
            "r_f_mm_s_min": min(rfs), "r_f_mm_s_mean": sum(rfs) / len(rfs),
            "r_f_mm_s_max": max(rfs),
        })
    return curve


def fronts_by_phi(distinct_rows, objectives=OBJECTIVES_STRATIFIED):
    """A separate Pareto front at each phi level."""
    by_target = {}
    for r in distinct_rows:
        by_target.setdefault(r["phi_target"], []).append(r)
    out = {}
    for phi_t in sorted(by_target):
        grp = by_target[phi_t]
        front = pareto(grp, objectives)
        out[phi_t] = [grp[i] for i in front]
    return out


def feasible(p):
    """Hard constraints. A design failing these is not on any front."""
    if p.th_gyr < 2 * ms.EXTRUSION_WIDTH:
        return False, "wall below two extrusion widths"
    if p.W_perf > 2.5:
        return False, "slit wider than the measured clearance"
    if p.no_chim not in (0, 2, 4):
        return False, "chimney count not 2 or 4"
    if p.no_perf not in (0, 2, 4):
        return False, "slit count not 2 or 4"
    return True, ""


@functools.lru_cache(maxsize=None)
def calibrate_coefficient(L, Lz, h):
    """phi/(t/L) measured on one cell, rather than assumed to be 3.09."""
    n = 120
    u = (np.arange(n) + 0.5) / n
    X, Y, Z = np.meshgrid(u * L, u * L, u * Lz, indexing="ij")
    p = grainfield.GrainParams.from_config().replace(
        L_cell=L, Lz_cell=Lz, x0_gyr=0.0, y0_gyr=0.0, z0_gyr=0.0)
    d = np.abs(grainfield.gyroid_distance(p, X, Y, Z))
    t_probe = 0.08 * L
    phi = float((d < 0.5 * t_probe).mean())
    return phi / (t_probe / L)


def s1_arithmetic(base, h, stretch_k=None):
    """Which cell sizes keep 3 cells across the annulus."""
    stretch_k = stretch_k if stretch_k is not None else base.Lz_cell / base.L_cell
    annulus_mm = base.R_skin - base.R_port
    L_needed = annulus_mm / S1_MIN_CELLS_RADIAL
    n_cells = max(1, round(base.L_puck / (L_needed * stretch_k)))
    Lz = base.L_puck / n_cells
    coeff = calibrate_coefficient(L_needed, Lz, h)
    phi_ref = 0.15
    t = phi_ref * L_needed / coeff
    return {
        "annulus_mm": annulus_mm,
        "L_cell_needed_mm": L_needed,
        "Lz_cell_at_needed": Lz,
        "coeff_at_needed": coeff,
        "phi_ref": phi_ref,
        "th_gyr_at_phi_ref_mm": t,
        "th_gyr_at_phi_ref_ew": t / ms.EXTRUSION_WIDTH,
        "two_perimeter_floor_mm": 2 * ms.EXTRUSION_WIDTH,
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--h", type=float, default=0.8)
    ap.add_argument("--beta", type=float, default=BETA_DEFAULT)
    ap.add_argument("--sigma", type=float, default=SIGMA_MNM_DEFAULT)
    ap.add_argument("--bubble-h", type=float, default=BUBBLE_H_MM_DEFAULT)
    ap.add_argument("--overhang-limit", type=float,
                     default=OVERHANG_LIMIT_DEG_DEFAULT)
    ap.add_argument("--out", default="geom/exports/sweep-results")
    args = ap.parse_args()

    base = grainfield.GrainParams.from_config()
    os.makedirs(args.out, exist_ok=True)

    # axes, phi included
    L_cells = [8.0, 10.0, 12.0, 16.0, 20.0, 24.0]
    stretches = [1.0, 1.2, 1.337, 1.5]
    phis = [0.10, 0.13, 0.15, 0.18, 0.21, 0.26]

    rows, skipped, t0 = [], 0, time.time()
    for L, k, phi in itertools.product(L_cells, stretches, phis):
        n_cells = max(1, round(base.L_puck / (L * k)))
        Lz = base.L_puck / n_cells
        coeff = calibrate_coefficient(L, Lz, args.h)
        t_ideal = phi * L / coeff
        t_rounded = round(t_ideal / 0.2) * 0.2
        p = base.replace(L_cell=L, Lz_cell=Lz, th_gyr=t_rounded,
                          x0_gyr=-L / 4.0)
        ok, why = feasible(p)
        if not ok:
            skipped += 1
            continue
        row = evaluate(p, h=args.h, beta=args.beta, sigma_mNm=args.sigma,
                        bubble_h_mm=args.bubble_h,
                        overhang_limit_deg=args.overhang_limit)
        row["n_cells_axial"] = n_cells
        row["phi_target"] = phi
        row["wall_coeff"] = coeff
        row["th_gyr_ideal"] = t_ideal
        row["th_gyr_rounded"] = t_rounded
        rows.append(row)
        print(f"  L={L:5.1f} k={k:5.3f} phi={phi:.2f} -> "
              f"trapped {row['trapped_frac']:.3%} "
              f"(id-closed {row['trapped_frac_id_closed']:.3%}) "
              f"E {row['E_MPa']:6.1f} r_f {row['r_f_mm_s']:.2f} "
              f"S1 {'y' if row['meets_S1'] else 'n'}")

    distinct = dedupe_by_geometry(rows)
    fronts = fronts_by_phi(distinct)
    curve = phi_trade_curve(distinct)
    s1 = s1_arithmetic(base, args.h)
    elapsed = time.time() - t0

    n_meets_s1 = sum(1 for r in distinct if r["meets_S1"])
    print(f"\n  {len(rows)} raw combinations, {len(distinct)} distinct "
          f"geometries, {skipped} infeasible, {elapsed:.0f}s")
    print(f"  {n_meets_s1}/{len(distinct)} distinct geometries meet S1 "
          f"(>= {S1_MIN_CELLS_RADIAL:.0f} cells radially)")
    for phi_t, front in fronts.items():
        print(f"  phi={phi_t:.2f}: {len(front)} on the stratified front "
              f"(of {sum(1 for r in distinct if r['phi_target'] == phi_t)} "
              f"distinct geometries at this phi)")

    with open(os.path.join(args.out, "all.csv"), "w", newline="") as fh:
        w = csv.DictWriter(fh, fieldnames=sorted(rows[0]))
        w.writeheader()
        w.writerows(rows)

    with open(os.path.join(args.out, "front.json"), "w") as fh:
        json.dump({
            "objectives_stratified": OBJECTIVES_STRATIFIED,
            "objectives_full_six_not_used_for_fronting": OBJECTIVES,
            "note": ("phi is stratified out, not scored as an objective: "
                     "E_MPa and r_f_mm_s are both pure, oppositely-signed "
                     "functions of phi alone (the two Bisin fits), so a "
                     "flat front over all six objectives puts ~100% of "
                     "designs on it. See phi_trade_curve for phi's own "
                     "effect, and fronts_by_phi for the front at each "
                     "phi level over the remaining four objectives."),
            "phi_trade_curve": curve,
            "fronts_by_phi": {f"{k:.2f}": v for k, v in fronts.items()},
            "s1_arithmetic": s1,
            "meta": {
                "h": args.h,
                "beta": args.beta,
                "sigma_mNm": args.sigma,
                "bubble_h_mm": args.bubble_h,
                "overhang_limit_deg": args.overhang_limit,
                "n_raw_combinations": len(rows),
                "n_distinct_geometries": len(distinct),
                "n_infeasible": skipped,
                "n_meets_S1": n_meets_s1,
                "s1_min_cells_radial": S1_MIN_CELLS_RADIAL,
                "wall_clock_s": elapsed,
            },
        }, fh, indent=2)
    print(f"  written to {args.out}")


if __name__ == "__main__":
    main()
