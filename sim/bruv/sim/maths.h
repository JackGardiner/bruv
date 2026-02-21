#pragma once
#include "br.h"



// ========================= //
//         CONSTANTS         //
// ========================= //

// coupla constants.

#define PI     (3.141592653589793)  // pi
#define PI_2   (1.5707963267948966) // pi/2
#define PI_4   (0.7853981633974483) // pi/4
#define TWOPI  (6.283185307179586)  // 2*pi
#define EUL    (2.718281828459045)  // e
#define LN2    (0.6931471805599453) // log_e(2)
#define LN10   (2.302585092994046)  // log_e(10)
#define LOG2E  (1.4426950408889634) // log_2(e)
#define LOG10E (0.4342944819032518) // log_10(e)
#define SQRTH  (0.7071067811865476) // (1/2)^(1/2)
#define SQRT2  (1.4142135623730951) // 2^(1/2)
#define SQRT3  (1.7320508075688772) // 3^(1/2)
#define SQRT4  (2.0000000000000000) // 4^(1/2)
#define CBRTH  (0.7937005259840997) // (1/2)^(1/3)
#define CBRT2  (1.259921049894873)  // 2^(1/3)
#define CBRT3  (1.4422495703074083) // 3^(1/3)

#define fPI     (3.1415927f)  // 32b; pi
#define fPI_2   (1.5707964f)  // 32b; pi/2
#define fPI_4   (0.7853982f)  // 32b; pi/4
#define fTWOPI  (6.2831855f)  // 32b; 2*pi
#define fEUL    (2.7182817f)  // 32b; e
#define fLN2    (0.6931472f)  // 32b; log_e(2)
#define fLN10   (2.3025851f)  // 32b; log_e(10)
#define fLOG2E  (1.442695f)   // 32b; log_2(e)
#define fLOG10E (0.43429446f) // 32b; log_10(e)
#define fSQRTH  (0.70710677f) // 32b; (1/2)^(1/2)
#define fSQRT2  (1.4142135f)  // 32b; 2^(1/2)
#define fSQRT3  (1.7320508f)  // 32b; 3^(1/2)
#define fSQRT4  (2.0000000f)  // 32b; 4^(1/2)
#define fCBRTH  (0.7937005f)  // 32b; (1/2)^(1/3)
#define fCBRT2  (1.2599211f)  // 32b; 2^(1/3)
#define fCBRT3  (1.4422495f)  // 32b; 3^(1/3)

#define v2PI     (vec2(fPI))     // vec2; pi
#define v2PI_2   (vec2(fPI_2))   // vec2; pi/2
#define v2PI_4   (vec2(fPI_4))   // vec2; pi/4
#define v2TWOPI  (vec2(fTWOPI))  // vec2; 2*pi
#define v2EUL    (vec2(fEUL))    // vec2; e
#define v2LN2    (vec2(fLN2))    // vec2; log_e(2)
#define v2LN10   (vec2(fLN10))   // vec2; log_e(10)
#define v2LOG2E  (vec2(fLOG2E))  // vec2; log_2(e)
#define v2LOG10E (vec2(fLOG10E)) // vec2; log_10(e)
#define v2SQRTH  (vec2(fSQRTH))  // vec2; (1/2)^(1/2)
#define v2SQRT2  (vec2(fSQRT2))  // vec2; 2^(1/2)
#define v2SQRT3  (vec2(fSQRT3))  // vec2; 3^(1/2)
#define v2SQRT4  (vec2(fSQRT4))  // vec2; 4^(1/2)
#define v2CBRTH  (vec2(fCBRTH))  // vec2; (1/2)^(1/3)
#define v2CBRT2  (vec2(fCBRT2))  // vec2; 2^(1/3)
#define v2CBRT3  (vec2(fCBRT3))  // vec2; 3^(1/3)

#define v3PI     (vec3(fPI))     // vec3; pi
#define v3PI_2   (vec3(fPI_2))   // vec3; pi/2
#define v3PI_4   (vec3(fPI_4))   // vec3; pi/4
#define v3TWOPI  (vec3(fTWOPI))  // vec3; 2*pi
#define v3EUL    (vec3(fEUL))    // vec3; e
#define v3LN2    (vec3(fLN2))    // vec3; log_e(2)
#define v3LN10   (vec3(fLN10))   // vec3; log_e(10)
#define v3LOG2E  (vec3(fLOG2E))  // vec3; log_2(e)
#define v3LOG10E (vec3(fLOG10E)) // vec3; log_10(e)
#define v3SQRTH  (vec3(fSQRTH))  // vec3; (1/2)^(1/2)
#define v3SQRT2  (vec3(fSQRT2))  // vec3; 2^(1/2)
#define v3SQRT3  (vec3(fSQRT3))  // vec3; 3^(1/2)
#define v3SQRT4  (vec3(fSQRT4))  // vec3; 4^(1/2)
#define v3CBRTH  (vec3(fCBRTH))  // vec3; (1/2)^(1/3)
#define v3CBRT2  (vec3(fCBRT2))  // vec3; 2^(1/3)
#define v3CBRT3  (vec3(fCBRT3))  // vec3; 3^(1/3)

#define v4PI     (vec4(fPI))     // vec4; pi
#define v4PI_2   (vec4(fPI_2))   // vec4; pi/2
#define v4PI_4   (vec4(fPI_4))   // vec4; pi/4
#define v4TWOPI  (vec4(fTWOPI))  // vec4; 2*pi
#define v4EUL    (vec4(fEUL))    // vec4; e
#define v4LN2    (vec4(fLN2))    // vec4; log_e(2)
#define v4LN10   (vec4(fLN10))   // vec4; log_e(10)
#define v4LOG2E  (vec4(fLOG2E))  // vec4; log_2(e)
#define v4LOG10E (vec4(fLOG10E)) // vec4; log_10(e)
#define v4SQRTH  (vec4(fSQRTH))  // vec4; (1/2)^(1/2)
#define v4SQRT2  (vec4(fSQRT2))  // vec4; 2^(1/2)
#define v4SQRT3  (vec4(fSQRT3))  // vec4; 3^(1/2)
#define v4SQRT4  (vec4(fSQRT4))  // vec4; 4^(1/2)
#define v4CBRTH  (vec4(fCBRTH))  // vec4; (1/2)^(1/3)
#define v4CBRT2  (vec4(fCBRT2))  // vec4; 2^(1/3)
#define v4CBRT3  (vec4(fCBRT3))  // vec4; 3^(1/3)



// ========================= //
//         BIT MANIP         //
// ========================= //

