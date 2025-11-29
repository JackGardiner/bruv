
using static Br;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using BBox3 = PicoGK.BBox3;
using Lattice = PicoGK.Lattice;
using PolyLine = PicoGK.PolyLine;

using Frames = Leap71.ShapeKernel.Frames;
using LocalFrame = Leap71.ShapeKernel.LocalFrame;
using BaseBox = Leap71.ShapeKernel.BaseBox;
using BaseCylinder = Leap71.ShapeKernel.BaseCylinder;
using LineModulation = Leap71.ShapeKernel.LineModulation;

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
    // Ir = inner radius.
    // L = length.
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


    private float axial_extra = 10f; // trimmed in final step.

    public void check_realisable() {
        // Easiest way to determine if the parameters create a realisable nozzle.

        assertx(nzl_z0 < nzl_z1, "z0={0}, z1={1}", nzl_z0, nzl_z1);
        assertx(nzl_z1 < nzl_z2, "z1={0}, z2={1}", nzl_z1, nzl_z2);
        assertx(nzl_z2 < nzl_z3, "z2={0}, z3={1}", nzl_z2, nzl_z3);
        assertx(nzl_z3 < nzl_z4, "z3={0}, z4={1}", nzl_z3, nzl_z4);
        assertx(nzl_z4 < nzl_z5, "z4={0}, z5={1}", nzl_z4, nzl_z5);
        assertx(nzl_z5 < nzl_z6, "z5={0}, z6={1}", nzl_z5, nzl_z6);
        assertx(nzl_zP > nzl_z5, "zP={0}, z5={1}", nzl_zP, nzl_z5);
        assertx(nzl_zP < nzl_z6, "zP={0}, z6={1}", nzl_zP, nzl_z6);

        // assertx(nzl_r0 == nzl_r1, "r0={0}, r1={1}", nzl_r0, nzl_r1);
        assertx(nzl_r1 > nzl_r2, "r1={0}, r2={1}", nzl_r1, nzl_r2);
        assertx(nzl_r2 > nzl_r3, "r2={0}, r3={1}", nzl_r2, nzl_r3);
        assertx(nzl_r3 > nzl_r4, "r3={0}, r4={1}", nzl_r3, nzl_r4);
        assertx(nzl_r4 < nzl_r5, "r4={0}, r5={1}", nzl_r4, nzl_r5);
        assertx(nzl_r5 < nzl_r6, "r5={0}, r6={1}", nzl_r5, nzl_r6);
        assertx(nzl_rP > nzl_r5, "rC={0}, r5={1}", nzl_rP, nzl_r5);
        assertx(nzl_rP < nzl_r6, "rC={0}, r6={1}", nzl_rP, nzl_r6);
    }


    private void divvy(int divisions, int[] divs) {
        assert(divs.Length == 6);
        // 0-1 and 2-3 are each one beam element. each other one gets a differing
        // number.
        assertx(divisions >= 20, "divisions={0}", divisions);
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

    private List<Vec3> points_interior(int divisions=100) {
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

    private List<Vec3> points_web(float theta0=0f, int divisions=100) {
        List<Vec3> points = new();
        for (int i=0; i<=divisions; ++i) {
            float z = nzl_z0 + i * (nzl_z6 - nzl_z0) / divisions;
            float theta = theta_web(z) + theta0;
            float r = Ir_part(z) + th_iw + th_web/2f;

            Vec3 point = new(r*cos(theta), r*sin(theta), z);
            points.Add(point);
        }
        return points;
    }

    private Voxels voxels_interior(int divisions=100) {
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

    private Voxels voxels_webs() {
        Voxels webs = new();
        for (int i=0; i<no_web; ++i) {
            float theta0 = i * TWOPI / no_web;
            float th = th_web + min(th_iw, th_ow) / 2;
            float wi = wi_web;
            List<Vec3> points = points_web(theta0);
            points.Insert(0, points[0] - axial_extra*Vec3.UnitZ);
            points.Add(points[^1] + axial_extra*Vec3.UnitZ);
            Frames frames = new(points, Frames.EFrameType.CYLINDRICAL);
            BaseBox web = new(frames, th, wi);
            webs += web.voxConstruct();
        }
        return webs;
    }

    public Voxels voxels_iface_bolt(float theta=0f) {

        Vec3 at = new(r_itrb*cos(theta), r_itrb*sin(theta), 0f);
        BaseCylinder hole = new(
            new LocalFrame(at),
            L_itfb,
            Bsz_itfb/2f
        );
        BaseCylinder surrounding = new(
            new LocalFrame(at),
            L_itfb,
            Bsz_itfb/2f + th_itfb
        );
        LocalFrame frame = new LocalFrame(
            at + new Vec3(0f, 0f, L_itfb/2f),
            new Vec3(-cos(theta), -sin(theta), 0f),
            new Vec3(sin(theta), -cos(theta), 0f)
        );
        Leap71.ShapeKernel.Sh.PreviewFrame(frame, 5f);
        BaseBox flange = new(
            frame,
            r_itrb,
            NAN,
            L_itfb
        );
        float width(float t) => Bsz_itfb + 2f*th_itfb * (1f + t / (r_itrb - r_cc) * r_itrb);
        flange.SetWidth(new LineModulation(width));

        Voxels vox;
        vox = surrounding.voxConstruct();
        vox += flange.voxConstruct();
        vox -= hole.voxConstruct();

        return vox;
    }

    public Voxels voxels_iface() {
        Voxels iface = new();
        for (int i=0; i<no_itfb; ++i) {
            float theta = i * TWOPI / no_itfb;
            iface += voxels_iface_bolt(theta);
        }
        Lattice flange = new();
        flange.AddBeam(
            Vec3.Zero, r_cc + th_iw + th_web + th_ow + 8f,
            new Vec3(0f, 0f, L_itfb), r_cc + th_iw + th_web + th_ow + 8f,
            false
        );
        iface += new Voxels(flange);
        return iface;
    }

    public Voxels voxels(bool crosssectioned = false) {

        // Order of construction is this:
        // - construct webs.
        // - construct interface.
        // - construct volume enclosed by inner wall (excluding the wall).
        // - offset inner enclosure to get outer enclosure (ex. wall).
        // - offset outer enclosure to get outer filled (= enclosure inc. wall).
        // - subtract inner enclosure from inner filled to get inner walls.
        // - subtract outer enclosure from outer filled to get outer walls.
        // - intersect webs with outer filled to trim excess.
        // - subtract inner enclosure from webs to trim excess.
        // - subtract outer filled from interface to trim excess.
        // - combine all.
        // These steps are interleaved however to reduce operations.

        Voxels webs = voxels_webs();
        Voxels iface = voxels_iface();

        Voxels inner_enclosure = voxels_interior();
        inner_enclosure.TripleOffset(-0.01f); // smooth.

        Voxels outer_enclosure = inner_enclosure.voxOffset(th_iw + th_web);


        Voxels vox;

        // Add outer wall.
        vox = outer_enclosure.voxOffset(th_ow + 0.5f);
        vox.Offset(-0.5f); // round sharp concave corner.

        // Now: vox = outer filled, so do some operations.
        webs.BoolIntersect(vox);
        iface.BoolSubtract(vox);

        // Make: vox = outer walls.
        vox.BoolSubtract(outer_enclosure);

        // Add inner walls and combine all.
        vox.BoolAdd(inner_enclosure.voxOffset(th_iw));
        vox.BoolAdd(webs);
        vox.BoolAdd(iface);

        // Add chamber cavity.
        vox.BoolSubtract(inner_enclosure);


        // Clip axial excess.
        BBox3 bounds = vox.oCalculateBoundingBox();
        bounds.vecMin.Z = 0f;
        bounds.vecMax.Z = L_part;
        if (crosssectioned)
            bounds.vecMin.X = 0f;
        vox.Trim(bounds);

        return vox;
    }



    public static void Task() {
        PicoGK.Library.Log("whas good");

        Chamber chamber = new Chamber{
            r_itrb = 72f,
            L_itfb = 5f,
            Bsz_itfb = 6f,
            th_itfb = 3.5f,
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

        Voxels voxels = chamber.voxels(crosssectioned: true);
        PicoGK.Library.Log("Baby made.");

        Leap71.ShapeKernel.Sh.PreviewVoxels(
            voxels,
            new PicoGK.ColorFloat("#501f14"),
            fTransparency: 0.5f,
            fMetallic: 0.4f,
            fRoughness: 0.3f
        );

        PolyLine polyline_interior = new("#FF0000");
        polyline_interior.Add(chamber.points_interior());
        PicoGK.Library.oViewer().Add(polyline_interior);

        for (int i=0; i<chamber.no_web; ++i) {
            PolyLine polyline_web = new("#00FF00");
            polyline_web.Add(chamber.points_web(i * TWOPI / chamber.no_web));
            PicoGK.Library.oViewer().Add(polyline_web);
        }

        string path = PicoGK.Utils.strProjectRootFolder();
        path = Path.Combine(path, "exports/chamber.stl");
        Leap71.ShapeKernel.Sh.ExportVoxelsToSTLFile(voxels, path);

        PicoGK.Library.Log("Don.");
    }
}
