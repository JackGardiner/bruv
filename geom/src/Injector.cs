using System.Numerics;
using static br.Br;
using br;
using Leap71.ShapeKernel;
using PicoGK;
using ports;

public class Injector : TwoPeasInAPod.Pea
{
    public required PartMating pm { get; init; }
    public float Ir_chnl;
    public float Or_chnl;
    protected void initialise_chnl()
    {
        Ir_chnl = pm.Mr_chnl - 0.5f*pm.min_wi_chnl;
        Or_chnl = pm.Mr_chnl + 0.5f*pm.min_wi_chnl;
    }
    // manufacturing vars
    public required float fPrintAngle { get; init; }

    // injector plate vars
    public required List<int> no_inj { get; init; }
    public required List<float> r_inj { get; init; }
    public required List<float> theta0_inj { get; init; }
    public int iTotalElementCount;
    public List<Vector3>? points_inj = null;
    protected void initialise_inj()
    {
        points_inj = new();
        iTotalElementCount = 0;
        for (int n=0; n<numel(no_inj); ++n) {
            List<Vector3> ps = circularly_distributed(
                no:     no_inj[n],
                z:      0f,
                r:      r_inj[n],
                theta0: theta0_inj[n]
            );
            points_inj.AddRange(ps);
            iTotalElementCount += no_inj[n];
        }
    }
    public required float fTargetdPFrac { get; init; }
    public required float fInjectorPlateThickness { get; init; }
    public float fTargetdP;
    public float fLOxPostLength;
    public float fLOxPostWT;
    public float fFuelAnnulusOR;
    public float fOxElementMFR;
    public float fFuelElementMFR;
    public float fCoreExitArea;
    public float fCoreExitRadius;
    public int iOxSwirlInlets;
    public int iFuelSwirlInlets;
    public float fTargetCdOx;
    public float fTargetCdFuel;
    protected void initialise_swirl()
    {
        fOxElementMFR = pm.fOxMassFlowRate / iTotalElementCount;
        fFuelElementMFR = pm.fFuelMassFlowRate / iTotalElementCount;

        fTargetdP = fTargetdPFrac*pm.fChamberPressure;
        fLOxPostLength = 10f;
        fLOxPostWT = 1f;
        fFuelAnnulusOR = 5f;

        iOxSwirlInlets = 4;
        iFuelSwirlInlets = 4;
        fTargetCdOx = 0.25f;
        fTargetCdFuel = 0.25f;
        fCoreExitArea = fOxElementMFR/(fTargetCdOx*sqrt(2*pm.fOxInjectionRho*fTargetdP));
        // A = PI*r^2
        // r = sqrt(A/pi)
        fCoreExitRadius = sqrt(fCoreExitArea/PI) * 1e3f; // mm(!)
    }

    // film cooling vars
    public required float Or_fc { get; init; }
    public required List<int> no_fc { get; init; }
    public required List<float> r_fc { get; init; }
    public required List<float> theta0_fc { get; init; }
    public List<Vector3>? points_fc = null;
    protected void initialise_fc()
    {
        points_fc = new();
        for (int n=0; n<numel(no_fc); ++n) {
            List<Vector3> ps = circularly_distributed(
                no:     no_fc[n],
                z:      0f,
                r:      r_fc[n],
                theta0: theta0_fc[n]
            );
            points_fc.AddRange(ps);
        }
    }

    // construction vars
    public float fMaxAtticR;
    public float fMaxAtticZ;
    List<Vector3>? points_bolts = null;
    protected void initialise_bolts()
    {
        points_bolts = circularly_distributed(pm.no_bolt, 0f, pm.Mr_bolt, 0f);
    }
    public Voxels? voxInnerPipeCrop;
    public required float fRoofThickness {get; init; }

    protected List<Vector3> circularly_distributed(int no, float z, float r,
            float theta0) {
        List<Vector3> points = new();
        for (int i=0; i<no; ++i)
            points.Add(tocart(r, theta0 + i*TWOPI/no, z));
        return points;
    }

