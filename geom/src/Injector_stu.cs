using static br.Br;
using br;
using TPIAP = TwoPeasInAPod;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using BBox3 = PicoGK.BBox3;

public class InjectorStu : TPIAP.Pea {

    // X_chnl = cooling channel property.
    // X_fc = film cooling property.
    // X_fcg = grouped film cooling property (each index = one group).
    // X_il1 = inlet to inner axial injector property.
    // X_il2 = inlet to outer axial injector property.
    // X_inj = axial injector property.
    // X_injg = grouped axial injector property (each index = one group).
    // X_inj1 = inner axial injector property.
    // X_inj2 = outer axial injector property.
    // X_IPA = IPA manifold property.
    // X_LOx = LOx manifold property.
    // X_nz1 = nozzle of inner axial injector property.
    // X_nz2 = nozzle of outer axial injector property.
    // X_plate = base plate propoerty.

    protected const float EXTRA = 6f;
    protected int DIVISIONS => max(200, (int)(200f / VOXEL_SIZE));

    public required PartMating pm { get; init; }

    public float Ir_chnl;
    public float Or_chnl;
    protected void initialise_chnl() {
        Ir_chnl = pm.Mr_chnl - 0.5f*pm.min_wi_chnl;
        Or_chnl = pm.Mr_chnl + 0.5f*pm.min_wi_chnl;
    }


    public required int[] no_injg { get; init; }
    public required float[] r_injg { get; init; }
    public required float[] theta0_injg { get; init; }
    public List<Vec2> points_inj = [];
    protected void initialise_inj() {
        points_inj = Polygon.circle(no_injg, r_injg, theta0_injg);
    }


    public required float D_fc { get; init; }
    public required int[] no_fcg { get; init; }
    public required float[] r_fcg { get; init; }
    public required float[] theta0_fcg { get; init; }
    public List<Vec2> points_fc = [];
    protected void initialise_fc() {
        points_fc = Polygon.circle(no_fcg, r_fcg, theta0_fcg);
    }


    public required float th_plate { get; init; }


    // the big boy.
    // public required int no_in1 { get; init; }
    // public required int no_in2 { get; init; }
    // public required float Pr_inj1 { get; init; }
    // public required float Pr_inj2 { get; init; }
    // public required float nu_LOx { get; init; }
    // public required float nu_IPA { get; init; }
    // public required float rho_LOx { get; init; }
    // public required float rho_IPA { get; init; }
    // public required float D_in1 { get; init; }
    // public required float D_in2 { get; init; }
    // public required float L_in1 { get; init; }
    // public required float L_in2 { get; init; }



    protected Voxels voxels_plate() {
        float th_edge = 2.0f;
        float th_oring = 1.7f;
        float phi_inner = torad(60f);
        List<Vec2> points = [
            new(-EXTRA, 0f),
            new(-EXTRA, Ir_chnl),
            new(th_edge, Ir_chnl),
            new(th_plate + th_oring, pm.Or_Ioring + 0.5f),
            new(th_plate + th_oring, pm.Ir_Ioring - 0.5f),
            new(th_plate, pm.Ir_Ioring - 1f - th_oring*tan(phi_inner)),
            new(th_plate, 0f),
        ];

        Polygon.fillet(points, 5, 3f);
        Polygon.fillet(points, 4, 2f);
        Polygon.fillet(points, 3, 3f);
        Polygon.fillet(points, 2, 2f);

        Voxels vox = new(Polygon.mesh_revolved(
            new Frame(),
            points,
            slicecount: DIVISIONS/2
        ));

        // injector holes.
        foreach (Vec2 p in points_inj) {
            vox.BoolSubtract(new Rod(
                new(rejxy(p, 0f)),
                th_plate,
                5f // TODO:
            ).extended(2f*EXTRA, Extend.UPDOWN));
        }

        // film cooling holes.
        foreach (Vec2 p in points_fc) {
            vox.BoolSubtract(new Rod(
                new(rejxy(p, 0f)),
                th_plate,
                D_fc/2f
            ).extended(2f*EXTRA, Extend.UPDOWN));
        }

        return vox;
    }

    public class ManiVol {
        /* volume = A-B */

        // /\ cone, base encompassing ipa inlet channels.
        public required Cone A { get; init; }
        // \/ cone, tip on Z axis extending until intersection with A boundary.
        public required Cone B { get; init; }
        // (z,r) point of A,B intersection.
        public required Vec2 peak { get; init; }

        // Roof slope magnitude.
        public float phi => abs(A.outer_phi);
        // Roof thickness along normal.
        public required float th { get; init; }
        // Roof thickness along z.
        public float Lz => th/cos(phi);
        // Roof thickness along r.
        public float Lr => th/sin(phi);
        public float z(float r)
            => (r > peak.Y)
             ? A.bbase.pos.Z + lerp(0f, A.Lz, invlerp(A.r0, A.r1, r))
             : B.bbase.pos.Z + lerp(0f, B.Lz, invlerp(B.r0, B.r1, r));
    }

