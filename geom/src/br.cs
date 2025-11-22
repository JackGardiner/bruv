
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

    public static float pow(float x, float y) => MathF.Pow(x, y);
    public static float exp(float x) => MathF.Exp(x);
    public static float log(float x, float b) => MathF.Log(x, b);
    public static float ln(float x) => MathF.Log(x);
    public static float log2(float x) => MathF.Log10(x);
    public static float log10(float x) => MathF.Log10(x);

    public static float sqrt(float x) => MathF.Sqrt(x);
    public static float cbrt(float x) => MathF.Cbrt(x);

    public const float PI = MathF.PI;
    public static float torad(float deg) => deg * (MathF.PI / 180.0f);
    public static float todeg(float rad) => rad * (180.0f / MathF.PI);

    public static float sin(float x) => MathF.Sin(x);
    public static float cos(float x) => MathF.Cos(x);
    public static float tan(float x) => MathF.Tan(x);

    public static float asin(float x) => MathF.Asin(x);
    public static float acos(float x) => MathF.Acos(x);
    public static float atan(float x) => MathF.Atan(x);

    public static float hypot(float x, float y) => MathF.Sqrt(x*x + y*y);
    public static float atan2(float y, float x) => MathF.Atan2(y, x);
}
