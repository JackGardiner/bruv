using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;
using Leap71.ShapeKernel;
using PicoGK;
using static System.MathF;

public class Injector
{

    SimConfig s;
    InjectorConfig inj;
    SlmConfig slm;
    EngineConfig eng;
    MaterialConfig mat;

    public class SimConfig
    {
        public required InjectorConfig inj { get; set; }
        public required SlmConfig slm { get; set; }
        public required EngineConfig eng { get; set; }
        public required MaterialConfig mat { get; set; }
    }

    public class InjectorConfig
    {
        public required List<int> aElementCount { get; set; }
        public required List<float> aElementRadii { get; set; }
        public required List<float> aElementClocking { get; set; }
        public required List<int> aFilmHoleCount { get; set; }
        public required List<float> aFilmHoleRadii { get; set; }
        public required List<float> aFilmHoleClocking { get; set; }

        public float fLOxPostFluidRad { get; set; }
        public float fLOxPostLength { get; set; }
        public float fLOxSwirlMflFluidRad { get; set; }
        public float fLOxSwirlMfldAngleDeg { get; set; }
        public float fLOxSwirlMfldLength { get; set; }
        public float fLOxPostWT { get; set; }
        public float fInjectorPlateThickness { get; set; }
        public float FuelAnnulusOR { get; set; }
    }

    public class SlmConfig
    {
        public float fPrintAngle { get; set; }
    }

    public class EngineConfig
    {
        // Currently empty in JSON, add fields here later, e.g.:
        // public float fChamberPressure { get; set; }
        // public float fThroatDiameter { get; set; }
    }

    public class MaterialConfig
    {
        public required string sName { get; set; }

        public float fTensileStrengthHorizontal { get; set; }
        public float fTensileStrengthVertical { get; set; }

        public float fYieldStrengthHorizontal { get; set; }
        public float fYieldStrengthVertical { get; set; }

        public float fElongationAtBreakHorizontal { get; set; }
        public float fElongationAtBreakVertical { get; set; }

        public float fHardnessVickers { get; set; }

        public float fRoughnessAverageRa { get; set; }
        public float fMeanRoughnessDepthRz { get; set; }

        public float fElectricalConductivity { get; set; }
    }


    public static void Task()
    {
        // placeholder task fn for just the injector
        Injector oCoaxShearInjector = new Injector();
        Voxels voxCoaxShearInjector = oCoaxShearInjector.voxConstruct();
        Sh.PreviewVoxels(voxCoaxShearInjector,
                            new PicoGK.ColorFloat("#501f14"),
                            fTransparency: 0.9f,
                            fMetallic: 0.4f,
                            fRoughness: 0.3f);

        // Sh.ExportVoxelsToSTLFile(voxCoaxShearInjector, Path.Combine(Utils.strProjectRootFolder(), "exports/injector.stl"));
    }

    public Injector()
    {
    s = JsonLoader.LoadFromJsonFile<SimConfig>(Path.Combine(Utils.strProjectRootFolder(), "src/config.json"));
    }

    List<float> aEmptyList = new();

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
        BaseCylinder oLOxSwirlFluid = new BaseCylinder(oInjectorLocation, s.inj.fLOxPostLength + s.inj.fLOxSwirlMfldLength);
        oLOxSwirlFluid.SetRadius(new SurfaceModulation(new LineModulation(fGetLOxSwirlMfldLineModulation)));
        Voxels voxLOxSwirlFluid = oLOxSwirlFluid.voxConstruct();
        BBox3 oCropBox = voxLOxSwirlFluid.oCalculateBoundingBox();
        oCropBox.vecMin.X -= 2*s.inj.fLOxPostWT; oCropBox.vecMin.Y -= 2*s.inj.fLOxPostWT; 
        oCropBox.vecMax.X += 2*s.inj.fLOxPostWT; oCropBox.vecMax.Y += 2*s.inj.fLOxPostWT;
        oCropBox.vecMax.Z = 2*(s.inj.fLOxPostLength + s.inj.fLOxSwirlMfldLength);
        Voxels voxLOxSwirlWall = voxLOxSwirlFluid.voxOffset(s.inj.fLOxPostWT) - voxLOxSwirlFluid;
        voxLOxSwirlWall.Trim(oCropBox);

