using static br.Br;

using Vec2 = System.Numerics.Vector2;

using Voxels = PicoGK.Voxels;
using ImageGrayScale = PicoGK.ImageGrayScale;
using PolySlice = PicoGK.PolySlice;

namespace br {

public static class Drawing {
    public static PolySlice as_poly_slice(Voxels vox, in Frame slice_at) {
        Transformer trans = new Transformer().to_local(slice_at);
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
        assert(origin_z <= 0, "slice outside voxels?");
        assert(-origin_z < size_z, "slice outside voxels?");

        ImageGrayScale img = new(size_x, size_y);
        vox.GetVoxelSlice(-origin_z, ref img, Voxels.ESliceMode.SignedDistance);

        Vec2 origin = VOXEL_SIZE * new Vec2(origin_x, origin_y);
        return PolySlice.oFromSdf(img, 0f, origin, VOXEL_SIZE);
    }

    public static void to_file(string path, Voxels vox, in Frame slice_at) {
        PolySlice slice = as_poly_slice(vox, slice_at);
        slice.SaveToSvgFile(path, false);
    }
}

}