// Returns 1 if every bit in `mask` is one (set) in `x`, 0 otherwise.
#define isset(x, mask) ( ((x) & (mask)) == (mask) )

// Returns 1 if every bit in `mask` is zero (cleared) in `x`, 0 otherwise.
#define isclr(x, mask) ( ((x) & (mask)) == 0 )


// Returns `n` contiguous bits, starting from the lowest bit. This can be used to
// mask the low `n` bits.
// - `n` must be in 0..64.
// - The type of the return is `u64`.
// - The value of the return is `(1 << n) - 1` (assuming infinite precision).
#define lobits(n) ( ((n) == 64) ? (u64)-1 : ((u64)1U << (n)) - 1 )

// Returns a number with only the `n`th bit set. This can be used to mask the
// `n`th bit.
// - `n` must be in 0..63.
// - The type of the return is `u64`.
// - The value of the return is `(1 << n)` (assuming infinite precision).
#define nthbit(n) ( (u64)1U << (n) )

// Returns 1 if `x` is a power-of-2 (meaning it has exactly one bit set), 0
// otherwise.
// - `x` must be >=0.
#define ispow2(x) ( (x) != 0 && ((x) & ((x) - 1)) == 0 )



// ========================= //
//        COMPARISONS        //
// ========================= //


// Returns 1 if all elements in the given expressions are element-wise equal, 0
// otherwise. This is needed since the builtin vector comparison operators act
// element-wise.
#define eq(xs...) ( 1 FOREACH(eq_, FIRST(xs), xs) )
// Two-argument version of `eq` (slightly more performant/doesn't depend on
// `FOREACH`).
#define eq2(a, b) ( choose_all_2_(eq, (a), (b)) )
i32 eq_i32(i32 a, i32 b);
i32 eq_i64(i64 a, i64 b);
i32 eq_u32(u32 a, u32 b);
i32 eq_u64(u64 a, u64 b);
i32 eq_f32(f32 a, f32 b);
i32 eq_f64(f64 a, f64 b);
i32 eq_vec2(vec2 a, vec2 b);
i32 eq_vec3(vec3 a, vec3 b);
i32 eq_vec4(vec4 a, vec4 b);

// Returns 1 if any elements in `a` and `b` are element-wise equal, 0 otherwise.
// This is needed since the builtin vector comparison operators act element-wise.
#define anyeq(a, b) ( choose_all_2_(anyeq, (a), (b)) )
i32 anyeq_i32(i32 a, i32 b);
i32 anyeq_i64(i64 a, i64 b);
i32 anyeq_u32(u32 a, u32 b);
i32 anyeq_u64(u64 a, u64 b);
i32 anyeq_f32(f32 a, f32 b);
i32 anyeq_f64(f64 a, f64 b);
i32 anyeq_vec2(vec2 a, vec2 b);
i32 anyeq_vec3(vec3 a, vec3 b);
i32 anyeq_vec4(vec4 a, vec4 b);

// =`!eq(a, b)`
#define neq(a, b) ( !eq2((a), (b)) )

// =`!anyeq(a, b)`
#define allneq(a, b) ( !anyeq((a), (b)) )


// Returns 1 if `x` is within [`lo`, `hi`], inclusive of each, 0 otherwise.
// - Does not support floating point vectors.
#define within(x, lo, hi) ((lo) <= (x) && (x) <= (hi))

// Returns 1 if `x` is equal to any of the values in `vals`, 0 otherwise.
// - If given no arguments, returns 0.
// - Supports floating point vectors.
#define oneof(x, vals...) ( 0 FOREACH(oneof_, (x), vals) )


// Returns 1 if any element of `x` is NaN, 0 otherwise.
#define isnan(x) ( choose_fp_1_(isnan, (x)) )
i32 isnan_f64(f64 x);
i32 isnan_f32(f32 x);
i32 isnan_vec2(vec2 x);
i32 isnan_vec3(vec3 x);
i32 isnan_vec4(vec4 x);
// Returns 1 if all elements in `x` are NaN, 0 otherwise.
#define isallnan(x) ( choose_fp_1_(isallnan, (x)) )
i32 isallnan_f64(f64 x);
i32 isallnan_f32(f32 x);
i32 isallnan_vec2(vec2 x);
i32 isallnan_vec3(vec3 x);
i32 isallnan_vec4(vec4 x);
// =`!isnan(x)`
#define notnan(x) ( !isnan((x)) )

// Returns 1 if any element in `x` is infinite, 0 otherwise.
#define isinf(x) ( choose_fp_1_(isinf, (x)) )
i32 isinf_f64(f64 x);
i32 isinf_f32(f32 x);
i32 isinf_vec2(vec2 x);
i32 isinf_vec3(vec3 x);
i32 isinf_vec4(vec4 x);
// Returns 1 if all elements in `x` are infinite, 0 otherwise.
#define isallinf(x) ( choose_fp_1_(isallinf, (x)) )
i32 isallinf_f64(f64 x);
i32 isallinf_f32(f32 x);
i32 isallinf_vec2(vec2 x);
i32 isallinf_vec3(vec3 x);
i32 isallinf_vec4(vec4 x);
// =`!isinf(x)`
#define notinf(x) ( !isinf((x)) )

// Returns 1 if every element in `x` is not infinite and not NaN, 0 otherwise.
#define isgood(x) ( notinf((x)) && notnan((x)) )



// ========================= //
//      ABS/MIN/MAX/MOD      //
// ========================= //

// Returns the absolute value of the given number. For integer types, returns the
// result as an unsigned integer. This unsigned promotion ensures that the result
// will always compare >=0 (e.g. `-intmin(i32)` is <0 when left as `i32`, but is
// the correct result in `u32`).
// - Element-wise for vector types.
// - Returns the same type, except promoting signed integers to unsigned.
#define abs(x) ( choose_all_on_god_1_(abs, (x)) )
u8 abs_i8(i8 x);
u16 abs_i16(i16 x);
u32 abs_i32(i32 x);
u64 abs_i64(i64 x);
u8 abs_u8(u8 x);
u16 abs_u16(u16 x);
u32 abs_u32(u32 x);
u64 abs_u64(u64 x);
f32 abs_f32(f32 x);
f64 abs_f64(f64 x);
vec2 abs_vec2(vec2 x);
vec3 abs_vec3(vec3 x);
vec4 abs_vec4(vec4 x);
// Identical to `abs` except doesn't promote signed integers to unsigned.
// - Note in the case where `x` is a signed integer and equal to `intmin(x)`, the
//      result will not change value (i.e. it will still be negative). Use
//      `abs(x)`, which returns unsigned, to avoid this issue.
// - Element-wise for vector types.
// - Returns the exact same type.
#define iabs(x) ( choose_all_on_god_1_(iabs, (x)) )
i8 iabs_i8(i8 x);
i16 iabs_i16(i16 x);
i32 iabs_i32(i32 x);
i64 iabs_i64(i64 x);
u8 iabs_u8(u8 x);
u16 iabs_u16(u16 x);
u32 iabs_u32(u32 x);
u64 iabs_u64(u64 x);
f32 iabs_f32(f32 x);
f64 iabs_f64(f64 x);
vec2 iabs_vec2(vec2 x);
vec3 iabs_vec3(vec3 x);
vec4 iabs_vec4(vec4 x);


