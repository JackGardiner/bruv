
using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;
using Colour = PicoGK.ColorFloat;

public static class Br {

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

    public static string PATH_ROOT
            => Directory.GetParent(AppContext.BaseDirectory)
                !.Parent
                !.Parent
                !.Parent
                !.FullName;
    public static string fromroot(string rel) => Path.Combine(PATH_ROOT, rel);

    public static int numel<T>(T[] x) => x.Length;
    public static int numel<T>(List<T> x) => x.Count;
    public static int numel<T,U>(Dictionary<T,U> x) where T:notnull => x.Count;

    public static Colour COLOUR_BLACK => new("#000000");
    public static Colour COLOUR_RED => new("#FF0000");
    public static Colour COLOUR_GREEN => new("#00FF00");
    public static Colour COLOUR_BLUE => new("#0000FF");
    public static Colour COLOUR_CYAN => new("#00FFFF");
    public static Colour COLOUR_PINK => new("#FF00FF");
    public static Colour COLOUR_YELLOW => new("#FFFF00");
    public static Colour COLOUR_WHITE => new("#FFFFFF");

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

    public const int AXISX = 0;
    public const int AXISY = 1;
    public const int AXISZ = 2;
    public static bool isaxis(int axis)
            => (axis == AXISX) || (axis == AXISY) || (axis == AXISZ);

    public static Vec2 ZERO2 => Vec2.Zero;
    public static Vec3 ZERO3 => Vec3.Zero;
    public static Vec2 ONE2 => Vec2.One;
    public static Vec3 ONE3 => Vec3.One;

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
        return abs(a - b) <= (atol + rtol*abs(b));
    }

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
    public static float wraprad(float rad)
        // shitass c sharp non-positive modulo.
        => ((rad + PI)%TWOPI + TWOPI)%TWOPI - PI;
    public static float wrapdeg(float deg)
        => ((deg + 180f)%360f + 360f)%360f - 180f;

    public static float sin(float a) => MathF.Sin(a);
    public static float cos(float a) => MathF.Cos(a);
    public static float tan(float a) => MathF.Tan(a);

    public static float asin(float a) => MathF.Asin(a);
    public static float acos(float a) => MathF.Acos(a);
    public static float atan(float a) => MathF.Atan(a);

    public static float hypot(float x, float y) => sqrt(x*x + y*y);
    public static float hypot(float x, float y, float z)
        => sqrt(x*x + y*y + z*z);
    public static float atan2(float y, float x) => MathF.Atan2(y, x);

    public static float mag(Vec2 a) => a.Length();
    public static float mag(Vec3 a) => a.Length();
    public static float arg(Vec2 a) => atan2(a.Y, a.X);

    public static float magxy(Vec3 a) => hypot(a.X, a.Y);
    public static float magxz(Vec3 a) => hypot(a.X, a.Z);
    public static float magyz(Vec3 a) => hypot(a.Y, a.Z);

    public static float argxy(Vec3 a) => atan2(a.Y, a.X); // angle from +x.
    public static float argxz(Vec3 a) => atan2(a.X, a.Z); // angle from +z.
    public static float argyz(Vec3 a) => atan2(a.Y, a.Z); // angle from +z.

    public static float argphi(Vec3 a) => acos(a.Z / mag(a));

    public static Vec2 tocart(float r, float theta)
        => new(r*cos(theta), r*sin(theta));
    public static Vec3 tocart(float r, float theta, float z)
        => new(r*cos(theta), r*sin(theta), z);

    public struct AsPhi { public required float v { get; init; } };
    public static AsPhi as_phi(float phi) => new AsPhi{v=phi};
    public static Vec3 tocart(float r, float theta, AsPhi phi)
        => new(r*cos(theta)*sin(phi.v), r*sin(theta)*sin(phi.v), r*cos(phi.v));

    public static Vec2 projxy(Vec3 a) => new(a.X, a.Y);
    public static Vec2 projxz(Vec3 a) => new(a.X, a.Z);
    public static Vec2 projyz(Vec3 a) => new(a.Y, a.Z);
    public static Vec2 project(Vec3 a, int axis) {
        assert(isaxis(axis), $"axis={axis}");
        if (axis == AXISX)
            return projyz(a);
        if (axis == AXISY)
            return projxz(a);
        return projxy(a);
    }

    public static Vec3 rejxy(Vec2 a, float b=0f) => new(a.X, a.Y, b);
    public static Vec3 rejxz(Vec2 a, float b=0f) => new(a.X, b, a.Y);
    public static Vec3 rejyz(Vec2 a, float b=0f) => new(b, a.X, a.Y);
    public static Vec3 reject(Vec2 a, int axis, float b=0f) {
        assert(isaxis(axis), $"axis={axis}");
        if (axis == AXISX)
            return rejyz(a, b);
        if (axis == AXISY)
            return rejxz(a, b);
        return rejxy(a, b);
    }

    public static Vec2 rotate(Vec2 a, float b)
        => new(a.X*cos(b) + a.Y*sin(b), a.X*-sin(b) + a.Y*cos(b));
    public static Vec3 rotxy(Vec3 a, float b)
        => rejxy(rotate(projxy(a), b), a.Z);
    public static Vec3 rotxz(Vec3 a, float b)
        => rejxz(rotate(projxz(a), b), a.Y);
    public static Vec3 rotyz(Vec3 a, float b)
        => rejyz(rotate(projyz(a), b), a.X);
    public static Vec3 rotate(Vec3 a, int axis, float b) {
        assert(isaxis(axis), $"axis={axis}");
        if (axis == AXISX)
            return rotyz(a, b);
        if (axis == AXISY)
            return rotxz(a, b);
        return rotxy(a, b);
    }
    public static Vec3 rotate(Vec3 p, Vec3 about, float by) {
        about = normalise(about);
        float cosby = cos(by);
        float sinby = sin(by);
        return p*cosby
             + cross(about, p)*sinby
             + about*dot(about, p)*(1f - cosby);
    }

    public static Vec2 normalise(Vec2 a) => Vec2.Normalize(a);
    public static Vec3 normalise(Vec3 a) => Vec3.Normalize(a);
    public static Vec2 normalise_nonzero(Vec2 a)
        => closeto(mag(a), 0f) ? a : normalise(a);
    public static Vec3 normalise_nonzero(Vec3 a)
        => closeto(mag(a), 0f) ? a : normalise(a);

    public static float dot(Vec2 a, Vec2 b) => Vec2.Dot(a, b);
    public static float dot(Vec3 a, Vec3 b) => Vec3.Dot(a, b);

    public static float cross(Vec2 a, Vec2 b) => a.X*b.Y - a.Y*b.X;
    public static Vec3 cross(Vec3 a, Vec3 b) => Vec3.Cross(a, b);
}
