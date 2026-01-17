
using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;
using Colour = PicoGK.ColorFloat;

namespace br {

public static partial class Br {

    /* assertions. */
    public class AssertionFailed : Exception {
        public AssertionFailed(string message) : base(message) {}
    }
    public static void assert(bool expression, string msg="<no extra info>") {
        if (!expression)
            throw new AssertionFailed(msg);
    }
    public static void assert_idx(int idx, int count,  bool negativeok=false) {
        if (negativeok && idx < 0) {
            assert(within(idx + count, 0, count - 1),
                    $"count: {count}, idx: {idx}");
            return;
        }
        assert(within(idx, 0, count - 1), $"count: {count}, idx: {idx}");
    }

    /* picogk aliases. */
    public static float VOXEL_SIZE => PicoGK.Library.fVoxelSizeMM;
    public static PicoGK.Viewer PICOGK_VIEWER => PicoGK.Library.oViewer();
    public static void print() => PicoGK.Library.Log("");
    public static void print(in string msg) => PicoGK.Library.Log(msg);
    public static void print(in object? obj)
        => PicoGK.Library.Log(obj?.ToString() ?? "<null>");

    public static Colour COLOUR_BLANK  => new("000000", 0f);
    public static Colour COLOUR_BLACK  => new("000000");
    public static Colour COLOUR_GREY   => new("404040");
    public static Colour COLOUR_WHITE  => new("FFFFFF");
    public static Colour COLOUR_RED    => new("FF0000");
    public static Colour COLOUR_GREEN  => new("00FF00");
    public static Colour COLOUR_BLUE   => new("0000FF");
    public static Colour COLOUR_CYAN   => new("00FFFF");
    public static Colour COLOUR_PINK   => new("FF00FF");
    public static Colour COLOUR_YELLOW => new("FFFF00");

    /* paths. */
    public static string PATH_ROOT
        => Directory.GetParent(AppContext.BaseDirectory)
            !.Parent
            !.Parent
            !.Parent
            !.FullName;
    public static string fromroot(string rel) => Path.Combine(PATH_ROOT, rel);

    /* element count. */
    public static int numel<T>(T[] x) => x.Length;
    public static int numel<T>(IReadOnlyCollection<T> x) => x.Count;

    /* swap me */
    public static void swap<T>(ref T a, ref T b) {
        (b, a) = (a, b);
        // oh damn i didnt know u could do that.
    }

    /* bit tricks. */
    public static bool isset(int x, int mask) => (x & mask) == mask;
    public static bool isclr(int x, int mask) => (x & mask) == 0;
    public static bool ispow2(int x) => (x > 0) && ((x & (x - 1)) == 0);
    public static int popcnt(int x)
        => System.Numerics.BitOperations.PopCount((uint)x);
    public static int lobits(int n) => (1 << n) - 1;
    public static int nthbit(int n) => 1 << n;
    public static bool popbits(ref int x, int mask) {
        bool set = isset(x, mask);
        x &= ~mask;
        return set;
    }

    /* vec->arr and back. */
    public static float[] toarr(float v) => [v];
    public static float[] toarr(Vec2 v) => [v.X, v.Y];
    public static float[] toarr(Vec3 v) => [v.X, v.Y, v.Z];
    public static float tovec1(float[] v) { // kinda.
        assert(numel(v) == 1);
        return v[0];
    }
    public static Vec2 tovec2(float[] v) {
        assert(numel(v) == 2);
        return new(v[0], v[1]);
    }
    public static Vec3 tovec3(float[] v) {
        assert(numel(v) == 3);
        return new(v[0], v[1], v[2]);
    }

    /* inf/nan. */
    public const float INF = float.PositiveInfinity;
    public const float NAN = float.NaN;

    public static bool isinf(float a) => float.IsInfinity(a);
    public static bool isinf(Vec2 a)  => isinf(a.X) || isinf(a.Y);
    public static bool isinf(Vec3 a)
        => isinf(a.X) || isinf(a.Y) || isinf(a.Z);
    public static bool noninf(float a) => !isinf(a);
    public static bool noninf(Vec2 a)  => !isinf(a);
    public static bool noninf(Vec3 a)  => !isinf(a);

