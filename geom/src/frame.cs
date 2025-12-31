using static br.Br;

using Vec3 = System.Numerics.Vector3;

using BBox3 = PicoGK.BBox3;

namespace br {

public class Frame {
    public Vec3 pos { get; }
    public Vec3 X { get; }
    public Vec3 Y { get; }
    public Vec3 Z { get; }

    public void rot_to_local(out Vec3 about, out float by) {
        float x00 = X.X;
        float x01 = X.Y;
        float x02 = X.Z;
        float x10 = Y.X;
        float x11 = Y.Y;
        float x12 = Y.Z;
        float x20 = Z.X;
        float x21 = Z.Y;
        float x22 = Z.Z;

        // Calculate the angle of rotation from the trace.
        // cos(by) = (trace - 1) / 2
        float cosby = (x00 + x11 + x22 - 1f) * 0.5f;
        by = acos(clamp(cosby, -1f, 1f));

        // Explicitly handle no rot.
        if (closeto(by, 0f)) {
            about = uZ3; // can be anything.
            return;
        }
        // Explicitly handle flip rot.
        if (closeto(by, PI)) {
            // We find the largest diagonal element to solve for the axis.
            if (x00 > x11 && x00 > x22) {
                float s = sqrt(x00 - x11 - x22 + 1f) * 0.5f;
                about = new Vec3(s, x01/s/2f, x02/s/2f);
            } else if (x11 > x22) {
                float s = sqrt(x11 - x00 - x22 + 1f) * 0.5f;
                about = new Vec3(x01/s/2f, s, x12/s/2f);
            } else {
                float s = sqrt(x22 - x00 - x11 + 1f) * 0.5f;
                about = new Vec3(x02/s/2f, x12/s/2f, s);
            }
            return;
        }
        // Otherwise normal.
        about = normalise(new Vec3(x12 - x21, x20 - x02, x01 - x10));
    }

    public void rot_to_global(out Vec3 about, out float by) {
        rot_to_local(out about, out by);
        by = -by;
    }


    public Frame()
        : this(ZERO3, uX3, uY3, uZ3) {}

    public Frame(Vec3 pos)
        : this(pos, uX3, uY3, uZ3) {}

    public Frame(Vec3 new_pos, Frame f)
        : this(new_pos, f.X, f.Y, f.Z) {}

    public Frame(Vec3 pos, Vec3 Z)
        : this(pos, arbitrary_perpendicular(Z), Z) {}

    public Frame(Vec3 pos, Vec3 X, Vec3 Z)
        : this(pos, normalise(X), normalise(cross(Z, X)), normalise(Z)) {}

    public class Cyl {
        public Frame centre { get; }
        public Cyl() : this(new Frame()) {}
        public Cyl(in Frame centre) {
            this.centre = centre;
        }

        public Frame axial(Vec3 pos) {
            // default to as-if lies on x.
            float theta = argxy(pos, ifzero: 0f);

            Vec3 Rad = fromcyl(1f, theta, 0f);
            Vec3 Cir = fromcyl(1f, theta + PI_2, 0f);
            Vec3 Axi = uZ3;
            Rad = centre.to_global_rot(Rad);
            Cir = centre.to_global_rot(Cir);
            Axi = centre.to_global_rot(Axi);
            return new(centre * pos, Rad, Cir, Axi);
        }
        public Frame radial(Vec3 pos) {
            return axial(pos).cycleccw();
        }
        public Frame circum(Vec3 pos) {
            return axial(pos).cyclecw();
        }
    }

    public class Sph {
        public Frame centre { get; }
        public Sph() : this(new Frame()) {}
        public Sph(in Frame centre) {
            this.centre = centre;
        }

