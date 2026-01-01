using static br.Br;
using br;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;

public class TwoPeasInAPod {

    /* GUIDE: how to create `make` (for smarties) */
    /* bitwise or (|) ANY/SEVERAL/ALL of these actions: */
    public const int ANYTHING = 0x1;
    public const int DRAWINGS = 0x2;
    public const int VOXELS   = 0x4;
    public const int LOOKSIE  = 0x8;
    /* bitwise or (|) ONE(1) of these peas: */
    public const int CHAMBER  = 1 << BITC_ACTION;
    public const int INJECTOR = 2 << BITC_ACTION;
    /* THATS ALL FOLKS */


    public const int DUMMY = 0;
    public const int BITC_ACTION = 4;
    public const int MASK_ACTION = (1 << BITC_ACTION) - 1;
    public const int MASK_PEA = ~MASK_ACTION;


    public interface Pea {
        string name { get; }
        Voxels voxels();
        void drawings(in Voxels vox);
        void anything();
    }


    public Chamber chamber { get; init; }
    public Injector injector { get; init; }

    public TwoPeasInAPod() {
        // Load the config files.
        string path_all = fromroot("../config/all.json");
        string path_extra = fromroot("../config/ammendments.json");
        Jason config = new(path_all);
        if (File.Exists(path_extra))
            config.overwrite_from(path_extra);

        { // Construct the chamber object.
            float max_phi = config.get<float>("printer/max_print_angle");
            config.set("chamber/phi_mani", max_phi);
            config.set("chamber/phi_fixt", max_phi);
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

    public static void entrypoint(int make, in Sectioner? sectioner) {
        // Setup geez.
        Geez.set_background_colour(dark: true);
        Geez.dflt_colour = new("#AB331A"); // copperish.
        Geez.dflt_metallic = 0.35f;
        Geez.dflt_roughness = 0.8f;
        if (sectioner != null)
            Geez.dflt_sectioner = sectioner;
        Geez.initialise();

        // Check dummy method.
        if (make == DUMMY) {
            Voxels? dummy_vox = dummy(out string? dummy_name);
            if (dummy_vox != null && dummy_name != null)
                save_voxels(dummy_name, dummy_vox);
            return;
        }

        // Check a pea and at least one action was given.
        assert(!isclr(make, MASK_ACTION));
        assert(!isclr(make, MASK_PEA));

        // Parse the objects.
        TwoPeasInAPod tpiap = new TwoPeasInAPod();
        Pea pea;
        switch (make & MASK_PEA) {
            case CHAMBER:  pea = tpiap.chamber; break;
            case INJECTOR: pea = tpiap.injector; break;
            default: throw new Exception("invalid pea");
        }

        // Test me.
        if (isset(make, ANYTHING))
            pea.anything();
        if ((make & MASK_ACTION) == ANYTHING)
            return;

        // Gimme voxels.
        Voxels? vox = get_voxels(pea, neednew: isset(make, VOXELS));
        if (vox == null) // unlucky.
            return;

        // Drawing sidequest.
        if (isset(make, DRAWINGS)) {
            print($"Making '{pea.name}' drawings...");
            print();
            pea.drawings(vox);
        }

        // Proper looksie.
        if (isset(make, LOOKSIE)) {
            print($"Giving '{pea.name}' a looksie...");
            print();
            Geez.clear();
            Geez.voxels(vox);
        }
    }



    public static void save_voxels(in string name, in Voxels vox,
            bool stl=true) {
        string path_vdb = fromroot($"exports/{name}.vdb");
        try {
            vox.SaveToVdbFile(path_vdb);
            print($"Saved to vdb: '{path_vdb}'");
        } catch (Exception e) {
            print($"Failed to export to vdb at '{path_vdb}'.");
            print("Exception log:");
            print(e.ToString());
            print();
            goto SKIP_VOXEL_SIZE;
        }
        // Save voxel size explicitly, since the vdb cant/doesnt check it matches
        // when loading it.
        string path_voxsize = fromroot($"exports/{name}.voxel_size");
        try {
            string text = VOXEL_SIZE.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            File.WriteAllText(path_voxsize, text);
            print($"  (saved voxel size to: '{path_voxsize}')");
        } catch (Exception e) {
            print($"Failed to save voxel size at '{path_voxsize}'.");
            print("Exception log:");
            print(e.ToString());
            print();
            /* fallthrough */
        }
      SKIP_VOXEL_SIZE:;

        if (stl) {
            string path_stl = fromroot($"exports/{name}.stl");
            try {
                Mesh mesh = new(vox);
                mesh.SaveToStlFile(path_stl);
                print($"Exported to stl: '{path_stl}'");
            } catch (Exception e) {
                print($"Failed to export to stl at '{path_stl}'.");
                print("Exception log:");
                print(e.ToString());
            }
        }
        print();
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
            print(e.ToString());
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
            print(e.ToString());
            print();
            goto FAILED;
        }

      FAILED:;
        vox = null;
        return false;
    }

    private static Voxels? get_voxels(in Pea pea, bool neednew=true) {
        Voxels? vox;
        if (!neednew) {
            if (load_voxels(pea.name, out vox))
                return vox;
        }
        print(neednew
            ? $"Generating '{pea.name}' voxels..."
            : $"Regenerating '{pea.name}' voxels..."
        );
        print();
        vox = pea.voxels();
        save_voxels(pea.name, vox);
        if (!neednew)
            Geez.clear();
        return vox;
    }

}