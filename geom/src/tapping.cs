using static br.Br;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;

namespace br {

public class Tapping {
    public string size { get; }
    public bool printable { get; }

    public bool right_handed { get; set; }
    public float major_diameter { get; set; }
    public float minor_diameter { get; set; }
    public float pitch { get; set; }
    public float taper { get; set; } // diametral reduction per unit depth.
    public float gauge_depth { get; set; } // only relevant when tapered.

    public float threaded_depth { get; set; }  // initialised to typical.
    public float extra_depth { get; set; }     // initialised to smth tiny.
    public float tip_depth_ratio { get; set; } // initialised to 1 if printable,
                                               // otherwise ~0.6.

    public float bore_diameter // conservative material removal:
        => 0.9f*minor_diameter + taper_offset(straight_depth);

    public float major_radius => 0.5f*major_diameter;
    public float minor_radius => 0.5f*minor_diameter;
    public float bore_radius  => 0.5f*bore_diameter;

    public float straight_depth => threaded_depth + 2f*pitch + extra_depth;

    public float minor_thread_truncation { get; set; } = 0.1f;
        // Note that 0.1mm is within tolerance for all sizes.

    public Tapping(string size, bool printable=true) {
        Dictionary<string, List<float>> lookup = new([
// Cheeky indentation cut.
/*                                                           req.  */
/*                                                         length  */
/*                  major    minor                 gauge   /major  */
/*         size     diam.    diam.   pitch  taper  depth    diam.  */
new(      "M1", [    1.0f,  0.729f,  0.25f,    0f,    0f,    2.0f ] ),
new(      "M2", [    2.0f,  1.567f,  0.40f,    0f,    0f,    2.0f ] ),
new(      "M3", [    3.0f,  2.459f,  0.50f,    0f,    0f,   1.75f ] ),
new(      "M4", [    4.0f,  3.242f,  0.70f,    0f,    0f,   1.75f ] ),
new(      "M5", [    5.0f,  4.134f,  0.80f,    0f,    0f,    1.5f ] ),
new(      "M6", [    6.0f,  4.917f,  1.00f,    0f,    0f,    1.5f ] ),
new(      "M8", [    8.0f,  6.647f,  1.25f,    0f,    0f,    1.5f ] ),
new(     "M10", [   10.0f,  8.376f,  1.50f,    0f,    0f,   1.25f ] ),
new(     "M12", [   12.0f, 10.106f,  1.75f,    0f,    0f,   1.25f ] ),
new(     "M14", [   14.0f, 11.835f,  2.00f,    0f,    0f,   1.25f ] ),
new(     "M16", [   16.0f, 13.835f,  2.00f,    0f,    0f,   1.25f ] ),

new(   "G1/16", [  7.723f,  6.561f, 0.907f,    0f,    0f,    0.9f ] ),
new(    "G1/8", [  9.728f,  8.566f, 0.907f,    0f,    0f,    0.9f ] ),
new(    "G1/4", [ 13.157f, 11.445f, 1.337f,    0f,    0f,    0.9f ] ),
new(    "G3/8", [ 16.662f, 14.950f, 1.337f,    0f,    0f,    0.9f ] ),
new(    "G1/2", [ 20.955f, 18.631f, 1.814f,    0f,    0f,    0.8f ] ),
new(    "G5/8", [ 22.911f, 20.587f, 1.814f,    0f,    0f,    0.8f ] ),
new(    "G3/4", [ 26.441f, 24.117f, 1.814f,    0f,    0f,    0.8f ] ),
new(    "G7/8", [ 30.201f, 27.877f, 1.814f,    0f,    0f,    0.8f ] ),
new(      "G1", [ 33.249f, 30.291f, 2.309f,    0f,    0f,    0.7f ] ),
new(  "G1-1/8", [ 37.897f, 34.939f, 2.309f,    0f,    0f,    0.7f ] ),
new(  "G1-1/4", [ 41.910f, 38.952f, 2.309f,    0f,    0f,    0.7f ] ),
new(  "G1-1/2", [ 47.803f, 44.845f, 2.309f,    0f,    0f,    0.7f ] ),

new(  "Rc1/16", [  7.723f,  6.561f, 0.907f, 1f/16,  4.9f,    1.1f ] ),
new(   "Rc1/8", [  9.728f,  8.566f, 0.907f, 1f/16,  4.9f,    1.0f ] ),
new(   "Rc1/4", [ 13.157f, 11.445f, 1.337f, 1f/16,  7.3f,    1.0f ] ),
new(   "Rc3/8", [ 16.662f, 14.950f, 1.337f, 1f/16,  7.7f,   0.85f ] ),
new(   "Rc1/2", [ 20.955f, 18.631f, 1.814f, 1f/16, 10.0f,   0.85f ] ),
new(   "Rc3/4", [ 26.441f, 24.117f, 1.814f, 1f/16, 11.3f,    0.8f ] ),
new(     "Rc1", [ 33.249f, 30.291f, 2.309f, 1f/16, 12.7f,    0.7f ] ),
new( "Rc1-1/4", [ 41.910f, 38.952f, 2.309f, 1f/16, 15.0f,    0.7f ] ),
new( "Rc1-1/2", [ 47.803f, 44.845f, 2.309f, 1f/16, 15.0f,    0.7f ] ),
        ]);
        List<float> entry = lookup[size];

        this.size = size;
        this.printable = printable;

        this.right_handed = true; // yeah.
        this.major_diameter  = entry[0];
        this.minor_diameter  = entry[1];
        this.pitch           = entry[2];
        this.taper           = entry[3];
        this.gauge_depth     = entry[4] * 1.15f; // 15% sf.
        float req_length_div_major_diam = entry[5];

        this.threaded_depth = this.major_diameter * req_length_div_major_diam;
        this.extra_depth = this.major_diameter * 0.05f;
                                                // 112deg tip:
        this.tip_depth_ratio = printable ? 1f : 1f/tan(torad(112f/2));
    }


