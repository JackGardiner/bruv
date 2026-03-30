#include "cea.h"

f64 cea_T0_cc(f64 P0_cc, f64 ofr) {
    /* max error of: */
    /*    abs 1.02% */
    /*    rel 2.51% */
    /* requires: */
    /*    x in [1.0, 5.0] */
    /*    y in [1.0, 3.0] */
    f64 x1 = P0_cc*1e-6;
    f64 y1 = ofr;
    assert(1.0 <= x1 && x1 <= 5.0, "approximation input oob: x=%g", x1);
    assert(1.0 <= y1 && y1 <= 3.0, "approximation input oob: y=%g", y1);
    f64 x2 = x1*x1;
    f64 x1y1 = x1*y1;
    f64 y2 = y1*y1;
    f64 n1 = +123.34625302167873;
    f64 n3 = -32.2781058175994;
    f64 n5 = -2255.901659607415;
    f64 d0 = -1.1127707856907334;
    f64 d3 = -0.013832444650478096;
    f64 d4 = +0.04998746632810915;
    f64 d5 = -0.9175741114603156;
    f64 Num = n1*x1 + n3*x2 + n5*y2;
    f64 Den = d0 + y1 + d3*x2 + d4*x1y1 + d5*y2;
    return Num / Den;
}

f64 cea_gamma_tht(f64 P0_cc, f64 ofr) {
    /* max error of: */
    /*    abs 0.106% */
    /*    rel 1.31% */
    /* requires: */
    /*    x in [1.0, 5.0] */
    /*    y in [1.0, 3.0] */
    f64 x1 = P0_cc*1e-6;
    f64 y1 = ofr;
    assert(1.0 <= x1 && x1 <= 5.0, "approximation input oob: x=%g", x1);
    assert(1.0 <= y1 && y1 <= 3.0, "approximation input oob: y=%g", y1);
    f64 x2 = x1*x1;
    f64 x1y1 = x1*y1;
    f64 y2 = y1*y1;
    f64 x2y1 = x1y1*x1;
    f64 x1y2 = x1y1*y1;
    f64 y3 = y2*y1;
    f64 n0 = +32.82147955140555;
    f64 n1 = +1.3228894091694776;
    f64 n2 = -42.33000446177469;
    f64 n3 = -0.03240937527988038;
    f64 n4 = -0.6575335726818564;
    f64 n5 = +15.259451980483384;
    f64 d0 = +24.56210633407845;
    f64 d2 = -30.732150194871508;
    f64 d3 = -0.021947960054804604;
    f64 d4 = -0.4581261217396036;
    f64 d5 = +10.304945776837606;
    f64 d7 = -0.0028426519897806657;
    f64 d8 = -0.019208001126138557;
    f64 d9 = +0.44738363922744784;
    f64 Num = n0 + n1*x1 + n2*y1 + n3*x2 + n4*x1y1 + n5*y2;
    f64 Den = d0 + x1 + d2*y1 + d3*x2 + d4*x1y1 + d5*y2 + d7*x2y1 + d8*x1y2
            + d9*y3;
    return Num / Den;
}

f64 cea_Mw_tht(f64 P0_cc, f64 ofr) {
    /* max error of: */
    /*    abs 0.662% */
    /*    rel 1.18% */
    /* requires: */
    /*    x in [1.0, 5.0] */
    /*    y in [1.0, 3.0] */
    f64 x1 = P0_cc*1e-6;
    f64 y1 = ofr;
    assert(1.0 <= x1 && x1 <= 5.0, "approximation input oob: x=%g", x1);
    assert(1.0 <= y1 && y1 <= 3.0, "approximation input oob: y=%g", y1);
    f64 x2 = x1*x1;
    f64 x1y1 = x1*y1;
    f64 y2 = y1*y1;
    f64 d0 = +35.22029366664648;
    f64 d1 = +0.009419396565043136;
    f64 d2 = +22.17612989121752;
    f64 d3 = +0.0479380554321869;
    f64 d4 = -0.29905846058463004;
    f64 d5 = +1.4811694084541214;
    f64 Num = y1;
    f64 Den = d0 + d1*x1 + d2*y1 + d3*x2 + d4*x1y1 + d5*y2;
    return Num / Den;
}

f64 cea_Isp(f64 P0_cc, f64 ofr) {
    /* max error of: */
    /*    abs 1.07% */
    /*    rel 2.62% */
    /* requires: */
    /*    x in [1.0, 5.0] */
    /*    y in [1.0, 3.0] */
    f64 x1 = P0_cc*1e-6;
    f64 y1 = ofr;
    assert(1.0 <= x1 && x1 <= 5.0, "approximation input oob: x=%g", x1);
    assert(1.0 <= y1 && y1 <= 3.0, "approximation input oob: y=%g", y1);
    f64 x1y1 = x1*y1;
    f64 x1y2 = x1y1*y1;
    f64 x1y3 = x1y2*y1;
    f64 n1 = -1.8846140475220607;
    f64 n2 = +3.042853779946767;
    f64 n4 = +16.27926627454528;
    f64 n8 = -7.118937069689952;
    f64 d0 = +0.006156788721733174;
    f64 d1 = +0.032259388948264474;
    f64 d2 = +0.017561434398487417;
    f64 Num = n1*x1 + n2*y1 + n4*x1y1 + n8*x1y2 + x1y3;
    f64 Den = d0 + d1*x1 + d2*y1;
    return Num / Den;
}
