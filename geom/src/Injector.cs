using System.Numerics;
using Calculations;
using static br.Br;
using br;
using Leap71.ShapeKernel;
using PicoGK;
using ports;

public class Injector : TwoPeasInAPod.Pea
{
    // Initialise the variables
    // coolin channel vars
    public required PartMating pm { get; init; }
    public float Ir_chnl;
    public float Or_chnl;
    protected void initialise_chnl()
    {
        Ir_chnl = pm.Mr_chnl - 0.5f*pm.min_wi_chnl;
        Or_chnl = pm.Mr_chnl + 0.5f*pm.min_wi_chnl;
    }

    // manufacturing vars
    public required float fPrintAngle { get; init; }

    // injector plate vars
    public required List<int> no_inj { get; init; }
    public required List<float> r_inj { get; init; }
    public required List<float> theta0_inj { get; init; }
    public required float fTargetdPFrac { get; init; }
    public required float fInjectorPlateThickness { get; init; }
    public float fTargetdP;
    public float fLOxPostLength;
    public float fLOxPostWT;
    public float fFuelAnnulusOR;
    public float fOxElementMFR;
    public float fFuelElementMFR;
    public float fCoreExitArea;
    public float fCoreExitRadius;
    public int iOxSwirlInlets;
    public int iFuelSwirlInlets;
    public float fTargetCdOx;
    public float fTargetCdFuel;
    public int no_annulus_rib;
    public float annulus_rib_width;
    public float area_swinlet_perinj;
    public float aspect_swinlet;
    public float rad_offset_swinlet;
    public int iTotalElementCount;
    public List<Vector3>? points_inj = null;

    public required float sw_n_1 { get; init; }
    public required float sw_n_2 { get; init; }
    public float sw_R_n1;
    public float sw_R_1;
    public float sw_R_n2;
    public float sw_R_in1;
    public float sw_r_in1;
    public float sw_R_in2;
    public float sw_r_in2;
    public float sw_l_in1;
    public float sw_l_n1;
    public float sw_l_s1;
    public float sw_delta_l_n;

    protected void initialise_inj()
    {
        // injector patterning / overall init
        points_inj = new();
        iTotalElementCount = 0;
        for (int n=0; n<numel(no_inj); ++n) {
            List<Vector3> ps = circularly_distributed(
                no:     no_inj[n],
                z:      0f,
                r:      r_inj[n],
                theta0: theta0_inj[n]
            );
            points_inj.AddRange(ps);
            iTotalElementCount += no_inj[n];
        }
        fOxElementMFR = pm.fOxMassFlowRate / iTotalElementCount;
        fFuelElementMFR = pm.fFuelMassFlowRate / iTotalElementCount;
        Library.Log($"total elements count: {iTotalElementCount}");

        // LOX / swirl / central post init
        fLOxPostLength = 10f;
        fLOxPostWT = 0.8f;

        iOxSwirlInlets = 4;
        iFuelSwirlInlets = 4;
        fTargetCdOx = 0.25f;
        // fTargetCdFuel = 0.25f;
        fTargetdP = fTargetdPFrac*pm.fChamberPressure;

        fCoreExitArea = fOxElementMFR/(fTargetCdOx*sqrt(2*pm.fOxInjectionRho*fTargetdP)) * 1e6f; // mm^2
        // A = PI*r^2
        // r = sqrt(A/pi)
        fCoreExitRadius = sqrt(fCoreExitArea/PI);
        Library.Log($"Ox core fluid rad = {fCoreExitRadius}");

        // IPA annulus init
        float min_rib_wid = 0.8f;
        float min_gap_wid = 0.8f;
        float cd_annulus = 0.75f;
        float rib_percent_area = 0.2f;

        float vel_bernoulli_annulus = sqrt(2*fTargetdP/pm.fFuelInjectionRho);
        Library.Log($"v_ideal = {vel_bernoulli_annulus}");
        float area_annulus = fFuelElementMFR/(cd_annulus*sqrt(2*pm.fFuelInjectionRho*fTargetdP)) * 1e6f;
        // float area_annulus = fFuelElementMFR/(pm.fFuelInjectionRho*cd_annulus*vel_bernoulli_annulus) * 1e6f; // mm^2
        Library.Log($"annulus area: {area_annulus}");

        float Or_post = fCoreExitRadius + fLOxPostWT;
        float total_annulus_area = area_annulus * (1+rib_percent_area);
        float total_rib_area = area_annulus*rib_percent_area;

        fFuelAnnulusOR = sqrt(total_annulus_area/PI + pow(Or_post,2));
        Library.Log($"Fuel annulus OR = {fFuelAnnulusOR}");
        Library.Log($"Fuel (annulus) area: {area_annulus} mm^2");
        Library.Log($"Oxidisr (core) area: {fCoreExitArea} mm^2");

        float annulus_width = fFuelAnnulusOR - Or_post;
        Library.Log($"Annulus width: {annulus_width}");

        if (annulus_width > min_gap_wid)
        {
           // calculate max rib count (min width)
            float smallest_rib_area = min_rib_wid*(fFuelAnnulusOR - Or_post);
            no_annulus_rib = (int)floor(total_rib_area / smallest_rib_area);
            float actual_rib_area = total_rib_area / no_annulus_rib;
            annulus_rib_width = actual_rib_area / (fFuelAnnulusOR - Or_post);
            Library.Log($"{no_annulus_rib} ribs @ {annulus_rib_width} wide");
        } else
        {
            fFuelAnnulusOR = Or_post + min_gap_wid;
            float required_rib_area = PI*(pow(fFuelAnnulusOR,2) - pow(Or_post,2)) - area_annulus;
            float smallest_rib_area = min_rib_wid*(fFuelAnnulusOR - Or_post);

            no_annulus_rib = (int)ceil(required_rib_area / smallest_rib_area);
            float actual_rib_area = required_rib_area / no_annulus_rib;
            annulus_rib_width = actual_rib_area / (fFuelAnnulusOR - Or_post);
            float blockage = required_rib_area / (PI*(pow(fFuelAnnulusOR,2) - pow(Or_post,2)));
            Library.Log($"Gap too small! Gap-bounded solution -> {blockage*100f}% blockage");
            Library.Log($"{no_annulus_rib} ribs @ {annulus_rib_width} wide");
        }

        /*
            segment area (inner) A = 0.5*r^2*(theta-sin(theta))
            rib area = rectangle - inner segment + outer segment
            approximate inner segment == outer segment
        */

        // // outer swirl init
        // float twoalpha_outer = torad(90);
        // float A_outer = 2.5f;
        // float mu_outer = 0.25f;
        // float nozzle_radius_outer = 0.475f * sqrt(fFuelElementMFR/(mu_outer*sqrt(pm.fFuelInjectionRho*fTargetdP))) * 1e3f; //mm
        // print($"nozzle rad outer = {nozzle_radius_outer}");


        // // inner swirl init
        // float twoalpha_inner = torad(105);
        // float A_inner = 4.5f;
        // float mu_inner = 0.15f;
        // float nozzle_radius_inner = 0.475f * sqrt(fOxElementMFR/(mu_inner*sqrt(pm.fOxInjectionRho*2*fTargetdP))) * 1e3f; //mm
        // print($"nozzle rad inner = {nozzle_radius_inner}");
        // area_swinlet_perinj = 10f; // mm^2
        // no_swinlet = 3;
        // aspect_swinlet = 5f; // height/width
        // rad_offset_swinlet = 2f;
    }