        public Frame normal(Vec3 pos) {
            // default to as-if lies on x.
            float theta = argxy(pos, ifzero: 0f);
            float phi = argphi(pos, ifzero: PI_2);

            Vec3 Lon = fromsph(1f, theta, phi + PI_2);
            Vec3 Lat = fromcyl(1f, theta + PI_2, 0f);
            Vec3 Nor = fromsph(1f, theta, phi);
            Lon = centre.to_global_rot(Lon);
            Lat = centre.to_global_rot(Lat);
            Nor = centre.to_global_rot(Nor);
            return new(centre * pos, Lon, Lat, Nor);
        }
        public Frame longit(Vec3 pos) {
            return normal(pos).cycleccw();
        }
        public Frame latit(Vec3 pos) {
            return normal(pos).cyclecw();
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
            about = to_global_rot(about);
        return new(pos, Br.rotate(X, about, by), Br.rotate(Z, about, by));
    }

    public Frame flipxy() {
        return new(pos, -X, -Y, Z);
    }
    public Frame flipzx() {
        return new(pos, -X, Y, -Z);
    }
    public Frame flipyz() {
        return new(pos, X, -Y, -Z);
    }

    public Frame swapxy() {
        return new(pos, Y, X, -Z);
    }
    public Frame swapzx() {
        return new(pos, Z, -Y, X);
    }
    public Frame swapyz() {
        return new(pos, -X, Z, Y);
    }

    public Frame cyclecw() {
        return new(pos, Z, X, Y);
    }
    public Frame cycleccw() {
        return new(pos, Y, Z, X);
    }

    public Frame compose(in Frame other) {
        Vec3 pos = to_global(other.pos);
        Vec3 X = to_global_rot(other.X);
        Vec3 Y = to_global_rot(other.Y);
        Vec3 Z = to_global_rot(other.Z);
        return new(pos, X, Y, Z);
    }

    public Frame lerp(in Frame other, float t) {
        Vec3 pos = Br.lerp(this.pos, other.pos, t);
        this.rot_to_local(out Vec3 this_about, out float this_by);
        other.rot_to_local(out Vec3 other_about, out float other_by);
        Vec3 about = Br.lerpdir(this_about, other_about, t);
        float by = Br.lerp(this_by, other_by, t);
        return new Frame(pos).rotate(about, by);
    }


    public Vec3 to_global(Vec3 p) {
        return pos + p.X*X + p.Y*Y + p.Z*Z;
    }
    public Vec3 from_global(Vec3 p) {
        p -= pos;
        return new(dot(p, X), dot(p, Y), dot(p, Z));
    }
    public static Vec3 operator*(in Frame frame, Vec3 p) => frame.to_global(p);
    public static Vec3 operator/(in Frame frame, Vec3 p) => frame.from_global(p);


    public Vec3 to_global_rot(Vec3 p) {
        return p.X*X + p.Y*Y + p.Z*Z;
    }
    public Vec3 from_global_rot(Vec3 p) {
        return new(dot(p, X), dot(p, Y), dot(p, Z));
    }