    public static bool isnan(float a) => float.IsNaN(a);
    public static bool isnan(Vec2 a)  => isnan(a.X) || isnan(a.Y);
    public static bool isnan(Vec3 a)
        => isnan(a.X) || isnan(a.Y) || isnan(a.Z);
    public static bool nonnan(float a) => !isnan(a);
    public static bool nonnan(Vec2 a)  => !isnan(a);
    public static bool nonnan(Vec3 a)  => !isnan(a);

    public static float ifnan(float a, float dflt) => isnan(a) ? dflt : a;
    public static Vec2 ifnan(Vec2 a, Vec2 dflt)    => isnan(a) ? dflt : a;
    public static Vec3 ifnan(Vec3 a, Vec3 dflt)    => isnan(a) ? dflt : a;
    public static float ifnanelem(float a, float dflt)
        => ifnan(a, dflt);
    public static Vec2 ifnanelem(Vec2 a, Vec2 dflt)
        => new(ifnan(a.X, dflt.X), ifnan(a.Y, dflt.Y));
    public static Vec3 ifnanelem(Vec3 a, Vec3 dflt)
        => new(ifnan(a.X, dflt.X), ifnan(a.Y, dflt.Y), ifnan(a.Z, dflt.Z));

    public static bool isgood(float a) => !isnan(a) && !isinf(a);
    public static bool isgood(Vec2 a)  => !isnan(a) && !isinf(a);
    public static bool isgood(Vec3 a)  => !isnan(a) && !isinf(a);


    /* MATHEMATICS */

    public const float SQRTH = 0.70710677f; // sqrt(1/2)
    public const float SQRT2 = 1.4142135f;
    public const float SQRT3 = 1.7320508f;
    public const float SQRT4 = 2.0000000f;

    public const float PI = MathF.PI;
    public const float TWOPI = 2f*PI;
    public const float PI_2 = PI/2f;
    public const float PI_4 = PI/4f;

    public const float DEG10 = 0.17453292f;
    public const float DEG15 = 0.2617994f;
    public const float DEG20 = 0.34906584f;
    public const float DEG30 = 0.5235988f;
    public const float DEG45 = PI_4;
    public const float DEG60 = 1.0471976f;
    public const float DEG90 = PI_2;
    public const float DEG180 = PI;

    public static Vec2 ZERO2 => Vec2.Zero;
    public static Vec3 ZERO3 => Vec3.Zero;
    public static Vec2 ONE2 => Vec2.One;
    public static Vec3 ONE3 => Vec3.One;
    public static Vec2 NAN2 => Vec2.NaN;
    public static Vec3 NAN3 => Vec3.NaN;
    public static Vec2 INF2 => Vec2.PositiveInfinity;
    public static Vec3 INF3 => Vec3.PositiveInfinity;

    public static Vec2 uX2 => Vec2.UnitX;
    public static Vec2 uY2 => Vec2.UnitY;
    public static Vec2 uXY2 => uX2 + uY2; // lowkey ONE2

    public static Vec3 uX3 => Vec3.UnitX;
    public static Vec3 uY3 => Vec3.UnitY;
    public static Vec3 uZ3 => Vec3.UnitZ;
    public static Vec3 uXY3 => uX3 + uY3;
    public static Vec3 uXZ3 => uX3 + uZ3;
    public static Vec3 uYZ3 => uY3 + uZ3;
    public static Vec3 uXYZ3 => uX3 + uY3 + uZ3; // lowkey ONE3

    public static int abs(int a) => (a < 0) ? -a : a;
    public static int min(int a, int b) => (b < a) ? b : a;
    public static int max(int a, int b) => (b > a) ? b : a;
    public static int clamp(int a, int lo, int hi)
        => (a > hi) ? hi : (a < lo) ? lo : a;
    public static bool within(int a, int lo, int hi) => (lo <= a) && (a <= hi);

    public static float abs(float a) => (a < 0f) ? -a : a;
    public static float min(float a, float b) => ((b < a) || isnan(a)) ? b : a;
    public static float max(float a, float b) => ((b > a) || isnan(a)) ? b : a;
    public static float clamp(float a, float lo, float hi)
        => (a > hi) ? hi : (a < lo) ? lo : a;
    public static bool within(float a, float lo, float hi)
        => (lo <= a) && (a <= hi);

