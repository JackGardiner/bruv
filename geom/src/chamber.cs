using static br.Br;
using br;
using TPIAP = TwoPeasInAPod;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using BBox3 = PicoGK.BBox3;

public class Chamber : TPIAP.Pea {

    // Bit how ya going.
    //
    // Typical frame used is cylindrical coordinates, +z vector pointing along
    // axis of symmetry (axial vector) from the injector towards the nozzle,
    // where z=0 is the chamber top plane. Define theta=0 radial-outwards vector
    // to coincide with +x vector and theta=PI_2 radial-outwards vector to
    // coincide with +y vector. Define the +y vector to be vertical upwards when
    // the motor is setup on the (horizontal) thrust stand.

    public required PartMating pm { get; init; }

    public required float L_cc { get; init; }

    public required float AEAT { get; init; }
    public required float R_tht { get; init; }
    public float R_exit => sqrt(AEAT) * R_tht;

    public required float NLF { get; init; }
    public required float phi_conv { get; init; }
    public required float phi_div { get; init; }
    public required float phi_exit { get; init; }

    public required float phi_wid { get; init; }

    public required float th_iw { get; init; }
    public required float th_ow { get; init; }

    public required float helix_angle { get; init; }

    public required int no_web { get; init; }
    public required float th_web { get; init; }
    public required float psi_web { get; init; }
    public float wi_web = NAN;

    public int no_chnl => no_web;
    public float th_chnl => th_web;
    public required float FR_chnl { get; init; }
    public float A_chnl_exit = NAN;
    public float z1_chnl = NAN;
    float wi_chnl(float r) {
        float wi = TWOPI*r/no_web - wi_web;
        assert(wi >= 0f, $"r={r}");
        return wi;
    }

    protected float[] theta_chnl_lookup = [];
    protected float theta_chnl_lookup_zlo = NAN;
    protected float theta_chnl_lookup_zhi = NAN;
    protected float theta_chnl(float z) { // NOTE: not angle wrapped.
        // cheeky lookup table mate. could write em in my sleep.
        z -= theta_chnl_lookup_zlo;
        z /= theta_chnl_lookup_zhi - theta_chnl_lookup_zlo;
        z = clamp(z, 0f, 1f);
        z *= numel(theta_chnl_lookup) - 1;
        int i = clamp(ifloor(z), 0, numel(theta_chnl_lookup) - 2);
        float t = z - i;
        float A = theta_chnl_lookup[i];
        float B = theta_chnl_lookup[i + 1];
        return lerp(A, B, t);
    }
    protected void initialise_chnl() {
        // Get circumferential width rather than normal-to-helix-angle width.
        wi_web = psi_web / cos(helix_angle);


        { // Setup "top" of channel to be the base of the triangular-ish opening.
            // guess then simple fixed-point iteration.
            z1_chnl = cnt_z6 - th_omani - th_chnl - 0.5f*wi_chnl(R_exit) - 1f;
            for (int i=0; true; ++i) {
                float r = cnt_radius_at(z1_chnl, th_iw + th_chnl, false);
                float next_z1_chnl = cnt_z6 - th_omani - th_chnl
                                   - 0.5f*wi_chnl(r) - 1f;
                float Dz = abs(next_z1_chnl - z1_chnl);
                z1_chnl = next_z1_chnl;
                if (Dz < 1e-3f)
                    break;
                assert(i < 100, $"z1_chnl={z1_chnl}, Dz={Dz}");
            }
        }


        // Compute theta channel.

        int N = 2*DIVISIONS;
        theta_chnl_lookup = new float[N];
        // distributed between zlo and zhi, clamped outside.

        float zlo = theta_chnl_lookup_zlo = 0f;
        float zhi = theta_chnl_lookup_zhi = z1_chnl;

        Vec3 prev = NAN3;
        for (int i=0; i<N; ++i) {
            float z = lerp(zlo, zhi, i, N);
            float r = cnt_radius_at(z, th_iw + 0.5f*th_chnl, true);
            float theta = 0f;
            if (i > 0) {
                float prev_theta = argxy(prev);
                Vec3 generatrix = normalise(fromcyl(r, prev_theta, z) - prev);
                Vec3 tangent = fromcyl(1f, prev_theta + PI_2, 0f);
                Vec3 outwards = cross(tangent, generatrix);
                Vec3 dir = rotate(generatrix, outwards, -helix_angle);
                Vec3 curr = Polygon.plane_line_intersection(prev, prev + dir,
                        z*uZ3, uZ3);
                theta = argxy(curr);
            }
            prev = fromcyl(r, theta, z);
            theta_chnl_lookup[i] = theta;

            // Just to check it doesnt shit the bed:
            _ = wi_chnl(r);
        }

        // ensure no full circles between steps.
        for (int i=1; i<N; ++i) {
            float Dtheta = theta_chnl_lookup[i] - theta_chnl_lookup[i - 1];
            Dtheta = wraprad(Dtheta);
            theta_chnl_lookup[i] = theta_chnl_lookup[i - 1] + Dtheta;
        }

        // ease in-out.
        {
            float[] Dthetas = new float[N - 1];
            for (int i=0; i<N - 1; ++i)
                Dthetas[i] = theta_chnl_lookup[i + 1] - theta_chnl_lookup[i];

            float L = 8f;

            int n = 0;
            for (int i=0; i<N; ++i) {
                float z = lerp(zlo, zhi, i, N);
                if (z >= L) {
                    n = i;
                    break;
                }
            }
            for (int i=n - 1; i>-1; --i) {
                float Dtheta = Dthetas[i];
                Dtheta *= i / (float)n;
                theta_chnl_lookup[i] = theta_chnl_lookup[i + 1] - Dtheta;
            }

            n = N - 1;
            for (int i=N - 1; i>-1; --i) {
                float z = lerp(zlo, zhi, i, N);
                if (z <= zhi - L) {
                    n = i;
                    break;
                }
            }
            for (int i=n + 1; i<N; ++i) {
                float Dtheta = Dthetas[i - 1];
                Dtheta *= (N - 1 - i) / (float)(N - n);
                theta_chnl_lookup[i] = theta_chnl_lookup[i - 1] + Dtheta;
            }
        }

        // ensure first channel is at stagnation point of manifold.
        {
            float Dtheta = theta_inlet + PI - theta_chnl(zhi);
            for (int i=0; i<N; ++i)
                theta_chnl_lookup[i] += Dtheta;
        }

        { // Compute cs area of channel at entry point (~nozzle exit).
            float rlo = cnt_radius_at(z1_chnl, th_iw, false);
            float rhi = cnt_radius_at(z1_chnl, th_iw + th_chnl, false);
            // https://www.desmos.com/calculator/c0by5f6cs8
            A_chnl_exit = (rhi - rlo) * (PI/no_web * (rhi + rlo) - wi_web);
        }
    }

    public required float th_imani { get; init; }
    public required float th_omani { get; init; }
    public required float phi_mani { get; init; }

    public required int no_fixt { get; init; }
    public required float wi_fixt { get; init; }
    public required float phi_fixt { get; init; }

    public required string portsize_inlet { get; init; }
    public required float theta_inlet { get; init; }
    public required float phi_inlet { get; init; }
    public required float th_inlet { get; init; }
    public required float FR_inlet { get; init; }
    public Tapping tap_inlet => new(portsize_inlet, printable_dmls)
            { threaded_length = 15f };

    public required int no_tc { get; init; }
    public required string portsize_tc { get; init; }
    public required float th_tc { get; init; }
    public required float phi_tc { get; init; }
    public required float D_tc { get; init; }
    public Tapping tap_tc => new(portsize_tc, printable_dmls);


    protected int DIVISIONS => max(200, (int)(200f / VOXEL_SIZE));

    protected const float EXTRA = 10f; // trimmed at some point.

    protected float extend_base_by => printable_dmls ? 20f : 0f;


    /*
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

    widening desie: https://www.desmos.com/calculator/qac1nmgrus
    */

    protected float cnt_wid_off = NAN;

    protected float cnt_X = NAN;


    protected float cnt_r_conv = NAN;
    protected float cnt_z0 = NAN;
    protected float cnt_r0 = NAN;
    protected float cnt_z1 = NAN;
    protected float cnt_r1 = NAN;
    protected float cnt_z2 = NAN;
    protected float cnt_r2 = NAN;
    protected float cnt_z3 = NAN;
    protected float cnt_r3 = NAN;
    protected float cnt_z4 = NAN;
    protected float cnt_r4 = NAN;
    protected float cnt_z5 = NAN;
    protected float cnt_r5 = NAN;
    protected float cnt_z6 = NAN;
    protected float cnt_r6 = NAN;
    protected float cnt_zP = NAN;
    protected float cnt_rP = NAN;

    protected float cnt_zQ = NAN;
    protected float cnt_rQ = NAN;
    protected float cnt_zR = NAN;
    protected float cnt_rR = NAN;

