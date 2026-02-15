#pragma once

#if !defined(__STDC_VERSION__) || __STDC_VERSION__ < 201112L
  #error requires c11 or newer
#endif

#ifndef __GNUC__
  #error currently gcc is the only supported compiler :/
#endif

#if 0 /* hmmm maybe let them get away with it. */
#ifdef __clang__
  // While clang attempts to emulate gcc, it doesn't do it well enough and the
  // code uses genuinely gcc-specifics.
  #error clang doesnt emulate gcc well enough :/
#endif
#endif

// Setup a macro to split code preview/intellisense vs genuine comiling, since
// preview/intellisense doesn't understand a lot of gcc specifics (i.e.
// __builtin_has_attribute, etc.) which we use. So, when using constructs which
// intellisense only define them correctly if `BR_COMPILING`, otherwise define to
// something with (possibly) incorrect behaviour but which intellisense will
// understand.
#ifndef BR_COMPILING
  #define BR_COMPILING 0
#endif



#include <setjmp.h>
#include <stdio.h>



// ========================== //
//            CORE            //
// ========================== //

#ifdef NULL
  #undef NULL
#endif
#define NULL ((void*)0) /* justin caseme */

typedef signed char       i8;   // 8-bit signed integer (two's complement).
typedef signed short      i16;  // 16-bit signed integer (two's complement).
typedef signed int        i32;  // 32-bit signed integer (two's complement).
typedef signed long long  i64;  // 64-bit signed integer (two's complement).

typedef unsigned char       u8;   // 8-bit unsigned integer.
typedef unsigned short      u16;  // 16-bit unsigned integer.
typedef unsigned int        u32;  // 32-bit unsigned integer.
typedef unsigned long long  u64;  // 64-bit unsigned integer.

typedef float   f32;  // 32-bit floating-point number (IEEE-754).
typedef double  f64;  // 64-bit floating-point number (IEEE-754).

// `char` used to indicate a character, but is guaranteed signed and therefore
// equivalent to `i8`.

// All pointers are 64-bit, and may be cast to `u64` (or `i64`).

// 8B aligned ordered collection of two `f32`s.
typedef f32 vec2 __attribute((__vector_size__(8)));
// 16B aligned ordered collection of four `f32`s.
typedef f32 vec4 __attribute((__vector_size__(16)));

// Just need some redundant attribute which we can detect to distinguish vec3/4.
#define VEC3_ATTR __warn_if_not_aligned__(16)

// No genuine three element vector since it isn't a register size, instead we
// just use a vec4 and ignore the last element. Yeah this makes dudspace in
// arrays and some redundant operations but the assured 16B alignment+register
// use is (probs) worth it.
// - `generic` WILL NOT DISTINGUISH between `vec3` and `vec4` (see
//      `distinguish_vec3` to accomodate this).
// - `sametype` WILL CORRECTLY DISTINGUISH between `vec3` and `vec4`.
typedef vec4 vec3 __attribute((VEC3_ATTR));


// Not considered "vectors" (since they aren't floating point) but still useful
// (especially for storing element-wise comparisons of vectors):

typedef i32 i32x2 __attribute((__vector_size__(8)));
typedef i32 i32x4 __attribute((__vector_size__(16)));



// ========================= //
//          ALIASES          //
// ========================= //

#define memcpy(d,s,n)   __builtin_memcpy((d),(s),(n))
#define memmove(d,s,n)  __builtin_memmove((d),(s),(n))
#define memset(d,c,n)   __builtin_memset((d),(c),(n))
#define strlen(s)       __builtin_strlen((s))

// i literally just cant be bother to type it out.
#define rstr restrict

// Alias `_Generic` as `generic`. I can't be bothered explained _Generic (i lack
// the capability).
// - Just remember each possibility must be a complete expression, it's not
//      evaluated like a macro.
#define generic(contents...) ( _Generic(contents) )

// coming soon in C23 (releasing 2024).
#define static_assert(x...) _Static_assert((x), "just read the code smile")

// Compile-time ternary expression
#define choose_expr(cond, if_, else_) \
        ( __builtin_choose_expr((cond), (if_), (else_)) )



// ========================== //
//       VECTOR HELPERS       //
// ========================== //

// Returns 1 if the given type or expression is a floating point vector, 0
// otherwise.
#define isvec(T...) ( __builtin_types_compatible_p(typeof(T), vec2) \
        || __builtin_types_compatible_p(typeof(T), vec4) )

