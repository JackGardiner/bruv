using static br.Br;
using PicoGK;
using br;
using TPIAP = TwoPeasInAPod;
using System.Numerics;
using Calculations;
using Leap71.ShapeKernel;

public class InjectorSample : TPIAP.Pea {

    public string name => "injector_sample";

    public void anything() { throw new NotImplementedException(); }
    public Voxels? cutaway(in Voxels part) { throw new NotImplementedException(); }
    public void drawings(in Voxels part) { throw new NotImplementedException(); }
    public void set_modifiers(int mods) {
        _ = popbits(ref mods, TPIAP.LOOKIN_FANCY);
        assert(mods == 0);
    }

    public Voxels? voxels()
    {
        // inputs
        float fTargetdPFrac = 0.35f; // target pressure drop fraction across injector
        float fChamberPressure = 20e5f; // chamber pressure in Pa
        float fOxMassFlowRate = 1.2f; // oxidizer mass flow rate in kg/s
        float fFuelMassFlowRate = 0.8f; // fuel mass flow rate in kg/s
        int iTotalElementCount = 8; // total number of injector elements

        float fTargetdP = fTargetdPFrac*fChamberPressure;
        float dP_i1 = fTargetdP;
        float dP_i2 = fTargetdP;
        print($"foxmassflow: {fOxMassFlowRate}, itotalelementcount: {iTotalElementCount}");
        float mdot_i1 = fOxMassFlowRate / iTotalElementCount;
        float mdot_i2 = fFuelMassFlowRate / iTotalElementCount;

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

        float fFuelInjectionRho = 786.0f;
        float fOxInjectionRho = 1141.0f;

        int n_1 = 3; // number of elements per stage
        int n_2 = 3;

        //   stage 1 (inner core) design
        float A_1 = GraphLookup.GetA(todeg(twoalpha_1), lbar_n1);
        float mu_1 = GraphLookup.GetFlowCoefficient(A_1, C_1);

        print($"mu_1: {mu_1}, mdot_i1: {mdot_i1}, dP_i1: {dP_i1}, pm.fOxInjectionRho: {fOxInjectionRho}");
        float R_n1 = 0.475f * sqrt(mdot_i1/(mu_1*sqrt(fOxInjectionRho*dP_i1))) * 1e3f; // mm
        float R_in1 = Rbar_in1 * R_n1;
        float r_in1 = sqrt(R_in1*R_n1/(n_1*A_1));

        float Re_in1 = 2*mdot_i1/(PI*sqrt(n_1)*(r_in1*1e-3f)*fOxInjectionRho*nu_LOX); // dimensionless

        float l_in1 = 4f   * r_in1;  // tangential passage length
        float l_n1  = 2f   * R_n1;   // nozzle length
        float l_s1  = 2.5f * R_in1;  // vortex chamber length

        float del_w = 0.8f;  // nozzle wall thickness
        float R_1 = R_n1 + del_w; // external radius of nozzle
        float rm_bar_1 = GraphLookup.GetRelativeVortexRadius(A_1, Rbar_in1);

        // --- stage 2 (outer annulus) design ---
        float r_m2_req = R_1 + 0.3f;  // Required physical gas-vortex radius (constant)
        float R_n2 = r_m2_req;        // Initial guess for nozzle radius

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
            mu_2 = (mdot_i1+mdot_i2) / (PI * pow(R_n2*1e-3f, 2) * sqrt(2*fFuelInjectionRho*dP_i2));

            // 2. Step 3: Find A based on mu (from Fig 34 correlations)
            A_2 = GraphLookup.GetAFromMu(mu_2, C_2);

            // 3. Step 3: Find dimensionless relative vortex radius (from Fig 35)
            rm_bar_2 = GraphLookup.GetRelativeVortexRadius(A_2, Rbar_in2);

            // 4. Step 4: Calculate NEW physical nozzle radius
            // Formula: Rn = r_m_physical / r_m_relative
            float R_n2_new = r_m2_req / rm_bar_2;

            diff = Math.Abs(R_n2_new - R_n2);

            R_n2 = R_n2_new;
            iter++;
        }

