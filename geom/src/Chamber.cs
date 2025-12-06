using static Br;
using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using Triangle = PicoGK.Triangle;
using Lattice = PicoGK.Lattice;
using BBox3 = PicoGK.BBox3;

public class Chamber {

    // Bit how ya going.
    //
    // Typical frame used is cylindrical coordinates, z vector pointing along
    // axis of symmettry (axial vector), from the nozzle towards the injector,
    // where z=0 is the exit plane.
    //
    // Legend for 'X':
    // AEAT = nozzle exit area to throat area ratio.
    // Bsz = bolt size (i.e. M4 for M4x10).
    // Bln = bolt length (i.e. 10e-3 for M4x10).
    // D_ = discrete change in this variable.
    // Ir = inner radius.
    // L = length.
    // L_ = length along this coordinate path.
    // NLF = nozzle length as a fraction of the length of a 15deg cone.
    // no = number of things.
    // r = radial coordinate.
    // th = thickness (normal to surface).
    // phi = conical half-angle relative to negative axial vector.
    // theta = circumferential angular coordinate.
    // wi = circumferential width.
    // x = x coordinate.
    // y = y coordinate.
    // z = z coordinate.
    //
    //
    // Context for 'X':
    //
    // nzl_X = nozzle construction value.
    // X_cc = chamber property.
    // X_conv = nozzle converging section property.
    // X_div = nozzle diverging section property.
    // X_exit = nozzle exit property.
    // X_ib = interface (with other part) bolt property.
    // X_iw = inner wall property.
    // X_ow = outer wall property.
    // X_part = entire chamber+nozzle part property.
    // X_tht = nozzle throat property.
    // X_web = between-cooling-channel wall property.

    public required float r_itrb;
    public required float L_itfb;
    public required float Bsz_itfb;
    public required float th_itfb;
    public required int no_itfb;

    public required float AEAT { get; init; }
    public required float L_cc { get; init; }
    public required float r_cc { get; init; }
    public required float r_tht { get; init; }
    public float r_exit => sqrt(AEAT) * r_tht;

    public required float NLF { get; init; }
    public required float phi_conv { get; init; }
    public required float phi_div { get; init; }
    public required float phi_exit { get; init; }

    public required int no_web { get; init; } // =# of cooling channels.
    public required float th_iw { get; init; }
    public required float th_ow { get; init; }
    public required float th_web { get; init; }
    public required float wi_web { get; init; }


