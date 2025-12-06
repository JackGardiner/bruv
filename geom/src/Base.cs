using static Br;
using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Lattice = PicoGK.Lattice;
using IImplicit = PicoGK.IImplicit;
using IBoundedImplicit = PicoGK.IBoundedImplicit;
using BBox3 = PicoGK.BBox3;


public abstract class SDFunbounded : IImplicit {
    public abstract float signed_dist(in Vec3 p);

    public Voxels voxels(BBox3 bounds) {
        return new Voxels(this, bounds);
    }


    public float fSignedDistance(in Vec3 p) => signed_dist(p);
}

public abstract class SDF : IBoundedImplicit {
    public abstract float signed_dist(in Vec3 p);

    public Voxels voxels() {
        assert(has_bounds, "no bounds have been set");
        // assert(bounds.vecMin != bounds.vecMax, "bounds cannot be empty");
        // eh maybe bounds can be empty.
        return new Voxels(this);
    }
    public Voxels voxels(params SDFunbounded[] intersect_with) {
        Voxels vox = new Voxels(this);
        foreach (IImplicit i in intersect_with)
            vox.IntersectImplicit(i);
        return vox;
    }

    public BBox3 bounds => bounds_fr;
    protected void set_bounds(Vec3 min, Vec3 max) {
        bounds_fr = new BBox3(min, max);
        has_bounds = true;
    }
    protected void include_in_bounds(Vec3 p) {
        if (!has_bounds) {
            bounds_fr = new BBox3(p, p);
        } else {
            bounds_fr.Include(p);
        }
        has_bounds = true;
    }


    public float fSignedDistance(in Vec3 p) => signed_dist(p);
    protected BBox3 bounds_fr;
    protected bool has_bounds = false;
    public BBox3 oBounds => bounds_fr;
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
        include_in_bounds(centre - ONE3*r);
        include_in_bounds(centre + ONE3*r);
    }


    public override float signed_dist(in Vec3 p) {
        return mag(p - centre) - r;
    }
}


public class Pill : SDF {
    public Vec3 a { get; }
    public Vec3 b { get; }
    public float r { get; }
    protected Vec3 ab { get; }
    protected Vec3 ab_magab2 { get; }

    public Pill(Frame centre, float L, float r)
        : this(centre.pos, centre.to_global(L*uZ3), r) {}
    public Pill(Vec3 a, Vec3 b, float r) {
        assert(r > 0f, $"r={r}");
        this.a = a;
        this.b = b;
        this.r = r;
        this.ab = b - a;
        float magab = mag(this.ab);
        this.ab_magab2 = ab / magab / magab;
        include_in_bounds(a - r*ONE3);
        include_in_bounds(a + r*ONE3);
        include_in_bounds(b - r*ONE3);
        include_in_bounds(b + r*ONE3);
    }


    public override float signed_dist(in Vec3 p) {
        float axial = dot(ab_magab2, p - a);
        Vec3 c = a + ab*clamp(axial, 0f, 1f);
        return mag(p - c) - r;
    }
}


public class Cuboid : SDF {
    public Frame centre { get; } // centre of low z end.
    public float Lx { get; }
    public float Ly { get; }
    public float Lz { get; }

    public Cuboid(in Frame centre, float L)
        : this(centre, L, L, L) {}
    public Cuboid(in Frame centre, float W, float Lz)
        : this(centre, W, W, Lz) {}
    public Cuboid(in Frame centre, float Lx, float Ly, float Lz) {
        assert(Lx > 0f, $"Lx={Lx}");
        assert(Ly > 0f, $"Ly={Ly}");
        assert(Lz > 0f, $"Lz={Lz}");
        this.centre = centre;
        this.Lx = Lx;
        this.Ly = Ly;
        this.Lz = Lz;
        include_in_bounds(centre.to_global(new(-Lx/2f, -Ly/2f, 0f)));
        include_in_bounds(centre.to_global(new(-Lx/2f, +Ly/2f, 0f)));
        include_in_bounds(centre.to_global(new(+Lx/2f, -Ly/2f, 0f)));
        include_in_bounds(centre.to_global(new(+Lx/2f, +Ly/2f, 0f)));
        include_in_bounds(centre.to_global(new(-Lx/2f, -Ly/2f, Lz)));
        include_in_bounds(centre.to_global(new(-Lx/2f, +Ly/2f, Lz)));
        include_in_bounds(centre.to_global(new(+Lx/2f, -Ly/2f, Lz)));
        include_in_bounds(centre.to_global(new(+Lx/2f, +Ly/2f, Lz)));
    }

    public Cuboid centred() {
        return new(centre.translate(uZ3*-Lz/2f), Lx, Ly, Lz);
    }