// Returns the minimum of the given numbers (or the only non-nan).
// - Element-wise for vector types.
#define min(a, b) ( choose_all_2_(min, (a), (b)) )
i32 min_i32(i32 a, i32 b);
i64 min_i64(i64 a, i64 b);
u32 min_u32(u32 a, u32 b);
u64 min_u64(u64 a, u64 b);
f32 min_f32(f32 a, f32 b);
f64 min_f64(f64 a, f64 b);
vec2 min_vec2(vec2 a, vec2 b);
vec3 min_vec3(vec3 a, vec3 b);
vec4 min_vec4(vec4 a, vec4 b);

// Returns the maximum of the given numbers (or the only non-nan).
// - Element-wise for vector types.
#define max(a, b) ( choose_all_2_(max, (a), (b)) )
i32 max_i32(i32 a, i32 b);
i64 max_i64(i64 a, i64 b);
u32 max_u32(u32 a, u32 b);
u64 max_u64(u64 a, u64 b);
f32 max_f32(f32 a, f32 b);
f64 max_f64(f64 a, f64 b);
vec2 max_vec2(vec2 a, vec2 b);
vec3 max_vec3(vec3 a, vec3 b);
vec4 max_vec4(vec4 a, vec4 b);


// Returns the minimum element (ignoring nans) in the given floating point
// vector. Accepts non-vectors, but acts as identity.
#define minelem(x) ( choose_all_1_(minelem, (x)) )
i32 minelem_i32(i32 x);
i64 minelem_i64(i64 x);
u32 minelem_u32(u32 x);
u64 minelem_u64(u64 x);
f32 minelem_f32(f32 x);
f64 minelem_f64(f64 x);
f32 minelem_vec2(vec2 x);
f32 minelem_vec3(vec3 x);
f32 minelem_vec4(vec4 x);

// Returns the maximum element (ignoring nans) in the given floating point
// vector. Accepts non-vectors, but acts as identity.
#define maxelem(x) ( choose_all_1_(maxelem, (x)) )
i32 maxelem_i32(i32 x);
i64 maxelem_i64(i64 x);
u32 maxelem_u32(u32 x);
u64 maxelem_u64(u64 x);
f32 maxelem_f32(f32 x);
f64 maxelem_f64(f64 x);
f32 maxelem_vec2(vec2 x);
f32 maxelem_vec3(vec3 x);
f32 maxelem_vec4(vec4 x);


// Returns `x` modulo `y`, corresponding to `x - floor(x/y) * y`. Note that this
// result will have the same sign as `y` (or will be 0).
#define mod(a, b) ( choose_all_2_(mod, (a), (b)) )
i32 mod_i32(i32 a, i32 b);
i64 mod_i64(i64 a, i64 b);
u32 mod_u32(u32 a, u32 b);
u64 mod_u64(u64 a, u64 b);
f32 mod_f32(f32 a, f32 b);
f64 mod_f64(f64 a, f64 b);
vec2 mod_vec2(vec2 a, vec2 b);
vec3 mod_vec3(vec3 a, vec3 b);
vec4 mod_vec4(vec4 a, vec4 b);




// ========================== //
//       FLOATING POINT       //
// ========================== //

// Returns `2^exp`.
f64 f64_with_exp(i32 exp);
// Returns `2^exp`.
f32 f32_with_exp(i32 exp);

// Finds `fraction` and `exponent` s.t. `pos_norm_f = fraction * 2^exponent`,
// where `fraction` is in [1, 2).
f64 f64_split(f64 pos_norm_f, i32* exp);
// Finds `fraction` and `exponent` s.t. `pos_norm_f = fraction * 2^exponent`,
// where `fraction` is in [1, 2).
f32 f32_split(f32 pos_norm_f, i32* exp);

// Returns the signbit of `x`.
#define signbit(x) ( choose_fpscal_1_(signbit, (x)) )
i32 signbit_f32(f32 x);
i32 signbit_f64(f64 x);


// Returns `dflt` if `x` is nan, `x` otherwise.
#define ifnan(x, dflt) ( choose_fp_2_(ifnan, (x), (dflt)) )
f64 ifnan_f64(f64 x, f64 dflt);
f32 ifnan_f32(f32 x, f32 dflt);
vec2 ifnan_vec2(vec2 x, vec2 dflt);
vec4 ifnan_vec4(vec4 x, vec4 dflt);
vec3 ifnan_vec3(vec3 x, vec3 dflt);

// Returns a vector of pair-wise `ifnan`s for each element of `x` and `dflt`.
// Accepts non-vector floating points, but acts identically to `ifnan`.
#define ifnanelem(x, dflt) ( choose_fp_2_(ifnanelem, (x), (dflt)) )
f64 ifnanelem_f64(f64 x, f64 dflt);
f32 ifnanelem_f32(f32 x, f32 dflt);
vec2 ifnanelem_vec2(vec2 x, vec2 dflt);
vec4 ifnanelem_vec4(vec4 x, vec4 dflt);
vec3 ifnanelem_vec3(vec3 x, vec3 dflt);


#define DFLT_RTOL (5e-5) // default relative tolerance.
#define DFLT_ATOL (1e-6) // default absolute tolerance.

// Returns 1 if `a` is near to `b`, 0 otherwise. Note this returns 0 for any
// infinite or nan.
#define nearto(a, b) ( neartox((a), (b), DFLT_RTOL, DFLT_ATOL) )
// Returns 1 if `a` is near to `b` under the given tolerances, 0 otherwise. The
// net tolerance used is `atol + mag(b)*rtol`. Note this returns 0 for any
// infinite or nan.
#define neartox(a, b, rtol, atol) ( generic(0               \
        , int: generic(distinguish_vec3((a) + (b))          \
            ,            f64: neartox_f64                   \
            ,            f32: neartox_f32                   \
            ,           vec2: neartox_vec2                  \
            , genuinely_vec3: neartox_vec3                  \
            ,           vec4: neartox_vec4                  \
        ) ((a), (b), (rtol), (atol))                        \
        , default: (const char*[isvec3(a) == isvec3(b)])    \
            {"vec3 IS ONLY COMPATIBLE WITH OTHER vec3"}     \
    ) )
