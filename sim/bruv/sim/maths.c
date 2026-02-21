#include "maths.h"



// =========================================================================== //
// = COMPARISONS ============================================================= //
// =========================================================================== //

i32 eq_i32(i32 a, i32 b) { return a == b; }
i32 eq_i64(i64 a, i64 b) { return a == b; }
i32 eq_u32(u32 a, u32 b) { return a == b; }
i32 eq_u64(u64 a, u64 b) { return a == b; }
i32 eq_f32(f32 a, f32 b) { return a == b; }
i32 eq_f64(f64 a, f64 b) { return a == b; }
i32 eq_vec2(vec2 a, vec2 b) {
    i32x2 comp = (a == b);
    return !!(comp[0] & comp[1]);
}
i32 eq_vec3(vec3 a, vec3 b) {
    // do 4-element compare but ignore last element.
    i32x4 comp = (a == b);
    i32 movmsk = __builtin_ia32_movmskps(fp_from_bits(comp));
    return (movmsk & 0x7) == 0x7;
}
i32 eq_vec4(vec4 a, vec4 b) {
    i32x4 comp = (a == b);
    // bloody rangle it to use movemask.
    i32 movmsk = __builtin_ia32_movmskps(fp_from_bits(comp));
    return movmsk == 0xF;
}

i32 anyeq_i32(i32 a, i32 b) { return a == b; }
i32 anyeq_i64(i64 a, i64 b) { return a == b; }
i32 anyeq_u32(u32 a, u32 b) { return a == b; }
i32 anyeq_u64(u64 a, u64 b) { return a == b; }
i32 anyeq_f32(f32 a, f32 b) { return a == b; }
i32 anyeq_f64(f64 a, f64 b) { return a == b; }
i32 anyeq_vec2(vec2 a, vec2 b) {
    i32x2 comp = (a == b);
    return !!(comp[0] | comp[1]);
}
i32 anyeq_vec3(vec3 a, vec3 b) {
    i32x4 comp = (a == b);
    i32 movmsk = __builtin_ia32_movmskps(fp_from_bits(comp));
    return (movmsk & 0x7) != 0;
}
i32 anyeq_vec4(vec4 a, vec4 b) {
    i32x4 comp = (a == b);
    i32 movmsk = __builtin_ia32_movmskps(fp_from_bits(comp));
    return movmsk != 0;
}


i32 isnan_f64(f64 x) { return !eq(x, x); }
i32 isnan_f32(f32 x) { return !eq(x, x); }
i32 isnan_vec2(vec2 x) { return !eq(x, x); }
i32 isnan_vec3(vec3 x) { return !eq(x, x); }
i32 isnan_vec4(vec4 x) { return !eq(x, x); }

i32 isallnan_f64(f64 x) { return !anyeq(x, x); }
i32 isallnan_f32(f32 x) { return !anyeq(x, x); }
i32 isallnan_vec2(vec2 x) { return !anyeq(x, x); }
i32 isallnan_vec3(vec3 x) { return !anyeq(x, x); }
i32 isallnan_vec4(vec4 x) { return !anyeq(x, x); }


i32 isinf_f64(f64 x) { return anyeq(abs(x), inf(x)); }
i32 isinf_f32(f32 x) { return anyeq(abs(x), inf(x)); }
i32 isinf_vec2(vec2 x) { return anyeq(abs(x), inf(x)); }
i32 isinf_vec3(vec3 x) { return anyeq(abs(x), inf(x)); }
i32 isinf_vec4(vec4 x) { return anyeq(abs(x), inf(x)); }

i32 isallinf_f64(f64 x) { return eq(abs(x), inf(x)); }
i32 isallinf_f32(f32 x) { return eq(abs(x), inf(x)); }
i32 isallinf_vec2(vec2 x) { return eq(abs(x), inf(x)); }
i32 isallinf_vec3(vec3 x) { return eq(abs(x), inf(x)); }
i32 isallinf_vec4(vec4 x) { return eq(abs(x), inf(x)); }



// =========================================================================== //
// = ABS/MIN/MAX/MOD ========================================================= //
// =========================================================================== //


// Cheeky rant as to why `abs` works:
// The issue is:
//  u64 u = iabs(intmin(i32));
//  assert(u != (u64)0x80000000U /* |i32 min| */);
// The incorrect result is obtained because `intmin(i32)` remains unchanged and
// is then cast to `u64`. If it was cast to `u32` in between, the result would be
// correct because of the properties of 2's complement and the integers being the
// same width. That is, it "just so happens" that:
//  (u32)intmin(i32) == -(i64)intmin(i32) /* == |i32 min| */
// So, adding the type-fitting unsigned conversion solves the issue.
u8 abs_i8(i8 x) { return iabs_i8(x); }
u16 abs_i16(i16 x) { return iabs_i16(x); }
u32 abs_i32(i32 x) { return iabs_i32(x); }
u64 abs_i64(i64 x) { return iabs_i64(x); }
u8 abs_u8(u8 x) { return iabs_u8(x); }
u16 abs_u16(u16 x) { return iabs_u16(x); }
u32 abs_u32(u32 x) { return iabs_u32(x); }
u64 abs_u64(u64 x) { return iabs_u64(x); }
f32 abs_f32(f32 x) { return iabs_f32(x); }
f64 abs_f64(f64 x) { return iabs_f64(x); }
vec2 abs_vec2(vec2 x) { return iabs_vec2(x); }
vec3 abs_vec3(vec3 x) { return iabs_vec3(x); }
vec4 abs_vec4(vec4 x) { return iabs_vec4(x); }

i8 iabs_i8(i8 x)    { return (x < 0) ? -x : x; }
i16 iabs_i16(i16 x) { return (x < 0) ? -x : x; }
i32 iabs_i32(i32 x) { return (x < 0) ? -x : x; }
i64 iabs_i64(i64 x) { return (x < 0) ? -x : x; }
u8 iabs_u8(u8 x)    { return x; }
u16 iabs_u16(u16 x) { return x; }
u32 iabs_u32(u32 x) { return x; }
u64 iabs_u64(u64 x) { return x; }
f32 iabs_f32(f32 x) { return __builtin_fabsf(x); }
f64 iabs_f64(f64 x) { return __builtin_fabs(x); }
vec2 iabs_vec2(vec2 x) {
    return vec2( __builtin_fabsf(x[0])
               , __builtin_fabsf(x[1]) );
}
vec3 iabs_vec3(vec3 x) {
    // full builtins is faster than anything ignoring elem 4.
    return iabs_vec4(x);
}
vec4 iabs_vec4(vec4 x) {
    // the asm of this is hot.
    return vec4( __builtin_fabsf(x[0])
               , __builtin_fabsf(x[1])
               , __builtin_fabsf(x[2])
               , __builtin_fabsf(x[3]) );
}


i32 min_i32(i32 a, i32 b) { return (a < b) ? a : b; }
i64 min_i64(i64 a, i64 b) { return (a < b) ? a : b; }
u32 min_u32(u32 a, u32 b) { return (a < b) ? a : b; }
u64 min_u64(u64 a, u64 b) { return (a < b) ? a : b; }
f32 min_f32(f32 a, f32 b) { return (a < b || b != b) ? a : b; }
f64 min_f64(f64 a, f64 b) { return (a < b || b != b) ? a : b; }
vec2 min_vec2(vec2 a, vec2 b) {
    i32x2 comp = (a < b) | (b != b);
    i32x2 ret = (fp_bits(a) & comp) | (fp_bits(b) & ~comp);
    return fp_from_bits(ret);
}
vec3 min_vec3(vec3 a, vec3 b) {
    return min_vec4(a, b);
}
vec4 min_vec4(vec4 a, vec4 b) {
    i32x4 comp = (a < b) | (b != b);
    i32x4 ret = (fp_bits(a) & comp) | (fp_bits(b) & ~comp);
    return fp_from_bits(ret);
}

