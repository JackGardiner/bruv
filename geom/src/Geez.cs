using static Br;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using Triangle = PicoGK.Triangle;
using PolyLine = PicoGK.PolyLine;
using BBox3 = PicoGK.BBox3;
using Colour = PicoGK.ColorFloat;

public class Geez {
    public static Colour? colour = null;
    public static float? alpha = null;
    public static float? metallic = null;
    public static float? roughness = null;
    public static bool? sectioned = null;

    public static Colour dflt_colour = new Colour("#FF0000");
    public static float dflt_alpha = 1f;
    public static float dflt_metallic = 0.35f;
    public static float dflt_roughness = 0.5f;
    public static bool dflt_sectioned = false;


    public static IDisposable like(Colour? colour=null, float? alpha=null,
            float? metallic=null, float? roughness=null, bool? sectioned=null) {
        var colour0 = Geez.colour;
        var alpha0 = Geez.alpha;
        var metallic0 = Geez.metallic;
        var roughness0 = Geez.roughness;
        var sectioned0 = Geez.sectioned;
        Geez.colour = colour ?? colour0;
        Geez.alpha = alpha ?? alpha0;
        Geez.metallic = metallic ?? metallic0;
        Geez.roughness = roughness ?? roughness0;
        Geez.sectioned = sectioned ?? sectioned0;
        return new OnLeave(() => {
                Geez.colour = colour0;
                Geez.alpha = alpha0;
                Geez.metallic = metallic0;
                Geez.roughness = roughness0;
                Geez.sectioned = sectioned0;
            });
    }
    public static IDisposable dflt_like(Colour? colour=null, float? alpha=null,
            float? metallic=null, float? roughness=null, bool? sectioned=null) {
        var colour0 = Geez.dflt_colour;
        var alpha0 = Geez.dflt_alpha;
        var metallic0 = Geez.dflt_metallic;
        var roughness0 = Geez.dflt_roughness;
        var sectioned0 = Geez.dflt_sectioned;
        Geez.dflt_colour = colour ?? colour0;
        Geez.dflt_alpha = alpha ?? alpha0;
        Geez.dflt_metallic = metallic ?? metallic0;
        Geez.dflt_roughness = roughness ?? roughness0;
        Geez.dflt_sectioned = sectioned ?? sectioned0;
        return new OnLeave(() => {
                Geez.dflt_colour = colour0;
                Geez.dflt_alpha = alpha0;
                Geez.dflt_metallic = metallic0;
                Geez.dflt_roughness = roughness0;
                Geez.dflt_sectioned = sectioned0;
            });
    }
    private class OnLeave : IDisposable {
        protected readonly Action _func;
        public OnLeave(Action func) { _func = func; }
        public void Dispose() { _func(); }
    }


    protected static Dictionary<int, (List<PolyLine>, List<Mesh>)> _geezed
            = new();
    protected static int _next = 1; // must leave 0 as illegal.
    protected static int _track(in List<PolyLine> lines, in List<Mesh> meshes) {
        int key = _next++;
        _geezed.Add(key, (lines, meshes));
        return key;
    }
    protected static void _view(in List<PolyLine> lines, in List<Mesh> meshes) {
        Vec3? point = null;
        foreach (PolyLine line in lines) {
            PicoGK.Library.oViewer().Add(line);
            if (point == null && line.nVertexCount() > 0)
                point = line.vecVertexAt(0);
        }
        foreach (Mesh mesh in meshes) {
            Leap71.ShapeKernel.Sh.PreviewMesh(
                mesh,
                clrColor: colour ?? dflt_colour,
                fTransparency: alpha ?? dflt_alpha,
                fMetallic: metallic ?? dflt_metallic,
                fRoughness: roughness ?? dflt_roughness
            );
        }
        // Dummy mesh to fix bug in picogk polyline colouring. Basically just
        // ensure a mesh is rendered after every line, otherwise the lines are
        // drawn white.
        if (_dummy != null)
            PicoGK.Library.oViewer().Remove(_dummy);
        _dummy = null;
        if (point != null) {
            _dummy = _dummy_og.mshCreateTransformed(ONE3, point.Value);
            Leap71.ShapeKernel.Sh.PreviewMesh(_dummy, COLOUR_BLACK);
        }
    }
    protected static int _push(in List<Mesh> meshes) {
        _view([], meshes);
        return _track([], meshes);
    }
    protected static int _push(in List<PolyLine> lines) {
        _view(lines, []);
        return _track(lines, []);
    }
    protected static int _push(in List<PolyLine> lines, in List<Mesh> meshes) {
        _view(lines, meshes);
        return _track(lines, meshes);
    }
    protected static int _push(in List<Mesh> meshes, in List<PolyLine> lines) {
        _view(lines, meshes);
        return _track(lines, meshes);
    }


