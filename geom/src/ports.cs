using System.Numerics;
using PicoGK;
using Leap71.ShapeKernel;

namespace ports
{
    public class GPort
    {
        // inputs
        private readonly string m_sSize;
        private readonly float m_fDownstreamDiameter;

        // Geometry Outputs
        private readonly float m_fFaceDiameter;
        private readonly float m_fPilotBoreDiameter;
        private readonly float m_fBoreDepthTotal;

        // Constructor
        public GPort(string sSize, float fDownstreamDiameter)
        {
            m_sSize = sSize;
            m_fDownstreamDiameter = fDownstreamDiameter;

            GetRelevantDimensions(sSize,
                                out m_fFaceDiameter,
                                out m_fPilotBoreDiameter,
                                out m_fBoreDepthTotal);
        }

        // public query methods
        public float fGetFaceDiameter()
        {
            return m_fFaceDiameter;
        }
        public float fGetPilotBoreDiameter()
        {
            return m_fPilotBoreDiameter;
        }
        public float fGetBoreDepthTotal()
        {
            return m_fBoreDepthTotal;
        }
        public float fGetDownstreamDiameter()
        {
            return m_fDownstreamDiameter;
        }

        // vox builder
        public Voxels voxConstruct(LocalFrame oFaceFrame)
        {
            float fPilotRadius = m_fPilotBoreDiameter / 2f;
            float fDownstreamRadius = m_fDownstreamDiameter / 2f;
            float fBoreDepthTotal = m_fBoreDepthTotal;

            float fConeLength = fPilotRadius - fDownstreamRadius;

            // The straight section length must ensure the total length (Straight + Cone) equals m_fBoreDepthTotal.
            float fStraightLength = m_fBoreDepthTotal - fConeLength;

            LocalFrame oStraightFrame = oFaceFrame;

            BasePipe oPilotBore = new BasePipe(oStraightFrame, -fStraightLength, 0, fPilotRadius);
            BaseCone oTaper = new BaseCone(
                oFaceFrame.oTranslate(new Vector3(0, 0, -fBoreDepthTotal)),
                fPilotRadius - fDownstreamRadius,
                fDownstreamRadius, fPilotRadius);

        return oPilotBore.voxConstruct() + oTaper.voxConstruct();
        }

        // private helpers
        private void GetRelevantDimensions(string sSize, out float fFaceDiam, out float fBoreDiam, out float fBoreDepth)
        {
            float fRequiredFullThreadLength; // Internal temp var for calculation

            switch (sSize)
            {
                case "1/8in":
                    fBoreDiam = 9.73f;
                    fRequiredFullThreadLength = 10.5f;
                    fFaceDiam = 17.2f;
                    break;
                case "1/4in":
                    fBoreDiam = 13.3f;
                    fRequiredFullThreadLength = 15.5f;
                    fFaceDiam = 20.7f;
                    break;
                case "1/2in":
                    fBoreDiam = 21.1f;
                    fRequiredFullThreadLength = 19f;
                    fFaceDiam = 34.0f;
                    break;
                case "3/4in":
                    fBoreDiam = 26.6f;
                    fRequiredFullThreadLength = 20.5f;
                    fFaceDiam = 40.0f;
                    break;
                default:
                    Library.Log($"BSPP size '{sSize}' not recognised. Using 1/4in default.");
                    goto case "1/4in";
            }

            // Calculate total bore depth using the empirical rule (L_thread + 0.5 * D_pilot)
            fBoreDepth = fRequiredFullThreadLength + 0.5f * fBoreDiam;
        }
    }
}