i32 max_i32(i32 a, i32 b) { return (a > b) ? a : b; }
i64 max_i64(i64 a, i64 b) { return (a > b) ? a : b; }
u32 max_u32(u32 a, u32 b) { return (a > b) ? a : b; }
u64 max_u64(u64 a, u64 b) { return (a > b) ? a : b; }
f32 max_f32(f32 a, f32 b) { return (a > b || b != b) ? a : b; }
f64 max_f64(f64 a, f64 b) { return (a > b || b != b) ? a : b; }
vec2 max_vec2(vec2 a, vec2 b) {
    i32x2 comp = (a > b) | (b != b);
    i32x2 ret = (fp_bits(a) & comp) | (fp_bits(b) & ~comp);
    return fp_from_bits(ret);
}
vec3 max_vec3(vec3 a, vec3 b) {
    return max_vec4(a, b);
}
vec4 max_vec4(vec4 a, vec4 b) {
    i32x4 comp = (a > b) | (b != b);
    i32x4 ret = (fp_bits(a) & comp) | (fp_bits(b) & ~comp);
    return fp_from_bits(ret);
}


i32 minelem_i32(i32 x) { return x; }
i64 minelem_i64(i64 x) { return x; }
u32 minelem_u32(u32 x) { return x; }
u64 minelem_u64(u64 x) { return x; }
f32 minelem_f32(f32 x) { return x; }
f64 minelem_f64(f64 x) { return x; }
f32 minelem_vec2(vec2 x) {
    return (x[0] < x[1] || x[1] != x[1]) ? x[0] : x[1];
}
f32 minelem_vec3(vec3 x) {
    f32 r = x[0];
    r = (r < x[1] || x[1] != x[1]) ? r : x[1];
    r = (r < x[2] || x[2] != x[2]) ? r : x[2];
    return r;
}
f32 minelem_vec4(vec4 x) {
    f32 r = x[0];
    r = (r < x[1] || x[1] != x[1]) ? r : x[1];
    r = (r < x[2] || x[2] != x[2]) ? r : x[2];
    r = (r < x[3] || x[3] != x[3]) ? r : x[3];
    return r;
}

i32 maxelem_i32(i32 x) { return x; }
i64 maxelem_i64(i64 x) { return x; }
u32 maxelem_u32(u32 x) { return x; }
u64 maxelem_u64(u64 x) { return x; }
f32 maxelem_f32(f32 x) { return x; }
f64 maxelem_f64(f64 x) { return x; }
f32 maxelem_vec2(vec2 x) {
    return (x[0] > x[1] || x[1] != x[1]) ? x[0] : x[1];
}
f32 maxelem_vec3(vec3 x) {
    f32 r = x[0];
    r = (r > x[1] || x[1] != x[1]) ? r : x[1];
    r = (r > x[2] || x[2] != x[2]) ? r : x[2];
    return r;
}
f32 maxelem_vec4(vec4 x) {
    f32 r = x[0];
    r = (r > x[1] || x[1] != x[1]) ? r : x[1];
    r = (r > x[2] || x[2] != x[2]) ? r : x[2];
    r = (r > x[3] || x[3] != x[3]) ? r : x[3];
    return r;
}


i32 mod_i32(i32 a, i32 b) { return a % b; }
i64 mod_i64(i64 a, i64 b) { return a % b; }
u32 mod_u32(u32 a, u32 b) { return a % b; }
u64 mod_u64(u64 a, u64 b) { return a % b; }
f32 mod_f32(f32 a, f32 b) { return a - floor(a / b) * b; }
f64 mod_f64(f64 a, f64 b) { return a - floor(a / b) * b; }
vec2 mod_vec2(vec2 a, vec2 b) { return a - floor(a / b) * b; }
vec3 mod_vec3(vec3 a, vec3 b) { return a - floor(a / b) * b; }
vec4 mod_vec4(vec4 a, vec4 b) { return a - floor(a / b) * b; }



// =========================================================================== //
// = FLOATING POINT ========================================================== //
// =========================================================================== //


f64 f64_with_exp(i32 exp) {
    return fp_from_bits((u64)(exp + fp_exp_bias(f64)) << fp_mant_len(f64));
}
f32 f32_with_exp(i32 exp) {
    return fp_from_bits((u32)(exp + fp_exp_bias(f32)) << fp_mant_len(f32));
}

f64 f64_split(f64 pos_norm_f, i32* exp) {
    // Get the exponent from the bits.
    u64 bits = fp_bits(pos_norm_f);
    *exp = ((bits & fp_exp_mask(f64)) >> fp_mant_len(f64)) - fp_exp_bias(f64);

    // Hard-replace the exponent with 0 (2^0 = 1), scaling `f` to be in [1,2).
    // Try our best to do it in the xmm reggie.
    Vector(f64, 2) fvec;
    fvec[0] = pos_norm_f;
    // for some reason, a `movq xmm0 xmm0` is generated here which i dont think
    // has any effect (it clears the hi 64b but like is that important?).
    Vector(f64, 2) not_exp = { fp_from_bits(~fp_exp_mask(f64)), 0 };
    Vector(f64, 2) zero_exp = { f64_with_exp(0), 0 };
    fvec = __builtin_ia32_andpd(fvec, not_exp);
    fvec = __builtin_ia32_orpd(fvec, zero_exp);
    return fvec[0];
}
f32 f32_split(f32 pos_norm_f, i32* exp) {
    u32 bits = fp_bits(pos_norm_f);
    *exp = ((bits & fp_exp_mask(f32)) >> fp_mant_len(f32)) - fp_exp_bias(f32);

    // eeh shes fine just in main reg.
    u32 not_exp = ~fp_exp_mask(f32);
    u32 zero_exp = (u32)fp_exp_bias(f32) << fp_mant_len(f32);
    bits &= not_exp;
    bits |= zero_exp;
    return fp_from_bits(bits);
}

i32 signbit_f32(f32 f) { return __builtin_signbitf(f); }
i32 signbit_f64(f64 f) { return __builtin_signbit(f); }


f64 ifnan_f64(f64 x, f64 dflt) { return isnan(x) ? dflt : x; }
f32 ifnan_f32(f32 x, f32 dflt) { return isnan(x) ? dflt : x; }
vec2 ifnan_vec2(vec2 x, vec2 dflt) { return isnan(x) ? dflt : x; }
vec3 ifnan_vec3(vec3 x, vec3 dflt) { return isnan(x) ? dflt : x; }
vec4 ifnan_vec4(vec4 x, vec4 dflt) { return isnan(x) ? dflt : x; }

f64 ifnanelem_f64(f64 x, f64 dflt) { return isnan(x) ? dflt : x; }
f32 ifnanelem_f32(f32 x, f32 dflt) { return isnan(x) ? dflt : x; }
vec2 ifnanelem_vec2(vec2 x, vec2 dflt) {
    i32x2 comp = (x == x);
    i32x2 ret = (fp_bits(x) & comp) | (fp_bits(dflt) & ~comp);
    return fp_from_bits(ret);
}
vec3 ifnanelem_vec3(vec3 x, vec3 dflt) {
    return ifnanelem_vec4(x, dflt);
}
vec4 ifnanelem_vec4(vec4 x, vec4 dflt) {
    i32x4 comp = (x == x);
    i32x4 ret = (fp_bits(x) & comp) | (fp_bits(dflt) & ~comp);
    return fp_from_bits(ret);
}