    public float taper_offset(float depth)
        => 0.5f*(threaded_depth - gauge_depth - depth)*taper;

    public void straight_bounds(out Vec2 A, out Vec2 B) {
        A = new(0f, minor_radius + minor_thread_truncation);
        B = new(-straight_depth, A.Y);
        // Include taper.
        A.Y += taper_offset(-A.X);
        B.Y += taper_offset(-B.X);
    }

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
        int count = (int)(dcount * (threaded_depth + 3f*pitch));

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
        for (int i=0; i<count; ++i) {
            float z1 = lerp(0.5f*pitch, -threaded_depth - 2.5f*pitch, i, count);
            float z0 = z1 + 0.5f*pitch;
            float z2 = z1 - 0.5f*pitch;

            // Frame rotates full every pitch. Initial rotation is unimportant.
            // WELLLLL it is important that its fucking right handed. bloody
            // brilliant mate. well done.
            float by = TWOPI * +z1/pitch;
            if (!right_handed)
                by = -by;
            frames.Add(face_out.rotxy(by).swapzx());

            // Last few pitches are shrinking as-if cutting bit easing in.
            float Dr = minor_radius + minor_thread_truncation - major_radius;
            float t = (-z1 - threaded_depth) / (2f*pitch);
            // Highkey tho make it drop off quick to avoid artefacts in mesh.
            // Specifically, start dropping off quickly when the spike height is
            // only two voxels.
            float cutoff = max(0.8f, 1f + 2f*VOXEL_SIZE/Dr);
            if (t > cutoff)
                t += 100f*sqed(t - cutoff);
            t = clamp(t, 0f, 1.1f);
            float falloff = t*Dr;

            float r0 = rlo + falloff + taper_offset(-z0);
            float r1 = rhi + falloff + taper_offset(-z1);
            float r2 = rlo + falloff + taper_offset(-z2);

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

        // Truncate the inner pointy part of the thread.
        straight_bounds(out Vec2 A, out Vec2 B);
        v.BoolAdd(new Cone(face_out, B.X - A.X, A.Y, B.Y));

        // Clip the top and bottom to be flat (it was created with extra pitch
        // lengths) and threads to not extend past major.
        v.BoolIntersect(new Cone(
            face_out,
            -straight_depth,
            major_radius + taper_offset(0f),
            major_radius + taper_offset(straight_depth)
        ));

        return v;
    }


