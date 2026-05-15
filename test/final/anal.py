import os
import re
from contextlib import contextmanager

import pandas as pd
import numpy as np
import matplotlib.pyplot as plt

import geez


RESIN_FACTOR = 2.045771 / 2.525993

FLOW_METER_1_FACTOR = 1.0 / 0.993350
FLOW_METER_2_FACTOR = 1.0 / 1.104725

LOX_DENSITY = 1141.0
IPA_DENSITY = 735.0

MARK_MFR_LOX     = 1.2556549058269346 /  9
MARK_MFR_LOX_ALT = 1.2556549058269346 / 12
MARK_MFR_IPA     = 0.8968963613049534 /  9
MARK_MFR_IPA_ALT = 0.8968963613049534 / 12


def denoise(Y, *, window_size=8):
    if len(Y) < 2:
        return Y

    pad_left = (window_size - 1) // 2
    pad_right = window_size - 1 - pad_left

    slope = (Y[len(Y) - 1] - Y[0]) / (len(Y) - 1)

    left_extrap = Y[0] + np.arange(-pad_left, 0) * slope
    right_extrap = Y[len(Y) - 1] + np.arange(1, pad_right + 1) * slope

    Y_padded = np.concatenate([left_extrap, Y, right_extrap])
    window = np.ones(window_size) / window_size
    return np.convolve(Y_padded, window, mode="valid")


def plot_linfit(ax, X, Y, col):
    mask = (X == X) & (Y == Y)
    m, c = np.polyfit(X[mask], Y[mask], 1)
    x = np.linspace(0.0, 1.2*np.nanmax(X), 2)
    ax.plot(x, m*x + c, color=col)


def find_in(directory, pattern):
    if not os.path.exists(directory):
        raise FileNotFoundError(f"Directory not found: {directory}")
    path = None
    for filename in os.listdir(directory):
        if not re.match(pattern, filename):
            continue
        if path is not None:
            raise FileNotFoundError(f"Too many files found: {pattern}")
        path = os.path.join(directory, filename)
    if path is None:
        raise FileNotFoundError(f"File not found: {pattern}")
    return path

C_RED_0     = "#C71E18"
C_RED_1     = "#FC6762"
C_BLUE_0    = "#0072BD"
C_BLUE_1    = "#7FC5F5"
C_ORANGE_0  = "#D95319"
C_ORANGE_1  = "#F58758"
C_YELLOW_0  = "#EDB120"
C_YELLOW_1  = "#FFD87A"
C_PURPLE_0  = "#7E2F8E"
C_PURPLE_1  = "#DE8CED"
C_GREEN_0   = "#77AC30"
C_GREEN_1   = "#B9ED72"
C_CYAN_0    = "#12C4AA"
C_CYAN_1    = "#62CCA2"
C_MAROON_0  = "#AB1D48"
C_MAROON_1  = "#DE527C"