    public static int recent(int ignore=0) {
        assert(numel(_geezed) > 0);
        assert(ignore >= 0);
        int key = _next;
        for (int i=-1; i<ignore; ++i) {
            while (!_geezed.ContainsKey(key - 1)) // O(n) skull emoji.
                --key;
            --key;
        }
        return key;
    }
    public static void pop(int count, int ignore=0) {
        assert(count <= numel(_geezed));
        assert(count >= 0);
        assert(ignore >= 0);
        while (count --> 0) // so glad down-to operator made it into c#.
            remove(recent(ignore));
    }
    public static void remove(int key) {
        (List<PolyLine> lines, List<Mesh> meshes) = _geezed[key];
        _geezed.Remove(key);

        foreach (Mesh mesh in meshes)
            PicoGK.Library.oViewer().Remove(mesh);
        foreach (PolyLine line in lines)
            PicoGK.Library.oViewer().Remove(line);
    }
    public static void remove(List<int> keys) {
        foreach (int key in keys)
            remove(key);
    }
    public static void clear() {
        List<int> keys = new(_geezed.Keys);
        remove(keys);
    }


    public class Cycle {
        protected int[] keys; // rolling buffer.
        protected int i; // next index.
        public Cycle(int size=1) {
            assert(size > 0);
            keys = new int[size];
            i = 0;
        }

        public static Cycle operator<<(Cycle c, int? key) {
            assert((key ?? 0) >= 0);
            if (c.keys[c.i] != 0)
                Geez.remove(c.keys[c.i]);
            c.keys[c.i] = key ?? 0;
            c.i = (c.i == numel(c.keys) - 1) ? 0 : c.i + 1;
            return c;
        }

        public void clear() {
            for (int j=0; j<numel(keys); ++j) {
                if (keys[j] != 0)
                    Geez.remove(keys[j]);
                keys[j] = 0;
            }
        }
    }


    public static int voxels(in Voxels vox, Colour? colour=null) {
        // Cross section if requested.
        Voxels new_vox = vox;
        if (sectioned ?? dflt_sectioned) {
            BBox3 bounds = vox.oCalculateBoundingBox();
            if (bounds.vecMin.X < 0f) {
                bounds.vecMin.X = 0f;
                Voxels box = new(PicoGK.Utils.mshCreateCube(bounds));
                new_vox = new_vox.voxBoolIntersect(box);
            }
        }
        Mesh mesh = new Mesh(new_vox);
        return Geez.mesh(mesh, colour: colour);
    }

    public static int mesh(in Mesh mesh, Colour? colour=null) {
        using (like(colour: colour))
            return _push([mesh]);
    }


    protected static Mesh _ball_hires;
    protected static Mesh _ball_lores;
    public static int point(in Vec3 p, float r=2f, Colour? colour=null,
            bool hires=true) {
        return Geez.points([p], r: r, colour: colour, hires: hires);
    }

