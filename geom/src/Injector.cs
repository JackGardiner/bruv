using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;
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
        public float fLOxPostWT {get; set;}
        public float fPrintAngle {get; set;}
        public float fInjectorPlateThickness {get; set;}
        public float FuelAnnulusOR {get; set;}
    } 

    public static void Task()
    {
        // placeholder task fn for just the injector
        Injector oCoaxShearInjector = new Injector();
        Voxels voxCoaxShearInjector = oCoaxShearInjector.voxConstruct();
        Sh.PreviewVoxels(voxCoaxShearInjector, Cp.clrCopper, 1);

        Sh.ExportVoxelsToSTLFile(voxCoaxShearInjector, "exports/cooked-cone.stl");
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

    public Voxels voxLOxSwirlCore(LocalFrame oInjectorLocation)
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
        oCropBox.vecMin.X -= 2*oInj.fLOxPostWT; oCropBox.vecMin.Y -= 2*oInj.fLOxPostWT; 
        oCropBox.vecMax.X += 2*oInj.fLOxPostWT; oCropBox.vecMax.Y += 2*oInj.fLOxPostWT;
        oCropBox.vecMax.Z = 2*(oInj.fLOxPostLength + oInj.fLOxSwirlMfldLength);
        Voxels voxLOxSwirlWall = voxLOxSwirlFluid.voxOffset(oInj.fLOxPostWT) - voxLOxSwirlFluid;
        voxLOxSwirlWall.Trim(oCropBox);

        return voxLOxSwirlWall;
    }

    public Voxels voxInjectorPlate(List<Vector3> aPointsList, float fAnnulusRadius, float fPlateThickness, float fOuterRadius)
    {
        BaseLens oPlate = new BaseLens(new LocalFrame(), fPlateThickness, 0, fOuterRadius);
        Voxels voxPlate = oPlate.voxConstruct();
        foreach (Vector3 aPoint in aPointsList)
        {
            BaseLens oHole = new BaseLens(new LocalFrame(aPoint), fPlateThickness, 0, fAnnulusRadius);
            voxPlate = voxPlate - oHole.voxConstruct();
        }
        return voxPlate;
    }

    public Voxels voxConeRoof(List<Vector3> aPointsList, float fRoofThickness, float fAlpha, float fOuterRadius)
    {
        // returns a roof structure constructed using cones originating at each point in aPointsList
        // in future need to figure out how to bound this, for now using BBox3

        BaseLens oRoof = new BaseLens(new LocalFrame(), fRoofThickness, 0f, fOuterRadius);
        BaseLens oLOxCrop = new BaseLens(new LocalFrame(), fRoofThickness, 0f, fOuterRadius);
        oRoof.SetHeight(new SurfaceModulation(fGetConeRoofLowerHeight), new SurfaceModulation(fGetConeRoofUpperHeight));
        oLOxCrop.SetHeight(new SurfaceModulation(fGetConeRoofUpperHeight), new SurfaceModulation(fGetConeRoofCropHeight));

        Voxels voxRoof = new Voxels(oRoof.voxConstruct());
        Voxels voxLOxCrop = new Voxels(oLOxCrop.voxConstruct());
        
        // iterate through each injector element and create wall section and fluid section
        foreach (Vector3 aPoint in aPointsList)
        {
            BasePipe oInjectorWall = new BasePipe(new LocalFrame(aPoint), oInj.fLOxPostLength,
                                                        0, oInj.fLOxPostFluidRad+oInj.fLOxPostWT);
            BasePipe oInjectorFluid = new BasePipe(new LocalFrame(aPoint), oInj.fLOxPostLength,
                                                        0, oInj.fLOxPostFluidRad);

            voxRoof = voxRoof + oInjectorWall.voxConstruct();
            voxRoof = voxRoof - oInjectorFluid.voxConstruct();
            voxRoof = voxRoof - voxLOxCrop;
        }

        float fGetConeRoofSurfaceModulation(float fPhi, float fLengthRatio)
        {
            float fRadius = fLengthRatio * fOuterRadius;
            float fZVal = float.NaN;
            // calculate vertical offset required (for now use cone mid-plane)
            float fConeOffset = oInj.fLOxPostLength - (oInj.fLOxPostFluidRad+oInj.fLOxPostWT)*Tan(oInj.fPrintAngle);
            foreach (Vector3 aPoint in aPointsList)
            {
                float fPointPhi = Atan2(aPoint.Y, aPoint.X);
                float fPointRad = Sqrt((aPoint.X*aPoint.X)+(aPoint.Y*aPoint.Y));

                float fTrialZ = Sqrt((fRadius*fRadius)+(fPointRad*fPointRad)-(2*fRadius*fPointRad*Cos(fPhi-fPointPhi)))*Tan(fAlpha);

                if(float.IsNaN(fZVal)){fZVal = fTrialZ;}
                else if (fTrialZ < fZVal){fZVal = fTrialZ;}
            }
            return fZVal + fConeOffset;
        }

        float fGetConeRoofUpperHeight(float fPhi, float fLengthRatio)
        {
            return fGetConeRoofSurfaceModulation(fPhi, fLengthRatio) + Cos(fAlpha)*fRoofThickness/2f;
        }

        float fGetConeRoofLowerHeight(float fPhi, float fLengthRatio)
        {
            return fGetConeRoofSurfaceModulation(fPhi, fLengthRatio) - Cos(fAlpha)*fRoofThickness/2f;
        }

        float fGetConeRoofCropHeight(float fPhi, float fLengthRatio)
        {
            return fGetConeRoofSurfaceModulation(fPhi, fLengthRatio) + Cos(fAlpha)*fRoofThickness*2f;
        }

        return voxRoof;
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

public class ImplicitCone : IImplicit
{
    protected float m_fAlpha;     // half angle in radians
    protected float m_fThickness;

    public ImplicitCone(float fAlpha, float fThickness)
    {
        m_fAlpha = fAlpha;
        m_fThickness = fThickness;
    }

    public float fSignedDistance(in Vector3 vecPt)
    {
        float x = vecPt.X;
        float y = vecPt.Y;
        float z = vecPt.Z;

        // Radial distance from axis
        float r = MathF.Sqrt(x * x + y * y);

        // Cone SDF
        float cosA = Cos(m_fAlpha);
        float sinA = Sin(m_fAlpha);
        float d = cosA * r - sinA * z;

        // Apply wall thickness (shell)
        return Abs(d) - m_fThickness * 0.5f;
    }
}



    public Voxels voxConstruct()
    {

        // List<Vector3> aPointsList = [new Vector3(0f,0f,0f), new Vector3(20f, 40f, 0f)];

        // messin around with implicit parabola thing
        // IImplicit sdfCone = new ImplicitCone(PI/4, 1f);
        // BBox3 oBBox = new BBox3(new Vector3(-10f,-10f,-10f), new Vector3(10f, 10f, 10f));

        // Voxels voxCone = new Voxels(sdfCone, oBBox);


        // return voxCone;

        List<Vector3> aInjectorLocations = InjectorPattern(oInj.aElementCount, oInj.aElementRadii, oInj.aElementClocking);
        // foreach(Vector3 vecLocation in aInjectorLocations)
        // {
        //     Sh.PreviewFrame(new LocalFrame(vecLocation), 10f);
        //     voxLOxManifold += voxLOxSwirlCore(new LocalFrame(vecLocation));
        // }

        Voxels voxDividingWall = new Voxels(voxConeRoof(aInjectorLocations, 1f, PI/4, 40f));
        Voxels voxFacePlate = new Voxels(voxInjectorPlate(aInjectorLocations, oInj.FuelAnnulusOR, oInj.fInjectorPlateThickness, 40f));

        Voxels voxOutput = voxDividingWall + voxFacePlate;

        // cross section
        BaseBox oCrossSection = new BaseBox(new LocalFrame(new Vector3(50f,0f,0f)), 100f, 100f, 100f);
        return voxOutput - oCrossSection.voxConstruct();
    }
}