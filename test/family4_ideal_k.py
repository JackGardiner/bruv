"""
Family 4 ideal K estimator.

This script estimates ideal K_extra using only Family 4 resin samples (RS18+)
from analysis_outputs/steady_collated_both.csv.

Method:
- Build k_mdot = mdot_exp / mdot_theory for each stage.
- Fit k_mdot = m * K_extra + b using Family 4 points only.
- Solve k_mdot = 1 for ideal K_extra.
- Also compute a paired optimum K_extra that balances both stages.

Outputs:
- analysis_outputs/family4_ideal_k.csv
- analysis_outputs/family4_ideal_k.txt
"""

from __future__ import annotations

from pathlib import Path

import numpy as np
import pandas as pd


BASE_DIR = Path(__file__).resolve().parent
COLLATED_CSV = BASE_DIR / "analysis_outputs" / "steady_collated_both.csv"
OUTPUT_DIR = BASE_DIR / "analysis_outputs"
OUTPUT_CSV = OUTPUT_DIR / "family4_ideal_k.csv"
OUTPUT_TXT = OUTPUT_DIR / "family4_ideal_k.txt"

NEW_FAMILY4_INDEX_MIN = 18

STAGE_CFG: dict[str, dict[str, str]] = {
    "stage2": {
        "stage_name": "Stage 2 (IPA)",
        "exp_col": "Flow1_kgs_mean",
        "theory_col": "stage2_mass_flow_rate_kgps",
        "corr_col": "empirical_ipa_correction_factor",
        "corr_name": "IPA correction factor",
    },
    "stage1": {
        "stage_name": "Stage 1 (LOX)",
        "exp_col": "Flow2_kgs_mean",
        "theory_col": "stage1_mass_flow_rate_kgps",
        "corr_col": "empirical_lox_correction_factor",
        "corr_name": "LOX correction factor",
    },
}

KEXTRA_COL = "empirical_additional_correction_factor"


def _coerce_numeric(work: pd.DataFrame, cols: list[str]) -> None:
    for col in cols:
        if col in work.columns:
            work[col] = pd.to_numeric(work[col], errors="coerce")


def _fit_ideal_kextra(df: pd.DataFrame, k_col: str, y_col: str) -> tuple[float, float, float, float, float]:
    """
    Returns (slope, intercept, ideal_kextra, fit_rmse, ideal_kextra_unc).
    """
    fit_df = df[[k_col, y_col]].dropna().copy()
    if len(fit_df) < 2:
        return np.nan, np.nan, np.nan, np.nan, np.nan

    x = fit_df[k_col].to_numpy(dtype=float)
    y = fit_df[y_col].to_numpy(dtype=float)
    slope, intercept = np.polyfit(x, y, 1)

    if abs(slope) < 1e-12:
        return float(slope), float(intercept), np.nan, np.nan, np.nan

    ideal_k = (1.0 - intercept) / slope

    y_hat = slope * x + intercept
    residuals = y - y_hat
    rmse = float(np.sqrt(np.mean(residuals ** 2)))
    ideal_k_unc = rmse / abs(float(slope)) if len(fit_df) >= 3 else np.nan

    return float(slope), float(intercept), float(ideal_k), rmse, float(ideal_k_unc)


def _paired_optimum(stage_fits: dict[str, dict[str, float]], k_min: float, k_max: float) -> float:
    """
    Minimize combined squared percent error from k_mdot=1 for both stages.
    """
    if not stage_fits:
        return np.nan

    grid = np.linspace(k_min, k_max, 2000)
    combined = np.zeros_like(grid)
    for fit in stage_fits.values():
        m = fit["slope"]
        b = fit["intercept"]
        if pd.isna(m) or pd.isna(b):
            return np.nan
        pred = m * grid + b
        # Percent error relative to 1.0 target k_mdot
        combined += ((pred - 1.0) / 1.0 * 100.0) ** 2

    best_idx = int(np.argmin(combined))
    return float(grid[best_idx])


