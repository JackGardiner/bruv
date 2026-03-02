using static br.Br;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;

namespace br {

public class Tapping {
    public string size { get; }
    public bool printable { get; }

    public float major_diameter { get; }
    public float minor_diameter { get; }
    public float pitch { get; }
    public float threaded_depth { get; set; }
    public float extra_depth { get; set; }
    public float tip_depth_ratio { get; set; }

    public float bore_diameter // conservative material removal:
        => 0.9f*minor_diameter;

    public float major_radius => 0.5f*major_diameter;
    public float minor_radius => 0.5f*minor_diameter;
    public float bore_radius  => 0.5f*bore_diameter;

    public float straight_depth => threaded_depth + extra_depth + 2*pitch;

    public Tapping(string size, bool printable=true) {
        Dictionary<string, (float, float, float)> lookup = new([
            /*       size, ( major diam, minor diam,  pitch ) */
            new(     "M1", (       1.0f,     0.729f,  0.25f ) ),
            new(     "M2", (       2.0f,     1.567f,  0.40f ) ),
            new(     "M3", (       3.0f,     2.459f,  0.50f ) ),
            new(     "M4", (       4.0f,     3.242f,  0.70f ) ),
            new(     "M5", (       5.0f,     4.134f,  0.80f ) ),
            new(     "M6", (       6.0f,     4.917f,  1.00f ) ),
            new(     "M8", (       8.0f,     6.647f,  1.25f ) ),
            new(    "M10", (      10.0f,     8.376f,  1.50f ) ),
            new(    "M12", (      12.0f,    10.106f,  1.75f ) ),
            new(    "M14", (      14.0f,    11.835f,  2.00f ) ),
            new(    "M16", (      16.0f,    13.835f,  2.00f ) ),

            new(  "G1/16", (     7.723f,     6.561f, 0.907f ) ),
            new(   "G1/8", (     9.728f,     8.566f, 0.907f ) ),
            new(   "G1/4", (    13.157f,    11.445f, 1.337f ) ),
            new(   "G3/8", (    16.662f,    14.950f, 1.337f ) ),
            new(   "G1/2", (    20.955f,    18.631f, 1.814f ) ),
            new(   "G5/8", (    22.911f,    20.587f, 1.814f ) ),
            new(   "G3/4", (    26.441f,    24.117f, 1.814f ) ),
            new(   "G7/8", (    30.201f,    27.877f, 1.814f ) ),
            new(     "G1", (    33.249f,    30.291f, 2.309f ) ),
            new( "G1-1/8", (    37.897f,    34.939f, 2.309f ) ),
            new( "G1-1/4", (    41.910f,    38.952f, 2.309f ) ),
            new( "G1-1/2", (    47.803f,    44.845f, 2.309f ) ),
        ]);

        this.size = size;
        this.printable = printable;

        this.major_diameter  = lookup[size].Item1;
        this.minor_diameter  = lookup[size].Item2;
        this.pitch           = lookup[size].Item3;
        // Typical thread engagement: (note this is kinda super wrong for small
        //                             bolt sizes, eh just manually adjust.)
        this.threaded_depth = 0.8f*this.major_diameter;
        this.extra_depth = 0.0f; // for use by caller.
        // Depth ratio of a 112deg tip:
        this.tip_depth_ratio = 1f/tan(torad(112f/2));
    }


    public Voxels threads(Frame face_out) {
        // oh yeah genuine thread visualiser.
        // now THIS is picogk.

        // Make the divs s.t. the longest spacing between vertices is one voxel.
        int count_per_pitch = (int)(TWOPI * major_radius / VOXEL_SIZE);
        count_per_pitch /= 3; // eh dont need that much LMAO.
        count_per_pitch = max(20, count_per_pitch);

        float dcount = count_per_pitch / pitch;
        int count = (int)(dcount * (threaded_depth + 3*pitch));

        List<Frame> frames = new(count);
        for (int i=0; i<count; ++i) {
            float rotation = lerp(0, TWOPI, i, count_per_pitch);
            float trans = lerp(0f, -pitch, i, count_per_pitch);
            trans += pitch;
            frames.Add(face_out.transz(trans).rotxy(rotation).swapzx());
        }

        List<Vec2> crosssection = [
            new(0f,        0f),
            new(0f,        minor_radius),
            new(-pitch/2f, major_radius),
            new(-pitch,    minor_radius),
            new(-pitch,    0f),
        ];
        List<Vec2> points = new(count*numel(crosssection));
        for (int i=0; i<count; ++i) {
            float j = i - (count - 2.75f*count_per_pitch);
            if (j < 0f)
                points.AddRange(crosssection);
            else {
                float t = j / (2f*count_per_pitch);
                t = clamp(t, 0f, 1f);
                points.AddRange([
                    new(0f,        0f),
                    new(0f,        minor_radius),
                    new(-pitch/2f, lerp(major_radius, minor_radius, t)),
                    new(-pitch,    minor_radius),
                    new(-pitch,    0f),
                ]);
            }
        }

        Mesh m = Polygon.mesh_swept(new FramesSequence(frames), points,
                closed: false, ringed: false);
        Voxels v = new(m);
        v.Smoothen(VOXEL_SIZE);
        v.BoolIntersect(new Bar(face_out, -straight_depth, major_diameter + 2f));
        return v;
    }


