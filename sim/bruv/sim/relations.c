#include "relations.h"


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
    const int MAX_ITERS = 20;
    for (i32 iter=0; iter<MAX_ITERS; ++iter) {
        f64 term = 2.0/(shr->y + 1.0) + sqed(M)/shr->n/2.0;
        f64 f = pow(term, shr->n) / M - A_on_Astar;
        f64 df = pow(term, shr->n - 1.0)
               * (1.0 - 1.0/sqed(M))
               * 2.0/(shr->y + 1.0);
        float DM = -f/df;
        M += DM;
        if (abs(DM) < 1e-8)
            break;
        assert(iter != MAX_ITERS - 1, "failed to converge");
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


f64 isentropic_M_from_P_on_P0(f64 P_on_P0, const SpecificHeatRatio* shr) {
    return sqrt(2.0/(shr->y - 1.0)*(pow(P_on_P0, (1.0 - shr->y)/shr->y) - 1.0));
}
