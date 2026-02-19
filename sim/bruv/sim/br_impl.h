#include "br.h"



// =========================================================================== //
// = GUARANTEES ============================================================== //
// =========================================================================== //

#if defined(__CHAR_UNSIGNED__) && __CHAR_UNSIGNED__
  // Char must be signed because it's nice to be able to assume signed/unsigned
  // and because of a bug in the gcc simd implementation (which doesn't matter
  // anymore but it doesn't exactly fill you with hope).
  #error `char` must be signed.
#endif

static_assert(__CHAR_BIT__ == 8);
// maaaaaaan

static_assert(sizeof(i8) == 1);
static_assert(sizeof(i16) == 2);
static_assert(sizeof(i32) == 4);
static_assert(sizeof(i64) == 8);

static_assert(sizeof(u8) == 1);
static_assert(sizeof(u16) == 2);
static_assert(sizeof(u32) == 4);
static_assert(sizeof(u64) == 8);

static_assert(sizeof(f32) == 4);
static_assert(sizeof(f64) == 8);

static_assert(sizeof(void*) == 8);
static_assert(sizeof(void(*)(void)) == 8);

// no guarantees about `[unsigned] long`.


#define br_assert_align_is_size(T) static_assert(alignof(T) == sizeof(T))

br_assert_align_is_size(i8);
br_assert_align_is_size(i16);
br_assert_align_is_size(i32);
br_assert_align_is_size(i64);

br_assert_align_is_size(u8);
br_assert_align_is_size(u16);
br_assert_align_is_size(u32);
br_assert_align_is_size(u64);

br_assert_align_is_size(f32);
br_assert_align_is_size(f64);

br_assert_align_is_size(void*);
br_assert_align_is_size(void(*)(void));

#undef br_assert_align_is_size


// Check two's complement.
static_assert(-1 == ~0);

#if !(defined(__FLT_IS_IEC_60559__) && __FLT_IS_IEC_60559__)
  #error must have IEEE-754 binary32 as float.
#endif

#if !(defined(__DBL_IS_IEC_60559__) && __DBL_IS_IEC_60559__)
  #error must have IEEE-754 binary64 as double.
#endif



// =========================================================================== //
// = IMPL ==================================================================== //
// =========================================================================== //


// VECTOR HELPERS //

#define isvec_(T...) __builtin_types_compatible_p(T, vec2) \
                  || __builtin_types_compatible_p(T, vec4)
#define isvec2_(T...) __builtin_types_compatible_p(T, vec2)
#if BR_COMPILING
  #define isvec3_(T...) __builtin_has_attribute(T, VEC3_ATTR)
  #define isvec4_(T...) __builtin_types_compatible_p(T, vec4) \
                     && !__builtin_has_attribute(T, VEC3_ATTR)
  #define all_or_none_vec3_2_(A, B) __builtin_has_attribute(A, VEC3_ATTR) \
                                 == __builtin_has_attribute(B, VEC3_ATTR)
#else
  #define isvec3_(T...) 0 /* pick something constexpr? */
  #define isvec4_(T...) __builtin_types_compatible_p(T, vec4)
  #define all_or_none_vec3_2_(A, B) 1 /* something */
#endif
#define all_or_none_vec3_(A, B) all_or_none_vec3_2(A, B)


#define vec2_2(x, y) (vec2){ (x), (y) }
#define vec2_1(x)    (vec2){ (x), (x) }

#define vec3_3(x, y, z) (vec3){ (x), (y), (z), (1.f) }
#define vec3_1(x)       (vec3){ (x), (x), (x), (1.f) }

#define vec4_4(x, y, z, w) (vec4){ (x), (y), (z), (w) }
#define vec4_1(x)          (vec4){ (x), (x), (x), (x) }
#define vec4_2(xyz, w) generic(0                                        \
        , int: (vec4){ (xyz)[0], (xyz)[1], (xyz)[2], (w) }              \
        , default:                                                      \
            (const char*[isvec3(xyz)]){"FIRST ARGUMENT MUST BE A vec3"} \
    )

#if BR_COMPILING
#define distinguish_vec3_(T) choose_expr(       \
        __builtin_has_attribute(T, VEC3_ATTR)   \
        , objof(genuinely_vec3)                 \
        , objof(T)                              \
    )
#else
#define distinguish_vec3_(T) objof(T)
#endif



// LIMITS //

#define isnan_(x) generic(x \
        ,  f32: id_i32      \
        ,  f64: id_i32      \
        , vec2: id_i32      \
        , vec4: id_i32      \
    ) (!eq2(x, x))
#define isallnan_(x) generic(x  \
        ,  f32: id_i32          \
        ,  f64: id_i32          \
        , vec2: id_i32          \
        , vec4: id_i32          \
    ) (!anyeq(x, x))

#define isinf_(x...) anyeq(inf(x), abs(x))
#define isallinf_(x...) eq2(inf(x), abs(x))


inline i32 fp_bits_f32_(f32 f) {
    i32 i;
    memcpy(&i, &f, sizeof(i));
    return i;
}
inline i64 fp_bits_f64_(f64 f) {
    i64 i;
    memcpy(&i, &f, sizeof(i));
    return i;
}
inline i32x2 fp_bits_vec2_(vec2 f) {
    i32x2 i;
    memcpy(&i, &f, sizeof(i));
    return i;
}
inline i32x4 fp_bits_vec4_(vec4 f) {
    i32x4 i;
    memcpy(&i, &f, sizeof(i));
    return i;
}

