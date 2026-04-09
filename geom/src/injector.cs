using static br.Br;
using br;
using TPIAP = TwoPeasInAPod;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;


/* INEJCTOR ELEMENT DESIGN, BI-SWIRL COAXIAL.
   the big boy.


                  ,------| |--,
                  .',     .   .',______ ^ +r
         ,-| |-------',   .   . .     . |
       ,'.  .     .  . ',__________   .
     ,'  .  .     .  .  . .   . . .   .  -> -z
- - - - - - - - - - - - - - - - - - - +
    .    .  .     .  .  . .   . . .   . \
FOR INJ1:.  .     .  .  . .   . . .   .  '- origin
    A    B  C     .  D  E .   . . F   .
                  .  .    .   . .     .
FOR INJ2:         .  .    .   . .     .
                  B  A    C   D E     F
define D1 as the intersection between B1C1 & B2E1.
define A2 = D1.

the C1 and C2 holes are offset s.t. their outer edge is tangential with the inner
boundary. so, the picture is a little misleading in that they may appear
perfectly radial when they're not.

clearest ascii diagram.

*/


public class InjectorElement {
    public Vec2 A1 = NAN2;
    public Vec2 B1 = NAN2;
    public Vec2 C1 = NAN2;
    public Vec2 D1 = NAN2;
    public Vec2 E1 = NAN2;
    public Vec2 F1 = NAN2;
    public Vec2 A2 = NAN2;
    public Vec2 B2 = NAN2;
    public Vec2 C2 = NAN2;
    public Vec2 D2 = NAN2;
    public Vec2 E2 = NAN2;
    public Vec2 F2 = NAN2;

    public bool printable { get; set; } = false;
    public float extend_base_by { get; set; } = NAN;

    public required float phi { get; init; }
    /* ^ must be same as LOx/OPA dividing cone */

    public required float th_plate { get; init; }
    public required float th_dmw { get; init; }

    public required float rho_1 { get; init; }
    public required float rho_2 { get; init; }
    public required float mu_1 { get; init; }
    public required float mu_2 { get; init; }

    public required float DP_1 { get; init; }
    public required float DP_2 { get; init; }
    public required float mdot_1 { get; init; }
    public required float mdot_2 { get; init; }

    public required float th_inj1 { get; init; }
    public required float th_inj2 { get; init; }
    public required float th_nz1 { get; init; }
    public required float th_il1 { get; init; }
    public required float th_il2 { get; init; }
    public required float FR_LOx { get; init; }
    public required float FR_IPA { get; init; }
    public required float FR_small { get; init; }
    public required float CR_nz1 { get; init; }
    public required float CR_nz2 { get; init; }
    public float D_il1 = NAN;
    public float D_il2 = NAN;
    public float L_il1 = NAN;
    public float L_il2 = NAN;
    public float z0_dmw = NAN;
    public float max_r = NAN;
    public float max_z = NAN;

    // JBS testing experimental correction factors; 'x' for eXperiment-corrected
    public float K_mdot_1 { get; init; } = 1.294592f;
    public float K_mdot_2 { get; init; } = 2.542903f;
    public float K_mdot_extra { get; init; } = 1f; // additional factor.

    // Number of tangential inlets.
    public int no_il1 { get; init; } = 4;
    public int no_il2 { get; init; } = 4;

    // Coefficients of nozzle opening: IR_ch/IR_nz
    // reasonable bounds: idx?
    public float Rbar_ch1 { get; init; } = 1.4f;
    public float Rbar_ch2 { get; init; } = 1.2f;

    // Relative nozzle lengths: L_nz/2/IR_nz
    // idk why they put a factor of 2.
    // Lbar_nz1 is prescribed as 1.0 by procedure.
    public float Lbar_nz1 { get; init; } = 1.0f;
    public float Lbar_nz2 { get; init; } = 0.5f;

    // Relative chamber lengths: L_ch/IR_ch
    // reasonable bounds: [2, 3]
    public float Lbar_ch1 { get; init; } = 4.0f;

    // Spray cone angle of stage 1.
    // reasonable bounds: [60deg, 80deg]
    public float twoalpha_1 { get; init; } = torad(80f);

    // Mixing residence time.
    // reasonable bounds: [0.1ms, 1.5ms]
    public float tau_i { get; init; } = 0.15e-3f;


    private const float EXTRA = 6f;
    public static string REPORT_PATH
        => fromroot($"exports/injector-element-report.txt");