    public static Vec2 abs(Vec2 a) => new(abs(a.X), abs(a.Y));
    public static Vec2 min(Vec2 a, Vec2 b) => new(min(a.X, b.X), min(a.Y, b.Y));
    public static Vec2 max(Vec2 a, Vec2 b) => new(max(a.X, b.X), max(a.Y, b.Y));

    public static Vec3 abs(Vec3 a) => new(abs(a.X), abs(a.Y), abs(a.Z));
    public static Vec3 min(Vec3 a, Vec3 b)
        => new(min(a.X, b.X), min(a.Y, b.Y), min(a.Z, b.Z));
    public static Vec3 max(Vec3 a, Vec3 b)
        => new(max(a.X, b.X), max(a.Y, b.Y), max(a.Z, b.Z));

    public static float minelem(Vec2 a) => min(a.X, a.Y);
    public static float maxelem(Vec2 a) => max(a.X, a.Y);
    public static float minelem(Vec3 a) => min(a.X, a.Y, a.Z);
    public static float maxelem(Vec3 a) => max(a.X, a.Y, a.Z);

    public static float min(float a, params float[] bs) {
        float m = a;
        foreach (float b in bs)
            m = min(m, b);
        return m;
    }
    public static Vec2 min(Vec2 a, params Vec2[] bs) {
        Vec2 m = a;
        foreach (Vec2 b in bs)
            m = min(m, b);
        return m;
    }
    public static Vec3 min(Vec3 a, params Vec3[] bs) {
        Vec3 m = a;
        foreach (Vec3 b in bs)
            m = min(m, b);
        return m;
    }

    public static float max(float a, params float[] bs) {
        float m = a;
        foreach (float b in bs)
            m = max(m, b);
        return m;
    }
    public static Vec2 max(Vec2 a, params Vec2[] bs) {
        Vec2 m = a;
        foreach (Vec2 b in bs)
            m = max(m, b);
        return m;
    }
    public static Vec3 max(Vec3 a, params Vec3[] bs) {
        Vec3 m = a;
        foreach (Vec3 b in bs)
            m = max(m, b);
        return m;
    }

    public static int sum(params int[] vs) {
        int m = 0;
        foreach (int v in vs)
            m += v;
        return m;
    }
    public static int prod(params int[] vs) {
        int m = 1;
        foreach (int v in vs)
            m *= v;
        return m;
    }

    public static float sum(params float[] vs) {
        float m = 0f;
        foreach (float v in vs)
            m += ifnan(v, 0f);
        return m;
    }
    public static float prod(params float[] vs) {
        float m = 1f;
        foreach (float v in vs)
            m *= ifnan(v, 1f);
        return m;
    }
    public static float ave(params float[] vs) {
        assert(numel(vs) > 0);
        return sum(vs) / numel(vs);
    }

    public static int[] cumsum(params int[] vs) {
        int[] m = new int[numel(vs)];
        if (numel(vs) > 0)
            m[0] = vs[0];
        for (int i=1; i<numel(vs); ++i)
            m[i] = m[i - 1] + vs[i];
        return m;
    }
    public static int[] cumprod(params int[] vs) {
        int[] m = new int[numel(vs)];
        if (numel(vs) > 0)
            m[0] = vs[0];
        for (int i=1; i<numel(vs); ++i)
            m[i] = m[i - 1] * vs[i];
        return m;
    }


    public static float round(float a) => float.Round(a);
    public static float floor(float a) => float.Floor(a);
    public static float ceil(float a)  => float.Ceiling(a);
    public static int iround(float a) => (int)round(a);
    public static int ifloor(float a) => (int)floor(a);
    public static int iceil(float a)  => (int)ceil(a);

    public static Vec2 round(Vec2 a) => Vec2.Round(a);
    public static Vec2 floor(Vec2 a) => new(floor(a.X), floor(a.Y));
    public static Vec2 ceil(Vec2 a)  => new(ceil(a.X), ceil(a.Y));

    public static Vec3 round(Vec3 a) => Vec3.Round(a);
    public static Vec3 floor(Vec3 a) => new(floor(a.X), floor(a.Y), floor(a.Z));
    public static Vec3 ceil(Vec3 a)  => new(ceil(a.X), ceil(a.Y), ceil(a.Z));