    public static int points(in List<Vec3> ps, float r=2f, Colour? colour=null,
            bool hires=false) {
        Mesh ball = hires ? _ball_hires : _ball_lores;
        List<Mesh> meshes = new();
        foreach (Vec3 p in ps) {
            Mesh mesh = ball.mshCreateTransformed(r*ONE3, p);
            meshes.Add(mesh);
        }
        using (like(colour: colour, alpha: 1f, metallic: 0.1f, roughness: 0f))
            return _push(meshes);
    }

    public static int line(in PolyLine line) {
        return Geez.lines([line]);
    }
    public static int line(in List<Vec3> points, Colour? colour=null,
            float arrow=0f) {
        PolyLine line = new(colour ?? COLOUR_RED);
        line.Add(points);
        if (arrow > 0f)
            line.AddArrow(arrow);
        return Geez.line(line);
    }
    public static int lines(in List<PolyLine> lines) {
        return _push(lines);
    }

    protected static void _frame_lines(out Mesh? mesh, List<PolyLine> lines,
            in Frame frame, float size, bool pos) {
        PolyLine X = new(COLOUR_RED);
        X.nAddVertex(frame.pos);
        X.nAddVertex(frame.pos + size*frame.X);
        X.AddArrow(0.2f*size);

        PolyLine Y = new(COLOUR_GREEN);
        Y.nAddVertex(frame.pos);
        Y.nAddVertex(frame.pos + size*frame.Y);
        Y.AddArrow(0.2f*size);

        PolyLine Z = new(COLOUR_BLUE);
        Z.nAddVertex(frame.pos);
        Z.nAddVertex(frame.pos + size*frame.Z);
        Z.AddArrow(0.2f*size);

        lines.Add(X);
        lines.Add(Y);
        lines.Add(Z);

        mesh = (!pos) ? null
             : _ball_lores.mshCreateTransformed(0.05f*size*ONE3, frame.pos);
    }
    public static int frame(in Frame frame, float size=5f, bool pos=true,
            Colour? col=null) {
        return Geez.frames([frame], size: size, pos: pos, colour: colour);
    }
    public static int frames(in List<Frame> frames, float size=5f,
            bool pos=true, Colour? colour=null) {
        List<PolyLine> lines = new();
        List<Mesh> meshes = new();
        foreach (Frame frame in frames) {
            _frame_lines(out Mesh? mesh, lines, frame, size, pos);
            if (mesh != null)
                meshes.Add(mesh);
        }
        using (like(colour: colour ?? COLOUR_WHITE, alpha: 1f, metallic: 0f,
                roughness: 0f))
            return _push(lines, meshes);
    }

