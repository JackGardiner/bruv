using static br.Br;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;

namespace br {

public class Breakout {
    public List<Vec2> V0 { get; }
    public float Lz { get; set; }
    public float Lx { get; set; }
    public float D1 => 2f*r1;
    public float r1 { get; set; }
    public float FR { get; set; }
    public static int N { get {
        int n = max(30, (int)(40f / VOXEL_SIZE));
        n += n & 1; // make even.
        return n;
    } }
    public static int M => max(20, (int)(27f / VOXEL_SIZE));
    private float max_y0;

    public float straight_for => 0.2f*Lz;
    public float swing_radius => Lz - straight_for;

    public Breakout(Slice<Vec2> V0, float Lz, float Lx, float D1, float FR=0f) {
        this.V0 = Polygon.resample(V0, N, true);
        // TODO: centroid calc rather than assume centred on origin.
        int start_at = 0;
        max_y0 = 0f;
        for (int i=0; i<numel(this.V0); ++i) {
            Vec2 a = this.V0[i];
            Vec2 b = this.V0[start_at];
            if (a.X < 0f)
                continue;
            if (b.X < 0f || abs(a.Y) < abs(b.Y))
                start_at = i;

            max_y0 = max(max_y0, abs(a.Y));
        }
        this.V0 = [..this.V0[start_at..], ..this.V0[..start_at]];
        this.Lz = Lz;
        this.Lx = Lx;
        this.r1 = 0.5f*D1;
        this.FR = FR;
    }

    public Voxels at(Frame start_up) {
        // The breakout contour will travel in the +X direction of `start_up`
        // (as well as in the -Z direction).


        // Get the point at the start of the swing.
        Frame F0 = start_up.transz(-straight_for);
        // F0: x = +outwards, y = sideways, z = +upwards

        // Get the swinging frames.
        List<Frame> F = new(M);
        for (int i=0; i<M; ++i) {
            float by = lerp(0f, -PI_2, i, M);
            F.Add(F0.swing(swing_radius * uX3, uY3, by));
        }
        // F1: x = +upwards, y = sideways, z = -outwards

        // CS goes from original -> circular.
        List<Vec2> V1 = Polygon.circle(N, r1); // +X is first index.

        List<Vec2> V = new(N * M);
        for (int i=0; i<M; ++i) {
            float t = i / (float)(M - 1);
            t = t*t*(3f - 2f*t); // ease in-out.
            V.AddRange(Polygon.lerp(V0, V1, t));
        }

        // Also add a teardrop roof to the entire channel to ensure nice
        // printing.
        float rot_to_vert = -argphi(start_up.X);
        Frame Ftear0 = start_up.rotzx(rot_to_vert);
        List<Frame> Ftear = [..F.Select((f) => new Frame(f.pos, Ftear0))];
        // x = +upwards, y = sideways
        List<Vec2> Vtear1 = new(N){ SQRT2*r1 * uX2 };
        for (int i=0; i<N - 1; ++i) {
            float ang = (i < N/2)
                      ? lerp(PI_4, PI_2, i, N/2)
                      : lerp(-PI_2, -PI_4, i - N/2, N - 1 - N/2);
            Vtear1.Add(frompol(r1, ang));
        }
        List<Vec2> Vtear = new(N * M);
        for (int i=0; i<M; ++i) {
            // double x length when travelling vertical.
            float phi = argphi(F[i].Z);
            float stretchx = 1f + abs(phi - PI_2)/PI_2;
            // morph to match inlet width.
            float t = i / (float)(M - 1);
            t = t*t*(3f - 2f*t); // ease in-out.
            float stretchy = lerp(max_y0/r1, 1f, t);
            Vec2 stretch = new(stretchx, stretchy);
            Vtear.AddRange(Vtear1.Select((v) => v * stretch));
        }

        // Extend at both ends.
        F.Insert(0, F[0].transz(straight_for + FR + 2f*VOXEL_SIZE));
        V.InsertRange(0, V[..N]);
        F.Add(F[^1].transz(-Lx));
        V.AddRange(V[^N..]);
        Ftear.Add(Ftear[^1].transz(-Lx));
        Vtear.AddRange(Vtear[^N..]);


        // Create.
        Voxels v = new(Polygon.mesh_swept(new FramesSequence(F), V));
        Fillet.convex(v, FR, true);
        v.BoolAdd(new(Polygon.mesh_swept(new FramesSequence(Ftear), Vtear)));
        Fillet.concave(v, FR, true);
        return v;
    }
}

}
