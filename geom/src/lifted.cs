using static br.Br;

using Voxels = PicoGK.Voxels;

namespace br {

public class Lifted : IDisposable {
    protected Voxels from { get; }
    public Voxels vox { get; }

    public Lifted(in Voxels from, in Voxels mask) {
        this.from = from;
        this.vox = from.voxBoolIntersect(mask);
    }

    public void Dispose() {
        from.BoolAdd(vox);
    }
}

}
