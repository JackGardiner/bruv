#pragma once
#include "br.h"

#include "cea.h"


// Straight off the dome.
#define STANDARD_GRAVITY (9.80665) // [m/s^2]
#define GAS_CONSTANT (8.31446261815324) // [J/mol/K]
#define STEFAN_BOLTZMAN_CONSTANT (5.670374419184429e-8) // [W/m^4/K^4]


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
f64 isentropic_M(i32 subsonic, f64 A_on_Astar, const SpecificHeatRatio* shr);

void isentropic_shr_M(SpecificHeatRatio* shr, f64* rstr M, i32 subsonic,
        f64 A_on_Astar, const ceaFit* fit_gamma, f64 seed_gamma);

f64 isentropic_A_on_Astar(f64 M, const SpecificHeatRatio* shr);

f64 isentropic_T_on_T0(f64 M, const SpecificHeatRatio* shr);
f64 isentropic_P_on_P0(f64 M, const SpecificHeatRatio* shr);
f64 isentropic_rho_on_rho0(f64 M, const SpecificHeatRatio* shr);

f64 get_y1M22(f64 M, const SpecificHeatRatio* shr);
f64 isentropicx_T_on_T0(f64 y1M22, const SpecificHeatRatio* shr);
f64 isentropicx_P_on_P0(f64 y1M22, const SpecificHeatRatio* shr);
f64 isentropicx_rho_on_rho0(f64 y1M22, const SpecificHeatRatio* shr);

f64 isentropic_M_from_P_on_P0(f64 P_on_P0, const SpecificHeatRatio* shr);


#define VISCOSITY_DFLT_PL_EXPONENT (0.7)
f64 viscosity_from_power_law(f64 T, f64 Tref, f64 muref, f64 exponent);


f64 friction_factor_haaland(f64 Re, f64 D, f64 eps);
f64 friction_factor_colebrook(f64 Re, f64 D, f64 eps);


f64 nusselt_gnielinski(f64 Re, f64 Pr, f64 ff);
f64 nusselt_dittus_boelter(f64 Re, f64 Pr, i32 is_heating);
f64 nusselt_sieder_tate(f64 Re, f64 Pr, f64 mu_bulk, f64 mu_wall);


f64 mach_for_temperature(f64 T_on_T0, const ceaFit* fit_gamma);