    public static bool nearto(float a, float b, float rtol=5e-5f,
            float atol=1e-6f) {
        if (a == b) // for infs.
            return true;
        if (isnan(a) || isnan(b)) // for nans.
            return false;
        return mag(a - b) <= (atol + rtol*mag(b));
    }
    public static bool nearto(Vec2 a, Vec2 b, float rtol=5e-5f,
            float atol=1e-6f) {
        if (a == b) // for infs.
            return true;
        if (isnan(a) || isnan(b)) // for nans.
            return false;
        return mag(a - b) <= (atol + rtol*mag(b));
    }
    public static bool nearto(Vec3 a, Vec3 b, float rtol=5e-5f,
            float atol=1e-6f) {
        if (a == b) // for infs.
            return true;
        if (isnan(a) || isnan(b)) // for nans.
            return false;
        return mag(a - b) <= (atol + rtol*mag(b));
    }

    public static float pow(float a, float b) => MathF.Pow(a, b);
    public static float exp(float a) => MathF.Exp(a);
    public static float log(float a) => MathF.Log(a);
    public static float log(float a, float b) => MathF.Log(a, b);
    public static float log2(float a) => MathF.Log2(a);
    public static float log10(float a) => MathF.Log10(a);

    public static float squared(float a) => a*a;
    public static float cubed(float a) => a*a*a;
    public static float sqrt(float a) => MathF.Sqrt(a);
    public static float cbrt(float a) => MathF.Cbrt(a);

    public static float torad(float deg) => deg * (PI / 180f);
    public static float todeg(float rad) => rad * (180f / PI);

    public static float wraprad(float rad, bool unsigned=false) {
        rad += unsigned ? 0f : PI;
        // shitass c sharp non-positive modulo.
        rad = (rad%TWOPI + TWOPI)%TWOPI;
        rad -= unsigned ? 0f : PI;
        return rad;
    }
    public static float wrapdeg(float deg, bool unsigned=false) {
        deg += unsigned ? 0f : 180f;
        deg = (deg%360f + 360f)%360f;
        deg -= unsigned ? 0f : 180f;
        return deg;
    }

    public static float sin(float a) => MathF.Sin(a);
    public static float cos(float a) => MathF.Cos(a);
    public static float tan(float a) => MathF.Tan(a);

    public static float asin(float a) => MathF.Asin(a);
    public static float acos(float a) => MathF.Acos(a);
    public static float atan(float a) => MathF.Atan(a);
    public static float atan2(float y, float x, float ifzero=0f)
        => nearzero(hypot(x, y)) ? ifzero : MathF.Atan2(y, x);

    public static float hypot(float x, float y) => sqrt(x*x + y*y);
    public static float hypot(float x, float y, float z)
        => sqrt(x*x + y*y + z*z);
    public static float nonhypot(float hypot, float other) // an instant classic.
        => sqrt(hypot*hypot - other*other);
    public static float nonhypot(float hypot, float other0, float other1)
        => sqrt(hypot*hypot - other0*other0 - other1*other1);

    public static bool nearzero(float a) => nearto(mag(a), 0f);
    public static bool nearzero(Vec2 a)  => nearto(mag(a), 0f);
    public static bool nearzero(Vec3 a)  => nearto(mag(a), 0f);

    public static bool nearunit(float a) => nearto(mag(a), 1f);
    public static bool nearunit(Vec2 a)  => nearto(mag(a), 1f);
    public static bool nearunit(Vec3 a)  => nearto(mag(a), 1f);

    public static bool nearvert(Vec3 a) {
        assert(!nearzero(a));
        float phi = argphi(a);
        return nearto(phi, 0f) || nearto(phi, PI);
    }
    public static bool nearhoriz(Vec3 a) {
        assert(!nearzero(a));
        float phi = argphi(a);
        return nearto(phi, PI_2);
    }
    public static bool nearpara(Vec2 a, Vec2 b) => nearpara(rejxy(a), rejxy(b));
    public static bool nearpara(Vec3 a, Vec3 b) {
        assert(!nearzero(a));
        assert(!nearzero(b));
        float beta = argbeta(a, b);
        return nearto(beta, 0f) || nearto(beta, PI);
    }
    public static bool nearperp(Vec2 a, Vec2 b) => nearperp(rejxy(a), rejxy(b));
    public static bool nearperp(Vec3 a, Vec3 b) {
        assert(!nearzero(a));
        assert(!nearzero(b));
        float beta = argbeta(a, b);
        return nearto(beta, PI_2);
    }

