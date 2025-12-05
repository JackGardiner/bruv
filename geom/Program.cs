
var voxel_size_mm = 0.1f;
var task = Chamber.Task;


// Task wrapper to configure some things and have nicer exception handling.
void wrapped_task() {
    try {
        PicoGK.Library.oViewer().SetBackgroundColor(new("#282B2E"));

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
