#include "relations.h"

#include "maths.h"


SpecificHeatRatio* init_shr(SpecificHeatRatio* shr, f64 gamma) {
    assert(within(gamma, 1.0, 8.0), "gamma outside supported range: %g", gamma);
    shr->y = gamma;
    shr->n = 0.5*(gamma + 1.0)/(gamma - 1.0);
    shr->sup_M_seed_n = (1.0 + gamma - pow(gamma, 0.2))/3.0;
    shr->sup_M_seed_m = cbed(gamma) / 64.0;
    shr->sub_M_seed_n = 0.55 + 0.7/(gamma + 2.0);
    shr->sub_M_seed_m = 0.8*gamma + 2.0;
    return shr;
}


static f64 isentropic_M_newton_raphson(f64 M, f64 A_on_Astar,
        const SpecificHeatRatio* shr) {
    // Find M s.t.:
    //  A_on_Astar = isentropic_A_on_Astar(M)
    //  0 = isentropic_A_on_Astar(M) - A_on_Astar
    //  0 = f(M)
    for (i32 iter=0; /* true */; ++iter) {
        enum { MAX_ITERS = 20 };
        f64 term = 2.0/(shr->y + 1.0) + sqed(M)/shr->n/2.0;
        f64 f = pow(term, shr->n) / M - A_on_Astar;
        f64 df = pow(term, shr->n - 1.0)
               * (1.0 - 1.0/sqed(M))
               * 2.0/(shr->y + 1.0);
        if (iterstep(&M, M - f/df) < 1e-8)
            break;
        assert(iter < MAX_ITERS, "failed to converge");
    }
    return M;
}

f64 isentropic_sup_M(f64 A_on_Astar, const SpecificHeatRatio* shr) {
    // Gotta find by inverting the M -> A/Astar relation. I dont believe this has
    // a simple inverse, so we guess then root find.

    assert(A_on_Astar >= 1.0, "A_on_Astar cannot be <1, got %f", A_on_Astar);
    if (A_on_Astar <= 1.0 + 1e-8)
        return 1.0;
    // Pretty mid initial seed smile.
    // https://www.desmos.com/calculator/jvhdhh0q4s
    f64 M = 1.0
          + 0.7*shr->y*pow(A_on_Astar - 1.0, shr->sup_M_seed_n)
          + shr->sup_M_seed_m*(A_on_Astar - 1.0);
    return isentropic_M_newton_raphson(M, A_on_Astar, shr);
}

f64 isentropic_sub_M(f64 A_on_Astar, const SpecificHeatRatio* shr) {
    assert(A_on_Astar >= 1.0, "A_on_Astar cannot be <1, got %f", A_on_Astar);
    if (A_on_Astar <= 1.0 + 1e-8)
        return 1.0;
    // Pretty bloody average seed smillleee.
    // https://www.desmos.com/calculator/xzohdrgmja
    f64 M = 1.0
          / (1.0 + pow(shr->sub_M_seed_m*(A_on_Astar - 1.0), shr->sub_M_seed_n));
    return isentropic_M_newton_raphson(M, A_on_Astar, shr);
}

f64 isentropic_M(i32 subsonic, f64 A_on_Astar,
        const SpecificHeatRatio* shr) {
    return (subsonic)
         ? isentropic_sub_M(A_on_Astar, shr)
         : isentropic_sup_M(A_on_Astar, shr);
}


void isentropic_shr_M(SpecificHeatRatio* shr, f64* rstr M, i32 subsonic,
        f64 A_on_Astar, const ceaFit* fit_gamma, f64 seed_gamma) {
    // gamma and mach are dep. on each other, simple fixed-point iteration to
    // numerically solve. Guess initial seed (throat is a good guess).
    init_shr(shr, seed_gamma);
    *M = isentropic_M(subsonic, A_on_Astar, shr);
    for (i32 iter=0; /* true */; ++iter) {
        enum { MAX_ITERS = 100 };

        f64 gamma = cea_sample(fit_gamma, *M);
        init_shr(shr, gamma);
        f64 new_M = isentropic_M(subsonic, A_on_Astar, shr);
        if (iterstep(M, new_M) < 1e-8)
            break;
        assert(iter < MAX_ITERS, "failed to converge");
    }
}


