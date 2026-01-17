using static br.Br;

using Vec3 = System.Numerics.Vector3;

using BBox3 = PicoGK.BBox3;

namespace br {

public class Frame {
    public Vec3 pos { get; }
    public Vec3 X { get; }
    public Vec3 Y { get; }
    public Vec3 Z { get; }


    public Frame()
        : this(ZERO3, uX3, uY3, uZ3, null) {}

    public Frame(Vec3 pos)
        : this(pos, uX3, uY3, uZ3, null) {}

    public Frame(Vec3 new_pos, Frame f)
        : this(new_pos, f.X, f.Y, f.Z, null) {}


    // note these constructors are "unassisted", in that it does no
    // normalisation/orthogonalisation. Use `Frame.orthonormal` to do that.

    public Frame(Vec3 pos, Vec3 Z)
        : this(pos, arbitrary_perpendicular(Z), Z) {}

    public Frame(Vec3 pos, Vec3 X, Vec3 Z)
        : this(pos, X, cross(Z, X) /* righthanded */, Z) {}

    public Frame(Vec3 pos, Vec3 X, Vec3 Y, Vec3 Z) {
        assert(isgood(pos), $"pos={pos}");
        assert(isgood(X), $"X={X}");
        assert(isgood(Y), $"Y={Y}");
        assert(isgood(Z), $"Z={Z}");
        assert(nearunit(X), $"X={X}");
        assert(nearunit(Y), $"Y={Y}");
        assert(nearunit(Z), $"Z={Z}");
        assert(nearperp(X, Y), $"X={X}, Y={Y}");
        assert(nearperp(Z, X), $"Z={Z}, X={X}");
        assert(nearperp(Y, Z), $"Y={Y}, Z={Z}");
        assert(dot(cross(Z, X), Y) > 0f, "must be right-handed");
        // orthonormalise just to prevent numerical drift over time.
        Frame o = orthonormal(pos, X, Z);
        this.pos = o.pos;
        this.X = o.X;
        this.Y = o.Y;
        this.Z = o.Z;
        X = o.X;
        Y = o.Y;
        Z = o.Z;
        assert(nearunit(X), $"X={X}");
        assert(nearunit(Y), $"Y={Y}");
        assert(nearunit(Z), $"Z={Z}");
        assert(nearperp(X, Y), $"X={X}, Y={Y}");
        assert(nearperp(Z, X), $"Z={Z}, X={X}");
        assert(nearperp(Y, Z), $"Y={Y}, Z={Z}");
        assert(dot(cross(Z, X), Y) > 0f, "must be right-handed");
    }

    private Frame(Vec3 pos, Vec3 X, Vec3 Y, Vec3 Z, object? nocheck) {
        assert(nearunit(X), $"X={X}");
        assert(nearunit(Y), $"Y={Y}");
        assert(nearunit(Z), $"Z={Z}");
        assert(nearperp(X, Y), $"X={X}, Y={Y}");
        assert(nearperp(Z, X), $"Z={Z}, X={X}");
        assert(nearperp(Y, Z), $"Y={Y}, Z={Z}");
        assert(dot(cross(Z, X), Y) > 0f, "must be right-handed");
        this.pos = pos;
        this.X = X;
        this.Y = Y;
        this.Z = Z;
    }

    // Prioritises (does not rotate) Z.
    public static Frame orthonormal(Vec3 pos, Vec3 X, Vec3 Z) {
        assert(!nearzero(X), $"X={X}");
        assert(!nearzero(Z), $"Z={Z}");
        assert(!nearpara(Z, X), $"X={X}, Z={Z}");
        Z = normalise(Z);
        X = normalise(X - Z * dot(Z, X));
        Vec3 Y = cross(Z, X);
        return new(pos, X, Y, Z, null);
    }
    public Frame orthonormalised()
        => orthonormal(pos, X, Z);


