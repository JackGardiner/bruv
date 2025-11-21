
using System.Numerics;
using Leap71.ShapeKernel;
using PicoGK;

public class HoleMaker
{
    LocalFrame m_oFrame;
    float m_fDiameter;
    float m_fDrillDepth;
    float m_fTaperDepth;
    /// <summary>
    /// Class to create a simple hole to remove material
    /// from a part.
    /// </summary>
    public HoleMaker(
        LocalFrame oFrame,
        float fDiameter,
        float fDrillDepth,
        float fTaperDepth = 0f)
    {
        m_oFrame = oFrame;
        m_fDiameter = fDiameter;
        m_fDrillDepth = fDrillDepth;
        m_fTaperDepth = fTaperDepth;
    }

    /// <summary>
    /// Constructs the hole voxels.
    /// </summary>
    public Voxels voxConstruct()
    {
        BasePipe oHole = new BasePipe(
            m_oFrame,
            -m_fDrillDepth,
            0f, m_fDiameter / 2f);
        Voxels voxHole = oHole.voxConstruct();

        BaseCone oTaper = new BaseCone(
            new LocalFrame(new Vector3(0, 0, -m_fDrillDepth)),
            -m_fTaperDepth, m_fDiameter, 0);

        return voxHole;
    }

    public float getTotalDepth()
    {
        return m_fDrillDepth + m_fTaperDepth;
    }
}