    /*
    https://www.desmos.com/calculator/6wogn9dm4x
    Consider the revolved contour of the nozzle interoir:
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

    public float nzl_r_conv => 1.5f*r_tht;

    public float nzl_z0 => 0f;
    public float nzl_r0 => r_cc;

    public float nzl_z1 => L_cc;
    public float nzl_r1 => r_cc;

    public float nzl_z2 => nzl_z1 - nzl_r_conv*sin(phi_conv);
    public float nzl_r2 => r_cc - nzl_r_conv*(1f - cos(phi_conv));

    public float nzl_z3 => nzl_z2 + (nzl_r3 - nzl_r2)/tan(phi_conv);
    public float nzl_r3 => r_tht * (2.5f - 1.5f*cos(phi_conv));

    public float nzl_z4 => nzl_z3 - 1.5f*r_tht*sin(phi_conv);
    public float nzl_r4 => r_tht;

    public float nzl_z5 => nzl_z4 + 0.382f*r_tht*sin(phi_div);
    public float nzl_r5 => r_tht*(1.382f - 0.382f*cos(phi_div));

    public float nzl_z6 => nzl_z4 + NLF*(3.732051f*r_exit - 3.683473f*r_tht);
    public float nzl_r6 => r_exit;

    public float nzl_zP => (nzl_z5*tan(phi_div) - nzl_z6*tan(phi_exit) + nzl_r6
                          - nzl_r5) / (tan(phi_div) - tan(phi_exit));
    public float nzl_rP => tan(phi_exit) * (nzl_zP - nzl_z6) + nzl_r6;

    public float nzl_para_az => nzl_z5 - 2f*nzl_zP + nzl_z6;
    public float nzl_para_bz => -2f*nzl_z6 + 2f*nzl_zP;
    public float nzl_para_cz => nzl_z6;
    public float nzl_para_ar => nzl_r5 - 2f*nzl_rP + nzl_r6;
    public float nzl_para_br => -2f*nzl_r6 + 2f*nzl_rP;
    public float nzl_para_cr => nzl_r6;

    public float[] nzl_zA => [nzl_z0, nzl_z1, nzl_z2, nzl_z3, nzl_z4, nzl_z5];
    public float[] nzl_zB => [nzl_z1, nzl_z2, nzl_z3, nzl_z4, nzl_z5, nzl_z6];


    public float L_part => nzl_z6;

    public float Ir_part(float z) {
        // Note we extend the radius at points 0 and 6 downwards and upwards,
        // respectively.
        if (z <= nzl_z1) {
            return nzl_r0;
        }
        if (z <= nzl_z2) {
            z -= nzl_z1;
            return nzl_r1 - nzl_r_conv + sqrt(nzl_r_conv*nzl_r_conv - z*z);
        }
        if (z <= nzl_z3) {
            z -= nzl_z3;
            return tan(phi_conv)*z + nzl_r3;
        }
        if (z <= nzl_z4) {
            z -= nzl_z4;
            return 2.5f*r_tht - sqrt(2.25f*r_tht*r_tht - z*z);
        }
        if (z <= nzl_z5) {
            z -= nzl_z4;
            return 1.382f*r_tht - sqrt(0.145924f*r_tht*r_tht - z*z);
        }
        if (z <= nzl_z6) {
            float p;
            p = 4f*nzl_para_az*(nzl_para_cz - z);
            p = sqrt(nzl_para_bz*nzl_para_bz - p);
            p = (-nzl_para_bz - p) / 2f / nzl_para_az;
            return (nzl_para_ar*p + nzl_para_br)*p + nzl_para_cr;
        }
        return r_exit;
    }

    public float theta_web(float z) {
        return PI/8 * cos(PI * z / L_part);
    }


    protected float axial_extra = 10f; // trimmed in final step.

    public void check_realisable() {
        // Easiest way to determine if the parameters create a realisable nozzle.

        assert(nzl_z0 < nzl_z1, $"z0={nzl_z0}, z1={nzl_z1}");
        assert(nzl_z1 < nzl_z2, $"z1={nzl_z1}, z2={nzl_z2}");
        assert(nzl_z2 < nzl_z3, $"z2={nzl_z2}, z3={nzl_z3}");
        assert(nzl_z3 < nzl_z4, $"z3={nzl_z3}, z4={nzl_z4}");
        assert(nzl_z4 < nzl_z5, $"z4={nzl_z4}, z5={nzl_z5}");
        assert(nzl_z5 < nzl_z6, $"z5={nzl_z5}, z6={nzl_z6}");
        assert(nzl_zP > nzl_z5, $"zP={nzl_zP}, z5={nzl_z5}");
        assert(nzl_zP < nzl_z6, $"zP={nzl_zP}, z6={nzl_z6}");

        assert(nzl_r0 == nzl_r1, $"r0={nzl_r0}, r1={nzl_r1}");
        assert(nzl_r1 > nzl_r2, $"r1={nzl_r1}, r2={nzl_r2}");
        assert(nzl_r2 > nzl_r3, $"r2={nzl_r2}, r3={nzl_r3}");
        assert(nzl_r3 > nzl_r4, $"r3={nzl_r3}, r4={nzl_r4}");
        assert(nzl_r4 < nzl_r5, $"r4={nzl_r4}, r5={nzl_r5}");
        assert(nzl_r5 < nzl_r6, $"r5={nzl_r5}, r6={nzl_r6}");
        assert(nzl_rP > nzl_r5, $"rP={nzl_rP}, r5={nzl_r5}");
        assert(nzl_rP < nzl_r6, $"rP={nzl_rP}, r6={nzl_r6}");
    }


    private void divvy(int divisions, int[] divs) {
        assert(numel(divs) == 6);
        // 0-1 and 2-3 are each one beam element. each other one gets a differing
        // number.
        assert(divisions >= 20, $"divisions={divisions}");
        float weight_12 = 1.5f * (nzl_z2 - nzl_z1);
        float weight_34 = 1.5f * (nzl_z4 - nzl_z3);
        float weight_45 = 4.0f * (nzl_z5 - nzl_z4);
        float weight_56 = 1.0f * (nzl_z6 - nzl_z5);

        float net_weight = weight_12 + weight_34 + weight_45 + weight_56;
        weight_12 /= net_weight;
        weight_34 /= net_weight;
        weight_45 /= net_weight;
        // weight_56 /= net_weight; // unused from here.

        divs[0] = 1;
        divs[1] = (int)(weight_12 * divisions);
        divs[2] = 1;
        divs[3] = (int)(weight_34 * divisions);
        divs[4] = (int)(weight_45 * divisions);
        // Last can just get all remaining, which is roughly what was allocated.
        divs[5] = divisions - divs[0] - divs[1] - divs[2] - divs[3] - divs[4];
    }

    protected List<Vec3> points_interior(int divisions=150) {
        List<Vec3> points = new();

        void push_point(float z) {
            float r = Ir_part(z);
            points.Add(new Vec3(0f, r, z));
        }
        void push_section(float zA, float zB, int divisions) {
            for (int i=0; i<divisions; ++i) {
                float z = zA + i * (zB - zA) / divisions;
                push_point(z);
            }
        }

        int[] divs = new int[6];
        divvy(divisions, divs);

        for (int i=0; i<6; ++i)
            push_section(nzl_zA[i], nzl_zB[i], divs[i]);
        push_point(nzl_z6);

        return points;
    }

    protected Voxels voxels_interior(int divisions=150) {
        Lattice lattice = new();

        void push_beam(float za, float zb) {
            float ra = Ir_part(za);
            float rb = Ir_part(zb);
            lattice.AddBeam(
                    new Vec3(0f, 0f, za), ra,
                    new Vec3(0f, 0f, zb), rb,
                    false
                );
        }
        void push_section(float zA, float zB, int divisions) {
            for (int i=0; i<divisions; ++i) {
                float za = zA +       i * (zB - zA) / divisions;
                float zb = zA + (i + 1) * (zB - zA) / divisions;
                push_beam(za, zb);
            }
        }

        int[] divs = new int[6];
        divvy(divisions, divs);

        push_section(nzl_z0 - axial_extra, nzl_z0, 1);
        for (int i=0; i<6; ++i)
            push_section(nzl_zA[i], nzl_zB[i], divs[i]);
        push_section(nzl_z6, nzl_z6 + axial_extra, 1);

        return new Voxels(lattice);
    }

    protected Mesh mesh_web(float theta0, int divisions=250,
            List<int>? keys=null, bool line=false) {
        List<Vec3> points = new();
        for (int i=0; i<divisions; ++i) {
            float z = nzl_z0 + i*(nzl_z6 - nzl_z0)/divisions;
            float r = 0.99f * Ir_part(z);
            float theta = theta0 + theta_web(z);
            points.Add(new(r*cos(theta), r*sin(theta), z));
        }
        if (line)
            Geez.line(points, COLOUR_GREEN);
        if (keys != null) {
            int key = Geez.frame(new(
                points[0],
                rejxy(tocart(1f, argxy(points[0]) + PI_2)),
                uZ3
            ));
            keys.Add(key);
        }
        points.Insert(0, points[0] - uZ3*axial_extra);
        points.Add(points[^1] + uZ3*axial_extra);

        float rlo = r_tht;
        float rhi = r_cc + th_iw + th_web + th_ow;
        float Dt = 0.5f*(wi_web / r_cc); // t for theta.

        Mesh mesh = new();
        List<Vec3> V = new();
        for (int i=1; i<numel(points); ++i) {
            Vec3 A = points[i - 1];
            Vec3 B = points[i];
            assert(B.Z > A.Z, $"A.z={A.Z}, B.z={B.Z}");
            float tA = argxy(A);
            float tB = argxy(B);
            Vec3 p000 = rejxy(tocart(rlo, tA - Dt), A.Z);
            Vec3 p100 = rejxy(tocart(rhi, tA - Dt), A.Z);
            Vec3 p010 = rejxy(tocart(rlo, tA + Dt), A.Z);
            Vec3 p110 = rejxy(tocart(rhi, tA + Dt), A.Z);
            Vec3 p001 = rejxy(tocart(rlo, tB - Dt), B.Z);
            Vec3 p101 = rejxy(tocart(rhi, tB - Dt), B.Z);
            Vec3 p011 = rejxy(tocart(rlo, tB + Dt), B.Z);
            Vec3 p111 = rejxy(tocart(rhi, tB + Dt), B.Z);
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

    protected Voxels voxels_webs() {
        Voxels webs = new();
        List<int> keys = new();
        for (int i=0; i<no_web; ++i) {
            float theta0 = i * TWOPI / no_web;
            Mesh mesh = mesh_web(theta0, keys: keys, line: i <= 4);
            webs += new Voxels(mesh);
        }
        Geez.remove(keys);
        return webs;
    }


    protected Voxels voxels_iface() {
        Voxels iface = new();
        for (int i=0; i<no_itfb; ++i) {
            float theta = i * TWOPI / no_itfb;
            Pipe disc = new(
                new Frame(rejxy(tocart(r_itrb, theta))),
                L_itfb,
                Bsz_itfb/2f + th_itfb
            );
            iface += disc.voxels();
        }
        iface += new Pipe(new Frame(), L_itfb, r_itrb).voxels();
        return iface;
    }

    protected Voxels voxels_iface_holes() {
        Voxels iface = new();
        for (int i=0; i<no_itfb; ++i) {
            float theta = i * TWOPI / no_itfb;
            Pipe hole = new(
                new Frame(rejxy(tocart(r_itrb, theta))),
                L_itfb,
                Bsz_itfb/2f
            );
            Geez.bbox(hole.bounds, COLOUR_CYAN);
            iface += hole.voxels();
        }
        return iface;
    }

    public Voxels voxels() {
        // Order of construction is this:
        // - construct webs.
        // - construct interface.
        // - construct volume enclosed by inner wall (excluding the wall).
        // These three are then offseted, filleted, and intersected or whatever
        // to make the final thing.

        // We view three things simultaneously.
        Geez.Cycle key_running = new();
        Geez.Cycle key_iface = new();
        Geez.Cycle key_webs = new();
        var col_iface = COLOUR_RED;
        var col_webs = COLOUR_GREEN;

        Voxels iface_holes = voxels_iface_holes();
        Voxels iface = voxels_iface();
        key_iface <<= Geez.voxels(iface, colour: col_iface);
        Voxels webs = voxels_webs();
        key_webs <<= Geez.voxels(webs, colour: col_webs);

        Voxels inner_enclosure = voxels_interior();
        inner_enclosure.TripleOffset(-0.01f); // smooth.

        key_running <<= Geez.voxels(inner_enclosure);

        Voxels outer_enclosure = inner_enclosure.voxOffset(th_iw + th_web);

        Voxels vox;

        vox = outer_enclosure.voxOffset(th_ow);
        // Now: vox = outer filled.
        key_running <<= Geez.voxels(vox);

        iface.BoolSubtract(vox);
        key_iface <<= Geez.voxels(iface, colour: col_iface);

        webs.BoolIntersect(vox);
        key_webs <<= Geez.voxels(webs, colour: col_webs);

        vox.BoolSubtract(outer_enclosure);
        // Now: vox = outer walls.
        key_running <<= Geez.voxels(vox);

        // Add interface and fillet this now complete outer surface.
        vox.BoolAdd(iface);
        vox.Fillet(3f);

        key_running <<= Geez.voxels(vox);
        key_iface <<= null;

        // Add inner walls and combine all.
        vox.BoolAdd(inner_enclosure.voxOffset(th_iw));
        vox.BoolAdd(webs);

        // Add chamber cavity.
        vox.BoolSubtract(inner_enclosure);

        // Remove holes.
        vox.BoolSubtract(iface_holes);

        key_running <<= Geez.voxels(vox);
        key_webs <<= null;

        // Clip axial excess.
        BBox3 bounds = vox.oCalculateBoundingBox();
        bounds.vecMin.Z = 0f;
        bounds.vecMax.Z = L_part;
        vox.Trim(bounds);

        key_running <<= Geez.voxels(vox);

        return vox;
    }



    public static void Task() {
        PicoGK.Library.Log("whas good");

        Chamber chamber = new Chamber{
            r_itrb = 67f,
            L_itfb = 5f,
            Bsz_itfb = 6f,
            th_itfb = 4.5f,
            no_itfb = 6,

            AEAT = 4f,
            L_cc = 100f,
            r_cc = 50f,
            r_tht = 20f,

            NLF = 1f,
            phi_conv = torad(-45f),
            phi_div = torad(21f),
            phi_exit = torad(10f),

            no_web = 40,
            th_iw = 1.5f,
            th_ow = 2.0f,
            th_web = 3f,
            wi_web = 1.5f,
        };
        chamber.check_realisable();

        Voxels vox = chamber.voxels();
        PicoGK.Library.Log("Baby made.");

        string path = fromroot("exports/chamber.stl");
        Leap71.ShapeKernel.Sh.ExportVoxelsToSTLFile(vox, path);

        PicoGK.Library.Log("Don.");
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

    public override float signed_dist(in Vec3 p) {
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
