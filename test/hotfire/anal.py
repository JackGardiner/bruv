import h5py
import numpy as np


NAMING = [
    "2sec",
    "6sec",
    "sweepTL-80-120",
    "sweepOFR-1.4-1.55",
    "sweepTL-75-125",
]
RANGES = [
    (0.0, 4),
    (0.0, 8),
    (0.0, 10),
    (0.0, 10),
    (0.0, 10),
]

def denoise(t, x, window=0.08):
    t, x = np.asarray(t), np.asarray(x, float)
    cs = np.concatenate(([0.0], np.cumsum(x)))
    lo = np.searchsorted(t, t - window / 2, side="left")
    hi = np.searchsorted(t, t + window / 2, side="right")
    return (cs[hi] - cs[lo]) / (hi - lo)


def read(path, tstart, tend):
    class Data:
        pass
    data = Data()

    with h5py.File(path, "r") as f:
        ch = f["channels"]

        data.t = ch["DAU1080_armed/time"][:]

        data.Thrust  = ch["LC190/data"][:]

        data.dm_lox = ch["M801/data"][:]
        data.dm_ipa = ch["M730/data"][:]

        data.P_lox0 = ch["PT815/data"][:]*1e5 + 101325
        data.T_lox0 = ch["TC815/data"][:] + 273.15
        data.P_ipa0 = ch["PT732/data"][:]*1e5 + 101325
        data.T_ipa0 = ch["TC732/data"][:] + 273.15

        data.P_cc   = ch["PTX101/data"][:]*1e5 + 101325
        data.P_lox1 = ch["PTX102/data"][:]*1e5 + 101325
        data.P_ipa1 = ch["PTX103/data"][:]*1e5 + 101325

        data.T_ipa = [(ch[f"TCX10{1 + i}/data"][:] + 273.15) for i in range(6)]

    t = data.t
    mask = (tstart <= data.t) & (data.t <= tend)
    for attr in dir(data):
        if attr.startswith("__"):
            continue
        v = getattr(data, attr)
        if isinstance(v, list):
            v = [denoise(t, x)[mask] for x in v]
        else:
            v = denoise(t, v)[mask]
        setattr(data, attr, v)

    return data





import matplotlib
import matplotlib.pyplot as plt
from mpl_toolkits.axes_grid1 import make_axes_locatable

G0 = 9.80665
A_tht = 0.001060237982126994

Z_TC = None         # <INSERT z_tc HERE> [m]   list/array of 6 TC axial positions
Z_EXIT = None       # <INSERT z_exit HERE> [m]

# unit helpers (base SI in, display units out)
kN  = lambda x: x * 1e-3
bar = lambda x: x * 1e-5
degC = lambda x: x - 273.15

SS_KW = dict(color="0.9", zorder=0)          # steady-state shading style
AVG_KW = dict(ls="--", lw=1, alpha=0.8)       # mean-line style


# ---------------------------------------------------------------- performance
def compute_performance(d, mask):
    """All critical values over the steady-state mask. Returns dict."""
    m = mask
    p = {}
    p["F"]      = np.mean(d.Thrust[m])
    p["F_std"]  = np.std(d.Thrust[m])
    p["Pc"]     = np.mean(d.P_cc[m])
    p["Po"]     = np.mean(d.P_lox1[m])
    p["Pf"]     = np.mean(d.P_ipa1[m])
    p["Po0"]    = np.mean(d.P_lox0[m])
    p["Pf0"]    = np.mean(d.P_ipa0[m])
    p["dm_lox"] = np.mean(d.dm_lox[m])
    p["dm_ipa"] = np.mean(d.dm_ipa[m])
    p["TC1"]    = d.T_ipa[0][m][-1]
    p["TC2"]    = d.T_ipa[1][m][-1]
    p["TC3"]    = d.T_ipa[2][m][-1]
    p["TC4"]    = d.T_ipa[3][m][-1]
    p["TC5"]    = d.T_ipa[4][m][-1]
    p["TC6"]    = d.T_ipa[5][m][-1]
    p["dm"]     = p["dm_lox"] + p["dm_ipa"]
    p["OF"]     = p["dm_lox"] / (p["dm_ipa"] / 1.15)
    p["Isp"]    = p["F"] / (p["dm"] * G0)
    p["cstar"]  = p["Pc"] * A_tht / p["dm"] if A_tht else None
    p["dP_lox"] = np.mean(d.P_lox1[m] - d.P_cc[m])
    p["dP_ipa"] = np.mean(d.P_ipa1[m] - d.P_cc[m])
    p["stiff_lox"] = p["dP_lox"] / p["Pc"]
    p["stiff_ipa"] = p["dP_ipa"] / p["Pc"]
    p["dP_regen"]  = np.mean(d.P_ipa0[m] - d.P_ipa1[m])
    p["t_burn"]    = d.t[m][-1] - d.t[m][0]
    T_peak = [np.max(T[m]) for T in d.T_ipa]
    p["Tw_peak"]   = max(T_peak)
    p["Tw_peak_i"] = int(np.argmax(T_peak))
    return p