    protected void initilise_biswirl()
    {
        // injector patterning / overall init
        points_inj = new();
        iTotalElementCount = 0;
        for (int n=0; n<numel(no_inj); ++n) {
            List<Vector3> ps = circularly_distributed(
                no:     no_inj[n],
                z:      0f,
                r:      r_inj[n],
                theta0: theta0_inj[n]
            );
            points_inj.AddRange(ps);
            iTotalElementCount += no_inj[n];
        }

        // inputs
        fTargetdP = fTargetdPFrac*pm.fChamberPressure;
        float dP_i1 = fTargetdP;
        float dP_i2 = fTargetdP;
        print($"foxmassflow: {pm.fOxMassFlowRate}, itotalelementcount: {iTotalElementCount}");
        float mdot_i1 = pm.fOxMassFlowRate / iTotalElementCount;
        float mdot_i2 = pm.fFuelMassFlowRate / iTotalElementCount;

        // selected params
        float Rbar_in1 = 1.4f; // coefficients of nozzle opening
        float Rbar_in2 = 1f; //TODO: should be bigger?  1 is convenient
        float lbar_n1 = 1f; // relative nozzle lengths:  l_n / (2 * R_n)
        float lbar_n2 = 0.5f;
        float twoalpha_1 = torad(80f); // spray cone angle of stage 1

        float C_1 = 2f;
        float C_2 = 1.2f;

        float nu_IPA = 2.66e-6f; // m2/s //TODO: f(T) this bad boy, it changes a lot w temp!
        float nu_LOX = 2.56e-6f; // at arithmetic mean of airborne ICD (114K)

        //   stage 1 (inner core) design
        float A_1 = GraphLookup.GetA(todeg(twoalpha_1), lbar_n1);
        float mu_1 = GraphLookup.GetFlowCoefficient(A_1, C_1);

        print($"mu_1: {mu_1}, mdot_i1: {mdot_i1}, dP_i1: {dP_i1}, pm.fOxInjectionRho: {pm.fOxInjectionRho}");
        sw_R_n1 = 0.475f * sqrt(mdot_i1/(mu_1*sqrt(pm.fOxInjectionRho*dP_i1))) * 1e3f; // mm
        sw_R_in1 = Rbar_in1 * sw_R_n1;
        sw_r_in1 = sqrt(sw_R_in1*sw_R_n1/(sw_n_1*A_1));

        float Re_in1 = 2*mdot_i1/(PI*sqrt(sw_n_1)*(sw_r_in1*1e-3f)*pm.fOxInjectionRho*nu_LOX); // dimensionless

        sw_l_in1 = 4f   * sw_r_in1;  // tangential passage length
        sw_l_n1  = 2f   * sw_R_n1;   // nozzle length
        sw_l_s1  = 2.5f * sw_R_in1;  // vortex chamber length

        float del_w = 0.8f;  // nozzle wall thickness
        sw_R_1 = sw_R_n1 + del_w; // external radius of nozzle
        float rm_bar_1 = GraphLookup.GetRelativeVortexRadius(A_1, Rbar_in1);

        // --- stage 2 (outer annulus) design ---
        float r_m2_req = sw_R_1 + 0.3f;  // Required physical gas-vortex radius (constant)
        sw_R_n2 = r_m2_req;        // Initial guess for nozzle radius

        float tolerance = 0.001f;
        int maxIter = 100;
        int iter = 0;
        float diff = 1f;
        float A_2 = 0;
        float mu_2 = 0;
        float rm_bar_2 = 0;

        while (diff > tolerance && iter < maxIter)
        {
            // 1. Calculate current flow coefficient mu based on current Rn
            mu_2 = (mdot_i1+mdot_i2) / (PI * pow(sw_R_n2*1e-3f, 2) * sqrt(2*pm.fFuelInjectionRho*dP_i2));

            // 2. Step 3: Find A based on mu (from Fig 34 correlations)
            A_2 = GraphLookup.GetAFromMu(mu_2, C_2);

            // 3. Step 3: Find dimensionless relative vortex radius (from Fig 35)
            rm_bar_2 = GraphLookup.GetRelativeVortexRadius(A_2, Rbar_in2);

            // 4. Step 4: Calculate NEW physical nozzle radius
            // Formula: Rn = r_m_physical / r_m_relative
            float R_n2_new = r_m2_req / rm_bar_2;

            diff = Math.Abs(R_n2_new - sw_R_n2);

            sw_R_n2 = R_n2_new;
            iter++;
        }

        sw_R_in2 = Rbar_in2 * sw_R_n2;
        sw_r_in2 = sqrt(sw_R_in2*sw_R_n2/(sw_n_2*A_2));
        float Re_in2 = 2*mdot_i2/(PI*sqrt(sw_n_2)*(sw_r_in2*1e-3f)*pm.fFuelInjectionRho*nu_IPA); // dimensionless
        // spray cone angle
        float two_alpha_2 = torad(GraphLookup.GetSprayConeAngle(A_2, lbar_n2));
        float two_alpha = two_alpha_2 - torad(35);
        float tau_i = 0.15e-3f;

        float K_m = mdot_i1/mdot_i2;
        float phi_1 = 1f - pow(rm_bar_1, 2);
        float phi_2 = 1f - pow(rm_bar_2, 2);

        // print inputs to l_mix calc
        print($"K_m: {K_m}, mu_2: {mu_2}, phi_2: {phi_2}, dP_i2: {dP_i2}, pm.fFuelInjectionRho: {pm.fFuelInjectionRho}");
        print($"mu_1: {mu_1}, phi_1: {phi_1}, dP_i1: {dP_i1}, pm.fOxInjectionRho: {pm.fOxInjectionRho}");


        float l_mix = sqrt(2)*tau_i*(
            (K_m*mu_2/(phi_2*(K_m+1)) * sqrt(dP_i2/pm.fFuelInjectionRho))
            + (mu_1/(phi_1*(K_m+1)) * sqrt(dP_i1/pm.fOxInjectionRho))
        ) * 1e3f;

        float l_n2 = 2f*lbar_n2*sw_R_n2; // stage 2 nozzle length

        sw_delta_l_n = l_n2 - l_mix;

        using (StreamWriter writer = new StreamWriter("exports/injector_report.txt"))
        {
            writer.WriteLine("Injector Report");
            writer.WriteLine("===============");
            writer.WriteLine();
            writer.WriteLine($"Total elements count: {iTotalElementCount}");
            writer.WriteLine();
            writer.WriteLine("Stage 1 (LOX):");
            writer.WriteLine($"  - Nozzle inner radius: {sw_R_n1} mm");
            writer.WriteLine($"  - Nozzle Reynolds number: {Re_in1}");
            writer.WriteLine($"  - Nozzle outer radius: {sw_R_1} mm");
            writer.WriteLine($"  - A_1: {A_1}");
            writer.WriteLine($"  - Rbar_in1: {Rbar_in1}");
            writer.WriteLine($"  - rm_bar_1: {rm_bar_1}");
            writer.WriteLine($"  - Tangential inlet radius: {sw_r_in1} mm");
            writer.WriteLine($"  - phi_1: {phi_1}");
            writer.WriteLine($"  - Reynolds number at tangential inlet: {Re_in1}");
            writer.WriteLine();
            writer.WriteLine("Stage 2 (IPA):");
            writer.WriteLine($"  - Nozzle outer radius: {sw_R_n2} mm");
            writer.WriteLine($"  - Nozzle Reynolds number: {Re_in2}");
            writer.WriteLine($"  - A_2: {A_2}");
            writer.WriteLine($"  - Mixing length: {l_mix} mm");
            writer.WriteLine($"  - Annulus radial gap: {sw_R_n2 - sw_R_1} mm");
            writer.WriteLine($"  - Nozzle length: {l_n2} mm");
            writer.WriteLine($"  - Tangential inlet radius: {sw_r_in2} mm");
            writer.WriteLine($"  - delta_L (inset length) {sw_delta_l_n} mm");
            writer.WriteLine($"  - phi_2: {phi_2}");
            writer.WriteLine($"  - Reynolds number at tangential inlet: {Re_in2}");
            writer.WriteLine();
        }

    }