    private Vec3 _about = NAN3;
    private float _by = NAN;
    public void get_rotation(out Vec3 about, out float by) {
        if (nonnan(_by)) {
            assert(nonnan(_about));
            about = _about;
            by = _by;
            return;
        }
        float R00 = X.X;
        float R01 = Y.X;
        float R02 = Z.X;
        float R10 = X.Y;
        float R11 = Y.Y;
        float R12 = Z.Y;
        float R20 = X.Z;
        float R21 = Y.Z;
        float R22 = Z.Z;

        // For a rotation matrix:
        // v = (R21 - R12, R02 - R20, R10 - R01) = 2*sin(by)*about
        // trace = R00 + R11 + R22 = 1 + 2*cos(by)

        Vec3 v = new(R21 - R12, R02 - R20, R10 - R01);
        float trace = R00 + R11 + R22;

        // Calculate the angle of rotation via a stable method.
        float twocosby = trace - 1f;
        float twosinby = mag(v);
        by = atan2(twosinby, twocosby);

        // Find axis of rotation, but gotta check degenerate.
        if (nearzero(v)) {
            if (twocosby > 0f) { // no rotation.
                about = uZ3; // can be anything.
            } else { // complete flip rotation.
                // We find the largest diagonal element to solve for the axis.
                if (R00 > R11 && R00 > R22) {
                    float ax = sqrt(max(0f, (R00 + 1f) * 0.5f));
                    about = new(
                        ax,
                        R01 * 0.5f / ax,
                        R02 * 0.5f / ax
                    );
                } else if (R11 > R22) {
                    float ay = sqrt(max(0f, (R11 + 1f) * 0.5f));
                    about = new(
                        R01 * 0.5f / ay,
                        ay,
                        R12 * 0.5f / ay
                    );
                } else {
                    float az = sqrt(max(0f, (R22 + 1f) * 0.5f));
                    about = new(
                        R02 * 0.5f / az,
                        R12 * 0.5f / az,
                        az
                    );
                }
                about = normalise(about); // for numerical drift.
            }
        } else {
            // Normal case.
            about = normalise(v);
        }

        // Cache result.
        _about = about;
        _by = by;
    }

    public static Frame rotation(Vec3 about, float by)
        => new Frame().rotate(about, by, false);


    public class Cyl {
        public Frame centre { get; }
        public Cyl() : this(new Frame()) {}
        public Cyl(in Frame centre) {
            this.centre = centre;
        }

        public Frame axial(Vec3 pos, bool relative=true) {
            if (!relative)
                pos = centre / pos;
            // default to as-if lies on x.
            float theta = argxy(pos, ifzero: 0f);

            Vec3 Rad = fromcyl(1f, theta, 0f);
            Vec3 Cir = fromcyl(1f, theta + PI_2, 0f);
            Vec3 Axi = uZ3;
            Frame local = new(pos, Rad, Cir, Axi);
            return centre + local;
        }
        public Frame radial(Vec3 pos, bool relative=true) {
            return axial(pos, relative).cycleccw();
        }
        public Frame circum(Vec3 pos, bool relative=true) {
            return axial(pos, relative).cyclecw();
        }
    }

    public class Sph {
        public Frame centre { get; }
        public Sph() : this(new Frame()) {}
        public Sph(in Frame centre) {
            this.centre = centre;
        }

        public Frame normal(Vec3 pos, bool relative=true) {
            if (!relative)
                pos = centre / pos;
            // default to as-if lies on x.
            float theta = argxy(pos, ifzero: 0f);
            float phi = argphi(pos, ifzero: PI_2);

            Vec3 Lon = fromsph(1f, theta, phi + PI_2);
            Vec3 Lat = fromcyl(1f, theta + PI_2, 0f);
            Vec3 Nor = fromsph(1f, theta, phi);
            Frame local = new(pos, Lon, Lat, Nor);
            return centre + local;
        }
        public Frame longit(Vec3 pos, bool relative=true) {
            return normal(pos, relative).cycleccw();
        }
        public Frame latit(Vec3 pos, bool relative=true) {
            return normal(pos, relative).cyclecw();
        }
    }