inline f32 fp_from_bits_i32_(i32 i) {
    f32 f;
    memcpy(&f, &i, sizeof(f));
    return f;
}
inline f64 fp_from_bits_i64_(i64 i) {
    f64 f;
    memcpy(&f, &i, sizeof(f));
    return f;
}
inline vec2 fp_from_bits_i32x2_(i32x2 i) {
    vec2 f;
    memcpy(&f, &i, sizeof(f));
    return f;
}
inline vec4 fp_from_bits_i32x4_(i32x4 i) {
    vec4 f;
    memcpy(&f, &i, sizeof(f));
    return f;
}



// TYPE MANIP //

__attribute((__error__("cannot use `objof` in an evaluated context")))
void* objof_evaled_(void);

#define sametype_(A, B) && sametype2(A, B)

#if BR_COMPILING
  #define sametype2_(A, B) __builtin_types_compatible_p(A, B)   \
        && __builtin_has_attribute(A, VEC3_ATTR) ==             \
            __builtin_has_attribute(B, VEC3_ATTR)
#else
  #define sametype2_(A, B) __builtin_types_compatible_p(A, B)   \
        && ( 1 /* just some constexpr ig? */ )
#endif



// NUMBER MANIP //

#define eq_(a, b) && eq2_((a), (b))

#define eq2_(a, b) generic(0                                \
        , int: generic(distinguish_vec3(a + b)              \
            ,            i32: eq2_i32_                      \
            ,            i64: eq2_i64_                      \
            ,            u32: eq2_u32_                      \
            ,            u64: eq2_u64_                      \
            ,            f32: eq2_f32_                      \
            ,            f64: eq2_f64_                      \
            ,           vec2: eq2_vec2_                     \
            , genuinely_vec3: eq2_vec3_                     \
            ,           vec4: eq2_vec4_                     \
        ) (a, b)                                            \
        , default: (const char*[all_or_none_vec3_2(a, b)])  \
            {"vec3 IS ONLY COMPATIBLE WITH OTHER vec3"}     \
    )
inline i32 eq2_i32_(i32 a, i32 b) { return a == b; }
inline i32 eq2_i64_(i64 a, i64 b) { return a == b; }
inline i32 eq2_u32_(u32 a, u32 b) { return a == b; }
inline i32 eq2_u64_(u64 a, u64 b) { return a == b; }
inline i32 eq2_f32_(f32 a, f32 b) { return a == b; }
inline i32 eq2_f64_(f64 a, f64 b) { return a == b; }
inline i32 eq2_vec2_(vec2 a, vec2 b) {
    i32x2 comp = (a == b);
    return !!(comp[0] & comp[1]);
}
inline i32 eq2_vec4_(vec4 a, vec4 b) {
    i32x4 comp = (a == b);
    // bloody rangle it to use movemask.
    i32 movmsk = __builtin_ia32_movmskps(fp_from_bits(comp));
    return movmsk == 0xF;
}
inline i32 eq2_vec3_(vec3 a, vec3 b) {
    // do 4-element compare but ignore last element.
    i32x4 comp = (a == b);
    i32 movmsk = __builtin_ia32_movmskps(fp_from_bits(comp));
    return (movmsk & 0x7) == 0x7;
}

#define anyeq_(a, b) generic(0                              \
        , int: generic(distinguish_vec3(a + b)              \
            ,            i32: anyeq_i32_                    \
            ,            i64: anyeq_i64_                    \
            ,            u32: anyeq_u32_                    \
            ,            u64: anyeq_u64_                    \
            ,            f32: anyeq_f32_                    \
            ,            f64: anyeq_f64_                    \
            ,           vec2: anyeq_vec2_                   \
            , genuinely_vec3: anyeq_vec3_                   \
            ,           vec4: anyeq_vec4_                   \
        ) (a, b)                                            \
        , default: (const char*[all_or_none_vec3_2(a, b)])  \
            {"vec3 IS ONLY COMPATIBLE WITH OTHER vec3"}     \
    )
inline i32 anyeq_i32_(i32 a, i32 b) { return a == b; }
inline i32 anyeq_i64_(i64 a, i64 b) { return a == b; }
inline i32 anyeq_u32_(u32 a, u32 b) { return a == b; }
inline i32 anyeq_u64_(u64 a, u64 b) { return a == b; }
inline i32 anyeq_f32_(f32 a, f32 b) { return a == b; }
inline i32 anyeq_f64_(f64 a, f64 b) { return a == b; }
inline i32 anyeq_vec2_(vec2 a, vec2 b) {
    i32x2 comp = (a == b);
    return !!(comp[0] | comp[1]);
}
inline i32 anyeq_vec4_(vec4 a, vec4 b) {
    i32x4 comp = (a == b);
    i32 movmsk = __builtin_ia32_movmskps(fp_from_bits(comp));
    return movmsk != 0;
}
inline i32 anyeq_vec3_(vec3 a, vec3 b) {
    i32x4 comp = (a == b);
    i32 movmsk = __builtin_ia32_movmskps(fp_from_bits(comp));
    return (movmsk & 0x7) != 0;
}


#define ifnan_(x, dflt) generic(0                               \
        , int: isnan(x) ? dflt : x                              \
        , default: (const char*[all_or_none_vec3_2(x, dflt)])   \
            {"vec3 IS ONLY COMPATIBLE WITH OTHER vec3"}         \
    )

#define ifnanelem_(x, dflt) generic(0                           \
        , int: generic(distinguish_vec3(x + dflt)               \
            ,            f32: ifnanelem_f32_                    \
            ,            f64: ifnanelem_f64_                    \
            ,           vec2: ifnanelem_vec2_                   \
            , genuinely_vec3: ifnanelem_vec3_                   \
            ,           vec4: ifnanelem_vec4_                   \
        ) (x, dflt)                                             \
        , default: (const char*[all_or_none_vec3_2(x, dflt)])   \
            {"vec3 IS ONLY COMPATIBLE WITH OTHER vec3"}         \
    )
