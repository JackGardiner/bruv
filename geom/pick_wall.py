"""Picks the gyroid sheet thickness for a target fill fraction."""

import argparse
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import grainfield
import metrics_solid as ms
import sweep

EW = ms.EXTRUSION_WIDTH  # mm, reused, not re-declared
PHI_LO, PHI_HI = ms.BISIN_PHI  # 0.10-0.15, the range Bisin et al. tested
H_PROBE = 0.8  # cache key only, doesn't change the coefficient


def rule(title):
    print("=" * 74)
    print(title)
    print("=" * 74)


def pick_wall(phi, L, Lz, L_puck):
    """Ideal and rounded wall thickness for a target gyroid-only phi."""
    coeff = sweep.calibrate_coefficient(L, Lz, H_PROBE)
    t_ideal = phi * L / coeff
    n_ew_ideal = t_ideal / EW
    t_rounded = round(t_ideal / EW) * EW
    n_ew_rounded = t_rounded / EW
    phi_ideal = coeff * t_ideal / L
    phi_rounded = coeff * t_rounded / L
    return {
        "L": L, "Lz": Lz, "L_puck": L_puck, "stretch_k": Lz / L,
        "coeff": coeff,
        "t_ideal_mm": t_ideal, "n_ew_ideal": n_ew_ideal, "phi_ideal": phi_ideal,
        "t_rounded_mm": t_rounded, "n_ew_rounded": n_ew_rounded,
        "phi_rounded": phi_rounded,
    }


# ------------------------------------------------------------ graded sheet

def taper_fill(base, t_id, t_od, expo, h, basis):
    """Measured fill for one taper. Sampled, not derived from a coefficient."""
    p = base.replace(th_id=t_id, th_od=t_od, expo_gyr=expo, W_perf=0.0)
    return ms.fill_fractions(grainfield.sample(p, h=h))[basis]


def pick_taper(phi, ratio, expo=1.0, base=None, h=0.6, basis="whole_part",
               tol=5e-5, maxit=12):
    """Solve a radial taper for a target fill fraction."""
    base = base or grainfield.GrainParams.from_config()
    assert ratio > 0, f"ratio={ratio}"

    # seed from the flat solution, the secant does the real work
    coeff = sweep.calibrate_coefficient(base.L_cell, base.Lz_cell, H_PROBE)
    t_flat = max(phi * base.L_cell / coeff, 2 * EW)
    s0 = t_flat / (0.5 * (1.0 + ratio))
    s1 = s0 * 1.15

    f0 = taper_fill(base, s0, s0 * ratio, expo, h, basis) - phi
    for _ in range(maxit):
        f1 = taper_fill(base, s1, s1 * ratio, expo, h, basis) - phi
        if abs(f1) < tol:
            break
        if abs(f1 - f0) < 1e-12:
            break
        s2 = s1 - f1 * (s1 - s0) / (f1 - f0)
        s0, f0, s1 = s1, f1, max(s2, 1e-3)
    else:
        f1 = taper_fill(base, s1, s1 * ratio, expo, h, basis) - phi

    t_id, t_od = s1, s1 * ratio
    return {
        "phi_target": phi, "phi_got": f1 + phi, "basis": basis, "h": h,
        "ratio": ratio, "expo": expo,
        "th_id_mm": t_id, "th_od_mm": t_od,
        "th_id_ew": t_id / EW, "th_od_ew": t_od / EW,
        "below_two_perimeters": bool(t_id < ms.MIN_PERIMETERS * EW),
    }


