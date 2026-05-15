import os
import re

import pandas as pd
import numpy as np

import geez


RESIN_FACTOR = 2.045771 / 2.525993

FLOW_METER_1_FACTOR = 1.0 / 0.993350
FLOW_METER_2_FACTOR = 1.0 / 1.104725

LOX_DENSITY = 1141.0
IPA_DENSITY = 735.0


def denoise(Y, *, window_size=8):
    window = np.ones(window_size) / window_size
    return np.convolve(Y, window, mode="same")

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
    data.number = int(name[2:] if name.startswith("RS") else name[1:])

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



def main():
    win = geez.new_window()

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


    names = {"R": [], "C": []}
    op_lox = {"R": [], "C": []}
    op_ipa = {"R": [], "C": []}
    for name in all_names:
        csv_name = None
        if name == "RS21":
            csv_name = r"RS21\.2"
        data = read(name, csv_name=csv_name)
        peep(win, data)

        x_lox = data.A_lox * np.sqrt(data.DP_lox.mean())
        x_ipa = data.A_ipa * np.sqrt(data.DP_ipa.mean())
        prefix = "R" if name.startswith("RS") else "C"
        names[prefix].append(name)
        op_lox[prefix].append([x_lox, data.mfr_lox.mean()])
        op_ipa[prefix].append([x_ipa, data.mfr_ipa.mean()])


    def plot_linfit(ax, X, Y, col):
        m, c = np.polyfit(X, Y, 1)
        x = np.linspace(0.0, 1.2*X.max(), 2)
        ax.plot(x, m*x + c, color=col)

    op_lox["R"] = np.array(op_lox["R"]).T
    op_ipa["R"] = np.array(op_ipa["R"]).T
    op_lox["C"] = np.array(op_lox["C"]).T
    op_ipa["C"] = np.array(op_ipa["C"]).T

    _, ax = geez.new_plots()
    ax.scatter(op_lox["R"][0], op_lox["R"][1], zorder=3, color=C_BLUE_0,
            label="resin LOx")
    ax.scatter(op_ipa["R"][0], op_ipa["R"][1], zorder=3, color=C_RED_0,
            label="resin IPA")
    ax.scatter(op_lox["C"][0], op_lox["C"][1], zorder=3, color=C_CYAN_0,
            label="copper LOx", edgecolors="#FF950A")
    ax.scatter(op_ipa["C"][0], op_ipa["C"][1], zorder=3, color=C_PURPLE_0,
            label="copper IPA", edgecolors="#FF950A")
    plot_linfit(ax, op_lox["R"][0], op_lox["R"][1], C_BLUE_1)
    plot_linfit(ax, op_ipa["R"][0], op_ipa["R"][1], C_RED_1)
    plot_linfit(ax, op_lox["C"][0], op_lox["C"][1], C_CYAN_1)
    plot_linfit(ax, op_ipa["C"][0], op_ipa["C"][1], C_PURPLE_1)
    ax.axhline(1.2556549058269346 / 9, color=C_BLUE_0, ls="--")
    ax.axhline(0.8968963613049534 / 9, color=C_RED_0, ls="--")
    ax.set_xlabel("A sqrt(DP) [m^2 Pa^0.5]")
    ax.set_ylabel("mfr [kg/s]")

    def labels(ax, op, names, DX, DY, col, lcol):
        for i, (x, y) in enumerate(zip(op[0], op[1])):
            X = x
            Y = y
            X += 0.5 * (X - op[0].mean())
            Y += 0.5 * (Y - op[1].mean())
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

    labels(ax, op_lox["R"], names["R"], -0.12e-2, +0.08, C_BLUE_0, "gray")
    labels(ax, op_lox["C"], names["C"], -0.12e-2, +0.08, C_CYAN_0, "#B59F82")
    labels(ax, op_ipa["R"], names["R"], +0.10e-2, -0.08, C_RED_0, "gray")
    labels(ax, op_ipa["C"], names["C"], +0.10e-2, -0.08, C_PURPLE_0, "#B59F82")



if __name__ == "__main__":
    with geez.instance():
        main()
