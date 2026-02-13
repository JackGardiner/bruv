/* Note this isn't in bridge/ since when this file is modified, it requires the
   c to be rebuilt rather than bridge.pyx. */

#include "../bridge/bridge.h"
#include "assert.h"
#include "hash.h"
#include "sim.h"


static_assert(sametype(c_IH, u64));

c_IH c_ih_initial(void) {
    return HASH_SEED;
}

c_IH c_ih_append(c_IH running, const char* name, c_IHentry entry) {
    /* `assert` is not valid here. */
    // Note there is no input validation. If its invalid, the resultant hash will
    // not match.
    if (name == NULL)
        name = "";

    // Hash name and entry (note entry is hashed to ensure nice bit distribution,
    // since `hash_aug` except strong hashes as input already).
    u64 name_hash = hash_bytes(name, __builtin_strlen(name));
    u64 entry_hash = hash_u64((u64)entry);
    // Combine in order-dependant fashion.
    running = hash_aug(running, name_hash);
    running = hash_aug(running, entry_hash);
    return running;
}


static_assert(sizeof(c_eight_bytes) == 8);

const char* c_execute(c_eight_bytes* state, c_IH interpretation_hash) {
    // Setup assert catch to handle ALL erroneous returns from this function.
    if (assertion_has_failed())
        return assertion_message();

    // Ensure proposed interpretation is correct.
    assert(interpretation_hash == sim_interpretation_hash(),
            "interpretation hash does not match, proposal is dismissed");
    // mr hoity toity over here.

    // Send to sim.
    sim_execute((simState*)state /* reinterpret */);
    return NULL; // no error.
}