// =`sametype(T, vec2)`
#define isvec2(T...) ( sametype2(typeof(T), vec2) )
// =`sametype(T, vec3)` (when compiling, in code preview not quite but dw).
#define isvec3(T...) ( isvec3_(typeof(T)) )
// =`sametype(T, vec4)` (when compiling, in code preview not quite but dw).
#define isvec4(T...) ( isvec4_(typeof(T)) )


// Constructs a `vec2` from the given arguments. If given only 1 argument, a
// vector with every element set to that is created.
// - Accepts 1 or 2 arguments (1 argument is equivalent to the same argument
//      repeated 2x).
#define vec2(xs...) ( GLUE2(vec2_, countva(xs)) (xs) )

// Constructs a `vec3` from the given arguments. If given only 1 argument, a
// vector with every element set to that is created.
// - Accepts 1 or 3 arguments (1 argument is equivalent to the same argument
//      repeated 3x).
// - The fourth/ignored element of the vec3 is initialised to 1.
#define vec3(xs...) ( GLUE2(vec3_, countva(xs)) (xs) )

// Constructs a `vec2` from the given arguments. If given only 1 argument, a
// vector with every element set to that is created.
// - Accepts 1 or 4 arguments (1 argument is equivalent to the same argument
//      repeated 4x).
#define vec4(xs...) ( GLUE2(vec4_, countva(xs)) (xs) )

// Note the cheeky vector constructors shadow the type name themselves, but will
// only overwrite when invoked with brackets.... so like maybe on function
// pointer prototypes? in that case use these helpful aliases:

typedef vec2 also_vec2;
typedef vec3 also_vec3;
typedef vec4 also_vec4;


// Helper type, an object of which may be returned by `distinguish_vec3`.
typedef struct genuinely_vec3 { char _; } genuinely_vec3;

// Returns an object of the given type, unless the given type is `vec3`, in which
// case returns an object of type `genuinely_vec3`. This may be used as the
// switch-expr of `generic`s to allow for explicit cases for `vec3` vs `vec4`
// (using case labels of `genuinely_vec3: ..., vec4: ...`).
#define distinguish_vec3(T...) ( choose_expr(sametype(T, vec3)  \
        , objof(genuinely_vec3)                                 \
        , objof(T)                                              \
    ) )


// Expands to the token corresponding to the type of `xs` (note all elements of
// `xs` must be the same type).
// - Any of the tokens may be omitted to exclude that case.
// - All of `xs` must be floating point expressions or types, of the same type.
#define fp_selectx(f_f32, f_f64, f_vec2, f_vec3, f_vec4, xs...) \
    ( fp_selectx_(f_f32, f_f64, f_vec2, f_vec3, f_vec4, xs) )

// Wrapper around `fp_selectx` for tokens `<f>_<type>_` for all floating point
// types.
#define fp_select(f, xs...) ( fp_selectx(   \
        GLUE2(f, _f32_),                    \
        GLUE2(f, _f64_),                    \
        GLUE2(f, _vec2_),                   \
        GLUE2(f, _vec3_),                   \
        GLUE2(f, _vec4_),                   \
        xs                                  \
    ) )

// Wrapper around `fp_selectx` for tokens `<f>_<type>_` for all non-vector
// floating point types.
#define fp_select_non_vec(f, xs...) ( fp_selectx(   \
        GLUE2(f, _f32_),                            \
        GLUE2(f, _f64_),                            \
        /* no case for vec2 */,                     \
        /* no case for vec3 */,                     \
        /* no case for vec4 */,                     \
        xs                                          \
    ) )

// Wrapper around `fp_selectx` for tokens `<f>_<type>_` for all vector floating
// point types.
#define fp_select_only_vec(f, xs...) ( fp_selectx(  \
        /* no case for f32 */,                      \
        /* no case for f64 */,                      \
        GLUE2(f, _vec2_),                           \
        GLUE2(f, _vec3_),                           \
        GLUE2(f, _vec4_),                           \
        xs                                          \
    ) )



// ========================== //
//           LIMITS           //
// ========================== //


// Quick two's complement signed integer rundown:
//       N bits
//       |----|
//  hi   s..cba   lo
//       ^  ||'- bit 0
//       |  |'- bit 1
//       |  '- bit 2
//       '- bit N-1, sometimes called the sign bit
//
// The value of the number is:
//     sum [i=0..N-2](2^i * bit[i]) - 2^(N-1) * bit[N-1]
// Aka, the value of each place is the same as a typical unsigned except the most
// significant bit has "negative weight" (it is subtracted instead of added). The
// number can only be negative if this bit is set. Note that zero is still only
// represented by all bits cleared.


