using static br.Br;
using br;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;

public class TwoPeasInAPod {

    public const int DUMMY = 0;
    public const int MASK_ACTION = 0b0011;
    public const int DRAWINGS    = 0b0001;
    public const int VOXELS      = 0b0010;
    public const int MASK_OBJECT = 0b1100;
    public const int CHAMBER     = 0b0100;
    public const int INJECTOR    = 0b1000;
    public const int CHAMBER_VOXELS    = CHAMBER | VOXELS;
    public const int CHAMBER_DRAWINGS  = CHAMBER | DRAWINGS;
    public const int INJECTOR_VOXELS   = INJECTOR | VOXELS;
    public const int INJECTOR_DRAWINGS = INJECTOR | DRAWINGS;

    public static int make = DUMMY; // ^ one of those constants.

    public static float voxel_size_mm = 0.7f;
    public static bool transparent = true;
    public static bool sectionview = true;
    public static Sectioner sectioner = new();


    public PartMating pm { get; init; }
    public Chamber chamber { get; init; }
    public Injector injector { get; init; }

    public TwoPeasInAPod() {
        // Configure some viewing options.
        Geez.initialise();
        PICOGK_VIEWER.SetFov(70f);
        PICOGK_VIEWER.SetBackgroundColor(new("#202020"));
        Geez.dflt_colour = new("#AB331A"); // copperish.
        Geez.dflt_alpha = transparent ? 0.8f : 1f;
        Geez.dflt_metallic = 0.35f;
        Geez.dflt_roughness = 0.8f;
        if (sectionview)
            Geez.dflt_sectioner = sectioner;

        // Load the config files.
        string path_all = fromroot("../config/all.json");
        string path_extra = fromroot("../config/ammendments.json");
        Jason config = new(path_all);
        if (File.Exists(path_extra))
            config.overwrite_from(path_extra);

        // Create the shared info struct.
        pm = config.deserialise<PartMating>("part_mating");

        { // Construct the chamber object.
            float max_phi = config.get<float>("printer/max_print_angle");
            config.set("chamber/phi_mani", max_phi);
            config.set("chamber/phi_inlet", -max_phi);
            config.set("chamber/phi_tc", max_phi);
            config.set("chamber/pm", config.get_map("part_mating"));
            chamber = config.deserialise<Chamber>("chamber");
            chamber.initialise();
        }

        { // Construct the injector object.
            float max_phi = config.get<float>("printer/max_print_angle");
            config.set("injector/fPrintAngle", max_phi);
            config.set("injector/pm", config.get_map("part_mating"));
            injector = config.deserialise<Injector>("injector");
            injector.initialise();
        }
    }

    public static Voxels? dummy(out string? name) {
        /* whatever your heart desires. */
        name = null;
        return null;
    }

    private delegate Voxels voxelsF();
    private class GetVoxels {
        public string name { get; set; }
        public voxelsF voxels_f { get; set; }
        public bool neednew { get; set; }
        public GetVoxels(string name, voxelsF voxels_f, bool neednew=true) {
            this.name = name;
            this.voxels_f = voxels_f;
            this.neednew = neednew;
        }
        public Voxels? voxels() {
            string path_vdb = fromroot($"exports/{name}.vdb");
            Voxels? vox;
            if (!neednew) {
                if (load_voxels(name, out vox)) {
                    log();
                    return vox;
                }
            }
            log(neednew
                ? $"Generating '{name}' voxels..."
                : $"Regenerating '{name}' voxels..."
            );
            log();
            vox = voxels_f();
            log();
            save_voxels(name, vox);
            log();
            if (!neednew)
                Geez.clear();
            return vox;
        }
    }
    private delegate void drawingsF(in Voxels part);
    private class Drawer { // cupboard type shi
        public string name { get; set; }
        public drawingsF drawings_f { get; set; }
        public Drawer(string name, drawingsF drawings_f) {
            this.name = name;
            this.drawings_f = drawings_f;
        }
        public void drawings(in Voxels part) {
            log($"Making '{name}' drawings...");
            log();
            drawings_f(part);
            log();
        }
    }