    // x = +radial, y = +circumferential, z = +axial
    public static Frame cyl_axial(Vec3 pos) => new Cyl().axial(pos);

    // x = +circumferential, y = +axial, z = +radial
    public static Frame cyl_radial(Vec3 pos) => new Cyl().radial(pos);

    // x = +axial, y = +radial, z = +circumferential
    public static Frame cyl_circum(Vec3 pos) => new Cyl().circum(pos);

    // x = +longitudinal, y = +latitudinal, z = +normal
    public static Frame sph_normal(Vec3 pos) => new Sph().normal(pos);

    // x = +latitudinal, y = +normal, z = +longitudinal
    public static Frame sph_longit(Vec3 pos) => new Sph().longit(pos);

    // x = +normal, y = +longitudinal, z = +latitudinal
    public static Frame sph_latit(Vec3 pos) => new Sph().latit(pos);


    public Frame transx(float shift, bool relative=true) {
        Vec3 along = relative ? X : uX3;
        return new(pos + shift*along, this);
    }
    public Frame transy(float shift, bool relative=true) {
        Vec3 along = relative ? Y : uY3;
        return new(pos + shift*along, this);
    }
    public Frame transz(float shift, bool relative=true) {
        Vec3 along = relative ? Z : uZ3;
        return new(pos + shift*along, this);
    }
    public Frame translate(Vec3 shift, bool relative=true) {
        if (relative)
            shift = to_global_dir(shift);
        return new(pos + shift, this);
    }

    public Frame rotxy(float by, bool relative=true) {
        if (relative)
            return new(pos, Br.rotate(X, Z, by), Z);
        return new(pos, Br.rotxy(X, by), Br.rotxy(Z, by));
    }
    public Frame rotzx(float by, bool relative=true) {
        if (relative)
            return new(pos, Br.rotate(X, Y, by), Br.rotate(Z, Y, by));
        return new(pos, Br.rotzx(X, by), Br.rotzx(Z, by));
    }
    public Frame rotyz(float by, bool relative=true) {
        if (relative)
            return new(pos, X, Br.rotate(Z, X, by));
        return new(pos, Br.rotyz(X, by), Br.rotyz(Z, by));
    }
    public Frame rotate(Vec3 about, float by, bool relative=true) {
        if (relative)
            about = to_global_dir(about);
        return new(pos, Br.rotate(X, about, by), Br.rotate(Z, about, by));
    }

    public Frame swing(Vec3 around, Vec3 about, float by, bool relative=true) {
        if (relative) {
            around = to_global_pos(around);
            about = to_global_dir(about);
        }
        Frame f = this;
        f = f.rotate(about, by, false);
        Vec3 shift = around - pos - Br.rotate(around - pos, about, by);
        f = f.translate(shift, false);
        return f;
    }

    public Frame reflect(Vec3 point, Vec3 normal, int flip_axis,
            bool relative=true) {
        if (relative) {
            point = to_global_pos(point);
            normal = to_global_dir(normal);
        }
        assert_idx(flip_axis, 3);
        Vec3 N = normalise(normal);
        // Reflect position.
        Vec3 newpos = pos - 2f * dot(pos - point, N) * N;
        // Reflect axes.
        Vec3 newX = X - 2f * dot(X, N) * N;
        Vec3 newY = Y - 2f * dot(Y, N) * N;
        Vec3 newZ = Z - 2f * dot(Z, N) * N;
        // Convert back to right-handed.
        switch (flip_axis) {
            case 0: newX = -newX; break;
            case 1: newY = -newY; break;
            case 2: newZ = -newZ; break;
        }
        return new(newpos, newX, newY, newZ);
    }
    public Frame reflectxy(Vec3 point, Vec3 normal, bool relative=true)
        => reflect(point, normal, 2, relative);
    public Frame reflectzx(Vec3 point, Vec3 normal, bool relative=true)
        => reflect(point, normal, 1, relative);
    public Frame reflectyz(Vec3 point, Vec3 normal, bool relative=true)
        => reflect(point, normal, 0, relative);

