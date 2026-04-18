#pragma once
#include "br.h"

// NASA-CEA approximations.

f64 cea_Isp(f64 P0_cc, f64 ofr);

f64 cea_T0_cc(f64 P0_cc, f64 ofr);
f64 cea_rho0_cc(f64 P0_cc, f64 ofr);

f64 cea_gamma_tht(f64 P0_cc, f64 ofr);
f64 cea_Mw_tht(f64 P0_cc, f64 ofr);

f64 cea_cp_cc(f64 P0_cc, f64 ofr);
f64 cea_cp_tht(f64 P0_cc, f64 ofr);
f64 cea_cp_lowm(f64 P0_cc, f64 ofr);
f64 cea_cp_midm(f64 P0_cc, f64 ofr);
f64 cea_cp_exit(f64 P0_cc, f64 ofr);

f64 cea_mu_cc(f64 P0_cc, f64 ofr);
f64 cea_mu_tht(f64 P0_cc, f64 ofr);
f64 cea_mu_lowm(f64 P0_cc, f64 ofr);
f64 cea_mu_midm(f64 P0_cc, f64 ofr);
f64 cea_mu_exit(f64 P0_cc, f64 ofr);

f64 cea_Pr_cc(f64 P0_cc, f64 ofr);
f64 cea_Pr_tht(f64 P0_cc, f64 ofr);
f64 cea_Pr_lowm(f64 P0_cc, f64 ofr);
f64 cea_Pr_midm(f64 P0_cc, f64 ofr);
f64 cea_Pr_exit(f64 P0_cc, f64 ofr);


// Also supply mach-property relations which allow property quering along the
// chamber/nozzle.

typedef struct ceaFit {
    f64 value_cc;
    f64 a;
    f64 b;
    f64 c;
    f64 d;
} ceaFit;
f64 cea_sample(const ceaFit* fit, f64 M);

void cea_fit_cp(ceaFit* fit, f64 P0_cc, f64 ofr, f64 M_exit);
void cea_fit_mu(ceaFit* fit, f64 P0_cc, f64 ofr, f64 M_exit);
void cea_fit_Pr(ceaFit* fit, f64 P0_cc, f64 ofr, f64 M_exit);
