
using static Br;
using Vec3 = System.Numerics.Vector3;
using Voxels = PicoGK.Voxels;
using Lattice = PicoGK.Lattice;

public class Chamber {

    public required float AEAT { get; init; }
    public required float L_cham { get; init; }
    public required float R_cham { get; init; }
    public required float R_thrt { get; init; }

    public required float NLF { get; init; }
    public required float theta_conv { get; init; }
    public required float theta_div { get; init; }
    public required float theta_exit { get; init; }

    public required float th_inner { get; init; }
    public required float th_outer { get; init; }
    public required float th_web { get; init; }
    public required int no_web { get; init; }

    public float R_exit => sqrt(AEAT) * R_thrt;

    /*
    https://www.desmos.com/calculator/uui8aqqr7u
    Consider the revolved contour of the nozzle interoir:
    _______
           '',,           C -,
               '\             o  ,,,-----
         CC      ',         ,-'''
                   '-.,__,-'      EXHAUST

    -------------------------------------
    '    '     ' '      '  '            '
    6    5     4 3      2  1            0
    6-5 line (chamber wall)
    5-4 circle arc
    4-3 line
    3-2 circle arc
    2-1 circle arc
    1-0 parabolic conic
    define a point C which is the intersection of the tangents at point 1 and 0.

    z: distance along axis of symmetry, 0 is throat plane.
    r: distance away from axis of symmetry, 0 is axis of symmetry.
    */

    public float nzl_Z0 => NLF * (3.732051f*R_exit
                                - 3.683473f*R_thrt);
    public float nzl_R0 => R_exit;
    public float nzl_Z1 => R_thrt * 0.382f*sin(theta_div);
    public float nzl_R1 => R_thrt * (1.382f - 0.382f*cos(theta_div));
    public float nzl_ZC => (tan(theta_exit)*nzl_Z0 - tan(theta_div)*nzl_Z1
                          + nzl_R1 - nzl_R0)
                           / (tan(theta_exit) - tan(theta_div));
    public float nzl_RC => tan(theta_exit) * (nzl_ZC - nzl_Z0) + nzl_R0;
    public float nzl_Z2 => 0f;
    public float nzl_R2 => R_thrt;
    public float nzl_Z3 => R_thrt * 1.5f*sin(theta_conv);
    public float nzl_R3 => R_thrt * (2.5f - 1.5f*cos(theta_conv));
    public float nzl_Z4 => nzl_Z3 + (R_cham - nzl_R3
                                   - 1.5f*R_thrt*(1f - cos(theta_conv)))
                                    / tan(theta_conv);
    public float nzl_R4 => tan(theta_conv)*(nzl_Z4 - nzl_Z3) + nzl_R3;
    public float nzl_Z5 => nzl_Z4 + (R_cham - nzl_R4) / (1f - cos(theta_conv))
                                    * sin(theta_conv);
    public float nzl_R5 => R_cham;
    public float nzl_Z6 => nzl_Z5 - L_cham;
    public float nzl_R6 => R_cham;

    public float nzl_para_ax => nzl_Z1 - 2f*nzl_ZC + nzl_Z0;
    public float nzl_para_bx => -2f*nzl_Z1 + 2f*nzl_ZC;
    public float nzl_para_cx => nzl_Z1;
    public float nzl_para_ay => nzl_R1 - 2f*nzl_RC + nzl_R0;
    public float nzl_para_by => -2f*nzl_R1 + 2f*nzl_RC;
    public float nzl_para_cy => nzl_R1;

    public void checkme() {
        // Easiest way to determine if the parameters create a realisable nozzle.

        assertx(nzl_Z0 > nzl_Z1, "Z0={0}, Z1={1}", nzl_Z0, nzl_Z1);
        assertx(nzl_Z1 > nzl_Z2, "Z1={0}, Z2={1}", nzl_Z1, nzl_Z2);
        assertx(nzl_Z2 > nzl_Z3, "Z2={0}, Z3={1}", nzl_Z2, nzl_Z3);
        assertx(nzl_Z3 > nzl_Z4, "Z3={0}, Z4={1}", nzl_Z3, nzl_Z4);
        assertx(nzl_Z4 > nzl_Z5, "Z4={0}, Z5={1}", nzl_Z4, nzl_Z5);
        assertx(nzl_ZC < nzl_Z0, "ZC={0}, Z0={1}", nzl_ZC, nzl_Z0);
        assertx(nzl_ZC > nzl_Z1, "ZC={0}, Z1={1}", nzl_ZC, nzl_Z1);

        assertx(nzl_R0 > nzl_R1, "R0={0}, R1={1}", nzl_R0, nzl_R1);
        assertx(nzl_R1 > nzl_R2, "R1={0}, R2={1}", nzl_R1, nzl_R2);
        assertx(nzl_R2 < nzl_R3, "R2={0}, R3={1}", nzl_R2, nzl_R3);
        assertx(nzl_R3 < nzl_R4, "R3={0}, R4={1}", nzl_R3, nzl_R4);
        assertx(nzl_R4 < nzl_R5, "R4={0}, R5={1}", nzl_R4, nzl_R5);
        assertx(nzl_RC < nzl_R0, "RC={0}, R0={1}", nzl_RC, nzl_R0);
        assertx(nzl_RC > nzl_R1, "RC={0}, R1={1}", nzl_RC, nzl_R1);
    }


