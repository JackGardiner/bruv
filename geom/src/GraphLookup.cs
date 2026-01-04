using System;
using System.Collections.Generic;

namespace Calculations
{
    public static class GraphLookup
    {
        public struct Point { public float X; public float Y; }

        // --- Spray Cone Angle Plot (Independent X: A, Dependent Y: TwoAlpha) ---
        // Samples at lbar_n = 2.0 and 0.5
        private static readonly Point[] SprayCone_A_to_TwoAlpha_N2 = new[]
        {
            new Point { X = 0.421421f, Y = 56.09843f }, new Point { X = 0.927129f, Y = 66.71354f },
            new Point { X = 1.299385f, Y = 73.60281f }, new Point { X = 1.636524f, Y = 78.17224f },
            new Point { X = 2.064969f, Y = 82.74165f }, new Point { X = 2.661984f, Y = 87.24077f },
            new Point { X = 3.167691f, Y = 89.77153f }, new Point { X = 3.834943f, Y = 92.51318f },
            new Point { X = 4.326602f, Y = 94.41125f }, new Point { X = 5.007903f, Y = 96.66081f },
            new Point { X = 5.661108f, Y = 98.27768f }, new Point { X = 6.356455f, Y = 99.61336f },
            new Point { X = 7.044778f, Y = 100.8084f }, new Point { X = 7.585603f, Y = 101.3708f },
            new Point { X = 8.091308f, Y = 101.7926f }
        };

        private static readonly Point[] SprayCone_A_to_TwoAlpha_N05 = new[]
        {
            new Point { X = 0.456541f, Y = 56.59052f }, new Point { X = 0.695346f, Y = 65.16697f },
            new Point { X = 0.870938f, Y = 70.29878f }, new Point { X = 1.046533f, Y = 74.93849f },
            new Point { X = 1.313432f, Y = 80.56239f }, new Point { X = 1.573309f, Y = 84.78032f },
            new Point { X = 1.833188f, Y = 88.36555f }, new Point { X = 2.128183f, Y = 91.59930f },
            new Point { X = 2.549605f, Y = 95.18454f }, new Point { X = 2.914838f, Y = 97.50440f },
            new Point { X = 3.427569f, Y = 100.3163f }, new Point { X = 3.898157f, Y = 102.5659f },
            new Point { X = 4.319579f, Y = 104.1828f }, new Point { X = 4.783144f, Y = 105.7996f },
            new Point { X = 5.260755f, Y = 107.2056f }, new Point { X = 5.808605f, Y = 108.8225f },
            new Point { X = 6.525022f, Y = 110.5097f }, new Point { X = 7.220371f, Y = 111.7047f },
            new Point { X = 7.676910f, Y = 112.0562f }, new Point { X = 8.091308f, Y = 112.5483f }
        };

        public static float GetSprayConeAngle(float targetA, float lbar_n)
        {
            float z = Math.Clamp(lbar_n, 0.5f, 2.0f);
            float yAt05 = Interpolate(SprayCone_A_to_TwoAlpha_N05, targetA);
            float yAt20 = Interpolate(SprayCone_A_to_TwoAlpha_N2, targetA);

            float t = (z - 0.5f) / (2.0f - 0.5f);
            return yAt05 + t * (yAt20 - yAt05);
        }

        // --- Spray Cone Angle Plot (Independent X: TwoAlpha, Dependent Y: A) ---
        private static readonly Point[] SprayCone_TwoAlpha_to_A_N2 = new[]
        {
            new Point { X = 56.09843f, Y = 0.421421f }, new Point { X = 66.71354f, Y = 0.927129f },
            new Point { X = 73.60281f, Y = 1.299385f }, new Point { X = 78.17224f, Y = 1.636524f },
            new Point { X = 82.74165f, Y = 2.064969f }, new Point { X = 87.24077f, Y = 2.661984f },
            new Point { X = 89.77153f, Y = 3.167691f }, new Point { X = 92.51318f, Y = 3.834943f },
            new Point { X = 94.41125f, Y = 4.326602f }, new Point { X = 96.66081f, Y = 5.007903f },
            new Point { X = 98.27768f, Y = 5.661108f }, new Point { X = 99.61336f, Y = 6.356455f },
            new Point { X = 100.8084f, Y = 7.044778f }, new Point { X = 101.3708f, Y = 7.585603f },
            new Point { X = 101.7926f, Y = 8.091308f }
        };

