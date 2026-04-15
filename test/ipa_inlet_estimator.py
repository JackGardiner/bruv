"""
IPA Inlet Radius Estimator
==========================
Fits empirical mdot vs inlet_radius data from all RS samples (IPA / Stage 2 = Flow1)
and finds the inlet radius needed to hit a target MFR.

Outputs:
  - analysis_outputs/ipa_inlet_estimate.txt  — text summary
  - analysis_outputs/ipa_inlet_estimate.png  — annotated plot
"""

from pathlib import Path

import matplotlib.pyplot as plt
import matplotlib.ticker as ticker
import numpy as np
import pandas as pd

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------
COLLATED_CSV    = "analysis_outputs/steady_collated_both.csv"
GUIDANCE_CSV    = "analysis_outputs/resin_copper_guidance.csv"
OUTPUT_DIR      = Path("analysis_outputs")
FIT_DEGREE      = 2          # polynomial degree for empirical fit
N_ELEMENTS      = 12         # injector elements (for per-element target)

# Target resin mdot per element (kg/s).
# Set to None to auto-load from resin_copper_guidance.csv.
TARGET_RESIN_MDOT_PER_ELEM_KGS = None

# Inlet count conversion: compute the equivalent radius for a different
# number of inlets while keeping total inlet area constant.
# Set INLET_COUNT_NEW = None to skip.
INLET_COUNT_ORIGINAL = 4    # inlets used in test samples (from reports)
INLET_COUNT_NEW      = 3    # target inlet count for the new design

# Which RS families to include in the fit.
# 'all'  — use every RS sample
# 'sweep' — only RS8+ (K_extra sweep series, consistent nozzle geometry)
FIT_POPULATION = "all"

# Colour scheme (matches main analysis)
COLOR_FAM1  = "tab:orange"    # RS0–7   Family 1
COLOR_FAM2  = "tab:green"     # RS8–12  Family 2
COLOR_FAM3  = "tab:purple"    # RS13+   Family 3
COLOR_FIT   = "tab:blue"
COLOR_TGT   = "crimson"

NEW_SAMPLE_INDEX_MIN = 8
NEW_ROUND_INDEX_MIN  = 13

# ---------------------------------------------------------------------------
# Load data
# ---------------------------------------------------------------------------
collated = pd.read_csv(COLLATED_CSV)

# Numeric coerce key columns
for col in ["stage2_inlet_radius_mm", "Flow1_kgs_mean", "sample_index"]:
    if col in collated.columns:
        collated[col] = pd.to_numeric(collated[col], errors="coerce")

# Separate S and RS
rs = collated[collated["sample_type"] == "RS"].copy().dropna(
    subset=["stage2_inlet_radius_mm", "Flow1_kgs_mean"]
)
s  = collated[collated["sample_type"] == "S"].copy().dropna(
    subset=["stage2_inlet_radius_mm", "Flow1_kgs_mean"]
)

# RS families
fam1 = rs[rs["sample_index"] < NEW_SAMPLE_INDEX_MIN]
fam2 = rs[(rs["sample_index"] >= NEW_SAMPLE_INDEX_MIN) & (rs["sample_index"] < NEW_ROUND_INDEX_MIN)]
fam3 = rs[rs["sample_index"] >= NEW_ROUND_INDEX_MIN]

# Population used for the fit
if FIT_POPULATION == "sweep":
    fit_data = rs[rs["sample_index"] >= NEW_SAMPLE_INDEX_MIN].copy()
    fit_label = f"RS{NEW_SAMPLE_INDEX_MIN}+ (sweep series)"
else:
    fit_data = rs.copy()
    fit_label = "All RS"

x_fit = fit_data["stage2_inlet_radius_mm"].values
y_fit = fit_data["Flow1_kgs_mean"].values * 1e3   # → g/s

