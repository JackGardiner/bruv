using static br.Br;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using IImplicit = PicoGK.IImplicit;
using IBoundedImplicit = PicoGK.IBoundedImplicit;
using BBox3 = PicoGK.BBox3;

namespace br {

public static partial class Br {
    // chuck a couple more constants in un-qualified land.

    public const int EXTEND_UP     = 0x1;
    public const int EXTEND_DOWN   = 0x2;
    public const int EXTEND_UPDOWN = 0x3;

    public const int CORNER_x0y0z0 = 0x0;
    public const int CORNER_x1y0z0 = 0x1;
    public const int CORNER_x0y1z0 = 0x2;
    public const int CORNER_x1y1z0 = 0x3;
    public const int CORNER_x0y0z1 = 0x4;
    public const int CORNER_x1y0z1 = 0x5;
    public const int CORNER_x0y1z1 = 0x6;
    public const int CORNER_x1y1z1 = 0x7;

    public const int EDGE_x0z0 = 0x8 + 0x8*0;
    public const int EDGE_x1z0 = 0x8 + 0x8*1;
    public const int EDGE_y0z0 = 0x8 + 0x8*2;
    public const int EDGE_y1z0 = 0x8 + 0x8*3;
    public const int EDGE_x0z1 = 0x8 + 0x8*4;
    public const int EDGE_x1z1 = 0x8 + 0x8*5;
    public const int EDGE_y0z1 = 0x8 + 0x8*6;
    public const int EDGE_y1z1 = 0x8 + 0x8*7;
    public const int EDGE_x0y0 = 0x8 + 0x8*8;
    public const int EDGE_x1y0 = 0x8 + 0x8*9;
    public const int EDGE_x0y1 = 0x8 + 0x8*10;
    public const int EDGE_x1y1 = 0x8 + 0x8*11;
}


public abstract class SDFunbounded : IImplicit {
    public float signed_dist(in Vec3 p) => fSignedDistance(p);

    public Voxels voxels(BBox3 bounds, bool enforce_faces=true) {
        if (!enforce_faces)
            return new Voxels(this, bounds);
        Voxels vox = new Cuboid(bounds).voxels();
        vox.IntersectImplicit(this);
        return vox;
    }


    public abstract float fSignedDistance(in Vec3 p);
}

public abstract class SDF : SDFunbounded, IBoundedImplicit {
    public BBox3 bounds => bounds_fr;

    public Voxels voxels() {
        assert(!inf_bounds, "shape is infinite and has no bounds");
        assert(has_bounds, "no bounds have been set");
        // assert(bounds.vecMin != bounds.vecMax, "bounds cannot be empty");
        // eh maybe bounds can be empty.
        return new Voxels(this);
    }


    protected void set_bounds(in BBox3 bbox) {
        assert(!inf_bounds);
        bounds_fr = bbox;
        has_bounds = true;
    }
    protected void set_bounds(in Vec3 min, in Vec3 max) {
        assert(!inf_bounds);
        assert(min.X <= max.X);
        assert(min.Y <= max.Y);
        assert(min.Z <= max.Z);
        bounds_fr = new BBox3(min, max);
        has_bounds = true;
    }
    protected void include_in_bounds(in Vec3 p) {
        assert(!inf_bounds);
        if (!has_bounds) {
            bounds_fr = new BBox3(p, p);
        } else {
            bounds_fr.Include(p);
        }
        has_bounds = true;
    }
    protected void unbounded_bc_inf() {
        inf_bounds = true;
    }

    protected BBox3 bounds_fr;
    protected bool has_bounds = false;
    protected bool inf_bounds = false;
    public BBox3 oBounds => bounds_fr;
}


public delegate float SDFfunction(in Vec3 p);

public abstract class SDFfunc : SDFunbounded {
    public abstract SDFfunction sdf { get; }
    public abstract float max_off { get; }
}

public class SDFfilled : SDFfunc {
    private SDFfunction _sdf;
    public float off { get; }

