using static br.Br;
using br;
using TPIAP = TwoPeasInAPod;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using BBox3 = PicoGK.BBox3;

public class InjectorSample : TPIAP.Pea {

    public string name => "injector-sample";

    public void anything()
        => throw new NotImplementedException();
    public Voxels? cutaway(in Voxels part)
        => throw new NotImplementedException();
    public void drawings(in Voxels part)
        => throw new NotImplementedException();

    public bool printable = false;
    public void set_modifiers(int mods) {
        printable = popbits(ref mods, TPIAP.PRINTABLE);
        _ = popbits(ref mods, TPIAP.MINIMISE_MEM);
        _ = popbits(ref mods, TPIAP.LOOKIN_FANCY);
        if (mods != 0)
            throw new NotImplementedException();
    }

    public float extend_base_by => printable ? 3f : 0f;

    public required PartMating pm { get; init; }
    public required InjectorElement[] element { get; init; }
    public required int[] no_inj { get; init; }
    public required float th_plate { get; init; }
    public required float th_dmw { get; init; }

    public int N => numel(element);
    public void initialise() {
        assert(N == numel(element));
        assert(N == numel(no_inj));
        for (int i=0; i<N; ++i) {
            element[i].initialise(pm, no_inj[i]);
            File.Move(
                InjectorElement.REPORT_PATH,
                fromroot($"exports/injector-sample-{i}-report.txt"),
                overwrite: true
            );
        }
    }


    public Voxels make(Frame at, InjectorElement element, ImageSignedDist img,
            bool datum_on_opposite) {

        const float EXTRA = 30f;
        const float th_outer = 5f;

        // position inlets s.t. port isnt straight on one.
        element.voxels(
                at.rotxy(-PI_2 - 2/3f*PI/element.no_il2), th_plate, th_dmw,
                out Voxels? pos, out Voxels? neg,
                please_put_the_inner_injector_on_the_build_plate: printable
            );
        // injector element = pos - neg.

        Rod interior /* kinda, loosy goosy top and bottom (dujj) */ = new(
            at,
            element.max_z - 1f,
            element.max_r + 2f
        );

        float R = interior.r + th_outer;
        float Rmid = interior.r + th_outer/2;


        /* WALLS */

        Cone top_cone = Cone.phied(
            at.transz(interior.Lz + interior.r).flipzx(),
            PI_4,
            r1: interior.r + 3f
        ).shelled(th_outer)
         .upto_tip();
        float Lz_side = (at / top_cone.centre.pos).Z
                      - lerp(-top_cone.Lz/2f, +top_cone.Lz/2f,
                             invlerp(top_cone.outer_r0, top_cone.outer_r1, R));
        Voxels top = (Voxels)top_cone;
        top.BoolIntersect(interior
                .girthed(R)
                .extended(EXTRA, Extend.UPDOWN));

        Voxels bot = new Rod(at, th_plate, Rmid)
                .extended(extend_base_by, Extend.DOWN);

        Voxels dividing = Cone.phied(
            at.transz(element.z0_dmw),
            element.phi,
            r1: R
        ).shelled(-th_dmw);
        dividing.BoolIntersect(interior
                .girthed(Rmid)
                .extended(EXTRA, Extend.UPDOWN));

        Voxels sides = interior.shelled(th_outer)
                .extended(extend_base_by, Extend.DOWN);

        Voxels vox = pos; // no copy.
        vox.BoolAdd(top);
        vox.BoolAdd(bot);
        vox.BoolAdd(dividing);
        vox.BoolAdd(sides);
        vox.BoolSubtract(neg);


        /* FLATS */

        float flat_length = R/3.5f;
        float flat_off = nonhypot(R, flat_length);
        Bar bar = new Bar(
            at,
            Lz_side,
            2.1f*flat_length
        ).extended(EXTRA, Extend.UPDOWN);
        // vox.BoolSubtract(bar.transx(+flat_off).at_face(Bar.X1));
        // ^ overriden by port pad.
        // vox.BoolSubtract(bar.transx(-flat_off).at_face(Bar.X0));
        // ^ at stag point, lets not thin it.
        vox.BoolSubtract(bar.transy(+flat_off).at_face(Bar.Y1));
        vox.BoolSubtract(bar.transy(-flat_off).at_face(Bar.Y0));


        /* DATUM? */
        const float th_datum = 3f;
        Vec3 datum_orient = ONE3;
        if (datum_on_opposite)
            datum_orient = flipy(datum_orient);
        Vec3 datum_corner = new(-R, -flat_off, 0f);
        Voxels datum = new Bar(
            at.translate(datum_orient * datum_corner/2f),
            R,
            flat_off,
            th_datum
        ).extended(extend_base_by, Extend.DOWN);
        datum.BoolSubtract(interior.extended(EXTRA, Extend.UPDOWN));
        datum.BoolSubtract(
            (Voxels)new Bar(
                at.translate(datum_orient * datum_corner),
                th_datum,
                R/3f
            ).at_edge(datum_on_opposite ? Bar.X1_Y0 : Bar.X1_Y1)
             .extended(EXTRA, Extend.UPDOWN)
            -
            (Voxels)new Rod(
                at.translate(datum_orient * (datum_corner + uXY3*R/3f)),
                th_datum,
                R/3f
            ).extended(EXTRA, Extend.UPDOWN)
            /* syntax is sick as fr */
        );
        vox.BoolAdd(datum);



        /* PORTS */
        Tapping tap = new Tapping("Rc1/8", printable){
            threaded_depth = 8.5f
        };
        float L_BSPPEIGTH = 15f;
        float r_port = tap.major_radius + 5f;
        float spanner_IPA = 18f;
        float spanner_LOx = 18f;

        // side port pad
        Voxels pad = new Bar(
            at,
            interior.r + L_BSPPEIGTH,
            spanner_IPA,
            Lz_side
        ).at_face(Bar.X1)
         .extended(extend_base_by, Extend.DOWN);
        pad.BoolSubtract(interior.extended(EXTRA, Extend.UPDOWN));
        vox.BoolAdd(pad);

        // side port
        Frame at1 = new Frame.Cyl(at).radial(
            new(interior.r + L_BSPPEIGTH, 0f, r_port)
        );
        Voxels port_IPA = tap.hole(at1);
        port_IPA.BoolAdd(new Bar(
            at1.rotxy(PI_4),
            -magxy(at / at1.pos),
            tap.bore_diameter*SQRTH
        ));
        port_IPA.BoolSubtract(interior);
        vox.BoolSubtract(port_IPA);

        // top port
        Frame at0 = top_cone.inner_tip!.flipzx();
        // we get bore radius length for free by trimming cone.
        at0 = at0.transz(L_BSPPEIGTH - tap.bore_radius);
        Voxels port_LOx = new Rod(at0, -L_BSPPEIGTH - R + 4f, r_port);
        port_LOx.BoolSubtract(Cone.phied(top_cone.inner_tip, PI_4, 200f));
        // flats.
        Bar lox_flat = new Bar(
            at0,
            EXTRA,
            EXTRA,
            -L_BSPPEIGTH
        ).extended(EXTRA, Extend.UPDOWN); // big fucking box (dujj).
        port_LOx.BoolSubtract(lox_flat.transy(+spanner_LOx/2f).at_face(Bar.Y1));
        port_LOx.BoolSubtract(lox_flat.transy(-spanner_LOx/2f).at_face(Bar.Y0));
        vox.BoolAdd(port_LOx);
        vox.BoolSubtract(tap.hole(at0));
        vox.BoolSubtract(new Rod(at0, -L_BSPPEIGTH - 0.2f, tap.bore_radius));


        // Worlds cheekiest label.
        float label_height = 4f;
        float label_th = 0.5f;
        float label_z0 = -0.2f;
        // float label_x0 = ave(th_outer, L_BSPPEIGTH);
        // float label_x0 = (2f*th_outer + 3f*L_BSPPEIGTH) / 5f;
        float label_x0 = L_BSPPEIGTH - 2.5f - label_height/2f;
        Frame label = at;
        label = label.transx(interior.r + label_x0);
        label = label.transz(Lz_side + label_z0);
        label = label.rotxy(PI_2);
        vox.BoolAdd(img.voxels_on_plane(
            label,
            label_th - label_z0,
            label_height,
            ImageSignedDist.HEIGHT
        ));



        /* Nozzle extension. */
        if (printable) {
            vox.BoolAdd(new Rod(
                at,
                3*VOXEL_SIZE,
                element.F1.Y,
                element.F1.Y + element.th_nz1
            ).extended(extend_base_by, Extend.DOWN));
        }


        Vec3 pos_IPA = at1.pos - at.pos;
        Vec3 pos_LOx = at0.pos - at.pos;
        print("important sample things:");
        print($"box height: {Lz_side}");
        print($"outer r: {interior.shelled(th_outer).outer_r}");
        print($"big flat w-w: {2f*flat_off}");
        print($"IPA flat w-w: {spanner_IPA}");
        print($"LOx flat w-w: {spanner_LOx}");
        print($"pos-z LOx: {pos_LOx.Z}");
        print($"pos-r IPA: {pos_IPA.X}");
        print($"pos-z IPA: {pos_IPA.Z}");

        return vox;
    }