inline f32 ifnanelem_f32_(f32 x, f32 dflt) {
    return (x == x) ? x : dflt;
}
inline f64 ifnanelem_f64_(f64 x, f64 dflt) {
    return (x == x) ? x : dflt;
}
inline vec2 ifnanelem_vec2_(vec2 x, vec2 dflt) {
    i32x2 comp = (x == x);
    i32x2 ret = (fp_bits(x) & comp) | (fp_bits(dflt) & ~comp);
    return fp_from_bits(ret);
}
inline vec4 ifnanelem_vec4_(vec4 x, vec4 dflt) {
    i32x4 comp = (x == x);
    i32x4 ret = (fp_bits(x) & comp) | (fp_bits(dflt) & ~comp);
    return fp_from_bits(ret);
}
inline vec3 ifnanelem_vec3_(vec3 x, vec3 dflt) {
    return ifnanelem_vec4_(x, dflt);
}


#define oneof_eq_(x, val) || eq2(x, (val))


#define iabs_(x) generic(distinguish_vec3(x)    \
        ,             i8: iabs_i8_              \
        ,            i16: iabs_i16_             \
        ,            i32: iabs_i32_             \
        ,            i64: iabs_i64_             \
        ,             u8: iabs_u8_              \
        ,            u16: iabs_u16_             \
        ,            u32: iabs_u32_             \
        ,            u64: iabs_u64_             \
        ,            f32: iabs_f32_             \
        ,            f64: iabs_f64_             \
        ,           vec2: iabs_vec2_            \
        , genuinely_vec3: iabs_vec3_            \
        ,           vec4: iabs_vec4_            \
    ) (x)
inline i8  iabs_i8_ (i8 x)  { return (x < 0) ? -x : x; }
inline i16 iabs_i16_(i16 x) { return (x < 0) ? -x : x; }
inline i32 iabs_i32_(i32 x) { return (x < 0) ? -x : x; }
inline i64 iabs_i64_(i64 x) { return (x < 0) ? -x : x; }
inline u8  iabs_u8_ (u8 x)  { return x; }
inline u16 iabs_u16_(u16 x) { return x; }
inline u32 iabs_u32_(u32 x) { return x; }
inline u64 iabs_u64_(u64 x) { return x; }
inline f32 iabs_f32_(f32 x) { return __builtin_fabsf(x); }
inline f64 iabs_f64_(f64 x) { return __builtin_fabs(x); }
inline vec2 iabs_vec2_(vec2 x) {
    return (vec2){ __builtin_fabsf(x[0])
                 , __builtin_fabsf(x[1]) };
}
inline vec4 iabs_vec4_(vec4 x) {
    // the asm of this is hot.
    return (vec4){ __builtin_fabsf(x[0])
                 , __builtin_fabsf(x[1])
                 , __builtin_fabsf(x[2])
                 , __builtin_fabsf(x[3]) };
}
inline vec3 iabs_vec3_(vec3 x) {
    // full builtins is faster than anything ignoring elem 4.
    return iabs_vec4_(x);
}

#define abs_(x) generic(distinguish_vec3(x)    \
        ,             i8: abs_i8_              \
        ,            i16: abs_i16_             \
        ,            i32: abs_i32_             \
        ,            i64: abs_i64_             \
        ,             u8: iabs_u8_             \
        ,            u16: iabs_u16_            \
        ,            u32: iabs_u32_            \
        ,            u64: iabs_u64_            \
        ,            f32: iabs_f32_            \
        ,            f64: iabs_f64_            \
        ,           vec2: iabs_vec2_           \
        , genuinely_vec3: iabs_vec3_           \
        ,           vec4: iabs_vec4_           \
    ) (x)
inline u8  abs_i8_ (i8 x)  { return (u8) ((x < 0) ? -x : x); }
inline u16 abs_i16_(i16 x) { return (u16)((x < 0) ? -x : x); }
inline u32 abs_i32_(i32 x) { return (u32)((x < 0) ? -x : x); }
inline u64 abs_i64_(i64 x) { return (u64)((x < 0) ? -x : x); }
// Cheeky rant as to why ^ these work:
// The issue is:
//  u64 u = iabs(intmin(i32));
//  assert(u != (u64)0x80000000U /* |i32 min| */);
// The incorrect result is obtained because `intmin(i32)` remains unchanged and
// is then cast to `u64`. If it was cast to `u32` in between, the result would be
// correct because of the properties of 2's complement and the integers being the
// same width. That is, it "just so happens" that:
//  (u32)intmin(i32) == -(i64)intmin(i32) /* == |i32 min| */
// So, adding the type-fitting unsigned conversion solves the issue.


#define min_(a, b) generic(0                                \
        , int: generic(distinguish_vec3(a + b)              \
            ,            i32: min_i32_                      \
            ,            i64: min_i64_                      \
            ,            u32: min_u32_                      \
            ,            u64: min_u64_                      \
            ,            f32: min_f32_                      \
            ,            f64: min_f64_                      \
            ,           vec2: min_vec2_                     \
            , genuinely_vec3: min_vec3_                     \
            ,           vec4: min_vec4_                     \
        ) (a, b)                                            \
        , default: (const char*[all_or_none_vec3_2(a, b)])  \
            {"vec3 IS ONLY COMPATIBLE WITH OTHER vec3"}     \
    )
