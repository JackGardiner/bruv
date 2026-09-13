using static br.Br;
using br;
using TPIAP = TwoPeasInAPod;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using BBox3 = PicoGK.BBox3;

public class Caplugs : TPIAP.Pea {
    public float axial_slop = 0.05f;
    public float radial_slop = 0.10f;

    public string name => "caplugs";

    public void initialise() {}
    public void set_modifiers(int mods) {
        _ = popbits(ref mods, TPIAP.MINIMISE_MEM);
        _ = popbits(ref mods, TPIAP.LOOKIN_FANCY);
        assert(mods == 0, $"disallowed modifiers: 0x{mods:X}");
    }

    public void anything()
        => throw new NotImplementedException();
    public Voxels? cutaway(in Voxels part)
        => throw new NotImplementedException();
    public void drawings(in Voxels part)
        => throw new NotImplementedException();


    public Voxels? voxels() {
        cap("SWG-1/4");
        cap("SWG-3/8");
        cap("SWG-1/2");
        cap("SWG-3/4");
        cap("BSPP-1/8");
        cap("BSPP-1/4");
        cap("BSPP-1/2");
        cap("AN-6");
        plug("SWG-1/4", 1f/4f*25.4f + 1.3f);
        plug("SWG-3/8", 3f/8f*25.4f + 1.35f);
        plug("SWG-1/2", 1f/2f*25.4f + 2.00f);
        plug("SWG-3/4", 3f/4f*25.4f + 2.32f);
        plug("BSPP-1/8");
        plug("BSPP-1/4");
        plug("BSPP-1/2");
        plug("AN-6");
        plug("UNF-9/16");
        cover("THRU-3/8", 3f/8f*25.4f - 0.18f, 9f);

        return null; // not really a single-voxels pea.
    }

    protected Frame place_at = new();

    protected Voxels knurling(float Lr, bool chamfer_top) {

        // Cheeky knurling.
        float Dt = 4.0f;
        float Dr = 1.5f;
        float FR = 0.2f;
        float CR = 1.0f;
        float Lz = 1.5f*Dt + CR + (chamfer_top ? CR : 0f);
        int tips = max(3, iround(TWOPI*Lr / Dt));
        int points = 2*tips;

        float dthetadz = 1f / Lr; // ideally 45deg helix.
        int rows = max(1, iround(dthetadz * points*Lz/TWOPI));
        dthetadz = rows * TWOPI/points/Lz;

        List<Vec2> star = [];
        points += points & 1;
        for (int i=0; i<points; ++i) {
            float theta = i*TWOPI/points;
            float r = Lr - Dr*(i & 1);
            star.Add(frompol(r, theta));
        }

        List<Frame> LHS = [];
        List<Frame> RHS = [];
        int layers = iceil(Lz / VOXEL_SIZE / 2.0f);
        for (int i=0; i<layers; ++i) {
            float z = lerp(0f, Lz, i, layers);
            float theta = (z - (chamfer_top ? CR : 0f)) * dthetadz;
            Frame frame = place_at.flipzx().transz(z);
            LHS.Add(frame.rotxy(+theta));
            RHS.Add(frame.rotxy(-theta));
        }

        Voxels v = new(Polygon.mesh_swept(
            new FramesSequence(LHS),
            new Slice<Vec2>(star, tile: layers)
        ));
        v.BoolAdd(new(Polygon.mesh_swept(
            new FramesSequence(RHS),
            new Slice<Vec2>(star, tile: layers)
        )));

        v.BoolIntersect(Cone.phied(
            place_at.transz(-Lz),
            PI_4,
            Lz: Lz,
            r0: Lr - CR
        ).lengthed(VOXEL_SIZE, VOXEL_SIZE));
        if (chamfer_top) {
            v.BoolIntersect(Cone.phied(
                place_at.flipzx(),
                PI_4,
                Lz: Lz,
                r0: Lr - CR
            ).lengthed(VOXEL_SIZE, VOXEL_SIZE));
        }

        Fillet.both(v, FR, inplace: true);

        return v;
    }


