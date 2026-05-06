#include "material.h"


f64 CuCr1Zr_Ys(f64 T) {
    f64 Ys = (-2.2847e2*T - 1.3931e5)*T + 2.268e8;
    return max(1e6, Ys);
}

f64 CuCr1Zr_Us(f64 T) {
    f64 Us = -4.2631e5*T + 3.4104e8;
    return max(1e6, Us);
}

f64 CuCr1Zr_E(f64 T) {
    f64 E = (-1.9234e5*T - 2.1233e7)*T + 1.2491e11;
    return max(1e9, E);
}

f64 CuCr1Zr_pois(f64 T) {
    (void)T;
    return 0.38;
}

f64 CuCr1Zr_k(void) {
    return 320.0; // cheeky constant.
}

f64 CuCr1Zr_alpha(f64 T) {
    (void)T;
    return 1.635e-5;
}