    public Frame flipxy() => new(pos, -X, -Y, Z);
    public Frame flipzx() => new(pos, -X, Y, -Z);
    public Frame flipyz() => new(pos, X, -Y, -Z);

    public Frame swapxy() => new(pos, Y, X, -Z);
    public Frame swapzx() => new(pos, Z, -Y, X);
    public Frame swapyz() => new(pos, -X, Z, Y);

    public Frame cyclecw() => new(pos, Z, X, Y);
    public Frame cycleccw() => new(pos, Y, Z, X);

    public Frame inverse() {
        Vec3 newX = new(X.X, Y.X, Z.X);
        Vec3 newY = new(X.Y, Y.Y, Z.Y);
        Vec3 newZ = new(X.Z, Y.Z, Z.Z);
        Vec3 newpos = new(
            -dot(pos, newX),
            -dot(pos, newY),
            -dot(pos, newZ)
        );
        return new(newpos, newX, newY, newZ);
    }
    public Frame compose(in Frame local) {
        Vec3 newpos = to_global_pos(local.pos);
        Vec3 newX = to_global_dir(local.X);
        Vec3 newY = to_global_dir(local.Y);
        Vec3 newZ = to_global_dir(local.Z);
        return new(newpos, newX, newY, newZ);
    }
    public Frame relativeto(in Frame reference) {
        Vec3 newpos = reference.from_global_pos(pos);
        Vec3 newX = reference.from_global_dir(X);
        Vec3 newY = reference.from_global_dir(Y);
        Vec3 newZ = reference.from_global_dir(Z);
        return new(newpos, newX, newY, newZ);
    }

    public static Frame operator+(in Frame frame, in Frame appended)
        => frame.compose(appended);
    public static Frame operator-(in Frame to, in Frame from)
        => to.relativeto(from);

    public Frame closest_parallel(in Frame other) {
        Vec3[] axes = {
             other.X,
            -other.X,
             other.Y,
            -other.Y,
             other.Z,
            -other.Z
        };
        float best = -INF;
        Frame bestframe = this;
        foreach (Vec3 newX in axes) {
            foreach (Vec3 newZ in axes) {
                if (nearpara(newZ, newX))
                    continue; // must be perpendicular.
                Vec3 newY = cross(newZ, newX);
                float score = dot(newX, X) + dot(newY, Y) + dot(newZ, Z);
                if (score > best) {
                    best = score;
                    bestframe = new(pos, newX, newY, newZ);
                }
            }
        }
        return bestframe;
    }


    public Frame lerp(in Frame other, float t) {
        Vec3 pos = Br.lerp(this.pos, other.pos, t);
        this.get_rotation(out Vec3 this_about, out float this_by);
        other.get_rotation(out Vec3 other_about, out float other_by);
        Vec3 about = Br.lerpdir(this_about, other_about, t);
        float by = Br.lerp(this_by, other_by, t);
        return new Frame(pos).rotate(about, by);
    }


    public Vec3 to_global_pos(Vec3 position) {
        return pos + position.X*X + position.Y*Y + position.Z*Z;
    }
    public Vec3 from_global_pos(Vec3 position) {
        position -= pos;
        return new(dot(position, X), dot(position, Y), dot(position, Z));
    }
    public static Vec3 operator*(in Frame frame, Vec3 position)
        => frame.to_global_pos(position);
    public static Vec3 operator/(in Frame frame, Vec3 position)
        => frame.from_global_pos(position);


