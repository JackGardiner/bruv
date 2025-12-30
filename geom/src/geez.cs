using static br.Br;

using Vec2 = System.Numerics.Vector2;
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
    public static float dflt_alpha = 0.8f;
    public static float dflt_metallic = 0.35f;
    public static float dflt_roughness = 0.5f;
    public static Sectioner dflt_sectioner = new();

    public static bool transparent = true;

    public static Colour get_background_colour() {
        return Perv.get<Colour>(PICOGK_VIEWER, "m_clrBackground");
    }
    public static void set_background_colour(bool dark) {
        Colour bgcol = dark ? new("#202020") : new("#FFFFFF");
        PICOGK_VIEWER.SetBackgroundColor(bgcol);
    }

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



    public class ViewerHack : Viewer.IViewerAction, Viewer.IKeyHandler {
        private class SnapTo {
            public float[] targets;
            public float responsiveness;
            public float elapsed_time;
            public float max_time;
            public float atol;
            public float rtol;

            public SnapTo(float responsiveness, float[] targets,
                    float max_time=NAN, float atol=5e-3f, float rtol=5e-3f) {
                assert(numel(targets) > 0);
                this.targets = targets;
                this.responsiveness = responsiveness;
                this.elapsed_time = 0f;
                this.max_time = max_time;
                this.atol = atol;
                this.rtol = rtol;
            }

            public delegate float get_difference_f(float target, float value);
            public float[] Dvalue(float Dt, float[] value,
                    get_difference_f? get_difference=null) {
                assert(numel(value) == numel(targets));
                if (get_difference == null)
                    get_difference = (float t, float v) => t - v;

                elapsed_time += Dt;
                if (elapsed_time >= max_time)
                    untarget();

                float[] Dvalue = new float[numel(targets)];
                for (int i=0; i<numel(targets); ++i) {
                    if (isnan(targets[i])) {
                        Dvalue[i] = 0f;
                    } else {
                        Dvalue[i] = get_difference(targets[i], value[i]);
                        if (abs(Dvalue[i]) > max(atol, rtol*targets[i]))
                            Dvalue[i] *= 1f - exp(-responsiveness * Dt);
                    }
                }

                if (isnan(targets[0]))
                    elapsed_time = 0f;

                return Dvalue;
            }

            public void retarget(float[] newtargets) {
                assert(numel(newtargets) == numel(targets));
                for (int i=0; i<numel(newtargets); ++i)
                    assert(isgood(newtargets[i]));
                targets = newtargets;
                elapsed_time = 0f;
            }

            public void untarget() {
                for (int i=0; i<numel(targets); ++i)
                    targets[i] = NAN;
            }


            public float target {
                get {
                    assert(numel(targets) == 1);
                    return targets[0];
                }
            }
            public Vec2 target2 {
                get {
                    assert(numel(targets) == 2);
                    return arr_to_vec2(targets);
                }
            }
            public Vec3 target3 {
                get {
                    assert(numel(targets) == 3);
                    return arr_to_vec3(targets);
                }
            }

            public SnapTo(float responsiveness, float target, float max_time=NAN,
                    float atol=5e-3f, float rtol=5e-3f)
                : this(responsiveness, [target],
                       max_time, atol, rtol) {}
            public SnapTo(float responsiveness, Vec2 target, float max_time=NAN,
                    float atol=5e-3f, float rtol=5e-3f)
                : this(responsiveness, vec_to_arr(target),
                       max_time, atol, rtol) {}
            public SnapTo(float responsiveness, Vec3 target, float max_time=NAN,
                    float atol=5e-3f, float rtol=5e-3f)
                : this(responsiveness, vec_to_arr(target),
                       max_time, atol, rtol) {}

            public float Dvalue(float Dt, float value,
                    get_difference_f? get_D=null) {
                return Dvalue(Dt, [value], get_D)[0];
            }
            public Vec2 Dvalue(float Dt, Vec2 value,
                    get_difference_f? get_D=null) {
                return arr_to_vec2(Dvalue(Dt, vec_to_arr(value), get_D));
            }
            public Vec3 Dvalue(float Dt, Vec3 value,
                    get_difference_f? get_D=null) {
                return arr_to_vec3(Dvalue(Dt, vec_to_arr(value), get_D));
            }

            public void retarget(float newtarget) {
                retarget([newtarget]);
            }
            public void retarget(Vec2 newtarget) {
                retarget(vec_to_arr(newtarget));
            }
            public void retarget(Vec3 newtarget) {
                retarget(vec_to_arr(newtarget));
            }
        }

        private static System.Diagnostics.Stopwatch stopwatch = new();

        /* middle pos and min->max size of canonical objects. */
        private static Vec3 origin = NAN3;
        private static Vec3 size = NAN3;

        /* number of times to skip overriding picogk so they can make the static
           projection matrix. */
        private static int get_a_word_in_edgewise = 5;

        /* camera pos (relative to origin) and velocity. */
        public static Vec3 pos = ZERO3;
        private static Vec3 vel = ZERO3;
        public static float speed = 50f;
        public static float sprint = 3f;
        private static SnapTo snap_pos = new(25f, NAN3, 0.25f /* don freeze */);
        private static SnapTo snap_vel = new(20f, NAN3);

        /* looking vector. */
        public static float theta {
            // need picogk to enable mouse->camera. note picogk stores camera
            // theta, not looking theta.
            get => PI + torad(PICOGK_VIEWER.m_fOrbit);
            set => PICOGK_VIEWER.m_fOrbit = todeg(value - PI);
        }
        public static float phi {
            // need picogk to enable mouse->camera. note picogk stores camera
            // elevation, not looking phi. aaalso note that by handling our own
            // view angles (and splicing them into picogk) we fix picogk's broken
            // elevation clamp.
            get => PI_2 + torad(PICOGK_VIEWER.m_fElevation);
            set => PICOGK_VIEWER.m_fElevation = todeg(value - PI_2);
        }
        private static SnapTo snap_ang = new(25f, NAN2, 0.25f /* don freeze */);
        private static float get_focal_dist() => mag(size) * zoom * 0.8f;
        private static float get_ortho_dist() => mag(size) * zoom_max * 10f;
        private static Vec3 get_looking() => tocart(1f, theta, as_phi(phi));
        private static Vec3 get_camera() => -tocart(1f, theta, as_phi(phi));

        /* ortho zoom value (and speed dial), scaled up by some amount to get
          around picogk's minimum of 0.1. */
        public static float zoom {
            // need picogk to enable scroll->zoom in/out.
            get => Perv.get<float>(PICOGK_VIEWER, "m_fZoom") / zoom_scale;
            set => Perv.set(PICOGK_VIEWER, "m_fZoom", value * zoom_scale);
        }
        public static float zoom_scale = 10f;
        // note zoom minimum is enforced by picogk (which is the whole reason we
        // use a scaler).
        private static float zoom_min => 0.1f / zoom_scale;
        private static float zoom_max = 2.5f;

        /* free field of view. */
        public static float fov {
            // could bypass picogk, we dont.
            get => torad(Perv.get<float>(PICOGK_VIEWER, "m_fFov"));
            set => Perv.set(PICOGK_VIEWER, "m_fFov", todeg(value));
        }
        private static SnapTo snap_fov = new(20f, torad(65f));

        /* orbit/free mode + ortho/perspective proportion. */
        private static bool orbit = true;
        private static float ortho = 1f;
        private static SnapTo snap_ortho = new(25f /* variable */, 1f);
        private static float extra_dist = 0f;
        private static SnapTo snap_extra_dist = new(20f, 0f);
        public static void set_mode(bool neworbit) {
            orbit = neworbit;
            snap_ortho.retarget(neworbit ? 1f : 0f);
        }

        /* prev variable caches, to change the scaling of them over time. */
        private static float last_theta = NAN;
        private static float last_phi = NAN;
        private static float last_zoom = NAN;

        /* key held-state. */
        private const int KEY_SPACE = 0b0000001;
        private const int KEY_SHIFT = 0b0000010;
        private const int KEY_W     = 0b0000100;
        private const int KEY_S     = 0b0001000;
        private const int KEY_A     = 0b0010000;
        private const int KEY_D     = 0b0100000;
        private const int KEY_CTRL  = 0b1000000;
        private static int held = 0;


        public static void reset() {
            // Cheeky reset.

            stopwatch.Stop();

            origin = NAN3;
            size = NAN3;

            // leave `get_a_word_in_edgewise`.

            pos = ZERO3;
            vel = ZERO3;
            snap_pos.untarget();
            snap_vel.untarget();

            theta = torad(225f);
            phi = torad(120f);
            snap_ang.untarget();

            zoom = 1f;

            fov = torad(65f);
            snap_fov.retarget(fov);

            orbit = true;
            ortho = 1f;
            snap_ortho.responsiveness = 25f;
            snap_ortho.retarget(ortho);
            extra_dist = 0f;
            snap_extra_dist.retarget(0f);

            last_theta = NAN;
            last_phi = NAN;
            last_zoom = NAN;
        }

        public static void initialise() {
            zoom = 1f;
            fov = snap_fov.target;
            ortho = snap_ortho.target;
            PICOGK_VIEWER.AddKeyHandler(new ViewerHack());
            _ = make_shit_happen_continuously();
        }


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
            if (isnan(origin) || isnan(size)) {
                if (bbox.Equals(new BBox3()))
                    return;
                origin = bbox.vecCenter();
                size = bbox.vecSize();
            }

            // Rescale their zoom handler too bc the linear one is bad.
            if (nonnan(last_zoom)) {
                float Dzoom = zoom - last_zoom;
                // since i dont like picogk's linearly scaling zoom:
                Dzoom *= 5f*log(zoom + 1f);
                // since picogk doesnt know about the scale:
                Dzoom *= zoom_scale;

                zoom = last_zoom + Dzoom;
                zoom = clamp(zoom, zoom_min, zoom_max);
            }
            last_zoom = zoom;

            // Since angles are typically wrapped before we see their change, we
            // cant get the raw difference. Instead we get the difference which
            // is at-most 1 half-turn.
            float get_Dangle(float curr, float prev) => wraprad(curr - prev);

            // In perspective, we scale-down the sensitivity.
            if (!orbit && nonnan(last_theta) && nonnan(last_phi)) {
                float Dtheta = get_Dangle(theta, last_theta);
                float Dphi = get_Dangle(phi, last_phi);
                theta = last_theta + 0.7f*Dtheta;
                phi   = last_phi   + 0.7f*Dphi;
            }
            theta = wraprad(theta - PI) + PI; // [0,TWOPI), to match picogk.
            phi = clamp(phi, 1e-3f, PI - 1e-3f);
            // dont save last_ yet, there be more to happen.

            // Get Dt across calls for movement.
            if (!stopwatch.IsRunning) {
                stopwatch.Start();
            } else {
                float Dt = (float)stopwatch.Elapsed.TotalSeconds;
                stopwatch.Restart();

                // Handle perspective/orthogonal switch.
                ortho += snap_ortho.Dvalue(Dt, ortho);
                extra_dist += snap_extra_dist.Dvalue(Dt, extra_dist);

                // Update fov.
                fov += snap_fov.Dvalue(Dt, fov);

                // Do any view snapping.
                float[] Dang = snap_ang.Dvalue(Dt, [theta, phi], get_Dangle);
                theta += Dang[0];
                phi   += Dang[1];

                // Handle accel/vel/pos.
                Frame frame = new(ZERO3, tocart(1f, theta, 0f), uZ3);
                Vec3 target_vel = ZERO3;
                if (isset(held, KEY_SPACE)) target_vel += frame.Z;
                if (isset(held, KEY_SHIFT)) target_vel -= frame.Z;
                if (isset(held, KEY_W))     target_vel += frame.X;
                if (isset(held, KEY_S))     target_vel -= frame.X;
                if (isset(held, KEY_A))     target_vel += frame.Y;
                if (isset(held, KEY_D))     target_vel -= frame.Y;
                if (!closeto(target_vel, ZERO3)) {
                    target_vel = normalise(target_vel) * speed;
                    if (isset(held, KEY_CTRL))
                       target_vel *= sprint;
                }
                snap_vel.retarget(target_vel);
                vel += snap_vel.Dvalue(Dt, vel);

                float by = 1f;
                // Scale velocity with zoom level.
                by *= zoom;
                // Scale velocity with total viewed size.
                // https://www.desmos.com/calculator/jyn4eclrcf
                by *= 0.0190255f * mag(size) + 0.00951883f;
                // Move slower in perspective mode.
                by *= lerp(0.5f, 1f, ortho);
                pos += by*vel*Dt;

                // Do any position snapping (after movement keys).
                pos += snap_pos.Dvalue(Dt, pos);
            }

            // Keep everyone informed.
            last_theta = theta;
            last_phi = phi;

            // Setup box to trick picogk into thinking its the origin.
            BBox3 newbbox = new BBox3(-size/2f, size/2f);
            Perv.set(PICOGK_VIEWER, "m_oBBox", newbbox);

            // HOLD ON. if we never allow bbox to be non-empty, picogk wont even
            // generate the matrices and we are free to splice our own in. fuck
            // yes. the clipping plane has been defeated.
            // weeeeeeeeeeeellllllllll actually we still sometimes need to let
            // picogk get a fuckin' word in edgewise (shut up) so that the
            // projection matrix actually has the correct/latest aspect ratio.
            // im hot dawg.
            if (get_a_word_in_edgewise > 0) {
                --get_a_word_in_edgewise;
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
            Vec3 looking = get_looking();
            Vec3 camera = get_camera();
            float focal_dist = get_focal_dist() + extra_dist;
            float focal_height = 2f * focal_dist * tan(fov/2f);
            float ortho_dist = get_ortho_dist();

            // Get the "closeness", which is just ortho but rescaled to be slower
            // (i.e. looks like x^2 kinda).
            float closeness = ortho / (2f - ortho);

            Vec3 shift = camera * extra_dist;
            Vec3 eye;
            Mat4 view;
            Mat4 projection;
            if (ortho > 0.99f) {
                float near = ortho_dist*0.05f;
                float far  = ortho_dist*50f;
                // thats farkin heaps deep enough jeff.

                view = Mat4.CreateLookTo(
                    camera,
                    looking,
                    uZ3
                );
                projection = Mat4.CreateOrthographic(
                    focal_height * aspect_ratio,
                    focal_height,
                    near,
                    far
                );
                eye = camera * ortho_dist; // very far.
            } else {
                // Move camera back at the exact inverse of fov increase to keep
                // focal plane static.
                float lerp_fov = lerp(fov, 1e-2f, ortho);
                float lerp_focal_dist = focal_height / 2f / tan(lerp_fov/2f);
                float near = lerp_focal_dist*0.05f;
                float far  = lerp_focal_dist*50f;

                // Want ultra deep clipping area when in perspective.
                near *= lerp(0.05f/zoom, 1f, ortho);
                far  *= lerp(0.2f/zoom, 1f, ortho);

                // shift is irrelevant in ortho, so just use the value for
                // perspective.
                shift += camera * closeness * focal_dist;

                view = Mat4.CreateLookTo(
                    camera * closeness * lerp_focal_dist,
                    looking,
                    uZ3
                );
                projection = Mat4.CreatePerspectiveFieldOfView(
                    lerp_fov,
                    aspect_ratio,
                    near,
                    far
                );

                // Eye somewhere between here and there.
                eye = camera * ortho_dist * squared(closeness);
            }
            Perv.set(
                PICOGK_VIEWER,
                "m_matModelViewProjection",
                view * projection
            );
            Perv.set( // important to keep updated for lighting purposes.
                PICOGK_VIEWER,
                "m_vecEye",
                eye
            );

            // Shift all objects into the origin established by first rendered
            // object and then to camera.
            Vec3 translate = origin + pos + shift;
            _set_all_transforms(Mat4.CreateTranslation(-translate));
        }

        public static void make_shit_happen_in_the_future() {
            var actions = Perv.get<Queue<Viewer.IViewerAction>>(
                PICOGK_VIEWER,
                "m_oActions"
            );
            lock (actions)
                actions.Enqueue(new ViewerHack());
        }

        private static async Task make_shit_happen_continuously() {
            for (;;) {
                make_shit_happen_in_the_future();
                await Task.Delay(15);
            }
        }


        private static Vec2 get_ang_axis_aligned() {
            Vec2 ang;
            Vec3 looking = get_looking();
            if (abs(looking.X) >= abs(looking.Y)) {
                ang.X = (looking.X < 0f) ? PI : 0f;
            } else {
                ang.X = (looking.Y < 0f) ? 1.5f*PI : PI_2;
            }
            if (abs(looking.Z)/2f >= max(abs(looking.X), abs(looking.Y))) {
                ang.Y = (looking.Z < 0f) ? PI - 1e-3f : 1e-3f;
            } else {
                ang.Y = PI_2;
            }
            return ang;
        }

        private static Vec3 get_pos_on_z_axis() {
            // cheeky plane line intersection.
            Vec3 point = pos;
            Vec3 normal = cross(get_looking(), tocart(1f, theta + PI_2, 0f));
            float z = -dot(normal, -point) / dot(normal, uZ3);
            Vec3 newpos = -origin;
            newpos.Z = z;
            return newpos;
        }

        private static Vec3 get_pos_focal_point() {
            Vec3 newpos = pos;
            if (orbit)
                newpos -= get_focal_dist() * get_looking();
            else
                newpos += get_focal_dist() * get_looking();
            return newpos;
        }

        /* Viewer.IViewerAction */
        public void Do(Viewer viewer) {
            make_shit_happen();
        }

        /* Viewer.IKeyHandler */
        public bool bHandleEvent(Viewer viewer, Viewer.EKeys key, bool pressed,
                bool shift, bool ctrl, bool alt, bool cmd) {
            // dont do some things before first render.
            bool first_render = nonnan(origin);

            int keycode;
            // couple of these keys are inexplicably unlabelled by picogk.
            switch (key) {
                case Viewer.EKeys.Key_W: keycode = KEY_W; goto MOVEMENT;
                case Viewer.EKeys.Key_S: keycode = KEY_S; goto MOVEMENT;
                case Viewer.EKeys.Key_A: keycode = KEY_A; goto MOVEMENT;
                case Viewer.EKeys.Key_D: keycode = KEY_D; goto MOVEMENT;
                case Viewer.EKeys.Key_Space:
                    keycode = KEY_SPACE;
                    goto MOVEMENT;
                case (Viewer.EKeys)340 /* shift */:
                    keycode = KEY_SHIFT;
                    goto MOVEMENT;
                case (Viewer.EKeys)341 /* ctrl */:
                    keycode = KEY_CTRL;
                    goto MOVEMENT;

                case Viewer.EKeys.Key_Tab:
                    if (pressed && first_render) {
                        pos = get_pos_focal_point();
                        if (!orbit)
                            extra_dist = get_focal_dist();
                        snap_ortho.responsiveness = orbit ? 24f : 18f;
                        set_mode(!orbit);
                    }
                    return true;
                case (Viewer.EKeys)'`':
                    if (pressed && first_render) {
                        snap_ortho.responsiveness = orbit ? 25f : 18f;
                        set_mode(!orbit);
                    }
                    return true;

                case Viewer.EKeys.Key_Z:
                    // get snapping.
                    if (pressed && first_render && orbit)
                        snap_ang.retarget(get_ang_axis_aligned());
                    return true;
                case Viewer.EKeys.Key_X:
                    // snap snap.
                    if (pressed && first_render && orbit)
                        snap_pos.retarget(get_pos_on_z_axis());
                    return true;

                case Viewer.EKeys.Key_Q:
                    if (pressed && first_render && !orbit) {
                        float target = snap_fov.target;
                        target += (PI - target)/15f;
                        snap_fov.retarget(target);
                    }
                    return true;
                case Viewer.EKeys.Key_E:
                    if (pressed && first_render && !orbit) {
                        float target = snap_fov.target;
                        target += (0f - target)/15f;
                        snap_fov.retarget(target);
                    }
                    return true;

                case Viewer.EKeys.Key_Backspace:
                    if (pressed && first_render)
                        reset();
                    return true;

                case Viewer.EKeys.Key_R:
                    if (pressed && first_render && ctrl)
                        get_a_word_in_edgewise = 5;
                    return true;

                case Viewer.EKeys.Key_T:
                    if (pressed)
                        _set_all_alpha(!transparent);
                    return true;

                case Viewer.EKeys.Key_Y:
                    if (pressed) {
                        bool light = get_background_colour()
                                    .Equals(new Colour("#FFFFFF"));
                        set_background_colour(light);
                    }
                    return true;
            }
            return false;

          MOVEMENT:;
            if (first_render) {
                if (pressed)
                    held |= keycode;
                else
                    held &= ~keycode;
            }
            return true;
        }
    }

    public static void initialise() {
        ViewerHack.initialise();
        log();
        log("[Geez] using a new hacked-in camera, keybinds:");
        log("     - W/A/S/D         move camera horizontally");
        log("     - space/shift     move camera up/down");
        log("     - ctrl            sprint (when held)");
        log("     - tab             toggle orbit/free mode at focal point");
        log("     - backtick [`/~]  toggle orbit/free mode at centre");
        log("     - scroll up/down  orbit zoom in/out + move slower/faster");
        log("     - ctrl+R          rescale for window aspect ratio");
        log("     - backspace       reset view");
        log("     - Z               orbit snap view along nearest axis");
        log("     - X               orbit snap centre to Z axis");
        log("     - Q/E             dial up/down free fov");
        log("     - T               toggle transparency");
        log("     - Y               toggle background dark-mode");
        log();
    }



    private static int _material_next = 2;
    private class _Material {
        public required Colour colour { get; init; }
        public required float metallic { get; init; }
        public required float roughness { get; init; }
    };
    private static List<_Material> _materials = new();
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
        int group_id = _material_next++;
        Colour col = new(colour ?? dflt_colour, alpha ?? dflt_alpha);
        float metal = metallic ?? dflt_metallic;
        float rough = roughness ?? dflt_roughness;
        _materials.Add(new _Material{
            colour=col,
            metallic=metal,
            roughness=rough,
        });
        if (!transparent)
            col.A = 1f;
        PICOGK_VIEWER.SetGroupMaterial(group_id, col, metal, rough);
        return group_id;
    }
    private static void _set_all_transforms(Mat4 m) {
        for (int i=1; i<_material_next; ++i)
            PICOGK_VIEWER.SetGroupMatrix(i, m);
    }
    private static void _set_all_alpha(bool transparent) {
        for (int i=2; i<_material_next; ++i) {
            _Material mat = _materials[i - 2];
            Colour col = mat.colour;
            if (!transparent)
                col.A = 1f;
            float metal = mat.metallic;
            float rough = mat.roughness;
            PICOGK_VIEWER.SetGroupMaterial(i, col, metal, rough);
        }
        Geez.transparent = transparent;
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

        ViewerHack.make_shit_happen_in_the_future();
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
            ViewerHack.make_shit_happen_in_the_future();
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
            assert(isgood(p), $"p={p}");
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