    protected Voxels voxels_maniwalls(Geez.Cycle key, out ManiVol vol,
            out Voxels neg_LOx, out Voxels neg_IPA) {
        using var __ = key.like();

        float phi = torad(45f); // TODO:
        float Lr = Or_chnl + (th_plate + 2.5f)*tan(phi); // TODO:

        // create roof + interior volume.
        float z0 = 8f; // TODO:
        // https://www.desmos.com/calculator/tusqawwtn5
        Vec2 peak = new( /* (z,r) */
            Lr/2f/tan(phi) + z0/2f,
            Lr/2f - z0/2f*tan(phi)
        );
        vol = new ManiVol(){
            A = Cone.phied(new(), -phi, peak.X, r0: Lr),
            B = Cone.phied(new(z0*uZ3), phi, peak.X - z0 + EXTRA),
            peak = peak,
            th = 5f // TODO:
        };

        // For lox/ipa boundary:
        float th = 3f; // TODO:
        float Dz = th/sin(phi);

        Voxels neg = new();
        Voxels pos = new();

        List<Geez.Key> keys = new(numel(points_inj));

        { // lox-ipa boundary.
            foreach (Vec2 p in points_inj) {
                Mesh po = Cone.phied(new(rejxy(p, th_plate)), phi, peak.X);
                Mesh ne = Cone.phied(new(rejxy(p, th_plate + Dz)), phi, peak.X);
                keys.Add(Geez.mesh(po));
                pos.BoolAdd(new(po));
                neg.BoolAdd(new(ne));
            }
        }



        // Sneak the individual fluid volumes in here.
        // NOTE:
        // - neg_LOx INCLUDES the lox/ipa dividing wall AND the ipa volume AND
        //      the plate.
        // - neg_IPA INCLUDES the plate.
        // - AND both are extruded down by 2*EXTRA
        neg_LOx = vol.A;
        neg_LOx.BoolSubtract(vol.B);
        neg_LOx.BoolAdd(new Rod(
            new(0.1f*uZ3 /* overlap */),
            -2f*EXTRA + 0.1f,
            vol.A.r0
        ));
        neg_IPA = neg_LOx.voxDuplicate();
        neg_IPA.BoolSubtract(pos);


        { // supports.
            float min_r = pm.Ir_Ioring - 0.5f;
            float max_r = pm.Or_Ioring + 0.5f;
            float length = max_r - min_r;
            float aspect_ratio = 2f;
            float width = length / aspect_ratio;

            //TODO:
            int no = 30;
            float Mr = ave(min_r, max_r);
            float z = pm.Lz_Ioring + th_plate/3f;
            List<Vec3> points = Polygon.circle(no, Mr, 0f, z);
            foreach (Vec3 p in points) {
                Mesh m = Polygon.mesh_extruded(
                    Frame.cyl_axial(p), // x = +radial, y = +circumferential
                    peak.X,
                    [
                        // diamond.
                        new(0f,         -width/2f),
                        new(-length/2f, 0f),
                        new(0f,         +width/2f),
                        new(+length/2f, 0f),
                    ]
                );
                keys.Add(Geez.mesh(m));
                pos.BoolAdd(new(m));
            }
        }

        key <<= Geez.group(keys);


        // Intersect with internal volume.

        neg.BoolIntersect(vol.A);
        neg.BoolSubtract(vol.B);

        // add safety margin for pos (leaving half of intersection with wall).
        pos.BoolIntersect(vol.A.transz(vol.Lz/2f));
        pos.BoolSubtract(vol.B.transz(vol.Lz/2f));


        // Add roof.
        pos.BoolAdd(vol.A.shelled(+vol.th).lengthed(0f, vol.Lz + EXTRA));
        pos.BoolAdd(vol.B.shelled(-vol.th).lengthed(0f, vol.Lz + EXTRA));
        // Add bounding wall for channel.
        pos.BoolAdd(new Rod(
            new(),
            vol.z(Or_chnl) + EXTRA,
            Or_chnl,
            vol.A.r0 + vol.Lr
        ).extended(EXTRA, Extend.DOWN));
        pos.BoolSubtract(vol.B.transz(vol.Lz)
                .lengthed(0f, 2f*EXTRA));
        pos.BoolSubtract(vol.A.transz(vol.Lz)
                .lengthed(2f*EXTRA, 2f*EXTRA)
                .shelled(4f*EXTRA));

        // Make final.
        pos.BoolSubtract(neg);
        key.voxels(pos);
        return pos;

    }


