using System.Numerics;
using PicoGK;
using Leap71.ShapeKernel;

public class ChannelRoughnessSample
{
    /// <summary>
    /// Task function that instantiates a new flow sample object and 
    /// calls its construction logic.
    /// </summary>
    public static void Task()
    {
        Library.Log("Let's get goin'");

        ChannelRoughnessSample oFirstSample = new ChannelRoughnessSample();

        Library.Log("Program complete.");
    }

    /// <summary>
    /// Instantiates a new object of the type FlowSample.
    /// </summary>
    public ChannelRoughnessSample()
    {
        float fChannelAngleDeg  = 45;
        float fChannelLength    = 80f;
        float fChannelWidth     = 5f;
        float fChannelDepth     = 1.5f;
        float fWebThickness     = 1.5f;
        float fHotWallThickness = 1f;


        float fSampleWidth = 50f;
        int iChannelCount = 3;

        float fChannelAngle = fChannelAngleDeg * ((float)Math.PI / 180f);


        float fWebThicknessGlobalX = fWebThickness / (float)Math.Cos(fChannelAngle);
        float fChannelWidthGlobalX = fChannelWidth / (float)Math.Cos(fChannelAngle);


        float fChannelSpacing = fChannelWidthGlobalX + fWebThicknessGlobalX;

        float fAllChannelsWidth = (iChannelCount * fChannelSpacing) + fWebThicknessGlobalX;

        float fDeltaX = -fSampleWidth / 2f * (float)Math.Tan(fChannelAngle) + fAllChannelsWidth / 2f; // amount to shift channels to centre them on sample
        
        List<LocalFrame> aChannelBases = new List<LocalFrame>(iChannelCount);
        Voxels voxChannels = new Voxels();
        for (int i = 0; i < iChannelCount; i++)
        {
            // define frame (centre of bottom face of channel)
            float fChannelX = (i * fChannelSpacing) + fWebThicknessGlobalX + (0.5f * fChannelWidthGlobalX) - (0.5f * fAllChannelsWidth);
            LocalFrame oChannelFrame = new LocalFrame(new Vector3(fChannelX, 0, 0));
            oChannelFrame = oChannelFrame.oTranslate(new Vector3(fDeltaX, 0, 0));
            // rotate frame about y-axis
            oChannelFrame = oChannelFrame.oRotate(fChannelAngle, new Vector3(0, 1, 0));
            aChannelBases.Add(oChannelFrame);
            // Sh.PreviewFrame(oChannelFrame, 10f);

            // create fluid region
            BaseBox oChannelUp = new BaseBox(aChannelBases[i], fChannelLength * 2, fChannelWidth, fChannelDepth);
            BaseBox oChannelDown = new BaseBox(aChannelBases[i], -fChannelLength * 2, fChannelWidth, fChannelDepth);
            Voxels voxChannel = oChannelUp.voxConstruct() + oChannelDown.voxConstruct();
            voxChannels = voxChannels + voxChannel;
        }
        // create solid box and boolean that bad boy
        BaseBox oSampleBox = new BaseBox(new LocalFrame(), fSampleWidth, fSampleWidth, fChannelDepth + (2f * fHotWallThickness));
        Voxels voxSampleBox = oSampleBox.voxConstruct();

        Voxels voxSample = voxSampleBox - voxChannels;

        // create angled box to apply phat chamfer to directly measure roughness at the channel angle
        LocalFrame oSampleBackCentre = new LocalFrame(new Vector3(fAllChannelsWidth / 2f, 0, fSampleWidth / 2f));
        oSampleBackCentre = oSampleBackCentre.oRotate(fChannelAngle, new Vector3(0, 1, 0));
        Vector3 vecShift = VecOperations.vecTranslateDirectionOntoFrame(oSampleBackCentre, new Vector3(0, 0, -fChannelLength));
        oSampleBackCentre = oSampleBackCentre.oTranslate(vecShift);
        BaseBox oCropBox = new BaseBox(oSampleBackCentre, 4 * fChannelLength, fAllChannelsWidth + fWebThickness, fChannelDepth + (2 * fHotWallThickness));

        Voxels voxCropBox = oCropBox.voxConstruct();
        
        voxSample = voxCropBox.voxBoolIntersect(voxSample);

        Library.Log("we're here!");
        Sh.PreviewVoxels(voxSample, Cp.clrLavender, 0.8f);

        // Sh.ExportVoxelsToSTLFile(voxSample, Path.Combine(Utils.strProjectRootFolder(),
        //     $"exports/Sample_CW_{fChannelWidth:F1}_CD_{fChannelDepth:F1}_Web_{fWebThickness:F1}_HW_{fHotWallThickness:F1}_PHI_{fChannelAngleDeg:F0}.stl"));
    }
}
