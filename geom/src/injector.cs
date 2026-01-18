using static br.Br;
using br;
using TPIAP = TwoPeasInAPod;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using BBox3 = PicoGK.BBox3;


// X_chnl = cooling channel property.
// X_ch1 = inner injector element chamber property.
// X_ch2 = outer injector element chamber property.
// X_dmw = LOx/IPA dividing manifold wall property.
// X_fc = film cooling property.
// X_fcg = grouped film cooling property (each index = one group).
// X_il1 = inner injector element inlet property.
// X_il2 = outer injector element inlet property.
// X_inj = axial injector property.
// X_injg = grouped axial injector property (each index = one group).
// X_inj1 = inner axial injector property.
// X_inj2 = outer axial injector property.
// X_IPA = IPA manifold property.
// X_LOx = LOx manifold property.
// X_nz1 = inner axial injector nozzle property.
// X_nz2 = outer axial injector nozzle property.
// X_omw = outer manifold wall property.
// X_plate = base plate propoerty.


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

    public required float phi { get; init; }
    /* ^ must be same as LOx/OPA dividing cone */

    public required int no_il1 { get; init; }
    public required int no_il2 { get; init; }
    public required float th_inj1 { get; init; }
    public required float th_inj2 { get; init; }
    public required float th_nz1 { get; init; }
    public required float th_il1 { get; init; }
    public required float th_il2 { get; init; }
    public required float Pr_inj1 { get; init; }
    public required float Pr_inj2 { get; init; }
    public required float Fr { get; init; }
    public float D_il1 = NAN;
    public float D_il2 = NAN;
    public float L_il1 = NAN;
    public float L_il2 = NAN;

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

    private bool inited = false;
    public void initialise(in PartMating pm, int N, out float z0_dmw,
            out float max_r) {
        assert(!inited);
        assert(phi > 0f);
        assert(phi <= PI_2);
        assert(N >= 1);

        // Within this ALL LENGTHS ARE IN METRES. converted to mm at end.

        float DP_1 = (Pr_inj1 - 1f)*pm.P_cc;
        float DP_2 = (Pr_inj2 - 1f)*pm.P_cc;
        assert(DP_1 > 0f);
        assert(DP_2 > 0f);


        /* Arbitrary params: */

        // Reasonable bounds for inlet counts:
        assert(within(no_il1, 2, 6));
        assert(within(no_il2, 2, 6));

        // Set chamber 2 length to fuck off uge. its clipped at end of this
        // function.
        float Lbar_ch2 = 100f;

        // idk why these are separate variables (in the paper).
        float C_1 = Rbar_ch1;
        float C_2 = Rbar_ch2;


        /* injection fluid state: */

        float mdot_1 = pm.mdot_LOx / N;
        float mdot_2 = pm.mdot_IPA / N;

        // TODO: f(T) these bad boy, changes a lot w temp!
        float nu_1 = 2.56e-6f; // arithmetic mean of airborne ICD (114K)
        float nu_2 = 2.66e-6f;

        float rho_1 = pm.rho_LOx;
        float rho_2 = pm.rho_IPA;


        /* stage 1 (LOx): */

        float A_1 = GraphLookup.get_A(twoalpha_1, Lbar_nz1);
        float mu_il1 = GraphLookup.get_mu_il(A_1, C_1);
        float rmbar_1 = GraphLookup.get_rmbar(A_1, Rbar_ch1);

        float Ir_nz1 = 0.475f*sqrt(mdot_1/mu_il1/sqrt(rho_1*DP_1));
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
        float mu_il2 = NAN;
        float rmbar_2 = NAN;

        const int MAX_ITERS = 100;
        const float TOLERANCE = 0.001e-3f;
        float diff = +INF;
        for (int iter=0; iter<MAX_ITERS; ++iter) {

            // 1. Calculate current flow coefficient mu based on current Rn.
            mu_il2 = (mdot_1 + mdot_2)/PI/squared(Ir_nz2)/sqrt(2f*rho_2*DP_2);

            // 2. Find A based on mu (from Fig 34).
            A_2 = GraphLookup.get_A_from_mu_il(mu_il2, C_2);

            // 3. Find dimensionless relative vortex radius (from Fig 35).
            rmbar_2 = GraphLookup.get_rmbar(A_2, Rbar_ch2);
            // float fluid_inner_radius_2 = rmbar_2 * Ir_nz2;
            float fluid_inner_radius_2 = rmbar_2;

            // Leave if we're under tol (note we recalced 1.2.3.).
            if (diff < TOLERANCE)
                break;
            assert(iter != MAX_ITERS - 1);

            // 4. Calculate NEW physical nozzle radius.
            float next = min_fluid_inner_radius_2 / fluid_inner_radius_2;
            diff = abs(next - Ir_nz2);
            Ir_nz2 = next;

            // re-loop to re-calc mu/A/rmbar.
        }
        assert(Ir_nz2 >= min_fluid_inner_radius_2);

        float L_nz2 = 2f*Lbar_nz2*Ir_nz2;

        float Ir_ch2 = Rbar_ch2*Ir_nz2;
        float L_ch2 = Lbar_ch2*Ir_ch2;

        float Ir_il2 = sqrt(Ir_ch2*Ir_nz2/no_il2/A_2);
        float L_il2 = Ir_ch2 + 3f*Ir_il2;


        /* Inner nozzle vertical offset: */

        float K_m = mdot_1/mdot_2;
        float cspf_1 = 1f - squared(rmbar_1); // coefficient of stage passage
        float cspf_2 = 1f - squared(rmbar_2); //   fullness.
        float mixing_length = SQRT2 * tau_i
                            * (
                                K_m/(K_m + 1f)*mu_il2/cspf_2*sqrt(DP_2/rho_2)
                               + 1f/(K_m + 1f)*mu_il1/cspf_1*sqrt(DP_1/rho_1)
                            );

        // float z0_inj1 = L_nz2 - mixing_length;
        assert(mixing_length <= L_nz2);
        float Dr_nz = Ir_nz2 - Ir_nz1;
        float z0_inj1 = mixing_length + Dr_nz/tan(twoalpha_1/2f);


        // Requires some turbulence.
        float Re_il1 = 2f*mdot_1/PI/sqrt(no_il1)/Ir_il1/rho_1/nu_1;
        float Re_il2 = 2f*mdot_2/PI/sqrt(no_il2)/Ir_il2/rho_2/nu_2;
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

        // Now snap inj2 upper setcion to integrate with dividing manifold wall.

        A2 = D1;
        B2 = Polygon.line_intersection(
            D1, E1,
            D2, C2,
            out _
        );

        A2.X -= th_nz1/sin(phi);
        B2.X -= th_nz1/sin(phi);
        z0_dmw = Polygon.line_intersection(
            B2, A2,
            ZERO2, uX2,
            out _
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
        max_r = max(magxy(furthest1), magxy(furthest2));

        inited = true;


        // cheeky report.
        File.WriteAllLines(fromroot($"exports/injector_element_report.txt"), [
            $"Injector Element Report",
            $"=======================",
            $"",
            $"Element count: {N}",
            $"",
            $"Stage 1 (LOx):",
            $"  - Nozzle inner radius: {Ir_nz1*1e3f} mm",
            $"  - Nozzle length: {L_nz1*1e3f} mm",
            $"  - Chamber inner radius: {Ir_ch1*1e3f} mm",
            $"  - Chamber length: {L_ch1*1e3f} mm",
            $"  - Inlet radius: {Ir_il1*1e3f} mm",
            $"  - Inlet Reynolds number: {Re_il1}",
            $"  - Pressure difference: {DP_1*1e-5} bar",
            $"  - Mass flow rate: {mdot_1} kg/s",
            $"  - A_1: {A_1}",
            $"  - rmbar_1: {rmbar_1}",
            $"  - cspf_1: {cspf_1}",
            $"",
            $"Stage 2 (IPA):",
            $"  - Nozzle inner radius: {Ir_nz2*1e3f} mm",
            $"  - Nozzle length: {L_nz2*1e3f} mm",
            $"  - Chamber inner radius: {Ir_ch2*1e3f} mm",
            $"  - Chamber length: {L_ch2*1e3f} mm",
            $"  - Inlet radius: {Ir_il2*1e3f} mm",
            $"  - Inlet Reynolds number: {Re_il2}",
            $"  - Pressure difference: {DP_2*1e-5} bar",
            $"  - Mass flow rate: {mdot_2} kg/s",
            $"  - A_2: {A_2}",
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

        // divisions on revolves about z.
        int N = (int)(TWOPI * max(C1.X, C2.X) / VOXEL_SIZE);
        N /= 3;
        N = max(10, N);

        // general fillet divisions.
        int M = N/20;
        M -= M % 2; // force even.
        M = max(M, 4);


        // Make injector 1.
        assert(nearzero(A1.Y));
        List<Vec2> neg_points1 = [
            A1,
            B1,
            D1,
            E1,
            F1,
            // Extend outer inj to plate + extra.
            new(-EXTRA, F1.Y),
            new(-EXTRA, 0f),
        ];
        List<Vec2> pos_points1 = [
            Polygon.corner_thicken(flipy(B1), A1, B1, th_inj1),
            Polygon.corner_thicken(A1, B1, D1, th_inj1),
            Polygon.line_intersection(
                A2, B2,
                B1 + th_inj1*uY2, D1 + th_inj1*uY2,
                out _
            ),
            Polygon.line_intersection(
                A2, B2,
                E1 + th_nz1*uY2, E1 + th_nz1*uY2 + uX2,
                out _
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
            new(-EXTRA, F2.Y),
            new(-EXTRA, 0f),
        ];
        neg_points2.Insert(0, neg_points2[0]*uX2);
        List<Vec2> pos_points2 = [
            Polygon.corner_thicken(A2*uX2, A2, B2, th_inj2),
            Polygon.corner_thicken(A2, B2, D2, th_inj2),
            new(0f, D2.Y + th_inj2),
            ZERO2,
        ];
        pos_points2.Insert(0, pos_points2[0]*uX2);


        Voxels neg1 = new(Polygon.mesh_revolved(
            at,
            neg_points1,
            slicecount: N
        ));
        Voxels pos1 = new(Polygon.mesh_revolved(
            at,
            pos_points1,
            slicecount: N
        ));
        Voxels neg2 = new(Polygon.mesh_revolved(
            at,
            neg_points2,
            slicecount: N
        ));
        Voxels pos2 = new(Polygon.mesh_revolved(
            at,
            pos_points2,
            slicecount: N
        ));

        // dont let it delete inner injector.
        neg2.BoolSubtract(pos1);


        // Send to returns.
        pos = pos1; // no copy.
        pos.BoolAdd(pos2);
        neg = neg1; // no copy.
        neg.BoolAdd(neg2);


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
        Fillet.concave(pos, Fr, true);
        Fillet.both(neg, Fr, true);

        // dont let the inner nozzle end get filleted.
        Rod just_the_tip = new(at.transz(F1.X), 1.4f*Fr, F1.Y, F1.Y + th_nz1);
        pos.BoolAdd(just_the_tip);
        neg.BoolSubtract(just_the_tip);
    }


    private static void voxels_il(Frame at, int no, Vec2 C, float D, float L,
            float th, float phi, out Voxels pos, out Voxels neg) {
        pos = new();
        neg = new();
        // Add the tangential inlets.
        for (int i=0; i<no; ++i) {
            float theta = i*TWOPI/no;
            Vec3 inlet = fromzr(C, theta);
            // move radially inwards to make tangent outer.
            inlet -= D/2f * fromcyl(1f, theta, 0f);
            // make frame circum to this element centre.
            Frame frame = new Frame.Cyl(at).circum(inlet);
            // x=+axial, y=+radial.
            Rod pipe = new(frame, L, D/2f);
            neg.BoolAdd(pipe.extended(3*VOXEL_SIZE, Extend.UP));

            Voxels this_pos = pipe.shelled(th);

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
        //             y: mu_il
        //  sampled over: C
        private static readonly float mu_il_tbl0_C = 1.0f;
        private static readonly Vec2[] mu_il_tbl0 = {
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
        private static readonly float mu_il_tbl1_C = 4.0f;
        private static readonly Vec2[] mu_il_tbl1 = {
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
        public static float get_mu_il(float A, float C) {
            Vec2 p0 = new(mu_il_tbl0_C, lookup(mu_il_tbl0, A));
            Vec2 p1 = new(mu_il_tbl1_C, lookup(mu_il_tbl1, A));
            return sample(p0, p1, C, false);
        }


        // Geometric characteristic parameter (Fig. 34b, transposed).
        //             x: mu_il
        //             y: A
        //  sampled over: C
        private static readonly float A_from_mu_il_tbl0_C = 1.0f;
        private static readonly Vec2[] A_from_mu_il_tbl0 = {
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
        private static readonly float A_from_mu_il_tbl1_C = 4.0f;
        private static readonly Vec2[] A_from_mu_il_tbl1 = {
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
        public static float get_A_from_mu_il(float mu_il, float C) {
            Vec2 p0 = new(A_from_mu_il_tbl0_C, lookup(A_from_mu_il_tbl0, mu_il));
            Vec2 p1 = new(A_from_mu_il_tbl1_C, lookup(A_from_mu_il_tbl1, mu_il));
            return sample(p0, p1, C, false);
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

    protected const float EXTRA = 6f;
    protected int DIVISIONS => max(200, (int)(200f / VOXEL_SIZE));

    public required PartMating pm { get; init; }

    public float Ir_chnl = NAN;
    public float Or_chnl = NAN;
    protected void initialise_chnl() {
        Ir_chnl = pm.Mr_chnl - 0.5f*pm.min_wi_chnl;
        Or_chnl = pm.Mr_chnl + 0.5f*pm.min_wi_chnl;
    }


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
    protected float z0_mw = NAN;

    public required InjectorElement element { get; init; }
    protected void initialise_elements() {
        element.initialise(pm, numel(points_inj), out z0_mw, out _);
    }



    protected Voxels voxels_plate() {
        float th_edge = 2.0f;
        float th_oring = 1.7f;
        float phi_inner = torad(60f);
        List<Vec2> points = [
            new(-EXTRA, 0f),
            new(-EXTRA, Ir_chnl),
            new(th_edge, Ir_chnl),
            new(th_plate + th_oring, pm.Or_Ioring + 0.5f),
            new(th_plate + th_oring, pm.Ir_Ioring - 0.5f),
            new(th_plate, pm.Ir_Ioring - 1f - th_oring*tan(phi_inner)),
            new(th_plate, 0f),
        ];

        Polygon.fillet(points, 5, 3f);
        Polygon.fillet(points, 4, 2f);
        Polygon.fillet(points, 3, 3f);
        Polygon.fillet(points, 2, 2f);

        Voxels vox = new(Polygon.mesh_revolved(
            new Frame(),
            points,
            slicecount: DIVISIONS/2
        ));

        // Film cooling holes.
        foreach (Vec2 p in points_fc) {
            vox.BoolSubtract(new Rod(
                new(rejxy(p, 0f)),
                th_plate,
                D_fc/2f
            ).extended(2f*EXTRA, Extend.UPDOWN));
        }

        return vox;
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
    }

    protected Voxels voxels_maniwalls(Geez.Cycle key, out ManiVol vol,
            out Voxels neg_LOx, out Voxels neg_IPA) {
        using var __ = key.like();


        { // create roof + interior volume.
            float max_r = Or_chnl + 1.8f*th_plate/tan(phi_mw);
            // https://www.desmos.com/calculator/tusqawwtn5
            Vec2 peak = new( /* (z,r) */
                max_r/2f/tan(phi_mw) + z0_mw/2f,
                max_r/2f - z0_mw/2f*tan(phi_mw)
            );
            vol = new ManiVol{
                A = Cone.phied(new(), -phi_mw, peak.X, r0: max_r),
                B = Cone.phied(new(z0_mw*uZ3), phi_mw, peak.X - z0_mw + EXTRA),
                peak = peak,
                th = th_omw
            };
        }

        // For lox/ipa boundary:
        float th = th_dmw;
        float Dz = th/sin(phi_mw);

        Voxels neg = new();
        Voxels pos = new();

        List<Geez.Key> keys = new(2*numel(points_inj));

        { // lox-ipa boundary.
            foreach (Vec2 p in points_inj) {
                Frame at = new(rejxy(p, z0_mw));
                Mesh po = Cone.phied(at, phi_mw, vol.peak.X);
                Mesh ne = Cone.phied(at.transz(Dz), phi_mw, vol.peak.X);
                keys.Add(Geez.mesh(po));
                pos.BoolAdd(new(po));
                neg.BoolAdd(new(ne));
            }
        }


        // Sneak the individual fluid volumes in here.
        // NOTE:
        // - neg_LOx INCLUDES the lox/ipa dividing wall AND the ipa volume AND
        //      the plate.
        // - neg_IPA INCLUDES the plate.
        // - AND both are extruded down by 2*EXTRA
        neg_LOx = vol.A;
        neg_LOx.BoolSubtract(vol.B);
        neg_LOx.BoolAdd(new Rod(
            new(0.1f*uZ3 /* overlap */),
            -2f*EXTRA + 0.1f,
            vol.A.r0
        ));
        neg_IPA = neg_LOx.voxDuplicate();
        neg_IPA.BoolSubtract(pos);


        { // supports.
            // TODO:
            float min_r = pm.Ir_Ioring - 1.3f;
            float max_r = pm.Or_Ioring + 1.3f;
            float length = max_r - min_r;
            float aspect_ratio = 1.5f;
            float width = length / aspect_ratio;

            // TODO:
            int no = 50;
            float Mr = ave(min_r, max_r);
            float z = pm.Lz_Ioring + th_plate/3f;
            List<Vec3> points = Polygon.circle(no, Mr, 0f, z);
            foreach (Vec3 p in points) {
                Mesh m = Polygon.mesh_extruded(
                    Frame.cyl_axial(p), // x = +radial, y = +circumferential
                    vol.peak.X,
                    [
                        // diamond.
                        new(0f,         -width/2f),
                        new(-length/2f, 0f),
                        new(0f,         +width/2f),
                        new(+length/2f, 0f),
                    ]
                );
                keys.Add(Geez.mesh(m));
                pos.BoolAdd(new(m));
            }
        }

        key <<= Geez.group(keys);


        // Intersect with internal volume.

        neg.BoolIntersect(vol.A);
        neg.BoolSubtract(vol.B);

        // add safety margin for pos (leaving half of intersection with wall).
        pos.BoolIntersect(vol.A.transz(vol.Lz/2f));
        pos.BoolSubtract(vol.B.transz(vol.Lz/2f));


        // Add roof.
        pos.BoolAdd(vol.A.shelled(+vol.th).lengthed(0f, vol.Lz + EXTRA));
        pos.BoolAdd(vol.B.shelled(-vol.th).lengthed(0f, vol.Lz + EXTRA));
        // Add bounding wall for channel.
        pos.BoolAdd(new Rod(
            new(),
            vol.z(Or_chnl) + EXTRA,
            Or_chnl,
            vol.A.r0 + vol.Lr
        ).extended(EXTRA, Extend.DOWN));
        pos.BoolSubtract(vol.B.transz(vol.Lz)
                .lengthed(0f, 2f*EXTRA));
        pos.BoolSubtract(vol.A.transz(vol.Lz)
                .lengthed(2f*EXTRA, 2f*EXTRA)
                .shelled(4f*EXTRA));

        // Make final.
        pos.BoolSubtract(neg);
        key.voxels(pos);
        return pos;
    }


    protected void voxels_asi(Geez.Cycle key_asi, out Voxels pos,
            out Voxels neg) {

        // TODO: fix asi magic numbers
        float Lz = 54f;
        // create augmented spark igniter through-port
        GPort port = new GPort("1/4in", 6.35f); // 1/4in OD for SS insert?
        // TODO: ^

        // fluid volume.
        neg = port.filled(new(Lz*uZ3), out _);
        neg.BoolAdd(new Rod(
            new(th_plate*uZ3),
            Lz - th_plate,
            port.downstream_radius
        ).extended(EXTRA, Extend.UP));
        neg.BoolAdd(new Rod(
            new(th_plate*uZ3),
            -th_plate,
            port.downstream_radius - 1f
        ).extended(EXTRA, Extend.UPDOWN));

        // walling.
        pos = port.shelled(new(Lz*uZ3), 4f, out _);
        pos.BoolAdd(new Rod(
            new(),
            Lz,
            port.downstream_radius
        ).shelled(4f));
        key_asi.voxels(pos);
    }


    protected Voxels voxels_flange(Geez.Cycle key) {
        using var __ = key.like();

        Rod flange = new Rod(
            new Frame(),
            pm.flange_thickness_inj,
            pm.flange_outer_radius
        );

        Voxels vox = flange.extended(EXTRA, Extend.DOWN);
        key.voxels(vox);

        for (int i=0; i<pm.no_bolt; ++i) {
            float theta = i*TWOPI/pm.no_bolt;
            vox.BoolAdd(new Rod(
                new(fromcyl(pm.Mr_bolt, theta, 0f)),
                flange.Lz,
                pm.Bsz_bolt/2f + pm.thickness_around_bolt
            ).extended(EXTRA, Extend.DOWN));
        }
        key.voxels(vox);

        Fillet.concave(vox, pm.flange_fillet_radius, inplace: true);
        key.voxels(vox);

        vox.BoolSubtract(new Rod(
            new(),
            flange.Lz,
            Or_chnl
        ).extended(EXTRA, Extend.UPDOWN));
        key.voxels(vox);

        return vox;
    }


    protected void voxels_elements(Geez.Cycle key, out Voxels pos,
            out Voxels neg) {
        pos = new();
        neg = new();
        foreach (Vec2 p in points_inj) {
            element.voxels(new(rejxy(p)), out Voxels po, out Voxels ne);
            pos.BoolAdd(po);
            neg.BoolAdd(ne);
            key.voxels(neg);
        }
    }


    protected Voxels voxels_bolts() {
        Voxels vox = new();
        for (int i=0; i<pm.no_bolt; ++i) {
            float theta = i*TWOPI/pm.no_bolt;
            Vec2 p = frompol(pm.Mr_bolt, theta);
            // for bolt.
            vox.BoolAdd(new Rod(
                new(rejxy(p, 0f)),
                pm.flange_thickness_inj,
                pm.Bsz_bolt/2f
            ).extended(2f*EXTRA, Extend.UPDOWN));
            // for washer/nut.
            vox.BoolAdd(new Rod(
                new(rejxy(p, pm.flange_thickness_inj)),
                2f*EXTRA,
                pm.Or_washer + 0.5f
            ));
        }
        return vox;
    }


    protected Voxels voxels_orings() {
        Voxels vox = new Rod(
            new(),
            pm.Lz_Ioring,
            pm.Ir_Ioring,
            pm.Or_Ioring
        ).extended(2f*EXTRA, Extend.DOWN);
        vox.BoolAdd(new Rod(
            new(),
            pm.Lz_Ooring,
            pm.Ir_Ooring,
            pm.Or_Ooring
        ).extended(2f*EXTRA, Extend.DOWN));
        return vox;
    }


    protected Voxels voxels_gussets(Geez.Cycle key, in ManiVol mani_vol) {

        Voxels vox = new();

        // Concial outer boundary.
        Cone volC = new(
            new((pm.flange_thickness_inj - 0.2f)*uZ3),
            mani_vol.peak.X + mani_vol.Lz - pm.flange_thickness_inj + 0.2f,
            pm.Mr_bolt + 2f,
            mani_vol.peak.Y
        );

        float wi = 2f*(pm.Or_washer + 2f);
        float semiwi = wi/2f;

        // Big blocks for each bolt.
        Slice<Vec2> points = Polygon.circle(pm.no_bolt, pm.Mr_bolt);
        for (int i=0; i<numel(points); ++i) {
            Vec2 p = points[i];
            Frame frame = Frame.cyl_axial(rejxy(p, 0f));
            // x=+radial, y=+circum.
            float Dx = -0.5f*pm.Mr_bolt + 0.5f*mani_vol.peak.Y;
            vox.BoolAdd(new Bar(
                frame.transx(Dx),
                2f*abs(Dx) + EXTRA,
                wi,
                mani_vol.peak.X + mani_vol.Lz
            ).extended(EXTRA, Extend.DOWN));
            key.voxels(vox);
        }

        // Trim down.
        vox.BoolIntersect(volC);
        key.voxels(vox);

        // Add the thrust structure mounts.
        assert(numel(points)%2 == 0);
        for (int i=0; i<numel(points); ++i) {
            if (i%2 == 0) // only every second.
                continue;

            // vertical loft: rectangle -> circle
            Vec2 p = points[i];
            p = (mag(p) - 20f)*normalise(p); // move in 20mm. TODO:

            float z0;
            { // calc z of intersection with cone C and s.t. rect can be flat.
                float t = invlerp(volC.r0, volC.r1, mag(p) - semiwi);
                z0 = lerp(0f, volC.Lz, t);
                z0 += volC.bbase.pos.Z;
            }

            Frame bot = Frame.cyl_axial(rejxy(p, z0)); // x=+radial
            Frame top = bot.transz(54f - bot.pos.Z, false); // TODO:

            float strut_area = squared(2f*(pm.Bsz_bolt/2f) + 4f);
            float strut_radius = 1.2f*sqrt(strut_area/PI);

            int N = DIVISIONS / 5;
            N = max(12, N);
            N -= N % 4;

            int M = max(10, DIVISIONS/10);

            List<Vec2> V_rect = [
                new(+semiwi, +semiwi),
                new(-semiwi, +semiwi),
                new(-semiwi, -semiwi),
                new(+semiwi, -semiwi),
            ];
            V_rect = Polygon.resample(V_rect, N, true);

            List<Vec2> V_circ = Polygon.circle(N, strut_radius, +PI_4);

            // Start with box.
            List<Vec2> vertices = [..V_rect];
            List<Frame> frames = [bot.transz(-z0)];

            // Interp between square and circle, with continuous derivative also.
            for (int j=0; j<M; ++j) {
                float t = lerp(0f, 1f, j, M);
                // smooth https://www.desmos.com/calculator/hf7a0a7upp.
              #if false
                float n = 1.25f;
                float s = pow(t, n) / (pow(t, n) + pow(1f - t, n));
              #else
                float s = t*t*(3f - 2f*t);
              #endif
                vertices.AddRange(Polygon.lerp(V_rect, V_circ, s));
                frames.Add(bot.lerp(top, t));
            }

            // Rect->circle + base.
            Mesh m = Polygon.mesh_swept(new FramesSequence(frames), vertices);
            vox.BoolAdd(new(m));

            // Bolt hole ish.
            vox.BoolSubtract(new Rod(top, -10f, 3f));

            key.voxels(vox);
        }

        // Trim out.
        vox.BoolSubtract(mani_vol.A.transz(mani_vol.Lz/2f));
        vox.BoolSubtract(new Rod(
            mani_vol.A.bbase.transz(mani_vol.Lz/2f + 0.1f),
            -4f*EXTRA,
            mani_vol.A.r0
        ));
        vox.BoolSubtract(new Rod(
            new(),
            mani_vol.peak.X + mani_vol.Lz + EXTRA,
            mani_vol.peak.Y
        ));

        key.voxels(vox);
        return vox;
    }



    protected void voxels_ports(Geez.Cycle key, in ManiVol mani_vol,
            in Voxels neg_LOx, in Voxels neg_IPA, out Voxels pos,
            out Voxels neg) {

        // determine vertical height of ports:  for now use placeholder
        float height = 54f; // TODO: magic nom
        float D_pt = 2f; // PT though-hole diameter.

        // fucking c sharp cannot access out var from local function.
        Voxels _pos = new();
        Voxels _neg = new();

        void portme(GPort port, Frame at, float th, in Voxels? sub=null) {
            Voxels this_pos = port.shelled(at, th, out _);
            this_pos.BoolAdd(new Rod(at, -height, port.downstream_radius + th));
            Voxels this_neg = port.filled(at, out _);
            this_neg.BoolAdd(new Rod(
                at,
                EXTRA,
                port.pilot_bore_radius
            ).extended(0.5f, Extend.DOWN));
            this_neg.BoolAdd(new Rod(
                at,
                -height - EXTRA,
                port.downstream_radius
            ));
            if (sub != null) {
                this_pos.BoolSubtract(sub);
                this_neg.BoolSubtract(sub);
            }
            _pos.BoolAdd(this_pos);
            _neg.BoolAdd(this_neg);
            key.voxels(_pos);
        }

        GPort port_LOx_inlet = new("1/2in", 21.1f);
        GPort port_LOx_pt    = new("1/4in", D_pt);
        GPort port_IPA_pt    = new("1/4in", D_pt);
        GPort port_cc_pt     = new("1/4in", D_pt);

        float r_LOx_inlet = mani_vol.peak.Y;
        float r_LOx_pt    = mani_vol.peak.Y;
        float r_IPA_pt    = mani_vol.peak.Y;
        float r_cc_pt     = mani_vol.peak.Y;

        Frame at_LOx_inlet = new(new Vec3(-r_LOx_inlet, 0f, height));
        Frame at_LOx_pt    = new(new Vec3(+r_LOx_pt,    0f, height));
        Frame at_IPA_pt    = new(new Vec3(0f,    +r_IPA_pt, height));
        Frame at_cc_pt     = new(new Vec3(0f,     -r_cc_pt, height));

        portme(port_LOx_inlet, at_LOx_inlet, 4f, neg_LOx);
        portme(port_LOx_pt,    at_LOx_pt,    4f, neg_LOx);
        portme(port_IPA_pt,    at_IPA_pt,    4f, neg_IPA);
        portme(port_cc_pt,     at_cc_pt,     4f);

        pos = _pos;
        neg = _neg;
    }



    delegate void _Op(in Voxels vox);
    public Voxels? voxels() {

        // gripped and ripped from chamber.

        /* cheeky timer. */
        System.Diagnostics.Stopwatch stopwatch = new();
        stopwatch.Start();
        using var _ = Scoped.on_leave(() => {
            print($"Baby made in {stopwatch.Elapsed.TotalSeconds:N1}s.");
            print();
        });


        /* create the part object and its key. */
        Voxels part = new();
        Geez.Cycle key_part = new();


        /* create overall bounding box to size screenshots. */
        float overall_Lr = pm.Mr_bolt
                         + pm.Bsz_bolt/2f
                         + pm.thickness_around_bolt;
        float overall_Lz = 54f; // approx total height
        float overall_Mz = overall_Lz/2f;
        BBox3 overall_bbox = new(
            new Vec3(-overall_Lr, -overall_Lr, overall_Mz - overall_Lz/2f),
            new Vec3(+overall_Lr, +overall_Lr, overall_Mz + overall_Lz/2f)
        );


        /* screen shot a */
        Geez.Screenshotta screenshotta = new(
            new Geez.ViewAs(
                overall_bbox,
                theta: torad(135f),
                phi: torad(105f),
                bgcol: Geez.BACKGROUND_COLOUR_LIGHT
            )
        );


        /* concept of "steps", which the construction is broken into. each step
           is also screenshotted (if requested). */
        int step_count = 0;
        void step(string msg, bool view_part=false) {
            ++step_count;
            if (view_part)
                key_part.voxels(part);
            if (take_screenshots)
                screenshotta.take(step_count.ToString());
            print($"[{step_count,2}] {msg}");
        }
        void substep(string msg, bool view_part=false) {
            if (view_part)
                key_part.voxels(part);
            print($"   | {msg}");
        }

        /* shorthand for adding/subtracting a component into the part. */
        void _op(_Op func, ref Voxels? vox, Geez.Cycle? key, bool keepme,
                bool view_part) {
            assert(vox != null);
            func(vox!);
            if (view_part)
                key_part.voxels(part);
            if (key != null)
                key.clear();
            if (!keepme)
                vox = null;
        }
        void add(ref Voxels? vox, Geez.Cycle? key=null, bool keepme=false,
                bool view_part=true)
            => _op(part.BoolAdd, ref vox, key, keepme, view_part);
        void sub(ref Voxels? vox, Geez.Cycle? key=null, bool keepme=false,
                bool view_part=true)
            => _op(part.BoolSubtract, ref vox, key, keepme, view_part);


        /* perform all the steps of creating the part. */

        Geez.Cycle key_plate = new(colour: COLOUR_CYAN);
        Geez.Cycle key_elements = new(colour: COLOUR_GREEN);
        Geez.Cycle key_maniwalls = new(colour: COLOUR_PINK);
        Geez.Cycle key_asi = new(colour: COLOUR_WHITE);
        Geez.Cycle key_flange = new(colour: COLOUR_BLUE);
        Geez.Cycle key_gussets = new(colour: COLOUR_YELLOW);
        Geez.Cycle key_ports = new(colour: COLOUR_RED);

        Voxels? plate = voxels_plate();
        key_plate.voxels(plate);
        step("created plate.");

        voxels_elements(key_elements, out Voxels? pos_elements,
                out Voxels? neg_elements);
        step("created injector elements");

        Voxels? maniwalls = voxels_maniwalls(key_maniwalls,
                out ManiVol mani_vol, out Voxels neg_LOx, out Voxels neg_IPA);
        step("created manifold walls.");

        voxels_asi(key_asi, out Voxels? pos_asi, out Voxels? neg_asi);
        step("created asi port.");

        Voxels? bolts = voxels_bolts();
        substep("created bolts.");
        Voxels? orings = voxels_orings();
        substep("created O-rings.");
        Voxels? flange = voxels_flange(key_flange);
        step("created flange.");

        Voxels? gussets = voxels_gussets(key_gussets, mani_vol);
        step("created gussets.");

        voxels_ports(key_ports, mani_vol, neg_LOx, neg_IPA,
                out Voxels? pos_ports, out Voxels? neg_ports);
        step("created ports.");

        add(ref plate, key_plate);
        substep("added plate.");
        add(ref pos_elements);
        substep("added elements.");
        add(ref maniwalls, key_maniwalls);
        substep("added manifold walls.");
        add(ref pos_asi, key_asi);
        substep("added asi.");
        add(ref flange, key_flange);
        substep("added flange.");
        add(ref gussets, key_gussets);
        substep("added gussets.");
        add(ref pos_ports, key_ports);
        substep("added ports.");

        step("added material.");

        sub(ref neg_elements, key_elements);
        substep("subtracted elements.");
        sub(ref bolts);
        substep("subtracted bolts.");
        sub(ref orings);
        substep("subtracted O-rings.");
        sub(ref neg_asi);
        substep("subtracted asi.");
        sub(ref neg_ports);
        substep("subtracted ports.");

        step("removed voids.");

        part.BoolSubtract(new Rod(
            new(),
            -2f*EXTRA,
            overall_Lr + EXTRA
        ));
        substep("clipped bottom.", view_part: true);

        step("finished.");


        return part;
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


    public void anything() {}


    public string name => "injector_stu";


    public bool minimise_mem     = false;
    public bool take_screenshots = false;
    public void set_modifiers(int mods) {
        _ = popbits(ref mods, TPIAP.MINIMISE_MEM);
        take_screenshots = popbits(ref mods, TPIAP.TAKE_SCREENSHOTS);
        _ = popbits(ref mods, TPIAP.LOOKIN_FANCY);
        if (mods == 0)
            return;
        throw new Exception("yeah nah dunno what it is");
        throw new Exception("honestly couldnt tell you");
        throw new Exception("what is happening right now");
                          // im selling out
    }



    public void initialise() {
        initialise_chnl();
        initialise_inj();
        initialise_fc();
        initialise_elements();
    }
}
