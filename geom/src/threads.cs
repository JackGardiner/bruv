using static br.Br;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;

namespace br {

public class Threads {
    public string size { get; }

    public bool right_handed { get; set; }
    public float major_diameter { get; set; }
    public float minor_diameter { get; set; }
    public float pitch { get; set; }
    public float taper { get; set; } // diametral reduction per unit length.
    public float gauge_length { get; set; } // only relevant when tapered.
    public float threaded_length { get; set; }  // =typical.
    public float extra_length { get; set; }     // =smth tiny.
    public float tip_length_ratio { get; set; } // =1.
    public float incomplete_upper_length { get; set; } // =1 pitch if male.
    public float incomplete_lower_length { get; set; } // =2 pitches.
    public float outer_thread_truncation { get; set; } // =15% if male.
    public float inner_thread_truncation { get; set; } // =15% if female.

    public float thread_depth => major_radius - minor_radius;

    public float straight_length => threaded_length + extra_length
                                  + incomplete_upper_length
                                  + incomplete_lower_length;


    public float major_radius => 0.5f*major_diameter;
    public float minor_radius => 0.5f*minor_diameter;

    public Threads(string size) {
        Dictionary<string, List<float>> lookup = new([
// Cheeky indentation cut.
/*                  major    minor                  gauge          */
/*         size     diam.    diam.   pitch  taper  length  length  */
new(      "M1", [    1.0f,  0.729f,  0.25f,    0f,     0f,   1.5f ] ),
new(      "M2", [    2.0f,  1.567f,  0.40f,    0f,     0f,   3.2f ] ),
new(      "M3", [    3.0f,  2.459f,  0.50f,    0f,     0f,   5.0f ] ),
new(      "M4", [    4.0f,  3.242f,  0.70f,    0f,     0f,   7.0f ] ),
new(      "M5", [    5.0f,  4.134f,  0.80f,    0f,     0f,   8.0f ] ),
new(      "M6", [    6.0f,  4.917f,  1.00f,    0f,     0f,  10.0f ] ),
new(      "M8", [    8.0f,  6.647f,  1.25f,    0f,     0f,  12.5f ] ),
new(     "M10", [   10.0f,  8.376f,  1.50f,    0f,     0f,  15.0f ] ),
new(     "M12", [   12.0f, 10.106f,  1.75f,    0f,     0f,  17.5f ] ),
new(     "M14", [   14.0f, 11.835f,  2.00f,    0f,     0f,  20.0f ] ),
new(     "M16", [   16.0f, 13.835f,  2.00f,    0f,     0f,  20.0f ] ),

new(   "G1/16", [  7.723f,  6.561f, 0.907f,    0f,     0f,   9.1f ] ),
new(    "G1/8", [  9.728f,  8.566f, 0.907f,    0f,     0f,   9.1f ] ),
new(    "G1/4", [ 13.157f, 11.445f, 1.337f,    0f,     0f,  14.7f ] ),
new(    "G3/8", [ 16.662f, 14.950f, 1.337f,    0f,     0f,  16.0f ] ),
new(    "G1/2", [ 20.955f, 18.631f, 1.814f,    0f,     0f,  21.8f ] ),
new(    "G5/8", [ 22.911f, 20.587f, 1.814f,    0f,     0f,  23.6f ] ),
new(    "G3/4", [ 26.441f, 24.117f, 1.814f,    0f,     0f,  23.6f ] ),
new(    "G7/8", [ 30.201f, 27.877f, 1.814f,    0f,     0f,  25.4f ] ),
new(      "G1", [ 33.249f, 30.291f, 2.309f,    0f,     0f,  32.3f ] ),
new(  "G1-1/8", [ 37.897f, 34.939f, 2.309f,    0f,     0f,  34.6f ] ),
new(  "G1-1/4", [ 41.910f, 38.952f, 2.309f,    0f,     0f,  34.6f ] ),
new(  "G1-1/2", [ 47.803f, 44.845f, 2.309f,    0f,     0f,  36.9f ] ),

new(  "Rc1/16", [  7.723f,  6.561f, 0.907f, 1f/16,   4.0f,   6.5f ] ),
new(   "Rc1/8", [  9.728f,  8.566f, 0.907f, 1f/16,   4.0f,   6.5f ] ),
new(   "Rc1/4", [ 13.157f, 11.445f, 1.337f, 1f/16,   6.0f,   9.7f ] ),
new(   "Rc3/8", [ 16.662f, 14.950f, 1.337f, 1f/16,   6.4f,  10.1f ] ),
new(   "Rc1/2", [ 20.955f, 18.631f, 1.814f, 1f/16,   8.2f,  13.2f ] ),
new(   "Rc3/4", [ 26.441f, 24.117f, 1.814f, 1f/16,   9.5f,  14.5f ] ),
new(     "Rc1", [ 33.249f, 30.291f, 2.309f, 1f/16,  10.4f,  16.8f ] ),
new( "Rc1-1/4", [ 41.910f, 38.952f, 2.309f, 1f/16,  12.7f,  19.1f ] ),
new( "Rc1-1/2", [ 47.803f, 44.845f, 2.309f, 1f/16,  12.7f,  19.1f ] ),
        ]);
        List<float> entry = lookup[size];

        this.size = size;

        this.right_handed = yeah;
        this.major_diameter  = entry[0];
        this.minor_diameter  = entry[1];
        this.pitch           = entry[2];
        this.taper           = entry[3];
        this.gauge_length    = entry[4];
        this.threaded_length = entry[5];
        this.extra_length = this.major_diameter * 0.1f;
        this.tip_length_ratio = 0f;
        this.incomplete_lower_length = 0f;
        this.incomplete_upper_length = 0f;
        this.inner_thread_truncation = 0f;
        this.outer_thread_truncation = 0f;
    }