# ---------------------------------------------------------------------------
# Determine target
# ---------------------------------------------------------------------------
if TARGET_RESIN_MDOT_PER_ELEM_KGS is not None:
    target_gs = TARGET_RESIN_MDOT_PER_ELEM_KGS * 1e3
    target_source = "user-specified"
else:
    guidance = pd.read_csv(GUIDANCE_CSV)
    row = guidance[guidance["stage"] == "stage2"]
    if row.empty:
        raise RuntimeError(f"stage2 row not found in {GUIDANCE_CSV}")
    resin_gate_total = float(row["resin_gate_mdot_to_hit_copper_target"].iloc[0])
    target_gs        = resin_gate_total / N_ELEMENTS * 1e3
    copper_total_gs  = float(row["target_copper_mdot"].iloc[0]) * 1e3
    transfer_ratio   = float(row["copper_over_resin_multiplier"].iloc[0])
    target_source    = f"auto (from resin_copper_guidance.csv: copper target / transfer ratio / {N_ELEMENTS} elements)"

# ---------------------------------------------------------------------------
# Polynomial fit
# ---------------------------------------------------------------------------
coeffs = np.polyfit(x_fit, y_fit, FIT_DEGREE)
poly   = np.poly1d(coeffs)

# Find intersection: poly(r) = target_gs
roots = (poly - target_gs).roots
# Keep only real roots within a sensible physical range
r_min_data, r_max_data = x_fit.min(), x_fit.max()
margin = (r_max_data - r_min_data) * 0.5
real_roots = [
    r.real for r in roots
    if abs(r.imag) < 1e-8
    and (r_min_data - margin) <= r.real <= (r_max_data + margin)
]

# Choose the root closest to the centre of the data
if real_roots:
    r_centre = (r_min_data + r_max_data) / 2
    best_root = min(real_roots, key=lambda r: abs(r - r_centre))
    root_found = True
else:
    best_root = float("nan")
    root_found = False

# Fit quality
y_pred   = poly(x_fit)
ss_res   = float(np.sum((y_fit - y_pred) ** 2))
ss_tot   = float(np.sum((y_fit - y_fit.mean()) ** 2))
r_squared = 1.0 - ss_res / ss_tot if ss_tot > 0 else float("nan")

# Area-conserved radius for different inlet count
# n_orig * r_orig^2 = n_new * r_new^2  =>  r_new = r_orig * sqrt(n_orig / n_new)
if root_found and INLET_COUNT_NEW is not None and INLET_COUNT_NEW > 0:
    r_area_conserved = best_root * np.sqrt(INLET_COUNT_ORIGINAL / INLET_COUNT_NEW)
    area_conserved_valid = True
else:
    r_area_conserved = float("nan")
    area_conserved_valid = False

# ---------------------------------------------------------------------------
# Plot
# ---------------------------------------------------------------------------
fig, ax = plt.subplots(figsize=(9, 6))

def scatter_family(df, color, marker, label):
    if df.empty:
        return
    xv = df["stage2_inlet_radius_mm"].values
    yv = df["Flow1_kgs_mean"].values * 1e3
    ax.scatter(xv, yv, color=color, marker=marker, s=80, alpha=0.9, zorder=3, label=label)
    for _, row in df.iterrows():
        tag  = f"RS{int(row['sample_index'])}"
        xval = row["stage2_inlet_radius_mm"]
        yval = row["Flow1_kgs_mean"] * 1e3
        if np.isfinite(xval) and np.isfinite(yval):
            ax.annotate(tag, (xval, yval), xytext=(5, 4),
                        textcoords="offset points", fontsize=8, color=color)

scatter_family(fam1, COLOR_FAM1, "o", "RS Family 1 (RS0–7)")
scatter_family(fam2, COLOR_FAM2, "D", "RS Family 2 (RS8–12)")
scatter_family(fam3, COLOR_FAM3, "s", "RS Family 3 (RS13+)")

