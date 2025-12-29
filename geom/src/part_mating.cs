
public class PartMating {
    /* Cheeky parts interface diagram.
                    ,- fuel channel
       cc wall -,      '
                '      : :       :__|  |_
    ___________________| |__________|  |_|  ___,- this plane is top of cc
                | '-'  | |   '-' ,--|  |-'
                :      : :      :
                   ,         ,      ,
     face sealing -'---------'      '-- bolt
          o-rings
    */

    // --- Required for realisable mate: ---

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

    // --- Descriptive geometry ---

    // --- Physics/Inlet Data ---
    public required float fChamberPressure { get; init; }
    public required float fOxMassFlowRate { get; init; }
    public required float fFuelMassFlowRate { get; init; }
    public required float fOxInjectionRho { get; init; }

    // --- Design Preference Params ---
    public required float thickness_around_bolt { get; init; }
    public required float flange_thickness { get; init; }
    public required float inj_flange_thickness { get; init; }

    public required float flange_outer_radius { get; init; }
    public required float radial_fillet_radius { get; init; }
    public required float axial_fillet_radius { get; init; }
}