    private bool inited = false;
    public void initialise() {
        assert(!inited);

        // Make "pretend" mass flow rates to design the element around which in
        // reality (from testing) give the desired mfr.
        float mdot_1_x = mdot_1 / K_mdot_1 / K_mdot_extra;
        float mdot_2_x = mdot_2 / K_mdot_2 / K_mdot_extra;

        // Within this ALL LENGTHS ARE IN METRES. converted to mm at end.

        assert(phi > 0f);
        assert(phi < PI_2);

        // Set chamber 2 length to fuck off uge. its clipped at end of this
        // function.
        float Lbar_ch2 = 100f;

        // idk why a separate variable C was used in the paper but we just gonna
        // use its definition of Rbar.


        /* stage 1 (LOx): */

        float A_1 = GraphLookup.get_A(twoalpha_1, Lbar_nz1);
        float Cd_1 = GraphLookup.get_Cd(A_1, Rbar_ch1);
        float rmbar_1 = GraphLookup.get_rmbar(A_1, Rbar_ch1);

        float Ir_nz1 = sqrt(mdot_1_x/PI/Cd_1/sqrt(2f*rho_1*DP_1));
        float L_nz1 = 2f*Lbar_nz1*Ir_nz1;

        float Ir_ch1 = Rbar_ch1*Ir_nz1;
        float L_ch1 = Lbar_ch1*Ir_ch1;

        float Ir_il1 = sqrt(Ir_ch1*Ir_nz1/no_il1/A_1);
        float L_il1 = Ir_ch1 + 3f*Ir_il1;


        /* stage 2 (IPA): */

        float min_fluid_inner_radius_2 = Ir_nz1 + th_nz1*1e-3f + 0.3e-3f;

        // Initial guess for nozzle radius.
        float Ir_nz2 = min_fluid_inner_radius_2;

        // Iteratively solve nozzle dimensions.
        float A_2 = NAN;
        float Cd_2 = NAN;
        float rmbar_2 = NAN;

        const int MAX_ITERS = 100;
        const float TOLERANCE = 0.001e-3f;
        float diff = +INF;
        for (int iter=0; iter<MAX_ITERS; ++iter) {

            // 1. Calculate current Cd based on current Ir.
            Cd_2 = (mdot_1_x + mdot_2_x)/PI/sqed(Ir_nz2)/sqrt(2f*rho_2*DP_2);

            // 2. Find A based on Cd (from Fig 34).
            A_2 = GraphLookup.get_A_from_Cd(Cd_2, Rbar_ch2);

            // 3. Find dimensionless relative vortex radius (from Fig 35).
            rmbar_2 = GraphLookup.get_rmbar(A_2, Rbar_ch2);

            // Leave if we're under tol (note we recalced 1.2.3.).
            if (diff < TOLERANCE)
                break;
            assert(iter != MAX_ITERS - 1);

            // 4. Calculate NEW physical nozzle radius.
            float next = min_fluid_inner_radius_2 / rmbar_2;
            diff = abs(next - Ir_nz2);
            Ir_nz2 = next;

            // re-loop to re-calc Cd/A/rmbar.
        }
        assert(Ir_nz2 >= min_fluid_inner_radius_2);

        float L_nz2 = 2f*Lbar_nz2*Ir_nz2;

        float Ir_ch2 = Rbar_ch2*Ir_nz2;
        float L_ch2 = Lbar_ch2*Ir_ch2;

        float Ir_il2 = sqrt(Ir_ch2*Ir_nz2/no_il2/A_2);
        float L_il2 = Ir_ch2 + 3f*Ir_il2;


        /* Inner nozzle vertical offset: */

        float K_m = mdot_1/mdot_2;
        float cspf_1 = 1f - sqed(rmbar_1); // coefficient of stage passage
        float cspf_2 = 1f - sqed(rmbar_2); //   fullness.
        float mixing_length = SQRT2 * tau_i
                            * (
                                K_m/(K_m + 1f)*Cd_2/cspf_2*sqrt(DP_2/rho_2)
                               + 1f/(K_m + 1f)*Cd_1/cspf_1*sqrt(DP_1/rho_1)
                            );

        // float z0_inj1 = L_nz2 - mixing_length;
        assert(mixing_length <= L_nz2);
        float Dr_nz = Ir_nz2 - Ir_nz1;
        float z0_inj1 = mixing_length + Dr_nz/tan(twoalpha_1/2f);


        // Requires some turbulence.
        float Re_il1 = 2f*mdot_1/PI/sqrt(no_il1)/Ir_il1/mu_1;
        float Re_il2 = 2f*mdot_2/PI/sqrt(no_il2)/Ir_il2/mu_2;
        // assert(Re_il1 > 10e3f);
        // assert(Re_il2 > 10e3f);
        // nm we dont meet it :)

        /* FINAL ACTION: */
        // - set all 1&2 points A-F.
        // - set inlet 1&2 D&L.

        F2 = 1e3f * new Vec2(0f, Ir_nz2);
        E2 = F2 + 1e3f * new Vec2(L_nz2, 0f);
        float Dr_E2D2 = Ir_ch2 - Ir_nz2;
        D2 = E2 + 1e3f * new Vec2(Dr_E2D2/tan(phi), Dr_E2D2);
        B2 = E2 + 1e3f * new Vec2(L_ch2, Dr_E2D2);
        C2 = B2 + 1e3f * new Vec2(-Ir_il2, 0f);
        float Dr_B2A2 = 0f - Ir_ch2;
        A2 = B2 + 1e3f * new Vec2(Dr_B2A2/tan(-phi), Dr_B2A2);

        F1 = 1e3f * new Vec2(z0_inj1, Ir_nz1);
        E1 = F1 + 1e3f * new Vec2(L_nz1, 0f);
        float Dr_E1D1 = Ir_ch1 - Ir_nz1;
        D1 = E1 + 1e3f * new Vec2(Dr_E1D1/tan(phi), Dr_E1D1);
        B1 = E1 + 1e3f * new Vec2(L_ch1, Dr_E1D1);
        C1 = B1 + 1e3f * new Vec2(-Ir_il1, 0f);
        float Dr_B1A1 = 0f - Ir_ch1;
        A1 = B1 + 1e3f * new Vec2(Dr_B1A1/tan(-phi), Dr_B1A1);

        assert(A1.X > B1.X, $"A1z={A1.X}, B1z={B1.X}");
        assert(B1.X > C1.X, $"B1z={B1.X}, C1z={C1.X}");
        assert(C1.X > D1.X, $"C1z={C1.X}, D1z={D1.X}");
        assert(D1.X > E1.X, $"D1z={D1.X}, E1z={E1.X}");
        assert(E1.X > F1.X, $"E1z={E1.X}, F1z={F1.X}");
        assert(F1.X > 0f, $"F1z={F1.X}");

        assert(A2.X > B2.X, $"A2z={A2.X}, B2z={B2.X}");
        assert(B2.X > C2.X, $"B2z={B2.X}, C2z={C2.X}");
        assert(C2.X > D2.X, $"C2z={C2.X}, D2z={D2.X}");
        assert(D2.X > E2.X, $"D2z={D2.X}, E2z={E2.X}");
        assert(E2.X > F2.X, $"E2z={E2.X}, F2z={F2.X}");
        assert(nearzero(F2.X), $"F2z={F2.X}");

        // Now snap inj2 upper section to integrate with dividing manifold wall.

        A2 = D1;
        B2 = Polygon.line_intersection(
            D1, E1,
            D2, C2
        );

        A2.X -= th_nz1/sin(phi);
        B2.X -= th_nz1/sin(phi);
        this.z0_dmw = Polygon.line_intersection(
            B2, A2,
            ZERO2, uX2
        ).X;

        // perfectly placed.
        C2.X = Polygon.corner_thicken(D2, B2, A2, 1e3f*Ir_il2).X;

        this.D_il1 = 1e3f * 2f*Ir_il1;
        this.D_il2 = 1e3f * 2f*Ir_il2;
        this.L_il1 = 1e3f * L_il1;
        this.L_il2 = 1e3f * L_il2;


        // Calculate furthest radial point.

        Vec3 inlet1 = fromzr(C1, 0f) - D_il1/2f*uX3;
        Frame frame1 = Frame.cyl_circum(inlet1); // x=+axial, y=+radial.
        Vec3 furthest1 = frame1 * new Vec3(0f, D_il1/2f + th_il1, this.L_il1);
        Vec3 inlet2 = fromzr(C2, 0f) - D_il2/2f*uX3;
        Frame frame2 = Frame.cyl_circum(inlet2);
        Vec3 furthest2 = frame2 * new Vec3(0f, D_il2/2f + th_il2, this.L_il2);
        this.max_r = max(magxy(furthest1), magxy(furthest2));


        // Calculate furthest axial point.
        max_z = max(A1.X + th_inj1, B2.X);


        inited = true;


        // cheeky report.
        File.WriteAllLines(REPORT_PATH, [
            $"Injector Element Report",
            $"=======================",
            $"",
            $"Empirical corrections:",
            $"  - LOX correction factor: {K_mdot_1}",
            $"  - IPA correction factor: {K_mdot_2}",
            $"  - Additional correction factor: {K_mdot_extra}",
            $"",
            $"Stage 1 (LOx):",
            $"  - Pressure difference: {DP_1*1e-5} bar",
            $"  - Mass flow rate: {mdot_1} kg/s",
            $"  - Nozzle inner radius: {Ir_nz1*1e3f} mm",
            $"  - Nozzle length: {L_nz1*1e3f} mm",
            $"  - Chamber inner radius: {Ir_ch1*1e3f} mm",
            $"  - Chamber length: {L_ch1*1e3f} mm",
            $"  - Inlet count: {no_il1}",
            $"  - Inlet radius: {Ir_il1*1e3f} mm",
            $"  - Inlet Reynolds number: {Re_il1}",
            $"  - A_1: {A_1}",
            $"  - Cd_1: {Cd_1}",
            $"  - rmbar_1: {rmbar_1}",
            $"  - cspf_1: {cspf_1}",
            $"",
            $"Stage 2 (IPA):",
            $"  - Pressure difference: {DP_2*1e-5} bar",
            $"  - Mass flow rate: {mdot_2} kg/s",
            $"  - Nozzle inner radius: {Ir_nz2*1e3f} mm",
            $"  - Nozzle length: {L_nz2*1e3f} mm",
            $"  - Chamber inner radius: {Ir_ch2*1e3f} mm",
            $"  - Chamber length: {L_ch2*1e3f} mm",
            $"  - Inlet count: {no_il2}",
            $"  - Inlet radius: {Ir_il2*1e3f} mm",
            $"  - Inlet Reynolds number: {Re_il2}",
            $"  - A_2: {A_2}",
            $"  - Cd_2: {Cd_2}",
            $"  - rmbar_2: {rmbar_2}",
            $"  - cspf_2: {cspf_2}",
            $"",
            $"Interactions:",
            $"  - Mixing length: {mixing_length*1e3f} mm",
            $"  - Annulus inner radius: {Ir_nz1*1e3f + th_nz1} mm",
            $"  - Annulus outer radius: {Ir_nz2*1e3f} mm",
            $"  - Annulus radial gap: {(Ir_nz2 - Ir_nz1)*1e3f - th_nz1} mm",
            $"  - Inner injector axial offset: {z0_inj1*1e3f} mm",
        ]);
    }