    public Vec3 to_global_dir(Vec3 direction) {
        return direction.X*X + direction.Y*Y + direction.Z*Z;
    }
    public Vec3 from_global_dir(Vec3 direction) {
        return new(dot(direction, X), dot(direction, Y), dot(direction, Z));
    }
    public static Vec3 operator|(in Frame frame, Vec3 direction)
        => frame.to_global_dir(direction);
    public static Vec3 operator^(in Frame frame, Vec3 direction)
        => frame.from_global_dir(direction);
    // here at bruv we only use operators when it is 100% clear what their
    // operation is.


    public BBox3 to_global_bbox(BBox3 bbox) {
        Vec3 v000 = bbox.vecMin;
        Vec3 v111 = bbox.vecMax;
        Vec3 v001 = new(v000.X, v000.Y, v111.Z);
        Vec3 v010 = new(v000.X, v111.Y, v000.Z);
        Vec3 v011 = new(v000.X, v111.Y, v111.Z);
        Vec3 v100 = new(v111.X, v000.Y, v000.Z);
        Vec3 v101 = new(v111.X, v000.Y, v111.Z);
        Vec3 v110 = new(v111.X, v111.Y, v000.Z);
        v000 = to_global_pos(v000);
        v001 = to_global_pos(v001);
        v010 = to_global_pos(v010);
        v011 = to_global_pos(v011);
        v100 = to_global_pos(v100);
        v101 = to_global_pos(v101);
        v110 = to_global_pos(v110);
        v111 = to_global_pos(v111);
        Vec3 vmin = min(v000, v001, v010, v011, v100, v101, v110, v111);
        Vec3 vmax = max(v000, v001, v010, v011, v100, v101, v110, v111);
        return new(vmin, vmax);
    }
    public BBox3 from_global_bbox(BBox3 bbox) {
        Vec3 v000 = bbox.vecMin;
        Vec3 v111 = bbox.vecMax;
        Vec3 v001 = new(v000.X, v000.Y, v111.Z);
        Vec3 v010 = new(v000.X, v111.Y, v000.Z);
        Vec3 v011 = new(v000.X, v111.Y, v111.Z);
        Vec3 v100 = new(v111.X, v000.Y, v000.Z);
        Vec3 v101 = new(v111.X, v000.Y, v111.Z);
        Vec3 v110 = new(v111.X, v111.Y, v000.Z);
        v000 = from_global_pos(v000);
        v001 = from_global_pos(v001);
        v010 = from_global_pos(v010);
        v011 = from_global_pos(v011);
        v100 = from_global_pos(v100);
        v101 = from_global_pos(v101);
        v110 = from_global_pos(v110);
        v111 = from_global_pos(v111);
        Vec3 vmin = min(v000, v001, v010, v011, v100, v101, v110, v111);
        Vec3 vmax = max(v000, v001, v010, v011, v100, v101, v110, v111);
        return new(vmin, vmax);
    }


    public void bbox_include_circle(ref BBox3 bbox, float r /* in xy plane */,
            float z=0f) {
        // Compute extents along each global axis.
        float ex = r * hypot(dot(uX3, X), dot(uX3, Y));
        float ey = r * hypot(dot(uY3, X), dot(uY3, Y));
        float ez = r * nonhypot(1f, dot(uZ3, Z));
        Vec3 e = new(ex, ey, ez);

        // Expand bounding box.
        bbox.Include(pos + z*Z - e);
        bbox.Include(pos + z*Z + e);
    }


    public Vec3 to_other(Frame other, Vec3 p) {
        Vec3 q = to_global_pos(p);
        return other.from_global_pos(q);
    }
    public Vec3 from_other(Frame other, Vec3 p) {
        Vec3 q = other.to_global_pos(p);
        return from_global_pos(q);
    }

    public Vec3 to_other_dir(Frame other, Vec3 p) {
        Vec3 q = to_global_dir(p);
        return other.from_global_dir(q);
    }
    public Vec3 from_other_dir(Frame other, Vec3 p) {
        Vec3 q = other.to_global_dir(p);
        return from_global_dir(q);
    }


