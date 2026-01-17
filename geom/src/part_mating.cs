
public class PartMating {
    // X_cc = combustion chamber property.
    // X_chnl = combustion chamber cooling channel property.
    // X_chnl = combustion chamber cooling channel property.
    // X_Ioring = inner O-ring property.
    // X_Ooring = outer O-ring property.
    // X_bolt = bolt property.
    // X_washer = washer property.
    // X_LOx = liquid oxygen (oxidiser) property.
    // X_IPA = isopropyl alcohol (fuel) property.

    /* Required geometric parameters: */

    public required float Or_cc { get; init; }

    public required float Mr_chnl { get; init; }
    public required float min_wi_chnl { get; init; }

    public required float Ir_Ioring { get; init; }
    public required float Ir_Ooring { get; init; }
    public required float Or_Ioring { get; init; }
    public required float Or_Ooring { get; init; }
    public required float Lz_Ioring { get; init; }
    public required float Lz_Ooring { get; init; }

    public required int no_bolt { get; init; }
    public required float Mr_bolt { get; init; }
    public required float Bsz_bolt { get; init; }
    public required float Bln_bolt { get; init; }
    public required float Or_washer { get; init; }

    /* Non-geometric parameters: */
    public required float P_cc { get; init; }
    public required float mdot_LOx { get; init; }
    public required float mdot_IPA { get; init; }
    public required float rho_LOx { get; init; }
    public required float rho_IPA { get; init; }

    /* For purely visual matches: */
    public required float thickness_around_bolt { get; init; }
    public required float flange_thickness_cc { get; init; }
    public required float flange_thickness_inj { get; init; }
    public required float flange_outer_radius { get; init; }
    public required float flange_fillet_radius { get; init; }
}