    // film cooling vars
    public required float Or_fc { get; init; }
    public required List<int> no_fc { get; init; }
    public required List<float> r_fc { get; init; }
    public required List<float> theta0_fc { get; init; }
    public List<Vector3>? points_fc = null;
    protected void initialise_fc()
    {
        points_fc = new();
        for (int n=0; n<numel(no_fc); ++n) {
            List<Vector3> ps = circularly_distributed(
                no:     no_fc[n],
                z:      0f,
                r:      r_fc[n],
                theta0: theta0_fc[n]
            );
            points_fc.AddRange(ps);
        }
    }

    // construction vars
    public float fMaxAtticR;
    public float fMaxAtticZ;
    List<Vector3>? points_bolts = null;
    public Voxels? voxInnerPipeCrop;
    public required float fRoofThickness {get; init; }
    protected void initialise_construction()
    {
        points_bolts = circularly_distributed(pm.no_bolt, 0f, pm.Mr_bolt, 0f);
    }


    // voxel builder functions
    protected Voxels voxels_injector_plate(out Voxels fc_fluid)
    {
        // generate initial base plate
        BaseLens oPlate = new BaseLens(
            new LocalFrame(),
            fInjectorPlateThickness,
            0,
            Ir_chnl
        );

        // piecewise function for raised edges (inner o-ring support)
        float fGetPlateHeight(float phi, float r_norm)
        {
            float r_actual = r_norm * Ir_chnl;

            float reinforced_height = 4f;
            float min_height = 0.5f;
            float inner_ramp_angle = torad(30f);
            float outer_ramp_angle = torad(45f);
            float inner_ramp_len = (reinforced_height-fInjectorPlateThickness) / tan(inner_ramp_angle);
            float outer_ramp_len = (reinforced_height-min_height) / tan(outer_ramp_angle);

            float r_inner = r_fc[0] + 1f;
            float r_outer = Ir_chnl;
            float r_plateau_inner = r_inner + inner_ramp_len;
            float r_plateau_outer = r_outer - outer_ramp_len;

            if (r_actual < r_inner)
                return fInjectorPlateThickness;
            else if (r_actual >= r_inner && r_actual < r_plateau_inner)
                return fInjectorPlateThickness + (reinforced_height - fInjectorPlateThickness) * (r_actual - r_inner) / inner_ramp_len;
            else if (r_actual >= r_plateau_inner && r_actual <= r_plateau_outer)
                return reinforced_height;
            else if (r_actual > r_plateau_outer && r_actual <= r_outer)
                return min_height + (reinforced_height - min_height) * (r_outer - r_actual) / outer_ramp_len;
            else
                return 0f;
        }

        oPlate.SetHeight(
            new SurfaceModulation(0f),
            new SurfaceModulation(fGetPlateHeight)
        );


        Voxels voxPlate = oPlate.voxConstruct();

        // remove holes to create IPA annulus
        if (points_inj != null)
        {
            foreach (Vector3 point in points_inj)
            {
                voxPlate -= new Pipe(
                    new Frame(point).transz(-fInjectorPlateThickness/2),
                    2f * fInjectorPlateThickness,
                    sw_R_n2 + 0.1f // offset to ensure clean bool add
                ).voxels();

                // // add ribs
                // Frame rib_frame = new Frame(point, uY3).transx(-fInjectorPlateThickness/2);
                // Voxels ribs = new();
                // for (int i=0; i<no_annulus_rib; i++)
                // {
                //     ribs += new Cuboid(
                //         rib_frame,
                //         fInjectorPlateThickness,
                //         annulus_rib_width,
                //         fFuelAnnulusOR
                //     ).voxels();
                //     rib_frame = rib_frame.rotyz(TWOPI/no_annulus_rib);
                // }
                // voxPlate += ribs;
            }
        }

        // remove holes to create film cooling (IPA)
        fc_fluid = new Voxels();
        if (points_fc != null)
        {
            foreach (Vector3 aFilmHole in points_fc)
            {
                voxPlate -= new BaseCylinder(
                    new LocalFrame(aFilmHole),
                    fInjectorPlateThickness,
                    Or_fc).voxConstruct();
            }
        }

        voxPlate.BoolSubtract(fc_fluid);

        return voxPlate;
    }

