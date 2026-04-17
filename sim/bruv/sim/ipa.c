#include "ipa.h"


f64 ipa_rho(f64 T, f64 P) {
    /* max error of: */
    /*    abs 1.64% */
    /*    rel 2.25% */
    /* requires: */
    /*    x in [250.0, 500.0] */
    /*    y in [2.0, 7.0] */
    /*    x < (y - 1.41) / (1.97e-3 * y - 2.582e-3) */
    f64 x1 = T;
    f64 y1 = P*1e-6;
    assert(250.0 <= x1 && x1 <= 500.0, "approximation input oob: x=%g", x1);
    assert(2.0 <= y1 && y1 <= 7.0, "approximation input oob: y=%g", y1);
    assert(x1 < (y1 - 1.41) / (1.97e-3 * y1 - 2.582e-3),
            "approximation input oob: x=%g (non-liquid)", x1);
    f64 x2 = x1*x1;
    f64 x1y1 = x1*y1;
    f64 n0 = +282638.5235314691;
    f64 n1 = -519.2100943026779;
    f64 n2 = +1445.7674253372486;
    f64 d0 = +264.5013494141863;
    f64 d1 = -0.21688649377215793;
    f64 d3 = -0.0004071782624757741;
    f64 d4 = +0.0025435407387404746;
    f64 Num = n0 + n1*x1 + n2*y1;
    f64 Den = d0 + d1*x1 + y1 + d3*x2 + d4*x1y1;
    return Num / Den;
}

f64 ipa_cp(f64 T, f64 P) {
    /* max error of: */
    /*    abs 1.2% */
    /*    rel 2.07% */
    /* requires: */
    /*    x in [250.0, 500.0] */
    /*    y in [2.0, 7.0] */
    /*    x < (y - 1.41) / (1.97e-3 * y - 2.582e-3) */
    f64 x1 = T;
    f64 y1 = P*1e-6;
    assert(250.0 <= x1 && x1 <= 500.0, "approximation input oob: x=%g", x1);
    assert(2.0 <= y1 && y1 <= 7.0, "approximation input oob: y=%g", y1);
    assert(x1 < (y1 - 1.41) / (1.97e-3 * y1 - 2.582e-3),
            "approximation input oob: x=%g (non-liquid)", x1);
    f64 x2 = x1*x1;
    f64 x3 = x2*x1;
    f64 x4 = x3*x1;
    f64 c0 = +31604.515041661092;
    f64 c1 = -360.8686557046566;
    f64 c3 = +1.5696639967203143;
    f64 c6 = -0.002879064877462158;
    f64 c10 = +1.9423751842834088e-06;
    return c0 + c1*x1 + c3*x2 + c6*x3 + c10*x4;
}

f64 ipa_mu(f64 T, f64 P) {
    /* max error of: */
    /*    abs 5.63% */
    /*    rel 0.099% */
    /* requires: */
    /*    x in [250.0, 500.0] */
    /*    y in [2.0, 7.0] */
    /*    x < (y - 1.41) / (1.97e-3 * y - 2.582e-3) */
    f64 x1 = T;
    f64 y1 = P*1e-6;
    assert(250.0 <= x1 && x1 <= 500.0, "approximation input oob: x=%g", x1);
    assert(2.0 <= y1 && y1 <= 7.0, "approximation input oob: y=%g", y1);
    assert(x1 < (y1 - 1.41) / (1.97e-3 * y1 - 2.582e-3),
            "approximation input oob: x=%g (non-liquid)", x1);
    f64 x2 = x1*x1;
    f64 x1y1 = x1*y1;
    f64 x3 = x2*x1;
    f64 n0 = -225.29047222582432;
    f64 n1 = +1.292043069018389;
    f64 n3 = -0.0026479876866582577;
    f64 n4 = -0.0006034661397471636;
    f64 n6 = +1.8576905024300686e-06;
    f64 d0 = -14660.696188283917;
    f64 d6 = -0.003285797777513892;
    f64 Num = n0 + n1*x1 + n3*x2 + n4*x1y1 + n6*x3;
    f64 Den = d0 + x2 + d6*x3;
    return Num / Den;
}

f64 ipa_k(f64 T, f64 P) {
    /* max error of: */
    /*    abs 0.787% */
    /*    rel 2.49% */
    /* requires: */
    /*    x in [250.0, 500.0] */
    /*    y in [2.0, 7.0] */
    /*    x < (y - 1.41) / (1.97e-3 * y - 2.582e-3) */
    f64 x1 = T;
    f64 y1 = P*1e-6;
    assert(250.0 <= x1 && x1 <= 500.0, "approximation input oob: x=%g", x1);
    assert(2.0 <= y1 && y1 <= 7.0, "approximation input oob: y=%g", y1);
    assert(x1 < (y1 - 1.41) / (1.97e-3 * y1 - 2.582e-3),
            "approximation input oob: x=%g (non-liquid)", x1);
    f64 x2 = x1*x1;
    f64 n0 = -125331.8756575357;
    f64 d0 = -470179.00892145606;
    f64 d1 = -1857.0665848655822;
    f64 d2 = +3376.790934356056;
    f64 Num = n0;
    f64 Den = d0 + d1*x1 + d2*y1 + x2;
    return Num / Den;
}
