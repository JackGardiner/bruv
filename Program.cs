using PicoGK;
using Leap71.ShapeKernel;

try
{
    Library.Go(
        0.1f,
        HelixCooling.Task);
}
catch (Exception e)
{
    Library.Log("Failed to run Task.");
    Library.Log(e.ToString());
    Library.oViewer().SetBackgroundColor(Cp.clrWarning);
}