    protected Voxels vox_inj_elements(out Voxels ox_post_fluid, out Voxels swirl_inlets, out Voxels cone_roof_crop)
    {
        // iterate through each injector element and create wall section and fluid section
        Voxels ox_post_wall = new();
        Voxels vtx_ch_1 = new();
        Voxels vtx_ch_2 = new();
        ox_post_fluid = new();
        swirl_inlets = new();
        cone_roof_crop = new();

        foreach (Vector3 aPoint in points_inj!)
        {
            Frame ox_post_frame = new Frame(aPoint).transz(sw_delta_l_n);
            ox_post_wall += new Pipe(ox_post_frame, sw_l_n1*2, sw_R_1).voxels();
            ox_post_wall -= cone_roof_lox_crop();
            ox_post_fluid += new Pipe(ox_post_frame, 2f*sw_l_n1, sw_R_n1).voxels();
            cone_roof_crop += new Pipe(ox_post_frame, 4f*sw_l_n1, sw_R_1).voxels();


            // build upper/inner swirl chamber
            // calculate upper wall height
            float fSwirlChamberLowerBound = fGetConeRoofUpperHeight(
                atan2(aPoint.Y, aPoint.X),
                (aPoint.R()+sw_R_in1)/Or_chnl
                );
            LocalFrame oSwirlChamberFrame = new LocalFrame(aPoint + new Vector3(0f, 0f, fSwirlChamberLowerBound));
            Frame swirl_chamber_frame = new Frame(aPoint).transz(fSwirlChamberLowerBound);
            // https://www.youtube.com/watch?v=TZtiJN6yiik

            Voxels voxNextSwirlChamber = new(); // temp so we don't cook old one w offsets
            voxNextSwirlChamber += new BasePipe(
                oSwirlChamberFrame,
                sw_l_s1,
                0f,
                sw_R_in1
                ).voxConstruct();

            voxNextSwirlChamber += new BaseCone(
                oSwirlChamberFrame.oTranslate(new Vector3(0f, 0f, sw_l_s1)),
                sw_R_in1,
                sw_R_in1,
                0
            ).voxConstruct();

            voxNextSwirlChamber += new BasePipe(
                oSwirlChamberFrame,
                -sw_l_s1,
                0f,
                sw_R_in1
            ).voxConstruct();

            // construct tangential swirl inlets (stage 1)
            Voxels inlets = new();
            Frame swinlet_frame = new Frame(aPoint);
            swinlet_frame = swinlet_frame.transz(fSwirlChamberLowerBound+sw_l_s1-sw_r_in1);
            Vector3 rot_point = swinlet_frame.pos;
            for (int i=0; i<sw_n_1; i++)
            {
                Frame shifted_frame = new Frame(rot_point, uZ3, uY3);
                shifted_frame = shifted_frame.rotyz(TWOPI*((float)i/sw_n_1));
                shifted_frame = shifted_frame.rotxy(DEG90);
                shifted_frame = shifted_frame.transx(0.8f*sw_R_in1); //TODO: fix hack

                inlets += new Pipe(
                    shifted_frame,
                    sw_R_in1*2,
                    sw_r_in1
                ).voxels();
            }

            voxNextSwirlChamber = voxNextSwirlChamber.voxOffset(1.5f) - voxNextSwirlChamber - inlets;
            vtx_ch_1 += voxNextSwirlChamber;

            // build lower/outer shear chamber
            float fShearChamberUpperBound = fGetConeRoofLowerHeight(
                atan2(aPoint.Y, aPoint.X),
                (aPoint.R()+sw_R_in2)/Or_chnl
            );
            Frame vtx_ch_2_frame = new Frame(aPoint);
            vtx_ch_2 += new Pipe(
                vtx_ch_2_frame,
                2*fShearChamberUpperBound,
                sw_R_n2,
                sw_R_n2 + 1f
            ).voxels();

            // construct tangential swirl inlets (stage 2)
            Voxels inlets_2 = new();
            Frame swinlet_frame_2 = new Frame(aPoint);
            swinlet_frame_2 = swinlet_frame_2.transz(fShearChamberUpperBound-sw_r_in2);
            Vector3 rot_point_2 = swinlet_frame_2.pos;
            for (int i=0; i<sw_n_2; i++)
            {
                Frame shifted_frame_2 = new Frame(rot_point_2, uZ3, uY3);
                shifted_frame_2 = shifted_frame_2.rotyz(TWOPI*((float)i/sw_n_2));
                shifted_frame_2 = shifted_frame_2.rotxy(DEG90);
                shifted_frame_2 = shifted_frame_2.transx(0.8f*sw_R_in2); //TODO: fix hack

                inlets_2 += new Pipe(
                    shifted_frame_2,
                    sw_R_in2*2,
                    sw_r_in2
                ).voxels();
            }

            vtx_ch_2 -= inlets_2;
        }
        vtx_ch_1 -= cone_roof_lower_crop();
        vtx_ch_2 -= cone_roof_lox_crop();
        return ox_post_wall + vtx_ch_1 + vtx_ch_2;
    }

    protected Voxels voxels_cone_roof()
    {
        // structure constructed using cones originating at each point in aPointsList

        BaseLens oRoof = new BaseLens(
            new LocalFrame(),
            fRoofThickness,
            0f,
            Or_chnl
        );

        oRoof.SetHeight(
            new SurfaceModulation(fGetConeRoofLowerHeight),
            new SurfaceModulation(fGetConeRoofUpperHeight)
            );

        return oRoof.voxConstruct();
    }

    protected Voxels voxels_attic()
    {
        // add top roof (attic)
        BaseLens oAttic = new BaseLens(
            new LocalFrame(),
            fRoofThickness,
            0f,
            Or_chnl
        );
        oAttic.SetHeight(new SurfaceModulation(fGetAtticLowerHeight), new SurfaceModulation(fGetAtticUpperHeight));
        Voxels voxAttic = new Voxels(oAttic.voxConstruct());
        fMaxAtticZ = voxAttic.oCalculateBoundingBox().vecMax.Z;
        fMaxAtticR = Or_chnl
            - ((fMaxAtticZ - fInjectorPlateThickness - cos(fPrintAngle)*8f) //TODO: fix magic number
            / tan(fPrintAngle));

        return voxAttic;
    }