    public static float lerp(float a, float b, float t) => a + t*(b - a);
    public static Vec2 lerp(Vec2 a, Vec2 b, float t)    => a + t*(b - a);
    public static Vec3 lerp(Vec3 a, Vec3 b, float t)    => a + t*(b - a);
    public static float lerp(float a, float b, int i, int N)
        => a + i*(b - a)/(N - 1);

    public static Colour lerp(Colour a, Colour b, float t)
        => new(
            lerp(a.R, b.R, t),
            lerp(a.G, b.G, t),
            lerp(a.B, b.B, t),
            lerp(a.A, b.A, t)
        );

    public static float invlerp(float a, float b, float x) => (x - a) / (b - a);

    public static float mag(float a) => abs(a); // same difference type shi.
    public static float mag(Vec2 a) => a.Length();
    public static float mag(Vec3 a) => a.Length();

    public static float mag2(float a) => a*a; // this ones kinda weird.
    public static float mag2(Vec2 a) => a.LengthSquared();
    public static float mag2(Vec3 a) => a.LengthSquared();

    public static float magxy(Vec3 a) => hypot(a.Y, a.X);
    public static float magzx(Vec3 a) => hypot(a.X, a.Z);
    public static float magyz(Vec3 a) => hypot(a.Z, a.Y);

    public static float arg(Vec2 a, float ifzero=0f)
        => nearzero(a) ? ifzero : atan2(a.Y, a.X);

    public static float argxy(Vec3 a, float ifzero=0f) // angle from +x.
        => (nearzero(a) || nearvert(a))
         ? ifzero
         : arg(projxy(a), ifzero);
    public static float argzx(Vec3 a, float ifzero=0f) // angle from +z.
        => (nearzero(a) || nearvert(new(a.Z, a.X, a.Y)))
         ? ifzero
         : arg(projzx(a), ifzero);
    public static float argyz(Vec3 a, float ifzero=0f) // angle from +y.
        => (nearzero(a) || nearvert(new(a.Y, a.Z, a.X)))
         ? ifzero
         : arg(projyz(a), ifzero);

    public static float argphi(Vec3 a, float ifzero=PI_2)
        => nearzero(a) ? ifzero : acos(a.Z/mag(a));
    public static float argphix(Vec3 a, float ifzero=PI_2)
        => nearzero(a) ? ifzero : acos(a.X/mag(a));
    public static float argphiy(Vec3 a, float ifzero=PI_2)
        => nearzero(a) ? ifzero : acos(a.Y/mag(a));

    public static float argbeta(Vec2 a, Vec2 b, float ifzero=PI_4)
        => argbeta(rejxy(a), rejxy(b), ifzero);
    public static float argbeta(Vec3 a, Vec3 b, float ifzero=PI_4) {
        if (nearzero(a) || nearzero(b))
            return ifzero;
        return atan2(mag(cross(a, b)), dot(a, b));
    }

    public static Vec2 normalise(Vec2 a) {
        assert(!nearzero(a));
        return Vec2.Normalize(a);
    }
    public static Vec3 normalise(Vec3 a) {
        assert(!nearzero(a));
        return Vec3.Normalize(a);
    }
    public static Vec2 normalise_nonzero(Vec2 a)
        => nearzero(a) ? a : normalise(a);
    public static Vec3 normalise_nonzero(Vec3 a)
        => nearzero(a) ? a : normalise(a);

    public static float dot(Vec2 a, Vec2 b) => Vec2.Dot(a, b);
    public static float dot(Vec3 a, Vec3 b) => Vec3.Dot(a, b);

    public static float cross(Vec2 a, Vec2 b) => a.X*b.Y - a.Y*b.X;
    public static Vec3 cross(Vec3 a, Vec3 b) => Vec3.Cross(a, b);

