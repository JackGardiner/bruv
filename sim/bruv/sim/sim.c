#include "sim.h"

#include "assert.h"
#include "maths.h"



// Nozzle diverging and exit angles extrapolated from data in nasa paper:
// https://ntrs.nasa.gov/citations/19770009165
// https://www.desmos.com/3d/lx2qg53joj

#define NZL_min_AEAT (2.0) // [-]
#define NZL_max_AEAT (12.0) // [-]
#define NZL_min_NLF (0.6) // [-]
#define NZL_max_NLF (1.0) // [-]

#define NZL_IN_AEAT(AEAT) (within((AEAT), NZL_min_AEAT, NZL_max_AEAT))
#define NZL_IN_NLF(NLF) (within((NLF), NZL_min_NLF, NZL_max_NLF))

#define NZL_ASSERT_IN_AEATNLF(AEAT, NLF) do {                           \
        assert(NZL_IN_AEAT((AEAT)), "AEAT=%f, NLF=%f", (AEAT), (NLF));  \
        assert(NZL_IN_NLF((NLF)), "AEAT=%f, NLF=%f", (AEAT), (NLF));    \
    } while (0)

static f64 nzl_phi_div(f64 AEAT, f64 NLF) {
    NZL_ASSERT_IN_AEATNLF(AEAT, NLF);
    f64 x = log2(AEAT);
    f64 y = NLF;
    f64 A;
    f64 B;
    f64 C;
    f64 D;
    f64 E;
    f64 F;
    f64 G;
    if (NLF >= 0.9) {
        A = -73.576668144;
        B = 2.0140420776;
        C = 89.1204667856;
        D = -278.203452801;
        E = 24.1209238324;
        F = 344.177751142;
        G = -24.3867292652;
    } else if (NLF >= 0.8) {
        A = -36.7821906186;
        B = 5.44659097911;
        C = 56.9174532511;
        D = -159.494910997;
        E = 19.8640728454;
        F = 253.581035759;
        G = -16.8130123402;
    } else if (NLF >= 0.7) {
        A = 31.6379252304;
        B = 55.8899435791;
        C = 59.7342379608;
        D = -0.628213679815;
        E = 31.902201812;
        F = 492.781259534;
        G = 32.8799713668;
    } else {
        A = -11.8198005202;
        B = 45.2314009746;
        C = 102.053219128;
        D = -161.375493686;
        E = 38.7116965704;
        F = 629.775421116;
        G = 8.3754483057;
    }
    return (A + B*x + C*y + x*y)
         / (D + E*x + F*y + G*x*y);
}

static f64 nzl_phi_exit(f64 AEAT, f64 NLF) {
    NZL_ASSERT_IN_AEATNLF(AEAT, NLF);
    f64 x = log2(AEAT);
    f64 y = NLF;
    f64 A;
    f64 B;
    f64 C;
    f64 D;
    f64 E;
    f64 F;
    f64 G;
    if (NLF >= 0.9) {
        A = -77.1832953717;
        B = 4.89002019523;
        C = 111.684097893;
        D = 108.741300367;
        E = -457.430200479;
        F = -128.559316968;
        G = 611.611528301;
    } else if (NLF >= 0.8) {
        A = 85.2862055798;
        B = -0.0581506237535;
        C = -90.9930605548;
        D = 189.894422987;
        E = 59.9863440541;
        F = -212.118540681;
        G = -51.6239165065;
    } else if (NLF >= 0.7) {
        A = -136.251317086;
        B = 2.56249489274;
        C = 241.089075404;
        D = -239.854862081;
        E = -270.48519269;
        F = 414.264012684;
        G = 443.983190791;
    } else {
        A = -68.1116220938;
        B = 1.75535969394;
        C = 132.256411039;
        D = -160.591697636;
        E = -60.2164539955;
        F = 283.313697674;
        G = 129.355138767;
    }
    return (A + B*x + C*y + x*y)
         / (D + E*x + F*y + G*x*y);
}



