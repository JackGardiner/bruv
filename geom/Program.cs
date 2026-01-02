
using static br.Br;
using br;
using TPIAP = TwoPeasInAPod;


/* Boot up options: */
float voxel_size_mm = 2f;
int make = TPIAP.INJECTOR
         | TPIAP.VOXELS;
// See TPIAP for construction guide of `make`.
bool leave_window_running = false;
bool sectionview = false;
Sectioner sectioner = Sectioner.pie(torad(0f), torad(270f));



/* ok don touch anything else in the file. */

// Task wrapper to configure some things and have nicer exception handling.
void wrapped_task() {
    try {
        print();
        print("whas good");
        print();

        TPIAP.entrypoint(
            make,
            !leave_window_running,
            sectionview ? sectioner : null
        );

        print("Don" + (leave_window_running ? "." : ", closing window..."));
        print();

        // Now loop until window is closed, since picogk will stop the instant
        // this function returns (and the thread is terminated).
        if (leave_window_running) {
            // Cheeky private variable bypass.
            PervField gimme = new(typeof(PicoGK.Library), "bRunning");
            bool running = true;
            while (running) {
                running = gimme.maybe_get<bool?>() ?? false;
                Thread.Sleep(50);
            }
        }
    } catch (Exception e) {
        print("FAILED when running task.");
        print("Exception log:");
        print(e);
        print();
    }
}

try {
    // Create exports directory.
    try {
        Directory.CreateDirectory(fromroot("exports"));
    } catch (Exception e) {
        Console.WriteLine("FAILED to create exports directory.");
        Console.WriteLine("Exception log:");
        Console.WriteLine(e);
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
        Console.WriteLine(e);
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
        Console.WriteLine(e);
        Console.WriteLine();
    }

} finally {
    // Clear temp directory.
    try {
        Directory.Delete(fromroot("tmp"), recursive: true);
    } catch (Exception e) {
        Console.WriteLine("FAILED to wipe tmp directory.");
        Console.WriteLine("Exception log:");
        Console.WriteLine(e);
        Console.WriteLine();
    }
}
