
using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;
using Colour = PicoGK.ColorFloat;

namespace br {

public static partial class Br {

    /* assertions. */
    public class AssertionFailed : Exception {
        public AssertionFailed(string message) : base(message) {}
    }
    public static void assert(bool expression, string? extra=null,
            [System.Runtime.CompilerServices.CallerFilePath]
                string file="<unknown file>",
            [System.Runtime.CompilerServices.CallerLineNumber]
                int line=-1,
            [System.Runtime.CompilerServices.CallerMemberName]
                string member="<unknown member>") {
        if (!expression) {
            string msg = $"file: {file}, line: {line}, member: {member}";
            if (extra != null)
                msg += $", extra: {extra}";
            throw new AssertionFailed(msg);
        }
    }
    public static void assert_idx(int idx, int count,
            [System.Runtime.CompilerServices.CallerFilePath]
                string file="<unknown file>",
            [System.Runtime.CompilerServices.CallerLineNumber]
                int line=-1,
            [System.Runtime.CompilerServices.CallerMemberName]
                string member="<unknown member>") {
        assert(within(idx, 0, count - 1), $"count: {count}, idx: {idx}",
                file: file, line: line, member: member);
    }

    /* picogk aliases. */
    public static float VOXEL_SIZE => PicoGK.Library.fVoxelSizeMM;
    public static PicoGK.Viewer PICOGK_VIEWER => PicoGK.Library.oViewer();
    public static void log() => PicoGK.Library.Log("");
    public static void log(in string msg) => PicoGK.Library.Log(msg);

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
    public static int numel<T>(List<T> x) => x.Count;
    public static int numel<T,U>(Dictionary<T,U> x) where T:notnull => x.Count;

    /* swap me */
    public static void swap<T>(ref T a, ref T b) {
        T c = a;
        a = b;
        b = c;
    }

    /* bit tricks. */
    public static bool isset(int x, int mask) => (x & mask) == mask;
    public static bool isclr(int x, int mask) => (x & mask) == 0;

    /* colours. */
    public static Colour COLOUR_BLACK  => new("#000000");
    public static Colour COLOUR_RED    => new("#FF0000");
    public static Colour COLOUR_GREEN  => new("#00FF00");
    public static Colour COLOUR_BLUE   => new("#0000FF");
    public static Colour COLOUR_CYAN   => new("#00FFFF");
    public static Colour COLOUR_PINK   => new("#FF00FF");
    public static Colour COLOUR_YELLOW => new("#FFFF00");
    public static Colour COLOUR_WHITE  => new("#FFFFFF");
    public static Colour COLOUR_BLANK  => new("#000000", 0f);

    /* inf/nan. */
    public const float INF = float.PositiveInfinity;
    public const float NAN = float.NaN;

    public static bool isinf(float a) => float.IsInfinity(a);
    public static bool isinf(Vec2 a) => isinf(a.X) || isinf(a.Y);
    public static bool isinf(Vec3 a)
        => isinf(a.X) || isinf(a.Y) || isinf(a.Z);
    public static bool noninf(float a) => !isinf(a);
    public static bool noninf(Vec2 a) => !isinf(a);
    public static bool noninf(Vec3 a) => !isinf(a);

    public static bool isnan(float a) => float.IsNaN(a);
    public static bool isnan(Vec2 a) => isnan(a.X) || isnan(a.Y);
    public static bool isnan(Vec3 a)
        => isnan(a.X) || isnan(a.Y) || isnan(a.Z);
    public static bool nonnan(float a) => !isnan(a);
    public static bool nonnan(Vec2 a) => !isnan(a);
    public static bool nonnan(Vec3 a) => !isnan(a);

    public static bool isgood(float a) => !isnan(a) && !isinf(a);
    public static bool isgood(Vec2 a) => !isnan(a) && !isinf(a);
    public static bool isgood(Vec3 a) => !isnan(a) && !isinf(a);


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
    public static Vec3 uX3 => Vec3.UnitX;
    public static Vec3 uY3 => Vec3.UnitY;
    public static Vec3 uZ3 => Vec3.UnitZ;

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

