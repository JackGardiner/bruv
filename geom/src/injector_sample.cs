using static br.Br;
using br;
using TPIAP = TwoPeasInAPod;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;

public class InjectorSample : TPIAP.Pea {

    public string name => "injector-sample";

    public void anything()
        => throw new NotImplementedException();
    public Voxels? cutaway(in Voxels part)
        => throw new NotImplementedException();
    public void drawings(in Voxels part)
        => throw new NotImplementedException();

    public bool printable_dmls = false;
    public bool printable_sla  = false;
    public void set_modifiers(int mods) {
        printable_dmls = popbits(ref mods, TPIAP.PRINTABLE_DMLS);
        printable_sla  = popbits(ref mods, TPIAP.PRINTABLE_SLA);
        _              = popbits(ref mods, TPIAP.MINIMISE_MEM);
        _              = popbits(ref mods, TPIAP.LOOKIN_FANCY);
        if (mods != 0)
            throw new NotImplementedException();
    }

    public required int index_offset { get; init; } = 0;
    public required InjectorElement[] elements { get; init; }
    public int N => numel(elements);

    public void initialise() {
        for (int i=0; i<N; ++i) {
            elements[i].printable = printable_dmls;
            elements[i].initialise();
            File.Move(
                InjectorElement.REPORT_PATH,
                fromroot(
                    $"exports/injector-sample-{index_offset + i}-report.txt"
                ),
                overwrite: true
            );
        }
    }


