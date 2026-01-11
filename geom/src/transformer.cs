using static br.Br;

using Vec3 = System.Numerics.Vector3;
using Mat4 = System.Numerics.Matrix4x4;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using Triangle = PicoGK.Triangle;

namespace br {

public class Transformer {
    protected Mat4 _m { get; } // why the fuck does csharp store it row major.
    protected Transformer(in Mat4 mat) {
        assert(nearto(mat[0, 3], 0f));
        assert(nearto(mat[1, 3], 0f));
        assert(nearto(mat[2, 3], 0f));
        assert(nearto(mat[3, 3], 1f));
        this._m = mat;
    }

    public Transformer() : this(Mat4.Identity) {}

    public Mat4 mat => Mat4.Transpose(_m);
    public Mat4 mat_T => _m;

    public Vec3 get_translation()
        => new(_m[3, 0], _m[3, 1], _m[3, 2]);
    public Vec3 get_scale()
        => new(
            mag(new Vec3(_m[0, 0], _m[0, 1], _m[0, 2])),
            mag(new Vec3(_m[1, 0], _m[1, 1], _m[1, 2])),
            mag(new Vec3(_m[2, 0], _m[2, 1], _m[2, 2]))
        );
    public void get_rotation(out Vec3 about, out float by) {
        Vec3 X = normalise(new Vec3(_m[0, 0], _m[0, 1], _m[0, 2]));
        Vec3 Y = normalise(new Vec3(_m[1, 0], _m[1, 1], _m[1, 2]));
        Vec3 Z = normalise(new Vec3(_m[2, 0], _m[2, 1], _m[2, 2]));
        // forward to frame.
        new Frame(ZERO3, X, Y, Z).get_rotation(out about, out by);
    }


    public Transformer translate(in Vec3 by) {
        Mat4 m = new(
              1f,   0f,   0f, 0f,
              0f,   1f,   0f, 0f,
              0f,   0f,   1f, 0f,
            by.X, by.Y, by.Z, 1f
        );
        return new(_m * m);
    }
    public Transformer scale(in Vec3 by) {
        Mat4 m = new(
            by.X,   0f,   0f, 0f,
              0f, by.Y,   0f, 0f,
              0f,   0f, by.Z, 0f,
              0f,   0f,   0f, 1f
        );
        return new(_m * m);
    }
    public Transformer scale(in Vec3 along, float by) {
        Vec3 v = normalise(along);
        Mat4 outer = new Mat4(
            (by - 1f)*v.X*v.X, (by - 1f)*v.X*v.Y, (by - 1f)*v.X*v.Z, 0f,
            (by - 1f)*v.Y*v.X, (by - 1f)*v.Y*v.Y, (by - 1f)*v.Y*v.Z, 0f,
            (by - 1f)*v.Z*v.X, (by - 1f)*v.Z*v.Y, (by - 1f)*v.Z*v.Z, 0f,
                           0f,                0f,                0f, 0f
        );
        return new(_m * (outer + Mat4.Identity));
    }
    public Transformer rotate(in Vec3 about, float by) {
        Vec3 X = Br.rotate(uX3, about, by);
        Vec3 Y = Br.rotate(uY3, about, by);
        Vec3 Z = Br.rotate(uZ3, about, by);
        Mat4 m = new(
            X.X, X.Y, X.Z, 0f,
            Y.X, Y.Y, Y.Z, 0f,
            Z.X, Z.Y, Z.Z, 0f,
             0f,  0f,  0f, 1f
        );
        return new(_m * m);
    }
    public Transformer apply(in Transformer other) {
        return new(_m * other._m);
    }

    public Transformer to_global(in Frame frame) {
        Mat4 m = new(
              frame.X.X,   frame.X.Y,   frame.X.Z, 0f,
              frame.Y.X,   frame.Y.Y,   frame.Y.Z, 0f,
              frame.Z.X,   frame.Z.Y,   frame.Z.Z, 0f,
            frame.pos.X, frame.pos.Y, frame.pos.Z, 1f
        );
        return new(_m * m);
    }
    public Transformer to_local(in Frame frame) {
        Mat4 m = new(
              frame.X.X,   frame.X.Y,   frame.X.Z, 0f,
              frame.Y.X,   frame.Y.Y,   frame.Y.Z, 0f,
              frame.Z.X,   frame.Z.Y,   frame.Z.Z, 0f,
            frame.pos.X, frame.pos.Y, frame.pos.Z, 1f
        );
        assert(Mat4.Invert(m, out m));
        return new(_m * m);
    }

    public Transformer inverse() {
        assert(Mat4.Invert(_m, out Mat4 m));
        return new(m);
    }

    public Vec3 vec(in Vec3 vec) {
        return Vec3.Transform(vec, _m);
    }

    public Mesh mesh(in Mesh mesh) {
        List<Vec3> V = new();
        List<Triangle> I = new();
        for (int i=0; i<mesh.nVertexCount(); ++i) {
            Vec3 v = vec(mesh.vecVertexAt(i));
            V.Add(v);
        }
        Mesh ret = new();
        ret.AddVertices(V, out _);
        for (int i=0; i<mesh.nTriangleCount(); ++i) {
            Triangle t = mesh.oTriangleAt(i);
            ret.nAddTriangle(t);
        }
        return ret;
    }

    public Voxels voxels(in Voxels vox, bool dispose=false) {
        Mesh a = new Mesh(vox);
        if (dispose)
            vox.Dispose();
        Mesh b = mesh(a);
        a.Dispose();
        return new Voxels(b);
    }
}

}
