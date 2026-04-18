#include "cea.h"

#include "assertion.h"
#include "maths.h"


#define CEA_2DLOOKUP() do {                                                 \
        f64 x = P0_cc*1e-6;                                                 \
        f64 y = ofr;                                                        \
        assert(XLO <= x && x <= XHI, "approximation input oob: x=%g", x);   \
        assert(YLO <= y && y <= YHI, "approximation input oob: y=%g", y);   \
        f64 t = (x - XLO) / (XHI - XLO);                                    \
        f64 s = (y - YLO) / (YHI - YLO);                                    \
        t *= XLEN - 1;                                                      \
        s *= YLEN - 1;                                                      \
        i32 i = min(max((i32)t, 0), XLEN - 2);                              \
        i32 j = min(max((i32)s, 0), YLEN - 2);                              \
        t -= i;                                                             \
        s -= j;                                                             \
        f64 v00 = tbl[YLEN*i + j];                                          \
        f64 v01 = tbl[YLEN*i + j + 1];                                      \
        f64 v10 = tbl[YLEN*(i + 1) + j];                                    \
        f64 v11 = tbl[YLEN*(i + 1) + j + 1];                                \
        f64 v0 = v00 + s*(v01 - v00);                                       \
        f64 v1 = v10 + s*(v11 - v10);                                       \
        f64 v = v0 + t*(v1 - v0);                                           \
        assert(notnan(v), "approximation input oob: x=%g, y=%g", x, y);     \
        return v;                                                           \
    } while (0)

f64 cea_Isp(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.018% */
    /*   rel 0.0518% */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_Isp.i"
    CEA_2DLOOKUP();
}


f64 cea_T0_cc(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.0201% */
    /*   rel 0.0405% */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_T_cc.i"
    CEA_2DLOOKUP();
}

f64 cea_rho0_cc(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.0534% */
    /*   rel 0.104% */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_rho_cc.i"
    CEA_2DLOOKUP();
}


f64 cea_gamma_tht(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.0109% */
    /*   rel 0.125% */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_gamma_tht.i"
    CEA_2DLOOKUP();
}

f64 cea_Mw_tht(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.00724% */
    /*   rel 0.016% */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_Mw_tht.i"
    CEA_2DLOOKUP();
}


f64 cea_cp_cc(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.0763% */
    /*   rel 0.041% */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_cp_cc.i"
    CEA_2DLOOKUP();
}

f64 cea_cp_tht(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.0877% */
    /*   rel 0.0459% */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_cp_tht.i"
    CEA_2DLOOKUP();
}

f64 cea_cp_midm(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.0945% */
    /*   rel 0.0506% */
    /* also requires: */
    /*   'M_midm' as: lerp(1, M_exit, 0.2) */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_cp_midm.i"
    CEA_2DLOOKUP();
}

f64 cea_cp_exit(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 2.29% */
    /*   rel 1.1% */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_cp_exit.i"
    CEA_2DLOOKUP();
}


f64 cea_mu_cc(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.0147% */
    /*   rel 0.0353% */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_mu_cc.i"
    CEA_2DLOOKUP();
}

f64 cea_mu_tht(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.0149% */
    /*   rel 0.0336% */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_mu_tht.i"
    CEA_2DLOOKUP();
}

f64 cea_mu_midm(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.0125% */
    /*   rel 0.0274% */
    /* also requires: */
    /*   'M_midm' as: lerp(1, M_exit, 0.2) */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_mu_midm.i"
    CEA_2DLOOKUP();
}

f64 cea_mu_exit(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.0798% */
    /*   rel 0.0622% */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_mu_exit.i"
    CEA_2DLOOKUP();
}


f64 cea_Pr_cc(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.0571% */
    /*   rel 0.254% */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_Pr_cc.i"
    CEA_2DLOOKUP();
}

f64 cea_Pr_tht(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.0613% */
    /*   rel 0.29% */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_Pr_tht.i"
    CEA_2DLOOKUP();
}

f64 cea_Pr_midm(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.0602% */
    /*   rel 0.257% */
    /* also requires: */
    /*   'M_midm' as: lerp(1, M_exit, 0.2) */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_Pr_midm.i"
    CEA_2DLOOKUP();
}

f64 cea_Pr_exit(f64 P0_cc, f64 ofr) {
    /* evenly-spaced flattened (C-ordered) 2D LUT */
    /* max error of: */
    /*   abs 0.537% */
    /*   rel 1.93% */
    const f64 XLO = 1.0;
    const f64 XHI = 5.0;
    const f64 YLO = 1.0;
    const f64 YHI = 3.0;
    enum { XLEN = 80,
           YLEN = 80, };
    #include "tbl/cea_Pr_exit.i"
    CEA_2DLOOKUP();
}

#undef CEA_2DLOOKUP



f64 cea_sample(const ceaFit* fit, f64 M) {
    if (M < 1.0)
        return lerp(fit->value_cc, fit->a + fit->b + fit->c, M);
    return (fit->a*M + fit->b)*M + fit->c;
}

#define cea_make_fit(name) do {                                 \
        f64 M_midm = 0.8 + 0.2*M_exit;                          \
        f64 value_cc = GLUE(cea_, name, _cc)(P0_cc, ofr);       \
        f64 value_tht = GLUE(cea_, name, _tht)(P0_cc, ofr);     \
        f64 value_midm = GLUE(cea_, name, _midm)(P0_cc, ofr);   \
        f64 value_exit = GLUE(cea_, name, _exit)(P0_cc, ofr);   \
        fit_quadratic(&fit->a, &fit->b, &fit->c,                \
                1.0, value_tht,                                 \
                M_midm, value_midm,                             \
                M_exit, value_exit                              \
            );                                                  \
        fit->value_cc = value_cc;                               \
    } while (0)
void cea_fit_cp(ceaFit* fit, f64 P0_cc, f64 ofr, f64 M_exit) {
    cea_make_fit(cp);
}
void cea_fit_mu(ceaFit* fit, f64 P0_cc, f64 ofr, f64 M_exit) {
    cea_make_fit(mu);
}
void cea_fit_Pr(ceaFit* fit, f64 P0_cc, f64 ofr, f64 M_exit) {
    cea_make_fit(Pr);
}
#undef cea_make_fit
