
using static Br;
using br;

Func<PicoGK.Voxels?> voxel_maker = Chamber.maker;
float voxel_size_mm = 0.8f;
bool section_view = true; // only affects viewing.
bool section_stl = false; // only affects stl export.
bool transparent = true; // only affects viewing (whats the alternative?).

// Setup sectioner.
Sectioner sectioner = Sectioner.pie(torad(0f), torad(270f));


// Task wrapper to configure some things and have nicer exception handling.
void wrapped_task() {
    try {
        // Configure some viewing options.
        PicoGK.Library.oViewer().SetBackgroundColor(new("#202020"));
        Geez.dflt_colour = new PicoGK.ColorFloat("#AB331A"); // copperish.
        Geez.dflt_alpha = transparent ? 0.8f : 1f;
        Geez.dflt_metallic = 0.35f;
        Geez.dflt_roughness = 0.8f;
        if (section_view)
            Geez.dflt_sectioner = sectioner;

        PicoGK.Library.Log("");
        PicoGK.Library.Log("whas good");

        PicoGK.Voxels? vox = voxel_maker();

        if (vox != null) {
            if (section_stl)
                vox = sectioner.cut(vox, inplace: true);
            string class_name = voxel_maker.Method.DeclaringType?.Name
                             ?? "rocket_ting";
            string stl_path = fromroot($"exports/{class_name}.stl");
            try {
                PicoGK.Mesh mesh = new(vox);
                mesh.SaveToStlFile(stl_path);
                PicoGK.Library.Log($"Exported to stl: {stl_path}");
            } catch (Exception e) {
                PicoGK.Library.Log("Failed to export to stl. Exception log:");
                PicoGK.Library.Log(e.ToString());
                PicoGK.Library.Log("");
            }
        }

        PicoGK.Library.Log("Don.");
        PicoGK.Library.Log("");

        // Now loop until window is closed, since picogk will stop the instant
        // this function returns (and the thread is terminated).

        // Cheeky private variable bypass.
        Type type = typeof(PicoGK.Library);
        System.Reflection.FieldInfo? field = type.GetField("bRunning",
                System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Static
            );
        bool running = true;
        while (running) {
            object? obj = field?.GetValue(null);
            running = (obj != null) ? (bool)obj : false;
            Thread.Sleep(50);
        }
    } catch (Exception e) {
        PicoGK.Library.Log("FAILED when running task. Exception log:");
        PicoGK.Library.Log(e.ToString());
        PicoGK.Library.Log("");
    }
}

// Create exports directory.
try {
    Directory.CreateDirectory(fromroot("exports"));
} catch (Exception e) {
    Console.WriteLine("FAILED to create exports directory. Exception log:");
    Console.WriteLine(e.ToString());
    Console.WriteLine();
    Console.WriteLine("Continuing regardless...");
    Console.WriteLine();
}

// Run picogk (+ task).
try {
    PicoGK.Library.Go(voxel_size_mm, wrapped_task, bEndAppWithTask: true);
} catch (Exception e) {
    Console.WriteLine("FAILED when executing PicoGK.");
    Console.WriteLine(e.ToString());
    Console.WriteLine();
}