    public void voxels(Frame at, out Voxels pos, out Voxels neg) {
        assert(inited);

        // Make volume by revolve.

        // general fillet divisions.
        int M = (int)(5 / VOXEL_SIZE);
        M = max(4, M);
        M -= M % 2; // force even.


        // Make injector 1.
        assert(nearzero(A1.Y));
        List<Vec2> neg_points1 = [
            A1,
            B1,
            D1,
            E1,
            F1,
            // Extend outer inj to plate + extra.
            new(-EXTRA - (printable ? extend_base_by : 0f), F1.Y),
            new(-EXTRA - (printable ? extend_base_by : 0f), 0f),
        ];
        Polygon.cull_adjacent_duplicates(neg_points1);
        List<Vec2> pos_points1 = [
            Polygon.corner_thicken(flipy(B1), A1, B1, th_inj1),
            Polygon.corner_thicken(A1, B1, D1, th_inj1),
            Polygon.line_intersection(
                A2, B2,
                B1 + th_inj1*uY2, D1 + th_inj1*uY2
            ),
            Polygon.line_intersection(
                A2, B2,
                E1 + th_nz1*uY2, E1 + th_nz1*uY2 + uX2
            ),
            Polygon.corner_thicken(D1, E1, F1, th_nz1),
            F1 + th_nz1*uY2,
            F1*uX2,
        ];
        Polygon.cull_adjacent_duplicates(pos_points1);
        Polygon.fillet(pos_points1, 1, th_inj1, divisions: M); // B
        pos_points1.Insert(0, flipy(pos_points1[1])); // for fillet purposes.
        Polygon.fillet(pos_points1, 1, th_inj1, divisions: M); // A
        pos_points1 = pos_points1[(1 + M/2)..]; // trim points.
        if (!nearzero(pos_points1[0].Y))
            pos_points1.Insert(0, pos_points1[0]*uX2);


        // Make injector 2.
        assert(!nearzero(A2.Y));
        assert(nearzero(F2.X));
        List<Vec2> neg_points2 = [
            A2,
            B2,
            D2,
            E2,
            F2,
            new(-EXTRA - (printable ? extend_base_by : 0f), F2.Y),
            new(-EXTRA - (printable ? extend_base_by : 0f), 0f),
        ];
        neg_points2.Insert(0, neg_points2[0]*uX2);
        Polygon.cull_adjacent_duplicates(neg_points2);
        List<Vec2> pos_points2 = [
            Polygon.corner_thicken(A2*uX2, A2, B2, th_inj2),
            Polygon.corner_thicken(A2, B2, D2, th_inj2),
            new(0f, D2.Y + th_inj2),
            ZERO2,
        ];
        pos_points2.Insert(0, pos_points2[0]*uX2);


        Voxels neg1 = new(Polygon.mesh_revolved(at, neg_points1));
        Voxels pos1 = new(Polygon.mesh_revolved(at, pos_points1));
        Voxels neg2 = new(Polygon.mesh_revolved(at, neg_points2));
        Voxels pos2 = new(Polygon.mesh_revolved(at, pos_points2));

        // dont let it delete inner injector.
        neg2.BoolSubtract(pos1);


        // Send to returns.
        pos = pos1; // no copy.
        pos.BoolAdd(pos2);
        neg = neg1; // no copy.
        neg.BoolAdd(neg2);


        // Add in cone divider and plate (for fillet purposes).
        pos.BoolAdd(Cone.phied(
            at.transz(z0_dmw),
            phi,
            r1: B2.Y + th_inj2 + FR_LOx + th_dmw/cos(phi) + 1f
        ).shelled(-th_dmw));
        pos.BoolAdd(new Rod(
            at,
            th_plate,
            D2.Y + th_inj2 + FR_IPA + 1f
        ));

        // Do big fillets (before inlets and pos/neg collapse)
        Voxels mask_LOx = Cone.phied(
            at.transz(z0_dmw + th_dmw/sin(phi)/2f),
            phi,
            r1: max(
                B2.Y + th_inj2 + FR_LOx + th_dmw/cos(phi) + 1f,
                1f/tan(phi) * (
                    A1.X - z0_dmw + th_dmw/sin(phi)/2f + th_inj1 + 1f
                )
            )
        );
        Voxels mask_IPA = new Rod(
            at,
            max_z + FR_IPA,
            D2.Y + th_inj2 + FR_IPA + 1f
        ).extended(1f, Extend.UPDOWN);
        mask_IPA.BoolSubtract(mask_LOx);

        mask_LOx.BoolIntersect(Fillet.concave(pos, FR_LOx));
        mask_IPA.BoolIntersect(Fillet.concave(pos, FR_IPA));
        pos.BoolAdd(mask_LOx);
        pos.BoolAdd(mask_IPA);



        // Add inlets.
        voxels_il(at, no_il1, C1, D_il1, L_il1, th_il1, phi, out Voxels pos_il1,
            out Voxels neg_il1);
        voxels_il(at, no_il2, C2, D_il2, L_il2, th_il2, phi, out Voxels pos_il2,
            out Voxels neg_il2);

        pos.BoolAdd(pos_il1);
        pos.BoolAdd(pos_il2);
        neg.BoolAdd(neg_il1);
        neg.BoolAdd(neg_il2);

        // Dreaded fillet.
        Fillet.concave(pos, FR_small, true);
        Fillet.concave(neg, FR_small, true);


        // dont let the inner nozzle end get filleted.
        Rod just_the_tip = new(
            at.transz(F1.X),
            1.4f*FR_small,
            F1.Y,
            F1.Y + th_nz1
        );
        if (printable) {
            // Make the tip a little bigger..
            assert(nonnan(extend_base_by));
            just_the_tip = just_the_tip.extended(F1.X + extend_base_by,
                    Extend.DOWN);
        }
        pos.BoolAdd(just_the_tip);
        neg.BoolSubtract(just_the_tip);

        // Add nozzle chamfers.
        if (!printable) {
            neg.BoolAdd(new Cone(
                at.transz(F1.X),
                CR_nz1,
                F1.Y + CR_nz1,
                F1.Y
            ).lengthed(CR_nz1, CR_nz1));
            neg.BoolAdd(new Cone(
                at.transz(F2.X),
                CR_nz2,
                F2.Y + CR_nz2,
                F2.Y
            ).lengthed(CR_nz2, CR_nz2));
        }
    }


    private static void voxels_il(Frame at, int no, Vec2 C, float D, float L,
            float th, float phi, out Voxels pos, out Voxels neg) {
        pos = new();
        neg = new();
        // Scale D to account for teardrop shape
        // https://www.desmos.com/calculator/2ftycddpdw
        D /= sqrt(0.75f + 1f/PI);
        // Add the tangential inlets.
        for (int i=0; i<no; ++i) {
            float theta = i*TWOPI/no;
            Vec3 inlet = fromzr(C, theta);
            // move radially inwards to make tangent outer.
            inlet -= D/2f * fromcyl(1f, theta, 0f);
            // make frame circum to this element centre.
            Frame frame = new Frame.Cyl(at).circum(inlet);
            // x=+axial, y=+radial.

            // Main pipe.
            neg.BoolAdd(new Rod(
                frame,
                L,
                D/2f
            ));
            // Tear drop sharp top edge.
            neg.BoolAdd(new Bar(
                frame.rotxy(-PI_4),
                L,
                D/2f
            ).at_edge(Bar.X1_Y1));
            // Rounded extra at the end.
            neg.BoolAdd(new Ball(frame.transz(L), D/2f));

            // Main pipe.
            Voxels this_pos = new Rod(
                frame,
                L,
                D/2f + th
            );
            // Tear drop top.
            this_pos.BoolAdd(new Bar(
                frame.rotxy(-PI_4),
                L,
                D/2f + th
            ).at_edge(Bar.X1_Y1));
            // round top by clearing square then adding pipe :)
            this_pos.BoolSubtract(new Bar(
                frame.transx(SQRT2*(D/2f + th)).rotxy(-PI_4),
                L,
                2f*th
            ).extended(3f*VOXEL_SIZE, Extend.UPDOWN));
            this_pos.BoolAdd(new Rod(
                frame.transx(SQRT2*D/2f),
                L,
                th
            ));

            // support.
            Bar tear = new Bar(
                frame.rotxy(-PI_4),
                L,
                D/2f + th
            ).at_edge(Bar.X0_Y0);
            Bar web = new Bar(
                frame,
                L/sin(phi), // axial
                th,         // radial
                L           // circum
            ).at_face(Bar.X0);
            this_pos.BoolAdd(tear);
            this_pos.BoolAdd(web);
            this_pos.IntersectImplicit(new Space(
                frame.translate(new Vec3(-tear.Ly*SQRT2 + web.Ly/2f, 0f, L))
                     .rotzx(-phi),
                -INF,
                0f
            ));
            this_pos.IntersectImplicit(new Space(new(), 0f, +INF));

            pos.BoolAdd(this_pos);
        }
    }



    public static class GraphLookup {
        /* https://doi.org/10.2514/5.9781600866760.0019.0103 */


        // Spray cone angle (Fig. 34a, transposed + torad).
        //             x: twoalpha
        //             y: A
        //  sampled over: Lbar_n
        private static readonly float A_tbl0_Lbar_nz = 0.5f;
        private static readonly Vec2[] A_tbl0 = {
            new(0.9876909f, 0.456541f), new(1.1373782f, 0.695346f),
            new(1.2269452f, 0.870938f), new(1.3079234f, 1.046533f),
            new(1.4060790f, 1.313432f), new(1.4796957f, 1.573309f),
            new(1.5422698f, 1.833188f), new(1.5987094f, 2.128183f),
            new(1.6612836f, 2.549605f), new(1.7017728f, 2.914838f),
            new(1.7508497f, 3.427569f), new(1.7901127f, 3.898157f),
            new(1.8183329f, 4.319579f), new(1.8465514f, 4.783144f),
            new(1.8710907f, 5.260755f), new(1.8993109f, 5.808605f),
            new(1.9287581f, 6.525022f), new(1.9496148f, 7.220371f),
            new(1.9557496f, 7.676910f), new(1.9643384f, 8.091308f)
        };
        private static readonly float A_tbl1_Lbar_nz = 2.0f;
        private static readonly Vec2[] A_tbl1 = {
            new(0.9791023f, 0.421421f), new(1.1643709f, 0.927129f),
            new(1.2846114f, 1.299385f), new(1.3643630f, 1.636524f),
            new(1.4441142f, 2.064969f), new(1.5226387f, 2.661984f),
            new(1.5668088f, 3.167691f), new(1.6146596f, 3.834943f),
            new(1.6477872f, 4.326602f), new(1.6870494f, 5.007903f),
            new(1.7152691f, 5.661108f), new(1.7385811f, 6.356455f),
            new(1.7594385f, 7.044778f), new(1.7692542f, 7.585603f),
            new(1.7766160f, 8.091308f)
        };
        public static float get_A(float twoalpha, float Lbar_nz) {
            Vec2 p0 = new(A_tbl0_Lbar_nz, lookup(A_tbl0, twoalpha));
            Vec2 p1 = new(A_tbl1_Lbar_nz, lookup(A_tbl1, twoalpha));
            return sample(p0, p1, Lbar_nz, false);
        }