    public float taper_offset(float length)
        => 0.5f*taper*(threaded_length - gauge_length - length);

    private void tapered_bounds(float r, out Vec2 A, out Vec2 B) {
        A = new(incomplete_upper_length, r);
        B = A - straight_length*uX2;
        // Include taper.
        A.Y += taper_offset(-A.X);
        B.Y += taper_offset(-B.X);
    }
    public void inner_thread_bounds(out Vec2 A, out Vec2 B)
        => tapered_bounds(minor_radius + inner_thread_truncation, out A, out B);
    public void outer_thread_bounds(out Vec2 A, out Vec2 B)
        => tapered_bounds(major_radius - outer_thread_truncation, out A, out B);

    public Voxels threads(Frame face_out) {
        // oh yeah genuine thread creator.
        // now THIS is picogk.

        // Female thread profile from ISO228-1. used also for metric bolts here.
        // Tapering from ISO7-1.

        // Make the divs s.t. the longest spacing between vertices is one voxel.
        int count_per_pitch = (int)(TWOPI * major_radius / VOXEL_SIZE);
        count_per_pitch /= 3; // eh dont need that much LMAO.
        count_per_pitch = max(20, count_per_pitch);

        float dcount = count_per_pitch / pitch;
        int count = (int)(dcount * (threaded_length + 3f*pitch));

        // Some thread properties.
        float pitch_radius = 0.5f*(minor_radius + major_radius);
        float triangle_height = 0.960491f*pitch;
        float FR = 0.137329f*pitch;

        float thread_angle = atan(pitch/2f / triangle_height);
        assert(nearto(thread_angle, torad(27.5f), rtol: 1e-3f));

        float rlo = pitch_radius - 0.5f*triangle_height;
        float rhi = pitch_radius + 0.5f*triangle_height;

        // Spinning (but not translating) frames to build each thread, one pitch
        // at a time, each from an xycrossection of a spike.
        List<Frame> frames = new(count);
        List<Vec2> points = new(5*count);
        float zhi = +0.5f*pitch + incomplete_upper_length;
        float zlo = -0.5f*pitch - incomplete_lower_length - threaded_length;
        for (int i=0; i<count; ++i) {
            float z1 = lerp(zhi, zlo, i, count);
            float z0 = z1 + 0.5f*pitch;
            float z2 = z1 - 0.5f*pitch;

            // Frame rotates full every pitch. Initial rotation is unimportant.
            // WELLLLL it is important that its fucking right handed. bloody
            // brilliant mate. well done.
            float by = TWOPI * +z1/pitch;
            if (!right_handed)
                by = -by;
            frames.Add(face_out.rotxy(by).swapzx());

            // See if its an incomplete thread. If so, lerp down to no peeking-
            // out threads.
            float t_upper = z1;
            float t_lower = -z1 - threaded_length;
            t_upper /= 0.95f*incomplete_upper_length;
            t_lower /= 0.95f*incomplete_lower_length;
            t_upper = clamp(t_upper, 0f, 1.1f);
            t_lower = clamp(t_lower, 0f, 1.1f);

            float falloff = thread_depth - inner_thread_truncation
                          - outer_thread_truncation;
            falloff *= t_lower + t_upper;

            float r0 = rlo + taper_offset(-z0) - falloff;
            float r1 = rhi + taper_offset(-z1) - falloff;
            float r2 = rlo + taper_offset(-z2) - falloff;

            points.Add(new(z0, 0f));
            points.Add(new(z0, r0));
            points.Add(new(z1, r1));
            points.Add(new(z2, r2));
            points.Add(new(z2, 0f));
        }

        Mesh m = Polygon.mesh_swept(new FramesSequence(frames), points,
                ringed: false);
        Voxels v = new(m);

        // Both fix the mesh->voxel (mesh has contacting faces) and fillet the
        // threads.
        v.TripleOffset(-FR);

        // Truncate the thread (and fill in interior).
        inner_thread_bounds(out Vec2 A, out Vec2 B);
        v.BoolAdd(new Cone(face_out.transz(A.X), B.X - A.X, A.Y, B.Y));
        outer_thread_bounds(out A, out B);
        v.BoolIntersect(new Cone(face_out.transz(A.X), B.X - A.X, A.Y, B.Y));

        // Clip the top and bottom to be flat (it was created with extra pitch
        // lengths) and threads to not extend past major.
        v.BoolIntersect(new Cone(
            face_out.transz(incomplete_upper_length),
            -straight_length,
            major_radius + taper_offset(0f),
            major_radius + taper_offset(straight_length)
        ));

        return v;
    }
}


public class Tapping : Threads {
    public bool printable { get; }

