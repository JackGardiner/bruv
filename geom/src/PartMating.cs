
public class PartMating {
/* Cheeky parts interface diagram.

                    ,- fuel channel
   cc wall -,       '
            '      : :      :__|  |_
___________________| |_________|  |_|   ___,- this plane is top of cc
            | '-'  | |  '-' ,--|  |-'
            :      : :      :
               ,         ,      ,
 face sealing -'---------'      '-- bolt
      o-rings

*/

    /* Required for realisable mate: */

    public required float r_cc { get; init; }

    public required float r_channel { get; init; }
    public required float min_wi_channel { get; init; }

    public required float Ir_Ioring { get; init; }
    public required float Ir_Ooring { get; init; }
    public required float Or_Ioring { get; init; }
    public required float Or_Ooring { get; init; }
    public required float Lz_Ioring { get; init; }
    public required float Lz_Ooring { get; init; }
    // note these are the groove dimensions, not the o-ring itself.

    // bolt positions are cyclically symmetry about Z, one bolt lies on +X axis.
    public required int no_bolt { get; init; }
    public required float r_bolt { get; init; }
    public required float Bsz_bolt { get; init; }
    public required float Bln_bolt { get; init; }


    /* NOT strictly required for realisable mate: */

    public required float thickness_around_bolt { get; init; }
    public required float flange_thickness { get; init; }
    public required float flange_outer_radius { get; init; }
    public required float radial_fillet_radius { get; init; }
    public required float axial_fillet_radius { get; init; }
}
