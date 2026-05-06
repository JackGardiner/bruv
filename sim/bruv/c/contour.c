#include "contour.h"

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

f64 nzl_phi_div(f64 AEAT, f64 NLF) {
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

f64 nzl_phi_exit(f64 AEAT, f64 NLF) {
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

Contour* init_cnt(Contour* cnt, f64 R_cc, f64 L_cc, f64 A_tht, f64 AEAT, f64 NLF,
        f64 phi_conv, f64 th_iw, f64 helix_angle, f64 th_chnl, i64 no_chnl,
        f64 prop_chnl) {

    cnt->R_tht = sqrt(A_tht/PI);
    cnt->R_exit = cnt->R_tht * sqrt(AEAT);
    cnt->phi_conv = phi_conv;
    cnt->phi_div = nzl_phi_div(AEAT, NLF);
    cnt->phi_exit = nzl_phi_exit(AEAT, NLF);

    cnt->th_iw = th_iw;
    cnt->helix_angle = helix_angle;
    cnt->th_chnl = th_chnl;
    cnt->no_chnl = (f64)no_chnl;
    cnt->prop_chnl = prop_chnl;

    cnt->R_conv = 1.5*cnt->R_tht;
    cnt->tan_phi_conv = tan(phi_conv);

    cnt->z0 = 0;
    cnt->r0 = R_cc;

    cnt->z1 = L_cc;
    cnt->r1 = R_cc;

    cnt->z2 = cnt->z1 - cnt->R_conv*sin(cnt->phi_conv);
    cnt->r2 = R_cc - cnt->R_conv*(1 - cos(cnt->phi_conv));

    cnt->r3 = cnt->R_tht * (2.5 - 1.5*cos(cnt->phi_conv));
    cnt->z3 = cnt->z2 + (cnt->r3 - cnt->r2)/tan(cnt->phi_conv);

    cnt->z4 = cnt->z3 - 1.5*cnt->R_tht*sin(cnt->phi_conv);
    cnt->r4 = cnt->R_tht;

    cnt->z5 = cnt->z4 + 0.382*cnt->R_tht*sin(cnt->phi_div);
    cnt->r5 = cnt->R_tht*(1.382 - 0.382*cos(cnt->phi_div));

    cnt->z6 = cnt->z4 + NLF*(3.73205080757*cnt->R_exit
                           - 3.68347318642*cnt->R_tht);
    cnt->r6 = cnt->R_exit;

    f64 zP = (cnt->z5*tan(cnt->phi_div)
            - cnt->z6*tan(cnt->phi_exit)
            + cnt->r6
            - cnt->r4)
           / (tan(cnt->phi_div) - tan(cnt->phi_exit));
    f64 rP = tan(cnt->phi_exit)*(zP - cnt->z6) + cnt->r6;

    cnt->para_az = cnt->z5 - 2*zP + cnt->z6;
    cnt->para_bz = -2*cnt->z6 + 2*zP;
    cnt->para_cz = cnt->z6;
    cnt->para_ar = cnt->r5 - 2*rP + cnt->r6;
    cnt->para_br = -2*cnt->r6 + 2*rP;
    cnt->para_cr = cnt->r6;

    cnt->z_tht = cnt->z4;
    cnt->z_exit = cnt->z6;

    // Check realisable nozzle.
    cnt->possible = 1;

    cnt->possible &= (cnt->z0 < cnt->z1);
    cnt->possible &= (cnt->z1 < cnt->z2);
    cnt->possible &= (cnt->z2 < cnt->z3);
    cnt->possible &= (cnt->z3 < cnt->z4);
    cnt->possible &= (cnt->z4 < cnt->z5);
    cnt->possible &= (cnt->z5 < cnt->z6);
    cnt->possible &= (zP > cnt->z5);
    cnt->possible &= (zP < cnt->z6);

    cnt->possible &= (nearto(cnt->r0, cnt->r1));
    cnt->possible &= (cnt->r1 > cnt->r2);
    cnt->possible &= (cnt->r2 > cnt->r3);
    cnt->possible &= (cnt->r3 > cnt->r4);
    cnt->possible &= (cnt->r4 < cnt->r5);
    cnt->possible &= (cnt->r5 < cnt->r6);
    cnt->possible &= (rP > cnt->r5);
    cnt->possible &= (rP < cnt->r6);

    return cnt;
}

void cnt_change_length(Contour* cnt, f64 DL_cc) {
    assert(cnt->z1 + DL_cc > 0.0, "invalid chamber length: DL_cc=%g", DL_cc);
    cnt->z1 += DL_cc;
    cnt->z2 += DL_cc;
    cnt->z3 += DL_cc;
    cnt->z4 += DL_cc;
    cnt->z5 += DL_cc;
    cnt->z6 += DL_cc;

    // Recalc parabolic parameterising parameters (PPP).
    f64 zP = (cnt->z5*tan(cnt->phi_div)
            - cnt->z6*tan(cnt->phi_exit)
            + cnt->r6
            - cnt->r4)
           / (tan(cnt->phi_div) - tan(cnt->phi_exit));

    cnt->para_az = cnt->z5 - 2*zP + cnt->z6;
    cnt->para_bz = -2*cnt->z6 + 2*zP;
    cnt->para_cz = cnt->z6;

    cnt->z_tht = cnt->z4;
    cnt->z_exit = cnt->z6;
}



f64 cnt_r(const Contour* cnt, f64 z) {
    // Note we extend the radius at points 0 and 6 downwards and upwards,
    // respectively.

    if (z <= cnt->z1) {
        return cnt->r0;
    }
    if (z <= cnt->z2) {
        z -= cnt->z1;
        return cnt->r1 - cnt->R_conv + nonhypot(cnt->R_conv, z);
    }
    if (z <= cnt->z3) {
        z -= cnt->z3;
        return cnt->tan_phi_conv*z + cnt->r3;
    }
    if (z <= cnt->z4) {
        z -= cnt->z4;
        return 2.5*cnt->R_tht - nonhypot(1.5*cnt->R_tht, z);
    }
    if (z <= cnt->z5) {
        z -= cnt->z4;
        return 1.382*cnt->R_tht - nonhypot(0.382*cnt->R_tht, z);
    }
    if (z <= cnt->z6) {
        f64 p;
        p = 4*cnt->para_az*(cnt->para_cz - z);
        p = sqrt(cnt->para_bz*cnt->para_bz - p);
        p = (-cnt->para_bz - p) * 0.5 / cnt->para_az;
        return (cnt->para_ar*p + cnt->para_br)*p + cnt->para_cr;
    }
    return cnt->r6;
}

f64 cnt_th_iw(const Contour* cnt, f64 z) {
    f64 prop = 1.0;
    if (z > cnt->z_tht)
        prop *= lerp(1.0, 1.5, invlerp(cnt->z_tht, cnt->z_exit, z));
    return cnt->th_iw * prop;
}
f64 cnt_helix_angle(const Contour* cnt, f64 z) {
    (void)z;
    f64 helix = cnt->helix_angle;
    return helix;
}

f64 cnt_th_chnl(const Contour* cnt, f64 z) {
    f64 th = cnt->th_chnl;
    f64 z_tht = cnt->z_tht;
    return (z > z_tht) ? lerp(0.7*th, th, invlerp(z_tht, cnt->z_exit, z))
         : (z > cnt->z1) ? 0.7*th
         : (z > 0.5*cnt->z1) ? lerp(0.7*th, th, invlerp(cnt->z1, cnt->z1*0.5, z))
         : th;
}

f64 cnt_wi_web(const Contour* cnt, f64 z) {
    f64 r = cnt_r(cnt, z) + cnt->th_iw + 0.5*cnt_th_chnl(cnt, z);
    f64 wi = TWOPI*r/cnt->no_chnl - cnt_wi_chnl(cnt, z);
    assert(wi > 0.0, "z=%g", z);
    return wi;
}
f64 cnt_wi_chnl(const Contour* cnt, f64 z) {
    f64 r = cnt_r(cnt, z) + cnt->th_iw + 0.5*cnt_th_chnl(cnt, z);
    // f64 prop = cnt->prop_chnl;
    // if (z > cnt->z_tht)
    //     prop *= lerp(1, 0.5, invlerp(cnt->z_tht, cnt->z_exit, z));
    return TWOPI*r/cnt->no_chnl * cnt->prop_chnl;
}

f64 cnt_psi_chnl(const Contour* cnt, f64 z) {
    return cnt_wi_chnl(cnt, z) * cos(cnt_helix_angle(cnt, z));
}


f64 cnt_V_subsonic(const Contour* cnt) {
    f64 V = 0.0;

    // Ramblings in here: https://www.desmos.com/calculator/3zfoctnl7z
    // the wolfram alpha queries will cost u extra.

    { /* 0 - 1 [constant] */
        f64 Dz = cnt->z1 - cnt->z0;
        V += PI*sqed(cnt->r0)*Dz;
    }

    { /* 1 - 2 [circular arc] */
        f64 Dz = cnt->z2 - cnt->z1;
        f64 Dr = nonhypot(cnt->R_conv, Dz);
        V += PI * (
            sqed(cnt->R_conv)*(cnt->r1 - cnt->R_conv)*atan(Dz / Dr)
            + Dz*(
                sqed(cnt->R_conv)
                + sqed(cnt->r1 - cnt->R_conv)
                + (cnt->r1 - cnt->R_conv)*Dr
                - sqed(Dz)/3.0
            )
        );
    }

    { /* 2 - 3 [linear] */
        f64 Dz = cnt->z3 - cnt->z2;
        V += PI * Dz * (sqed(cnt->r3)
                      + Dz * (-cnt->r3 * cnt->tan_phi_conv
                            + Dz * sqed(cnt->tan_phi_conv)/3.0));
    }

    { /* 3 - 4 [circular arc] */
        f64 Dz = cnt->z4 - cnt->z3;
        f64 Dr = nonhypot(1.5*cnt->R_tht, Dz);
        V += PI * (
            -5.625*cbed(cnt->R_tht)*atan(Dz/Dr)
            + Dz*(
                8.5*sqed(cnt->R_tht)
                - 2.5*cnt->R_tht*Dr
                - sqed(Dz)/3.0
            )
        );
    }

    // Reached throat.

    return V;
}