def main() -> None:
    if not COLLATED_CSV.exists():
        raise FileNotFoundError(f"Missing input file: {COLLATED_CSV}")

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    work = pd.read_csv(COLLATED_CSV)

    required = ["sample_type", "sample_index", "sample_id", KEXTRA_COL]
    for cfg in STAGE_CFG.values():
        required.extend([cfg["exp_col"], cfg["theory_col"], cfg["corr_col"]])

    missing = [c for c in required if c not in work.columns]
    if missing:
        raise KeyError(f"Missing required columns: {missing}")

    _coerce_numeric(work, ["sample_index", KEXTRA_COL] + [
        c for cfg in STAGE_CFG.values() for c in [cfg["exp_col"], cfg["theory_col"], cfg["corr_col"]]
    ])

    fam4 = work[
        (work["sample_type"] == "RS")
        & (work["sample_index"] >= NEW_FAMILY4_INDEX_MIN)
    ].copy()

    fam4 = fam4.dropna(subset=[KEXTRA_COL])
    fam4 = fam4.sort_values(["sample_index", "sample_id"])

    if fam4.empty:
        raise RuntimeError("No Family 4 RS data found (expected RS18+ with K_extra).")

    fit_rows: list[dict[str, float | str]] = []
    stage_fits: dict[str, dict[str, float]] = {}

    lines: list[str] = []
    lines.append("=" * 90)
    lines.append("FAMILY 4 IDEAL K ESTIMATE (RS18+ ONLY)")
    lines.append("=" * 90)
    lines.append("")
    lines.append("Data scope:")
    lines.append(f"  Input file: {COLLATED_CSV}")
    lines.append(f"  Family 4 filter: sample_type=RS and sample_index >= {NEW_FAMILY4_INDEX_MIN}")
    lines.append(
        "  RS samples used: "
        + ", ".join(fam4["sample_id"].astype(str).tolist())
    )
    lines.append("")
    lines.append("Fit model per stage: k_mdot = slope * K_extra + intercept")
    lines.append("Ideal K_extra is where k_mdot = 1.")
    lines.append("")

    for stage_key, cfg in STAGE_CFG.items():
        stage_name = cfg["stage_name"]
        exp_col = cfg["exp_col"]
        theory_col = cfg["theory_col"]
        corr_col = cfg["corr_col"]
        corr_name = cfg["corr_name"]

        stage_df = fam4[["sample_id", KEXTRA_COL, exp_col, theory_col, corr_col]].copy()
        stage_df["k_mdot"] = stage_df[exp_col] / stage_df[theory_col].replace(0, np.nan)
        stage_df = stage_df.dropna(subset=["k_mdot", KEXTRA_COL])

        slope, intercept, ideal_k, fit_rmse, ideal_k_unc = _fit_ideal_kextra(stage_df, KEXTRA_COL, "k_mdot")

        corr_vals = pd.to_numeric(stage_df[corr_col], errors="coerce").dropna()
        corr_old_mean = float(corr_vals.mean()) if not corr_vals.empty else np.nan
        corr_new = corr_old_mean * ideal_k if pd.notna(corr_old_mean) and pd.notna(ideal_k) else np.nan

        stage_fits[stage_key] = {
            "slope": slope,
            "intercept": intercept,
            "ideal_k": ideal_k,
        }

        fit_rows.append({
            "stage": stage_key,
            "stage_name": stage_name,
            "n_points": int(len(stage_df)),
            "kextra_min": float(stage_df[KEXTRA_COL].min()) if not stage_df.empty else np.nan,
            "kextra_max": float(stage_df[KEXTRA_COL].max()) if not stage_df.empty else np.nan,
            "slope": slope,
            "intercept": intercept,
            "fit_rmse_kmdot": fit_rmse,
            "ideal_k_extra": ideal_k,
            "ideal_k_extra_unc": ideal_k_unc,
            "current_correction_factor_mean": corr_old_mean,
            "recommended_correction_factor": corr_new,
            "correction_factor_name": corr_name,
        })

        lines.append(f">>> {stage_name}")
        lines.append("-" * 90)
        lines.append(f"  Points used: {len(stage_df)}")
        if len(stage_df) > 0:
            lines.append(
                f"  K_extra range: {stage_df[KEXTRA_COL].min():.3f} to {stage_df[KEXTRA_COL].max():.3f}"
            )
        lines.append(f"  Fit: k_mdot = {slope:.6f} * K_extra + {intercept:.6f}")
        if pd.notna(ideal_k):
            unc_str = f" +/- {ideal_k_unc:.4f}" if pd.notna(ideal_k_unc) else ""
            lines.append(f"  Ideal K_extra (k_mdot=1): {ideal_k:.4f}{unc_str}")
        else:
            lines.append("  Ideal K_extra (k_mdot=1): unavailable")
        if pd.notna(corr_old_mean) and pd.notna(corr_new):
            lines.append(f"  Current {corr_name}: {corr_old_mean:.6f}")
            lines.append(f"  Recommended {corr_name}: {corr_new:.6f} (= current * ideal_K_extra)")
        lines.append("")

    k_vals = fam4[KEXTRA_COL].dropna()
    paired = _paired_optimum(stage_fits, float(k_vals.min()) - 0.1, float(k_vals.max()) + 0.1)

    lines.append(">>> PAIRED OPTIMUM (single K_extra for both stages)")
    lines.append("-" * 90)
    if pd.notna(paired):
        lines.append(f"  Best paired K_extra: {paired:.4f}")
        for stage_key, cfg in STAGE_CFG.items():
            m = stage_fits[stage_key]["slope"]
            b = stage_fits[stage_key]["intercept"]
            pred = m * paired + b if pd.notna(m) and pd.notna(b) else np.nan
            err_pct = (pred - 1.0) * 100.0 if pd.notna(pred) else np.nan
            lines.append(f"  {cfg['stage_name']}: predicted k_mdot={pred:.4f}, error={err_pct:+.2f}%")
    else:
        lines.append("  Paired optimum unavailable")
    lines.append("")

    fit_df = pd.DataFrame(fit_rows)
    fit_df.to_csv(OUTPUT_CSV, index=False)
    OUTPUT_TXT.write_text("\n".join(lines), encoding="utf-8")

    print(f"Saved: {OUTPUT_CSV}")
    print(f"Saved: {OUTPUT_TXT}")


if __name__ == "__main__":
    main()