        return voxLOxSwirlWall;
    }

    public Voxels voxInjectorPlate(List<Vector3> aInjElements, List<Vector3> aFilmElements, float fFilmCoolingHoleRadius,
                                    float fAnnulusRadius, float fPlateThickness, float fOuterRadius,
                                    float fInnerORRadius, float fInnerORWidth, float fInnerORDepth,
                                    float fOuterORRadius, float fOuterORWidth, float fOuterORDepth)
    {
        // generate initial base plate
        BaseLens oPlate = new BaseLens(new LocalFrame(), fPlateThickness, 0, fOuterRadius);

        Voxels voxPlate = oPlate.voxConstruct();
        // remove holes to create IPA annulus
        foreach (Vector3 aElement in aInjElements)
        {
            BaseLens oHole = new BaseLens(new LocalFrame(aElement), fPlateThickness, 0, fAnnulusRadius);
            voxPlate = voxPlate - oHole.voxConstruct();
        }

        // remove holes to create film cooling (IPA)
        foreach (Vector3 aFilmHole in aFilmElements)
        {
            BaseLens oHole = new BaseLens(new LocalFrame(aFilmHole), fPlateThickness, 0, fFilmCoolingHoleRadius);
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
            BasePipe oInjectorWall = new BasePipe(new LocalFrame(aPoint), s.inj.fLOxPostLength,
                                                        0, s.inj.fLOxPostFluidRad+s.inj.fLOxPostWT);
            BasePipe oInjectorFluid = new BasePipe(new LocalFrame(aPoint), s.inj.fLOxPostLength,
                                                        0, s.inj.fLOxPostFluidRad);

            voxRoof = voxRoof + oInjectorWall.voxConstruct();
            voxRoof = voxRoof - oInjectorFluid.voxConstruct();
            voxRoof = voxRoof - voxLOxCrop;
        }

        float fGetConeRoofSurfaceModulation(float fPhi, float fLengthRatio)
        {
            float fRadius = fLengthRatio * fOuterRadius;
            float fZVal = float.NaN;
            // calculate vertical offset required (for now use cone mid-plane)
            float fConeOffset = s.inj.fLOxPostLength - (s.inj.fLOxPostFluidRad+s.inj.fLOxPostWT)*Tan(s.slm.fPrintAngle);
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
        // calculate bottom cone section length (angle = s.inj.fLOxSwirlMfldAngleDeg)
        // calculate top cone section length (angle = 45deg)
        float fTopConeLength = s.inj.fLOxSwirlMflFluidRad * Tan(45f*(PI/180f)); // remove AM constraint magic num.
        float fBottomConeLength = (s.inj.fLOxSwirlMflFluidRad-s.inj.fLOxPostFluidRad) * Tan(s.inj.fLOxSwirlMfldAngleDeg*(PI/180f));
        float fStraightLength = s.inj.fLOxSwirlMfldLength - fTopConeLength - fBottomConeLength;

        float fLength = (s.inj.fLOxPostLength + s.inj.fLOxSwirlMfldLength) * fLengthRatio;

        if (fLength < s.inj.fLOxPostLength) {
            return s.inj.fLOxPostFluidRad;
            }
        else if (fLength < (s.inj.fLOxPostLength + fBottomConeLength)) {
            return s.inj.fLOxPostFluidRad + (fLength-s.inj.fLOxPostLength)*(s.inj.fLOxSwirlMflFluidRad-s.inj.fLOxPostFluidRad)/fBottomConeLength;
            }
        else if (fLength < (s.inj.fLOxPostLength + fBottomConeLength + fStraightLength)) {
            return s.inj.fLOxSwirlMflFluidRad;
            }
        else
        {
            return s.inj.fLOxSwirlMflFluidRad - ((fLength-(s.inj.fLOxPostLength + fBottomConeLength + fStraightLength))*
                    s.inj.fLOxSwirlMflFluidRad/fTopConeLength);
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

        List<Vector3> aInjectorLocations = InjectorPattern(s.inj.aElementCount, s.inj.aElementRadii, s.inj.aElementClocking);
        List<Vector3> aFilmCoolingLocations = InjectorPattern(s.inj.aFilmHoleCount, s.inj.aFilmHoleRadii, s.inj.aFilmHoleClocking);
        // foreach(Vector3 vecLocation in aInjectorLocations)
        // {
        //     Sh.PreviewFrame(new LocalFrame(vecLocation), 10f);
        //     voxLOxManifold += voxLOxSwirlCore(new LocalFrame(vecLocation));
        // }

        Voxels voxDividingWall = new Voxels(voxConeRoof(aInjectorLocations, 1f, PI/4, 40f));
        Voxels voxFacePlate = new Voxels(voxInjectorPlate(aInjectorLocations, aFilmCoolingLocations, 0.5f,
                                                            s.inj.FuelAnnulusOR, s.inj.fInjectorPlateThickness, 50f,
                                                            1f, 1f, 1f, 1f, 1f, 1f));

        Voxels voxOutput = voxDividingWall + voxFacePlate;

        // cross section
        BaseBox oCrossSection = new BaseBox(new LocalFrame(new Vector3(50f,0f,0f)), 100f, 100f, 100f);
        return voxOutput - oCrossSection.voxConstruct();
    }
}