    public static Vec2 frompol(float r, float theta)
        => new(r*cos(theta), r*sin(theta));
    public static Vec3 fromcyl(float r, float theta, float z)
        => new(r*cos(theta), r*sin(theta), z);
    public static Vec3 fromsph(float r, float theta, float phi)
        => new(r*cos(theta)*sin(phi), r*sin(theta)*sin(phi), r*cos(phi));
    public static Vec3 fromzr(Vec2 zr, float theta)
        => rejxy(frompol(zr.Y, theta), zr.X);

    public static float magpara(Vec3 a, Vec3 b) {
        assert(!nearzero(b));
        return dot(a, b)/mag(b);
    }
    public static Vec3 projpara(Vec3 a, Vec3 b) {
        assert(!nearzero(b));
        return dot(a, b)/mag2(b) * b;
    }

    public static float magperp(Vec3 a, Vec3 b)
        => nonhypot(mag(a), magpara(a, b));
    public static Vec3 projperp(Vec3 a, Vec3 b)
        => a - projpara(a, b);

    public static Vec2 projspan(Vec3 a, Vec3 u, Vec3 v) {
        assert(!nearzero(u));
        assert(!nearzero(v));
        Vec3 n = cross(u, v);
        assert(!nearzero(n));
        float mag2n = mag2(n);
        return new(
            dot(cross(a, v), n) / mag2n,
            dot(cross(u, a), n) / mag2n
        );
    }

    public static Vec2 rotate(Vec2 a, float b)
        => new(a.X*cos(b) - a.Y*sin(b), a.X*sin(b) + a.Y*cos(b));
    public static Vec3 rotate(Vec3 p, Vec3 about, float by) {
        about = normalise(about);
        float cosby = cos(by);
        float sinby = sin(by);
        return p*cosby
             + cross(about, p)*sinby
             + about*dot(about, p)*(1f - cosby);
    }

    public static void rotfromto(Vec3 a, Vec3 b, out Vec3 about, out float by) {
        // We want to go from a->b.
        assert(!nearzero(a));
        assert(!nearzero(b));
        Vec3 A = normalise(a);
        Vec3 B = normalise(b);
        by = argbeta(A, B);
        about = cross(A, B);
        if (nearzero(about)) { // a and b are parallel.
            if (by > PI_2) {
                // opposite, can travel in any direction. we prefer a path which
                // crosses the xy plane exactly half-way and travels +circum.
                float theta = argxy(A);
                about = nearvert(A)
                      ? uY3
                      : cross(A, fromcyl(1f, theta + PI_2, 0f));
            } else {
                // same direction, no rotation.
                about = uZ3;
                by = 0f;
            }
        }
        about = normalise(about);
    }

    public static Vec3 lerpdir(Vec3 A, Vec3 B, float t) {
        assert(nearunit(A));
        assert(nearunit(B));
        rotfromto(A, B, out Vec3 about, out float by);
        return rotate(A, about, by * t);
    }

    public static Vec2 rot90ccw(Vec2 a) => new(-a.Y, a.X);
    public static Vec2 rot90cw(Vec2 a) => new(a.Y, -a.X);

    public static Vec2 projxy(Vec3 a) => new(a.X, a.Y);
    public static Vec2 projzx(Vec3 a) => new(a.Z, a.X);
    public static Vec2 projyz(Vec3 a) => new(a.Y, a.Z);

    public static Vec3 rejxy(Vec2 a, float b=0f) => new(a.X, a.Y, b);
    public static Vec3 rejzx(Vec2 a, float b=0f) => new(a.Y, b, a.X);
    public static Vec3 rejyz(Vec2 a, float b=0f) => new(b, a.X, a.Y);

    public static Vec3 rotxy(Vec3 a, float b) => rotate(a, uZ3, b);
    public static Vec3 rotzx(Vec3 a, float b) => rotate(a, uY3, b);
    public static Vec3 rotyz(Vec3 a, float b) => rotate(a, uX3, b);
}

// lets get some python in here ay. use like:
//  void func(int a, int b, KeywordOnly? _=null, int c=0)
// which "forces" c to be given by keyword.... except they can give null but like
// nah she's right. also allows overloads like:
//  void func(KeywordOnly? _=null, int c=0, int d=0)
//  void func(int a, int b, int c=0, int d=0)
// which allows the following to be unambiguous:
//  func(c: 1);
public class KeywordOnly { private KeywordOnly() {} }

}