i32 neartox_f64(f64 a, f64 b, f64 rtol, f64 atol);
i32 neartox_f32(f32 a, f32 b, f32 rtol, f32 atol);
i32 neartox_vec2(vec2 a, vec2 b, f32 rtol, f32 atol);
i32 neartox_vec3(vec3 a, vec3 b, f32 rtol, f32 atol);
i32 neartox_vec4(vec4 a, vec4 b, f32 rtol, f32 atol);


// Returns `x` rounded to the nearest integer.
// - Element-wise for vector types.
#define round(x) ( choose_fp_1_(round, (x)) )
f64 round_f64(f64 x);
f32 round_f32(f32 x);
vec2 round_vec2(vec2 x);
vec3 round_vec3(vec3 x);
vec4 round_vec4(vec4 x);
// Integer-returning version of `round`.
#define iround(x) ( choose_fpscal_1_(iround, (x)) )
i64 iround_f64(f64 x);
i64 iround_f32(f32 x);

// Returns `x` rounded down to the nearest integer.
// - Element-wise for vector types.
#define floor(x) ( choose_fp_1_(floor, (x)) )
f64 floor_f64(f64 x);
f32 floor_f32(f32 x);
vec2 floor_vec2(vec2 x);
vec3 floor_vec3(vec3 x);
vec4 floor_vec4(vec4 x);
// Integer-returning version of `round`.
#define ifloor(x) ( choose_fpscal_1_(ifloor, (x)) )
i64 ifloor_f64(f64 x);
i64 ifloor_f32(f32 x);

// Returns `x` rounded up to the nearest integer.
// - Element-wise for vector types.
#define ceil(x) ( choose_fp_1_(ceil, (x)) )
f64 ceil_f64(f64 x);
f32 ceil_f32(f32 x);
vec2 ceil_vec2(vec2 x);
vec3 ceil_vec3(vec3 x);
vec4 ceil_vec4(vec4 x);
// Integer-returning version of `round`.
#define iceil(x) ( choose_fpscal_1_(iceil, (x)) )
i64 iceil_f64(f64 x);
i64 iceil_f32(f32 x);



// ========================== //
//         ELEMENTARY         //
// ========================== //


// Returns the floored-square-root of `x`, aka the integer-sqrt.
u64 isqrt(u64 x);


// Returns the square root of `x`, `x^(1/2)`. Requires the compiler to be using
// an instruction set that has a sqrt instrinsic bc i cant be bothered to impl it
// myself.
// - Element-wise for vector types.
#define sqrt(x) ( choose_fp_1_(sqrt, (x)) )
f64 sqrt_f64(f64 x);
f32 sqrt_f32(f32 x);
vec2 sqrt_vec2(vec2 x);
vec3 sqrt_vec3(vec3 x);
vec4 sqrt_vec4(vec4 x);

// Returns the cube root of `x`, `x^(1/3)`. Note this works for negative inputs,
// while `pow` would not.
// - Element-wise for vector types.
#define cbrt(x) ( choose_fp_1_(cbrt, (x)) )
f64 cbrt_f64(f64 x);
f32 cbrt_f32(f32 x);
vec2 cbrt_vec2(vec2 x);
vec3 cbrt_vec3(vec3 x);
vec4 cbrt_vec4(vec4 x);

// Returns `x` squared, `x^2`.
// - Element-wise for vector types.
#define sqed(x) ( choose_fp_1_(sqed, (x)) )
f64 sqed_f64(f64 x);
f32 sqed_f32(f32 x);
vec2 sqed_vec2(vec2 x);
vec3 sqed_vec3(vec3 x);
vec4 sqed_vec4(vec4 x);

// Returns `x` cubed, `x^3`.
// - Element-wise for vector types.
#define cbed(x) ( choose_fp_1_(cbed, (x)) )
f64 cbed_f64(f64 x);
f32 cbed_f32(f32 x);
vec2 cbed_vec2(vec2 x);
vec3 cbed_vec3(vec3 x);
vec4 cbed_vec4(vec4 x);


// Returns `2^x`, requiring `-1022 < x < 1024`. Not perfectly accurate, max error
// of ~0.00000002%.
// - Element-wise for vector types.
#define exp2(x) ( choose_fp_1_(exp2, (x)) )
f64 exp2_f64(f64 x);
f32 exp2_f32(f32 x);
vec2 exp2_vec2(vec2 x);
vec3 exp2_vec3(vec3 x);
vec4 exp2_vec4(vec4 x);

// Returns `log_2(x)`, requiring `x >= 2.2250738585072014e-308`. Not perfectly
// accurate, max error of ~0.0000008%, however much higher as the output grows
// very close to 0 (input closer to 1).
// - Element-wise for vector types.
#define log2(x) ( choose_fp_1_(log2, (x)) )
f64 log2_f64(f64 x);
f32 log2_f32(f32 x);
vec2 log2_vec2(vec2 x);
vec3 log2_vec3(vec3 x);
vec4 log2_vec4(vec4 x);

// Returns `x^n`, requiring `x > 0`. Uses a combination of `exp2` and `log2`, so
// not perfectly accurate.
// - Element-wise for vector types.
#define pow(x, n) ( choose_fp_2_(pow, (x), (n)) )
f64 pow_f64(f64 x, f64 n);
f32 pow_f32(f32 x, f32 n);
vec2 pow_vec2(vec2 x, vec2 n);
vec3 pow_vec3(vec3 x, vec3 n);
vec4 pow_vec4(vec4 x, vec4 n);


// Trigonometric sine, `sin(x)`. Over inputs in [-2 pi, 2 pi], maximum error of
// ~0.0000007%. At inputs greater in manitude than 1e+12, this functions accuracy
// breaks down and 0 is returned as a fail-safe.
// - Element-wise for vector types.
#define sin(x) ( choose_fp_1_(sin, (x)) )
f64 sin_f64(f64 x);
f32 sin_f32(f32 x);
vec2 sin_vec2(vec2 x);
vec3 sin_vec3(vec3 x);
vec4 sin_vec4(vec4 x);

