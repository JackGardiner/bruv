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
    args = ap.parse_args()

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