i32 neartox_f64(f64 a, f64 b, f64 rtol, f64 atol) {
    return mag(a - b) <= (atol + rtol*mag(b));
}
i32 neartox_f32(f32 a, f32 b, f32 rtol, f32 atol) {
    return mag(a - b) <= (atol + rtol*mag(b));
}
i32 neartox_vec2(vec2 a, vec2 b, f32 rtol, f32 atol) {
    return mag(a - b) <= (atol + rtol*mag(b));
}
i32 neartox_vec3(vec3 a, vec3 b, f32 rtol, f32 atol) {
    return mag(a - b) <= (atol + rtol*mag(b));
}
i32 neartox_vec4(vec4 a, vec4 b, f32 rtol, f32 atol) {
    return mag(a - b) <= (atol + rtol*mag(b));
}


f64 round_f64(f64 x) {
    return __builtin_round(x);
}
f32 round_f32(f32 x) {
    return __builtin_roundf(x);
}
vec2 round_vec2(vec2 x) {
    return vec2( __builtin_roundf(x[0])
               , __builtin_roundf(x[1]) );
}
vec3 round_vec3(vec3 x) {
    return round_vec4(x);
}
vec4 round_vec4(vec4 x) {
    return vec4( __builtin_roundf(x[0])
               , __builtin_roundf(x[1])
               , __builtin_roundf(x[2])
               , __builtin_roundf(x[3]) );
}

i64 iround_f64(f64 x) {
    return (i64)__builtin_round(x);
}
i64 iround_f32(f32 x) {
    return (i64)__builtin_roundf(x);
}


f64 floor_f64(f64 x) {
    return __builtin_floor(x);
}
f32 floor_f32(f32 x) {
    return __builtin_floorf(x);
}
vec2 floor_vec2(vec2 x) {
    return vec2( __builtin_floorf(x[0])
               , __builtin_floorf(x[1]) );
}
vec3 floor_vec3(vec3 x) {
    return floor_vec4(x);
}
vec4 floor_vec4(vec4 x) {
    return vec4( __builtin_floorf(x[0])
               , __builtin_floorf(x[1])
               , __builtin_floorf(x[2])
               , __builtin_floorf(x[3]) );
}

i64 ifloor_f64(f64 x) {
    return (i64)__builtin_floor(x);
}
i64 ifloor_f32(f32 x) {
    return (i64)__builtin_floorf(x);
}


f64 ceil_f64(f64 x) {
    return __builtin_ceil(x);
}
f32 ceil_f32(f32 x) {
    return __builtin_ceilf(x);
}
vec2 ceil_vec2(vec2 x) {
    return vec2( __builtin_ceilf(x[0])
               , __builtin_ceilf(x[1]) );
}
vec3 ceil_vec3(vec3 x) {
    return ceil_vec4(x);
}
vec4 ceil_vec4(vec4 x) {
    return vec4( __builtin_ceilf(x[0])
               , __builtin_ceilf(x[1])
               , __builtin_ceilf(x[2])
               , __builtin_ceilf(x[3]) );
}

i64 iceil_f64(f64 x) {
    return (i64)__builtin_ceil(x);
}
i64 iceil_f32(f32 x) {
    return (i64)__builtin_ceilf(x);
}



// =========================================================================== //
// = ELEMENTARY ============================================================== //
// =========================================================================== //


u64 isqrt(u64 x) {
    // sqrt(0)=0 and sqrt(1)=1.
    if (x <= 1)
        return x;

    // Find an upper-bound for the sqrt.
    //  x = 2^a, where a is log2(x)
    //  x^(1/2) = (2^a)^(1/2)
    //  x^(1/2) = 2^(a/2)
    //  x^(1/2) = 2^b
    // if b = ceil(a/2), this is an upper-bound.

    i32 floorlog2x = 63 - __builtin_clzll(x);
    u64 x0 = (u64)1U << (floorlog2x/2 + 1); // upper-bound.
    u64 x1;

    // Do a Newton-raphson iteration/Heron's method.
    x1 = x0;
    x0 = (x1 + x / x1) / 2;
    // We could check if it's already converged here, but its unlikely so just
    // unconditionally do more iterations.
    x1 = x0;
    x0 = (x1 + x / x1) / 2;
    x1 = x0;
    x0 = (x1 + x / x1) / 2;
    // If it still hasn't converged, do iterations until convergence.
    if (x0 < x1) {
        do {
            x1 = x0;
            x0 = (x1 + x / x1) / 2;
        } while (x0 < x1);
    }

    // Bounds collapsed.
    return x0;
}


f64 sqrt_f64(f64 x) {
    return __builtin_sqrt(x);
}
f32 sqrt_f32(f32 x) {
    return __builtin_sqrtf(x);
}
vec2 sqrt_vec2(vec2 x) {
    return vec2( __builtin_sqrtf(x[0])
               , __builtin_sqrtf(x[1]) );
}
vec3 sqrt_vec3(vec3 x) {
    return sqrt_vec4(x);
}
vec4 sqrt_vec4(vec4 x) {
    return vec4( __builtin_sqrtf(x[0])
               , __builtin_sqrtf(x[1])
               , __builtin_sqrtf(x[2])
               , __builtin_sqrtf(x[3]) );
}


f64 cbrt_f64(f64 x) {
    // clean return for a couple edge cases. this probably looks amazing in asm
    // ngl.
    if (isnan(x) || isinf(x))
        return x;

    // Note that `(-x)^(1/3) = -(x^(1/3))`.
    if (x < 0)
        return -cbrt_f64(-x);

    // We can't use subnormals since we use split, so just treat them as 0.
    if (/* x == 0 || */ x < fp_norm_min(f64))
        return 0.0;

    // Basic idea:
    //  x = m * 2^e
    //  x = m * 2^(3k + j)
    //  x^(1/3) = (m * 2^(3k + j))^(1/3)
    //  x^(1/3) = m^(1/3) * 2^((3k + j)/3)
    //  x^(1/3) = m^(1/3) * 2^(k + j/3)
    //  x^(1/3) = m^(1/3) * 2^k * 2^(j/3)

    // Get `m` and `e` from the bits of the float.
    i32 e;
    f64 m = f64_split(x, &e);

    // Rational polynomial approximation of `m^(1/3)`, for `m` in 1..2. See:
    // https://www.desmos.com/calculator/8rg4ttbjbb
    f64 n0 = 0.1489960767650;
    f64 n1 = 1.5893022238500;
    f64 n2 = 1.5670873713800;
    f64 n3 = 0.1821935521380;
    f64 d0 = 0.4659660634460;
    f64 d1 = 1.9817634131800;
    f64 d2 = 0.9937808263990;
    f64 d3 = 0.0460689162968;
    f64 r = (
        (((n3 * m + n2) * m + n1) * m + n0)
        /
        (((d3 * m + d2) * m + d1) * m + d0)
    );
    // Now: `r = m^(1/3)`.

    // `e = 3k + j`
    i32 k = (e / 3);
    i32 j = (e % 3);

    // Do the `* 2^k * 2^(j/3)`
    r *= f64_with_exp(k);
    switch (j) {
        case -2: r *= 0.629960524947; break;
        case -1: r *= 0.793700525984; break;
        case  1: r *= 1.259921049890; break;
        case  2: r *= 1.587401051970; break;
    }
    return r;
}
f32 cbrt_f32(f32 x) {
    if (isnan(x) || isinf(x))
        return x;
    if (x < 0)
        return -cbrt_f32(-x);
    if (x < fp_norm_min(f32))
        return 0.f;

    i32 e;
    f32 m = f32_split(x, &e);

    f32 n0 = 0.1489960767650f;
    f32 n1 = 1.5893022238500f;
    f32 n2 = 1.5670873713800f;
    f32 n3 = 0.1821935521380f;
    f32 d0 = 0.4659660634460f;
    f32 d1 = 1.9817634131800f;
    f32 d2 = 0.9937808263990f;
    f32 d3 = 0.0460689162968f;
    f32 r = (
        (((n3 * m + n2) * m + n1) * m + n0)
        /
        (((d3 * m + d2) * m + d1) * m + d0)
    );

    i32 k = (e / 3);
    i32 j = (e % 3);

    r *= f32_with_exp(k);
    switch (j) {
        case -2: r *= 0.629960524947f; break;
        case -1: r *= 0.793700525984f; break;
        case  1: r *= 1.259921049890f; break;
        case  2: r *= 1.587401051970f; break;
    }
    return r;
}
vec2 cbrt_vec2(vec2 x) {
    return vec2( cbrt_f32(x[0])
               , cbrt_f32(x[1]) );
}
vec3 cbrt_vec3(vec3 x) {
    return vec3( cbrt_f32(x[0])
               , cbrt_f32(x[1])
               , cbrt_f32(x[2]) );
}
vec4 cbrt_vec4(vec4 x) {
    return vec4( cbrt_f32(x[0])
               , cbrt_f32(x[1])
               , cbrt_f32(x[2])
               , cbrt_f32(x[3]) );
}