# Copper samples
if not s.empty:
    xs = s["stage2_inlet_radius_mm"].values
    ys = s["Flow1_kgs_mean"].values * 1e3
    ax.scatter(xs, ys, color="tab:blue", marker="^", s=80, alpha=0.9, zorder=3, label="Copper (S)")
    for _, row in s.iterrows():
        tag  = f"S{int(row['sample_index'])}"
        xval = row["stage2_inlet_radius_mm"]
        yval = row["Flow1_kgs_mean"] * 1e3
        if np.isfinite(xval) and np.isfinite(yval):
            ax.annotate(tag, (xval, yval), xytext=(5, 4),
                        textcoords="offset points", fontsize=8, color="tab:blue")

# Fit curve
x_dense = np.linspace(
    max(0.3, r_min_data - margin * 0.4),
    r_max_data + margin * 0.4,
    400,
)
y_dense = poly(x_dense)
ax.plot(x_dense, y_dense, color=COLOR_FIT, linewidth=2,
        label=f"Poly fit deg-{FIT_DEGREE} ({fit_label})  R²={r_squared:.4f}")

# Target line
ax.axhline(target_gs, color=COLOR_TGT, linewidth=1.6, linestyle="--",
           label=f"Target  {target_gs:.2f} g/s / element")

# Intersection — 4-inlet design
COLOR_CONV = "tab:brown"
if root_found:
    ax.axvline(best_root, color=COLOR_TGT, linewidth=1.2, linestyle=":",
               label=f"{INLET_COUNT_ORIGINAL}-inlet estimate  r = {best_root:.4f} mm")
    ax.plot(best_root, target_gs, "o", color=COLOR_TGT, markersize=10,
            zorder=5, markeredgecolor="black", markeredgewidth=0.8)
    ax.annotate(
        f"  {INLET_COUNT_ORIGINAL} inlets\n  r = {best_root:.4f} mm",
        xy=(best_root, target_gs),
        xytext=(12, -32),
        textcoords="offset points",
        fontsize=9,
        color=COLOR_TGT,
        arrowprops=dict(arrowstyle="-", color=COLOR_TGT, lw=0.9),
    )

# Area-conserved radius for new inlet count
if area_conserved_valid:
    ax.axvline(r_area_conserved, color=COLOR_CONV, linewidth=1.5, linestyle="--",
               label=f"{INLET_COUNT_NEW}-inlet (area-conserved)  r = {r_area_conserved:.4f} mm")
    ax.plot(r_area_conserved, target_gs, "D", color=COLOR_CONV, markersize=10,
            zorder=5, markeredgecolor="black", markeredgewidth=0.8)
    ax.annotate(
        f"  {INLET_COUNT_NEW} inlets (area-conserved)\n  r = {r_area_conserved:.4f} mm",
        xy=(r_area_conserved, target_gs),
        xytext=(12, 10),
        textcoords="offset points",
        fontsize=9,
        color=COLOR_CONV,
        arrowprops=dict(arrowstyle="-", color=COLOR_CONV, lw=0.9),
    )

ax.set_xlabel("Stage 2 (IPA) inlet radius (mm)", fontsize=11)
ax.set_ylabel("mdot_exp — IPA (g/s per element)", fontsize=11)
ax.set_title("IPA Inlet Radius Estimator\nEmpirical fit: measured mdot vs nozzle inlet radius", fontsize=12)
ax.grid(True, alpha=0.3)
ax.legend(fontsize=8.5)
ax.xaxis.set_minor_locator(ticker.AutoMinorLocator())
ax.yaxis.set_minor_locator(ticker.AutoMinorLocator())

fig.tight_layout()
out_png = OUTPUT_DIR / "ipa_inlet_estimate.png"
fig.savefig(out_png, dpi=150)
plt.close(fig)
print(f"Saved plot: {out_png}")

