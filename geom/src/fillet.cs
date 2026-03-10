using static br.Br;

using Voxels = PicoGK.Voxels;

namespace br {

public static class Fillet {
    public static Voxels concave(in Voxels vox, float FR, bool inplace=false) {
        Voxels v = inplace ? vox : vox.voxDuplicate();
        v.DoubleOffset(FR, -FR);
        return v;
    }
    public static Voxels convex(in Voxels vox, float FR, bool inplace=false) {
        Voxels v = inplace ? vox : vox.voxDuplicate();
        v.DoubleOffset(-FR, FR);
        return v;
    }
    public static Voxels both(in Voxels vox, float FR, bool inplace=false) {
        return both(vox, FR, FR, inplace);
    }
    public static Voxels both(in Voxels vox, float concave_FR, float convex_FR,
            bool inplace=false) {
        Voxels v = inplace ? vox : vox.voxDuplicate();
        if (concave_FR == convex_FR) {
            v.TripleOffset(-concave_FR); // maintain out->in->return order.
        } else {
            v.DoubleOffset(concave_FR, -concave_FR - convex_FR);
            v.Offset(convex_FR);
        }
        return v;
    }
}

}
