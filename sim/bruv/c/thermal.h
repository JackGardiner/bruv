#pragma once
#include "br.h"

#include "contour.h"
#include "sim.h"


typedef struct thermalStation {
    f64 q;
    f64 h_g;
    f64 h_c;
    f64 vel_c;
    f64 rho_c;
    f64 ff_c;
    f64 Re_c;
    f64 Pr_c;
    f64 T_c;
    f64 P_c;
    f64 T_gw;
    f64 T_pdms;
    f64 T_wg;
    f64 T_wc;
    f64 xtra;
} thermalStation;

i32 thermal_sim(const simState* s, const Contour* cnt, thermalStation* stns,
        i32 N);
