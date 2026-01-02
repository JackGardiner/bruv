using System;
using System.Collections.Generic;

namespace Calculations
{
    public static class GraphLookup
    {
        public struct Point { public float X; public float Y; }

        // --- Spray Cone Angle Plot (TwoAlpha vs. A) ---
        // Samples at lbar_n = 2.0 and 0.5
        private static readonly Point[] SprayCone_N2 = new[]
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

        private static readonly Point[] SprayCone_N05 = new[]
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
            float yAt05 = Interpolate(SprayCone_N05, twoalpha);
            float yAt20 = Interpolate(SprayCone_N2, twoalpha);

            float t = (z - 0.5f) / (2.0f - 0.5f);
            return yAt05 + t * (yAt20 - yAt05);
        }

        // --- Flow Coefficient Plot (Forward: A -> Mu_in) ---
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

        // --- Relative Liquid Vortex Radius (A vs. r_m_on_R_n) ---
        // Samples at Rbar_in = 1, 3, and 4
        private static readonly Point[] Vortex_R1 = new[]
        {
            new Point { X = 0.509229f, Y = 0.000839f }, new Point { X = 0.580521f, Y = 0.042812f },
            new Point { X = 0.656906f, Y = 0.078909f }, new Point { X = 0.723105f, Y = 0.105771f },
            new Point { X = 0.799490f, Y = 0.137671f }, new Point { X = 0.967536f, Y = 0.188877f },
            new Point { X = 1.069382f, Y = 0.221616f }, new Point { X = 1.211966f, Y = 0.265268f },
            new Point { X = 1.359642f, Y = 0.299685f }, new Point { X = 1.542965f, Y = 0.339979f },
            new Point { X = 1.716103f, Y = 0.380273f }, new Point { X = 1.899427f, Y = 0.415530f },
            new Point { X = 2.098027f, Y = 0.447429f }, new Point { X = 2.316994f, Y = 0.473452f },
            new Point { X = 2.571609f, Y = 0.504512f }, new Point { X = 2.826225f, Y = 0.531375f },
            new Point { X = 3.075748f, Y = 0.553200f }, new Point { X = 3.371100f, Y = 0.575866f },
            new Point { X = 3.666454f, Y = 0.601049f }, new Point { X = 3.951623f, Y = 0.616160f },
            new Point { X = 4.216422f, Y = 0.631270f }, new Point { X = 4.506683f, Y = 0.646380f },
            new Point { X = 4.807129f, Y = 0.662330f }, new Point { X = 5.087204f, Y = 0.677440f },
            new Point { X = 5.377466f, Y = 0.693389f }, new Point { X = 5.688095f, Y = 0.703463f },
            new Point { X = 5.963078f, Y = 0.714376f }, new Point { X = 6.273709f, Y = 0.725289f },
            new Point { X = 6.604708f, Y = 0.739559f }, new Point { X = 6.910246f, Y = 0.745435f },
            new Point { X = 7.215786f, Y = 0.756348f }, new Point { X = 7.506047f, Y = 0.760546f },
            new Point { X = 7.740290f, Y = 0.763064f }, new Point { X = 8.005092f, Y = 0.769780f }
        };

        private static readonly Point[] Vortex_R3 = new[]
        {
            new Point { X = 0.870783f, Y = 0.003358f }, new Point { X = 0.931890f, Y = 0.029381f },
            new Point { X = 0.977721f, Y = 0.057083f }, new Point { X = 1.043920f, Y = 0.088143f },
            new Point { X = 1.161043f, Y = 0.121721f }, new Point { X = 1.298536f, Y = 0.160336f },
            new Point { X = 1.486950f, Y = 0.205666f }, new Point { X = 1.685550f, Y = 0.242602f },
            new Point { X = 1.924888f, Y = 0.280378f }, new Point { X = 2.194779f, Y = 0.321511f },
            new Point { X = 2.454487f, Y = 0.354250f }, new Point { X = 2.673455f, Y = 0.379433f },
            new Point { X = 2.892424f, Y = 0.398741f }, new Point { X = 3.203055f, Y = 0.426443f },
            new Point { X = 3.462761f, Y = 0.449108f }, new Point { X = 3.977083f, Y = 0.481007f },
            new Point { X = 4.639081f, Y = 0.509549f }, new Point { X = 5.285803f, Y = 0.542288f },
            new Point { X = 5.891786f, Y = 0.567471f }, new Point { X = 6.548693f, Y = 0.596013f },
            new Point { X = 7.159768f, Y = 0.610283f }, new Point { X = 7.526413f, Y = 0.615320f },
            new Point { X = 7.898153f, Y = 0.619517f }
        };

        private static readonly Point[] Vortex_R4 = new[]
        {
            new Point { X = 1.211966f, Y = 0.009234f }, new Point { X = 1.364735f, Y = 0.048689f },
            new Point { X = 1.548058f, Y = 0.088143f }, new Point { X = 1.751749f, Y = 0.127597f },
            new Point { X = 1.935073f, Y = 0.156978f }, new Point { X = 2.210056f, Y = 0.191396f },
            new Point { X = 2.515594f, Y = 0.221616f }, new Point { X = 2.841501f, Y = 0.249318f },
            new Point { X = 3.136854f, Y = 0.270304f }, new Point { X = 3.447485f, Y = 0.292130f },
            new Point { X = 3.763206f, Y = 0.310598f }, new Point { X = 4.134944f, Y = 0.334103f },
            new Point { X = 4.476128f, Y = 0.356768f }, new Point { X = 4.852958f, Y = 0.373557f },
            new Point { X = 5.143219f, Y = 0.389507f }, new Point { X = 5.570971f, Y = 0.402099f },
            new Point { X = 5.963078f, Y = 0.416369f }, new Point { X = 6.360279f, Y = 0.434837f },
            new Point { X = 6.793124f, Y = 0.450787f }, new Point { X = 7.134307f, Y = 0.462539f },
            new Point { X = 7.546783f, Y = 0.471773f }, new Point { X = 7.872690f, Y = 0.483526f },
            new Point { X = 8.056012f, Y = 0.484365f }
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