def read(name, csv_name=None):
    if csv_name is None:
        csv_name = name

    class Data:
        pass
    data = Data()

    csv = pd.read_csv(find_in(
        directory="../sample_data/trimmed",
        pattern=fr"^{csv_name}-daq_log_\d{{8}}_\d{{6}}.csv$"
    ))

    data.name = name
    data.number = int(name[2:].split("-", 2)[0] if name.startswith("RS")
                      else name[1:].split("-", 2)[0])

    data.t = csv["timestamp"]
    data.t = pd.to_datetime(data.t, format="%H:%M:%S.%f")
    data.t = (data.t - data.t.iloc[0]).dt.total_seconds().to_numpy()

    data.P_lox = csv["PT2_bar"] * 1e5
    data.P_ipa = csv["PT1_bar"] * 1e5
    data.mfr_lox = csv["Flow2_gs"] / FLOW_METER_2_FACTOR * 1e-6 * LOX_DENSITY
    data.mfr_ipa = csv["Flow1_gs"] / FLOW_METER_1_FACTOR * 1e-6 * IPA_DENSITY
    data.mfr_total = data.mfr_lox + data.mfr_ipa
    data.m_water = csv["LC_total_g"] * 1e-3
    data.DP_lox = data.P_lox - 101325.0
    data.DP_ipa = data.P_ipa - 101325.0

    winsize = min(40, int(np.sqrt(len(data.m_water))))
    data.denoised_m_water = denoise(data.m_water, window_size=winsize)
    data.mfr_derived = -np.gradient(data.denoised_m_water, data.t)
    data.mfr_derived[data.mfr_derived < 0] = np.nan
    data.mfr_derived[data.mfr_derived > 3*data.mfr_total.max()] = np.nan

    if name.startswith("RS"):
        data.mfr_lox /= RESIN_FACTOR
        data.mfr_ipa /= RESIN_FACTOR
        data.mfr_total /= RESIN_FACTOR
        data.mfr_derived /= RESIN_FACTOR

    report_path = find_in(
        directory="../injector_reports",
        pattern=fr"injector-sample-{data.number}-report.txt"
    )

    with open(report_path, "r") as f:
        report_lines = f.readlines()

    def getv(param):
        target = f"  -{param}: "

        for line in report_lines:
            if target not in line:
                continue

            remainder = line.split(target, 1)[1].strip()
            tokens = remainder.split()
            assert tokens

            value = float(tokens[0])
            if len(tokens) > 1:
                assert len(tokens) == 2
                if tokens[1] == "mm":
                    value *= 1e-3
                elif tokens[1] == "bar":
                    value *= 1e5
                elif tokens[1] == "kg/s":
                    pass
                else:
                    assert False, f"{tokens}"
            return value
        return None

    data.target_DP_lox = getv("1 Pressure difference")
    data.target_DP_ipa = getv("2 Pressure difference")
    data.target_mfr_lox = getv("1 Mass flow rate")
    data.target_mfr_ipa = getv("2 Mass flow rate")
    data.A_lox = getv("1 Inlet count") * np.pi*getv("1 Inlet radius")**2
    data.A_ipa = getv("2 Inlet count") * np.pi*getv("2 Inlet radius")**2

    return data



def mdot_lookups():
    path_lox = "../mdot_lookups/injector-mdot-lookup-lox.csv"
    path_ipa = "../mdot_lookups/injector-mdot-lookup-ipa.csv"
    lox = pd.read_csv(path_lox)
    ipa = pd.read_csv(path_ipa)

    X = np.geomspace(lox["X"].min(), lox["X"].max(), 1000)
    mdot_lox = np.interp(X, lox["X"].to_numpy(), lox["mdot"].to_numpy())
    mdot_ipa = np.interp(X, ipa["X"].to_numpy(), ipa["mdot"].to_numpy())
    return X, mdot_lox, mdot_ipa




def peep(win, data, include_load_cells=False):
    if include_load_cells:
        _, axes = win.new_plots(title=data.name, rows=2, cols=2)
        ax_P, ax_mass, ax_mfr, _ = axes.flatten()
    else:
        _, (ax_P, ax_mfr) = win.new_plots(title=data.name, rows=2)

    if include_load_cells:
        ax_mass.plot(data.t, data.m_water, color="darkkhaki")
        ax_mass.plot(data.t, data.denoised_m_water, color="forestgreen", ls="--",
                lw=2.4)
        ax_mass.set_xlabel("time [s]")
        ax_mass.set_ylabel("tank mass [kg]")

    ax_P.plot(data.t, data.DP_lox, color=C_BLUE_0, label="LOx")
    ax_P.plot(data.t, data.DP_ipa, color=C_RED_0, label="IPA")
    ax_P.axhline(data.target_DP_lox, color=C_BLUE_1, ls="--")
    ax_P.axhline(data.target_DP_ipa, color=C_RED_1, ls="--")
    ax_P.axhline(0.0, color=(0.0,)*4)
    ax_P.set_xlabel("time [s]")
    ax_P.set_ylabel("DP [Pa]")

    ax_mfr.plot(data.t, data.mfr_lox, color=C_BLUE_0, label="LOx")
    ax_mfr.plot(data.t, data.mfr_ipa, color=C_RED_0, label="IPA")
    if include_load_cells:
        ax_mfr.plot(data.t, data.mfr_total, color="darkorange", label="summed")
        ax_mfr.plot(data.t, data.mfr_derived, color="forestgreen",
                label="derived")
    ax_mfr.axhline(data.target_mfr_lox, color=C_BLUE_1, ls="--")
    ax_mfr.axhline(data.target_mfr_ipa, color=C_RED_1, ls="--")
    ax_mfr.axhline(0.0, color=(0.0,)*4)
    ax_mfr.set_xlabel("time [s]")
    ax_mfr.set_ylabel("MFR [kg/s]")



