#pragma once
#include "br.h"

#include "sim.h"


f64 nzl_phi_div(f64 AEAT, f64 NLF);
f64 nzl_phi_exit(f64 AEAT, f64 NLF);

typedef struct Contour {
    f64 R_tht;
    f64 R_exit;
    f64 z_tht;
    f64 z_exit;
    f64 phi_conv;
    f64 phi_div;
    f64 phi_exit;

    f64 R_conv;
    f64 tan_phi_conv;
    f64 z0;
    f64 r0;
    f64 z1;
    f64 r1;
    f64 z2;
    f64 r2;
    f64 z3;
    f64 r3;
    f64 z4;
    f64 r4;
    f64 z5;
    f64 r5;
    f64 z6;
    f64 r6;
    f64 para_az;
    f64 para_bz;
    f64 para_cz;
    f64 para_ar;
    f64 para_br;
    f64 para_cr;
} Contour;
Contour* init_cnt(Contour* cnt, f64 R_cc, f64 L_cc, f64 A_tht, f64 AEAT, f64 NLF,
        f64 phi_conv);
#define get_cnt(args...) ( init_cnt(&(Contour){0}, args) )

void cnt_change_length(Contour* cnt, f64 DL_cc);

f64 cnt_r(Contour* cnt, f64 z);
f64 cnt_V_subsonic(Contour* cnt); // volume up-to throat.