# ---------------------------------------------------------------------------
# Text summary
# ---------------------------------------------------------------------------
poly_str = "  mdot (g/s) = " + " + ".join(
    f"({c:.6g}) × r^{FIT_DEGREE - i}" if (FIT_DEGREE - i) > 0 else f"({c:.6g})"
    for i, c in enumerate(coeffs)
)

lines = [
    "IPA Inlet Radius Estimator",
    "=" * 60,
    "",
    f"Target MFR per element : {target_gs:.4f} g/s  ({target_gs/1e3:.6f} kg/s)",
    f"Target source          : {target_source}",
    f"Number of elements     : {N_ELEMENTS}",
    f"Total resin target     : {target_gs * N_ELEMENTS:.2f} g/s",
    "",
    "--- Empirical Fit ---",
    f"Population             : {fit_label}  (n={len(x_fit)})",
    f"Fit degree             : {FIT_DEGREE}",
    f"R²                     : {r_squared:.5f}",
    poly_str,
    "",
    "--- Data range ---",
    f"Inlet radius range     : {r_min_data:.4f} – {r_max_data:.4f} mm",
    f"mdot range (fit data)  : {y_fit.min():.2f} – {y_fit.max():.2f} g/s",
    "",
    "--- Intersection ---",
]

if root_found:
    in_range = r_min_data <= best_root <= r_max_data
    lines += [
        f"Basis ({INLET_COUNT_ORIGINAL} inlets)",
        f"  Estimated inlet radius : {best_root:.4f} mm",
        f"  Within data range      : {'YES' if in_range else 'NO — extrapolation'}",
    ]
    if not in_range:
        lines.append("  ** Extrapolation: treat estimate with caution **")
else:
    lines.append("No real root found in physical range — target may be outside fit.")

if area_conserved_valid:
    # radii are in mm, so areas are in mm²
    total_area_orig = INLET_COUNT_ORIGINAL * np.pi * best_root**2
    total_area_new  = INLET_COUNT_NEW      * np.pi * r_area_conserved**2
    in_range_conv   = r_min_data <= r_area_conserved <= r_max_data
    lines += [
        "",
        f"--- Inlet Count Conversion ({INLET_COUNT_ORIGINAL} → {INLET_COUNT_NEW} inlets, constant total area) ---",
        f"  Formula                : r_new = r_orig × sqrt(n_orig / n_new)",
        f"                         = {best_root:.4f} × sqrt({INLET_COUNT_ORIGINAL}/{INLET_COUNT_NEW})",
        f"                         = {best_root:.4f} × {np.sqrt(INLET_COUNT_ORIGINAL/INLET_COUNT_NEW):.6f}",
        f"  New inlet radius       : {r_area_conserved:.4f} mm",
        f"  Total inlet area check : orig = {total_area_orig:.5f} mm²,  new = {total_area_new:.5f} mm²",
        f"  Within data range      : {'YES' if in_range_conv else 'NO — extrapolation'}",
    ]
    if not in_range_conv:
        lines.append("  ** Extrapolation: treat estimate with caution **")

lines += [
    "",
    "--- All RS sample points (IPA) ---",
    f"{'Sample':<10} {'Inlet r (mm)':<16} {'mdot_exp (g/s)':<18} {'Family'}",
    "-" * 58,
]
for _, row in rs.sort_values("sample_index").iterrows():
    si   = int(row["sample_index"])
    r_v  = row["stage2_inlet_radius_mm"]
    md_v = row["Flow1_kgs_mean"] * 1e3
    if si < NEW_SAMPLE_INDEX_MIN:
        fam = "1"
    elif si < NEW_ROUND_INDEX_MIN:
        fam = "2"
    else:
        fam = "3"
    lines.append(f"RS{si:<8} {r_v:<16.4f} {md_v:<18.3f} {fam}")

out_txt = OUTPUT_DIR / "ipa_inlet_estimate.txt"
out_txt.write_text("\n".join(lines), encoding="utf-8")
print(f"Saved summary: {out_txt}")