f64 sqed_f64(f64 x) { return x*x; }
f32 sqed_f32(f32 x) { return x*x; }
vec2 sqed_vec2(vec2 x) { return x*x; }
vec3 sqed_vec3(vec3 x) { return x*x; }
vec4 sqed_vec4(vec4 x) { return x*x; }


f64 cbed_f64(f64 x) { return x*x*x; }
f32 cbed_f32(f32 x) { return x*x*x; }
vec2 cbed_vec2(vec2 x) { return x*x*x; }
vec3 cbed_vec3(vec3 x) { return x*x*x; }
vec4 cbed_vec4(vec4 x) { return x*x*x; }


f64 exp2_f64(f64 x) {
    if (isnan(x))
        return x; // return nan.

    if (x >= 1024.0)
        return INF;

    // We can't return subnormals since we use `f64_with_exp`, so just return 0
    // instead of the correct subnormal return.
    if (x <= -1022.0)
        return 0.0;

    // Basic idea:
    //  x = i + f
    //  2^x = 2^(i + f)
    //  2^x = 2^i * 2^f

    // Split `x` into two parts, an integer and a fraction.
    i32 i = (i32)x; // integer part (truncating).
    f64 f = x - i;  // fractional part.
    // Now: `x = i + f`.

    // Now `f` is in -1..1, but if we move it to 0..1, our approximation becomes
    // ~100x more accurate for the same number of terms. so lets do that.
    i32 take = (f < 0.0);
    i -= take;
    f += take;
    // Note that `x` still ~=`i + f`.

    // Rational polynomial approximation of `2^f`, for `f` in 0..1. See:
    // https://www.desmos.com/calculator/eyj8pnvkdc
    f64 n0 = +3.67657762666000;
    f64 n1 = +1.31942423003000;
    f64 n2 = +0.19282487450200;
    f64 n3 = +0.01220740233640;
    f64 d0 = +3.67657762721000;
    f64 d1 = -1.22898523359000;
    f64 d2 = +0.16148178495000;
    f64 d3 = -0.00855711197218;
    f64 exp2f = (
        (((n3 * f + n2) * f + n1) * f + n0)
        /
        (((d3 * f + d2) * f + d1) * f + d0)
    );

    // Get `2^i` by directly placing the bits of `i` into the exponent bits.
    f64 exp2i = f64_with_exp(i);

    // Now calculate the final result: `2^x = 2^i * 2^f`.
    return exp2i * exp2f;
}
f32 exp2_f32(f32 x) {
    if (isnan(x))
        return x;

    if (x >= 1024.f)
        return fINF;

    if (x <= -1022.f)
        return 0.f;

    i32 i = (i32)x;
    f32 f = x - i;

    i32 take = (f < 0.f);
    i -= take;
    f += take;

    f32 n0 = +3.67657762666000f;
    f32 n1 = +1.31942423003000f;
    f32 n2 = +0.19282487450200f;
    f32 n3 = +0.01220740233640f;
    f32 d0 = +3.67657762721000f;
    f32 d1 = -1.22898523359000f;
    f32 d2 = +0.16148178495000f;
    f32 d3 = -0.00855711197218f;
    f32 exp2f = (
        (((n3 * f + n2) * f + n1) * f + n0)
        /
        (((d3 * f + d2) * f + d1) * f + d0)
    );

    f32 exp2i = f32_with_exp(i);

    return exp2i * exp2f;
}
vec2 exp2_vec2(vec2 x) {
    return vec2( exp2_f32(x[0])
               , exp2_f32(x[1]) );
}
vec3 exp2_vec3(vec3 x) {
    return vec3( exp2_f32(x[0])
               , exp2_f32(x[1])
               , exp2_f32(x[2]) );
}
vec4 exp2_vec4(vec4 x) {
    return vec4( exp2_f32(x[0])
               , exp2_f32(x[1])
               , exp2_f32(x[2])
               , exp2_f32(x[3]) );
}


f64 log2_f64(f64 x) {
    if (isnan(x) || x < 0.0)
        return NAN;

    // We can't use subnormals since we use `f64_split`, so just treat them as 0.
    // who needs subnormals anyway.
    if (/* x == 0 || */ x < fp_norm_min(f64))
        return -INF;

    if (x == INF)
        return INF;

    // It's useful enough to ensure an exactly 0 return on 1.
    if (x == 1.0)
        return 0.0;

    // Basic idea:
    //  x = m * 2^e
    //  log2(x) = log2(m * 2^e)
    //  log2(x) = log2(m) + log2(2^e)
    //  log2(x) = log2(m) + e

    // hey we got a function for that first step.
    i32 e;
    f64 m = f64_split(x, &e);

    // Rational polynomial approximation of `log2(m)`, for `m` in 1..2. See:
    // https://www.desmos.com/calculator/he0wfmt2dt
    // Note that the %error explodes as the output goes to zero, what can you do.
    // Also the constant generation is super random and i managed to lose the
    // exact desmos i used for these contants (oops).
    f64 n0 = -4.958898909080;
    f64 n1 = -6.612108365090;
    f64 n2 = +9.333280025010;
    f64 n3 = +2.237727255500;
    f64 d0 = +1.023735966000;
    f64 d1 = +6.730341730150;
    f64 d2 = +4.867463372800;
    f64 d3 = +0.387193700112;
    f64 log2m = (
        (((n3 * m + n2) * m + n1) * m + n0)
        /
        (((d3 * m + d2) * m + d1) * m + d0)
    );

    // Easy final result: `log2(x) = log2(m) + e`
    return log2m + e;
}
f32 log2_f32(f32 x) {
    if (isnan(x) || x < 0.f)
        return fNAN;

    if (x < fp_norm_min(f32))
        return -fINF;

    if (x == fINF)
        return fINF;

    if (x == 1.f)
        return 0.f;

    i32 e;
    f32 m = f32_split(x, &e);

    f32 n0 = -4.958898909080f;
    f32 n1 = -6.612108365090f;
    f32 n2 = +9.333280025010f;
    f32 n3 = +2.237727255500f;
    f32 d0 = +1.023735966000f;
    f32 d1 = +6.730341730150f;
    f32 d2 = +4.867463372800f;
    f32 d3 = +0.387193700112f;
    f32 log2m = (
        (((n3 * m + n2) * m + n1) * m + n0)
        /
        (((d3 * m + d2) * m + d1) * m + d0)
    );

    return log2m + e;
}
vec2 log2_vec2(vec2 x) {
    return vec2( log2_f32(x[0])
               , log2_f32(x[1]) );
}
vec3 log2_vec3(vec3 x) {
    return vec3( log2_f32(x[0])
               , log2_f32(x[1])
               , log2_f32(x[2]) );
}
vec4 log2_vec4(vec4 x) {
    return vec4( log2_f32(x[0])
               , log2_f32(x[1])
               , log2_f32(x[2])
               , log2_f32(x[3]) );
}


