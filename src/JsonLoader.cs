using System;
using System.IO;
using System.Text.Json;

public class EngineConfig
{
    // Use public auto-properties so the JSON deserializer reliably binds values.
    public double rChamber { get; set; }
    public double rThroat { get; set; }
    public double rExit { get; set; }
    public double chamberLength { get; set; }
    public double throatAngleDeg { get; set; }
    public double exitAngleDeg { get; set; }
}

public static class JsonLoader
{
    public static T LoadFromJsonFile<T>(string path)
    {
        string json = File.ReadAllText(path);

        var result = JsonSerializer.Deserialize<T>(json);
        if (result is null)
            throw new InvalidOperationException("Deserialization returned null");

        return result;
    }
}