    protected Voxels voxels_asi(out Voxels voxASIFluid, out Voxels stainless_tube)
    {
        // TODO: fix asi magic numbers
        // create augmented spark igniter through-port
        GPort ASIPort = new GPort("1/4in", 6.35f); // 1/4in OD for SS insert?
        Voxels voxPort = ASIPort.voxConstruct(new LocalFrame(new Vector3(0, 0, 54)));
        float Or_stainless = 6.35f/2;

        Voxels voxASIWall = new Pipe(
            new Frame(),
            54f - ASIPort.fGetPilotBoreDiameter(),
            Or_stainless,
            Or_stainless + 4f
        ).voxels();

        voxASIFluid = new Pipe(
            new Frame().transz(fInjectorPlateThickness),
            54f - ASIPort.fGetBoreDepthTotal(),
            0,
            Or_stainless
        ).voxels();

        voxASIFluid += new Pipe(
            new Frame().transz(1.5f * fInjectorPlateThickness).rotyz(DEG180),
            3f * fInjectorPlateThickness,
            Or_stainless - 1f
        ).voxels();

        stainless_tube = new Pipe(
            new Frame().transz(fInjectorPlateThickness),
            54f - fInjectorPlateThickness - 18f,
            Or_stainless - 1f,
            Or_stainless
            ).voxels();

        voxASIFluid += voxPort;
        Voxels total_wall = voxASIFluid.voxOffset(1.5f) + voxASIWall;
        total_wall.Trim(new BBox3(
            -Or_chnl,
            -Or_chnl,
            0f,
            Or_chnl,
            Or_chnl,
            54f
        ));

        return total_wall;
    }

    protected Voxels voxels_flange()
    {
        // create flange
        BasePipe oFlangeBase = new BasePipe(
            new LocalFrame(new Vector3(0f, 0f, -pm.inj_flange_thickness)),
            3*pm.inj_flange_thickness,
            Ir_chnl/2,
            pm.flange_outer_radius
        );
        Voxels voxBoltClearances = new();
        List<Vector3> aBoltPos = new();
        for (int i=0; i<pm.no_bolt; i++)
        {
            float fAngle = i*(2*PI/pm.no_bolt);
            Vector3 oBoltPos = new Vector3(pm.Mr_bolt*cos(fAngle), pm.Mr_bolt*sin(fAngle), 0);
            aBoltPos.Add(oBoltPos);
            voxBoltClearances += new BasePipe(
                new LocalFrame(oBoltPos+new Vector3(0f,0f,-pm.inj_flange_thickness)),
                3*pm.inj_flange_thickness,
                0,
                pm.Or_washer+2f
            ).voxConstruct();
            //TODO: add bridging rectangular prism here
        }
        Voxels voxFlange = oFlangeBase.voxConstruct() + voxBoltClearances;
        voxFlange += voxFlange.voxOverOffset(3f); // fillet concave only
        //Sh.PreviewVoxels(voxFlange, Cp.clrRandom(), 0.2f);
        BBox3 oVerticalTrimBox = new BBox3(
            -2*pm.flange_outer_radius,
            -2*pm.flange_outer_radius,
            0f,
            2*pm.flange_outer_radius,
            2*pm.flange_outer_radius,
            pm.inj_flange_thickness
        );
        voxFlange.Trim(oVerticalTrimBox);
        voxFlange -= cone_roof_upper_crop();

        return voxFlange;
    }

    protected Voxels voxels_gussets(out Voxels voxStrutHoles)
    {
        // make gussets
        // create cone shape
        BaseCone oGussetCone = new BaseCone(
            new LocalFrame(new Vector3(0f, 0f, pm.inj_flange_thickness)),
            fMaxAtticZ-pm.inj_flange_thickness,
            pm.Mr_bolt+2f,
            fMaxAtticR);
        Voxels voxGussetCone = oGussetCone.voxConstruct();
        // create spider web box thing and bolt holes voxel fields
        Voxels voxGussetBox = new();
        Voxels voxBoltHoles = new();
        Voxels voxThrustRods = new();
        voxStrutHoles = new();

        for (int i=0; i<points_bolts!.Count(); i++)
        {
            Vector3 oBoltPos = points_bolts![i];
            LocalFrame oGussetFrame = new LocalFrame(oBoltPos, Vector3.UnitZ, oBoltPos);
            voxGussetBox += new BaseBox(
                oGussetFrame,
                fMaxAtticZ,
                pm.Mr_bolt*2,
                2*(pm.Or_washer+2f)
            ).voxConstruct();

            // thrust rods
            if (i%2 == 1)
            {
                // vertical loft: rectangle -> circle
                Vector3 vecCast = VecOperations.vecUpdateRadius(oBoltPos, -20f)
                    + new Vector3(0f, 0f, fMaxAtticZ);
                vecCast = voxGussetCone.vecRayCastToSurface(vecCast, -Vector3.UnitZ);
                LocalFrame oBottomFrame = new LocalFrame(vecCast).oTranslate(new Vector3(0f, 0f, 0f));
                oBottomFrame = oBottomFrame.oRotate(VecOperations.fGetPhi(vecCast), Vector3.UnitZ);
                float fBottomFrameZ = oBottomFrame.vecGetPosition().Z;

                LocalFrame oTopFrame = new LocalFrame(
                    new Vector3(oBottomFrame.vecGetPosition().X,
                    oBottomFrame.vecGetPosition().Y,
                    fMaxAtticZ + 15)
                );
                float fStrutArea = pow(2f*(pm.Bsz_bolt/2)+4, 2);
                float fStrutRadius = 1.2f*sqrt(fStrutArea/PI);
                Rectangle oRectangle = new Rectangle(2*(pm.Or_washer+2f), 2*(pm.Or_washer+2f));
                Circle oCircle = new Circle(fStrutRadius);
                float fLoftHeight = oTopFrame.vecGetPosition().Z - oBottomFrame.vecGetPosition().Z;
                BaseLoft oStrut = new BaseLoft(oBottomFrame, oRectangle, oCircle, fLoftHeight);
                BaseBox oBridgeBox = new BaseBox(
                    oBottomFrame.oTranslate(new Vector3(0f, 0f, 0.04f*fLoftHeight)),
                    -fBottomFrameZ,
                    2*(pm.Or_washer+2f),
                    2*(pm.Or_washer+2f)
                );
                voxStrutHoles += new BaseCylinder(oTopFrame, -10f, 3f).voxConstruct();
                voxThrustRods += new Voxels(oStrut.mshConstruct()) + new Voxels(oBridgeBox.mshConstruct());
            }
        }
        Library.Log("Flange details (gussets etc.) created");


        // booleans
        voxInnerPipeCrop = new BasePipe(new LocalFrame(), 2*fMaxAtticZ, 0f, fMaxAtticR).voxConstruct();
        Voxels voxGusset = (voxGussetCone & voxGussetBox)
            + voxThrustRods
            - cone_roof_upper_crop() - voxInnerPipeCrop;

        return voxGusset;
    }