        private static readonly Point[] SprayCone_TwoAlpha_to_A_N05 = new[]
        {
            new Point { X = 56.59052f, Y = 0.456541f }, new Point { X = 65.16697f, Y = 0.695346f },
            new Point { X = 70.29878f, Y = 0.870938f }, new Point { X = 74.93849f, Y = 1.046533f },
            new Point { X = 80.56239f, Y = 1.313432f }, new Point { X = 84.78032f, Y = 1.573309f },
            new Point { X = 88.36555f, Y = 1.833188f }, new Point { X = 91.59930f, Y = 2.128183f },
            new Point { X = 95.18454f, Y = 2.549605f }, new Point { X = 97.50440f, Y = 2.914838f },
            new Point { X = 100.3163f, Y = 3.427569f }, new Point { X = 102.5659f, Y = 3.898157f },
            new Point { X = 104.1828f, Y = 4.319579f }, new Point { X = 105.7996f, Y = 4.783144f },
            new Point { X = 107.2056f, Y = 5.260755f }, new Point { X = 108.8225f, Y = 5.808605f },
            new Point { X = 110.5097f, Y = 6.525022f }, new Point { X = 111.7047f, Y = 7.220371f },
            new Point { X = 112.0562f, Y = 7.676910f }, new Point { X = 112.5483f, Y = 8.091308f }
        };

        public static float GetA(float twoalpha, float lbar_n)
        {
            float z = Math.Clamp(lbar_n, 0.5f, 2.0f);
            float yAt05 = Interpolate(SprayCone_TwoAlpha_to_A_N05, twoalpha);
            float yAt20 = Interpolate(SprayCone_TwoAlpha_to_A_N2, twoalpha);

            float t = (z - 0.5f) / (2.0f - 0.5f);
            return yAt05 + t * (yAt20 - yAt05);
        }

        // --- Flow Coefficient Plot (Independent X: A, Dependent Y: Mu_in) ---
        // Samples at C = 1.0 and 4.0
        private static readonly Point[] FlowCoeff_C1 = new[]
        {
            new Point { X = 0.827858f, Y = 0.400318f }, new Point { X = 0.947264f, Y = 0.373270f },
            new Point { X = 1.026863f, Y = 0.353858f }, new Point { X = 1.146268f, Y = 0.331901f },
            new Point { X = 1.305473f, Y = 0.309626f }, new Point { X = 1.416915f, Y = 0.291169f },
            new Point { X = 1.631838f, Y = 0.268894f }, new Point { X = 1.870646f, Y = 0.243755f },
            new Point { X = 2.157212f, Y = 0.220525f }, new Point { X = 2.499500f, Y = 0.198886f },
            new Point { X = 2.857709f, Y = 0.179157f }, new Point { X = 3.367164f, Y = 0.157518f },
            new Point { X = 3.804973f, Y = 0.141289f }, new Point { X = 4.378109f, Y = 0.126333f },
            new Point { X = 4.959204f, Y = 0.116150f }, new Point { X = 5.484577f, Y = 0.107239f },
            new Point { X = 6.073631f, Y = 0.098966f }, new Point { X = 6.726366f, Y = 0.091329f },
            new Point { X = 7.299502f, Y = 0.085601f }, new Point { X = 7.689552f, Y = 0.081782f },
            new Point { X = 8.000000f, Y = 0.079236f }
        };

        private static readonly Point[] FlowCoeff_C4 = new[]
        {
            new Point { X = 1.082585f, Y = 0.399364f }, new Point { X = 1.194027f, Y = 0.377725f },
            new Point { X = 1.313432f, Y = 0.354177f }, new Point { X = 1.424875f, Y = 0.336993f },
            new Point { X = 1.560198f, Y = 0.321400f }, new Point { X = 1.703481f, Y = 0.305807f },
            new Point { X = 1.878605f, Y = 0.287669f }, new Point { X = 2.061691f, Y = 0.269531f },
            new Point { X = 2.300495f, Y = 0.251710f }, new Point { X = 2.483581f, Y = 0.236436f },
            new Point { X = 2.722386f, Y = 0.222116f }, new Point { X = 3.000992f, Y = 0.206205f },
            new Point { X = 3.287561f, Y = 0.194431f }, new Point { X = 3.653730f, Y = 0.182657f },
            new Point { X = 4.083580f, Y = 0.172474f }, new Point { X = 4.473629f, Y = 0.162928f },
            new Point { X = 5.054724f, Y = 0.152426f }, new Point { X = 5.556216f, Y = 0.143834f },
            new Point { X = 6.145271f, Y = 0.135879f }, new Point { X = 6.766166f, Y = 0.127924f },
            new Point { X = 7.379101f, Y = 0.121559f }, new Point { X = 8.031838f, Y = 0.115195f }
        };

