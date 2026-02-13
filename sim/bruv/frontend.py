"""
GUI front-end for the c back-end.
"""

import sys
import numpy as np

from . import bridge

def main():

    interp = bridge.Interpretation()
    interp.append("hi",   interp.F64, interp.INPUT)
    interp.append("bye",  interp.F64, interp.OUTPUT)
    interp.append("size", interp.I64, interp.INPUT)
    interp.append("data", interp.PTR_U16, interp.INPUT | interp.INPUT_DATA | interp.OUTPUT_DATA)
    interp.finalise()

    state = bridge.State(interp)
    state["hi"] = 1234.5678e9
    state["bye"] = 1234.5678e9
    arr = np.array([1.0, 2.0, 3.0, 80.0], dtype=np.uint16)
    state["size"] = len(arr)
    state["data"] = arr
    print("state.hi:", state["hi"])
    print("state.bye:", state["bye"])
    print("state.size:", state["size"])
    print("state.data:", state["data"].view(state["size"]))

    ret = state.execute()
    print("returned:", ret)
    print("modified array:", arr)

    return ret is not None


if __name__ == "__main__":
    if len(sys.argv) > 1:
        raise RuntimeError("bruv.frontend does not have command line args")
    sys.exit(main())
