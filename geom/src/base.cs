using static br.Br;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using IImplicit = PicoGK.IImplicit;
using BBox3 = PicoGK.BBox3;

using Colour = PicoGK.ColorFloat;
using Image = PicoGK.Image;
using ImageColor = PicoGK.ImageColor;
using TgaIo = PicoGK.TgaIo;

namespace br {


public static class _SDF {
    public static Voxels voxels(IImplicit imp, BBox3 bounds,
            bool enforce_faces=true) {
        if (!enforce_faces)
            return new Voxels(imp, bounds);
        Voxels vox = new Bar(bounds);
        vox.IntersectImplicit(imp);
        return vox;
    }
}


public delegate float SDFfunction(in Vec3 p);

public abstract class SDFfromfunc : IImplicit {
    public abstract SDFfunction sdf { get; }
    public abstract float max_off { get; }
    public abstract float fSignedDistance(in Vec3 p);

    public Voxels voxels(BBox3 bounds, bool enforce_faces=true)
        => _SDF.voxels(this, bounds, enforce_faces);
}

public class SDFfilled : SDFfromfunc {
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

public class SDFshelled : SDFfromfunc {
    private SDFfunction _sdf;
    public float off { get; }
    public float semith { get; }
    public float th => 2f*semith;
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
    public SDFshelled at_inner() => new(sdf, off + semith, 2f*semith);
    public SDFshelled at_outer() => new(sdf, off - semith, 2f*semith);

    public override float fSignedDistance(in Vec3 p) {
        return abs(sdf(p) - off) - semith;
    }
}


public abstract class FramedShape<This>
        where This : FramedShape<This> {

    public abstract Frame centre { get; }
    public abstract This with_centre(in Frame newcentre);

    public This transx(float shift, bool relative=true)
        => with_centre(centre.transx(shift, relative));
    public This transy(float shift, bool relative=true)
        => with_centre(centre.transy(shift, relative));
    public This transz(float shift, bool relative=true)
        => with_centre(centre.transz(shift, relative));
    public This translate(Vec3 shift, bool relative=true)
        => with_centre(centre.translate(shift, relative));

    public This rotxy(float by, bool relative=true)
        => with_centre(centre.rotxy(by, relative));
    public This rotzx(float by, bool relative=true)
        => with_centre(centre.rotzx(by, relative));
    public This rotyz(float by, bool relative=true)
        => with_centre(centre.rotyz(by, relative));
    public This rotate(Vec3 about, float by, bool relative=true)
        => with_centre(centre.rotate(about, by, relative));

    public This swing(Vec3 around, Vec3 about, float by, bool relative=true)
        => with_centre(centre.swing(around, about, by, relative));

    public This reflect(Vec3 point, Vec3 normal, int flip_axis,
            bool relative=true)
        => with_centre(centre.reflect(point, normal, flip_axis, relative));
    public This reflectxy(Vec3 point, Vec3 normal, bool relative=true)
        => with_centre(centre.reflectxy(point, normal, relative));
    public This reflectzx(Vec3 point, Vec3 normal, bool relative=true)
        => with_centre(centre.reflectzx(point, normal, relative));
    public This reflectyz(Vec3 point, Vec3 normal, bool relative=true)
        => with_centre(centre.reflectyz(point, normal, relative));

    public This flipxy() => with_centre(centre.flipxy());
    public This flipzx() => with_centre(centre.flipzx());
    public This flipyz() => with_centre(centre.flipyz());

    public This swapxy() => with_centre(centre.swapxy());
    public This swapzx() => with_centre(centre.swapzx());
    public This swapyz() => with_centre(centre.swapyz());

    public This cyclecw() => with_centre(centre.cyclecw());
    public This cycleccw() => with_centre(centre.cycleccw());

    public This compose(in Frame other) => with_centre(centre.compose(other));
}


public abstract class BoundedShape<This> : FramedShape<This>, IImplicit
        // gotta implement the non-bounded IImplicit version so that when
        // defining the implicit conversion to voxels it doesnt hit a conflict
        // with two one-arg new Voxels() definitions.
        where This : BoundedShape<This> {

    public abstract BBox3 bounds { get; }
    public abstract float fSignedDistance(in Vec3 p);

    public static implicit operator Voxels(BoundedShape<This> obj)
        => new(obj, obj.bounds);
}

public abstract class ShellableShape<This> : BoundedShape<This>
        where This : ShellableShape<This> {

    public abstract bool isfilled { get; }
    public bool isshelled => !isfilled;

    /* when currently filled: */
    public This shelled(float th) {
        assert(isfilled);
        return _shelled(th);
    }

    /* when currently shelled: */
    public This negative {
        get {
            assert(isshelled);
            return _negative();
        }
    }

    /* either: */
    public This positive { get { return _positive(); } }
    public abstract This hollowed(float inner_length);
    public abstract This girthed(float outer_length);
            // voted best function name in bruv three years running.


    public abstract This _shelled(float th);
    public abstract This _negative();
    public abstract This _positive();
}




public enum Extend {
    NONE   = 0,
    UP     = 0x1,
    DOWN   = 0x2,
    UPDOWN = 0x3,
}

public abstract class AxialShape<This> : ShellableShape<This>
        // tragically, the logical requirements of an axial shape do not require
        // shellable, however does csharp has made some appalling decisions, this
        // must inherit from shellable if anything axial wants to be shellable.
        where This : AxialShape<This> {

    // Note that axial shape also implies shelling does not happen in the axial
    // direction, i.e. an "axial shape" cube should not shell the top and bottom
    // face when shelled. this is just an arbitrary decision i have come to.

    public abstract float Lz { get; }
    public abstract This with_Lz(float new_Lz);

    public Frame bbase => centre.transz(-Lz/2f);
              // bloody c sharp keywords out the wah zu.
    public This with_bbase(in Frame newbbase)
        => with_centre(newbbase.transz(+Lz/2f));

    public This at_base() => transz(-Lz/2f);
    public This at_top()  => transz(+Lz/2f);

    public This next_down(float count=1f) => transz(-count*Lz);
    public This next_up(float count=1f)   => transz(+count*Lz);

    public This extended(float extra, Extend dir) {
        float Dz = 0f;
        switch (dir) {
            case Extend.UP:     Dz = +extra/2f; break;
            case Extend.DOWN:   Dz = -extra/2f; break;
            case Extend.UPDOWN: Dz = 0f; extra *= 2f; break;
        }
        return transz(Dz).with_Lz(Lz + extra);
    }
}




public class Space : FramedShape<Space>, IImplicit {
    public override Frame centre { get; }
    public float min_z { get; }
    public float max_z { get; }

