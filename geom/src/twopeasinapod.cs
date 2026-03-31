using static br.Br;
using br;

using JsonMap = System.Text.Json.Nodes.JsonObject; // object is a stupid name.
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using BBox3 = PicoGK.BBox3;

public static class TwoPeasInAPod {

    public interface Pea {
        string name { get; }

        void initialise();
        void set_modifiers(int mods);

        void anything();
        Voxels? voxels();
        Voxels? cutaway(in Voxels part);
        void drawings(in Voxels part);
    }

    /* GUIDE: how to create `make` (for smarties) */
    /* bitwise or (|) ANY/SEVERAL/ALL of these actions: */
    public const int ANYTHING        = 0x01;
    public const int VOXELS          = 0x02;
    public const int CUTAWAY         = 0x04;
    public const int DRAWINGS        = 0x08;
    public const int LOOKSIE         = 0x10;
    public const int LOOKSIE_CUTAWAY = 0x20;
    /* bitwise or (|) ANY/SEVERAL/ALL of these modifiers: */
    public const int MINIMISE_MEM     = 0x01 << BITC_ACTION;
    public const int PRINTABLE        = 0x02 << BITC_ACTION;
    public const int FILLETLESS       = 0x04 << BITC_ACTION;
    public const int TAKE_SCREENSHOTS = 0x08 << BITC_ACTION;
    public const int LOOKIN_FANCY     = 0x10 << BITC_ACTION;
    public const int BRANDINGLESS     = 0x20 << BITC_ACTION;
    public const int PRINT_RESIN      = 0x40 << BITC_ACTION;
    public const int ELEMENTLESS      = 0x80 << BITC_ACTION;
    /* bitwise or (|) ONE(1) of these peas: */
    public const int CHAMBER  = 1 << (BITC_ACTION + BITC_MODIFIER);
    public const int INJECTOR = 2 << (BITC_ACTION + BITC_MODIFIER);
    public const int INJECTOR_SAMPLE = 3 << (BITC_ACTION + BITC_MODIFIER);
    /* theres lowk more than 2 peas in this pod. */
    /* THATS ALL FOLKS */


    public const int DUMMY = 0;
    public const int BITC_ACTION = 6;
    public const int MASK_ACTION = (1 << BITC_ACTION) - 1;
    public const int BITC_MODIFIER = 8;
    public const int MASK_MODIFIER = ((1 << BITC_MODIFIER) - 1) << BITC_ACTION;
    public const int MASK_PEA = ~(MASK_ACTION | MASK_MODIFIER);


    public static void entrypoint(int make, bool shutdown_on_exit,
            in Sectioner sectioner) {
        try {
            // Setup geez.
            Geez.set_background_colour(dark: true);
            Geez.dflt_colour = new("#AB331A"); // copperish.
            Geez.dflt_metallic = 0.35f;
            Geez.dflt_roughness = 0.8f;
            Geez.dflt_sectioner = sectioner;
            Geez.initialise();

            execute(make);
        } catch {
            Geez.shutdown(); // :(
            throw;
        } finally {
            if (shutdown_on_exit)
                Geez.shutdown(); // ok to double-call.
        }
    }

