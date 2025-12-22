using static Br;
using Vec3 = System.Numerics.Vector3;

using BBox3 = PicoGK.BBox3;

namespace br {

public class Frame {
    public Vec3 pos { get; }
    public Vec3 X { get; }
    public Vec3 Y { get; }
    public Vec3 Z { get; }

    public Frame()
        : this(ZERO3, uX3, uY3, uZ3) {}
    public Frame(Vec3 pos)
        : this(pos, uX3, uY3, uZ3) {}
    public Frame(Vec3 new_pos, Frame f)
        : this(new_pos, f.X, f.Y, f.Z) {}
    public Frame(Vec3 pos, Vec3 Z)
        : this(pos, arbitrary_perpendicular(Z), Z) {}
    public Frame(Vec3 pos, Vec3 X, Vec3 Z) {
        assert(isgood(pos));
        assert(!closeto(mag(X), 0f), "gotta be non-zero");
        assert(!closeto(mag(Z), 0f), "gotta be non-zero");
        X = normalise(X);
        Z = normalise(Z);
        assert(closeto(dot(X, Z), 0f), "gotta be right angle");
        this.pos = pos;
        this.X = X;
        this.Y = cross(Z, X);
        this.Z = Z;
    }
    protected Frame(Vec3 pos, Vec3 X, Vec3 Y, Vec3 Z) {
        assert(isgood(pos));
        this.pos = pos;
        this.X = X;
        this.Y = Y;
        this.Z = Z;
    }

    public Frame transx(float by, bool relative=true) {
        Vec3 along = relative ? X : uX3;
        return new(pos + by*along, this);
    }
    public Frame transy(float by, bool relative=true) {
        Vec3 along = relative ? Y : uY3;
        return new(pos + by*along, this);
    }
    public Frame transz(float by, bool relative=true) {
        Vec3 along = relative ? Z : uZ3;
        return new(pos + by*along, this);
    }
    public Frame translate(Vec3 by, bool relative=true) {
        if (relative)
            by = to_global_rot(by);
        return new(pos + by, this);
    }

    public Frame rotxy(float by, bool relative=true) {
        if (relative)
            return new(pos, Br.rotate(X, Z, by), Z);
        return new(pos, Br.rotxy(X, by), Br.rotxy(Z, by));
    }
    public Frame rotxz(float by, bool relative=true) {
        if (relative)
            return new(pos, Br.rotate(X, Y, by), Br.rotate(Z, Y, by));
        return new(pos, Br.rotxz(X, by), Br.rotxz(Z, by));
    }
    public Frame rotyz(float by, bool relative=true) {
        if (relative)
            return new(pos, X, Br.rotate(Z, X, by));
        return new(pos, Br.rotyz(X, by), Br.rotyz(Z, by));
    }
    public Frame rotate(Vec3 about, float by, bool relative=true) {
        if (relative)
            about = to_global_rot(about);
        return new(pos, Br.rotate(X, about, by), Br.rotate(Z, about, by));
    }

    public Frame flip() {
        return new(pos, -X, -Y, -Z);
    }

    public Vec3 to_global(Vec3 p) {
        return pos + p.X*X + p.Y*Y + p.Z*Z;
    }
    public Vec3 from_global(Vec3 p) {
        p -= pos;
        return new(dot(p, X), dot(p, Y), dot(p, Z));
    }

    public Vec3 to_global_rot(Vec3 p) {
        return p.X*X + p.Y*Y + p.Z*Z;
    }
    public Vec3 from_global_rot(Vec3 p) {
        return new(dot(p, X), dot(p, Y), dot(p, Z));
    }

    public BBox3 to_global(BBox3 bbox) {
        Vec3 v000 = bbox.vecMin;
        Vec3 v111 = bbox.vecMax;
        Vec3 v001 = new(v000.X, v000.Y, v111.Z);
        Vec3 v010 = new(v000.X, v111.Y, v000.Z);
        Vec3 v011 = new(v000.X, v111.Y, v111.Z);
        Vec3 v100 = new(v111.X, v000.Y, v000.Z);
        Vec3 v101 = new(v111.X, v000.Y, v111.Z);
        Vec3 v110 = new(v111.X, v111.Y, v000.Z);
        v000 = to_global(v000);
        v001 = to_global(v001);
        v010 = to_global(v010);
        v011 = to_global(v011);
        v100 = to_global(v100);
        v101 = to_global(v101);
        v110 = to_global(v110);
        v111 = to_global(v111);
        Vec3 vmin = min(v000, v001, v010, v011, v100, v101, v110, v111);
        Vec3 vmax = max(v000, v001, v010, v011, v100, v101, v110, v111);
        return new(vmin, vmax);
    }
    public BBox3 from_global(BBox3 bbox) {
        Vec3 v000 = bbox.vecMin;
        Vec3 v111 = bbox.vecMax;
        Vec3 v001 = new(v000.X, v000.Y, v111.Z);
        Vec3 v010 = new(v000.X, v111.Y, v000.Z);
        Vec3 v011 = new(v000.X, v111.Y, v111.Z);
        Vec3 v100 = new(v111.X, v000.Y, v000.Z);
        Vec3 v101 = new(v111.X, v000.Y, v111.Z);
        Vec3 v110 = new(v111.X, v111.Y, v000.Z);
        v000 = from_global(v000);
        v001 = from_global(v001);
        v010 = from_global(v010);
        v011 = from_global(v011);
        v100 = from_global(v100);
        v101 = from_global(v101);
        v110 = from_global(v110);
        v111 = from_global(v111);
        Vec3 vmin = min(v000, v001, v010, v011, v100, v101, v110, v111);
        Vec3 vmax = max(v000, v001, v010, v011, v100, v101, v110, v111);
        return new(vmin, vmax);
    }

    public Vec3 to_other(Frame other, Vec3 p) {
        Vec3 q = to_global(p);
        return other.from_global(q);
    }
    public Vec3 from_other(Frame other, Vec3 p) {
        Vec3 q = other.to_global(p);
        return from_global(q);
    }

    public Vec3 to_other_rot(Frame other, Vec3 p) {
        Vec3 q = to_global_rot(p);
        return other.from_global_rot(q);
    }
    public Vec3 from_other_rot(Frame other, Vec3 p) {
        Vec3 q = other.to_global_rot(p);
        return from_global_rot(q);
    }

    public BBox3 to_other(Frame other, BBox3 p) {
        BBox3 q = to_global(p);
        return other.from_global(q);
    }
    public BBox3 from_other(Frame other, BBox3 p) {
        BBox3 q = other.to_global(p);
        return from_global(q);
    }


    protected static Vec3 arbitrary_perpendicular(Vec3 Z) {
        assert(!closeto(mag(Z), 0f), "gotta be non-zero");
        Z = normalise(Z);
        Vec3 O = uY3; // dflt to getting unit X as perp to z.
        if (abs(dot(Z, O)) > 0.99f) // too close to parallel.
            O = uX3;
        return cross(Z, O);
    }
}

}