    public static void entrypoint() {
        // Check dummy method.
        if (make == DUMMY) {
            Voxels? dummy_vox = dummy(out string? dummy_name);
            if (dummy_vox != null && dummy_name != null)
                save_voxels(dummy_name, dummy_vox);
            return;
        }

        // Gotta be either voxels or drawings and either chamber or injector.
        assert(!isclr(make, MASK_ACTION));
        assert(!isclr(make, MASK_OBJECT));

        // Parse the objects.
        TwoPeasInAPod tpiap = new();

        // Make the voxels.
        bool neednew = isset(make, VOXELS);
        GetVoxels getter;
        switch (make & MASK_OBJECT) {
            case CHAMBER:
                getter = new("chamber", tpiap.chamber.voxels);
                break;
            case INJECTOR:
                getter = new("injector", tpiap.injector.voxels);
                break;
            default:
                throw new Exception("missing object");
        }
        getter.neednew = isset(make, VOXELS);
        Voxels? vox = getter.voxels();
        if (vox == null) // unlucky.
            return;

        // Do drawings if requested.
        if (isset(make, DRAWINGS)) {
            Drawer drawer;
            switch (make & MASK_OBJECT) {
                case CHAMBER:
                    drawer = new("chamber", tpiap.chamber.drawings);
                    break;
                case INJECTOR:
                    drawer = new("chamber", tpiap.chamber.drawings);
                    // drawer = new("injector", tpiap.injector.drawings);
                    // TODO: uncomment ^
                    break;
                default:
                    throw new Exception("missing object");
            }
            drawer.drawings(vox);
        }
    }



    public static void save_voxels(in string name, in Voxels vox,
            bool stl=true) {
        string path_vdb = fromroot($"exports/{name}.vdb");
        try {
            vox.SaveToVdbFile(path_vdb);
            log($"Saved to vdb: '{path_vdb}'");
        } catch (Exception e) {
            log($"Failed to export to vdb at '{path_vdb}'.");
            log("Exception log:");
            log(e.ToString());
            log();
            goto SKIP_VOXEL_SIZE;
        }
        // Save voxel size explicitly, since the vdb cant/doesnt check it matches
        // when loading it.
        string path_voxsize = fromroot($"exports/{name}.voxel_size");
        try {
            string text = VOXEL_SIZE.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            File.WriteAllText(path_voxsize, text);
            log($"  (saved voxel size to: '{path_voxsize}')");
        } catch (Exception e) {
            log($"Failed to save voxel size at '{path_voxsize}'.");
            log("Exception log:");
            log(e.ToString());
            log();
            /* fallthrough */
        }
      SKIP_VOXEL_SIZE:;

        if (stl) {
            string path_stl = fromroot($"exports/{name}.stl");
            try {
                Mesh mesh = new(vox);
                mesh.SaveToStlFile(path_stl);
                log($"Exported to stl: '{path_stl}'");
            } catch (Exception e) {
                log($"Failed to export to stl at '{path_stl}'.");
                log("Exception log:");
                log(e.ToString());
                log();
            }
        }
    }

    public static bool load_voxels(in string name, out Voxels? vox) {
        string path_vdb = fromroot($"exports/{name}.vdb");
        string path_voxsize = fromroot($"exports/{name}.voxel_size");
        // Ensure all files are chilling.
        if (!File.Exists(path_vdb)) {
            log($"No voxels at: '{path_vdb}'");
            goto FAILED;
        }
        FileInfo fileInfo = new(path_voxsize);
        if (!fileInfo.Exists) {
            log($"Missing voxel size at: '{path_voxsize}'");
            goto FAILED;
        }
        if (fileInfo.Length > 1024*1024 /* 1MB */) {
            log($"Voxel size file invalid (too large) at: '{path_voxsize}'");
            goto FAILED;
        }
        // Ensure voxel size matches.
        try {
            string text = File.ReadAllText(path_voxsize);
            float voxsize = float.Parse(text,
                    System.Globalization.CultureInfo.InvariantCulture);
            if (voxsize != VOXEL_SIZE) {
                log($"Voxel size mismatch at: '{path_voxsize}'");
                log($"  (needed {VOXEL_SIZE}, found {voxsize})");
                goto FAILED;
            }
        } catch (Exception e) {
            log($"Failed to read voxel size at '{path_vdb}'.");
            log("Exception log:");
            log(e.ToString());
            log();
            goto FAILED;
        }
        // Read dem voxels.
        try {
            vox = Voxels.voxFromVdbFile(path_vdb);
            log($"Loaded from vdb: '{path_vdb}'");
            return true;
        } catch (Exception e) {
            log($"Failed to load from vdb at '{path_vdb}'.");
            log("Exception log:");
            log(e.ToString());
            log();
            goto FAILED;
        }

      FAILED:;
        vox = null;
        return false;
    }
}