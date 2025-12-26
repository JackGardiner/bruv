using static Br;
using Vec3 = System.Numerics.Vector3;
using Mat4 = System.Numerics.Matrix4x4;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using Triangle = PicoGK.Triangle;

namespace br {

public class Transformer {
    protected Mat4 _m { get; } // why the fuck does csharp store it row major.
    protected Transformer(in Mat4 mat) {
        assert(mat[0, 3] == 0f);
        assert(mat[1, 3] == 0f);
        assert(mat[2, 3] == 0f);
        assert(mat[3, 3] == 1f);
        this._m = mat;
    }

    public Mat4 mat => Mat4.Transpose(_m);
    public Vec3 trans => new(mat[0, 3], mat[1, 3], mat[2, 3]);

    public Transformer() : this(Mat4.Identity) {}

    public Transformer translate(Vec3 by) {
        Mat4 m = new(
              1f,   0f,   0f, 0f,
              0f,   1f,   0f, 0f,
              0f,   0f,   1f, 0f,
            by.X, by.Y, by.Z, 1f
        );
        return new(_m * m);
    }
    public Transformer scale(Vec3 by) {
        Mat4 m = new(
            by.X,   0f,   0f, 0f,
              0f, by.Y,   0f, 0f,
              0f,   0f, by.Z, 0f,
              0f,   0f,   0f, 1f
        );
        return new(_m * m);
    }
    public Transformer rotate(Vec3 about, float by) {
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

    public Voxels voxels(in Voxels vox) {
        Mesh a = new Mesh(vox);
        Mesh b = mesh(a);
        return new Voxels(a);
    }
}

}
