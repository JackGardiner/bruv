
using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;
using Colour = PicoGK.ColorFloat;

public static class Br {

    public static void assert(bool expression) {
        if (!expression) {
            var stackFrame = new System.Diagnostics.StackTrace(true).GetFrame(1);
            string file = stackFrame?.GetFileName() ?? "unknown file";
            int line = stackFrame?.GetFileLineNumber() ?? 0;

            throw new InvalidOperationException($"{file}:{line}");
        }
    }

    public static void assertx(bool expression, string message,
            params object[] args) {
        if (!expression) {
            var stackFrame = new System.Diagnostics.StackTrace(true).GetFrame(1);
            string file = stackFrame?.GetFileName() ?? "unknown file";
            int line = stackFrame?.GetFileLineNumber() ?? 0;

            string extra = string.Format(message, args);
            throw new InvalidOperationException($"{file}:{line} - {extra}");
        }
    }

    public static float INF => float.PositiveInfinity;
    public static float NAN => float.NaN;

    public static float PI => MathF.PI;
    public static float TWOPI => 2f*MathF.PI;
    public static float PI_2 => MathF.PI/2f;

    public static int AXISX => 0;
    public static int AXISY => 1;
    public static int AXISZ => 2;

    public static Vec2 ZERO2 => Vec2.Zero;
    public static Vec3 ZERO3 => Vec3.Zero;
    public static Vec2 ONE2 => Vec2.One;
    public static Vec3 ONE3 => Vec3.One;

    public static Vec2 uX2 => Vec2.UnitX;
    public static Vec2 uY2 => Vec2.UnitY;
    public static Vec3 uX3 => Vec3.UnitX;
    public static Vec3 uY3 => Vec3.UnitY;
    public static Vec3 uZ3 => Vec3.UnitZ;

    public static float abs(float a) => (a < 0.0) ? -a : a;

    public static float min(params float[] v) {
        assertx(v.Length > 0, "length={0}", v.Length);
        float m = v[0];
        for (int i=1; i<v.Length; ++i) {
            if (v[i] < m)
                m = v[i];
        }
        return m;
    }
    public static float max(params float[] v) {
        assertx(v.Length > 0, "length={0}", v.Length);
        float m = v[0];
        for (int i=1; i<v.Length; ++i) {
            if (v[i] > m)
                m = v[i];
        }
        return m;
    }

    public static float clamp(float a, float lo, float hi)
            => (a > hi) ? hi : (a < lo) ? lo : a;

    public static float pow(float a, float b) => MathF.Pow(a, b);
    public static float exp(float a) => MathF.Exp(a);
    public static float log(float a) => MathF.Log(a);
    public static float log(float a, float b) => MathF.Log(a, b);
    public static float log2(float a) => MathF.Log10(a);
    public static float log10(float a) => MathF.Log10(a);

    public static float sqrt(float a) => MathF.Sqrt(a);
    public static float cbrt(float a) => MathF.Cbrt(a);

    public static float torad(float deg) => deg * (PI / 180f);
    public static float todeg(float rad) => rad * (180f / PI);

    public static float sin(float a) => MathF.Sin(a);
    public static float cos(float a) => MathF.Cos(a);
    public static float tan(float a) => MathF.Tan(a);

    public static float asin(float a) => MathF.Asin(a);
    public static float acos(float a) => MathF.Acos(a);
    public static float atan(float a) => MathF.Atan(a);

    public static float hypot(float y, float x) => MathF.Sqrt(x*x + y*y);
    public static float atan2(float y, float x) => MathF.Atan2(y, x);

    public static float mag(Vec2 a) => a.Length();
    public static float mag(Vec3 a) => a.Length();
    public static float arg(Vec2 a) => atan2(a.Y, a.X);

    public static Vec2 tocart(float r, float theta)
            => new Vec2(r*cos(theta), r*sin(theta));

    public static Vec2 projxy(Vec3 a) => new Vec2(a.X, a.Y);
    public static Vec2 projxz(Vec3 a) => new Vec2(a.X, a.Z);
    public static Vec2 projyz(Vec3 a) => new Vec2(a.Y, a.Z);
    public static Vec2 projection(Vec3 a, int axis)
        => axis switch
        {
            0 => projyz(a),
            1 => projxz(a),
            2 => projxy(a),
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "")
        };
    public static Vec3 rejxy(Vec2 a, float b=0f) => new Vec3(a.X, a.Y, b);
    public static Vec3 rejxz(Vec2 a, float b=0f) => new Vec3(a.X, b, a.Y);
    public static Vec3 rejyz(Vec2 a, float b=0f) => new Vec3(b, a.X, a.Y);
    public static Vec3 rejection(Vec2 a, int axis, float b=0f)
        => axis switch
        {
            0 => rejyz(a, b),
            1 => rejxz(a, b),
            2 => rejxy(a, b),
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "")
        };

    public static Vec2 normalise(Vec2 a) => Vec2.Normalize(a);
    public static Vec3 normalise(Vec3 a) => Vec3.Normalize(a);

    public static float dot(Vec2 a, Vec2 b) => Vec2.Dot(a, b);
    public static float dot(Vec3 a, Vec3 b) => Vec3.Dot(a, b);

    public static float cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;
    public static Vec3 cross(Vec3 a, Vec3 b) => Vec3.Cross(a, b);


    public static Vec2 min(params Vec2[] v) { // element-wise.
        assertx(v.Length > 0, "length={0}", v.Length);
        Vec2 m = v[0];
        for (int i=1; i<v.Length; ++i) {
            if (v[i].X < m.X)
                m.X = v[i].X;
            if (v[i].Y < m.Y)
                m.Y = v[i].Y;
        }
        return m;
    }
    public static Vec2 max(params Vec2[] v) { // element-wise.
        assertx(v.Length > 0, "length={0}", v.Length);
        Vec2 m = v[0];
        for (int i=1; i<v.Length; ++i) {
            if (v[i].X > m.X)
                m.X = v[i].X;
            if (v[i].Y > m.Y)
                m.Y = v[i].Y;
        }
        return m;
    }

    public static Vec3 min(params Vec3[] v) { // element-wise.
        assertx(v.Length > 0, "length={0}", v.Length);
        Vec3 m = v[0];
        for (int i=1; i<v.Length; ++i) {
            if (v[i].X < m.X)
                m.X = v[i].X;
            if (v[i].Y < m.Y)
                m.Y = v[i].Y;
            if (v[i].Z < m.Z)
                m.Z = v[i].Z;
        }
        return m;
    }
    public static Vec3 max(params Vec3[] v) { // element-wise.
        assertx(v.Length > 0, "length={0}", v.Length);
        Vec3 m = v[0];
        for (int i=1; i<v.Length; ++i) {
            if (v[i].X > m.X)
                m.X = v[i].X;
            if (v[i].Y > m.Y)
                m.Y = v[i].Y;
            if (v[i].Z > m.Z)
                m.Z = v[i].Z;
        }
        return m;
    }


    public static Colour COLOUR_RED => new Colour("#FF0000");
    public static Colour COLOUR_GREEN => new Colour("#00FF00");
    public static Colour COLOUR_BLUE => new Colour("#0000FF");
    public static Colour COLOUR_CYAN => new Colour("#00FFFF");
    public static Colour COLOUR_PINK => new Colour("#FF00FF");
    public static Colour COLOUR_YELLOW => new Colour("#FFFF00");
}