/*
Chamber contour same as geom-side.
master desie: https://www.desmos.com/calculator/lmik9lpejc
Consider the revolved contour of the chamber interoir:
 _______
        '',,           P -,
            '\             o  ,,,-----
      CC      ',         ,-'''
                '-.,__,-'      EXHAUST

 -------------------------------------
 '      '   ' '      ' '             '
 0      1   2 3      4 5             6

define a point P which is the intersection of the tangents at point 5 and 6.
all other points lie on the contour at the z offset indicated.

The contour is made from the following sections:
    [cc:]
0-1 line
    [converging:]
1-2 circular arc
2-3 line
3-4 circular arc
    [diverging:]
4-5 circular arc
5-6 rotated parabolic arc
*/
static void cnt_init(simState* s) {
    s->R_exit = s->R_tht * sqrt(s->AEAT);
    s->phi_div = nzl_phi_div(s->AEAT, s->NLF);
    s->phi_exit = nzl_phi_exit(s->AEAT, s->NLF);

    s->cnt_r_conv = 1.5*s->R_tht;

    s->cnt_z0 = 0;
    s->cnt_r0 = s->R_cc;

    s->cnt_z1 = s->L_cc;
    s->cnt_r1 = s->R_cc;

    s->cnt_z2 = s->cnt_z1 - s->cnt_r_conv*sin(s->phi_conv);
    s->cnt_r2 = s->R_cc - s->cnt_r_conv*(1 - cos(s->phi_conv));

    s->cnt_r3 = s->R_tht * (2.5 - 1.5*cos(s->phi_conv));
    s->cnt_z3 = s->cnt_z2 + (s->cnt_r3 - s->cnt_r2)/tan(s->phi_conv);

    s->cnt_z4 = s->cnt_z3 - 1.5*s->R_tht*sin(s->phi_conv);
    s->cnt_r4 = s->R_tht;

    s->cnt_z5 = s->cnt_z4 + 0.382*s->R_tht*sin(s->phi_div);
    s->cnt_r5 = s->R_tht*(1.382 - 0.382*cos(s->phi_div));

    s->cnt_z6 = s->cnt_z4 + s->NLF*(3.73205080757*s->R_exit
                                  - 3.68347318642*s->R_tht);
    s->cnt_r6 = s->R_exit;

    f64 cnt_zP = (s->cnt_z5*tan(s->phi_div)
                - s->cnt_z6*tan(s->phi_exit)
                + s->cnt_r6
                - s->cnt_r4)
               / (tan(s->phi_div) - tan(s->phi_exit));
    f64 cnt_rP = tan(s->phi_exit)*(cnt_zP - s->cnt_z6) + s->cnt_r6;

    s->cnt_para_az = s->cnt_z5 - 2*cnt_zP + s->cnt_z6;
    s->cnt_para_bz = -2*s->cnt_z6 + 2*cnt_zP;
    s->cnt_para_cz = s->cnt_z6;
    s->cnt_para_ar = s->cnt_r5 - 2*cnt_rP + s->cnt_r6;
    s->cnt_para_br = -2*s->cnt_r6 + 2*cnt_rP;
    s->cnt_para_cr = s->cnt_r6;

    // Check realisable nozzle.

    assert(s->cnt_z0 < s->cnt_z1, "z0=%f, z1=%f", s->cnt_z0, s->cnt_z1);
    assert(s->cnt_z1 < s->cnt_z2, "z1=%f, z2=%f", s->cnt_z1, s->cnt_z2);
    assert(s->cnt_z2 < s->cnt_z3, "z2=%f, z3=%f", s->cnt_z2, s->cnt_z3);
    assert(s->cnt_z3 < s->cnt_z4, "z3=%f, z4=%f", s->cnt_z3, s->cnt_z4);
    assert(s->cnt_z4 < s->cnt_z5, "z4=%f, z5=%f", s->cnt_z4, s->cnt_z5);
    assert(s->cnt_z5 < s->cnt_z6, "z5=%f, z6=%f", s->cnt_z5, s->cnt_z6);
    assert(cnt_zP > s->cnt_z5, "zP=%f, z5=%f", cnt_zP, s->cnt_z5);
    assert(cnt_zP < s->cnt_z6, "zP=%f, z6=%f", cnt_zP, s->cnt_z6);

    assert(nearto(s->cnt_r0, s->cnt_r1), "r0=%f, r1=%f", s->cnt_r0, s->cnt_r1);
    assert(s->cnt_r1 > s->cnt_r2, "r1=%f, r2=%f", s->cnt_r1, s->cnt_r2);
    assert(s->cnt_r2 > s->cnt_r3, "r2=%f, r3=%f", s->cnt_r2, s->cnt_r3);
    assert(s->cnt_r3 > s->cnt_r4, "r3=%f, r4=%f", s->cnt_r3, s->cnt_r4);
    assert(s->cnt_r4 < s->cnt_r5, "r4=%f, r5=%f", s->cnt_r4, s->cnt_r5);
    assert(s->cnt_r5 < s->cnt_r6, "r5=%f, r6=%f", s->cnt_r5, s->cnt_r6);
    assert(cnt_rP > s->cnt_r5, "rP=%f, r5=%f", cnt_rP, s->cnt_r5);
    assert(cnt_rP < s->cnt_r6, "rP=%f, r6=%f", cnt_rP, s->cnt_r6);
}

