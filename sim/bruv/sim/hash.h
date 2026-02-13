#pragma once
#include "br.h"


// A value with essentially random bits which can be used to initialise a hash
// value when hashing multiple objects. Or it can be used for anything really.
// - =`2^64 / phi`.
#define HASH_SEED ((u64)0x9E3779B97F4A7C15U)


// Hashes the given 64 bit integer.
// - Each input value returns a unique hash.
// - 0 returns 0.
u64 hash_u64(u64 x);

// Hashes the given byte sequence using the 64 bit FNV-1a hashing algorithm.
// - FNV-1a has quite good avalanching and ok speed, however if it is critical a
//      specialised (most likely simd) hashing function would be a much better
//      option.
// - `buf` must span `size` bytes.
u64 hash_bytes(const void* buf, i64 size);


// Combines the two given hashes into one hash value. This operation is not
// commutative, so the order of `running` vs `add` affects the result. For a
// commutative result, use `hash_blend`.
// - Non-commutative.
// - Generally used for hashing ordered sequences.
u64 hash_aug(u64 running, u64 add);

// Combines the two given hashes into one hash value. This operation is
// commutative, so the order of `a` vs `b` has no effect on the result. For a
// non-commutative result, use `hash_aug`.
// - Commutative.
// - Generally used for hashing collections.
u64 hash_blend(u64 a, u64 b);