    protected void voxels_asi(Geez.Cycle key_asi, out Voxels pos,
            out Voxels neg) {

        // TODO: fix asi magic numbers
        float Lz = 54f;
        // create augmented spark igniter through-port
        brGPort port = new brGPort("1/4in", 6.35f); // 1/4in OD for SS insert?
        // TODO: ^

        // fluid volume.
        neg = port.filled(new(Lz*uZ3), out _);
        neg.BoolAdd(new Rod(
            new(th_plate*uZ3),
            Lz - th_plate,
            port.downstream_radius
        ).extended(EXTRA, Extend.UP));
        neg.BoolAdd(new Rod(
            new(th_plate*uZ3),
            -th_plate,
            port.downstream_radius - 1f
        ).extended(EXTRA, Extend.UPDOWN));

        // walling.
        pos = port.shelled(new(Lz*uZ3), 1.5f, out _);
        pos.BoolAdd(new Rod(
            new(),
            Lz,
            port.downstream_radius,
            port.downstream_radius + 1.5f*2f
        ));
        key_asi.voxels(pos);
    }


    protected Voxels voxels_flange(Geez.Cycle key, in ManiVol mani_vol) {
        using var __ = key.like();
        List<Geez.Key> keys = new();

        Voxels vox;

        vox = new Rod(
            new Frame(),
            pm.flange_thickness,
            pm.flange_outer_radius
        ).extended(EXTRA, Extend.DOWN);
        key.voxels(vox);

        for (int i=0; i<pm.no_bolt; ++i) {
            float theta = i*TWOPI/pm.no_bolt;
            vox.BoolAdd(new Rod(
                new(fromcyl(pm.Mr_bolt, theta, 0f)),
                pm.inj_flange_thickness,
                pm.Bsz_bolt/2f + pm.thickness_around_bolt
            ).extended(EXTRA, Extend.DOWN));
        }
        key.voxels(vox);

        Fillet.concave(vox, 3f, inplace: true);
        key.voxels(vox);

        vox.BoolSubtract(mani_vol.A);
        vox.BoolSubtract(new Rod(
            new((th_plate + EXTRA)*uZ3),
            -2f*EXTRA - th_plate - EXTRA,
            Ir_chnl
        ));
        vox.BoolSubtract(new Rod(
            new(),
            -2f*EXTRA,
            mani_vol.A.r0
        ));
        key.voxels(vox);

        return vox;
    }


    protected Voxels voxels_bolts() {
        Voxels vox = new();
        for (int i=0; i<pm.no_bolt; ++i) {
            float theta = i*TWOPI/pm.no_bolt;
            Vec2 p = frompol(pm.Mr_bolt, theta);
            // for bolt.
            vox.BoolAdd(new Rod(
                new(rejxy(p, 0f)),
                pm.inj_flange_thickness,
                pm.Bsz_bolt/2f
            ).extended(2f*EXTRA, Extend.UPDOWN));
            // for washer/nut.
            vox.BoolAdd(new Rod(
                new(rejxy(p, pm.flange_thickness)),
                2f*EXTRA,
                pm.Or_washer + 0.5f
            ));
        }
        return vox;
    }

    protected Voxels voxels_orings() {
        Voxels vox = new Rod(
            new(),
            pm.Lz_Ioring,
            pm.Ir_Ioring,
            pm.Or_Ioring
        ).extended(2f*EXTRA, Extend.DOWN);
        vox.BoolAdd(new Rod(
            new(),
            pm.Lz_Ooring,
            pm.Ir_Ooring,
            pm.Or_Ooring
        ).extended(2f*EXTRA, Extend.DOWN));
        return vox;
    }


