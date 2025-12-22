using static Br;
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
    // X_inlet = fuel inlet property.
    // X_iw = inner wall property.
    // X_mani = fuel inlet manifold property.
    // X_ow = outer wall property.
    // X_part = entire chamber+nozzle part property.
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
    protected float theta_chnl(float z) {
        return 4*TWOPI/no_chnl * (0.5f + 0.5f*cos(PI * z / cnt_z6));
    }
    protected void initialise_chnl() {
        no_chnl = no_web;
        th_chnl = th_web;
        Ltheta_chnl = TWOPI/no_chnl - Ltheta_web;
        assert(Ltheta_chnl > 0f);
        A_chnl_exit = Ltheta_chnl/TWOPI * PI*(squared(r_exit + th_iw + th_chnl)
                                            - squared(r_exit + th_iw));
    }

    public required float th_mani { get; init; }
    public required float phi_mani { get; init; }

    public required float D_inlet { get; init; }
    public required float theta_inlet { get; init; }
    public required float th_inlet { get; init; }
    public required float phi_inlet { get; init; }


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
    */

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

    protected void initialise_cnt() {
        cnt_X = AXIAL_EXTRA + 1.1f*max(pm.Mr_chnl, r_exit);
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
    }


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
    protected float cnt_wid_zT = NAN;
    protected float cnt_wid_rT = NAN;

    protected void initialise_cnt_wid() {
        cnt_wid_alpha = 2f;
        cnt_wid_r_s = (pm.Mr_chnl - pm.Or_cc)
                    / (2f - 2f*cos(phi_wid)
                     + cnt_wid_alpha*phi_wid*sin(phi_wid));

        cnt_wid_z0 = 0f;
        cnt_wid_r0 = pm.Mr_chnl;

        cnt_wid_z1 = 0.5f*-phi_wid*cnt_wid_r_s;
        cnt_wid_r1 = pm.Mr_chnl;

        cnt_wid_z2 = cnt_wid_z1 - cnt_wid_r_s*sin(phi_wid);
        cnt_wid_r2 = cnt_wid_r1 - cnt_wid_r_s*(1f - cos(phi_wid));

        cnt_wid_r4 = pm.Or_cc + th_iw + 0.5f*th_chnl;
        cnt_wid_r3 = cnt_wid_r4 + cnt_wid_r_s*(1f - cos(phi_wid));
        cnt_wid_z3 = cnt_wid_z2 + (cnt_wid_r3 - cnt_wid_r2)/tan(phi_wid);
        cnt_wid_z4 = cnt_wid_z3 - cnt_wid_r_s*sin(phi_wid);

        cnt_wid_zS = cnt_wid_z0 - cnt_X;
        cnt_wid_rS = cnt_wid_r0;
        cnt_wid_zT = cnt_wid_z4 + cnt_X;
        cnt_wid_rT = cnt_wid_r4;
    }


    protected int DIVISIONS
            => max(200, (int)(200f / PicoGK.Library.fVoxelSizeMM));

    protected const float AXIAL_EXTRA = 10f; // trimmed at some point.
    protected const float RADIAL_EXTRA = 5f; // trimmed at some point.


    // Caching is hugely impactful here, and we can/should use genuine equality
    // instead of imprecise.
    protected class cnt_vec2eq : IEqualityComparer<Vec2> {
        public bool Equals(Vec2 a, Vec2 b) => (a.X == b.X) && (a.Y == b.Y);
        public int GetHashCode(Vec2 v) => HashCode.Combine(v.X, v.Y);
    }
    protected Vec2 cnt_0;
    protected Vec2 cnt_1;
    protected Vec2 cnt_2;
    protected Vec2 cnt_3;
    protected Vec2 cnt_4;
    protected Vec2 cnt_5;
    protected Vec2 cnt_6;
    protected Vec2 cnt_Q;
    protected Vec2 cnt_R;
    protected Vec2 cnt_2_3;
    protected Vec2 cnt_2_3_n;
    protected float cnt_2_3_mag;
    protected Vec2 cnt_5_6;
    protected float cnt_5_6_mag;
    protected Dictionary<Vec2, float>? _cnt_cache;
    protected int _cnt_cache_hits;
    protected void initialise_cnt_sdf() {
        cnt_0 = new(cnt_z0, cnt_r0);
        cnt_1 = new(cnt_z1, cnt_r1);
        cnt_2 = new(cnt_z2, cnt_r2);
        cnt_3 = new(cnt_z3, cnt_r3);
        cnt_4 = new(cnt_z4, cnt_r4);
        cnt_5 = new(cnt_z5, cnt_r5);
        cnt_6 = new(cnt_z6, cnt_r6);
        cnt_Q = new(cnt_zQ, cnt_rQ);
        cnt_R = new(cnt_zR, cnt_rR);
        cnt_2_3 = tocart(1f, phi_conv);
        cnt_2_3_n = tocart(1f, phi_conv + PI_2);
        cnt_2_3_mag = mag(cnt_3 - cnt_2);
        cnt_5_6 = normalise(cnt_6 - cnt_5);
        cnt_5_6_mag = mag(cnt_6 - cnt_5);
        _cnt_cache = new(10000000 /* ten milly */, new cnt_vec2eq());
        _cnt_cache_hits = 0;
    }

    protected float cnt_wid_off;
    protected Vec2 cnt_wid_0;
    protected Vec2 cnt_wid_1;
    protected Vec2 cnt_wid_2;
    protected Vec2 cnt_wid_3;
    protected Vec2 cnt_wid_4;
    protected Vec2 cnt_wid_2_3;
    protected Vec2 cnt_wid_2_3_n;
    protected float cnt_wid_2_3_mag;
    protected Dictionary<Vec2, float>? _cnt_wid_cache;
    protected int _cnt_wid_cache_hits;
    protected void initialise_cnt_wid_sdf() {
        // The sdf gives the distance to the channel mid-contour.
        cnt_wid_off = th_iw + 0.5f*th_chnl;

        cnt_wid_0 = new(cnt_wid_z0, cnt_wid_r0);
        cnt_wid_1 = new(cnt_wid_z1, cnt_wid_r1);
        cnt_wid_2 = new(cnt_wid_z2, cnt_wid_r2);
        cnt_wid_3 = new(cnt_wid_z3, cnt_wid_r3);
        cnt_wid_4 = new(cnt_wid_z4, cnt_wid_r4);
        cnt_wid_2_3 = tocart(1f, phi_conv);
        cnt_wid_2_3_n = tocart(1f, phi_conv + PI_2);
        cnt_wid_2_3_mag = mag(cnt_wid_3 - cnt_wid_2);
        _cnt_wid_cache = new(10000000 /* ten milly */, new cnt_vec2eq());
        _cnt_wid_cache_hits = 0;
    }

    protected float cnt_sdf(in Vec3 p) {
        float z = p.Z;
        float r = magxy(p);
        Vec2 q = new(z, r);
        // CACHE ME.
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
        float z = p.Z;
        float r = magxy(p);
        Vec2 q = new(z, r);
        // CACHE ME.
        if (_cnt_wid_cache!.TryGetValue(q, out float cached)) {
            ++_cnt_wid_cache_hits; // HIIIIIIT
            return cached;
        }
        float dist = +INF;
        void update(ref float dist, float d) {
            if (abs(d) < abs(dist))
                dist = d;
        }
        // Same deal as `cnt_sdf`.

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
        if (z >= cnt_wid_zT) {
            Vec2 d = q - new Vec2(cnt_wid_zT - cnt_wid_rT, 0f);
            update(ref dist, mag(d) - cnt_wid_rT);
        }

        _cnt_wid_cache[q] = dist;
        return dist;
    }

    protected float cnt_widened_sdf(in Vec3 p) {
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
    protected Voxels voxels_cnt_chnl() {
        SDFshelled sdf = new(cnt_widened_sdf, th_iw, th_chnl);
        sdf = sdf.innered();
        // Clip the top to not have fuel shoot out the end of the nozzle.
        float max_z = cnt_z6 - th_ow;
        float min_z = cnt_z0 - AXIAL_EXTRA;
        BBox3 bbox = bbox_cnt_widened(sdf.max_off);
        bbox.vecMax.Z = max_z;
        bbox.vecMin.Z = min_z;
        Voxels vox = sdf.voxels(bbox);
        // Extend it radially outwards at the nozzle exit to interface with
        // manifold.
        Frame exit = new(max_z*uZ3, -uZ3);
        float Ir = cnt_radius_at(max_z, th_iw);
        float Or = cnt_radius_at(max_z - th_chnl, th_iw + th_chnl + th_ow);
        Ir += 0.2f; // safety.
        Or += RADIAL_EXTRA;
        vox.BoolAdd(new Pipe(exit, th_chnl, Ir, Or).voxels());
        return vox;
    }

    protected float cnt_radius_at(float z, float signed_dist=0f,
            bool widened=false) {
        assert(within(z, cnt_zQ, cnt_zR), $"z={z}");
        SDFfunction sdf = widened ? cnt_widened_sdf : cnt_sdf;

        // Initially, estimate by just offseting the inner point horizontally by
        // the signed dist. Though note that for horizontal linear segments, this
        // is the exact solution so we return straight away.
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


    protected Mesh mesh_chnl(float rlo, float rhi, float theta0,
            bool draw_line=false) {
        List<Vec3> points = new();
        for (int i=0; i<=DIVISIONS; ++i) {
            float z = cnt_z0 + i*(cnt_z6 - cnt_z0)/DIVISIONS;
            float r = cnt_radius_at(z, -0.1f);
            float theta = theta0 + theta_chnl(z);
            points.Add(tocart(r, theta, z));
        }
        if (draw_line)
            Geez.line(points);
        points.Insert(0, points[0] - uZ3*AXIAL_EXTRA);
        points.Add(points[^1] + uZ3*AXIAL_EXTRA);

        float Dt = 0.5f*Ltheta_chnl; // t for theta.

        Mesh mesh = new();
        List<Vec3> V = new();
        for (int i=1; i<numel(points); ++i) {
            Vec3 A = points[i - 1];
            Vec3 B = points[i];
            assert(B.Z > A.Z, $"A.z={A.Z}, B.z={B.Z}");
            float tA = argxy(A);
            float tB = argxy(B);
            Vec3 p000 = tocart(rlo, tA - Dt, A.Z);
            Vec3 p100 = tocart(rhi, tA - Dt, A.Z);
            Vec3 p010 = tocart(rlo, tA + Dt, A.Z);
            Vec3 p110 = tocart(rhi, tA + Dt, A.Z);
            Vec3 p001 = tocart(rlo, tB - Dt, B.Z);
            Vec3 p101 = tocart(rhi, tB - Dt, B.Z);
            Vec3 p011 = tocart(rlo, tB + Dt, B.Z);
            Vec3 p111 = tocart(rhi, tB + Dt, B.Z);
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
        mesh.nAddTriangle(numel(V)-4 + 0, numel(V)-4 + 1, numel(V)-4 + 3);
        mesh.nAddTriangle(numel(V)-4 + 0, numel(V)-4 + 3, numel(V)-4 + 2);

        mesh.AddVertices(V, out _);
        return mesh;
    }

    protected Voxels voxels_chnl(List<int>? keys=null) {
        float min_r = r_tht;
        float max_r = pm.Mr_chnl + 0.5f*th_chnl;
        max_r = max(max_r, r_exit + th_iw + th_chnl + th_ow);
        max_r += RADIAL_EXTRA;
        Voxels vox = new();
        for (int i=0; i<no_chnl; ++i) {
            float theta0 = i * TWOPI / no_chnl;
            Mesh mesh = mesh_chnl(min_r, max_r, theta0, draw_line: i <= 4);
            if (keys != null) {
                int key = Geez.mesh(mesh);
                keys.Add(key);
            }
            vox.BoolAdd(new Voxels(mesh));
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
        float neg_max_z = cnt_z6 - th_mani;
        for (int i=0; i<=DIVISIONS/8; ++i) {
            float z = neg_max_z + i*(neg_min_z - neg_max_z)/(DIVISIONS/8);
            float r = cnt_radius_at(z, th_iw + th_chnl + th_ow);
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
        Vec2 A = a + tocart(th_mani/cos(phi_aA), phi_aA);
        Vec2 C = c + uY2*th_mani/cos(phi_mani);
        Vec2 d = new(cnt_z6, r_tht); // throat radius reasonable lower extreme.
        Vec2 e = new(
            (d.Y - b.Y)/tan(phi_mani) + b.X - th_mani/sin(phi_mani),
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
        Polygon.fillet(pos, 3, Fr_a + th_mani, divisions: divisions_a);
        Polygon.fillet(pos, 2, Fr_c + th_mani, divisions: divisions_c);
        pos = Polygon.line_resample(pos, 2*DIVISIONS);

        // Place the inlet some way up the lower edge. Note the last coordinate
        // stores the axial angle.
        inlet = rejxy((2f*b + 3f*c)/5f, phi_inlet);

        // Output the lower edge projected length to hugely aid the numerical
        // search for matching area.
        Lr = abs((c - b).Y);
    }

    protected void points_mani(float theta,
            out List<Vec3> neg, out List<Vec3> pos, out Frame inlet) {
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

    protected void voxels_mani(out Frame inlet, out Voxels neg, out Voxels pos) {
        PointsManiF get_pos = (List<Vec3> points, float theta) => {
            points_mani(theta, out _, out List<Vec3> pos, out _);
            points.AddRange(pos);
        };
        Mesh mesh_pos = mesh_mani(get_pos);
        pos = new(mesh_pos);

        Frame? inlet_fr = null;
        PointsManiF get_neg = (List<Vec3> points, float theta) => {
            points_mani(theta, out List<Vec3> neg, out _, out Frame maybeinlet);
            if (closeto(theta, theta_inlet)) {
                assert(inlet_fr == null);
                inlet_fr = maybeinlet;
            }
            points.AddRange(neg);
        };
        Mesh mesh_neg = mesh_mani(get_neg);
        neg = new(mesh_neg);
        assert(inlet_fr != null);
        inlet = inlet_fr!;
    }

    protected void voxels_inlet(in Frame inlet, out Voxels neg, out Voxels pos) {
        float L = 15f;
        float L_extra = 2f;
        Vec3 a = inlet.to_global(new Vec3(0f, 0f, L_extra));
        Vec3 b = inlet.to_global(new Vec3(0f, 0f, -L));
        float phi = -argphi(inlet.Z) - PI_2;
        Vec3 c = b + tocart((r_tht - magxy(b))/cos(phi), argxy(b), as_phi(phi));
        pos = new Tubing([a, b], D_inlet + 2f*th_inlet).voxels();
        pos.BoolAdd(new Cuboid(
            new Frame(0.5f*(b + c), cross(inlet.Z, uZ3), inlet.Z),
            4.5f,
            mag(b - c),
            L + L_extra
        ).voxels());
        Fillet.concave(pos, 8f, inplace: true);
        neg = new Tubing([a, b], D_inlet).voxels();
    }

    protected Voxels voxels_flange() {
        Voxels vox;

        vox = new Pipe(
            new Frame(-AXIAL_EXTRA*uZ3),
            pm.flange_thickness + AXIAL_EXTRA,
            pm.flange_outer_radius
        ).voxels();

        for (int i=0; i<pm.no_bolt; ++i) {
            float theta = i * TWOPI / pm.no_bolt;
            Pipe bolt_surrounding = new(
                new Frame(tocart(pm.Mr_bolt, theta, -AXIAL_EXTRA)),
                pm.flange_thickness + AXIAL_EXTRA,
                pm.Bsz_bolt/2f + pm.thickness_around_bolt
            );
            vox.BoolAdd(bolt_surrounding.voxels());
        }

        return vox;
    }

    protected Voxels voxels_neg_bolts() {
        Voxels vox = new();
        for (int i=0; i<pm.no_bolt; ++i) {
            float theta = i * TWOPI / pm.no_bolt;
            Frame frame = new Frame(tocart(pm.Mr_bolt, theta, -AXIAL_EXTRA));
            vox.BoolAdd(new Pipe(
                frame,
                pm.flange_thickness + AXIAL_EXTRA,
                pm.Bsz_bolt/2f
            ).voxels());
        }
        return vox;
    }

    protected Voxels voxels_neg_orings() {
      // PREPROCESSOR MY BELOVED.
      // c sharp has spared you.
      // LONG LIVE THE PREPROCESSOR.
      #if true
        return new(); // oring grooves on injector side?
      #else
        Voxels vox;
        vox = new Pipe(
            new Frame(),
            pm.Lz_Ioring,
            pm.Ir_Ioring,
            pm.Or_Ioring
        ).voxels();
        vox.BoolAdd(new Pipe(
            new Frame(),
            pm.Lz_Ooring,
            pm.Ir_Ooring,
            pm.Or_Ooring
        ).voxels());
        return vox;
      #endif
    }

    public Voxels voxels() {
        // We view a few things as they are updated.
        Geez.Cycle key_neg_mani = new();
        Geez.Cycle key_inlet = new();
        Geez.Cycle key_gas = new();
        Geez.Cycle key_flange = new();
        Geez.Cycle key_chnl = new();
        Geez.Cycle key_part = new();
        var col_gas = COLOUR_PINK;
        var col_flange = COLOUR_RED;
        var col_neg_mani = COLOUR_BLUE;
        var col_chnl = COLOUR_GREEN;

        voxels_mani(out Frame inlet, out Voxels neg_mani, out Voxels pos_mani);
        key_neg_mani <<= Geez.voxels(neg_mani, colour: col_neg_mani);
        key_inlet <<= Geez.frame(inlet, size: 8f);
        voxels_inlet(inlet, out Voxels neg_inlet, out Voxels pos_inlet);

        Voxels gas = voxels_cnt_gas();
        key_gas <<= Geez.voxels(gas, colour: col_gas);

        Voxels ow_filled = voxels_cnt_ow_filled();

        Voxels chnl;
        using (Geez.like(colour: col_chnl)) {
            List<int> keys_chnl = new();
            chnl = voxels_chnl(keys_chnl);
            key_chnl <<= Geez.voxels(chnl);
            Geez.remove(keys_chnl);
        }
        Voxels cnt_chnl = voxels_cnt_chnl();
        chnl.BoolIntersect(cnt_chnl);
        key_chnl <<= Geez.voxels(chnl, colour: col_chnl);
        Fillet.convex(chnl, 0.4f, inplace: true);
        chnl.BoolIntersect(ow_filled);
        key_chnl <<= Geez.voxels(chnl, colour: col_chnl);

        Voxels flange = voxels_flange();
        key_flange <<= Geez.voxels(flange, colour: col_flange);

        Voxels part = ow_filled; // no copy.
        part.BoolAdd(flange);
        part.BoolAdd(pos_mani);
        part.BoolAdd(pos_inlet);
        key_part <<= Geez.voxels(part);
        key_neg_mani <<= Geez.CLEAR;
        key_inlet <<= Geez.CLEAR;
        key_flange <<= Geez.CLEAR;

        Fillet.concave(part, 3f, inplace: true);
        key_part <<= Geez.voxels(part);

        part.BoolSubtract(chnl);
        key_part <<= Geez.voxels(part);
        key_chnl <<= Geez.CLEAR;

        part.BoolSubtract(gas);
        key_part <<= Geez.voxels(part);
        key_gas <<= Geez.CLEAR;

        part.BoolSubtract(neg_mani);
        part.BoolSubtract(neg_inlet);
        key_part <<= Geez.voxels(part);

        part.BoolSubtract(voxels_neg_bolts());
        part.BoolSubtract(voxels_neg_orings());
        part.IntersectImplicit(new Space(new Frame(), 0f, cnt_z6));
        key_part <<= Geez.voxels(part);

        return part;
    }


    public void initialise() {
        initialise_exit();
        initialise_chnl();
        initialise_cnt();
        initialise_cnt_wid();
        initialise_cnt_sdf();
        initialise_cnt_wid_sdf();

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



    public static Voxels maker() {
        Chamber chamber = new Chamber{
            pm = new PartMating{
                Or_cc=40f,

                Mr_chnl=50f,
                min_wi_chnl=2f,

                Ir_Ioring=42.5f,
                Or_Ioring=45.5f,
                Ir_Ooring=54.5f,
                Or_Ooring=57.5f,
                Lz_Ioring=2f,
                Lz_Ooring=2f,

                no_bolt=10,
                Mr_bolt=66f,
                Bsz_bolt=8f,
                Bln_bolt=20f,

                thickness_around_bolt=5f,
                flange_thickness=7f,
                flange_outer_radius=68f,
                radial_fillet_radius=5f,
                axial_fillet_radius=10f,
            },

            L_cc=70f,

            AEAT=4f,
            r_tht=18f,

            NLF=1f,
            phi_conv=-DEG30,
            phi_div=torad(18f),
            phi_exit=torad(8f),

            phi_wid=-DEG45,

            th_iw=1.5f,
            th_ow=3.0f,

            no_web=40,
            th_web=2f,
            Ltheta_web=1.5f/50f,

            th_mani=3f,
            phi_mani=DEG45,

            D_inlet=10f,
            theta_inlet=-DEG90,
            th_inlet=3f,
            phi_inlet=-DEG45,
        };
        chamber.initialise();

        Voxels vox = chamber.voxels();
        PicoGK.Library.Log("Baby made.");

        int cnt_hits = chamber._cnt_cache_hits;
        int cnt_total = cnt_hits + numel(chamber._cnt_cache!);
        int cnt_wid_hits = chamber._cnt_wid_cache_hits;
        int cnt_wid_total = cnt_wid_hits + numel(chamber._cnt_wid_cache!);
        PicoGK.Library.Log($"  cache sdf: "
                + $"{cnt_hits:N0} / {cnt_total:N0} "
                + $"({cnt_hits * 100f / cnt_total:F2}%)");
        PicoGK.Library.Log($"  cache wid_sdf: "
                + $"{cnt_wid_hits:N0} / {cnt_wid_total:N0} "
                + $"({cnt_wid_hits * 100f / cnt_wid_total:F2}%)");
        PicoGK.Library.Log("  bang.");

        return vox;
    }
}




public class TwistedPlane : SDFunbounded {
    protected float Dtheta { get; }
    protected float Dz { get; }
    protected float bracket_lo { get; }
    protected float bracket_hi { get; }
    protected bool positive { get; }
    protected float slope { get; } // slope of surface, as dy/dz, at x=1.
    protected float slope2 { get; } // slope^2.
    protected float slope3 { get; } // slope^3.
    protected float slope4 { get; } // slope^4.

    static protected int MAX_ITERS => 12;
    static protected float TOL => 1e-3f;

    public TwistedPlane(in Vec3 a, in Vec3 b, bool positive)
            : this(argxy(a), argxy(b), a.Z, b.Z, positive) {
        float a_r = magxy(a);
        float b_r = magxy(b);
        // Check they have equal radius.
        assert(abs(a_r - b_r) < 1e-5f, $"a_r={a_r}, b_r={b_r}");
        assert(abs(a_r / b_r) < 1f + 1e-5f, $"a_r={a_r}, b_r={b_r}");
    }
    public TwistedPlane(float theta0, float theta1, float z0, float z1,
            bool positive) {
        // Check this isnt a horizontal plane.
        assert(abs(z1 - z0) > 1e-5f, $"z0={z0}, z1={z1}");
        this.Dtheta = theta0;
        this.Dz = z0;
        this.positive = positive;
        // Recall:
        //  surf(t, s) = (t, s*t*slope, s)
        // We know that (1,0,0) lies on the surface (trivial), need to find
        // `slope` s.t. (cos(theta1 - theta0), sin(theta1 - theta0), z1 - z0)
        // lies on the surface.
        // => t = cos(theta1 - theta0)
        // => s = z1 - z0
        // => slope = sin(theta1 - theta0) / t / s
        //  slope = tan(theta1 - theta0) / (z1 - z0)
        this.slope = tan(theta1 - theta0) / (z1 - z0);
        assert(this.slope < 2f, "too unstable at extreme pringle shapes");
        this.slope2 = this.slope*this.slope;
        this.slope3 = this.slope2*this.slope;
        this.slope4 = this.slope3*this.slope;
    }

    protected Vec3 closest(in Vec3 p) {
        // The surface is:
        Vec3 surf(float s, float t) => new(t, s*t*slope, s);

        // Trying to find the closest point (on the surface) to p:
        //  P = surf(s, t) s.t. mag(P - p) is minimised over s,t.
        // Equivalent to minimising:
        //  f(s,t) = dot(surf(s, t) - p, surf(s, t) - p)
        //  f(s,t) = (t - p.X)^2 + (s*t*slope - p.Y)^2 + (s - p.Z)^2
        // This is a multivariable problem (for now....).
        // The minimum will occur at zero gradient:
        //  df/ds = 2*t*slope*(s*t*slope - p.Y) + 2*(s - p.Z)
        //  df/dt = 2*s*slope*(s*t*slope - p.Y) + 2*(t - p.X)
        //  df/ds = df/dt = 0 @ minimum.
        // f_s=0:
        //  0 = 2*t*slope*(s*t*slope - p.Y) + 2*(s - p.Z)
        //  0 = s*t^2*slope^2 - p.Y*t*slope + s - p.Z
        //  s*(t^2*slope^2 + 1) = t*slope*p.Y + p.Z
        // f_t=0:
        //  0 = 2*s*slope*(s*t*slope - p.Y) + 2*(t - p.X)
        //  0 = s^2*t*slope^2 - p.Y*s*slope + t - p.X
        //  t*(s^2*slope^2 + 1) = s*slope*p.Y + p.X
        // Either of these can be used to reduce the problem to a single
        // variable. We'll eliminate t.
        //  t*(s^2*slope^2 + 1) = s*slope*p.Y + p.X
        // => t = (s*slope*p.Y + p.X) / (s^2*slope^2 + 1)
        // With a lot of pain (aka wolfram alpha queries/sympy scripts) this
        // reduces grad(f)=0 to:
        //  0 = eff(s)
        //  eff(s) = s^5 * (slope^4)
        //         + s^4 * (-p.Z*slope^4)
        //         + s^3 * (2*slope^2)
        //         + s^2 * (p.X*p.Y*slope^3 - 2*p.Z*slope^2)
        //         + s   * (p.X^2*slope^2 - p.Y^2*slope^2 + 1)
        //         + 1   * (-p.X*p.Y*slope - p.Z)
        //  eff(s) = s^5 * A
        //         + s^4 * B
        //         + s^3 * C
        //         + s^2 * D
        //         + s   * E
        //         + 1   * F
        // ayyy a simple polynomial root. fifth order so no closed form but no
        // worries.
        float A = slope4;
        float B = -p.Z*slope4;
        float C = 2f*slope2;
        float D = p.X*p.Y*slope3 - 2f*p.Z*slope2;
        float E = p.X*p.X*slope2 - p.Y*p.Y*slope2 + 1f;
        float F = -p.X*p.Y*slope - p.Z;

        float eff(float s)
            => ((((A*s + B)*s + C)*s + D)*s + E)*s + F;
        float eff_s(float s)
            => (((5f*A*s + 4f*B)*s + 3f*C)*s + 2f*D)*s + E;
        float eff_ss(float s)
            => ((20f*A*s + 12f*B)*s + 6f*C)*s + 2f*D;

        // Initial guess, which is close if slope is small (common). Otherwise
        // its pretty average but its also very difficult to get an easy accurate
        // guess.
        float s = p.Z;
        float f = eff(s);

        // Bounds for bracketed search. These will be enlarged until a root is
        // found.
        float lo = -10f; // informed by nothing lmao.
        float hi = +10f;

        // Try gradient descent initially, but its not always stable.
        for (int _=0; _<MAX_ITERS; ++_) {
            // Check solution.
            if (abs(f) < TOL)
                goto ROOTED;
            // Halley's method (second order gradient descent).
            float f_s = eff_s(s);
            float f_ss = eff_ss(s);
            float ds = -f*f_s / (f_s*f_s - 0.5f*f*f_ss);
            // Check stagnation (and dont assume its a solution :().
            if (s + ds == s) {
                lo = s - 0.1f;
                hi = s + 0.1f;
                goto BRACKETED;
            }
            // Descend the gradient.
            s += ds;
            f = eff(s);
        }
        lo = s - 2f;
        hi = s + 2f;

      BRACKETED:;
        // FIIIINE lets do bisection.

        // Expand bounds until a root is bracketed.
        float flo = eff(lo);
        while (flo * eff(hi) > 0f) {
            // Double search range if no sign change.
            float mid = 0.5f*(lo + hi);
            assert((mid != lo) && (mid != hi));
            lo = mid + 2f*(lo - mid);
            hi = mid + 2f*(hi - mid);
            assert((lo != -INF) && (hi != +INF));
            flo = eff(lo);
        }

        assert(flo * eff(hi) <= 0f, "cooked");
        for (;;) {
            float mid = 0.5f*(lo + hi);
            float fmid = eff(mid);
            // Check solution (or precision limit).
            if (abs(fmid) < TOL || (mid == lo) || (mid == hi)) {
                s = mid;
                f = fmid;
                goto ROOTED;
            }
            // Iterate brackets.
            if (flo*fmid < 0f) {
                hi = mid;
            } else {
                lo = mid;
                flo = fmid;
            }
        }

      ROOTED:;
        // assert(abs(f) < TOL);
        // Don't even worry bout it actually f32 precision shits on our balls.

        // Find the corresponding t value.
        float t = (s*slope*p.Y + p.X) / (s*s*slope2 + 1f);

        // Return the point on the surface which we have found to be the closest.
        return surf(s, t);
    }

    public override float fSignedDistance(in Vec3 p) {
        Vec3 P = rotxy(p, Dtheta) - uZ3*Dz;
        Vec3 C = closest(P);
        float dist = mag(P - C);
        if ((P.Y > C.Y) == positive) // solid on either pos or neg side.
           dist = -dist;
        return dist;
    }
}


public class RunningStats {
    public int N = 0;
    public float mean = 0f;
    public float maximum = 0f;
    public double stddev => sqrt((N > 1) ? M2/(N - 1) : 0f);

    protected float M2 = 0f; // sum of squared diffs

    public RunningStats() {}

    public void add(float x) {
        N += 1;
        maximum = max(maximum, x);
        float delta = x - mean;
        mean += delta / N;
        float delta2 = x - mean;
        M2 += delta * delta2;
    }
}