    public static bool closeto(float a, float b, float rtol=1e-4f,
            float atol=1e-5f) {
        if (a == b) // for infs.
            return true;
        if (isnan(a) || isnan(b)) // for nans.
            return false;
        return mag(a - b) <= (atol + rtol*mag(b));
    }
    public static bool closeto(Vec2 a, Vec2 b, float rtol=1e-4f,
            float atol=1e-5f) {
        if (a == b) // for infs.
            return true;
        if (isnan(a) || isnan(b)) // for nans.
            return false;
        return mag(a - b) <= (atol + rtol*mag(b));
    }
    public static bool closeto(Vec3 a, Vec3 b, float rtol=1e-4f,
            float atol=1e-5f) {
        if (a == b) // for infs.
            return true;
        if (isnan(a) || isnan(b)) // for nans.
            return false;
        return mag(a - b) <= (atol + rtol*mag(b));
    }

    public static bool nearzero(float a) => closeto(mag(a), 0f);
    public static bool nearzero(Vec2 a)  => closeto(mag(a), 0f);
    public static bool nearzero(Vec3 a)  => closeto(mag(a), 0f);

    public static bool nearunit(float a) => closeto(mag(a), 1f);
    public static bool nearunit(Vec2 a)  => closeto(mag(a), 1f);
    public static bool nearunit(Vec3 a)  => closeto(mag(a), 1f);

    public static float round(float a) => float.Round(a);

    public static float lerp(float a, float b, float t)
        => a + clamp(t, 0f, 1f)*(b - a);
    public static Vec2 lerp(Vec2 a, Vec2 b, float t)
        => a + clamp(t, 0f, 1f)*(b - a);
    public static Vec3 lerp(Vec3 a, Vec3 b, float t)
        => a + clamp(t, 0f, 1f)*(b - a);
    public static float lerp(float a, float b, int n, int d)
        => a + clamp(n/(float)d, 0f, 1f)*(b - a);

    public static Vec2 min(Vec2 a, Vec2 b) => new(min(a.X, b.X), min(a.Y, b.Y));
    public static Vec2 max(Vec2 a, Vec2 b) => new(max(a.X, b.X), max(a.Y, b.Y));

    public static Vec3 min(Vec3 a, Vec3 b)
        => new(min(a.X, b.X), min(a.Y, b.Y), min(a.Z, b.Z));
    public static Vec3 max(Vec3 a, Vec3 b)
        => new(max(a.X, b.X), max(a.Y, b.Y), max(a.Z, b.Z));

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

    public static float sum(params float[] vs) {
        float m = 0f;
        foreach (float v in vs)
            m += isnan(v) ? 0f : v;
        return m;
    }
    public static float prod(params float[] vs) {
        float m = 1f;
        foreach (float v in vs)
            m *= isnan(v) ? 1f : v;
        return m;
    }
    public static float ave(params float[] vs) {
        assert(numel(vs) > 0);
        return sum(vs) / numel(vs);
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
    public static float wraprad(float rad) // [-PI, PI)
        // shitass c sharp non-positive modulo.
        => ((rad + PI)%TWOPI + TWOPI)%TWOPI - PI;
    public static float wrapdeg(float deg) // [-180, 180)
        => ((deg + 180f)%360f + 360f)%360f - 180f;

    public static float sin(float a) => MathF.Sin(a);
    public static float cos(float a) => MathF.Cos(a);
    public static float tan(float a) => MathF.Tan(a);

    public static float asin(float a) => MathF.Asin(a);
    public static float acos(float a) => MathF.Acos(a);
    public static float atan(float a) => MathF.Atan(a);
    public static float atan2(float y, float x, float ifzero=0f)
        => (nearzero(y) && nearzero(x)) ? ifzero : MathF.Atan2(y, x);

    public static float hypot(float x, float y) => sqrt(x*x + y*y);
    public static float hypot(float x, float y, float z)
        => sqrt(x*x + y*y + z*z);
    public static float nonhypot(float x, float y) // an instant classic.
        => sqrt(x*x - y*y);

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
        => arg(projxy(a), ifzero);
    public static float argzx(Vec3 a, float ifzero=0f) // angle from +z.
        => arg(projzx(a), ifzero);
    public static float argyz(Vec3 a, float ifzero=0f) // angle from +y.
        => arg(projyz(a), ifzero);

    public static float argphi(Vec3 a, float ifzero=PI_2)
        => nearzero(a) ? ifzero : acos(a.Z/mag(a));

    public static float argbeta(Vec2 a, Vec2 b) => argbeta(rejxy(a), rejxy(b));
    public static float argbeta(Vec3 a, Vec3 b) {
        float maga = mag(a);
        float magb = mag(b);
        if (nearzero(maga) || nearzero(magb))
            return 0;
        return acos(clamp(dot(a, b) / maga / magb, -1f, 1f));
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

}