    public static void execute(int make) {
        // Check a pea and at least one action was given.
        if (isclr(make, MASK_ACTION))
            throw new Exception("invalid 'make': no action selected");
        if (isclr(make, MASK_PEA))
            throw new Exception("invalid 'make': no pea selected");

        // Peep into modifiers and disable rendering if minimising memory.
        if (isset(make, MINIMISE_MEM)) {
            print("minimising memory means no rendering, going dark...");
            print();
            Geez.lockdown = true;
        }

        // Setup fancy rendering settings if requested.
        if (isset(make, LOOKIN_FANCY)) {
            print("entering barcelona...");
            print();
            string barcelona = fromroot("assets/Barcelona.zip");
            try {
                PICOGK_VIEWER.LoadLightSetup(barcelona);
                // Note that the loading prints a "Loading Lights" message, which
                // we may view as an annoyance, but we may also view as an
                // opportunity.
                queue_print_action(["ignore that ^", ""]);
                // its "possible" (not really since there are no threads that
                // could) for an action to slip in between these but dw.

                Geez.dflt_metallic = 0.4f;
                Geez.dflt_roughness = 0.1f;
            } catch (Exception e) {
                print($"oops, failed to enter barcelona at '{barcelona}'");
                print("Exception log:");
                print(e);
                print();
            }
        }

        // Take a break if screenshotting.
        if (isset(make, TAKE_SCREENSHOTS)) {
            print("since 'TAKE_SCREENSHOTS' has been selected,");
            print("sleeping for 5s to allow window resizing...");
            print("  (be sure to hit 'backspace' to update)");
            print();
            Thread.Sleep(5000);
        }

        // Load the config files.
        string path_all = fromroot("../config/all.json");
        string path_extra = fromroot("../config/ammendments.json");
        Jason config = new(path_all);
        if (File.Exists(path_extra))
            config.overwrite_from(path_extra);

        // Construct the pea.
        Pea pea;
        switch (make & MASK_PEA) {
          case CHAMBER: {
            // Construct the chamber object.
            config.set("chamber/pm", config.get_map("part_mating"));
            var max_phi = config.get<float>("printer/max_print_angle");
            config.set("chamber/phi_mani", max_phi);
            config.set("chamber/phi_fixt", max_phi);
            config.set("chamber/phi_inlet", -max_phi);
            config.set("chamber/phi_tc", max_phi);
            pea = config.deserialise<Chamber>("chamber");
          } break;

          /* DUAL CASE */
          case INJECTOR_SAMPLE:
          case INJECTOR: {
            // Construct the injector object.
            config.set("injector/pm", config.get_map("part_mating"));
            var max_phi = config.get<float>("printer/max_print_angle");
            config.set("injector/phi_mw", max_phi);
            config.set("injector/element/phi", max_phi);
            var th_plate = config.get<float>("injector/th_plate");
            var th_dmw = config.get<float>("injector/th_dmw");
            config.set("injector/element/th_plate", th_plate);
            config.set("injector/element/th_dmw", th_dmw);
            var rho_1 = config.get<float>("operating_conditions/rho_LOx");
            var rho_2 = config.get<float>("operating_conditions/rho_IPA");
            var mu_1 = config.get<float>("operating_conditions/mu_LOx");
            var mu_2 = config.get<float>("operating_conditions/mu_IPA");
            config.set("injector/element/rho_1", rho_1);
            config.set("injector/element/rho_2", rho_2);
            config.set("injector/element/mu_1", mu_1);
            config.set("injector/element/mu_2", mu_2);
            var Pr_1 = config.get<float>("operating_conditions/Pr_LOx");
            var Pr_2 = config.get<float>("operating_conditions/Pr_IPA");
            var mdot_1 = config.get<float>("operating_conditions/mdot_LOx");
            var mdot_2 = config.get<float>("operating_conditions/mdot_IPA");
            var P_cc = config.get<float>("operating_conditions/P_cc");
            var no_injg = config.get<List<int>>("injector/no_injg");
            int no_inj = 0;
            foreach (int this_no in no_injg)
                no_inj += this_no;
            config.set("injector/element/DP_1", (Pr_1 - 1f)*P_cc);
            config.set("injector/element/DP_2", (Pr_2 - 1f)*P_cc);
            config.set("injector/element/mdot_1", mdot_1 / no_inj);
            config.set("injector/element/mdot_2", mdot_2 / no_inj);

            if ((make & MASK_PEA) == INJECTOR) {
                pea = config.deserialise<Injector>("injector");
            } else {
                assert((make & MASK_PEA) == INJECTOR_SAMPLE);
                // Copy element over to a new map.
                config.new_map("sample");

                var element = config.get_map("injector/element").DeepClone();
                List<JsonMap> elements = [];
                void setup(int no_inj=-1, float twoalpha_1=NAN,
                        float Lbar_nz2=NAN, float Lbar_ch1=NAN,
                        float mdot_scale = NAN) {
                    JsonMap e = (JsonMap)element.DeepClone();

                    if (no_inj != -1) {
                        e["mdot_1"] = mdot_1 / no_inj;
                        e["mdot_2"] = mdot_2 / no_inj;
                    }
                    if (nonnan(twoalpha_1))
                        e["twoalpha_1"] = twoalpha_1;
                    if (nonnan(Lbar_nz2))
                        e["Lbar_nz2"] = Lbar_nz2;
                    if (nonnan(Lbar_ch1))
                        e["Lbar_ch1"] = Lbar_ch1;
                    if (nonnan(mdot_scale)) {
                        e["mdot_scale"] = mdot_scale;
                    }
                    elements.Add(e);
                }

        /* 8   */ setup(no_inj: 15, mdot_scale: 0.8f);
        /* 9   */ setup(no_inj: 15, mdot_scale: 0.9f);
        /* 10  */ setup(no_inj: 15, mdot_scale: 1.0f);
        /* 11  */ setup(no_inj: 15, mdot_scale: 1.1f);
        /* 12  */ setup(no_inj: 15, mdot_scale: 1.2f);

                config.set("sample/elements", elements);

                pea = config.deserialise<InjectorSample>("sample");
            }
          } break;

          default:
            throw new Exception("invalid 'make': unrecognised pea "
                             + $"0x{make & MASK_PEA:X}");
        }

        // Set pea modifiers.
        pea.set_modifiers(make & MASK_MODIFIER);

        // Initialise the pea.
        pea.initialise();

        // Anything?
        if (isset(make, ANYTHING))
            pea.anything();

        // Gimme voxels.
        GetVoxels part = new(pea);
        GetVoxelsCutaway cutaway = new(pea);

        // Voxel me.
        if (isset(make, VOXELS))
            part.generate(must: true);

        // Cutaway gag.
        if (isset(make, CUTAWAY))
            cutaway.generate(part.voxels(), must: true);

        // Drawing sidequest.
        if (isset(make, DRAWINGS) && part.succeeded()) {
            print($"Making '{pea.name}' drawings...");
            print();
            Geez.clear();
            pea.drawings(part.voxels()!);
        }

        // Proper looksie.
        if (isset(make, LOOKSIE) && part.succeeded()) {
            print($"Giving '{pea.name}' a looksie...");
            print();
            Voxels vox = part.voxels()!;
            using (Geez.locked(false)) {
                Geez.clear();
                Geez.voxels(vox);
            }
            print("press any key when finished viewing to continue...");
            Console.ReadKey(intercept: true);
            print();
        }

        // Additional proper looksie.
        if (isset(make, LOOKSIE_CUTAWAY) && cutaway.succeeded(part.voxels())) {
            print($"Giving '{pea.name}' a cutawayed looksie...");
            print();
            Voxels vox = cutaway.voxels(part.voxels()!)!;
            using (Geez.locked(false)) {
                Geez.clear();
                Geez.voxels(vox);
            }
            print("press any key when finished viewing to continue...");
            Console.ReadKey(intercept: true);
            print();
        }
    }


