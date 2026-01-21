"""
GUI front-end for the c back-end.
"""

def run():
    from . import bridge
    print("loaded bridge:", bridge)

    interp = bridge.Interpretation()
    interp.add("hi", "f64", bridge.FLAG_INPUT)
    interp.add("bye", "f64", bridge.FLAG_INPUT)
    interp.add("another_one", "i64", bridge.FLAG_OUTPUT)
    interp.add("data", "u16[]", 0)
    interp.finalise()

    state = bridge.State(interp)
    state.set_f64(0, 1234.5678e9)
    print("state:", state)
    print("state[0]:", state.get_f64(0))

    ret = state.execute()
    print("returned:", ret)
