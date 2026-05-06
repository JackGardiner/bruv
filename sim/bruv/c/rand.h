#pragma once
#include "br.h"



// ========================== //
//           RANDOM           //
// ========================== //

// Encapsulates the state of a random generator.
// - Opaque struct, please don't touch any members :).
typedef struct brRand {
    u64 _state[4];
} brRand;

// Initialises the given random state, using a seed generated from the current
// time.
void rand_init(brRand* rand);
// Initialises `rand_init`. Must be called once before any calls to `rand_init`.
void rand_init_init(void);

// Initialises the given random state, using the given `seed`. If the same seed
// is used to seed multiple generators, they will produce identical output (while
// they are used in identical ways).
void rand_seed(brRand* rand, u64 seed);


// Returns 64 random bits, using the given `rand` generator.
// - Increments the `rand` state once.
u64 rand_u64(brRand* rand);

// Returns a random floating-point number in [0, 1), using the given `rand`
// generator.
// - Increments the `rand` state once.
// - Returns within [0, 1).
f64 rand_0to1(brRand* rand);

// Sets `size` bytes of `buf` to random bytes, using the given `rand` generator.
// - Increments the `rand` state once every 8B, rounded up.
// - `buf` must span `size` bytes.
void rand_bytes(brRand* rand, void* rstr buf, i64 size);