    public override float signed_dist(in Vec3 p) {
        Vec3 q = centre.from_global(p);
        float distx = max(-Lx/2f - q.X, q.X - Lx/2f);
        float disty = max(-Ly/2f - q.Y, q.Y - Ly/2f);
        float distz = max(-q.Z, q.Z - Lz);

        float dist;
        if (distx <= 0f || disty <= 0f || distz <= 0f) {
            dist = max(distx, disty, distz);
        } else {
            dist = hypot(distx, disty, distz);
        }
        return dist;
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
        assert(isgood(Lz));
        assert(Lz > 0f, $"Lz={Lz}");
        assert(rhi > rlo, $"rlo={rlo}, rhi={rhi}");
        assert(rlo >= 0f, $"rlo={rlo}, rhi={rhi}");
        this.centre = centre;
        this.Lz = Lz;
        this.rlo = rlo;
        this.rhi = rhi;
        include_in_bounds(centre.to_global(new(-rhi, -rhi, 0f)));
        include_in_bounds(centre.to_global(new(-rhi, +rhi, 0f)));
        include_in_bounds(centre.to_global(new(+rhi, -rhi, 0f)));
        include_in_bounds(centre.to_global(new(+rhi, +rhi, 0f)));
        include_in_bounds(centre.to_global(new(-rhi, -rhi, Lz)));
        include_in_bounds(centre.to_global(new(-rhi, +rhi, Lz)));
        include_in_bounds(centre.to_global(new(+rhi, -rhi, Lz)));
        include_in_bounds(centre.to_global(new(+rhi, +rhi, Lz)));
    }

    public Pipe filled() {
        if (rlo == 0f)
            return this;
        return new Pipe(centre, Lz, rhi);
    }
    public Pipe hole() {
        return new Pipe(centre, Lz, rlo);
    }


    public override float signed_dist(in Vec3 p) {
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


public class Tubing {
    public List<Vec3> points { get; }
    public float ID { get; }
    public float th { get; }
    public float Fr { get; }
    public BBox3 bounds { get; }

    public Tubing(in List<Vec3> points, float OD)
        : this(points, 0f, 0.5f*OD) {}
    public Tubing(in List<Vec3> points, float ID, float th, float Fr=0f) {
        assert(numel(points) >= 2, $"numel={numel(points)}");
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
        float remove_r = 1.01f*(0.5f*ID + th);
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
        vox.BoolSubtract(inner);
        vox.BoolSubtract(S.voxels());
        vox.BoolSubtract(E.voxels());
        return vox;
    }
}


public class Polygon : SDF {
    public Frame centre { get; } // centre of low z end.
    public List<Vec2> points { get; }
    public float L { get; }

    public Polygon(in Frame centre, in List<Vec2> points, float L) {
        assert(numel(points) >= 3, $"numel={numel(points)}");
        assert(_is_simple_polygon(points), "cooked it");
        assert(L > 0f, $"L={L}");
        this.centre = centre;
        this.points = points;
        this.L = L;
        for (int i=0; i<numel(points); ++i) {
            include_in_bounds(centre.to_global(rejxy(points[i], 0f)));
            include_in_bounds(centre.to_global(rejxy(points[i], L)));
        }
    }


    protected static bool _is_simple_polygon(in List<Vec2> points) {
        int N = numel(points);
        if (N < 3)
            return false;

        for (int i=0; i<N; ++i) {
            int i_1 = (i + 1 == N) ? 0 : i + 1;
            Vec2 a0 = points[i];
            Vec2 a1 = points[i_1];

            for (int j=i + 1; j<N; ++j) {
                int j_1 = (j + 1 == N) ? 0 : j + 1;
                Vec2 b0 = points[j];
                Vec2 b1 = points[j_1];

                // Skip adjacent edges.
                if (i == j || i_1 == j || j_1 == i)
                    continue;

                float o1 = cross(a1 - a0, b0 - a0);
                float o2 = cross(a1 - a0, b1 - a0);
                float o3 = cross(b1 - b0, a0 - b0);
                float o4 = cross(b1 - b0, a1 - b0);
                if ((o1*o2 < 0) && (o3*o4 < 0))
                    return false;
            }
        }
        return true;
    }

    public override float signed_dist(in Vec3 p) {
        Vec3 q = centre.from_global(p);
        Vec2 q2 = projxy(q);

        int N = points.Count;
        int winding = 0;

        float dist_proj = INF;
        for (int i=0; i<N; ++i) {
            Vec2 a = points[i];
            Vec2 b = points[(i + 1 == N) ? 0 : i + 1];

            Vec2 ab = b - a;
            Vec2 aq2 = q2 - a;

            if (a.Y <= q2.Y) {
                if (b.Y > q2.Y && cross(ab, aq2) > 0)
                    winding += 1;
            } else {
                if (b.Y <= q2.Y && cross(ab, aq2) < 0)
                    winding -= 1;
            }

            float t = dot(aq2, ab) / dot(ab, ab);
            t = clamp(t, 0f, 1f); // must lie on segment.
            Vec2 closest = a + t*ab;

            float d = mag(q2 - closest);
            dist_proj = min(dist_proj, d);
        }

        if (winding != 0)
            dist_proj = -dist_proj;

        float dist_axial = max(-q.Z, q.Z - L);

        float dist;
        if (dist_proj <= 0f || dist_axial <= 0f) {
            dist = max(dist_proj, dist_axial);
        } else {
            dist = hypot(dist_proj, dist_axial);
        }
        return dist;
    }
}