    protected Voxels voxels_injector_plate()
    {
        // generate initial base plate
        Voxels voxPlate = new BaseLens(
            new LocalFrame(),
            fInjectorPlateThickness,
            0,
            Or_chnl
        ).voxConstruct();

        // remove holes to create IPA annulus
        if (points_inj != null)
        {
            foreach (Vector3 aElement in points_inj)
            {
                voxPlate -= new BaseCylinder(
                    new LocalFrame(aElement),
                    fInjectorPlateThickness,
                    fFuelAnnulusOR).voxConstruct();
            }
        }

        // remove holes to create film cooling (IPA)
        if (points_fc != null)
        {
            foreach (Vector3 aFilmHole in points_fc)
            {
                voxPlate -= new BaseCylinder(
                    new LocalFrame(aFilmHole),
                    fInjectorPlateThickness,
                    Or_fc).voxConstruct();
            }
        }

        // remove hole for ignitor (//TODO fix placeholder)
        voxPlate -= new BaseCylinder(
            new LocalFrame(),
            fInjectorPlateThickness,
            4.0f
        ).voxConstruct();

        return voxPlate;
    }

    // coneroof helper methods
    // TODO: make these const. vox vars
    public Voxels cone_roof_upper_crop()
    {
        BaseLens oCrop = new BaseLens(new LocalFrame(), 1f, 0f, Or_chnl);
        oCrop.SetHeight(new SurfaceModulation(-100f), new SurfaceModulation(fGetAtticLowerHeight));
        return oCrop.voxConstruct();
    }

    public Voxels cone_roof_lower_crop()
    {
        BaseLens oCrop = new BaseLens(new LocalFrame(), 1f, 0f, Or_chnl);
        oCrop.SetHeight(new SurfaceModulation(-100f), new SurfaceModulation(fGetConeRoofLowerHeight));
        return oCrop.voxConstruct();
    }

    public Voxels cone_roof_lox_crop()
    {
        BaseLens oCrop = new BaseLens(new LocalFrame(), 1f, 0f, Or_chnl);
        oCrop.SetHeight(new SurfaceModulation(fGetConeRoofUpperHeight), new SurfaceModulation(fGetAtticLowerHeight));
        return oCrop.voxConstruct();
    }

    public Voxels voxels_cone_roof()
    {
        // structure constructed using cones originating at each point in aPointsList

        BaseLens oRoof = new BaseLens(
            new LocalFrame(),
            fRoofThickness,
            0f,
            Or_chnl
        );

        oRoof.SetHeight(
            new SurfaceModulation(fGetConeRoofLowerHeight),
            new SurfaceModulation(fGetConeRoofUpperHeight)
            );

        return oRoof.voxConstruct();
    }

