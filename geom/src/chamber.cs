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
    //
    //
    // Context for 'X':
    // cnt_X = chamber contour construction value.
    // X_cc = chamber property.
    // X_chnl = cooling channel property.
    // X_conv = nozzle converging section property.
    // X_div = nozzle diverging section property.
    // X_exit = nozzle exit property.
    // X_gas = gas/gas volume (aka interior of cc+nozzle) property.
    // X_imani = fuel inlet manifold inner (wall) property.
    // X_inlet = fuel inlet property.
    // X_iw = inner wall property.
    // X_mani = fuel inlet manifold property.
    // X_fixt = fixtures property (note fixtures are at nozzle).
    // X_omani = fuel inlet manifold outer (wall) property.
    // X_ow = outer wall property.
    // X_part = entire chamber+nozzle part property.
    // X_tc = thermocouple property.
    // X_tht = nozzle throat property.
    // X_web = between-cooling-channel wall property.
    //
    // Legend for 'X':
    // A = area.
    // AEAT = nozzle exit area to throat area ratio.
    // beta = angle between two line segments.
    // Bln = bolt length (i.e. 10e-3 for M4x10).
    // Bsz = bolt size (i.e. 4e-3 for M4x10).
    // D_ = discrete change in this variable.
    // ell = arc length.
    // Fr = fillet radius.
    // Ir = inner radius.
    // Ioff = inner offset.
    // L = length.
    // L_ = length along this coordinate path.
    // Mr = middle radius (average of inner and outer).
    // NLF = nozzle length as a fraction of the length of a 15deg cone.
    // no = number of things.
    // off = offset, normal to surface.
    // Ooff = outer offset.
    // Or = outer radius.
    // r = radial coordinate.
    // semith = semi-thickness (half thickness).
    // th = thickness, normal to surface.
    // phi = axial angle relative to +Z.
    // theta = circumferential angular coordinate.
    // wi = circumferential width.
    // x = x coordinate.
    // y = y coordinate.
    // z = z coordinate.

    public required PartMating pm { get; init; }

    public required float L_cc { get; init; }

    public required float AEAT { get; init; }
    public required float r_tht { get; init; }
    public float r_exit = NAN;
    protected void initialise_exit() {
        r_exit = sqrt(AEAT) * r_tht;
    }

    public required float NLF { get; init; }
    public required float phi_conv { get; init; }
    public required float phi_div { get; init; }
    public required float phi_exit { get; init; }

    public required float phi_wid { get; init; }

    public required float th_iw { get; init; }
    public required float th_ow { get; init; }

    public required int no_web { get; init; }
    public required float th_web { get; init; }
    public required float Ltheta_web { get; init; }

    public int no_chnl = -1;
    public float th_chnl = NAN;
    public float Ltheta_chnl = NAN;
    public float A_chnl_exit = NAN;

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
        int j = i + 1;
        float t = z - i;
        float A = theta_chnl_lookup[i];
        float B = theta_chnl_lookup[j];
        return lerp(A, B, t);

        // assert(pm.no_bolt == 8, "change special constant smile");
        // z = z/cnt_z6 - 0.5f;
        // return 7.7f*TWOPI/no_chnl * 0.5f*sin(PI*z);
    }
    protected void initialise_chnl() {
        no_chnl = no_web;
        th_chnl = th_web;
        Ltheta_chnl = TWOPI/no_chnl - Ltheta_web;
        assert(Ltheta_chnl > 0f);

        // Compute theta channel.

        float phi = torad(30f); // MAGIC (heart eyes)

        int N = DIVISIONS;
        theta_chnl_lookup = new float[N];
        // distributed between zlo and zhi, clamped outside.

        float zlo = theta_chnl_lookup_zlo = cnt_z0;
        float zhi = theta_chnl_lookup_zhi = cnt_z6 - th_chnl - th_omani;
        float Dz = (zhi - zlo) / (N - 1);
        float rho = Dz * tan(phi); // radius of difference between points.

        float prev_r = NAN;
        float prev_theta = NAN;
        for (int i=0; i<N; ++i) {
            float z = lerp(zlo, zhi, i, N);
            float r = cnt_radius_at(z, th_iw + 0.5f*th_chnl, true);
            float theta;
            if (i > 0) {
                // got a 3d triangle to solve.
                float cosa = (r*r - prev_r*prev_r - rho*rho)
                           / (2f * prev_r * rho);
                float a = acos(clamp(cosa, 0f, 1f));
                Vec2 step = rho * frompol(1, prev_theta + a);
                Vec2 curr = frompol(prev_r, prev_theta) + step;
                theta = arg(curr);
            } else {
                // kinda required, since theta0_chnl handles any offset.
                theta = 0f;
            }
            prev_r = r;
            prev_theta = theta;
            theta_chnl_lookup[i] = theta;
        }

        // ensure first channel is at stagnation point of manifold.
        float Dtheta = theta_inlet + PI - theta_chnl(zhi);
        for (int i=0; i<N; ++i)
            theta_chnl_lookup[i] += Dtheta;

        A_chnl_exit = Ltheta_chnl/TWOPI * PI*(squared(r_exit + th_iw + th_chnl)
                                            - squared(r_exit + th_iw));
    }

    public required float th_imani { get; init; }
    public required float th_omani { get; init; }
    public required float phi_mani { get; init; }

    public required int no_fixt { get; init; }
    public required float wi_fixt { get; init; }
    public required float phi_fixt { get; init; }

    public required float theta_inlet { get; init; }
    public required float L_inlet { get; init; }
    public required float D_inlet { get; init; }
    public required float th_inlet { get; init; }
    public required float phi_inlet { get; init; }
    public required float Fr_inlet { get; init; }

    public required float theta_tc { get; init; }
    public required int no_tc { get; init; }
    public required float D_tc { get; init; }
    public required float th_tc { get; init; }
    public required float phi_tc { get; init; }
    public required float L_tc { get; init; }


    protected int DIVISIONS => max(200, (int)(200f / VOXEL_SIZE));

    protected const float EXTRA = 10f; // trimmed at some point.


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

        cnt_X = EXTRA + 1.1f*max(pm.Mr_chnl, r_exit);


        cnt_r_conv = 1.5f*r_tht;

        cnt_z0 = 0f;
        cnt_r0 = pm.Or_cc;

        cnt_z1 = L_cc;
        cnt_r1 = pm.Or_cc;

        cnt_z2 = cnt_z1 - cnt_r_conv*sin(phi_conv);
        cnt_r2 = pm.Or_cc - cnt_r_conv*(1f - cos(phi_conv));

        cnt_r3 = r_tht * (2.5f - 1.5f*cos(phi_conv));
        cnt_z3 = cnt_z2 + (cnt_r3 - cnt_r2)/tan(phi_conv);

        cnt_z4 = cnt_z3 - 1.5f*r_tht*sin(phi_conv);
        cnt_r4 = r_tht;

        cnt_z5 = cnt_z4 + 0.382f*r_tht*sin(phi_div);
        cnt_r5 = r_tht*(1.382f - 0.382f*cos(phi_div));

        cnt_z6 = cnt_z4 + NLF*(3.732051f*r_exit - 3.683473f*r_tht);
        cnt_r6 = r_exit;

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
        cnt_wid_r4 = pm.Or_cc + th_iw + 0.5f*th_web;

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
        Vec2 d34 = q - new Vec2(cnt_z4, 2.5f*r_tht);
        if (within(arg(d34), -PI_2 + phi_conv, -PI_2)) {
            update(ref dist, -mag(d34) + 1.5f*r_tht);
        }
        Vec2 d56 = q - new Vec2(cnt_z4, 1.382f*r_tht);
        if (within(arg(d56), -PI_2, -PI_2 + phi_div)) {
            update(ref dist, -mag(d56) + 0.382f*r_tht);
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

            // Chuck it threw some newton raphson iters.
            float f;
            float df;
            for (int i=0; i<10; ++i) { // don loop forever.
                eff(S, out f, out df);
                S -= f/df;
                if (f < 1e-3f)
                    break;
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
            r = 2.5f*r_tht - nonhypot(1.5f*r_tht, z - cnt_z4);
        } else if (z <= cnt_z5) {
            r = 1.382f*r_tht - nonhypot(0.382f*r_tht, z - cnt_z4);
        } else if (z <= cnt_z6) {
            float p;
            p = 4f*cnt_para_az*(cnt_para_cz - z);
            p = sqrt(cnt_para_bz*cnt_para_bz - p);
            p = (-cnt_para_bz - p) / 2f / cnt_para_az;
            r = (cnt_para_ar*p + cnt_para_br)*p + cnt_para_cr;
        } else {
            r = r_exit;
        }
        // Also note i couldnt be bothered to put the widened analytic solns.

        r += signed_dist;

        // Now its close the correct solution, but not exactly. So, just kinda
        // brute force it by assuming the distance we are from the actual desired
        // offset is perfectly horizontal. note this has like i think linear
        // convergence so pretty bad but eh its fine.
        for (int i=0; i<10; ++i) { // don loop forever.
            float Dr = signed_dist - sdf(new(r, 0f, z));
            r += Dr; // doctor semi colon.
            if (abs(Dr) < 1e-3)
                break;
            assert(i != 9, $"Dr={Dr}");
        }
        return r;
    }

    protected Voxels voxels_cnt_filled(float max_off, bool widened,
            bool extra=true) {
        List<Vec2> V = new(DIVISIONS + 2);
        float zlo = cnt_z0;
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
        Mesh mesh = Polygon.mesh_revolved(new Frame(), V, slicecount: DIVISIONS);
        return new(mesh);
    }
    protected Voxels voxels_cnt_shelled(float min_off, float th, bool widened,
            bool extra=true) {
        List<Vec2> V = new();
        float zlo = cnt_z0;
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
        Mesh mesh = Polygon.mesh_revolved(new Frame(), V, donut: true,
                                          slicecount: DIVISIONS);
        return new(mesh);
    }

    protected Voxels voxels_gas(Geez.Cycle key) {
        using var __ = key.like();

        // view a cheeky wireframe while its generating.
        if (!minimise_mem) {
            List<int> wireframe = new();
            int N = 16;
            for (int i=0; i<N; ++i) {
                float z = lerp(cnt_z0, cnt_z6, i, N);
                float r = cnt_radius_at(z, 0f, false);
                Pipe pipe = new(
                    new Frame(z*uZ3),
                    1e-2f,
                    r,
                    r + 1e-2f
                );
                int wire = Geez.pipe(pipe, rings: 2, bars: 0);
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
                int wire = Geez.line(contour);
                wireframe.Add(wire);
            }

            key.cycle(Geez.group(wireframe));
        }

        Voxels vox = voxels_cnt_filled(0f, false);
        key.cycle(Geez.voxels(vox));
        return vox;
    }

    protected Mesh mesh_outer_lines(float Mtheta, float Ltheta, float Lr) {
        return new(); // mmm naw.
        int N = 2*DIVISIONS;
        assert(N >= 4);

        List<Vec2> vertices = new(N + 1); // (z,r)
        // ccw winding of vertices about +circum.

        float zlo = cnt_z0;
        float zhi = cnt_z6;
        for (int i=0; i<N/2; ++i) {
            float z = lerp(zhi, zlo, i, N/2);
            float r = cnt_radius_at(z, th_iw + th_chnl + th_ow, true);
            r -= 2f; // safety. can have big factor.
            vertices.Add(new(z, r));
        }
        for (int i=0; i<N/2; ++i) {
            float z = lerp(zlo, zhi, i, N/2);
            float r = cnt_radius_at(z, th_iw + th_chnl + th_ow, true);
            r += Lr;
            vertices.Add(new(z, r));
        }
        // duplicate lowest points downwards by EXTRA.
        vertices.Insert(N/2, vertices[N/2] - EXTRA*uX2);
        vertices.Insert(N/2, vertices[N/2 - 1] - EXTRA*uX2);

        return Polygon.mesh_revolved(
            new Frame().rotxy(Mtheta - Ltheta/2f),
            vertices,
            Ltheta,
            slicecount: max(4, (int)(Ltheta * DIVISIONS / TWOPI)),
            donut: true
        );
    }

    protected Voxels voxels_outer(Geez.Cycle key) {
        using var __ = key.like();

        Voxels vox = voxels_cnt_filled(th_iw + th_chnl + th_ow, true);
        key.voxels(vox);

        List<int> mesh_keys = new();

        float Ltheta = 0.004f*TWOPI;
        float Lr = 1f;
        int no = 52; //%4==0
        Mesh mesh0 = mesh_outer_lines(0f, Ltheta, Lr);
        for (int i=0; i<no; ++i) {
            float Dtheta = i*TWOPI/no;
            Transformer trans = new Transformer().rotate(uZ3, Dtheta);
            Mesh mesh = trans.mesh(mesh0);
            mesh_keys.Add(Geez.mesh(mesh));
            vox.BoolAdd(new(mesh));
        }

        key.cycle(Geez.voxels(vox));
        Geez.remove(mesh_keys);

        return vox;
    }


    protected Mesh mesh_chnl() {
        int N0 = DIVISIONS;
        int N1 = max(4, DIVISIONS/60);
        int M = max(4, (int)(Ltheta_chnl*DIVISIONS/TWOPI));
        M += M & 1; // make even.
        assert(N0 >= 2);
        assert(N1 >= 2);
        assert(M >= 2);

        List<Vec3> frames = new(N0 + N1 + 1);
        List<Vec2> vertices = new((N0 + N1 + 1)*M);
        // ccw winding of vertices about +axial.

        float zlo = cnt_z0;
        float zhi = cnt_z6 - th_omani - th_chnl;
        for (int i=0; i<N0; ++i) {
            float z = lerp(zlo, zhi, i, N0);
            float rlo = cnt_radius_at(z, th_iw, true);
            float rhi = cnt_radius_at(z, th_iw + th_chnl, true);
            float Mtheta = theta_chnl(z);
            float thetalo = Mtheta - Ltheta_chnl/2f;
            float thetahi = Mtheta + Ltheta_chnl/2f;

            frames.Add(z*uZ3);
            for (int j=0; j<M/2; ++j) {
                float theta = lerp(thetahi, thetalo, j, M/2);
                vertices.Add(frompol(rlo, theta));
            }
            for (int j=0; j<M/2; ++j) {
                float theta = lerp(thetalo, thetahi, j, M/2);
                vertices.Add(frompol(rhi, theta));
            }
        }
        // duplicate lowest polygon downwards by EXTRA.
        frames.Insert(0, frames[0] - EXTRA*uZ3);
        for (int j=0; j<M; ++j)
            vertices.Insert(0, vertices[M - 1]);

        zlo = zhi;
        zhi = cnt_z6 - th_omani;
        for (int i=0; i<N1; ++i) {
            float z = lerp(zlo, zhi, i, N1);
            float Mtheta = theta_chnl(z);
            float rlo = cnt_radius_at(z, th_iw, true);
            float rhi = cnt_radius_at(z, th_iw + th_chnl, true);
            float thetalo = Mtheta - Ltheta_chnl/2f;
            float thetahi = Mtheta + Ltheta_chnl/2f;
            rhi += th_chnl/2f; // safety.

            frames.Add(z*uZ3);
            for (int j=0; j<M/2; ++j) {
                float theta = lerp(thetahi, thetalo, j, M/2);
                vertices.Add(frompol(rlo, theta));
            }
            for (int j=0; j<M/2; ++j) {
                float theta = lerp(thetalo, thetahi, j, M/2);
                vertices.Add(frompol(rhi, theta));
            }
        }

        return Polygon.mesh_swept(new FramesCart(frames), vertices);
    }

    protected Mesh mesh_chnl_const_th_web(float wi_web) {
        int N0 = DIVISIONS;
        int N1 = DIVISIONS/20;
        int M = max(5, (int)(Ltheta_chnl*DIVISIONS/TWOPI));
        M += M & 1; // make even.
        assert(N0 >= 2);
        assert(N1 >= 2);
        assert(M >= 2);

        List<Vec3> frames = new(N0 + N1 + 1);
        List<Vec2> vertices = new((N0 + N1 + 1)*M);
        // ccw winding of vertices about +axial.

        float zlo = cnt_z0;
        float zhi = cnt_z6 - th_omani - th_chnl;
        for (int i=0; i<N0; ++i) {
            float z = lerp(zlo, zhi, i, N0);
            float Mtheta = theta_chnl(z);
            float rlo = cnt_radius_at(z, th_iw, true);
            float rhi = cnt_radius_at(z, th_iw + th_chnl, true);

            frames.Add(z*uZ3);

            float r;
            float wi() => TWOPI*r/no_web - wi_web;

            r = rlo;
            float theta0lo = Mtheta - wi()/2f/r;
            float theta0hi = Mtheta + wi()/2f/r;
            assert(theta0hi > theta0lo);
            for (int j=0; j<M/2; ++j) {
                float theta = lerp(theta0hi, theta0lo, j, M/2);
                vertices.Add(frompol(rlo, theta));
            }
            r = rhi;
            float theta1lo = Mtheta - wi()/2f/r;
            float theta1hi = Mtheta + wi()/2f/r;
            assert(theta1hi > theta1lo);
            for (int j=0; j<M/2; ++j) {
                float theta = lerp(theta1lo, theta1hi, j, M/2);
                vertices.Add(frompol(rhi, theta));
            }
        }
        // duplicate lowest polygon downwards by EXTRA.
        frames.Insert(0, frames[0] - EXTRA*uZ3);
        for (int j=0; j<M; ++j)
            vertices.Insert(0, vertices[M - 1]);

        zlo = zhi;
        zhi = cnt_z6 - th_omani;
        for (int i=0; i<N1; ++i) {
            float z = lerp(zlo, zhi, i, N1);
            float Mtheta = theta_chnl(z);
            float rlo = cnt_radius_at(z, th_iw, true);
            float rhi = cnt_radius_at(z, th_iw + th_chnl + th_imani, true);
            rhi += th_chnl/2f; // safety.

            frames.Add(z*uZ3);

            float r;
            float wi() => TWOPI*r/no_web - wi_web;

            r = rlo;
            float theta0lo = Mtheta - wi()/2f/r;
            float theta0hi = Mtheta + wi()/2f/r;
            for (int j=0; j<M/2; ++j) {
                float theta = lerp(theta0hi, theta0lo, j, M/2);
                vertices.Add(frompol(rlo, theta));
            }
            // dont use rhi here since thats not the outer radius of channel.
            r = cnt_radius_at(z, th_iw + th_chnl, true);
            float theta1lo = Mtheta - wi()/2f/r;
            float theta1hi = Mtheta + wi()/2f/r;
            for (int j=0; j<M/2; ++j) {
                float theta = lerp(theta1lo, theta1hi, j, M/2);
                vertices.Add(frompol(rhi, theta));
            }
        }

        return Polygon.mesh_swept(new FramesCart(frames), vertices);
    }


    protected Voxels voxels_chnl(Geez.Cycle key) {
        using var __ = key.like();

        Voxels vox = new();
        List<int> mesh_keys = new();

      #if false
        print("th_chnl,   30deg helix angle,  1.5mm wi_web: "
           + $"{(TWOPI*r_tht/no_web - 1.5f)*cos(torad(30f))} mm");
        print("th_chnl,   30deg helix angle,  1.5mm th_web: "
           + $"{TWOPI*r_tht/no_web*cos(torad(30f)) - 1.5f} mm");
        print("wi_chnl,   30deg helix angle,  1.5mm wi_web: "
           + $"{TWOPI*r_tht/no_web - 1.5f} mm");
        print("wi_chnl,   30deg helix angle,  1.5mm th_web: "
           + $"{TWOPI*r_tht/no_web - 1.5f/cos(torad(30f))} mm");
      #endif
        float wi_web = 1.5f / cos(torad(30f));

        Mesh mesh0 = mesh_chnl_const_th_web(wi_web); // TODO: hi
        for (int i=0; i<no_chnl; ++i) {
            float Dtheta = i*TWOPI/no_chnl;
            Transformer trans = new Transformer().rotate(uZ3, Dtheta);
            Mesh mesh = trans.mesh(mesh0);
            mesh_keys.Add(Geez.mesh(mesh));
            vox.BoolAdd(new(mesh));
        }

        key.cycle(Geez.voxels(vox));
        Geez.remove(mesh_keys);

        if (!filletless) {
            Fillet.convex(vox, 0.4f, inplace: true);
            key.cycle(Geez.voxels(vox));
        }

        return vox;
    }

    protected const float min_A_neg_mani = 18f; // mm^2
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
            out List<Vec2> pos, out Vec3 inlet, out float Lr) {
        // note neg_min_z for neg is only a "construction" value, it is filleted
        // so is not the true minimum.

        /* Neg is a outer-wall hugging triangle thing:
    |---,._   - a (filleted)
    |      `-,
    |         `-,
    |           `;  - c (filleted)
   |          ,-'
   |      ,-'
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
        float Fr_a = 2f;
        float Fr_b = 1.5f;
        float Fr_c = 2f;
        int divisions_a = DIVISIONS/100;
        int divisions_c = DIVISIONS/80;

        // Get the contour hugging the outer wall.
        neg = new();
        float neg_max_z = cnt_z6 - th_omani;
        for (int i=0; i<DIVISIONS/8; ++i) {
            float z = lerp(neg_max_z, neg_min_z, i, DIVISIONS/8);
            float r = cnt_radius_at(z, th_iw + th_chnl + th_imani, false);
            neg.Add(new(z, r));
        }
        // Get an "overhanging" point (to ensure the channel isnt partially
        // restricted by being in a non-right-angle corner).
        Vec2 a = neg[0] + uY2*th_chnl;
        // Get the lowest point.
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
        Vec2 d = new(cnt_z6, r_tht); // throat radius reasonable lower extreme.
        Vec2 e = new(
            (d.Y - b.Y)/tan(phi_mani) + b.X - th_omani/sin(phi_mani),
            r_tht
        );

        // Now fillet the lower corner.
        neg.Add(c); // temp, to ensure the line.
        int divs_fillet = DIVISIONS/10;
        Polygon.fillet(neg, numel(neg) - 2, Fr_b, divisions: divs_fillet);
        neg.RemoveAt(numel(neg) - 1);

        // Note that this fillet has made the polygon variable-length, so
        // resample it to a fixed amount.
        List<Vec2> wall = neg[..^(divs_fillet + 1)];
        List<Vec2> fillet = neg[^divs_fillet..];
        wall = Polygon.resample(wall, DIVISIONS/25);
        fillet = Polygon.resample(fillet, DIVISIONS/60);
        // Add the other two points to make the final closed loop.
        neg = [..wall, ..fillet, c, a];

        // Fixed-count fillet for the outer corner and the overhang corner.
        Polygon.fillet(neg, numel(neg) - 2, Fr_c, divisions: divisions_c);
        Polygon.fillet(neg, numel(neg) - 1, Fr_a, divisions: divisions_a);

        // Pos so easyyy with all fixed-division fillets.
        pos = [d, e, C, A];
        Polygon.fillet(pos, 3, Fr_a + th_omani, divisions: divisions_a);
        Polygon.fillet(pos, 2, Fr_c + th_omani, divisions: divisions_c);

        // Place the inlet some way up the lower edge. Note the last coordinate
        // stores the axial angle.
        inlet = rejxy((2f*b + 3f*c)/5f, phi_inlet + PI);

        // Output the lower edge projected length to hugely aid the numerical
        // search for matching area.
        Lr = abs((c - b).Y);
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
        float max_phi = PI_4;
        float cosa = cos(phi_exit);
        float sina = sin(phi_exit);
        float tanb = tan(max_phi);
        float L = 2f*sqrt(A/(cosa*cosa*tanb - sina*sina/tanb));
        float Az = L*cosa;
      #if false
        float Ar = L*sina;
        float Bz = L/2f*(sina/tanb + cosa);
        float Br = L/2f*(sina + cosa*tanb);
      #endif
        float min_z = cnt_z6 - th_ow - Az; // guess.
        Vec3 inlet_ZRp = new(NAN, NAN, NAN);
        for (int i=0; i<10; ++i) {
            points_maybe_mani(min_z, out neg, out pos, out inlet_ZRp,
                    out float Lr);
            float rem_A = A - Polygon.area(neg);
            float Dz = -rem_A/Lr; // very good guess, but still a guess.
            min_z += Dz;
            assert(min_z > cnt_z4);
            if (abs(Dz) < 1e-3 && abs(rem_A) < 1e-2)
                break;
            assert(i != 9, $"Dz={Dz}, rem_A={rem_A}");
        }
        inlet = new(
            fromcyl(inlet_ZRp.Y, theta, inlet_ZRp.X),
            fromcyl(1f, theta + PI_2, 0f),
            fromsph(1f, theta, inlet_ZRp.Z)
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

        float zextra = 1.5f;
        vox.BoolAdd(new Pipe(
            inlet,
            L_inlet,
            D_inlet/2f
        ).extended(zextra, EXTEND_DOWN)
         .extended(EXTRA, EXTEND_UP)
         .voxels());
        key.cycle(Geez.voxels(vox));

        if (!filletless) {
            Voxels mask = new Pipe(
                inlet.transz(-zextra),
                zextra + 1.1f*Fr_inlet,
                D_inlet/2f + 1.1f*Fr_inlet
            ).voxels();
            using (Lifted l = new(vox, mask))
                Fillet.concave(l.vox, Fr_inlet, inplace: true);
            key.cycle(Geez.voxels(vox));
        }

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
            List<Vec2> points = [new(cnt_z6, r_tht)];
            points.Add(new(
                points[^1].X,
                r_exit + th_iw + th_chnl + th_omani + 15f
            ));
            points.Add(points[^1] - uX2*14);
            points.Add(
                Polygon.line_intersection(
                    points[^1], points[^1] - ONE2,
                    points[0], points[0] - uX2,
                    out _
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
        float Lr = D_inlet/2f + th_inlet;

        Frame inlet_end = inlet.transz(L_inlet).flipzx().rotxy(PI_2);

        float phi = PI_2 + phi_inlet;
        float ell = (magxy(inlet_end.pos) - r_tht)/cos(phi);
        vox.BoolAdd(new Pipe(
            inlet_end,
            L_inlet,
            Lr
        ).extended(zextra, EXTEND_UP)
         .voxels());

        if (!filletless) {
            Voxels mask = new Pipe(
                inlet.transz(-zextra),
                zextra + 1.1f*Fr_inlet,
                D_inlet/2f + 1.1f*Fr_inlet
            ).voxels();
            using (Lifted l = new(vox, mask))
                Fillet.concave(l.vox, Fr_inlet, inplace: true);
        }

        vox.BoolAdd(new Cuboid(
            inlet_end.rotxy(-PI_4),
            L_inlet,
            Lr
        ).extended(zextra, EXTEND_UP)
         .at_corner(CORNER_x0y0z0)
         .voxels());
        vox.BoolAdd(new Cuboid(
            inlet_end,
            ell,
            4.5f,
            L_inlet + zextra
        ).at_edge(EDGE_x0z0)
         .voxels());

        return vox;
    }

    protected List<Vec3> points_tc() {
        float min_z = cnt_z0;
        float max_z = cnt_z6;
        float dif = (max_z - min_z)/(no_tc + 2);
        min_z += dif;
        max_z -= dif;
        List<Vec3> line = new();
        for (int i=0; i<DIVISIONS; ++i) {
            float z = lerp(min_z, max_z, i, DIVISIONS);
            float theta = theta_chnl(z);
            float r = cnt_radius_at(z, th_iw + 0.5f*th_chnl, true);
            line.Add(fromcyl(r, theta, z));
        }

        List<float> dists = new(numel(line)){ 0f };
        float sumlen = 0f;
        for (int i=1; i<numel(line); ++i) {
            sumlen += mag(line[i] - line[i - 1]);
            dists.Add(sumlen);
        }

        List<Vec3> points = new(no_tc);
        for (int i=0; i<no_tc; ++i) {
            float target = i*sumlen/(no_tc - 1);
            int lo = 0;
            int hi = numel(dists) - 1;
            int idx = 0;
            while (lo <= hi) {
                int mid = (lo + hi) / 2;
                if (dists[mid] >= target) {
                    idx = mid; // maybe found.
                    hi = mid - 1;
                } else {
                    lo = mid + 1;
                }
            }
            if (idx == 0) {
                points.Add(line[idx]);
            } else {
                float d0 = dists[idx - 1];
                float d1 = dists[idx];
                float t = (target - d0) / (d1 - d0);
                Vec3 p = t*line[idx] + (1f - t)*line[idx - 1];
                points.Add(p);
            }
        }
        return points;
    }

    protected void voxels_tc(Geez.Cycle key, out Voxels neg, out Voxels pos) {
        List<Vec3> points = points_tc();
        neg = new();
        pos = new();
        float neg_Lr = 0.5f*D_tc;
        float pos_Lr = 0.5f*D_tc + th_tc;
        foreach (Vec3 p in points) {
            Frame frame = Frame.cyl_radial(p);
            float Lz = cnt_radius_at(p.Z, th_iw + 0.5f*th_chnl + L_tc, true)
                     - magxy(p);

            Voxels this_neg = new Pipe(
                frame,
                Lz,
                neg_Lr
            ).extended(EXTRA, EXTEND_UP)
             .voxels();
            this_neg.IntersectImplicit(new Cone(
                frame.transz(th_chnl/2f + th_ow),
                +INF,
                Cone.as_phi(PI_4)
            ));

            Voxels this_pos = new Pipe(
                frame,
                Lz,
                pos_Lr
            ).extended(2*EXTRA, EXTEND_DOWN)
             .voxels();
            this_pos.BoolAdd(new Cuboid(
                frame.rotxy(PI_4),
                Lz,
                pos_Lr
            ).extended(2*EXTRA, EXTEND_DOWN)
             .at_corner(CORNER_x0y0z0)
             .voxels());
            Cuboid web = new Cuboid(
                frame,
                th_tc,
                100f,
                Lz
            ).extended(2*EXTRA, EXTEND_DOWN)
             .at_edge(EDGE_y0z0);
            this_pos.BoolAdd(web.voxels());
            this_pos.IntersectImplicit(new Space(
                frame.translate(new Vec3(0f, -pos_Lr/SQRTH + web.Lx/2f, Lz))
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
        List<int> keys = new();

        Voxels vox;

        vox = new Pipe(
            new Frame(),
            pm.flange_thickness,
            pm.flange_outer_radius
        ).extended(EXTRA, EXTEND_DOWN)
         .voxels();
        keys.Add(Geez.voxels(vox));

        for (int i=0; i<pm.no_bolt; ++i) {
            float theta = i*TWOPI/pm.no_bolt;
            float r = pm.Mr_bolt;
            float Lz = pm.flange_thickness;
            float Lr = pm.Bsz_bolt/2f + pm.thickness_around_bolt;
            Vec3 p = fromcyl(r, theta, 0f);

            List<Vec2> tracezr = new();
            float rlo = pm.Or_cc + th_iw + th_chnl + th_ow - r;
            float zhi = squared((Lr - rlo)/Lr/2f)*10f;
            int N = DIVISIONS/16;
            for (int j=0; j<N; ++j) {
                float x = squared(j/(float)(N - 1))*(zhi + 2f*EXTRA);
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
            float length = 2f*(r - pm.Or_cc - th_iw - th_chnl - th_ow);
            Vec2 A = frompol(Lr, -PI - theta_stop)
                   + frompol(length, -PI - theta_stop + PI_2);
            Vec2 B = frompol(Lr, -PI + theta_stop)
                   + frompol(length, -PI + theta_stop - PI_2);
            if (A.Y < B.Y) {
                Vec2 C = Polygon.line_intersection(
                    A, tracexy[^1],
                    B, tracexy[0],
                    out _
                );
                tracexy.Add(C);
            } else {
                tracexy.Add(A);
                tracexy.Add(B);
            }

            Voxels this_vox = new(Polygon.mesh_extruded(
                Frame.cyl_axial(p),
                Lz + zhi,
                tracexy,
                extend_by: 2f*EXTRA
            ));
            this_vox.BoolSubtract(new(Polygon.mesh_extruded(
                Frame.cyl_circum(p + (Lz - 1f)*uZ3),
                2f*Lr,
                tracezr,
                at_middle: true,
                extend_by: 2f*EXTRA
            )));
            this_vox.BoolAdd(new Pipe(
                new Frame(p),
                Lz,
                Lr
            ).extended(EXTRA, EXTEND_DOWN)
             .voxels());
            keys.Add(Geez.voxels(this_vox));
            vox.BoolAdd(this_vox);
        }

        key.cycle(Geez.group(keys));

        return vox;
    }

    protected Voxels? voxels_branding(Geez.Cycle key) {
        if (VOXEL_SIZE > 0.5)
            return null;

        int scale = (VOXEL_SIZE < 0.1) ? 2 : 1; // might as well.
        SDFimage image = new(fromroot("assets/unimelblogo.tga"), scale: scale);

        Frame centre = new((cnt_z1 - 15f)*uZ3, -uY3, uZ3);
        float Lz = 20f;
        float Lr = 1.5f;
        float R = cnt_r1 + th_iw + th_chnl + th_ow - 0.4f;
        float length = Lz * image.aspect_x_on_y;
        Voxels vox = image.voxels_on_cyl(vertical: true, centre, R, Lr, length);

        key.voxels(vox);

        return vox;
    }

    protected Voxels voxels_neg_bolts(Geez.Cycle key) {
        using var __ = key.like();

        Voxels vox = new();
        List<int> keys = new(pm.no_bolt);
        for (int i=0; i<pm.no_bolt; ++i) {
            float theta = i*TWOPI/pm.no_bolt;
            Frame frame = new Frame(fromcyl(pm.Mr_bolt, theta, 0f));
            Pipe hole = new Pipe(frame, pm.flange_thickness, pm.Bsz_bolt/2f);
            keys.Add(Geez.pipe(hole));

            vox.BoolAdd(hole.extended(EXTRA, EXTEND_DOWN).voxels());
            vox.BoolAdd(new Pipe(
                frame.transz(pm.flange_thickness),
                3f*EXTRA,
                pm.Bsz_bolt/2f + 3f
            ).voxels());
        }

        // Also view o-ring grooves.
        keys.Add(Geez.pipe(new Pipe(
            new(ZERO3, -uZ3),
            pm.Lz_Ioring,
            pm.Ir_Ioring,
            pm.Or_Ioring
        ), rings: 2, bars: 6));
        keys.Add(Geez.pipe(new Pipe(
            new(ZERO3, -uZ3),
            pm.Lz_Ooring,
            pm.Ir_Ooring,
            pm.Or_Ooring
        ), rings: 2, bars: 6));

        key.cycle(Geez.group(keys));
        return vox;
    }



    /* pea interface: */

    delegate void _Op(in Voxels vox);
    public Voxels? voxels() {
        /* cheeky timer. */
        System.Diagnostics.Stopwatch stopwatch = new();
        stopwatch.Start();
        using var _ = Scoped.on_leave(() => {
            print($"Baby made in {stopwatch.Elapsed.TotalSeconds:N1}s.");
            print();
        });


        /* disable rendering if minimising mem. */
        if (minimise_mem)
            print("minimising memory means no rendering, going dark...");
        using var __ = minimise_mem
                     ? Geez.locked()
                     : Scoped.do_nothing();


        /* see if we wanna do cutaway actually. */
        if (cutaway) {
            do_cutaway();
            return null;
        }


        /* create the part object and its key. */
        Voxels part = new();
        Geez.Cycle key_part = new();


        /* create overall bounding box to size screenshots. */
        float overall_Lr = pm.Mr_bolt
                         + pm.Bsz_bolt/2f
                         + pm.thickness_around_bolt;
        float overall_Lz = cnt_z6 - cnt_z0 + 2f*EXTRA;
        float overall_Mz = overall_Lz/2f - EXTRA;
        BBox3 overall_bbox = new(
            new Vec3(-overall_Lr, -overall_Lr, overall_Mz - overall_Lz/2f),
            new Vec3(+overall_Lr, +overall_Lr, overall_Mz + overall_Lz/2f)
        );
        // Since we're actually viewing a pipe, not a box, scale down a little.
        overall_bbox.Grow(-0.2f*overall_Lr);

        float lookat_theta = torad(135f);
        float lookat_phi = torad(105f);
        Geez.lookat(overall_bbox, lookat_theta, lookat_phi);


        /* concept of "steps", which the construction is broken into. each step
           is also screenshotted (if requested). */
        int step_count = 0;
        void step(string msg, bool view_part=false) {
            ++step_count;
            if (view_part)
                key_part.voxels(part);
            if (take_screenshots) {
                using (Geez.remember_current_layout()) {
                    using var _ = Geez.ViewerHack.locked();

                    Geez.lookat(overall_bbox, lookat_theta, lookat_phi);
                    Geez.screenshot(step_count.ToString());
                }
            }
            print($"[{step_count,2}] {msg}");
        }
        void substep(string msg, bool view_part=false) {
            if (view_part)
                key_part.voxels(part);
            print($"   | {msg}");
        }
        void no_step(string msg) {
            ++step_count;
            if (take_screenshots)
                Geez.wipe_screenshot(step_count.ToString());
            print($"[--] {msg}");
        }


        /* shorthand for adding/subtracting a component into the part. */
        void _op(_Op func, ref Voxels? vox, Geez.Cycle? key, bool keepme) {
            assert(vox != null);
            func(vox!);
            if (key != null)
                key.clear();
            if (!keepme)
                vox = null;
        }
        void add(ref Voxels? vox, Geez.Cycle? key=null, bool keepme=false)
            => _op(part.BoolAdd, ref vox, key, keepme);
        void sub(ref Voxels? vox, Geez.Cycle? key=null, bool keepme=false)
            => _op(part.BoolSubtract, ref vox, key, keepme);


        /* perform all the steps of creating the part. */

        Geez.Cycle key_gas = new(colour: COLOUR_RED);
        Voxels? gas = voxels_gas(key_gas);
        step("created gas.");

        Geez.Cycle key_mani = new(colour: COLOUR_BLUE);
        Voxels? neg_mani = voxels_neg_mani(key_mani, out Frame inlet);
        Voxels? pos_mani = voxels_pos_mani(inlet);
        step("created manifold.");

        Geez.Cycle key_chnl = new(colour: COLOUR_GREEN);
        Voxels? chnl = voxels_chnl(key_chnl);
        step("created channels.");

        Geez.Cycle key_tc = new(colour: COLOUR_PINK);
        voxels_tc(key_tc, out Voxels? neg_tc, out Voxels? pos_tc);
        step("created thermocouples.");

        Geez.Cycle key_bolts = new(colour: new("#404040" /* grey */));
        Voxels? neg_bolts = voxels_neg_bolts(key_bolts);
        substep("created bolt holes.");

        Geez.Cycle key_flange = new(colour: COLOUR_YELLOW);
        Voxels? flange = voxels_flange(key_flange);
        step("created flange.");

        Geez.Cycle key_branding = new(colour: COLOUR_CYAN);
        Voxels? branding = brandingless
                         ? null
                         : voxels_branding(key_branding);
        if (branding != null)
            step("created branding.");
        else if (brandingless)
            no_step("skipping branding (brandingless requested)");
        else
            no_step("skipping branding (voxel size too large, would fail).");

        part = voxels_outer(key_part);
        step("created outer wall.");

        add(ref pos_mani);
        substep($"added positive manifold.", view_part: true);
        add(ref pos_tc, key_tc);
        substep($"added thermocouples.", view_part: true);
        add(ref flange, key_flange);
        substep($"added flange.", view_part: true);

        step($"added outer componenets.");

        part.BoolSubtract(new Cuboid(
            new Frame(cnt_z6*uZ3),
            2f*EXTRA,
            2f*(cnt_r6 + th_iw + th_chnl + th_ow + EXTRA)
        ).voxels());
        substep("clipped top excess.", view_part: true);

        if (!filletless) {
            Fillet.concave(part, 3f, inplace: true);
            step("filleted.", view_part: true);
        } else {
            no_step("skipping fillet.");
        }

        if (branding != null) {
            add(ref branding, key_branding);
            step("added branding.");
        } else {
            no_step("no branding to add.");
        }

        sub(ref gas, key_gas, keepme: cutaway);
        substep("subtracted gas cavity.", view_part: true);
        sub(ref chnl, key_chnl, keepme: cutaway);
        substep("subtracted channels.", view_part: true);
        sub(ref neg_mani, key_mani);
        substep("subtracted negative manifold.", view_part: true);
        sub(ref neg_tc);
        substep("subtracted negative thermocouples.", view_part: true);
        sub(ref neg_bolts, key_bolts);
        substep("subtracted bolt holes.", view_part: true);

        step($"added inner componenets.");

        part.BoolSubtract(new Cuboid(
            new Frame(ZERO3, -uZ3),
            2f*EXTRA,
            2f*(pm.Mr_bolt + pm.Bsz_bolt/2f + pm.thickness_around_bolt + EXTRA)
        ).voxels());
        substep("clipped bottom excess.", view_part: false);

        step("finished.", view_part: true);

        return part;
    }

    public void do_cutaway() {

        print("birthing alternative offspring.");
        print();

        if (!TPIAP.load_voxels(name, out Voxels? part))
            throw new Exception("cutaway requires part already generated, "
                              + "please re-run the normal voxels");
        assert(part != null);

        Voxels gas;
        Voxels chnl;
        using (Geez.locked()) {
            gas = voxels_gas(new Geez.Cycle());
            chnl = voxels_chnl(new Geez.Cycle());
        }

        Voxels outer = new(part!) ; // copy.

        Voxels inner = voxels_cnt_filled(th_iw + th_chnl - 0.1f, true, false);
        outer.BoolSubtract(inner);

        float Lx = 200f;
        float Ly = 140f;
        float Lz = 100f;
        Frame f = new Frame((cnt_z4 + 45f)*uZ3)
                .rotxy(PI/6f + 0.05f)
                .rotzx(PI/3f)
                .translate(new(38.8f, -Ly/2f, -16f));
        Geez.frame(f);
        f = f.swing(new Vec3(Lx/2f, Ly/2f, 0f), uZ3, torad(-8f));
        Geez.frame(f, pos_colour: COLOUR_BLUE);
        // Transformer rotateme = new Transformer();
        // Vec3 about = f * new Vec3(Lx/2f, Ly/2f, 0f);
        // rotateme = rotateme.translate(-about);
        // rotateme = rotateme.rotate(new(0.5f, 0.8f, 0f), torad(-8f));
        // rotateme = rotateme.translate(about);
        // rotateme.get_rotation(out Vec3 trans_about, out float trans_by);
        // Vec3 trans_shift = rotateme.get_translation();
        // f = new Frame(trans_shift).rotate(trans_about, trans_by).compose(f);

        outer.BoolIntersect(new Cuboid(f, Lx, Ly, Lz).voxels());

        part!.BoolSubtract(outer);

        Mesh mesh = new(part);
        string barcelona = fromroot(
            ".info/PicoGK/ViewerEnvironment/Barcelona.zip"
        );
        try { PICOGK_VIEWER.LoadLightSetup(barcelona); }
        catch { print($"oops, no barcelona lightmap at '{barcelona}'"); }
        using (Geez.like(metallic: 0.4f, roughness: 0.1f))
            Geez.mesh(mesh);
        TPIAP.save_mesh_only("chamber_cutaway", mesh);
    }


    public void drawings(in Voxels part) {
        Mesh mesh = new(part); // only gen once.

        if (!minimise_mem)
            Geez.mesh(mesh); // someting to look at.

        Cuboid bounds;

        Frame frame_xy = new(3f*VOXEL_SIZE*uZ3, uX3, uZ3);
        print("cross-sectioning xy...");
        Drawing.to_file(
            fromroot($"exports/chamber_xy.svg"),
            mesh,
            frame_xy,
            out bounds
        );
        using (Geez.like(colour: COLOUR_BLUE))
            Geez.cuboid(bounds, divide_x: 3, divide_y: 3);

        Frame frame_yz = new(ZERO3, uY3, uX3);
        print("cross-sectioning yz...");
        Drawing.to_file(
            fromroot($"exports/chamber_yz.svg"),
            mesh,
            frame_yz,
            out bounds
        );
        using (Geez.like(colour: COLOUR_GREEN))
            Geez.cuboid(bounds, divide_x: 3, divide_y: 4);

        print("Baby drawed.");
        print();
    }


    public void anything() {

        // List<int> l = [0, 1, 2, 3, 4, 5];
        // Slice<int> s = new(l, 1, 3, 2);

        // Slice<int> t = s.slice(1, 5, 2);
        // foreach (int i in t)
        //     print(i);
        // print();



        static void printme(Slice<int> s) {
            string m = "";
            foreach (int i in s) {
                m += $" {i,2}";
            }
            print(m);
        }
        Slice<int> s = [0,1,2,3,4,5,6,7,8,9];
        printme(s);
        // printme(s.subslice(3, 2, rep:4));
        // printme(s.subslice(3, 2, -1));
        // printme(s.subslice(3, -2, -1, rep:4));
        printme(s.subslice(5, 2));
        printme(s.subslice(5, -2));
        printme(s.subslice(5, 2, 2));
        printme(s.subslice(5, -2, 2));
        printme(s.subslice(5, -2, 2).reversed());
    }


    public string name => "chamber";


    public bool filletless       = false;
    public bool cutaway          = false;
    public bool minimise_mem     = false;
    public bool take_screenshots = false;
    public bool brandingless     = false;
    public void set_modifiers(int mods) {
        filletless       = popbits(ref mods, TPIAP.FILLETLESS);
        cutaway          = popbits(ref mods, TPIAP.CUTAWAY);
        minimise_mem     = popbits(ref mods, TPIAP.MINIMISE_MEM);
        take_screenshots = popbits(ref mods, TPIAP.TAKE_SCREENSHOTS);
        brandingless     = popbits(ref mods, TPIAP.BRANDINGLESS);
        assert(mods == 0, $"unrecognised modifiers: 0x{mods:X}");
    }


    // not part of tpiap.pea but invoked when tpiap constructors the chamber
    // object.
    public void initialise() {
        initialise_exit();
        initialise_cnt();
        initialise_chnl();

        // Easiest way to determine if the parameters create a realisable nozzle.

        assert(cnt_z0 < cnt_z1, $"z0={cnt_z0}, z1={cnt_z1}");
        assert(cnt_z1 < cnt_z2, $"z1={cnt_z1}, z2={cnt_z2}");
        assert(cnt_z2 < cnt_z3, $"z2={cnt_z2}, z3={cnt_z3}");
        assert(cnt_z3 < cnt_z4, $"z3={cnt_z3}, z4={cnt_z4}");
        assert(cnt_z4 < cnt_z5, $"z4={cnt_z4}, z5={cnt_z5}");
        assert(cnt_z5 < cnt_z6, $"z5={cnt_z5}, z6={cnt_z6}");
        assert(cnt_zP > cnt_z5, $"zP={cnt_zP}, z5={cnt_z5}");
        assert(cnt_zP < cnt_z6, $"zP={cnt_zP}, z6={cnt_z6}");

        assert(cnt_r0 == cnt_r1, $"r0={cnt_r0}, r1={cnt_r1}");
        assert(cnt_r1 > cnt_r2, $"r1={cnt_r1}, r2={cnt_r2}");
        assert(cnt_r2 > cnt_r3, $"r2={cnt_r2}, r3={cnt_r3}");
        assert(cnt_r3 > cnt_r4, $"r3={cnt_r3}, r4={cnt_r4}");
        assert(cnt_r4 < cnt_r5, $"r4={cnt_r4}, r5={cnt_r5}");
        assert(cnt_r5 < cnt_r6, $"r5={cnt_r5}, r6={cnt_r6}");
        assert(cnt_rP > cnt_r5, $"rP={cnt_rP}, r5={cnt_r5}");
        assert(cnt_rP < cnt_r6, $"rP={cnt_rP}, r6={cnt_r6}");

        assert(th_chnl + 1e-2 >= pm.min_wi_chnl,
                $"th_chnl={th_chnl}, min_wi_chnl={pm.min_wi_chnl}");
    }
}
