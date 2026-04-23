"""
GUI front-end for the c back-end.
"""

import json
import math
import sys
import time
from contextlib import contextmanager

import numpy as np

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

    dm_ox = config["operating_conditions"]["mdot_LOx"]
    dm_fu = config["operating_conditions"]["mdot_IPA"]
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
        mask[0] = mask[-1] = True # always keep endpoints
        for Y in (helix_angle, th_chnl, psi_chnl):
            mask[1:-1] |= (Y[:-2] != Y[2:])
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
    plot_me(get_out)

    return 0





def plot_me(get_out):
    g = get_out
    z = 1e3*g("z")


    win = geez.new_window()

    _, axes = win.new_plots(rows=2, cols=3,
            fig_kw=dict(figsize=(13, 7)))
    axes[0,0].set_aspect(1.0)
    axes[0,0].plot(z, get_out("r")*1e3)
    axes[0,0].set_title("contour [mm]")
    axes[1,0].plot(z, get_out("M_g"))
    axes[1,0].set_title("mach number")
    axes[0,1].plot(z, get_out("T_g"))
    axes[0,1].set_title("temperature [K]")
    axes[1,1].plot(z, get_out("P_g")*1e-5)
    axes[1,1].set_title("pressure [bar]")
    axes[0,2].plot(z, get_out("rho_g"))
    axes[0,2].set_title("density [kg/m3]")
    axes[1,2].plot(z, get_out("gamma_g"))
    axes[1,2].set_title("gamma")

    _, axes = win.new_plots(rows=2, cols=3,
            fig_kw=dict(figsize=(13, 7)))
    axes[0,0].set_aspect(1.0)
    axes[0,0].plot(z, get_out("r")*1e3)
    axes[0,0].set_title("contour [mm]")
    axes[1,0].plot(z, get_out("M_g"))
    axes[1,0].set_title("mach number")
    axes[0,1].plot(z, get_out("cp_g"))
    axes[0,1].set_title("specific heat [J/kg/K]")
    axes[1,1].plot(z, get_out("mu_g"))
    axes[1,1].set_title("viscosity [Pa*s]")
    axes[0,2].plot(z, get_out("Pr_g"))
    axes[0,2].set_title("prandtl")

    _, axes = win.new_plots(rows=2, cols=3,
            fig_kw=dict(figsize=(13, 7)))
    axes[0,0].plot(z, get_out("T_gw"), label="gas-ish")
    axes[0,0].plot(z, get_out("T_pdms"), "--", label="pdms surface")
    axes[0,0].plot(z, get_out("T_wg"), label="wall gas-side")
    axes[0,0].plot(z, get_out("T_wc"), label="wall coolant-side")
    ax_q = axes[0,0].twinx()
    ax_q.plot(z, get_out("q")*1e-6, "-.", color="black", label="heat flux")
    axes[0,0].set_title("temperature / heat flux [K / MW]")
    axes[1,0].plot(z, get_out("h_g"), label="gas")
    axes[1,0].plot(z, get_out("h_c"), label="coolant")
    axes[1,0].set_title("convection coefficients")
    axes[0,1].plot(z, get_out("Re_c"))
    axes[0,1].set_title("reynolds number")
    axes[1,1].plot(z, get_out("SF"))
    axes[1,1].set_title("SF")
    axes[0,2].plot(z, get_out("T_c"))
    axes[0,2].set_title("coolant temperature [K]")
    axes[1,2].plot(z, get_out("P_c")*1e-5)
    axes[1,2].set_title("coolant pressure [bar]")



    fig, axes = geez.new_plots("thermal", rows=2, cols=3,
            fig_kw=dict(figsize=(13, 7)))
    ax_Tw, ax_q, ax_h, ax_PT, ax_rv, ax_re = axes.flat

    KW = dict(lw=1.6)

    # ── Re ────────────────────────────────────────────────────────────────────────
    ax_re.plot(z, g("Re_c"), color="steelblue", **KW)
    ax_re.set_title("Reynolds number")
    ax_re.set_xlabel("z [mm]"); ax_re.set_ylabel("Re [-]")
    ax_re.grid(alpha=0.4)

    # ── T & P  (twin) ─────────────────────────────────────────────────────────────
    ax_PT2 = ax_PT.twinx()
    ax_PT.plot(z, g("T_c"),  color="crimson",     label="T_c", **KW)
    ax_PT2.plot(z, g("P_c")*1e-5, color="saddlebrown", label="P_c", **KW)
    ax_PT.set_title("Fluid temperature & pressure")
    ax_PT.set_xlabel("z [mm]"); ax_PT.set_ylabel("T [K]", color="crimson")
    ax_PT2.set_ylabel("P [bar]", color="saddlebrown")
    ax_PT.tick_params(axis="y", colors="crimson")
    ax_PT2.tick_params(axis="y", colors="saddlebrown")
    lines = ax_PT.get_lines() + ax_PT2.get_lines()
    ax_PT.legend(lines, [l.get_label() for l in lines], loc="center right")
    ax_PT.grid(alpha=0.4)

    # ── rho & vel  (twin) ─────────────────────────────────────────────────────────
    ax_rv2 = ax_rv.twinx()
    ax_rv.plot(z, g("rho_c"),  color="mediumpurple", label="rho_c", **KW)
    ax_rv2.plot(z, g("vel_c"), color="seagreen",     label="vel_c", **KW)
    ax_rv.set_title("Fluid density & velocity")
    ax_rv.set_xlabel("z [mm]"); ax_rv.set_ylabel("ρ [kg m⁻³]", color="mediumpurple")
    ax_rv2.set_ylabel("v [m s⁻¹]", color="seagreen")
    ax_rv.tick_params(axis="y", colors="mediumpurple")
    ax_rv2.tick_params(axis="y", colors="seagreen")
    lines = ax_rv.get_lines() + ax_rv2.get_lines()
    ax_rv.legend(lines, [l.get_label() for l in lines], loc="best")
    ax_rv.grid(alpha=0.4)

    # ── Wall temperatures ─────────────────────────────────────────────────────────
    ax_Tw.plot(z, g("T_wg"),   color="tomato",       label="T_wg",   **KW)
    ax_Tw.plot(z, g("T_wc"),   color="royalblue",    label="T_wc",   **KW)
    ax_Tw.plot(z, g("T_pdms"), color="mediumorchid", label="T_pdms", **KW)
    ax_Tw.set_title("Wall & coating temperatures")
    ax_Tw.set_xlabel("z [mm]"); ax_Tw.set_ylabel("T [K]")
    ax_Tw.legend(); ax_Tw.grid(alpha=0.4)

    # ── Convection coefficients ───────────────────────────────────────────────────
    ax_h.plot(z, g("h_g"), color="tomato",    label="h_g", **KW)
    ax_h.plot(z, g("h_c"), color="royalblue", label="h_c", **KW)
    ax_h.set_title("Convection coefficients")
    ax_h.set_xlabel("z [mm]"); ax_h.set_ylabel("h [W m⁻² K⁻¹]")
    ax_h.legend(); ax_h.grid(alpha=0.4)

    # ── Heat flux ─────────────────────────────────────────────────────────────────
    ax_q.plot(z, g("q")*1e-6, color="goldenrod", **KW)
    ax_q.set_title("Heat flux")
    ax_q.set_xlabel("z [mm]"); ax_q.set_ylabel("q [MW m⁻²]")
    ax_q.grid(alpha=0.4)



    fig, axes = geez.new_plots("structural", rows=1, cols=3,
            fig_kw=dict(figsize=(13, 4)))
    ax_comp, ax_res, ax_SF = axes.flat

    KW = dict(lw=1.6)

    # ── Hoop stress components ────────────────────────────────────────────────────
    ax_comp.plot(z, 1e-6*g("sigmah_pressure"), color="steelblue",  label=r"$\sigma_{h,pressure}$",       **KW)
    ax_comp.plot(z, 1e-6*g("sigmah_thermal"),  color="crimson",    label=r"$\sigma_{h,thermal}$",       **KW)
    ax_comp.plot(z, 1e-6*g("sigmah_bending"),  color="darkorange", label=r"$\sigma_{h,bending}$",       **KW)
    ax_comp.plot(z, 1e-6*g("sigmah"),          color="grey",      label=r"$\sigma_{h,total}$", lw=2, ls="--")
    ax_comp.set_title("Hoop stress components")
    ax_comp.set_xlabel("z [mm]"); ax_comp.set_ylabel(r"$\sigma$ [MPa]")
    ax_comp.legend(); ax_comp.grid(alpha=0.4)

    # ── Resultant stresses + yield strength ───────────────────────────────────────
    ax_res.plot(z, 1e-6*g("sigmah"),   color="steelblue", label=r"$\sigma_h$",    **KW)
    ax_res.plot(z, 1e-6*g("sigmam"),   color="seagreen",  label=r"$\sigma_m$",    **KW)
    ax_res.plot(z, 1e-6*g("sigma_vm"), color="crimson",   label=r"$\sigma_{vm}$", **KW)
    ax_res.plot(z, 1e-6*g("Ys"),       color="black",     label=r"$Y_s$", lw=2, ls="--")
    ax_res.set_title(r"Resultant stresses \& yield strength")
    ax_res.set_xlabel("z [mm]"); ax_res.set_ylabel(r"$\sigma$ [MPa]")
    ax_res.legend(); ax_res.grid(alpha=0.4)

    # ── Safety factor ─────────────────────────────────────────────────────────────
    ax_SF.plot(z, g("SF"), color="blue", **KW)
    ax_SF.axhline(1.0, color="red", lw=1.2, ls="--")
    ax_SF.set_title("Safety factor")
    ax_SF.set_xlabel("z [mm]"); ax_SF.set_ylabel("SF [-]")
    ax_SF.grid(alpha=0.4)




    fig, axes = geez.new_plots("startup_structural", rows=1, cols=2,
            fig_kw=dict(figsize=(13, 4)))
    ax_comp, ax_SF = axes.flat

    KW = dict(lw=1.6)

    # ── Hoop stress components ────────────────────────────────────────────────────
    ax_comp.plot(z, 1e-6*g("startup_sigma"),  color="darkorange", label=r"$\sigma_{h,bending}$",       **KW)
    ax_comp.plot(z, 1e-6*g("startup_Ys"),             color="black",     label=r"$Y_s$", lw=2, ls="--")
    ax_comp.set_title("Hoop stress components")
    ax_comp.set_xlabel("z [mm]"); ax_comp.set_ylabel(r"$\sigma$ [MPa]")
    ax_comp.legend(); ax_comp.grid(alpha=0.4)

    # ── Safety factor ─────────────────────────────────────────────────────────────
    ax_SF.plot(z, g("startup_SF"), color="blue", **KW)
    ax_SF.axhline(1.0, color="red", lw=1.2, ls="--")
    ax_SF.set_title("Safety factor")
    ax_SF.set_xlabel("z [mm]"); ax_SF.set_ylabel("SF [-]")
    ax_SF.grid(alpha=0.4)



def run():
    with geez.instance():
        return now_this_is_bruv()


if __name__ == "__main__":
    if len(sys.argv) > 1:
        raise RuntimeError("bruv.frontend does not have command line args")
    sys.exit(run())