    protected float cnt_para_az = NAN;
    protected float cnt_para_bz = NAN;
    protected float cnt_para_cz = NAN;
    protected float cnt_para_ar = NAN;
    protected float cnt_para_br = NAN;
    protected float cnt_para_cr = NAN;


    protected float cnt_wid_alpha = NAN;
    protected float cnt_wid_r_s = NAN;

    protected float cnt_wid_z0 = NAN;
    protected float cnt_wid_r0 = NAN;
    protected float cnt_wid_z1 = NAN;
    protected float cnt_wid_r1 = NAN;
    protected float cnt_wid_z2 = NAN;
    protected float cnt_wid_r2 = NAN;
    protected float cnt_wid_z3 = NAN;
    protected float cnt_wid_r3 = NAN;
    protected float cnt_wid_z4 = NAN;
    protected float cnt_wid_r4 = NAN;

    protected float cnt_wid_zS = NAN;
    protected float cnt_wid_rS = NAN;


    protected Vec2 cnt_0 = NAN2;
    protected Vec2 cnt_1 = NAN2;
    protected Vec2 cnt_2 = NAN2;
    protected Vec2 cnt_3 = NAN2;
    protected Vec2 cnt_4 = NAN2;
    protected Vec2 cnt_5 = NAN2;
    protected Vec2 cnt_6 = NAN2;
    protected Vec2 cnt_Q = NAN2;
    protected Vec2 cnt_R = NAN2;
    protected Vec2 cnt_2_3 = NAN2;
    protected Vec2 cnt_2_3_n = NAN2;
    protected float cnt_2_3_mag = NAN;
    protected Vec2 cnt_5_6 = NAN2;
    protected float cnt_5_6_mag = NAN;

    protected Vec2 cnt_wid_0 = NAN2;
    protected Vec2 cnt_wid_1 = NAN2;
    protected Vec2 cnt_wid_2 = NAN2;
    protected Vec2 cnt_wid_3 = NAN2;
    protected Vec2 cnt_wid_4 = NAN2;
    protected Vec2 cnt_wid_2_3 = NAN2;
    protected Vec2 cnt_wid_2_3_n = NAN2;
    protected float cnt_wid_2_3_mag = NAN;

    // Caching is hugely impactful here, and we can/should use genuine equality
    // instead of imprecise.
    protected class cnt_vec2eq : IEqualityComparer<Vec2> {
        public bool Equals(Vec2 a, Vec2 b) => (a.X == b.X) && (a.Y == b.Y);
        public int GetHashCode(Vec2 v) => HashCode.Combine(v.X, v.Y);
    }
    protected Dictionary<Vec2, float>? _cnt_cache = null;
    protected Dictionary<Vec2, float>? _cnt_wid_cache = null;
    protected long _cnt_cache_hits = long.MinValue;
    protected long _cnt_wid_cache_hits = long.MinValue;
    protected long _cnt_cache_total = long.MinValue;
    protected long _cnt_wid_cache_total = long.MinValue;

    protected void initialise_cnt() {
        // The wid sdf gives the distance to the channel mid-contour.
        cnt_wid_off = th_iw + 0.5f*th_web;

        cnt_X = EXTRA + extend_base_by + 1.1f*max(pm.Mr_chnl, R_exit);


        cnt_r_conv = 1.5f*R_tht;

        cnt_z0 = 0f;
        cnt_r0 = pm.R_cc;

        cnt_z1 = L_cc;
        cnt_r1 = pm.R_cc;

        cnt_z2 = cnt_z1 - cnt_r_conv*sin(phi_conv);
        cnt_r2 = pm.R_cc - cnt_r_conv*(1f - cos(phi_conv));

        cnt_r3 = R_tht * (2.5f - 1.5f*cos(phi_conv));
        cnt_z3 = cnt_z2 + (cnt_r3 - cnt_r2)/tan(phi_conv);

        cnt_z4 = cnt_z3 - 1.5f*R_tht*sin(phi_conv);
        cnt_r4 = R_tht;

        cnt_z5 = cnt_z4 + 0.382f*R_tht*sin(phi_div);
        cnt_r5 = R_tht*(1.382f - 0.382f*cos(phi_div));

        cnt_z6 = cnt_z4 + NLF*(3.732051f*R_exit - 3.683473f*R_tht);
        cnt_r6 = R_exit;

        cnt_zP = (cnt_z5*tan(phi_div) - cnt_z6*tan(phi_exit) + cnt_r6 - cnt_r4)
               / (tan(phi_div) - tan(phi_exit));
        cnt_rP = tan(phi_exit)*(cnt_zP - cnt_z6) + cnt_r6;

        cnt_zQ = cnt_z0 - cnt_X;
        cnt_rQ = cnt_r0;
        cnt_zR = cnt_z6 + cnt_X;
        cnt_rR = cnt_r6;

        cnt_para_az = cnt_z5 - 2f*cnt_zP + cnt_z6;
        cnt_para_bz = -2f*cnt_z6 + 2f*cnt_zP;
        cnt_para_cz = cnt_z6;
        cnt_para_ar = cnt_r5 - 2f*cnt_rP + cnt_r6;
        cnt_para_br = -2f*cnt_r6 + 2f*cnt_rP;
        cnt_para_cr = cnt_r6;


        cnt_wid_r0 = pm.Mr_chnl;
        cnt_wid_r4 = pm.R_cc + th_iw + 0.5f*th_web;

        cnt_wid_alpha = 2f;
        cnt_wid_r_s = (cnt_wid_r0 - cnt_wid_r4)
                    / (2f - 2f*cos(phi_wid)
                     + cnt_wid_alpha*phi_wid*sin(phi_wid));

        cnt_wid_z0 = 0f;

        cnt_wid_z1 = -phi_wid*cnt_wid_r_s;
        cnt_wid_r1 = cnt_wid_r0;

        cnt_wid_z2 = cnt_wid_z1 - cnt_wid_r_s*sin(phi_wid);
        cnt_wid_r2 = cnt_wid_r1 - cnt_wid_r_s*(1f - cos(phi_wid));

        cnt_wid_r3 = cnt_wid_r4 + cnt_wid_r_s*(1f - cos(phi_wid));
        cnt_wid_z3 = cnt_wid_z2 + (cnt_wid_r3 - cnt_wid_r2)/tan(phi_wid);
        cnt_wid_z4 = cnt_wid_z3 - cnt_wid_r_s*sin(phi_wid);

        cnt_wid_zS = cnt_wid_z0 - cnt_X;
        cnt_wid_rS = cnt_wid_r0;


        cnt_0 = new(cnt_z0, cnt_r0);
        cnt_1 = new(cnt_z1, cnt_r1);
        cnt_2 = new(cnt_z2, cnt_r2);
        cnt_3 = new(cnt_z3, cnt_r3);
        cnt_4 = new(cnt_z4, cnt_r4);
        cnt_5 = new(cnt_z5, cnt_r5);
        cnt_6 = new(cnt_z6, cnt_r6);
        cnt_Q = new(cnt_zQ, cnt_rQ);
        cnt_R = new(cnt_zR, cnt_rR);
        cnt_2_3_mag = mag(cnt_3 - cnt_2);
        cnt_2_3 = (cnt_3 - cnt_2) / cnt_2_3_mag;
        cnt_2_3_n = rot90ccw(cnt_2_3);
        cnt_5_6_mag = mag(cnt_6 - cnt_5);
        cnt_5_6 = (cnt_6 - cnt_5) / cnt_5_6_mag;


        cnt_wid_0 = new(cnt_wid_z0, cnt_wid_r0);
        cnt_wid_1 = new(cnt_wid_z1, cnt_wid_r1);
        cnt_wid_2 = new(cnt_wid_z2, cnt_wid_r2);
        cnt_wid_3 = new(cnt_wid_z3, cnt_wid_r3);
        cnt_wid_4 = new(cnt_wid_z4, cnt_wid_r4);
        cnt_wid_2_3_mag = mag(cnt_wid_3 - cnt_wid_2);
        cnt_wid_2_3 = (cnt_wid_3 - cnt_wid_2) / cnt_wid_2_3_mag;
        cnt_wid_2_3_n = rot90ccw(cnt_wid_2_3);


        _cnt_cache = new(10000000 /* ten milly */, new cnt_vec2eq());
        _cnt_wid_cache = new(10000000 /* ten milly */, new cnt_vec2eq());
        _cnt_cache_hits = 0;
        _cnt_wid_cache_hits = 0;
        _cnt_cache_total = 0;
        _cnt_wid_cache_total = 0;
    }

