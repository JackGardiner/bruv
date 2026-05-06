#include "ethanol.h"

#include "assertion.h"
#include "maths.h"


// function of pressure to avoid gas+super critical regions.
f64 ethanol_max_T(f64 P) {
    f64 T = 16.5e-6*P + 409.9;
    return min(500.0, T);
}


#define ETHANOL_2DLOOKUP() do {                                                 \
        f64 x = T;                                                              \
        f64 y = P*1e-6;                                                         \
        assert(XLO <= x && x <= XHI, "approximation input oob: x=%g", x);       \
        assert(YLO <= y && y <= YHI, "approximation input oob: y=%g", y);       \
        assert(x <= 18.75 * y + 410.6, "approximation input oob: x=%g, y=%g",   \
                x, y);                                                          \
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
        assert(notnan(v), "approximation nan output: x=%g, y=%g", x, y);        \
        return v;                                                               \
    } while (0)

f64 ethanol_rho(f64 T, f64 P) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.131% */
    /*   rel 0.177% */
    /* also requires: */
    /*   x <= 16.5 * y + 409.9 */
    const f64 XLO = 250.0;
    const f64 XHI = 500.0;
    const f64 YLO = 2.0;
    const f64 YHI = 7.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/ethanol_rho.i"
    ETHANOL_2DLOOKUP();
}

f64 ethanol_cp(f64 T, f64 P) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 1.24% */
    /*   rel 1.66% */
    /* also requires: */
    /*   x <= 16.5 * y + 409.9 */
    const f64 XLO = 250.0;
    const f64 XHI = 500.0;
    const f64 YLO = 2.0;
    const f64 YHI = 7.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/ethanol_cp.i"
    ETHANOL_2DLOOKUP();
}

f64 ethanol_mu(f64 T, f64 P) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.161% */
    /*   rel 0.0925% */
    /* also requires: */
    /*   x <= 16.5 * y + 409.9 */
    const f64 XLO = 250.0;
    const f64 XHI = 500.0;
    const f64 YLO = 2.0;
    const f64 YHI = 7.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/ethanol_mu.i"
    ETHANOL_2DLOOKUP();
}

f64 ethanol_k(f64 T, f64 P) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.0629% */
    /*   rel 0.183% */
    /* also requires: */
    /*   x <= 16.5 * y + 409.9 */
    const f64 XLO = 250.0;
    const f64 XHI = 500.0;
    const f64 YLO = 2.0;
    const f64 YHI = 7.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/ethanol_k.i"
    ETHANOL_2DLOOKUP();
}

#undef ETHANOL_2DLOOKUP