    protected Studding plug(string size, float pipe_diam=NAN) {
        Studding stud = new(size);
        stud.major_diameter -= 2f*(radial_slop - axial_slop);
        stud.minor_diameter -= 2f*(radial_slop - axial_slop);
        stud.threaded_length = 4.0f*stud.pitch + stud.pitch /* buried */;
        stud.extra_length = 0f;
        stud.incomplete_lower_length = 0.5f*stud.pitch;
        stud.incomplete_upper_length = 0f;
        stud.unprintable_ending_CR = 1.0f*stud.thread_depth;

        float r = stud.major_radius + stud.taper_offset(0f) + 4f;
        Voxels v = knurling(r, chamfer_top: true);

        Voxels male = stud.at(place_at.transz(-stud.pitch).flipzx(),
                extra: 2f*VOXEL_SIZE);
        male.Offset(-axial_slop);
        v.BoolAdd(male);

        float CR = min(0.9f, 0.11f*stud.minor_radius);
        float Lr = ifnan(pipe_diam/2f,
                   (stud.minor_radius + stud.taper_offset(0f)) * 0.78f);
        float r_inner = stud.minor_radius
                      + stud.inner_thread_truncation
                      + stud.taper_offset(-stud.incomplete_upper_length
                                        + stud.straight_length)
                      - stud.unprintable_ending_CR
                      - axial_slop*tan(torad(22.5f));
        float r_outer = Lr + CR;
        float early_cutoff = max(r_outer - r_inner, 0f);
        float z = -4.7f;
        float Lz = stud.straight_length - stud.pitch
                 - axial_slop
                 - early_cutoff
                 - z;
        Voxels hole = new Rod(place_at.transz(z), Lz, Lr)
                .extended(10f, Extend.UP);
        hole.BoolIntersect(Cone.phied(
            place_at.transz(z),
            PI_4,
            Lz: Lz,
            r0: Lr - CR
        ).lengthed(VOXEL_SIZE, 10f));
        hole.BoolAdd(Cone.phied(
            place_at.transz(z + Lz - CR),
            PI_4,
            Lz: CR,
            r0: Lr
        ).lengthed(VOXEL_SIZE, 10f));
        v.BoolSubtract(hole);

        if (early_cutoff > 0f) {
            float FR = 0.4f;
            float top_r = r_outer;
            Voxels fillet = new Rod(
                place_at.transz(z + Lz),
                -FR*SQRT2,
                top_r - FR/SQRT2,
                top_r + FR/SQRT2
            ).extended(VOXEL_SIZE, Extend.UP);
            fillet.BoolSubtract(new Donut(
                place_at.transz(z + Lz - FR*SQRT2),
                top_r,
                FR
            ));
            v.BoolSubtract(fillet);
        }

        ImageSignedDist img = new($".labels/{size.Replace("/", "_")}.tga",
                scale: 1);
        v.BoolSubtract(img.voxels_on_plane(place_at.transz(-8f), 0.5f, 13f));

        Mesh m = new(v);
        Geez.mesh(m);
        TPIAP.save_mesh_only($"PLUG-{size.Replace("/", "_")}", m);
        place_at = place_at.transx(40f);

        return stud;
    }


    protected Tapping cap(string size) {
        Tapping tap = new(size);
        tap.major_diameter += 2f*(radial_slop - axial_slop);
        tap.minor_diameter += 2f*(radial_slop - axial_slop);
        tap.threaded_length = 12.0f*tap.pitch;
        tap.extra_length = 0f;
        tap.incomplete_lower_length = 0.5f*tap.pitch;
        tap.incomplete_upper_length = 0f;
        tap.unprintable_entrance_CR = 2.0f*tap.thread_depth;

        float r = max(tap.major_radius + tap.taper_offset(0f) + 4f,
                      (tap.major_radius + tap.taper_offset(0f)) * 1.42f);
        Voxels v = knurling(r, chamfer_top: true);

        float CR = min(0.9f, 0.11f*tap.minor_radius);
        float Lr = (tap.major_radius + tap.taper_offset(0f)) * 1.18f;
        float r_outer = tap.minor_radius
                      + tap.inner_thread_truncation
                      + tap.taper_offset(-tap.incomplete_upper_length)
                      + tap.unprintable_entrance_CR
                      + axial_slop*tan(torad(22.5f));
        float r_inner = Lr - CR;
        float early_cutoff = max(r_outer - r_inner, 0f);
        float z = tap.straight_length - 3.7f;
        float Lz = tap.straight_length;
        Voxels rod = new Rod(place_at.transz(z), -Lz, Lr)
                .extended(VOXEL_SIZE, Extend.DOWN);
        rod.BoolIntersect(Cone.phied(
            place_at.transz(z).flipzx(),
            PI_4,
            Lz: Lz,
            r0: Lr - CR
        ).lengthed(VOXEL_SIZE, VOXEL_SIZE));
        rod.BoolAdd(Cone.phied(
            place_at,
            -PI_4,
            Lz: CR,
            r0: Lr + CR
        ).lengthed(VOXEL_SIZE, VOXEL_SIZE));
        v.BoolAdd(rod);

        Voxels female = tap.at(place_at.transz(z + early_cutoff),
                extra: 2f*VOXEL_SIZE);
        female.Offset(axial_slop);
        female.BoolIntersect(new Rod(place_at.transz(z - Lz - 1f), Lz + 1f, Lr)
                .extended(VOXEL_SIZE, Extend.UPDOWN));
        v.BoolSubtract(female);

        if (early_cutoff > 0f) {
            float FR = 0.4f;
            float top_r = ave(r_inner, r_outer);
            float Dz = 0.5f*(r_outer - r_inner);
            Voxels fillet = new Rod(
                place_at.transz(z - Dz),
                -FR*SQRT2,
                top_r - FR/SQRT2,
                top_r + FR/SQRT2
            ).extended(VOXEL_SIZE, Extend.UP);
            fillet.BoolSubtract(new Donut(
                place_at.transz(z - Dz - FR*SQRT2),
                top_r,
                FR
            ));
            v.BoolSubtract(fillet);
        }

        ImageSignedDist img = new($".labels/{size.Replace("/", "_")}.tga",
                scale: 1);
        v.BoolSubtract(img.voxels_on_plane(place_at.transz(-8f), 0.5f, 13f));

        Mesh m = new(v);
        Geez.mesh(m);
        TPIAP.save_mesh_only($"CAP-{size.Replace("/", "_")}", m);
        place_at = place_at.transx(40f);

        return tap;
    }


