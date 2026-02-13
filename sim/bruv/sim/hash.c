#include "hash.h"


u64 hash_u64(u64 x) {
    // Pretty typical pattern for invertible hashes, but its also just a fast and
    // good hash all around.
    u64 h = x;
    h ^= (h >> 32);
    h *= (u64)0xD6E8FEB86659FD93U;
    h ^= (h >> 32);
    h *= (u64)0xD6E8FEB86659FD93U;
    h ^= (h >> 32);
    return h;
}

u64 hash_bytes(const void* buf, i64 size) {
    // FNV-1a algorithm.
    // https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    #define HASH_FNV_OFFSET ((u64)0xCBF29CE484222325U)
    #define HASH_FNV_PRIME ((u64)0x00000100000001B3U)
    // yeah pretty shocking algorithm compared to the other overengineered things
    // in here but past me hadn't gotten around to it :/

    const u8* ptr = buf;

    u64 hash = HASH_FNV_OFFSET;
    while (size --> 0) { // down-to talk about the wonders of down-to.
        hash ^= (u64)*ptr++;
        hash *= HASH_FNV_PRIME;
    }
    return hash;
}


u64 hash_aug(u64 running, u64 add) {
    // Since we are assuming `running` and `add` are strong hashes, we are simply
    // trying to find a way to combine these hash values in a way that makes the
    // order most-impactful.
    return running ^ (add + running + (u64)0x5BFE84C76C35F380U);
    // Randomly generated constant btw (best constant through some metrics over
    // some trials).
}

u64 hash_blend(u64 a, u64 b) {
    // Reasons to use xor over addition:
    // - faster (probably skull emoji).
    // - These are the main drawbacks of the two:
    //      -h + h = 0
    //       h ^ h = 0
    //      where `h` is any hash value.
    //   However, on data structures where blend would often be used (ie maps)
    //   two keys cannot be identical, meaning two identical hashes are much
    //   less likely than two hashes which sum to zero. (probably?? idk hashes
    //   are weird man).
    return a ^ b;
}