// Expands to the smallest value that `typeof(T)` can hold.
// - `T` must be an integer expression or type.
// - Expands to a constant-expression, which is a negative number for signed
//      types and 0 for unsigned types.
#define intmin(T...) ( (typeof(T))generic(objof(T)  \
        ,  i8: -0x80                                \
        , i16: -0x8000                              \
        , i32: -0x80000000                          \
        , i64: -0x8000000000000000                  \
        ,  u8: 0                                    \
        , u16: 0                                    \
        , u32: 0                                    \
        , u64: 0                                    \
    ) )

// Expands to the largest value that `typeof(T)` can hold.
// - `T` must be an integer expression or type.
// - Expands to a constant-expression, which is a positive number for signed and
//      unsigned types.
#define intmax(T...) ( (typeof(T))generic(objof(T)  \
        ,  i8: 0x7F                                 \
        , i16: 0x7FFF                               \
        , i32: 0x7FFFFFFF                           \
        , i64: 0x7FFFFFFFFFFFFFFF                   \
        ,  u8: 0xFFU                                \
        , u16: 0xFFFFU                              \
        , u32: 0xFFFFFFFFU                          \
        , u64: 0xFFFFFFFFFFFFFFFFU                  \
    ) )

// Expands to the value of `typeof(T)` that has all bits set.
// - `T` must be an integer expression or type.
// - Expands to a constant-expression, which is equal to -1 for signed types and
//      a positive number for unsigned types.
#define intones(T...) ( (typeof(T))generic(objof(T) \
        ,  i8: -1                                   \
        , i16: -1                                   \
        , i32: -1                                   \
        , i64: -1                                   \
        ,  u8: 0xFFU                                \
        , u16: 0xFFFFU                              \
        , u32: 0xFFFFFFFFU                          \
        , u64: 0xFFFFFFFFFFFFFFFFU                  \
    ) )



// Quick IEEE-754 floating point rundown:
//        32 or 64 bits
//       |-------------|
//  hi   s ee..ee mm..mm   lo
//       ^ |----| |----|
//       | |      '- mantissa, m_len bits
//       | '- exponent, e_len bits
//       '- sign, 1 bit
//
// 5 possible "classes" of the number:
//
//  Zero, represents nothing and no one.
//        s 00..00 00..00
//      = (-1)^s * 0  (where -0 and 0 are distinct)
//
//  Infinity (inf), represents an infinite amount.
//        s 11..11 00..00
//      = (-1)^s * infinity
//
//  Not-a-number (nan), represents undefined/indetermine/invalid numbers.
//        s 11..11 qm..mm,  where:  (q != 0) || (m != 0)
//      = no value.
//      `s` is typically ignored.
//      `q` indicates a "quiet nan" (shouldn't throw exceptions) if set.
//
//  Subnormal, represents numbers very close to zero.
//        s 00..00 mm..mm,  where:  (m != 0)
//      = (-1)^s * 2^(2 - 2^(e_len - 1)) * (m / 2^m_len)
//
//  Normal, represents all real numbers that aren't zero or subnormal.
//        s ee..ee mm..mm,  where:  (e != 0)
//      = (-1)^s * 2^(e + 1 - 2^(e_len - 1)) * (1 + m / 2^m_len)
//      the `1 +` in the mantissa is often interpreted as an "implied" bit being
//          set just above the most significant bit of the mantissa.


// Expands to positive infinity in the given type.
// - `T` must be a floating point expression or type.
#define inf(T...) ( fp_selectx(fINF, INF, v2INF, v3INF, v4INF, objof(T)) )
#define INF   ( __builtin_inff() )       // f64 inf
#define fINF  ( __builtin_inf() )        // f32 inf
#define v2INF ( vec2(__builtin_inff()) ) // vec2 inf
#define v3INF ( vec3(__builtin_inff()) ) // vec3 inf
#define v4INF ( vec4(__builtin_inff()) ) // vec4 inf

// Returns 1 if `x` is infinite, 0 otherwise.
// - `x` must be a floating point expression.
// - For vector types, any inf element causes a 1 return.
#define isinf(x...) ( anyeq(abs(x), inf(x)) )

// Returns 1 if all elements in `x` are infinite, 0 otherwise.
// - `x` must be a floating point vector expression.
#define isallinf(x...) ( eq(abs(x), inf(x)) )

// Returns 1 if `x` is not an infinite value, 0 otherwise.
// - `x` must be a floating point expression.
// - For vector types, any inf element causes a 0 return.
#define notinf(x...) ( !isinf(x) )