    public override SDFfunction sdf => _sdf;
    public override float max_off => off;

    public SDFfilled(in SDFfunction sdf, float off=0f) {
        this._sdf = sdf;
        this.off = off;
    }
    public override float fSignedDistance(in Vec3 p) {
        return sdf(p) - off;
    }
}
public class SDFshelled : SDFfunc {
    private SDFfunction _sdf;
    public float off { get; }
    public float semith { get; }
    public float Ioff => off - semith;
    public float Ooff => off + semith;

    public override SDFfunction sdf => _sdf;
    public override float max_off => Ooff;

    public SDFshelled(in SDFfunction sdf, float th)
        : this(sdf, 0f, th) {}
    public SDFshelled(in SDFfunction sdf, float off, float th) {
        this._sdf = sdf;
        this.semith = 0.5f*th;
        this.off = off;
    }
    public SDFshelled innered() {
        return new SDFshelled(sdf, off + semith, 2f*semith);
    }
    public SDFshelled outered() {
        return new SDFshelled(sdf, off - semith, 2f*semith);
    }

    public override float fSignedDistance(in Vec3 p) {
        return abs(sdf(p) - off) - semith;
    }
}


public class Space : SDFunbounded {
    public Frame centre { get; }
    public float zlo { get; }
    public float zhi { get; }

    public Space(Frame centre, float zlo, float zhi) {
        assert(zhi > zlo, $"zlo={zlo}, zhi={zhi}");
        assert(zlo != +INF, $"zlo={zlo}");
        assert(zhi != -INF, $"zhi={zhi}");
        assert(zlo != -INF || zhi != +INF);
        this.centre = centre;
        this.zlo = zlo;
        this.zhi = zhi;
    }

    public override float fSignedDistance(in Vec3 p) {
        float z = dot(p - centre.pos, centre.Z);
        return max(zlo - z, z - zhi);
    }
}

public class Sectioner {
    protected List<object> ops = new();
    protected bool has_union = false;

    public bool has_cuts() => numel(ops) > 0;

    public void intersect(Space space) {
        ops.Add(space);
    }
    public void intersect(Sectioner sect) {
        ops.AddRange(sect.ops);
    }
    public void union(Space space) {
        Sectioner sect = new();
        sect.intersect(space);
        ops.Add(sect);
        has_union = true;
    }
    public void union(Sectioner sect) {
        ops.Add(sect);
        has_union = true;
    }

    public Voxels cut(in Voxels vox, bool inplace=false) {
        Voxels cutted = inplace ? vox : vox.voxDuplicate();
        Voxels original = (has_union && inplace) ? vox.voxDuplicate() : vox;
        foreach (object op in ops) {
            if (op is Space space) {
                cutted.IntersectImplicit(space);
            } else if (op is Sectioner sect) {
                Voxels add = sect.cut(original, inplace: false);
                cutted.BoolAdd(add);
            } else {
                assert(false);
            }
        }
        return cutted;
    }


    public static Sectioner pie(float min_theta, float max_theta) {
        assert(min_theta < max_theta);
        float Dtheta = max_theta - min_theta;
        Frame frame0 = new(ZERO3, fromcyl(1f, min_theta + PI_2, 0f));
        Frame frame1 = new(ZERO3, fromcyl(1f, max_theta - PI_2, 0f));
        Space space0 = new(frame0, 0f, +INF);
        Space space1 = new(frame1, 0f, +INF);
        Sectioner sectioner = new();
        sectioner.intersect(space0);
        if (Dtheta > PI)
            sectioner.union(space1);
        else
            sectioner.intersect(space1);
        return sectioner;
    }
}


public class Ball : SDF {
    public Vec3 centre { get; }
    public float r { get; }

