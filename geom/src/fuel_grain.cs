using static br.Br;
using br;
using TPIAP = TwoPeasInAPod;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using BBox3 = PicoGK.BBox3;

public class FuelGrain : TPIAP.Pea {
    public float axial_slop = 0.05f;
    public float radial_slop = 0.10f;

    public string name => "fuel-grain";

    public void initialise() {}
    public bool minimise_mem = false;
    public void set_modifiers(int mods) {
        minimise_mem = popbits(ref mods, TPIAP.MINIMISE_MEM);
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
        print($"Voxel size: {VOXEL_SIZE} mm");
        print();

        Gyroid gyroid = new Gyroid(
            new(3f*uX3),    // random gyroid offset.
            th: 1.0f,       // gyroid wall thickness.
            period: 20.0f   // gyroid period.
        );
        Rod shape = new Rod(
            new(),
            Lz: 100f,       // fuel grain length.
            inner_r: 59f/2, // fuel grain bore radius.
            outer_r: 95f/2  // fuel grain outer radius.
        );
        Rod wall = new Rod(
            new(),
            Lz: shape.Lz,
            outer_r: shape.outer_r
        ).shelled(-1f);     // outer wall thickness.
        Rod bbase = new Rod(new(),
            Lz: 3f,         // base thickness.
            inner_r: shape.inner_r,
            outer_r: shape.outer_r
        );


        PartMaker get_part() {
            PartMaker part = new();
            part.total_volume_to_calc_volumetric_fill =
                shape.Lz*PI*(sqed(shape.outer_r) - sqed(shape.inner_r));
            return part;
        }

        Voxels? foundation = null;
        Voxels get_foundation() {
            if (foundation == null) {
                foundation = (Voxels)shape;
                foundation.IntersectImplicit(gyroid);
            }
            return foundation.voxDuplicate();
        }

        Geez.Cycle key = new();
        void emit(Voxels vox, string name) {
            Mesh m = new(vox);
            key <<= Geez.mesh(m);
            TPIAP.save_mesh_only($"{DateTime.Now:yyyyMMdd}-{name}", m);
        }


        {
            using PartMaker part = get_part();
            part.voxels = get_foundation();
            emit(part.voxels, "gyroid-no-wall-no-base");
        }
        {
            using PartMaker part = get_part();
            part.voxels = get_foundation();
            part.voxels.BoolAdd(wall);
            emit(part.voxels, "gyroid-walled-no-base");
        }
        {
            using PartMaker part = get_part();
            part.voxels = get_foundation();
            part.voxels.BoolAdd(bbase);
            emit(part.voxels, "gyroid-no-wall-based");
        }
        {
            using PartMaker part = get_part();
            part.voxels = get_foundation();
            part.voxels.BoolAdd(wall);
            part.voxels.BoolAdd(bbase);
            emit(part.voxels, "gyroid-walled-based");
        }

        return null; // not really a single-voxels pea.
    }
}