static f64 cnt_r(simState* s, f64 z) {
    // Note we extend the radius at points 0 and 6 downwards and upwards,
    // respectively.

    if (z <= s->cnt_z1) {
        return s->cnt_r0;
    }
    if (z <= s->cnt_z2) {
        z -= s->cnt_z1;
        return s->cnt_r1 - s->cnt_r_conv + nonhypot(s->cnt_r_conv, z);
    }
    if (z <= s->cnt_z3) {
        z -= s->cnt_z3;
        return tan(s->phi_conv)*z + s->cnt_r3;
    }
    if (z <= s->cnt_z4) {
        z -= s->cnt_z4;
        return 2.5*s->R_tht - nonhypot(1.5*s->R_tht, z);
    }
    if (z <= s->cnt_z5) {
        z -= s->cnt_z4;
        return 1.382*s->R_tht - nonhypot(0.382*s->R_tht, z);
    }
    if (z <= s->cnt_z6) {
        f64 p;
        p = 4*s->cnt_para_az*(s->cnt_para_cz - z);
        p = sqrt(s->cnt_para_bz*s->cnt_para_bz - p);
        p = (-s->cnt_para_bz - p) * 0.5 / s->cnt_para_az;
        return (s->cnt_para_ar*p + s->cnt_para_br)*p + s->cnt_para_cr;
    }
    return s->R_exit;
}


