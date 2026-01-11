using static br.Br;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using ImageGrayScale = PicoGK.ImageGrayScale;
using PolySlice = PicoGK.PolySlice;

namespace br {

public static class Drawing {
    public static PolySlice slice(Voxels vox, Frame slice_at,
                out Bar bounds) {
        return slice(new Mesh(vox), slice_at, out bounds);
    }

    public static PolySlice slice(Mesh mesh, Frame slice_at, out Bar bounds) {
        Transformer trans = new Transformer().to_local(slice_at);
        mesh = trans.mesh(mesh);
        Voxels vox = new(mesh);

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
        Vec3 origin = VOXEL_SIZE * new Vec3(origin_x, origin_y, origin_z);
        Vec3 size = VOXEL_SIZE * new Vec3(size_x, size_y, size_z);

        Vec3 centre = origin + size/2f;
        centre.Z = 0f;
        bounds = new(
            slice_at.translate(centre),
            size.X,
            size.Y,
            VOXEL_SIZE
        );

        ImageGrayScale img = new(size_x, size_y);
        vox.GetVoxelSlice(-origin_z, ref img, Voxels.ESliceMode.SignedDistance);
        return PolySlice.oFromSdf(img, 0f, projxy(origin), VOXEL_SIZE);
    }

    public static void to_file(string name, Voxels vox, in Frame slice_at,
            out Bar bounds, bool filled=true) {
        string path = fromroot($"exports/draw-{name}.svg");
        PolySlice poly_slice = slice(vox, slice_at, out bounds);
        poly_slice.SaveToSvgFile(path, filled);
    }
    public static void to_file(string name, Mesh mesh, in Frame slice_at,
            out Bar bounds, bool filled=true) {
        string path = fromroot($"exports/draw-{name}.svg");
        PolySlice poly_slice = slice(mesh, slice_at, out bounds);
        poly_slice.SaveToSvgFile(path, filled);
    }
}

}
