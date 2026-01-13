using static br.Br;
using br;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;

public static class TwoPeasInAPod {

    public interface Pea {
        string name { get; }

        void anything();
        Voxels? voxels();
        Voxels? cutaway(in Voxels part);
        void drawings(in Voxels part);

        void set_modifiers(int mods);
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
    public const int FILLETLESS       = 0x02 << BITC_ACTION;
    public const int TAKE_SCREENSHOTS = 0x04 << BITC_ACTION;
    public const int LOOKIN_FANCY     = 0x08 << BITC_ACTION;
    public const int BRANDINGLESS     = 0x10 << BITC_ACTION;
    /* bitwise or (|) ONE(1) of these peas: */
    public const int CHAMBER  = 1 << (BITC_ACTION + BITC_MODIFIER);
    public const int INJECTOR = 2 << (BITC_ACTION + BITC_MODIFIER);
    public const int INJECTOR_STU = 4 << (BITC_ACTION + BITC_MODIFIER);
    /* THATS ALL FOLKS */


    public const int DUMMY = 0;
    public const int BITC_ACTION = 6;
    public const int MASK_ACTION = (1 << BITC_ACTION) - 1;
    public const int BITC_MODIFIER = 5;
    public const int MASK_MODIFIER = ((1 << BITC_MODIFIER) - 1) << BITC_ACTION;
    public const int MASK_PEA = ~(MASK_ACTION | MASK_MODIFIER);


    public static void entrypoint(int make, bool shutdown_on_exit,
            in Sectioner? sectioner) {
        try {
            // Setup geez.
            Geez.set_background_colour(dark: true);
            Geez.dflt_colour = new("#AB331A"); // copperish.
            Geez.dflt_metallic = 0.35f;
            Geez.dflt_roughness = 0.8f;
            if (sectioner != null)
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
            string barcelona = fromroot("barcelona/Barcelona.zip");
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
                float max_phi = config.get<float>("printer/max_print_angle");
                config.set("chamber/phi_mani", max_phi);
                config.set("chamber/phi_fixt", max_phi);
                config.set("chamber/phi_inlet", -max_phi);
                config.set("chamber/phi_tc", max_phi);
                config.set("chamber/pm", config.get_map("part_mating"));
                Chamber chamber = config.deserialise<Chamber>("chamber");
                chamber.initialise();
                pea = chamber;
            } break;

            case INJECTOR: {
                // Construct the injector object.
                float max_phi = config.get<float>("printer/max_print_angle");
                config.set("injector/fPrintAngle", max_phi);
                config.set("injector/pm", config.get_map("part_mating"));
                Injector injector = config.deserialise<Injector>("injector");
                injector.initialise();
                pea = injector;
            } break;

            case INJECTOR_STU: {
                // Construct stu's injector object.
                config.set("injectorstu/pm", config.get_map("part_mating"));
                InjectorStu stu = config.deserialise<InjectorStu>("injectorstu");
                stu.initialise();
                pea = stu;
            } break;

            default:
                throw new Exception("invalid 'make': unrecognised pea "
                                 + $"0x{make & MASK_PEA:X}");
        }

        // Modify the objects.
        pea.set_modifiers(make & MASK_MODIFIER);

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
            Geez.clear();
            Geez.voxels(part.voxels()!);
            print("press any key when finished viewing to continue...");
            Console.ReadKey(intercept: true);
            print();
        }

        // Additional proper looksie.
        if (isset(make, LOOKSIE_CUTAWAY) && cutaway.succeeded(part.voxels())) {
            print($"Giving '{pea.name}' a cutawayed looksie...");
            print();
            Geez.clear();
            Geez.voxels(cutaway.voxels(part.voxels()!)!);
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
        var actions = Perv.get<Queue<PicoGK.Viewer.IViewerAction>>(
            PICOGK_VIEWER,
            "m_oActions"
        );
        lock (actions)
            actions.Enqueue(new PrintAction(msgs));
    }
}