        // Flow coefficient (Fig. 34b).
        //             x: A
        //             y: Cd
        //  sampled over: Rbar_ch
        private static readonly float Cd_tbl0_Rbar_ch = 1.0f;
        private static readonly Vec2[] Cd_tbl0 = {
            new(0.827858f, 0.400318f), new(0.947264f, 0.373270f),
            new(1.026863f, 0.353858f), new(1.146268f, 0.331901f),
            new(1.305473f, 0.309626f), new(1.416915f, 0.291169f),
            new(1.631838f, 0.268894f), new(1.870646f, 0.243755f),
            new(2.157212f, 0.220525f), new(2.499500f, 0.198886f),
            new(2.857709f, 0.179157f), new(3.367164f, 0.157518f),
            new(3.804973f, 0.141289f), new(4.378109f, 0.126333f),
            new(4.959204f, 0.116150f), new(5.484577f, 0.107239f),
            new(6.073631f, 0.098966f), new(6.726366f, 0.091329f),
            new(7.299502f, 0.085601f), new(7.689552f, 0.081782f),
            new(8.000000f, 0.079236f)
        };
        private static readonly float Cd_tbl1_Rbar_ch = 4.0f;
        private static readonly Vec2[] Cd_tbl1 = {
            new(1.082585f, 0.399364f), new(1.194027f, 0.377725f),
            new(1.313432f, 0.354177f), new(1.424875f, 0.336993f),
            new(1.560198f, 0.321400f), new(1.703481f, 0.305807f),
            new(1.878605f, 0.287669f), new(2.061691f, 0.269531f),
            new(2.300495f, 0.251710f), new(2.483581f, 0.236436f),
            new(2.722386f, 0.222116f), new(3.000992f, 0.206205f),
            new(3.287561f, 0.194431f), new(3.653730f, 0.182657f),
            new(4.083580f, 0.172474f), new(4.473629f, 0.162928f),
            new(5.054724f, 0.152426f), new(5.556216f, 0.143834f),
            new(6.145271f, 0.135879f), new(6.766166f, 0.127924f),
            new(7.379101f, 0.121559f), new(8.031838f, 0.115195f)
        };
        public static float get_Cd(float A, float Rbar_ch) {
            Vec2 p0 = new(Cd_tbl0_Rbar_ch, lookup(Cd_tbl0, A));
            Vec2 p1 = new(Cd_tbl1_Rbar_ch, lookup(Cd_tbl1, A));
            return sample(p0, p1, Rbar_ch, false);
        }


        // Geometric characteristic parameter (Fig. 34b, transposed).
        //             x: Cd
        //             y: A
        //  sampled over: Rbar_ch
        private static readonly float A_from_Cd_tbl0_Rbar_ch = 1.0f;
        private static readonly Vec2[] A_from_Cd_tbl0 = {
            new(0.079236f, 8.000000f), new(0.081782f, 7.689552f),
            new(0.085601f, 7.299502f), new(0.091329f, 6.726366f),
            new(0.098966f, 6.073631f), new(0.107239f, 5.484577f),
            new(0.116150f, 4.959204f), new(0.126333f, 4.378109f),
            new(0.141289f, 3.804973f), new(0.157518f, 3.367164f),
            new(0.179157f, 2.857709f), new(0.198886f, 2.499500f),
            new(0.220525f, 2.157212f), new(0.243755f, 1.870646f),
            new(0.268894f, 1.631838f), new(0.291169f, 1.416915f),
            new(0.309626f, 1.305473f), new(0.331901f, 1.146268f),
            new(0.353858f, 1.026863f), new(0.373270f, 0.947264f),
            new(0.400318f, 0.827858f)
        };
        private static readonly float A_from_Cd_tbl1_Rbar_ch = 4.0f;
        private static readonly Vec2[] A_from_Cd_tbl1 = {
            new(0.115195f, 8.031838f), new(0.121559f, 7.379101f),
            new(0.127924f, 6.766166f), new(0.135879f, 6.145271f),
            new(0.143834f, 5.556216f), new(0.152426f, 5.054724f),
            new(0.162928f, 4.473629f), new(0.172474f, 4.083580f),
            new(0.182657f, 3.653730f), new(0.194431f, 3.287561f),
            new(0.206205f, 3.000992f), new(0.222116f, 2.722386f),
            new(0.236436f, 2.483581f), new(0.251710f, 2.300495f),
            new(0.269531f, 2.061691f), new(0.287669f, 1.878605f),
            new(0.305807f, 1.703481f), new(0.321400f, 1.560198f),
            new(0.336993f, 1.424875f), new(0.354177f, 1.313432f),
            new(0.377725f, 1.194027f), new(0.399364f, 1.082585f)
        };
        public static float get_A_from_Cd(float Cd, float Rbar_ch) {
            Vec2 p0 = new(A_from_Cd_tbl0_Rbar_ch, lookup(A_from_Cd_tbl0, Cd));
            Vec2 p1 = new(A_from_Cd_tbl1_Rbar_ch, lookup(A_from_Cd_tbl1, Cd));
            return sample(p0, p1, Rbar_ch, false);
        }


        // Relative liquid vortex radius (Fig. 34b, transposed).
        //             x: A
        //             y: rmbar
        //  sampled over: Rbar_ch
        private static readonly float rmbar_tbl0_Rbar_ch = 1.0f;
        private static readonly Vec2[] rmbar_tbl0 = {
            new(0.523076f, 0.300632f), new(0.676923f, 0.356258f),
            new(0.953845f, 0.413780f), new(1.212306f, 0.459924f),
            new(1.606152f, 0.519975f), new(1.901537f, 0.557901f),
            new(2.276921f, 0.592667f), new(2.719998f, 0.623641f),
            new(3.076923f, 0.645133f), new(3.612306f, 0.672313f),
            new(4.030769f, 0.686220f), new(4.670769f, 0.710240f),
            new(5.230768f, 0.726675f), new(5.846152f, 0.744374f),
            new(6.283075f, 0.753856f), new(6.769228f, 0.763338f),
            new(7.347693f, 0.772819f), new(8.000000f, 0.781037f)
        };
        private static readonly float rmbar_tbl1_Rbar_ch = 4.0f;
        private static readonly Vec2[] rmbar_tbl1 = {
            new(1.224614f, 0.299368f), new(1.464615f, 0.341719f),
            new(1.692306f, 0.368268f), new(1.993845f, 0.397977f),
            new(2.313845f, 0.421365f), new(2.646153f, 0.440961f),
            new(2.984613f, 0.459924f), new(3.415383f, 0.478255f),
            new(3.821536f, 0.495954f), new(4.301537f, 0.513021f),
            new(4.769228f, 0.528824f), new(5.310767f, 0.543363f),
            new(5.766152f, 0.554741f), new(6.289230f, 0.567383f),
            new(6.873845f, 0.578129f), new(7.458459f, 0.590139f),
            new(8.067689f, 0.600885f)
        };
        public static float get_rmbar(float A, float Rbar_ch) {
            // Plot shows midpoint between Rbar_ch=1 and Rbar_ch=4 curves is at
            // Rbar_ch~=3 (instead of 2). so, dont use a linear interpolation
            // between curves sampled at different Rbar_ch.
            // https://www.desmos.com/calculator/avfdhjpouz
            assert(nearto(rmbar_tbl0_Rbar_ch, 1.0f));
            assert(nearto(rmbar_tbl1_Rbar_ch, 4.0f));
            assert(within(Rbar_ch, 1.0f, 4.0f));
            float t = 1/3f * (pow(4f, (Rbar_ch - 1f)/3f) - 1f);
            float rmbar_at1 = lookup(rmbar_tbl0, A);
            float rmbar_at4 = lookup(rmbar_tbl1, A);
            assert(rmbar_at1 > rmbar_at4); // vague bounds check.
            float rmbar = lerp(rmbar_at1, rmbar_at4, t);
            assert(rmbar > 0f);
            return rmbar;
        }