    public BBox3 to_global_bbox(BBox3 bbox) {
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
    public BBox3 from_global_bbox(BBox3 bbox) {
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


    protected static Vec3 arbitrary_perpendicular(Vec3 Z) {
        assert(!nearzero(Z), "gotta be non-zero");
        Z = normalise(Z);
        Vec3 O = uY3; // dflt to getting unit X as perp to z.
        if (abs(dot(Z, O)) > 0.99f) // too close to parallel.
            O = uX3;
        return cross(Z, O);
    }

    protected Frame(Vec3 pos, Vec3 X, Vec3 Y, Vec3 Z) {
        assert(isgood(pos));
        assert(isgood(X));
        assert(isgood(Y));
        assert(isgood(Z));
        assert(nearunit(X));
        assert(nearunit(Y));
        assert(nearunit(Z));
        assert(nearzero(dot(X, Y)));
        assert(nearzero(dot(X, Z)));
        assert(nearzero(dot(Y, Z)));
        assert(closeto(cross(Z, X), Y)); // rhs
        this.pos = pos;
        this.X = X;
        this.Y = Y;
        this.Z = Z;
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
    public List<Frame> frames { get; }
    public FramesSequence()
        : this(new List<Frame>()) {}
    public FramesSequence(in List<Frame> frames) {
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
    private float _tlo;
    private float _thi;
    private Frame _start;
    private Frame _end;
    public FramesLerp(int N, in Frame frame, in Vec3 start, in Vec3 end)
        : this(N, frame.translate(start), frame.translate(end)) {}
    public FramesLerp(int N, in Frame start, in Frame end)
        : this(N, start, end, 0f, 1f) {}
    private FramesLerp(int N, in Frame start, in Frame end, float tlo,
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
    public List<Vec3> points { get; }
    public FramesCart(in List<Vec3> points)
        : this(new Frame(), points) {}
    public FramesCart(in Frame centre, in List<Vec3> points) {
        this.centre = centre;
        this.points = points;
    }

    public int count => numel(points);
    public Frame at(int i) => centre.translate(points[i]);
}


public abstract class FramesCyl : Frames {
    public Frame.Cyl centre { get; }
    public List<Vec3> points { get; }
    protected FramesCyl(in Frame.Cyl centre, in List<Vec3> points) {
        this.centre = centre;
        this.points = points;
    }

    public int count => numel(points);
    public Frame at(int i) => _frame(points[i]);
    protected abstract Frame _frame(Vec3 pos);
}
public class FramesCylAxial : FramesCyl {
    public FramesCylAxial(in List<Vec3> points)
        : base(new Frame.Cyl(), points) {}
    public FramesCylAxial(in Frame.Cyl centre, in List<Vec3> points)
        : base(centre, points) {}
    protected override Frame _frame(Vec3 pos) => centre.axial(pos);
}
public class FramesCylRadial : FramesCyl {
    public FramesCylRadial(in List<Vec3> points)
        : base(new Frame.Cyl(), points) {}
    public FramesCylRadial(in Frame.Cyl centre, in List<Vec3> points)
        : base(centre, points) {}
    protected override Frame _frame(Vec3 pos) => centre.radial(pos);
}
public class FramesCylCircum : FramesCyl {
    public FramesCylCircum(in List<Vec3> points)
        : base(new Frame.Cyl(), points) {}
    public FramesCylCircum(in Frame.Cyl centre, in List<Vec3> points)
        : base(centre, points) {}
    protected override Frame _frame(Vec3 pos) => centre.circum(pos);
}


public abstract class FramesSph : Frames {
    public Frame.Sph centre { get; }
    public List<Vec3> points { get; }
    protected FramesSph(in Frame.Sph centre, in List<Vec3> points) {
        this.centre = centre;
        this.points = points;
    }

    public int count => numel(points);
    public Frame at(int i) => _frame(points[i]);
    protected abstract Frame _frame(Vec3 pos);
}
public class FramesSphNormal : FramesSph {
    public FramesSphNormal(in List<Vec3> points)
        : base(new Frame.Sph(), points) {}
    public FramesSphNormal(in Frame.Sph centre, in List<Vec3> points)
        : base(centre, points) {}
    protected override Frame _frame(Vec3 pos) => centre.normal(pos);
}
public class FramesSphLongit : FramesSph {
    public FramesSphLongit(in List<Vec3> points)
        : base(new Frame.Sph(), points) {}
    public FramesSphLongit(in Frame.Sph centre, in List<Vec3> points)
        : base(centre, points) {}
    protected override Frame _frame(Vec3 pos) => centre.longit(pos);
}
public class FramesSphLatit : FramesSph {
    public FramesSphLatit(in List<Vec3> points)
        : base(new Frame.Sph(), points) {}
    public FramesSphLatit(in Frame.Sph centre, in List<Vec3> points)
        : base(centre, points) {}
    protected override Frame _frame(Vec3 pos) => centre.latit(pos);
}



}