    protected Voxels voxels_ports(out Voxels voxFluids)
    {
        Voxels voxLOXCrop = cone_roof_upper_crop();
        Voxels voxIPACrop = cone_roof_lower_crop();

        // determine vertical height of ports:  for now use placeholder
        float fPortsHeight = 54f; // TODO: magic nom
        float pt_through_rad = 1f; // radius of through-hole for PTs
        // determine spacing:  ASI in middle, LOX inlet and 3x PT spaced evenly
        float lox_port_radius = 0.8f*pm.Or_cc;
        float through_port_radius = 0.5f * (r_inj[0] + r_inj[1]);


        // create vectors
        LocalFrame aLOXInlet = new LocalFrame(new Vector3(-lox_port_radius, 0, fPortsHeight));
        LocalFrame aLOXPT =  new LocalFrame(new Vector3(lox_port_radius, 0, fPortsHeight));
        LocalFrame aChamberPT =  new LocalFrame(new Vector3(0, -through_port_radius, fPortsHeight));
        LocalFrame aIPAPT =  new LocalFrame(new Vector3(0, through_port_radius, fPortsHeight));

        GPort oLOXInlet = new GPort("1/2in", 14f);
        BasePipe oLOXInletPipe = new BasePipe(aLOXInlet, -fPortsHeight, 0f, 14f/2);
        Voxels voxLOXInletFluid = oLOXInlet.voxConstruct(aLOXInlet);// + oLOXInletPipe.voxConstruct();
        Voxels voxLOXInletWall = voxLOXInletFluid.voxOffset(2f);
        voxLOXInletFluid = voxLOXInletFluid - voxLOXCrop;
        voxLOXInletWall = voxLOXInletWall - voxLOXCrop;

        GPort oLOXPT = new GPort("1/4in", pt_through_rad*2);
        BasePipe oLOXPTPipe = new BasePipe(aLOXPT, -fPortsHeight, 0f, pt_through_rad);
        Voxels voxLOXPTFluid = oLOXPT.voxConstruct(aLOXPT) + oLOXPTPipe.voxConstruct();
        Voxels voxLOXPTWall = voxLOXPTFluid.voxOffset(2f);
        voxLOXPTFluid = voxLOXPTFluid - voxLOXCrop;
        voxLOXPTWall = voxLOXPTWall - voxLOXCrop;

        GPort oIPAPT = new GPort("1/4in", pt_through_rad*2);
        BasePipe oIPAPTPipe = new BasePipe(aIPAPT, -fPortsHeight, 0f, pt_through_rad);
        Voxels voxIPAPTFluid = oIPAPT.voxConstruct(aIPAPT) + oIPAPTPipe.voxConstruct();
        Voxels voxIPAPTWall = voxIPAPTFluid.voxOffset(2f);
        voxIPAPTFluid = voxIPAPTFluid - voxIPACrop;
        voxIPAPTWall = voxIPAPTWall - voxIPACrop;

        GPort oChamberPT = new GPort("1/4in", pt_through_rad*2);
        BasePipe oChamberPTPipe = new BasePipe(aChamberPT, -fPortsHeight, 0f, pt_through_rad);
        Voxels voxChamberPTFluid = oChamberPT.voxConstruct(aChamberPT) + oChamberPTPipe.voxConstruct();
        Voxels voxChamberPTWall = voxChamberPTFluid.voxOffset(2f);

        voxFluids = voxLOXInletFluid + voxLOXPTFluid + voxIPAPTFluid + voxChamberPTFluid;
        Voxels voxWalls = voxLOXInletWall + voxLOXPTWall + voxIPAPTWall + voxChamberPTWall
            - voxCropBoxZ(fPortsHeight) - voxCropBoxZ(0f, 100f, -10f);

        return voxWalls;
    }

    protected Voxels voxels_bolts()
    {
        Voxels bolt_holes = new();
        for (int i=0; i<pm.no_bolt; i++)
        {
            Vector3 bolt_pos = points_bolts![i];
            BasePipe oBoltHole = new BasePipe(
                new LocalFrame(bolt_pos),
                2*pm.inj_flange_thickness,
                0f,
                pm.Bsz_bolt/2);

            BasePipe oWasherHole = new BasePipe(
                new LocalFrame(bolt_pos+new Vector3(0f,0f,pm.inj_flange_thickness)),
                2*pm.inj_flange_thickness,
                0f,
                pm.Or_washer+0.5f);

            bolt_holes += oBoltHole.voxConstruct() + oWasherHole.voxConstruct();
        }
        return bolt_holes;
    }

    protected Voxels voxels_oring_grooves()
    {
        Voxels oring_inner = new BasePipe(
            new LocalFrame(),
            pm.Lz_Ioring,
            pm.Ir_Ioring,
            pm.Or_Ioring
        ).voxConstruct();

        Voxels oring_outer = new BasePipe(
            new LocalFrame(),
            pm.Lz_Ooring,
            pm.Ir_Ooring,
            pm.Or_Ooring
        ).voxConstruct();

        return oring_inner + oring_outer;
    }

    protected Voxels voxels_supports()
    {
        float r_inner = r_fc[0] + 1f;
        float r_outer = Ir_chnl;
        float length = r_outer - r_inner;
        float aspect_ratio = 4f;
        float width = length / aspect_ratio;

        List<Vector3> points_supports = circularly_distributed(
            no_fc[0],
            0f,
            r_inner,
            PI/no_fc[0]
        );
        Voxels supports = new();
        foreach (Vector3 point in points_supports)
        {
            Frame support_frame = new(point, point, uZ3);
            supports += new Voxels(Polygon.mesh_extruded(
                support_frame,
                fMaxAtticZ,
                [
                    new (0, 0),
                    new (length/2, width/2),
                    new (length, 0),
                    new (length/2, -width/2),
                ]
            ));
        }

        supports = supports & cone_roof_lower_crop();

        return supports;
    }

    // private helpers
    private List<Vector3> circularly_distributed(int no, float z, float r, float theta0)
    {
        List<Vector3> points = new();
        for (int i=0; i<no; ++i)
            points.Add(fromcyl(r, theta0 + i*TWOPI/no, z));
        return points;
    }

    private Voxels cone_roof_upper_crop()
    {
        BaseLens oCrop = new BaseLens(new LocalFrame(), 1f, 0f, Or_chnl);
        oCrop.SetHeight(new SurfaceModulation(-100f), new SurfaceModulation(fGetAtticLowerHeight));
        return oCrop.voxConstruct();
    }

    private Voxels cone_roof_lower_crop()
    {
        BaseLens oCrop = new BaseLens(new LocalFrame(), 1f, 0f, Or_chnl);
        oCrop.SetHeight(new SurfaceModulation(-100f), new SurfaceModulation(fGetConeRoofLowerHeight));
        return oCrop.voxConstruct();
    }

    private Voxels cone_roof_lox_crop()
    {
        BaseLens oCrop = new BaseLens(new LocalFrame(), 1f, 0f, Or_chnl);
        oCrop.SetHeight(new SurfaceModulation(fGetConeRoofUpperHeight), new SurfaceModulation(fGetAtticLowerHeight));
        return oCrop.voxConstruct();
    }

