import argparse
from dataclasses import dataclass
from pathlib import Path

import matplotlib.dates as mdates
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from matplotlib.widgets import Button, SpanSelector


@dataclass
class SelectionWindow:
    t_start: pd.Timestamp | None = None
    t_end: pd.Timestamp | None = None


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "One-off LC calibration tool. Select FM1 and FM2 time windows "
            "graphically, then compute load-cell scaling factors."
        )
    )
    parser.add_argument(
        "--input",
        default="sample_data/LC-CAL*.csv",
        help="Input file path or glob pattern (default: sample_data/LC-CAL*.csv).",
    )
    parser.add_argument(
        "--smooth",
        type=int,
        default=15,
        help="Rolling-window points used for preview LC-derived flow trace (default: 15).",
    )
    return parser.parse_args()


def resolve_input_file(input_pattern: str) -> Path:
    path_obj = Path(input_pattern)
    if path_obj.exists() and path_obj.is_file():
        return path_obj

    matches = sorted(Path(".").glob(input_pattern))
    if not matches:
        raise FileNotFoundError(f"No files found for pattern: {input_pattern}")
    if len(matches) > 1:
        matched = "\n".join(str(p) for p in matches)
        raise ValueError(
            "Pattern matched multiple files. Provide a single file with --input.\n"
            f"Matches:\n{matched}"
        )
    return matches[0]


def load_dataframe(path: Path) -> pd.DataFrame:
    df = pd.read_csv(path)
    required = ["timestamp", "LC1_g", "LC2_g", "Flow1_gs", "Flow2_gs"]
    missing = [col for col in required if col not in df.columns]
    if missing:
        raise KeyError(f"Missing required columns: {missing}")

    df = df.copy()
    df["timestamp"] = pd.to_datetime(df["timestamp"], format="%H:%M:%S.%f")
    t0 = df["timestamp"].iloc[0]
    df["t_sec"] = (df["timestamp"] - t0).dt.total_seconds()
    return df


def robust_mass_flow_gs(df: pd.DataFrame, mass_col: str, t0: pd.Timestamp, t1: pd.Timestamp) -> float:
    mask = (df["timestamp"] >= t0) & (df["timestamp"] <= t1)
    segment = df.loc[mask, ["t_sec", mass_col]].dropna()
    if len(segment) < 3:
        return float("nan")

    t = segment["t_sec"].to_numpy()
    m = segment[mass_col].to_numpy()
    slope_g_per_s, _intercept = np.polyfit(t, m, 1)
    return float(-slope_g_per_s)


def mean_meter_flow_gs(df: pd.DataFrame, meter_col: str, t0: pd.Timestamp, t1: pd.Timestamp) -> float:
    mask = (df["timestamp"] >= t0) & (df["timestamp"] <= t1)
    return float(df.loc[mask, meter_col].mean())


def safe_ratio(num: float, den: float) -> float:
    if np.isnan(num) or np.isnan(den) or abs(den) < 1e-12:
        return float("nan")
    return float(num / den)


def compute_scaling_table(df: pd.DataFrame, fm1_window: SelectionWindow, fm2_window: SelectionWindow) -> pd.DataFrame:
    if fm1_window.t_start is None or fm1_window.t_end is None:
        raise ValueError("FM1 range is not selected.")
    if fm2_window.t_start is None or fm2_window.t_end is None:
        raise ValueError("FM2 range is not selected.")

    rows: list[dict] = []
    specs = [
        ("FM1", "Flow1_gs", fm1_window),
        ("FM2", "Flow2_gs", fm2_window),
    ]

    for meter_name, meter_col, window in specs:
        assert window.t_start is not None and window.t_end is not None
        t0 = min(window.t_start, window.t_end)
        t1 = max(window.t_start, window.t_end)

        meter_mean = mean_meter_flow_gs(df, meter_col, t0, t1)
        lc1_rate = robust_mass_flow_gs(df, "LC1_g", t0, t1)
        lc2_rate = robust_mass_flow_gs(df, "LC2_g", t0, t1)
        lct_rate = robust_mass_flow_gs(df, "LC_total_g", t0, t1) if "LC_total_g" in df.columns else float("nan")

        row = {
            "meter": meter_name,
            "meter_col": meter_col,
            "t_start": t0,
            "t_end": t1,
            "n_points": int(((df["timestamp"] >= t0) & (df["timestamp"] <= t1)).sum()),
            "meter_mean_gs": meter_mean,
            "lc1_rate_gs": lc1_rate,
            "lc2_rate_gs": lc2_rate,
            "lc_total_rate_gs": lct_rate,
            "lc1_scale_lc_to_meter": safe_ratio(meter_mean, lc1_rate),
            "lc2_scale_lc_to_meter": safe_ratio(meter_mean, lc2_rate),
            "lc_total_scale_lc_to_meter": safe_ratio(meter_mean, lct_rate),
            "lc1_scale_meter_to_lc": safe_ratio(lc1_rate, meter_mean),
            "lc2_scale_meter_to_lc": safe_ratio(lc2_rate, meter_mean),
            "lc_total_scale_meter_to_lc": safe_ratio(lct_rate, meter_mean),
        }
        rows.append(row)

    return pd.DataFrame(rows)


