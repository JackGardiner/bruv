#include "cea.h"

#include "assertion.h"
#include "maths.h"


#define CEA_2DLOOKUP() do {                                                 \
        f64 x = P0_cc*1e-6;                                                 \
        f64 y = ofr;                                                        \
        assert(XLO <= x && x <= XHI, "approximation input oob: x=%g", x);   \
        assert(YLO <= y && y <= YHI, "approximation input oob: y=%g", y);   \
        f64 t = (x - XLO) / (XHI - XLO);                                    \
        f64 u = (y - YLO) / (YHI - YLO);                                    \
        t *= XLEN - 1;                                                      \
        u *= YLEN - 1;                                                      \
        i32 i = min(max((i32)t, 0), XLEN - 2);                              \
        i32 j = min(max((i32)u, 0), YLEN - 2);                              \
        t -= i;                                                             \
        u -= j;                                                             \
        f64 X00 = tbl[(i  )*YLEN + (j  )];                                  \
        f64 X01 = tbl[(i  )*YLEN + (j+1)];                                  \
        f64 X10 = tbl[(i+1)*YLEN + (j  )];                                  \
        f64 X11 = tbl[(i+1)*YLEN + (j+1)];                                  \
        f64 X0 = lerp(X00, X01, u);                                         \
        f64 X1 = lerp(X10, X11, u);                                         \
        f64 X = lerp(X0, X1, t);                                            \
        assert(notnan(X), "approximation input oob: x=%g, y=%g", x, y);     \
        return X;                                                           \
    } while (0)

#define CEA_3DLOOKUP() do {                                                     \
        f64 x = P0_cc*1e-6;                                                     \
        f64 y = ofr;                                                            \
        f64 z = AR;                                                             \
        assert(XLO <= x && x <= XHI, "approximation input oob: x=%g", x);       \
        assert(YLO <= y && y <= YHI, "approximation input oob: y=%g", y);       \
        assert(ZLO <= z && z <= ZHI, "approximation input oob: z=%g", z);       \
        f64 t = (x - XLO) / (XHI - XLO);                                        \
        f64 u = (y - YLO) / (YHI - YLO);                                        \
        f64 v = (z - ZLO) / (ZHI - ZLO);                                        \
        t *= XLEN - 1;                                                          \
        u *= YLEN - 1;                                                          \
        v *= ZLEN - 1;                                                          \
        i32 i = min(max((i32)t, 0), XLEN - 2);                                  \
        i32 j = min(max((i32)u, 0), YLEN - 2);                                  \
        i32 k = min(max((i32)v, 0), ZLEN - 2);                                  \
        t -= i;                                                                 \
        u -= j;                                                                 \
        v -= k;                                                                 \
        f64 X000 = tbl[((i  )*YLEN + (j  ))*ZLEN + (k  )];                      \
        f64 X001 = tbl[((i  )*YLEN + (j  ))*ZLEN + (k+1)];                      \
        f64 X010 = tbl[((i  )*YLEN + (j+1))*ZLEN + (k  )];                      \
        f64 X011 = tbl[((i  )*YLEN + (j+1))*ZLEN + (k+1)];                      \
        f64 X100 = tbl[((i+1)*YLEN + (j  ))*ZLEN + (k  )];                      \
        f64 X101 = tbl[((i+1)*YLEN + (j  ))*ZLEN + (k+1)];                      \
        f64 X110 = tbl[((i+1)*YLEN + (j+1))*ZLEN + (k  )];                      \
        f64 X111 = tbl[((i+1)*YLEN + (j+1))*ZLEN + (k+1)];                      \
        f64 X00 = lerp(X000, X001, v);                                          \
        f64 X01 = lerp(X010, X011, v);                                          \
        f64 X10 = lerp(X100, X101, v);                                          \
        f64 X11 = lerp(X110, X111, v);                                          \
        f64 X0 = lerp(X00, X01, u);                                             \
        f64 X1 = lerp(X10, X11, u);                                             \
        f64 X = lerp(X0, X1, t);                                                \
        assert(notnan(X), "approximation input oob: x=%g, y=%g, z=%g", x, y, z);\
        return X;                                                               \
    } while (0)


f64 cea_perfexp_AEAT(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    #include "tbl/cea_perfexp_AEAT.i"
    CEA_2DLOOKUP();
}

