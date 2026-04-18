#include "ipa.h"

#include "assertion.h"
#include "maths.h"


#define IPA_2DLOOKUP() do {                                                     \
        f64 x = T;                                                              \
        f64 y = P*1e-6;                                                         \
        assert(XLO <= x && x <= XHI, "approximation input oob: x=%g", x);       \
        assert(YLO <= y && y <= YHI, "approximation input oob: y=%g", y);       \
        assert(x < 18.75 * y + 417.5, "approximation input oob: x=%g, y=%g", x, \
                y);                                                             \
        f64 t = (x - XLO) / (XHI - XLO);                                        \
        f64 s = (y - YLO) / (YHI - YLO);                                        \
        t *= XLEN - 1;                                                          \
        s *= YLEN - 1;                                                          \
        i32 i = min(max((i32)t, 0), XLEN - 2);                                  \
        i32 j = min(max((i32)s, 0), YLEN - 2);                                  \
        t -= i;                                                                 \
        s -= j;                                                                 \
        f64 v00 = tbl[YLEN*i + j];                                              \
        f64 v01 = tbl[YLEN*i + j + 1];                                          \
        f64 v10 = tbl[YLEN*(i + 1) + j];                                        \
        f64 v11 = tbl[YLEN*(i + 1) + j + 1];                                    \
        f64 v0 = v00 + s*(v01 - v00);                                           \
        f64 v1 = v10 + s*(v11 - v10);                                           \
        f64 v = v0 + t*(v1 - v0);                                               \
        assert(notnan(v), "approximation input oob: x=%g, y=%g", x, y);         \
        return v;                                                               \
    } while (0)

f64 ipa_rho(f64 T, f64 P) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.366% */
    /*   rel 0.458% */
    /* also requires: */
    /*   x < 18.75 * y + 417.5 */
    const f64 XLO = 250.0;
    const f64 XHI = 500.0;
    const f64 YLO = 2.0;
    const f64 YHI = 7.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/ipa_rho.i"
    IPA_2DLOOKUP();
}

f64 ipa_cp(f64 T, f64 P) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.00939% */
    /*   rel 0.0072% */
    /* also requires: */
    /*   x < 18.75 * y + 417.5 */
    const f64 XLO = 250.0;
    const f64 XHI = 500.0;
    const f64 YLO = 2.0;
    const f64 YHI = 7.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/ipa_cp.i"
    IPA_2DLOOKUP();
}

f64 ipa_mu(f64 T, f64 P) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.239% */
    /*   rel 0.222% */
    /* also requires: */
    /*   x < 18.75 * y + 417.5 */
    const f64 XLO = 250.0;
    const f64 XHI = 500.0;
    const f64 YLO = 2.0;
    const f64 YHI = 7.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/ipa_mu.i"
    IPA_2DLOOKUP();
}

f64 ipa_k(f64 T, f64 P) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.000662% */
    /*   rel 0.00209% */
    /* also requires: */
    /*   x < 18.75 * y + 417.5 */
    const f64 XLO = 250.0;
    const f64 XHI = 500.0;
    const f64 YLO = 2.0;
    const f64 YHI = 7.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/ipa_k.i"
    IPA_2DLOOKUP();
}

#undef IPA_2DLOOKUP