PALETTE = {
    # "red":    "#E63946",  # vivid crimson
    # "blue":   "#1D6FB8",  # clear medium blue
    # "amber":  "#F4A522",  # warm golden amber
    # "teal":   "#2A9D8F",  # deep teal green

    "red":    "C3",
    "blue":   "C0",
    "amber":  "C1",
    "teal":   "C2",
    "purple": "C4",
    "pink":   "C6",

    "maroon": "#8a0000",#(0.0680784, 0.261334, 0.395294),
}


def clamp_lower(ax, min_val, axis="y"):
    get, set_ = (ax.get_ylim, ax.set_ylim) if axis == "y" else (ax.get_xlim, ax.set_xlim)
    lo, hi = get()
    set_(max(lo, min_val), hi)


def plot_signal(ax, t, y, yave, col, pos=1.0, fmt="{:.2f}", **plot_kw):
    ax.plot(t, y, color=col, **plot_kw)
    ax.axhline(yave, color=col, ls="--", lw=1)
    ax.annotate(fmt.format(yave), xy=(pos, yave),
                xycoords=ax.get_yaxis_transform(),  # x: axes frac, y: data
                ha="left" if pos >= 1.0 else "center", va="center",
                color=col, fontsize=10, fontweight="bold")

def tile_thrust(ax, d, mask, perf):
    ax.axvspan(d.t[mask][0], d.t[mask][-1], **SS_KW)

    plot_signal(ax, d.t, kN(d.Thrust), kN(perf["F"]), PALETTE["amber"],
            pos=1.01, fmt="{:.2f} kN")

    ax.set_title("Thrust [kN]", fontweight="bold")
    # ax.set_ylim([0, 6.0])
    clamp_lower(ax, 0.0)


def tile_pressures(ax, d, mask, perf):
    # narrow DP strip appended below the main axes
    ax_dp = make_axes_locatable(ax).append_axes("bottom", size="30%", pad=0.13,
            sharex=ax)
    clean_axes(ax_dp, d)


    ax.axvspan(d.t[mask][0], d.t[mask][-1], **SS_KW)

    plot_signal(ax, d.t, bar(d.P_lox1), bar(perf["Po"]), PALETTE["blue"],
            label="LOx", pos=1.01, fmt="{:.1f} bar")
    plot_signal(ax, d.t, bar(d.P_ipa1), bar(perf["Pf"]), PALETTE["red"],
            label="IPA", pos=1.01, fmt="{:.1f} bar")
    plot_signal(ax, d.t, bar(d.P_ipa0), bar(perf["Pf0"]), PALETTE["red"],
            ls=":", label="IPA inlet", pos=1.01, fmt="{:.1f} bar")
    plot_signal(ax, d.t, bar(d.P_cc), bar(perf["Pc"]), PALETTE["teal"],
            label="Chamber", pos=1.01, fmt="{:.1f} bar")

    ax.set_title("Pressures [bar]", fontweight="bold")
    ax.legend(loc="lower left", fontsize=8)
    # ax.set_ylim([0, 70])
    clamp_lower(ax, 0.0)
    ax.tick_params(labelbottom=False)


    ax_dp.axvspan(d.t[mask][0], d.t[mask][-1], **SS_KW)

    plot_signal(ax_dp, d.t, bar(d.P_lox1 - d.P_cc), bar(perf["Po"] - perf["Pc"]), PALETTE["blue"],
            pos=1.01, fmt="{:.1f} bar")
    plot_signal(ax_dp, d.t, bar(d.P_ipa1 - d.P_cc), bar(perf["Pf"] - perf["Pc"]), PALETTE["red"],
            pos=1.01, fmt="{:.1f} bar")

    ax_dp.set_ylabel("inj. DP")
    # ax_dp.set_ylim([0, 15])
    clamp_lower(ax_dp, 0.0)