    protected Voxels voxels_gussets(Geez.Cycle key, in ManiVol mani_vol) {

        Voxels vox = new();

        // Concial outer boundary.
        Cone volC = new(
            new((pm.inj_flange_thickness - 0.2f)*uZ3),
            mani_vol.peak.X + mani_vol.Lz - pm.inj_flange_thickness + 0.2f,
            pm.Mr_bolt + 2f,
            mani_vol.peak.Y
        );

        // Big blocks for each bolt.
        Slice<Vec2> points = Polygon.circle(pm.no_bolt, pm.Mr_bolt);
        for (int i=0; i<numel(points); ++i) {
            Vec2 p = points[i];
            Frame frame = Frame.cyl_axial(rejxy(p, 0f));
            // x=+radial, y=+circum.

            vox.BoolAdd(new Bar(
                frame.transx(-pm.Mr_bolt/2f),
                1.5f*pm.Mr_bolt,
                2f*(pm.Or_washer + 2f),
                mani_vol.peak.X + mani_vol.Lz
            ));
            key.voxels(vox);
        }

        // Trim down.
        vox.BoolIntersect(volC);

        // Add the thrust structure mounts.
        assert(numel(points)%2 == 0);
        for (int i=0; i<numel(points); ++i) {
            if (i%2 == 0) // only every second.
                continue;
            // vertical loft: rectangle -> circle
            Vec2 p = points[i];
            // move in 20mm. TODO:
            p = (mag(p) - 20f)*normalise(p);
            // calc z of intersection with cone C.
            float t = invlerp(volC.r0, volC.r1, mag(p));
            float z0 = lerp(0f, volC.Lz, t);
            z0 += volC.bbase.pos.Z;
            Vec3 at = rejxy(p, z0);

            Leap71.ShapeKernel.LocalFrame bot = new(
                at,
                uZ3,
                normalise(at - at*uZ3)
            );
            Leap71.ShapeKernel.LocalFrame top = new(
                at + (mani_vol.peak.X + 15f - at.Z)*uZ3,
                uZ3,
                normalise(at - at*uZ3)
            );

            float strut_area = squared(2f*(pm.Bsz_bolt/2f) + 4f);
            float strut_radius = 1.2f*sqrt(strut_area/PI);

            Rectangle rectangle = new(2*(pm.Or_washer+2f), 2*(pm.Or_washer+2f));
            Circle circle = new(strut_radius);

            float loft_height = top.vecGetPosition().Z - bot.vecGetPosition().Z;
            BaseLoft strut = new(bot, rectangle, circle, loft_height);
            Leap71.ShapeKernel.BaseBox bridge = new(
                bot.oTranslate(0.04f*loft_height*uZ3),
                -bot.vecGetPosition().Z,
                2f*(pm.Or_washer + 2f),
                2f*(pm.Or_washer + 2f)
            );

            vox.BoolAdd(new(strut.mshConstruct()));
            vox.BoolAdd(new(bridge.mshConstruct()));
            vox.BoolSubtract(
                new Leap71.ShapeKernel.BaseCylinder(top, -10f, 3f)
                 .voxConstruct()
            );

            key.voxels(vox);
        }

        // Trim out.
        vox.BoolSubtract(mani_vol.A.transz(mani_vol.Lz/2f));
        vox.BoolSubtract(new Rod(
            mani_vol.A.bbase.transz(mani_vol.Lz/2f + 0.1f),
            -4f*EXTRA,
            mani_vol.A.r0
        ));
        vox.BoolSubtract(new Rod(
            new(),
            mani_vol.peak.X + mani_vol.Lz + EXTRA,
            mani_vol.peak.Y
        ));

        key.voxels(vox);
        return vox;
    }



    protected void voxels_ports(Geez.Cycle key, in ManiVol mani_vol,
            in Voxels neg_LOx, in Voxels neg_IPA, out Voxels pos,
            out Voxels neg) {

        // determine vertical height of ports:  for now use placeholder
        float height = 54f; // TODO: magic nom
        float D_pt = 2f; // PT though-hole diameter.

        // fucking c sharp cannot access out var from local function.
        Voxels _pos = new();
        Voxels _neg = new();

        void portme(brGPort port, Frame at, float th, in Voxels? sub=null) {
            Voxels this_pos = port.shelled(at, th, out _);
            this_pos.BoolAdd(new Rod(at, -height, port.downstream_radius + th));
            Voxels this_neg = port.filled(at, out _);
            this_neg.BoolAdd(new Rod(
                at,
                EXTRA,
                port.pilot_bore_radius
            ).extended(0.5f, Extend.DOWN));
            this_neg.BoolAdd(new Rod(
                at,
                -height - EXTRA,
                port.downstream_radius
            ));
            if (sub != null) {
                this_pos.BoolSubtract(sub);
                this_neg.BoolSubtract(sub);
            }
            _pos.BoolAdd(this_pos);
            _neg.BoolAdd(this_neg);
            key.voxels(_pos);
        }

        brGPort port_LOx_inlet = new("1/2in", 14f); // TODO:
        brGPort port_LOx_pt    = new("1/4in", D_pt);
        brGPort port_IPA_pt    = new("1/4in", D_pt);
        brGPort port_cc_pt     = new("1/4in", D_pt);

        // determine spacing:  ASI in middle, LOX inlet and 3x PT spaced evenly
        float r_LOx_inlet = 0.6f*pm.Or_cc;
        float r_LOx_pt = 0.8f*pm.Or_cc;
        assert(numel(r_injg) == 2);
        float r_IPA_pt = ave(r_injg[0], r_injg[1]);
        float r_cc_pt = ave(r_injg[0], r_injg[1]);
        // thru for IPA/chamber.

        Frame at_LOx_inlet = new(new Vec3(-r_LOx_inlet, 0f, height));
        Frame at_LOx_pt    = new(new Vec3(+r_LOx_pt, 0f, height));
        Frame at_IPA_pt    = new(new Vec3(0f, +r_IPA_pt, height));
        Frame at_cc_pt     = new(new Vec3(0f, -r_cc_pt, height));

        portme(port_LOx_inlet, at_LOx_inlet, 2f, neg_LOx);
        portme(port_LOx_pt,    at_LOx_pt,    2f, neg_LOx);
        portme(port_IPA_pt,    at_IPA_pt,    2f, neg_IPA);
        portme(port_cc_pt,     at_cc_pt,     2f);

        pos = _pos;
        neg = _neg;
    }



