using static Br;
using br;

using Voxels = PicoGK.Voxels;

namespace br {

public class Fillet {
    static public Voxels concave(in Voxels vox, float Fr, bool inplace=false) {
        Voxels v = inplace ? vox : vox.voxDuplicate();
        v.DoubleOffset(Fr, -Fr);
        return v;
    }
    static public Voxels convex(in Voxels vox, float Fr, bool inplace=false) {
        Voxels v = inplace ? vox : vox.voxDuplicate();
        v.DoubleOffset(-Fr, Fr);
        return v;
    }
    static public Voxels both(in Voxels vox, float Fr, bool inplace=false) {
        return both(vox, Fr, Fr, inplace);
    }
    static public Voxels both(in Voxels vox, float concave_Fr, float convex_Fr,
            bool inplace=false) {
        Voxels v = inplace ? vox : vox.voxDuplicate();
        v.DoubleOffset(concave_Fr, -concave_Fr - convex_Fr);
        v.Offset(convex_Fr);
        return v;
    }
}

}
