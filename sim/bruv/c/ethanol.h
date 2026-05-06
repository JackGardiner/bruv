#pragma once
#include "br.h"

// CoolProp ethanol approximations.

#define ETHANOL_MIN_T (250.0)
f64 ethanol_max_T(f64 P);
#define ETHANOL_MIN_P (2.0e6)
#define ETHANOL_MAX_P (7.0e6)

f64 ethanol_rho(f64 T, f64 P);
f64 ethanol_cp(f64 T, f64 P);
f64 ethanol_mu(f64 T, f64 P);
f64 ethanol_k(f64 T, f64 P);