def main():
    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--phi", type=float, required=True,
                     help="target gyroid-only fill fraction")
    ap.add_argument("--L", type=float, default=None,
                     help="cell length mm (default: config/all.json's L_cell)")
    grp = ap.add_mutually_exclusive_group()
    grp.add_argument("--Lz", type=float, default=None,
                      help="axial cell length mm "
                           "(default: config/all.json's Lz_cell)")
    grp.add_argument("--cells", type=int, default=None,
                      help="whole axial cells over the puck length -- "
                           "derives Lz = L_puck / cells")
    ap.add_argument("--puck-length", type=float, default=None,
                     help="finished puck length mm "
                          "(default: config/all.json's L_puck)")
    ap.add_argument("--taper", type=float, default=None, metavar="RATIO",
                     help="solve a radial taper of this th_od/th_id ratio for "
                          "--phi instead of a flat wall")
    ap.add_argument("--expo", type=float, default=1.0,
                     help="taper exponent (1 linear, >1 thickens later)")
    ap.add_argument("--basis", default="whole_part",
                     choices=("whole_part", "gyroid_only_chim_in",
                              "gyroid_only_chim_out"),
                     help="which fill fraction --taper targets")
    ap.add_argument("--h", type=float, default=0.6,
                     help="sampling spacing for --taper, mm")
    args = ap.parse_args()

    if args.taper is not None:
        r = pick_taper(args.phi, args.taper, expo=args.expo, h=args.h,
                       basis=args.basis)
        rule("PICK TAPER -- th_id/th_od for a target fill")
        print(f"  target {r['basis']} fill = {r['phi_target']:.4f}"
              f"   (solved to {r['phi_got']:.4f}, h={r['h']}mm)")
        print(f"  shape: th_od/th_id = {r['ratio']:.2f}, "
              f"exponent {r['expo']:.2f}")
        print()
        print(f"  th_id = {r['th_id_mm']:.4f} mm  "
              f"({r['th_id_ew']:.2f} extrusion widths)")
        print(f"  th_od = {r['th_od_mm']:.4f} mm  "
              f"({r['th_od_ew']:.2f} extrusion widths)")
        if r["below_two_perimeters"]:
            print()
            print(f"  WARNING: th_id is below the {ms.MIN_PERIMETERS}-perimeter "
                  f"floor ({ms.MIN_PERIMETERS * EW}mm). The slicer cannot")
            print("           print that wall; the taper is too aggressive.")
        print()
        return

    base = grainfield.GrainParams.from_config()
    L = args.L if args.L is not None else base.L_cell
    L_puck = args.puck_length if args.puck_length is not None else base.L_puck
    if args.cells is not None:
        Lz = L_puck / args.cells
    elif args.Lz is not None:
        Lz = args.Lz
    else:
        Lz = base.Lz_cell

    r = pick_wall(args.phi, L, Lz, L_puck)

    rule("PICK WALL -- t for a target gyroid-only phi")
    print(f"  cell geometry: L={r['L']:.4f}mm  Lz={r['Lz']:.4f}mm  "
          f"(stretch k={r['stretch_k']:.3f})  L_puck={r['L_puck']:.4f}mm")
    print(f"  coefficient phi/(t/L), measured numerically for this "
          f"geometry: {r['coeff']:.4f}")
    print(f"  (literature's isotropic thin-sheet limit is 3.09 -- "
          f"{abs(r['coeff']/3.09 - 1):.1%} off here)")
    print()
    print(f"  target phi_gyroid_only = {args.phi:.4f}")
    print()
    print(f"  ideal wall     t = {r['t_ideal_mm']:.4f} mm  "
          f"({r['n_ew_ideal']:.2f} extrusion widths, {EW}mm each)")
    print(f"                 delivers phi = {r['phi_ideal']:.4f}")
    print(f"  rounded wall   t = {r['t_rounded_mm']:.4f} mm  "
          f"({r['n_ew_rounded']:.0f} extrusion widths, exact)")
    print(f"                 delivers phi = {r['phi_rounded']:.4f}")
    print()

    if r["t_rounded_mm"] < 2 * EW:
        print(f"  WARNING: rounded wall {r['t_rounded_mm']:.2f}mm is under "
              f"two extrusion widths ({2*EW:.1f}mm), the printable floor.")
        print()
    if not (PHI_LO <= r["phi_rounded"] <= PHI_HI):
        print(f"  WARNING: delivered phi {r['phi_rounded']:.4f} is outside "
              f"{PHI_LO:.2f}-{PHI_HI:.2f}, the range Bisin et al. actually "
              f"tested. The stiffness and regression correlations are "
              f"extrapolating at this wall.")
        print()


if __name__ == "__main__":
    main()