// Expands to a quiet NaN in the given type.
// - `T` must be a floating point expression or type.
// - Do not use this with `==` to test for nans (nan always compare unequal), use
//      `isnan` or `nonnan`.
#define nan(T...) ( fp_selectx(fNAN, NAN, v2NAN, v3NAN, v4NAN, objof(T)) )
#define NAN   ( __builtin_nanf("") )       // f64 NaN
#define fNAN  ( __builtin_nan("") )        // f32 NaN
#define v2NAN ( vec2(__builtin_nanf("")) ) // vec2 NaN
#define v3NAN ( vec3(__builtin_nanf("")) ) // vec3 NaN
#define v4NAN ( vec4(__builtin_nanf("")) ) // vec4 NaN

// Returns 1 if `x` is NaN, 0 otherwise.
// - `x` must be a floating point expression.
// - For vector types, any nan element causes a 1 return.
#define isnan(x...) ( !eq2(x, x) )

// Returns 1 if all elements in `x` are NaN, 0 otherwise.
// - `x` must be a floating point vector expression.
#define isallnan(x...) ( !anyeq(x, x) )

// Returns 1 if `x` is not NaN, 0 otherwise.
// - `x` must be a floating point expression.
// - For vector types, any nan element causes a 0 return.
#define notnan(x...) ( !isnan(x) )


// Returns 1 if `x` is not infinite and not NaN, 0 otherwise.
// - For vector types, any non-good element causes a 0 return.
#define isgood(x...) ( notinf(x) && notnan(x) )


// Returns the bits of `f`, as a unsigned integer of the same size.
// - `f` must be an `f32` or `f64` expression.
// - The memory of `f` is reinterpreted, no conversion takes place.
#define fp_bits(f...) ( fp_select_non_vec(fp_bits, f)(f) )

// Returns the floating point that has the same size and bits as `u`.
// - `u` must be a `u32` or `u64` expression.
// - The memory of `u` is reinterpreted, no conversion takes place.
#define fp_from_bits(u...) ( fp_select_non_vec(fp_from_bits, u)(u) )


// Expands to the greatest magnitude representable by a normal floating point
// of the given type.
// - `T` must be an `f32` or `f64` expression or type.
#define fp_norm_max(T...) \
    ( fp_selectx_non_vec(3.40282347e38f, 1.7976931348623157e308, T) )

// Expands to the smallest magnitude representable by a normal floating point
// of the given type.
// - `T` must be an `f32` or `f64` expression or type.
#define fp_norm_min(T...) \
    ( fp_selectx_non_vec(1.17549435e-38f, 2.2250738585072014e-308, T) )

// Expands to the greatest magnitude representable by a subnormal floating point
// of the given type.
// - `T` must be an `f32` or `f64` expression or type.
#define fp_subnorm_max(T...) \
    ( fp_selectx_non_vec(1.17549421e-38f, 2.2250738585072009e-308, T))

// Expands to the smallest magnitude representable by a subnormal floating point
// of the given type.
// - `T` must be an `f32` or `f64` expression or type.
#define fp_subnorm_min(T...) \
    ( fp_selectx_non_vec(1.40129846e-45f, 4.9406564584124654e-324, T) )


// Expands to an unsigned integer of the same size as `typeof(T)` with only the
// "exponent" bits set. This can be used to mask the exponent, however beware
// that does not sit in the lo bits and will still be biased.
// - `T` must be an `f32` or `f64` expression or type.
#define fp_exp_mask(T...) \
    ( fp_selectx_non_vec((u32)0x7F800000U, (u64)0x7FF0000000000000U, T) )

// Expands to the total number of bits in the exponent of `typeof(T)`.
// - `T` must be an `f32` or `f64` expression or type.
#define fp_exp_len(T...) ( fp_selectx_non_vec(8, 11, T) )

// Expands to the value used to bias the exponent of `typeof(T)` before it is put
// into bits. For example, to store the the exponent `-2` you would place the low
// `fp_exp_len(T)` bits of the expression `-2 + fp_exp_bias(T)` into the exponent
// bits. Then to retrieve the genuine exponents from its bits, you would use
// `bits - fp_exp_bias(T)`.
// - `T` must be an `f32` or `f64` expression or type.
#define fp_exp_bias(T...) ( fp_selectx_non_vec(127, 1023, T) )


// Expands to an unsigned integer of the same size as `typeof(T)` with only the
// "mantissa" bits set. This can be used to mask the mantissa, however note that
// it may be desirable to set the `fp_mant_implied` bit (if the exponent is non-
// zero).
// - `T` must be an `f32` or `f64` expression or type.
#define fp_mant_mask(T...) \
    ( fp_selectx_non_vec((u32)0x007FFFFFU, (u64)0x000FFFFFFFFFFFFFU, T) )

