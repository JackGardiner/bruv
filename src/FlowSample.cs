using System.Numerics;
using PicoGK;
using Leap71.ShapeKernel;

public class FlowSample
{
    /// <summary>
    /// Task function that instantiates a new flow sample object and 
    /// calls its construction logic.
    /// </summary>
    public static void Task()
    {
        Library.Log("Let's get goin'");

        FlowSample oFirstSample = new FlowSample();

        Library.Log("Program complete.");
    }

    /// <summary>
    /// Instantiates a new object of the type FlowSample.
    /// </summary>
    public FlowSample()
    {

        float fChannelLength = 80f;
        float fChannelWidth = 2f;
        float fChannelDepth = 2f;

        float fHoleDiameter = 8.8f;
        float fHoleDepth = 12f;
        float fOffsetThickness = 2f;

        float fLoftHeight = 10f;

        int iChannelCount = 3;

        float fWebThickness = 0.5f;

        float fChannelSpacing = fChannelWidth + fWebThickness;

        List<LocalFrame> aFrames = new List<LocalFrame>(iChannelCount);
        Voxels voxChannels = new Voxels();
        for (int i = 0; i < iChannelCount; i++)
        {
            // define frame (centre of bottom face of channel)
            aFrames.Add(new LocalFrame(new Vector3(i * fChannelSpacing, 0, 0)));
            // create fluid region
            BaseBox oChannel = new BaseBox(aFrames[i], fChannelLength, fChannelWidth, fChannelDepth);
            Voxels voxChannel = oChannel.voxConstruct();
            voxChannels = voxChannels + voxChannel;
        }
        // create solid region around channels and boolean

        // add lofts (square->circle) at top and bottom
        INormalizedContour2d oLoftRectangle = new Rectangle(fChannelWidth, fChannelDepth);
        INormalizedContour2d oLoftCircle = new Circle(fHoleDiameter / 2f);

        BaseLoft oBtmLoft = new BaseLoft(new LocalFrame(new Vector3(0, 0, -fLoftHeight/2f), Vector3.UnitZ), oLoftCircle, oLoftRectangle, fLoftHeight);
        BaseLoft oTopLoft = new BaseLoft(new LocalFrame(new Vector3(0,0,fChannelLength+fLoftHeight/2f), -Vector3.UnitZ), oLoftRectangle, oLoftCircle, fLoftHeight);

        Voxels voxBtmLoft = new Voxels(oBtmLoft.mshConstruct());
        Voxels voxTopLoft = new Voxels(oTopLoft.mshConstruct());

        // add ports for fittings connection at top and bottom
        HoleMaker oTopHole = new HoleMaker(new LocalFrame(new Vector3(0, 0, fChannelLength+fLoftHeight), -Vector3.UnitZ), fHoleDiameter, fHoleDepth);
        HoleMaker oBottomHole = new HoleMaker(new LocalFrame(new Vector3(0, 0, -fLoftHeight), Vector3.UnitZ), fHoleDiameter, fHoleDepth);
        Voxels voxTopHole = oTopHole.voxConstruct();
        Voxels voxBottomHole = oBottomHole.voxConstruct();

        // combine all fluid voxels
        Voxels voxFluidRegion =  voxBtmLoft + voxTopLoft + voxBottomHole + voxTopHole;

        // Sh.PreviewVoxels(voxFluidRegion, Cp.clrBlue, 0.5f);

        BaseBox bCropBox = new BaseBox(
            new LocalFrame(new Vector3(0, 0, -(fHoleDepth+fLoftHeight)), Vector3.UnitZ),
            fChannelLength + 2f*fHoleDepth + 2f*fLoftHeight,
            fHoleDiameter + 2f * fOffsetThickness,
            fHoleDiameter + 2f * fOffsetThickness);

        Voxels voxCropBox = bCropBox.voxConstruct();

        Voxels voxShell = voxFluidRegion.voxOffset(fOffsetThickness);
        voxShell = voxShell.voxBoolIntersect(voxCropBox) - voxFluidRegion;
        Sh.PreviewVoxels(voxShell, Cp.clrRock, 1f);

        Sh.ExportVoxelsToSTLFile(voxShell, "FlowSample.stl");
    }
}