    public static bool save_voxels_only(in string name, in Voxels vox) {
        string path_vdb = fromroot($"exports/{name}.vdb");
        try {
            vox.SaveToVdbFile(path_vdb);
            print($"Saved to vdb: '{path_vdb}'");
        } catch (Exception e) {
            print($"Failed to export to vdb at '{path_vdb}'.");
            print("Exception log:");
            print(e);
            print();
            return false;
        }
        // Save voxel size explicitly, since the vdb cant/doesnt check it matches
        // when loading it.
        string path_voxsize = fromroot($"exports/{name}.voxel_size");
        try {
            string text = VOXEL_SIZE.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            File.WriteAllText(path_voxsize, text);
            print($"  (saved voxel size to: '{path_voxsize}')");
            print();
        } catch (Exception e) {
            print($"Failed to save voxel size at '{path_voxsize}'.");
            print("Exception log:");
            print(e);
            print();
            return false;
        }
        return true;
    }

    public static bool save_mesh_only(in string name, in Mesh mesh) {
        string path_stl = fromroot($"exports/{name}.stl");
        try {
            mesh.SaveToStlFile(path_stl);
            print($"Exported to stl: '{path_stl}'");
            print();
        } catch (Exception e) {
            print($"Failed to export to stl at '{path_stl}'.");
            print("Exception log:");
            print(e);
            return false;
        }
        return true;
    }