    protected static void _bbox_lines(List<PolyLine> lines, in BBox3 bbox,
            Colour? colour) {
        Colour col = colour ?? Geez.colour ?? Geez.dflt_colour;
        Vec3 p000 = bbox.vecMin;
        Vec3 p111 = bbox.vecMax;
        Vec3 p100 = p000 + uX3*(p111 - p000);
        Vec3 p010 = p000 + uY3*(p111 - p000);
        Vec3 p001 = p000 + uZ3*(p111 - p000);
        Vec3 p011 = p000 + (uY3 + uZ3)*(p111 - p000);
        Vec3 p101 = p000 + (uX3 + uZ3)*(p111 - p000);
        Vec3 p110 = p000 + (uX3 + uY3)*(p111 - p000);
        PolyLine l;

        // Each corner (+3+3), then the zigzag joining (+6).

        l = new(col);
        l.nAddVertex(p000);
        l.nAddVertex(p100);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p000);
        l.nAddVertex(p010);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p000);
        l.nAddVertex(p001);
        lines.Add(l);

        l = new(col);
        l.nAddVertex(p111);
        l.nAddVertex(p011);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p111);
        l.nAddVertex(p101);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p111);
        l.nAddVertex(p110);
        lines.Add(l);

        l = new(col);
        l.nAddVertex(p100);
        l.nAddVertex(p101);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p101);
        l.nAddVertex(p001);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p001);
        l.nAddVertex(p011);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p011);
        l.nAddVertex(p010);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p010);
        l.nAddVertex(p110);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p110);
        l.nAddVertex(p100);
        lines.Add(l);
    }
    public static int bbox(in BBox3 bbox, Colour? colour=null) {
        return Geez.bboxes([bbox], colour);
    }
    public static int bboxes(in List<BBox3> bboxes, Colour? colour=null) {
        List<PolyLine> lines = new();
        using (Geez.dflt_like(colour: COLOUR_CYAN)) {
            foreach (BBox3 bbox in bboxes)
                _bbox_lines(lines, bbox, colour);
        }
        return _push(lines);
    }




    static Mesh _dummy_og;
    static Mesh? _dummy;
    static Geez() {
        // Dummy mesh to fix polyline colouring bug.
        _dummy_og = new();
        _dummy_og.AddVertices(
            [new(0,0,0), new(1e-3f,0,0), new(0,1e-3f,0), new(0,0,1e-3f)],
            out _
        );
        _dummy_og.nAddTriangle(0, 2, 1);
        _dummy_og.nAddTriangle(0, 1, 3);
        _dummy_og.nAddTriangle(0, 3, 2);
        _dummy_og.nAddTriangle(1, 2, 3);
        _dummy = null;

        // Create the unit origin ball mesh, starting with an icosahedron then
        // subdividing.

        float phi = 0.5f*(1f + sqrt(5f));
        List<Vec3> V = [
            new(-1,    phi,  0),   new( 1,    phi,  0),   new(-1,   -phi,  0),
            new( 1,   -phi,  0),   new( 0,   -1,    phi), new( 0,    1,    phi),
            new( 0,   -1,   -phi), new( 0,    1,   -phi), new( phi,  0,   -1),
            new( phi, 0,    1),    new(-phi,  0,   -1),   new(-phi,  0,    1)
        ];
        for (int i=0; i<numel(V); ++i)
            V[i] = normalise(V[i]);
        List<Triangle> T = [
            new(0,  11, 5),  new(0, 5, 1),  new(0, 1,  7), new(0,  7,  10),
            new(0,  10, 11), new(1, 5, 9),  new(5, 11, 4), new(11, 10, 2),
            new(10, 7,  6),  new(7, 1, 8),  new(3, 9,  4), new(3,  4,  2),
            new(3,  2,  6),  new(3, 6, 8),  new(3, 8,  9), new(4,  9,  5),
            new(2,  4,  11), new(6, 2, 10), new(8, 6,  7), new(9,  8,  1)
        ];
        void subdivide() {
            List<Triangle> newT = new();
            Dictionary<ulong, int> midpoints = new();
            int midpoint(int a, int b) {
                ulong key = (a < b) ? ((ulong)a << 32) | (uint)b
                                    : ((ulong)b << 32) | (uint)a;
                if (midpoints.TryGetValue(key, out int value))
                    return value;
                Vec3 m = normalise(V[a] + V[b]);
                V.Add(m);
                int index = numel(V) - 1;
                midpoints[key] = index;
                return index;
            }
            foreach (Triangle t in T) {
                int a = t.A;
                int b = t.B;
                int c = t.C;
                int ab = midpoint(a, b);
                int bc = midpoint(b, c);
                int ca = midpoint(c, a);
                newT.Add(new Triangle(a, ab, ca));
                newT.Add(new Triangle(b, bc, ab));
                newT.Add(new Triangle(c, ca, bc));
                newT.Add(new Triangle(ab, bc, ca));
            }
            T.Clear();
            T.AddRange(newT);
        }
        subdivide();
        subdivide();
        // 20*4^n tris = 320
        _ball_lores = new();
        _ball_lores.AddVertices(V, out _);
        foreach (Triangle t in T)
            _ball_lores.nAddTriangle(t);

        subdivide();
        subdivide();
        // 20*4^n tris = 5120
        _ball_hires = new();
        _ball_hires.AddVertices(V, out _);
        foreach (Triangle t in T)
            _ball_hires.nAddTriangle(t);
    }
}