inline i32 min_i32_(i32 a, i32 b) { return (a < b) ? a : b; }
inline i64 min_i64_(i64 a, i64 b) { return (a < b) ? a : b; }
inline u32 min_u32_(u32 a, u32 b) { return (a < b) ? a : b; }
inline u64 min_u64_(u64 a, u64 b) { return (a < b) ? a : b; }
inline f32 min_f32_(f32 a, f32 b) { return (a < b || b != b) ? a : b; }
inline f64 min_f64_(f64 a, f64 b) { return (a < b || b != b) ? a : b; }
inline vec2 min_vec2_(vec2 a, vec2 b) {
    i32x2 comp = (a < b) | (b != b);
    i32x2 ret = (fp_bits(a) & comp) | (fp_bits(b) & ~comp);
    return fp_from_bits(ret);
}
inline vec4 min_vec4_(vec4 a, vec4 b) {
    i32x4 comp = (a < b) | (b != b);
    i32x4 ret = (fp_bits(a) & comp) | (fp_bits(b) & ~comp);
    return fp_from_bits(ret);
}
inline vec3 min_vec3_(vec3 a, vec3 b) {
    // full builtins is faster than anything ignoring elem 4.
    return min_vec4_(a, b);
}

#define max_(a, b) generic(0                                \
        , int: generic(distinguish_vec3(a + b)              \
            ,            i32: max_i32_                      \
            ,            i64: max_i64_                      \
            ,            u32: max_u32_                      \
            ,            u64: max_u64_                      \
            ,            f32: max_f32_                      \
            ,            f64: max_f64_                      \
            ,           vec2: max_vec2_                     \
            , genuinely_vec3: max_vec3_                     \
            ,           vec4: max_vec4_                     \
        ) (a, b)                                            \
        , default: (const char*[all_or_none_vec3_2(a, b)])  \
            {"vec3 IS ONLY COMPATIBLE WITH OTHER vec3"}     \
    )
inline i32 max_i32_(i32 a, i32 b) { return (a > b) ? a : b; }
inline i64 max_i64_(i64 a, i64 b) { return (a > b) ? a : b; }
inline u32 max_u32_(u32 a, u32 b) { return (a > b) ? a : b; }
inline u64 max_u64_(u64 a, u64 b) { return (a > b) ? a : b; }
inline f32 max_f32_(f32 a, f32 b) { return (a > b || b != b) ? a : b; }
inline f64 max_f64_(f64 a, f64 b) { return (a > b || b != b) ? a : b; }
inline vec2 max_vec2_(vec2 a, vec2 b) {
    i32x2 comp = (a > b) | (b != b);
    i32x2 ret = (fp_bits(a) & comp) | (fp_bits(b) & ~comp);
    return fp_from_bits(ret);
}
inline vec4 max_vec4_(vec4 a, vec4 b) {
    i32x4 comp = (a > b) | (b != b);
    i32x4 ret = (fp_bits(a) & comp) | (fp_bits(b) & ~comp);
    return fp_from_bits(ret);
}
inline vec3 max_vec3_(vec3 a, vec3 b) {
    // full builtins is faster than anything ignoring elem 4.
    return max_vec4_(a, b);
}


#define minelem_(x) generic(distinguish_vec3(x + 0) \
            ,            i32: minelem_i32_          \
            ,            i64: minelem_i64_          \
            ,            u32: minelem_u32_          \
            ,            u64: minelem_u64_          \
            ,            f32: minelem_f32_          \
            ,            f64: minelem_f64_          \
            ,           vec2: minelem_vec2_         \
            , genuinely_vec3: minelem_vec3_         \
            ,           vec4: minelem_vec4_         \
    ) (x)
inline i32 minelem_i32_(i32 x) { return x; }
inline i64 minelem_i64_(i64 x) { return x; }
inline u32 minelem_u32_(u32 x) { return x; }
inline u64 minelem_u64_(u64 x) { return x; }
inline f32 minelem_f32_(f32 x) { return x; }
inline f64 minelem_f64_(f64 x) { return x; }
inline f32 minelem_vec2_(vec2 x) {
    return (x[0] < x[1] || x[1] != x[1]) ? x[0] : x[1];
}
inline f32 minelem_vec4_(vec4 x) {
    f32 r = x[0];
    r = (r < x[1] || x[1] != x[1]) ? r : x[1];
    r = (r < x[2] || x[2] != x[2]) ? r : x[2];
    r = (r < x[3] || x[3] != x[3]) ? r : x[3];
    return r;
}
inline f32 minelem_vec3_(vec3 x) {
    f32 r = x[0];
    r = (r < x[1] || x[1] != x[1]) ? r : x[1];
    r = (r < x[2] || x[2] != x[2]) ? r : x[2];
    return r;
}

#define maxelem_(x) generic(distinguish_vec3(x + 0) \
            ,            i32: maxelem_i32_          \
            ,            i64: maxelem_i64_          \
            ,            u32: maxelem_u32_          \
            ,            u64: maxelem_u64_          \
            ,            f32: maxelem_f32_          \
            ,            f64: maxelem_f64_          \
            ,           vec2: maxelem_vec2_         \
            , genuinely_vec3: maxelem_vec3_         \
            ,           vec4: maxelem_vec4_         \
    ) (x)
