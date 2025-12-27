using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using br;
using Leap71.ShapeKernel;
using PicoGK;
using static System.MathF;

public class Injector
{

    // MARK: Configs
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
        public float fInnerOringPadW {get; set; }
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
        public float fFlangeOR;
        public float fBoltMR;
        public float fBoltRadius;
        public float fBoltClearanceRadius;
        public int iBoltCount;
        public float fBoltCBoreDepth;
        public float fBoltCBoreWidth;
        public float fNutVertexWidth;
        public float fFlangeThickness;
        public float fWasherRadius;
        
        // constructor: initialise what we already can
        public InterfacesConfig(SimConfig s)
        {
            // TODO: replace placeholder values (InterfacesConfig)
            fCoolingChannelWidth = 3f; // should be a little larger than the chamber channel width

            //calculate inner o-ring position
            fInnerOringIR = s.eng.fChamberRadius + 2f; // placeholder clearance for now - need calcs
            fInnerOringOR = fInnerOringIR + s.inj.fInnerOringW;
            fInnerOringDepth = s.inj.fInnerOringDepth; // should probs not be in two place

            float fOuterAnnulusBoundingRad = s.inj.aElementRadii[^1] + s.inj.fFuelAnnulusOR;
            float fFilmCoolingBoundingRad = s.inj.aFilmHoleRadii[^1] + s.inj.aFilmHoleRadii[^1];
            fCoolingChannelIR = fInnerOringOR + 2f; // yet another placeholder offset
            fCoolingChannelMR = fCoolingChannelIR + 0.5f*fCoolingChannelWidth;
            fCoolingChannelOR = fCoolingChannelIR + fCoolingChannelWidth;

            fOuterOringIR = fCoolingChannelOR + 2f; // placeholder offset
            fOuterOringOR = fOuterOringIR + s.inj.fOuterOringW;
            fOuterOringDepth = s.inj.fOuterOringDepth;

            fFlangeOR = fOuterOringOR + 2f;
            fBoltRadius = 5f/2;
            fBoltClearanceRadius = 5.3f/2;
            iBoltCount = 8;

            fBoltMR = fFlangeOR + fBoltRadius + 2f;
            fBoltCBoreDepth = 5f;
            fBoltCBoreWidth = 10f;
            fFlangeThickness = fBoltCBoreDepth + 5f;

            fWasherRadius = 9f/2;
        }
    }

    public static Voxels maker()
    {
        // placeholder task fn for just the injector
        Injector oCoaxShearInjector = new Injector();
        Voxels voxCoaxShearInjector = oCoaxShearInjector.voxConstruct();
        Sh.PreviewVoxels(voxCoaxShearInjector,
                            new PicoGK.ColorFloat("#501f14"),
                            fTransparency: 0.9f,
                            fMetallic: 0.4f,
                            fRoughness: 0.3f);
        return voxCoaxShearInjector;
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

        // remove hole for ignitor (//TODO fix placeholder)
        BaseLens oASIHole = new BaseLens(new LocalFrame(), fPlateThickness, 0, 4.0f);
        voxPlate = voxPlate - oASIHole.voxConstruct();
        return voxPlate;
    }

    public Voxels voxGroovyBaby(LocalFrame oFrame, float fGrooveDepth, float fGrooveIR, float fGrooveOR)
    {
        BasePipe oGroove = new BasePipe(oFrame, fGrooveDepth, fGrooveIR, fGrooveOR);
        return oGroove.voxConstruct();
    }

    public Voxels voxASIPassThrough()
    {
        GPort ASIPort = new GPort("1/4in", 6.35f); // 1/4in OD for SS insert?

        Voxels voxPort = ASIPort.voxConstruct(new LocalFrame(new Vector3(0,0,50)));

        return voxPort; //TODO
    }

    public List<Voxels> voxPorts(ConeRoof oRoof)
    {
        Voxels voxLOXCrop = oRoof.voxUpperCrop();
        Voxels voxIPACrop = oRoof.voxLowerCrop();

        // determine vertical height of ports:  for now use placeholder
        float fPortsHeight = 50f;
        // determine spacing:  ASI in middle, LOX inlet and 3x PT spaced evenly
        float fPortRadialDistance = 0.5f*s.eng.fChamberRadius;

        // create vectors
        LocalFrame aLOXInlet = new LocalFrame(new Vector3(-fPortRadialDistance, 0, fPortsHeight));
        LocalFrame aLOXPT =  new LocalFrame(new Vector3(fPortRadialDistance, 0, fPortsHeight));
        LocalFrame aChamberPT =  new LocalFrame(new Vector3(0, -fPortRadialDistance, fPortsHeight));
        LocalFrame aIPAPT =  new LocalFrame(new Vector3(0, fPortRadialDistance, fPortsHeight));

        GPort oLOXInlet = new GPort("1/2in", 14f);
        BasePipe oLOXInletPipe = new BasePipe(aLOXInlet, -fPortsHeight, 0f, 14f/2);
        Voxels voxLOXInletFluid = oLOXInlet.voxConstruct(aLOXInlet) + oLOXInletPipe.voxConstruct();
        Voxels voxLOXInletWall = voxLOXInletFluid.voxOffset(2f);
        voxLOXInletFluid = voxLOXInletFluid - voxLOXCrop;
        voxLOXInletWall = voxLOXInletWall - voxLOXCrop;

        GPort oLOXPT = new GPort("1/4in", 7.5f);
        BasePipe oLOXPTPipe = new BasePipe(aLOXPT, -fPortsHeight, 0f, 7.5f/2);
        Voxels voxLOXPTFluid = oLOXPT.voxConstruct(aLOXPT) + oLOXPTPipe.voxConstruct();
        Voxels voxLOXPTWall = voxLOXPTFluid.voxOffset(2f);
        voxLOXPTFluid = voxLOXPTFluid - voxLOXCrop;
        voxLOXPTWall = voxLOXPTWall - voxLOXCrop;

        GPort oIPAPT = new GPort("1/4in", 7.5f);
        BasePipe oIPAPTPipe = new BasePipe(aIPAPT, -fPortsHeight, 0f, 7.5f/2);
        Voxels voxIPAPTFluid = oIPAPT.voxConstruct(aIPAPT) + oIPAPTPipe.voxConstruct();
        Voxels voxIPAPTWall = voxIPAPTFluid.voxOffset(2f);
        voxIPAPTFluid = voxIPAPTFluid - voxIPACrop;
        voxIPAPTWall = voxIPAPTWall - voxIPACrop;

        GPort oChamberPT = new GPort("1/8in", 4.5f);
        BasePipe oChamberPTPipe = new BasePipe(aChamberPT, -fPortsHeight, 0f, 7.5f/2);
        Voxels voxChamberPTFluid = oChamberPT.voxConstruct(aChamberPT) + oChamberPTPipe.voxConstruct();
        Voxels voxChamberPTWall = voxChamberPTFluid.voxOffset(2f);

        Sh.PreviewFrame(aLOXInlet, 5f);
        Sh.PreviewFrame(aLOXPT, 5f);
        Sh.PreviewFrame(aChamberPT, 5f);
        Sh.PreviewFrame(aIPAPT, 5f);

        Voxels voxFluids = voxLOXInletFluid + voxLOXPTFluid + voxIPAPTFluid + voxChamberPTFluid;
        Voxels voxWalls = voxLOXInletWall + voxLOXPTWall + voxIPAPTWall + voxChamberPTWall - voxCropBoxZ(fPortsHeight) - voxCropBoxZ(0f, 100f, -10f);

        // return list of voxels:  {wall voxels, fluid voxels}
        return new List<Voxels> {voxWalls, voxFluids};
    }

    public Voxels voxPrisonBarSupports(ConeRoof oRoof, float fZ, float fMiddleRadius, float fRadius, int iNumBars)
    {
        // bar type supports for the manifold dividing wall
        // cylinder for now, make better later (//TODO change bar supports to arc segments)
        float fDeltaPhi = 2*PI / iNumBars;
        Voxels voxBars = new Voxels();
        for (int i=0; i<iNumBars; i++)
        {
            float fPhi = i*fDeltaPhi;
            float fX = fMiddleRadius*Cos(fPhi);
            float fY = fMiddleRadius*Sin(fPhi);
            Vector3 oBarLocation = new Vector3(fX, fY, fZ);
            BasePipe oBar = new BasePipe(new LocalFrame(oBarLocation), 100f, 0f, fRadius);
            voxBars += oBar.voxConstruct();
        }
        voxBars.BoolIntersect(oRoof.voxLowerCrop());
        return voxBars;
    }

    // MARK: ConeRoof
    public class ConeRoof
    {
        // inputsw
        private readonly InjectorConfig Inj;
        private readonly SlmConfig Slm;
        private readonly InterfacesConfig Ifc;
        private readonly List<Vector3> m_aPointsList;
        private readonly Vector3 m_aASILocation;
        private readonly float m_fRoofThickness;

        // outputs

        // constructor
        public ConeRoof(InjectorConfig inj, SlmConfig slm, InterfacesConfig ifc,
                        List<Vector3> aPointsList, Vector3 aASILocation, float fRoofThickness)
        {
            Inj = inj;
            Slm = slm;
            Ifc = ifc;
            m_aPointsList = aPointsList;
            m_aASILocation = aASILocation;
            m_fRoofThickness = fRoofThickness;
        }

        // public query methods
        public Voxels voxUpperCrop()
        {
            BaseLens oCrop = new BaseLens(new LocalFrame(), 1f, 0f, Ifc.fCoolingChannelOR);
            oCrop.SetHeight(new SurfaceModulation(-100f), new SurfaceModulation(fGetAtticLowerHeight));
            return oCrop.voxConstruct();
        }

        public Voxels voxLowerCrop()
        {
            BaseLens oCrop = new BaseLens(new LocalFrame(), 1f, 0f, Ifc.fCoolingChannelOR);
            oCrop.SetHeight(new SurfaceModulation(-100f), new SurfaceModulation(fGetConeRoofLowerHeight));
            return oCrop.voxConstruct();
        }

        public float fOuterBoundingRadius()
        {
            return 0f;
        }

        public float fGetGussetWidth()
        {
            return 0f;
        }

        // vox builder
        public Voxels voxConstruct()
        {
            // returns a roof structure constructed using cones originating at each point in aPointsList

            BaseLens oRoof = new BaseLens(new LocalFrame(), m_fRoofThickness, 0f, Ifc.fCoolingChannelOR);
            BaseLens oLOxCrop = new BaseLens(new LocalFrame(), m_fRoofThickness, 0f, Ifc.fCoolingChannelOR);
            oRoof.SetHeight(new SurfaceModulation(fGetConeRoofLowerHeight), new SurfaceModulation(fGetConeRoofUpperHeight));
            oLOxCrop.SetHeight(new SurfaceModulation(fGetConeRoofUpperHeight), new SurfaceModulation(fGetConeRoofCropHeight));

            Voxels voxRoof = oRoof.voxConstruct();
            Voxels voxLOxCrop = oLOxCrop.voxConstruct();
            Library.Log("Middle roof and crop zone constructed");
            
            // iterate through each injector element and create wall section and fluid section
            foreach (Vector3 aPoint in m_aPointsList)
            {
                BasePipe oInjectorWall = new BasePipe(new LocalFrame(aPoint), Inj.fLOxPostLength,
                                                            0, Inj.fLOxPostFluidRad+Inj.fLOxPostWT);
                BasePipe oInjectorFluid = new BasePipe(new LocalFrame(aPoint), Inj.fLOxPostLength,
                                                            0, Inj.fLOxPostFluidRad);

                voxRoof = voxRoof + oInjectorWall.voxConstruct();
                voxRoof = voxRoof - oInjectorFluid.voxConstruct();
            }
            voxRoof = voxRoof - voxLOxCrop; // crop away stuff
            Library.Log("Injector elements constructed");

            // add top roof (attic)
            BaseLens oAttic = new BaseLens(new LocalFrame(), m_fRoofThickness, 0f, Ifc.fCoolingChannelOR);
            oAttic.SetHeight(new SurfaceModulation(fGetAtticLowerHeight), new SurfaceModulation(fGetAtticUpperHeight));
            Voxels voxAttic = new Voxels(oAttic.voxConstruct());
            float fMaxAtticZ = voxAttic.oCalculateBoundingBox().vecMax.Z;
            float fMaxAtticR = Ifc.fCoolingChannelOR - ((fMaxAtticZ - Inj.fInjectorPlateThickness - Cos(Slm.fPrintAngle)*4*m_fRoofThickness) / Tan(Slm.fPrintAngle));
            Library.Log("Attic constructed");

            // create augmented spark igniter through-port
            BasePipe oASIWall = new BasePipe(new LocalFrame(m_aASILocation), 50f, 6.35f, 10.35f);
            BasePipe oASIFluid = new BasePipe(new LocalFrame(m_aASILocation), 50f, 0, 6.35f);
            Voxels voxOut = voxRoof + voxAttic + oASIWall.voxConstruct() - oASIFluid.voxConstruct();
            Library.Log("ASI port constructed");

            // create flange
            BasePipe oFlangeBase = new BasePipe(new LocalFrame(new Vector3(0f, 0f, -Ifc.fFlangeThickness)),
                                                3*Ifc.fFlangeThickness, Ifc.fCoolingChannelIR/2, Ifc.fFlangeOR);
            Voxels voxBoltClearances = new();
            List<Vector3> aBoltPos = new();
            for (int i=0; i<Ifc.iBoltCount; i++)
            {
                float fAngle = i*(2*PI/Ifc.iBoltCount);
                Vector3 oBoltPos = new Vector3(Ifc.fBoltMR*Cos(fAngle), Ifc.fBoltMR*Sin(fAngle), 0);
                aBoltPos.Add(oBoltPos);
                BasePipe oBoltClearance = new BasePipe(new LocalFrame(oBoltPos+new Vector3(0f,0f,-Ifc.fFlangeThickness)), 
                                            3*Ifc.fFlangeThickness, 0, Ifc.fWasherRadius+2f);
                //TODO: add bridging rectangular prism here
                Sh.PreviewFrame(new LocalFrame(oBoltPos), 5f);
                voxBoltClearances += oBoltClearance.voxConstruct();
            }
            Voxels voxFlange = oFlangeBase.voxConstruct() + voxBoltClearances;
            voxFlange += voxFlange.voxOverOffset(8f); // fillet concave only
            //Sh.PreviewVoxels(voxFlange, Cp.clrRandom(), 0.2f);
            BBox3 oVerticalTrimBox = new BBox3(-2*Ifc.fFlangeOR,-2*Ifc.fFlangeOR, 0f, 2*Ifc.fFlangeOR, 2*Ifc.fFlangeOR, Ifc.fFlangeThickness);
            voxFlange.Trim(oVerticalTrimBox);
            voxFlange -= voxUpperCrop();
            
            voxOut += voxFlange;
            Library.Log("Flange constructed");

            // make gussets
            // create cone shape
            BaseCone oGussetCone = new BaseCone(new LocalFrame(new Vector3(0f, 0f, Ifc.fFlangeThickness)),
                                                fMaxAtticZ-Ifc.fFlangeThickness, Ifc.fBoltMR+2f, fMaxAtticR);
            Voxels voxGussetCone = oGussetCone.voxConstruct();
                
            // create spider web box thing and bolt holes voxel fields
            Voxels voxGussetBox = new();
            Voxels voxBoltHoles = new();
            Voxels voxThrustRods = new();
            Voxels voxStrutHoles = new();
            
            for (int i=0; i<aBoltPos.Count(); i++)
            {
                Vector3 oBoltPos = aBoltPos[i];
                LocalFrame oGussetFrame = new LocalFrame(oBoltPos, Vector3.UnitZ, oBoltPos);
                BaseBox oGussetBox = new BaseBox(oGussetFrame, fMaxAtticZ, Ifc.fBoltMR*2, 2*(Ifc.fWasherRadius+2f));
                voxGussetBox += oGussetBox.voxConstruct();

                // thrust rods
                if (i%2 == 0)
                {
                    // vertical loft: rectangle -> circle
                    Vector3 vecCast = VecOperations.vecUpdateRadius(oBoltPos, -20f) + new Vector3(0f, 0f, fMaxAtticZ);
                    vecCast = voxGussetCone.vecRayCastToSurface(vecCast, -Vector3.UnitZ);
                    LocalFrame oBottomFrame = new LocalFrame(vecCast).oTranslate(new Vector3(0f, 0f, 0f));
                    float fBottomFrameZ = oBottomFrame.vecGetPosition().Z;
                    LocalFrame oTopFrame = new LocalFrame(new Vector3(oBottomFrame.vecGetPosition().X,
                                                                        oBottomFrame.vecGetPosition().Y,
                                                                        fMaxAtticZ + 15));
                    float fStrutArea = Pow(2f*Ifc.fBoltRadius+4, 2);
                    float fStrutRadius = Sqrt(fStrutArea/PI);
                    Library.Log($"Strut radius = {fStrutRadius}");
                    Rectangle oRectangle = new Rectangle(2*(Ifc.fWasherRadius+2f), 2*(Ifc.fWasherRadius+2f));
                    Circle oCircle = new Circle(fStrutRadius);
                    float fLoftHeight = oTopFrame.vecGetPosition().Z - oBottomFrame.vecGetPosition().Z;
                    BaseLoft oStrut = new BaseLoft(oBottomFrame, oRectangle, oCircle, fLoftHeight);
                    BaseBox oBridgeBox = new BaseBox(oBottomFrame, -fBottomFrameZ, 2*(Ifc.fWasherRadius+2f), 2*(Ifc.fWasherRadius+2f));
                    voxStrutHoles += new BaseCylinder(oTopFrame, -10f, 3f).voxConstruct();
                    voxThrustRods += new Voxels(oStrut.mshConstruct()) + new Voxels(oBridgeBox.mshConstruct());
                }

                // bolt holes
                BasePipe oBoltHole = new BasePipe(new LocalFrame(oBoltPos), 2*Ifc.fFlangeThickness, 0f, Ifc.fBoltClearanceRadius);
                BasePipe oWasherHole = new BasePipe(new LocalFrame(oBoltPos+new Vector3(0f,0f,Ifc.fFlangeThickness)),
                                                                                2*Ifc.fFlangeThickness, 0f, Ifc.fWasherRadius+0.5f);
                voxBoltHoles += oBoltHole.voxConstruct() + oWasherHole.voxConstruct() + voxStrutHoles;
            }
            Library.Log("Flange details (gussets etc.) created");
 

            // booleans
            Voxels voxInnerPipeCrop = new BasePipe(new LocalFrame(), 2*fMaxAtticZ, 0f, fMaxAtticR).voxConstruct();
            Voxels voxGusset = (voxGussetCone & voxGussetBox) + voxThrustRods - voxUpperCrop() - voxInnerPipeCrop;

            voxOut += voxGusset;
            Library.Log("Flange details (gussets etc.) added");

            

            Voxels voxCropZone = voxOut - voxUpperCrop() - voxInnerPipeCrop;
            Voxels voxNoCropZone = voxOut & (voxUpperCrop() + voxInnerPipeCrop);
            voxCropZone.OverOffset(5f);
            Library.Log("Filleting completed");

            voxOut = voxCropZone + voxNoCropZone;
            voxOut -= voxBoltHoles;
            
            return voxOut;
        }
                
        // private helpers
        private float fGetConeRoofSurfaceModulation(float fPhi, float fLengthRatio)
        {
            float fRadius = fLengthRatio * Ifc.fCoolingChannelOR;
            float fZVal = (Ifc.fCoolingChannelOR - fRadius)*Tan(Slm.fPrintAngle) + Inj.fInjectorPlateThickness;  // outer wall case
            // calculate vertical offset required (for now use cone mid-plane)
            float fConeOffset = Inj.fLOxPostLength - (Inj.fLOxPostFluidRad+Inj.fLOxPostWT)*Tan(Slm.fPrintAngle);
            List<Vector3> aAllPoints = new List<Vector3>(m_aPointsList) { m_aASILocation };

            foreach (Vector3 aPoint in aAllPoints)
            {
                float fPointPhi = Atan2(aPoint.Y, aPoint.X);
                float fPointRad = Sqrt((aPoint.X*aPoint.X)+(aPoint.Y*aPoint.Y));

                float fTrialZ = Sqrt((fRadius*fRadius)+(fPointRad*fPointRad)-(2*fRadius*fPointRad*Cos(fPhi-fPointPhi)))*Tan(Slm.fPrintAngle) + fConeOffset;
                
                if (float.IsNaN(fZVal)) {fZVal = fTrialZ;}
                else if (fTrialZ < fZVal){fZVal = fTrialZ;}
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

        private float fGetAtticSurfaceModulation(float fPhi, float fLengthRatio)
        {
            float fRadius = fLengthRatio * Ifc.fCoolingChannelOR;
            float fInnerWallZ = Sqrt(fRadius*fRadius)*Tan(Slm.fPrintAngle) + 3f; // TODO: fix magic number
            float fOuterWallZ = (Ifc.fCoolingChannelOR - fRadius)*Tan(Slm.fPrintAngle) + Inj.fInjectorPlateThickness;
            return Min(fInnerWallZ, fOuterWallZ);
        }
        private float fGetAtticLowerHeight(float fPhi, float fLengthRatio)
        {
            return fGetAtticSurfaceModulation(fPhi, fLengthRatio);
        }
        private float fGetAtticUpperHeight(float fPhi, float fLengthRatio)
        {
            return fGetAtticSurfaceModulation(fPhi, fLengthRatio) + Cos(Slm.fPrintAngle)*4*m_fRoofThickness; //TODO:fix magic number attic WT
        }
    }

    // MARK: GPort
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

    public Voxels voxCropBoxZ(float fZ, float fR=100, float fH=100)
    {
        BaseCylinder oCropCyl = new BaseCylinder(new LocalFrame(new Vector3(0f, 0f, fZ)), fH, fR);
        return oCropCyl.voxConstruct();
    }

    protected Voxels voxBoltFlange()
    {

        return new();
    }

    // MARK: voxConstruct
    public Voxels voxConstruct()
    {
        List<Vector3> aInjectorLocations = InjectorPattern(s.inj.aElementCount, s.inj.aElementRadii, s.inj.aElementClocking);
        List<Vector3> aFilmCoolingLocations = InjectorPattern(s.inj.aFilmHoleCount, s.inj.aFilmHoleRadii, s.inj.aFilmHoleClocking);

        Sh.PreviewCylinderWireframe(new BaseCylinder(new LocalFrame(), -50, i.fCoolingChannelIR), Cp.clrBlack);
        Sh.PreviewCylinderWireframe(new BaseCylinder(new LocalFrame(), -50, i.fCoolingChannelOR), Cp.clrBlack);
        Sh.PreviewCylinderWireframe(new BaseCylinder(new LocalFrame(), -50, s.eng.fChamberRadius), Cp.clrBlack);

        Sh.PreviewPointCloud(aInjectorLocations, 1f, Cp.clrGreen);
        Sh.PreviewPointCloud(aFilmCoolingLocations, 1f, Cp.clrLavender);

        ConeRoof oDividingWall = new ConeRoof(s.inj, s.slm, i, aInjectorLocations, new Vector3(), 2f);
        Voxels voxDividingWall = oDividingWall.voxConstruct();

        Voxels voxFacePlate = new Voxels(voxInjectorPlate(aInjectorLocations, aFilmCoolingLocations, 0.5f,
                                                            s.inj.fFuelAnnulusOR, s.inj.fInjectorPlateThickness, i.fCoolingChannelOR));
        Voxels voxInnerOringGroove = new Voxels(voxGroovyBaby(new LocalFrame(), i.fInnerOringDepth, i.fInnerOringIR, i.fInnerOringOR));
        Voxels voxOuterOringGroove = new Voxels(voxGroovyBaby(new LocalFrame(), i.fOuterOringDepth, i.fOuterOringIR, i.fOuterOringOR));
        Voxels voxRegenSlot = new Voxels(voxGroovyBaby(new LocalFrame(), s.inj.fInjectorPlateThickness, i.fCoolingChannelIR, i.fCoolingChannelOR));

        Voxels voxGrooves = voxInnerOringGroove + voxOuterOringGroove + voxRegenSlot;

        Voxels voxInletsWalls = voxPorts(oDividingWall)[0];
        Voxels voxInletsFluids = voxPorts(oDividingWall)[1];
        Library.Log("Ports constructed");

        Voxels voxBars = voxPrisonBarSupports(oDividingWall, 2, 40, 1, 40);
        Library.Log("Prison bars constructed");

        Voxels voxOutput = voxDividingWall + voxFacePlate - voxGrooves + voxInletsWalls - voxInletsFluids + voxBars;
        BBox3 bounds = voxOutput.oCalculateBoundingBox();
        if (bounds.vecMin.X < 0f) {
            bounds.vecMin.X = 0f;
            Voxels box = new(PicoGK.Utils.mshCreateCube(bounds));
            voxOutput = voxOutput.voxBoolIntersect(box);
        }

        // PolySliceStack oStack = voxOutput.oVectorize(1f, true);
        // oStack.AddToViewer(Library.oViewer());
        // PolySlice oSlice = oStack.oSliceAt(2);
        // string strFilename = Br.fromroot($"exports/slice.svg");
        // oSlice.SaveToSvgFile(strFilename, false);

        return voxOutput;
    }       
}