typedef struct SpecificHeatRatio {
    f64 y;
    f64 n; /* (y + 1)/(y - 1)/2 */
    f64 sup_M_seed_n; /* (1 + y - y^0.2)/3 */
    f64 sup_M_seed_m; /* y^3 / 64 */
    f64 sub_M_seed_n; /* 0.55 + 0.7/(y + 2) */
    f64 sub_M_seed_m; /* 0.8*y + 2 */
} SpecificHeatRatio;
static void init_specific_heat_ratio(SpecificHeatRatio* shr, f64 gamma) {
    assert(within(gamma, 1.0, 8.0), "gamma outside supported range: %g", gamma);
    shr->y = gamma;
    shr->n = (gamma + 1.0)/(gamma - 1.0)/2.0;
    shr->sup_M_seed_n = (1.0 + gamma - pow(gamma, 0.2))/3.0;
    shr->sup_M_seed_m = cbed(gamma) / 64.0;
    shr->sub_M_seed_n = 0.55 + 0.7/(gamma + 2.0);
    shr->sub_M_seed_m = 0.8*gamma + 2.0;
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

static f64 isentropic_sup_M(f64 A_on_Astar, const SpecificHeatRatio* shr) {
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

static f64 isentropic_sub_M(f64 A_on_Astar, const SpecificHeatRatio* shr) {
    assert(A_on_Astar >= 1.0, "A_on_Astar cannot be <1, got %f", A_on_Astar);
    if (A_on_Astar <= 1.0 + 1e-8)
        return 1.0;
    // Pretty bloody average seed smillleee.
    // https://www.desmos.com/calculator/xzohdrgmja
    f64 M = 1.0
          / (1.0 + pow(shr->sub_M_seed_m*(A_on_Astar - 1.0), shr->sub_M_seed_n));
    return isentropic_M_newton_raphson(M, A_on_Astar, shr);
}

UNUSED static f64 isentropic_A_on_Astar(f64 M, const SpecificHeatRatio* shr) {
    return pow(2.0/(shr->y + 1.0) + sqed(M)/shr->n/2.0, shr->n) / M;
}

static f64 isentropic_Pstag_on_P(f64 M, const SpecificHeatRatio* shr) {
    return pow(1.0 + (shr->y - 1.0)/2.0 * sqed(M), shr->y/(shr->y - 1.0));
}

static f64 isentropic_Tstag_on_T(f64 M, const SpecificHeatRatio* shr) {
    return 1.0 + (shr->y - 1.0)/2.0 * sqed(M);
}



void sim_execute(simState* rstr s) {
    printf("helloooo c\n");
    cnt_init(s);

    assert(s->out_count > 20, "output contour array is too small (%lld)",
            s->out_count);
    assert(s->out_z, "output contour array Z is null");
    assert(s->out_r, "output contour array R is null");
    assert(s->out_T, "output contour array R is null");
    assert(s->out_P, "output contour array R is null");
    assert(s->out_M, "output contour array R is null");

    SpecificHeatRatio shr;
    init_specific_heat_ratio(&shr, 1.4);

    f64 Tstag = 3000.0;
    f64 Pstag = 35e5;

    for (i64 i=0; i<s->out_count; ++i) {
        f64 z = lerpidx(s->cnt_z0, s->cnt_z6, i, s->out_count);
        f64 r = cnt_r(s, z);
        f64 A_on_Astar = sqed(r) / sqed(s->R_tht);
        f64 M = (z < s->cnt_z4)
              ? isentropic_sub_M(A_on_Astar, &shr)
              : isentropic_sup_M(A_on_Astar, &shr);
        f64 T = Tstag / isentropic_Tstag_on_T(M, &shr);
        f64 P = Pstag / isentropic_Pstag_on_P(M, &shr);
        s->out_z[i] = z;
        s->out_r[i] = r;
        s->out_T[i] = T;
        s->out_P[i] = P;
        s->out_M[i] = M;
    }

    printf("goodbye c :(\n");
}


c_IH sim_interpretation_hash(void) {
    c_IH h = c_ih_initial();
    #define X(name, type, flags)                                \
        h = c_ih_append(h, #name, flags | generic(objof(type)   \
            , f64:  C_F64                                       \
            , i64:  C_I64                                       \
            , f32*: C_PTR_F32                                   \
            , f64*: C_PTR_F64                                   \
            , i8*:  C_PTR_I8                                    \
            , i16*: C_PTR_I16                                   \
            , i32*: C_PTR_I32                                   \
            , i64*: C_PTR_I64                                   \
            , u8*:  C_PTR_U8                                    \
            , u16*: C_PTR_U16                                   \
            , u32*: C_PTR_U32                                   \
            , u64*: C_PTR_U64                                   \
        ));
    SIM_INTERPRETATION
    #undef X
    return h;
}
