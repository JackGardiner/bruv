using static br.Br;
using br;
using TPIAP = TwoPeasInAPod;

using Vec2 = System.Numerics.Vector2;
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
    public required InjectorElement element0 { get; init; }
    public required InjectorElement element1 { get; init; }
    public required int no_inj0 { get; init; }
    public required int no_inj1 { get; init; }
    public float z0_cone0 = NAN;
    public float z0_cone1 = NAN;
    public float max_r_0 = NAN;
    public float max_r_1 = NAN;
    public void initialise() {
        element0.initialise(pm, no_inj0, out z0_cone0, out max_r_0);
        element1.initialise(pm, no_inj1, out z0_cone1, out max_r_1);
    }


    public static Voxels make(Vec2 at, InjectorElement element, float z0_cone,
            float max_r) {

        element.voxels(at, out Voxels? pos, out Voxels? neg);
        // injector element = pos - neg.

        BBox3 bounds = pos.oCalculateBoundingBox();


        Voxels dividing = Cone.phied(
            new(rejxy(at, z0_cone)),
            PI_4,
            Lz: 20f,
            r0: 0f
        );
        dividing.BoolSubtract(Cone.phied(
            new(rejxy(at, z0_cone + 2f*SQRT2)),
            PI_4,
            Lz: 30f,
            r0: 0f
        ));

        Geez.rod(new(new(rejxy(at)), 40f, max_r));


        Rod rod = new Rod(
            new(rejxy(at)),
            bounds.vecSize().Z * 1.2f,
            hypot(bounds.vecSize().X, bounds.vecSize().Y) / 2f * 1.2f
        );

        pos.BoolAdd(dividing);
        pos.BoolIntersect(rod);
        float th = 5f;
        pos.BoolAdd(rod.shelled(th));
        pos.BoolAdd(new Rod(new(rejxy(at)), 3f, rod.r + th));
        pos.BoolAdd(new Rod(new(rejxy(at, rod.Lz - 0.05f)), th, rod.r + th));


        Voxels vox = pos; // no copy.
        vox.BoolSubtract(neg);
        vox.TripleOffset(0.03f);


        float R = rod.r + th;
        float r0 = nonhypot(R, R/3.5f);
        Bar bar = new Bar(
            new(rejxy(at)),
            2f*r0,
            2f*r0,
            rod.Lz + 2f*th
        ).extended(3f, Extend.UPDOWN);
        vox.BoolSubtract(bar.transx(+2f*r0));
        vox.BoolSubtract(bar.transx(-2f*r0));
        vox.BoolSubtract(bar.transy(+2f*r0));
        vox.BoolSubtract(bar.transy(-2f*r0));


        Frame at0 = new(rejxy(at) + new Vec3(rod.r/2f, 0f, rod.Lz));
        Voxels voxport0 = new Rod(at0, 12f, 9.73f/2f)
                .shelled(5f)
                .extended(1f, Extend.DOWN);
        voxport0.BoolSubtract(new Rod(at0.transz(0.5f), -2f, 20f));

        vox.BoolAdd(voxport0);
        vox.BoolSubtract(new Rod(at0.transz(-0.5f), 30f, 9.73f/2f));

        Frame at1 = new(rejxy(at) + new Vec3(-rod.r/1.5f, 0f, rod.Lz));
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
        return vox;
    }

    public Voxels? voxels() {

        Geez.voxels(make(ZERO2, element0, z0_cone0, max_r_0));
        Geez.voxels(make(50*uX2, element1, z0_cone1, max_r_1));
        return null;
    }
}