def tile_wall_temps(ax, d, mask, perf):
    ax.axvspan(d.t[mask][0], d.t[mask][-1], **SS_KW)
    cmap = plt.cm.inferno
    for i, T in enumerate(d.T_ipa):
        col = cmap((0.01 + len(d.T_ipa) - i) / (len(d.T_ipa) - 1) * 0.7)
        plot_signal(ax, d.t, degC(T), degC(perf[f"TC{i + 1}"]), col,
            # label=f"TC{i + 1}",
            pos=1.01, fmt="{:.1f}°C")

    ax.set_title("Channel axial temps [°C]", fontweight="bold")
    clamp_lower(ax, 25.0)
    # ax.set_ylim([20, 75])


def tile_mfr(ax, d, mask, perf):
    # narrow O/F strip appended below the main axes
    ax_of = make_axes_locatable(ax).append_axes("bottom", size="30%", pad=0.13,
            sharex=ax)
    clean_axes(ax_of, d)

    ax.axvspan(d.t[mask][0], d.t[mask][-1], **SS_KW)
    plot_signal(ax, d.t, d.dm_lox, perf["dm_lox"], PALETTE["blue"],
            label="LOx", pos=1.01, fmt="{:.2f} kg/s")
    plot_signal(ax, d.t, d.dm_ipa / 1.15, perf["dm_ipa"] / 1.15, PALETTE["red"],
            label="IPA", pos=1.01, fmt="\n{:.2f} kg/s\n(ex. FC)")

    ax.set_title("Mass flow rates [kg/s]", fontweight="bold")
    ax.legend(loc="lower left", fontsize=8)
    ax.tick_params(labelbottom=False)
    clamp_lower(ax, 0.0)
    # ax.set_ylim([0, 2.0])

    with np.errstate(divide="ignore", invalid="ignore"):
        ofr = d.dm_lox / (d.dm_ipa / 1.15)
    ax_of.axvspan(d.t[mask][0], d.t[mask][-1], **SS_KW)

    plot_signal(ax_of, d.t, ofr, perf["OF"], PALETTE["purple"],
            pos=1.01, fmt="\n{:.2f}\n(ex. FC)")
    ax_of.set_ylim(perf["OF"] * 0.85, perf["OF"] * 1.15)
    ax_of.set_ylabel("O/F")



