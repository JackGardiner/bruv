#pragma once
#include "br.h"

#include "contour.h"
#include "sim.h"
#include "thermal.h"


typedef struct stressStation {
    struct {
        f64 sigma;
        f64 SF;
    } startup;
    struct {
        f64 sigma;
        f64 SF;
    } firing;
} stressStation;

void stress_sim(const simState* s, const Contour* cnt,
        const thermalStation* thermal_stns, i32 thermal_N, stressStation* stns,
        i32 N);