    public Voxels make(Frame at, InjectorElement element, ImageSignedDist img,
            bool datum_on_opposite) {

        float EXTRA = 30f;
        float th_datum = 3f;
        float extend_base_by = printable_dmls ? 3f : 0f;
        float th_outer = printable_sla ? 5f : 3f;

        // position inlets s.t. port isnt straight on one.
        element.voxels(at.rotxy(-PI_2 - 2/3f*PI/element.no_il2),
                out Voxels? pos, out Voxels? neg);
        // injector element = pos - neg.

        Rod interior /* kinda, loosy goosy top and bottom (dujj) */ = new(
            at,
            element.max_z - 1f,
            element.max_r + 3f
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

        Voxels bot = new Rod(at, element.th_plate, Rmid)
                .extended(extend_base_by, Extend.DOWN);

        Voxels dividing = Cone.phied(
            at.transz(element.z0_dmw),
            element.phi,
            r1: R
        ).shelled(-element.th_dmw);
        dividing.BoolIntersect(interior
                .girthed(Rmid)
                .extended(EXTRA, Extend.UPDOWN));
        dividing.BoolIntersect(top_cone.positive
                .lengthed(0f, interior.Lz + EXTRA));

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
        if (!printable_sla) { // no part-wide flats resin.
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
        }


        /* DATUM? */
        if (!printable_sla) { // no datum on resin.
            Vec3 datum_orient = ONE3;
            float FR_datum = 4f;
            if (datum_on_opposite)
                datum_orient = flipy(datum_orient);
            Vec3 datum_corner = new(-R, -flat_off, 0f);
            Voxels vox_datum = new Bar(
                at.translate(datum_orient * datum_corner/2f),
                R,
                flat_off,
                th_datum
            ).extended(extend_base_by, Extend.DOWN);
            vox_datum.BoolSubtract(interior.extended(EXTRA, Extend.UPDOWN));
            vox_datum.BoolSubtract(
                (Voxels)new Bar(
                    at.translate(datum_orient * datum_corner),
                    th_datum,
                    FR_datum
                ).at_edge(datum_on_opposite ? Bar.X1_Y0 : Bar.X1_Y1)
                .extended(EXTRA, Extend.UPDOWN)
                -
                (Voxels)new Rod(
                    at.translate(datum_orient * (datum_corner + uXY3*FR_datum)),
                    th_datum,
                    FR_datum
                ).extended(EXTRA, Extend.UPDOWN)
                /* syntax is sick as fr */
            );
            vox.BoolAdd(vox_datum);
        }



        /* PORTS */
        float L_port = 14f;
        Tapping tap = new("Rc1/8", printable_dmls);
        // Drill thru in resin.
        if (printable_sla)
            tap.extra_length = L_port - tap.straight_length + 4f;
        tap.tip_length_ratio = 0f;

        float r_port = tap.major_radius + tap.taper_offset(0f) + 5.5f;

        // side port pad
        Voxels pad = new Bar(
            at,
            interior.r + L_port,
            2*r_port,
            2*r_port
        ).at_face(Bar.X1)
         .extended(extend_base_by, Extend.DOWN);
        pad.BoolSubtract(interior.extended(EXTRA, Extend.UPDOWN));
        vox.BoolAdd(pad);

        // side port
        Frame at1 = new Frame.Cyl(at).radial(
            new(interior.r + L_port, 0f, r_port)
        );
        Voxels port_IPA = tap.at(at1);
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
        at0 = at0.transz(L_port - tap.bore_radius).rotxy(PI_2);
        Voxels port_LOx = new Flats(r_port, L_port + R, 0f).at(at0);
        port_LOx.BoolSubtract(Cone.phied(top_cone.inner_tip, PI_4, 200f));
        vox.BoolAdd(port_LOx);
        vox.BoolSubtract(tap.at(at0));
        vox.BoolSubtract(new Rod(at0, -L_port - 0.2f, tap.bore_radius));


        // Worlds cheekiest label.
        float label_height = 4f;
        float label_th = 0.5f;
        float label_z0 = -0.2f;
        float label_x0 = L_port - 2.5f - label_height/2f;
        Frame label = at;
        label = label.transx(interior.r + label_x0);
        label = label.transz(2*r_port + label_z0);
        label = label.rotxy(PI_2);
        vox.BoolAdd(img.voxels_on_plane(
            label,
            label_th - label_z0,
            label_height,
            ImageSignedDist.HEIGHT
        ));



        /* Nozzle extension. */
        if (printable_dmls) {
            vox.BoolAdd(new Rod(
                at,
                3*VOXEL_SIZE,
                element.F1.Y,
                element.F1.Y + element.th_nz1
            ).extended(extend_base_by, Extend.DOWN));
        }

        return vox;
    }

    public Voxels? voxels() {
        Bar buildplate = new(new(), 0.01f, 100f);
        if (printable_dmls)
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
            return new((i*60*uX3));
            // if (!printable_dmls)
            //     return new(i*60*uX3);
            // // or try to stack nicely:
            // assert(N == 8);

            // i = remap[i];
            // float x0 = -buildplate.Lx/4f - buildplate.Lx/10f;
            // float y0 = -buildplate.Ly/2f + buildplate.Ly/8f;
            // if (i >= 4)
            //     x0 += buildplate.Lx/2f;
            // Vec3 point = new(
            //     x0 + (i % 2) * buildplate.Lx/5f,
            //     y0 + (i % 4) * buildplate.Ly/4f,
            //     0f
            // );
            // point += rejxy(corrections[i]);
            // Frame at = new(point);
            // if (i % 2 != 0)
            //     at = at.rotxy(PI);
            // return at;
        }

        Voxels all = new();
        for (int i=0; i<N; ++i) {

            Frame at = get_at(i);

            ImageSignedDist img = new(
                fromroot(
                    $"assets/sample-labels/sample-{index_offset + i}.tga"
                ),
                invert: true,
                flipy: true
            );

            PartMaker part = new();
            part.name = $"Injector sample {index_offset + i}";
            part.voxels = make(
                at,
                elements[i],
                img,
                i % 2 == 0
            );
            part.Dispose(); // printing onlyyy.

            all.BoolAdd(part.voxels);

            Mesh mesh = new(part.voxels);
            if ((Geez.sectioner ?? Geez.dflt_sectioner).has_cuts())
                Geez.voxels(part.voxels);
            else
                Geez.mesh(mesh);

            TPIAP.save_mesh_only($"injector-sample-{index_offset + i}", mesh);
        }
        return all;
    }
}
