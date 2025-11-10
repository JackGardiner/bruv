using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Leap71.ShapeKernel;
using PicoGK;
using static System.MathF;

public class HelixCooling
{
    HelixConfig m_oConfig;

    float m_fCylinderLength;
    float m_fCylinderRadius;
    float m_fHelixAngleDeg;
    int m_iChannelCount;

    public class HelixConfig
    {
        public float rChamber { get; set; }
        public float rThroat { get; set; }
        public float rExit { get; set; }
        public float chamberLength { get; set; }
        public float throatAngleDeg { get; set; }
        public float exitAngleDeg { get; set; }
        public float NLF { get; set; }
        public float D_exit { get; set; }
        public float D_tht { get; set; }
        public float D_c { get; set; }
        public float theta_div { get; set; }
        public float theta_conv { get; set; }
        public float theta_exit { get; set; }
    }

    public static void Task()
    {
        Library.Log("Starting Task.");


        HelixCooling oCoolChannels = new HelixCooling();
        Voxels voxCoolChannels = oCoolChannels.voxConstruct();

        Sh.ExportVoxelsToSTLFile(voxCoolChannels, Path.Combine(Utils.strProjectRootFolder(), $"exports/helix.stl"));

        Library.Log("Finished Task successfully.");
    }

    public HelixCooling()
    {
        m_oConfig = JsonLoader.LoadFromJsonFile<HelixConfig>
            (Path.Combine(Utils.strProjectRootFolder(), $"src/engine.json"));
        m_fCylinderLength = 250f;
        m_fCylinderRadius = 80f;
        m_fHelixAngleDeg = 30f;
        m_iChannelCount = 10;
    }

    public float HelixAngleProfile(float fHeight)
    {
        // simple sinusoidal sweep for now
        float fHelixAngleDeg = 30f + 5f * Cos(fHeight * (2 * PI) / m_fCylinderLength);
        return fHelixAngleDeg * (float)Math.PI / 180f;
    }

    public float RadiusProfile(float fHeight)
    {
        float fRadius = 50f + 10f * Cos(fHeight * (2 * PI) / m_fCylinderLength);
        return fRadius;
    }

    ///
    public float RaoProfile(float fHeight)
    {
        var s = m_oConfig;
        float x0 = s.NLF * (1.8660254037844386f * s.D_exit
                         - 1.8408797767452460f * s.D_tht);
        float y0 = s.D_exit * 0.5f;
        float x1 = s.D_tht * 0.191f * Sin(s.theta_div);
        float y1 = s.D_tht * (0.691f - 0.191f * Sin(s.theta_div));
        float x2 = 0.0f;
        float y2 = s.D_tht * 0.5f;
        float x3 = s.D_tht * 0.75f * Sin(s.theta_conv);
        float y3 = s.D_tht * (1.25f - 0.75f * Cos(s.theta_conv));
        float x4 = s.D_c * 0.5f / Tan(s.theta_conv)
               + s.D_tht * (0.75f / Sin(s.theta_conv)
                           - 1.25f / Tan(s.theta_conv));
        float y4 = s.D_c * 0.5f;
        float x5 = x4 - 0.6f * s.D_c;
        float y5 = s.D_c * 0.5f;
        float xC = (Tan(s.theta_exit) * x0 - Tan(s.theta_div) * x1 + y1 - y0)
               / (Tan(s.theta_exit) - Tan(s.theta_div));
        float yC = Tan(s.theta_exit) * (xC - x0) + y0;



        float ax = x1 - 2f * xC + x0;
        float bx = -2f * x1 + 2f * xC;
        float cx = x1;
        float ay = y1 - 2f * yC + y0;
        float by = -2f * y1 + 2f * yC;
        float cy = y1;

        int i = 1; // change this obviously
        int start = 1;
        int count_1_0 = 1;
        float x = x1 + (x0 - x1) * (i - start) / (float)(count_1_0 - 1);
        float p = (-bx + Sqrt(bx * bx - 4.0f * ax * (cx - x))) / (2.0f * ax);
        float y = ay * p * p + by * p + cy;
        // s.nzl_contour_x[i] = x;
        // s.nzl_contour_y[i] = y;
        // ++i;
        //     }

        float fRadius = 1f;
        return fRadius;
    }

    public List<Vector3> GetProfilePoints(float fPhiOffset)
    {
        List<Vector3> aPoints = new List<Vector3>();
        float fSampleDensity = 10; // pts per mm

        float fPhi = fPhiOffset;
        // simple cylindrical profile (for now...)
        for (int i = 0; i < fSampleDensity * m_fCylinderLength; i++)
        {
            float fHeight = i / fSampleDensity;
            float fHelixAngle = HelixAngleProfile(fHeight);
            float fRadius = RadiusProfile(fHeight);

            float fDelHeight = 1 / fSampleDensity;
            float fDelPhi = fDelHeight / (fRadius * (float)Math.Tan(fHelixAngle)) % 2f * (float)Math.PI;

            Vector3 vecPoint = new Vector3(0, fRadius, fHeight);
            vecPoint = VecOperations.vecRotateAroundZ(vecPoint, fPhi);
            aPoints.Add(vecPoint);

            fPhi += fDelPhi;
        }
        return aPoints;
    }
    
    public Voxels voxConstruct()
    {
        float fHelixAngleRad = m_fHelixAngleDeg * (float)(Math.PI / 180f);

        LocalFrame oBaseFrame = new LocalFrame();
        BaseCylinder oGasCylinder = new BaseCylinder(oBaseFrame, m_fCylinderLength, m_fCylinderRadius-0.75f);
        Voxels voxGasCylinder = oGasCylinder.voxConstruct();

        Voxels voxChannels = new Voxels();
        for (int i = 0; i < m_iChannelCount; i++)
        {
            float fPhiOffset = i * (2f*(float)Math.PI / m_iChannelCount);
            List<Vector3> aPoints = GetProfilePoints(fPhiOffset);
            Frames aFrames = new Frames(aPoints, Frames.EFrameType.CYLINDRICAL);

            BaseBox oChannel = new BaseBox(aFrames, 1.5f, 3f);
            voxChannels += oChannel.voxConstruct();
        }
        
        
        // Sh.PreviewVoxels(voxGasCylinder, Cp.clrBlue, 0.5f);
        Sh.PreviewVoxels(voxChannels, Cp.clrRacingGreen);

        return voxChannels;
    }
}