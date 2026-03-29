"""
GUI front-end for the c back-end.
"""

import json
import sys
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

    interp.append("ofr", interp.F64, IN)
    interp.append("dm_cc", interp.F64, IN)
    interp.append("dm_ox", interp.F64, OUT)
    interp.append("dm_fu", interp.F64, OUT)
    interp.append("P_exit", interp.F64, IN)
    interp.append("P0_cc", interp.F64, IN)
    interp.append("T0_cc", interp.F64, OUT)
    interp.append("gamma_tht", interp.F64, OUT)
    interp.append("Mw_tht", interp.F64, OUT)
    interp.append("Thrust", interp.F64, OUT)
    interp.append("Isp", interp.F64, OUT)

    interp.append("out_count", interp.I64, IN)
    interp.append("out_z", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_r", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_M", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_T", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_P", interp.PTR_F64, IN | interp.OUTPUT_DATA)

    interp.append("optimise", interp.I64, IN)
    interp.append("target_Thrust", interp.F64, IN)
    interp.append("forced_ofr", interp.F64, IN)
    interp.append("forced_dm_cc", interp.F64, IN)

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


    dm_ox = config["part_mating"]["mdot_LOx"]
    dm_fu = config["part_mating"]["mdot_IPA"]
    state["ofr"] = dm_ox / dm_fu
    state["dm_cc"] = dm_ox + dm_fu
    state["P_exit"] = 101325.0 # atmos @ sea level
    state["P0_cc"] = config["part_mating"]["P_cc"]

    state["out_count"] = 10000
    state["out_z"] = np.empty(shape=(state["out_count"],), dtype=np.float64)
    state["out_r"] = np.empty(shape=(state["out_count"],), dtype=np.float64)
    state["out_M"] = np.empty(shape=(state["out_count"],), dtype=np.float64)
    state["out_T"] = np.empty(shape=(state["out_count"],), dtype=np.float64)
    state["out_P"] = np.empty(shape=(state["out_count"],), dtype=np.float64)

    state["optimise"] = 1
    state["target_Thrust"] = 5000.0
    state["forced_ofr"] = float("nan")
    state["forced_dm_cc"] = float("nan")

    return state

def write_ammendments(state):
    extra = {
        "part_mating": {
            "mdot_LOx": state["dm_ox"],
            "mdot_IPA": state["dm_fu"],
        },
        "chamber": {
            "L_cc": state["L_cc"] * 1e3,
            "R_tht": state["R_tht"] * 1e3,
            "AEAT": state["AEAT"],
            "phi_div": state["phi_div"],
            "phi_exit": state["phi_exit"],
        },
    }
    with open(paths.ROOT / "../config/ammendments.json", "w") as f:
        json.dump(extra, f, indent=4)
        f.write("\n") # trailing newline smile


def now_this_is_bruv():
    interp = get_interpretation()
    state = get_state(interp)

    ret = state.execute()
    if ret is not None:
        print("FAILED:", ret)
        return 1

    print(state)
    write_ammendments(state)

    z = state["out_z"].view(state["out_count"])
    r = state["out_r"].view(state["out_count"])
    M = state["out_M"].view(state["out_count"])
    T = state["out_T"].view(state["out_count"])
    P = state["out_P"].view(state["out_count"])
    _, axes = geez.new_plots(rows=2, cols=2)
    axes[0,0].set_aspect(1.0)
    axes[0,0].plot(z*1e3, r*1e3)
    axes[0,0].set_title("contour [mm]")
    axes[1,0].plot(z*1e3, M)
    axes[1,0].set_title("mach number")
    axes[0,1].plot(z*1e3, T)
    axes[0,1].set_title("temperature [K]")
    axes[1,1].plot(z*1e3, P*1e-6)
    axes[1,1].set_title("pressure [MPa]")

    return 0


def run():
    with geez.instance():
        return now_this_is_bruv()


if __name__ == "__main__":
    if len(sys.argv) > 1:
        raise RuntimeError("bruv.frontend does not have command line args")
    sys.exit(run())