    protected static Vec3 arbitrary_perpendicular(Vec3 Z) {
        assert(!nearzero(Z), "gotta be non-zero");
        Z = normalise(Z);
        Vec3 O = uY3; // dflt to getting unit X as perp to z.
        if (abs(dot(Z, O)) > 0.99f) // too close to parallel.
            O = uX3;
        return cross(Z, O);
    }

    public override string ToString() {
        string tostr(Vec3 a) => $"({a.X}, {a.Y}, {a.Z})";
        return $"<frame @{tostr(pos)}, X={tostr(X)}, Y={tostr(Y)}, Z={tostr(Z)}";
    }
}


public interface Frames {
    int count { get; }
    Frame at(int i);
}
public static partial class Br {
    public static int numel(in Frames frames) => frames.count;
}


public class FramesSequence : Frames {
    public Slice<Frame> frames { get; }
    public FramesSequence()
        : this(new Slice<Frame>()) {}
    public FramesSequence(in Slice<Frame> frames) {
        this.frames = frames;
    }

    public int count => numel(frames);
    public Frame at(int i) => frames[i];
}


public class FramesLerp : Frames {
    public int N { get; }
    /* NOTE: may not be direct, though always is *initially* */
    public Frame start => at(0);
    public Frame end => at(N - 1);
    protected float _tlo;
    protected float _thi;
    protected Frame _start;
    protected Frame _end;
    public FramesLerp(int N, in Frame frame, in Vec3 start, in Vec3 end)
        : this(N, frame.translate(start), frame.translate(end)) {}
    public FramesLerp(int N, in Frame start, in Frame end)
        : this(N, start, end, 0f, 1f) {}
    protected FramesLerp(int N, in Frame start, in Frame end, float tlo,
            float thi) {
        assert(N >= 2);
        this.N = N;
        this._start = start;
        this._end = end;
        this._tlo = tlo;
        this._thi = thi;
    }

    public FramesLerp resampled(int newN) {
        return new(newN, _start, _end, _tlo, _thi);
    }
    public FramesLerp rebounded(int startsteps, int endsteps) {
        int newN = N - startsteps + endsteps;
        float newtlo = _tlo + startsteps / (float)(N - 1);
        float newthi = _thi - (startsteps - endsteps) / (float)(N - 1);
        return new(newN, _start, _end, newtlo, newthi);
    }
    public FramesLerp excluding_start() => resampled(N + 1).skipping_start();
    public FramesLerp excluding_end() => resampled(N + 1).skipping_end();
    public FramesLerp skipping_start() => skipping(start: 1);
    public FramesLerp skipping_end() => skipping(end: 1);
    public FramesLerp skipping(int start=0, int end=0)
        => rebounded(+start, -end);
    public FramesLerp extended(int start=0, int end=0)
        => rebounded(-start, +end);

    public int count => N;
    public Frame at(int i) => _start.lerp(_end, lerp(_tlo, _thi, i, N));
}


public class FramesSpin : Frames {
    public int N { get; }
    public Frame centre { get; }
    public Vec3 about { get; }
    public float by { get; }
    public FramesSpin(int N, float by=TWOPI)
        : this(N, new Frame(), uZ3, by) {}
    public FramesSpin(int N, in Frame centre, float by=TWOPI)
        : this(N, centre, uZ3, by) {}
    public FramesSpin(int N, in Frame centre, in Vec3 about, float by=TWOPI) {
        assert(N >= 2);
        assert(isgood(by));
        this.N = N;
        this.centre = centre;
        this.about = about;
        this.by = by;
    }