    public Voxels vox_inj_elements(out Voxels voxOxPostFluid)
    {
        // iterate through each injector element and create wall section and fluid section
        Voxels voxOxPostWall = new();
        Voxels voxSwirlChamber = new();
        Voxels voxShearChamber = new();
        voxOxPostFluid = new();

        foreach (Vector3 aPoint in points_inj!)
        {
            voxOxPostWall += new BasePipe(
                new LocalFrame(aPoint),
                fLOxPostLength,
                0,
                fCoreExitRadius+fLOxPostWT
            ).voxConstruct();

            voxOxPostFluid += new BasePipe(
                new LocalFrame(aPoint),
                fLOxPostLength,
                0,
                fCoreExitRadius
            ).voxConstruct();

            float fSwirlChamberStraightHeight = 12f;
            float fSwirlChamberRadius = 4f;

            // build upper/inner swirl chamber
            // calculate upper wall height
            float fSwirlChamberLowerBound = fGetConeRoofUpperHeight(
                atan2(aPoint.Y, aPoint.X),
                (aPoint.R()+fSwirlChamberRadius)/Or_chnl
                );
            LocalFrame oSwirlChamberFrame = new LocalFrame(aPoint + new Vector3(0f, 0f, fSwirlChamberLowerBound));

            Voxels voxNextSwirlChamber = new(); // temp so we don't cook old one w offsets
            voxNextSwirlChamber += new BasePipe(
                oSwirlChamberFrame,
                fSwirlChamberStraightHeight,
                0f,
                fSwirlChamberRadius
                ).voxConstruct();

            voxNextSwirlChamber += new BaseCone(
                oSwirlChamberFrame.oTranslate(new Vector3(0f, 0f, fSwirlChamberStraightHeight)),
                fSwirlChamberRadius,
                fSwirlChamberRadius,
                0
            ).voxConstruct();

            voxNextSwirlChamber += new BasePipe(
                oSwirlChamberFrame,
                -fSwirlChamberStraightHeight,
                0f,
                fSwirlChamberRadius
            ).voxConstruct();

            voxNextSwirlChamber = voxNextSwirlChamber.voxOffset(1.5f) - voxNextSwirlChamber;
            voxSwirlChamber += voxNextSwirlChamber;

            // build lower/outer shear chamber
            float fShearChamberUpperBound = fGetConeRoofLowerHeight(
                atan2(aPoint.Y, aPoint.X),
                aPoint.R()/Or_chnl
            );
            LocalFrame oShearChamberFrame = new LocalFrame(aPoint);
            voxShearChamber += new BasePipe(
                oShearChamberFrame,
                2*fShearChamberUpperBound,
                fFuelAnnulusOR,
                fFuelAnnulusOR+1f
            ).voxConstruct();

        }
        voxSwirlChamber -= cone_roof_lower_crop();
        voxShearChamber -= cone_roof_lox_crop();
        return voxOxPostWall + voxSwirlChamber + voxShearChamber;
    }

    public Voxels voxels_attic()
    {
        // add top roof (attic)
        BaseLens oAttic = new BaseLens(
            new LocalFrame(),
            fRoofThickness,
            0f,
            Or_chnl
        );
        oAttic.SetHeight(new SurfaceModulation(fGetAtticLowerHeight), new SurfaceModulation(fGetAtticUpperHeight));
        Voxels voxAttic = new Voxels(oAttic.voxConstruct());
        fMaxAtticZ = voxAttic.oCalculateBoundingBox().vecMax.Z;
        fMaxAtticR = Or_chnl
            - ((fMaxAtticZ - fInjectorPlateThickness - cos(fPrintAngle)*4*fRoofThickness)
            / tan(fPrintAngle));

        return voxAttic;
    }

    public Voxels voxels_asi(out Voxels oASIFluid)
    {
        // create augmented spark igniter through-port
        GPort ASIPort = new GPort("1/4in", 6.35f); // 1/4in OD for SS insert?
        Voxels voxPort = ASIPort.voxConstruct(new LocalFrame(new Vector3(0,0,50)));

        BasePipe oASIWall = new BasePipe(new LocalFrame(), 50f, 6.35f, 10.35f);
        oASIFluid = new BasePipe(new LocalFrame(), 50f, 0, 6.35f).voxConstruct();

        return voxPort + oASIWall.voxConstruct();
    }

    public Voxels voxels_flange()
    {
        // create flange
        BasePipe oFlangeBase = new BasePipe(
            new LocalFrame(new Vector3(0f, 0f, -pm.inj_flange_thickness)),
            3*pm.inj_flange_thickness,
            Ir_chnl/2,
            pm.flange_outer_radius
        );
        Voxels voxBoltClearances = new();
        List<Vector3> aBoltPos = new();
        for (int i=0; i<pm.no_bolt; i++)
        {
            float fAngle = i*(2*PI/pm.no_bolt);
            Vector3 oBoltPos = new Vector3(pm.Mr_bolt*cos(fAngle), pm.Mr_bolt*sin(fAngle), 0);
            aBoltPos.Add(oBoltPos);
            voxBoltClearances += new BasePipe(
                new LocalFrame(oBoltPos+new Vector3(0f,0f,-pm.inj_flange_thickness)),
                3*pm.inj_flange_thickness,
                0,
                pm.Or_washer+2f
            ).voxConstruct();
            //TODO: add bridging rectangular prism here
        }
        Voxels voxFlange = oFlangeBase.voxConstruct() + voxBoltClearances;
        voxFlange += voxFlange.voxOverOffset(8f); // fillet concave only
        //Sh.PreviewVoxels(voxFlange, Cp.clrRandom(), 0.2f);
        BBox3 oVerticalTrimBox = new BBox3(
            -2*pm.flange_outer_radius,
            -2*pm.flange_outer_radius,
            0f,
            2*pm.flange_outer_radius,
            2*pm.flange_outer_radius,
            pm.inj_flange_thickness
        );
        voxFlange.Trim(oVerticalTrimBox);
        voxFlange -= cone_roof_upper_crop();

        return voxFlange;
    }