    protected float cnt_sdf(in Vec3 p) {
        float z = p.Z;
        float r = magxy(p);
        Vec2 q = new(z, r);
        // CACHE ME.
        ++_cnt_cache_total;
        if (_cnt_cache!.TryGetValue(q, out float cached)) {
            ++_cnt_cache_hits; // HIIIIIIT
            return cached;
        }
        float dist = +INF;
        void update(ref float dist, float d) {
            if (abs(d) < abs(dist))
                dist = d;
        }
        // Check each segment and take the total minimum distance. Note that it
        // may not be possible that this point is closest to a given segment.

        assert(phi_conv < 0f);
        assert(phi_div > 0f);
        assert(phi_exit > 0f);

        // Check line segments.
        if (within(z, cnt_z0, cnt_z1)) { // <3 horiz lines.
            update(ref dist, r - cnt_r0);
        }
        float rlo23 = cnt_r3 + (cnt_z3 - z)/tan(phi_conv);
        float rhi23 = cnt_r2 + (cnt_z2 - z)/tan(phi_conv);
        if (within(r, rlo23, rhi23)) {
            float t = clamp(dot(q - cnt_2, cnt_2_3), 0f, cnt_2_3_mag);
            Vec2 P = cnt_2 + t*cnt_2_3;
            update(ref dist, dot(q - P, cnt_2_3_n));
        }

        // Check circular arcs.
        Vec2 d12 = q - new Vec2(cnt_z1, cnt_r1 - cnt_r_conv);
        if (within(arg(d12), PI_2 + phi_conv, PI_2)) {
            update(ref dist, +mag(d12) - cnt_r_conv);
        }
        Vec2 d34 = q - new Vec2(cnt_z4, 2.5f*R_tht);
        if (within(arg(d34), -PI_2 + phi_conv, -PI_2)) {
            update(ref dist, -mag(d34) + 1.5f*R_tht);
        }
        Vec2 d56 = q - new Vec2(cnt_z4, 1.382f*R_tht);
        if (within(arg(d56), -PI_2, -PI_2 + phi_div)) {
            update(ref dist, -mag(d56) + 0.382f*R_tht);
        }

        // Check that bloody rotated parabola.
        float rlo56 = cnt_r5 + (cnt_z5 - z)/tan(phi_div);
        float rhi56 = cnt_r6 + (cnt_z6 - z)/tan(phi_exit);
        if (within(r, rlo56, rhi56)) {
            // Closest point is somewhere on tilted parabola. It will occur at a
            // point s.t. the normal of that point intersects q.

            // Look at the desie for more, but basically we just gotta root
            // `f(S)` (where `eff` computes it and its derivative), and this `S`
            // value will be the x value of the closest point.

            // Start with linear approximation (very good initial guess).
            float t = clamp(dot(q - cnt_5, cnt_5_6), 0f, cnt_5_6_mag);
            float S = (cnt_5 + t*cnt_5_6).X;

            float A0 = 2*cnt_para_ar*cnt_para_ar;
            float B0 = 3*cnt_para_ar*cnt_para_br;
            float C0 = 2*cnt_para_ar*cnt_para_cr - 2*r*cnt_para_ar
                     + cnt_para_br*cnt_para_br;
            float D0 = cnt_para_br*cnt_para_cr - r*cnt_para_br;
            void eff(float s, out float f, out float df) {
                float term0 = sqrt(cnt_para_bz*cnt_para_bz
                                 - 4f*cnt_para_az*(cnt_para_cz - s));
                float p = (-cnt_para_bz - term0) / 2f / cnt_para_az;
                float dp = -1f/term0;
                float term1 = ((A0*p + B0)*p + C0)*p + D0;
                f = -z + s + dp*term1;
                float ddp = 2f*cnt_para_az/term0/term0/term0;
                df = 1f + ddp*term1 + dp*dp*((3*A0*p + 2*B0)*p + C0);
            }

            // Chuck it through some newton raphson iters.
            for (int i=0; true; ++i) {
                eff(S, out float f, out float df);
                S -= f / df;
                if (f < 1e-3f)
                    break;
                assert(i < 10, $"f={f}");
            }

            float pS = (-cnt_para_bz - sqrt(cnt_para_bz*cnt_para_bz
                                          - 4f*cnt_para_az*(cnt_para_cz - S)))
                     / 2f / cnt_para_az;
            Vec2 P = new(S, (cnt_para_ar*pS + cnt_para_br)*pS + cnt_para_cr);
            float d = mag(q - P);
            if (r < P.Y)
                d = -d; // inside conic.
            update(ref dist, d);
        } else if (r >= rhi56 && z <= cnt_z6) {
            // Point 6 is necessarily closest (on the conic) and outside.
            update(ref dist, mag(q - cnt_6));
        }

        // Check end zones (TOUCHDOWWWWWWN or something idk not american).
        if (z <= cnt_z0) {
            float dist_z = cnt_zQ - z;
            float dist_r = r - cnt_rQ;
            float d;
            if (dist_z <= 0f || dist_r <= 0f)
                d = max(dist_z, dist_r);
            else
                d = hypot(dist_z, dist_r);
            update(ref dist, d);
        }
        if (z >= cnt_z6) {
            float dist_z = z - cnt_zR;
            float dist_r = r - cnt_rR;
            float d;
            if (dist_z <= 0f || dist_r <= 0f)
                d = max(dist_z, dist_r);
            else
                d = hypot(dist_z, dist_r);
            update(ref dist, d);
        }

        _cnt_cache[q] = dist;
        return dist;
    }

    protected float cnt_wid_sdf(in Vec3 p) {
        // Same deal as `cnt_sdf`.

        float z = p.Z;
        float r = magxy(p);
        Vec2 q = new(z, r);
        ++_cnt_wid_cache_total;
        if (_cnt_wid_cache!.TryGetValue(q, out float cached)) {
            ++_cnt_wid_cache_hits;
            return cached;
        }
        float dist = +INF;
        void update(ref float dist, float d) {
            if (abs(d) < abs(dist))
                dist = d;
        }

        assert(phi_wid < 0f);


        // Check line segments.
        if (within(z, cnt_wid_z0, cnt_wid_z1)) {
            update(ref dist, r - cnt_wid_r0);
        }
        float rlo23 = cnt_wid_r3 + (cnt_wid_z3 - z)/tan(phi_wid);
        float rhi23 = cnt_wid_r2 + (cnt_wid_z2 - z)/tan(phi_wid);
        if (within(r, rlo23, rhi23)) {
            float t = dot(q - cnt_wid_2, cnt_wid_2_3);
            t = clamp(t, 0f, cnt_wid_2_3_mag);
            Vec2 P = cnt_wid_2 + t*cnt_wid_2_3;
            update(ref dist, dot(q - P, cnt_wid_2_3_n));
        }

        // Check circular arcs.
        Vec2 d12 = q - new Vec2(cnt_wid_z1, cnt_wid_r1 - cnt_wid_r_s);
        if (within(arg(d12), PI_2 + phi_wid, PI_2)) {
            update(ref dist, +mag(d12) - cnt_wid_r_s);
        }
        Vec2 d34 = q - new Vec2(cnt_wid_z4, cnt_wid_r4 + cnt_wid_r_s);
        if (within(arg(d34), -PI_2 + phi_wid, -PI_2)) {
            update(ref dist, -mag(d34) + cnt_wid_r_s);
        }

        // Check end zones (TOUCHDOWWWWWWN or something idk not american).
        if (z <= cnt_wid_z0) {
            float dist_z = cnt_wid_zS - z;
            float dist_r = r - cnt_wid_rS;
            float d;
            if (dist_z <= 0f || dist_r <= 0f)
                d = max(dist_z, dist_r);
            else
                d = hypot(dist_z, dist_r);
            update(ref dist, d);
        }
        if (z >= cnt_wid_z4) {
            Vec2 d = q - new Vec2(cnt_wid_z4, 0f);
            update(ref dist, +mag(d) - cnt_wid_r4);
        }

        _cnt_wid_cache[q] = dist;
        return dist;
    }

    protected float cnt_widened_sdf(in Vec3 p) {
        // Note that its not universally true that the min function is the proper
        // sdf union (i.e. get distance magnitude correct, not just
        // non-zero-ness), but in this case im pretty sure its correct.
        return min(cnt_sdf(p), cnt_wid_sdf(p) + cnt_wid_off);
    }

