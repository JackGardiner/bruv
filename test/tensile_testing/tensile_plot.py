"""
tensile_plot.py
===============
Visualisation helper for tensile test data organised by build orientation (X / Y / Z),
3 repetitions each.

Usage
-----
from tensile_plot import plot_tensile_results

# Provide raw arrays (SI units: Pa for stress, dimensionless for strain, Pa for E)
plot_tensile_results(
    stress   = {"X": [arr1, arr2, arr3], "Y": [...], "Z": [...]},
    strain   = {"X": [arr1, arr2, arr3], "Y": [...], "Z": [...]},
    E_manual = {"X": [E1, E2, E3],       "Y": [...], "Z": [...]},   # Pa
)
"""

import numpy as np
import matplotlib.pyplot as plt
import matplotlib.gridspec as gridspec
from matplotlib.lines import Line2D
from scipy.interpolate import interp1d

# ── Palette ──────────────────────────────────────────────────────────────────
# One hue family per orientation; light tint = individual run, deep = mean.
PALETTE = {
    "X": {"runs": ["#f4a582", "#d6604d", "#a50026"],
          "mean": "#67001f",
        #   "elastic": "#b52c22",
          "elastic": "#d6604d",
          "fill": "#f4a582"},
    "Y": {"runs": ["#74add1", "#4393c3", "#2166ac"],
          "mean": "#053061",
        #   "elastic": "#2166ac",
          "elastic": "#4393c3",
          "fill": "#74add1"},
    "Z": {"runs": ["#a1d99b", "#41ab5d", "#006d2c"],
          "mean": "#00441b",
        #   "elastic": "#0c7d39",
          "elastic": "#41ab5d",
          "fill": "#a1d99b"},
}

MPa = 1e-6   # Pa → MPa
GPa = 1e-9   # Pa → GPa


# ── Core mechanics ────────────────────────────────────────────────────────────

def _uts(stress: np.ndarray) -> float:
    """Ultimate tensile strength = peak stress (Pa)."""
    return float(np.max(stress))


def _yield_strength_02(stress: np.ndarray, strain: np.ndarray,
                        E: float) -> float:
    """
    0.2 % offset yield strength (Pa).
    Constructs the offset line  σ = E·(ε − 0.002)  and finds its intersection
    with the stress–strain curve.
    """
    offset_line = E * (strain - 0.002)
    # Find first crossing: stress - offset_line changes sign from negative to positive
    diff = stress - offset_line
    sign_changes = np.where(np.diff(np.sign(diff)) > 0)[0]
    if len(sign_changes) == 0:
        # Fallback: return stress at 0.2 % strain (or closest point)
        idx = np.argmin(np.abs(strain - 0.002))
        return float(stress[idx])
    i = sign_changes[0]
    # Linear interpolation between i and i+1
    if diff[i + 1] != diff[i]:
        frac = -diff[i] / (diff[i + 1] - diff[i])
    else:
        frac = 0.0
    return float(stress[i] + frac * (stress[i + 1] - stress[i]))


def _elastic_modulus_fit(stress: np.ndarray, strain: np.ndarray,
                          strain_max: float = 0.01) -> float:
    """
    Fit E from the linear elastic region (strain ≤ strain_max) via least-squares.
    Falls back to the full curve if there are fewer than 3 points in range.
    """
    # mask = strain <= strain_max
    # if mask.sum() < 3:
    #     mask = np.ones(len(strain), dtype=bool)
    # coeffs = np.polyfit(strain[mask], stress[mask], 1)
    # return float(coeffs[0])
    strain_mid = 3.5e-2
    mask = (strain_mid - 0.2e-2 <= strain) & (strain <= strain_mid + 0.2e-2)
    return float(stress[mask].sum() / len(stress[mask])) / strain_mid


def _compute_stats(values: list[float]) -> dict:
    arr = np.array(values)
    return {"mean": arr.mean(), "std": arr.std(ddof=1), "vals": arr}


def _elongation_at_fracture(stress: np.ndarray, strain: np.ndarray) -> float:
    """Strain at maximum stress (approximation of fracture strain)."""
    # return float(strain[np.argmax(stress)])
    return float(strain.max())


# ── Main plotting function ────────────────────────────────────────────────────