    private Voxels ext_inner_cone_crop()
    {
        float fGetInnerCone(float fPhi, float fLengthRatio)
        {
            float fRadius = fLengthRatio * Or_chnl;
            float fConeOffset = sw_l_n1+sw_delta_l_n - sw_R_1*tan(fPrintAngle); // set offset same as coneroof
            float fInnerWallZ = fRadius*tan(fPrintAngle) + fConeOffset;
            return fInnerWallZ;
        }

        BaseLens oCrop = new BaseLens(new LocalFrame(), 1f, 0f, Or_chnl);
        oCrop.SetHeight(new SurfaceModulation(fGetInnerCone), new SurfaceModulation(40f));
        return oCrop.voxConstruct();
    }

    private Voxels voxCropBoxZ(float fZ, float fR=100, float fH=100)
    {
        BaseCylinder oCropCyl = new BaseCylinder(new LocalFrame(new Vector3(0f, 0f, fZ)), fH, fR);
        return oCropCyl.voxConstruct();
    }

    private float fGetConeRoofSurfaceModulation(float fPhi, float fLengthRatio)
    {
        float fRadius = fLengthRatio * Or_chnl;
        float fZVal = (Or_chnl - fRadius)*tan(fPrintAngle) + fInjectorPlateThickness;  // outer wall case
        // calculate vertical offset required, remembering delta-l! (for now use cone mid-plane)
        float fConeOffset = sw_l_n1+sw_delta_l_n - sw_R_1*tan(fPrintAngle);
        List<Vector3> aAllPoints = new List<Vector3>(points_inj!) { new Vector3(0f, 0f, 0f) };

        foreach (Vector3 aPoint in aAllPoints)
        {
            float fPointPhi = atan2(aPoint.Y, aPoint.X);
            float fPointRad = aPoint.R();

            float fTrialZ = sqrt((fRadius*fRadius)+(fPointRad*fPointRad)
                -(2*fRadius*fPointRad*cos(fPhi-fPointPhi)))*tan(fPrintAngle)
                + fConeOffset;
            if (fTrialZ < fZVal) { fZVal = fTrialZ; }
        }

        return fZVal;
    }

    private float fGetConeRoofUpperHeight(float fPhi, float fLengthRatio)
    {
        return fGetConeRoofSurfaceModulation(fPhi, fLengthRatio) + cos(fPrintAngle)*fRoofThickness;
    }

    private float fGetConeRoofLowerHeight(float fPhi, float fLengthRatio)
    {
        return fGetConeRoofSurfaceModulation(fPhi, fLengthRatio);
    }

    private float fGetAtticLowerHeight(float fPhi, float fLengthRatio)
    {
        float fRadius = fLengthRatio * Or_chnl;
        float fConeOffset = sw_l_n1+sw_delta_l_n - sw_R_1*tan(fPrintAngle); // set offset same as coneroof
        float fInnerWallZ = fRadius*tan(fPrintAngle) + fConeOffset;
        float fOuterWallZ = (Or_chnl - fRadius)*tan(fPrintAngle) + fInjectorPlateThickness;
        return min(fInnerWallZ, fOuterWallZ);
    }

    private float fGetAtticUpperHeight(float fPhi, float fLengthRatio)
    {
        return fGetAtticLowerHeight(fPhi, fLengthRatio) + cos(fPrintAngle)*8f; //TODO:fix magic number attic WT
    }

