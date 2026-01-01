using static br.Br;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using IImplicit = PicoGK.IImplicit;
using IBoundedImplicit = PicoGK.IBoundedImplicit;
using BBox3 = PicoGK.BBox3;

using Colour = PicoGK.ColorFloat;
using Image = PicoGK.Image;
using ImageColor = PicoGK.ImageColor;
using TgaIo = PicoGK.TgaIo;

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

    public Cuboid(in Frame centre, float L)
        : this(centre, L, L, L) {}
    public Cuboid(in Frame centre, float Lz, float Lxy)
        : this(centre, Lxy, Lxy, Lz) {}
    public Cuboid(in Frame centre, Vec3 L)
        : this(centre, L.X, L.Y, L.Z) {}
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

    public Cuboid(in BBox3 box)
        : this(new Frame(), box.vecMin, box.vecMax) {}
    public Cuboid(in Frame frame, in BBox3 box)
        : this(frame, box.vecMin, box.vecMax) {}
    public Cuboid(in Vec3 a, in Vec3 b)
        : this(new Frame(), a, b) {}
    public Cuboid(in Frame frame, in Vec3 a, in Vec3 b)
        : this(
            frame.translate(new(ave(a.X, b.X), ave(a.Y, b.Y), min(a.Z, b.Z))),
            max(a, b) - min(a, b)
        ) {}


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

    public Pipe at_centre() {
        return new(centre.transz(-Lz/2f), Lz, rlo, rhi);
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
        Vec3 q = centre / p;
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
        Vec3 q = centre / p;
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
        Vec3 q = centre / p;
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



public class SDFimage {

    private static float[,] euclidean_distance_transform(bool[,] boundary) {
        const float inf = 1e10f;

        void edt_1d(float[] f, float[] d, int[] v, float[] z, int N) {
            int k = 0; // index of rightmost parabola in envelope

            v[0] = 0;
            z[0] = -inf;
            z[1] = +inf;

            for (int q=1; q<N; ++q) {
                float s;
                // Compute intersection s between parabola q and parabola v[k].
                while (true) {
                    int p = v[k];
                    s = (f[q] - f[p] + q*q - p*p) / 2f / (q - p);
                    if (s <= z[k]) {
                        // The last parabola v[k] is never part of the lower
                        // envelope.
                        --k;
                        if (k < 0) // safety
                            break;
                        continue;
                    }
                    break;
                }

                ++k;
                v[k] = q;
                z[k] = s;
                z[k + 1] = +inf;
            }

            // Sample lower envelope.
            k = 0;
            for (int q=0; q<N; ++q) {
                while (z[k + 1] < q)
                    ++k;
                int p = v[k];
                float diff = q - p;
                d[q] = diff*diff + f[p];
            }
        }

        int W = boundary.GetLength(0);
        int H = boundary.GetLength(1);

        // Setup distances with zero on boundary and max everywhere else.
        float[,] dist = new float[W, H];
        for (int x=0; x<W; ++x) {
            for (int y=0; y<H; ++y)
                dist[x, y] = boundary[x, y] ? 0f : inf;
        }

        float[] f = new float[max(W, H)]; // Buffer to give distances.
        float[] d = new float[max(W, H)]; // Buffer to receive distances.

        // Locations of parabolas in lower envelope.
        int[] v = new int[max(W, H)];
        // Breakpoints between parabolas.
        float[] z = new float[max(W, H) + 1];

        // Vertical pass.
        for (int x=0; x<W; ++x) {
            for (int y=0; y<H; ++y)
                f[y] = dist[x, y];
            edt_1d(f, d, v, z, H);
            for (int y=0; y<H; ++y)
                dist[x, y] = d[y];
        }
        // Horizontal pass.
        for (int y=0; y<H; ++y) {
            for (int x=0; x<W; ++x)
                f[x] = dist[x, y];
            edt_1d(f, d, v, z, W);
            for (int x=0; x<W; ++x)
                dist[x, y] = d[x];
        }
        // Convert from sqr dist to genuine.
        for (int x=0; x<W; ++x) {
            for (int y=0; y<H; ++y)
                dist[x, y] = sqrt(dist[x, y]);
        }
        return dist;
    }

    private static float[,] make_sda(bool[,] mask) {
        int W = mask.GetLength(0);
        int H = mask.GetLength(1);

        bool[,] inside_boundary  = new bool[W, H];
        bool[,] outside_boundary = new bool[W, H];
        for (int x=0; x<W; ++x) {
            for (int y=0; y<H; ++y) {
                bool v = mask[x, y];

                for (int dx=-1; dx<=1; ++dx) {
                    for (int dy=-1; dy<=1; ++dy) {
                        if (dx == 0 && dy == 0)
                            continue;
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H)
                            continue;
                        if (mask[nx, ny] != v)
                            goto ON_BOUNDARY;
                    }
                }
                continue;

              ON_BOUNDARY:;
                inside_boundary[x, y]  = !v;
                outside_boundary[x, y] =  v;
            }
        }

        float[,] inside_dist  = euclidean_distance_transform(inside_boundary);
        float[,] outside_dist = euclidean_distance_transform(outside_boundary);

        float[,] sda = new float[W, H];
        for (int x=0; x<W; ++x) {
            for (int y=0; y<H; ++y) {
                sda[x, H - 1 - y] = mask[x, y]
                                  ? -inside_dist[x, y]
                                  : +outside_dist[x, y];

            }
        }
        return sda;
    }

    private static bool[,] make_mask(Image img, int scale, int xextra,
            int yextra, float threshold, bool invert, bool flipx, bool flipy) {
        int W = img.nWidth;
        int H = img.nHeight;

        bool[,] mask = new bool[scale*(W + 2*xextra), scale*(H + 2*yextra)];
        for (int x=0; x<W; ++x) {
            for (int y=0; y<H; ++y) {
                Colour col = img.clrValue(flipx ? W - 1 - x : x,
                                          flipy ? H - 1 - y : y);
                float grey = 0.2126f*col.R + 0.7152f*col.G + 0.0722f*col.B;
                grey *= col.A;
                bool on = invert == (grey < threshold);

                for (int dx=0; dx<scale; ++dx) {
                    for (int dy=0; dy<scale; ++dy) {
                        int i = scale * (x + xextra) + dx;
                        int j = scale * (y + yextra) + dy;
                        mask[i, j] = on;
                    }
                }
            }
        }
        return mask;
    }



    private float[,] sda;
    private int scale { get; }
    private int xextra { get; }
    private int yextra { get; }
    public int width { get; }
    public int height { get; }
    public float aspect_x_on_y => width / (float)height;

    // loads tga image from the given path, expecting (0,0) to be bottom-left.
    public SDFimage(in string path, int scale=1, float threshold=0.5f,
            bool invert=false, bool flipx=false, bool flipy=false) {
        assert(scale >= 1);
        assert(xextra >= 0);
        assert(yextra >= 0);
        TgaIo.LoadTga(path, out Image img);
        this.width  = img.nWidth;
        this.height = img.nHeight;
        this.scale = scale;
        // choose extra s.t. an image spanning a quarter of a circle may be
        // sampled anywhere on the circle (aka up to 1.5x away).
        this.xextra = scale * 16 * width / 10;
        this.yextra = scale * 16 * height / 10;
        bool[,] mask = make_mask(img, scale, xextra, yextra, threshold, invert,
                flipx, flipy);
        this.sda = make_sda(mask);
    }


    // Returns the signed distance in units of pixels, with an input vector also
    // in units of pixels. The valid bounds are ~(width, -height) to
    // ~(2 width, 2 height).
    public float signed_dist(in Vec2 p) {
        assert(within(p.X, -xextra, width  + xextra));
        assert(within(p.Y, -yextra, height + yextra));

        float x = (p.X + xextra) * scale - 0.5f;
        float y = (p.Y + yextra) * scale - 0.5f;

        int x0 = clamp(ifloor(x), 0, sda.GetLength(0) - 2);
        int y0 = clamp(ifloor(y), 0, sda.GetLength(1) - 2);
        int x1 = x0 + 1;
        int y1 = y0 + 1;

        float d00 = sda[x0, y0];
        float d01 = sda[x0, y1];
        float d10 = sda[x1, y0];
        float d11 = sda[x1, y1];

        float tx = x - x0;
        float ty = y - y0;
        float d0 = lerp(d00, d01, ty);
        float d1 = lerp(d10, d11, ty);
        float d = lerp(d0, d1, tx);
        return d / scale;
    }


    public SDFfunction sdf_on_plane(out BBox3 bbox, Frame centre, float Lz,
            float length /* for longer image dimension */,
            bool i_understand_sampling_oob_is_illegal_and_i_wont_do_it=false) {
        assert(Lz > 0f);
        assert(length > 0f);
        if (!i_understand_sampling_oob_is_illegal_and_i_wont_do_it) {
            centre.rot_to_local(out Vec3 rot_about, out float rot_by);
            assert(nearzero(rot_by),
                    "the raw sdf will not work with anything that rotates at "
                  + "all. you will need to use a transformer");
        }

        float scale = length / max(width, height);
        Vec2 size = (aspect_x_on_y >= 1f)
                  ? new(length, length / aspect_x_on_y)
                  : new(length * aspect_x_on_y, length);

        bbox = new(rejxy(-size/2f, 0f), rejxy(size/2f, Lz));
        bbox = centre.to_global_bbox(bbox);

        float sdf(in Vec3 p) {
            Vec3 q = centre / p;

            float z = q.Z;
            float dist_z = max(-z, z - Lz);

            Vec2 pixel = projxy(q) + size/2f;
            float dist_xy = signed_dist(pixel / scale) * scale;

            float dist;
            if (dist_xy <= 0f || dist_z <= 0f) {
                dist = max(dist_xy, dist_z);
            } else {
                dist = hypot(dist_xy, dist_z);
            }
            return dist;
        }

        return sdf;
    }

    public SDFfunction sdf_on_cyl(bool vertical, out BBox3 bbox, Frame centre,
            float R, float Lr, float length /* for longer image dimension */,
            bool i_understand_sampling_oob_is_illegal_and_i_wont_do_it=false) {
        assert(R > 0f);
        assert(Lr > 0f);
        assert(length > 0f);

        Frame orient = vertical ? new Frame()
                                : new Frame(ZERO3, uX3, uY3);

        if (!i_understand_sampling_oob_is_illegal_and_i_wont_do_it) {
            centre.rot_to_local(out Vec3 rot_about, out float rot_by);
            assert(nearzero(rot_by) || nearvert(orient / rot_about),
                    "the raw sdf will not work with anything that rotates away "
                  + "from axial. you will need to use a transformer");
        }

        float scale = length / max(width, height);
        Vec2 size = (aspect_x_on_y >= 1f)
                  ? new(length, length / aspect_x_on_y)
                  : new(length * aspect_x_on_y, length);

        float Lz = vertical ? size.Y : size.X; // cylinder z.
        float Ltheta = (vertical ? size.X : size.Y) / R;
        assert(Ltheta <= TWOPI, "cannot wrap around");

        bbox = new();
        bbox.Include(orient * new Vec3(R + Lr, 0f, -Lz/2f));
        bbox.Include(orient * new Vec3(R + Lr, 0f, +Lz/2f));
        bbox.Include(orient * fromcyl(R, -Ltheta/2f, 0f));
        bbox.Include(orient * fromcyl(R, +Ltheta/2f, 0f));
        bbox.Include(orient * fromcyl(R + Lr, -Ltheta/2f, 0f));
        bbox.Include(orient * fromcyl(R + Lr, +Ltheta/2f, 0f));
        if (Ltheta > PI) {
            bbox.Include(orient * new Vec3(0f, +R + Lr, 0f));
            bbox.Include(orient * new Vec3(0f, -R - Lr, 0f));
        }
        bbox = centre.to_global_bbox(bbox);

        float sdf(in Vec3 p) {
            Vec3 q = centre / p;
            q = orient / q;

            float r = magxy(q);
            float dist_r = max(R - r, r - (R + Lr));

            float z = q.Z + Lz/2f;
            float theta = (vertical ? argxy(q) : -argxy(q)) + Ltheta/2f;
            Vec2 pixel = vertical ? new(R*theta, z) : new(z, R*theta);
            float dist_surf = signed_dist(pixel / scale) * scale;
            dist_surf *= r / R;
            // technically, this is the distance along the surface of the
            // cylinder and not true euclidean in xyz. but like, theyre pretty
            // similar provided the image has no super larger gaps/solids.

            float dist;
            if (dist_surf <= 0f || dist_r <= 0f) {
                dist = max(dist_surf, dist_r);
            } else {
                dist = hypot(dist_surf, dist_r);
            }
            return dist;
        }

        return sdf;
    }


    public Voxels voxels_on_plane(in Frame centre, float Lz,
            float length /* for longer image dimension */) {
        centre.rot_to_local(out Vec3 rot_about, out float rot_by);
        if (!nearzero(rot_by)) {
            // darn its got rotation we need a transformer.
            SDFfunction sdf = sdf_on_plane(out BBox3 bbox, new(), Lz, length);
            Voxels vox = new SDFfilled(sdf).voxels(bbox);
            Transformer transformer = new Transformer().to_global(centre);
            return transformer.voxels(vox);
        } else {
            // okie nws just send to sdf.
            SDFfunction sdf = sdf_on_plane(out BBox3 bbox, centre, Lz, length);
            return new SDFfilled(sdf).voxels(bbox);
        }
    }

    public Voxels voxels_on_cyl(bool vertical, in Frame centre, float R,
            float Lr, float length /* for longer image dimension */) {
        Frame orient = vertical ? new Frame()
                                : new Frame(ZERO3, uX3, uY3);
        centre.rot_to_local(out Vec3 rot_about, out float rot_by);

        // same deal except can take inplane rotations.
        if (!nearzero(rot_by) && !nearvert(orient / rot_about)) {
            SDFfunction sdf = sdf_on_cyl(vertical, out BBox3 bbox, new Frame(),
                    R, Lr, length);
            Voxels vox = new SDFfilled(sdf).voxels(bbox);
            Transformer transformer = new Transformer().to_global(centre);
            return transformer.voxels(vox);
        } else {
            SDFfunction sdf = sdf_on_cyl(vertical, out BBox3 bbox, centre, R, Lr,
                    length);
            return new SDFfilled(sdf).voxels(bbox);
        }
    }



    // save the sdf to an image, just so we can see whats up.
    public Image cop_a_look(int resolution=1, float sharpness=1f) {
        Colour col_inside  = COLOUR_PINK;
        Colour col_outside = COLOUR_GREEN;

        int W = resolution * scale * width;
        int H = resolution * scale * height;
        ImageColor img = new(W, H);
        for (int x=0; x<W; ++x) {
            for (int y=0; y<H; ++y) {
                Vec2 p = new(x, y);
                p += 0.5f*ONE2;
                p /= resolution;
                p /= scale;
                float value = signed_dist(p);
                value /= min(width, height) / 4f;
                value *= sharpness;

                PicoGK.ColorHSV col = (value < 0)
                                    ? col_inside
                                    : col_outside;
                col.V *= 1f - exp(-2f * abs(value));

                img.SetValue(x, H - 1 - y, col);
            }
        }
        return img;
    }

    public void stash_a_look(in string path, int resolution=1,
            float sharpness=1f) {
        Image img = cop_a_look(resolution: resolution, sharpness: sharpness);
        TgaIo.SaveTga(path, img);
    }
}

}
