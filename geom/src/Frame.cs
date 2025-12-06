using static Br;
using Vec3 = System.Numerics.Vector3;

public class Frame {
    public Vec3 pos { get; }
    public Vec3 X { get; }
    public Vec3 Y { get; }
    public Vec3 Z { get; }

    public Frame() {
        this.pos = ZERO3;
        this.X = uX3;
        this.Y = uY3;
        this.Z = uZ3;
    }
    public Frame(Vec3 pos) {
        this.pos = pos;
        this.X = uX3;
        this.Y = uY3;
        this.Z = uZ3;
    }
    public Frame(Vec3 new_pos, Frame f) {
        this.pos = new_pos;
        this.X = f.X;
        this.Y = f.Y;
        this.Z = f.Z;
    }
    public Frame(Vec3 pos, Vec3 Z)
        : this(pos, arbitrary_perpendicular(Z), Z) {}
    public Frame(Vec3 pos, Vec3 X, Vec3 Z) {
        assert(mag(X) > 1e-5f, "gotta be non-zero");
        assert(mag(Z) > 1e-5f, "gotta be non-zero");
        X = normalise(X);
        Z = normalise(Z);
        assert(dot(X, Z) < 1e-5f, "gotta be right angle");
        this.pos = pos;
        this.X = X;
        this.Y = cross(Z, X);
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


    protected static Vec3 arbitrary_perpendicular(Vec3 Z) {
        assert(mag(Z) > 1e-5f, "gotta be non-zero");
        Z = normalise(Z);
        Vec3 O = uY3; // dflt to getting unit X as perp to z.
        if (abs(dot(Z, O)) > 0.99f) // too close to parallel.
            O = uX3;
        return cross(Z, O);
    }
}
