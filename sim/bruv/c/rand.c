#include "rand.h"

#include "maths.h"
#include "hash.h"


// =========================================================================== //
// = RANDOM ================================================================== //
// =========================================================================== //

// `brRand` is a xoshiro256** generator, some info from the creators can be found
// at https://prng.di.unimi.it/.

// Simple splitmix64 random generator, used to expand 64b seeds to more bits.
static u64 rand_splitmix64(u64* s) {
    u64 r = (*s += (u64)0x9E3779B97F4A7C15U);
    r = (r ^ (r >> 30)) * (u64)0xBF58476D1CE4E5B9U;
    r = (r ^ (r >> 27)) * (u64)0x94D049BB133111EBU;
    return r ^ (r >> 31);
}


static u64 rand_init_seed;
void rand_init(brRand* rand) {
    rand_seed(rand, ++rand_init_seed);
}

void rand_init_init(void) {
    // Initialise the base seed with bits dependant on the current time. Note
    // this has kinda no guarantees, and i think its possible for it to be the
    // same across different program executions but extremely unlikely so fine by
    // me.
    rand_init_seed = (u64)time(NULL) ^ ((u64)clock() << 32);
}

void rand_seed(brRand* rand, u64 seed) {
    // Initialise the 256b state from the 64b seed.
    for (i32 i=0; i<numel(rand->_state); ++i)
        rand->_state[i] = rand_splitmix64(&seed);
}


u64 rand_u64(brRand* rand) {
    // Perform the xoshiro256**.
    u64* s = rand->_state;

    u64 u = bit_rotl(s[1] * 5, 7) * 9;
    u64 t = (s[1] << 17);
    s[2] ^= s[0];
    s[3] ^= s[1];
    s[1] ^= s[2];
    s[0] ^= s[3];
    s[2] ^= t;
    s[3] = bit_rotl(s[3], 45);
    return u;
}

f64 rand_0to1(brRand* rand) {
    u64 u = rand_u64(rand);
    // As suggested by https://prng.di.unimi.it/#remarks, we use a 53 bit random
    // number and map it to the range [0, 1).
    //  (53 random bits) * 2^-53
    // Note the high 53b are taken, since they have slightly better distribution.
    f64 two_n53 = 1.0 / ((u64)1 << 53);
    f64 f = (u >> 11) * two_n53;
    return f;
}

void rand_bytes(brRand* rand, void* rstr buf, i64 size) {
    u8* p = buf;

    // Gotta guarantee we won't inc the random state.
    if (!size)
        return;

    // Since we generate 8 bytes of random data at a time, we need explicit
    // handling for fewer bytes.
    if (size < 8) {
        u64 r = rand_u64(rand);
        if (size > 4) {
            // Two overlapping 4B sets.
            *(u32_u*)p = (u32)r;
            *(u32_u*)(p + size - 4) = (r >> 32);
        } else {
            // Otherwise just set each byte.
            while (size --> 0) { // down-to best operator 2025.
                *p++ = (u8)r;
                r >>= 8;
            }
        }
        return;
    }

    // For cases with more than 8 bytes, we can use the tried and true copy up in
    // the good increments then one final (possibly overlapping) copy. pistol
    // drew right at you type shi.
    i64 size8 = alignto(size - 8, 8);
    for (i64 i = 0; i < size8; i += 8)
        *(u64_u*)(p + i) = rand_u64(rand);
    *(u64_u*)(p + size - 8) = rand_u64(rand);
}
