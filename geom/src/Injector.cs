using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Runtime.InteropServices;
using Leap71.ShapeKernel;
using PicoGK;
using static System.MathF;

public class Injector
{

    SimConfig s;
    InterfacesConfig i;

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
        public float fFuelAnnulusOR { get; set; }
        public float fInnerOringW {get; set; }
        public float fInnerOringMR {get; set; }
        public float fInnerOringDepth {get; set; }
        public float fOuterOringW {get; set; }
        public float fOuterOringMR {get; set; }
        public float fOuterOringDepth {get; set; }

    }

    public class SlmConfig
    {
        public float fPrintAngle { get; set; }
    }

    public class EngineConfig
    {
        public float fChamberRadius { get; set; }
        // holding space for stu x
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

    public class InterfacesConfig
    {
        public LocalFrame oLOXInlet;
        public LocalFrame oPTLOXInlet;
        public LocalFrame oPTIPAInlet;
        public LocalFrame oPTChamberInlet;
        public LocalFrame oASIInlet;
        public float fInnerOringIR;
        public float fInnerOringMR;
        public float fInnerOringOR;
        public float fInnerOringDepth;
        public float fCoolingChannelMR;
        public float fCoolingChannelWidth;
        public float fCoolingChannelIR;
        public float fCoolingChannelOR;
        public float fOuterOringMR;
        public float fOuterOringIR;
        public float fOuterOringOR;
        public float fOuterOringDepth;
        public float fBoltMR;
        
        // constructor: initialise what we already can
        public InterfacesConfig(SimConfig s)
        {
            fInnerOringMR = s.inj.fInnerOringMR;
            fInnerOringIR = s.inj.fInnerOringMR - s.inj.fInnerOringW/2f;
            fInnerOringOR = s.inj.fInnerOringMR + s.inj.fInnerOringW/2f;
            fInnerOringDepth = s.inj.fInnerOringDepth;

            fOuterOringMR = s.inj.fOuterOringMR;
            fOuterOringIR = s.inj.fOuterOringMR - s.inj.fOuterOringW/2f;
            fOuterOringOR = s.inj.fOuterOringMR + s.inj.fOuterOringW/2f;
            fOuterOringDepth = s.inj.fOuterOringDepth;

            fCoolingChannelMR = 55f; //TODO fix magic number
            fCoolingChannelWidth = 3f; //TODO as above
            fCoolingChannelIR = fCoolingChannelMR - fCoolingChannelWidth/2f;
            fCoolingChannelOR = fCoolingChannelMR + fCoolingChannelWidth/2f;

            fBoltMR = 80f; //TODO as above
        }
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
        Sh.PreviewFrame(new LocalFrame(new Vector3(0f, 56.5f, 0f)), 10f);
    }

    public Injector()
    {
        s = JsonLoader.LoadFromJsonFile<SimConfig>(Path.Combine(Utils.strProjectRootFolder(), "src/config.json"));
        i = new InterfacesConfig(s);
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


    public Voxels voxInjectorPlate(List<Vector3> aInjElements, List<Vector3> aFilmElements, float fFilmCoolingHoleRadius,
                                    float fAnnulusRadius, float fPlateThickness, float fOuterRadius)
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

    public Voxels voxGroovyBaby(LocalFrame oFrame, float fGrooveDepth, float fGrooveIR, float fGrooveOR)
    {
        BasePipe oGroove = new BasePipe(oFrame, fGrooveDepth, fGrooveIR, fGrooveOR);
        return oGroove.voxConstruct();
    }

    public Voxels voxASIPassThrough()
    {
        BSPPPort ASIPort = new BSPPPort("1/4in", 6.35f); // 1/4in OD for SS insert?

        return new Voxels(); //TODO
    }

    public class ConeRoof
    {
        // inputs
        private readonly InjectorConfig Inj;
        private readonly SlmConfig Slm;
        private readonly InterfacesConfig Ifc;
        private readonly List<Vector3> m_aPointsList;
        private readonly float m_fRoofThickness;

        // outputs

        // constructor
        public ConeRoof(InjectorConfig inj, SlmConfig slm, InterfacesConfig ifc,
                        List<Vector3> aPointsList, float fRoofThickness)
        {
            Inj = inj;
            Slm = slm;
            Ifc = ifc;
            m_aPointsList = aPointsList;
            m_fRoofThickness = fRoofThickness;
        }

        // public query methods
        // public Voxels voxUpperCrop()
        // {
        //     BaseLens oCrop = new BaseLens(new LocalFrame(), 0f, 0f,m_fOuterRadius);
        //     oCrop.SetHeight(fConstantSurfaceModulation(0f), )
        //     return new Voxels();
        // }

        public Voxels voxLowerCropVolume()
        {
            return new Voxels();
        }

        // vox builder
        public Voxels voxConstruct()
        {
            // returns a roof structure constructed using cones originating at each point in aPointsList

            BaseLens oRoof = new BaseLens(new LocalFrame(), m_fRoofThickness, 0f, Ifc.fCoolingChannelOR);
            BaseLens oLOxCrop = new BaseLens(new LocalFrame(), m_fRoofThickness, 0f, Ifc.fCoolingChannelOR);
            oRoof.SetHeight(new SurfaceModulation(fGetConeRoofLowerHeight), new SurfaceModulation(fGetConeRoofUpperHeight));
            oLOxCrop.SetHeight(new SurfaceModulation(fGetConeRoofUpperHeight), new SurfaceModulation(fGetConeRoofCropHeight));

            Voxels voxRoof = new Voxels(oRoof.voxConstruct());
            Voxels voxLOxCrop = new Voxels(oLOxCrop.voxConstruct());
            
            // iterate through each injector element and create wall section and fluid section
            foreach (Vector3 aPoint in m_aPointsList)
            {
                BasePipe oInjectorWall = new BasePipe(new LocalFrame(aPoint), Inj.fLOxPostLength,
                                                            0, Inj.fLOxPostFluidRad+Inj.fLOxPostWT);
                BasePipe oInjectorFluid = new BasePipe(new LocalFrame(aPoint), Inj.fLOxPostLength,
                                                            0, Inj.fLOxPostFluidRad);

                voxRoof = voxRoof + oInjectorWall.voxConstruct();
                voxRoof = voxRoof - oInjectorFluid.voxConstruct();
                voxRoof = voxRoof - voxLOxCrop;
            }


            return voxRoof;
        }

        // private helpers
        private float fConstantSurfaceModulation(float fHeight)
        {
            return fHeight;
        }
        private float fGetConeRoofSurfaceModulation(float fPhi, float fLengthRatio)
        {
            float fRadius = fLengthRatio * Ifc.fCoolingChannelOR;
            float fZVal = (Ifc.fCoolingChannelOR - fRadius)*Tan(Slm.fPrintAngle) + Inj.fInjectorPlateThickness;  // outer wall case
            // calculate vertical offset required (for now use cone mid-plane)
            float fConeOffset = Inj.fLOxPostLength - (Inj.fLOxPostFluidRad+Inj.fLOxPostWT)*Tan(Slm.fPrintAngle);
            foreach (Vector3 aPoint in m_aPointsList)
            {
                float fPointPhi = Atan2(aPoint.Y, aPoint.X);
                float fPointRad = Sqrt((aPoint.X*aPoint.X)+(aPoint.Y*aPoint.Y));

                float fTrialZ = Sqrt((fRadius*fRadius)+(fPointRad*fPointRad)-(2*fRadius*fPointRad*Cos(fPhi-fPointPhi)))*Tan(Slm.fPrintAngle) + fConeOffset;

                if (fTrialZ < fZVal){fZVal = fTrialZ;}
            }            
        
            return fZVal;
        }

        private float fGetConeRoofUpperHeight(float fPhi, float fLengthRatio)
        {
            return fGetConeRoofSurfaceModulation(fPhi, fLengthRatio) + Cos(Slm.fPrintAngle)*m_fRoofThickness;
        }

        private float fGetConeRoofLowerHeight(float fPhi, float fLengthRatio)
        {
            return fGetConeRoofSurfaceModulation(fPhi, fLengthRatio);
        }

        private float fGetConeRoofCropHeight(float fPhi, float fLengthRatio)
        {
            return fGetConeRoofSurfaceModulation(fPhi, fLengthRatio) + Cos(Slm.fPrintAngle)*m_fRoofThickness*2f;
        }

    }

    public class BSPPPort
    {
        // inputs
        private readonly string m_sSize;
        private readonly float m_fDownstreamDiameter;
        
        // Geometry Outputs
        private readonly float m_fFaceDiameter;
        private readonly float m_fPilotBoreDiameter;
        private readonly float m_fBoreDepthTotal;

        // Constructor
        public BSPPPort(string sSize, float fDownstreamDiameter)
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
                case "1/4in":
                    fBoreDiam = 13.3f;
                    fRequiredFullThreadLength = 12.4f;
                    fFaceDiam = 20.7f;
                    break;
                case "1/2in":
                    fBoreDiam = 21.1f;
                    fRequiredFullThreadLength = 14.5f;
                    fFaceDiam = 34.0f;
                    break;
                case "3/4in":
                    fBoreDiam = 26.6f;
                    fRequiredFullThreadLength = 16.5f;
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

    public Voxels voxConstruct()
    {

        List<Vector3> aInjectorLocations = InjectorPattern(s.inj.aElementCount, s.inj.aElementRadii, s.inj.aElementClocking);
        List<Vector3> aFilmCoolingLocations = InjectorPattern(s.inj.aFilmHoleCount, s.inj.aFilmHoleRadii, s.inj.aFilmHoleClocking);


        ConeRoof oDividingWall = new ConeRoof(s.inj, s.slm, i, aInjectorLocations, 2f);
        Voxels voxDividingWall = oDividingWall.voxConstruct();

        Voxels voxFacePlate = new Voxels(voxInjectorPlate(aInjectorLocations, aFilmCoolingLocations, 0.5f,
                                                            s.inj.fFuelAnnulusOR, s.inj.fInjectorPlateThickness, i.fBoltMR));
        Voxels voxInnerOringGroove = new Voxels(voxGroovyBaby(new LocalFrame(), i.fInnerOringDepth, i.fInnerOringIR, i.fInnerOringOR));
        Voxels voxOuterOringGroove = new Voxels(voxGroovyBaby(new LocalFrame(), i.fOuterOringDepth, i.fOuterOringIR, i.fOuterOringOR));

        Voxels voxRegenInletGroove = new Voxels(voxGroovyBaby(new LocalFrame(), s.inj.fInjectorPlateThickness, 
                                                i.fCoolingChannelIR, i.fCoolingChannelOR));

        Voxels voxGrooves = voxInnerOringGroove + voxOuterOringGroove + voxRegenInletGroove;

        BSPPPort oASIPort = new BSPPPort("1/4in", 7f);
        BSPPPort oLOXPort = new BSPPPort("1/2in", 15f);
        BSPPPort oPTPort  = new BSPPPort("1/4in", 5f);

        // Voxels voxPorts = oASIPort.voxConstruct();

        Voxels voxOutput = voxDividingWall + voxFacePlate - voxGrooves;// + oASIPort.voxConstruct(new LocalFrame(new Vector3(0f, 0f, 0f)));;
        BBox3 bounds = voxOutput.oCalculateBoundingBox();
        if (bounds.vecMin.X < 0f) {
            bounds.vecMin.X = 0f;
            Voxels box = new(PicoGK.Utils.mshCreateCube(bounds));
            voxOutput = voxOutput.voxBoolIntersect(box);
        }
        return voxOutput;
    }       
    
}