def peep_sweep(ax, data, col_lox, col_lox_alt, col_ipa, col_ipa_alt, label=True):
    X_lox = data.A_lox * np.sqrt(np.maximum(0.0, data.DP_lox))
    X_ipa = data.A_ipa * np.sqrt(np.maximum(0.0, data.DP_ipa))

    if col_lox is not None:
        ax.scatter(X_lox, data.mfr_lox, zorder=3, color=col_lox, alpha=0.25,
                label="LOx" if (label or True) else None)
    if col_ipa is not None:
        ax.scatter(X_ipa, data.mfr_ipa, zorder=3, color=col_ipa, alpha=0.25,
                label="IPA" if (label or True) else None)
    if col_lox_alt is not None:
        plot_linfit(ax, X_lox, data.mfr_lox, col=col_lox_alt)
    if col_ipa_alt is not None:
        plot_linfit(ax, X_ipa, data.mfr_ipa, col=col_ipa_alt)
    if col_lox_alt is not None:
        ax.axvline(data.A_lox * np.sqrt(data.target_DP_lox), color=col_lox_alt,
                ls="-.")
    if col_ipa_alt is not None:
        ax.axvline(data.A_ipa * np.sqrt(data.target_DP_ipa), color=col_ipa_alt,
                ls="-.")

    if label:
        ax.set_ylabel("MFR [kg/s]")
        ax.set_xlabel("A sqrt(DP) [m^2 Pa^0.5]")



def sweeps(win_peep):
    s26 = read("S26-sweep")
    s26_lox = read("S26-lox-sweep")
    s26_ipa = read("S26-ipa-sweep")

    peep(win_peep, s26, include_load_cells=True)
    peep(win_peep, s26_lox, include_load_cells=True)
    peep(win_peep, s26_ipa, include_load_cells=True)

    _, ax = geez.new_plots()
    peep_sweep(ax, s26, C_BLUE_0, C_BLUE_1, C_RED_0, C_RED_1)
    peep_sweep(ax, s26_lox, C_CYAN_0, C_CYAN_1, None, None, False)
    peep_sweep(ax, s26_ipa, None, None, C_PURPLE_0, C_PURPLE_1, False)