    protected void cover(string name, float bore_diameter, float bore_length) {
        float r = max(bore_diameter/2f + 4f,
                      bore_diameter/2f * 1.42f);
        Voxels v = knurling(r, chamfer_top: true);

        float CR = min(0.9f, 0.13f*bore_diameter/2f);
        float Lr = bore_diameter/2f * 1.4f;
        float z = bore_length - 4.7f;
        float Lz = bore_length;
        Voxels rod = new Rod(place_at.transz(z), -Lz, Lr)
                .extended(VOXEL_SIZE, Extend.DOWN);
        rod.BoolIntersect(Cone.phied(
            place_at.transz(z).flipzx(),
            PI_4,
            Lz: Lz,
            r0: Lr - CR
        ).lengthed(VOXEL_SIZE, VOXEL_SIZE));
        rod.BoolAdd(Cone.phied(
            place_at,
            -PI_4,
            Lz: CR,
            r0: Lr + CR
        ).lengthed(VOXEL_SIZE, VOXEL_SIZE));
        v.BoolAdd(rod);

        int prongs = 8;
        float Lr_prong = 0.45f;
        Voxels female = new Rod(
            place_at.transz(z),
            -bore_length,
            bore_diameter/2f + 2.35f*Lr_prong
        ).extended(VOXEL_SIZE, Extend.UP);

        Voxels prong = new Cone(
            place_at.transz(z - bore_length),
            bore_length/2f,
            (bore_diameter/2f + Lr_prong) * 1.05f,
            bore_diameter/2f + Lr_prong
        ).lengthed(0f, VOXEL_SIZE);
        prong.BoolAdd(new Cone(
            place_at.transz(z - bore_length/2f),
            bore_length/2f,
            bore_diameter/2f + Lr_prong,
            (bore_diameter/2f + Lr_prong) * 1.05f
        ).lengthed(VOXEL_SIZE, VOXEL_SIZE));
        prong.BoolSubtract(new Cone(
            place_at.transz(z - bore_length),
            bore_length/2f,
            bore_diameter/2f * 1.05f,
            bore_diameter/2f
        ).lengthed(VOXEL_SIZE, VOXEL_SIZE));
        prong.BoolSubtract(new Cone(
            place_at.transz(z - bore_length/2f),
            bore_length/2f,
            bore_diameter/2f,
            bore_diameter/2f * 1.05f
        ).lengthed(VOXEL_SIZE, VOXEL_SIZE));

        Voxels support = new Rod(
            place_at.transz(z),
            -bore_length,
            bore_diameter/2f * 1.045f,
            bore_diameter/2f + 3f*Lr_prong
        ).extended(VOXEL_SIZE, Extend.UP);

        for (int i=0; i<prongs; ++i) {
            float theta = i*TWOPI/prongs;
            float Ltheta = TWOPI/prongs * 0.5f;
            Voxels this_prong = Sectioner.pie(theta - Ltheta/2f,
                                              theta + Ltheta/2f).cut(prong);
            female.BoolSubtract(this_prong);
            Voxels this_support = Sectioner.pie(
                    theta + PI/prongs - (TWOPI/prongs - Ltheta)*0.68f/2f,
                    theta + PI/prongs + (TWOPI/prongs - Ltheta)*0.68f/2f)
                .cut(support);
            female.BoolSubtract(this_support);
        }
        female.BoolAdd(Cone.phied(
            place_at.transz(z - 1.5f*CR),
            PI_4,
            Lz: 1.5f*CR,
            r0: bore_diameter/2f
        ).lengthed(VOXEL_SIZE, VOXEL_SIZE));
        v.BoolSubtract(female);

        float r_outer = bore_diameter/2f + 1.5f*CR;
        float r_inner = Lr - CR;
        if (r_outer > r_inner) {
            float FR = 0.4f;
            float top_r = ave(r_inner, r_outer);
            float diff_r = r_outer - r_inner;
            Voxels fillet = new Rod(
                place_at.transz(z - diff_r/2f),
                -FR*SQRT2,
                top_r - FR/SQRT2,
                top_r + FR/SQRT2
            ).extended(VOXEL_SIZE, Extend.UP);
            fillet.BoolSubtract(new Donut(
                place_at.transz(z - diff_r/2f - FR*SQRT2),
                top_r,
                FR
            ));
            v.BoolSubtract(fillet);
        }

        ImageSignedDist img = new($".labels/{name.Replace("/", "_")}.tga",
                scale: 1);
        v.BoolSubtract(img.voxels_on_plane(place_at.transz(-8f), 0.5f, 13f));

        Mesh m = new(v);
        Geez.mesh(m);
        TPIAP.save_mesh_only($"COVER-{name.Replace("/", "_")}", m);
        place_at = place_at.transx(40f);
    }

}
