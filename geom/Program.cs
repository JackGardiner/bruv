
try {
    string exports = PicoGK.Utils.strProjectRootFolder();
    exports = Path.Combine(exports, "exports");
    Directory.CreateDirectory(exports);

    PicoGK.Library.Go(
        1f,
        Chamber.Task
    );
} catch (Exception e) {
    Console.WriteLine("Failed to run Task.");
    Console.WriteLine(e.ToString());
}