// Expands to the total number of bits in the mantissa of `typeof(T)`.
// - `T` must be an `f32` or `f64` expression or type.
#define fp_mant_len(T...) ( fp_selectx_non_vec(23, 52, T) )

// Expands to an unsigned integer of the same size as `typeof(T)` with only the
// implied mantissa bit set. This bit is "implied" to be set in the mantissa of
// a normal floating point, but is not explicitly stored. Note it overlaps the
// least significant bit of the exponent. To get the genuine mantissa of a
// number, get the mantissa bits and use `bits | fp_mant_implied(T)`.
// - `T` must be an `f32` or `f64` expression or type.
#define fp_mant_implied(T...) \
    ( fp_selectx_non_vec((u32)0x00800000U, (u64)0x0010000000000000U, T) )



// ========================== //
//         TYPE MANIP         //
// ========================== //

// Refers to the type of `x`. If `x` is a type, refers to that same type.
// - `x` must be an expression or type.
// - Expands to something that is essentially a typedefed declarator.
#define typeof(x...) __typeof(x)

// Expands to an expression of type `T`. If `T` is an expression, refers to
// another expression of the same type.
// - Cannot be used in an evaluated context.
// - `T` must be an expression or type.
// - Expands to an expression with an undefined value but of the given type.
#define objof(T...) ( *(typeof(T)*)objof_evaled_() )


// Returns 1 if `typeof(A)` and `typeof(B)` are the same unqualified type, 0
// otherwise. Additionally, has logic to correctly distinguish `vec3` and `vec4`.
// - To compare types without ignoring qualifiers, make both types pointed to.
// - "Unqualified" means without modifiers such as const, volatile, and restrict,
//      however it will also strip array sizes (i.e. `i32[2]` is the "same" as
//      `i32[1]` and `i32[]` and etc.).
// - `A` and `B` must be expressions or types.
// - Correctly distinguishes `vec3` and `vec4`.
#define sametype(Ts...) ( 1 FOREACH(sametype_, FIRST(Ts), Ts) )

// Two-argument version of `sametype` (slightly more performant/doesn't depend on
// `FOREACH`).
#define sametype2(A, B) ( sametype2_(typeof(A), typeof(B)) )


// Returns the size of `typeof(x)`, in bytes. yeah, cheeky sizeof overwrite.
// Sizes are signed.
// - `x` must be an expression or type.
// - Always returns >0.
#define sizeof(x...) ( (i64)sizeof(x) )

// Returns the alignment of `typeof(x)`, in bytes. (also signed :) )
// - `x` must be an expression or type.
// - Always returns a power-of-2.
#define alignof(x...) ( (i64)__alignof(x) )

// Returns the byte-offset of the given `member` within the type `typeof(T)`.
// - `T` must be an expression or type.
// - `member` must refer to a member of `typeof(T)`, consisting of:
//        identifier
//      | member "." identifier
//      | member "[" expr "]".
#define offsetof(T, member) ( (i64)__builtin_offsetof(typeof(T), member) )

// Returns the byte-offset of the first byte after the given `member` within the
// type `typeof(T)`. Note this may not be the same as the offset of the next
// member, due to potential padding to fulfill alignment.
// - `T` must be an expression or type.
// - `member` must refer to a member of `typeof(T)`, consisting of:
//        identifier
//      | member "." identifier
//      | member "[" expr "]".
#define offsetofend(T, member) ( offsetof(T, member) + sizeof(objof(T).member) )


// Returns the number of elements in the given array.
// - `a` must be an expression which is an array. Note this does not include
//      pointers.
// - If `a` is a string literal, the count includes the null-terminator.
// - Always returns >0.
#define numel(x...) ( sizeof((x)) / sizeof(*(x)) )



// ========================== //
//        NUMBER MANIP        //
// ========================== //

// Returns 1 if all elements in the given expressions are element-wise equal, 0
// otherwise. This is needed since the builtin vector comparison operators act
// element-wise.
// - All of `xs` must be floating point expressions of the same type.
#define eq(xs...) ( 1 FOREACH(eq_, FIRST(xs), xs) )

// Returns 1 if any elements in `a` and `b` are element-wise equal, 0 otherwise.
// This is needed since the builtin vector comparison operators act element-wise.
// - `a` and `b` must be floating point expressions of the same type.
#define anyeq(a, b) ( anyeq_((a), (b)) )

// Two-argument version of `eq` (slightly more performant/doesn't depend on
// `FOREACH`).
#define eq2(a, b) ( eq2_((a), (b)) )

