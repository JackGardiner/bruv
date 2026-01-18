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
    public void set_modifiers(int mods) {
        _ = popbits(ref mods, TPIAP.MINIMISE_MEM);
        _ = popbits(ref mods, TPIAP.LOOKIN_FANCY);
        assert(mods == 0);
    }


    public required PartMating pm { get; init; }
    public required InjectorElement[] element { get; init; }
    public required int[] no_inj { get; init; }
    public required float th_plate { get; init; }
    public required float th_dmw { get; init; }

    public float[] z0_cone = [];
    public float[] max_r = [];
    public int N => numel(element);
    public void initialise() {
        assert(N == numel(element));
        assert(N == numel(no_inj));
        z0_cone = new float[N];
        max_r = new float[N];
        for (int i=0; i<N; ++i) {
            element[i].initialise(pm, no_inj[i]);
            File.Move(
                InjectorElement.REPORT_PATH,
                fromroot($"exports/injector-sample-{i}-report.txt"),
                overwrite: true
            );
        }
    }


    public static Voxels make(Frame at, InjectorElement element, float th_plate,
            float th_dmw, ImageSignedDist img, bool datum_on_opposite) {

        const float EXTRA = 30f;
        const float th_outer = 3f;

        // position inlets s.t. port isnt straight on one.
        element.voxels(
                at.rotxy(-PI_2 - 2/3f*PI/element.no_il2), th_plate, th_dmw,
                out Voxels? pos, out Voxels? neg,
                please_put_the_inner_injector_on_the_build_plate: true
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

        Voxels bot = new Rod(at, th_plate, Rmid);

        Voxels dividing = Cone.phied(
            at.transz(element.z0_dmw),
            element.phi,
            r1: R
        ).shelled(-th_dmw);
        dividing.BoolIntersect(interior
                .girthed(Rmid)
                .extended(EXTRA, Extend.UPDOWN));

        Voxels sides = interior.shelled(th_outer);

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
        );
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
        float r_BSPPEIGTH = 8.2f/2f; // tap drill size (undersize)
        float L_BSPPEIGTH = 15f;
        float r_port = r_BSPPEIGTH + 5f;
        float spanner_IPA = 17f;
        float spanner_LOx = 17f;

        // side port pad
        Voxels pad = new Bar(
            at,
            interior.r + L_BSPPEIGTH,
            spanner_IPA,
            Lz_side
        ).at_face(Bar.X1);
        pad.BoolSubtract(interior.extended(EXTRA, Extend.UPDOWN));
        vox.BoolAdd(pad);

        // side port
        Frame at1 = new Frame.Cyl(at).radial(
            new(interior.r + L_BSPPEIGTH, 0f, r_port)
        ).flipzx();
        Voxels port_IPA = new Bar(
            at1.rotxy(PI_4),
            magxy(at / at1.pos),
            2f * r_BSPPEIGTH*SQRTH // size diag to tap size.
        );
        port_IPA.BoolSubtract(interior);
        vox.BoolSubtract(port_IPA);

        // top port
        Frame at0 = top_cone.inner_tip!.flipzx();
        // we get r_BSPPEIGTH length for free by trimming cone.
        Voxels port_LOx = new Rod(at0, L_BSPPEIGTH - r_BSPPEIGTH, r_port)
                .extended(0.1f + r_port, Extend.DOWN);
        port_LOx.BoolSubtract(top_cone.positive.transz(th_outer*SQRT2));
        // flats.
        Bar lox_flat = new Bar(
            at0,
            EXTRA,
            EXTRA,
            L_BSPPEIGTH
        ).extended(EXTRA, Extend.UPDOWN); // big fucking box (dujj).
        port_LOx.BoolSubtract(lox_flat.transy(+spanner_LOx/2f).at_face(Bar.Y1));
        port_LOx.BoolSubtract(lox_flat.transy(-spanner_LOx/2f).at_face(Bar.Y0));
        vox.BoolAdd(port_LOx);
        vox.BoolSubtract(new Rod(at0, L_BSPPEIGTH - r_BSPPEIGTH, r_BSPPEIGTH)
                .extended(0.1f + r_BSPPEIGTH, Extend.DOWN)
                .extended(EXTRA, Extend.UP));


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


        return vox;
    }

    public Voxels? voxels() {
        Bar buildplate = new(new(), VOXEL_SIZE, 100f);
        TPIAP.save_mesh_only($"buildplate-100x100", buildplate);
        Geez.bar(buildplate);

        assert(N == 8);
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
            // return new(i*50*uX3);
            // or try to stack nicely:

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
                th_plate,
                th_dmw,
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