    public Voxels voxels()
    {

        Thread.Sleep(3000);
        Voxels part = new();
        report();

        /* create overall bounding box to size screenshots. */
        float overall_Lr = pm.Mr_bolt
                         + pm.Bsz_bolt/2f
                         + pm.thickness_around_bolt;
        float overall_Lz = 54f; // approx total height
        const float EXTRA = 10f;
        float overall_Mz = overall_Lz/2f - EXTRA;
        BBox3 overall_bbox = new(
            new Vector3(-overall_Lr, -overall_Lr, overall_Mz),
            new Vector3(+overall_Lr, +overall_Lr, overall_Mz + overall_Lz/2f)
        );
        // Since we're actually viewing a pipe, not a box, scale down a little.
        // overall_bbox.Grow(0.1f);

        // float lookat_theta = torad(135f);
        // float lookat_phi = torad(105f);
        float lookat_theta = torad(180f);
        float lookat_phi = torad(90f);
        Geez.lookat(overall_bbox, lookat_theta, lookat_phi);
        Geez.lookat(zoom: 95);

        void screenshot(string name) {
            using (Geez.remember_current_layout()) {
                using var _ = Geez.ViewerHack.locked();

                Geez.lookat(overall_bbox, lookat_theta, lookat_phi);
                Geez.background_colour = new ColorFloat("#FFFFFF");
                Geez.screenshot(name);
            }
        }

        Geez.Cycle key_part = new();
        Geez.Cycle key_plate = new(colour: COLOUR_CYAN);
        Geez.Cycle key_cone_roof = new(colour: COLOUR_PINK);
        Geez.Cycle key_gusset = new(colour: COLOUR_YELLOW);
        Geez.Cycle key_flange = new(colour: COLOUR_BLUE);
        Geez.Cycle key_asi = new(colour: COLOUR_WHITE);
        Geez.Cycle key_attic = new(colour: COLOUR_GREEN);
        Geez.Cycle key_inj_elements = new(colour: COLOUR_RED);
        Geez.Cycle key_ports = new(colour: Cp.clrRandom());
        Geez.Cycle key_supports = new(colour: Cp.clrRandom());

        Voxels voxInjectorPlate = voxels_injector_plate(out Voxels fc_fluid);
        part += voxInjectorPlate;
        using (key_plate.like())
            key_plate <<= Geez.voxels(voxInjectorPlate);
        Library.Log("created plate.");
        screenshot("injector_plate.png");

        Voxels cone_roof = voxels_cone_roof();
        part += cone_roof;
        using (key_cone_roof.like())
            key_cone_roof <<= Geez.voxels(cone_roof);
        Library.Log("created cone roof.");
        screenshot("injector_cone_roof.png");

        Voxels attic = voxels_attic();
        part += attic;
        using (key_attic.like())
            key_attic <<= Geez.voxels(attic);
        using (key_cone_roof.like())
            key_cone_roof <<= Geez.voxels(cone_roof - attic);
        Library.Log("created attic.");
        screenshot("injector_attic.png");

        Voxels supports = voxels_supports();
        part += supports;
        using (key_supports.like())
            key_supports <<= Geez.voxels(supports - voxInjectorPlate - cone_roof - attic);
        Library.Log("created supports.");
        screenshot("injector_supports.png");

        Voxels asi = voxels_asi(out Voxels asi_fluid, out Voxels stainless_tube);
        part += asi;
        using (key_attic.like())
            key_attic <<= Geez.voxels(attic - asi);
        using (key_cone_roof.like())
            key_cone_roof <<= Geez.voxels(cone_roof - asi);
        using (key_asi.like())
            key_asi <<= Geez.voxels(asi - asi_fluid);
        using (key_plate.like())
            key_plate <<= Geez.voxels(voxInjectorPlate - asi);
        Library.Log("created ASI.");
        screenshot("injector_asi.png");

        Voxels flange = voxels_flange();
        part += flange;
        using (key_flange.like())
            key_flange <<= Geez.voxels(flange);
        using (key_cone_roof.like())
            key_cone_roof <<= Geez.voxels(cone_roof - flange);
        using (key_attic.like())
            key_attic <<= Geez.voxels(attic - flange);
        Library.Log("created flange.");
        screenshot("injector_flange.png");

        Voxels gussets = voxels_gussets(out Voxels voxStrutHoles);
        part += gussets;
        using (key_gusset.like())
            key_gusset <<= Geez.voxels(gussets - voxStrutHoles);
        using (key_attic.like())
            key_attic <<= Geez.voxels(attic - gussets - voxStrutHoles);
        using (key_cone_roof.like())
            key_cone_roof <<= Geez.voxels(cone_roof - gussets - voxStrutHoles);
        Library.Log("created gussets.");
        screenshot("injector_gussets.png");

        Voxels inj_elements = vox_inj_elements(out Voxels voxOxPostFluid, out Voxels swirl_inlets, out Voxels cone_roof_crop);
        part += inj_elements;
        part -= voxOxPostFluid;
        // part -= cone_roof_crop;
        part -= swirl_inlets;
        using (key_inj_elements.like())
            key_inj_elements <<= Geez.voxels(inj_elements - voxOxPostFluid);
        using (key_cone_roof.like())
            key_cone_roof <<= Geez.voxels(cone_roof - inj_elements - voxOxPostFluid - cone_roof_crop);
        Library.Log("created injector elements.");
        screenshot("injector_injector_elements.png");

        Voxels ports = voxels_ports(out Voxels ports_fluids);
        part += ports;
        using (key_ports.like())
            key_ports <<= Geez.voxels(ports - ports_fluids);
        using (key_attic.like())
            key_attic <<= Geez.voxels(attic - ports_fluids);
        using (key_gusset.like())
            key_gusset <<= Geez.voxels(gussets - ports_fluids);
        using (key_plate.like())
            key_plate <<= Geez.voxels(voxInjectorPlate - ports_fluids);
        Library.Log("created ports.");
        screenshot("injector_ports.png");

        // external fillets
        Voxels voxCropZone = part - cone_roof_upper_crop() - voxInnerPipeCrop!;
        Voxels voxNoCropZone = part & (cone_roof_upper_crop() + voxInnerPipeCrop!);
        voxCropZone.OverOffset(5f);
        voxCropZone.voxBoolSubtract(voxNoCropZone);
        part = voxCropZone + voxNoCropZone;

        Voxels inner_cone_fillet = part & ext_inner_cone_crop();
        inner_cone_fillet.OverOffset(5f);
        part += inner_cone_fillet;

        Library.Log("created external fillets.");

        // internal fillets
        Voxels asi_base_crop = new Pipe(
            new Frame().transz(fInjectorPlateThickness/2f),
            15,
            15
        ).voxels();
        Voxels asi_base_fillet = part & asi_base_crop;
        asi_base_fillet.OverOffset(3f);
        part += asi_base_fillet;

        Voxels asi_middle_crop = new Pipe(
            new Frame().transz(10f),
            10f,
            15f
        ).voxels();
        Voxels asi_middle_fillet = part & asi_middle_crop;
        asi_middle_fillet.OverOffset(1f);
        part += asi_middle_fillet;

        Library.Log("created internal fillets.");

        part -= ports_fluids;
        // part -= voxOxPostFluid;
        part -= swirl_inlets;
        part -= asi_fluid;
        part -= fc_fluid;
        part -= voxels_bolts();
        part -= voxStrutHoles;
        part -= voxels_oring_grooves();
        Library.Log("created bolt holes and O-ring grooves.");

        Geez.clear();
        key_part.voxels(part);
        screenshot("injector_part.png");


        Library.Log("Baby made.");
        print();

        return part;
    }

    public void report()
    {
        using (StreamWriter writer = new StreamWriter("exports/manufacturing_report.txt"))
        {
            writer.WriteLine("Manufacturing considerations:");
            writer.WriteLine($" - Annulus gap width: {fFuelAnnulusOR - (fCoreExitRadius + fLOxPostWT)} mm");
            writer.WriteLine($" - Annulus rib width: {annulus_rib_width} mm");
            writer.WriteLine($" - Core internal diameter: {2*fCoreExitRadius} mm");
            writer.WriteLine($" - LOx post wall thickness: {fLOxPostWT} mm");
        }
    }

    public void drawings(in Voxels part) {
        Geez.voxels(part);
        Cuboid bounds;

        Frame frame_xy = new(3f*VOXEL_SIZE*uZ3, uX3, uZ3);
        print("cross-sectioning xy...");
        Drawing.to_file(
            fromroot($"exports/injector_xy.svg"),
            part,
            frame_xy,
            out bounds
        );
        using (Geez.like(colour: COLOUR_BLUE))
            Geez.cuboid(bounds, divide_x: 3, divide_y: 3);

        Frame frame_yz = new(ZERO3, uY3, uX3);
        print("cross-sectioning yz...");
        Drawing.to_file(
            fromroot($"exports/injector_yz.svg"),
            part,
            frame_yz,
            out bounds);
        using (Geez.like(colour: COLOUR_GREEN))
            Geez.cuboid(bounds, divide_x: 3, divide_y: 4);

        print();
    }

    public void anything()
    {

    }

    public string name => "injector";

    public void set_modifiers(int mods) {
        if (mods == 0)
            return;
        throw new Exception("yeah nah dunno what it is");
        throw new Exception("honestly couldnt tell you");
        throw new Exception("what is happening right now");
                          // im selling out
        // note chamber has the following fields and sets them like so:
        // filletless       = popbits(ref mods, TPIAP.FILLETLESS);
        // cutaway          = popbits(ref mods, TPIAP.CUTAWAY);
        // minimise_mem     = popbits(ref mods, TPIAP.MINIMISE_MEM);
        // take_screenshots = popbits(ref mods, TPIAP.TAKE_SCREENSHOTS);
        // assert(mods == 0, $"unrecognised modifiers: 0x{mods:X}");
    }

    public void initialise()
    {
        initilise_biswirl();
        // initialise_inj(); // depreciated
        initialise_fc();
        initialise_chnl();
        initialise_construction();
    }

}