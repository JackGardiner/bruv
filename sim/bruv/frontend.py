"""
GUI front-end for the c back-end.
"""

import sys
import numpy as np

from . import bridge
from . import geez

__all__ = ["run"]


def interpretation():
    interp = bridge.Interpretation()
    IN = interp.INPUT
    OUT = interp.OUTPUT

    interp.append("L_cc", interp.F64, IN)
    interp.append("R_cc", interp.F64, IN)
    interp.append("R_tht", interp.F64, IN)
    interp.append("R_exit", interp.F64, OUT)
    interp.append("AEAT", interp.F64, IN)
    interp.append("NLF", interp.F64, IN)
    interp.append("phi_conv", interp.F64, IN)
    interp.append("phi_div", interp.F64, OUT)
    interp.append("phi_exit", interp.F64, OUT)

    interp.append("out_count", interp.I64, IN)
    interp.append("out_z", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_r", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_M", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_T", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("out_P", interp.PTR_F64, IN | interp.OUTPUT_DATA)

    interp.append("cnt_r_conv", interp.F64, OUT)
    interp.append("cnt_z0", interp.F64, OUT)
    interp.append("cnt_r0", interp.F64, OUT)
    interp.append("cnt_z1", interp.F64, OUT)
    interp.append("cnt_r1", interp.F64, OUT)
    interp.append("cnt_z2", interp.F64, OUT)
    interp.append("cnt_r2", interp.F64, OUT)
    interp.append("cnt_z3", interp.F64, OUT)
    interp.append("cnt_r3", interp.F64, OUT)
    interp.append("cnt_z4", interp.F64, OUT)
    interp.append("cnt_r4", interp.F64, OUT)
    interp.append("cnt_z5", interp.F64, OUT)
    interp.append("cnt_r5", interp.F64, OUT)
    interp.append("cnt_z6", interp.F64, OUT)
    interp.append("cnt_r6", interp.F64, OUT)
    interp.append("cnt_para_az", interp.F64, OUT)
    interp.append("cnt_para_bz", interp.F64, OUT)
    interp.append("cnt_para_cz", interp.F64, OUT)
    interp.append("cnt_para_ar", interp.F64, OUT)
    interp.append("cnt_para_br", interp.F64, OUT)
    interp.append("cnt_para_cr", interp.F64, OUT)

    interp.finalise()
    return interp


def now_this_is_bruv():
    interp = interpretation()
    state = bridge.State(interp)

    state["L_cc"] = 100.0e-3
    state["R_cc"] = 50.0e-3
    state["R_tht"] = 23.0e-3
    state["AEAT"] = 5.2
    state["NLF"] = 0.94
    state["phi_conv"] = -np.pi/4

    state["out_count"] = 10000
    state["out_z"] = np.empty(shape=(state["out_count"],), dtype=np.float64)
    state["out_r"] = np.empty(shape=(state["out_count"],), dtype=np.float64)
    state["out_M"] = np.empty(shape=(state["out_count"],), dtype=np.float64)
    state["out_T"] = np.empty(shape=(state["out_count"],), dtype=np.float64)
    state["out_P"] = np.empty(shape=(state["out_count"],), dtype=np.float64)

    ret = state.execute()
    if ret is not None:
        print("returned:", ret)
        return 1

    z = state["out_z"].view(state["out_count"])
    r = state["out_r"].view(state["out_count"])
    M = state["out_M"].view(state["out_count"])
    T = state["out_T"].view(state["out_count"])
    P = state["out_P"].view(state["out_count"])
    _, axes = geez.new_figure(rows=2, cols=2)
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
