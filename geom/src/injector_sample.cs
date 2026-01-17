using static br.Br;
using br;
using TPIAP = TwoPeasInAPod;

using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using BBox3 = PicoGK.BBox3;

public class InjectorSample : TPIAP.Pea {

    public string name => "injector_sample";

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
    public required InjectorElement element { get; init; }
    public required int no_inj { get; init; }
    public float z0_cone = NAN;
    public void initialise() {
        element.initialise(pm, no_inj, out z0_cone);
    }


    public Voxels? voxels() {
        element.voxels(ZERO2, out Voxels? pos, out Voxels? neg);
        // injector element = pos - neg.

        BBox3 bounds = pos.oCalculateBoundingBox();


        Voxels dividing = Cone.phied(
            new(z0_cone*uZ3),
            PI_4,
            Lz: 20f,
            r0: 0f
        );
        dividing.BoolSubtract(Cone.phied(
            new((z0_cone + 2f*SQRT2)*uZ3),
            PI_4,
            Lz: 30f,
            r0: 0f
        ));


        Rod rod = new Rod(
            new(),
            bounds.vecSize().Z * 1.2f,
            hypot(bounds.vecSize().X, bounds.vecSize().Y) / 2f * 1.2f
        );

        pos.BoolAdd(dividing);
        pos.BoolIntersect(rod);
        float th = 5f;
        pos.BoolAdd(rod.shelled(th));
        pos.BoolAdd(new Rod(new(), 3f, rod.r + th));
        pos.BoolAdd(new Rod(new((rod.Lz - 0.05f)*uZ3), th, rod.r + th));


        Voxels vox = pos; // no copy.
        vox.BoolSubtract(neg);
        vox.TripleOffset(0.03f);


        float R = rod.r + th;
        float r0 = nonhypot(R, R/3.5f);
        Bar bar = new Bar(
            new(),
            2f*r0,
            2f*r0,
            rod.Lz + 2f*th
        ).extended(3f, Extend.UPDOWN);
        vox.BoolSubtract(bar.transx(+2f*r0));
        vox.BoolSubtract(bar.transx(-2f*r0));
        vox.BoolSubtract(bar.transy(+2f*r0));
        vox.BoolSubtract(bar.transy(-2f*r0));


        Frame at0 = new(new Vec3(rod.r/2f, 0f, rod.Lz));
        Voxels voxport0 = new Rod(at0, 12f, 9.73f/2f)
                .shelled(5f)
                .extended(1f, Extend.DOWN);
        voxport0.BoolSubtract(new Rod(at0.transz(0.5f), -2f, 20f));

        vox.BoolAdd(voxport0);
        vox.BoolSubtract(new Rod(at0.transz(-0.5f), 30f, 9.73f/2f));

        Frame at1 = new(new Vec3(-rod.r/1.5f, 0f, rod.Lz));
        Geez.frame(at1);
        Geez.rod(new Rod(at1, 10f, 9.73f/2f));
        // Voxels voxport1 = new Rod(at0, 20f, 9.73f)
        //         .shelled(5f)
        //         .extended(3f, Extend.DOWN);
        // voxport1.BoolSubtract(new Rod(at0.transz(1f), -5f, 20f));

        // vox.BoolAdd(voxport1);
        // vox.BoolSubtract(new Rod(at1.transz(-5f), 30f, 9.73f));

        BBox3 outer = vox.oCalculateBoundingBox();
        print(outer.vecSize());

        Geez.voxels(vox);

        return vox;
    }
}