    delegate void _Op(in Voxels vox);
    public Voxels? voxels() {

        // gripped and ripped from chamber.

        /* cheeky timer. */
        System.Diagnostics.Stopwatch stopwatch = new();
        stopwatch.Start();
        using var _ = Scoped.on_leave(() => {
            print($"Baby made in {stopwatch.Elapsed.TotalSeconds:N1}s.");
            print();
        });


        /* create the part object and its key. */
        Voxels part = new();
        Geez.Cycle key_part = new();


        /* create overall bounding box to size screenshots. */
        float overall_Lr = pm.Mr_bolt
                         + pm.Bsz_bolt/2f
                         + pm.thickness_around_bolt;
        float overall_Lz = 54f; // approx total height
        float overall_Mz = overall_Lz/2f;
        BBox3 overall_bbox = new(
            new Vec3(-overall_Lr, -overall_Lr, overall_Mz - overall_Lz/2f),
            new Vec3(+overall_Lr, +overall_Lr, overall_Mz + overall_Lz/2f)
        );


        /* screen shot a */
        Geez.Screenshotta screenshotta = new(
            new Geez.ViewAs(
                overall_bbox,
                theta: torad(135f),
                phi: torad(105f),
                bgcol: Geez.BACKGROUND_COLOUR_LIGHT
            )
        );


        /* concept of "steps", which the construction is broken into. each step
           is also screenshotted (if requested). */
        int step_count = 0;
        void step(string msg, bool view_part=false) {
            ++step_count;
            if (view_part)
                key_part.voxels(part);
            if (take_screenshots)
                screenshotta.take(step_count.ToString());
            print($"[{step_count,2}] {msg}");
        }
        void substep(string msg, bool view_part=false) {
            if (view_part)
                key_part.voxels(part);
            print($"   | {msg}");
        }
        // void no_step(string msg) {
        //     ++step_count;
        //     if (take_screenshots)
        //         Geez.wipe_screenshot(step_count.ToString());
        //     print($"[--] {msg}");
        // }


        /* shorthand for adding/subtracting a component into the part. */
        void _op(_Op func, ref Voxels? vox, Geez.Cycle? key, bool keepme,
                bool view_part) {
            assert(vox != null);
            func(vox!);
            if (view_part)
                key_part.voxels(part);
            if (key != null)
                key.clear();
            if (!keepme)
                vox = null;
        }
        void add(ref Voxels? vox, Geez.Cycle? key=null, bool keepme=false,
                bool view_part=true)
            => _op(part.BoolAdd, ref vox, key, keepme, view_part);
        void sub(ref Voxels? vox, Geez.Cycle? key=null, bool keepme=false,
                bool view_part=true)
            => _op(part.BoolSubtract, ref vox, key, keepme, view_part);


        /* perform all the steps of creating the part. */

        Geez.Cycle key_plate = new(colour: COLOUR_CYAN);
        Geez.Cycle key_maniwalls = new(colour: COLOUR_PINK);
        Geez.Cycle key_asi = new(colour: COLOUR_WHITE);
        Geez.Cycle key_flange = new(colour: COLOUR_BLUE);
        Geez.Cycle key_gussets = new(colour: COLOUR_YELLOW);
        Geez.Cycle key_inj_elements = new(colour: COLOUR_RED);
        Geez.Cycle key_ports = new(colour: new("332799"));

        Voxels? plate = voxels_plate();
        key_plate.voxels(plate);
        step("created plate.");

        Voxels? maniwalls = voxels_maniwalls(key_maniwalls,
                out ManiVol mani_vol, out Voxels neg_LOx, out Voxels neg_IPA);
        step("created manifold walls.");

        voxels_asi(key_asi, out Voxels? pos_asi, out Voxels? neg_asi);
        step("created asi port.");

        Voxels? bolts = voxels_bolts();
        substep("created bolts.");
        Voxels? orings = voxels_orings();
        substep("created O-rings.");
        Voxels? flange = voxels_flange(key_flange, mani_vol);
        step("created flange.");

        Voxels? gussets = voxels_gussets(key_gussets, mani_vol);
        step("created gussets.");

        voxels_ports(key_ports, mani_vol, neg_LOx, neg_IPA,
                out Voxels? pos_ports, out Voxels? neg_ports);
        step("created ports.");

        add(ref plate, key_plate);
        substep("added plate.");
        add(ref maniwalls, key_maniwalls);
        substep("added manifold walls.");
        add(ref pos_asi, key_asi);
        substep("added asi.");
        add(ref flange, key_flange);
        substep("added flange.");
        add(ref gussets, key_gussets);
        substep("added gussets.");
        add(ref pos_ports, key_ports);
        substep("added ports.");

        step("added material.");

        sub(ref bolts);
        substep("subtracted bolts.");
        sub(ref orings);
        substep("subtracted O-rings.");
        sub(ref neg_asi);
        substep("subtracted asi.");
        sub(ref neg_ports);
        substep("subtracted ports.");

        step("removed voids.");

        part.BoolSubtract(new Rod(
            new(),
            -2f*EXTRA,
            overall_Lr + EXTRA
        ));
        substep("clipped bottom.", view_part: true);

        step("finished.");


        return part;
    }