// =`!eq(a, b)`
#define neq(a, b) ( !eq2(a, b) )

// =`!anyeq(a, b)`
#define allneq(a, b) ( !anyeq(a, b) )


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


// Returns 1 if `x` is within [`lo`, `hi`], inclusive of each, 0 otherwise.
// - Does not support floating point vectors.
#define within(x, lo, hi) ((lo) <= (x) && (x) <= (hi))

// Returns 1 if `x` is equal to any of the values in `vals`, 0 otherwise.
// - If given no arguments, returns 0.
// - Supports floating point vectors.
#define oneof(x, vals...) ( 0 FOREACH(oneof_eq_, (x), vals) )


// Returns the absolute value of the given number. For vector types, this is
// element-wise.
#define abs(x...) ( abs_(x) )


// Returns the minimum of the given numbers (or the only non-nan). For vector
// types, this is element-wise.
#define min(a, b) ( min_((a), (b)) )

// Returns the maximum of the given numbers (or the only non-nan). For vector
// types, this is element-wise.
#define max(a, b) ( max_((a), (b)) )



// ========================= //
//          EXPECTS          //
// ========================= //

// Expects `x` to be non-zero most of the time, but it is not illegal for `x` to
// be zero. This aids the compiler when determing which code paths are hot/cold
// and what to optimise.
// - Must be used as the only expression in an `if` statement, e.g.:
//      `if (likely(in == 1.0)) /* something */;`
#define likely(x...) ( __builtin_expect_with_probability(!!(x), 1, 0.95) )

// Expects `x` to be zero most of the time, but it is not illegal for `x` to be
// non-zero. This aids the compiler when determing which code paths are hot/cold
// and what to optimise.
// - Must be used as the only expression in an `if` statement, e.g.:
//      `if (unlikely(arg < 0.0)) /* something */;`
#define unlikely(x...) ( __builtin_expect_with_probability(!!(x), 0, 0.95) )



// ========================= //
//       COUNTING ARGS       //
// ========================= //

// Expands to `1` if any arguments are given, otherwise expands to `0`.
// - Must give fewer than 1000 arguments.
#define anyva(args...) anyva_(args)

// Expands to `1` if two or more arguments are given, otherwise expands to `0`.
// - Must give fewer than 1000 arguments.
#define multipleva(args...) multipleva_(args)

// Expands to a bare integer of the number of arguments given.
// - Must give fewer than 1000 arguments.
// - `countva()` expands to `0`.
#define countva(args...) countva_(args)



// ========================= //
//       BOOLEAN MANIP       //
// ========================= //

// Returns 1 if all values in `xs` are non-zero, 0 otherwise.
// - If no arguments are given, returns 1.
#define all(xs...) ( all_(xs) )

// Returns 1 if any value in `xs` is non-zero, 0 otherwise.
// - If no arguments are given, returns 0.
#define any(xs...) ( any_(xs) )

// Returns 1 if all values in `xs` are zero, 0 otherwise.
// - If no arguments are given, returns 1.
#define none(xs...) ( !any_(xs) )

// Returns 1 if exactly one of the values in `xs` is non-zero, 0 otherwise.
// - If no arguments are given, returns 0.
#define just_one(xs...) ( just_one_(xs) )



// ========================== //
//            MISC            //
// ========================== //


// Swaps the given two lvalues, using a temporary. Both `a` and `b` must be the
// same unqualified type, and neither should be const.
// - `a` and `b` must be lvalues.
// - (potentially) Stack-allocates a variable of type `typeof(a)`.
#define swap(a, b) do { swap_((a), (b)) } while (0)


// "Touches" the given variable, doing nothing except forcing the compiler to
// assume it was read from and written to.
// - `var` must be an lvalue.
// - Useful to disable certain optimisations and control code output.
#define touchvar(var) do { __asm__ volatile ("" : "+rmx"((var))); } while (0)


// When used within a `switch` as the statement just before a fallthrough to a
// different label, suppresses the warning of implicit fallthrough.
// - This is the preferred method to indicate fallthrough. Other methods
//      (including comments) should still give warnings, but gcc seems to have a
//      soft spot for that one so. consider yourself lucky.
#define fallthrough() do { __attribute((__fallthrough__)); } while (0)



// ========================== //
//           MACROS           //
// ========================== //

// Expands to a string literal of the given arguments. Note that when given
// multiple arguments, they will be seperated by commas in the string.
#define STRINGIFY(xs...) STRINGIFY_(x)

// Glues every argument given, via `a##b##c##...`.
#define GLUE(xs...) REDUCE(INVOKE, GLUE2, xs)

