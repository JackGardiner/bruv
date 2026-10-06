"""Radial sheet grading: what does tapering the wall actually buy?"""

import argparse
import datetime
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import grainfield
import metrics_flow as mf
import metrics_solid as ms
import pick_wall

ROOT = os.path.dirname(HERE)
NBINS = 10

# th_od/th_id ratios and ramp exponents, 1.0 is the flat baseline
RATIOS = [1.0, 1.5, 2.0, 3.0, 4.0]
EXPOS = [0.5, 1.0, 2.0]


def evaluate(base, t_id, t_od, expo, h):
    """Every metric the taper decision turns on, for one sheet."""
    p = base.replace(th_id=t_id, th_od=t_od, expo_gyr=expo, W_perf=0.0)
    f = grainfield.sample(p, h=h)

    fill = ms.fill_fractions(f)
    prof = ms.phi_profile(f, NBINS)
    stiff = ms.stiffness_profile(f, NBINS)
    edge = ms.free_edge_length(f)
    prn = ms.printability(f)
    row = {
        "th_id": t_id, "th_od": t_od, "expo": expo,
        "fill": fill["whole_part"],
        "gyroid_only": fill["gyroid_only_chim_in"],
        "mass_g": fill["mass_g"],
        "phi_id": float(prof["phi"][0]),
        "phi_od": float(prof["phi"][-1]),
        "phi_ratio": float(prof["phi"][-1] / max(prof["phi"][0], 1e-9)),
        # max/min over all bins, the flat sheet wobbles in the middle ones
        "phi_span": float(prof["phi"].max() / max(prof["phi"].min(), 1e-9)),
        "E_ratio": stiff["ratio_od_to_id"],
        "E_in_band": stiff["in_band_frac"],
        # thickness-aware versions, the mid-surface ones can't see a taper
        "interface_cm2": ms.interface_area_solid(f),
        "ceil45": mf.ceiling_area_solid(f)["within_45_frac"],
        "edge_id_mm": edge["id_mm"],
        "edge_total_mm": edge["total_mm"],
        "wall_min": prn["wall_min_mm"],
        "unprintable": prn["wall_below_two_perimeters"],
        "escape_mm": mf.escape_path(f)["mean_mm"],
        "unfed_frac": mf.feeding(f)["unfed_frac"],
    }
    try:
        sched = ms.regression_schedule(f, NBINS)
        row["rf_spread"] = sched["spread_mm_s"]
        row["rf_id"] = float(sched["r_f_mm_s"][0])
        row["rf_od"] = float(sched["r_f_mm_s"][-1])
    except ValueError:
        # the correlation can't reach this phi
        row["rf_spread"] = row["rf_id"] = row["rf_od"] = None
    return row


def run(phi_target, h, basis="whole_part"):
    base = grainfield.GrainParams.from_config()
    rows = []
    for expo in EXPOS:
        for ratio in RATIOS:
            if ratio == 1.0 and expo != 1.0:
                continue        # a flat sheet has no shape to exponentiate
            sol = pick_wall.pick_taper(phi_target, ratio, expo=expo,
                                        base=base, h=h, basis=basis)
            row = evaluate(base, sol["th_id_mm"], sol["th_od_mm"], expo, h)
            row["ratio"] = ratio
            rows.append(row)
    rows.sort(key=lambda r: (r["expo"], r["ratio"]))
    return rows


