"""
GUI front-end for the c back-end.
"""

import json
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

    interp.append("helix_angle", interp.F64, IN)
    interp.append("th_iw", interp.F64, IN)
    interp.append("no_chnl", interp.I64, IN)
    interp.append("th_chnl", interp.F64, IN)
    interp.append("wi_chnl", interp.F64, IN)
    interp.append("eps_chnl", interp.F64, IN)
    interp.append("T_fu0", interp.F64, IN)

    interp.append("ofr", interp.F64, IN | OUT)
    interp.append("dm_cc", interp.F64, IN | OUT)
    interp.append("dm_ox", interp.F64, OUT)
    interp.append("dm_fu", interp.F64, OUT)
    interp.append("P_exit", interp.F64, IN)
    interp.append("P0_cc", interp.F64, IN)
    interp.append("T0_cc", interp.F64, OUT)
    interp.append("rho0_cc", interp.F64, OUT)
    interp.append("gamma_tht", interp.F64, OUT)
    interp.append("Mw_tht", interp.F64, OUT)
    interp.append("Isp", interp.F64, OUT)
    interp.append("Thrust", interp.F64, OUT)
    interp.append("efficiency", interp.F64, OUT)

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
    interp.append("out_T_wg", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_T_wc", interp.PTR_F64, IN | interp.OUTPUT_DATA)

    interp.append("target_Thrust", interp.F64, IN)
    interp.append("optimise_ofr", interp.I64, IN)
    interp.append("optimise_dm_cc", interp.I64, IN)

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

    state["helix_angle"] = config["chamber"]["helix_angle"]
    state["th_iw"] = config["chamber"]["th_iw"] * 1e-3
    state["no_chnl"] = config["chamber"]["no_web"]
    state["th_chnl"] = config["chamber"]["th_web"]
    state["eps_chnl"] = config["material"]["roughness_depth"]
    state["th_chnl"] = 1.5e-3
    state["wi_chnl"] = 2*3.14159265358979323*17.6e-3/32 - 1.5e-3
    state["eps_chnl"] = 0.0
    state["T_fu0"] = 25.0 + 273.15

    dm_ox = config["operating_conditions"]["mdot_LOx"]
    dm_fu = config["operating_conditions"]["mdot_IPA"]
    state["ofr"] = dm_ox / dm_fu
    state["dm_cc"] = dm_ox + dm_fu
    state["P_exit"] = config["operating_conditions"]["P_exit"]
    state["P0_cc"] = config["operating_conditions"]["P_cc"]

    state["out_count"] = 10000
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
    state["out_T_wg"] = new_out()
    state["out_T_wc"] = new_out()

    state["target_Thrust"] = config["operating_conditions"]["Thrust"]
    state["optimise_ofr"] = 0
    state["optimise_dm_cc"] = 0

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

    # todo: injector filmcooling + element placement?

    extra = {
        "operating_conditions": {
            "Thrust": state["Thrust"],
            "P_cc": state["P0_cc"],
            "P_exit": state["P_exit"],
            "mdot_LOx": state["dm_ox"],
            "mdot_IPA": state["dm_fu"],
        },
        "part_mating": part_mating(),
        "chamber": {
            "L_cc": state["L_cc"] * 1e3,
            "R_tht": state["R_tht"] * 1e3,
            "AEAT": state["AEAT"],
            "NLF": state["NLF"],
            "phi_conv": state["phi_conv"],
            "phi_div": state["phi_div"],
            "phi_exit": state["phi_exit"],
            "th_iw": state["th_iw"],
            "th_chnl": state["th_chnl"],
            "wi_chnl": state["wi_chnl"],
        },
    }
    with open(paths.ROOT / "../config/ammendments.json", "w") as f:
        json.dump(extra, f, indent=4)
        f.write("\n") # trailing newline smile


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

    z = state["out_z"].view(state["out_count"])
    r = state["out_r"].view(state["out_count"])
    M_g = state["out_M_g"].view(state["out_count"])
    T_g = state["out_T_g"].view(state["out_count"])
    P_g = state["out_P_g"].view(state["out_count"])
    rho_g = state["out_rho_g"].view(state["out_count"])
    gamma_g = state["out_gamma_g"].view(state["out_count"])
    cp_g = state["out_cp_g"].view(state["out_count"])
    mu_g = state["out_mu_g"].view(state["out_count"])
    Pr_g = state["out_Pr_g"].view(state["out_count"])
    T_c = state["out_T_c"].view(state["out_count"])
    P_c = state["out_P_c"].view(state["out_count"])
    T_wg = state["out_T_wg"].view(state["out_count"])
    T_wc = state["out_T_wc"].view(state["out_count"])
    win = geez.new_window()

    _, axes = win.new_plots(rows=2, cols=3)
    axes[0,0].set_aspect(1.0)
    axes[0,0].plot(z*1e3, r*1e3)
    axes[0,0].set_title("contour [mm]")
    axes[1,0].plot(z*1e3, M_g)
    axes[1,0].set_title("mach number")
    axes[0,1].plot(z*1e3, T_g)
    axes[0,1].set_title("temperature [K]")
    axes[1,1].plot(z*1e3, P_g*1e-5)
    axes[1,1].set_title("pressure [bar]")
    axes[0,2].plot(z*1e3, rho_g)
    axes[0,2].set_title("density [kg/m3]")
    axes[1,2].plot(z*1e3, gamma_g)
    axes[1,2].set_title("gamma")

    _, axes = win.new_plots(rows=2, cols=3)
    axes[0,0].set_aspect(1.0)
    axes[0,0].plot(z*1e3, r*1e3)
    axes[0,0].set_title("contour [mm]")
    axes[1,0].plot(z*1e3, M_g)
    axes[1,0].set_title("mach number")
    axes[0,1].plot(z*1e3, cp_g)
    axes[0,1].set_title("specific heat [J/kg/K]")
    axes[1,1].plot(z*1e3, mu_g)
    axes[1,1].set_title("viscosity [Pa*s]")
    axes[0,2].plot(z*1e3, Pr_g)
    axes[0,2].set_title("prandtl")

    _, axes = win.new_plots(rows=2, cols=3)
    axes[0,0].set_aspect(1.0)
    axes[0,0].plot(z*1e3, r*1e3)
    axes[0,0].set_title("contour [mm]")
    axes[1,0].plot(z*1e3, M_g)
    axes[1,0].set_title("mach number")
    axes[0,1].plot(z*1e3, T_c)
    axes[0,1].set_title("coolant temperature [K]")
    axes[1,1].plot(z*1e3, T_wg)
    axes[1,1].set_title("wall gas temperature [K]")
    axes[0,2].plot(z*1e3, P_c*1e-5)
    axes[0,2].set_title("coolant pressure [bar]")
    axes[1,2].plot(z*1e3, T_wc)
    axes[1,2].set_title("wall coolant temperature [K]")

    return 0


def run():
    with geez.instance():
        return now_this_is_bruv()


if __name__ == "__main__":
    if len(sys.argv) > 1:
        raise RuntimeError("bruv.frontend does not have command line args")
    sys.exit(run())