f64 isentropic_A_on_Astar(f64 M, const SpecificHeatRatio* shr) {
    return pow(2.0/(shr->y + 1.0) + sqed(M)/shr->n/2.0, shr->n) / M;
}

f64 isentropic_T_on_T0(f64 M, const SpecificHeatRatio* shr) {
    return 1.0 / (1.0 + (shr->y - 1.0)/2.0 * sqed(M));
}
f64 isentropic_P_on_P0(f64 M, const SpecificHeatRatio* shr) {
    return pow(1.0 + (shr->y - 1.0)/2.0 * sqed(M), shr->y/(1.0 - shr->y));
}
f64 isentropic_rho_on_rho0(f64 M, const SpecificHeatRatio* shr) {
    return pow(1.0 + (shr->y - 1.0)/2.0 * sqed(M), 1.0/(1.0 - shr->y));
}

f64 get_y1M22(f64 M, const SpecificHeatRatio* shr) {
    return 0.5 * (shr->y - 1.0) * sqed(M);
}
f64 isentropicx_T_on_T0(f64 y1M22, const SpecificHeatRatio* shr) {
    (void)shr;
    return 1.0 / (1.0 + y1M22);
}
f64 isentropicx_P_on_P0(f64 y1M22, const SpecificHeatRatio* shr) {
    return pow(1.0 + y1M22, shr->y/(1.0 - shr->y));
}
f64 isentropicx_rho_on_rho0(f64 y1M22, const SpecificHeatRatio* shr) {
    return pow(1.0 + y1M22, 1.0/(1.0 - shr->y));
}

f64 isentropic_M_from_P_on_P0(f64 P_on_P0, const SpecificHeatRatio* shr) {
    return sqrt(2.0/(shr->y - 1.0)*(pow(P_on_P0, (1.0 - shr->y)/shr->y) - 1.0));
}


f64 viscosity_from_power_law(f64 T, f64 Tref, f64 muref, f64 exponent) {
    return muref * pow(T/Tref, exponent);
}


f64 friction_factor_haaland(f64 Re, f64 D, f64 eps) {
    if (eps == 0.0)
        return 1.0/sqed(1.82*LOG10TWO*log2(Re) - 1.64);
    return 1.0/sqed(-1.8*LOG10TWO*log2(pow(eps/D/3.7, 1.11) + 6.9/Re));
}

f64 friction_factor_colebrook(f64 Re, f64 D, f64 eps) {
    if (eps == 0.0)
        return 1.0/sqed(1.82*LOG10TWO*log2(Re) - 1.64);

    f64 ff = friction_factor_haaland(Re, D, eps); // guess.
    for (i32 iter=0; /* true */; ++iter) {
        enum { MAX_ITERS = 100 };
        f64 x = -2.0*LOG10TWO*log2(eps/3.71*D) + 2.52/(Re*sqrt(ff));
        if (iterstep(&ff, 1.0 / sqed(x)) < 1e-8)
            break;
    }
    return ff;
}


f64 nusselt_gnielinski(f64 Re, f64 Pr, f64 ff) {
    return 0.125*ff*(Re - 1000.0)*Pr
         / (1.0 + 12.7*sqrt(0.125*ff)*(cbrt(sqed(Pr)) - 1.0));
}

f64 nusselt_dittus_boelter(f64 Re, f64 Pr, i32 is_heating) {
    f64 n = (is_heating) ? 0.4 : 0.3;
    return 0.023 * pow(Re, 0.8) * pow(Pr, n);
}