        private static float lookup(Vec2[] points, float x) {
            assert(numel(points) > 0);
            if (numel(points) == 1)
                return points[0].Y;

            int lo = 0;
            int hi = numel(points) - 1;
            // assert(within(x, points[lo].X, points[hi].X));
            // ^ allow extrapolation.

            int idx = -1;
            while (lo <= hi) {
                int mid = (lo + hi)/2;
                if (points[mid].X < x) {
                    idx = mid;
                    lo = mid + 1;
                } else {
                    hi = mid - 1;
                }
            }
            idx = clamp(idx, 0, numel(points) - 2);
            return sample(points[idx], points[idx + 1], x);
        }

        private static float sample(Vec2 left, Vec2 right, float x,
                bool extend=true) {
            assert(right.X > left.X);
            float t = invlerp(left.X, right.X, x);
            if (!extend)
                assert(within(t, 0f, 1f));
            return lerp(left.Y, right.Y, t);
        }
    }
}





public class Injector : TPIAP.Pea {

    public required PartMating pm { get; init; }

    public float Ir_chnl => pm.Mr_chnl - 0.5f*pm.max_th_chnl;
    public float Or_chnl => pm.Mr_chnl + 0.5f*pm.max_th_chnl;

    public required int[] no_injg { get; init; }
    public required float[] r_injg { get; init; }
    public required float[] theta0_injg { get; init; }
    public List<Vec2> points_inj = [];
    protected void initialise_inj() {
        points_inj = Polygon.circle(no_injg, r_injg, theta0_injg);
    }

    public required float D_fc { get; init; }
    public required int[] no_fcg { get; init; }
    public required float[] r_fcg { get; init; }
    public required float[] theta0_fcg { get; init; }
    public List<Vec2> points_fc = [];
    protected void initialise_fc() {
        points_fc = Polygon.circle(no_fcg, r_fcg, theta0_fcg);
    }

    public required float th_plate { get; init; }

    public required float phi_mw { get; init; }
    public required float th_dmw { get; init; }
    public required float th_omw { get; init; }

    public required string boltsize_mount { get; init; }
    public required float r_mount { get; init; }
    public required float z_mount { get; init; }
    public float extra_on_mounts => printable_dmls ? 5f : 0f;
    public Tapping tap_mount => new(boltsize_mount, printable_dmls)
            { extra_length = 3f + extra_on_mounts };

    public required InjectorElement element { get; init; }
    protected void initialise_elements() {
        element.printable = printable_dmls;
        element.extend_base_by = extend_base_by;
        element.initialise();
    }

    public required int no_suprt { get; init; }
    public required float Lr_suprt { get; init; }
    public required float wi_suprt { get; init; }
    public required float FR_suprt { get; init; }

    public required string portsize_igniter { get; init; }
    public required string portsize_LOxinlet { get; init; }
    public required string portsize_LOxPT { get; init; }
    public required string portsize_IPAPT { get; init; }
    public required string portsize_CCPT { get; init; }
    public required float z_igniter { get; init; }
    public required float z_LOxinlet { get; init; }
    public required float z_LOxPT { get; init; }
    public required float z_IPAPT { get; init; }
    public required float z_CCPT { get; init; }
    public required float th_igniter { get; init; }
    public required float th_LOxinlet { get; init; }
    public required float th_LOxPT { get; init; }
    public required float th_IPAPT { get; init; }
    public required float th_CCPT { get; init; }
    public required float D_igniterh { get; init; }
    public required float D_LOxinleth { get; init; }
    public required float D_LOxPTh { get; init; }
    public required float D_IPAPTh { get; init; }
    public required float D_CCPTh { get; init; }
    public required float th_igniterh { get; init; }
    public required float th_LOxinleth { get; init; }
    public required float th_LOxPTh { get; init; }
    public required float th_IPAPTh { get; init; }
    public required float th_CCPTh { get; init; }
    public Tapping tap_igniter => new(portsize_igniter, printable_dmls);
    public Tapping tap_LOxinlet => new(portsize_LOxinlet, printable_dmls)
            { extra_length = 2f };
    public Tapping tap_LOxPT => new(portsize_LOxPT, printable_dmls)
            { extra_length = 2f };
    public Tapping tap_IPAPT => new(portsize_IPAPT, printable_dmls)
            { extra_length = 2f };
    public Tapping tap_CCPT => new(portsize_CCPT, printable_dmls)
            { extra_length = 2f };


    protected const float EXTRA = 6f;

    protected float extend_base_by => printable_dmls ? 5f : 0f;


    protected void voxels_plate(out Voxels pos, out Voxels? neg) {
        float phi_inner = torad(60f);
        List<Vec2> points = [
            new(-EXTRA - extend_base_by, 0f),
            new(-EXTRA - extend_base_by, Ir_chnl),
            new(th_plate, Ir_chnl),
            new(pm.Lz_Ioring + th_plate, pm.OR_Ioring + 0.5f),
            new(pm.Lz_Ioring + th_plate, pm.IR_Ioring - 0.5f),
            new(th_plate, pm.IR_Ioring - 1f - pm.Lz_Ioring*tan(phi_inner)),
            new(th_plate, 0f),
        ];

        Polygon.fillet(points, 5, 3f);
        Polygon.fillet(points, 4, 2f);
        Polygon.fillet(points, 3, 3f);
        Polygon.fillet(points, 2, 2f);

        pos = new(Polygon.mesh_revolved(
            new(),
            points
        ));

        // Film cooling holes (not included in metal printing).
        neg = null;
        if (!printable_dmls) {
            neg = new();
            foreach (Vec2 p in points_fc) {
                neg.BoolAdd(new Rod(
                    new(rejxy(p, 0f)),
                    th_plate + pm.Lz_Ioring,
                    D_fc/2f
                ).extended(2*VOXEL_SIZE, Extend.UP)
                 .extended(2f*EXTRA + extend_base_by, Extend.DOWN));
            }
        }
    }


    public class ManiVol {
        /* volume = A-B */

        // /\ cone, base encompassing ipa inlet channels.
        public required Cone A { get; init; }
        // \/ cone, tip on Z axis extending until intersection with A boundary.
        public required Cone B { get; init; }
        // (z,r) point of A,B intersection.
        public required Vec2 peak { get; init; }

        // Roof slope magnitude.
        public float phi => abs(A.outer_phi);
        // Roof thickness along normal.
        public required float th { get; init; }
        // Roof thickness along z.
        public float Lz => th/cos(phi);
        // Roof thickness along r.
        public float Lr => th/sin(phi);
        // Arbitrary roof lowest z point query. this kinda thing:
        //  /\/\
        // /    \
        public float z(float r)
            => (r > peak.Y)
             ? A.bbase.pos.Z + lerp(0f, A.Lz, invlerp(A.r0, A.r1, r))
             : B.bbase.pos.Z + lerp(0f, B.Lz, invlerp(B.r0, B.r1, r));

        // Voxel mask for some manifold volume, including:
        // - entire manifold roof
        // - LOx void
        // - dividing wall
        // - IPA void
        // Note this is shifted up by a voxel (more or less (is it more or
        // less?)).
        public required Voxels volume_entire { get; init; }
        // Voxel mask for some manifold volume, including:
        // - dividing wall
        // - IPA void
        // - lower manifold roof
        // Note this is shifted up by a voxel or so.
        public required Voxels volume_only_lower { get; init; }
    }

    protected Voxels voxels_manifold(Geez.Cycle key, out ManiVol vol) {
        using var __ = key.like();

        // Dividing wall cones offset height.
        float z0_dmw = element.z0_dmw;

        // For lox/ipa boundary:
        float Dz_dmw = th_dmw/sin(phi_mw);
        float Dz_omw = th_omw/sin(phi_mw);


        // Manifold peak.
        float max_r = Or_chnl + (th_plate + pm.max_th_chnl)*tan(phi_mw);
        // https://www.desmos.com/calculator/tusqawwtn5
        Vec2 peak = new( /* (z,r) */
            max_r/2f/tan(phi_mw) + z0_dmw/2f,
            max_r/2f - z0_dmw/2f*tan(phi_mw)
        );


        // Manifold bounding cones.
        Cone A = Cone.phied(new(), -phi_mw, peak.X, r0: max_r);
        Cone B = Cone.phied(new(z0_dmw*uZ3), phi_mw, peak.X - z0_dmw + EXTRA);


        // Sneak the enclosing volumes in here (to be modified in the dividing
        // wall creation loop).
        Voxels volume_entire = A.lengthed(EXTRA, 0f).transz(1.5f*VOXEL_SIZE);
        volume_entire.BoolSubtract(B.transz(1.5f*VOXEL_SIZE));
        Voxels volume_only_lower = volume_entire.voxDuplicate();


        // Make dividing lox-ipa boundary.
        Voxels pos = new();
        Voxels neg = new();
        List<Geez.Key> keys = new(numel(points_inj));
        foreach (Vec2 p in points_inj) {
            Frame at = new(rejxy(p, z0_dmw));
            Mesh this_pos = Cone.phied(at, phi_mw, peak.X);
            Cone this_neg = Cone.phied(at.transz(Dz_dmw), phi_mw, peak.X);
            keys.Add(Geez.mesh(this_pos));
            pos.BoolAdd(new(this_pos));
            neg.BoolAdd((Voxels)this_neg);
            volume_only_lower.BoolSubtract(this_neg.transz(-1.5f*VOXEL_SIZE));
        }
        key <<= Geez.group(keys);

        // Create manifold volume object.
        vol = new ManiVol{
            A=A,
            B=B,
            peak=peak,
            th=th_omw,
            volume_entire=volume_entire,
            volume_only_lower=volume_only_lower,
        };


        // Make correct cone-walls.
        pos.BoolSubtract(neg);


        // Intersect with internal volume, w safety margin for pos (leaving half
        // of intersection with wall).
        pos.BoolIntersect(vol.A.transz(vol.Lz/2f));
        pos.BoolSubtract(vol.B.transz(vol.Lz/2f));
        key.voxels(pos);


        // Make big roof.
        pos.BoolAdd(vol.A.lengthed(0f, vol.Lz + 0.5f).shelled(+vol.th));
        pos.BoolAdd(vol.B.shelled(-vol.th));
        pos.BoolSubtract(vol.B.transz(vol.Lz));
        pos.BoolIntersect(vol.A.lengthed(vol.Lz + 0.5f, 0f).transz(vol.Lz));
        key.voxels(pos);

        return pos;
    }