inline i32 maxelem_i32_(i32 x) { return x; }
inline i64 maxelem_i64_(i64 x) { return x; }
inline u32 maxelem_u32_(u32 x) { return x; }
inline u64 maxelem_u64_(u64 x) { return x; }
inline f32 maxelem_f32_(f32 x) { return x; }
inline f64 maxelem_f64_(f64 x) { return x; }
inline f32 maxelem_vec2_(vec2 x) {
    return (x[0] > x[1] || x[1] != x[1]) ? x[0] : x[1];
}
inline f32 maxelem_vec4_(vec4 x) {
    f32 r = x[0];
    r = (r > x[1] || x[1] != x[1]) ? r : x[1];
    r = (r > x[2] || x[2] != x[2]) ? r : x[2];
    r = (r > x[3] || x[3] != x[3]) ? r : x[3];
    return r;
}
inline f32 maxelem_vec3_(vec3 x) {
    f32 r = x[0];
    r = (r > x[1] || x[1] != x[1]) ? r : x[1];
    r = (r > x[2] || x[2] != x[2]) ? r : x[2];
    return r;
}



// COUNTING ARGS //

#define BR_SELECT_N_(args...) BR_SELECT_N_FR_(args)
#define BR_SELECT_N_FR_(                                    \
               a1,  a2,  a3,  a4,  a5,  a6,  a7,  a8,  a9,  \
         a10, a11, a12, a13, a14, a15, a16, a17, a18, a19,  \
         a20, a21, a22, a23, a24, a25, a26, a27, a28, a29,  \
         a30, a31, a32, a33, a34, a35, a36, a37, a38, a39,  \
         a40, a41, a42, a43, a44, a45, a46, a47, a48, a49,  \
         a50, a51, a52, a53, a54, a55, a56, a57, a58, a59,  \
         a60, a61, a62, a63, a64, a65, a66, a67, a68, a69,  \
         a70, a71, a72, a73, a74, a75, a76, a77, a78, a79,  \
         a80, a81, a82, a83, a84, a85, a86, a87, a88, a89,  \
         a90, a91, a92, a93, a94, a95, a96, a97, a98, a99,  \
                                                            \
        a100,a101,a102,a103,a104,a105,a106,a107,a108,a109,  \
        a110,a111,a112,a113,a114,a115,a116,a117,a118,a119,  \
        a120,a121,a122,a123,a124,a125,a126,a127,a128,a129,  \
        a130,a131,a132,a133,a134,a135,a136,a137,a138,a139,  \
        a140,a141,a142,a143,a144,a145,a146,a147,a148,a149,  \
        a150,a151,a152,a153,a154,a155,a156,a157,a158,a159,  \
        a160,a161,a162,a163,a164,a165,a166,a167,a168,a169,  \
        a170,a171,a172,a173,a174,a175,a176,a177,a178,a179,  \
        a180,a181,a182,a183,a184,a185,a186,a187,a188,a189,  \
        a190,a191,a192,a193,a194,a195,a196,a197,a198,a199,  \
                                                            \
        a200,a201,a202,a203,a204,a205,a206,a207,a208,a209,  \
        a210,a211,a212,a213,a214,a215,a216,a217,a218,a219,  \
        a220,a221,a222,a223,a224,a225,a226,a227,a228,a229,  \
        a230,a231,a232,a233,a234,a235,a236,a237,a238,a239,  \
        a240,a241,a242,a243,a244,a245,a246,a247,a248,a249,  \
        a250,a251,a252,a253,a254,a255,a256,a257,a258,a259,  \
        a260,a261,a262,a263,a264,a265,a266,a267,a268,a269,  \
        a270,a271,a272,a273,a274,a275,a276,a277,a278,a279,  \
        a280,a281,a282,a283,a284,a285,a286,a287,a288,a289,  \
        a290,a291,a292,a293,a294,a295,a296,a297,a298,a299,  \
                                                            \
        a300,a301,a302,a303,a304,a305,a306,a307,a308,a309,  \
        a310,a311,a312,a313,a314,a315,a316,a317,a318,a319,  \
        a320,a321,a322,a323,a324,a325,a326,a327,a328,a329,  \
        a330,a331,a332,a333,a334,a335,a336,a337,a338,a339,  \
        a340,a341,a342,a343,a344,a345,a346,a347,a348,a349,  \
        a350,a351,a352,a353,a354,a355,a356,a357,a358,a359,  \
        a360,a361,a362,a363,a364,a365,a366,a367,a368,a369,  \
        a370,a371,a372,a373,a374,a375,a376,a377,a378,a379,  \
        a380,a381,a382,a383,a384,a385,a386,a387,a388,a389,  \
        a390,a391,a392,a393,a394,a395,a396,a397,a398,a399,  \
                                                            \
        a400,a401,a402,a403,a404,a405,a406,a407,a408,a409,  \
        a410,a411,a412,a413,a414,a415,a416,a417,a418,a419,  \
        a420,a421,a422,a423,a424,a425,a426,a427,a428,a429,  \
        a430,a431,a432,a433,a434,a435,a436,a437,a438,a439,  \
        a440,a441,a442,a443,a444,a445,a446,a447,a448,a449,  \
        a450,a451,a452,a453,a454,a455,a456,a457,a458,a459,  \
        a460,a461,a462,a463,a464,a465,a466,a467,a468,a469,  \
        a470,a471,a472,a473,a474,a475,a476,a477,a478,a479,  \
        a480,a481,a482,a483,a484,a485,a486,a487,a488,a489,  \
        a490,a491,a492,a493,a494,a495,a496,a497,a498,a499,  \
                                                            \
        a500,a501,a502,a503,a504,a505,a506,a507,a508,a509,  \
        a510,a511,a512,a513,a514,a515,a516,a517,a518,a519,  \
        a520,a521,a522,a523,a524,a525,a526,a527,a528,a529,  \
        a530,a531,a532,a533,a534,a535,a536,a537,a538,a539,  \
        a540,a541,a542,a543,a544,a545,a546,a547,a548,a549,  \
        a550,a551,a552,a553,a554,a555,a556,a557,a558,a559,  \
        a560,a561,a562,a563,a564,a565,a566,a567,a568,a569,  \
        a570,a571,a572,a573,a574,a575,a576,a577,a578,a579,  \
        a580,a581,a582,a583,a584,a585,a586,a587,a588,a589,  \
        a590,a591,a592,a593,a594,a595,a596,a597,a598,a599,  \
                                                            \
        a600,a601,a602,a603,a604,a605,a606,a607,a608,a609,  \
        a610,a611,a612,a613,a614,a615,a616,a617,a618,a619,  \
        a620,a621,a622,a623,a624,a625,a626,a627,a628,a629,  \
        a630,a631,a632,a633,a634,a635,a636,a637,a638,a639,  \
        a640,a641,a642,a643,a644,a645,a646,a647,a648,a649,  \
        a650,a651,a652,a653,a654,a655,a656,a657,a658,a659,  \
        a660,a661,a662,a663,a664,a665,a666,a667,a668,a669,  \
        a670,a671,a672,a673,a674,a675,a676,a677,a678,a679,  \
        a680,a681,a682,a683,a684,a685,a686,a687,a688,a689,  \
        a690,a691,a692,a693,a694,a695,a696,a697,a698,a699,  \
                                                            \
        a700,a701,a702,a703,a704,a705,a706,a707,a708,a709,  \
        a710,a711,a712,a713,a714,a715,a716,a717,a718,a719,  \
        a720,a721,a722,a723,a724,a725,a726,a727,a728,a729,  \
        a730,a731,a732,a733,a734,a735,a736,a737,a738,a739,  \
        a740,a741,a742,a743,a744,a745,a746,a747,a748,a749,  \
        a750,a751,a752,a753,a754,a755,a756,a757,a758,a759,  \
        a760,a761,a762,a763,a764,a765,a766,a767,a768,a769,  \
        a770,a771,a772,a773,a774,a775,a776,a777,a778,a779,  \
        a780,a781,a782,a783,a784,a785,a786,a787,a788,a789,  \
        a790,a791,a792,a793,a794,a795,a796,a797,a798,a799,  \
                                                            \
        a800,a801,a802,a803,a804,a805,a806,a807,a808,a809,  \
        a810,a811,a812,a813,a814,a815,a816,a817,a818,a819,  \
        a820,a821,a822,a823,a824,a825,a826,a827,a828,a829,  \
        a830,a831,a832,a833,a834,a835,a836,a837,a838,a839,  \
        a840,a841,a842,a843,a844,a845,a846,a847,a848,a849,  \
        a850,a851,a852,a853,a854,a855,a856,a857,a858,a859,  \
        a860,a861,a862,a863,a864,a865,a866,a867,a868,a869,  \
        a870,a871,a872,a873,a874,a875,a876,a877,a878,a879,  \
        a880,a881,a882,a883,a884,a885,a886,a887,a888,a889,  \
        a890,a891,a892,a893,a894,a895,a896,a897,a898,a899,  \
                                                            \
        a900,a901,a902,a903,a904,a905,a906,a907,a908,a909,  \
        a910,a911,a912,a913,a914,a915,a916,a917,a918,a919,  \
        a920,a921,a922,a923,a924,a925,a926,a927,a928,a929,  \
        a930,a931,a932,a933,a934,a935,a936,a937,a938,a939,  \
        a940,a941,a942,a943,a944,a945,a946,a947,a948,a949,  \
        a950,a951,a952,a953,a954,a955,a956,a957,a958,a959,  \
        a960,a961,a962,a963,a964,a965,a966,a967,a968,a969,  \
        a970,a971,a972,a973,a974,a975,a976,a977,a978,a979,  \
        a980,a981,a982,a983,a984,a985,a986,a987,a988,a989,  \
        a990,a991,a992,a993,a994,a995,a996,a997,a998,a999,  \
                                                            \
        N, ignored...) N



