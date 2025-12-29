using static br.Br;
using br;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using BBox3 = PicoGK.BBox3;

public class Chamber {

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
    public float theta0_chnl = NAN;
    public float A_chnl_exit = NAN;
    protected float theta_chnl(float z) {
        z = z/cnt_z6 - 0.5f;
        return 4*TWOPI/no_chnl * 0.5f*sin(PI*z);
    }
    protected void initialise_chnl() {
        no_chnl = no_web;
        th_chnl = th_web;
        Ltheta_chnl = TWOPI/no_chnl - Ltheta_web;
        assert(Ltheta_chnl > 0f);

        float ave_theta = 0f;
        int N = 10000;
        for (int i=0; i<N; ++i) {
            float z = cnt_z0 + i*(cnt_z6 - cnt_z0)/N;
            float theta = theta_chnl(z);
            ave_theta += (theta - ave_theta) / (i + 1);
        }
        // theta0_chnl = theta_tc - ave_theta;
        theta0_chnl = theta_tc - theta_chnl(cnt_z6);

        A_chnl_exit = Ltheta_chnl/TWOPI * PI*(squared(r_exit + th_iw + th_chnl)
                                            - squared(r_exit + th_iw));
    }

    public required float th_imani { get; init; }
    public required float th_omani { get; init; }
    public required float phi_mani { get; init; }

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