    protected float cnt_radius_at(float z, float signed_dist, bool widened) {
        assert(within(z, cnt_zQ, cnt_zR), $"z={z}");
        SDFfunction sdf = widened ? cnt_widened_sdf : cnt_sdf;

        // Initially, estimate by just offseting the inner point horizontally by
        // the signed dist.
        float r;
        if (z <= cnt_z1) {
            r = cnt_r0;
        } else if (z <= cnt_z2) {
            r = cnt_r1 - cnt_r_conv + nonhypot(cnt_r_conv, z - cnt_z1);
        } else if (z <= cnt_z3) {
            r = tan(phi_conv)*(z - cnt_z3) + cnt_r3;
        } else if (z <= cnt_z4) {
            r = 2.5f*R_tht - nonhypot(1.5f*R_tht, z - cnt_z4);
        } else if (z <= cnt_z5) {
            r = 1.382f*R_tht - nonhypot(0.382f*R_tht, z - cnt_z4);
        } else if (z <= cnt_z6) {
            float p;
            p = 4f*cnt_para_az*(cnt_para_cz - z);
            p = sqrt(cnt_para_bz*cnt_para_bz - p);
            p = (-cnt_para_bz - p) / 2f / cnt_para_az;
            r = (cnt_para_ar*p + cnt_para_br)*p + cnt_para_cr;
        } else {
            r = R_exit;
        }
        // Also note i couldnt be bothered to put the widened analytic solns.

        r += signed_dist;

        // Now its close the correct solution, but not exactly. So, just kinda
        // brute force it by assuming the distance we are from the actual desired
        // offset is perfectly horizontal. note this has like i think linear
        // convergence so pretty bad but eh its fine.
        for (int i=0; true; ++i) {
            float Dr = signed_dist - sdf(new(r, 0f, z));
            r += Dr; // doctor semi colon.
            if (abs(Dr) < 1e-3)
                break;
            assert(i < 10, $"Dr={Dr}"); // don loop forever.
        }
        return r;
    }

    protected Voxels voxels_cnt_filled(float max_off, bool widened,
            bool extra=true) {
        List<Vec2> V = new(DIVISIONS + 2);
        float zlo = cnt_z0 - extend_base_by;
        float zhi = cnt_z6;
        if (extra) {
            zlo -= EXTRA;
            zhi += EXTRA;
        }
        V.Add(new(zlo, 0f));
        for (int i=0; i<DIVISIONS; ++i) {
            float z = lerp(zlo, zhi, i, DIVISIONS);
            V.Add(new(z, cnt_radius_at(z, max_off, widened)));
        }
        V.Add(new(zhi, 0f));
        Mesh mesh = Polygon.mesh_revolved(new(), V);
        return new(mesh);
    }
    protected Voxels voxels_cnt_shelled(float min_off, float th, bool widened,
            bool extra=true) {
        List<Vec2> V = new(2*DIVISIONS);
        float zlo = cnt_z0 - extend_base_by;
        float zhi = cnt_z6;
        if (extra) {
            zlo -= EXTRA;
            zhi += EXTRA;
        }
        for (int i=0; i<DIVISIONS; ++i) {
            float z = lerp(zlo, zhi, i, DIVISIONS);
            V.Add(new(z, cnt_radius_at(z, min_off + th, widened)));
        }
        for (int i=DIVISIONS - 1; i>-1; --i) {
            float z = lerp(zlo, zhi, i, DIVISIONS);
            V.Add(new(z, cnt_radius_at(z, min_off, widened)));
        }
        Mesh mesh = Polygon.mesh_revolved(new(), V, donut: true);
        return new(mesh);
    }

    protected Voxels voxels_gas(Geez.Cycle key) {
        using var __ = key.like();

        // view a cheeky wireframe while its generating.
        if (!minimise_mem) {
            List<Geez.Key> wireframe = new();
            int N = 16;
            for (int i=0; i<N; ++i) {
                float z = lerp(cnt_z0, cnt_z6, i, N);
                float r = cnt_radius_at(z, 0f, false);
                Rod rod = new Rod(
                    new Frame(z*uZ3),
                    1e-2f,
                    r
                ).shelled(-1e-2f);
                Geez.Key wire = Geez.rod(rod, rings: 2, columns: 0);
                wireframe.Add(wire);
            }
            N = 6;
            for (int n=0; n<N; ++n) {
                List<Vec3> contour = new();
                float theta = n*TWOPI/N;
                for (int i=0; i<DIVISIONS; ++i) {
                    float z = lerp(cnt_z0, cnt_z6, i, DIVISIONS);
                    float r = cnt_radius_at(z, 0f, false);
                    contour.Add(fromcyl(r, theta, z));
                }
                Geez.Key wire = Geez.line(contour);
                wireframe.Add(wire);
            }

            key <<= Geez.group(wireframe);
        }

        Voxels vox = voxels_cnt_filled(0f, false);
        key.voxels(vox);
        return vox;
    }

    protected Voxels voxels_outer(Geez.Cycle key) {
        Voxels vox = voxels_cnt_filled(th_iw + th_chnl + th_ow, true);
        key.voxels(vox);
        return vox;
    }


    protected Mesh mesh_chnl(out List<Vec2> V0) {
        /* constant webbing length (not angular) thickness */

        int N = DIVISIONS;
        int M = max(6, (int)(wi_chnl(pm.R_cc)/VOXEL_SIZE));
        M += M & 1; // make even.
        assert(M >= 2);

        List<Vec3> frames = new(N);
        List<Vec2> vertices = new(N*M);
        // ccw winding of vertices about +axial.

        float zlo = 0f;
        float zhi = z1_chnl;
        for (int i=0; i<N; ++i) {
            float z = lerp(zlo, zhi, i, N);
            float Mtheta = theta_chnl(z);
            float rlo = cnt_radius_at(z, th_iw, true);
            float rhi = cnt_radius_at(z, th_iw + th_chnl, true);

            frames.Add(z*uZ3);

            float theta0lo = Mtheta - wi_chnl(rlo)/rlo/2f;
            float theta0hi = Mtheta + wi_chnl(rlo)/rlo/2f;
            assert(theta0hi > theta0lo);
            for (int j=0; j<M/2; ++j) {
                float theta = lerp(theta0hi, theta0lo, j, M/2);
                vertices.Add(frompol(rlo, theta));
            }
            float theta1lo = Mtheta - wi_chnl(rhi)/rhi/2f;
            float theta1hi = Mtheta + wi_chnl(rhi)/rhi/2f;
            assert(theta1hi > theta1lo);
            for (int j=0; j<M/2; ++j) {
                float theta = lerp(theta1lo, theta1hi, j, M/2);
                vertices.Add(frompol(rhi, theta));
            }
        }
        // duplicate lowest polygon downwards.
        frames.Insert(0, frames[0] - (EXTRA + extend_base_by)*uZ3);
        vertices.InsertRange(0, vertices[..M]);

        // Export lowest polygon.
        V0 = vertices[..M];


        // Handle channel inlets. Shape as a recantangle w triangle roof.

        // Do the rectangle in the same mesh.
        {
            float Mtheta = theta_chnl(z1_chnl);
            float wi0 = wi_chnl(
                cnt_radius_at(z1_chnl, th_iw, true)
            );
            float wi1 = wi_chnl(
                cnt_radius_at(z1_chnl, th_iw + th_chnl + th_imani, true)
            );
            for (float z=z1_chnl + 1e-3f; true; z += VOXEL_SIZE) {
                float rlo = cnt_radius_at(z, th_iw, true);
                float rhi = cnt_radius_at(z, th_iw + th_chnl + th_imani, true);
                rhi += 4f*VOXEL_SIZE; // safety.

                float curr_wi1 = wi1 - 2f*max(0f, z - z1_chnl - th_chnl);
                float curr_wi0 = lerp(0f, wi0, invlerp(0f, wi1, curr_wi1));
                if (curr_wi0 < 1e-3f || curr_wi1 < 1e-3f)
                    break;

                frames.Add(z*uZ3);

                float theta0lo = Mtheta - curr_wi0/rlo/2f;
                float theta0hi = Mtheta + curr_wi0/rlo/2f;
                for (int j=0; j<M/2; ++j) {
                    float theta = lerp(theta0hi, theta0lo, j, M/2);
                    vertices.Add(frompol(rlo, theta));
                }
                float theta1lo = Mtheta - curr_wi1/rhi/2f;
                float theta1hi = Mtheta + curr_wi1/rhi/2f;
                for (int j=0; j<M/2; ++j) {
                    float theta = lerp(theta1lo, theta1hi, j, M/2);
                    vertices.Add(frompol(rhi, theta));
                }
            }
        }

        // Now add the triangle top with weird meshing tech.

        return Polygon.mesh_swept(new FramesCart(frames), vertices);
    }


