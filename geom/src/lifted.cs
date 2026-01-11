using static br.Br;

using Voxels = PicoGK.Voxels;

namespace br {

public class Lifted : IDisposable {
    public Voxels from { get; } /* must not be modified over lifted lifetime. */
    public Voxels mask { get; } /* must not be modified over lifted lifetime. */
    public Voxels vox { get; set; }

    public Lifted(in Voxels from, in Voxels mask) {
        this.from = from;
        this.mask = mask;
        vox = from.voxBoolIntersect(mask);
    }

    public void Dispose() {
        from.BoolSubtract(mask.voxOffset(-VOXEL_SIZE));
        vox.BoolIntersect(mask);
        from.BoolAdd(vox);
        vox.Dispose();
    }
}

}