// Glues the two given symbols, via `a##b`.
#define GLUE2(a, b) GLUE2_(a, b)

// Identity function, can be used to enforce macro expansion or remove brackets.
#define IDENTITY(arg...) arg

// Nothin? https://youtu.be/BG7273yDpdA?t=11
#define NOTHIN(ignored...)

// Comma,
#define COMMA(ignored...) , // comma (',')
// , = COMMA



// ========================== //
//       MACRO NUMERALS       //
// ========================== //

// Some of these macros take or return numerical inputs, which will be referred
// to as "macro numerals". These numerals must:
// - consist of a single token, or expand to single token. This means no
//      extraneous tokens such as brackets.
// - be a number in 0..999.
// - contain no leading zero digits.
// Note that some macro numeral input/outputs have additional range restrictions.
// Examples of macro numerals: `0`, `1`, `100`, `999`.
// Not examples of macro numerals: `(0)`, `-1`, `010`, `1000`.

// Essentially `n + 1`.
// - `n` must be a macro numeral in 0..998.
// - Expands to a macro numeral in 1..999.
#define MN_INC(n) GLUE2(MN_INC_, n)

// Essentially `n - 1`.
// - `n` must be a macro numeral in 1..999.
// - Expands to a macro numeral in 0..998.
#define MN_DEC(n) GLUE2(MN_DEC_, n)

// Essentially `a + b`.
// - `a` must be a macro numeral in 0..`999 - b`.
// - `b` must be a macro numeral in 0..`999 - a`.
// - Expands to a macro numeral.
#define MN_ADD(a, b) ITERATED(INVOKE, MN_INC, b, a)

// Essentially `a - b`.
// - `a` must be a macro numeral in `b`..999.
// - `b` must be a macro numeral in 0..`a`.
// - Expands to a macro numeral.
#define MN_SUB(a, b) ITERATED(INVOKE, MN_DEC, b, a)

// Essentially `!n` or `n == 0`.
// - `n` must be a macro numeral.
// - Expands to a macro numeral in 0..1.
#define MN_NOT(n) GLUE2(MN_NOT_, n)

// Essentially `a && b`.
// - `a` and `b` must be macro numerals.
// - Expands to a macro numeral in 0..1.
#define MN_AND(a, b) IF_ELSE(a, IF_ELSE(b, 1, 0), 0)

// Essentially `a || b`.
// - `a` and `b` must be macro numerals.
// - Expands to a macro numeral in 0..1.
#define MN_OR(a, b) IF_ELSE(a, 1, IF_ELSE(b, 1, 0))

// Essentially `a == b`.
// - `a` and `b` must be macro numerals.
// - Expands to a macro numeral in 0..1.
#define MN_EQ(a, b) MN_AND(MN_NOT(MN_GT(a, b)), MN_NOT(MN_LT(a, b)))

// Essentially `a != b`.
// - `a` and `b` must be macro numerals.
// - Expands to a macro numeral in 0..1.
#define MN_NEQ(a, b) MN_OR(MN_GT(a, b), MN_LT(a, b))

// Essentially `a > b`.
// - `a` and `b` must be macro numerals.
// - Expands to a macro numeral in 0..1.
#define MN_GT(a, b) MN_GT_(a, b)

// Essentially `a >= b`.
// - `a` and `b` must be macro numerals.
// - Expands to a macro numeral in 0..1.
#define MN_GTE(a, b) MN_NOT(MN_LT(a, b))

// Essentially `a < b`.
// - `a` and `b` must be macro numerals.
// - Expands to a macro numeral in 0..1.
#define MN_LT(a, b) MN_GT(b, a)

// Essentially `a <= b`.
// - `a` and `b` must be macro numerals.
// - Expands to a macro numeral in 0..1.
#define MN_LTE(a, b) MN_NOT(MN_GT(a, b))

// Expands to the greater of `a` and `b`.
// - `a` and `b` must be macro numerals.
// - Expands to a macro numeral.
#define MN_MAX(a, b) IF_ELSE(MN_GT(a, b), a, b)

// Expands to the lesser of `a` and `b`.
// - `a` and `b` must be macro numerals.
// - Expands to a macro numeral.
#define MN_MIN(a, b) IF_ELSE(MN_LT(a, b), a, b)



// ========================== //
//     MACRO CONDITIONALS     //
// ========================== //

// Expands to `x` if `!!n`, otherwise expands to nothing.
// - `n` must be a macro numeral.
#define IF(n, x...) GLUE2(IF_, MN_NOT(n)) (x)

