/* OD skin vent holes, the alternative to the slits (perf_mode = "holes").
   A regular staggered grid of teardrops, every hole kept. Rows are Lz_cell/4
   apart and the ring count is 2 mod 4, so the lattice's screw maps the grid
   onto itself. Teardrops so the roof never bridges flat. */

using static br.Br;
using br;

using Vec3 = System.Numerics.Vector3;
using Voxels = PicoGK.Voxels;

public partial class FuelGrain {

    /* HOLES */
    public required string perf_mode { get; init; } // "slits" or "holes".
    public required float D_hole { get; init; } // circle diameter. 0 for none.
    public required int no_hole_ring { get; init; } // holes per ring.
    public required float theta0_hole { get; init; } // grid phase, from +x.
    public required float edge_hole { get; init; } // min gap to either end.

    public const float MIN_D_HOLE = 4f;

    public bool slits_on => perf_mode == "slits" && W_perf > 0f && no_perf > 0;
    public bool holes_on => perf_mode == "holes" && D_hole > 0f;

    public float A_hole => sqed(0.5f*D_hole)*(0.75f*PI + 1f);
    public float pitch_z_hole => 0.25f*Lz_cell; // the screw's z step.
    public float pitch_t_hole => TWOPI*R_grain/no_hole_ring; // at the OD.
    public int no_hole => hole_grid().Length;

    // Open fraction of the skin over the finished length, whichever mode.
    public float frac_open => holes_on ? no_hole*A_hole/A_skin
                            : slits_on ? frac_perf
                            : 0f;


    protected void assert_holes() {
        assert(perf_mode == "slits" || perf_mode == "holes",
                $"perf_mode='{perf_mode}', expected 'slits' or 'holes'");
        assert(D_hole >= 0f, $"D_hole={D_hole}");
        if (!holes_on)
            return;

        assert(D_hole >= MIN_D_HOLE,
                $"D_hole={D_hole}mm is below the {MIN_D_HOLE}mm floor");
        assert(no_hole_ring >= 1, $"no_hole_ring={no_hole_ring}");
        float a = 0.5f*D_hole;
        assert(pitch_t_hole > D_hole,
                $"no_hole_ring={no_hole_ring} leaves no ligament between holes "
                + $"({pitch_t_hole:F2}mm pitch, {D_hole}mm holes)");
        assert(pitch_z_hole > a + a*SQRT2,
                $"rows {pitch_z_hole:F2}mm apart are under one hole's height");
        assert(edge_hole >= 0f, $"edge_hole={edge_hole}");

        if (no_hole_ring % 4 != 2) {
            print($"WARNING: no_hole_ring={no_hole_ring} is not 2 mod 4, so the");
            print( "         grid does not repeat with the lattice's screw and");
            print( "         each quarter of the part meets the sheet");
            print( "         differently. Still a regular grid.");
            print();
        }

        // the base shelf would refill a bottom row that reaches below it.
        var grid = hole_grid();
        if (grid.Length > 0) {
            double lowest = grid.Min(s => s.z) - a;
            if (lowest < th_shelf) {
                print($"WARNING: the bottom row of holes reaches z={lowest:F2}mm,");
                print($"         below the {th_shelf}mm base shelf, so the base");
                print( "         variant will fill part of it back in. Raise");
                print( "         edge_hole.");
                print();
            }
        }

        print($"holes: {no_hole} ({no_hole_ring} per ring, rows "
                + $"{pitch_z_hole:F2}mm apart), {frac_open:P2} of the skin open.");
        print();
    }


    // every hole centre (theta, z). Mirrors grainfield.hole_grid().
    protected (double th, double z)[]? _hole_grid = null;
    protected (double th, double z)[] hole_grid() {
        if (_hole_grid is not null)
            return _hole_grid;
        double a = 0.5*D_hole;
        int n_t = no_hole_ring;
        double dth = 2.0*Math.PI/n_t;
        double pitch = 0.25*Lz_cell;
        double z_lo = edge_hole + a;
        double z_hi = L_puck - edge_hole - a*Math.Sqrt(2.0);
        List<(double, double)> out_ = new();
        if (z_hi >= z_lo) {
            int n_z = (int)Math.Floor((z_hi - z_lo)/pitch) + 1;
            double z_first = z_lo + 0.5*((z_hi - z_lo) - (n_z - 1)*pitch);
            for (int k=0; k<n_z; ++k) {
                double off = (k % 2 == 1) ? 0.5*dth : 0.0;
                for (int j=0; j<n_t; ++j)
                    out_.Add((theta0_hole + j*dth + off, z_first + k*pitch));
            }
        }
        _hole_grid = out_.ToArray();
        return _hole_grid;
    }


    /* CONSTRUCTION */

    // same radial span as the slits.
    protected Voxels? holes() {
        if (!holes_on)
            return null;
        float a = 0.5f*D_hole;
        float r0 = R_skin - VOXEL_SIZE;
        float r1 = R_grain + VOXEL_SIZE;

        Voxels cut = new();
        foreach (var (thd, zd) in hole_grid()) {
            float th = (float)thd, zc = (float)zd;
            Vec3 er = fromcyl(1f, th, 0f);
            Vec3 et = fromcyl(1f, th + PI_2, 0f);
            Vec3 at = fromcyl(r0, th, zc);
            // circle: a rod along the outward radial.
            cut.BoolAdd(new Rod(new Frame(at, et, er), r1 - r0, a));
            // cap: a square turned 45 deg, corners on centre and apex.
            Vec3 X = normalise(et + uZ3);
            cut.BoolAdd(new Bar(new Frame(at + a*SQRTH*uZ3, X, er),
                    a, a, r1 - r0));
        }
        return cut;
    }
}
