using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using Leap71.ShapeKernel;
using PicoGK;
using static System.MathF;

public class Injector
{
    InjectorConfig oInj;
    public class InjectorConfig
    {
        //     {
        // "aElementCount": [1, 5, 8],
        // "aElementRadii": [0, 10, 20],
        // "aElementClocking": [0, 0, 0],
        public List<int> aElementCount {get; set;}
        public List<float> aElementRadii {get; set;}
        public List<float> aElementClocking {get; set;}
        public float fLOxPostFluidRad {get; set;}
        public float fLOxPostLength {get; set;}
        public float fLOxSwirlMflFluidRad {get; set;}
        public float fLOxSwirlMfldAngleDeg {get; set;}
        public float fLOxSwirlMfldLength {get; set;}
        public float fLOxSwirlWallThickness {get; set;}
    } 

    public static void Task()
    {
        // placeholder task fn for just the injector
        Injector oCoaxShearInjector = new Injector();
        Voxels voxCoaxShearInjector = oCoaxShearInjector.voxConstruct();
        Sh.PreviewVoxels(voxCoaxShearInjector, Cp.clrBillie);
    }

    public Injector()
    {
        oInj = JsonLoader.LoadFromJsonFile<InjectorConfig>
            (Path.Combine(Utils.strProjectRootFolder(), $"src/injector.json"));

    }

    public List<Vector3> InjectorPattern(List<int> aElementCount, List<float> aElementRadii, List<float> aElementClocking)
    {
        // check all lists same size
        // check for geom conflicts
        List<Vector3> aInjectorLocations = new List<Vector3>();
        for (int iRing=0; iRing<aElementCount.Count(); iRing++)
        {
            int iCount = aElementCount[iRing];
            float fRadius = aElementRadii[iRing];
            float fClocking = aElementClocking[iRing];
            for (int i=0; i<iCount; i++)
            {
                // x = r cos(phi)
                // y = r sin(phi)
                aInjectorLocations.Add(
                    new Vector3(fRadius*Cos(fClocking), fRadius*Sin(fClocking), 0f));
                fClocking += 2*PI/iCount;
            }
        }
        return aInjectorLocations;
    }

    public Voxels LOxSwirlCore(LocalFrame oInjectorLocation)
    {
        /*
               * *
             *     *
            *       *
            *   ==  *== swirl inlet
            * ||    *
            *  ===  *
             *  // *
              * | *
              * \ *     
        ______* | *______
               /\
        */

        LocalFrame oElementFaceFrame = new LocalFrame();
        BaseCylinder oLOxSwirlFluid = new BaseCylinder(oInjectorLocation, oInj.fLOxPostLength + oInj.fLOxSwirlMfldLength);
        oLOxSwirlFluid.SetRadius(new SurfaceModulation(new LineModulation(fGetLOxSwirlMfldLineModulation)));
        Voxels voxLOxSwirlFluid = oLOxSwirlFluid.voxConstruct();
        BBox3 oCropBox = voxLOxSwirlFluid.oCalculateBoundingBox();
        oCropBox.vecMin.X -= 2*oInj.fLOxSwirlWallThickness; oCropBox.vecMin.Y -= 2*oInj.fLOxSwirlWallThickness; 
        oCropBox.vecMax.X += 2*oInj.fLOxSwirlWallThickness; oCropBox.vecMax.Y += 2*oInj.fLOxSwirlWallThickness;
        oCropBox.vecMax.Z = 2*(oInj.fLOxPostLength + oInj.fLOxSwirlMfldLength);
        Voxels voxLOxSwirlWall = voxLOxSwirlFluid.voxOffset(oInj.fLOxSwirlWallThickness) - voxLOxSwirlFluid;
        voxLOxSwirlWall.Trim(oCropBox);

        return voxLOxSwirlWall;
    }

    public float fGetLOxSwirlMfldLineModulation(float fLengthRatio)
    {
        // total length of swirl manifold given
        // calculate bottom cone section length (angle = oInj.fLOxSwirlMfldAngleDeg)
        // calculate top cone section length (angle = 45deg)
        float fTopConeLength = oInj.fLOxSwirlMflFluidRad * Tan(45f*(PI/180f)); // remove AM constraint magic num.
        float fBottomConeLength = (oInj.fLOxSwirlMflFluidRad-oInj.fLOxPostFluidRad) * Tan(oInj.fLOxSwirlMfldAngleDeg*(PI/180f));
        float fStraightLength = oInj.fLOxSwirlMfldLength - fTopConeLength - fBottomConeLength;

        float fLength = (oInj.fLOxPostLength + oInj.fLOxSwirlMfldLength) * fLengthRatio;

        if (fLength < oInj.fLOxPostLength) {
            return oInj.fLOxPostFluidRad;
            }
        else if (fLength < (oInj.fLOxPostLength + fBottomConeLength)) {
            return oInj.fLOxPostFluidRad + (fLength-oInj.fLOxPostLength)*(oInj.fLOxSwirlMflFluidRad-oInj.fLOxPostFluidRad)/fBottomConeLength;
            }
        else if (fLength < (oInj.fLOxPostLength + fBottomConeLength + fStraightLength)) {
            return oInj.fLOxSwirlMflFluidRad;
            }
        else
        {
            return oInj.fLOxSwirlMflFluidRad - ((fLength-(oInj.fLOxPostLength + fBottomConeLength + fStraightLength))*
                    oInj.fLOxSwirlMflFluidRad/fTopConeLength);
        }
    }

    public class ImplicitParaboloid : IImplicit
    {
        protected float m_fA;
        protected float m_fB;
        protected float m_fThickness;
        public ImplicitParaboloid(float fA, float fB, float fThickness)
        {
            m_fA = fA;
            m_fB = fB;
            m_fThickness = fThickness;
        }

        public float fSignedDistance(in Vector3 vecPt)
        {
            float dX = vecPt.X;
            float dY = vecPt.Y;
            float dZ = vecPt.Z;

            float fDist = dX*dX/(m_fA*m_fA) + dY*dY/(m_fB*m_fB) - dZ;

            // add thickness
            return (float)(Math.Abs(fDist) - 0.5f * m_fThickness);  // note not ACTUAL thickness! change dis l8r
        }
    }

    public Voxels voxConstruct()
    {
        
        // messin around with implicit parabola thing
        IImplicit sdfCone = new ImplicitParaboloid(1f, 1f, 1f);
        BBox3 oBBox = new BBox3(new Vector3(-10f,-10f,-10f), new Vector3(10f, 10f, 10f));

        Voxels voxCone = new Voxels(sdfCone, oBBox);

        return voxCone;

        Voxels voxLOxManifold = new Voxels();

        List<Vector3> aInjectorLocations = InjectorPattern(oInj.aElementCount, oInj.aElementRadii, oInj.aElementClocking);
        foreach(Vector3 vecLocation in aInjectorLocations)
        {
            Sh.PreviewFrame(new LocalFrame(vecLocation), 10f);
            voxLOxManifold += LOxSwirlCore(new LocalFrame(vecLocation));
        }

        return voxLOxManifold;
    }
}