    protected Voxels voxels_supports(Geez.Cycle key, in ManiVol vol) {
        using var __ = key.like();

        Voxels vox = new();

        float ave_r = ave(pm.IR_Ioring, pm.OR_Ioring);
        float min_r = ave_r - Lr_suprt/2f;
        float max_r = ave_r + Lr_suprt/2f;
        float min_z = th_plate;
        float max_z = vol.z(min_r);
        List<Vec3> points = Polygon.circle(no_suprt, ave_r, 0f, min_z);

        List<Geez.Key> keys = new(numel(points));
        foreach (Vec3 p in points) {
            List<Vec2> vertices = [
                // diamond.
                new(0f,           -wi_suprt/2f),
                new(-Lr_suprt/2f, 0f),
                new(0f,           +wi_suprt/2f),
                new(+Lr_suprt/2f, 0f),
            ];
            Polygon.fillet(vertices, 3, FR_suprt, prec: 2f);
            Polygon.fillet(vertices, 2, FR_suprt, prec: 2f);
            Polygon.fillet(vertices, 1, FR_suprt, prec: 2f);
            Polygon.fillet(vertices, 0, FR_suprt, prec: 2f);

            Mesh m = Polygon.mesh_extruded(
                Frame.cyl_axial(p), // x = +radial, y = +circumferential
                max_z - min_z,
                vertices,
                extend_by: 0.5f*th_plate,
                extend_dir: Extend.UPDOWN
            );
            keys.Add(Geez.mesh(m));
            vox.BoolAdd(new(m));
        }

        // Intersect with internal volume, w safety margin for pos (leaving half
        // of intersection with wall).
        vox.BoolIntersect(vol.A.transz(vol.Lz/2f));

        key.voxels(vox);
        Geez.remove(keys);

        return vox;
    }


    protected void voxels_igniter(Geez.Cycle key, out Voxels pos,
            out Voxels neg, out Voxels neg_no_tap) {

        // Fluid volume.
        neg = new Rod(
            new(th_plate*uZ3),
            z_igniter - th_plate,
            D_igniterh/2f
        ).extended(EXTRA, Extend.UP);
        neg.BoolAdd(new Rod(
            new(th_plate*uZ3),
            -th_plate,
            D_igniterh/2f - 1f // 1mm ledge for tube to sit on.
        ).extended(EXTRA, Extend.UPDOWN)
         .extended(extend_base_by, Extend.DOWN));

        neg_no_tap = neg.voxDuplicate();
        neg.BoolAdd(tap_igniter.at(new(z_igniter*uZ3)));

        // Filled pipe.
        pos = new Flats(tap_igniter, th_igniter)
                .at(new Frame(z_igniter*uZ3).rotxy(PI_4));
        pos.BoolAdd(new Rod(
            new(),
            z_igniter,
            D_igniterh/2f + th_igniterh
        ));
        key.voxels(pos);

        // Donut fillet at the base.
        float FR = 4f;
        pos.BoolAdd(new Rod(
            new(uZ3*th_plate),
            FR - VOXEL_SIZE,
            D_igniterh/2f + th_igniterh + FR - VOXEL_SIZE
        ).extended(EXTRA, Extend.DOWN));
        pos.BoolSubtract(new Donut(
            new(uZ3*(th_plate + FR)),
            D_igniterh/2f + th_igniterh + FR,
            FR
        ));
        key.voxels(pos);
    }


    protected Voxels voxels_flange(Geez.Cycle key, ManiVol mani_vol) {
        using var __ = key.like();

        Rod flange = new(
            new(),
            pm.flange_thickness_inj,
            pm.flange_outer_radius
        );

        Voxels vox = flange.extended(EXTRA + extend_base_by, Extend.DOWN);
        key.voxels(vox);

        for (int i=0; i<pm.no_bolt; ++i) {
            float theta = i*TWOPI/pm.no_bolt;
            vox.BoolAdd(new Rod(
                new(fromcyl(pm.r_bolt, theta, 0f)),
                flange.Lz,
                pm.D_bolt/2f + pm.thickness_around_bolt
            ).extended(EXTRA + extend_base_by, Extend.DOWN));
        }
        key.voxels(vox);

        // Flange extension into cone.
        Voxels extra = new Rod(new(), mani_vol.peak.X, pm.flange_outer_radius);
        extra.BoolIntersect(mani_vol.A.shelled(mani_vol.th/2f).positive
                                      .lengthed(2f*EXTRA + extend_base_by, 0f));
        vox.BoolAdd(extra);
        key.voxels(vox);

        // Clip correctly.
        vox.BoolSubtract(new Rod(
            new(),
            mani_vol.peak.X,
            Or_chnl
        ).extended(2f*EXTRA + extend_base_by, Extend.UPDOWN));
        key.voxels(vox);

        return vox;
    }


    protected void voxels_elements(Geez.Cycle key, out Voxels pos,
            out Voxels neg) {
        neg = new();
        pos = new();
        foreach (Vec2 p in points_inj) {
            element.voxels(new(rejxy(p)), out Voxels po, out Voxels ne);
            pos.BoolAdd(po);
            neg.BoolAdd(ne);
            key.voxels(neg);
        }
    }


    protected void voxels_bolts(out Voxels hole, out Voxels clearance) {
        hole = new();
        clearance = new();
        for (int i=0; i<pm.no_bolt; ++i) {
            float theta = i*TWOPI/pm.no_bolt;
            Frame frame = new(fromcyl(pm.r_bolt, theta, 0f));

            // for bolt.
            hole.BoolAdd(new Rod(
                frame,
                pm.flange_thickness_inj + EXTRA,
                pm.D_bolt/2f
            ).extended(2f*EXTRA + extend_base_by, Extend.DOWN));
            // for washer/nut.
            clearance.BoolAdd(new Rod(
                frame.transz(pm.flange_thickness_inj),
                z_mount - pm.flange_thickness_inj,
                pm.D_washer/2f
            ).extended(EXTRA, Extend.UP));
        }
    }


    protected Voxels? voxels_orings() {
        // No oring groove are printed, all are post machined.
        if (printable_dmls)
            return null;

        Voxels vox = new Rod(
            new(),
            pm.Lz_Ioring,
            pm.IR_Ioring,
            pm.OR_Ioring
        ).extended(EXTRA + extend_base_by, Extend.DOWN);
        vox.BoolAdd(new Rod(
            new(),
            pm.Lz_Ooring,
            pm.IR_Ooring,
            pm.OR_Ooring
        ).extended(EXTRA + extend_base_by, Extend.DOWN));
        return vox;
    }