def plot_tensile_results(
    stress:   dict[str, list[np.ndarray]],
    strain:   dict[str, list[np.ndarray]],
    E_manual: dict[str, list[float]] | None = None,
    title:    str = "Tensile Test Results",
    save_path: str | None = None,
) -> dict:
    """
    Plot tensile test data for up to three build orientations with three
    repetitions each.

    Parameters
    ----------
    stress    : {"X": [σ₁, σ₂, σ₃], "Y": ..., "Z": ...}   arrays in Pa
    strain    : {"X": [ε₁, ε₂, ε₃], "Y": ..., "Z": ...}   dimensionless
    E_manual  : {"X": [E₁, E₂, E₃], "Y": ..., "Z": ...}   manually estimated
                Young's moduli in Pa.  If None, E is fitted from the data.
    title     : overall figure title
    save_path : if given, save figure to this path (e.g. "result.png")

    Returns
    -------
    stats : nested dict with computed metrics per orientation
    """
    orientations = list(stress.keys())

    # ── Compute metrics ───────────────────────────────────────────────────────
    stats = {}
    for ori in orientations:
        uts_vals, ys_vals, E_vals, elong_vals = [], [], [], []
        for i, (sig, eps) in enumerate(zip(stress[ori], strain[ori])):
            sig, eps = np.asarray(sig, float), np.asarray(eps, float)
            # Sort by strain (safety)
            order = np.argsort(eps)
            sig, eps = sig[order], eps[order]

            E_use = (E_manual[ori][i] if E_manual is not None
                     else _elastic_modulus_fit(sig, eps))
            E_vals.append(E_use)
            uts_vals.append(_uts(sig))
            ys_vals.append(_yield_strength_02(sig, eps, E_use))
            elong_vals.append(_elongation_at_fracture(sig, eps))

        stats[ori] = {
            "E":    _compute_stats(E_vals),
            "UTS":  _compute_stats(uts_vals),
            "YS":   _compute_stats(ys_vals),
            "elong": _compute_stats(elong_vals),
        }

    # ── Figure layout ─────────────────────────────────────────────────────────
    fig = plt.figure(figsize=(15, 10))
    fig.suptitle(title, fontsize=18, fontweight="bold", color="#1a1a2e",
                 y=0.98)#, fontfamily="serif")

    gs_top = gridspec.GridSpec(1, 3, figure=fig,
                               top=0.91, bottom=0.55,
                               left=0.06, right=0.97,
                               wspace=0.28)
    gs_bot = gridspec.GridSpec(1, 3, figure=fig,
                               top=0.46, bottom=0.08,
                               left=0.06, right=0.97,
                               wspace=0.38)

    # ── Row 1: individual stress–strain curves per orientation ────────────────
    ax_ss = []
    for col, ori in enumerate(orientations):
        ax = fig.add_subplot(gs_top[0, col])
        ax.set_xlim([-0.5, 17])
        ax.set_ylim([-20, 600])
        pal = PALETTE[ori]
        sig_all, eps_all = [], []

        # elastic_legend_added = False
        for i, (sig, eps) in enumerate(zip(stress[ori], strain[ori])):
            sig, eps = np.asarray(sig, float), np.asarray(eps, float)
            order = np.argsort(eps)
            sig, eps = sig[order], eps[order]
            sig_all.append(sig); eps_all.append(eps)

            ax.plot(eps * 100, sig * MPa,
                    color=pal["runs"][i], lw=1.5, alpha=0.75,
                    label=f"Run {i+1}")

            # # Dotted elastic model line: σ = E·ε, up to the yield point
            # E_i = (E_manual[ori][i] if E_manual is not None
            #        else _elastic_modulus_fit(sig, eps))
            # ys_i = _yield_strength_02(sig, eps, E_i)
            # eps_y_i = ys_i / E_i          # strain at yield on the model line
            # eps_elastic = np.linspace(0, eps_y_i * 1.15, 120)  # slight overshoot
            # sig_elastic = E_i * eps_elastic
            # label_elastic = "E model" if not elastic_legend_added else "_nolegend_"
            # elastic_legend_added = True
            # ax.plot(eps_elastic * 100, sig_elastic * MPa,
            #         color="#444444", lw=1.2, ls=":", alpha=0.55,
            #         zorder=6, label=label_elastic)


        # Mean curve via common strain grid
        eps_common = np.linspace(
            max(e.min() for e in eps_all),
            min(e.max() for e in eps_all),
            500
        )
        sig_interp = np.array([
            interp1d(eps_all[i], sig_all[i], bounds_error=False,
                     fill_value=np.nan)(eps_common)
            for i in range(len(sig_all))
        ])
        sig_mean = np.nanmean(sig_interp, axis=0)
        sig_std  = np.nanstd(sig_interp, axis=0, ddof=1)

        ax.plot(eps_common * 100, sig_mean * MPa,
                color=pal["mean"], lw=2.5, zorder=5, label="Mean")
        ax.fill_between(eps_common * 100,
                         (sig_mean - sig_std) * MPa,
                         (sig_mean + sig_std) * MPa,
                         color=pal["fill"], alpha=0.25, zorder=4)

        # Markers: YS and UTS from stats
        ys_m  = stats[ori]["YS"]["mean"]  * MPa
        uts_m = stats[ori]["UTS"]["mean"] * MPa
        # ax.axhline(ys_m,  color=pal["mean"], lw=1.2, ls=":", alpha=0.6)
        ax.axhline(uts_m, color=pal["mean"], lw=1.2, ls="--",  alpha=0.6)
        # ax.text(ax.get_xlim()[1] if ax.get_xlim()[1] > 0 else 1,
        #         ys_m + 0.5, f"YS", fontsize=9.0, weight="heavy",
        #         color=pal["mean"], ha="right", va="bottom")
        ax.text(ax.get_xlim()[1] - 0.5,
                uts_m + 0.5, f"UTS", fontsize=9.0, weight="heavy",
                color=pal["mean"], ha="right", va="bottom")

        _style_ax(ax, f"Build orientation: {ori}",
                  "Strain [%]", "Stress [MPa]")
        ax.legend(fontsize=8, framealpha=1.0, loc="upper left")
        ax_ss.append(ax)

    # ── Row 2: bar charts for YS, UTS, E, elongation ─────────────────────────
    metrics = [
        ("UTS",   "Ultimate Tensile Strength",      "MPa", MPa,
                {"X": 340e6, "Y": 340e6, "Z": 295e6}),
        # ("YS",    "Yield Strength (0.2% offset)",   "MPa", MPa),
        # ("E",     "Young's Modulus",                "GPa", GPa),
        ("elong", "Elongation at Fracture",         "%",   100,
                {"X": 0.27, "Y": 0.27, "Z": 0.29}),
    ]

    for col, (key, label, unit, scale, expected) in enumerate(metrics):
        ax = fig.add_subplot(gs_bot[0, col])
        _bar_chart(ax, orientations, stats, key, label, unit, scale,expected)

    ax = fig.add_subplot(gs_bot[0, 2])
    strain_limit_pct = 5.0
    # strain_limit_pct = 1.5
    for ori, pal in PALETTE.items():
        if ori not in stress:
            continue

        sig_interp_list = []
        eps_common = None

        for i, (sig, eps) in enumerate(zip(stress[ori], strain[ori])):
            sig = np.asarray(sig, float)
            eps = np.asarray(eps, float)
            order = np.argsort(eps)
            sig, eps = sig[order], eps[order]

            # Clip to elastic window
            mask = eps <= (strain_limit_pct * 1.1 / 100)
            sig, eps = sig[mask], eps[mask]

            if eps_common is None:
                eps_common = np.linspace(eps.min(), eps.max(), 300)

            sig_interp_list.append(
                interp1d(eps, sig, bounds_error=False, fill_value=np.nan)(eps_common)
            )

        sig_mean = np.nanmean(sig_interp_list, axis=0)

        # Mean measured curve
        ax.plot(eps_common * 100, sig_mean * MPa,
                color=pal["elastic"], lw=2.0, label=f"{ori} - mean")

        # # E model line for mean E
        # E_vals = [E_manual[ori][i] if E_manual else _elastic_modulus_fit(
        #               np.asarray(stress[ori][i]), np.asarray(strain[ori][i]))
        #           for i in range(len(stress[ori]))]
        # E_mean = np.mean(E_vals)
    ax.plot(eps_common * 100, 100e9 * eps_common * MPa,
            color="black", lw=1.2, ls="--", alpha=0.8,
            label="Expected E = 100 GPa")
    ax.plot(eps_common * 100, 20e9 * eps_common * MPa,
            color="grey", lw=1.2, ls="--", alpha=0.8,
            label="Approx. fit (?) E = 20 GPa")


    ax.set_xlim(-0.2, strain_limit_pct)
    ax.set_ylim(-20, 600)
    ax.grid(color="#ddd", lw=0.7)
    _style_ax(ax, "Elastic Region", "Strain [%]", "Stress [MPa]")
    # ax.set_title(, fontsize=11, fontweight="bold")
    # ax.spines[["top", "right"]].set_visible(False)
    ax.legend(fontsize=9, framealpha=1.0)


    if save_path:
        fig.savefig(save_path, dpi=180, bbox_inches="tight",
                    facecolor=fig.get_facecolor())
        print(f"\nFigure saved → {save_path}")

    plt.tight_layout(rect=[0, 0, 1, 0.97])
    plt.show()
    return stats