    public Voxels voxels_gussets(out Voxels voxStrutHoles)
    {
        // make gussets
        // create cone shape
        BaseCone oGussetCone = new BaseCone(new LocalFrame(new Vector3(0f, 0f, pm.inj_flange_thickness)),
                                            fMaxAtticZ-pm.inj_flange_thickness, pm.Mr_bolt+2f, fMaxAtticR);
        Voxels voxGussetCone = oGussetCone.voxConstruct();
        // create spider web box thing and bolt holes voxel fields
        Voxels voxGussetBox = new();
        Voxels voxBoltHoles = new();
        Voxels voxThrustRods = new();
        voxStrutHoles = new();

        for (int i=0; i<points_bolts!.Count(); i++)
        {
            Vector3 oBoltPos = points_bolts![i];
            LocalFrame oGussetFrame = new LocalFrame(oBoltPos, Vector3.UnitZ, oBoltPos);
            voxGussetBox += new BaseBox(
                oGussetFrame,
                fMaxAtticZ,
                pm.Mr_bolt*2,
                2*(pm.Or_washer+2f)
            ).voxConstruct();

            // thrust rods
            if (i%2 == 1)
            {
                // vertical loft: rectangle -> circle
                Vector3 vecCast = VecOperations.vecUpdateRadius(oBoltPos, -20f)
                    + new Vector3(0f, 0f, fMaxAtticZ);
                vecCast = voxGussetCone.vecRayCastToSurface(vecCast, -Vector3.UnitZ);
                LocalFrame oBottomFrame = new LocalFrame(vecCast).oTranslate(new Vector3(0f, 0f, 0f));
                oBottomFrame = oBottomFrame.oRotate(VecOperations.fGetPhi(vecCast), Vector3.UnitZ);
                float fBottomFrameZ = oBottomFrame.vecGetPosition().Z;

                LocalFrame oTopFrame = new LocalFrame(
                    new Vector3(oBottomFrame.vecGetPosition().X,
                    oBottomFrame.vecGetPosition().Y,
                    fMaxAtticZ + 15)
                );
                float fStrutArea = pow(2f*(pm.Bsz_bolt/2)+4, 2);
                float fStrutRadius = sqrt(fStrutArea/PI);
                Library.Log($"Strut radius = {fStrutRadius}");
                Rectangle oRectangle = new Rectangle(2*(pm.Or_washer+2f), 2*(pm.Or_washer+2f));
                Circle oCircle = new Circle(fStrutRadius);
                float fLoftHeight = oTopFrame.vecGetPosition().Z - oBottomFrame.vecGetPosition().Z;
                BaseLoft oStrut = new BaseLoft(oBottomFrame, oRectangle, oCircle, fLoftHeight);
                BaseBox oBridgeBox = new BaseBox(
                    oBottomFrame.oTranslate(new Vector3(0f, 0f, 0.04f*fLoftHeight)),
                    -fBottomFrameZ,
                    2*(pm.Or_washer+2f),
                    2*(pm.Or_washer+2f)
                );
                voxStrutHoles += new BaseCylinder(oTopFrame, -10f, 3f).voxConstruct();
                voxThrustRods += new Voxels(oStrut.mshConstruct()) + new Voxels(oBridgeBox.mshConstruct());
            }
        }
        Library.Log("Flange details (gussets etc.) created");


        // booleans
        voxInnerPipeCrop = new BasePipe(new LocalFrame(), 2*fMaxAtticZ, 0f, fMaxAtticR).voxConstruct();
        Voxels voxGusset = (voxGussetCone & voxGussetBox)
            + voxThrustRods
            - cone_roof_upper_crop() - voxInnerPipeCrop;

        return voxGusset;
    }