    public Voxels? cutaway(in Voxels part) {
        Voxels cutted = Sectioner.pie(0f, -1.5f*PI).cut(part);
        Geez.voxels(cutted);
        return cutted;
    }


    public void drawings(in Voxels part) {
        Geez.voxels(part);
        Bar bounds;

        Frame frame_xy = new(3f*VOXEL_SIZE*uZ3, uX3, uZ3);
        print("cross-sectioning xy...");
        Drawing.to_file(
            fromroot($"exports/injector_xy.svg"),
            part,
            frame_xy,
            out bounds
        );
        using (Geez.like(colour: COLOUR_BLUE))
            Geez.bar(bounds, divide_x: 3, divide_y: 3);

        Frame frame_yz = new(ZERO3, uY3, uX3);
        print("cross-sectioning yz...");
        Drawing.to_file(
            fromroot($"exports/injector_yz.svg"),
            part,
            frame_yz,
            out bounds
        );
        using (Geez.like(colour: COLOUR_GREEN))
            Geez.bar(bounds, divide_x: 3, divide_y: 4);

        print();
    }


    public void anything() {}


    public string name => "injector_stu";


    public bool minimise_mem     = false;
    public bool take_screenshots = false;
    public void set_modifiers(int mods) {
        _ = popbits(ref mods, TPIAP.MINIMISE_MEM);
        take_screenshots = popbits(ref mods, TPIAP.TAKE_SCREENSHOTS);
        _ = popbits(ref mods, TPIAP.LOOKIN_FANCY);
        if (mods == 0)
            return;
        throw new Exception("yeah nah dunno what it is");
        throw new Exception("honestly couldnt tell you");
        throw new Exception("what is happening right now");
                          // im selling out
    }



    public void initialise() {
        initialise_chnl();
        initialise_inj();
        initialise_fc();
    }










    public static class GraphLookup {
        /* https://doi.org/10.2514/5.9781600866760.0019.0103 */

        // --- Spray Cone Angle Plot (Independent X: A, Dependent Y: TwoAlpha) ---
        // Samples at lbar_n = 2.0 and 0.5
        private static readonly Vec2[] SprayCone_A_to_TwoAlpha_N2 = {
            new(0.421421f, 56.09843f), new(0.927129f, 66.71354f),
            new(1.299385f, 73.60281f), new(1.636524f, 78.17224f),
            new(2.064969f, 82.74165f), new(2.661984f, 87.24077f),
            new(3.167691f, 89.77153f), new(3.834943f, 92.51318f),
            new(4.326602f, 94.41125f), new(5.007903f, 96.66081f),
            new(5.661108f, 98.27768f), new(6.356455f, 99.61336f),
            new(7.044778f, 100.8084f), new(7.585603f, 101.3708f),
            new(8.091308f, 101.7926f)
        };

        private static readonly Vec2[] SprayCone_A_to_TwoAlpha_N05 = {
            new(0.456541f, 56.59052f), new(0.695346f, 65.16697f),
            new(0.870938f, 70.29878f), new(1.046533f, 74.93849f),
            new(1.313432f, 80.56239f), new(1.573309f, 84.78032f),
            new(1.833188f, 88.36555f), new(2.128183f, 91.59930f),
            new(2.549605f, 95.18454f), new(2.914838f, 97.50440f),
            new(3.427569f, 100.3163f), new(3.898157f, 102.5659f),
            new(4.319579f, 104.1828f), new(4.783144f, 105.7996f),
            new(5.260755f, 107.2056f), new(5.808605f, 108.8225f),
            new(6.525022f, 110.5097f), new(7.220371f, 111.7047f),
            new(7.676910f, 112.0562f), new(8.091308f, 112.5483f)
        };

        public static float GetSprayConeAngle(float targetA, float lbar_n) {
            assert(within(lbar_n, 0.5f, 2.0f));
            Vec2 left = new(0.5f, lookup(SprayCone_A_to_TwoAlpha_N05, targetA));
            Vec2 right = new(2.0f, lookup(SprayCone_A_to_TwoAlpha_N2, targetA));
            return sample(left, right, lbar_n);
        }

        // --- Spray Cone Angle Plot (Independent X: TwoAlpha, Dependent Y: A) ---
        private static readonly Vec2[] SprayCone_TwoAlpha_to_A_N2 = {
            new(56.09843f, 0.421421f), new(66.71354f, 0.927129f),
            new(73.60281f, 1.299385f), new(78.17224f, 1.636524f),
            new(82.74165f, 2.064969f), new(87.24077f, 2.661984f),
            new(89.77153f, 3.167691f), new(92.51318f, 3.834943f),
            new(94.41125f, 4.326602f), new(96.66081f, 5.007903f),
            new(98.27768f, 5.661108f), new(99.61336f, 6.356455f),
            new(100.8084f, 7.044778f), new(101.3708f, 7.585603f),
            new(101.7926f, 8.091308f)
        };