    public Ball(float r)
        : this(ZERO3, r) {}
    public Ball(in Vec3 centre, float r) {
        assert(r > 0f, $"r={r}");
        this.centre = centre;
        this.r = r;
        if (isinf(r)) {
            unbounded_bc_inf();
        } else {
            include_in_bounds(centre - ONE3*r);
            include_in_bounds(centre + ONE3*r);
        }

    }


    public override float fSignedDistance(in Vec3 p) {
        return mag(p - centre) - r;
    }
}


public class Pill : SDF {
    public Vec3 a { get; }
    public Vec3 b { get; }
    public float r { get; }
    protected float magab { get; }
    protected Vec3 AB { get; }

    public Pill(Frame centre, float L, float r)
        : this(centre.pos, centre * (L*uZ3), r) {}
    public Pill(Vec3 a, Vec3 b, float r) {
        assert(r > 0f, $"r={r}");
        assert(!closeto(a, b), $"a={a}, b={b}");
        this.a = a;
        this.b = b;
        this.r = r;
        this.magab = mag(b - a);
        this.AB = (b - a) / magab;
        if (isinf(a) || isinf(b) || isinf(r)) {
            unbounded_bc_inf();
        } else {
            include_in_bounds(a - r*ONE3);
            include_in_bounds(a + r*ONE3);
            include_in_bounds(b - r*ONE3);
            include_in_bounds(b + r*ONE3);
        }
    }


    public override float fSignedDistance(in Vec3 p) {
        float axial = dot(AB, p - a);
        Vec3 c = a + AB*clamp(axial, 0f, magab);
        return mag(p - c) - r;
    }
}


public class Cuboid {
    public Frame centre { get; } // centre of low z end.
    public float Lx { get; }
    public float Ly { get; }
    public float Lz { get; }

    public Cuboid(in BBox3 box)
        : this(box.vecMin, box.vecMax) {}
    public Cuboid(in Vec3 a, in Vec3 b) {
        Vec3 lo = min(a, b);
        Vec3 hi = max(a, b);
        assert(lo.X < hi.X, $"lo={lo.X}, hi={hi.X}");
        assert(lo.Y < hi.Y, $"lo={lo.Y}, hi={hi.Y}");
        assert(lo.Z < hi.Z, $"lo={lo.Z}, hi={hi.Z}");
        this.centre = new(new Vec3(ave(lo.X, hi.X), ave(lo.Y, hi.Y), lo.Z));
        this.Lx = hi.X - lo.X;
        this.Ly = hi.Y - lo.Y;
        this.Lz = hi.Z - lo.Z;
        assert(noninf(Lx));
        assert(noninf(Ly));
        assert(noninf(Lz));
    }
    public Cuboid(in Frame centre, float L)
        : this(centre, L, L, L) {}
    public Cuboid(in Frame centre, float Lz, float Lxy)
        : this(centre, Lxy, Lxy, Lz) {}
    public Cuboid(in Frame centre, float Lx, float Ly, float Lz) {
        assert(Lx > 0f, $"Lx={Lx}");
        assert(Ly > 0f, $"Ly={Ly}");
        assert(Lz > 0f, $"Lz={Lz}");
        this.centre = centre;
        this.Lx = Lx;
        this.Ly = Ly;
        this.Lz = Lz;
        assert(noninf(Lx));
        assert(noninf(Ly));
        assert(noninf(Lz));
    }

    public Cuboid at_centre() {
        return new(centre.transz(-Lz/2f), Lx, Ly, Lz);
    }

    public Cuboid at_corner(int corner) {
        Vec3 trans = new(-Lx/2f, -Ly/2f, 0f);
        if (isset(corner, 0x1))
            trans.X += Lx;
        if (isset(corner, 0x2))
            trans.Y += Ly;
        if (isset(corner, 0x4))
            trans.Z += Lz;
        return new(centre.translate(trans), Lx, Ly, Lz);
    }

