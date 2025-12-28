using static br.Br;

using Vec3 = System.Numerics.Vector3;
using Mat4 = System.Numerics.Matrix4x4;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using Triangle = PicoGK.Triangle;
using PolyLine = PicoGK.PolyLine;
using BBox3 = PicoGK.BBox3;
using Colour = PicoGK.ColorFloat;
using Viewer = PicoGK.Viewer;
using System.Numerics;

namespace br {

public static class Geez {
    public static Colour? colour = null;
    public static float? alpha = null;
    public static float? metallic = null;
    public static float? roughness = null;
    public static Sectioner? sectioner = null;

    public static Colour dflt_colour = new Colour("#FFFFFF");
    public static float dflt_alpha = 1f;
    public static float dflt_metallic = 0.35f;
    public static float dflt_roughness = 0.5f;
    public static Sectioner dflt_sectioner = new();


    public static IDisposable like(Colour? colour=null, float? alpha=null,
            float? metallic=null, float? roughness=null,
            Sectioner? sectioner=null) {
        var colour0 = Geez.colour;
        var alpha0 = Geez.alpha;
        var metallic0 = Geez.metallic;
        var roughness0 = Geez.roughness;
        var sectioner0 = Geez.sectioner;
        Geez.colour = colour ?? colour0;
        Geez.alpha = alpha ?? alpha0;
        Geez.metallic = metallic ?? metallic0;
        Geez.roughness = roughness ?? roughness0;
        Geez.sectioner = sectioner ?? sectioner0;
        return new OnLeave(() => {
                Geez.colour = colour0;
                Geez.alpha = alpha0;
                Geez.metallic = metallic0;
                Geez.roughness = roughness0;
                Geez.sectioner = sectioner0;
            });
    }
    public static IDisposable dflt_like(Colour? colour=null, float? alpha=null,
            float? metallic=null, float? roughness=null,
            Sectioner? sectioner=null) {
        var colour0 = Geez.dflt_colour;
        var alpha0 = Geez.dflt_alpha;
        var metallic0 = Geez.dflt_metallic;
        var roughness0 = Geez.dflt_roughness;
        var sectioner0 = Geez.dflt_sectioner;
        Geez.dflt_colour = colour ?? colour0;
        Geez.dflt_alpha = alpha ?? alpha0;
        Geez.dflt_metallic = metallic ?? metallic0;
        Geez.dflt_roughness = roughness ?? roughness0;
        Geez.dflt_sectioner = sectioner ?? sectioner0;
        return new OnLeave(() => {
                Geez.dflt_colour = colour0;
                Geez.dflt_alpha = alpha0;
                Geez.dflt_metallic = metallic0;
                Geez.dflt_roughness = roughness0;
                Geez.dflt_sectioner = sectioner0;
            });
    }
    private class OnLeave : IDisposable {
        protected readonly Action _func;
        public OnLeave(Action func) { _func = func; }
        public void Dispose() { _func(); }
    }


    private static Dictionary<int, object> _geezed
            = new();
    private static int _next = 1; // must leave 0 as illegal.
    private static int _track(in List<PolyLine> lines, in List<Mesh> meshes) {
        int key = _next++;
        _geezed.Add(key, (lines, meshes));
        return key;
    }
    private static int _track(in List<int> group) {
        int key = _next++;
        _geezed.Add(key, group);
        return key;
    }


    public class HackView : Viewer.IViewerAction, Viewer.IKeyHandler {
        private static Vec3 _shift = ZERO3;
        private static float _zoom = NAN;

