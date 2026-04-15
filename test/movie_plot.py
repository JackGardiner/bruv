"""
video_data_overlay.py
─────────────────────
Overlay time-series test data (scrolling traces, numeric readouts, highlighted
events) on a video using moviepy + matplotlib.

Handles wall-clock timestamps (HH:MM:SS.ffffff) in the 'timestamp' column.

Dependencies:
    pip install moviepy matplotlib pandas numpy pillow

Usage:
    python video_data_overlay.py

Edit the CONFIG section at the top to suit your test.
"""

import numpy as np
import pandas as pd
import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.gridspec import GridSpec, GridSpecFromSubplotSpec
from PIL import Image
from moviepy import VideoFileClip, ImageSequenceClip  # moviepy v2
import io, warnings

warnings.filterwarnings("ignore")

# ─────────────────────────────────────────────────────────────────────────────
# CONFIG  ← edit these
# ─────────────────────────────────────────────────────────────────────────────

VIDEO_PATH = r"videos\converted.mp4"
DATA_PATH = r"sample_data\S5-daq_log_20260321_140742.csv"
OUTPUT_PATH = "output_overlay.mp4"

# Wall-clock time of the FIRST VIDEO FRAME — must match your CSV timestamps.
# Set this to the timestamp of the first CSV row if video starts at that moment.
VIDEO_START_TIME = "14:09:38.747108"

# ── Channels plotted on the scrolling trace ───────────────────────────────────
# Keys must match CSV column names exactly.
# 'plot_group': channels sharing a group share a y-axis sub-plot.
CHANNELS = {
    "PT1_bar": {
        "label": "PT1",
        "unit": "bar",
        "color": "#00d4ff",
        "plot_group": "pressure",
    },
    "PT2_bar": {
        "label": "PT2",
        "unit": "bar",
        "color": "#0077ff",
        "plot_group": "pressure",
    },
    "Flow1_gs": {
        "label": "Flow 1",
        "unit": "g/s",
        "color": "#ff6b35",
        "plot_group": "flow",
    },
    "Flow2_gs": {
        "label": "Flow 2",
        "unit": "g/s",
        "color": "#ff9a00",
        "plot_group": "flow",
    },
    "LC_total_g": {
        "label": "Mass tot",
        "unit": "g",
        "color": "#a8e063",
        "plot_group": "mass",
    },
}

# Channels shown in the numeric panel only (not plotted on the trace).
READOUT_ONLY_CHANNELS = {
    "LC1_g": {"label": "LC1", "unit": "g", "color": "#c8f07a"},
    "LC2_g": {"label": "LC2", "unit": "g", "color": "#90c040"},
}

# ── Events ────────────────────────────────────────────────────────────────────
# 'time' = seconds from video start (not wall-clock).
EVENTS = [
    # {"time": 5.0,  "label": "Valve Open",  "color": "#ffdd57"},
    # {"time": 20.0, "label": "Valve Close", "color": "#ff4757"},
]

# ── Layout ────────────────────────────────────────────────────────────────────
OVERLAY_HEIGHT_FRACTION = 0.32  # fraction of video height
SCROLL_WINDOW_S = 10.0  # seconds visible in scrolling plot
FPS_OUTPUT = None  # None = match source video FPS

# ─────────────────────────────────────────────────────────────────────────────
# TIMESTAMP HELPERS
# ─────────────────────────────────────────────────────────────────────────────


def parse_wall_time(s: str) -> float:
    """Convert HH:MM:SS[.ffffff] to total seconds."""
    parts = s.strip().split(":")
    return int(parts[0]) * 3600 + int(parts[1]) * 60 + float(parts[2])


def load_and_sync_data(
    csv_path: str, video_start: str, video_duration: float
) -> pd.DataFrame:
    df = pd.read_csv(csv_path)
    assert "timestamp" in df.columns, "CSV must have a 'timestamp' column."

    t0 = parse_wall_time(video_start)
    df["time"] = df["timestamp"].apply(parse_wall_time) - t0
    df = df[(df["time"] >= 0) & (df["time"] <= video_duration)]
    df = df.sort_values("time").reset_index(drop=True)

    if df.empty:
        raise ValueError(
            "No CSV rows fall within the video window. "
            "Check VIDEO_START_TIME matches your timestamps."
        )

    print(
        f"  Data: {df['time'].iloc[0]:.3f} s → "
        f"{df['time'].iloc[-1]:.3f} s  ({len(df)} rows)"
    )

    # Interpolate to 5 ms grid
    t_fine = np.arange(0, video_duration, 0.005)
    out = pd.DataFrame({"time": t_fine})
    for col in list(CHANNELS) + list(READOUT_ONLY_CHANNELS):
        if col not in df.columns:
            print(f"  WARNING: column '{col}' not in CSV — skipping.")
            continue
        # Ensure the x-axis is numeric and sorted
        x_orig = pd.to_numeric(df["time"], errors='coerce').values
        y_orig = pd.to_numeric(df[col], errors='coerce').values

        # Basic check to ensure no NaNs are breaking the logic
        mask = ~np.isnan(x_orig) & ~np.isnan(y_orig)

        out[col] = np.interp(
            t_fine,
            x_orig[mask],
            y_orig[mask],
            left=y_orig[mask][0],
            right=y_orig[mask][-1]
        )
    return out