def tile_numbers(ax, d, mask, perf):
    from matplotlib.patches import FancyBboxPatch
    from matplotlib.colors import to_rgba
    ax.axis("off")
    stats = [  # (label, value, colour)
        ("Thrust", f'{kN(perf["F"]):.2f} kN',    PALETTE["amber"]),
        ("Isp",    f'{perf["Isp"]:.0f} s',       PALETTE["pink"]),
        ("Pc",     f'{bar(perf["Pc"]):.1f} bar', PALETTE["teal"]),
        ("O/F (ex. FC)",    f'{perf["OF"]:.2f}',          PALETTE["purple"]),
        # ("C*",     f'{perf["cstar"]:.0f} m/s',   "C5"),
        ("LOx stiffness", f'{perf["stiff_lox"]*100:.0f}%', PALETTE["blue"]),
        ("IPA stiffness", f'{perf["stiff_ipa"]*100:.0f}%', PALETTE["red"]),
        ("FC (IPA)", "15%", "C5"),
    ]
    # patch coords are axes fractions, and this axes is ~12x wider than tall,
    # so isotropic (round-in-pixels) corners need mutation_aspect = w/h
    pos = ax.get_position()
    figw, figh = ax.figure.get_size_inches()
    aspect = (pos.width * figw) / (pos.height * figh)
    n = len(stats)
    w = 0.85 / n
    for i, (label, value, col) in enumerate(stats):
        x = (i + 0.5) / n
        ax.add_patch(FancyBboxPatch(
            (x - w / 2, 0.06), w, 0.88, transform=ax.transAxes,
            boxstyle="round,pad=0.006,rounding_size=0.018",
            mutation_aspect=aspect,
            facecolor=to_rgba(col, 0.08), edgecolor=to_rgba(col, 0.55),
            lw=1.3, clip_on=False))
        ax.text(x, 0.72, label, ha="center", va="center", fontsize=11,
                color="0.45", fontweight="bold", transform=ax.transAxes)
        ax.text(x, 0.28, value, ha="center", va="center", fontsize=15,
                color=col, fontweight="bold", transform=ax.transAxes)



def clean_axes(ax, d):
    ax.set_xlim([d.t[0], d.t[-1]])

    ax.title.set_color("0.15")
    ax.xaxis.label.set_color("0.15")
    ax.yaxis.label.set_color("0.15")
    ax.tick_params(colors="0.15")            # tick marks + tick labels
    for spine in ax.spines.values():
        spine.set_color("0.15")

    ax.grid(False, axis="both", which="both")
    autminloc = matplotlib.ticker.AutoMinorLocator
    ax.xaxis.set_minor_locator(autminloc())
    ax.yaxis.set_minor_locator(autminloc())
    ax.grid(True, axis="both", which="major", lw=1.0, ls="-", alpha=0.6)
    ax.grid(True, axis="both", which="minor", lw=0.6, ls="--", alpha=0.35)




# tile layout — swap entries to rearrange the grid
TILES = [
    [tile_numbers],
    [tile_thrust,     tile_mfr      ],
    [tile_wall_temps, tile_pressures],
]
HEIGHT_RATIOS = [0.22, 1, 1]

def plot_hotfire(d, name, title, tstart_ss, tend_ss):
    mask = (tstart_ss <= d.t) & (d.t <= tend_ss)
    perf = compute_performance(d, mask)

    nrows, ncols = len(TILES), len(TILES[0])
    fig = plt.figure(figsize=(9.5, 8), constrained_layout={
            "w_pad": 0.1,
            "h_pad": 0.1,
            "wspace": 0.0,
            "hspace": 0.1,
        })
    gs = fig.add_gridspec(len(TILES), 1, height_ratios=HEIGHT_RATIOS)
    for r, row in enumerate(TILES):
        sub = gs[r].subgridspec(1, len(row))
        for c, tile in enumerate(row):
            ax = fig.add_subplot(sub[c])
            tile(ax, d, mask, perf)
            clean_axes(ax, d)
    fig.suptitle(title, fontsize=20, fontweight="bold", color="0.15")
    fig.savefig(f"images/{name}.png", dpi=300, bbox_inches="tight")
    return fig



def main():
    data = [read(f"data/{1 + i}.h5", *RANGES[i]) for i in range(len(NAMING))]

    plot_hotfire(data[1], "2-nominal", "Nominal", 1.6, 6.4)
    plot_hotfire(data[2], "3-TL20", "Thrust sweep (±20%)", 6.1, 7.9)
    plot_hotfire(data[3], "4-OF", "O/F sweep (+0.15)", 6.1, 7.9)
    plot_hotfire(data[4], "5-TL25", "Thrust sweep (±25%)", 6.1, 7.9)
    plt.show()

if __name__ == "__main__":
    main()