f64 pow_f64(f64 x, f64 n) { return exp2(log2(n) * x); }
f32 pow_f32(f32 x, f32 n) { return exp2(log2(n) * x); }
vec2 pow_vec2(vec2 x, vec2 n) { return exp2(log2(n) * x); }
vec3 pow_vec3(vec3 x, vec3 n) { return exp2(log2(n) * x); }
vec4 pow_vec4(vec4 x, vec4 n) { return exp2(log2(n) * x); }


f64 sin_f64(f64 x) {
    if (isnan(x) || isinf(x))
        return NAN;

    // Note that:
    //  sin(-x) = -sin(x)
    // So flip inputs from below zero to above and store whether to negate the
    // result.
    i32 isneg = signbit(x);
    if (isneg)
        x = -x;

    // Check for loss of precision. Note that doubles are still accurate to ~0.01
    // at this magnitude, but this function jus aint equipped for it and that's
    // completely fine by me.
    if (unlikely(x > 1e12))
        return 0.0;

    // Wrap x down to 0..pi/2.
    //  x = x - floor(x / (pi/2))
    //  x = x - q
    // `q` also happens to be useful, since it represents the number of quadrants
    // that `x` has passed.
    i64 q = x * (2.0 / PI); // x / (pi/2) == x * (2/pi)
    x -= q * (PI / 2.0);
    // Correct `x` for the quadrant that it's in.
    switch (q & 3) {
        case 0: // ^> quadrant.
            break;
        case 1: // ^< quadrant.
            x = PI_2 - x;
            break;
        case 2: // v< quadrant.
            isneg = !isneg;
            break;
        case 3: // v> quadrant.
            isneg = !isneg;
            x = PI_2 - x;
            break;
    }

    // Improve accuracy and guarantee upper-bound at high-end.
    if (x > PI_2 - 0.00012)
        return (isneg) ? -1.0 : 1.0;

    // Now use a polynomial approximation of `sin(x)`, for `x` in 0..pi/2. See:
    // https://www.desmos.com/calculator/puilcnyfxn
    f64 c1 = +1.00000000000000000;
    f64 c3 = -0.16666658910800000;
    f64 c5 = +0.00833305796660000;
    f64 c7 = -0.00019809319994700;
    f64 c9 = +0.00000260558007796;
    f64 x2 = x*x;
    f64 sinx = (
        ((((c9 * x2 + c7) * x2 + c5) * x2 + c3) * x2 + c1) * x
    );

    return (isneg) ? -sinx : sinx;
}
f32 sin_f32(f32 x) {
    if (isnan(x) || isinf(x))
        return fNAN;

    i32 isneg = signbit(x);
    if (isneg)
        x = -x;

    if (unlikely(x > 1e12f))
        return 0.f;

    i64 q = x * (2.f / fPI);
    x -= q * (fPI / 2.f);
    switch (q & 3) {
        case 0:
            break;
        case 1:
            x = fPI_2 - x;
            break;
        case 2:
            isneg = !isneg;
            break;
        case 3:
            isneg = !isneg;
            x = fPI_2 - x;
            break;
    }

    if (x > fPI_2 - 0.00012f)
        return (isneg) ? -1.f : 1.f;

    f32 c1 = +1.00000000000000000f;
    f32 c3 = -0.16666658910800000f;
    f32 c5 = +0.00833305796660000f;
    f32 c7 = -0.00019809319994700f;
    f32 c9 = +0.00000260558007796f;
    f32 x2 = x*x;
    f32 sinx = (
        ((((c9 * x2 + c7) * x2 + c5) * x2 + c3) * x2 + c1) * x
    );

    return (isneg) ? -sinx : sinx;
}
vec2 sin_vec2(vec2 x) {
    return vec2( sin_f32(x[0])
               , sin_f32(x[1]) );
}
vec3 sin_vec3(vec3 x) {
    return vec3( sin_f32(x[0])
               , sin_f32(x[1])
               , sin_f32(x[2]) );
}
vec4 sin_vec4(vec4 x) {
    return vec4( sin_f32(x[0])
               , sin_f32(x[1])
               , sin_f32(x[2])
               , sin_f32(x[3]) );
}

f64 cos_f64(f64 x) {
    if (isnan(x) || isinf(x))
        return NAN;

    // `cos(-x) = cos(x)` so ignore sign.
    x = abs(x);

    // Same loss of precision as sin.
    if (unlikely(x > 1e12))
        return 1.0;

    // Same wrap as sin.
    i64 q = x * (2.0 / PI);
    x -= q * (PI / 2.0);

    // Map input to sin based on the quadrant. Note:
    //  cos(x) = sin(pi/2 - x)
    // So, use the same cases as sin but add this `x = pi/2 - x`. Note this
    // cancels with itself. Also we gotta reintroduce `isneg`.
    i32 isneg = 0;
    switch (q & 3) {
        case 0: // ^> quadrant.
            x = PI_2 - x;
            break;
        case 1: // ^< quadrant.
            isneg = 1;
            break;
        case 2: // v< quadrant.
            isneg = 1;
            x = PI_2 - x;
            break;
        case 3: // v> quadrant.
            break;
    }

    // Now we have a value of `x` s.t.:
    //  sin(x) = cos(original x).
    // So just evaluate the same sin approximation. This is done over creating a
    // new one for cos because:
    // - shared constants is better for cache and space
    // - accuracy of outputs around 1 would be improved for cos-tailored approx
    //      but at the cost of outputs around 0 becoming significantly less
    //      accurate (exacerbated since they near 0).

    // Note this check guarantees `cos(0) = 1`.
    if (x > PI_2 - 0.00012)
        return (isneg) ? -1.0 : 1.0;

    f64 c1 = +1.00000000000000000;
    f64 c3 = -0.16666658910800000;
    f64 c5 = +0.00833305796660000;
    f64 c7 = -0.00019809319994700;
    f64 c9 = +0.00000260558007796;
    f64 x2 = x*x;
    f64 sinx = (
        ((((c9 * x2 + c7) * x2 + c5) * x2 + c3) * x2 + c1) * x
    );

    return (isneg) ? -sinx : sinx;
}
f32 cos_f32(f32 x) {
    if (isnan(x) || isinf(x))
        return fNAN;

    x = abs(x);

    if (unlikely(x > 1e12f))
        return 1.f;

    i64 q = x * (2.f / fPI);
    x -= q * (fPI / 2.f);

    i32 isneg = 0;
    switch (q & 3) {
        case 0:
            x = fPI_2 - x;
            break;
        case 1:
            isneg = 1;
            break;
        case 2:
            isneg = 1;
            x = fPI_2 - x;
            break;
        case 3:
            break;
    }

    if (x > fPI_2 - 0.00012f)
        return (isneg) ? -1.f : 1.f;

    f32 c1 = +1.00000000000000000f;
    f32 c3 = -0.16666658910800000f;
    f32 c5 = +0.00833305796660000f;
    f32 c7 = -0.00019809319994700f;
    f32 c9 = +0.00000260558007796f;
    f32 x2 = x*x;
    f32 sinx = (
        ((((c9 * x2 + c7) * x2 + c5) * x2 + c3) * x2 + c1) * x
    );

    return (isneg) ? -sinx : sinx;
}
vec2 cos_vec2(vec2 x) {
    return vec2( cos_f32(x[0])
               , cos_f32(x[1]) );
}
vec3 cos_vec3(vec3 x) {
    return vec3( cos_f32(x[0])
               , cos_f32(x[1])
               , cos_f32(x[2]) );
}
vec4 cos_vec4(vec4 x) {
    return vec4( cos_f32(x[0])
               , cos_f32(x[1])
               , cos_f32(x[2])
               , cos_f32(x[3]) );
}