    public Cuboid at_edge(int edge) {
        Vec3 trans = new();
        switch (edge) {
            case EDGE_x0z0: trans = new(-Lx/2f, 0f, 0f); break;
            case EDGE_x1z0: trans = new(+Lx/2f, 0f, 0f); break;
            case EDGE_y0z0: trans = new(0f, -Ly/2f, 0f); break;
            case EDGE_y1z0: trans = new(0f, +Ly/2f, 0f); break;

            case EDGE_x0z1: trans = new(-Lx/2f, 0f, +Lz); break;
            case EDGE_x1z1: trans = new(+Lx/2f, 0f, +Lz); break;
            case EDGE_y0z1: trans = new(0f, -Ly/2f, +Lz); break;
            case EDGE_y1z1: trans = new(0f, +Ly/2f, +Lz); break;

            case EDGE_x0y0: trans = new(-Lx/2f, -Ly/2f, +Lz/2f); break;
            case EDGE_x1y0: trans = new(+Lx/2f, -Ly/2f, +Lz/2f); break;
            case EDGE_x0y1: trans = new(-Lx/2f, +Ly/2f, +Lz/2f); break;
            case EDGE_x1y1: trans = new(+Lx/2f, +Ly/2f, +Lz/2f); break;

            default: assert(false); break;
        }
        return new(centre.translate(trans), Lx, Ly, Lz);
    }

    public Cuboid extended(float Lz, int direction) {
        assert(direction == EXTEND_UP || direction == EXTEND_DOWN
            || direction == EXTEND_UPDOWN);
        float Dz = 0f;
        switch (direction) {
            case EXTEND_DOWN: Dz = -Lz; break;
            case EXTEND_UPDOWN: Dz = -Lz/2f; break;
        }
        return new(centre.transz(Dz), Lx, Ly, this.Lz + Lz);
    }

    public Cuboid as_sized(float Lx, float Ly, float Lz) {
        return new(centre, Lx, Ly, Lz);
    }
    public Cuboid as_scaled(float Px, float Py, float Pz) {
        return new(centre, Px*Lx, Py*Ly, Pz*Lz);
    }


    public List<Vec3> get_corners() {
        return [
            centre * new Vec3(-Lx/2f, -Ly/2f, 0f),
            centre * new Vec3(-Lx/2f, -Ly/2f, Lz),
            centre * new Vec3(-Lx/2f, +Ly/2f, 0f),
            centre * new Vec3(-Lx/2f, +Ly/2f, Lz),
            centre * new Vec3(+Lx/2f, -Ly/2f, 0f),
            centre * new Vec3(+Lx/2f, -Ly/2f, Lz),
            centre * new Vec3(+Lx/2f, +Ly/2f, 0f),
            centre * new Vec3(+Lx/2f, +Ly/2f, Lz),
        ];
    }

    public Voxels voxels() {
        return new(mesh());
    }

    public Mesh mesh() {
        Mesh mesh = new();
        mesh.AddVertices(get_corners(), out _);
        // +X quad.
        mesh.nAddTriangle(4, 5, 7);
        mesh.nAddTriangle(4, 7, 6);
        // -X quad.
        mesh.nAddTriangle(0, 2, 3);
        mesh.nAddTriangle(0, 3, 1);
        // +Y quad.
        mesh.nAddTriangle(2, 6, 7);
        mesh.nAddTriangle(2, 7, 3);
        // -Y quad.
        mesh.nAddTriangle(0, 1, 5);
        mesh.nAddTriangle(0, 5, 4);
        // +Z quad.
        mesh.nAddTriangle(1, 3, 7);
        mesh.nAddTriangle(1, 7, 5);
        // -Z quad.
        mesh.nAddTriangle(0, 4, 6);
        mesh.nAddTriangle(0, 6, 2);
        return mesh;
    }
}


public class Pipe : SDF {
    public Frame centre { get; } // centre of low z end.
    public float Lz { get; }
    public float rlo { get; }
    public float rhi { get; }