def make_preview_flow(df: pd.DataFrame, mass_col: str, smooth: int) -> pd.Series:
    raw = -df[mass_col].diff() / df["t_sec"].diff()
    if smooth <= 1:
        return raw
    return raw.rolling(smooth, center=True, min_periods=1).mean()


def main() -> None:
    args = parse_args()
    input_path = resolve_input_file(args.input)
    df = load_dataframe(input_path)

    fm1_window = SelectionWindow()
    fm2_window = SelectionWindow()

    lc_total_preview = make_preview_flow(df, "LC_total_g", max(args.smooth, 1))

    fig = plt.figure(figsize=(13, 8))
    fig.canvas.manager.set_window_title("One-off LC Calibration Selector")

    ax_fm1 = fig.add_axes([0.08, 0.57, 0.88, 0.32])
    ax_fm2 = fig.add_axes([0.08, 0.15, 0.88, 0.32], sharex=ax_fm1)
    ax_btn_calc = fig.add_axes([0.08, 0.03, 0.16, 0.07])
    ax_btn_clear = fig.add_axes([0.26, 0.03, 0.12, 0.07])
    ax_status = fig.add_axes([0.40, 0.03, 0.56, 0.07])
    ax_status.set_frame_on(False)
    ax_status.axis("off")

    status_text = ax_status.text(0, 0.5, "Drag FM1 range (top) and FM2 range (bottom).", va="center")

    fm1_patch = None
    fm2_patch = None

    def set_status(msg: str, color: str = "black") -> None:
        status_text.set_text(msg)
        status_text.set_color(color)
        fig.canvas.draw_idle()

    ax_fm1.plot(df["timestamp"], df["Flow1_gs"], label="Flow1_gs", color="tab:blue", linewidth=1.3)
    ax_fm1.plot(df["timestamp"], lc_total_preview, label="LC_total-derived flow (g/s)", color="tab:gray", alpha=0.8, linewidth=1.0)
    ax_fm1.set_ylabel("Flow (g/s)")
    ax_fm1.set_title("FM1 range selection")
    ax_fm1.grid(True, alpha=0.3)
    ax_fm1.legend(loc="upper right", fontsize=8)

    ax_fm2.plot(df["timestamp"], df["Flow2_gs"], label="Flow2_gs", color="tab:green", linewidth=1.3)
    ax_fm2.plot(df["timestamp"], lc_total_preview, label="LC_total-derived flow (g/s)", color="tab:gray", alpha=0.8, linewidth=1.0)
    ax_fm2.set_ylabel("Flow (g/s)")
    ax_fm2.set_title("FM2 range selection")
    ax_fm2.grid(True, alpha=0.3)
    ax_fm2.legend(loc="upper right", fontsize=8)
    ax_fm2.set_xlabel("timestamp")
    ax_fm2.xaxis.set_major_formatter(mdates.DateFormatter("%H:%M:%S"))

    # Force initial x-limits to the loaded dataset to avoid backend-dependent over-zoom.
    t_min = df["timestamp"].min()
    t_max = df["timestamp"].max()
    ax_fm1.set_xlim(t_min, t_max)
    ax_fm1.margins(x=0)

    def on_select_fm1(xmin: float, xmax: float) -> None:
        nonlocal fm1_patch
        if abs(xmax - xmin) < 1e-9:
            return
        t0 = pd.Timestamp(mdates.num2date(min(xmin, xmax)).replace(tzinfo=None))
        t1 = pd.Timestamp(mdates.num2date(max(xmin, xmax)).replace(tzinfo=None))
        fm1_window.t_start, fm1_window.t_end = t0, t1
        if fm1_patch is not None:
            fm1_patch.remove()
        fm1_patch = ax_fm1.axvspan(t0, t1, color="tab:blue", alpha=0.25)
        set_status(f"FM1 window set: {t0.strftime('%H:%M:%S.%f')[:-3]} -> {t1.strftime('%H:%M:%S.%f')[:-3]}")

    def on_select_fm2(xmin: float, xmax: float) -> None:
        nonlocal fm2_patch
        if abs(xmax - xmin) < 1e-9:
            return
        t0 = pd.Timestamp(mdates.num2date(min(xmin, xmax)).replace(tzinfo=None))
        t1 = pd.Timestamp(mdates.num2date(max(xmin, xmax)).replace(tzinfo=None))
        fm2_window.t_start, fm2_window.t_end = t0, t1
        if fm2_patch is not None:
            fm2_patch.remove()
        fm2_patch = ax_fm2.axvspan(t0, t1, color="tab:green", alpha=0.25)
        set_status(f"FM2 window set: {t0.strftime('%H:%M:%S.%f')[:-3]} -> {t1.strftime('%H:%M:%S.%f')[:-3]}")

    span_fm1 = SpanSelector(
        ax_fm1,
        on_select_fm1,
        "horizontal",
        useblit=False,
        props={"alpha": 0.25, "facecolor": "tab:blue"},
        interactive=True,
        drag_from_anywhere=True,
    )
    span_fm2 = SpanSelector(
        ax_fm2,
        on_select_fm2,
        "horizontal",
        useblit=False,
        props={"alpha": 0.25, "facecolor": "tab:green"},
        interactive=True,
        drag_from_anywhere=True,
    )
    # Keep strong references so selectors remain interactive for the life of the figure.
    _selector_refs = [span_fm1, span_fm2]

    btn_calc = Button(ax_btn_calc, "Compute", color="steelblue", hovercolor="#1a5fa8")
    btn_calc.label.set_color("white")
    btn_clear = Button(ax_btn_clear, "Clear", color="#777777", hovercolor="#555555")
    btn_clear.label.set_color("white")

    def on_clear(_event) -> None:
        nonlocal fm1_patch, fm2_patch
        fm1_window.t_start = None
        fm1_window.t_end = None
        fm2_window.t_start = None
        fm2_window.t_end = None
        if fm1_patch is not None:
            fm1_patch.remove()
            fm1_patch = None
        if fm2_patch is not None:
            fm2_patch.remove()
            fm2_patch = None
        set_status("Selections cleared.")

    def on_compute(_event) -> None:
        try:
            out = compute_scaling_table(df, fm1_window, fm2_window)
        except Exception as exc:
            set_status(str(exc), color="red")
            return

        out_dir = Path("analysis_outputs")
        out_dir.mkdir(parents=True, exist_ok=True)

        # Single canonical output — read by steady_anal.py and other scripts.
        scale_rows = []
        for meter in ["FM1", "FM2"]:
            row = out.loc[out["meter"] == meter, "lc_total_scale_meter_to_lc"]
            scale_rows.append({"meter": meter, "scale": float(row.iloc[0]) if not row.empty else float("nan")})
        scale_df = pd.DataFrame(scale_rows)
        out_csv = out_dir / "flow_scaling.csv"
        scale_df.to_csv(out_csv, index=False)

        print("\n" + "=" * 80)
        print(f"LC calibration — {input_path.name}")
        print("=" * 80)
        print(out.to_string(index=False))
        print(f"\nScaling factors (meter → LC-corrected):")
        print(scale_df.to_string(index=False))
        print(f"\nSaved: {out_csv}")
        set_status(f"Saved: {out_csv}", color="green")

    btn_calc.on_clicked(on_compute)
    btn_clear.on_clicked(on_clear)

    plt.show()


if __name__ == "__main__":
    main()