    public Voxels hole(Frame face_out, float extra=2f) {

        if (!printable) {
            // Hole will look like the threads with a cone tip.
            List<Vec2> zr = [
                new(extra,           0f),
                new(extra,           major_radius),
                new(0f,              major_radius),
                new(0f,              minor_radius),
                new(-straight_depth, minor_radius),
                new(-straight_depth - tip_depth_ratio*minor_radius, 0f),
            ];
            Voxels vox = new(Polygon.mesh_revolved(face_out, zr));
            vox.BoolAdd(threads(face_out));
            return vox;
        }

        // Simple bore cylinder w cone tip.
        if (nearvert(face_out.Z)) {
            List<Vec2> zr = [
                new(extra,           0f),
                new(extra,           bore_radius),
                new(-straight_depth, bore_radius),
                new(-straight_depth - tip_depth_ratio*bore_radius, 0f),
            ];
            return new(Polygon.mesh_revolved(face_out, zr));
        }

        // Translate frame into equivalent w X horizontal.
        face_out = new(face_out.pos, normalise(cross(uZ3, face_out.Z)),
                face_out.Z);

        List<Vec3> points;
        // Simple bore diamond w tetrahedral tip.
        if (nearhoriz(face_out.Z)) {
            points = [
                face_out * new Vec3(+bore_radius, 0f, extra),
                face_out * new Vec3(0f, +bore_radius, extra),
                face_out * new Vec3(-bore_radius, 0f, extra),
                face_out * new Vec3(0f, -bore_radius, extra),
                face_out * new Vec3(+bore_radius, 0f, -straight_depth),
                face_out * new Vec3(0f, +bore_radius, -straight_depth),
                face_out * new Vec3(-bore_radius, 0f, -straight_depth),
                face_out * new Vec3(0f, -bore_radius, -straight_depth),
                face_out * (uZ3*(-straight_depth - tip_depth_ratio*bore_radius)),
            ];
        } else {
            // Default to "simple" bore rhombus w weird tip. This is the best
            // case for 45deg, and just use it for all others. It has a shortened
            // width to ensure no angles greater than 45deg. Purely by vibing it
            // out, a rhombus ratio of sqrt(2) wider than tall gives a maximum
            // inclination from vertical of 45deg.
            points = [
                face_out * new Vec3(+bore_radius, 0f, extra),
                face_out * new Vec3(0f, +SQRTH*bore_radius, extra),
                face_out * new Vec3(-bore_radius, 0f, extra),
                face_out * new Vec3(0f, -SQRTH*bore_radius, extra),
                face_out * new Vec3(+bore_radius, 0f, -straight_depth),
                face_out * new Vec3(0f, +SQRTH*bore_radius, -straight_depth),
                face_out * new Vec3(-bore_radius, 0f, -straight_depth),
                face_out * new Vec3(0f, -SQRTH*bore_radius, -straight_depth),
                face_out * (uZ3*(-straight_depth - tip_depth_ratio*bore_radius)),
            ];
        }

        Mesh mesh = new();
        mesh.AddVertices(points, out _);

        mesh.nAddTriangle(new(0, 1, 2));
        mesh.nAddTriangle(new(2, 3, 0));

        mesh.nAddTriangle(new(0, 4, 1));
        mesh.nAddTriangle(new(1, 4, 5));
        mesh.nAddTriangle(new(1, 5, 2));
        mesh.nAddTriangle(new(2, 5, 6));
        mesh.nAddTriangle(new(2, 6, 3));
        mesh.nAddTriangle(new(3, 6, 7));
        mesh.nAddTriangle(new(3, 7, 0));
        mesh.nAddTriangle(new(0, 7, 4));

        mesh.nAddTriangle(new(8, 4 + 1, 4 + 0));
        mesh.nAddTriangle(new(8, 4 + 2, 4 + 1));
        mesh.nAddTriangle(new(8, 4 + 3, 4 + 2));
        mesh.nAddTriangle(new(8, 4 + 0, 4 + 3));

        return new(mesh);
    }

    public Voxels supporting(Frame face_out, float th) {
        assert(th > 0f);

        // Just a thickened fill, but with always 45deg cone.
        List<Vec2> points = [
            new(0f,                              0f),
            new(0f,                              major_radius + th),
            new(-straight_depth - th*tan(PI/8f), major_radius + th),
            new(-straight_depth - major_radius - SQRT2*th, 0f),
        ];
        Mesh mesh = Polygon.mesh_revolved(face_out, points);
        return new(mesh);
    }
}


}
