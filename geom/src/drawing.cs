using static br.Br;

using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using ImageGrayScale = PicoGK.ImageGrayScale;
using PolySlice = PicoGK.PolySlice;

namespace br {

public static class Drawing {
    public static void to_file(string path, Voxels vox, in Frame slice_at) {
        float voxel_size = PicoGK.Library.fVoxelSizeMM;

        Transformer trans = Transformer.to_local(slice_at);
        vox = trans.voxels(vox);

        vox.GetVoxelDimensions(
            out int origin_x,
            out int origin_y,
            out int origin_z,
            out int size_x,
            out int size_y,
            out int size_z
        );
        assert(size_x > 0, "cannot be empty");
        assert(size_y > 0, "cannot be empty");
        assert(size_z > 0, "cannot be empty");
        Vec3 origin = voxel_size * new Vec3(origin_x, origin_y, origin_z);
        assert(origin_z <= 0, "slice outside voxels?");
        assert(-origin_z < size_z, "slice outside voxels?");

        ImageGrayScale img = new(size_x, size_y);
        vox.GetVoxelSlice(-origin_z, ref img, Voxels.ESliceMode.SignedDistance);

        PolySlice svg = PolySlice.oFromSdf(img, 0f, projxy(origin), voxel_size);
        svg.SaveToSvgFile(path, false);
    }
}

}