    public Voxels voxels_ports(out Voxels voxFluids)
    {
        Voxels voxLOXCrop = cone_roof_upper_crop();
        Voxels voxIPACrop = cone_roof_lower_crop();

        // determine vertical height of ports:  for now use placeholder
        float fPortsHeight = 54f; // TODO: magic nom
        // determine spacing:  ASI in middle, LOX inlet and 3x PT spaced evenly
        float fPortRadialDistance = 0.8f*pm.Or_cc;

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

        voxFluids = voxLOXInletFluid + voxLOXPTFluid + voxIPAPTFluid + voxChamberPTFluid;
        Voxels voxWalls = voxLOXInletWall + voxLOXPTWall + voxIPAPTWall + voxChamberPTWall
            - voxCropBoxZ(fPortsHeight) - voxCropBoxZ(0f, 100f, -10f);

        return voxWalls;
    }

    private Voxels voxels_bolts()
    {
        Voxels bolt_holes = new();
        for (int i=0; i<pm.no_bolt; i++)
        {
            Vector3 bolt_pos = points_bolts![i];
            BasePipe oBoltHole = new BasePipe(
                new LocalFrame(bolt_pos),
                2*pm.inj_flange_thickness,
                0f,
                pm.Bsz_bolt/2);

            BasePipe oWasherHole = new BasePipe(
                new LocalFrame(bolt_pos+new Vector3(0f,0f,pm.inj_flange_thickness)),
                2*pm.inj_flange_thickness,
                0f,
                pm.Or_washer+0.5f);

            bolt_holes += oBoltHole.voxConstruct() + oWasherHole.voxConstruct();
        }
        return bolt_holes;
    }

    // private helpers

    private Voxels voxCropBoxZ(float fZ, float fR=100, float fH=100)
    {
        BaseCylinder oCropCyl = new BaseCylinder(new LocalFrame(new Vector3(0f, 0f, fZ)), fH, fR);
        return oCropCyl.voxConstruct();
    }
    private float fGetConeRoofSurfaceModulation(float fPhi, float fLengthRatio)
    {
        float fRadius = fLengthRatio * Or_chnl;
        float fZVal = (Or_chnl - fRadius)*tan(fPrintAngle) + fInjectorPlateThickness;  // outer wall case
        // calculate vertical offset required (for now use cone mid-plane)
        float fConeOffset = fLOxPostLength - (fCoreExitRadius+fLOxPostWT)*tan(fPrintAngle);
        List<Vector3> aAllPoints = new List<Vector3>(points_inj!) { new Vector3(0f, 0f, 0f) };

        foreach (Vector3 aPoint in aAllPoints)
        {
            float fPointPhi = atan2(aPoint.Y, aPoint.X);
            float fPointRad = aPoint.R();

            float fTrialZ = sqrt((fRadius*fRadius)+(fPointRad*fPointRad)
                -(2*fRadius*fPointRad*cos(fPhi-fPointPhi)))*tan(fPrintAngle)
                + fConeOffset;
            if (fTrialZ < fZVal) { fZVal = fTrialZ; }
        }

        return fZVal;
    }

    private float fGetConeRoofUpperHeight(float fPhi, float fLengthRatio)
    {
        return fGetConeRoofSurfaceModulation(fPhi, fLengthRatio) + cos(fPrintAngle)*fRoofThickness;
    }

    private float fGetConeRoofLowerHeight(float fPhi, float fLengthRatio)
    {
        return fGetConeRoofSurfaceModulation(fPhi, fLengthRatio);
    }