f64 cea_Ivac(f64 P0_cc, f64 ofr, f64 AR) {
    /* evenly-spaced flattened (C-ordered) 3D LUT */
    #include "tbl/cea_Ivac.i"
    CEA_3DLOOKUP();
}
f64 cea_T(f64 P0_cc, f64 ofr, f64 AR) {
    if (AR == 0.0) {
        /* evenly-spaced flattened (C-ordered) 2D LUT */
        #include "tbl/cea_cc_T.i"
        CEA_2DLOOKUP();
    } else if (AR >= 1.0) {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sup_T.i"
        CEA_3DLOOKUP();
    } else {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sub_T.i"
        CEA_3DLOOKUP();
    }
}
f64 cea_P(f64 P0_cc, f64 ofr, f64 AR) {
    if (AR == 0.0) {
        /* evenly-spaced flattened (C-ordered) 2D LUT */
        #include "tbl/cea_cc_P.i"
        CEA_2DLOOKUP();
    } else if (AR >= 1.0) {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sup_P.i"
        CEA_3DLOOKUP();
    } else {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sub_P.i"
        CEA_3DLOOKUP();
    }
}
f64 cea_rho(f64 P0_cc, f64 ofr, f64 AR) {
    if (AR == 0.0) {
        /* evenly-spaced flattened (C-ordered) 2D LUT */
        #include "tbl/cea_cc_rho.i"
        CEA_2DLOOKUP();
    } else if (AR >= 1.0) {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sup_rho.i"
        CEA_3DLOOKUP();
    } else {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sub_rho.i"
        CEA_3DLOOKUP();
    }
}
f64 cea_M(f64 P0_cc, f64 ofr, f64 AR) {
    if (AR == 0.0) {
        return 0.0;
    } else if (AR >= 1.0) {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sup_M.i"
        CEA_3DLOOKUP();
    } else {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sub_M.i"
        CEA_3DLOOKUP();
    }
}
f64 cea_a(f64 P0_cc, f64 ofr, f64 AR) {
    if (AR == 0.0) {
        /* evenly-spaced flattened (C-ordered) 2D LUT */
        #include "tbl/cea_cc_a.i"
        CEA_2DLOOKUP();
    } else if (AR >= 1.0) {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sup_a.i"
        CEA_3DLOOKUP();
    } else {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sub_a.i"
        CEA_3DLOOKUP();
    }
}
f64 cea_gamma(f64 P0_cc, f64 ofr, f64 AR) {
    if (AR == 0.0) {
        /* evenly-spaced flattened (C-ordered) 2D LUT */
        #include "tbl/cea_cc_gamma.i"
        CEA_2DLOOKUP();
    } else if (AR >= 1.0) {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sup_gamma.i"
        CEA_3DLOOKUP();
    } else {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sub_gamma.i"
        CEA_3DLOOKUP();
    }
}
f64 cea_cp(f64 P0_cc, f64 ofr, f64 AR) {
    if (AR == 0.0) {
        /* evenly-spaced flattened (C-ordered) 2D LUT */
        #include "tbl/cea_cc_cp.i"
        CEA_2DLOOKUP();
    } else if (AR >= 1.0) {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sup_cp.i"
        CEA_3DLOOKUP();
    } else {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sub_cp.i"
        CEA_3DLOOKUP();
    }
}
f64 cea_mu(f64 P0_cc, f64 ofr, f64 AR) {
    if (AR == 0.0) {
        /* evenly-spaced flattened (C-ordered) 2D LUT */
        #include "tbl/cea_cc_mu.i"
        CEA_2DLOOKUP();
    } else if (AR >= 1.0) {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sup_mu.i"
        CEA_3DLOOKUP();
    } else {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sub_mu.i"
        CEA_3DLOOKUP();
    }
}
f64 cea_Pr(f64 P0_cc, f64 ofr, f64 AR) {
    if (AR == 0.0) {
        /* evenly-spaced flattened (C-ordered) 2D LUT */
        #include "tbl/cea_cc_Pr.i"
        CEA_2DLOOKUP();
    } else if (AR >= 1.0) {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sup_Pr.i"
        CEA_3DLOOKUP();
    } else {
        /* evenly-spaced flattened (C-ordered) 3D LUT */
        #include "tbl/cea_sub_Pr.i"
        CEA_3DLOOKUP();
    }
}


#undef CEA_2DLOOKUP
#undef CEA_3DLOOKUP