# ─────────────────────────────────────────────────────────────────────────────
# RENDERING
# ─────────────────────────────────────────────────────────────────────────────


def current_val(df, col, t):
    if col not in df.columns:
        return None
    return float(np.interp(t, df["time"].values, df[col].values))


def find_active_events(t, events, window=1.5):
    return [e for e in events if t - window <= e["time"] <= t]


def render_overlay_frame(
    t: float, df: pd.DataFrame, video_w: int, overlay_h: int
) -> np.ndarray:
    dpi = 96
    fig = plt.figure(
        figsize=(video_w / dpi, overlay_h / dpi), dpi=dpi, facecolor="#0d0d0d"
    )

    groups = list(dict.fromkeys(m["plot_group"] for m in CHANNELS.values()))
    n_groups = len(groups)
    outer = GridSpec(
        1,
        2,
        figure=fig,
        width_ratios=[3, 1],
        left=0.05,
        right=0.97,
        top=0.90,
        bottom=0.15,
        wspace=0.04,
    )
    inner = GridSpecFromSubplotSpec(n_groups, 1, subplot_spec=outer[0], hspace=0.08)

    t_start = max(0.0, t - SCROLL_WINDOW_S)
    df_win = df[(df["time"] >= t_start) & (df["time"] <= t)]

    axes = []
    for gi, group in enumerate(groups):
        ax = fig.add_subplot(inner[gi], sharex=axes[0] if axes else None)
        ax.set_facecolor("#0d0d0d")
        axes.append(ax)

        grp_ch = {k: v for k, v in CHANNELS.items() if v["plot_group"] == group}
        for col, meta in grp_ch.items():
            if col not in df_win.columns:
                continue
            ax.plot(
                df_win["time"],
                df_win[col],
                color=meta["color"],
                linewidth=1.5,
                label=f'{meta["label"]} ({meta["unit"]})',
            )

        ax.axvline(t, color="white", linewidth=0.7, linestyle="--", alpha=0.5)

        for ev in EVENTS:
            if t_start <= ev["time"] <= t:
                ec = ev.get("color", "#ffdd57")
                ax.axvline(
                    ev["time"], color=ec, linewidth=1.0, linestyle=":", alpha=0.8
                )
                if gi == 0:
                    ax.text(
                        ev["time"],
                        1.0,
                        ev["label"],
                        color=ec,
                        fontsize=5.5,
                        va="top",
                        ha="center",
                        rotation=90,
                        transform=ax.get_xaxis_transform(),
                    )

        ax.set_xlim(t_start, t_start + SCROLL_WINDOW_S)
        ax.tick_params(colors="#aaaaaa", labelsize=6.5)
        for sp in ax.spines.values():
            sp.set_edgecolor("#2a2a2a")
        unit = next(iter(grp_ch.values()))["unit"]
        ax.set_ylabel(unit, color="#666666", fontsize=6, labelpad=2)
        ax.legend(
            loc="upper left",
            fontsize=6,
            facecolor="#141414",
            edgecolor="#2a2a2a",
            labelcolor="white",
            framealpha=0.85,
            handlelength=1.2,
            borderpad=0.4,
        )

        if gi < n_groups - 1:
            plt.setp(ax.get_xticklabels(), visible=False)
        else:
            ax.set_xlabel("time (s)", color="#666666", fontsize=6.5)

    # ── Numeric readout panel ─────────────────────────────────────────────────
    ax_n = fig.add_subplot(outer[1])
    ax_n.set_facecolor("#111111")
    ax_n.set_xticks([])
    ax_n.set_yticks([])
    for sp in ax_n.spines.values():
        sp.set_edgecolor("#1e1e1e")

    all_ro = {**CHANNELS, **READOUT_ONLY_CHANNELS}
    n = len(all_ro)
    row_h = 1.0 / max(n, 1)

    for i, (col, meta) in enumerate(all_ro.items()):
        y = 1.0 - (i + 0.5) * row_h
        val = current_val(df, col, t)
        val_str = f"{val:.2f}" if val is not None else "—"
        fs = max(7, 12 - n // 2)

        ax_n.text(
            0.5,
            y + row_h * 0.22,
            meta["label"].upper(),
            transform=ax_n.transAxes,
            color="#666666",
            fontsize=5.5,
            ha="center",
            va="center",
            fontfamily="monospace",
        )
        ax_n.text(
            0.5,
            y - row_h * 0.04,
            val_str,
            transform=ax_n.transAxes,
            color=meta["color"],
            fontsize=fs,
            fontweight="bold",
            ha="center",
            va="center",
            fontfamily="monospace",
        )
        ax_n.text(
            0.5,
            y - row_h * 0.28,
            meta["unit"],
            transform=ax_n.transAxes,
            color="#444444",
            fontsize=5.5,
            ha="center",
            va="center",
            fontfamily="monospace",
        )
        if i < n - 1:
            ax_n.axhline(1.0 - (i + 1) * row_h, color="#222222", linewidth=0.5)

    # ── Event banner ──────────────────────────────────────────────────────────
    active = find_active_events(t, EVENTS)
    if active:
        ev = active[-1]
        ec = ev.get("color", "#ffdd57")
        dt = t - ev["time"]
        alpha = min(1.0, dt / 0.3) * (1.0 - max(0.0, (dt - 1.0) / 0.5))
        alpha = max(0.0, min(1.0, alpha))
        fig.text(
            0.5,
            0.97,
            f"▶  {ev['label']}",
            color=ec,
            fontsize=8,
            fontweight="bold",
            ha="center",
            va="top",
            alpha=alpha,
            bbox=dict(
                boxstyle="round,pad=0.3",
                facecolor="#1a1a1a",
                edgecolor=ec,
                alpha=alpha * 0.9,
            ),
        )

    buf = io.BytesIO()
    fig.savefig(buf, format="png", dpi=dpi, facecolor="#0d0d0d")
    plt.close(fig)
    buf.seek(0)
    return np.array(Image.open(buf).convert("RGBA"))


def composite_frame(video_frame, overlay_rgba):
    h_v, w_v = video_frame.shape[:2]
    h_o, w_o = overlay_rgba.shape[:2]
    if w_o != w_v:
        overlay_rgba = np.array(
            Image.fromarray(overlay_rgba).resize((w_v, h_o), Image.Resampling.LANCZOS)
        )
        h_o = overlay_rgba.shape[0]
    out = video_frame.copy()
    y0 = h_v - h_o
    alpha = overlay_rgba[:, :, 3:4] / 255.0
    out[y0:, :, :3] = (
        alpha * overlay_rgba[:, :, :3] + (1 - alpha) * out[y0:, :, :3]
    ).astype(np.uint8)
    return out


# ─────────────────────────────────────────────────────────────────────────────
# MAIN
# ─────────────────────────────────────────────────────────────────────────────


def main():
    print(f"Loading video : {VIDEO_PATH}")
    clip = VideoFileClip(VIDEO_PATH)
    fps = FPS_OUTPUT or clip.fps
    duration = clip.duration
    w, h = int(clip.w), int(clip.h)
    overlay_h = int(h * OVERLAY_HEIGHT_FRACTION)
    print(f"  {w}×{h} @ {fps:.2f} fps, {duration:.1f} s")

    print(f"Loading data  : {DATA_PATH}")
    df = load_and_sync_data(DATA_PATH, VIDEO_START_TIME, duration)

    print("Rendering frames …")
    frames = []
    t_vals = np.arange(0, duration, 1.0 / fps)
    total = len(t_vals)

    for i, t in enumerate(t_vals):
        frames.append(
            composite_frame(
                clip.get_frame(t), render_overlay_frame(t, df, w, overlay_h)
            )
        )
        if (i + 1) % 30 == 0 or i == total - 1:
            print(f"  {i+1}/{total}  ({100*(i+1)/total:.0f}%)", end="\r")

    print("\nEncoding …")
    out_clip = ImageSequenceClip(frames, fps=fps)
    if clip.audio:
        out_clip = out_clip.with_audio(clip.audio)
    out_clip.write_videofile(OUTPUT_PATH, codec="libx264", audio_codec="aac")
    clip.close()
    print(f"\nDone  →  {OUTPUT_PATH}")


if __name__ == "__main__":
    main()