// Trigonometric cosine, `cos(x)`. Over inputs in [-2 pi, 2 pi], maximum error of
// ~0.000002%. At inputs greater in manitude than 1e+12, this functions accuracy
// breaks down and 1 is returned as a fail-safe.
// - Element-wise for vector types.
#define cos(x) ( choose_fp_1_(cos, (x)) )
f64 cos_f64(f64 x);
f32 cos_f32(f32 x);
vec2 cos_vec2(vec2 x);
vec3 cos_vec3(vec3 x);
vec4 cos_vec4(vec4 x);

// Trigonometric tangent, `tan(x)`. Over inputs in [-2 pi, 2 pi], maximum error
// of ~0.005%, however this error increases when very very close to the poles.
// Note that when calling this function you can never even attempt to sample the
// poles, since finite floating-point can never exactly represent a non-zero
// multiple of pi/2. At inputs greater in manitude than 1e+10, this functions
// accuracy breaks down and 0 is returned as a fail-safe.
// - Element-wise for vector types.
#define tan(x) ( choose_fp_1_(tan, (x)) )
f64 tan_f64(f64 x);
f32 tan_f32(f32 x);
vec2 tan_vec2(vec2 x);
vec3 tan_vec3(vec3 x);
vec4 tan_vec4(vec4 x);


// Inverse of `sin`, returning `y` s.t. `sin(y) = x`. Not perfectly accurate,
// maximum error of ~0.00002%.
// - Element-wise for vector types.
#define asin(x) ( choose_fp_1_(asin, (x)) )
f64 asin_f64(f64 x);
f32 asin_f32(f32 x);
vec2 asin_vec2(vec2 x);
vec3 asin_vec3(vec3 x);
vec4 asin_vec4(vec4 x);

// Inverse of `cos`, returning `y` s.t. `cos(y) = x`. Not perfectly accurate,
// maximum error of ~0.00002%.
// - Element-wise for vector types.
#define acos(x) ( choose_fp_1_(acos, (x)) )
f64 acos_f64(f64 x);
f32 acos_f32(f32 x);
vec2 acos_vec2(vec2 x);
vec3 acos_vec3(vec3 x);
vec4 acos_vec4(vec4 x);

// Inverse of `tan`, returning `y` s.t. `tan(y) = x`. Not perfectly accurate,
// maximum error of ~0.00007%.
// - Element-wise for vector types.
#define atan(x) ( choose_fp_1_(atan, (x)) )
f64 atan_f64(f64 x);
f32 atan_f32(f32 x);
vec2 atan_vec2(vec2 x);
vec3 atan_vec3(vec3 x);
vec4 atan_vec4(vec4 x);


// Returns `atan(y/x)`, with a quadrant-aware-ly signed return.
#define atan2(y, x) ( choose_fpscal_2_(atan2, (y), (x)) )
f64 atan2_f64(f64 y, f64 x);
f32 atan2_f32(f32 y, f32 x);

// Returns `sqrt(sum(sides**2))`. Only accepts 2 or 3 arguments.
#define hypot(sides...) ( IDENTITY(GLUE2(hypot_, countva(sides)) (sides)) )
#define hypot_2(a, b) choose_fpscal_2_(hypot_2, a, b)
#define hypot_3(a, b, c) choose_fpscal_3_(hypot_3, a, b, c)
f64 hypot_2_f64(f64 a, f64 b);
f32 hypot_2_f32(f32 a, f32 b);
f64 hypot_3_f64(f64 a, f64 b, f64 c);
f32 hypot_3_f32(f32 a, f32 b, f32 c);
// Returns `sqrt(hypot**2 - sum(others**2))`. Only accepts 2 or 3 arguments.
// - an instant classic.
#define nonhypot(hypotenuse, others...) ( IDENTITY(                         \
        GLUE2(nonhypot_, countva(hypotenuse, others)) (hypotenuse, others)  \
    ) )
#define nonhypot_2(a, b) choose_fpscal_2_(nonhypot_2, a, b)
#define nonhypot_3(a, b, c) choose_fpscal_3_(nonhypot_3, a, b, c)
f64 nonhypot_2_f64(f64 a, f64 b);
f32 nonhypot_2_f32(f32 a, f32 b);
f64 nonhypot_3_f64(f64 a, f64 b, f64 c);
f32 nonhypot_3_f32(f32 a, f32 b, f32 c);


// Returns a value which is proportionally `t` of the way from `a` to `b`,
// `a + t*(b - a)`.
#define lerp(a, b, t) ( choose_fpscal_3_(lerp, (a), (b), (t)) )
f64 lerp_f64(f64 a, f64 b, f64 t);
f32 lerp_f32(f32 a, f32 b, f32 t);
// Forwards to lerp with `t = i / (N - 1)`.
#define lerpidx(a, b, i, N) \
    ( lerp_((a), (b), ((i) / (typeof((a) + (b)))((N) - 1))) )

// Inverse of `lerp`, returning the `t` value s.t. `x = lerp(a, b, t)`,
// `(x - a) / (b - a)`.
#define invlerp(a, b, x) ( choose_fpscal_3_(invlerp, (a), (b), (x)) )
f64 invlerp_f64(f64 a, f64 b, f64 x);
f32 invlerp_f32(f32 a, f32 b, f32 x);



// ========================== //
//       LINEAR ALGEBRA       //
// ========================== //

// Dot product of `a` and `b`. Supports scalars, acting as a multiply.
#define dot(a, b) ( choose_fp_2_(dot, (a), (b)) )
f64 dot_f64(f64 a, f64 b);
f32 dot_f32(f32 a, f32 b);
f32 dot_vec2(vec2 a, vec2 b);
f32 dot_vec3(vec3 a, vec3 b);
f32 dot_vec4(vec4 a, vec4 b);

// 3D cross product of `a` and `b`.
#define cross(a, b) ( choose_fp2D3D_2_(cross, (a), (b)) )
f32 cross_vec2(vec2 a, vec2 b);
vec3 cross_vec3(vec3 a, vec3 b);


// Normalises `v` to a magnitude of 1. Returns nan if `v` is zero.
#define normalise(v) ( choose_fp_1_(normalise, (v)) )
f64 normalise_f64(f64 v);
f32 normalise_f32(f32 v);
vec2 normalise_vec2(vec2 v);
vec3 normalise_vec3(vec3 v);
vec4 normalise_vec4(vec4 v);
// Specialisation of `normalise` to return zero if `v` is zero.
#define normalise_nonzero(v) ( choose_fp_1_(normalise_nonzero_, (v)) )
f64 normalise_nonzero_f64(f64 v);
f32 normalise_nonzero_f32(f32 v);
vec2 normalise_nonzero_vec2(vec2 v);
vec3 normalise_nonzero_vec3(vec3 v);
vec4 normalise_nonzero_vec4(vec4 v);