    public Voxels hole(Frame face_out, float extra=2f) {
        straight_bounds(out Vec2 A, out Vec2 B);

        if (!printable) {
            // Hole will look like the threads with a cone tip and a small round
            // slug at the top.
            Voxels vox = threads(face_out);
            List<Vec2> zr = [
                new(A.X + extra, 0f),
                new(A.X + extra, A.Y + (major_radius - minor_radius) + 0.2f),
                new(A.X,         A.Y + (major_radius - minor_radius) + 0.2f),
                A,
                B,
                new(B.X - tip_depth_ratio*B.Y, 0f),
            ];
            Polygon.cull_adjacent_duplicates(zr);
            vox.BoolAdd(new(Polygon.mesh_revolved(face_out, zr)));
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
                face_out * new Vec3(+bore_radius, 0f, -straight_depth),
                face_out * new Vec3(0f, +bore_radius, -straight_depth),
                face_out * new Vec3(-bore_radius, 0f, -straight_depth),
                face_out * new Vec3(0f, -bore_radius, -straight_depth),
                face_out * (uZ3*(-straight_depth - tip_depth_ratio*bore_radius)),
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
                face_out * new Vec3(+SQRTH*bore_radius, 0f, -straight_depth),
                face_out * new Vec3(0f, +bore_radius, -straight_depth),
                face_out * new Vec3(-SQRTH*bore_radius, 0f, -straight_depth),
                face_out * new Vec3(0f, -bore_radius, -straight_depth),
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

    public Voxels supporting(Frame face_out, float th, bool flats=true,
            float tip_depth_ratio=NAN, float depth=NAN, float flats_depth=NAN) {
        assert(th > 0f);

        if (isnan(depth)) {
            depth = straight_depth;
            tip_depth_ratio = ifnan(tip_depth_ratio, 1f);
        } else {
            tip_depth_ratio = ifnan(tip_depth_ratio, 0f);
        }
        assert(tip_depth_ratio >= 0f);


        // Simple cylinder into cone, with `th` being the smallest wall
        // thickness.
        // https://www.desmos.com/calculator/xai3ejik2o
        float ang = PI_2 - atan(tip_depth_ratio); // tip end half-angle.
        float rhi = major_radius + taper_offset(0f);
        List<Vec2> points = [
            new(0f,                     0f),
            new(0f,                     rhi + th),
            new(-depth - th*tan(ang/2f), rhi + th),
            new(-depth - rhi*tip_depth_ratio - th/sin(ang), 0f),
        ];
        float Lz = -points[3].X;
        Mesh mesh = Polygon.mesh_revolved(face_out, points);
        Voxels vox = new(mesh);

        if (!flats)
            return vox;
        flats_depth = ifnan(flats_depth, max(8f, rhi + th));
        flats_depth = min(flats_depth, Lz);

        // Place flats on +-X. Note these are external flats, to not comprimise
        // strength/minimum thickness.
        float flat_ang = PI/8f;
        if (nearvert(face_out.Z))
            flat_ang = PI/6f; // fatter flats if we can print them.
        Vec2 flat_edge = (rhi + th) * new Vec2(1f, tan(flat_ang));
        Vec2 tangent_edge = frompol(rhi + th, 2f*flat_ang);

        List<Vec2> xy = [
            tangent_edge,
            flat_edge,
            flipy(flat_edge),
            flipy(tangent_edge),
            -tangent_edge,
            -flat_edge,
            flipx(flat_edge),
            flipx(tangent_edge),
        ];
        // Excess extrude and we'll clip to conical.
        Mesh m = Polygon.mesh_extruded(face_out, -Lz, xy);
        Voxels vox_flats = new(m);
        // Clip flats to the intended depth w 45deg cone.
        vox_flats.BoolIntersect(Cone.phied(
            face_out.transz(-flats_depth),
            PI_4,
            Lz: flats_depth,
            r0: mag(flat_edge)
        ).upto_tip());
        // Clip to total supporting boundary, w requested angle.
        if (ang < PI_2 - 0.001f) {
            vox_flats.BoolIntersect(Cone.phied(
                face_out.transz(-Lz),
                ang,
                Lz: Lz,
                r0: 0f
            ));
        } else {
            // Just flat.
            vox_flats.BoolIntersect(new Rod(
                face_out,
                -Lz,
                major_radius + taper_offset(0f)
            ));
        }

        vox.BoolAdd(vox_flats);
        return vox;
    }
}


}
