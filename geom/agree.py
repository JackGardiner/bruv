"""Tier-1 / Tier-2 agreement gate."""

import argparse
import sys

import numpy as np

import check_stl
import grainfield
import metrics_flow as mf
import metrics_solid as ms

TOL = 0.03          # fill fraction, relative
ANGLES = (45, 30, 20)
# overhang tolerance is absolute, percentage points of downward area
OH_TOL = 0.05


def _volume_from_tris(tris):
    a, b, c = tris[:, 0], tris[:, 1], tris[:, 2]
    return float(np.abs(np.einsum("ij,ij->i", a, np.cross(b, c)).sum()) / 6.0)


def mesh_volume_mm3(path):
    return _volume_from_tris(check_stl.read_stl(path))


def compare(stl_path, params, h=0.4):
    """Tier 1 (sampled field) vs Tier 2 (emitted mesh): fill AND overhang."""
    if params.blend_gyr > 0:
        raise ValueError(
            "agree.compare is undefined for blend_gyr > 0: grainfield.sample "
            "does not model the boundary blend (see GrainParams.blend_gyr "
            "docstring), so any measured drift would not be a genuine "
            f"Tier1/Tier2 comparison. Got blend_gyr={params.blend_gyr}."
        )

    tris = check_stl.read_stl(stl_path)          # read once, share below
    env = np.pi * (params.R_grain**2 - params.R_port**2) * params.L_print

    tier2_fill = _volume_from_tris(tris) / env
    field = grainfield.sample(params, h=h)
    tier1_fill = ms.fill_fractions(field)["whole_part"]
    fill_drift = tier1_fill / tier2_fill - 1.0
    fill_pass = bool(abs(fill_drift) < TOL)

    tier2_oh = check_stl.overhang(tris)
    tier1_oh = mf.ceiling_area(field, angles=ANGLES)
    overhang_drift_pp = {
        a: tier1_oh[f"within_{a}_frac"] - tier2_oh[f"frac_below_{a}deg"]
        for a in ANGLES
    }
    overhang_pass = bool(max(abs(v) for v in overhang_drift_pp.values())
                          < OH_TOL)

    return {
        "tier1_fill": tier1_fill,
        "tier2_fill": tier2_fill,
        "drift": float(fill_drift),
        "fill_pass": fill_pass,
        "tier1_overhang": {a: tier1_oh[f"within_{a}_frac"] for a in ANGLES},
        "tier2_overhang": {a: tier2_oh[f"frac_below_{a}deg"] for a in ANGLES},
        "overhang_drift_pp": {a: float(v) for a, v in
                               overhang_drift_pp.items()},
        "overhang_pass": overhang_pass,
        "h": h,
        "pass": bool(fill_pass and overhang_pass),
    }


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("stl")
    ap.add_argument("--h", type=float, default=0.4)
    args = ap.parse_args()
    p = grainfield.GrainParams.from_config()
    r = compare(args.stl, p, h=args.h)
    print(f"  tier 1 fill (sampled field): {r['tier1_fill']:.4%}")
    print(f"  tier 2 fill (emitted mesh):  {r['tier2_fill']:.4%}")
    print(f"  fill drift: {r['drift']:+.3%}  tolerance {TOL:.0%}  "
          f"[{'PASS' if r['fill_pass'] else 'FAIL'}]")
    print(f"\n  overhang (downward-facing area within angle of horizontal), "
          f"tolerance {OH_TOL * 100:.0f} pp")
    for a in ANGLES:
        t1, t2 = r["tier1_overhang"][a], r["tier2_overhang"][a]
        d = r["overhang_drift_pp"][a]
        print(f"    <{a:>2} deg   tier1 {t1:6.2%}   tier2 {t2:6.2%}   "
              f"drift {d * 100:+.2f} pp")
    print(f"  overhang: [{'PASS' if r['overhang_pass'] else 'FAIL'}]")
    print()
    print("  AGREEMENT OK" if r["pass"] else "  DISAGREE -- do not trust tier 1")
    sys.exit(0 if r["pass"] else 1)