    private float fGetAtticSurfaceModulation(float fPhi, float fLengthRatio)
    {
        float fRadius = fLengthRatio * Or_chnl;
        float fInnerWallZ = sqrt(fRadius*fRadius)*tan(fPrintAngle) + 3f; // TODO: fix magic number
        float fOuterWallZ = (Or_chnl - fRadius)*tan(fPrintAngle) + fInjectorPlateThickness;
        return min(fInnerWallZ, fOuterWallZ);
    }

    private float fGetAtticLowerHeight(float fPhi, float fLengthRatio)
    {
        return fGetAtticSurfaceModulation(fPhi, fLengthRatio);
    }

    private float fGetAtticUpperHeight(float fPhi, float fLengthRatio)
    {
        return fGetAtticSurfaceModulation(fPhi, fLengthRatio) + cos(fPrintAngle)*4*fRoofThickness; //TODO:fix magic number attic WT
    }

    public Voxels voxels()
    {
        Voxels part = new();

        Geez.Cycle key_part = new();
        Geez.Cycle key_plate = new(colour: COLOUR_CYAN);
        Geez.Cycle key_cone_roof = new(colour: COLOUR_PINK);
        Geez.Cycle key_gusset = new(colour: COLOUR_YELLOW);
        Geez.Cycle key_flange = new(colour: COLOUR_BLUE);
        Geez.Cycle key_asi = new(colour: COLOUR_WHITE);
        Geez.Cycle key_attic = new(colour: COLOUR_GREEN);
        Geez.Cycle key_inj_elements = new(colour: COLOUR_RED);
        Geez.Cycle key_ports = new(colour: Cp.clrRandom());

        Voxels voxInjectorPlate = voxels_injector_plate();
        part += voxInjectorPlate;
        using (key_plate.like())
            key_plate <<= Geez.voxels(voxInjectorPlate);
        Library.Log("created plate.");

        Voxels cone_roof = voxels_cone_roof();
        part += cone_roof;
        using (key_cone_roof.like())
            key_cone_roof <<= Geez.voxels(cone_roof);

        Voxels attic = voxels_attic();
        part += attic;
        using (key_attic.like())
            key_attic <<= Geez.voxels(attic);

        Voxels asi = voxels_asi(out Voxels asi_fluid);
        part += asi;
        part -= asi_fluid;
        using (key_asi.like())
            key_asi <<= Geez.voxels(asi - asi_fluid);

        Voxels flange = voxels_flange();
        part += flange;
        using (key_flange.like())
            key_flange <<= Geez.voxels(flange);

        Voxels gussets = voxels_gussets(out Voxels voxStrutHoles);
        part += gussets;
        using (key_gusset.like())
            key_gusset <<= Geez.voxels(gussets - voxStrutHoles);

        Voxels inj_elements = vox_inj_elements(out Voxels voxOxPostFluid);
        part += inj_elements;
        part -= voxOxPostFluid;
        using (key_inj_elements.like())
            key_inj_elements <<= Geez.voxels(inj_elements);

        Voxels ports = voxels_ports(out Voxels ports_fluids);
        part += ports;
        using (key_ports.like())
            key_ports <<= Geez.voxels(ports-ports_fluids);

        // external fillets
        Voxels voxCropZone = part - cone_roof_upper_crop() - voxInnerPipeCrop!;
        Voxels voxNoCropZone = part & (cone_roof_upper_crop() + voxInnerPipeCrop!);
        voxCropZone.OverOffset(5f);
        Library.Log("Filleting completed");
        part = voxCropZone + voxNoCropZone;

        Voxels bolt_holes = voxels_bolts();
        part -= ports_fluids;
        part -= bolt_holes;
        part -= voxStrutHoles;

        Geez.clear();
        using (key_part.like())
            key_part <<= Geez.voxels(part);


        Library.Log("Baby made.");
        log();

        return part;
    }

    public void drawings(in Voxels part) {
        /* HI JACK do some doodles here. */
    }

    public void anything() {
        /* testing playground if you wish. */
    }

    public string name => "injector";


    public void initialise()
    {
        initialise_inj();
        initialise_swirl();
        initialise_fc();
        initialise_chnl();
        initialise_bolts();
    }

}