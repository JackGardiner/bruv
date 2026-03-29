#include "cea.h"

f64 cea_T_cc(f64 P_cc, f64 ofr) {
    /* max error of: */
    /*    abs 1.02% */
    /*    rel 2.51% */
    /* requires: */
    /*    x in [1.0, 5.0] */
    /*    y in [1.0, 3.0] */
    f64 x1 = P_cc*1e-6;
    f64 y1 = ofr;
    assert(1.0 <= x1 && x1 <= 5.0, "approximation input oob: x=%g", x1);
    assert(1.0 <= y1 && y1 <= 3.0, "approximation input oob: y=%g", y1);
    f64 x2 = x1*x1;
    f64 x1y1 = x1*y1;
    f64 y2 = y1*y1;
    f64 n1 = -0.05467712775792748;
    f64 n3 = +0.014308284185872235;
    f64 d0 = +0.0004932709558257683;
    f64 d2 = -0.00044328172198611327;
    f64 d3 = +6.131665652741771e-06;
    f64 d4 = -2.215852512414299e-05;
    f64 d5 = +0.00040674383761361474;
    f64 Num = n1*x1 + n3*x2 + y2;
    f64 Den = d0 + d2*y1 + d3*x2 + d4*x1y1 + d5*y2;
    return Num / Den;
}

f64 cea_gamma_tht(f64 P_cc, f64 ofr) {
    /* max error of: */
    /*    abs 0.106% */
    /*    rel 1.31% */
    /* requires: */
    /*    x in [1.0, 5.0] */
    /*    y in [1.0, 3.0] */
    f64 x1 = P_cc*1e-6;
    f64 y1 = ofr;
    assert(1.0 <= x1 && x1 <= 5.0, "approximation input oob: x=%g", x1);
    assert(1.0 <= y1 && y1 <= 3.0, "approximation input oob: y=%g", y1);
    f64 x2 = x1*x1;
    f64 x1y1 = x1*y1;
    f64 y2 = y1*y1;
    f64 x2y1 = x1y1*x1;
    f64 x1y2 = x1y1*y1;
    f64 y3 = y2*y1;
    f64 n0 = +2.1508941839670896;
    f64 n1 = +0.08669298012254245;
    f64 n2 = -2.7740182406593723;
    f64 n3 = -0.00212388458948488;
    f64 n4 = -0.04309018267263435;
    f64 d0 = +1.6096318157648224;
    f64 d1 = +0.06553305167549286;
    f64 d2 = -2.0139747678811837;
    f64 d3 = -0.001438316186297209;
    f64 d4 = -0.03002240439731848;
    f64 d5 = +0.6753157963272326;
    f64 d7 = -0.00018628765093960367;
    f64 d8 = -0.0012587586767171725;
    f64 d9 = +0.029318427459950006;
    f64 Num = n0 + n1*x1 + n2*y1 + n3*x2 + n4*x1y1 + y2;
    f64 Den = d0 + d1*x1 + d2*y1 + d3*x2 + d4*x1y1 + d5*y2 + d7*x2y1 + d8*x1y2
            + d9*y3;
    return Num / Den;
    return Num / Den;
}

f64 cea_Mw_tht(f64 P_cc, f64 ofr) {
    /* max error of: */
    /*    abs 0.662% */
    /*    rel 1.18% */
    /* requires: */
    /*    x in [1.0, 5.0] */
    /*    y in [1.0, 3.0] */
    f64 x1 = P_cc*1e-6;
    f64 y1 = ofr;
    assert(1.0 <= x1 && x1 <= 5.0, "approximation input oob: x=%g", x1);
    assert(1.0 <= y1 && y1 <= 3.0, "approximation input oob: y=%g", y1);
    f64 x2 = x1*x1;
    f64 x1y1 = x1*y1;
    f64 y2 = y1*y1;
    f64 d0 = +35.22029369321208;
    f64 d1 = +0.009419404170285828;
    f64 d2 = +22.176129843855225;
    f64 d3 = +0.047938054912658144;
    f64 d4 = -0.29905846342913067;
    f64 d5 = +1.481169424122832;
    f64 Num = y1;
    f64 Den = d0 + d1*x1 + d2*y1 + d3*x2 + d4*x1y1 + d5*y2;
    return Num / Den;
}
