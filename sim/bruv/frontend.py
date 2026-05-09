"""
GUI front-end for the c back-end.
"""

import json
import math
import sys
import time
from contextlib import contextmanager

import numpy as np
import matplotlib.ticker

from . import bridge
from . import geez
from . import paths

__all__ = ["run"]


def get_interpretation():
    interp = bridge.Interpretation()
    IN = interp.INPUT
    OUT = interp.OUTPUT

    interp.append("Lstar", interp.F64, IN)
    interp.append("R_cc", interp.F64, IN)
    interp.append("L_cc", interp.F64, OUT)
    interp.append("R_tht", interp.F64, OUT)
    interp.append("R_exit", interp.F64, OUT)
    interp.append("z_tht", interp.F64, OUT)
    interp.append("z_exit", interp.F64, OUT)
    interp.append("A_tht", interp.F64, OUT)
    interp.append("AEAT", interp.F64, OUT)
    interp.append("NLF", interp.F64, IN)
    interp.append("phi_conv", interp.F64, IN)
    interp.append("phi_div", interp.F64, OUT)
    interp.append("phi_exit", interp.F64, OUT)

    interp.append("prop_fc", interp.F64, IN)
    interp.append("helix_angle", interp.F64, IN | OUT)
    interp.append("th_pdms", interp.F64, IN)
    interp.append("k_pdms", interp.F64, IN)
    interp.append("th_iw", interp.F64, IN | OUT)
    interp.append("th_ow", interp.F64, IN | OUT)
    interp.append("no_chnl", interp.I64, IN)
    interp.append("th_chnl", interp.F64, IN | OUT)
    interp.append("prop_chnl", interp.F64, IN | OUT)
    interp.append("wi_web", interp.F64, OUT)
    interp.append("wi_chnl", interp.F64, OUT)
    interp.append("psi_chnl", interp.F64, OUT)
    interp.append("eps_chnl", interp.F64, IN)
    interp.append("Pr_fu", interp.F64, IN)
    interp.append("T_fu0", interp.F64, IN)
    interp.append("P_fu0", interp.F64, OUT)
    interp.append("T_fu1", interp.F64, OUT)
    interp.append("P_fu1", interp.F64, OUT)

    interp.append("ofr", interp.F64, IN | OUT)
    interp.append("dm_cc", interp.F64, IN | OUT)
    interp.append("dm_ox", interp.F64, OUT)
    interp.append("dm_fu", interp.F64, OUT)
    interp.append("P_exit", interp.F64, IN)
    interp.append("M_exit", interp.F64, OUT)
    interp.append("gamma_exit", interp.F64, OUT)
    interp.append("P0_cc", interp.F64, IN)
    interp.append("T0_cc", interp.F64, OUT)
    interp.append("rho0_cc", interp.F64, OUT)
    interp.append("gamma_tht", interp.F64, OUT)
    interp.append("Mw_tht", interp.F64, OUT)
    interp.append("Isp", interp.F64, OUT)
    interp.append("Thrust", interp.F64, OUT)
    interp.append("efficiency", interp.F64, OUT)

    interp.append("min_SF", interp.F64, OUT)
    interp.append("possible_system", interp.I64, OUT)

    interp.append("out_count", interp.I64, IN)
    interp.append("out_z", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_r", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_M_g", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_T_g", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_P_g", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_rho_g", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_gamma_g", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_cp_g", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_mu_g", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_Pr_g", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_T_c", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_P_c", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_T_gw", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_T_pdms", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_T_wg", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_T_wc", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_q", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_h_g", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_h_c", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_vel_c", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_rho_c", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_ff_c", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_Re_c", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_Pr_c", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_startup_sigma", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_startup_Ys", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_startup_SF", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_sigmah_pressure", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_sigmah_thermal", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_sigmah_bending", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_sigmah", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_sigmam", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_sigma_vm", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_Ys", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_SF", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_xtra", interp.PTR_F64, IN | interp.OUTPUT_DATA)

    interp.append("export_count", interp.I64, IN)
    interp.append("export_z", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("export_helix_angle", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("export_th_chnl", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("export_psi_chnl", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("export_th_iw", interp.PTR_F64, IN | interp.OUTPUT_DATA)

    interp.append("target_Thrust", interp.F64, IN)
    interp.append("optimise_ofr", interp.I64, IN)
    interp.append("optimise_dm_cc", interp.I64, IN)
    interp.append("optimise_helix_angle", interp.I64, IN)
    interp.append("optimise_th_iw", interp.I64, IN)
    interp.append("optimise_th_ow", interp.I64, IN)
    interp.append("optimise_th_chnl", interp.I64, IN)
    interp.append("optimise_prop_chnl", interp.I64, IN)

    interp.finalise()
    return interp

def get_state(interp):
    with open(paths.ROOT / "../config/all.json", "r") as f:
        config = json.load(f)

    state = bridge.State(interp)

    state["Lstar"] = 0.8
    state["R_cc"] = config["part_mating"]["R_cc"] * 1e-3
    state["NLF"] = config["chamber"]["NLF"]
    state["phi_conv"] = config["chamber"]["phi_conv"]

    state["prop_fc"] = 0.15
    state["helix_angle"] = math.radians(30)
    state["th_pdms"] = 30e-6
    state["k_pdms"] = 1.3
    state["th_iw"] = 1.1e-3
    state["th_ow"] = 3.5e-3
    state["no_chnl"] = 40
    state["th_chnl"] = 1.5e-3
    state["prop_chnl"] = 0.6
    state["eps_chnl"] = 135e-6
    state["Pr_fu"] = config["operating_conditions"]["Pr_IPA"]
    state["T_fu0"] = config["operating_conditions"]["T_IPA"]

    state["ofr"] = 1.4
    state["dm_cc"] = 2.152551267131888
    state["P_exit"] = config["operating_conditions"]["P_exit"]
    state["P0_cc"] = config["operating_conditions"]["P_cc"]

    state["out_count"] = 1000
    new_out = lambda: np.empty(shape=(state["out_count"],), dtype=np.float64)
    state["out_z"] = new_out()
    state["out_r"] = new_out()
    state["out_M_g"] = new_out()
    state["out_T_g"] = new_out()
    state["out_P_g"] = new_out()
    state["out_rho_g"] = new_out()
    state["out_gamma_g"] = new_out()
    state["out_cp_g"] = new_out()
    state["out_mu_g"] = new_out()
    state["out_Pr_g"] = new_out()
    state["out_T_c"] = new_out()
    state["out_P_c"] = new_out()
    state["out_T_gw"] = new_out()
    state["out_T_pdms"] = new_out()
    state["out_T_wg"] = new_out()
    state["out_T_wc"] = new_out()
    state["out_q"] = new_out()
    state["out_h_g"] = new_out()
    state["out_h_c"] = new_out()
    state["out_vel_c"] = new_out()
    state["out_rho_c"] = new_out()
    state["out_ff_c"] = new_out()
    state["out_Re_c"] = new_out()
    state["out_Pr_c"] = new_out()
    state["out_startup_sigma"] = new_out()
    state["out_startup_Ys"] = new_out()
    state["out_startup_SF"] = new_out()
    state["out_sigmah_pressure"] = new_out()
    state["out_sigmah_thermal"] = new_out()
    state["out_sigmah_bending"] = new_out()
    state["out_sigmah"] = new_out()
    state["out_sigmam"] = new_out()
    state["out_sigma_vm"] = new_out()
    state["out_Ys"] = new_out()
    state["out_SF"] = new_out()
    state["out_xtra"] = new_out()

    state["export_count"] = 3000
    new_export = lambda: np.empty(shape=(state["export_count"],),
            dtype=np.float64)
    state["export_z"] = new_export()
    state["export_helix_angle"] = new_export()
    state["export_th_chnl"] = new_export()
    state["export_psi_chnl"] = new_export()
    state["export_th_iw"] = new_export()

    state["target_Thrust"] = config["operating_conditions"]["Thrust"]
    state["optimise_ofr"] = 0
    state["optimise_dm_cc"] = 0
    state["optimise_helix_angle"] = 0
    state["optimise_th_iw"] = 0
    state["optimise_th_ow"] = 0
    state["optimise_th_chnl"] = 0
    state["optimise_prop_chnl"] = 0

    return state

def write_ammendments(state):

    def part_mating():
        # all in mm.

        with open(paths.ROOT / "../config/all.json", "r") as f:
            pm = json.load(f)["part_mating"]

        data = {}
        r = 0.0
        def push_boundary(name, length):
            nonlocal r
            r += length
            data[name] = round(r, 4)
        def push_inner_outer(name_inner, name_outer, gap):
            nonlocal r
            length = pm[name_outer] - pm[name_inner]
            r += gap
            data[name_inner] = round(r, 4)
            r += length
            data[name_outer] = round(r, 4)
        def push_middle_length(name_middle, name_length, gap):
            nonlocal r
            length = pm[name_length]
            r += gap
            data[name_middle] = round(r + 0.5*length, 4)
            r += length
            data[name_length] = round(length, 4)

        push_boundary("R_cc", state["R_cc"] * 1e3)
        push_inner_outer("IR_Ioring", "OR_Ioring", 3.0)
        push_middle_length("Mr_chnl", "max_th_chnl", 3.0)
        push_inner_outer("IR_Ooring", "OR_Ooring", 3.0)
        push_middle_length("r_bolt", "D_bolt", 4.85)
        push_boundary("flange_outer_radius", -1.15)
        return data

    # TODO: element+fc placement?

    mprop_fc = state["prop_fc"]
    dm_fc = mprop_fc * state["dm_fu"]
    DP_ipa = state["P_fu1"] - state["P0_cc"]
    rho_fu1 = state["out_rho_c"].view(state["out_count"])[0]

    min_hole_diam = 0.5e-3 # reasonable minimum hole diameter for post-machining
    min_hole_area = math.pi/4 * min_hole_diam**2
    Cd_fc = 0.65 # discharge coefficient
    A_fc_tot = dm_fc / Cd_fc / math.sqrt(2*rho_fu1*DP_ipa)
    no_fc = max(1, int(A_fc_tot / min_hole_area))
    A_fc = A_fc_tot / no_fc
    D_fc = 2.0 * math.sqrt(A_fc / math.pi)

    print(f"film cooling")
    print(f"     mdot: {dm_fc:.4f} kg/s ({int(100*mprop_fc)}%)")
    print(f"  rho_fu1: {rho_fu1:.1f} kg/m3")
    print(f"    holes: {no_fc}X {D_fc*1e3:.2f} mm (diam.)")

    extra = {
        "operating_conditions": {
            "Thrust": state["Thrust"],
            "P_cc": state["P0_cc"],
            "P_exit": state["P_exit"],
            "Pr_IPA": state["Pr_fu"],
            "T_IPA": state["T_fu0"],
            "mdot_LOx": state["dm_ox"],
            "mdot_IPA": state["dm_fu"],
        },
        "part_mating": part_mating(),
        "chamber": {
            "L_cc": state["L_cc"] * 1e3,
            "AEAT": state["AEAT"],
            "R_tht": state["R_tht"] * 1e3,
            "NLF": state["NLF"],
            "phi_conv": state["phi_conv"],
            "phi_div": state["phi_div"],
            "phi_exit": state["phi_exit"],
            "th_ow": state["th_ow"] * 1e3,
        },
        "injector" : {
            "D_fc": D_fc * 1e3,
            "no_fcg": [no_fc],
            "theta0_fcg": [0.0]
        },
    }
    with open(paths.ROOT / "../config/ammendments.json", "w") as f:
        json.dump(extra, f, indent=4)
        f.write("\n") # trailing newline smile


    def chamber():
        get_export = lambda s: state[f"export_{s}"].view(state["export_count"])

        z = 1e3*get_export("z")
        helix_angle = get_export("helix_angle")
        th_chnl = 1e3*get_export("th_chnl")
        psi_chnl = 1e3*get_export("psi_chnl")
        # trim superfluous values.
        mask = np.zeros(len(z), dtype=bool)
        mask[0] = True # always keep endpoints
        for Y in (helix_angle, th_chnl, psi_chnl):
            mask[1:] |= (Y[:-1] != Y[1:])
        z = z[mask]
        helix_angle = helix_angle[mask]
        th_chnl = th_chnl[mask]
        psi_chnl = psi_chnl[mask]
        channels = {
            "no": state["no_chnl"],
            "z": z.tolist(),
            "helix_angle": helix_angle.tolist(),
            "th": th_chnl.tolist(),
            "psi": psi_chnl.tolist(),
        }

        z = 1e3*get_export("z")
        th_iw = 1e3*get_export("th_iw")
        mask = np.zeros(len(z), dtype=bool)
        mask[0] = mask[-1] = True
        for Y in (th_iw,):
            mask[1:-1] |= (Y[:-2] != Y[2:])
        z = z[mask]
        th_iw = th_iw[mask]
        inner_wall = {
            "z": z.tolist(),
            "th": th_iw.tolist(),
        }

        return {
            "channels": channels,
            "inner_wall": inner_wall,
        }

    with open(paths.ROOT / "../config/chamber.json", "w") as f:
        json.dump(chamber(), f, indent=None)
        f.write("\n") # trailing newline double smile



def now_this_is_bruv():
    interp = get_interpretation()
    state = get_state(interp)
    print(state)

    @contextmanager
    def time_me(label):
        start = time.perf_counter()
        try:
            yield
        finally:
            end = time.perf_counter()
            print(f"{label} took {end - start:.4g} s.")
    with time_me("Simulation"):
        ret = state.execute()
    if ret is not None:
        print("FAILED:", ret)
        return 1

    print(state)
    write_ammendments(state)

    get_out = lambda s: state[f"out_{s}"].view(state["out_count"])
    plot_me(state, get_out)

    return 0





def plot_me(state, get_out):
    g = get_out
    z = 1e3*g("z")

    colors = [
        # Reds / Pinks
        "crimson", "firebrick", "indianred", "lightcoral", "salmon",
        "darksalmon", "rosybrown", "palevioletred", "mediumvioletred",

        # Oranges
        "darkorange", "coral", "tomato", "orangered", "chocolate", "sienna",

        # Yellows / Golds
        "gold", "goldenrod", "darkgoldenrod", "khaki", "darkkhaki",

        # Greens
        "forestgreen", "seagreen", "mediumseagreen", "limegreen",
        "olivedrab", "olive", "darkolivegreen", "springgreen",

        # Cyans / Teals
        "teal", "darkcyan", "cadetblue", "lightseagreen", "turquoise",
        "mediumturquoise", "paleturquoise",

        # Blues
        "steelblue", "royalblue", "dodgerblue", "deepskyblue",
        "cornflowerblue", "slateblue", "mediumslateblue", "navy",

        # Purples
        "mediumpurple", "blueviolet", "darkviolet", "darkorchid",
        "mediumorchid", "plum", "thistle",

        # Browns / Earth tones
        "saddlebrown", "peru", "burlywood", "tan", "wheat",

        # Greys / Neutrals
        "dimgray", "gray", "darkgray", "silver", "lightgray",

        # Extras (still readable)
        "slategray", "lightslategray", "darkslategray",
    ]


    def graph1(ax, title, ylabel, *label_name_scale_colour_ls):
        assert len(label_name_scale_colour_ls) % 5 == 0
        for i in range(len(label_name_scale_colour_ls) // 5):
            label, name, scale, colour, ls = \
                label_name_scale_colour_ls[5*i:5*i + 5]
            ax.plot(z, scale*g(name), color=colour, ls=ls, label=label)
        ax.set_title(title)
        ax.set_xlabel("z [mm]")
        if len(label_name_scale_colour_ls)//5 > 1:
            colour = "black"
        ax.set_ylabel(ylabel, color=colour)
        ax.tick_params(axis="y", which="both", colors=colour, width=1.2)
        return ax

    def graph2(ax, title, Lylabel, Lname, Lscale, Lcolour, Lls,
                          Rylabel, Rname, Rscale, Rcolour, Rls):
        axR = ax.twinx()
        ax.plot(z, Lscale * g(Lname), color=Lcolour, ls=Lls)
        axR.plot(z, Rscale * g(Rname), color=Rcolour, ls=Rls)
        ax.set_title(title)
        ax.set_xlabel("z [mm]")
        ax.set_ylabel(Lylabel, color=Lcolour)
        axR.set_ylabel(Rylabel, color=Rcolour)
        ax.tick_params(axis="y", which="both", colors=Lcolour, width=1.2)
        axR.tick_params(axis="y", which="both", colors=Rcolour, width=1.2)

        def align_yaxis_nice(ax1, ax2):
            """
            Aligns the right axis (ax2) to the left axis (ax1) grid using "nice"
            whole numbers for the right axis ticks.
            """
            # 1. Get left axis visible ticks and limits.
            y1_min, y1_max = ax1.get_ylim()
            y1_ticks = ax1.get_yticks()
            y1_ticks = y1_ticks[(y1_ticks >= y1_min) & (y1_ticks <= y1_max)]

            n_intervals = len(y1_ticks) - 1
            if n_intervals <= 0:
                return

            # 2. Get the fractional position of the first and last left ticks.
            f0 = (y1_ticks[0] - y1_min) / (y1_max - y1_min)
            fn = (y1_ticks[-1] - y1_min) / (y1_max - y1_min)
            df = fn - f0

            # 3. Get right axis data limits.
            y2_min_data, y2_max_data = ax2.get_ylim()
            y2_data_range = y2_max_data - y2_min_data

            # 4. Determine a "nice" step size for the right axis.
            raw_step = (y2_data_range * df) / n_intervals
            exponent = np.floor(np.log10(raw_step))
            fraction = raw_step / 10**exponent

            # Round up to the nearest nice fraction.
            if fraction <= 1:
                nice_fraction = 1
            elif fraction <= 1.5:
                nice_fraction = 1.5
            elif fraction <= 2:
                nice_fraction = 2
            elif fraction <= 2.5:
                nice_fraction = 2.5
            elif fraction <= 4:
                nice_fraction = 4
            elif fraction <= 5:
                nice_fraction = 5
            else:
                nice_fraction = 10

            S_nice = nice_fraction * 10**exponent

            # 5. Calculate new limits to ensure data fits and ticks perfectly
            # align.
            R_range = (n_intervals * S_nice) / df

            # Calculate upper bound for our starting tick to ensure data stays
            # inside limits.
            upper_bound = y2_min_data + f0 * R_range

            # Snap the starting tick to the nearest valid multiple of our nice
            # step.
            Rt_0 = np.floor(upper_bound / S_nice) * S_nice

            # Calculate final right axis limits.
            R_min = Rt_0 - f0 * R_range
            R_max = R_min + R_range

            # Generate the nice major ticks.
            y2_ticks = Rt_0 + np.arange(n_intervals + 1) * S_nice

            # 6. Apply limits and ticks to ax2.
            ax2.set_ylim(R_min, R_max)
            ax2.set_yticks(y2_ticks)

            # 7. Add minor ticks.
            ax2.yaxis.set_minor_locator(matplotlib.ticker.AutoMinorLocator(5))
        align_yaxis_nice(ax, axR)

        return ax, axR


    fig, axes = geez.new_plots("combustion", rows=2, cols=3,
            fig_kw=dict(figsize=(13, 7)))
    ax = graph1(axes[0, 0], "Chamber contour",
        "r [mm]", None, "r", 1e3, "black", "-",
    )
    ax.axhline(0.0, color="grey", ls="--", lw=1.0)
    ax.plot(z, -1e3*g("r"), color="black", ls="-")
    ax.plot([0, 0], [-1e3*g("r")[0], 1e3*g("r")[0]], color="black", ls="-")
    ax.set_ylim([1.5e3*max(g("r")), -1.5e3*max(g("r"))])
    ax.set_aspect(1.0)
    graph1(axes[0, 1], "Chamber temperature",
        "T [K]", None, "T_g", 1, "crimson", "-",
    )
    graph1(axes[0, 2], "Chamber pressure",
        "P [bar]", None, "P_g", 1e-5, "saddlebrown", "-",
    )
    graph2(axes[1, 0], "Chamber density & mach number",
        "ρ [kg m⁻³]", "rho_g", 1, "mediumpurple", "-",
        "M [-]",      "M_g",   1, "cadetblue",    "-",
    )
    graph2(axes[1, 1], "Chamber specific heat & ratio",
        "cₚ [J kg⁻¹ K⁻¹]", "cp_g",    1, "teal",       "-",
        "γ [-]",          "gamma_g", 1, "darkorange", "-",
    )
    graph2(axes[1, 2], "Chamber viscosity & Prandtl number",
        "μ [Pa s]", "mu_g", 1, "seagreen",  "-",
        "Pr [-]",   "Pr_g", 1, "goldenrod", "-",
    )



    fig, axes = geez.new_plots("thermal", rows=2, cols=3,
            fig_kw=dict(figsize=(13, 7)))
    graph1(axes[0, 0], "Wall & coating temperatures",
        "T [K]", "pdms",         "T_pdms", 1, "mediumorchid", "-",
                 "wall-pdms",    "T_wg",   1, "tomato",    "-",
                 "wall-coolant", "T_wc",   1, "royalblue", "-",
    )
    graph1(axes[0, 1], "Heat flux",
        "q [MW m⁻²]", None, "q", 1e-6, "goldenrod", "-",
    )
    graph1(axes[0, 2], "Convection coefficients",
        "h [W m⁻² K⁻¹]", "combustion", "h_g", 1, "tomato",    "-",
                         "coolant",    "h_c", 1, "royalblue", "-",
    )
    graph2(axes[1, 0], "Coolant temperature & pressure",
        "T [K]",   "T_c", 1,    "crimson",     "-",
        "P [bar]", "P_c", 1e-5, "saddlebrown", "-",
    )
    graph2(axes[1, 1], "Coolant density & velocity",
        "ρ [kg m⁻³]", "rho_c", 1, "mediumpurple", "-",
        "v [m s⁻¹]",  "vel_c", 1, "seagreen",     "-",
    )
    graph1(axes[1, 2], "Coolant Reynolds number",
        "Re [-]", None, "Re_c", 1, "steelblue", "-",
    )



    fig, axes = geez.new_plots("structural", rows=2, cols=3,
            fig_kw=dict(figsize=(13, 7)))
    graph1(axes[0, 0], "Hoop stresses", r"$\sigma$ [MPa]",
        "pressure", "sigmah_pressure", 1e-6, "steelblue",   "-",
        "thermal",  "sigmah_thermal",  1e-6, "crimson",     "-",
        "bending",  "sigmah_bending",  1e-6, "forestgreen", "-",
        "total",    "sigmah",          1e-6, "grey",        "--",
    )
    graph1(axes[0, 1], "Net stresses & yeild strength", r"$\sigma$ [MPa]",
        "hoop",       "sigmah",   1e-6, "darkviolet", "-",
        "meridional", "sigmam",   1e-6, "seagreen",   "-",
        "von-mises",  "sigma_vm", 1e-6, "darkorange", "-",
        "Ys",         "Ys",       1e-6, "black",      "--",
    )
    ax = graph1(axes[0, 2], "Safety factor",
        "SF [-]", None, "SF", 1, "blue", "-",
    )
    ax.axhline(1.0, color="red", ls="--", lw=1.2)

    axes[1, 0].axis("off")
    axes[1, 0].text(0.5, 0.5,
        "Start-up transient →\n(only channels pressurised)",
        ha="center", va="center", transform=axes[1, 0].transAxes
    )
    graph1(axes[1, 1], "Start-up stress & yield strength", r"$\sigma$ [MPa]",
        "stress", "startup_sigma", 1e-6, "darkorange", "-",
        "Ys",     "startup_Ys",    1e-6, "black",      "--",
    )
    ax = graph1(axes[1, 2], "Start-up safety factor",
        "SF [-]", None, "startup_SF", 1, "blue", "-",
    )
    ax.axhline(1.0, color="red", ls="--", lw=1.2)




def run():
    with geez.instance():
        return now_this_is_bruv()


if __name__ == "__main__":
    if len(sys.argv) > 1:
        raise RuntimeError("bruv.frontend does not have command line args")
    sys.exit(run())