    public float bore_diameter // conservative material removal:
        => 0.8f*minor_diameter + taper_offset(straight_length);

    public float bore_radius => 0.5f*bore_diameter;

    public Tapping(string size, bool printable=false)
            : base(size.Split(" ", 2)[0]) {
        // note that `size` is allowed to specify a threaded length after the
        // size, separated by a space.
        this.printable = printable;

        incomplete_lower_length = 2f*pitch;
        inner_thread_truncation = 0.15f*thread_depth;

        // Add safety factor to gauge length.
        if (gauge_length != 0f) {
            gauge_length += 1.5f*pitch;
            threaded_length += 3.0f*pitch; // little more...
        }
        // 45deg tip ig.
        tip_length_ratio = 1f;

        string[] parts = size.Split(" ");
        if (numel(parts) > 1) {
            threaded_length = float.Parse(parts[1],
                    System.Globalization.CultureInfo.InvariantCulture);
        }
        assert(numel(parts) <= 2, "unrecognised sizing string");
    }


    public Voxels at(Frame face_out, float extra=2f) {

        inner_thread_bounds(out Vec2 A, out Vec2 B);

        if (!printable) {
            // Hole will look like the threads, with a conical tip, a small
            // chamfer at the leading edge, and a small round slug at the top.
            float CR = 2.0f*thread_depth;
            Voxels vox = threads(face_out);
            List<Vec2> zr = [
                new(A.X + extra, 0f),
                new(A.X + extra, major_radius + CR + min(0.2f*major_radius, 3f)),
                new(A.X,         major_radius + CR + min(0.2f*major_radius, 3f)),
                A + uY2*(CR - 0.5f*taper*CR),
                A - new Vec2(CR, 0.5f*taper*CR),
                B,
                new(B.X - tip_length_ratio*B.Y, 0f),
            ];
            if (extra == 0f)
                zr.RemoveRange(1, 2);
            Polygon.cull_adjacent_duplicates(zr);
            vox.BoolAdd(new(Polygon.mesh_revolved(face_out, zr)));
            return vox;
        }

        // Simple bore cylinder w cone tip.
        if (nearvert(face_out.Z)) {
            List<Vec2> zr = [
                new(extra,           0f),
                new(extra,           bore_radius),
                new(-straight_length, bore_radius),
                new(-straight_length - tip_length_ratio*bore_radius, 0f),
            ];
            return new(Polygon.mesh_revolved(face_out, zr));
        }

        // Translate frame into equivalent w X horizontal and Y +vertical.
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
                face_out * new Vec3(+bore_radius, 0f, -straight_length),
                face_out * new Vec3(0f, +bore_radius, -straight_length),
                face_out * new Vec3(-bore_radius, 0f, -straight_length),
                face_out * new Vec3(0f, -bore_radius, -straight_length),
                face_out * (uZ3*(-straight_length
                                 - tip_length_ratio*bore_radius)),
            ];
        } else {
            // Default to "simple" bore rhombus w weird tip. For the worst-case
            // of a drill-upwards-45deg-phi hole, no hole will have a leading
            // edge less than 45deg, and this sqrt2 ratio has a leading edge of
            // ~55deg which im happy to call a good middle ground.
            points = [
                face_out * new Vec3(+SQRTH*bore_radius, 0f, extra),
                face_out * new Vec3(0f, +bore_radius, extra),
                face_out * new Vec3(-SQRTH*bore_radius, 0f, extra),
                face_out * new Vec3(0f, -bore_radius, extra),
                face_out * new Vec3(+SQRTH*bore_radius, 0f, -straight_length),
                face_out * new Vec3(0f, +bore_radius, -straight_length),
                face_out * new Vec3(-SQRTH*bore_radius, 0f, -straight_length),
                face_out * new Vec3(0f, -bore_radius, -straight_length),
                face_out * (uZ3*(-straight_length
                                 - tip_length_ratio*bore_radius)),
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
}


public class Studding : Threads {
    public Studding(string size)
            : base(size) {

        incomplete_upper_length = 1f*pitch;
        incomplete_lower_length = 2f*pitch;
        outer_thread_truncation = 0.15f*thread_depth;

        // Add safety factor to gauge length.
        if (gauge_length != 0f) {
            // gauge_length -= 0.5f*pitch;
            // eh nah its fine.
            threaded_length += 2.0f*pitch;
        }
        tip_length_ratio = 0f; // circumcise.
    }


    public Voxels at(Frame face_out, Tapping? for_tap=null, float extra=2f) {

        if (for_tap != null) {
            assert(right_handed == for_tap.right_handed, "big dog");
                                                       // behind you big dog.
            // Align gauge planes.
            float Dz = -for_tap.threaded_length + for_tap.gauge_length
                     + threaded_length - gauge_length;
            face_out = face_out.transz(Dz);
            face_out = face_out.rotxy(TWOPI * Dz / pitch
                                    * (right_handed ? 1f : -1f));
        }

        inner_thread_bounds(out Vec2 A, out Vec2 B);

        Voxels vox = threads(face_out);

        List<Vec2> zr = [
            new(A.X + extra, 0f),
            new(A.X + extra, A.Y + 0.5f*taper*extra),
            B,
            new(B.X - tip_length_ratio*B.Y, 0f),
        ];
        Polygon.cull_adjacent_duplicates(zr);
        vox.BoolAdd(new(Polygon.mesh_revolved(face_out, zr)));

        float CR = 1.5f*thread_depth;
        Vec2 chamfer_corner = B + new Vec2(CR, 0.5f*taper*CR);
        vox.BoolIntersect(Cone.phied(
            face_out.transz(chamfer_corner.X),
            PI_4,
            Lz: zr[0].X - chamfer_corner.X,
            r0: chamfer_corner.Y
        ).lengthed(chamfer_corner.X - zr[^1].X + 2f*VOXEL_SIZE, 2f*VOXEL_SIZE));

        return vox;
    }
}

public class Flats {
    public float r { get; set; }
    public float Lz { get; set; }
    public float net_Lz => Lz + tip_length_ratio*r;

    public float tip_length_ratio { get; set; }
    public float tip_phi {
        get => PI_2 - atan(tip_length_ratio);
        set => tip_length_ratio = tan(PI_2 - value);
    }

    public bool flats_yeah_or_nah { get; set; }
    public float flats_beta { get; set; }
    public float flats_Lx { get; set; }
    public float flats_Lz { get; set; }

    public Flats(Threads tap, float th, float tip_length_ratio=1f)
            : this(
                tap.major_radius + tap.taper_offset(0f) + th,
                tap.straight_length + th*tan(PI_4 - 0.5f*atan(tip_length_ratio)),
                tip_length_ratio
            ) {
        // Previously, and equivalently:
        /*
        // Simple cylinder into cone, with `th` being the smallest wall
        // thickness.
        // https://www.desmos.com/calculator/xai3ejik2o
        float ang = PI_2 - atan(tip_length_ratio); // tip end half-angle.
        float rhi = major_radius + taper_offset(0f);
        List<Vec2> points = [
            new(0f,                     0f),
            new(0f,                     rhi + th),
            new(-length - th*tan(ang/2f), rhi + th),
            new(-length - rhi*tip_length_ratio - th/sin(ang), 0f),
        ];
        */
        // Boils down to the difference being:
        //  tan(ang/2f) + tip_length_ratio - 1/sin(ang)
        // Which always =0. :)
    }
    public Flats(float r, float Lz, float tip_length_ratio=1f) {
        this.r = r;
        this.Lz = Lz;
        this.tip_length_ratio = tip_length_ratio;
        this.flats_yeah_or_nah = true;
        this.flats_beta = NAN; // determine based on input frame.
        this.flats_Lx = 2f*r; // adds no extra width.
        this.flats_Lz = max(10f, r);
    }

    public Voxels at(Frame face_out) {
        assert(r > 0f);
        assert(Lz >= 0f);
        assert(tip_length_ratio >= 0f);
        assert(isnan(this.flats_beta) || this.flats_beta >= 0f);
        assert(flats_Lx > 0f);
        assert(flats_Lz > 0f);

        List<Vec2> points = [
            new(0f, 0f),
            new(0f, r),
            new(-Lz, r),
            new(-net_Lz, 0f),
        ];
        Voxels vox = new(Polygon.mesh_revolved(face_out, points));

        if (!flats_yeah_or_nah)
            return vox;

        // Place flats on +-X. Note these are external flats, to not comprimise
        // strength/minimum thickness.
        float flats_beta = this.flats_beta;
        if (isnan(flats_beta)) {
            // Default to max-phi 45deg flats, but lets do fatter flats if we can
            // print them (aka is vertical).
            flats_beta = nearvert(face_out.Z) ? PI/3f : PI/4f;
        }
        Vec2 corner_edge = flats_Lx/2f * new Vec2(1f, tan(flats_beta/2f));
        assert(corner_edge.Y <= r);
        Vec2 flat_U = corner_edge / mag2(corner_edge);
        Vec2 flat_V = rot90ccw(flat_U);
        Vec2 tangent_edge = flat_U * r*r
                          + flat_V * r*sqrt(mag2(corner_edge) - r*r);

        List<Vec2> xy = [
            corner_edge,
            tangent_edge,
            flipx(tangent_edge),
            flipx(corner_edge),
            -corner_edge,
            -tangent_edge,
            flipy(tangent_edge),
            flipy(corner_edge),
        ];
        // Excess extrude and we'll clip to conical.
        Voxels vox_flats = new(Polygon.mesh_extruded(face_out, -net_Lz, xy));
        // Clip flats to the intended depth w 45deg cone.
        if (flats_Lz < +INF) {
            vox_flats.BoolIntersect(Cone.phied(
                face_out,
                -PI_4,
                -flats_Lz - mag(corner_edge)
            ));
        }
        // Clip to total supporting boundary, w requested angle.
        if (tip_length_ratio > 0.01f) {
            vox_flats.BoolIntersect(Cone.phied(face_out, -tip_phi, -net_Lz));
        } else {
            // Just flat.
            vox_flats.BoolIntersect(new Rod(
                face_out,
                -net_Lz,
                mag(corner_edge))
            );
        }

        vox.BoolAdd(vox_flats);
        return vox;
    }
}

}