f64 tan_f64(f64 x) {
    if (isnan(x) || isinf(x))
        return NAN;

    // Similar to `sin_f64`.

    // tan(-x) = -tan(x)
    i32 isneg = signbit(x);
    if (isneg)
        x = -x;

    // Check precision bounds (tighter than sin).
    if (unlikely(x > 1e10))
        return 0.0;

    // tan is periodic over 2 quadrants (instead of the typical 4), so wrap x
    // down to 0..pi.
    i64 q = x * (2.0 / PI);
    // Collapse odd `q`s to even to double wrap range.
    x -= (q & ~(i64)1) * (PI / 2.0);
    if (q & 1) { // quadrant 2 or 4
        isneg = !isneg;
        x = PI - x;
    }

    // Guarantee edge case (and improve accuracy).
    if (x < 0.002)
        return (isneg) ? -x : x;

    // Pull out the trident true:
    // https://www.desmos.com/calculator/avcaxwrj3u
    // I did refine these with a few scripts also, to ensure a clean change from
    // increasing to decreasing and never div by zero.
    f64 n1 = +5.624372875039875000;
    f64 n2 = -1.386618711575506400;
    f64 n3 = -0.331229592695866050;
    f64 n4 = +0.063926907091139600;
    f64 d0 = +5.624628055840510000;
    f64 d1 = -1.389903933089908000;
    f64 d2 = -2.197496144919844200;
    f64 d3 = +0.514290396523128500;
    f64 d4 = -0.002060825866599919;
    f64 tanx = (
        ((((n4 * x + n3) * x + n2) * x + n1) * x)
        /
        ((((d4 * x + d3) * x + d2) * x + d1) * x + d0)
    );

    return (isneg) ? -tanx : tanx;
}
f32 tan_f32(f32 x) {
    if (isnan(x) || isinf(x))
        return fNAN;

    i32 isneg = signbit(x);
    if (isneg)
        x = -x;

    if (unlikely(x > 1e10f))
        return 0.f;

    i64 q = x * (2.f / fPI);
    x -= (q & ~(i64)1) * (fPI / 2.f);
    if (q & 1) { // quadrant 2 or 4
        isneg = !isneg;
        x = fPI - x;
    }

    if (x < 0.002f)
        return (isneg) ? -x : x;

    f32 n1 = +5.624372875039875000f;
    f32 n2 = -1.386618711575506400f;
    f32 n3 = -0.331229592695866050f;
    f32 n4 = +0.063926907091139600f;
    f32 d0 = +5.624628055840510000f;
    f32 d1 = -1.389903933089908000f;
    f32 d2 = -2.197496144919844200f;
    f32 d3 = +0.514290396523128500f;
    f32 d4 = -0.002060825866599919f;
    f32 tanx = (
        ((((n4 * x + n3) * x + n2) * x + n1) * x)
        /
        ((((d4 * x + d3) * x + d2) * x + d1) * x + d0)
    );

    return (isneg) ? -tanx : tanx;
}
vec2 tan_vec2(vec2 x) {
    return vec2( tan_f32(x[0])
               , tan_f32(x[1]) );
}
vec3 tan_vec3(vec3 x) {
    return vec3( tan_f32(x[0])
               , tan_f32(x[1])
               , tan_f32(x[2]) );
}
vec4 tan_vec4(vec4 x) {
    return vec4( tan_f32(x[0])
               , tan_f32(x[1])
               , tan_f32(x[2])
               , tan_f32(x[3]) );
}


f64 asin_f64(f64 x) {
    // `asin(-x) = -asin(x)` sooo:
    i32 isneg = signbit(x);
    if (isneg)
        x = -x;

    // Now check for oob (|x| > 1.0) and nan.
    if (isnan(x) || x > 1.0)
        return NAN;

    // Note that:
    //  asin(x) = pi/2 - asin(sqrt(1 - x^2))
    // This is always true, but we can use it in the upper-portion of the domain
    // so that our approximation doesn't need to model the wacky end of asin. The
    // specific bound of sqrt(1/2) comes from:
    //  let B be the bound
    //  maximum x value the approximation is evaluted at is:
    //  max(B, sqrt(1 - B^2))
    //  the minimum of this function (aka the lowest maximum value of x we will
    //      evaluate at) occurs at B=sqrt(1/2)
    i32 invert = 0; // whether to `pi/2 - y` the result.
    if (x > SQRTH) {
        invert = 1;
        x = sqrt(1.0 - x*x);
    }

    // Wait shit this is the same verse i just did this.
    // https://www.desmos.com/calculator/krp7mnrntb
    f64 n1 = +0.479805928911;
    f64 n2 = -0.387583015474;
    f64 d0 = +2.879460183850;
    f64 d1 = -2.334320245840;
    f64 d2 = -1.247444840450;
    f64 d3 = +0.916368877443;
    f64 asinx = x + (
        ((n2 * x + n1) * x * x * x)
        /
        (((d3 * x + d2) * x + d1) * x + d0)
    );

    // Invert the result if needed (invert as-in idk tbh jus look at the maths).
    if (invert)
        asinx = PI_2 - asinx;

    // Negate if needed.
    return (isneg) ? -asinx : asinx;
}
f32 asin_f32(f32 x) {
    i32 isneg = signbit(x);
    if (isneg)
        x = -x;

    if (isnan(x) || x > 1.f)
        return fNAN;

    i32 invert = 0;
    if (x > fSQRTH) {
        invert = 1;
        x = sqrt(1.f - x*x);
    }

    f32 n1 = +0.479805928911f;
    f32 n2 = -0.387583015474f;
    f32 d0 = +2.879460183850f;
    f32 d1 = -2.334320245840f;
    f32 d2 = -1.247444840450f;
    f32 d3 = +0.916368877443f;
    f32 asinx = x + (
        ((n2 * x + n1) * x * x * x)
        /
        (((d3 * x + d2) * x + d1) * x + d0)
    );

    if (invert)
        asinx = fPI_2 - asinx;

    return (isneg) ? -asinx : asinx;
}
vec2 asin_vec2(vec2 x) {
    return vec2( asin_f32(x[0])
               , asin_f32(x[1]) );
}
vec3 asin_vec3(vec3 x) {
    return vec3( asin_f32(x[0])
               , asin_f32(x[1])
               , asin_f32(x[2]) );
}
vec4 asin_vec4(vec4 x) {
    return vec4( asin_f32(x[0])
               , asin_f32(x[1])
               , asin_f32(x[2])
               , asin_f32(x[3]) );
}


