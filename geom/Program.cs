
using static br.Br;
using br;

using TPIAP = TwoPeasInAPod;

float voxel_size_mm = 0.4f;
TPIAP.make = TPIAP.MAKE_INJECTOR;
TPIAP.transparent = true; // only affects viewing (duh?)
TPIAP.section_view = true;
TPIAP.section_export = false;
TPIAP.sectioner = Sectioner.pie(torad(0f), torad(270f));


// Task wrapper to configure some things and have nicer exception handling.
void wrapped_task() {
    try {
        PicoGK.Library.Log("");
        PicoGK.Library.Log("whas good");

        TPIAP.entrypoint();

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
        PicoGK.Library.Log("FAILED when running task.");
        PicoGK.Library.Log("Exception log:");
        PicoGK.Library.Log(e.ToString());
        PicoGK.Library.Log("");
    }
}

try {
    // Create exports directory.
    try {
        Directory.CreateDirectory(fromroot("exports"));
    } catch (Exception e) {
        Console.WriteLine("FAILED to create exports directory.");
        Console.WriteLine("Exception log:");
        Console.WriteLine(e.ToString());
        Console.WriteLine();
        Console.WriteLine("Continuing regardless...");
        Console.WriteLine();
    }

    // Create temp directory.
    try {
        Directory.CreateDirectory(fromroot("tmp"));
    } catch (Exception e) {
        Console.WriteLine("FAILED to create tmp directory.");
        Console.WriteLine("Exception log:");
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
        Console.WriteLine("Exception log:");
        Console.WriteLine(e.ToString());
        Console.WriteLine();
    }

} finally {
    // Clear temp directory.
    try {
        Directory.Delete(fromroot("tmp"), recursive: true);
    } catch (Exception e) {
        Console.WriteLine("FAILED to wipe tmp directory.");
        Console.WriteLine("Exception log:");
        Console.WriteLine(e.ToString());
        Console.WriteLine();
    }
}