// ANYVA //

#define anyva_(args...) GLUE2(anyva_, GLUE2(    \
        multipleva(COMMA args ()),              \
        multipleva(COMMA args)                  \
    ))
#define anyva_00 1
#define anyva_01 1
#define anyva_10 0
#define anyva_11 1



// MULTIPLEVA //

#define multipleva_(args...) BR_SELECT_N_(args, BR_MULTIPLEVA_SEQ_)

#define BR_MULTIPLEVA_SEQ_      \
        /* 900..999: */         \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        /* 800..899: */         \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        /* 700..799: */         \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        /* 600..699: */         \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        /* 500..599: */         \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        /* 400..499: */         \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        /* 300..399: */         \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        /* 200..299: */         \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        /* 100..199: */         \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        /* 0..99: */            \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,1,1,    \
        1,1,1,1,1,1,1,1,0,0



// COUNTVA //
// Adapted from https://stackoverflow.com/a/11742317 to correctly identify inputs
// with a bracket-ed argument. e.g. `countva( (1) )`

#define countva_(args...) BR_COUNTVA_DO_(                                       \
        /* (A) Counts the number of arguments, however expands to 1 for both */ \
        /* no arguments and one argument given. */                              \
        BR_SELECT_N_(args, BR_COUNTVA_REVERSE_1000_),                           \
        /* > if (A) == 1: goto (B); else return (A). */                         \
                                                                                \
        /* (B) Checks for empty input by expanding to 1 if input isn't empty, */\
        /* and 0 otherwise. HOWEVER, it can be tricked with an input of */      \
        /* "(...)" which expands the thang anyway. sooo.... */                  \
        multipleva(COMMA args ()),                                              \
        /* > if (B): goto (C); else return 1. */                                \
                                                                                \
        /* (C) Check argument isn't wrapped in brackets by expanding to 1 if */ \
        /* is wrapped, and 0 otherwise. */                                      \
        multipleva(COMMA args)                                                  \
        /* > return (C). :D */                                                  \
    )

