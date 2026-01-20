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

#include <setjmp.h>
#include <stdio.h>
#include <string.h>


#ifdef NULL
  #undef NULL
#endif
#define NULL ((void*)0)

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


// i literally just cant be bother to type it out.
#define rstr restrict

// Alias `_Generic` as `generic`. I can't be bothered explained _Generic (i lack
// the capability).
// - Just remember each possibility must be a complete expression, it's not
//      evaluated like a macro.
#define generic(contents...) ( _Generic(contents) )


// Refers to the type of `x`. If `x` is a type, refers to that same type.
// - `x` must be an expression or type.
// - Expands to something that is essentially a typedefed declarator.
#define typeof(x...) __typeof(x)

// Expands to an expression of type `T`. If `x` is an expression, refers to
// another expression of the same type.
// - Cannot be used in an evaluated context.
// - `x` must be an expression or type.
// - Expands to an expression with an undefined value but of the given type.
#define objof(T...) ( *(typeof(T)*)objof_evaled_() )


// Returns the size of `typeof(x)`, in bytes. yeah, cheeky sizeof overwrite.
// Sizes are signed.
// - `x` must be an expression or type.
// - Always returns >0.
#define sizeof(x...) ( (i64)sizeof(x) )

// Returns the number of elements in the given array.
// - `a` must be an expression which is an array. Note this does not include
//      pointers.
// - If `a` is a string literal, the count includes the null-terminator.
// - Always returns >0.
#define numel(x...) ( sizeof((x)) / sizeof(*(x)) )


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


// Returns 1 if every bit in `mask` is one (set) in `x`, 0 otherwise.
#define isset(x, mask) ( ((x) & (mask)) == (mask) )

// Returns 1 if every bit in `mask` is zero (cleared) in `x`, 0 otherwise.
#define isclr(x, mask) ( ((x) & (mask)) == 0 )


// Expands to positive infinity in the given type.
#define inf(x...) ( generic(typeof(x),  \
        f32: __builtin_inff(),          \
        f64: __builtin_inf())           \
    )
#define INF  ( inf(f64) )
#define fINF ( inf(f32) )

// Returns 1 if `x` is an infinite value, 0 otherwise.
#define isinf(x...) ( (x) == inf(x) || (x) == -inf(x) )
// Returns 1 if `x` is not an infinite value, 0 otherwise.
#define noninf(x...) (!isinf(x))


// Expands to a quiet NaN in the given type.
// - Do not use this with `==` to test for nans (nan always compare unequal), use
//      `isnan` or `nonnan`.
#define nan(x...) ( generic(typeof(x),  \
        f32: __builtin_nanf(""),        \
        f64: __builtin_nan(""))         \
    )
#define NAN  ( nan(f64) )
#define fNAN ( nan(f32) )

// Returns 1 if `x` is NaN, 0 otherwise.
#define isnan(x...) ( (x) != (x) )
// Returns 1 if `x` is not NaN, 0 otherwise.
#define notnan(x...) (!isnan(x))


// Returns 1 if `x` is not infinite and not NaN, 0 otherwise.
#define isgood(x...) (noninf(x) && nonnan(x))


// Function attribute to suppress unused warnings.
#define UNUSED_OK __attribute((__unused__))




/* PRIVATE */

__attribute((__error__("cannot use `objof` in an evaluated context")))
void* objof_evaled_(void);