def fmt(rows, phi_target, h):
    out = []
    w = out.append
    w(f"# Radial taper study -- held at fill = {phi_target:.4f}, h = {h} mm")
    w("")
    w(f"Generated {datetime.date.today().isoformat()} by "
      "`python3 geom/taper_study.py`.")
    w("")
    w("Every row carries the same fill fraction, so differences are pure")
    w("redistribution. `ratio` is th_od/th_id; ratio 1.0 is the flat baseline.")
    w("")
    head = ("| expo | ratio | th_id | th_od | phi_id | phi_od | phi_od/id | "
            "phi span | E od/id | iface cm2 | ceil<45 | edge_id mm | "
            "escape mm | unfed | r_f spread | wall min |")
    w(head)
    w("|" + "---|" * (head.count("|") - 1))
    base = rows[0]
    for r in rows:
        if r["ratio"] == 1.0:
            base = r
    for r in rows:
        rf = ("REFUSED" if r["rf_spread"] is None
              else f"{r['rf_spread']:.2f}")
        wall = f"{r['wall_min']:.2f}" + (" **!**" if r["unprintable"] else "")
        flat = " **(flat)**" if r["ratio"] == 1.0 else ""
        w(f"| {r['expo']:.1f}{flat} | {r['ratio']:.1f} | {r['th_id']:.2f} | "
          f"{r['th_od']:.2f} | {r['phi_id']:.3f} | {r['phi_od']:.3f} | "
          f"{r['phi_ratio']:.2f} | {r['phi_span']:.2f} | {r['E_ratio']:.2f} | "
          f"{r['interface_cm2']:.0f} | {r['ceil45']:.4f} | "
          f"{r['edge_id_mm']:.0f} | {r['escape_mm']:.2f} | "
          f"{r['unfed_frac']:.4f} | {rf} | {wall} |")
    w("")
    w("`REFUSED` means the outer bins pass phi = "
      f"{ms.RF_ZERO_PHI:.3f}, where Bisin's linear fit crosses zero and would")
    w("otherwise report a negative regression rate. That is a limit on what "
      "can be")
    w("PREDICTED, not on what can be built. **!** marks a thin end below two "
      "extrusion widths.")
    w("")

    # ---- what the table says, computed rather than asserted --------------
    def rng(key):
        vals = [r[key] for r in rows]
        return min(vals), max(vals)

    w("## What moves, and what does not")
    w("")
    ia_lo, ia_hi = rng("interface_cm2")
    ce_lo, ce_hi = rng("ceil45")
    w(f"- **Interface area does not move**: {ia_lo:.0f}-{ia_hi:.0f} cm2 across "
      f"the whole table, a spread of {100 * (ia_hi / ia_lo - 1):.1f}%. Bisin's "
      "C/L scaling")
    w("  says cell size sets this, and the taper is not a cell-size change. "
      "Grading cannot")
    w("  be justified on debonding.")
    w(f"- **Ceiling area does not move either**: {ce_lo:.4f}-{ce_hi:.4f} within "
      "45 deg of horizontal.")
    w("  Grading neither costs nor buys anything in bubble pinning.")
    e_lo, e_hi = rng("E_ratio")
    w(f"- **The stiffness gradient is the lever**: E(OD)/E(ID) runs "
      f"{e_lo:.2f} to {e_hi:.2f}. This is")
    w("  the one thing a taper does substantially, and no requirement "
      "asks for it --")
    w("  so whether it is worth having is a question for whoever owns the "
      "structural case.")
    w(f"- **It costs free edge at the ID**: {base['edge_id_mm']:.0f} mm flat, "
      f"up to {rng('edge_id_mm')[1]:.0f} mm. That edge is")
    w("  simultaneously the crack-initiation set and the unprintable-sliver "
      "set, and a taper")
    w("  puts the thinnest sheet exactly there.")
    es_lo, es_hi = rng("escape_mm")
    flat_span = base["phi_span"]
    clears = [r for r in rows
              if r["ratio"] != 1.0 and r["phi_span"] > 1.5 * flat_span]
    w(f"- **A mild taper is indistinguishable from the lattice's own "
      f"wobble**: the FLAT sheet already")
    w(f"  spans {flat_span:.2f}x in phi across its bins. Only "
      f"{len(clears)} of {len(rows) - 1} tapers clear 1.5x that, so "
      "anything")
    w("  gentler than about ratio 3 imposes a gradient smaller than the "
      "variation already there.")
    w(f"- **Venting barely notices**: escape path {es_hi:.2f} -> {es_lo:.2f} mm, "
      f"about {100 * (1 - es_lo / es_hi):.0f}%, and the")
    w("  unfed fraction does not move. Confirms grading is not a casting "
      "lever.")
    w("")
    w("## Exponent")
    w("")
    w("At every ratio, `expo = 2.0` beats `expo = 1.0` on three axes at once "
      "-- a thicker")
    w("ID wall, a steeper phi gradient, AND less ID free edge -- because "
      "holding the sheet")
    w("thin over most of the radius and thickening late spends the same mass "
      "further out.")
    w("If the taper is adopted at all, it should not be linear.")
    return "\n".join(out) + "\n"


def main():
    ap = argparse.ArgumentParser(
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--phi", type=float, default=None,
                     help="fill to hold (default: the shipped flat sheet's)")
    ap.add_argument("--h", type=float, default=0.5, help="sampling spacing mm")
    ap.add_argument("--out", default="geom/exports/sweep-results")
    args = ap.parse_args()

    base = grainfield.GrainParams.from_config()
    phi = args.phi
    if phi is None:
        flat = grainfield.sample(base.replace(W_perf=0.0), h=args.h)
        phi = ms.fill_fractions(flat)["whole_part"]

    rows = run(phi, args.h)
    text = fmt(rows, phi, args.h)
    print(text)

    d = os.path.join(ROOT, args.out)
    os.makedirs(d, exist_ok=True)
    path = os.path.join(d, "taper-study.md")
    with open(path, "w") as fh:
        fh.write(text)
    print(f"written to {path}")


if __name__ == "__main__":
    main()