    public FramesSpin resampled(int newN) {
        return new(newN, centre, about, by);
    }
    public FramesSpin rebounded(int startsteps, int endsteps) {
        int newN = N - startsteps + endsteps;
        Frame newcentre = centre.rotate(about, startsteps * by / (N - 1));
        float newby = by - (startsteps - endsteps) * by / (N - 1); // haha.
        return new(newN, newcentre, about, newby);
    }
    public FramesSpin excluding_start() => resampled(N + 1).skipping_start();
    public FramesSpin excluding_end() => resampled(N + 1).skipping_end();
    public FramesSpin skipping_start() => skipping(start: 1);
    public FramesSpin skipping_end() => skipping(end: 1);
    public FramesSpin skipping(int start=0, int end=0)
        => rebounded(+start, -end);
    public FramesSpin extended(int start=0, int end=0)
        => rebounded(-start, +end);

    public int count => N;
    public Frame at(int i) => centre.rotate(about, i*by/(N - 1));
}


public class FramesCart : Frames {
    public Frame centre { get; }
    public Slice<Vec3> points { get; }
    public FramesCart(in Slice<Vec3> points)
        : this(new Frame(), points) {}
    public FramesCart(in Frame centre, in Slice<Vec3> points) {
        this.centre = centre;
        this.points = points;
    }

    public int count => numel(points);
    public Frame at(int i) => centre.translate(points[i]);
}


public abstract class FramesCyl : Frames {
    public Frame.Cyl centre { get; }
    public Slice<Vec3> points { get; }
    protected FramesCyl(in Frame.Cyl centre, in Slice<Vec3> points) {
        this.centre = centre;
        this.points = points;
    }

    public int count => numel(points);
    public Frame at(int i) => _frame(points[i]);
    protected abstract Frame _frame(Vec3 pos);
}
public class FramesCylAxial : FramesCyl {
    public FramesCylAxial(in Slice<Vec3> points)
        : base(new Frame.Cyl(), points) {}
    public FramesCylAxial(in Frame.Cyl centre, in Slice<Vec3> points)
        : base(centre, points) {}
    protected override Frame _frame(Vec3 pos) => centre.axial(pos);
}
public class FramesCylRadial : FramesCyl {
    public FramesCylRadial(in Slice<Vec3> points)
        : base(new Frame.Cyl(), points) {}
    public FramesCylRadial(in Frame.Cyl centre, in Slice<Vec3> points)
        : base(centre, points) {}
    protected override Frame _frame(Vec3 pos) => centre.radial(pos);
}
public class FramesCylCircum : FramesCyl {
    public FramesCylCircum(in Slice<Vec3> points)
        : base(new Frame.Cyl(), points) {}
    public FramesCylCircum(in Frame.Cyl centre, in Slice<Vec3> points)
        : base(centre, points) {}
    protected override Frame _frame(Vec3 pos) => centre.circum(pos);
}


public abstract class FramesSph : Frames {
    public Frame.Sph centre { get; }
    public Slice<Vec3> points { get; }
    protected FramesSph(in Frame.Sph centre, in Slice<Vec3> points) {
        this.centre = centre;
        this.points = points;
    }

    public int count => numel(points);
    public Frame at(int i) => _frame(points[i]);
    protected abstract Frame _frame(Vec3 pos);
}
public class FramesSphNormal : FramesSph {
    public FramesSphNormal(in Slice<Vec3> points)
        : base(new Frame.Sph(), points) {}
    public FramesSphNormal(in Frame.Sph centre, in Slice<Vec3> points)
        : base(centre, points) {}
    protected override Frame _frame(Vec3 pos) => centre.normal(pos);
}
public class FramesSphLongit : FramesSph {
    public FramesSphLongit(in Slice<Vec3> points)
        : base(new Frame.Sph(), points) {}
    public FramesSphLongit(in Frame.Sph centre, in Slice<Vec3> points)
        : base(centre, points) {}
    protected override Frame _frame(Vec3 pos) => centre.longit(pos);
}
public class FramesSphLatit : FramesSph {
    public FramesSphLatit(in Slice<Vec3> points)
        : base(new Frame.Sph(), points) {}
    public FramesSphLatit(in Frame.Sph centre, in Slice<Vec3> points)
        : base(centre, points) {}
    protected override Frame _frame(Vec3 pos) => centre.latit(pos);
}



}
