
var voxel_size_mm = 0.7f;
var task = Chamber.Task;
bool sectioned = true; // only affects viewing.
bool transparent = true; // also only affects viewing (whats the alternative?).


// Task wrapper to configure some things and have nicer exception handling.
void wrapped_task() {
    try {
        // Configure some viewing options.
        PicoGK.Library.oViewer().SetBackgroundColor(new("#202020"));
        Geez.dflt_colour = new PicoGK.ColorFloat("#AB331A"); // copperish.
        Geez.dflt_alpha = transparent ? 0.8f : 1f;
        Geez.dflt_metallic = 0.35f;
        Geez.dflt_roughness = 0.8f;
        Geez.dflt_sectioned = sectioned;

        task();

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
            Thread.Sleep(5);
        }
    } catch (Exception e) {
        PicoGK.Library.Log("FAILED when running task. Exception log:");
        PicoGK.Library.Log(e.ToString());
        PicoGK.Library.Log("");
    }
}

// Create exports directory.
try {
    string exports = PicoGK.Utils.strProjectRootFolder();
    exports = Path.Combine(exports, "exports");
    Directory.CreateDirectory(exports);
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