    protected Voxels voxels_chnl(Geez.Cycle key) {
        using var __ = key.like();

        Voxels vox = new();
        List<Geez.Key> keys = new();

        Mesh mesh0 = mesh_chnl(out List<Vec2> V0);
        for (int i=0; i<no_chnl; ++i) {
            float Dtheta = i*TWOPI/no_chnl;
            Transformer trans = new Transformer().rotate(uZ3, Dtheta);
            Mesh mesh = trans.mesh(mesh0);
            keys.Add(Geez.mesh(mesh));
            vox.BoolAdd(new(mesh));
        }

        key.voxels(vox);
        Geez.remove(keys);
        keys = [];

        if (!filletless) {
            Fillet.convex(vox, FR_chnl, inplace: true);
            key.voxels(vox);
        }

        if (!printable_dmls)
            return vox;

        // Cooling channel breakouts.

        // Setup.
        V0 = [.. V0.Select((v) => rotate(v, -theta_chnl(0f)))];
        V0 = [.. V0.Select((v) => v - pm.Mr_chnl*uX2)];
        float z_spacing = extend_base_by*0.2f;
        Breakout breakout = new(V0
                , Lz: extend_base_by - 2f*z_spacing
                , Lx: pm.r_bolt + pm.D_bolt/2f + pm.thickness_around_bolt
                    - pm.Mr_chnl + EXTRA
                , D1: 7f
                , FR: filletless ? 0f : FR_chnl
            );

        vox.BoolIntersect(new Rod(
            new(),
            cnt_z6,
            pm.flange_outer_radius
        ).extended(breakout.straight_for + z_spacing, Extend.DOWN));
        key.voxels(vox);

        for (int i=0; i<no_chnl; ++i) {
            float Dtheta = i*TWOPI/no_chnl;
            float theta = theta_chnl(0f) + Dtheta;
            Vec3 p = fromcyl(pm.Mr_chnl, theta, -z_spacing);
            Voxels v = breakout.at(Frame.cyl_axial(p));
            keys.Add(Geez.voxels(v));
            vox.BoolAdd(v);
        }
        key.voxels(vox);
        Geez.remove(keys);
        keys = [];

        return vox;
    }

    protected const float min_A_neg_mani = 45f; // mm^2
    protected float A_neg_mani(float theta) {
        // Lerp between initial (at mani inlet) and final (opposite mani inlet).
        // This is so that the cross-sectional area lost from one channel to the
        // next (travelling downstream) is exactly one channel area.
        float Dtheta = wraprad(theta - theta_inlet);
        // in one half-turn, no_chnl/2 channels are passed.
        float t = abs(Dtheta) / (PI / (0.5f*no_chnl));
        // Note however that we cant end with a zero area manifold, so we instead
        // limit it to some minimum. Buuut this would cause a kink so we fillet
        // the function itself lmao:
        // https://www.desmos.com/calculator/l75u4xcn9c
        float x = t * A_chnl_exit;
        /* using this x, As == 1 */
        float A0 = 0.5f*no_chnl*A_chnl_exit;
        float Am = min_A_neg_mani;
        float F0 = A0/8f;
        float y0 = A0 - F0*SQRT2;
        float x0h = F0*SQRTH;
        float F1 = 2f*Am;
        float y1 = Am + F1;
        float x1l = A0 - Am + F1*(SQRTH - 1f);
        float x1h = A0 - Am + F1/(1f + SQRT2);
        float A;
        if (x < x0h)
            A = y0 + nonhypot(F0, x);
        else if (x <= x1l)
            A = A0 - x;
        else if (x < x1h)
            A = y1 - nonhypot(F1, x - x1h);
        else
            A = Am;
        return A;
    }


    protected void points_maybe_mani(float neg_min_z, out List<Vec2> neg,
            out List<Vec2> pos, out Vec2 inlet) {
        // note neg_min_z for neg is only a "construction" value, it is filleted
        // so is not the true minimum.

        /* Neg is a outer-wall hugging triangle thing:
    |'-,  - a
    |   `-,
    |      `-,
    |         ',  - c (filleted)
   |        ,-'
   |     ,-'
  ,'`---'
  |   '---- b (filleted)
        */

        /* Pos is a huge wedge:
 ,- d

 ,-------,._   - A (filleted)
 |          `-,
 |             `-,
 |                `;  - C (filleted)
 |              ,-'
 |           ,-'
 |        ,-'
 |     ,-'
 |  ,-'
 '-'      - e
        */
        float FR_b = 1.5f;
        float FR_c = 2f;
        int divisions_wall = DIVISIONS/20;
        int divisions_b = max(6, DIVISIONS/60);
        int divisions_c = max(6, DIVISIONS/100);
        int divisions_A = max(6, DIVISIONS/80);

        // Get the contour hugging the outer wall.
        neg = new(2*divisions_wall);
        float neg_max_z = cnt_z6 - th_omani;
        for (int i=0; i<2*divisions_wall; ++i) {
            float z = lerp(neg_max_z, neg_min_z, i, 2*divisions_wall);
            float r = cnt_radius_at(z, th_iw + th_chnl + th_imani, false);
            neg.Add(new(z, r));
        }
        Vec2 a = neg[0];
        Vec2 b = neg[^1];
        // Then get the intersection between the shallowest lines we can make
        // from these two points. https://www.desmos.com/calculator/xfhcgwu2el
        assert(phi_mani > 0f);
        Vec2 c = new(
            0.5f*(a.Y - b.Y)/tan(phi_mani) + 0.5f*(a.X + b.X),
            0.5f*(a.X - b.X)*tan(phi_mani) + 0.5f*(a.Y + b.Y)
        );

        float phi_aA = 1.25f*PI - 0.5f*phi_mani;
        Vec2 A = a + frompol(th_omani/cos(phi_aA), phi_aA);
        Vec2 C = c + uY2*th_omani/cos(phi_mani);
        Vec2 d = new(cnt_z6, R_tht); // throat radius reasonable lower extreme.
        Vec2 e = new(
            (d.Y - b.Y)/tan(phi_mani) + b.X - th_omani/sin(phi_mani),
            R_tht
        );
        neg.Add(c);

        // Now fillet the lower corner.
        Polygon.fillet(neg, numel(neg) - 2, FR_b, divisions: divisions_b);

        // Note that this fillet has made the polygon variable-length, so
        // resample it to a fixed amount.
        List<Vec2> wall = neg[.. (^divisions_b)];
        wall = Polygon.resample(wall, divisions_wall);
        neg = [..wall, ..neg[(^divisions_b) ..]];

        // Fixed-count fillet for the outer corner.
        Polygon.fillet(neg, numel(neg) - 1, FR_c, divisions: divisions_c,
                only_this_vertex: true /* justin caseme */);

        // Pos so easyyy with all fixed-division fillets.
        pos = [d, e, C, A];
        Polygon.fillet(pos, 3, th_omani, divisions: divisions_A,
                only_this_vertex: true);
        Polygon.fillet(pos, 2, FR_c + th_omani, divisions: divisions_c,
                only_this_vertex: true);

        // Place the inlet s.t. its top point coincides with c.
        float Dell = new Flats(tap_inlet, th_inlet).r - th_omani;
        inlet = c + normalise(b - c)*Dell;
    }

    protected void points_mani(float theta, out List<Vec2> neg,
            out List<Vec2> pos, out Frame inlet) {
        neg = new();
        pos = new();
        float A = A_neg_mani(theta);
        assert(A > 0);
        // Use an approximation to get an initial guess for min z.
        // https://www.desmos.com/calculator/bfsipdjpsd
        assert(phi_exit > 0f);
        assert(phi_mani > 0f);
        assert(phi_mani > phi_exit, $"phi_exit={phi_exit}, phi_mani={phi_mani}");
        float cosa = cos(phi_exit);
        float sina = sin(phi_exit);
        float tanb = tan(phi_mani);
        float L = 2f*sqrt(A/(cosa*cosa*tanb - sina*sina/tanb));
        float Az = L*cosa;
      #if false
        float Ar = L*sina;
        float Bz = L/2f*(sina/tanb + cosa);
        float Br = L/2f*(sina + cosa*tanb);
      #endif
        float min_z = cnt_z6 - th_ow - Az; // guess.
        Vec2 inlet_zr = NAN2;
        for (int i=0; true; ++i) {
            points_maybe_mani(min_z, out neg, out pos, out inlet_zr);
            float A0 = Polygon.area(neg);
            float f0 = A - A0;
            if (abs(f0) < 1e-1f)
                break;
            // discrete newton-raphson iter.
            float eps = 1e-2f;
            points_maybe_mani(min_z - eps, out List<Vec2> negA, out _, out _);
            points_maybe_mani(min_z + eps, out List<Vec2> negB, out _, out _);
            float fA = A - Polygon.area(negA);
            float fB = A - Polygon.area(negB);
            float DfDz = (fB - fA) / eps / 2f;
            assert(!nearzero(DfDz));
            min_z -= f0/DfDz;
            assert(min_z > cnt_z4);
            // hopefully an overkill number of iters.
            assert(i < 20, $"f0={f0}, DfDz={DfDz}");
        }
        inlet = new(
            fromzr(inlet_zr, theta),
            fromcyl(1f, theta + PI_2, 0f),
            fromsph(1f, theta, phi_mani + PI_2)
        );
    }

