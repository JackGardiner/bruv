using static br.Br;

using Voxels = PicoGK.Voxels;

namespace br {

public static class Fillet {
    public static Voxels concave(in Voxels vox, float Fr, bool inplace=false) {
        Voxels v = inplace ? vox : vox.voxDuplicate();
        v.DoubleOffset(Fr, -Fr);
        return v;
    }
    public static Voxels convex(in Voxels vox, float Fr, bool inplace=false) {
        Voxels v = inplace ? vox : vox.voxDuplicate();
        v.DoubleOffset(-Fr, Fr);
        return v;
    }
    public static Voxels both(in Voxels vox, float Fr, bool inplace=false) {
        return both(vox, Fr, Fr, inplace);
    }
    public static Voxels both(in Voxels vox, float concave_Fr, float convex_Fr,
            bool inplace=false) {
        Voxels v = inplace ? vox : vox.voxDuplicate();
        if (concave_Fr == convex_Fr) {
            v.TripleOffset(-concave_Fr); // maintain out->in->return order.
        } else {
            v.DoubleOffset(concave_Fr, -concave_Fr - convex_Fr);
            v.Offset(convex_Fr);
        }
        return v;
    }

    public static Voxels ring(Frame ring, float Ir, float Fr,
            float inset=0.05f) {
        float Dz = -inset*Fr;
        float Dr = -inset*Fr;
        ring = ring.transz(Dz);
        Ir += Dr;
        Voxels vox = new Rod(ring, Fr, Ir + Fr);
        vox.BoolSubtract(new Donut(ring.transz(Fr), Ir + Fr, Fr));
        return vox;
    }
}

}