    public static bool save_voxels(in string name, in Voxels vox) {
        Mesh? mesh = null;
        return save_voxels(name, vox, ref mesh);
    }
    public static bool save_voxels(in string name, in Voxels vox,
            ref Mesh? mesh) {
        mesh ??= new(vox);
        return save_voxels_only(name, vox)
             & save_mesh_only(name, mesh);
    }

    public static bool load_voxels(in string name, out Voxels? vox) {
        string path_vdb = fromroot($"exports/{name}.vdb");
        string path_voxsize = fromroot($"exports/{name}.voxel_size");
        // Ensure all files are chilling.
        if (!File.Exists(path_vdb)) {
            print($"No voxels at: '{path_vdb}'");
            print();
            goto FAILED;
        }
        FileInfo file_info = new(path_voxsize);
        if (!file_info.Exists) {
            print($"Missing voxel size at: '{path_voxsize}'");
            print();
            goto FAILED;
        }
        if (file_info.Length > 1024*1024 /* 1MB */) {
            print($"Voxel size file invalid at: '{path_voxsize}'");
            print($"  (sus, file over 1MB)");
            print();
            goto FAILED;
        }
        // Ensure voxel size matches.
        try {
            string text = File.ReadAllText(path_voxsize);
            float voxsize = float.Parse(text,
                    System.Globalization.CultureInfo.InvariantCulture);
            if (voxsize != VOXEL_SIZE) {
                print($"Voxel size mismatch at: '{path_voxsize}'");
                print($"  (needed {VOXEL_SIZE}, found {voxsize})");
                print();
                goto FAILED;
            }
        } catch (Exception e) {
            print($"Failed to read voxel size at '{path_vdb}'.");
            print("Exception log:");
            print(e);
            print();
            goto FAILED;
        }
        // Read dem voxels.
        try {
            vox = Voxels.voxFromVdbFile(path_vdb);
            print($"Loaded from vdb: '{path_vdb}'");
            print();
            return true;
        } catch (Exception e) {
            print($"Failed to load from vdb at '{path_vdb}'.");
            print("Exception log:");
            print(e);
            print();
            goto FAILED;
        }

      FAILED:;
        vox = null;
        return false;
    }

    private class GetVoxels {
        public Pea pea { get; }
        public bool tried { get; private set; }

        public GetVoxels(in Pea pea) {
            this.pea = pea;
            this.tried = false;
            _vox = null;
        }

        public void generate(bool must=false) {
            assert(!must || !tried);
            if (tried)
                return;
            tried = true;
            if (!must) {
                if (load_voxels(pea.name, out _vox))
                    return;
            }
            print(must
                ? $"Generating '{pea.name}' voxels..."
                : $"Regenerating '{pea.name}' voxels..."
            );
            print();
            Geez.clear();
            _vox = pea.voxels();
            if (_vox != null)
                save_voxels(pea.name, _vox);
        }

        public Voxels? voxels() {
            generate();
            return _vox;
        }
        public bool succeeded() {
            generate();
            return _vox != null;
        }
        private Voxels? _vox;
    }

    private class GetVoxelsCutaway {
        public Pea pea { get; }
        public bool tried { get; private set; }

        public GetVoxelsCutaway(in Pea pea) {
            this.pea = pea;
            this.tried = false;
            _vox = null;
        }

        public void generate(in Voxels? part, bool must=false) {
            assert(!must || !tried);
            if (tried)
                return;
            if (part == null)
                return; // dont set tried. dont error even if must.
            tried = true;
            if (!must) {
                if (load_voxels($"{pea.name}-cutaway", out _vox))
                    return;
            }
            print(must
                ? $"Cutting '{pea.name}' away..."
                : $"Recutting '{pea.name}' away..."
            );
            print();
            Geez.clear();
            _vox = pea.cutaway(part);
            if (_vox != null)
                save_voxels($"{pea.name}-cutaway", _vox);
        }

