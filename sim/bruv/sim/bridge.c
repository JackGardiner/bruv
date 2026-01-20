/* Note this isn't in bridge/ since when this file is modified, it requires the
   c to be rebuilt rather than bridge.pyx. */

#include "../bridge/bridge.h"
#include "assert.h"
#include "sim.h"

const char* c_execute(c_eight_bytes* c_state) {
    // Setup assert catch.
    if (assertion_has_failed())
        return assertion_message();

    // Send to sim.
    brState* state = (brState*)c_state; // reinterpret.
    br_sim(state);
    return NULL;
}
