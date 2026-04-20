import argparse
from pathlib import Path

import matplotlib.pyplot as plt
import numpy as np
import pandas as pd


P_ATM_PA = 101325.0


def configure_report_style() -> None:
    # Use mathtext-compatible serif styling for report-ready plots without requiring a LaTeX install.
    plt.rcParams.update(
        {
            "figure.dpi": 140,
            "savefig.dpi": 300,
            "font.family": "serif",
            "font.serif": ["Times New Roman", "DejaVu Serif", "Times"],
            "mathtext.fontset": "stix",
            "axes.labelsize": 12,
            "axes.titlesize": 13,
            "legend.fontsize": 10,
            "xtick.labelsize": 10,
            "ytick.labelsize": 10,
            "axes.grid": True,
            "grid.alpha": 0.22,
            "grid.linestyle": "--",
            "grid.linewidth": 0.6,
        }
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Read a swept-dP DAQ log, plot MFR vs dP, and estimate Cd from fit gradient."
    )
    parser.add_argument(
        "csv_path",
        nargs="?",
        default=r"sample_data\SWEEP-RS21-daq_log_20260417_145831.csv",
        help="Path to swept-dP CSV relative to test/ or absolute path.",
    )
    parser.add_argument(
        "--pt-column",
        default="PT2_bar",
        help="Pressure transducer column to use for dP (default: PT2_bar).",
    )
    parser.add_argument(
        "--flow-column",
        default="Flow1_gs",
        help="Flow meter column in g/s to use for MFR (default: Flow1_gs).",
    )
    parser.add_argument(
        "--flow-scale",
        type=float,
        default=1.0,
        help="Optional multiplicative correction for the selected flow column.",
    )
    parser.add_argument(
        "--orifice-diameter-mm",
        type=float,
        default=4.53,
        help="Orifice diameter in mm used for Cd calculation.",
    )
    parser.add_argument(
        "--rho",
        type=float,
        default=1000.0,
        help="Fluid density in kg/m^3 (default: 1000 for water).",
    )
    parser.add_argument(
        "--min-dp-kpa",
        type=float,
        default=5.0,
        help="Minimum dP in kPa included in fitting.",
    )
    parser.add_argument(
        "--show",
        action="store_true",
        help="Show plots interactively instead of only saving PNGs.",
    )
    parser.add_argument(
        "--min-sqrt-dp",
        type=float,
        default=600.0,
        help="Minimum sqrt(dP) [Pa^0.5] included in fitting (default: 600).",
    )
    parser.add_argument(
        "--max-sqrt-dp",
        type=float,
        default=1000.0,
        help="Maximum sqrt(dP) [Pa^0.5] included in fitting (default: 1000).",
    )
    parser.add_argument(
        "--operating-dp-bar",
        type=float,
        default=7.0,
        help="Operating-point dP in bar for origin-through-point linear comparison (default: 7.0).",
    )
    return parser.parse_args()


def resolve_csv_path(csv_path_arg: str) -> Path:
    candidate = Path(csv_path_arg)
    if candidate.is_absolute() and candidate.exists():
        return candidate

    script_dir = Path(__file__).resolve().parent
    from_script = (script_dir / csv_path_arg).resolve()
    if from_script.exists():
        return from_script

    from_cwd = Path.cwd() / csv_path_arg
    if from_cwd.exists():
        return from_cwd.resolve()

    raise FileNotFoundError(f"Could not find CSV at: {csv_path_arg}")


def build_analysis_frame(
    df: pd.DataFrame,
    pt_column: str,
    flow_column: str,
    flow_scale: float,
    min_dp_kpa: float,
) -> pd.DataFrame:
    for required in ["timestamp", pt_column, flow_column]:
        if required not in df.columns:
            raise KeyError(f"Missing required column: {required}")

    out = df.copy()
    out["timestamp"] = pd.to_datetime(out["timestamp"], format="%H:%M:%S.%f")

    out["MFR_kgs"] = out[flow_column].astype(float) * flow_scale * 1e-3
    out["dP_Pa"] = out[pt_column].astype(float) * 1e5 - P_ATM_PA
    out = out.replace([np.inf, -np.inf], np.nan)
    out = out.dropna(subset=["MFR_kgs", "dP_Pa"])
    out = out[out["dP_Pa"] > min_dp_kpa * 1000.0].copy()
    out = out.sort_values("timestamp").reset_index(drop=True)

    if out.empty:
        raise ValueError("No valid data left after filtering. Check selected columns and dP threshold.")

    return out


