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

    public Transformer() : this(Mat4.Identity) {}

    public Mat4 mat => Mat4.Transpose(_m);

    public Vec3 get_translation()
        => new(_m[3, 0], _m[3, 1], _m[3, 2]);
    public Vec3 get_scale()
        => new(
            mag(new Vec3(_m[0, 0], _m[0, 1], _m[0, 2])),
            mag(new Vec3(_m[1, 0], _m[1, 1], _m[1, 2])),
            mag(new Vec3(_m[2, 0], _m[2, 1], _m[2, 2]))
        );
    public void get_rotation(out Vec3 about, out float by) {
        Vec3 scale = get_scale();

        float x00 = _m[0, 0] / scale.X;
        float x01 = _m[0, 1] / scale.X;
        float x02 = _m[0, 2] / scale.X;
        float x10 = _m[1, 0] / scale.Y;
        float x11 = _m[1, 1] / scale.Y;
        float x12 = _m[1, 2] / scale.Y;
        float x20 = _m[2, 0] / scale.Z;
        float x21 = _m[2, 1] / scale.Z;
        float x22 = _m[2, 2] / scale.Z;

        // Calculate the angle of rotation from the trace.
        // cos(by) = (trace - 1) / 2
        float cosby = (x00 + x11 + x22 - 1f) * 0.5f;
        by = acos(clamp(cosby, -1f, 1f));

        // Explicitly handle no rot.
        if (closeto(by, 0f)) {
            about = uZ3; // can be anything.
            return;
        }

        // Explicitly handle flip rot.
        if (closeto(by, PI)) {
            // We find the largest diagonal element to solve for the axis.
            if (x00 > x11 && x00 > x22) {
                float s = sqrt(x00 - x11 - x22 + 1f) * 0.5f;
                about = new Vec3(s, x01/s/2f, x02/s/2f);
            } else if (x11 > x22) {
                float s = sqrt(x11 - x00 - x22 + 1f) * 0.5f;
                about = new Vec3(x01/s/2f, s, x12/s/2f);
            } else {
                float s = sqrt(x22 - x00 - x11 + 1f) * 0.5f;
                about = new Vec3(x02/s/2f, x12/s/2f, s);
            }
            return;
        }

        // Otherwise normal.
        about = normalise(new Vec3(x12 - x21, x20 - x02, x01 - x10));
    }


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
    public Transformer apply(in Transformer other) {
        return new(_m * other._m);
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