    protected void voxels_gussets(Geez.Cycle key, in ManiVol mani_vol,
            out Voxels pos, out Voxels neg) {
        using var __ = key.like();

        pos = new();
        neg = new();

        float wi = pm.D_washer + 4f;
        float semiwi = wi/2f;
        float max_r = pm.r_bolt + pm.D_washer/4f;

        // Big blocks for each bolt.
        Slice<Vec2> points = Polygon.circle(pm.no_bolt, max_r);
        for (int i=0; i<numel(points); ++i) {
            Vec2 p = points[i];
            pos.BoolAdd(new Bar(
                Frame.cyl_axial(rejxy(p, 0f)), // x=+radial, y=+circum.
                mag(p),
                wi,
                mani_vol.peak.X + mani_vol.Lz
            ).at_face(Bar.X0)
             .extended(EXTRA, Extend.UPDOWN));
            key.voxels(pos);
        }

        // Trim down all to conical outer boundary.
        Cone volC = new Cone(
            new(pm.flange_thickness_inj*uZ3),
            mani_vol.peak.X + mani_vol.Lz - pm.flange_thickness_inj,
            max_r,
            mani_vol.peak.Y
        ).upto_tip()
         .lengthed(pm.flange_thickness_inj + 2f*EXTRA, 0f);
        pos.BoolIntersect(volC);
        key.voxels(pos);

        // Trim out middle.
        pos.BoolSubtract(new Rod(
            new(),
            mani_vol.peak.X + mani_vol.Lz,
            mani_vol.peak.Y
        ).extended(2f*EXTRA, Extend.UPDOWN));
        key.voxels(pos);


        // Add the thrust structure mounts.
        assert(numel(points)%2 == 0);
        for (int i=0; i<numel(points); ++i) {
            if (i%2 == 0) // only every second.
                continue;

            // vertical loft: rectangle -> circle

            Vec2 p = points[i] * (r_mount/mag(points[i]));

            // Point on volC at radius mag(p).
            float bot_z = lerp(
                dot(uZ3, volC.bbase * (0f*uZ3)),
                dot(uZ3, volC.bbase * (volC.Lz*uZ3)),
                invlerp(volC.r0, volC.r1, mag(p))
            );
            Frame bot = new(
                rejxy(p, bot_z),
                fromsph(1f, arg(p), volC.outer_phi + PI), // +~radial
                fromsph(1f, arg(p), volC.outer_phi + PI_2) // +up
            );
            Frame top = new(
                bot.pos + uZ3*(z_mount - bot.pos.Z),
                fromcyl(1f, arg(p), 0f),
                uZ3
            );

            // float strut_radius = wi/sqrt(PI)f;
            // ^ equal-area, but lets actually just do constant width.
            float strut_radius = semiwi;

            int N = 2*Polygon.full_res_divs(mag(top.pos.Z - bot.pos.Z));
            int M = Polygon.full_res_divs(TWOPI*strut_radius);
            M -= M % 4;

            List<Vec2> V0_rect = [
                new(+semiwi, +semiwi),
                new(-semiwi, +semiwi),
                new(-semiwi, -semiwi),
                new(+semiwi, -semiwi),
            ];
            V0_rect = Polygon.resample(V0_rect, M, true);

            List<Vec2> V0_circ = Polygon.circle(M, strut_radius, +PI_4);

            List<Vec2> vertices = new();
            List<Frame> frames = new();

            // Interp between square and circle, with continuous derivative also.
            for (int j=0; j<N; ++j) {
                float t = lerp(0f, 1f, j, N);
                // smooth https://www.desmos.com/calculator/hf7a0a7upp.
              #if false
                float n = 1.25f;
                float s = pow(t, n) / (pow(t, n) + pow(1f - t, n));
              #else
                float s = t*t*(3f - 2f*t);
              #endif

                Frame frame = bot.lerp(top, t);

                // Adjust for slant.
                Vec2 slant = new(1f/dot(frame.Z, uZ3), 1f);
                List<Vec2> V_rect = V0_rect.Select((v) => v*slant).ToList();
                List<Vec2> V_circ = V0_circ.Select((v) => v*slant).ToList();

                vertices.AddRange(Polygon.lerp(V_rect, V_circ, s));
                frames.Add(frame);
            }

            // Extend up.
            frames.Add(top.transz(EXTRA + extra_on_mounts));
            vertices.AddRange(vertices[(numel(vertices) - M)..]);

            // Extend in.
            frames.Insert(0, bot.transz(-EXTRA));
            vertices.InsertRange(0, vertices[..M]);

            Mesh m = Polygon.mesh_swept(new FramesSequence(frames), vertices);
            pos.BoolAdd(new(m));

            // Bolt hole + clean top.
            top = top.transz(extra_on_mounts);
            neg.BoolAdd(tap_mount.at(top));
            neg.BoolAdd(new Rod(top, EXTRA, 2f*strut_radius));

            key.voxels(pos);
        }

        // Trim underside.
        pos.BoolSubtract(mani_vol.volume_entire);
        pos.BoolSubtract(new Rod(
            new(),
            pm.flange_thickness_inj/2f,
            max_r + EXTRA
        ).extended(2f*EXTRA, Extend.DOWN));
        key.voxels(pos);
    }



    protected void voxels_ports(Geez.Cycle key, ManiVol mani_vol,
            out Voxels pos, out Voxels neg, out Voxels neg_no_tap) {

        // fucking c sharp cannot access out var from local function.
        Voxels _pos = new();
        Voxels _neg = new();
        Voxels _neg_no_tap = new();

        List<Geez.Key> keys = new();
        void portme(Tapping tap, Frame at, float th, float D_h, float th_h,
                in Voxels? sub_pos=null, in Voxels? sub_neg=null) {
            Flats flats = new Flats(tap, th);
            Voxels this_pos = flats.at(at);
            this_pos.BoolAdd(new Rod(at, -at.pos.Z, flats.r));
            this_pos.BoolSubtract(mani_vol.volume_entire);

            this_pos.BoolAdd(new Rod(
                at,
                -at.pos.Z - EXTRA - (sub_pos == null ? extend_base_by : 0f),
                D_h/2f + th_h
            ));
            Voxels this_neg_no_tap = new Rod(
                at,
                -at.pos.Z - EXTRA - (sub_neg == null ? extend_base_by : 0f),
                D_h/2f
            ).extended(EXTRA, Extend.UPDOWN);
            Voxels this_neg = this_neg_no_tap.voxDuplicate();
            this_neg.BoolAdd(tap.at(at));

            if (sub_pos != null)
                this_pos.BoolSubtract(sub_pos);
            if (sub_neg != null) {
                this_neg.BoolSubtract(sub_neg);
                this_neg_no_tap.BoolSubtract(sub_neg);
            }
            using (key.like())
                keys.Add(Geez.voxels(this_pos));
            _pos.BoolAdd(this_pos);
            _neg.BoolAdd(this_neg);
            _neg_no_tap.BoolAdd(this_neg_no_tap);
        }

        float r_LOxinlet = mani_vol.peak.Y * 1.1f;
        float r_LOxPT    = mani_vol.peak.Y;
        float r_IPAPT    = mani_vol.peak.Y;
        float r_CCPT     = mani_vol.peak.Y;

        Vec3 pos_LOxinlet = new(-r_LOxinlet, 0f, z_LOxinlet);
        Vec3 pos_LOxPT    = new(+r_LOxPT,    0f, z_LOxPT);
        Vec3 pos_IPAPT    = new(0f,    +r_IPAPT, z_IPAPT);
        Vec3 pos_CCPT     = new(0f,     -r_CCPT, z_CCPT);

        Frame at_LOxinlet = Frame.cyl_axial(pos_LOxinlet).rotxy(PI_2);
        Frame at_LOxPT    = Frame.cyl_axial(pos_LOxPT)   .rotxy(PI_2);
        Frame at_IPAPT    = Frame.cyl_axial(pos_IPAPT)   .rotxy(PI_2);
        Frame at_CCPT     = Frame.cyl_axial(pos_CCPT)    .rotxy(PI_2);
        // +z = +axial, +x = -circumferential.

        // Rough bounding for plate voxels.
        Voxels volume_plate = new Rod(
            new(),
            th_plate + pm.Lz_Ioring,
            mani_vol.A.outer_r0
        ).extended(EXTRA, Extend.DOWN);

        Voxels underneath = new Rod(new(), -3f*EXTRA, pm.flange_outer_radius);
        portme(tap_LOxinlet, at_LOxinlet, th_LOxinlet, D_LOxinleth, th_LOxinleth,
                sub_pos: underneath + mani_vol.volume_entire,
                sub_neg: underneath + mani_vol.volume_only_lower);
        portme(tap_LOxPT, at_LOxPT, th_LOxPT, D_LOxPTh, th_LOxPTh,
                sub_pos: underneath + mani_vol.volume_entire,
                sub_neg: underneath + mani_vol.volume_only_lower);
        portme(tap_IPAPT, at_IPAPT, th_IPAPT, D_IPAPTh, th_IPAPTh,
                sub_pos: underneath + mani_vol.volume_only_lower,
                sub_neg: underneath + volume_plate);
        portme(tap_CCPT, at_CCPT, th_CCPT, D_CCPTh, th_CCPTh);

        pos = _pos;
        neg = _neg;
        neg_no_tap = _neg_no_tap;

        key <<= Geez.group(keys);
    }