def select_ramp_up_frame(
    analysis: pd.DataFrame,
    min_sqrt_dp: float,
    max_sqrt_dp: float,
) -> pd.DataFrame:
    if analysis.empty:
        raise ValueError("No data available for ramp-up selection.")
    if min_sqrt_dp >= max_sqrt_dp:
        raise ValueError("min_sqrt_dp must be less than max_sqrt_dp.")

    peak_idx = int(analysis["dP_Pa"].to_numpy().argmax())
    ramp = analysis.iloc[: peak_idx + 1].copy()
    ramp["sqrt_dP"] = np.sqrt(ramp["dP_Pa"])
    ramp = ramp[(ramp["sqrt_dP"] >= min_sqrt_dp) & (ramp["sqrt_dP"] <= max_sqrt_dp)].copy()
    if len(ramp) < 3:
        raise ValueError("Ramp-up segment is too short for fitting.")
    return ramp


def fit_cd(
    fit_frame: pd.DataFrame,
    orifice_diameter_mm: float,
    rho: float,
) -> dict:
    d_orifice_m = orifice_diameter_mm * 1e-3
    area = np.pi * (d_orifice_m / 2.0) ** 2

    x = np.sqrt(fit_frame["dP_Pa"].to_numpy())
    y = fit_frame["MFR_kgs"].to_numpy()

    slope, intercept = np.polyfit(x, y, 1)
    cd = slope / (area * np.sqrt(2.0 * rho))

    y_fit = slope * x + intercept
    ss_res = np.sum((y - y_fit) ** 2)
    ss_tot = np.sum((y - np.mean(y)) ** 2)
    r2 = 1.0 - (ss_res / ss_tot) if ss_tot > 0 else np.nan

    return {
        "area_m2": area,
        "slope": slope,
        "intercept": intercept,
        "cd": cd,
        "r2": r2,
        "x": x,
        "y": y,
        "y_fit": y_fit,
    }


def build_operating_point_linear_model(
    fit_frame: pd.DataFrame,
    operating_dp_bar: float,
) -> dict:
    if fit_frame.empty:
        raise ValueError("No fit-frame data available for operating-point linear model.")

    target_dp_pa = operating_dp_bar * 1e5
    dp = fit_frame["dP_Pa"].to_numpy()
    mfr = fit_frame["MFR_kgs"].to_numpy()
    idx = int(np.argmin(np.abs(dp - target_dp_pa)))

    dp_op = float(dp[idx])
    mfr_op = float(mfr[idx])
    if dp_op <= 0:
        raise ValueError("Operating-point dP is non-positive; cannot build origin-through-point model.")

    sqrt_dp_op = float(np.sqrt(dp_op))
    slope = mfr_op / sqrt_dp_op
    return {
        "target_dp_pa": target_dp_pa,
        "dp_op": dp_op,
        "sqrt_dp_op": sqrt_dp_op,
        "mfr_op": mfr_op,
        "slope": slope,
    }


