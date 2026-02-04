
public class PartMating {

    /* Required geometric parameters: */

    public required float R_cc { get; init; }

    public required float Mr_chnl { get; init; }
    public required float min_wi_chnl { get; init; }

    public required float IR_Ioring { get; init; }
    public required float IR_Ooring { get; init; }
    public required float OR_Ioring { get; init; }
    public required float OR_Ooring { get; init; }
    public required float Lz_Ioring { get; init; }
    public required float Lz_Ooring { get; init; }

    public required int no_bolt { get; init; }
    public required float r_bolt { get; init; }
    public required float D_bolt { get; init; }
    public required float D_washer { get; init; }

    /* Non-geometric parameters: */
    public required float P_cc { get; init; }
    public required float mdot_LOx { get; init; }
    public required float mdot_IPA { get; init; }
    public required float rho_LOx { get; init; }
    public required float rho_IPA { get; init; }

    /* Nice to agree on for purely visual matches: */
    public required float thickness_around_bolt { get; init; }
    public required float flange_thickness_cc { get; init; }
    public required float flange_thickness_inj { get; init; }
    public required float flange_outer_radius { get; init; }
    public required float flange_fillet_radius { get; init; }
}