      #if false
        [InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [InteropServices.StructLayout(InteropServices.LayoutKind.Sequential)]
        private struct RECT {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
      #endif

        private static void make_shit_happen() {
            Perv.invoke(PICOGK_VIEWER, "RecalculateBoundingBox");
            BBox3 bbox = Perv.get<BBox3>(PICOGK_VIEWER, "m_oBBox");
            Vec3 centre = bbox.vecCenter();
            Vec3 size = bbox.vecSize();

            float zoom = Perv.get<float>(PICOGK_VIEWER, "m_fZoom");

          #if false
            // Leaving this here just so you can see how cooked the required
            // workaround is to get picogk's own orthographic projection working
            // correctly regardless of window size. Follow pointer to viewer
            // object, then to glfwWindow object, then find the win32 api window
            // handle at byte 872.
            IntPtr hwnd_ptr_ptr = Perv.get<IntPtr>(PICOGK_VIEWER, "m_hThis");
            IntPtr hwnd_ptr = InteropServices.Marshal.ReadIntPtr(hwnd_ptr_ptr);
            IntPtr hwnd = InteropServices.Marshal.ReadIntPtr(hwnd_ptr, 872);

            float aspect_ratio = 1f;
            if (hwnd == IntPtr.Zero) {
                Console.WriteLine("HWND is null.");
            } else if (!GetClientRect(hwnd, out RECT rect)) {
                Console.WriteLine("GetClientRect failed.");
            } else {
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                if (height == 0) {
                    Console.WriteLine("Invalid viewport size.");
                } else {
                    aspect_ratio = (float)width / height;
                }
            }

            if (max(size.X, size.Y) > size.Z) {
                size.X = max(size.X, size.Y);
                size.Y = size.X / aspect_ratio;
            } else {
                size.Y = size.Z;
                size.X = size.Z * aspect_ratio;
            }
            size /= 1.5f;
            size *= pow(zoom, 3f);
          #endif

            // We pretend our object is 50x smaller to get a 50x further away far
            // clip plane, but this also means our zoom scales 50x slower. So,
            // we implement a custom zoom-re-scaler and also initialise zoom to
            // 50x.
            size /= 50f;
            if (_zoom != zoom) {
                if (nonnan(_zoom)) {
                    float Dzoom = zoom - _zoom;
                    Dzoom *= 1f + pow(zoom, 1.3f);
                    zoom = _zoom + Dzoom;
                    zoom = max(0.1f, zoom);
                    Perv.set(PICOGK_VIEWER, "m_fZoom", zoom);
                }
                _zoom = zoom;
            }

            BBox3 newbbox = new BBox3(-size/2f, size/2f);
            // For some reason, if the bbox is the max finite size (like after)
            // initialisation, picogk cracks it if u change that without viewing
            // something.
            if (bbox.Equals(new BBox3()))
                newbbox = new BBox3();
            Perv.set(PICOGK_VIEWER, "m_oBBox", newbbox);

            Transformer t = new();
            t = t.translate(_shift - centre);
            _set_all_transforms(t.mat_T);
        }

        public static void make_shit_happen_in_the_future() {
            var actions = Perv.get<Queue<Viewer.IViewerAction>>(
                PICOGK_VIEWER,
                "m_oActions"
            );
            lock (actions)
                actions.Enqueue(new HackView());
        }

        private static async Task make_shit_happen_continuously() {
            for (;;) {
                make_shit_happen_in_the_future();
                await Task.Delay(16);
            }
        }

        /* Viewer.IViewerAction */
        public void Do(Viewer viewer) {
            make_shit_happen();
        }

        /* Viewer.IKeyHandler */
        public bool bHandleEvent(Viewer viewer, Viewer.EKeys key, bool pressed,
                bool shift, bool ctrl, bool alt, bool cmd) {
            if (!pressed)
                return false;

            Vec3 eye = Perv.get<Vec3>(PICOGK_VIEWER, "m_vecEye");
            eye = normalise(eye);
            Vec3 X = (closeto(eye, uZ3) || closeto(eye, -uZ3))
                   ? uX3
                   : cross(-eye, uZ3);
            Frame frame = new(ZERO3, X, -eye);
            float zoom = Perv.get<float>(PICOGK_VIEWER, "m_fZoom");
            float by = pow(3f*zoom, (3f*zoom < 1f) ? 3f : 1/3f);

            switch (key) {
                case Viewer.EKeys.Key_W: _shift -= 2f*by*frame.Z; break;
                case Viewer.EKeys.Key_S: _shift += 2f*by*frame.Z; break;
                case Viewer.EKeys.Key_A: _shift -= by*frame.X; break;
                case Viewer.EKeys.Key_D: _shift += by*frame.X; break;
                case Viewer.EKeys.Key_Space:
                    _shift += shift ? by*uZ3 : -by*uZ3; break;
            }
            return true;
        }

        public static void initialise() {
            Perv.set(PICOGK_VIEWER, "m_fZoom", 50f);
            PICOGK_VIEWER.AddKeyHandler(new HackView());
            _ = make_shit_happen_continuously();
        }
    }

    public static void initialise() {
        HackView.initialise();
    }



    private static int _materials = 2;
    private static bool _dummy_materialed = false;
    private static int _dummy_material() {
        int group_id = 1;
        if (!_dummy_materialed) {
            PICOGK_VIEWER.SetGroupMaterial(
                group_id,
                COLOUR_BLACK,
                0f,
                0f
            );
        }
        return group_id;
    }
    private static int _material() {
        int group_id = _materials++;
        Colour col = new(colour ?? dflt_colour, alpha ?? dflt_alpha);
        PICOGK_VIEWER.SetGroupMaterial(
            group_id,
            col,
            metallic ?? dflt_metallic,
            roughness?? dflt_roughness
        );
        return group_id;
    }
    private static void _set_all_transforms(Mat4 m) {
        for (int i=1; i<_materials; ++i)
            PICOGK_VIEWER.SetGroupMatrix(i, m);
    }

    private static void _view(in List<PolyLine> lines, in List<Mesh> meshes) {
        int group_id = _material();
        Vec3? point = null;
        foreach (PolyLine line in lines) {
            PICOGK_VIEWER.Add(line, group_id);
            if (point == null && line.nVertexCount() > 0)
                point = line.vecVertexAt(0);
        }
        foreach (Mesh mesh in meshes)
            PICOGK_VIEWER.Add(mesh, group_id);

        // Dummy mesh to fix bug in picogk polyline colouring. Basically just
        // ensure a mesh is rendered after every line, otherwise the lines are
        // drawn white.
        if (_dummy != null)
            PICOGK_VIEWER.Remove(_dummy);
        _dummy = null;
        if (point != null) {
            _dummy = _dummy_og.mshCreateTransformed(ONE3, point.Value);
            PICOGK_VIEWER.Add(_dummy, _dummy_material());
        }

        HackView.make_shit_happen_in_the_future();
    }
    private static int _push(in List<Mesh> meshes) {
        _view([], meshes);
        return _track([], meshes);
    }
    private static int _push(in List<PolyLine> lines) {
        _view(lines, []);
        return _track(lines, []);
    }
    private static int _push(in List<PolyLine> lines, in List<Mesh> meshes) {
        _view(lines, meshes);
        return _track(lines, meshes);
    }
    private static int _push(in List<Mesh> meshes, in List<PolyLine> lines) {
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
        if (key <= 0) // noop.
            return;

        object item = _geezed[key];
        _geezed.Remove(key);

        if (item is (List<PolyLine> lines, List<Mesh> meshes)) {
            foreach (Mesh mesh in meshes)
                PICOGK_VIEWER.Remove(mesh);
            foreach (PolyLine line in lines)
                PICOGK_VIEWER.Remove(line);
            HackView.make_shit_happen_in_the_future();
        } else if (item is List<int> group) {
            remove(group);
        } else {
            assert(false);
        }
    }
    public static void remove(List<int> keys) {
        foreach (int key in keys)
            remove(key);
    }
    public static void clear() {
        List<int> keys = new(_geezed.Keys);
        remove(keys);
    }
    public static int group(List<int> keys) {
        return _track(keys);
    }


    public const int BLANK = 0;
    public const int CLEAR = -1;
    public class Cycle {
        protected int[] keys; // rolling buffer.
        protected int i; // next index.

        public Colour? colour = null;
        public float? alpha = null;
        public float? metallic = null;
        public float? roughness = null;
        public Sectioner? sectioner = null;

        public Cycle(int size=1, Colour? colour=null, float? alpha = null,
                float? metallic = null, float? roughness = null,
                Sectioner? sectioner = null
            ) {
            assert(size > 0);
            keys = new int[size];
            i = 0;

            this.colour = colour;
            this.alpha = alpha;
            this.metallic = metallic;
            this.roughness = roughness;
            this.sectioner = sectioner;
        }

        public void cycle(int key) {
            assert(key > 0 || key == CLEAR || key == BLANK);
            if (key == CLEAR) {
                clear();
                return;
            }
            if (keys[i] > 0)
                Geez.remove(keys[i]);
            keys[i] = key;
            i = (i == numel(keys) - 1) ? 0 : i + 1;
        }

        public void clear() {
            for (int j=0; j<numel(keys); ++j) {
                if (keys[j] != 0)
                    Geez.remove(keys[j]);
                keys[j] = 0;
            }
        }

        public static Cycle operator<<(Cycle c, int key) {
            c.cycle(key);
            return c;
        }

        public IDisposable like() {
            return Geez.like(
                colour: colour,
                alpha: alpha,
                metallic: metallic,
                roughness: roughness,
                sectioner: sectioner
            );
        }
    }


    public static int voxels(in Voxels vox, Colour? colour=null) {
        // Apply sectioner.
        Sectioner sect = sectioner ?? dflt_sectioner;
        Voxels new_vox = sect.has_cuts() ? sect.cut(vox) : vox;
        Mesh mesh = new(new_vox);
        return Geez.mesh(mesh, colour: colour);
    }

    public static int mesh(in Mesh mesh, Colour? colour=null) {
        using (like(colour: colour))
            return _push([mesh]);
    }


    private static Mesh _ball_hires;
    private static Mesh _ball_lores;
    public static int point(in Vec3 p, float r=2f, Colour? colour=null,
            bool hires=true) {
        return Geez.points([p], r: r, colour: colour, hires: hires);
    }

    public static int points(in List<Vec3> ps, float r=2f, Colour? colour=null,
            bool hires=false) {
        Mesh ball = hires ? _ball_hires : _ball_lores;
        List<Mesh> meshes = new();
        foreach (Vec3 p in ps) {
            assert(isgood(p), $"p={vecstr(p)}");
            Mesh mesh = ball.mshCreateTransformed(r*ONE3, p);
            meshes.Add(mesh);
        }
        using (dflt_like(colour: COLOUR_RED, alpha: 1f, metallic: 0.1f,
                roughness: 0f))
        using (like(colour: colour))
            return _push(meshes);
    }

    public static int line(in PolyLine line) {
        return Geez.lines([line]);
    }
    public static int line(in List<Vec3> points, Colour? colour=null,
            float arrow=0f) {
        PolyLine line = new(colour ?? Geez.colour ?? COLOUR_GREEN);
        line.Add(points);
        if (arrow > 0f)
            line.AddArrow(arrow);
        return Geez.lines([line]);
    }
    public static int lines(in List<PolyLine> lines) {
        return _push(lines);
    }

    private static void _frame_lines(out Mesh? mesh, List<PolyLine> lines,
            in Frame frame, float size, bool mark_pos) {
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

        mesh = (!mark_pos) ? null
             : _ball_lores.mshCreateTransformed(0.05f*size*ONE3, frame.pos);
    }
    public static int frame(in Frame frame, float size=5f, bool mark_pos=true,
            Colour? colour=null) {
        return Geez.frames([frame], size: size, mark_pos: mark_pos,
                colour: colour);
    }
    public static int frames(in List<Frame> frames, float size=5f,
            bool mark_pos=true, Colour? colour=null) {
        List<PolyLine> lines = new();
        List<Mesh> meshes = new();
        foreach (Frame frame in frames) {
            _frame_lines(out Mesh? mesh, lines, frame, size, mark_pos);
            if (mesh != null)
                meshes.Add(mesh);
        }
        using (dflt_like(colour: COLOUR_WHITE, alpha: 1f, metallic: 0f,
                roughness: 0f))
        using (like(colour: colour))
            return _push(lines, meshes);
    }

    public static int bbox(in BBox3 bbox, Colour? colour=null) {
        return Geez.cuboid(new Cuboid(bbox), colour: colour);
    }

    public static int pipe(in Pipe pipe, Colour? colour=null, int? rings=null,
            int bars=6) {
        float r = pipe.rhi;
        float L = pipe.Lz;
        Frame frame = pipe.centre;

        if (rings == null)
            rings = max(3, (int)(L * 2.5f / (TWOPI/bars*r)));
        assert(rings >= 2);
        assert(bars >= 2);

        List<PolyLine> lines = new();
        PolyLine line;
        Colour col = colour ?? Geez.colour ?? COLOUR_BLUE;

        bool done_inner = false;
      DO:;

        // Rings.
        for (int n=0; n<rings.Value; ++n) {
            float z = n*L/(rings.Value - 1);
            line = new(col);
            int N = 100;
            for (int i=0; i<N; ++i) {
                float theta = i*TWOPI/(N - 1);
                line.nAddVertex(frame * tocart(r, theta, z));
            }
            lines.Add(line);
        }

        // Bars.
        for (int n=0; n<bars; ++n) {
            float theta = n*TWOPI/bars;
            line = new(col);
            line.Add([
                frame * tocart(r, theta, 0f),
                frame * tocart(r, theta, L),
            ]);
            lines.Add(line);
        }

        // Maybe do inner also.
        if (!done_inner) {
            if (pipe.rlo > 0f) {
                // Add connections to the bars.
                for (int n=0; n<bars; ++n) {
                    float theta = n*TWOPI/bars;
                    line = new(col);
                    line.Add([
                        frame * tocart(pipe.rlo, theta, 0f),
                        frame * tocart(pipe.rhi, theta, 0f),
                    ]);
                    lines.Add(line);
                    line = new(col);
                    line.Add([
                        frame * tocart(pipe.rlo, theta, L),
                        frame * tocart(pipe.rhi, theta, L),
                    ]);
                    lines.Add(line);
                }

                r = pipe.rlo;
                done_inner = true;
                goto DO;
            } else {
                // Add crosses on the end.
                assert((bars%2) == 0);
                for (int n=0; n<bars/2; ++n) {
                    float theta = n*TWOPI/bars;
                    line = new(col);
                    line.Add([
                        frame * tocart(r, theta, 0f),
                        frame * tocart(r, theta + PI, 0f),
                    ]);
                    lines.Add(line);
                    line = new(col);
                    line.Add([
                        frame * tocart(r, theta, L),
                        frame * tocart(r, theta + PI, L),
                    ]);
                    lines.Add(line);
                }
            }
        }

        return _push(lines);
    }

    private static void _cuboid_lines(List<PolyLine> lines, in Cuboid cuboid,
            Colour? colour) {
        List<Vec3> corners = cuboid.get_corners();
        Colour col = colour ?? Geez.colour ?? Geez.dflt_colour;
        PolyLine l;

        // Each corner (+3+3), then the zigzag joining (+6).

        l = new(col);
        l.nAddVertex(corners[0b000]);
        l.nAddVertex(corners[0b100]);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(corners[0b000]);
        l.nAddVertex(corners[0b010]);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(corners[0b000]);
        l.nAddVertex(corners[0b001]);
        lines.Add(l);

        l = new(col);
        l.nAddVertex(corners[0b111]);
        l.nAddVertex(corners[0b011]);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(corners[0b111]);
        l.nAddVertex(corners[0b101]);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(corners[0b111]);
        l.nAddVertex(corners[0b110]);
        lines.Add(l);

        l = new(col);
        l.nAddVertex(corners[0b100]);
        l.nAddVertex(corners[0b101]);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(corners[0b101]);
        l.nAddVertex(corners[0b001]);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(corners[0b001]);
        l.nAddVertex(corners[0b011]);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(corners[0b011]);
        l.nAddVertex(corners[0b010]);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(corners[0b010]);
        l.nAddVertex(corners[0b110]);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(corners[0b110]);
        l.nAddVertex(corners[0b100]);
        lines.Add(l);
    }

    public static int cuboid(in Cuboid cuboid, Colour? colour=null) {
        List<PolyLine> lines = new();
        using (dflt_like(colour: COLOUR_BLUE))
            _cuboid_lines(lines, cuboid, colour: colour);
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

}