    protected Voxels voxels_neg_mani(Geez.Cycle key, out Frame inlet) {
        using var __ = key.like();

        List<Vec2> vertices;
        points_mani(theta_inlet, out vertices, out _, out inlet);
        for (int n=1; n<DIVISIONS; ++n) {
            float theta = theta_inlet + n*TWOPI/DIVISIONS;
            points_mani(theta, out List<Vec2> more, out _, out _);
            vertices.AddRange(more);
        }
        Mesh mesh = Polygon.mesh_revolved(
            new Frame().rotxy(theta_inlet),
            vertices,
            slicesize: numel(vertices) / DIVISIONS,
            donut: true
        );
        key.cycle(Geez.mesh(mesh));
        Voxels vox = new(mesh);

        float zextra = R_exit;
        vox.BoolAdd(new Rod(
            inlet,
            FR_inlet + EXTRA,
            tap_inlet.minor_radius
        ).extended(zextra, Extend.DOWN));
        key.cycle(Geez.voxels(vox));

        if (!filletless) {
            Voxels mask = new Rod(
                inlet,
                1.1f*FR_inlet,
                tap_inlet.minor_radius + 1.1f*FR_inlet
            ).extended(zextra, Extend.DOWN);
            using (Lifted l = new(vox, mask))
                Fillet.concave(l.vox, FR_inlet, inplace: true);
            key.cycle(Geez.voxels(vox));
        }

        vox.BoolSubtract(new Bar(
            inlet.transz(FR_inlet),
            FR_inlet + 2*EXTRA,
            SQRT2*tap_inlet.major_diameter + EXTRA)
        );
        Frame inlet_out = inlet.transz(tap_inlet.threaded_length
                                     + 0.8f*FR_inlet);
        vox.BoolAdd(tap_inlet.at(inlet_out));
        key.cycle(Geez.voxels(vox));

        // ensure it doesnt clip inwards.
        vox.BoolSubtract(voxels_cnt_filled(th_iw + th_chnl + th_imani, false));
        key.cycle(Geez.voxels(vox));

        return vox;
    }

    protected Voxels voxels_pos_mani(Frame inlet) {
        List<Vec2> vertices = new();
        for (int n=0; n<DIVISIONS; ++n) {
            float theta = theta_inlet + n*TWOPI/DIVISIONS;
            points_mani(theta, out _, out List<Vec2> more, out _);
            vertices.AddRange(more);
        }
        Mesh mesh = Polygon.mesh_revolved(
            new Frame().rotxy(theta_inlet),
            vertices,
            slicesize: numel(vertices) / DIVISIONS,
            donut: true
        );
        Voxels vox = new(mesh);

        for (int i=0; i<no_fixt; ++i) {
            float theta = i*TWOPI/no_fixt + PI/no_fixt;
            List<Vec2> points = [new(cnt_z6, R_tht)];
            points.Add(new(
                points[^1].X,
                R_exit + th_iw + th_chnl + th_omani + 15f
            ));
            points.Add(points[^1] - uX2*14f);
            points.Add(
                Polygon.line_intersection(
                    points[^1], points[^1] + frompol(1f, PI + phi_fixt),
                    points[0], points[0] - uX2
                )
            );
            Polygon.fillet(points, 2, 7f);
            // Polygon.fillet(points, 1, 2f);
            Frame frame = new(
                ZERO3,
                uZ3,
                fromcyl(1f, theta + PI_2, 0f)
            );
            vox.BoolAdd(new(Polygon.mesh_extruded(
                frame,
                wi_fixt,
                points,
                at_middle: true
            )));
        }

        float zextra = 2.5f;
        float inlet_Lz = tap_inlet.threaded_length + 0.8f*FR_inlet;
        float inlet_R = tap_inlet.major_radius + th_inlet;
        Frame inlet_end = inlet.transz(inlet_Lz);
        // +z = +normal, +x = +circum

        vox.BoolAdd(new Flats(tap_inlet, th_inlet){
            Lz = inlet_Lz + zextra,
            flats_Lz = INF,
        }.at(inlet_end));

        if (!filletless) {
            Voxels mask = new Rod(
                inlet,
                1.1f*FR_inlet,
                inlet_R + 1.1f*FR_inlet
            ).extended(zextra, Extend.DOWN);
            using (Lifted l = new(vox, mask))
                Fillet.concave(l.vox, FR_inlet, inplace: true);
        }

        // Diamond underneath.
        vox.BoolAdd(new Bar(
            inlet_end.rotxy(PI_4),
            -inlet_Lz,
            inlet_R
        ).extended(zextra, Extend.DOWN)
         .at_edge(Bar.X0_Y0));
        // Flange connection.
        float th = 4.5f;
        Frame bottom_tip = inlet_end.transy(-inlet_R * SQRT2 + th);
        bottom_tip = bottom_tip.rotyz(phi_inlet + PI_2 - argphi(inlet_end.Z));
        vox.BoolAdd(new Bar(
            bottom_tip,
            th,
            (magxy(bottom_tip.pos) - R_tht)/sin(phi_inlet),
            -inlet_Lz
        ).at_face(Bar.Y0));
        vox.BoolSubtract(new Bar(
            bottom_tip,
            inlet_Lz,
            4f*th
        ));

        return vox;
    }

    protected List<Vec3> points_tc() {
        int N = 2*DIVISIONS;

        float min_z = pm.R_cc;
        float max_z = cnt_z6 - th_chnl - th_omani;
        max_z -= (max_z - min_z) / (no_tc - 1) / 3;

        List<Vec3> line = new(N);
        for (int i=0; i<N; ++i) {
            float z = lerp(min_z, max_z, i, N);
            float theta = theta_chnl(z);
            float r = cnt_radius_at(z, th_iw + 0.5f*th_chnl, true);
            line.Add(fromcyl(r, theta, z));
        }

        List<float> dists = new(N){ 0f };
        for (int i=1; i<N; ++i)
            dists.Add(dists[i - 1] + mag(line[i] - line[i - 1]));

        List<Vec3> points = new(no_tc);
        for (int i=0; i<no_tc; ++i) {
            float target = lerp(dists[0], dists[N - 1], i, no_tc);
            int lo = 0;
            int hi = numel(dists) - 1;
            while (lo + 1 < hi) {
                int mid = (lo + hi) / 2;
                if (dists[mid] <= target) {
                    lo = mid;
                } else {
                    hi = mid;
                }
            }
            float d0 = dists[lo];
            float d1 = dists[hi];
            Vec3 p0 = line[lo];
            Vec3 p1 = line[hi];
            Vec3 p = lerp(p0, p1, invlerp(d0, d1, target));
            points.Add(p);
        }
        return points;
    }

    protected void voxels_tc(Geez.Cycle key, out Voxels neg, out Voxels pos) {
        List<Vec3> points = points_tc();
        neg = new();
        pos = new();

        foreach (Vec3 p in points) {
            Frame frame = Frame.cyl_radial(p);
            float Dz = cnt_radius_at(p.Z,
                    th_iw + th_chnl + th_ow + 5f + tap_tc.straight_length, true);
            Dz -= magxy(p);

            Voxels this_neg = tap_tc.at(frame.transz(Dz));
            this_neg.BoolAdd(new Rod(
                frame,
                Dz,
                D_tc/2f
            ).extended(EXTRA, Extend.UP));
            this_neg.BoolAdd(new Bar(
                frame.rotxy(PI_4),
                Dz,
                D_tc/2f
            ).extended(EXTRA, Extend.UP)
             .at_edge(Bar.X1_Y1));

            float pos_Lr = tap_tc.major_radius + th_tc;
            // Supporting from tapping to include flats.
            Voxels this_pos = new Flats(tap_tc, th_tc){
                Lz = Dz + 2f*EXTRA,
                flats_Lz = INF,
            }.at(frame.transz(Dz));
            this_pos.BoolAdd(new Rod(
                frame,
                Dz,
                pos_Lr
            ).extended(2*EXTRA, Extend.DOWN));
            this_pos.BoolAdd(new Bar(
                frame.rotxy(PI_4),
                Dz,
                pos_Lr
            ).extended(2*EXTRA, Extend.DOWN)
             .at_edge(Bar.X0_Y0));

            Bar web = new Bar(
                frame,
                th_tc,
                100f,
                Dz
            ).extended(2*EXTRA, Extend.DOWN)
             .at_face(Bar.Y0);
            this_pos.BoolAdd(web);
            this_pos.IntersectImplicit(new Space(
                frame.translate(new Vec3(0f, -pos_Lr*SQRT2 + web.Lx/2f, Dz))
                     .rotyz(phi_tc),
                -INF,
                0f
            ));

            neg.BoolAdd(this_neg);
            pos.BoolAdd(this_pos);
            key.voxels(pos);
        }
    }