        float R_in2 = Rbar_in2 * R_n2;
        float r_in2 = sqrt(R_in2*R_n2/(n_2*A_2));
        float Re_in2 = 2*mdot_i2/(PI*sqrt(n_2)*(r_in2*1e-3f)*fFuelInjectionRho*nu_IPA); // dimensionless
        // spray cone angle
        float two_alpha_2 = torad(GraphLookup.GetSprayConeAngle(A_2, lbar_n2));
        float two_alpha = two_alpha_2 - torad(35);
        float tau_i = 0.15e-3f;

        float K_m = mdot_i1/mdot_i2;
        float phi_1 = 1f - pow(rm_bar_1, 2);
        float phi_2 = 1f - pow(rm_bar_2, 2);

        // print inputs to l_mix calc
        print($"K_m: {K_m}, mu_2: {mu_2}, phi_2: {phi_2}, dP_i2: {dP_i2}, pm.fFuelInjectionRho: {fFuelInjectionRho}");
        print($"mu_1: {mu_1}, phi_1: {phi_1}, dP_i1: {dP_i1}, pm.fOxInjectionRho: {fOxInjectionRho}");


        float l_mix = sqrt(2)*tau_i*(
            (K_m*mu_2/(phi_2*(K_m+1)) * sqrt(dP_i2/fFuelInjectionRho))
            + (mu_1/(phi_1*(K_m+1)) * sqrt(dP_i1/fOxInjectionRho))
        ) * 1e3f;

        float l_n2 = 2f*lbar_n2*R_n2; // stage 2 nozzle length

        float delta_l_n = l_n2 - l_mix;

        using (StreamWriter writer = new StreamWriter("exports/inj_sample_report.txt"))
        {
            writer.WriteLine("Injector Report");
            writer.WriteLine("===============");
            writer.WriteLine();
            writer.WriteLine($"Total elements count: {iTotalElementCount}");
            writer.WriteLine();
            writer.WriteLine("Stage 1 (LOX):");
            writer.WriteLine($"  - Nozzle inner radius: {R_n1} mm");
            writer.WriteLine($"  - Nozzle Reynolds number: {Re_in1}");
            writer.WriteLine($"  - Nozzle outer radius: {R_1} mm");
            writer.WriteLine($"  - A_1: {A_1}");
            writer.WriteLine($"  - Rbar_in1: {Rbar_in1}");
            writer.WriteLine($"  - rm_bar_1: {rm_bar_1}");
            writer.WriteLine($"  - Tangential inlet radius: {r_in1} mm");
            writer.WriteLine($"  - phi_1: {phi_1}");
            writer.WriteLine($"  - Reynolds number at tangential inlet: {Re_in1}");
            writer.WriteLine();
            writer.WriteLine("Stage 2 (IPA):");
            writer.WriteLine($"  - Nozzle outer radius: {R_n2} mm");
            writer.WriteLine($"  - Nozzle Reynolds number: {Re_in2}");
            writer.WriteLine($"  - A_2: {A_2}");
            writer.WriteLine($"  - Mixing length: {l_mix} mm");
            writer.WriteLine($"  - Annulus radial gap: {R_n2 - R_1} mm");
            writer.WriteLine($"  - Nozzle length: {l_n2} mm");
            writer.WriteLine($"  - Tangential inlet radius: {r_in2} mm");
            writer.WriteLine($"  - delta_L (inset length) {delta_l_n} mm");
            writer.WriteLine($"  - phi_2: {phi_2}");
            writer.WriteLine($"  - Reynolds number at tangential inlet: {Re_in2}");
            writer.WriteLine();
        }


        // construction
        print("her2");

        Voxels output = new();

        output.BoolAdd(new Rod(
            new Frame().transz(delta_l_n),
            l_n1,
            R_n1,
            R_1
        ));

        output.BoolAdd(new Cone(
            new Frame().transz(delta_l_n + l_n1),
            (Rbar_in1*R_n1) - R_n1,
            R_n1,
            R_1,
            Rbar_in1*R_n1,
            Rbar_in1*R_n1 + del_w
        ));

        output.BoolAdd(new Rod(
            new Frame().transz(delta_l_n + l_n1 + (Rbar_in1*R_n1) - R_n1),
            Rbar_in1*R_n1,
            Rbar_in1*R_n1 + del_w
        ));

        output.BoolAdd(new Cone(
            new Frame().transz(delta_l_n + l_n1 + (Rbar_in1*R_n1) - R_n1 + Rbar_in1*R_n1),
            0f,
            0f,
            0f
        ));

        Geez.voxels(output);

        return output;
    }
}

