using static br.Br;
using br;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;

public class TwoPeasInAPod {

    public const int MAKE_DUMMY     = 0x0;
    public const int MAKE_CHAMBER   = 0x1;
    public const int MAKE_INJECTOR  = 0x2;
    public const int MAKE_BOTH      = 0x3;
    public static int make = 0;

    public static float voxel_size_mm = 0.7f;
    public static bool transparent = true;
    public static bool section_view = true;
    public static bool section_export = false;
    public static Sectioner sectioner = new();


    public PartMating pm { get; init; }
    public Chamber chamber { get; init; }
    public Injector injector { get; init; }

    public TwoPeasInAPod() {
        string path_all = fromroot("../config/all.json");
        string path_extra = fromroot("../config/ammendments.json");
        Jason config = new(path_all);
        if (File.Exists(path_extra))
            config.overwrite_from(path_extra);

        float max_phi = config.get<float>("printer/max_print_angle");
        config.set("chamber/phi_mani", max_phi);
        config.set("chamber/phi_inlet", -max_phi);
        config.set("chamber/phi_tc", max_phi);
        config.set("chamber/pm", config.get_map("part_mating"));

        pm = config.deserialise<PartMating>("part_mating");

        chamber = config.deserialise<Chamber>("chamber");
        chamber.initialise();

        // TODO:
        // injector = config.deserialise<Injector>("injector");
        // injector.initialise(pm);
        injector = null;
    }

    public static Voxels? dummy() {
        /* whatever your heart desires. */
        return null;
    }

    public static void entrypoint() {
        // Configure some viewing options.
        PicoGK.Library.oViewer().SetBackgroundColor(new("#202020"));
        Geez.dflt_colour = new PicoGK.ColorFloat("#AB331A"); // copperish.
        Geez.dflt_alpha = transparent ? 0.8f : 1f;
        Geez.dflt_metallic = 0.35f;
        Geez.dflt_roughness = 0.8f;
        if (section_view)
            Geez.dflt_sectioner = sectioner;

        // Parse the objects.
        TwoPeasInAPod tpiap = new();

        // Make the voxels.
        Voxels? vox = null;
        string? name = null;
        switch (make) {
            case MAKE_DUMMY:
                vox = dummy();
                name = "dummy";
                break;
            case MAKE_CHAMBER:
                vox = tpiap.chamber.voxels();
                name = "chamber";
                break;
            case MAKE_INJECTOR:
                // vox = tpiap.injector.voxels();
                // name = "injector";
                break;
            case MAKE_BOTH:
                // TODO:
                break;
        }

        // Save the voxels.
        if (vox != null && name != null) {
            if (section_export)
                vox = sectioner.cut(vox, inplace: true);

            string path_vdb = fromroot($"exports/{name}.vdb");
            try {
                vox.SaveToVdbFile(path_vdb);
                PicoGK.Library.Log($"Exported to vdb: '{path_vdb}'");
            } catch (Exception e) {
                PicoGK.Library.Log($"Failed to export to vdb at '{path_vdb}'.");
                PicoGK.Library.Log("Exception log:");
                PicoGK.Library.Log(e.ToString());
                PicoGK.Library.Log("");
            }

            string path_stl = fromroot($"exports/{name}.stl");
            try {
                Mesh mesh = new(vox);
                mesh.SaveToStlFile(path_stl);
                PicoGK.Library.Log($"Exported to stl: '{path_stl}'");
            } catch (Exception e) {
                PicoGK.Library.Log($"Failed to export to stl at '{path_stl}'.");
                PicoGK.Library.Log("Exception log:");
                PicoGK.Library.Log(e.ToString());
                PicoGK.Library.Log("");
            }
        }
    }
}