    protected Voxels voxels_flange(Geez.Cycle key) {
        using var __ = key.like();
        List<Geez.Key> keys = new();

        Voxels vox;

        vox = new Rod(
            new Frame(),
            pm.flange_thickness_cc,
            pm.flange_outer_radius
        ).extended(EXTRA + extend_base_by, Extend.DOWN);
        keys.Add(Geez.voxels(vox));

        for (int i=0; i<pm.no_bolt; ++i) {
            float theta = i*TWOPI/pm.no_bolt;
            float r = pm.r_bolt;
            float Lz = pm.flange_thickness_cc;
            float Lr = pm.D_bolt/2f + pm.thickness_around_bolt;
            Vec3 p = fromcyl(r, theta, 0f);

            List<Vec2> tracezr = new();
            float rlo = pm.R_cc + th_iw + th_chnl + th_ow - r;
            float zhi = sqed((Lr - rlo)/Lr/2f)*10f;
            int N = DIVISIONS/16;
            for (int j=0; j<N; ++j) {
                float x = sqed(j/(float)(N - 1))*(zhi + 2f*EXTRA);
                float y = Lr - Lr*2f*sqrt(x/10f);
                tracezr.Add(new(x, y));
            }
            tracezr.Add(new(zhi + 2f*EXTRA, rlo));
            tracezr.Add(new(zhi + 2f*EXTRA, Lr + EXTRA));
            tracezr.Add(new(0f,             Lr + EXTRA));

            List<Vec2> tracexy = new();
            float theta_stop = PI/2.3f;
            N = (int)(DIVISIONS*(TWOPI - 2f*theta_stop)/TWOPI)/2;
            for (int j=0; j<N; ++j) {
                float t = -PI + j*TWOPI/N;
                if (abs(wraprad(t + PI)) < theta_stop)
                    continue;
                tracexy.Add(frompol(Lr, t));
            }
            float length = 2f*(r - pm.R_cc - th_iw - th_chnl - th_ow);
            Vec2 A = frompol(Lr, -PI - theta_stop)
                   + frompol(length, -PI - theta_stop + PI_2);
            Vec2 B = frompol(Lr, -PI + theta_stop)
                   + frompol(length, -PI + theta_stop - PI_2);
            if (A.Y < B.Y) {
                Vec2 C = Polygon.line_intersection(
                    A, tracexy[^1],
                    B, tracexy[0]
                );
                tracexy.Add(C);
            } else {
                tracexy.Add(A);
                tracexy.Add(B);
            }

            Voxels this_vox = new(Polygon.mesh_extruded(
                Frame.cyl_axial(p),
                Lz + zhi + EXTRA,
                tracexy,
                extend_by: EXTRA + extend_base_by,
                extend_dir: Extend.DOWN
            ));
            this_vox.BoolSubtract(new(Polygon.mesh_extruded(
                Frame.cyl_circum(p + (Lz - 1f)*uZ3),
                2f*Lr + EXTRA,
                tracezr,
                at_middle: true,
                extend_by: EXTRA + extend_base_by,
                extend_dir: Extend.DOWN
            )));
            this_vox.BoolAdd(new Rod(
                new Frame(p),
                Lz,
                Lr
            ).extended(EXTRA + extend_base_by, Extend.DOWN));
            keys.Add(Geez.voxels(this_vox));
            vox.BoolAdd(this_vox);
        }

        key <<= Geez.group(keys);

        return vox;
    }

    protected Voxels? voxels_branding(Geez.Cycle key) {
        using var __ = key.like();

        if (VOXEL_SIZE > 0.5)
            return null;

        float wi = 26f;
        float Lr = 0.6f;
        float Lr_inset = 0.4f;
        float R = cnt_r1 + th_iw + th_chnl + th_ow - Lr_inset;
        Lr += Lr_inset;

        Frame at = new Frame(cnt_z1*uZ3).rotxy(theta_inlet - PI_2);
        Voxels vox = new();
        void add(string path, float rot=0f, float scale=1f, bool unimelb=false) {
            ImageSignedDist img = new(path, flipy: true);
            if (unimelb)
                img.fix_unimelb_lmao();

            at = at.transz(-0.5f * wi/img.aspect_x_on_y);
            vox.BoolAdd(img.voxels_on_cyl(true, at.rotxy(rot), R, Lr, wi * scale,
                which: ImageSignedDist.WIDTH));
            at = at.transz(-0.5f * wi/img.aspect_x_on_y);
            key.voxels(vox);

            at = at.transz(-0.17f*wi);
        }

        add(fromroot("assets/unimelb_ccw.tga"), unimelb: true);
        add(fromroot("assets/csiro_ccw.tga"), scale: 0.92f);

        return vox;
    }

    protected void voxels_bolts(out Voxels hole, out Voxels clearance) {
        hole = new();
        clearance = new();
        for (int i=0; i<pm.no_bolt; ++i) {
            float theta = i*TWOPI/pm.no_bolt;
            Frame frame = new(fromcyl(pm.r_bolt, theta, 0f));

            hole.BoolAdd(new Rod(
                frame,
                pm.flange_thickness_cc + EXTRA,
                pm.D_bolt/2f
            ).extended(EXTRA + extend_base_by, Extend.DOWN));
            clearance.BoolAdd(new Rod(
                frame.transz(pm.flange_thickness_cc),
                3f*EXTRA,
                pm.D_washer/2f
            ));
        }
    }



    /* pea interface: */

    public Voxels? voxels() {

        // Get overall bounding box to size screenshots.
        float overall_Lr = pm.r_bolt
                         + pm.D_bolt/2f
                         + pm.thickness_around_bolt;
        float overall_Lz = cnt_z6 - cnt_z0 + 2f*EXTRA;
        float overall_Mz = overall_Lz/2f;

        // Initialise the part manager.
        using PartMaker part = new(overall_Lz, overall_Lr, overall_Mz);
        if (!take_screenshots)
            part.screenshotta = null;


        // Create the part.

        Geez.Cycle key_gas = new(colour: COLOUR_RED);
        Voxels? gas = voxels_gas(key_gas);
        part.step("created gas cavity.");

        Geez.Cycle key_chnl = new(colour: COLOUR_GREEN);
        Voxels? chnl = voxels_chnl(key_chnl);
        part.step("created channels.");

        Geez.Cycle key_mani = new(colour: COLOUR_BLUE);
        Voxels? neg_mani = voxels_neg_mani(key_mani, out Frame inlet);
        Voxels? pos_mani = voxels_pos_mani(inlet);
        part.step("created manifold.");

        Geez.Cycle key_tc = new(colour: COLOUR_PINK);
        voxels_tc(key_tc, out Voxels? neg_tc, out Voxels? pos_tc);
        part.step("created thermocouples.");

        voxels_bolts(out Voxels? neg_bolt_hole,
                out Voxels? neg_bolt_clearance);
        part.step("created bolt holes.");

        Geez.Cycle key_flange = new(colour: COLOUR_YELLOW);
        Voxels? flange = voxels_flange(key_flange);
        part.step("created flange.");

        Geez.Cycle key_branding = new(colour: COLOUR_CYAN);
        Voxels? branding = brandingless
                         ? null
                         : voxels_branding(key_branding);
        if (branding != null)
            part.step("created branding.");
        else if (brandingless)
            part.no_step("skipping branding (brandingless requested).");
        else
            part.no_step("skipping branding (voxel size too large).");

        part.voxels = voxels_outer(part.key);
        part.substep("contoured outer wall.");

        // Fillet the throat to stop it looking like a discontinuity lmao.
        if (!filletless) {
            float FR = 0.75f*R_tht;
            Voxels mask = new Donut(
                new Frame(cnt_z4*uZ3),
                R_tht + th_iw + th_chnl + th_ow,
                FR + 8f
            );
            using (Lifted l = new(part.voxels, mask))
                Fillet.concave(l.vox, FR, inplace: true);
            part.substep("filleted throat.", view_part: true);
        } else {
            part.substep("skipping throat fillet (filletless requested).");
        }

        part.step("created outer wall.");

        part.add(ref pos_mani);
        part.substep($"added positive manifold.");
        part.add(ref pos_tc, key_tc);
        part.substep($"added thermocouples.");
        part.add(ref flange, key_flange);
        part.substep($"added flange.");

        part.step($"added outer componenets.");

        part.voxels.BoolSubtract(new Rod(
            new Frame(cnt_z6*uZ3),
            2f*EXTRA,
            cnt_r6 + th_iw + th_chnl + th_ow + 2f*EXTRA
        ));
        part.substep("clipped top excess.", view_part: true);

        part.sub(ref neg_bolt_clearance, keepme: true);
        part.substep("subtracted bolt hole clearance (1/2).");

        if (!filletless) {
            Fillet.both(part.voxels,
                concave_FR: pm.concave_fillet_radius,
                convex_FR: pm.convex_fillet_radius,
                inplace: true
            );
            part.substep("filleted part.", view_part: true);
        } else {
            part.substep("skipping part fillet (filletless requested).");
        }

        part.step("partial clean up.");

        part.sub(ref gas, key_gas);
        part.substep("subtracted gas cavity.");
        part.sub(ref chnl, key_chnl);
        part.substep("subtracted channels.");
        part.sub(ref neg_mani, key_mani);
        part.substep("subtracted negative manifold.");
        part.sub(ref neg_tc);
        part.substep("subtracted negative thermocouples.");
        part.sub(ref neg_bolt_clearance);
        part.substep("subtracted bolt hole clearance (2/2).");
        part.sub(ref neg_bolt_hole);
        part.substep("subtracted bolt hole.");

        part.step($"added inner componenets.");

        if (branding != null) {
            part.add(ref branding, key_branding);
            part.substep("added branding.");
        } else {
            part.substep("no branding to add.");
        }

        part.voxels.BoolIntersect(new Rod(
            new(),
            cnt_z6,
            pm.r_bolt + pm.D_bolt/2f + pm.thickness_around_bolt + 2f*EXTRA
        ).extended(extend_base_by, Extend.DOWN));
        part.substep("clipped bottom excess.", view_part: true);

        part.step("finished.");

        return part.voxels;
    }