        cnt_wid_z1 = 0.5f*-phi_wid*cnt_wid_r_s;
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
        // Note that its not universally true that the min function is the true
        // sdf union, but in this case (where the two removed)
        return min(cnt_sdf(p), cnt_wid_sdf(p) + cnt_wid_off);
    }


    protected BBox3 bbox_cnt(float max_off) {
        float r = max(cnt_rQ, cnt_rR) + max_off;
        float zlo = cnt_zQ - max_off;
        float zhi = cnt_zR + max_off;
        return new(new Vec3(-r, -r, zlo), new Vec3(+r, +r, zhi));
    }
    protected BBox3 bbox_cnt_widened(float max_off) {
        float r = max(cnt_rQ, cnt_rR, cnt_wid_rS) + max_off;
        float zlo = min(cnt_zQ, cnt_wid_zS) - max_off;
        float zhi = cnt_zR + max_off;
        return new(new Vec3(-r, -r, zlo), new Vec3(+r, +r, zhi));
    }

    protected Voxels voxels_cnt_gas() {
        SDFfilled sdf = new(cnt_sdf);
        return sdf.voxels(bbox_cnt(sdf.max_off), enforce_faces: false);
    }
    protected Voxels voxels_cnt_ow_filled() {
        SDFfilled sdf = new(cnt_widened_sdf, th_iw + th_chnl + th_ow);
        return sdf.voxels(bbox_cnt_widened(sdf.max_off), enforce_faces: false);
    }

    protected float cnt_radius_at(float z, float signed_dist=0f,
            bool widened=false) {
        assert(within(z, cnt_zQ, cnt_zR), $"z={z}");
        SDFfunction sdf = widened ? cnt_widened_sdf : cnt_sdf;

        // Initially, estimate by just offseting the inner point horizontally by
        // the signed dist.
        float r;
        if (z <= cnt_z1) {
            r = cnt_r0;
        } else if (z <= cnt_z2) {
            r = cnt_r1 - cnt_r_conv + sqrt(cnt_r_conv*cnt_r_conv
                                         - squared(z - cnt_z1));
        } else if (z <= cnt_z3) {
            r = tan(phi_conv)*(z - cnt_z3) + cnt_r3;
        } else if (z <= cnt_z4) {
            r = 2.5f*r_tht - sqrt(2.25f*r_tht*r_tht - squared(z - cnt_z4));
        } else if (z <= cnt_z5) {
            r = 1.382f*r_tht - sqrt(0.145924f*r_tht*r_tht - squared(z - cnt_z4));
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


    protected void mesh_chnl(float theta0, out Mesh wall, out Mesh top) {
        Mesh mesh_points(in List<Vec3> ps) {
            Mesh mesh = new();
            List<Vec3> V = new();
            for (int i=1; i<numel(ps)/4; ++i) {
                Vec3 p000 = ps[i*4 - 4];
                Vec3 p100 = ps[i*4 - 3];
                Vec3 p010 = ps[i*4 - 2];
                Vec3 p110 = ps[i*4 - 1];
                Vec3 p001 = ps[i*4 + 0];
                Vec3 p101 = ps[i*4 + 1];
                Vec3 p011 = ps[i*4 + 2];
                Vec3 p111 = ps[i*4 + 3];

                if (i == 1) {
                    V.Add(p000);
                    V.Add(p100);
                    V.Add(p010);
                    V.Add(p110);
                }
                V.Add(p001);
                V.Add(p101);
                V.Add(p011);
                V.Add(p111);
                int a = numel(V) - 8;
                int b = numel(V) - 4;
                // Front face.
                mesh.nAddTriangle(a, b, b + 2);
                mesh.nAddTriangle(a, b + 2, a + 2);
                // Back face.
                mesh.nAddTriangle(a + 1, a + 3, b + 3);
                mesh.nAddTriangle(a + 1, b + 3, b + 1);
                // Left face.
                mesh.nAddTriangle(a, a + 1, b + 1);
                mesh.nAddTriangle(a, b + 1, b);
                // Right face.
                mesh.nAddTriangle(a + 3, a + 2, b + 2);
                mesh.nAddTriangle(a + 3, b + 2, b + 3);
            }
            // Bottom face.
            mesh.nAddTriangle(0, 2, 3);
            mesh.nAddTriangle(0, 3, 1);
            // Top face.
            int off = numel(V) - 4;
            mesh.nAddTriangle(off + 0, off + 1, off + 3);
            mesh.nAddTriangle(off + 0, off + 3, off + 2);

            mesh.AddVertices(V, out _);
            return mesh;
        }


        float Dt = 0.5f*Ltheta_chnl; // t for theta.
        List<Vec3> points = new(DIVISIONS*4 + 4);
        float zlo = cnt_z0;
        float zhi = cnt_z6 - th_omani;
        for (int i=0; i<DIVISIONS; ++i) {
            float z = zlo + i*(zhi - zlo)/(DIVISIONS - 1);
            float theta = theta0 + theta_chnl(z);
            float thetalo = theta - Dt;
            float thetahi = theta + Dt;
            float rlo = cnt_radius_at(z, th_iw, widened: true);
            float rhi = cnt_radius_at(z, th_iw + th_chnl, widened: true);

            points.Add(tocart(rlo, thetalo, z));
            points.Add(tocart(rhi, thetalo, z));
            points.Add(tocart(rlo, thetahi, z));
            points.Add(tocart(rhi, thetahi, z));
        }
        points.Insert(0, points[3] - uZ3*EXTRA);
        points.Insert(0, points[3] - uZ3*EXTRA);
        points.Insert(0, points[3] - uZ3*EXTRA);
        points.Insert(0, points[3] - uZ3*EXTRA);

        wall = mesh_points(points);

        zlo = cnt_z6 - th_omani - th_chnl;
        zhi = cnt_z6 - th_omani;
        points = new(DIVISIONS/20*4);
        for (int i=0; i<DIVISIONS/20; ++i) {
            float z = zlo + i*(zhi - zlo)/(DIVISIONS/20 - 1);
            float theta = theta0 + theta_chnl(z);
            float thetalo = theta - Dt;
            float thetahi = theta + Dt;
            float rlo = cnt_radius_at(z, th_iw);
            float rhi = cnt_radius_at(z, th_iw + th_chnl + th_imani);
            rhi += th_chnl/2f; // safety.

            points.Add(tocart(rlo, thetalo, z));
            points.Add(tocart(rhi, thetalo, z));
            points.Add(tocart(rlo, thetahi, z));
            points.Add(tocart(rhi, thetahi, z));
        }
        top = mesh_points(points);
    }

    protected Voxels voxels_chnl(ref Geez.Cycle key) {
        Voxels vox = new();
        using (key.like()) {
            List<int> mesh_keys = new();
            for (int i=0; i<no_chnl; ++i) {
                float theta = theta0_chnl + i*TWOPI/no_chnl;
                mesh_chnl(theta, out Mesh wall, out Mesh top);
                mesh_keys.Add(Geez.mesh(wall));
                mesh_keys.Add(Geez.mesh(top));
                vox.BoolAdd(new Voxels(wall));
                vox.BoolAdd(new Voxels(top));
            }
            key <<= Geez.voxels(vox);
            Geez.remove(mesh_keys);

            Fillet.convex(vox, 0.4f, inplace: true);
            key <<= Geez.voxels(vox);
        }
        return vox;
    }

    protected const float min_A_neg_mani = 16f; // mm^2
    protected float A_neg_mani(float theta) {
        // Lerp between initial (at mani inlet) and final (opposite mani inlet).
        // This is so that the cross-sectional area lost from one channel to the
        // next (travelling downstream) is exactly one channel area.
        float Dtheta = wraprad(theta - theta_inlet);
        // in one half-turn, no_chnl/2 channels are passed.
        float t = abs(Dtheta) / (PI / (0.5f*no_chnl));
        float A0 = 0.5f*no_chnl*A_chnl_exit;
        float A = A0 - A_chnl_exit*t;
        A = max(A, min_A_neg_mani);
        return A;
    }

    // Must produce the same number of points each time.
    // Must produce points in anticlockwise winding (meaning an in-order
    //   traversal would travel down the edge closest to the z-axis).
    protected delegate void PointsManiF(List<Vec3> points, float theta);

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
        neg = new(); // =[(z,r),...]
        float neg_max_z = cnt_z6 - th_omani;
        for (int i=0; i<=DIVISIONS/8; ++i) {
            float z = neg_max_z + i*(neg_min_z - neg_max_z)/(DIVISIONS/8);
            float r = cnt_radius_at(z, th_iw + th_chnl + th_imani);
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
        Vec2 A = a + tocart(th_omani/cos(phi_aA), phi_aA);
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
        List<Vec2> wall = neg[..^divs_fillet];
        List<Vec2> fillet = neg[^divs_fillet..];
        wall = Polygon.line_resample(wall, DIVISIONS/25);
        fillet = Polygon.line_resample(fillet, DIVISIONS/60);
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

    protected void points_mani(float theta, out List<Vec3> neg,
            out List<Vec3> pos, out Frame inlet) {
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
        List<Vec2> neg2 = new();
        List<Vec2> pos2 = new();
        Vec3 inlet_ZRp = new(NAN, NAN, NAN);
        for (int i=0; i<10; ++i) {
            points_maybe_mani(min_z, out neg2, out pos2, out inlet_ZRp,
                    out float Lr);
            float rem_A = A - Polygon.area(neg2);
            float Dz = -rem_A/Lr; // very good guess, but still a guess.
            min_z += Dz;
            assert(min_z > cnt_z4);
            if (abs(Dz) < 1e-3 && abs(rem_A) < 1e-2)
                break;
            assert(i != 9, $"Dz={Dz}, rem_A={rem_A}");
        }
        neg = new();
        foreach (Vec2 p in neg2)
            neg.Add(tocart(p.Y, theta, p.X));
        pos = new();
        foreach (Vec2 p in pos2)
            pos.Add(tocart(p.Y, theta, p.X));
        inlet = new(
            tocart(inlet_ZRp.Y, theta, inlet_ZRp.X),
            tocart(1f, theta + PI_2, 0f),
            tocart(1f, theta, as_phi(inlet_ZRp.Z))
        );
    }

    protected Mesh mesh_mani(PointsManiF points_f) {
        Mesh mesh = new();
        List<Vec3> V = new();
        int N = 0;
        for (int n=0; n<=DIVISIONS; ++n) {
            int i;
            int j;
            if (n == DIVISIONS) {
                i = numel(V) - N;
                j = 0;
            } else {
                float theta = theta_inlet + n*TWOPI/DIVISIONS;
                int numel0 = numel(V);
                points_f(V, theta);
                int Dnumel = numel(V) - numel0;
                if (n == 0) {
                    N = Dnumel;
                    continue;
                }
                assert(N == Dnumel, $"N={N}, Dnumel={Dnumel}");
                i = numel(V) - 2*N;
                j = numel(V) - N;
            }
            // Join as quads.
            for (int t0=0; t0<N; ++t0) {
                int t1 = (t0 == N - 1) ? 0 : t0 + 1;
                int a0 = i + t0;
                int a1 = i + t1;
                int b0 = j + t0;
                int b1 = j + t1;
                mesh.nAddTriangle(a0, b0, b1);
                mesh.nAddTriangle(a0, b1, a1);
            }
        }
        mesh.AddVertices(V, out _);
        return mesh;
    }

    protected Voxels voxels_neg_mani(out Frame inlet) {
        Frame? inlet_fr = null;
        PointsManiF get_neg = (List<Vec3> points, float theta) => {
            points_mani(theta, out List<Vec3> neg, out _, out Frame maybeinlet);
            if (closeto(theta, theta_inlet)) {
                assert(inlet_fr == null);
                inlet_fr = maybeinlet;
            }
            points.AddRange(neg);
        };
        Mesh mesh = mesh_mani(get_neg);
        Voxels vox = new(mesh);

        inlet = inlet_fr!;

        float zextra = 1.5f;
        vox.BoolAdd(new Pipe(
            inlet,
            L_inlet,
            D_inlet/2f
        ).extended(zextra, EXTEND_DOWN)
         .voxels());

        Voxels mask = new Pipe(
            inlet.transz(-zextra),
            zextra + 1.1f*Fr_inlet,
            D_inlet/2f + 1.1f*Fr_inlet
        ).voxels();
        using (Lifted l = new(vox, mask))
            Fillet.concave(l.vox, Fr_inlet, inplace: true);

        return vox;
    }

    protected Voxels voxels_pos_mani(Frame inlet) {
        PointsManiF get_pos = (List<Vec3> points, float theta) => {
            points_mani(theta, out _, out List<Vec3> pos, out _);
            points.AddRange(pos);
        };
        Mesh mesh = mesh_mani(get_pos);
        Voxels vox = new(mesh);

        float zextra = 2.5f;
        float Lr = D_inlet/2f + th_inlet;

        Frame inlet_end = inlet.transz(L_inlet).flipxz().rotxy(PI_2);

        float phi = PI_2 + phi_inlet;
        float ell = (magxy(inlet_end.pos) - r_tht)/cos(phi);
        vox.BoolAdd(new Pipe(
            inlet_end,
            L_inlet,
            Lr
        ).extended(zextra, EXTEND_UP)
         .voxels());

        Voxels mask = new Pipe(
            inlet.transz(-zextra),
            zextra + 1.1f*Fr_inlet,
            D_inlet/2f + 1.1f*Fr_inlet
        ).voxels();
        using (Lifted l = new(vox, mask))
            Fillet.concave(l.vox, Fr_inlet, inplace: true);

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
            float z = min_z + i*(max_z - min_z)/(DIVISIONS - 1);
            float theta = theta0_chnl + theta_chnl(z);
            float r = cnt_radius_at(z, th_iw + 0.5f*th_chnl, widened: true);
            line.Add(tocart(r, theta, z));
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

    protected void voxels_tc(out Voxels neg, out Voxels pos) {
        List<Vec3> points = points_tc();
        neg = new();
        pos = new();
        float neg_Lr = 0.5f*D_tc;
        float pos_Lr = 0.5f*D_tc + th_tc;
        foreach (Vec3 p in points) {
            Frame frame = Frame.cyl_radial(p);
            float Lz = cnt_radius_at(p.Z, th_iw + 0.5f*th_chnl + L_tc)
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
        }
    }

    protected Voxels voxels_flange() {
        Voxels vox;

        vox = new Pipe(
            new Frame(),
            pm.flange_thickness,
            pm.flange_outer_radius
        ).extended(EXTRA, EXTEND_DOWN)
         .voxels();

        for (int i=0; i<pm.no_bolt; ++i) {
            float theta = i*TWOPI/pm.no_bolt;
            float r = pm.Mr_bolt;
            float Lz = pm.flange_thickness;
            float Lr = pm.Bsz_bolt/2f + pm.thickness_around_bolt;
            Vec3 p = tocart(r, theta, 0f);

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
                tracexy.Add(tocart(Lr, t));
            }
            float length = 2f*(r - pm.Or_cc - th_iw - th_chnl - th_ow);
            Vec2 A = tocart(Lr, -PI - theta_stop)
                   + tocart(length, -PI - theta_stop + PI_2);
            Vec2 B = tocart(Lr, -PI + theta_stop)
                   + tocart(length, -PI + theta_stop - PI_2);
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

            Voxels this_vox = new Polygon(
                Frame.cyl_axial(p),
                Lz + zhi,
                tracexy
            ).extended(2*EXTRA, EXTEND_UPDOWN)
             .voxels();
            this_vox.BoolSubtract(new Polygon(
                Frame.cyl_circum(p + (Lz - 1f)*uZ3),
                2f*Lr,
                tracezr
            ).at_middle()
             .extended(2*EXTRA, EXTEND_UPDOWN)
             .voxels());
            this_vox.BoolAdd(new Pipe(
                new Frame(p),
                Lz,
                Lr
            ).extended(EXTRA, EXTEND_DOWN)
             .voxels());
            vox.BoolAdd(this_vox);
        }

        return vox;
    }

    protected Voxels voxels_neg_bolts(ref Geez.Cycle key) {
        Voxels vox = new();
        using (key.like()) {
            List<int> keys = new(pm.no_bolt);
            for (int i=0; i<pm.no_bolt; ++i) {
                float theta = i*TWOPI/pm.no_bolt;
                Frame frame = new Frame(tocart(pm.Mr_bolt, theta, 0f));
                Pipe hole = new Pipe(frame, pm.flange_thickness, pm.Bsz_bolt/2f);
                keys.Add(Geez.pipe(hole));

                vox.BoolAdd(hole.extended(EXTRA, EXTEND_DOWN).voxels());
                vox.BoolAdd(new Pipe(
                    frame.transz(pm.flange_thickness),
                    3f*EXTRA,
                    pm.Bsz_bolt/2f + 3f
                ).voxels());
            }
            key <<= Geez.group(keys);
        }
        return vox;
    }

    public Voxels voxels() {
        Voxels part = new();
        Geez.Cycle key_part = new();

        void add(ref Voxels vox, Geez.Cycle? key=null) {
            part.BoolAdd(vox);
            using (key_part.like())
                key_part <<= Geez.voxels(part);
            if (key != null)
                key.clear();
            vox.Dispose();
            vox = new();
        }
        void sub(ref Voxels vox, Geez.Cycle? key=null) {
            part.BoolSubtract(vox);
            using (key_part.like())
                key_part <<= Geez.voxels(part);
            if (key != null)
                key.clear();
            vox.Dispose();
            vox = new();
        }

        // We view a few things as they are updated.
        Geez.Cycle key_gas = new(colour: COLOUR_RED);
        Geez.Cycle key_mani = new(colour: COLOUR_BLUE);
        Geez.Cycle key_chnl = new(colour: COLOUR_GREEN);
        Geez.Cycle key_tc = new(colour: COLOUR_PINK);
        Geez.Cycle key_bolts = new(colour: COLOUR_BLACK);
        Geez.Cycle key_flange = new(colour: COLOUR_YELLOW);

        Voxels gas = voxels_cnt_gas();
        using (key_gas.like())
            key_gas <<= Geez.voxels(gas);
        log("created gas.");

        Voxels neg_mani = voxels_neg_mani(out Frame inlet);
        using (key_mani.like())
            key_mani <<= Geez.voxels(neg_mani);
        log("created negative manifold.");

        Voxels pos_mani = voxels_pos_mani(inlet);
        log("created positive manifold.");

        Voxels chnl = voxels_chnl(ref key_chnl);
        log("created channels.");

        voxels_tc(out Voxels neg_tc, out Voxels pos_tc);
        using (key_tc.like())
            key_tc <<= Geez.voxels(pos_tc);
        log("created thermocouples.");

        Voxels neg_bolts = voxels_neg_bolts(ref key_bolts);

        // Also view o-ring grooves.
        Geez.pipe(new Pipe(
            new(ZERO3, -uZ3),
            pm.Lz_Ioring,
            pm.Ir_Ioring,
            pm.Or_Ioring
        ), colour: COLOUR_BLACK, rings: 2, bars: 6);
        Geez.pipe(new Pipe(
            new(ZERO3, -uZ3),
            pm.Lz_Ooring,
            pm.Ir_Ooring,
            pm.Or_Ooring
        ), colour: COLOUR_BLACK, rings: 2, bars: 6);

        Voxels flange = voxels_flange();
        using (key_flange.like())
            key_flange <<= Geez.voxels(flange);
        log("created flange.");

        part = voxels_cnt_ow_filled();
        log("created outer wall.");
        add(ref pos_mani, key_mani);
        log("added positive manifold.");
        add(ref pos_tc, key_tc);
        log("added positive thermocouples.");
        add(ref flange, key_flange);
        log("added flange.");

        Voxels top_excess = new Pipe(
            new Frame(cnt_z6*uZ3),
            cnt_zR - cnt_z6 + th_iw + th_chnl + th_ow + EXTRA,
            cnt_rR + th_iw + th_chnl + th_ow + EXTRA
        ).voxels();
        part.BoolSubtract(top_excess);
        using (key_part.like())
            key_part <<= Geez.voxels(part);
        log("clipped top excess.");

        Fillet.concave(part, 3f, inplace: true);
        using (key_part.like())
            key_part <<= Geez.voxels(part);
        log("filleted.");

        sub(ref gas, key_gas);
        log("subtracted gas cavity.");
        sub(ref chnl, key_chnl);
        log("subtracted channels.");
        sub(ref neg_mani);
        log("subtracted negative manifold.");
        sub(ref neg_tc);
        log("subtracted negative thermocouples.");
        sub(ref neg_bolts, key_bolts);
        log("subtracted bolt holes.");

        Voxels bot_excess = new Pipe(
            new Frame(ZERO3, -uZ3),
            max(-cnt_zQ, -cnt_wid_zS) + th_iw + th_chnl + th_ow + EXTRA,
            pm.Mr_bolt + pm.Bsz_bolt/2f + pm.thickness_around_bolt + EXTRA
        ).voxels();
        part.BoolSubtract(bot_excess);
        using (key_part.like())
            key_part <<= Geez.voxels(part);
        log("clipped bottom excess.");


        log("Baby made.");

        if (_cnt_cache_total == 0) {
            log($"  cache sdf: unused");
        } else {
            log($"  cache sdf: "
                    + $"{_cnt_cache_hits:N0} / {_cnt_cache_total:N0} "
                    + $"({_cnt_cache_hits*100f/_cnt_cache_total:F2}%)");
        }
        if (_cnt_wid_cache_total == 0) {
            log($"  cache wid_sdf: unused");
        } else {
            log($"  cache wid_sdf: "
                    + $"{_cnt_wid_cache_hits:N0} / {_cnt_wid_cache_total:N0} "
                    + $"({_cnt_wid_cache_hits*100f/_cnt_wid_cache_total:F2}%)");
        }
        log("  bang.");

        return part;
    }


    public void drawings(in Voxels part) {
        Geez.voxels(part); // someting to look at.
        Cuboid bounds;

        Frame frame_xy = new(3f*VOXEL_SIZE*uZ3, uX3, uZ3);
        log("cross-sectioning xy...");
        Drawing.to_file(
            fromroot($"exports/chamber_xy.svg"),
            part,
            frame_xy,
            out bounds
        );
        using (Geez.like(colour: COLOUR_BLUE))
            Geez.cuboid(bounds, divide_x: 3, divide_y: 3);

        Frame frame_yz = new(ZERO3, uY3, uX3);
        log("cross-sectioning yz...");
        Drawing.to_file(
            fromroot($"exports/chamber_yz.svg"),
            part,
            frame_yz,
            out bounds);
        using (Geez.like(colour: COLOUR_GREEN))
            Geez.cuboid(bounds, divide_x: 3, divide_y: 4);
    }


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