        public static float GetFlowCoefficient(float targetA, float C)
        {
            float z = Math.Clamp(C, 1.0f, 4.0f);
            float yAtC1 = Interpolate(FlowCoeff_C1, targetA);
            float yAtC4 = Interpolate(FlowCoeff_C4, targetA);

            float t = (z - 1.0f) / (4.0f - 1.0f);
            return yAtC1 + t * (yAtC4 - yAtC1);
        }

        // --- Flow Coefficient Plot (Inverse: Mu_in -> A) ---
        private static readonly Point[] FlowCoeff_C1_Inv = new[]
        {
            new Point { X = 0.079236f, Y = 8.000000f }, new Point { X = 0.081782f, Y = 7.689552f },
            new Point { X = 0.085601f, Y = 7.299502f }, new Point { X = 0.091329f, Y = 6.726366f },
            new Point { X = 0.098966f, Y = 6.073631f }, new Point { X = 0.107239f, Y = 5.484577f },
            new Point { X = 0.116150f, Y = 4.959204f }, new Point { X = 0.126333f, Y = 4.378109f },
            new Point { X = 0.141289f, Y = 3.804973f }, new Point { X = 0.157518f, Y = 3.367164f },
            new Point { X = 0.179157f, Y = 2.857709f }, new Point { X = 0.198886f, Y = 2.499500f },
            new Point { X = 0.220525f, Y = 2.157212f }, new Point { X = 0.243755f, Y = 1.870646f },
            new Point { X = 0.268894f, Y = 1.631838f }, new Point { X = 0.291169f, Y = 1.416915f },
            new Point { X = 0.309626f, Y = 1.305473f }, new Point { X = 0.331901f, Y = 1.146268f },
            new Point { X = 0.353858f, Y = 1.026863f }, new Point { X = 0.373270f, Y = 0.947264f },
            new Point { X = 0.400318f, Y = 0.827858f }
        };

        private static readonly Point[] FlowCoeff_C4_Inv = new[]
        {
            new Point { X = 0.115195f, Y = 8.031838f }, new Point { X = 0.121559f, Y = 7.379101f },
            new Point { X = 0.127924f, Y = 6.766166f }, new Point { X = 0.135879f, Y = 6.145271f },
            new Point { X = 0.143834f, Y = 5.556216f }, new Point { X = 0.152426f, Y = 5.054724f },
            new Point { X = 0.162928f, Y = 4.473629f }, new Point { X = 0.172474f, Y = 4.083580f },
            new Point { X = 0.182657f, Y = 3.653730f }, new Point { X = 0.194431f, Y = 3.287561f },
            new Point { X = 0.206205f, Y = 3.000992f }, new Point { X = 0.222116f, Y = 2.722386f },
            new Point { X = 0.236436f, Y = 2.483581f }, new Point { X = 0.251710f, Y = 2.300495f },
            new Point { X = 0.269531f, Y = 2.061691f }, new Point { X = 0.287669f, Y = 1.878605f },
            new Point { X = 0.305807f, Y = 1.703481f }, new Point { X = 0.321400f, Y = 1.560198f },
            new Point { X = 0.336993f, Y = 1.424875f }, new Point { X = 0.354177f, Y = 1.313432f },
            new Point { X = 0.377725f, Y = 1.194027f }, new Point { X = 0.399364f, Y = 1.082585f }
        };

        public static float GetAFromMu(float mu_in, float C)
        {
            float z = Math.Clamp(C, 1.0f, 4.0f);
            float yAtC1 = Interpolate(FlowCoeff_C1_Inv, mu_in);
            float yAtC4 = Interpolate(FlowCoeff_C4_Inv, mu_in);

            float t = (z - 1.0f) / (4.0f - 1.0f);
            return yAtC1 + t * (yAtC4 - yAtC1);
        }