// Sepcialised `n == 1` for `n` in 1..999.
#define BR_COUNTVA_EQ_1_(n) MN_NOT(MN_DEC(n))

#define BR_COUNTVA_DO_(A, B, C) BR_COUNTVA_DO_FR_(A, B, C)
#define BR_COUNTVA_DO_FR_(A, B, C)      \
        IF_ELSE(BR_COUNTVA_EQ_1_(A),    \
        /* A==1: */ IF_ELSE(B,          \
                    /* B: */ C,         \
                    /* else: */ 1       \
                    ),                  \
        /* else: */ A                   \
        ) // mmmmmm macro conditionals

#define BR_COUNTVA_REVERSE_1000_                    \
                                                    \
        999,998,997,996,995,994,993,992,991,990,    \
        989,988,987,986,985,984,983,982,981,980,    \
        979,978,977,976,975,974,973,972,971,970,    \
        969,968,967,966,965,964,963,962,961,960,    \
        959,958,957,956,955,954,953,952,951,950,    \
        949,948,947,946,945,944,943,942,941,940,    \
        939,938,937,936,935,934,933,932,931,930,    \
        929,928,927,926,925,924,923,922,921,920,    \
        919,918,917,916,915,914,913,912,911,910,    \
        909,908,907,906,905,904,903,902,901,900,    \
                                                    \
        899,898,897,896,895,894,893,892,891,890,    \
        889,888,887,886,885,884,883,882,881,880,    \
        879,878,877,876,875,874,873,872,871,870,    \
        869,868,867,866,865,864,863,862,861,860,    \
        859,858,857,856,855,854,853,852,851,850,    \
        849,848,847,846,845,844,843,842,841,840,    \
        839,838,837,836,835,834,833,832,831,830,    \
        829,828,827,826,825,824,823,822,821,820,    \
        819,818,817,816,815,814,813,812,811,810,    \
        809,808,807,806,805,804,803,802,801,800,    \
                                                    \
        799,798,797,796,795,794,793,792,791,790,    \
        789,788,787,786,785,784,783,782,781,780,    \
        779,778,777,776,775,774,773,772,771,770,    \
        769,768,767,766,765,764,763,762,761,760,    \
        759,758,757,756,755,754,753,752,751,750,    \
        749,748,747,746,745,744,743,742,741,740,    \
        739,738,737,736,735,734,733,732,731,730,    \
        729,728,727,726,725,724,723,722,721,720,    \
        719,718,717,716,715,714,713,712,711,710,    \
        709,708,707,706,705,704,703,702,701,700,    \
                                                    \
        699,698,697,696,695,694,693,692,691,690,    \
        689,688,687,686,685,684,683,682,681,680,    \
        679,678,677,676,675,674,673,672,671,670,    \
        669,668,667,666,665,664,663,662,661,660,    \
        659,658,657,656,655,654,653,652,651,650,    \
        649,648,647,646,645,644,643,642,641,640,    \
        639,638,637,636,635,634,633,632,631,630,    \
        629,628,627,626,625,624,623,622,621,620,    \
        619,618,617,616,615,614,613,612,611,610,    \
        609,608,607,606,605,604,603,602,601,600,    \
                                                    \
        599,598,597,596,595,594,593,592,591,590,    \
        589,588,587,586,585,584,583,582,581,580,    \
        579,578,577,576,575,574,573,572,571,570,    \
        569,568,567,566,565,564,563,562,561,560,    \
        559,558,557,556,555,554,553,552,551,550,    \
        549,548,547,546,545,544,543,542,541,540,    \
        539,538,537,536,535,534,533,532,531,530,    \
        529,528,527,526,525,524,523,522,521,520,    \
        519,518,517,516,515,514,513,512,511,510,    \
        509,508,507,506,505,504,503,502,501,500,    \
                                                    \
        499,498,497,496,495,494,493,492,491,490,    \
        489,488,487,486,485,484,483,482,481,480,    \
        479,478,477,476,475,474,473,472,471,470,    \
        469,468,467,466,465,464,463,462,461,460,    \
        459,458,457,456,455,454,453,452,451,450,    \
        449,448,447,446,445,444,443,442,441,440,    \
        439,438,437,436,435,434,433,432,431,430,    \
        429,428,427,426,425,424,423,422,421,420,    \
        419,418,417,416,415,414,413,412,411,410,    \
        409,408,407,406,405,404,403,402,401,400,    \
                                                    \
        399,398,397,396,395,394,393,392,391,390,    \
        389,388,387,386,385,384,383,382,381,380,    \
        379,378,377,376,375,374,373,372,371,370,    \
        369,368,367,366,365,364,363,362,361,360,    \
        359,358,357,356,355,354,353,352,351,350,    \
        349,348,347,346,345,344,343,342,341,340,    \
        339,338,337,336,335,334,333,332,331,330,    \
        329,328,327,326,325,324,323,322,321,320,    \
        319,318,317,316,315,314,313,312,311,310,    \
        309,308,307,306,305,304,303,302,301,300,    \
                                                    \
        299,298,297,296,295,294,293,292,291,290,    \
        289,288,287,286,285,284,283,282,281,280,    \
        279,278,277,276,275,274,273,272,271,270,    \
        269,268,267,266,265,264,263,262,261,260,    \
        259,258,257,256,255,254,253,252,251,250,    \
        249,248,247,246,245,244,243,242,241,240,    \
        239,238,237,236,235,234,233,232,231,230,    \
        229,228,227,226,225,224,223,222,221,220,    \
        219,218,217,216,215,214,213,212,211,210,    \
        209,208,207,206,205,204,203,202,201,200,    \
                                                    \
        199,198,197,196,195,194,193,192,191,190,    \
        189,188,187,186,185,184,183,182,181,180,    \
        179,178,177,176,175,174,173,172,171,170,    \
        169,168,167,166,165,164,163,162,161,160,    \
        159,158,157,156,155,154,153,152,151,150,    \
        149,148,147,146,145,144,143,142,141,140,    \
        139,138,137,136,135,134,133,132,131,130,    \
        129,128,127,126,125,124,123,122,121,120,    \
        119,118,117,116,115,114,113,112,111,110,    \
        109,108,107,106,105,104,103,102,101,100,    \
                                                    \
         99, 98, 97, 96, 95, 94, 93, 92, 91, 90,    \
         89, 88, 87, 86, 85, 84, 83, 82, 81, 80,    \
         79, 78, 77, 76, 75, 74, 73, 72, 71, 70,    \
         69, 68, 67, 66, 65, 64, 63, 62, 61, 60,    \
         59, 58, 57, 56, 55, 54, 53, 52, 51, 50,    \
         49, 48, 47, 46, 45, 44, 43, 42, 41, 40,    \
         39, 38, 37, 36, 35, 34, 33, 32, 31, 30,    \
         29, 28, 27, 26, 25, 24, 23, 22, 21, 20,    \
         19, 18, 17, 16, 15, 14, 13, 12, 11, 10,    \
          9,  8,  7,  6,  5,  4,  3,  2,  1,  0