    public Voxels? voxels() {

        // Get overall bounding box to size screenshots.
        float overall_Lr = pm.r_bolt
                         + pm.D_bolt/2f
                         + pm.thickness_around_bolt;
        float overall_Lz = max(z_mount, z_igniter, z_LOxinlet, z_LOxPT, z_IPAPT,
                               z_CCPT) + EXTRA;
        float overall_Mz = overall_Lz/2f - EXTRA/2f;

        // Initialise the part manager.
        using PartMaker part = new(overall_Lz, overall_Lr, overall_Mz);
        if (!take_screenshots)
            part.screenshotta = null;


        // Create the part.

        Geez.Cycle key_plate = new(colour: COLOUR_CYAN);
        voxels_plate(out Voxels? plate, out Voxels? neg_film_cooling);
        key_plate.voxels(plate);
        part.step("created plate.");

        Geez.Cycle key_elements = new(colour: COLOUR_GREEN);
        Voxels? pos_elements;
        Voxels? neg_elements;
        if (elementless) {
            pos_elements = new();
            neg_elements = new();
            part.no_step("skipping injector elements (elementless requested)");
        } else {
            voxels_elements(key_elements, out pos_elements, out neg_elements);
            part.step("created injector elements");
        }

        Geez.Cycle key_manifold = new(colour: COLOUR_PINK);
        Voxels? manifold = voxels_manifold(key_manifold, out ManiVol mani_vol);
        part.substep("created manifold walls.");

        Geez.Cycle key_supports = new(colour: COLOUR_ORANGE);
        Voxels? supports = voxels_supports(key_supports, mani_vol);
        part.substep("created manifold supports.");

        part.step("created manifold.");

        Geez.Cycle key_igniter = new(colour: COLOUR_WHITE);
        voxels_igniter(key_igniter, out Voxels? pos_igniter,
                out Voxels? neg_igniter, out Voxels? neg_igniter_no_tap);
        part.step("created igniter port.");

        voxels_bolts(out Voxels? neg_bolt_hole, out Voxels? neg_bolt_clearance);
        part.substep("created bolts.");

        Voxels? neg_orings = voxels_orings();
        part.substep("created O-rings.");

        Geez.Cycle key_flange = new(colour: COLOUR_BLUE);
        Voxels? flange = voxels_flange(key_flange, mani_vol);
        part.step("created flange.");

        Geez.Cycle key_gussets = new(colour: COLOUR_YELLOW);
        voxels_gussets(key_gussets, mani_vol, out Voxels? gussets,
                out Voxels? neg_mounting);
        part.step("created gussets.");

        Geez.Cycle key_ports = new(colour: COLOUR_RED);
        voxels_ports(key_ports, mani_vol, out Voxels? pos_ports,
                out Voxels? neg_ports, out Voxels? neg_ports_no_tap);
        part.step("created ports.");

        part.add(ref flange, key_flange);
        part.substep("added flange.");
        part.add(ref gussets, key_gussets);
        part.substep("added gussets.");
        part.add(ref manifold, key_manifold, keepme: true);
        part.substep("added manifold walls (1/2).");

        part.step("added material.");

        part.sub(ref neg_bolt_clearance, keepme: true);
        part.substep("subtracted mating bolt clearance (1/2).");

        part.sub(ref neg_igniter_no_tap, keepme: true);
        part.substep("subtracted igniter hole (1/2).");

        part.sub(ref neg_ports_no_tap, keepme: true);
        part.substep("subtracted port holes (1/2).");

        // Want to fillet the igniter and the ports separately so they dont have
        // weird interactions with each other. It is a grim double fillet tho.
        Voxels? part_igniter = part.voxels.voxDuplicate();
        part_igniter.BoolAdd(pos_igniter);
        part.key.voxels(part_igniter);
        part.substep("branched and added igniter (1/2).");

        if (!filletless) {
            Fillet.both(part_igniter,
                concave_FR: pm.concave_fillet_radius,
                convex_FR: pm.convex_fillet_radius,
                inplace: true
            );
            part.substep("filleted branched part (1/2).", view_part: true);
        } else {
            part.substep("skipping branched part fillet (1/2) "
                       + "(filletless requested).");
        }

        part.add(ref pos_ports, key_ports, keepme: true);
        part.substep("branched and added ports (1/2).");

        if (!filletless) {
            Fillet.both(part.voxels,
                concave_FR: pm.concave_fillet_radius,
                convex_FR: pm.convex_fillet_radius,
                inplace: true
            );
            part.substep("filleted branched part (2/2).", view_part: true);
        } else {
            part.substep("skipping branched part fillet (2/2) "
                       + "(filletless requested).");
        }

        part.add(ref part_igniter);
        part.substep("combined both branches.");


        part.step("partial clean up");

        {
            Voxels? inside_mani = new Rod(new(), mani_vol.peak.X, Ir_chnl);
            inside_mani.BoolIntersect(mani_vol.volume_entire);

            part.sub(ref inside_mani);
            part.substep("cleared manifold.");
            part.add(ref manifold, key_manifold);
            part.substep("added manifold walls (2/2).");
            part.add(ref pos_igniter, key_igniter);
            part.substep("added igniter (2/2).");
            part.add(ref pos_ports, key_ports);
            part.substep("added ports (2/2).");
        }

        part.step("overhang clean up");


        part.add(ref plate, key_plate);
        part.substep("added base plate.");
        part.add(ref supports, key_supports);
        part.substep("added supports.");
        part.add(ref pos_elements);
        part.substep("added injector elements.");

        part.step("added plate material.");


        if (neg_film_cooling != null) {
            part.sub(ref neg_film_cooling);
            part.substep("subtracted film cooling holes.");
        } else {
            part.substep("skipping film cooling holes (not printed).");
        }
        part.sub(ref neg_bolt_hole);
        part.substep("subtracted mating bolt hole.");
        part.sub(ref neg_bolt_clearance);
        part.substep("subtracted mating bolt clearance (2/2).");
        part.sub(ref neg_mounting);
        part.substep("subtracted mounting bolts.");
        if (neg_orings != null) {
            part.sub(ref neg_orings);
            part.substep("subtracted O-rings.");
        } else {
            part.substep("skipping O-rings (not printed).");
        }
        part.sub(ref neg_igniter);
        part.substep("subtracted igniter void (2/2).");
        part.sub(ref neg_ports);
        part.substep("subtracted port voids (2/2).");
        part.sub(ref neg_elements, key_elements);
        part.substep("subtracted injector elements.");

        part.step("removed voids.");

        part.voxels.BoolSubtract(new Rod(
            new(-extend_base_by*uZ3),
            -3f*EXTRA,
            overall_Lr + EXTRA
        ));
        part.substep("clipped bottom.", view_part: true);

        part.step("finished.");

        return part.voxels;
    }


    public Voxels? cutaway(in Voxels part) {
        Voxels cutted = Sectioner.pie(0f, -1.5f*PI).cut(part);
        Geez.voxels(cutted);
        return cutted;
    }


    public void drawings(in Voxels part) {
        Geez.voxels(part);
        Bar bounds;

        Frame frame_xy = new(3f*VOXEL_SIZE*uZ3, uX3, uZ3);
        print("cross-sectioning xy...");
        Drawing.to_file(
            fromroot($"exports/injector_xy.svg"),
            part,
            frame_xy,
            out bounds
        );
        using (Geez.like(colour: COLOUR_BLUE))
            Geez.bar(bounds, divide_x: 3, divide_y: 3);

        Frame frame_yz = new(ZERO3, uY3, uX3);
        print("cross-sectioning yz...");
        Drawing.to_file(
            fromroot($"exports/injector_yz.svg"),
            part,
            frame_yz,
            out bounds
        );
        using (Geez.like(colour: COLOUR_GREEN))
            Geez.bar(bounds, divide_x: 3, divide_y: 4);

        print();
    }


    public void anything() {
        element.voxels(new(), out Voxels pos, out Voxels neg);
        pos.BoolSubtract(neg);
        Geez.voxels(pos);
    }


    public string name => "injector";


    public bool printable_dmls   = false;
    public bool minimise_mem     = false;
    public bool take_screenshots = false;
    public bool filletless       = false;
    public bool elementless      = false;
    public void set_modifiers(int mods) {
        printable_dmls   = popbits(ref mods, TPIAP.PRINTABLE_DMLS);
        minimise_mem     = popbits(ref mods, TPIAP.MINIMISE_MEM);
        take_screenshots = popbits(ref mods, TPIAP.TAKE_SCREENSHOTS);
        _                = popbits(ref mods, TPIAP.LOOKIN_FANCY);
        filletless       = popbits(ref mods, TPIAP.FILLETLESS);
        elementless      = popbits(ref mods, TPIAP.ELEMENTLESS);
        if (mods == 0)
            return;
        throw new Exception("yeah nah dunno what it is");
        throw new Exception("honestly couldnt tell you");
        throw new Exception("what is happening right now");
                          // im selling out
    }


    public void initialise() {
        initialise_inj();
        initialise_fc();
        initialise_elements();
    }
}