# ── Helper: bar chart with error bars ────────────────────────────────────────

def _bar_chart(ax, orientations, stats, key, title, unit, scale, expected=None):
    x = np.arange(len(orientations))
    bar_w = 0.55

    for xi, ori in enumerate(orientations):
        pal  = PALETTE[ori]
        mean = stats[ori][key]["mean"] * scale
        std  = stats[ori][key]["std"]  * scale
        vals = stats[ori][key]["vals"] * scale

        ax.bar(xi, mean, width=bar_w,
               color=pal["fill"], edgecolor=pal["mean"],
               linewidth=1.4, zorder=3)
        ax.errorbar(xi, mean, yerr=std,
                    fmt="none", color=pal["mean"],
                    capsize=6, capthick=1.8, elinewidth=1.8, zorder=4)
        # Scatter individual values
        jitter = np.linspace(-bar_w * 0.25, bar_w * 0.25, len(vals))
        ax.scatter(xi + jitter, vals,
                   color=pal["mean"], s=28, zorder=5, alpha=0.85)

        # ── Expected reference line + % diff ─────────────────────────────────
        xtra = ""
        if expected is not None and ori in expected:
            ref = expected[ori] * scale
            ax.plot([xi - bar_w * 0.6, xi + bar_w * 0.6], [ref, ref],
                    color="#222", lw=1.6, ls="--", zorder=6)
            pct = (mean - ref) / ref * 100
            sign = "+" if pct >= 0 else ""
            # Place label just outside the right edge of the bar
            xtra = f"\n({sign}{pct:.0f}%)"
            # ax.text(xi + bar_w * 0.62, ref,
            #         f"{sign}{pct:.1f}%",
            #         ha="left", va="center", fontsize=8,
            #         color="#222", fontstyle="italic")

        # Annotate mean ± std
        ax.text(xi, mean + std + (mean * 0.02 + 0.5 + mean * 0.02 * (expected is not None)),
                f"{mean:.1f}±{std:.1f}{xtra}",
                ha="center", va="bottom", fontsize=8,
                color=pal["mean"], fontweight="bold",
                bbox=dict(
                    boxstyle="round", fc="white", alpha=0.8,
                )
        )


    # Legend entry for the reference line (only if used)
    if expected is not None:
        ax.plot([], [], color="#222", lw=1.6, ls="--", label="Expected")
        ax.legend(fontsize=8, framealpha=0.6, loc="upper right")

    ax.set_xticks(x)
    ax.set_xticklabels(orientations, fontsize=11, fontweight="bold")
    _style_ax(ax, title, "Build orientation", f"{unit}")
    ax.set_xlim(-0.6, len(orientations) - 0.4)
    # Expand y to fit annotations and % diff labels
    ymin, ymax = ax.get_ylim()
    ax.set_ylim(max(0, ymin - ymax * 0.05), ymax * 1.22)



# ── Helper: axis styling ──────────────────────────────────────────────────────

def _style_ax(ax, title, xlabel, ylabel):
    # ax.set_facecolor("#fefefe")
    ax.set_title(title, fontsize=10, fontweight="bold",
                 color="#1a1a2e", pad=8)#, fontfamily="serif")
    ax.set_xlabel(xlabel, fontsize=9, color="#444")
    ax.set_ylabel(ylabel, fontsize=9, color="#444")
    ax.tick_params(colors="#555", labelsize=8.5)
    ax.spines[["top", "right"]].set_visible(False)
    ax.spines[["left", "bottom"]].set_color("#bbb")
    ax.minorticks_on()

    ax.grid(True, axis="both", which="major", lw=1.0, ls="-", alpha=0.8)
    ax.grid(True, axis="both", which="minor", lw=0.6, ls="--", alpha=0.5)


    # ax.grid(axis="y", color="#ddd", linewidth=0.7, zorder=0)
    # ax.grid(axis="x", visible=False)