// BOOLEAN MANIP //

#define all_2_(_, a, b) (a) && (b)
#define all_(xs...) ( IF_ELSE(anyva(xs) \
        , IF_ELSE(multipleva(xs)        \
            , REDUCE(all_2_, _, xs)     \
            , !!(xs)                    \
        )                               \
        , 1                             \
    ) )

#define any_2_(_, a, b) (a) || (b)
#define any_(xs...) ( IF_ELSE(anyva(xs) \
        , IF_ELSE(multipleva(xs)        \
            , REDUCE(any_2_, _, xs)     \
            , !!(xs)                    \
        )                               \
        , 0                             \
    ) )

#define just_one_not_(_, x) !!(x),
#define just_one_2_(_, a, b) a + b
#define just_one_(xs...) ( IF_ELSE(anyva(xs)    \
        , IF_ELSE(multipleva(xs)                \
            , 1 == REDUCE(just_one_2_, _,       \
                FOREACH(just_one_not_, _, xs) 0 \
            )                                   \
            , !!(xs)                            \
        )                                       \
        , 0                                     \
    ) )



// MACROS //

#define STRINGIFY_(xs...) #xs

#define GLUE2_(a, b) a##b



// MACRO NUMERALS //

// MN_INC in br.ii

// MN_DEC in br.ii

// MN_NOT in br.ii

// sneak this in.
#ifndef BREAD
  #error BROTHER
#endif

#define MN_GT_0 a_greater_than_b , padding
#define MN_GT_N_(_1, _2, n, ignored...) n
#define MN_GT_N(args...) MN_GT_N_(args)
#define MN_GT_(a, b) MN_GT_N(GLUE2(MN_GT_, MN_NOT(MN_SUB(a, b))), 1, 0, )
// If `!(a - b) == 0`:
//      means `a - b > 0` -> `a > b`, return 1.
// Else:
//      the MN_SUB is undefined and will be some random expression.
//      OR `!(a - b) == 1` -> `a == b` -> MN_GT_1 is a single token.
//      both of these cases return 0.



// MACRO CONDITIONALS //

#define IF_0(x...) x
#define IF_1(x...)

#define IF_NOT_0(x...)
#define IF_NOT_1(x...) x

#define IF_ELSE_0(if_, else_) if_
#define IF_ELSE_1(if_, else_) else_



// MACRO UTILITIES //

// REPEAT in br.ii

// FOREACH in br.ii

// REDUCE in br.ii

// ITERATED in br.ii


// REVERSE in br.ii

#define FIRST_(first, rest...) first

#define REST_(args...) IDENTITY( REST_FR (args IF(multipleva(args), ,)) )
#define REST_FR(first, rest...) rest // fr.



// MISC //

#define swap_(a, b)                                                             \
        static_assert(sametype(a, b));                                          \
        /* need a `typeof_unqual`, which you can see a proper-like definition */\
        /* of below, but i cant be bothered setting up the full framework to */ \
        /* let that work so just support the non-array version. */              \
        typeof( ((void)0, a) ) tmp = b;                                         \
        b = a;                                                                  \
        a = tmp;                                                                \


#if 0
// Use comma-operator to force lvalue to value conversion (which strips
// qualifiers). However, this will also decay arrays to pointers to dont use it
// for that.
#define typeof_unqual_nonarray_(x) objof( (void)0, x )

// Manually construct a stripped array if given, otherwise just use the non array
// version.
#define typeof_unqual_(T)                                                       \
        compiletime_if_else(is_array(T)                                         \
            , objof(                                                            \
                ensure_nonvoid(                                                 \
                    typeof_unqual_nonarray_(*objof(ensure_derefable(T)))        \
                )[                                                              \
                    sizeof(ensure_nonvoid(T))                                   \
                    /                                                           \
                    sizeof(ensure_nonvoid(*objof(ensure_derefable(T))))         \
                ]                                                               \
            )                                                                   \
            , typeof_unqual_nonarray_(objof(T))                                 \
        )
#endif