        private static readonly Vec2[] SprayCone_TwoAlpha_to_A_N05 = {
            new(56.59052f, 0.456541f), new(65.16697f, 0.695346f),
            new(70.29878f, 0.870938f), new(74.93849f, 1.046533f),
            new(80.56239f, 1.313432f), new(84.78032f, 1.573309f),
            new(88.36555f, 1.833188f), new(91.59930f, 2.128183f),
            new(95.18454f, 2.549605f), new(97.50440f, 2.914838f),
            new(100.3163f, 3.427569f), new(102.5659f, 3.898157f),
            new(104.1828f, 4.319579f), new(105.7996f, 4.783144f),
            new(107.2056f, 5.260755f), new(108.8225f, 5.808605f),
            new(110.5097f, 6.525022f), new(111.7047f, 7.220371f),
            new(112.0562f, 7.676910f), new(112.5483f, 8.091308f)
        };

        public static float GetA(float twoalpha, float lbar_n) {
            assert(within(lbar_n, 0.5f, 2.0f));
            Vec2 left = new(0.5f, lookup(SprayCone_TwoAlpha_to_A_N05, twoalpha));
            Vec2 right = new(2.0f, lookup(SprayCone_TwoAlpha_to_A_N2, twoalpha));
            return sample(left, right, lbar_n);
        }

        // --- Flow Coefficient Plot (Independent X: A, Dependent Y: Mu_in) ---
        // Samples at C = 1.0 and 4.0
        private static readonly Vec2[] FlowCoeff_C1 = {
            new(0.827858f, 0.400318f), new(0.947264f, 0.373270f),
            new(1.026863f, 0.353858f), new(1.146268f, 0.331901f),
            new(1.305473f, 0.309626f), new(1.416915f, 0.291169f),
            new(1.631838f, 0.268894f), new(1.870646f, 0.243755f),
            new(2.157212f, 0.220525f), new(2.499500f, 0.198886f),
            new(2.857709f, 0.179157f), new(3.367164f, 0.157518f),
            new(3.804973f, 0.141289f), new(4.378109f, 0.126333f),
            new(4.959204f, 0.116150f), new(5.484577f, 0.107239f),
            new(6.073631f, 0.098966f), new(6.726366f, 0.091329f),
            new(7.299502f, 0.085601f), new(7.689552f, 0.081782f),
            new(8.000000f, 0.079236f)
        };

        private static readonly Vec2[] FlowCoeff_C4 = {
            new(1.082585f, 0.399364f), new(1.194027f, 0.377725f),
            new(1.313432f, 0.354177f), new(1.424875f, 0.336993f),
            new(1.560198f, 0.321400f), new(1.703481f, 0.305807f),
            new(1.878605f, 0.287669f), new(2.061691f, 0.269531f),
            new(2.300495f, 0.251710f), new(2.483581f, 0.236436f),
            new(2.722386f, 0.222116f), new(3.000992f, 0.206205f),
            new(3.287561f, 0.194431f), new(3.653730f, 0.182657f),
            new(4.083580f, 0.172474f), new(4.473629f, 0.162928f),
            new(5.054724f, 0.152426f), new(5.556216f, 0.143834f),
            new(6.145271f, 0.135879f), new(6.766166f, 0.127924f),
            new(7.379101f, 0.121559f), new(8.031838f, 0.115195f)
        };

        public static float GetFlowCoefficient(float targetA, float C) {
            assert(within(C, 1.0f, 4.0f));
            Vec2 left = new(1.0f, lookup(FlowCoeff_C1, targetA));
            Vec2 right = new(4.0f, lookup(FlowCoeff_C4, targetA));
            return sample(left, right, C);
        }

        // --- Flow Coefficient Plot (Inverse: Mu_in -> A) ---
        private static readonly Vec2[] FlowCoeff_C1_Inv = {
            new(0.079236f, 8.000000f), new(0.081782f, 7.689552f),
            new(0.085601f, 7.299502f), new(0.091329f, 6.726366f),
            new(0.098966f, 6.073631f), new(0.107239f, 5.484577f),
            new(0.116150f, 4.959204f), new(0.126333f, 4.378109f),
            new(0.141289f, 3.804973f), new(0.157518f, 3.367164f),
            new(0.179157f, 2.857709f), new(0.198886f, 2.499500f),
            new(0.220525f, 2.157212f), new(0.243755f, 1.870646f),
            new(0.268894f, 1.631838f), new(0.291169f, 1.416915f),
            new(0.309626f, 1.305473f), new(0.331901f, 1.146268f),
            new(0.353858f, 1.026863f), new(0.373270f, 0.947264f),
            new(0.400318f, 0.827858f)
        };

