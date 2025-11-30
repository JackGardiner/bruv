
using static Br;
using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Lattice = PicoGK.Lattice;
using IBoundedImplicit = PicoGK.IBoundedImplicit;
using BBox3 = PicoGK.BBox3;

using Frames = Leap71.ShapeKernel.Frames;
using BaseBox = Leap71.ShapeKernel.BaseBox;

public class Chamber {

    public bool sectionview = false;

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


    protected float axial_extra = 10f; // trimmed in final step.

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

    protected List<Vec3> points_interior(int divisions=100) {
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

    protected List<Vec3> points_web(float theta0=0f, int divisions=100) {
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

    protected Voxels voxels_interior(int divisions=100) {
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

    protected Voxels voxels_webs() {
        Voxels webs = new();
        for (int i=0; i<no_web; ++i) {
            float theta0 = i * TWOPI / no_web;
            float th = th_web + min(th_iw, th_ow) / 2;
            float wi = wi_web;
            List<Vec3> points = points_web(theta0);
            points.Insert(0, points[0] - axial_extra*Vec3.UnitZ);
            points.Add(points[^1] + axial_extra*Vec3.UnitZ);
            Frames frames = new(points, Frames.EFrameType.CYLINDRICAL);
            Leap71.ShapeKernel.Sh.PreviewFrame(frames.oGetLocalFrame(0f), 3f);
            BaseBox web = new(frames, th, wi);
            webs += web.voxConstruct();
        }
        return webs;
    }


    protected Voxels voxels_iface_bolt_hole(float theta=0f) {
        Vec3 at = rejxy(tocart(r_itrb, theta));
        Pipe hole = new(
            at,
            L_itfb,
            Bsz_itfb/2f
        );
        Leap71.ShapeKernel.Sh.PreviewBoxWireframe(hole.bounds, COLOUR_CYAN);
        return hole.voxels();
    }

    protected Voxels voxels_iface_bolt(float theta=0f) {
        Vec2 at2 = tocart(r_itrb, theta);
        Vec3 at = rejxy(at2);
        Pipe disc = new(
            at,
            L_itfb,
            Bsz_itfb/2f + th_itfb
        );

        float ang_off = torad(10f);
        List<Vec2> corners = new List<Vec2>(4){
            at2 + tocart(Bsz_itfb/2f + th_itfb, theta - PI_2 + ang_off),
            at2 + tocart(Bsz_itfb/2f + th_itfb, theta + PI_2 - ang_off)
        };
        float x2;
        float y2;
        float x3;
        float y3;
        if (abs(theta - ang_off - PI_2) < 1e-5 ||
                abs(theta - ang_off - 3*PI_2) < 1e-5) {
            // m = vertical.
            x2 = corners[1].X;
            y2 = 0f;
        } else {
            float m = tan(theta - ang_off);
            // m * (x - corners[*].X) + corners[*].Y = -1/m x
            x2 = (-m*corners[1].X + corners[1].Y) / (-m - 1/m);
            y2 = -x2/m;
        }
        if (abs(theta + ang_off - PI_2) < 1e-5 ||
                abs(theta + ang_off - 3*PI_2) < 1e-5) {
            x3 = corners[0].X;
            y3 = 0f;
        } else {
            float m = tan(theta + ang_off);
            x3 = (-m*corners[0].X + corners[0].Y) / (-m - 1/m);
            y3 = -x2/m;
        }
        corners.Add(new Vec2(x2, y2));
        corners.Add(new Vec2(x3, y3));
        Polygon flange = new(corners, 0f, L_itfb);

        return disc.voxels() + flange.voxels();
    }

    protected Voxels voxels_iface() {
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

    protected Voxels voxels_iface_holes() {
        Voxels iface = new();
        for (int i=0; i<no_itfb; ++i) {
            float theta = i * TWOPI / no_itfb;
            iface += voxels_iface_bolt_hole(theta);
        }
        return iface;
    }
    public Voxels voxels() {

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

        Voxels iface_holes = voxels_iface_holes();
        Voxels iface = voxels_iface();
        geez(iface);
        Voxels webs = voxels_webs();
        geez(webs);

        Voxels inner_enclosure = voxels_interior();
        inner_enclosure.TripleOffset(-0.01f); // smooth.

        geez(inner_enclosure);

        Voxels outer_enclosure = inner_enclosure.voxOffset(th_iw + th_web);


        Voxels vox;

        // Add outer wall.
        vox = outer_enclosure.voxOffset(th_ow);

        geez(vox, pop: 1);

        // Now: vox = outer filled, so do some operations.
        webs.BoolIntersect(vox);
        iface.BoolSubtract(vox);

        // Make: vox = outer walls.
        vox.BoolSubtract(outer_enclosure);

        geez(vox, pop: 1);

        // Add interface and fillet this now complete outer surface.
        vox.BoolAdd(iface);
        vox.Fillet(3f);

        geez(vox, pop: 1);

        // Add inner walls and combine all.
        vox.BoolAdd(inner_enclosure.voxOffset(th_iw));
        vox.BoolAdd(webs);

        // Add chamber cavity.
        vox.BoolSubtract(inner_enclosure);

        // Remove holes.
        vox.BoolSubtract(iface_holes);

        geez(vox, pop: 3);

        // Clip axial excess.
        BBox3 bounds = vox.oCalculateBoundingBox();
        bounds.vecMin.Z = 0f;
        bounds.vecMax.Z = L_part;
        vox.Trim(bounds);

        geez(vox, pop: 1);

        return vox;
    }



    public static void Task() {
        PicoGK.Library.Log("whas good");

        Chamber chamber = new Chamber{
            r_itrb = 72f,
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
        chamber.sectionview = true;

        Voxels vox = chamber.voxels();
        PicoGK.Library.Log("Baby made.");

        string path = PicoGK.Utils.strProjectRootFolder();
        path = Path.Combine(path, "exports/chamber.stl");
        Leap71.ShapeKernel.Sh.ExportVoxelsToSTLFile(vox, path);

        PicoGK.Library.Log("Don.");
    }


    protected List<Voxels> _viewed = new();
    protected void geez(in Voxels vox, int pop=0) {
        // Cross section if requested.
        Voxels avox;
        if (sectionview) {
            BBox3 bounds = vox.oCalculateBoundingBox();
            bounds.vecMin.X = 0f;
            // dont trim the original.
            Voxels box = new(PicoGK.Utils.mshCreateCube(bounds));
            avox = vox.voxBoolIntersect(box);
        } else avox = vox;
        Leap71.ShapeKernel.Sh.PreviewVoxels(
            avox,
            new PicoGK.ColorFloat("#501f14"),
            fTransparency: 0.5f,
            fMetallic: 0.4f,
            fRoughness: 0.3f
        );

        while (pop --> 0) // so glad down-to operator made it into c#.
            pop_geez();

        _viewed.Add(avox);
    }
    protected void pop_geez() {
        Voxels vox = _viewed[_viewed.Count - 1];
        _viewed.RemoveAt(_viewed.Count - 1);
        PicoGK.Library.oViewer().Remove(vox);
    }
}



public abstract class SDF : IBoundedImplicit {
    public abstract float signed_dist(in Vec3 p);

    public Voxels voxels() {
        return new Voxels(this);
    }


    public float fSignedDistance(in Vec3 p) => signed_dist(p);
    protected BBox3 bounds_fr;
    protected bool has_bounds = false;
    public BBox3 bounds => bounds_fr;
    public BBox3 oBounds => bounds_fr;

    protected void set_bounds(Vec3 min, Vec3 max) {
        bounds_fr = new BBox3(min, max);
        has_bounds = true;
    }
    protected void include_in_bounds(Vec3 p) {
        if (!has_bounds) {
            bounds_fr = new BBox3(p, p);
        } else {
            bounds_fr.Include(p);
        }
        has_bounds = true;
    }
}


public class Pipe : SDF {
    public int axis { get; }
    public Vec3 centre { get; }
    public float L { get; }
    public float rlo { get; }
    public float rhi { get; }
    protected float axial_lo;
    protected float axial_hi;
    protected Vec2 centre_proj;

    public Pipe(in Vec3 centre, float L, float rhi, int axis=2)
        : this(centre, L, 0f, rhi, axis) {}
    public Pipe(in Vec3 centre, float L, float rlo, float rhi, int axis=2) {
        assertx(rhi >= rlo, "rlo={0}, rhi={1}", rlo, rhi);
        assertx(rlo >= 0f, "rlo={0}, rhi={1}", rlo, rhi);
        assertx(0 <= axis && axis <= 2, "axis={0}", axis);
        this.axis = axis;
        this.centre = centre;
        this.L = L;
        this.rlo = rlo;
        this.rhi = rhi;
        Vec3 pmin = centre - rhi*ONE3;
        Vec3 pmax = centre + rhi*ONE3;
        pmin[axis] = centre[axis] + min(0f, L);
        pmax[axis] = centre[axis] + max(0f, L);

        axial_lo = pmin[axis];
        axial_hi = pmax[axis];
        centre_proj = projection(centre, axis);
        set_bounds(pmin, pmax);
    }

    public override float signed_dist(in Vec3 p) {
        float r = mag(projection(p, axis) - centre_proj);

        float dist_radial = max(rlo - r, r - rhi);

        float dist_axial = max(axial_lo - p[axis], p[axis] - axial_hi);

        float dist;
        if (dist_radial <= 0f || dist_axial <= 0f) {
            dist = max(dist_radial, dist_axial);
        } else {
            dist = hypot(dist_radial, dist_axial);
        }
        return dist;
    }

    public Pipe filled() {
        if (rlo == 0f)
            return this;
        return new Pipe(centre, L, rhi, axis);
    }
    public Pipe hole() {
        return new Pipe(centre, L, rlo, axis);
    }
}


public class Polygon : SDF {
    public int axis { get; }
    public List<Vec2> points { get; }
    public float axial_lo { get; }
    public float axial_hi { get; }

    public Polygon(in List<Vec2> points, float axial_lo, float axial_hi,
            int axis=2) {
        assert(is_simple_polygon(points));
        assertx(points.Count >= 3, "length={0}", points.Count);
        assertx(axial_hi >= axial_lo, "lo={0}, hi={1}", axial_lo, axial_hi);
        this.axis = axis;
        this.points = points;
        this.axial_lo = axial_lo;
        this.axial_hi = axial_hi;
        for (int i=0; i<points.Count; ++i) {
            float axial = ((i % 2) == 0) ? axial_lo : axial_hi;
            Vec3 p = rejection(points[i], axis, axial);
            include_in_bounds(p);
        }
    }

    public override float signed_dist(in Vec3 p) {
        Vec2 p_proj = projection(p, axis);

        int N = points.Count;
        int winding = 0;

        float dist_proj = INF;
        for (int i=0; i<N; ++i) {
            Vec2 a = points[i];
            Vec2 b = points[(i + 1 == N) ? 0 : i + 1];

            Vec2 ab = b - a;
            Vec2 ap = p_proj - a;

            if (a.Y <= p_proj.Y) {
                if (b.Y > p_proj.Y && cross(ab, ap) > 0)
                    winding += 1;
            } else {
                if (b.Y <= p_proj.Y && cross(ab, ap) < 0)
                    winding -= 1;
            }

            float t = dot(ap, ab) / dot(ab, ab);
            t = clamp(t, 0f, 1f); // clamp to segment
            Vec2 closest = a + t*ab;

            float d = mag(p_proj - closest);
            dist_proj = min(dist_proj, d);
        }

        if (winding != 0)
            dist_proj = -dist_proj;

        float dist_axial = max(axial_lo - p[axis], p[axis] - axial_hi);

        float dist;
        if (dist_proj <= 0f || dist_axial <= 0f) {
            dist = max(dist_proj, dist_axial);
        } else {
            dist = hypot(dist_proj, dist_axial);
        }
        return dist;
    }



    protected static bool is_simple_polygon(in List<Vec2> points) {
        int N = points.Count;
        if (N < 3) return false;

        for (int i=0; i<N; ++i) {
            int i_1 = (i + 1 == N) ? 0 : i + 1;
            Vec2 a0 = points[i];
            Vec2 a1 = points[i_1];

            for (int j=i + 1; j<N; ++j) {
                int j_1 = (j + 1 == N) ? 0 : j + 1;
                Vec2 b0 = points[j];
                Vec2 b1 = points[j_1];

                // Skip adjacent edges.
                if (i == j || i_1 == j || j_1 == i)
                    continue;

                float o1 = cross(a1 - a0, b0 - a0);
                float o2 = cross(a1 - a0, b1 - a0);
                float o3 = cross(b1 - b0, a0 - b0);
                float o4 = cross(b1 - b0, a1 - b0);
                if ((o1*o2 < 0) && (o3*o4 < 0))
                    return false;
            }
        }
        return true;
    }
}



// public class Web : SDF {
//     public List<Vec3> points { get; }
//     public float Ltheta { get; }
//     public float Lr { get; }
//     protected float zlo;
//     protected float zhi;

//     public Web(in List<Vec3> points, float Ltheta, float Lr) {
//         assertx(points.Count >= 2, "length={0}", points.Count);
//         this.points = points;
//         this.Ltheta = Ltheta;
//         this.Lr = Lr;

//         zlo = +INF;
//         zhi = -INF;
//         float prevz = -INF;
//         for (int i=0; i<points.Count; ++i) {
//             float z = points[i].Z;
//             assertx(z > prevz, "prevz={0}, z={1}", prevz, z);
//             prevz = z;
//             zlo = min(zlo, z);
//             zhi = max(zhi, z);

//             Vec2 p = projxy(points[i]);
//             float r = mag(p);
//             float theta = arg(p);
//             float rmin = r - Lr/2f;
//             float rmax = r + Lr/2f;
//             float thetamin = theta - Ltheta/2f;
//             float thetamax = theta - Ltheta/2f;
//             Vec2 corner0 = tocart(rmin, thetamin);
//             Vec2 corner1 = tocart(rmin, thetamax);
//             Vec2 corner2 = tocart(rmax, thetamin);
//             Vec2 corner3 = tocart(rmax, thetamax);
//             Vec2 pmin = min(corner0, corner1, corner2, corner3);
//             Vec2 pmax = max(corner0, corner1, corner2, corner3);
//             include_in_bounds(rejxy(pmin, z));
//             include_in_bounds(rejxy(pmax, z));
//         }
//     }

//     public override float signed_dist(in Vec3 p) {
//         int N = points.Count;

//         float dist_proj = INF;
//         float angle = 0f;
//         for (int i=1; i<N; ++i) {
//             Vec3 a = points[i - 1];
//             Vec3 b = points[i];

//             Vec3 ab = b - a;
//             Vec3 ap = p - a;

//             float t = dot(ap, ab) / dot(ab, ab);
//             t = clamp(t, 0f, 1f); // clamp to segment
//             Vec3 closest = a + t*ab;

//             float d = mag(p - closest);
//             if (d < dist_proj) {
//                 dist_proj = d;
//                 angle = acos(dot(ab, p - closest) / mag(ab) / d);
//             }
//         }

//         float dist_z = max(zlo - p.Z, p.Z - zhi);
//         float dist_along = dist_z *
//         float dist = hypot(dist_proj, dist_along);
//         return dist;
//     }
// }
