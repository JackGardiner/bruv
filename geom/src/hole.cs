using static br.Br;

using Vec2 = System.Numerics.Vector2;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;

namespace br {

public class GPort {
    public string size { get; }
    public float downstream_diameter { get; }

    public float face_diameter { get; }
    public float pilot_bore_diameter { get; }
    public float required_thread_length { get; }

    public float downstream_radius => 0.5f*downstream_diameter;
    public float face_radius       => 0.5f*face_diameter;
    public float pilot_bore_radius => 0.5f*pilot_bore_diameter;

    public float total_bore_depth // empirical rule:
        => required_thread_length + 0.5f*pilot_bore_diameter;


    public GPort(string size, float downstream_diameter) {
        this.size = size;
        this.downstream_diameter = downstream_diameter;

        Dictionary<string, (float, float, float)> lookup = new([
            /*     size, (face diam, pilot bore diam, req thread length) */
            new("1/8in", (    17.2f,           9.73f,             10.5f)),
            new("1/4in", (    20.7f,           13.3f,             15.5f)),
            new("1/2in", (    34.0f,           21.1f,             19.0f)),
            new("3/4in", (    40.0f,           26.6f,             20.5f)),
        ]);
        face_diameter          = lookup[size].Item1;
        pilot_bore_diameter    = lookup[size].Item2;
        required_thread_length = lookup[size].Item3;
    }

    private static string _from_size_mm(float size) {
        if (nearto(size, 25.4f*(1/8f)))
            return "1/8in";
        if (nearto(size, 25.4f*(1/4f)))
            return "1/4in";
        if (nearto(size, 25.4f*(1/2f)))
            return "1/2in";
        if (nearto(size, 25.4f*(3/4f)))
            return "3/4in";
        throw new Exception($"unknown port size mm: {size}");
    }
    public GPort(float size, float downstream_diameter)
        : this(_from_size_mm(size), downstream_diameter) {}


    public Voxels filled(Frame face_out, out float Lz) {
        // Choose cone length s.t. angle = 45deg.
        float cone_length = abs(pilot_bore_radius - downstream_radius);
        float straight_length = total_bore_depth - cone_length;

        Lz = total_bore_depth;

        List<Vec2> points = [
            new(0f,                 0f),
            new(0f,                 pilot_bore_radius),
            new(-straight_length,   pilot_bore_radius),
            new(-total_bore_depth,  downstream_radius),
            new(-Lz,                downstream_radius),
            new(-Lz,                0f),
        ];
        Polygon.cull_adjacent_duplicates(points);

        Mesh mesh = Polygon.mesh_revolved(face_out, points);
        return new(mesh);
    }

    public Voxels shelled(Frame face_out, float th, out float Lz) {
        assert(th > 0f);

        // Same as neg but thickened.
        float cone_length = abs(pilot_bore_radius - downstream_radius);
        float straight_length = total_bore_depth - cone_length;

        // since we always choose cone with phi=+-45deg.
        float Dz = th*tan(PI/8f);
        if (nearto(pilot_bore_diameter, downstream_diameter))
            Dz = 0f;
        else if (pilot_bore_diameter < downstream_diameter)
            Dz = -Dz;

        Lz = total_bore_depth + max(Dz, 0f);

        List<Vec2> points = [
            new(0f,                      pilot_bore_radius),
            new(0f,                      pilot_bore_radius + th),
            new(-straight_length - Dz,   pilot_bore_radius + th),
            new(-total_bore_depth - Dz,  downstream_radius + th),
            new(-Lz,                     downstream_radius + th),
            new(-Lz,                     downstream_radius),
            new(-total_bore_depth,       downstream_radius),
            new(-straight_length,        pilot_bore_radius),
        ];
        Polygon.cull_adjacent_duplicates(points);

        Mesh mesh = Polygon.mesh_revolved(face_out, points, donut: true);
        return new(mesh);
    }
}

}