def main():
    win_peep = geez.new_window()

    all_names = []
    for num in range(25):
        if num == 0:
            continue
        if num == 13:
            continue
        all_names.append(f"RS{num}")
    for num in range(8):
        if num == 0:
            continue
        all_names.append(f"S{num}")
    all_names.append("S26")
    all_names.append("S26-ipa")
    all_names.append("S26-lox")


    names = {"R": [], "C": []}
    op_lox = {"R": [], "C": []}
    op_ipa = {"R": [], "C": []}
    for name in all_names:
        csv_name = None
        if name == "RS21":
            csv_name = r"RS21\.2"
        data = read(name, csv_name=csv_name)
        # peep(win_peep, data, include_load_cells=True)

        prefix = "R" if name.startswith("RS") else "C"
        names[prefix].append(name)
        if not name.endswith("-ipa"):
            x_lox = data.A_lox * np.sqrt(np.maximum(0.0, data.DP_lox.mean()))
            op_lox[prefix].append([x_lox, data.mfr_lox.mean()])
        else:
            op_lox[prefix].append([np.nan, np.nan])
        if not name.endswith("-lox"):
            x_ipa = data.A_ipa * np.sqrt(np.maximum(0.0, data.DP_ipa.mean()))
            op_ipa[prefix].append([x_ipa, data.mfr_ipa.mean()])
        else:
            op_ipa[prefix].append([np.nan, np.nan])

    sweeps(win_peep)


    op_lox["R"] = np.array(op_lox["R"]).T
    op_ipa["R"] = np.array(op_ipa["R"]).T
    op_lox["C"] = np.array(op_lox["C"]).T
    op_ipa["C"] = np.array(op_ipa["C"]).T

    fig = plt.figure()
    ax = fig.add_subplot(axes_class=geez.BrAxes)
    ax.scatter(op_lox["R"][0], op_lox["R"][1], zorder=3, color=C_BLUE_0,
            alpha=0.7, label="resin LOx")
    ax.scatter(op_ipa["R"][0], op_ipa["R"][1], zorder=3, color=C_RED_0,
            alpha=0.7, label="resin IPA")
    ax.scatter(op_lox["C"][0], op_lox["C"][1], zorder=3, color=C_CYAN_0,
            alpha=0.7, label="copper LOx", edgecolors="#FF950A")
    ax.scatter(op_ipa["C"][0], op_ipa["C"][1], zorder=3, color=C_PURPLE_0,
            alpha=0.7, label="copper IPA", edgecolors="#FF950A")
    plot_linfit(ax, op_lox["R"][0], op_lox["R"][1], C_BLUE_1)
    plot_linfit(ax, op_ipa["R"][0], op_ipa["R"][1], C_RED_1)
    plot_linfit(ax, op_lox["C"][0], op_lox["C"][1], C_CYAN_1)
    plot_linfit(ax, op_ipa["C"][0], op_ipa["C"][1], C_PURPLE_1)
    ax.axhline(MARK_MFR_LOX,     color=C_BLUE_0, ls="--")
    ax.axhline(MARK_MFR_LOX_ALT, color=C_BLUE_1, ls="-.")
    ax.axhline(MARK_MFR_IPA,     color=C_RED_0, ls="--")
    ax.axhline(MARK_MFR_IPA_ALT, color=C_RED_1, ls="-.")
    ax.set_xlabel("A sqrt(DP) [m^2 Pa^0.5]")
    ax.set_ylabel("mfr [kg/s]")

    def labels(ax, op, names, DX, DY, col, lcol):
        for i, (x, y) in enumerate(zip(op[0], op[1])):
            X = x
            Y = y
            X += 0.5 * (X - np.nanmean(op[0]))
            Y += 0.5 * (Y - np.nanmean(op[1]))
            X += DX
            Y += DY
            x0 = min(x, X)
            x1 = max(x, X)
            y0 = y if x < X else Y
            y1 = Y if x < X else y
            ax.text(X, Y, str(names[i]), color=col, fontsize=9, zorder=3,
                horizontalalignment="center",
                verticalalignment="center",
                bbox=dict(
                    boxstyle="round,pad=0.1",
                    facecolor="white",
                    edgecolor="white",
                    linewidth=0
                )
            )
            ax.plot([x0, x1], [y0, y1], color=lcol, lw=1.0)

    labels(ax, op_lox["R"], names["R"], -1.2e-3, +0.08, C_BLUE_0, "gray")
    labels(ax, op_lox["C"], names["C"], -1.2e-3, +0.08, C_CYAN_0, "#B59F82")
    labels(ax, op_ipa["R"], names["R"], +1.0e-3, -0.08, C_RED_0, "gray")
    labels(ax, op_ipa["C"], names["C"], +1.0e-3, -0.08, C_PURPLE_0, "#B59F82")

    fig.show()

    ax.set_title("CLICK 1: Select a point for LOx", fontsize=14, color="blue",
            fontweight="bold")
    lox_selection = fig.ginput(1, timeout=0)
    x_lox, y_lox = lox_selection[0]
    ax.plot(x_lox, y_lox, "bx", markersize=10, mew=2)

    ax.set_title("CLICK 2: Select a point for IPA", fontsize=14, color="red",
            fontweight="bold")
    ipa_selection = fig.ginput(1, timeout=0)
    x_ipa, y_ipa = ipa_selection[0]
    ax.plot(x_ipa, y_ipa, "rx", markersize=10, mew=2)

    ax.set_title("Selections Captured!", color="green")

    fig.show()

    X, mdot_lox, mdot_ipa = mdot_lookups()

    chosen_mdot_lox = float(np.interp(x_lox, X, mdot_lox))
    chosen_mdot_ipa = float(np.interp(x_ipa, X, mdot_ipa))
    # note that the MARK mfrs must be the design mfrs of the lookup table.
    print(f"For a LOx X of: {x_lox}")
    print(f"     pick mdot: {chosen_mdot_lox}")
    print(f"       (Kmdot): {chosen_mdot_lox / MARK_MFR_LOX}")
    print(f"For an IPA X of: {x_ipa}")
    print(f"      pick mdot: {float(np.interp(x_ipa, X, mdot_ipa))}")
    print(f"        (Kmdot): {chosen_mdot_ipa / MARK_MFR_IPA}")



if __name__ == "__main__":
    with geez.instance():
        main()