    public static void Task() {
        PicoGK.Library.Log("Starting Task.");

        Chamber chamber = new Chamber{
            AEAT = 4f,
            L_cham = 100f,
            R_cham = 50f,
            R_thrt = 20f,
            NLF = 1f,
            theta_conv = -torad(45f),
            theta_div = torad(25f),
            theta_exit = torad(8f),

            th_inner = 1.5f,
            th_outer = 2.0f,
            th_web = 1.5f,
            no_web = 40,
        };
        Voxels voxels = chamber.voxels();

        PicoGK.Library.Log("voxel done.");
        Leap71.ShapeKernel.Sh.PreviewVoxels(
            voxels,
            new PicoGK.ColorFloat("#501f14"),
            fTransparency: 0.9f,
            fMetallic: 0.4f,
            fRoughness: 0.3f
        );
        // Leap71.ShapeKernel.Sh.ExportVoxelsToSTLFile(
        //     voxels,
        //     Path.Combine(PicoGK.Utils.strProjectRootFolder(), $"exports/helix.stl")
        // );

        PicoGK.Library.Log("Finished Task successfully.");
    }


    private float r_inner(float z) {
        if (z >= nzl_Z0) {
            // Techincally past exit plane but keep for edge cases or similar.
            return R_exit;
        }
        if (z >= nzl_Z1) {
            float p = (-nzl_para_bx + sqrt(nzl_para_bx*nzl_para_bx
                                         - 4f*nzl_para_ax*(nzl_para_cx - z)))
                      / 2f / nzl_para_ax;
            return (nzl_para_ay*p + nzl_para_by)*p + nzl_para_cy;
        }
        if (z >= nzl_Z2) {
            return 1.382f*R_thrt - sqrt(0.145924f*R_thrt*R_thrt - z*z);
        }
        if (z >= nzl_Z3) {
            return 2.5f*R_thrt - sqrt(2.25f*R_thrt*R_thrt - z*z);
        }
        if (z >= nzl_Z4) {
            return tan(theta_conv) * (z - nzl_Z3) + nzl_R3;
        }
        if (z >= nzl_Z5) {
            float R_conv = (R_cham - nzl_R4) / (1f - cos(theta_conv));
            z += -nzl_Z4 - R_conv*sin(theta_conv);
            z *= z;
            return nzl_R4 - R_conv*cos(theta_conv) + sqrt(R_conv*R_conv - z);
        }
        // Might be past chamber top plane but we dont worry about it.
        return R_cham;
    }

    private Lattice interior(int divisions = 200) {
        Lattice lattice = new Lattice();

        void push_beam(float Za, float Zb) {
            float Ra = r_inner(Za);
            float Rb = r_inner(Zb);
            lattice.AddBeam(
                    new Vec3(0f, 0f, nzl_Z0 - Za), Ra,
                    new Vec3(0f, 0f, nzl_Z0 - Zb), Rb,
                    false
                );
        }
        void push_section(float ZA, float ZB, int divisions) {
            for (int i=0; i<divisions; ++i) {
                float Za = ZA +       i * (ZB - ZA) / divisions;
                float Zb = ZA + (i + 1) * (ZB - ZA) / divisions;
                push_beam(Za, Zb);
            }
        }

        // 3-4 and 5-6 are each one beam element. each other one gets a differing
        // number. we also add a beam element to the top.
        assertx(divisions >= 20, "divisions={0}", divisions);
        float weight_01 = 1.0f * (nzl_Z0 - nzl_Z1);
        float weight_12 = 4.0f * (nzl_Z1 - nzl_Z2);
        float weight_23 = 1.5f * (nzl_Z2 - nzl_Z3);
        float weight_45 = 1.5f * (nzl_Z4 - nzl_Z5);

        float net_weight = weight_01 + weight_12 + weight_23 + weight_45;
        // weight_01 /= net_weight; // unused from here.
        weight_12 /= net_weight;
        weight_23 /= net_weight;
        weight_45 /= net_weight;

        divisions -= 3;
        int divisions_12 = (int)(weight_12 * divisions);
        int divisions_23 = (int)(weight_23 * divisions);
        int divisions_45 = (int)(weight_45 * divisions);
        divisions -= divisions_12 + divisions_23 + divisions_45;
        int divisions_01 = divisions; // all remaining.

        push_section(nzl_Z0 + 10f, nzl_Z0, 1);
        push_section(nzl_Z0, nzl_Z1, divisions_01);
        push_section(nzl_Z1, nzl_Z2, divisions_12);
        push_section(nzl_Z2, nzl_Z3, divisions_23);
        push_section(nzl_Z3, nzl_Z4, 1);
        push_section(nzl_Z4, nzl_Z5, divisions_45);
        push_section(nzl_Z5, nzl_Z6 - 10f, 1);

        return lattice;
    }

    public Voxels voxels() {
        this.checkme();

        Voxels inner_interior = new Voxels(interior());
        // Substantially improves the smoothness:
        inner_interior = inner_interior.voxOffset(0.01f);
        inner_interior = inner_interior.voxOffset(-0.01f);

        Voxels outer_interior = inner_interior.voxOffset(th_inner + th_web);

        Voxels walls;
        walls = outer_interior.voxOffset(th_outer);
        walls.BoolSubtract(outer_interior);
        walls.BoolAdd(inner_interior.voxOffset(th_inner));
        walls.BoolSubtract(inner_interior);

        PicoGK.BBox3 bounds = new PicoGK.BBox3(
                new Vec3(0f, -2f*R_cham, 0f),
                new Vec3( 2f*R_cham,  2f*R_cham, nzl_Z0 - nzl_Z6)
            );
        walls.Trim(bounds);

        return walls;
    }
}