// Expands to `x` if `!n`, otherwise expands to nothing.
// - `n` must be a macro numeral.
#define IF_NOT(n, x...) GLUE2(IF_NOT_, MN_NOT(n)) (x)

// Expands to `else_` if `!!n`, otherwise expands to `if_`.
// - `n` must be a macro numeral.
#define IF_ELSE(n, if_, else_) GLUE2(IF_ELSE_, MN_NOT(n)) (if_, else_)



// ========================= //
//      MACRO UTILITIES      //
// ========================= //

// Invokes `m` with `x`, as `m(x)`. Note `x` may contain commas.
// - Helpful for macros such as `FOREACH`, which require an input macro which
//      accepts a user argument as the first parameter. If you do not have this
//      macro already available, you may use this to essentially create it.
// - For example, if we want to sum every argument:
//      Without this macro:
//          `#define ADD_WITH_USER(_, a, b) MN_ADD(a, b)`
//          `sum = REDUCE(ADD_WITH_USER, _, ...)`
//      with this macro:
//          `sum = REDUCE(INVOKE, MN_ADD, ...)`
#define INVOKE(m, x...) m(x)


// Expands to `x` repeated `n` times. Note each occurence of `x` is only
// separated by whitespace.
// - `n` must be a macro numeral.
// - The expansion is not comma-separated. To make it comma-separated, just give
//      a comma at the end of the arguments (i.e. `REPEAT(5, 0, )`).
#define REPEAT(n, x...) REPEAT_(n, x)

// Expands to `m(user,x0) m(user,x1) ...` for each argument in `xs`.
// - Must give fewer than 1000 arguments.
// - The expansion is not comma-separated. To make it comma-separated, modify `m`
//      to expand with a comma at the end of it.
#define FOREACH(m, user, xs...) FOREACH_(m, user, xs)

// Expands to `m(user, x0, m(user, x1, ... m(user, xn-1, xn)))` for each
// argument in `xs`.
// - Must give fewer than 1000 arguments.
// - If only one argument is given, expands to it without invoking `m`.
// - If no arguments are given, expands to nothing.
#define REDUCE(m, user, xs...) REDUCE_(m, user, xs)

// Expands to `m(user, m(user, ... m(user, x)))`, repeatedly invoking `m` `n`
// times. Note `x` may contain commas.
// - `n` must be a macro numeral.
// - If `n` is 0, expands to `x` without invoking `m`.
#define ITERATED(m, user, n, x...) ITERATED_(m, user, n, x)


// Expands to `xs` in reverse order.
// - Must give fewer than 1000 arguments.
#define REVERSE(xs...) REVERSE_(xs)

// Expands to the first argument given.
// - Expands to nothing if given no arguments.
#define FIRST(xs...) FIRST_(xs, _)

// Expands to everything but the first argument given.
// - Must give fewer than 1000 arguments.
// - Expands to nothing if given fewer than two arguments.
#define REST(xs...) REST_(xs)



// ========================== //
//         ATTRIBUTES         //
// ========================== //


// Will warn if the return value from calling this function is unused.
// - Function attribute.
// - To suppress this warning, try something like `(void)-func(...)`. This is
//      because a gcc bug (feature?) prevents the normal cast to void from
//      suppressing the no discard warning.
#define NODISCARD __attribute((__warn_unused_result__))


// Indicates that this function can never return, meaning it either infinitely
// loops, terminates the program, or crashes :).
// - Function attribute.
#define NORETURN __attribute((__noreturn__))


// Silences warnings about this object being unused.
// - Function, variable, or type attribute.
#define UNUSED __attribute((__unused__))

// Forces this function to be emitted into the final program.
// - Global/static function attribute.
// - May be useful to view the assembly of a funcion.
#define MUSTEMIT __attribute((__dllexport__, __used__, __externally_visible__))


// Allows this object or type to alias other types when used through a pointer,
// without breaking strict-aliasing. Note that this qualifier on a type which
// isn't being pointed to does nothing.
// - Type attribute.
// - Apply to a type, then access via a pointer to that type to alias (this
//      attribute doesn't go on the pointer itself).
#define MAYALIAS __attribute((__may_alias__))



// ========================= //
//           BREAD           //
// ========================= //

// Incredibly important. Program will not compile if removed. Just overall vital.
#define BREAD "\xF0\x9F\x8D\x9E"






// ========================== //
//           DETAIL           //
// ========================== //

// wtf man
#ifndef BREAD
  #error wtf man
#endif

// Ultra long macros defined in here. Big ass file. Also put before the other one
// bc some of those depend on these.
#include "br.ii"

// Most implementations in here.
#include "br.i"