        // --- Relative Liquid Vortex Radius (Independent X: A, Dependent Y: r_m_on_R_n) ---
        // Samples at Rbar_in = 1, 3, and 4
        private static readonly Point[] Vortex_R1 = new[]
        {
            new Point { X = 0.523076f, Y = 0.300632f }, new Point { X = 0.676923f, Y = 0.356258f },
            new Point { X = 0.953845f, Y = 0.413780f }, new Point { X = 1.212306f, Y = 0.459924f },
            new Point { X = 1.606152f, Y = 0.519975f }, new Point { X = 1.901537f, Y = 0.557901f },
            new Point { X = 2.276921f, Y = 0.592667f }, new Point { X = 2.719998f, Y = 0.623641f },
            new Point { X = 3.076923f, Y = 0.645133f }, new Point { X = 3.612306f, Y = 0.672313f },
            new Point { X = 4.030769f, Y = 0.686220f }, new Point { X = 4.670769f, Y = 0.710240f },
            new Point { X = 5.230768f, Y = 0.726675f }, new Point { X = 5.846152f, Y = 0.744374f },
            new Point { X = 6.283075f, Y = 0.753856f }, new Point { X = 6.769228f, Y = 0.763338f },
            new Point { X = 7.347693f, Y = 0.772819f }, new Point { X = 8.000000f, Y = 0.781037f }
        };

        private static readonly Point[] Vortex_R3 = new[]
        {
            new Point { X = 0.916922f, Y = 0.301264f }, new Point { X = 0.990769f, Y = 0.331606f },
            new Point { X = 1.107691f, Y = 0.362579f }, new Point { X = 1.396921f, Y = 0.410619f },
            new Point { X = 1.667691f, Y = 0.449178f }, new Point { X = 2.024615f, Y = 0.487737f },
            new Point { X = 2.461537f, Y = 0.519343f }, new Point { X = 2.769229f, Y = 0.541466f },
            new Point { X = 3.224613f, Y = 0.568015f }, new Point { X = 3.735383f, Y = 0.589507f },
            new Point { X = 4.523076f, Y = 0.619216f }, new Point { X = 5.212306f, Y = 0.640708f },
            new Point { X = 5.975383f, Y = 0.661568f }, new Point { X = 6.775382f, Y = 0.679267f },
            new Point { X = 7.495382f, Y = 0.695070f }, new Point { X = 8.049228f, Y = 0.703919f }
        };

        private static readonly Point[] Vortex_R4 = new[]
        {
            new Point { X = 1.224614f, Y = 0.299368f }, new Point { X = 1.464615f, Y = 0.341719f },
            new Point { X = 1.692306f, Y = 0.368268f }, new Point { X = 1.993845f, Y = 0.397977f },
            new Point { X = 2.313845f, Y = 0.421365f }, new Point { X = 2.646153f, Y = 0.440961f },
            new Point { X = 2.984613f, Y = 0.459924f }, new Point { X = 3.415383f, Y = 0.478255f },
            new Point { X = 3.821536f, Y = 0.495954f }, new Point { X = 4.301537f, Y = 0.513021f },
            new Point { X = 4.769228f, Y = 0.528824f }, new Point { X = 5.310767f, Y = 0.543363f },
            new Point { X = 5.766152f, Y = 0.554741f }, new Point { X = 6.289230f, Y = 0.567383f },
            new Point { X = 6.873845f, Y = 0.578129f }, new Point { X = 7.458459f, Y = 0.590139f },
            new Point { X = 8.067689f, Y = 0.600885f }
        };

        public static float GetRelativeVortexRadius(float targetA, float rbar_in)
        {
            float z = Math.Clamp(rbar_in, 1.0f, 4.0f);
            if (z <= 3.0f)
            {
                float yAt1 = Interpolate(Vortex_R1, targetA);
                float yAt3 = Interpolate(Vortex_R3, targetA);
                float t = (z - 1.0f) / (3.0f - 1.0f);
                return yAt1 + t * (yAt3 - yAt1);
            }
            else
            {
                float yAt3 = Interpolate(Vortex_R3, targetA);
                float yAt4 = Interpolate(Vortex_R4, targetA);
                float t = (z - 3.0f) / (4.0f - 3.0f);
                return yAt3 + t * (yAt4 - yAt3);
            }
        }

        private static float Interpolate(Point[] points, float x)
        {
            if (x <= points[0].X) return points[0].Y;
            if (x >= points[^1].X) return points[^1].Y;

            int index = Array.BinarySearch(
                points,
                new Point { X = x },
                Comparer<Point>.Create((p1, p2) => p1.X.CompareTo(p2.X))
            );

            if (index < 0) index = ~index;
            if (index == 0) return points[0].Y;

            Point p0 = points[index - 1];
            Point p1 = points[index];

            float fraction = (x - p0.X) / (p1.X - p0.X);
            return p0.Y + fraction * (p1.Y - p0.Y);
        }
    }
}