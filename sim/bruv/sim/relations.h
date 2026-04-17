#pragma once
#include "br.h"


// Straight off the dome.
#define GAS_CONSTANT (8.31446261815324) // [J/mol/K]
#define STANDARD_GRAVITY (9.80665) // [m/s^2]


typedef struct SpecificHeatRatio {
    f64 y;
    f64 n; /* (y + 1)/(y - 1)/2 */
    f64 sup_M_seed_n; /* (1 + y - y^0.2)/3 */
    f64 sup_M_seed_m; /* y^3 / 64 */
    f64 sub_M_seed_n; /* 0.55 + 0.7/(y + 2) */
    f64 sub_M_seed_m; /* 0.8*y + 2 */
} SpecificHeatRatio;
#define get_shr(args...) ( init_shr(&(SpecificHeatRatio){0}, args) )
SpecificHeatRatio* init_shr(SpecificHeatRatio* shr, f64 gamma);


f64 isentropic_sup_M(f64 A_on_Astar, const SpecificHeatRatio* shr);
f64 isentropic_sub_M(f64 A_on_Astar, const SpecificHeatRatio* shr);

f64 isentropic_A_on_Astar(f64 M, const SpecificHeatRatio* shr);

f64 isentropic_T_on_T0(f64 M, const SpecificHeatRatio* shr);
f64 isentropic_P_on_P0(f64 M, const SpecificHeatRatio* shr);
f64 isentropic_rho_on_rho0(f64 M, const SpecificHeatRatio* shr);

f64 isentropic_M_from_P_on_P0(f64 P_on_P0, const SpecificHeatRatio* shr);
