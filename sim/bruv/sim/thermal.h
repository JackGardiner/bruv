#pragma once
#include "br.h"

#include "contour.h"
#include "sim.h"


typedef struct thermalStation {
    f64 T_c;
    f64 P_c;
    f64 T_wg;
    f64 T_wc;
} thermalStation;

i32 thermal_sim(const simState* s, const Contour* cnt, thermalStation* stns,
        i32 N);