    public Voxels? voxels() {
        Bar buildplate = new(new(), 0.01f, 100f);
        if (printable)
            Geez.bar(buildplate);

        Vec2[] corrections = [
            new(+2.0f, +3.0f),
            new(+2.0f, +2.5f),
            new(+2.0f, +0.9f),
            new(+2.0f, -0.8f),

            new(-2.0f, +1.45f),
            new(-2.0f, -0.15f),
            new(-2.0f, -2.2f),
            new(-2.0f, -2.85f),
        ];
        int[] remap = [
            0,
            6,
            5,
            3,
            1,
            2,
            4,
            7,
        ];
        Frame get_at(int i) {
            if (!printable)
                return new(i*60*uX3);
            // or try to stack nicely:
            assert(N == 8);

            i = remap[i];
            float x0 = -buildplate.Lx/4f - buildplate.Lx/10f;
            float y0 = -buildplate.Ly/2f + buildplate.Ly/8f;
            if (i >= 4)
                x0 += buildplate.Lx/2f;
            Vec3 point = new(
                x0 + (i % 2) * buildplate.Lx/5f,
                y0 + (i % 4) * buildplate.Ly/4f,
                0f
            );
            point += rejxy(corrections[i]);
            Frame at = new(point);
            if (i % 2 != 0)
                at = at.rotxy(PI);
            return at;
        }

        Voxels all = new();
        for (int i=0; i<N; ++i) {
            print($"Creating injector sample {i}.");
            Frame at = get_at(i);

            ImageSignedDist img = new(
                fromroot($"assets/sample-labels/sample-{i}.tga"),
                invert: true,
                flipy: true
            );

            Voxels vox = make(
                at,
                element[i],
                img,
                i % 2 == 0
            );
            Geez.voxels(vox);
            all.BoolAdd(vox);

            BBox3 bounds = vox.oCalculateBoundingBox();
            print($"Bounding size: {bounds.vecSize()}");

            TPIAP.save_mesh_only($"injector-sample-{i}", new(vox));
        }
        return all;
    }
}
