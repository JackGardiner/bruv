using System.Numerics;
using System.Runtime.CompilerServices;
using Leap71.ShapeKernel;
using PicoGK;

public class HelixCooling
{
    float m_fCylinderLength;
    float m_fCylinderRadius;
    float m_fHelixAngleDeg;
    int m_iChannelCount;

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
        m_fCylinderLength = 250f;
        m_fCylinderRadius = 80f;
        m_fHelixAngleDeg = 30f;
        m_iChannelCount = 10;
    }

    public float HelixAngleProfile(float fHeight)
    {
        // simple sinusoidal sweep for now
        float fHelixAngleDeg = 30f + 5f * (float)Math.Cos(fHeight * (2 * Math.PI) / m_fCylinderLength);
        return fHelixAngleDeg * (float)Math.PI / 180f;
    }
    
    public float RadiusProfile(float fHeight)
    {
        float fRadius = 50f + 10f * (float)Math.Cos(fHeight * (2 * Math.PI) / m_fCylinderLength);
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