    public Pipe(in Frame centre, float Lz, float r)
        : this(centre, Lz, 0f, r) {}
    public Pipe(in Frame centre, float Lz, float rlo, float rhi) {
        assert(Lz > 0f, $"Lz={Lz}");
        assert(rhi > rlo, $"rlo={rlo}, rhi={rhi}");
        assert(rlo >= 0f, $"rlo={rlo}, rhi={rhi}");
        this.centre = centre;
        this.Lz = Lz;
        this.rlo = rlo;
        this.rhi = rhi;
        if (isinf(Lz) || isinf(rlo) || isinf(rhi)) {
            unbounded_bc_inf();
        } else {
            BBox3 bbox = new(
                new Vec3(-rhi, -rhi, 0f),
                new Vec3(+rhi, +rhi, Lz)
            );
            set_bounds(centre.to_global_bbox(bbox));
        }
    }

    public Pipe filled() {
        if (rlo == 0f)
            return this;
        return new Pipe(centre, Lz, rhi);
    }
    public Pipe hole() {
        return new Pipe(centre, Lz, rlo);
    }

    public Pipe hollowed(float Ir) {
        return new Pipe(centre, Lz, Ir, rhi);
    }

    public Pipe extended(float Lz, int direction) {
        assert(direction == EXTEND_UP || direction == EXTEND_DOWN
            || direction == EXTEND_UPDOWN);
        float Dz = 0f;
        switch (direction) {
            case EXTEND_DOWN: Dz = -Lz; break;
            case EXTEND_UPDOWN: Dz = -Lz/2f; break;
        }
        return new(centre.transz(Dz), this.Lz + Lz, rlo, rhi);
    }


    public override float fSignedDistance(in Vec3 p) {
        Vec3 q = centre.from_global(p);
        float r = magxy(q);
        float z = q.Z;

        float dist_axial = max(-z, z - Lz);
        float dist_radial = r - rhi;
        if (rlo > 1e-3f) // theres gotta be a better way, but i cannot see.
            dist_radial = max(dist_radial, rlo - r);

        float dist;
        if (dist_radial <= 0f || dist_axial <= 0f) {
            dist = max(dist_radial, dist_axial);
        } else {
            dist = hypot(dist_radial, dist_axial);
        }
        return dist;
    }
}


public class Donut : SDF {
    public Frame centre { get; } // centre point, z along axis of symmetry.
    public float R { get; } // distance from axis of revoultion.
    public float r { get; } // half-thickness.

    public Donut(in Frame centre, float R, float r) {
        assert(R >= 0f);
        assert(r > 0f);
        this.centre = centre;
        this.R = R;
        this.r = r;
        if (isinf(R) || isinf(r)) {
            unbounded_bc_inf();
        } else {
            BBox3 bbox = new(
                new Vec3(-R - r, -R - r, -r),
                new Vec3(+R + r, +R + r, +r)
            );
            set_bounds(centre.to_global_bbox(bbox));
        }
    }

    public override float fSignedDistance(in Vec3 p) {
        Vec3 q = centre.from_global(p);
        float dist = hypot(magxy(q) - R, q.Z);
        return abs(dist) - r;
    }
}


public class Cone : SDF {
    public Frame centre { get; } // cone tip, +z towards cone.
    public float Lz { get; }
    public float phi { get; }
    public float? th { get; }
    protected float cosphi { get; }
    protected float sinphi { get; }

    public struct AsPhi { public required float v { get; init; } }
    public static AsPhi as_phi(float phi) => new AsPhi{v=phi};

    public struct AsRadius { public required float v { get; init; } }
    public static AsRadius as_radius(float r) => new AsRadius{v=r};

