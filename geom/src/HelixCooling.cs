using System.Diagnostics;
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
        
        Sh.PreviewVoxels(voxChannels, Cp.clrRacingGreen);

        return voxChannels;
    }
}