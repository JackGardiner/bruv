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
        private static Vec3 _origin = NAN3;
        private static Vec3 _size = NAN3;
        private static int _get_a_word_in_edgewise = 5;

        private static bool _orbit = true;
        private static float _last_theta = NAN;
        private static float _last_phi = NAN;

        private static System.Diagnostics.Stopwatch _stopwatch = new();

        private static float _last_zoom = NAN;

        private static Vec3 _pos = ZERO3;
        private static Vec3 _vel = ZERO3;
        private static float _target_speed = 50f;
        private static float _speed_responsiveness = 20f;

        private static float _ortho = 1f;
        private static float _ortho_responsiveness = 15f;

        private const int KEY_SPACE = 0b00000001;
        private const int KEY_SHIFT = 0b00000010;
        private const int KEY_W     = 0b00000100;
        private const int KEY_S     = 0b00001000;
        private const int KEY_A     = 0b00010000;
        private const int KEY_D     = 0b00100000;
        private const int KEY_Z     = 0b01000000;
        private const int KEY_CTRL  = 0b10000000; /* must be last. */
        private static int _held = 0;

      #if false
        // Leaving this here just so you can see how cooked the required
        // workaround is to get picogk's own orthographic projection working
        // correctly regardless of window size. Follow pointer to viewer object,
        // then to glfwWindow object, then find the win32 api window handle at
        // byte 872.

        [InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [InteropServices.StructLayout(InteropServices.LayoutKind.Sequential)]
        private struct RECT {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

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




        // yoooo for future reference this is a much easier way to get the aspect
        // ratio:
        Mat4 proj_stat = Perv.get<Mat4>(PICOGK_VIEWER, "m_matProjectionStatic");
        float aspect_ratio = proj_stat[1,1] / proj_stat[0,0];
      #endif


        private static void make_shit_happen() {
            // Checkout the genuine bounding box.
            Perv.invoke(PICOGK_VIEWER, "RecalculateBoundingBox");
            BBox3 bbox = Perv.get<BBox3>(PICOGK_VIEWER, "m_oBBox");

            // Only set the initial box once, on the first thing rendered.
            if (isnan(_origin) || isnan(_size)) {
                if (bbox.Equals(new BBox3()))
                    return;
                _origin = bbox.vecCenter();
                _size = bbox.vecSize();
            }

            // Rescale their zoom handler too bc the linear one is bad.
            float zoom = Perv.get<float>(PICOGK_VIEWER, "m_fZoom");
            if (!(_last_zoom == zoom)) {
                if (nonnan(_last_zoom)) {
                    float Dzoom = zoom - _last_zoom;
                    Dzoom *= log(zoom + 1f);
                    Dzoom *= 5f;
                    zoom = _last_zoom + Dzoom;
                    zoom = max(zoom, 0.1f);
                    Perv.set(PICOGK_VIEWER, "m_fZoom", zoom);
                }
                _last_zoom = zoom;
            }

            // Get angles of camera position (note this is not camera looking
            // angles).
            float theta = torad(PICOGK_VIEWER.m_fOrbit);
            float phi = PI_2 - torad(PICOGK_VIEWER.m_fElevation);

            // In non-orbit, we scale-down the sensitivity.
            if (!_orbit && nonnan(_last_theta) && nonnan(_last_phi)) {
                float Dtheta = theta - _last_theta;
                float Dphi = phi - _last_phi;
                // Assume wrapping caused any larger-than-half turns.
                if (Dtheta > PI)
                    Dtheta -= TWOPI;
                else if (Dtheta < -PI)
                    Dtheta += TWOPI;
                Dtheta /= 1.5f;
                Dphi /= 1.5f;
                theta = _last_theta + Dtheta;
                phi = _last_phi + Dphi;
            }
            theta = wraprad(theta - PI) + PI; // [0,TWOPI), to match picogk.
            phi = clamp(phi, 1e-3f, PI - 1e-3f);
            _last_theta = theta;
            _last_phi = phi;

            // Keep the viewer in the loop. Note this also fixed picogk's broken
            // elevation clamp.
            PICOGK_VIEWER.m_fOrbit = todeg(theta);
            PICOGK_VIEWER.m_fElevation = todeg(PI_2 - phi);

            // we could bypass picogk fov store but lets not.
            float fov = torad(Perv.get<float>(PICOGK_VIEWER, "m_fFov"));

            // Get Dt across calls for movement.
            if (!_stopwatch.IsRunning) {
                _stopwatch.Start();
            } else {
                float Dt = (float)_stopwatch.Elapsed.TotalSeconds;
                _stopwatch.Restart();

                // Handle accel/vel/pos.
                Frame frame = new(ZERO3, tocart(1f, theta + PI, 0f), uZ3);
                Vec3 target_vel = ZERO3;
                if (isset(_held, KEY_SPACE)) target_vel += frame.Z;
                if (isset(_held, KEY_SHIFT) /* several ways to go down (dujj) */
                    || isset(_held, KEY_Z))  target_vel -= frame.Z;
                if (isset(_held, KEY_W))     target_vel += frame.X;
                if (isset(_held, KEY_S))     target_vel -= frame.X;
                if (isset(_held, KEY_A))     target_vel += frame.Y;
                if (isset(_held, KEY_D))     target_vel -= frame.Y;
                if (target_vel != ZERO3) {
                    target_vel = normalise(target_vel) * _target_speed;
                    if ((_held & KEY_CTRL) != 0)
                       target_vel *= 3f;
                }
                _vel += (target_vel - _vel)
                      * (1f - exp(-_speed_responsiveness * Dt));
                if (mag(_vel) < 1e-3f && mag(target_vel) < 1e-3f)
                    _vel = ZERO3;

                // Scale velocity impact with zoom level also.
                float by = zoom * 8f;
                by *= lerp(0.5f, 1f, _ortho); // also slower on non-ortho.
                _pos += by*_vel*Dt;

                // Handle perspective/orthogonal switch.
                float target_ortho = _orbit ? 1f : 0f;
                _ortho += (target_ortho - _ortho)
                        * (1f - exp(-_ortho_responsiveness * Dt));
            }

            // Setup box to trick picogk into thinking its the origin.
            BBox3 newbbox = new BBox3(-_size/2f, _size/2f);
            Perv.set(PICOGK_VIEWER, "m_oBBox", newbbox);


            // HOLD ON. if we never allow bbox to be non-empty, picogk wont even
            // generate the matrices and we are free to splice our own in. fuck
            // yes. the clipping plane has been defeated.
            // weeeeeeeeeeeellllllllll actually we still sometimes need to let
            // picogk get a fuckin' word in edgewise (shut up) so that the
            // projection matrix actually has the correct/latest aspect ratio.
            // im hot dawg.
            if (_get_a_word_in_edgewise > 0) {
                --_get_a_word_in_edgewise;
            } else {
                Perv.set(PICOGK_VIEWER, "m_oBBox", new BBox3());
            }

            // Get aspect ratio by backing it out of the static projection
            // matrix.
            Mat4 proj_static = Perv.get<Mat4>(PICOGK_VIEWER,
                                              "m_matProjectionStatic");
            float aspect_ratio = proj_static[1,1] / proj_static[0,0];

            // Create the mvp matrix, perhaps blending between perspective and
            // ortho.
            Vec3 look = -tocart(1f, theta, as_phi(phi));
            float dist = mag(_size) * zoom * 0.8f;
            float height = 2f * dist * tan(fov/2f);

            float near = dist*0.05f;
            float far  = dist*50f;
            // thats farkin heaps deep enough jeff.

            Vec3 camera = tocart(dist, theta, as_phi(phi));
            Vec3 shift = ZERO3;
            Mat4 view;
            Mat4 projection;
            if (_ortho > 0.99f) {
                view = Mat4.CreateLookTo(
                    camera,
                    look,
                    uZ3
                );
                projection = Mat4.CreateOrthographic(
                    height * aspect_ratio,
                    height,
                    near,
                    far
                );
            } else {
                float newfov = lerp(fov, 1e-2f, _ortho);
                float newdist = height / 2f / tan(newfov/2f);
                near = newdist*0.05f;
                far  = newdist*50f;

                float move = lerp(0f, 1f, _ortho / (2f - _ortho));
                newdist *= move;

                view = Mat4.CreateLookTo(
                    camera / dist * newdist,
                    look,
                    uZ3
                );
                projection = Mat4.CreatePerspectiveFieldOfView(
                    newfov,
                    aspect_ratio,
                    near,
                    far
                );

                // Move camera pos to "origin", to ensure mouse directly drags
                // view.
                camera *= move;
                shift = camera;
            }
            Perv.set(
                PICOGK_VIEWER,
                "m_matModelViewProjection",
                view * projection
            );
            Perv.set( // important to keep updated for lighting purposes.
                PICOGK_VIEWER,
                "m_vecEye",
                camera
            );

            // Shift all objects into the origin established by first rendered
            // object and then to camera.
            Vec3 translate = -_pos - _origin - shift;
            _set_all_transforms(Mat4.CreateTranslation(translate));
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
                await Task.Delay(15);
            }
        }

        public static void reset() {
            // Cheeky reset.
            _origin = NAN3;
            _size = NAN3;

            _orbit = true;
            _last_theta = NAN;
            _last_phi = NAN;

            _last_zoom = NAN;

            _pos = ZERO3;
            _vel = ZERO3;

            _ortho = 1f;

            Perv.set(PICOGK_VIEWER, "m_fZoom", 1f);
        }
        public static void recalc() {
            _get_a_word_in_edgewise = 5;
        }
        public static void change(bool orbit) {
            _orbit = orbit;
        }

        /* Viewer.IViewerAction */
        public void Do(Viewer viewer) {
            make_shit_happen();
        }

        /* Viewer.IKeyHandler */
        public bool bHandleEvent(Viewer viewer, Viewer.EKeys key, bool pressed,
                bool shift, bool ctrl, bool alt, bool cmd) {
            int keycode;
            switch (key) {
                case Viewer.EKeys.Key_Space:
                    // Gotta turn off both shift and space upon space release.
                    // Otherwise we have no way of catching the shift up event.
                    keycode = pressed
                            ? (shift ? KEY_SHIFT : KEY_SPACE)
                            : (KEY_SHIFT | KEY_SPACE);
                    goto RECOGNISED;
                case Viewer.EKeys.Key_W: keycode = KEY_W; goto RECOGNISED;
                case Viewer.EKeys.Key_S: keycode = KEY_S; goto RECOGNISED;
                case Viewer.EKeys.Key_A: keycode = KEY_A; goto RECOGNISED;
                case Viewer.EKeys.Key_D: keycode = KEY_D; goto RECOGNISED;
                case Viewer.EKeys.Key_Z: keycode = KEY_Z; goto RECOGNISED;

                case Viewer.EKeys.Key_Tab:
                    if (!pressed)
                        break;
                    change(!_orbit);
                    return true;

                case Viewer.EKeys.Key_Backspace:
                    if (!pressed)
                        break;
                    reset();
                    return true;

                case Viewer.EKeys.Key_R:
                    if (!pressed || !ctrl)
                        break;
                    recalc();
                    return true;
            }
            return false;

          RECOGNISED:;
            if (pressed) {
                if (ctrl)
                    _held |= KEY_CTRL;
                _held |= keycode;
            } else {
                _held &= ~keycode;
                if (isclr(_held, KEY_CTRL - 1))
                    _held &= ~KEY_CTRL;
            }
            return true;
        }

        public static void initialise() {
            PICOGK_VIEWER.AddKeyHandler(new HackView());
            _ = make_shit_happen_continuously();
        }
    }

    public static void initialise() {
        HackView.initialise();
        log();
        log("[Geez] using a new hacked-in camera, keybinds:");
        log("     - w/a/s/d        move camera horizontally");
        log("     - space          move camera up");
        log("     - z/shift+space  move camera down");
        log("     - ctrl           sprint on movement key-down");
        log("     - tab            toggle orbit/free mode");
        log("     - scroll         orbit zoom in-out + move slower/faster");
        log("     - ctrl+r         rescale for window size");
        log("     - backspace      reset view");
        log();
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

    public static int cuboid(in Cuboid cuboid, Colour? colour=null,
            int divide_x=1, int divide_y=1, int divide_z=1) {
        List<PolyLine> lines = new();
        using (dflt_like(colour: COLOUR_BLUE)) {
            float Lx = cuboid.Lx / divide_x;
            float Ly = cuboid.Ly / divide_y;
            float Lz = cuboid.Lz / divide_z;
            for (int x=0; x<divide_x; ++x)
            for (int y=0; y<divide_y; ++y)
            for (int z=0; z<divide_z; ++z) {
                Vec3 pos = new(
                    (1f - divide_x)*Lx/2f + x*Lx,
                    (1f - divide_y)*Ly/2f + y*Ly,
                    (1f - divide_z)*Lz/2f + z*Lz
                );
                _cuboid_lines(
                    lines,
                    new Cuboid(cuboid.centre.translate(pos), Lx, Ly, Lz),
                    colour: colour
                );
            }
        }
        return _push(lines);
    }




    static Mesh _dummy_og;
    static Mesh? _dummy;
    static Geez() {
        // Dummy mesh to fix polyline colouring bug.
        _dummy_og = new();
        _dummy_og.AddVertices(
            [new(0,0,0), new(1e-1f,0,0), new(0,1e-1f,0), new(0,0,1e-1f)],
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