    public Cone(in Frame centre, float Lz, float r, float? th=null,
            bool at_tip=false)
        : this(centre, Lz, as_phi(atan(r / Lz)), th, at_tip) {}
    public Cone(in Frame centre, AsRadius r, AsPhi phi, float? th=null,
            bool at_tip=false)
        : this(centre, r.v / tan(phi.v), phi, th, at_tip) {}
    public Cone(in Frame centre, float Lz, AsPhi phi, float? th=null,
            bool at_tip=true) {
        assert(Lz > 0f, $"Lz={Lz}");
        assert(0f < phi.v && phi.v < PI_2, $"phi={phi.v}");
        this.centre = centre;
        if (!at_tip)
            this.centre = this.centre.transz(Lz).flipyz();
        this.Lz = Lz;
        this.phi = phi.v;
        this.th = th;
        this.cosphi = cos(this.phi);
        this.sinphi = sin(this.phi);
        float r = Lz * sinphi/cosphi;
        if (isinf(Lz) || isinf(th ?? 0f)) {
            unbounded_bc_inf();
        } else {
            include_in_bounds(centre * new Vec3(0f, 0f, 0f));
            include_in_bounds(centre * new Vec3(-r, -r, Lz));
            include_in_bounds(centre * new Vec3(-r, +r, Lz));
            include_in_bounds(centre * new Vec3(+r, -r, Lz));
            include_in_bounds(centre * new Vec3(+r, +r, Lz));
        }
    }


    public override float fSignedDistance(in Vec3 p) {
        Vec3 q = centre.from_global(p);
        float r = magxy(q);
        float z = q.Z;

        // Cone sdf.
        float dist = cosphi * r - sinphi * z;

        // Distance along cone face.
        Vec2 face = new(sinphi, cosphi);
        Vec2 point = new(r, z);
        float along = dot(face, point);
        float length = Lz / cosphi;
        if (along > length) {
            along -= length;
            dist = hypot(dist, along);
        }

        // Make shell if thickness was given.
        if (th != null)
            dist = abs(dist + 0.5f*th.Value) - 0.5f*th.Value;

        // Make flat base.
        dist = max(dist, z - Lz);

        return dist;
    }
}


public class Tubing {
    public List<Vec3> points { get; }
    public float ID { get; }
    public float th { get; }
    public float Fr { get; }

    public Tubing(in List<Vec3> points, float OD)
        : this(points, 0f, 0.5f*OD) {}
    public Tubing(in List<Vec3> points, float ID, float th, float Fr=0f) {
        assert(numel(points) >= 2, $"numel={numel(points)}");
        assert(ID >= 0f);
        assert(th > 0f);
        assert(Fr >= 0f);
        this.points = points;
        this.ID = ID;
        this.th = th;
        this.Fr = Fr;
    }

    public Voxels voxels() {
        Vec3 S0 = points[0];
        Vec3 S1 = points[1];
        Vec3 E0 = points[^1];
        Vec3 E1 = points[^2];
        float remove_r = 1.5f*(0.5f*ID + th);
        Pipe S = new(new Frame(S0, S0 - S1), remove_r, remove_r);
        Pipe E = new(new Frame(E0, E0 - E1), remove_r, remove_r);

        Voxels vox = new();
        Voxels inner = new();
        bool hollow = (ID > 1e-3f);
        bool filleted = (Fr > 1e-3f);

        if (hollow) {
            for (int i=1; i<numel(points); ++i) {
                Vec3 a = points[i - 1];
                Vec3 b = points[i];
                inner.BoolAdd(new Pill(a, b, 0.5f*ID).voxels());
            }
        }
        if (!hollow || !filleted) {
            for (int i=1; i<numel(points); ++i) {
                Vec3 a = points[i - 1];
                Vec3 b = points[i];
                vox.BoolAdd(new Pill(a, b, 0.5f*ID + th).voxels());
            }
        }
        if (filleted) {
            if (hollow)
                vox = inner.voxDoubleOffset(th + Fr, -Fr);
            else
                vox = vox.voxDoubleOffset(Fr, -Fr);
        }
        if (hollow)
            vox.BoolSubtract(inner);
        vox.BoolSubtract(S.voxels());
        vox.BoolSubtract(E.voxels());
        return vox;
    }
}

}