        private static readonly Vec2[] FlowCoeff_C4_Inv = {
            new(0.115195f, 8.031838f), new(0.121559f, 7.379101f),
            new(0.127924f, 6.766166f), new(0.135879f, 6.145271f),
            new(0.143834f, 5.556216f), new(0.152426f, 5.054724f),
            new(0.162928f, 4.473629f), new(0.172474f, 4.083580f),
            new(0.182657f, 3.653730f), new(0.194431f, 3.287561f),
            new(0.206205f, 3.000992f), new(0.222116f, 2.722386f),
            new(0.236436f, 2.483581f), new(0.251710f, 2.300495f),
            new(0.269531f, 2.061691f), new(0.287669f, 1.878605f),
            new(0.305807f, 1.703481f), new(0.321400f, 1.560198f),
            new(0.336993f, 1.424875f), new(0.354177f, 1.313432f),
            new(0.377725f, 1.194027f), new(0.399364f, 1.082585f)
        };

        public static float GetAFromMu(float mu_in, float C) {
            assert(within(C, 1.0f, 4.0f));
            Vec2 left = new(1.0f, lookup(FlowCoeff_C1_Inv, mu_in));
            Vec2 right = new(4.0f, lookup(FlowCoeff_C4_Inv, mu_in));
            return sample(left, right, C);
        }

        // --- Relative Liquid Vortex Radius (Independent X: A, Dependent Y: r_m_on_R_n) ---
        // Samples at Rbar_in = 1, 3, and 4
        private static readonly Vec2[] Vortex_R1 = {
            new(0.523076f, 0.300632f), new(0.676923f, 0.356258f),
            new(0.953845f, 0.413780f), new(1.212306f, 0.459924f),
            new(1.606152f, 0.519975f), new(1.901537f, 0.557901f),
            new(2.276921f, 0.592667f), new(2.719998f, 0.623641f),
            new(3.076923f, 0.645133f), new(3.612306f, 0.672313f),
            new(4.030769f, 0.686220f), new(4.670769f, 0.710240f),
            new(5.230768f, 0.726675f), new(5.846152f, 0.744374f),
            new(6.283075f, 0.753856f), new(6.769228f, 0.763338f),
            new(7.347693f, 0.772819f), new(8.000000f, 0.781037f)
        };

        private static readonly Vec2[] Vortex_R3 = {
            new(0.916922f, 0.301264f), new(0.990769f, 0.331606f),
            new(1.107691f, 0.362579f), new(1.396921f, 0.410619f),
            new(1.667691f, 0.449178f), new(2.024615f, 0.487737f),
            new(2.461537f, 0.519343f), new(2.769229f, 0.541466f),
            new(3.224613f, 0.568015f), new(3.735383f, 0.589507f),
            new(4.523076f, 0.619216f), new(5.212306f, 0.640708f),
            new(5.975383f, 0.661568f), new(6.775382f, 0.679267f),
            new(7.495382f, 0.695070f), new(8.049228f, 0.703919f)
        };

        private static readonly Vec2[] Vortex_R4 = {
            new(1.224614f, 0.299368f), new(1.464615f, 0.341719f),
            new(1.692306f, 0.368268f), new(1.993845f, 0.397977f),
            new(2.313845f, 0.421365f), new(2.646153f, 0.440961f),
            new(2.984613f, 0.459924f), new(3.415383f, 0.478255f),
            new(3.821536f, 0.495954f), new(4.301537f, 0.513021f),
            new(4.769228f, 0.528824f), new(5.310767f, 0.543363f),
            new(5.766152f, 0.554741f), new(6.289230f, 0.567383f),
            new(6.873845f, 0.578129f), new(7.458459f, 0.590139f),
            new(8.067689f, 0.600885f)
        };

        public static float GetRelativeVortexRadius(float targetA, float rbar_in)
        {
            assert(within(rbar_in, 1.0f, 4.0f));
            Vec2 left;
            Vec2 right;
            if (rbar_in <= 3.0f) {
                left = new(1.0f, lookup(Vortex_R1, targetA));
                right = new(3.0f, lookup(Vortex_R3, targetA));
            } else {
                left = new(3.0f, lookup(Vortex_R3, targetA));
                right = new(4.0f, lookup(Vortex_R4, targetA));
            }
            return sample(left, right, rbar_in);
        }



        private static float lookup(Vec2[] points, float x) {
            assert(numel(points) > 0);
            if (numel(points) == 1)
                return points[0].Y;

            int lo = 0;
            int hi = numel(points) - 1;

            int idx = -1;
            while (lo <= hi) {
                int mid = (lo + hi)/2;
                if (points[mid].X < x) {
                    idx = mid;
                    lo = mid + 1;
                } else {
                    hi = mid - 1;
                }
            }
            assert(within(idx, -1, numel(points) - 1));
            if (idx == numel(points) - 1) {
                // assert(nearto(points[^1].X, x));
                --idx;
            }
            if (idx == -1)
                idx += 1;
            return sample(points[idx], points[idx + 1], x);
        }

        private static float sample(Vec2 left, Vec2 right, float x) {
            assert(right.X > left.X);
            float t = (x - left.X) / (right.X - left.X);
            return lerp(left.Y, right.Y, t);
        }
    }

}