// Returns 1 if `mag(x)` is near to 0.0, 0 otherwise.
#define nearzero(x) ( choose_fp_1_(nearzero, (x)) )
i32 nearzero_f64(f64 v);
i32 nearzero_f32(f32 v);
i32 nearzero_vec2(vec2 v);
i32 nearzero_vec3(vec3 v);
i32 nearzero_vec4(vec4 v);

// Returns 1 if `mag(x)` is near to 1.0, 0 otherwise.
#define nearunit(x) ( choose_fp_1_(nearunit, (x)) )
i32 nearunit_f64(f64 v);
i32 nearunit_f32(f32 v);
i32 nearunit_vec2(vec2 v);
i32 nearunit_vec3(vec3 v);
i32 nearunit_vec4(vec4 v);


// Magnitude of `v`, `sqrt(sum(v**2))`.
#define mag(v) ( choose_fp_1_(mag, (v)) )
f64 mag_f64(f64 v);
f32 mag_f32(f32 v);
f32 mag_vec2(vec2 v);
f32 mag_vec3(vec3 v);
f32 mag_vec4(vec4 v);
// Squared magnitude of `v`, `sum(v**2)`.
#define mag2(v) ( choose_fp_1_(mag2, (v)) )
f64 mag2_f64(f64 v);
f32 mag2_f32(f32 v);
f32 mag2_vec2(vec2 v);
f32 mag2_vec3(vec3 v);
f32 mag2_vec4(vec4 v);

// Magnitude of the X-Y projection of `xyz`.
#define magxy(xyz) ( choose_fpvec3_(magxy, (xyz)) )
f32 magxy_vec3(vec3 xyz);
// Magnitude of the Z-X projection of `xyz`.
#define magzx(xyz) ( choose_fpvec3_(magzx, (xyz)) )
f32 magzx_vec3(vec3 xyz);
// Magnitude of the Y-Z projection of `xyz`.
#define magyz(xyz) ( choose_fpvec3_(magyz, (xyz)) )
f32 magyz_vec3(vec3 xyz);


// Argument of `v`, `atan2(v[1], v[0])`.
f32 arg(vec2 v);

// Argument of the X-Y projection of `xyz`.
#define argxy(xyz) ( choose_fpvec3_(argxy, (xyz)) )
f32 argxy_vec3(vec3 xyz);
// Argument of the Z-X projection of `xyz`.
#define argzx(xyz) ( choose_fpvec3_(argzx, (xyz)) )
f32 argzx_vec3(vec3 xyz);
// Argument of the Y-Z projection of `xyz`.
#define argyz(xyz) ( choose_fpvec3_(argyz, (xyz)) )
f32 argyz_vec3(vec3 xyz);


// Angle between `xyz` and the +Z axis.
#define argphi(xyz) ( choose_fpvec3_(argphi, (xyz)) )
f32 argphi_vec3(vec3 xyz);
// Angle between `xyz` and the +X axis.
#define argphix(xyz) ( choose_fpvec3_(argphix, (xyz)) )
f32 argphix_vec3(vec3 xyz);
// Angle between `xyz` and the +Y axis.
#define argphiy(xyz) ( choose_fpvec3_(argphiy, (xyz)) )
f32 argphiy_vec3(vec3 xyz);


// Returns the cartesian vector corresponding to the given polar coordinates.
vec2 frompol(f32 r, f32 theta);
// Returns the cartesian vector corresponding to the given cylindrical
// coordinates.
vec3 fromcyl(f32 r, f32 theta, f32 z);
// Returns the cartesian vector corresponding to the given spherical coordinates.
vec3 fromsph(f32 r, f32 theta, f32 phi);
// Returns the cartesian vector corresponding to the given cylindrical
// coordinates.
vec3 fromzr(vec2 zr, f64 theta);


// 3D projection, onto the X-Y plane.
#define projxy(xyz) ( choose_fpvec3_(projxy, (xyz)) )
vec2 projxy_vec3(vec3 xyz);
// 3D projection, onto the Z-X plane.
#define projzx(xyz) ( choose_fpvec3_(projzx, (xyz)) )
vec2 projzx_vec3(vec3 xyz);
// 3D projection, onto the Y-Z plane.
#define projyz(xyz) ( choose_fpvec3_(projyz, (xyz)) )
vec2 projyz_vec3(vec3 xyz);

// 3D rejection, along the +Z axis.
vec3 rejxy(vec2 xy, f32 z);
// 3D rejection, along the +Y axis.
vec3 rejzx(vec2 zx, f32 y);
// 3D rejection, along the +X axis.
vec3 rejyz(vec2 yz, f32 x);


// Counter-clockwise rotation of `xy` by `by` radians.
vec2 rot2D(vec2 xy, f32 by);

// Counterclockwise rotation about +Z of `xyz` by `by` radians.
#define rotxy(xyz, by) ( generic(distinguish_vec3((xyz))    \
        , genuinely_vec3: rotxy_vec3                        \
    ) ((xyz), (by)) )
vec3 rotxy_vec3(vec3 xyz, f32 by);
// Counterclockwise rotation about +Y of `xyz` by `by` radians.
#define rotzx(xyz, by) ( generic(distinguish_vec3((xyz))    \
        , genuinely_vec3: rotzx_vec3                        \
    ) ((xyz), (by)) )
vec3 rotzx_vec3(vec3 xyz, f32 by);
// Counterclockwise rotation about +X of `xyz` by `by` radians.
#define rotyz(xyz, by) ( generic(distinguish_vec3((xyz))    \
        , genuinely_vec3: rotyz_vec3                        \
    ) ((xyz), (by)) )
vec3 rotyz_vec3(vec3 xyz, f32 by);


// Angle between two 2D or 3D vectors. In 2D, signed and always about +Z. In 3D,
// "unsigned"/always about `cross(a, b)`.
#define argbeta(a, b) ( choose_fp2D3D_2_(argbeta, (a), (b)) )
f32 argbeta_vec2(vec2 a, vec2 b);
f32 argbeta_vec3(vec3 a, vec3 b);


// Magnitude of `v` parallel to `dir`.
// - `dir` must be normalised.
#define magpara(v, dir) ( choose_fp2D3D_2_(magpara, (v), (dir)) )
f32 magpara_vec2(vec2 v, vec2 dir);
f32 magpara_vec3(vec3 v, vec3 dir);
// Projection of `v` onto `dir`.
// - `dir` must be normalised.
#define projpara(v, dir) ( choose_fp2D3D_2_(projpara, (v), (dir)) )
vec2 projpara_vec2(vec2 v, vec2 dir);
vec3 projpara_vec3(vec3 v, vec3 dir);