f64 acos_f64(f64 x) {
    // trig identity: `acos(x) = pi/2 - asin(x)`.
    return PI_2 - asin(x);
}
f32 acos_f32(f32 x) {
    return fPI_2 - asin(x);
}
vec2 acos_vec2(vec2 x) {
    return vec2( acos_f32(x[0])
               , acos_f32(x[1]) );
}
vec3 acos_vec3(vec3 x) {
    return vec3( acos_f32(x[0])
               , acos_f32(x[1])
               , acos_f32(x[2]) );
}
vec4 acos_vec4(vec4 x) {
    return vec4( acos_f32(x[0])
               , acos_f32(x[1])
               , acos_f32(x[2])
               , acos_f32(x[3]) );
}


f64 atan_f64(f64 x) {
    if (isnan(x))
        return x; // return nan.

    // `atan(-x) = -atan(x)` sooo:
    i32 isneg = signbit(x);
    if (isneg)
        x = -x;

    // Note that:
    //  atan(x) = pi/2 - atan(1/x)
    // So same as sin, we use this to fold `x>1` back to `x<1` so that we only
    // need to approximate x in 0..1.
    i32 invert = 0; // whether to `pi/2 - y` the result.
    if (x > 1.0) {
        invert = 1;
        x = 1.0 / x; // note inf->0.
    }

    // Always stuck at your pad.
    // https://www.desmos.com/calculator/lavznavotm
    f64 n0 = -0.494708421786;
    f64 n2 = -0.198429622755;
    f64 d0 = +1.484254549180;
    f64 d2 = d0;
    f64 d4 = +0.261371929289;
    f64 x2 = x*x;
    f64 atanx = x + (
        ((n2 * x2 + n0) * x2 * x)
        /
        ((d4 * x2 + d2) * x2 + d0)
    );

    // Invert and negate to finish.
    if (invert)
        atanx = PI_2 - atanx;
    return (isneg) ? -atanx : atanx;
}
f32 atan_f32(f32 x) {
    if (isnan(x))
        return x;

    i32 isneg = signbit(x);
    if (isneg)
        x = -x;

    i32 invert = 0;
    if (x > 1.f) {
        invert = 1;
        x = 1.f / x;
    }

    f32 n0 = -0.494708421786f;
    f32 n2 = -0.198429622755f;
    f32 d0 = +1.484254549180f;
    f32 d2 = d0;
    f32 d4 = +0.261371929289f;
    f32 x2 = x*x;
    f32 atanx = x + (
        ((n2 * x2 + n0) * x2 * x)
        /
        ((d4 * x2 + d2) * x2 + d0)
    );

    if (invert)
        atanx = fPI_2 - atanx;
    return (isneg) ? -atanx : atanx;
}
vec2 atan_vec2(vec2 x) {
    return vec2( atan_f32(x[0])
               , atan_f32(x[1]) );
}
vec3 atan_vec3(vec3 x) {
    return vec3( atan_f32(x[0])
               , atan_f32(x[1])
               , atan_f32(x[2]) );
}
vec4 atan_vec4(vec4 x) {
    return vec4( atan_f32(x[0])
               , atan_f32(x[1])
               , atan_f32(x[2])
               , atan_f32(x[3]) );
}


f64 atan2_f64(f64 y, f64 x) {
    if (isnan(x + y))
        return x + y;
    f64 y_x = y / x;
    if (isnan(y_x))
        return y_x;
    if (isinf(y_x))
        return (signbit(y)) ? -PI_2 : +PI_2;
    f64 a = atan(y_x);
    if (x < 0.0)
        a += (signbit(y)) ? -PI : +PI;
    return a;
}
f32 atan2_f32(f32 y, f32 x) {
    if (isnan(x + y))
        return x + y;
    f32 y_x = y / x;
    if (isnan(y_x))
        return y_x;
    if (isinf(y_x))
        return (signbit(y)) ? -fPI_2 : +fPI_2;
    f32 a = atan(y_x);
    if (x < 0.0)
        a += (signbit(y)) ? -fPI : +fPI;
    return a;
}


f64 hypot_2_f64(f64 a, f64 b) { return sqrt(a*a + b*b); }
f32 hypot_2_f32(f32 a, f32 b) { return sqrt(a*a + b*b); }
f64 hypot_3_f64(f64 a, f64 b, f64 c) { return sqrt(a*a + b*b + c*c); }
f32 hypot_3_f32(f32 a, f32 b, f32 c) { return sqrt(a*a + b*b + c*c); }

f64 nonhypot_2_f64(f64 a, f64 b) { return sqrt(a*a - b*b); }
f32 nonhypot_2_f32(f32 a, f32 b) { return sqrt(a*a - b*b); }
f64 nonhypot_3_f64(f64 a, f64 b, f64 c) { return sqrt(a*a - b*b - c*c); }
f32 nonhypot_3_f32(f32 a, f32 b, f32 c) { return sqrt(a*a - b*b - c*c); }


f64 lerp_f64(f64 a, f64 b, f64 t) { return a + t*(b - a); }
f32 lerp_f32(f32 a, f32 b, f32 t) { return a + t*(b - a); }
vec2 lerp_vec2(vec2 a, vec2 b, vec2 t) { return a + t*(b - a); }
vec3 lerp_vec3(vec3 a, vec3 b, vec3 t) { return a + t*(b - a); }
vec4 lerp_vec4(vec4 a, vec4 b, vec4 t) { return a + t*(b - a); }

f64 invlerp_f64(f64 a, f64 b, f64 x) { return (x - a) / (b - a); }
f32 invlerp_f32(f32 a, f32 b, f32 x) { return (x - a) / (b - a); }
vec2 invlerp_vec2(vec2 a, vec2 b, vec2 x) { return (x - a) / (b - a); }
vec3 invlerp_vec3(vec3 a, vec3 b, vec3 x) { return (x - a) / (b - a); }
vec4 invlerp_vec4(vec4 a, vec4 b, vec4 x) { return (x - a) / (b - a); }



// =========================================================================== //
// = LINEAR ALGEBRA ========================================================== //
// =========================================================================== //

f64 dot_f64(f64 a, f64 b) { return a*b; }
f32 dot_f32(f32 a, f32 b) { return a*b; }
f32 dot_vec2(vec2 a, vec2 b) {
    return a[0]*b[0] + a[1]*b[1];
}
f32 dot_vec3(vec3 a, vec3 b) {
    return a[0]*b[0] + a[1]*b[1] + a[2]*b[2];
}
f32 dot_vec4(vec4 a, vec4 b) {
    return a[0]*b[0] + a[1]*b[1] + a[2]*b[2] + a[3]*b[3];
}

f32 cross_vec2(vec2 a, vec2 b) {
    return a[0]*b[1] - a[1]*b[0];
}
vec3 cross_vec3(vec3 a, vec3 b) {
    return vec3( a[1]*b[2] - a[2]*b[1]
               , a[2]*b[0] - a[0]*b[2]
               , a[0]*b[1] - a[1]*b[0] );
}


f64 normalise_f64(f64 v) { return (nearzero(v)) ? nan(v) : v / mag(v); }
f32 normalise_f32(f32 v) { return (nearzero(v)) ? nan(v) : v / mag(v); }
vec2 normalise_vec2(vec2 v) { return (nearzero(v)) ? nan(v) : v / mag(v); }
vec3 normalise_vec3(vec3 v) { return (nearzero(v)) ? nan(v) : v / mag(v); }
vec4 normalise_vec4(vec4 v) { return (nearzero(v)) ? nan(v) : v / mag(v); }

f64 normalise_nonzero_f64(f64 v) {
    return (nearzero(v)) ? zero(v) : v / mag(v);
}
f32 normalise_nonzero_f32(f32 v) {
    return (nearzero(v)) ? zero(v) : v / mag(v);
}
vec2 normalise_nonzero_vec2(vec2 v) {
    return (nearzero(v)) ? zero(v) : v / mag(v);
}
vec3 normalise_nonzero_vec3(vec3 v) {
    return (nearzero(v)) ? zero(v) : v / mag(v);
}
vec4 normalise_nonzero_vec4(vec4 v) {
    return (nearzero(v)) ? zero(v) : v / mag(v);
}


