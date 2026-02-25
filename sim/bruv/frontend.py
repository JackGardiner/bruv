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

    interp.append("cnt_out_count", interp.I64, IN)
    interp.append("cnt_out_z", interp.PTR_F64, IN | interp.OUTPUT_DATA)
    interp.append("cnt_out_r", interp.PTR_F64, IN | interp.OUTPUT_DATA)

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

    state["cnt_out_count"] = 1000
    state["cnt_out_z"] = np.empty(shape=(1000,), dtype=np.float64)
    state["cnt_out_r"] = np.empty(shape=(1000,), dtype=np.float64)

    ret = state.execute()
    print("returned:", ret)

    _, ax = geez.new_figure()
    ax.set_aspect(1.0)
    ax.plot(state["cnt_out_z"].view(state["cnt_out_count"]),
            state["cnt_out_r"].view(state["cnt_out_count"]))

    return ret is not None


def run():
    with geez.instance():
        return now_this_is_bruv()


if __name__ == "__main__":
    if len(sys.argv) > 1:
        raise RuntimeError("bruv.frontend does not have command line args")
    sys.exit(run())
