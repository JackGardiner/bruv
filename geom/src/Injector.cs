using System.Numerics;
using static br.Br;
using br;
using Leap71.ShapeKernel;
using PicoGK;
using ports;

public class Injector : TwoPeasInAPod.Pea
{
    // Initialise the variables
    // coolin channel vars
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
    public int no_annulus_rib;
    public float annulus_rib_width;
    public float area_swinlet_perinj;
    public int no_swinlet;
    public float aspect_swinlet;
    public float rad_offset_swinlet;
    public int iTotalElementCount;
    public List<Vector3>? points_inj = null;
    protected void initialise_inj()
    {
        // injector patterning / overall init
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
        fOxElementMFR = pm.fOxMassFlowRate / iTotalElementCount;
        fFuelElementMFR = pm.fFuelMassFlowRate / iTotalElementCount;
        Library.Log($"total elements count: {iTotalElementCount}");

        // LOX / swirl / central post init
        fLOxPostLength = 10f;
        fLOxPostWT = 0.8f;

        iOxSwirlInlets = 4;
        iFuelSwirlInlets = 4;
        fTargetCdOx = 0.25f;
        // fTargetCdFuel = 0.25f;
        fTargetdP = fTargetdPFrac*pm.fChamberPressure;

        fCoreExitArea = fOxElementMFR/(fTargetCdOx*sqrt(2*pm.fOxInjectionRho*fTargetdP)) * 1e6f; // mm^2
        // A = PI*r^2
        // r = sqrt(A/pi)
        fCoreExitRadius = sqrt(fCoreExitArea/PI);
        Library.Log($"Ox core fluid rad = {fCoreExitRadius}");

        // IPA annulus init
        float min_rib_wid = 0.8f;
        float min_gap_wid = 0.8f;
        float cd_annulus = 0.25f;
        float rib_percent_area = 0.2f;

        float vel_bernoulli_annulus = sqrt(2*fTargetdP/pm.fFuelInjectionRho);
        Library.Log($"v_ideal = {vel_bernoulli_annulus}");
        float area_annulus = fFuelElementMFR/(cd_annulus*sqrt(2*pm.fFuelInjectionRho*fTargetdP)) * 1e6f;
        // float area_annulus = fFuelElementMFR/(pm.fFuelInjectionRho*cd_annulus*vel_bernoulli_annulus) * 1e6f; // mm^2
        Library.Log($"annulus area: {area_annulus}");

        float Or_post = fCoreExitRadius + fLOxPostWT;
        float total_annulus_area = area_annulus * (1+rib_percent_area);
        float total_rib_area = area_annulus*rib_percent_area;

        fFuelAnnulusOR = sqrt(total_annulus_area/PI + pow(Or_post,2));
        Library.Log($"Fuel annulus OR = {fFuelAnnulusOR}");
        Library.Log($"Fuel (annulus) area: {area_annulus} mm^2");
        Library.Log($"Oxidisr (core) area: {fCoreExitArea} mm^2");

        float annulus_width = fFuelAnnulusOR - Or_post;
        Library.Log($"Annulus width: {annulus_width}");

        if (annulus_width > min_gap_wid)
        {
           // calculate max rib count (min width)
            float smallest_rib_area = min_rib_wid*(fFuelAnnulusOR - Or_post);
            no_annulus_rib = (int)ceil(total_rib_area / smallest_rib_area);
            float actual_rib_area = total_rib_area / no_annulus_rib;
            annulus_rib_width = actual_rib_area / (fFuelAnnulusOR - Or_post);
            Library.Log($"{no_annulus_rib} ribs @ {annulus_rib_width} wide");
        } else
        {
            fFuelAnnulusOR = Or_post + min_gap_wid;
            float required_rib_area = PI*(pow(fFuelAnnulusOR,2) - pow(Or_post,2)) - area_annulus;
            float smallest_rib_area = min_rib_wid*(fFuelAnnulusOR - Or_post);

            no_annulus_rib = (int)ceil(required_rib_area / smallest_rib_area);
            float actual_rib_area = required_rib_area / no_annulus_rib;
            annulus_rib_width = actual_rib_area / (fFuelAnnulusOR - Or_post);
            float blockage = required_rib_area / (PI*(pow(fFuelAnnulusOR,2) - pow(Or_post,2)));
            Library.Log($"Gap too small! Gap-bounded solution -> {blockage*100f}% blockage");
            Library.Log($"{no_annulus_rib} ribs @ {annulus_rib_width} wide");
        }

        /*
            segment area (inner) A = 0.5*r^2*(theta-sin(theta))
            rib area = rectangle - inner segment + outer segment
            approximate inner segment == outer segment
        */

        area_swinlet_perinj = 10f; // mm^2
        no_swinlet = 3;
        aspect_swinlet = 5f; // height/width
        rad_offset_swinlet = 2f;
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
    public Voxels? voxInnerPipeCrop;
    public required float fRoofThickness {get; init; }
    protected void initialise_construction()
    {
        points_bolts = circularly_distributed(pm.no_bolt, 0f, pm.Mr_bolt, 0f);
    }


    // voxel builder functions
    protected Voxels voxels_injector_plate()
    {
        // generate initial base plate
        Voxels voxPlate = new BaseLens(
            new LocalFrame(),
            fInjectorPlateThickness,
            0,
            Ir_chnl
        ).voxConstruct();

        // remove holes to create IPA annulus
        if (points_inj != null)
        {
            foreach (Vector3 point in points_inj)
            {
                voxPlate -= new BaseCylinder(
                    new LocalFrame(point),
                    fInjectorPlateThickness,
                    fFuelAnnulusOR
                ).voxConstruct();

                // add ribs
                Frame rib_frame = new Frame(point, uY3).transx(-fInjectorPlateThickness/2);
                Voxels ribs = new();
                for (int i=0; i<no_annulus_rib; i++)
                {
                    ribs += new Cuboid(
                        rib_frame,
                        fInjectorPlateThickness,
                        annulus_rib_width,
                        fFuelAnnulusOR
                    ).voxels();
                    rib_frame = rib_frame.rotyz(TWOPI/no_annulus_rib);
                }
                voxPlate += ribs;
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

        return voxPlate;
    }

    protected Voxels vox_inj_elements(out Voxels voxOxPostFluid, out Voxels swirl_inlets)
    {
        // iterate through each injector element and create wall section and fluid section
        Voxels voxOxPostWall = new();
        Voxels voxSwirlChamber = new();
        Voxels voxShearChamber = new();
        voxOxPostFluid = new();
        swirl_inlets = new();

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

            float fSwirlChamberStraightHeight = 8f;
            float fSwirlChamberRadius = 1.5f * fCoreExitRadius;

            // build upper/inner swirl chamber
            // calculate upper wall height
            float fSwirlChamberLowerBound = fGetConeRoofUpperHeight(
                atan2(aPoint.Y, aPoint.X),
                (aPoint.R()+fSwirlChamberRadius)/Or_chnl
                );
            LocalFrame oSwirlChamberFrame = new LocalFrame(aPoint + new Vector3(0f, 0f, fSwirlChamberLowerBound));
            Frame swirl_chamber_frame = new Frame(aPoint).transz(fSwirlChamberLowerBound);
            // https://www.youtube.com/watch?v=TZtiJN6yiik

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

            // construct tangential swirl inlets
            float sw_a = area_swinlet_perinj / no_swinlet;
            float sw_w = sqrt(sw_a/(aspect_swinlet-0.25f));
            float sw_h_t = sw_w/2;  // triangle height
            float sw_h_r = sw_w * (aspect_swinlet - 0.5f);

            Voxels inlets = new();
            Frame swinlet_frame = new Frame(aPoint);
            swinlet_frame = swinlet_frame.transz(fSwirlChamberLowerBound+fSwirlChamberStraightHeight);
            Vector3 rot_point = swinlet_frame.pos;
            for (int i=0; i<no_swinlet; i++)
            {
                Frame shifted_frame = new Frame(rot_point, uZ3, uY3);
                shifted_frame = shifted_frame.rotyz(TWOPI*((float)i/no_swinlet));
                shifted_frame = shifted_frame.rotxy(DEG90);
                shifted_frame = shifted_frame.transx(rad_offset_swinlet);

                inlets += new Voxels(Polygon.mesh_extruded(
                    shifted_frame,
                    fSwirlChamberRadius*4,
                    [
                        new(0, 0),
                        new(sw_w, sw_h_t),
                        new(sw_w, sw_h_t+sw_h_r),
                        new(-sw_w, sw_h_t+sw_h_r),
                        new(-sw_w, sw_h_t),
                    ]
                ));

            }

            voxNextSwirlChamber = voxNextSwirlChamber.voxOffset(1.5f) - voxNextSwirlChamber - inlets;
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
        return voxOxPostWall + voxSwirlChamber;// + voxShearChamber;
    }

    protected Voxels voxels_attic()
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

    protected Voxels voxels_asi(out Voxels oASIFluid)
    {
        // create augmented spark igniter through-port
        GPort ASIPort = new GPort("1/4in", 6.35f); // 1/4in OD for SS insert?
        Voxels voxPort = ASIPort.voxConstruct(new LocalFrame(new Vector3(0,0,50)));

        BasePipe oASIWall = new BasePipe(new LocalFrame(), 50f, 6.35f, 10.35f);
        oASIFluid = new BasePipe(new LocalFrame(), 50f, 0, 6.35f).voxConstruct();

        return voxPort + oASIWall.voxConstruct();
    }

    protected Voxels voxels_flange()
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

    protected Voxels voxels_gussets(out Voxels voxStrutHoles)
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

    protected Voxels voxels_ports(out Voxels voxFluids)
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

        voxFluids = voxLOXInletFluid + voxLOXPTFluid + voxIPAPTFluid + voxChamberPTFluid;
        Voxels voxWalls = voxLOXInletWall + voxLOXPTWall + voxIPAPTWall + voxChamberPTWall
            - voxCropBoxZ(fPortsHeight) - voxCropBoxZ(0f, 100f, -10f);

        return voxWalls;
    }

    protected Voxels voxels_bolts()
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

    protected Voxels voxels_oring_grooves()
    {
        Voxels oring_inner = new BasePipe(
            new LocalFrame(),
            pm.Lz_Ioring,
            pm.Ir_Ioring,
            pm.Or_Ioring
        ).voxConstruct();

        Voxels oring_outer = new BasePipe(
            new LocalFrame(),
            pm.Lz_Ooring,
            pm.Ir_Ooring,
            pm.Or_Ooring
        ).voxConstruct();

        return oring_inner + oring_outer;
    }

    protected Voxels voxels_supports()
    {
        List<Vector3> points_supports = circularly_distributed(
            no_fc[0],
            0f,
            45,
            PI/no_fc[0]
        );
        Voxels supports = new();
        foreach (Vector3 point in points_supports)
        {
            Frame support_frame = new(point, point, uZ3);
            supports += new Voxels(Polygon.mesh_extruded(
                support_frame,
                fMaxAtticZ,
                [
                    new (-4f, 0f),
                    new (0f, 2f),
                    new (4f, 0f),
                    new (0f, -2f),
                ]
            ));
        }

        supports = supports & cone_roof_lower_crop();

        return supports;
    }

    // private helpers
    private List<Vector3> circularly_distributed(int no, float z, float r, float theta0)
    {
        List<Vector3> points = new();
        for (int i=0; i<no; ++i)
            points.Add(fromcyl(r, theta0 + i*TWOPI/no, z));
        return points;
    }

    private Voxels cone_roof_upper_crop()
    {
        BaseLens oCrop = new BaseLens(new LocalFrame(), 1f, 0f, Or_chnl);
        oCrop.SetHeight(new SurfaceModulation(-100f), new SurfaceModulation(fGetAtticLowerHeight));
        return oCrop.voxConstruct();
    }

    private Voxels cone_roof_lower_crop()
    {
        BaseLens oCrop = new BaseLens(new LocalFrame(), 1f, 0f, Or_chnl);
        oCrop.SetHeight(new SurfaceModulation(-100f), new SurfaceModulation(fGetConeRoofLowerHeight));
        return oCrop.voxConstruct();
    }

    private Voxels cone_roof_lox_crop()
    {
        BaseLens oCrop = new BaseLens(new LocalFrame(), 1f, 0f, Or_chnl);
        oCrop.SetHeight(new SurfaceModulation(fGetConeRoofUpperHeight), new SurfaceModulation(fGetAtticLowerHeight));
        return oCrop.voxConstruct();
    }

    private Voxels voxels_cone_roof()
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

        Voxels supports = voxels_supports();
        Geez.voxels(supports, Cp.clrRandom());
        part += supports;

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

        Voxels inj_elements = vox_inj_elements(out Voxels voxOxPostFluid, out Voxels swirl_inlets);
        part += inj_elements;
        part -= voxOxPostFluid;
        part -= swirl_inlets;
        using (key_inj_elements.like())
            key_inj_elements <<= Geez.voxels(inj_elements - voxOxPostFluid);

        Voxels ports = voxels_ports(out Voxels ports_fluids);
        part += ports;
        using (key_ports.like())
            key_ports <<= Geez.voxels(ports - ports_fluids);

        // external fillets
        Voxels voxCropZone = part - cone_roof_upper_crop() - voxInnerPipeCrop!;
        Voxels voxNoCropZone = part & (cone_roof_upper_crop() + voxInnerPipeCrop!);
        voxCropZone.OverOffset(5f);
        Library.Log("Filleting completed");
        part = voxCropZone + voxNoCropZone;

        part -= ports_fluids;
        part -= voxels_bolts();
        part -= voxStrutHoles;
        part -= voxels_oring_grooves();

        Geez.clear();
        using (key_part.like())
            key_part <<= Geez.voxels(part);


        Library.Log("Baby made.");
        print();

        return part;
    }

    public void drawings(in Voxels part) {
        Geez.voxels(part);
        Cuboid bounds;

        Frame frame_xy = new(3f*VOXEL_SIZE*uZ3, uX3, uZ3);
        print("cross-sectioning xy...");
        Drawing.to_file(
            fromroot($"exports/injector_xy.svg"),
            part,
            frame_xy,
            out bounds
        );
        using (Geez.like(colour: COLOUR_BLUE))
            Geez.cuboid(bounds, divide_x: 3, divide_y: 3);

        Frame frame_yz = new(ZERO3, uY3, uX3);
        print("cross-sectioning yz...");
        Drawing.to_file(
            fromroot($"exports/injector_yz.svg"),
            part,
            frame_yz,
            out bounds);
        using (Geez.like(colour: COLOUR_GREEN))
            Geez.cuboid(bounds, divide_x: 3, divide_y: 4);

        print();
    }

    public void anything() {
        /* testing playground if you wish. */
    }

    public string name => "injector";


    public void initialise()
    {
        initialise_inj();
        initialise_fc();
        initialise_chnl();
        initialise_construction();
    }

}