    public Voxels? cutaway(in Voxels part) {

        // something to look at.
        Geez.voxels(part);

        float z = cnt_z1;
        Frame f1 = new(
            fromcyl(
                cnt_radius_at(z, th_iw + th_chnl + th_ow + 2f, false),
                PI_2,
                z
            ),
            uY3,
            uZ3
        );
        f1 = f1.rotzx(torad(90f - 22f));
        f1 = f1.transz(-75f, false);
        f1 = f1.transy(-5f, false);
        f1 = f1.transx(50f);
        f1 = f1.cyclecw();

        Frame f2 = new(fromcyl(pm.r_bolt, 3*TWOPI/pm.no_bolt, 0f));
        f2 = f2.transx(-f2.pos.X, false);
        f2 = f2.transz(-EXTRA, false);
        f2 = f2.cycleccw();
        f2 = f2.flipyz();


        Bar cube1 = new Bar(f1, 100f, 350f, 80f);
        cube1 = cube1.at_edge(Bar.X1_Y0);
        Geez.bar(cube1);

        Bar cube2 = new Bar(f2, 50f, 100f, 80f);
        cube2 = cube2.at_edge(Bar.X1_Y0);
        Geez.bar(cube2);

        print("created cutting cubes.");

        Voxels inner = voxels_cnt_filled(th_iw + th_chnl + 0.05f, true, false);
        print("created filled channels.");

        Voxels outer = part.voxDuplicate();
        print("duplicated part.");

        outer.BoolSubtract(inner);
        Voxels tmp = (Voxels)cube1;
        tmp.BoolAdd(cube2);
        outer.BoolIntersect(tmp);
        Geez.voxels(outer);
        print("made scissors.");

        Voxels cutted = part - outer;
        Geez.voxels(cutted);
        Geez.remove(Geez.recent(4, 1));
        print("finished.");
        print();

        return cutted;
    }


    public void drawings(in Voxels part) {

        float Lr = pm.r_bolt
                 + pm.D_bolt/2f
                 + pm.thickness_around_bolt;
        float Lz = cnt_z6 - cnt_z0;

        Frame frame_xy = new(3f*VOXEL_SIZE*uZ3, uX3, uZ3);
        Frame frame_xy_throat = new(cnt_z4*uZ3, uX3, uZ3);
        Frame frame_yz = new(Lz/2f*uZ3, uY3, uX3);

        void screenshot(string name, ref Voxels? vox, float theta, float phi,
                float zoom) {
            assert(vox != null);
            Geez.Key key = Geez.voxels(vox!);
            new Geez.Screenshotta(
                new Geez.ViewAs(
                    pos: Lz/2f*uZ3,
                    theta: theta,
                    phi: phi,
                    zoom: zoom,
                    bgcol: Geez.BACKGROUND_COLOUR_LIGHT,
                    transparent: false
                )
            ).take(name);
            Geez.remove(key);
            vox = null;
        }

        Voxels? vox_xy = part - new Bar(
            frame_xy.flipyz(),
            EXTRA,
            Lr*2f + EXTRA
        );
        screenshot($"{name}-xy", ref vox_xy, PI_2, 0f, 180f);

        Voxels? vox_xy_throat = part - new Bar(
            frame_xy_throat,
            cnt_z6 - cnt_z4 + EXTRA,
            Lr*2f + EXTRA
        );
        screenshot($"{name}-xy-throat", ref vox_xy_throat, PI_2, PI, 180f);

        Voxels? vox_yz = part - new Bar(
            frame_yz,
            Lr + EXTRA,
            Lz + EXTRA
        );
        screenshot($"{name}-yz", ref vox_yz, PI, PI_2, 280f);


        Mesh mesh = new(part); // only gen once.

        Geez.mesh(mesh); // someting to look at.

        Bar bounds;

        print("cross-sectioning xy...");
        Drawing.to_file(
            $"{name}-xy",
            mesh,
            frame_xy,
            out bounds
        );
        using (Geez.like(colour: COLOUR_BLUE))
            Geez.bar(bounds, divide_x: 3, divide_y: 3);

        print("cross-sectioning xy throat...");
        Drawing.to_file(
            $"{name}-xy-throat",
            mesh,
            frame_xy_throat,
            out bounds
        );
        using (Geez.like(colour: COLOUR_BLUE))
            Geez.bar(bounds, divide_x: 3, divide_y: 3);

        print("cross-sectioning yz...");
        Drawing.to_file(
            $"{name}-yz",
            mesh,
            frame_yz,
            out bounds
        );
        using (Geez.like(colour: COLOUR_GREEN))
            Geez.bar(bounds, divide_x: 3, divide_y: 4);


        print("Baby drawed.");
        print();
    }


    public void anything() {
        Tapping tap = new("Rc1/8");
        Studding stud = new("Rc1/8");
        tap.right_handed = false;
        stud.right_handed = false;

        Frame frame = new();

        Geez.threads(tap, frame);
        Geez.threads(stud, frame.transz(-tap.threaded_length + tap.gauge_length
                     + stud.threaded_length - stud.gauge_length));
        Voxels hole = new Bar(new(), -30f, 30f);
        hole.BoolSubtract(tap.at(frame, extra: 3f));
        hole.BoolIntersect(new Bar(new(), -40f, 40f).at_face(Bar.Y1));
        Geez.voxels(hole, COLOUR_RED);
        Geez.voxels(stud.at(frame, tap, extra: 3f), COLOUR_BLUE);
    }


    public string name => "chamber";


    public bool printable_dmls   = false;
    public bool minimise_mem     = false;
    public bool take_screenshots = false;
    public bool filletless       = false;
    public bool brandingless     = false;
    public void set_modifiers(int mods) {
        printable_dmls   = popbits(ref mods, TPIAP.PRINTABLE_DMLS);
        minimise_mem     = popbits(ref mods, TPIAP.MINIMISE_MEM);
        take_screenshots = popbits(ref mods, TPIAP.TAKE_SCREENSHOTS);
        _                = popbits(ref mods, TPIAP.LOOKIN_FANCY);
        filletless       = popbits(ref mods, TPIAP.FILLETLESS);
        brandingless     = popbits(ref mods, TPIAP.BRANDINGLESS);
        assert(mods == 0, $"disallowed modifiers: 0x{mods:X}");
    }


    public void initialise() {
        initialise_cnt();
        initialise_chnl(); // requires initialised contour.

        // Easiest way to determine if the parameters create a realisable nozzle.

        assert(cnt_z0 < cnt_z1, $"z0={cnt_z0}, z1={cnt_z1}");
        assert(cnt_z1 < cnt_z2, $"z1={cnt_z1}, z2={cnt_z2}");
        assert(cnt_z2 < cnt_z3, $"z2={cnt_z2}, z3={cnt_z3}");
        assert(cnt_z3 < cnt_z4, $"z3={cnt_z3}, z4={cnt_z4}");
        assert(cnt_z4 < cnt_z5, $"z4={cnt_z4}, z5={cnt_z5}");
        assert(cnt_z5 < cnt_z6, $"z5={cnt_z5}, z6={cnt_z6}");
        assert(cnt_zP > cnt_z5, $"zP={cnt_zP}, z5={cnt_z5}");
        assert(cnt_zP < cnt_z6, $"zP={cnt_zP}, z6={cnt_z6}");

        assert(nearto(cnt_r0, cnt_r1), $"r0={cnt_r0}, r1={cnt_r1}");
        assert(cnt_r1 > cnt_r2, $"r1={cnt_r1}, r2={cnt_r2}");
        assert(cnt_r2 > cnt_r3, $"r2={cnt_r2}, r3={cnt_r3}");
        assert(cnt_r3 > cnt_r4, $"r3={cnt_r3}, r4={cnt_r4}");
        assert(cnt_r4 < cnt_r5, $"r4={cnt_r4}, r5={cnt_r5}");
        assert(cnt_r5 < cnt_r6, $"r5={cnt_r5}, r6={cnt_r6}");
        assert(cnt_rP > cnt_r5, $"rP={cnt_rP}, r5={cnt_r5}");
        assert(cnt_rP < cnt_r6, $"rP={cnt_rP}, r6={cnt_r6}");

        assert(nearto(th_chnl, pm.max_th_chnl) || th_chnl < pm.max_th_chnl,
                $"th_chnl={th_chnl}, max_th_chnl={pm.max_th_chnl}");
    }
}