def plot_results(
    analysis: pd.DataFrame,
    fit_frame: pd.DataFrame,
    fit: dict,
    operating_linear: dict,
    csv_path: Path,
    out_dir: Path,
    show: bool,
) -> None:
    configure_report_style()
    stem = csv_path.stem

    # Main requested view.
    fig1, ax1 = plt.subplots(figsize=(9.0, 5.3))
    plt.scatter(
        analysis["dP_Pa"] / 1000.0,
        analysis["MFR_kgs"],
        s=10,
        alpha=0.25,
        label="All filtered samples",
        color="tab:gray",
    )
    ax1.scatter(
        fit_frame["dP_Pa"] / 1000.0,
        fit_frame["MFR_kgs"],
        s=12,
        alpha=0.7,
        label="Ramp-up fit samples",
        color="tab:blue",
    )
    dp_fit_kpa = fit_frame["dP_Pa"].to_numpy() / 1000.0
    x_min_kpa = max(0.0, float(np.nanmin(dp_fit_kpa)) * 0.90)
    x_max_kpa = float(np.nanmax(dp_fit_kpa)) * 1.05
    ax1.set_xlim(x_min_kpa, x_max_kpa)
    ax1.set_xlabel(r"$\Delta p$ [kPa]")
    ax1.set_ylabel(r"$\dot{m}$ [kg s$^{-1}$]")
    ax1.set_title("Mass Flow vs Pressure Drop")
    ax1.legend(loc="upper left", frameon=True)
    fig1.tight_layout()
    path_mfr_dp = out_dir / f"{stem}_mfr_vs_dp.png"
    path_mfr_dp_pdf = out_dir / f"{stem}_mfr_vs_dp.pdf"
    fig1.savefig(path_mfr_dp)
    fig1.savefig(path_mfr_dp_pdf)

    # Linearized fit used for gradient-based Cd extraction.
    order = np.argsort(fit["x"])
    x_sorted = fit["x"][order]
    y_sorted = fit["y"][order]
    y_fit_sorted = fit["y_fit"][order]

    fig2, ax2 = plt.subplots(figsize=(9.0, 5.3))
    ax2.scatter(x_sorted, y_sorted, s=10, alpha=0.5, label="Ramp-up samples")
    ax2.plot(
        x_sorted,
        y_fit_sorted,
        color="tab:red",
        linewidth=2,
        label=(
            r"Regression: $\dot{m}=s\sqrt{\Delta p}+b$" + "\n"
            f"s={fit['slope']:.2e}, b={fit['intercept']:.2e}, R^2={fit['r2']:.2f}\n"
            f"Cd={fit['cd']:.4f}"
        ),
    )
    x_op_line = np.linspace(0.0, x_sorted.max(), 200)
    y_op_line = operating_linear["slope"] * x_op_line
    ax2.plot(
        x_op_line,
        y_op_line,
        color="tab:green",
        linewidth=2,
        label=(
            f"Operating-point line (origin to ~{operating_linear['dp_op']/1e5:.2f} bar): "
            rf"$\dot{{m}}={operating_linear['slope']:.3e}\sqrt{{\Delta p}}$"
        ),
    )

    x_margin = 0.03 * (x_sorted.max() - x_sorted.min()) if len(x_sorted) > 1 else 10.0
    ax2.set_xlim(x_sorted.min() - x_margin, x_sorted.max() + x_margin)
    ax2.set_xlabel(r"$\sqrt{\Delta p}$ [Pa$^{1/2}$]")
    ax2.set_ylabel(r"$\dot{m}$ [kg s$^{-1}$]")
    ax2.set_title("Linearized Discharge-Coefficient Fit")
    ax2.legend(loc="lower left", frameon=True)
    ax2.text(
        0.98,
        0.03,
        f"Operating point: {operating_linear['dp_op']/1e5:.1f} bar",
        transform=ax2.transAxes,
        ha="right",
        va="bottom",
        bbox={"boxstyle": "round,pad=0.25", "fc": "white", "ec": "0.7", "alpha": 0.9},
    )
    ax2.scatter(
        [operating_linear["sqrt_dp_op"]],
        [operating_linear["mfr_op"]],
        color="tab:orange",
        s=75,
        marker="x",
        linewidths=2,
        label="Selected operating point",
    )
    fig2.tight_layout()
    path_lin = out_dir / f"{stem}_cd_fit.png"
    path_lin_pdf = out_dir / f"{stem}_cd_fit.pdf"
    fig2.savefig(path_lin)
    fig2.savefig(path_lin_pdf)

    if show:
        plt.show()
    else:
        plt.close("all")

    print(f"Saved plot: {path_mfr_dp}")
    print(f"Saved plot: {path_mfr_dp_pdf}")
    print(f"Saved plot: {path_lin}")
    print(f"Saved plot: {path_lin_pdf}")


def main() -> None:
    args = parse_args()
    csv_path = resolve_csv_path(args.csv_path)

    df = pd.read_csv(csv_path)
    analysis = build_analysis_frame(
        df,
        pt_column=args.pt_column,
        flow_column=args.flow_column,
        flow_scale=args.flow_scale,
        min_dp_kpa=args.min_dp_kpa,
    )
    fit_frame = select_ramp_up_frame(
        analysis,
        min_sqrt_dp=args.min_sqrt_dp,
        max_sqrt_dp=args.max_sqrt_dp,
    )

    fit = fit_cd(
        fit_frame,
        orifice_diameter_mm=args.orifice_diameter_mm,
        rho=args.rho,
    )
    operating_linear = build_operating_point_linear_model(
        fit_frame,
        operating_dp_bar=args.operating_dp_bar,
    )

    out_dir = Path(__file__).resolve().parent / "analysis_outputs"
    out_dir.mkdir(parents=True, exist_ok=True)
    plot_results(
        analysis,
        fit_frame,
        fit,
        operating_linear,
        csv_path=csv_path,
        out_dir=out_dir,
        show=args.show,
    )

    print(f"CSV: {csv_path}")
    print(f"Using pressure column: {args.pt_column}")
    print(f"Using flow column: {args.flow_column} (scale={args.flow_scale})")
    print(f"Fit sqrt(dP) window: [{args.min_sqrt_dp:.1f}, {args.max_sqrt_dp:.1f}] Pa^0.5")
    print(f"Operating point target dP: {args.operating_dp_bar:.3f} bar")
    print(
        "Selected operating point: "
        f"dP={operating_linear['dp_op']/1e5:.4f} bar, m_dot={operating_linear['mfr_op']:.6f} kg/s"
    )
    print(f"Origin-through-op slope on m_dot vs sqrt(dP) (kg/s/Pa^0.5): {operating_linear['slope']:.6e}")
    print(f"Samples passing filters: {len(analysis)}")
    print(f"Ramp-up fit samples: {len(fit_frame)}")
    print(f"Orifice area: {fit['area_m2']:.6e} m^2")
    print(f"Fit slope (dm/dsqrt(dP)): {fit['slope']:.6e}")
    print(f"Fit intercept: {fit['intercept']:.6e} kg/s")
    print(f"Fit R^2: {fit['r2']:.6f}")
    print(f"Estimated Cd: {fit['cd']:.6f}")


if __name__ == "__main__":
    main()