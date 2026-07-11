#pragma once
#include "br.h"

// NASA-CEA approximations.

#define CEA_AR_stag (0.0)
#define CEA_AR_tht (1.0)
#define CEA_AR_min (0.02)
#define CEA_AR_max (10.0)

f64 cea_perfexp_AEAT(f64 P0_cc, f64 ofr);

f64 cea_Ivac(f64 P0_cc, f64 ofr, f64 AR);
f64 cea_T(f64 P0_cc, f64 ofr, f64 AR);
f64 cea_P(f64 P0_cc, f64 ofr, f64 AR);
f64 cea_rho(f64 P0_cc, f64 ofr, f64 AR);
f64 cea_M(f64 P0_cc, f64 ofr, f64 AR);
f64 cea_a(f64 P0_cc, f64 ofr, f64 AR);
f64 cea_gamma(f64 P0_cc, f64 ofr, f64 AR);
f64 cea_cp(f64 P0_cc, f64 ofr, f64 AR);
f64 cea_mu(f64 P0_cc, f64 ofr, f64 AR);
f64 cea_Pr(f64 P0_cc, f64 ofr, f64 AR);
