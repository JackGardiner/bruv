
using static br.Br;
using br;
using TPIAP = TwoPeasInAPod;


/* Boot up options: */

int make = TPIAP.INJECTOR | TPIAP.VOXELS;
// See TPIAP for construction guide of `make`.

float voxel_size_mm = 0.4f;
bool leave_viewer_open = yeah;
Sectioner sectioner = Sectioner.pie(0f, PI);



/* ok don touch anything else in the file. */

// Task wrapper to configure some things and have nicer exception handling.
void wrapped_task() {
    try {
        print();
        print("whas good");
        print();

        TPIAP.entrypoint(make, !leave_viewer_open, sectioner);

        print("Don" + (leave_viewer_open ? "." : ", closing window..."));
        print();

        // Now loop until window is closed, since picogk will stop the instant
        // this function returns (and the thread is terminated).
        if (leave_viewer_open) {
            // Cheeky private variable bypass.
            PervField gimme = new(typeof(PicoGK.Library), "bRunning");
            for (;;) {
                bool running = gimme.maybe_get<bool?>() ?? false;
                if (!running)
                    break;
                Thread.Sleep(100);
            }
        }
    } catch (Exception e) {
        print("FAILED when running task.");
        print("Exception log:");
        print(e);
        print();
    }
}


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

// Run picogk (+ task).
try {
    PicoGK.Library.Go(voxel_size_mm, wrapped_task, bEndAppWithTask: true);
} catch (Exception e) {
    Console.WriteLine("FAILED when executing PicoGK.");
    Console.WriteLine("Exception log:");
    Console.WriteLine(e);
    Console.WriteLine();
}