    public Space(in Frame centre, float min_z, float max_z) {
        assert(max_z > min_z, $"min_z={min_z}, max_z={max_z}");
        assert(min_z != +INF, $"min_z={min_z}");
        assert(max_z != -INF, $"max_z={max_z}");
        assert(min_z != -INF || max_z != +INF);
        this.centre = centre;
        this.min_z = min_z;
        this.max_z = max_z;
    }


    public float fSignedDistance(in Vec3 p) {
        float z = dot(p - centre.pos, centre.Z);
        return max(min_z - z, z - max_z);
    }

    public override Space with_centre(in Frame newcentre)
        => new(newcentre, min_z, max_z);


    // literally fuck c sharp for not inheriting default implementations from
    // interfaces.
    public Voxels voxels(BBox3 bounds, bool enforce_faces=true)
        => _SDF.voxels(this, bounds, enforce_faces);
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


    public static Sectioner pie(float theta0, float theta1) {
        assert(isgood(theta0));
        assert(isgood(theta1));
        if (theta1 < theta0)
            swap(ref theta0, ref theta1);
        float Dtheta = theta1 - theta0;
        if (Dtheta >= TWOPI)
            return new(); // no cuts.
        Frame frame0 = new(ZERO3, fromcyl(1f, theta0 + PI_2, 0f));
        Frame frame1 = new(ZERO3, fromcyl(1f, theta1 - PI_2, 0f));
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


public class Ball : ShellableShape<Ball> {
    public override Frame centre { get; } // rotation irrevelant.
    public override BBox3 bounds { get; }
    public float inner_r { get; } // >0, or =-inf
    public float outer_r { get; } // >0

    public Vec3 pos => centre.pos;
    public float r { get { assert(isfilled); return outer_r; } }

    public Ball(float outer_r)
        : this(-INF, outer_r) {}
    public Ball(float inner_r, float outer_r)
        : this(ZERO3, inner_r, outer_r) {}
    public Ball(in Vec3 centre, float outer_r)
        : this(centre, -INF, outer_r) {}
    public Ball(in Vec3 centre, float inner_r, float outer_r)
        : this(new Frame(centre), inner_r, outer_r) {}
    public Ball(in Frame centre, float inner_r, float outer_r) {
        assert(isgood(inner_r) || inner_r == -INF, $"inner_r={inner_r}");
        assert(isgood(outer_r), $"outer_r={outer_r}");
        assert(inner_r > 0f || inner_r == -INF, $"inner_r={inner_r}");
        assert(outer_r > 0f, $"outer_r={outer_r}");
        assert(outer_r > inner_r, $"inner_r={inner_r}, outer_r={outer_r}");
        this.centre = centre;
        this.inner_r = inner_r;
        this.outer_r = outer_r;
        bounds = new(centre.pos - ONE3*outer_r, centre.pos + ONE3*outer_r);
    }


    public override float fSignedDistance(in Vec3 p) {
        float r = mag(p - centre.pos);
        return max(inner_r - r, r - outer_r);
    }

    public override Ball with_centre(in Frame newcentre)
        => new(newcentre, inner_r, outer_r);

    public override bool isfilled => inner_r == -INF;
    public override Ball _shelled(float th)
        => (th >= 0f)
         ? new(centre, outer_r, outer_r + th)
         : new(centre, outer_r + th, outer_r);
    public override Ball _negative() => new(centre, -INF, inner_r);
    public override Ball _positive() => new(centre, -INF, outer_r);
    public override Ball hollowed(float inner_length)
        => new(centre, inner_length, outer_r);
    public override Ball girthed(float outer_length)
        => new(centre, inner_r, outer_length);
}


public class Pill : ShellableShape<Pill> {
    public override Frame centre { get; }
    public override BBox3 bounds { get; }
    public float Lz { get; } // >0
    public float inner_r { get; } // >0, or =-inf.
    public float outer_r { get; } // >0

    public float r { get { assert(isfilled); return outer_r; } }
    public Vec3 a { get; }
    public Vec3 b { get; }
    public Vec3 AB { get; } // may be zero, otherwise unit.

    public Pill(Vec3 a, Vec3 b, float outer_r)
        : this(a, b, -INF, outer_r) {}
    public Pill(Vec3 a, Vec3 b, float inner_r, float outer_r)
        : this(new Frame(a + (a + b)/2f, b - a), mag(b - a), inner_r, outer_r) {}
    public Pill(in Frame centre, float Lz, float outer_r)
        : this(centre, Lz, -INF, outer_r) {}
    public Pill(in Frame centre, float Lz, float inner_r, float outer_r) {
        assert(isgood(Lz), $"Lz={Lz}");
        assert(isgood(inner_r) || inner_r == -INF, $"inner_r={inner_r}");
        assert(isgood(outer_r), $"outer_r={outer_r}");
        assert(Lz > 0f, $"Lz={Lz}");
        assert(inner_r > 0f || inner_r == -INF, $"inner_r={inner_r}");
        assert(outer_r > 0f, $"outer_r={outer_r}");
        assert(outer_r > inner_r, $"inner_r={inner_r}, outer_r={outer_r}");
        this.centre = centre;
        this.Lz = Lz;
        this.inner_r = inner_r;
        this.outer_r = outer_r;
        a = centre * (-Lz/2f * uZ3);
        b = centre * (+Lz/2f * uZ3);
        AB = normalise_nonzero(b - a);
        bounds = new(a - outer_r*ONE3, b + outer_r*ONE3);
        bounds.Include(a + outer_r*ONE3);
        bounds.Include(b - outer_r*ONE3);
    }

    public Pill(in Frame frame, Vec3 a, Vec3 b, float outer_r)
        : this(frame * a, frame * b, -INF, outer_r) {}
    public Pill(in Frame frame, Vec3 a, Vec3 b, float inner_r, float outer_r)
        : this(frame * a, frame * b, inner_r, outer_r) {}


    public override float fSignedDistance(in Vec3 p) {
        float axial = dot(AB, p - a);
        Vec3 c = a + AB*clamp(axial, 0f, Lz);
        float r = mag(p - c);
        return max(inner_r - r, r - outer_r);
    }

    public override Pill with_centre(in Frame newcentre)
        => new(newcentre, Lz, inner_r, outer_r);

    public override bool isfilled => inner_r == -INF;
    public override Pill _shelled(float th)
        => (th >= 0f)
         ? new(centre, Lz, outer_r, outer_r + th)
         : new(centre, Lz, outer_r + th, outer_r);
    public override Pill _negative() => new(centre, Lz, -INF, inner_r);
    public override Pill _positive() => new(centre, Lz, -INF, outer_r);
    public override Pill hollowed(float inner_length)
        => new(centre, Lz, inner_length, outer_r);
    public override Pill girthed(float outer_length)
        => new(centre, Lz, inner_r, outer_length);
}



public class Donut : ShellableShape<Donut> { // torus is too lame. its also the
                                             // surface not the solid.
    public override Frame centre { get; }
    public override BBox3 bounds { get; }
    public float R { get; } // distance from axis of revolution. >=0
    public float inner_r { get; } // inner half-thickness. >0, or =-inf
    public float outer_r { get; } // outer half-thickness. >0

    public Donut(in Frame centre, float R, float outer_r)
        : this(centre, R, 0f, outer_r) {}
    public Donut(in Frame centre, float R, float inner_r, float outer_r) {
        assert(isgood(R), $"R={R}");
        assert(isgood(inner_r), $"inner_r={inner_r}");
        assert(isgood(outer_r), $"outer_r={outer_r}");
        assert(R >= 0f, $"R={R}");
        assert(inner_r >= 0f, $"inner_r={inner_r}");
        assert(outer_r > 0f, $"outer_r={outer_r}");
        assert(outer_r > inner_r, $"inner_r={inner_r}, outer_r={outer_r}");
        assert(R >= outer_r, $"R={R}, outer_r={outer_r}"); // sorry.
        this.centre = centre;
        this.R = R;
        this.inner_r = inner_r;
        this.outer_r = outer_r;
        BBox3 bbox = new();
        centre.bbox_include_circle(ref bbox, R + outer_r, -outer_r);
        centre.bbox_include_circle(ref bbox, R + outer_r, +outer_r);
        bounds = bbox;
    }


    public override float fSignedDistance(in Vec3 p) {
        Vec3 q = centre / p;
        float r = hypot(magxy(q) - R, q.Z);
        return max(inner_r - r, r - outer_r);
    }

    public override Donut with_centre(in Frame newcentre)
        => new(newcentre, R, inner_r, outer_r);

    public override bool isfilled => inner_r == -INF;
    public override Donut _shelled(float th)
        => (th >= 0f)
         ? new(centre, R, outer_r, outer_r + th)
         : new(centre, R, outer_r + th, outer_r);
    public override Donut _positive() => new(centre, R, -INF, outer_r);
    public override Donut _negative() => new(centre, R, -INF, inner_r);
    public override Donut hollowed(float inner_length)
        => new(centre, R, inner_length, outer_r);
    public override Donut girthed(float outer_length)
        => new(centre, R, inner_r, outer_length);
}



public class Bar : AxialShape<Bar> {
    public override Frame centre { get; }
    public override BBox3 bounds { get; }
    public override float Lz { get; } // >0
    public float inner_Lx { get; } // >0, or =-inf.
    public float outer_Lx { get; } // >0
    public float inner_Ly { get; } // >0, or =-inf iff inner_Lx == -inf.
    public float outer_Ly { get; } // >0
    public Vec3 inner_size => new(inner_Lx, inner_Ly, isfilled ? -INF : Lz);
        // note that when filled, inner_size = -INF3.
    public Vec3 outer_size => new(outer_Lx, outer_Ly, Lz);

    public float Lx { get { assert(isfilled); return outer_Lx; } }
    public float Ly { get { assert(isfilled); return outer_Ly; } }
    public Vec3 size { get { assert(isfilled); return new(Lx, Ly, Lz); } }


    public Bar(in Frame bbase, float L)
        : this(bbase, L, L, L) {}
    public Bar(in Frame bbase, float Lz, float Lxy)
        : this(bbase, Lxy, Lxy, Lz) {}
    public Bar(in Frame bbase, Vec3 size)
        : this(bbase, size.X, size.Y, size.Z) {}
    public Bar(in Frame bbase, float Lx, float Ly, float Lz)
        : this(bbase, Lz, -INF, Lx, -INF, Ly) {}
    public Bar(in Frame bbase, float Lz, float inner_Lx, float outer_Lx,
            float inner_Ly, float outer_Ly) {
        assert(isgood(Lz), $"Lz={Lz}");
        assert(isgood(inner_Lx) || inner_Lx == -INF, $"inner_Lx={inner_Lx}");
        assert(isgood(outer_Lx), $"outer_Lx={outer_Lx}");
        assert(isgood(inner_Ly) || inner_Ly == -INF, $"inner_Ly={inner_Ly}");
        assert(isgood(outer_Ly), $"outer_Ly={outer_Ly}");
        assert(abs(Lz) > 0f, $"Lz={Lz}");
        assert(inner_Lx > 0f || inner_Lx == -INF, $"inner_Lx={inner_Lx}");
        assert(outer_Lx > 0f, $"outer_Lx={outer_Lx}");
        assert(inner_Ly > 0f || inner_Ly == -INF, $"inner_Ly={inner_Ly}");
        assert(outer_Ly > 0f, $"outer_Ly={outer_Ly}");
        assert(outer_Lx > inner_Lx, $"inner_Lx={inner_Lx}, outer_Lx={outer_Lx}");
        assert(outer_Ly > inner_Ly, $"inner_Ly={inner_Ly}, outer_Ly={outer_Ly}");
        if (inner_Lx == -INF || inner_Ly == -INF)
            inner_Lx = inner_Ly = -INF; // collapse to empty.
            // note we could require both to be -inf or both not, but nah.
        this.centre = bbase.transz(Lz/2f);
        Lz = abs(Lz);
        this.Lz = Lz;
        this.inner_Lx = inner_Lx;
        this.outer_Lx = outer_Lx;
        this.inner_Ly = inner_Ly;
        this.outer_Ly = outer_Ly;
        BBox3 bbox = new(-outer_size/2f,  outer_size/2f);
        // unreal of c sharp to miss unary plus on the fucking vectors.
        bounds = centre.to_global_bbox(bbox);
    }

    public Bar(in BBox3 box)
        : this(new Frame(), box.vecMin, box.vecMax) {}
    public Bar(in Frame frame, in BBox3 box)
        : this(frame, box.vecMin, box.vecMax) {}
    public Bar(in Vec3 a, in Vec3 b)
        : this(new Frame(), a, b) {}
    public Bar(in Frame frame, in Vec3 a, in Vec3 b)
        // shitass csharp not being able to forward to factory functions in pre-
        // init.
        : this(
            frame.translate(new(ave(a.X, b.X), ave(a.Y, b.Y), min(a.Z, b.Z))),
            max(a, b) - min(a, b)
        ) {}

    public static Bar centred(in Frame centre, float Lz, float inner_Lx,
            float outer_Lx, float inner_Ly, float outer_Ly) {
        assert(Lz > 0f, $"Lz={Lz}");
        return new(centre.transz(-Lz/2f), Lz, inner_Lx, outer_Lx, inner_Ly,
                outer_Ly);
    }


    public Bar sizez(float new_Lz)
        => centred(centre, new_Lz, inner_Lx, outer_Lx, inner_Ly, outer_Ly);
    public Bar sizex(float length) => sized(new Vec3(length, Ly, Lz));
    public Bar sizey(float length) => sized(new Vec3(Lx, length, Lz));
    public Bar sized(Vec3 newsize) {
        assert(isfilled);
        return centred(centre, newsize.Z, -INF, newsize.X, -INF, newsize.Y);
    }

    public Bar scalez(float by)
        => centred(centre, by*Lz, inner_Lx, outer_Lx, inner_Ly, outer_Ly);
    public Bar scalex(float by) => scaled(ONE3 + (by - 1f)*uX3);
    public Bar scaley(float by) => scaled(ONE3 + (by - 1f)*uY3);
    public Bar scaled(Vec3 by) {
        assert(isfilled);
        return centred(centre, by.Z*Lz, -INF, by.X*Lx, -INF, by.Y*Ly);
    }


    [Flags] public enum Shift {
        X0 = 0x01,
        X1 = 0x02,
        Y0 = 0x04,
        Y1 = 0x08,
        Z0 = 0x10,
        Z1 = 0x20,
        MASK_ALL = 0x3F,
        MASK_X = 0x03,
        MASK_Y = 0x0C,
        MASK_Z = 0x30,
    }

    public const Shift X0 = Shift.X0;
    public const Shift X1 = Shift.X1;
    public const Shift Y0 = Shift.Y0;
    public const Shift Y1 = Shift.Y1;
    public const Shift Z0 = Shift.Z0;
    public const Shift Z1 = Shift.Z1;

    public const Shift X0_Y0 = X0 | Y0;
    public const Shift X0_Y1 = X0 | Y1;
    public const Shift X0_Z0 = X0 | Z0;
    public const Shift X0_Z1 = X0 | Z1;
    public const Shift X1_Y0 = X1 | Y0;
    public const Shift X1_Y1 = X1 | Y1;
    public const Shift Y0_Z0 = Y0 | Z0;
    public const Shift Y0_Z1 = Y0 | Z1;
    public const Shift Y1_Z0 = Y1 | Z0;
    public const Shift Y1_Z1 = Y1 | Z1;

    public const Shift X0_Y0_Z0 = X0 | Y0 | Z0;
    public const Shift X0_Y0_Z1 = X0 | Y0 | Z1;
    public const Shift X0_Y1_Z0 = X0 | Y1 | Z0;
    public const Shift X0_Y1_Z1 = X0 | Y1 | Z1;
    public const Shift X1_Y0_Z0 = X1 | Y0 | Z0;
    public const Shift X1_Y0_Z1 = X1 | Y0 | Z1;
    public const Shift X1_Y1_Z0 = X1 | Y1 | Z0;
    public const Shift X1_Y1_Z1 = X1 | Y1 | Z1;


    public Vec3 point_on_surface(Shift where, bool relative=false,
            bool validate=false, int max_popcnt=32) {
        int wher = (int)where; // no implicit enum to int is ridiculous.
        assert(popcnt(wher) <= max_popcnt);
        if (validate) {
            assert(isclr(wher, ~(int)Shift.MASK_ALL), $"where={where}");
            assert(popcnt(wher & (int)Shift.MASK_X) <= 1, $"where={where}");
            assert(popcnt(wher & (int)Shift.MASK_Y) <= 1, $"where={where}");
            assert(popcnt(wher & (int)Shift.MASK_Z) <= 1, $"where={where}");
        }
        assert(isfilled);
        Vec3 p = ZERO3;
        if (isset(wher, (int)X0)) p.X -= outer_Lx/2f;
        if (isset(wher, (int)X1)) p.X += outer_Lx/2f;
        if (isset(wher, (int)Y0)) p.Y -= outer_Ly/2f;
        if (isset(wher, (int)Y1)) p.Y += outer_Ly/2f;
        if (isset(wher, (int)Z0)) p.Z -= Lz/2f;
        if (isset(wher, (int)Z1)) p.Z += Lz/2f;
        if (!relative)
            p = centre * p;
        return p;
    }

    public Bar at_surface(Shift where)
        => translate(point_on_surface(where, true));
    public Bar at_face(Shift where)
        => translate(point_on_surface(where, true, true, 1));
    public Bar at_edge(Shift where)
        => translate(point_on_surface(where, true, true, 2));
    public Bar at_corner(Shift where)
        => translate(point_on_surface(where, true, true, 3));

    public Bar at_outer_surface(Shift where)
        => translate(positive.point_on_surface(where, true));
    public Bar at_outer_face(Shift where)
        => translate(positive.point_on_surface(where, true, true, 1));
    public Bar at_outer_edge(Shift where)
        => translate(positive.point_on_surface(where, true, true, 2));
    public Bar at_outer_corner(Shift where)
        => translate(positive.point_on_surface(where, true, true, 3));

    public Bar at_inner_surface(Shift where)
        => translate(negative.point_on_surface(where, true));
    public Bar at_inner_face(Shift where)
        => translate(negative.point_on_surface(where, true, true, 1));
    public Bar at_inner_edge(Shift where)
        => translate(negative.point_on_surface(where, true, true, 2));
    public Bar at_inner_corner(Shift where)
        => translate(negative.point_on_surface(where, true, true, 3));


    public Bar hollowed(float new_inner_Lx, float new_inner_Ly)
        => centred(centre, Lz, new_inner_Lx, outer_Lx, new_inner_Ly, outer_Ly);
    public Bar girthed(float new_outer_Lx, float new_outer_Ly)
        => centred(centre, Lz, inner_Lx, new_outer_Lx, inner_Ly, new_outer_Ly);


    public Vec3[] get_corners(bool relative=false) {
        assert(isfilled);
        Vec3[] corners = [
            new Vec3(-outer_Lx/2f, -outer_Ly/2f, -Lz/2f),
            new Vec3(-outer_Lx/2f, -outer_Ly/2f, +Lz/2f),
            new Vec3(-outer_Lx/2f, +outer_Ly/2f, -Lz/2f),
            new Vec3(-outer_Lx/2f, +outer_Ly/2f, +Lz/2f),
            new Vec3(+outer_Lx/2f, -outer_Ly/2f, -Lz/2f),
            new Vec3(+outer_Lx/2f, -outer_Ly/2f, +Lz/2f),
            new Vec3(+outer_Lx/2f, +outer_Ly/2f, -Lz/2f),
            new Vec3(+outer_Lx/2f, +outer_Ly/2f, +Lz/2f),
        ];
        if (!relative) {
            for (int i=0; i<numel(corners); ++i)
                corners[i] = centre * corners[i];
        }
        return corners;
    }


    public static implicit operator Mesh(Bar obj) {
        Mesh mesh = new();
        mesh.AddVertices(obj.positive.get_corners(), out _);
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
        if (obj.isshelled) {
            // anti inner mesh.
            mesh.AddVertices(obj.negative.get_corners(), out _);
            // +X quad.
            mesh.nAddTriangle(8 + 4, 8 + 7, 8 + 5);
            mesh.nAddTriangle(8 + 4, 8 + 6, 8 + 7);
            // -X quad.
            mesh.nAddTriangle(8 + 0, 8 + 3, 8 + 2);
            mesh.nAddTriangle(8 + 0, 8 + 1, 8 + 3);
            // +Y quad.
            mesh.nAddTriangle(8 + 2, 8 + 7, 8 + 6);
            mesh.nAddTriangle(8 + 2, 8 + 3, 8 + 7);
            // -Y quad.
            mesh.nAddTriangle(8 + 0, 8 + 5, 8 + 1);
            mesh.nAddTriangle(8 + 0, 8 + 4, 8 + 5);
            // +Z quad.
            mesh.nAddTriangle(8 + 1, 8 + 7, 8 + 3);
            mesh.nAddTriangle(8 + 1, 8 + 5, 8 + 7);
            // -Z quad.
            mesh.nAddTriangle(8 + 0, 8 + 6, 8 + 4);
            mesh.nAddTriangle(8 + 0, 8 + 2, 8 + 6);
        }
        return mesh;
    }

    public static implicit operator Voxels(Bar obj)
        => new((Mesh)obj);


    public override float fSignedDistance(in Vec3 p) {
        Vec3 q = centre / p;

        float outer_dist;
        {
            float outside = minelem(abs(q) - outer_size/2f);
            float inside  = maxelem(abs(q) - outer_size/2f);
            outside = max(outside, 0f);
            inside  = min(inside, 0f);
            outer_dist = inside + outside;
        }
        float inner_dist;
        {
            float outside = minelem(abs(q) - inner_size/2f);
            float inside  = maxelem(abs(q) - inner_size/2f);
            outside = max(outside, 0f);
            inside  = min(inside, 0f);
            inner_dist = inside + outside;
        }
        return max(outer_dist, -inner_dist);
    }

    public override Bar with_centre(in Frame newcentre)
        => centred(newcentre, Lz, inner_Lx, outer_Lx, inner_Ly, outer_Ly);
    public override Bar with_Lz(float new_Lz)
        => centred(centre, new_Lz, inner_Lx, outer_Lx, inner_Ly, outer_Ly);

    public override bool isfilled => inner_Lx == -INF;
    public override Bar _shelled(float th)
        => (th >= 0f)
         ? centred(centre, Lz, outer_Lx, outer_Lx + th, outer_Ly, outer_Ly + th)
         : centred(centre, Lz, outer_Lx + th, outer_Lx, outer_Ly + th, outer_Ly);
    public override Bar _negative()
        => centred(centre, Lz, -INF, inner_Lx, -INF, inner_Ly);
    public override Bar _positive()
        => centred(centre, Lz, -INF, outer_Lx, -INF, outer_Ly);
    public override Bar hollowed(float inner_length)
        => hollowed(inner_length, inner_length);
    public override Bar girthed(float outer_length)
        => girthed(outer_length, outer_length);
}



public class Rod : AxialShape<Rod> {
    public override Frame centre { get; }
    public override BBox3 bounds { get; }
    public override float Lz { get; } // >0
    public float inner_r { get; } // >0, or =-inf
    public float outer_r { get; } // >0

    public float r { get { assert(isfilled); return outer_r; } }

    public Rod(in Frame bbase, float Lz, float outer_r)
        : this(bbase, Lz, -INF, outer_r) {}
    public Rod(in Frame bbase, float Lz, float inner_r, float outer_r) {
        assert(isgood(Lz), $"Lz={Lz}");
        assert(isgood(inner_r) || inner_r == -INF, $"inner_r={inner_r}");
        assert(isgood(outer_r), $"outer_r={outer_r}");
        assert(abs(Lz) > 0f, $"Lz={Lz}");
        assert(inner_r > 0f || inner_r == -INF, $"inner_r={inner_r}");
        assert(outer_r > 0f, $"outer_r={outer_r}");
        assert(outer_r > inner_r, $"inner_r={inner_r}, outer_r={outer_r}");
        this.centre = bbase.transz(Lz/2f);
        Lz = abs(Lz);
        this.Lz = Lz;
        this.inner_r = inner_r;
        this.outer_r = outer_r;
        BBox3 bbox = new();
        centre.bbox_include_circle(ref bbox, outer_r, -Lz/2f);
        centre.bbox_include_circle(ref bbox, outer_r, +Lz/2f);
        bounds = bbox;
    }

    public static Rod centred(in Frame centre, float Lz, float inner_r,
            float outer_r) {
        assert(Lz > 0f, $"Lz={Lz}");
        return new(centre.transz(-Lz/2f), Lz, inner_r, outer_r);
    }


    public Rod flipz() => flipzx(); // axisymmetric.

    public Rod sizez(float new_Lz)
        => centred(centre, new_Lz, inner_r, outer_r);
    public Rod sizer(float length) => sized(Lz, length);
    public Rod sized(float new_Lz, float new_r) {
        assert(isfilled);
        return centred(centre, new_Lz, 0f, new_r);
    }

    public Rod scalez(float by)
        => centred(centre, by*Lz, inner_r, outer_r);
    public Rod scaler(float by) => scaled(1f, by);
    public Rod scaled(float Pz, float Pr) {
        assert(isfilled);
        return centred(centre, Pz*Lz, 0f, Pr*outer_r);
    }


    public override float fSignedDistance(in Vec3 p) {
        Vec3 q = centre / p;
        float r = magxy(q);
        float z = q.Z;

        float dist_axial = max(-Lz/2f - z, z - Lz/2f);
        float dist_radial = max(inner_r - r, r - outer_r);

        float dist;
        if (dist_radial <= 0f || dist_axial <= 0f) {
            dist = max(dist_radial, dist_axial);
        } else {
            dist = hypot(dist_radial, dist_axial);
        }
        return dist;
    }

    public override Rod with_centre(in Frame newcentre)
        => centred(newcentre, Lz, inner_r, outer_r);
    public override Rod with_Lz(float new_Lz)
        => centred(centre, new_Lz, inner_r, outer_r);

    public override bool isfilled => inner_r == -INF;
    public override Rod _shelled(float th)
        => (th >= 0f)
         ? centred(centre, Lz, outer_r, outer_r + th)
         : centred(centre, Lz, outer_r + th, outer_r);
    public override Rod _positive() => centred(centre, Lz, -INF, outer_r);
    public override Rod _negative() => centred(centre, Lz, -INF, inner_r);
    public override Rod hollowed(float inner_length)
        => centred(centre, Lz, inner_length, outer_r);
    public override Rod girthed(float outer_length)
        => centred(centre, Lz, inner_r, outer_length);
}




public class Cone : AxialShape<Cone> {
    public override Frame centre { get; }
    public override BBox3 bounds { get; }
    public override float Lz { get; } // >0
    public float inner_r0 { get; private set; } // >=0
    public float inner_r1 { get; private set; } // >=0
    public float outer_r0 { get; private set; } // >=0
    public float outer_r1 { get; private set; } // >=0
    // ^ fully defined by those

    // little extra for flavour:
    public float inner_phi { get; private set; } // (-pi/2, pi/2) U {nan}
    public float outer_phi { get; private set; } // (-pi/2, pi/2)
    public Frame? inner_tip { get; private set; } // centre translated.
    public Frame? outer_tip { get; private set; } // centre translated.

    public float r0 { get { assert(isfilled); return outer_r0; } }
    public float r1 { get { assert(isfilled); return outer_r1; } }
    public float phi { get { assert(isfilled); return outer_phi; } }
    public Frame? tip { get { assert(isfilled); return outer_tip; } }

    private Vec2 a;
    private Vec2 b;
    private Vec2 c;
    private Vec2 d;
    private float magab;
    private float magcd;
    private Vec2 AB;
    private Vec2 CD;
    private Vec2 ABperp;
    private Vec2 CDperp;


    public Cone(in Frame bbase, float Lz, float r0, float r1)
        : this(bbase, Lz, -INF, r0, -INF, r1) {}

    public Cone(in Frame bbase, float Lz, float inner_r0, float outer_r0,
            float inner_r1, float outer_r1) {
        assert(isgood(Lz), $"Lz={Lz}");
        assert(isgood(inner_r0) || inner_r0 == -INF, $"inner_r0={inner_r0}");
        assert(isgood(outer_r0), $"outer_r0={outer_r0}");
        assert(isgood(inner_r1) || inner_r1 == -INF, $"inner_r1={inner_r1}");
        assert(isgood(outer_r1), $"outer_r1={outer_r1}");
        assert(abs(Lz) > 0f, $"Lz={Lz}");
        assert(outer_r0 >= 0f, $"outer_r0={outer_r0}");
        assert(outer_r1 >= 0f, $"outer_r1={outer_r1}");
        assert(outer_r0 >= inner_r0,
                $"inner_r0={inner_r0}, outer_r0={outer_r0}");
        assert(outer_r1 >= inner_r1,
                $"inner_r1={inner_r1}, outer_r1={outer_r1}");
        assert(outer_r0 > 0f || outer_r1 > 0f,
                $"outer_r0={outer_r0}, outer_r1={outer_r1}");
        assert((inner_r0 == -INF) == (inner_r1 == -INF),
                $"inner_r0={inner_r0}, inner_r1={inner_r1}");
        if (inner_r0 <= 0f && inner_r1 <= 0f) {
            // collapse to filled.
            inner_r0 = -INF;
            inner_r1 = -INF;
        }
        this.centre = bbase.transz(Lz/2f);
        if (Lz < 0f) {
            swap(ref inner_r0, ref inner_r1);
            swap(ref outer_r0, ref outer_r1);
            Lz = -Lz;
        }
        assert(Lz > 0f);
        this.Lz = Lz;
        this.inner_r0 = inner_r0;
        this.inner_r1 = inner_r1;
        this.outer_r0 = outer_r0;
        this.outer_r1 = outer_r1;
        inner_phi = atan2(inner_r1 - inner_r0, Lz);
        outer_phi = atan2(outer_r1 - outer_r0, Lz);
        inner_tip = (nonnan(inner_phi) && inner_phi != 0f)
                  ? centre.transz((-Lz/2f) - inner_r0/tan(inner_phi))
                  : null;
        outer_tip = (nonnan(outer_phi) && outer_phi != 0f)
                  ? centre.transz((-Lz/2f) - outer_r0/tan(outer_phi))
                  : null;
        a = new(-Lz/2f, outer_r0);
        b = new(+Lz/2f, outer_r1);
        c = new(-Lz/2f, inner_r0);
        d = new(+Lz/2f, inner_r1);
        magab = mag(b - a);
        magcd = mag(d - c);
        AB = (b - a) / magab;
        CD = (d - c) / magcd;
        ABperp = rot90ccw(AB);
        CDperp = rot90cw(CD);

        BBox3 bbox = new();
        centre.bbox_include_circle(ref bbox, outer_r0, -Lz/2f);
        centre.bbox_include_circle(ref bbox, outer_r1, +Lz/2f);
        bounds = bbox;
    }

    public static Cone phied(in Frame bbase, float phi, float Lz=NAN,
            float r0=NAN, float r1=NAN) {
        assert(isnan(Lz) || isnan(r0) || isnan(r1));
        if (isnan(Lz)) {
            if (isnan(r0)) {
                assert(nonnan(r1));
                assert(phi > 0f);
                r0 = 0f;
            }
            if (isnan(r1)) {
                assert(nonnan(r0));
                assert(phi < 0f);
                r1 = 0f;
            }
            Lz = (r1 - r0)/tan(phi);
        }
        if (isnan(r0) && isnan(r1)) {
            assert(phi != 0f);
            if (phi > 0f)
                r0 = 0f;
            else
                r1 = 0f;
        }
        r0 = ifnan(r0, r1 - abs(Lz)*tan(phi));
        r1 = ifnan(r1, r0 + abs(Lz)*tan(phi));
        assert(nonnan(Lz) && nonnan(r0) && nonnan(r1));
        return new(bbase, Lz, r0, r1);
    }

    public static Cone centred(in Frame centre, float Lz, float inner_r0,
            float outer_r0, float inner_r1, float outer_r1) {
        assert(Lz > 0f, $"Lz={Lz}");
        return new(centre.transz(-Lz/2f), Lz, inner_r0, outer_r0, inner_r1,
                outer_r1);
    }


    public Cone flipz() => flipzx(); // axisymmetric.

    public Cone hollowed(float new_inner_r0, float new_inner_r1)
        => centred(centre, Lz, new_inner_r0, outer_r0, new_inner_r1, outer_r1);
    public Cone girthed(float new_outer_r0, float new_outer_r1)
        => centred(centre, Lz, inner_r0, new_outer_r0, inner_r1, new_outer_r1);

    public Cone upto_tip() {
        if (outer_tip == null)
            throw new Exception("tip kinda far (infinitely)");
        float z2tip = dot(outer_tip.pos - centre.pos, centre.Z);
        float new_Lz = abs(z2tip) + Lz/2f;
        float new_inner_r0;
        float new_outer_r0;
        float new_inner_r1;
        float new_outer_r1;
        if (outer_phi > 0f) {
            new_inner_r1 = inner_r1;
            new_outer_r1 = outer_r1;
            new_inner_r0 = inner_r1 - ifnan(new_Lz*tan(inner_phi), 0f);
            new_outer_r0 = 0f;
        } else {
            new_inner_r0 = inner_r0;
            new_outer_r0 = outer_r0;
            new_inner_r1 = inner_r0 + ifnan(new_Lz*tan(inner_phi), 0f);
            new_outer_r1 = 0f;
        }
        return centred(
            centre.transz(z2tip + ((z2tip > 0f) ? -new_Lz/2f : new_Lz/2f)),
            new_Lz,
            new_inner_r0,
            new_outer_r0,
            new_inner_r1,
            new_outer_r1
        );
    }

    public Cone lengthed(float Dz0, float Dz1) {
        float Dz = 0.5f*(Dz1 - Dz0);
        float new_Lz = Lz + Dz0 + Dz1;
        float new_outer_r0 = outer_r0 - Dz0*tan(outer_phi);
        float new_outer_r1 = outer_r1 + Dz1*tan(outer_phi);
        float new_inner_r0 = inner_r0 - ifnan(Dz0*tan(inner_phi), 0f);
        float new_inner_r1 = inner_r1 + ifnan(Dz1*tan(inner_phi), 0f);
        return centred(
            centre.transz(Dz),
            new_Lz,
            new_inner_r0,
            new_outer_r0,
            new_inner_r1,
            new_outer_r1
        );
    }


    public static implicit operator Mesh(Cone obj) {
        List<Vec2> vertices;
        bool donut;
        if (obj.isfilled) {
            vertices = [obj.a*uX2, obj.a, obj.b, obj.b*uX2];
            donut = false;
        } else {
            if (obj.c.Y >= 0f && obj.d.Y >= 0f) {
                vertices = [obj.c, obj.a, obj.b, obj.d];
                donut = true;
            } else {
                vertices = [
                    new(obj.c.X, max(0f, obj.c.Y)),
                    obj.a,
                    obj.b,
                    new(obj.d.X, max(0f, obj.d.Y)),
                ];
                float intercept_x = cross(obj.c, obj.d) / (obj.d.Y - obj.c.Y);
                Vec2 intercept = new(intercept_x, 0f);
                // Add if non-duplicate.
                if (obj.c.Y < 0f) {
                    if (!nearto(vertices[^1], intercept))
                        vertices.Add(intercept);
                } else {
                    if (!nearto(vertices[0], intercept))
                        vertices.Insert(0, intercept);
                }
                donut = false;
            }
        }
        Polygon.cull_duplicates(vertices);
        assert(numel(vertices) >= 3, "too thin");

        // Make the divs s.t. the longest spacing between vertices is one voxel.
        int divs = (int)(TWOPI * max(obj.outer_r0, obj.outer_r1) / VOXEL_SIZE);
        divs = max(20, divs);
        return Polygon.mesh_revolved(obj.centre, vertices, slicecount: divs,
                                     donut: donut);
    }

    public static implicit operator Voxels(Cone obj)
        => new((Mesh)obj);


    public override float fSignedDistance(in Vec3 p) {
        Vec3 q = centre / p;
        float r = magxy(q);
        float z = q.Z;
        Vec2 u = new(z, r);

        // Treat as kinda a polygon lmao.

        Vec2 au = u - a;
        float t = clamp(dot(au, AB), 0f, magab);
        float dist_ab = mag(au - AB*t);
        if (dot(au, ABperp) < 0f)
            dist_ab = -dist_ab;

        Vec2 cu = u - c;
        float s = clamp(dot(cu, CD), 0f, magcd);
        float dist_cd = mag(cu - CD*s);
        if (dot(cu, CDperp) < 0f)
            dist_cd = -dist_cd;

        float dist_z = max(a.X - z, z - b.X);

        return max(max(dist_ab, dist_cd), dist_z);
    }


    public override Cone with_centre(in Frame newcentre)
        => centred(newcentre, Lz, inner_r0, outer_r0, inner_r1, outer_r1);

    public override Cone with_Lz(float new_Lz)
        => throw new Exception("not for cone");

    public override bool isfilled => inner_r0 == -INF;
    public override Cone _shelled(float th)
        => centred(
            centre,
            Lz,
            outer_r0 + min(0f, th/cos(outer_phi)),
            outer_r0 + max(0f, th/cos(outer_phi)),
            outer_r1 + min(0f, th/cos(outer_phi)),
            outer_r1 + max(0f, th/cos(outer_phi))
        );
    public override Cone _positive()
        => centred(centre, Lz, -INF, outer_r0, -INF, outer_r1);
    public override Cone _negative()
        => centred(centre, Lz, -INF, inner_r0, -INF, inner_r1);
    public override Cone hollowed(float inner_length)
        => hollowed(inner_length, inner_length);
    public override Cone girthed(float outer_length)
        => girthed(outer_length, outer_length);
}









public class Tubing {
    public Slice<Vec3> points { get; }
    public float ID { get; }
    public float th { get; }
    public float Fr { get; }

    public Tubing(in Slice<Vec3> points, float OD)
        : this(points, 0f, 0.5f*OD) {}
    public Tubing(in Slice<Vec3> points, float ID, float th, float Fr=0f) {
        assert(numel(points) >= 2, $"numel={numel(points)}");
        assert(ID >= 0f);
        assert(th > 0f);
        assert(Fr >= 0f);
        this.points = points;
        this.ID = ID;
        this.th = th;
        this.Fr = Fr;
    }

    public static implicit operator Voxels(Tubing obj) {
        Voxels vox = new();
        Voxels inner = new();
        bool hollow = obj.ID > 1e-3f;
        bool filleted = obj.Fr > 1e-3f;

        if (hollow) {
            for (int i=1; i<numel(obj.points); ++i) {
                Vec3 a = obj.points[i - 1];
                Vec3 b = obj.points[i];
                inner.BoolAdd(new Pill(a, b, 0.5f*obj.ID));
            }
        }
        if (!hollow || !filleted) {
            for (int i=1; i<numel(obj.points); ++i) {
                Vec3 a = obj.points[i - 1];
                Vec3 b = obj.points[i];
                vox.BoolAdd(new Pill(a, b, 0.5f*obj.ID + obj.th));
            }
        }
        if (filleted) {
            if (hollow)
                vox = inner.voxDoubleOffset(obj.th + obj.Fr, -obj.Fr);
            else
                vox = vox.voxDoubleOffset(obj.Fr, -obj.Fr);
        }
        if (hollow)
            vox.BoolSubtract(inner);

        Vec3 S0 = obj.points[0];
        Vec3 S1 = obj.points[1];
        Vec3 E0 = obj.points[^1];
        Vec3 E1 = obj.points[^2];
        float remove_r = 1.5f*(0.5f*obj.ID + obj.th);
        vox.BoolSubtract(new Rod(new Frame(S0, S0 - S1), remove_r, remove_r));
        vox.BoolSubtract(new Rod(new Frame(E0, E0 - E1), remove_r, remove_r));
        return vox;
    }
}



public class ImageSignedDist {

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
                sda[x, y] = mask[x, y]
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

    private static bool[,] make_mask_offset(float[,] sda, float off) {
        int W = sda.GetLength(0);
        int H = sda.GetLength(1);
        bool[,] mask = new bool[W, H];
        for (int x=0; x<W; ++x) {
            for (int y=0; y<H; ++y) {
                mask[x, y] = sda[x, y] <= off;
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
    public ImageSignedDist(in string path, int scale=1, float threshold=0.5f,
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

    public void fix_unimelb_lmao() {
        float min_th = 7f * scale;
        int min_x = xextra + 770 * scale;

        int W = sda.GetLength(0);
        int H = sda.GetLength(1);
        float min_r = 0.5f*min_th;

        // Find regions which are thick enough.
        bool[,] thick_mask = make_mask_offset(sda, -min_r);
        float[,] thick_sda = make_sda(thick_mask);

        // Using the thick-enough find the too-thin.
        bool[,] thin_mask = new bool[W, H];
        for (int x=0; x<W; ++x) {
            for (int y=0; y<H; ++y) {
                thin_mask[x, y] = (sda[x, y] < 0f) && (thick_sda[x, y] > 0f);
            }
        }
        float[,] thin_sda = make_sda(thin_mask);

        // Expand the too-thin and union it with current.
        bool[,] new_mask = new bool[W, H];
        for (int x=0; x<W; ++x) {
            for (int y=0; y<H; ++y) {
                new_mask[x, y] = sda[x, y] < 0f;
                if (x > min_x)
                    new_mask[x, y] |= thin_sda[x, y] < min_r;
            }
        }

        // Make the new sda.
        sda = make_sda(new_mask);
    }


    // Returns the signed distance in units of pixels, with an input vector also
    // in units of pixels. The valid bounds are ~(width, -height) to
    // ~(2 width, 2 height).
    public float signed_dist(in Vec2 p /* pixels */) {
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


    public enum WhichLength {
        LONGEST,
        SHORTEST,
        WIDTH,
        HEIGHT,
    }
    // why does csharp not expose these by default man? or at least have a
    // shorthand for it.
    public const WhichLength LONGEST = WhichLength.LONGEST;
    public const WhichLength SHORTEST = WhichLength.SHORTEST;
    public const WhichLength WIDTH = WhichLength.WIDTH;
    public const WhichLength HEIGHT = WhichLength.HEIGHT;


    private void get_scale_size(float length, WhichLength which, out float scale,
            out Vec2 size) {
        scale = length;
        size = length * ONE2;
        switch (which) {
            case LONGEST:
                scale /= max(width, height);
                if (aspect_x_on_y > 1f)
                    size.Y /= aspect_x_on_y;
                else
                    size.X *= aspect_x_on_y;
                break;
            case SHORTEST:
                scale /= min(width, height);
                if (aspect_x_on_y < 1f)
                    size.Y /= aspect_x_on_y;
                else
                    size.X *= aspect_x_on_y;
                break;
            case WIDTH:
                scale /= width;
                size.Y /= aspect_x_on_y;
                break;
            case HEIGHT:
                scale /= height;
                size.X *= aspect_x_on_y;
                break;
            default:
                throw new Exception();
        }
    }

    public SDFfunction sdf_on_plane(out BBox3 bbox, Frame centre, float Lz,
            float length, WhichLength which=LONGEST,
            bool i_understand_sampling_oob_is_illegal_and_i_wont_do_it=false) {
        assert(Lz > 0f);
        assert(length > 0f);
        if (!i_understand_sampling_oob_is_illegal_and_i_wont_do_it) {
            centre.get_rotation(out Vec3 rot_about, out float rot_by);
            assert(nearzero(rot_by),
                    "the raw sdf will not work with anything that rotates at "
                  + "all. you will need to use a transformer");
        }

        get_scale_size(length, which, out float scale, out Vec2 size);

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
            float R, float Lr, float length, WhichLength which=LONGEST,
            bool i_understand_sampling_oob_is_illegal_and_i_wont_do_it=false) {
        assert(R > 0f);
        assert(Lr > 0f);
        assert(length > 0f);

        Frame orient = vertical ? new Frame()
                                : new Frame(ZERO3, uX3, uY3);

        if (!i_understand_sampling_oob_is_illegal_and_i_wont_do_it) {
            centre.get_rotation(out Vec3 rot_about, out float rot_by);
            assert(nearzero(rot_by) || nearvert(orient / rot_about),
                    "the raw sdf will not work with anything that rotates away "
                  + "from axial. you will need to use a transformer");
        }

        get_scale_size(length, which, out float scale, out Vec2 size);

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


    public Voxels voxels_on_plane(in Frame centre, float Lz, float length,
            WhichLength which=LONGEST) {
        centre.get_rotation(out Vec3 rot_about, out float rot_by);
        if (!nearzero(rot_by)) {
            // darn its got rotation we need a transformer.
            SDFfunction sdf = sdf_on_plane(
                out BBox3 bbox,
                new Frame(),
                Lz,
                length,
                which
            );
            Voxels vox = new SDFfilled(sdf).voxels(bbox);
            Transformer transformer = new Transformer().to_global(centre);
            return transformer.voxels(vox);
        } else {
            // okie nws just send to sdf.
            SDFfunction sdf = sdf_on_plane(
                out BBox3 bbox,
                centre,
                Lz,
                length,
                which
            );
            return new SDFfilled(sdf).voxels(bbox);
        }
    }

    public Voxels voxels_on_cyl(bool vertical, in Frame centre, float R,
            float Lr, float length, WhichLength which=LONGEST) {
        Frame orient = vertical ? new Frame()
                                : new Frame(ZERO3, uX3, uY3);
        centre.get_rotation(out Vec3 rot_about, out float rot_by);

        // same deal except can take inplane rotations.
        if (!nearzero(rot_by) && !nearvert(orient / rot_about)) {
            SDFfunction sdf = sdf_on_cyl(
                vertical,
                out BBox3 bbox,
                new Frame(),
                R,
                Lr,
                length,
                which
            );
            Voxels vox = new SDFfilled(sdf).voxels(bbox);
            Transformer transformer = new Transformer().to_global(centre);
            return transformer.voxels(vox);
        } else {
            SDFfunction sdf = sdf_on_cyl(
                vertical,
                out BBox3 bbox,
                centre,
                R,
                Lr,
                length,
                which
            );
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