// Magnitude of `v` perpendicular to `dir`.
// - `dir` must be normalised.
#define magperp(v, dir) ( choose_fp2D3D_2_(magperp, (v), (dir)) )
f32 magperp_vec2(vec2 v, vec2 dir);
f32 magperp_vec3(vec3 v, vec3 dir);
// Projection of `v` onto the space perpendicular to `dir`.
// - `dir` must be normalised.
#define projperp(v, dir) ( choose_fp2D3D_2_(projperp, (v), (dir)) )
vec2 projperp_vec2(vec2 v, vec2 dir);
vec3 projperp_vec3(vec3 v, vec3 dir);


// Returns 1 if `v` is nearly parallel to `dir`, 0 otherwise.
// - `dir` must be normalised.
#define nearpara(v, dir) ( choose_fp2D3D_2_(nearpara, (v), (dir)) )
i32 nearpara_vec2(vec2 v, vec2 dir);
i32 nearpara_vec3(vec3 v, vec3 dir);
// Returns 1 if `v` is nearly perpendicular to `dir`, 0 otherwise.
// - `dir` must be normalised.
#define nearperp(v, dir) ( choose_fp2D3D_2_(nearperp, (v), (dir)) )
i32 nearperp_vec2(vec2 v, vec2 dir);
i32 nearperp_vec3(vec3 v, vec3 dir);

// Returns 1 if `x` is nearly parallel to +Z axis, 0 otherwise.
#define nearvert(xyz) ( choose_fpvec3_(nearvert, (xyz)) )
i32 nearvert_vec3(vec3 xyz);
// Returns 1 if `x` is nearly perpendicular to +Z axis, 0 otherwise.
#define nearhoriz(xyz) ( choose_fpvec3_(nearhoriz, (xyz)) )
i32 nearhoriz_vec3(vec3 xyz);



// ========================= //
//        QUATERNIONS        //
// ========================= //

// Quaternion, consisting of `quat[0] + quat[1] i + quat[2] j + quat[3] k`. Used
// here to represent arbitrary 3D rotations.
typedef vec4 quat;

// Constructs a `quat` from the given arguments.
// - Overloaded:
//      quat(quat q) -> copy
//      quat() -> identity (no rotation)
//      quat(vec3 axis, f32 angle) -> rotation about `axis` by `by` radians.
#define quat(xs...) ( GLUE2(quat_, countva(xs)) (xs) )

// wait shit this is the same verse i just did this, we shadowed the bloody type
// name with the constructor.
typedef quat also_quat;


// Quaternion of no rotation.
quat quat_id(void);

// Identical quaternion.
quat quat_copy(quat q);

// Represents a rotation about `axis` by `by` radians.
// - `axis` must be normalised.
quat quat_from_axis_angle(vec3 axis, f32 angle);


// Sets `*axis` to the axis that `q` acts about and returns the angle of its
// applied rotation. If `q` is no rotation, returns 0 and sets `*axis` to +Z.
f32 quat_axis_angle(quat q, vec3* axis);


// Normalises `q`. All quaternions representing rotations should be normalised,
// but may numerically drift after many operations, etc.
quat quat_normalise(quat q);

// Return an equivalent rotation to `q`, possibly taking a shorter-than-half-turn
// if `q`'s angle was >pi.
quat quat_shortest(quat q);


// Returns a rotation which would reverse `q`.
quat quat_inverse(quat q);

// Returns a rotation equivalent to first applying `a`, then `b`.
quat quat_compose(quat a, quat b);

// Returns the rotation that, when applied to `ref`, results in `q`.
quat quat_relativeto(quat q, quat ref);

// Returns the shortest rotation to point `start` in the direction of `end`.
// - `start` and `end` must be normalised.
quat quat_fromto(vec3 start, vec3 end);

// Spherical-linear interpolation from `a` to `b`, using factor `t`.
quat quat_slerp(quat a, quat b, f32 t);


// Applies the rotation of `q` to `v`.
vec3 quat_apply(quat q, vec3 v);



// ========================== //
//           DETAIL           //
// ========================== //

#define choose_all_1_(f, a)                 \
    generic(distinguish_vec3(a)             \
        ,            i32: GLUE2(f, _i32)    \
        ,            i64: GLUE2(f, _i64)    \
        ,            u32: GLUE2(f, _u32)    \
        ,            u64: GLUE2(f, _u64)    \
        ,            f32: GLUE2(f, _f32)    \
        ,            f64: GLUE2(f, _f64)    \
        ,           vec2: GLUE2(f, _vec2)   \
        , genuinely_vec3: GLUE2(f, _vec3)   \
        ,           vec4: GLUE2(f, _vec4)   \
    ) (a)
#define choose_all_2_(f, a, b) generic(0                    \
        , int: generic(distinguish_vec3(a + b)              \
            ,            i32: GLUE2(f, _i32)                \
            ,            i64: GLUE2(f, _i64)                \
            ,            u32: GLUE2(f, _u32)                \
            ,            u64: GLUE2(f, _u64)                \
            ,            f32: GLUE2(f, _f32)                \
            ,            f64: GLUE2(f, _f64)                \
            ,           vec2: GLUE2(f, _vec2)               \
            , genuinely_vec3: GLUE2(f, _vec3)               \
            ,           vec4: GLUE2(f, _vec4)               \
        ) (a, b)                                            \
        , default: (const char*[isvec3(a) == isvec3(b)])    \
            {"vec3 IS ONLY COMPATIBLE WITH OTHER vec3"}     \
    )
#define choose_all_3_(f, a, b, c) generic(0                 \
        , int: generic(distinguish_vec3(a + b + c)          \
            ,            i32: GLUE2(f, _i32)                \
            ,            i64: GLUE2(f, _i64)                \
            ,            u32: GLUE2(f, _u32)                \
            ,            u64: GLUE2(f, _u64)                \
            ,            f32: GLUE2(f, _f32)                \
            ,            f64: GLUE2(f, _f64)                \
            ,           vec2: GLUE2(f, _vec2)               \
            , genuinely_vec3: GLUE2(f, _vec3)               \
            ,           vec4: GLUE2(f, _vec4)               \
        ) (a, b, c)                                         \
        , default: (const char*[isvec3(a) == isvec3(b)      \
                             && isvec3(a) == isvec3(c)])    \
            {"vec3 IS ONLY COMPATIBLE WITH OTHER vec3"}     \
    )

