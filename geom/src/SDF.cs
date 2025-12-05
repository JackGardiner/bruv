using static Br;
using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
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
        assert(r >= 0f, $"r={r}");
        this.centre = centre;
        this.r = r;
        include_in_bounds(centre - ONE3*r);
        include_in_bounds(centre + ONE3*r);
    }

    public override float signed_dist(in Vec3 p) {
        return mag(p - centre) - r;
    }
}


public class Pipe : SDF {
    public int axis { get; }
    public Vec3 centre { get; } // centre of one end.
    public float L { get; }
    public float rlo { get; }
    public float rhi { get; }
    protected float axial_lo;
    protected float axial_hi;
    protected Vec2 centre_proj;

    public Pipe(in Vec3 centre, float L, float rhi, int axis=2)
        : this(centre, L, 0f, rhi, axis) {}
    public Pipe(in Vec3 centre, float L, float rlo, float rhi, int axis=2) {
        assert(isgood(centre));
        assert(isgood(L));
        assert(rhi >= rlo, $"rlo={rlo}, rhi={rhi}");
        assert(rlo >= 0f, $"rlo={rlo}, rhi={rhi}");
        assert(isaxis(axis), $"axis={axis}");
        this.axis = axis;
        this.centre = centre;
        this.L = L;
        this.rlo = rlo;
        this.rhi = rhi;
        Vec3 pmin = centre - rhi*ONE3;
        Vec3 pmax = centre + rhi*ONE3;
        pmin[axis] = centre[axis] + min(0f, L);
        pmax[axis] = centre[axis] + max(0f, L);

        axial_lo = pmin[axis];
        axial_hi = pmax[axis];
        centre_proj = projection(centre, axis);
        set_bounds(pmin, pmax);
    }

    public override float signed_dist(in Vec3 p) {
        float r = mag(projection(p, axis) - centre_proj);

        float dist_radial = r - rhi;
        if (rlo > 1e-3)
            dist_radial = max(dist_radial, rlo - r);

        float dist_axial = max(axial_lo - p[axis], p[axis] - axial_hi);

        float dist;
        if (dist_radial <= 0f || dist_axial <= 0f) {
            dist = max(dist_radial, dist_axial);
        } else {
            dist = hypot(dist_radial, dist_axial);
        }
        return dist;
    }

    public Pipe filled() {
        if (rlo == 0f)
            return this;
        return new Pipe(centre, L, rhi, axis);
    }
    public Pipe hole() {
        return new Pipe(centre, L, rlo, axis);
    }
}


public class Polygon : SDF {
    public int axis { get; }
    public List<Vec2> points { get; }
    public float axial_lo { get; }
    public float axial_hi { get; }

    public Polygon(in List<Vec2> points, float axial_lo, float axial_hi,
            int axis=2) {
        assert(points.Count >= 3, $"numel={points.Count}");
        assert(is_simple_polygon(points), "cooked it");
        assert(axial_hi >= axial_lo, $"lo={axial_lo}, hi={axial_hi}");
        this.axis = axis;
        this.points = points;
        this.axial_lo = axial_lo;
        this.axial_hi = axial_hi;
        for (int i=0; i<points.Count; ++i) {
            float axial = ((i % 2) == 0) ? axial_lo : axial_hi;
            Vec3 p = rejection(points[i], axis, axial);
            include_in_bounds(p);
        }
    }

    public override float signed_dist(in Vec3 p) {
        Vec2 p_proj = projection(p, axis);

        int N = points.Count;
        int winding = 0;

        float dist_proj = INF;
        for (int i=0; i<N; ++i) {
            Vec2 a = points[i];
            Vec2 b = points[(i + 1 == N) ? 0 : i + 1];

            Vec2 ab = b - a;
            Vec2 ap = p_proj - a;

            if (a.Y <= p_proj.Y) {
                if (b.Y > p_proj.Y && cross(ab, ap) > 0)
                    winding += 1;
            } else {
                if (b.Y <= p_proj.Y && cross(ab, ap) < 0)
                    winding -= 1;
            }

            float t = dot(ap, ab) / dot(ab, ab);
            t = clamp(t, 0f, 1f); // clamp to segment
            Vec2 closest = a + t*ab;

            float d = mag(p_proj - closest);
            dist_proj = min(dist_proj, d);
        }

        if (winding != 0)
            dist_proj = -dist_proj;

        float dist_axial = max(axial_lo - p[axis], p[axis] - axial_hi);

        float dist;
        if (dist_proj <= 0f || dist_axial <= 0f) {
            dist = max(dist_proj, dist_axial);
        } else {
            dist = hypot(dist_proj, dist_axial);
        }
        return dist;
    }



    protected static bool is_simple_polygon(in List<Vec2> points) {
        int N = points.Count;
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
}