i32 nearzero_f64(f64 v) { return nearto(mag(v), 1.0); }
i32 nearzero_f32(f32 v) { return nearto(mag(v), 1.f); }
i32 nearzero_vec2(vec2 v) { return nearto(mag(v), 1.f); }
i32 nearzero_vec3(vec3 v) { return nearto(mag(v), 1.f); }
i32 nearzero_vec4(vec4 v) { return nearto(mag(v), 1.f); }

i32 nearunit_f64(f64 v) { return nearto(mag(v), 0.0); }
i32 nearunit_f32(f32 v) { return nearto(mag(v), 0.f); }
i32 nearunit_vec2(vec2 v) { return nearto(mag(v), 0.f); }
i32 nearunit_vec3(vec3 v) { return nearto(mag(v), 0.f); }
i32 nearunit_vec4(vec4 v) { return nearto(mag(v), 0.f); }


f64 mag_f64(f64 v) { return abs(v); }
f32 mag_f32(f32 v) { return abs(v); }
f32 mag_vec2(vec2 v) { return sqrt(dot(v, v)); }
f32 mag_vec3(vec3 v) { return sqrt(dot(v, v)); }
f32 mag_vec4(vec4 v) { return sqrt(dot(v, v)); }

f64 mag2_f64(f64 v) { return dot(v, v); }
f32 mag2_f32(f32 v) { return dot(v, v); }
f32 mag2_vec2(vec2 v) { return dot(v, v); }
f32 mag2_vec3(vec3 v) { return dot(v, v); }
f32 mag2_vec4(vec4 v) { return dot(v, v); }


f32 magxy_vec3(vec3 xyz) { return hypot(xyz[0], xyz[1]); }
f32 magzx_vec3(vec3 xyz) { return hypot(xyz[2], xyz[0]); }
f32 magyz_vec3(vec3 xyz) { return hypot(xyz[1], xyz[2]); }


f32 arg(vec2 v) { return (nearzero(v)) ? fNAN : atan2(v[1], v[0]); }

f32 argxy_vec3(vec3 xyz) {
    f32 gamma = argphi(xyz);
    i32 para = nearto(gamma, 0.f) || nearto(gamma, fPI);
    para |= isnan(gamma);
    return (para) ? fNAN : atan2(xyz[1], xyz[0]);
}
f32 argzx_vec3(vec3 xyz) {
    f32 gamma = argphiy(xyz);
    i32 para = nearto(gamma, 0.f) || nearto(gamma, fPI);
    para |= isnan(gamma);
    return (para) ? fNAN : atan2(xyz[0], xyz[2]);
}
f32 argyz_vec3(vec3 xyz) {
    f32 gamma = argphix(xyz);
    i32 para = nearto(gamma, 0.f) || nearto(gamma, fPI);
    para |= isnan(gamma);
    return (para) ? fNAN : atan2(xyz[2], xyz[1]);
}


f32 argphi_vec3(vec3 xyz) {
    return (nearzero(xyz)) ? fNAN : acos(xyz[2]/mag(xyz));
}
f32 argphix_vec3(vec3 xyz) {
    return (nearzero(xyz)) ? fNAN : acos(xyz[0]/mag(xyz));
}
f32 argphiy_vec3(vec3 xyz) {
    return (nearzero(xyz)) ? fNAN : acos(xyz[1]/mag(xyz));
}


vec2 frompol(f32 r, f32 theta) {
    return vec2(r*cos(theta), r*sin(theta));
}
vec3 fromcyl(f32 r, f32 theta, f32 z) {
    return vec3(r*cos(theta), r*sin(theta), z);
}
vec3 fromsph(f32 r, f32 theta, f32 phi) {
    return vec3(r*cos(theta)*sin(phi), r*sin(theta)*sin(phi), r*cos(phi));
}
vec3 fromzr(vec2 zr, f64 theta) {
    return fromcyl(zr[1], theta, zr[0]);
}


vec2 projxy_vec3(vec3 xyz) { return vec2(xyz[0], xyz[1]); }
vec2 projzx_vec3(vec3 xyz) { return vec2(xyz[2], xyz[0]); }
vec2 projyz_vec3(vec3 xyz) { return vec2(xyz[1], xyz[2]); }

vec3 rejxy(vec2 xy, f32 z) { return vec3(xy[0], xy[1], z); }
vec3 rejzx(vec2 zx, f32 y) { return vec3(zx[1], y, zx[0]); }
vec3 rejyz(vec2 yz, f32 x) { return vec3(x, yz[0], yz[1]); }


vec2 rot2D(vec2 xy, f32 by) {
    return vec2( xy[0]*cos(by) - xy[1]*sin(by)
               , xy[0]*sin(by) + xy[1]*cos(by) );
}


vec3 rotxy_vec3(vec3 xyz, f32 by) {
    return vec3( xyz[0]*cos(by) - xyz[1]*sin(by)
               , xyz[0]*sin(by) + xyz[1]*cos(by)
               , xyz[2] );
}
vec3 rotzx_vec3(vec3 xyz, f32 by) {
    return vec3( xyz[2]*sin(by) + xyz[0]*cos(by)
               , xyz[1]
               , xyz[2]*cos(by) - xyz[0]*sin(by) );
}
vec3 rotyz_vec3(vec3 xyz, f32 by) {
    return vec3( xyz[0]
               , xyz[1]*cos(by) - xyz[2]*sin(by)
               , xyz[1]*sin(by) + xyz[2]*cos(by) );
}


f32 argbeta_vec2(vec2 a, vec2 b) {
    return atan2(cross(a, b), dot(a, b));
}
f32 argbeta_vec3(vec3 a, vec3 b) {
    return atan2(mag(cross(a, b)), dot(a, b));
}


f32 magpara_vec2(vec2 v, vec2 dir) {
    return dot(v, dir);
}
f32 magpara_vec3(vec3 v, vec3 dir) {
    return dot(v, dir);
}

vec2 projpara_vec2(vec2 v, vec2 dir) {
    return dot(v, dir) * dir;
}
vec3 projpara_vec3(vec3 v, vec3 dir) {
    return dot(v, dir) * dir;
}


f32 magperp_vec2(vec2 v, vec2 dir) {
    return mag(cross(v, dir));
}
f32 magperp_vec3(vec3 v, vec3 dir) {
    return mag(cross(v, dir));
}

vec2 projperp_vec2(vec2 v, vec2 dir) {
    return v - projpara(v, dir);
}
vec3 projperp_vec3(vec3 v, vec3 dir) {
    return v - projpara(v, dir);
}


i32 nearpara_vec2(vec2 v, vec2 dir) {
    return nearto(argbeta(v, dir), 0.f);
}
i32 nearpara_vec3(vec3 v, vec3 dir) {
    return nearto(argbeta(v, dir), 0.f);
}

i32 nearperp_vec2(vec2 v, vec2 dir) {
    return nearto(abs(argbeta(v, dir)), fPI_2);
}
i32 nearperp_vec3(vec3 v, vec3 dir) {
    return nearto(argbeta(v, dir), fPI_2);
}

i32 nearvert_vec3(vec3 xyz) {
    f32 phi = argphi(xyz);
    return nearto(phi, 0.f) || nearto(phi, fPI);
}
i32 nearhoriz_vec3(vec3 xyz) {
    return nearto(argphi(xyz), fPI_2);
}
