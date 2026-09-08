/* OD skin vent slits: no_perf straight axial slots, W_perf wide, cut through
   the skin only. W_perf = 0 for none. */

using static br.Br;
using br;

using Vec3 = System.Numerics.Vector3;
using Voxels = PicoGK.Voxels;

public partial class Grain {

    /* SLITS */
    public required int no_perf { get; init; } // 2 or 4.
    public required float W_perf { get; init; } // slot width. 0 for none.
    public required float z0_perf { get; init; } // band bottom.
    public required float z1_perf { get; init; } // band top. 0 = full height.
    public required float theta0_perf { get; init; } // first slot, from +x.

    // full height is the finished puck, not the printed length.
    public float z1_perf_or_top => (z1_perf > 0f) ? z1_perf : L_puck;

    public float A_perf => no_perf*W_perf*(z1_perf_or_top - z0_perf);
    public float frac_perf => A_perf/A_skin;

    protected void assert_slits() {
        assert(no_perf >= 0, $"no_perf={no_perf}");
        assert(W_perf >= 0f, $"W_perf={W_perf}");
        if (W_perf <= 0f || no_perf <= 0)
            return;

        // Only 2- and 4-fold survive the lattice's screw symmetry.
        assert(no_perf == 2 || no_perf == 4,
                $"no_perf={no_perf}: only 2 or 4 stay equivalent under the "
                + "lattice's 4_1 screw symmetry");

        assert(z0_perf >= 0f, $"z0_perf={z0_perf}");
        assert(z1_perf_or_top > z0_perf,
                $"z1_perf={z1_perf}, z0_perf={z0_perf}");
        assert(z1_perf_or_top <= L_print, $"z1_perf={z1_perf}");

        if (W_perf > 2.0f) {
            print($"WARNING: W_perf={W_perf}mm exceeds the 2.0mm that was");
            print( "         measured clear of the sheet at the cut radius.");
            print( "         It will bite into the lattice.");
            print();
        }

        float across = W_perf/VOXEL_SIZE;
        if (across < 3f) {
            print($"WARNING: only {across:F1} voxels across the {W_perf}mm");
            print( "         slit. It will not resolve properly.");
            print();
        }
    }

    protected Voxels? slits() {
        if (W_perf <= 0f || no_perf <= 0)
            return null;

        float z0 = z0_perf;
        float Lz = z1_perf_or_top - z0;
        // one voxel inside the skin to one past the OD, no membrane.
        float r0 = R_skin - VOXEL_SIZE;
        float r1 = R_grain + VOXEL_SIZE;

        Voxels? cut = new();
        for (int i=0; i<no_perf; ++i) {
            float theta = theta0_perf + i*TWOPI/no_perf;
            // Bar's frame is its base, centred in x and y.
            Frame f = new(fromcyl(0.5f*(r0 + r1), theta, z0),
                    fromcyl(1f, theta, 0f), uZ3);
            cut.BoolAdd(new Bar(f, r1 - r0, W_perf, Lz));
        }
        return cut;
    }
}