        public Voxels? voxels(in Voxels? part) {
            generate(part);
            return _vox;
        }
        public bool succeeded(in Voxels? part) {
            generate(part);
            return _vox != null;
        }
        private Voxels? _vox;
    }

    private class PrintAction : PicoGK.Viewer.IViewerAction {
        public List<string> msgs { get; }
        public PrintAction(List<string> msgs) {
            this.msgs = msgs;
        }

        /* Viewer.IViewerAction */
        public void Do(PicoGK.Viewer viewer) {
            foreach (string msg in msgs)
                print(msg);
        }
    }

    public static void queue_print_action(in List<string> msgs) {
        var actions = Pierce.get<Queue<PicoGK.Viewer.IViewerAction>>(
            PICOGK_VIEWER,
            "m_oActions"
        );
        lock (actions)
            actions.Enqueue(new PrintAction(msgs));
    }
}


public class PartMaker : IDisposable {
    // gripped and ripped from chamber.

    public Voxels voxels { get; set; }
    public Geez.Cycle key { get; set; }
    public Geez.Screenshotta? screenshotta { get; set; } // screen shot a
    protected System.Diagnostics.Stopwatch stopwatch; // cheeky timer.

    public int step_count = 0;

    public string name = "Baby";

    public PartMaker() {
        voxels = new();
        key = new();
        screenshotta = null;
        stopwatch = new();
        stopwatch.Start();
    }
    public PartMaker(float Lz, float Lr, float Mz)
        : this(new(new(-Lr, -Lr, Mz - Lz/2f), new(Lr, Lr, Mz + Lz/2f))) {}
    public PartMaker(BBox3 bounds) : this() {
        screenshotta = new(
            new Geez.ViewAs(
                bounds,
                theta: torad(135f),
                phi: torad(105f),
                bgcol: Geez.BACKGROUND_COLOUR_LIGHT
            )
        );
    }

    public void Dispose() {
        print($"{name} made in {stopwatch.Elapsed.TotalSeconds:N1}s.");
        voxels.CalculateProperties(out float vol_mm3, out BBox3 bounds);
        Vec3 size = bounds.vecSize();
        print($"- bounds: {size.X:G4}mm x {size.Y:G4}mm x {size.Z:G4}mm");
        print($"- volume: {vol_mm3*1e-3:G4} mL");
        print();
    }


    /* concept of "steps", which the construction is broken into. each step is
       also screenshotted (if requested). */

    public void substep(string msg, bool view_part=false) {
        if (view_part)
            key.voxels(voxels);
        print($"   | {msg}");
    }
    public void step(string msg) {
        ++step_count;
        if (screenshotta != null)
            screenshotta.take(step_count.ToString());
        print($"[{step_count,2}] {msg}");
    }
    public void no_step(string msg) {
        ++step_count;
        if (screenshotta != null)
            Geez.wipe_screenshot(step_count.ToString());
        print($"[--] {msg}");
    }


    /* shorthand for adding/subtracting a component into the part. */

    private void addorsub(bool add, ref Voxels? vox, Geez.Cycle? key,
            bool keepme, bool view_part) {
        assert(vox != null);
        if (add)
            voxels.BoolAdd(vox!);
        else
            voxels.BoolSubtract(vox!);
        if (view_part && voxels != null)
            this.key.voxels(voxels);
        if (key != null)
            key.clear();
        if (!keepme)
            vox = null;
    }
    public void add(ref Voxels? vox, Geez.Cycle? key=null, bool keepme=false,
            bool view_part=true)
        => addorsub(true, ref vox, key, keepme, view_part);
    public void sub(ref Voxels? vox, Geez.Cycle? key=null, bool keepme=false,
            bool view_part=true)
        => addorsub(false, ref vox, key, keepme, view_part);
}