#define choose_all_on_god_1_(f, a)          \
    generic(distinguish_vec3(a)             \
        ,             i8: GLUE2(f, _i8)     \
        ,            i16: GLUE2(f, _i16)    \
        ,            i32: GLUE2(f, _i32)    \
        ,            i64: GLUE2(f, _i64)    \
        ,             u8: GLUE2(f, _u8)     \
        ,            u16: GLUE2(f, _u16)    \
        ,            u32: GLUE2(f, _u32)    \
        ,            u64: GLUE2(f, _u64)    \
        ,            f32: GLUE2(f, _f32)    \
        ,            f64: GLUE2(f, _f64)    \
        ,           vec2: GLUE2(f, _vec2)   \
        , genuinely_vec3: GLUE2(f, _vec3)   \
        ,           vec4: GLUE2(f, _vec4)   \
    ) (a)

#define choose_fp_1_(f, a)                  \
    generic(distinguish_vec3(a)             \
        ,            f64: GLUE2(f, _f64)    \
        ,            f32: GLUE2(f, _f32)    \
        ,           vec2: GLUE2(f, _vec2)   \
        , genuinely_vec3: GLUE2(f, _vec3)   \
        ,           vec4: GLUE2(f, _vec4)   \
    ) (a)
#define choose_fp_2_(f, a, b) generic(0                     \
        , int: generic(distinguish_vec3(a + b)              \
            ,            f64: GLUE2(f, _f64)                \
            ,            f32: GLUE2(f, _f32)                \
            ,           vec2: GLUE2(f, _vec2)               \
            , genuinely_vec3: GLUE2(f, _vec3)               \
            ,           vec4: GLUE2(f, _vec4)               \
        ) (a, b)                                            \
        , default: (const char*[isvec3(a) == isvec3(b)])    \
            {"vec3 IS ONLY COMPATIBLE WITH OTHER vec3"}     \
    )
#define choose_fp_3_(f, a, b, c) generic(0                  \
        , int: generic(distinguish_vec3(a + b + c)          \
            ,            f64: GLUE2(f, _f64)                \
            ,            f32: GLUE2(f, _f32)                \
            ,           vec2: GLUE2(f, _vec2)               \
            , genuinely_vec3: GLUE2(f, _vec3)               \
            ,           vec4: GLUE2(f, _vec4)               \
        ) (a, b, c)                                         \
        , default: (const char*[isvec3(a) == isvec3(b)      \
                             && isvec3(a) == isvec3(c)])    \
            {"vec3 IS ONLY COMPATIBLE WITH OTHER vec3"}     \
    )

#define choose_fpscal_1_(f, a)  \
    generic(a                   \
        , f64: GLUE2(f, _f64)   \
        , f32: GLUE2(f, _f32)   \
    ) (a)
#define choose_fpscal_2_(f, a, b)   \
    generic(a + b                   \
        , f64: GLUE2(f, _f64)       \
        , f32: GLUE2(f, _f32)       \
    ) (a, b)
#define choose_fpscal_3_(f, a, b, c)    \
    generic(a + b + c                   \
        , f64: GLUE2(f, _f64)           \
        , f32: GLUE2(f, _f32)           \
    ) (a, b, c)

#if BR_COMPILING
#define choose_fp2D3D_1_(f, a)              \
    generic(distinguish_vec3(a)             \
        ,           vec2: GLUE2(f, _vec2)   \
        , genuinely_vec3: GLUE2(f, _vec3)   \
    ) (a)
#define choose_fp2D3D_2_(f, a, b) generic(0                 \
        , int: generic(distinguish_vec3(a + b)              \
            ,           vec2: GLUE2(f, _vec2)               \
            , genuinely_vec3: GLUE2(f, _vec3)               \
        ) (a, b)                                            \
        , default: (const char*[isvec3(a) == isvec3(b)])    \
            {"vec3 IS ONLY COMPATIBLE WITH OTHER vec3"}     \
    )
#define choose_fp2D3D_3_(f, a, b, c) generic(0              \
        , int: generic(distinguish_vec3(a + b + c)          \
            ,           vec2: GLUE2(f, _vec2)               \
            , genuinely_vec3: GLUE2(f, _vec3)               \
        ) (a, b, c)                                         \
        , default: (const char*[isvec3(a) == isvec3(b)      \
                             && isvec3(a) == isvec3(c)])    \
            {"vec3 IS ONLY COMPATIBLE WITH OTHER vec3"}     \
    )
#else

// NOT THE REAL DEAL.

#define choose_fp2D3D_1_(f, a)  \
    generic((a)                 \
        , vec2: GLUE2(f, _vec2) \
        , vec4: GLUE2(f, _vec3) \
    ) (a)
#define choose_fp2D3D_2_(f, a, b) generic(0                 \
        , int: generic((a + b)                              \
            , vec2: GLUE2(f, _vec2)                         \
            , vec4: GLUE2(f, _vec3)                         \
        ) (a, b)                                            \
        , default: (const char*[isvec3(a) == isvec3(b)])    \
            {"vec3 IS ONLY COMPATIBLE WITH OTHER vec3"}     \
    )
#define choose_fp2D3D_3_(f, a, b, c) generic(0              \
        , int: generic((a + b + c)                          \
            , vec2: GLUE2(f, _vec2)                         \
            , vec4: GLUE2(f, _vec3)                         \
        ) (a, b, c)                                         \
        , default: (const char*[isvec3(a) == isvec3(b)      \
                             && isvec3(a) == isvec3(c)])    \
            {"vec3 IS ONLY COMPATIBLE WITH OTHER vec3"}     \
    )

#endif

#define choose_fpvec3_(f, xs...) generic(0      \
        , int: GLUE2(f, _vec3)(xs)              \
        , default: (const char*[all_vec3(xs)])  \
            {"MUST BE vec3"}                    \
    )



// IMPLS //

#define eq_(a, b) && eq2((a), (b))

#define oneof_(x, val) || eq2(x, (val))

#define quat_0() quat_id()
#define quat_1(q) generic(distinguish_vec3((q)) \
        , vec4: quat_copy                       \
    ) ((q))

#if BR_COMPILING
#define quat_2(axis, angle) generic(distinguish_vec3((axis))    \
        , genuinely_vec3: quat_from_axis_angle                  \
    ) ((axis), (angle))
#else
#define quat_2(axis, angle) generic((axis)  \
        , vec4: quat_from_axis_angle        \
    ) ((axis), (angle))
#endif
