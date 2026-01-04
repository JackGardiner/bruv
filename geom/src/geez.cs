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
using VEK = PicoGK.Viewer.EKeys;

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

    public static float dflt_line_metallic = 0.42f;
    public static float dflt_line_roughness = 0.8f;


    public static bool lockdown = false;
    public static IDisposable locked() {
        bool prev = lockdown;
        lockdown = true;
        return Scoped.on_leave(() => lockdown = prev);
    }
    public static IDisposable unlocked() {
        bool prev = lockdown;
        lockdown = false;
        return Scoped.on_leave(() => lockdown = prev);
    }

    public static Colour background_colour {
        get => Perv.get<Colour>(PICOGK_VIEWER, "m_clrBackground");
        set => PICOGK_VIEWER.SetBackgroundColor(value);
    }
    public static void set_background_colour(bool dark)
        => background_colour = dark ? new("#202020") : new("#FFFFFF");

    public static bool transparent { get; private set; } = true;
    public static void set_transparency(bool newtransparent) {
        for (int i=_MATERIAL_START; i<_material_next; ++i) {
            _Material mat = _materials[i - _MATERIAL_START];
            Colour col = mat.colour;
            if (!newtransparent)
                col.A = 1f;
            float metal = mat.metallic;
            float rough = mat.roughness;
            PICOGK_VIEWER.SetGroupMaterial(i, col, metal, rough);
        }
        transparent = newtransparent;
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
        return Scoped.on_leave(() => {
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
        return Scoped.on_leave(() => {
                Geez.dflt_colour = colour0;
                Geez.dflt_alpha = alpha0;
                Geez.dflt_metallic = metallic0;
                Geez.dflt_roughness = roughness0;
                Geez.dflt_sectioner = sectioner0;
            });
    }



    public class ViewerHack : Viewer.IViewerAction, Viewer.IKeyHandler {
        private interface Snappable<T,V> {
            T from(V v);
            V to { get; }
            T NAN { get; }
            T ZERO { get; }
            bool isnan { get; }
            bool isinf { get; }
            bool nonnan => !isnan;
            bool noninf => !isinf;
            bool isgood => nonnan && noninf;
            float mag();
            T sub(T b);
            T mul(float b);
        }
        private class SnapFloat : Snappable<SnapFloat, float> {
            public float v;
            public SnapFloat() : this(Br.NAN) {}
            public SnapFloat(float v) { this.v = v; }
            public SnapFloat from(float v) => new(v);
            public float to => v;
            public SnapFloat NAN => new(Br.NAN);
            public SnapFloat ZERO => new(0f);
            public bool isnan => Br.isnan(v);
            public bool isinf => Br.isinf(v);
            public float mag() => Br.mag(v);
            public SnapFloat sub(SnapFloat b) => new(v - b.v);
            public SnapFloat mul(float b) => new(v * b);
        }
        private class SnapVec2 : Snappable<SnapVec2, Vec2> {
            public Vec2 v;
            public SnapVec2() : this(Br.NAN2) {}
            public SnapVec2(Vec2 v) { this.v = v; }
            public SnapVec2 from(Vec2 v) => new(v);
            public Vec2 to => v;
            public SnapVec2 NAN => new(NAN2);
            public SnapVec2 ZERO => new(ZERO2);
            public bool isnan => Br.isnan(v);
            public bool isinf => Br.isinf(v);
            public float mag() => Br.mag(v);
            public SnapVec2 sub(SnapVec2 b) => new(v - b.v);
            public SnapVec2 mul(float b) => new(v * b);
        }
        private class SnapVec3 : Snappable<SnapVec3, Vec3> {
            public Vec3 v;
            public SnapVec3() : this(Br.NAN3) {}
            public SnapVec3(Vec3 v) { this.v = v; }
            public SnapVec3 from(Vec3 v) => new(v);
            public Vec3 to => v;
            public SnapVec3 NAN => new(NAN3);
            public SnapVec3 ZERO => new(ZERO3);
            public bool isnan => Br.isnan(v);
            public bool isinf => Br.isinf(v);
            public float mag() => Br.mag(v);
            public SnapVec3 sub(SnapVec3 b) => new(v - b.v);
            public SnapVec3 mul(float b) => new(v * b);
        }
        private class SnapTo<T,V> where T: Snappable<T,V>, new() {
            public T target_t;
            public float responsiveness;
            public float elapsed_time;
            public bool release_on_snap;
            public float max_time;
            public float atol;
            public float rtol;

            public V target => target_t.to;

            public SnapTo(float responsiveness, V target_v,
                    bool release_on_snap=true, float max_time=NAN,
                    float atol=5e-3f, float rtol=5e-3f) {
                target_t = new T().from(target_v);
                assert(target_t.noninf);
                this.responsiveness = responsiveness;
                this.elapsed_time = 0f;
                this.release_on_snap = release_on_snap;
                this.max_time = max_time;
                this.atol = atol;
                this.rtol = rtol;
            }

            public V Dvalue(float Dt, V value_v) {
                T value = new T().from(value_v);
                elapsed_time += Dt;
                if (elapsed_time >= max_time)
                    untarget();
                T Dvalue;
                if (target_t.isnan) {
                    Dvalue = target_t.ZERO;
                } else {
                    float ease = 1f - exp(-responsiveness * Dt);
                    Dvalue = target_t.sub(value);
                    if (Dvalue.mag() > max(atol, rtol*target_t.mag()))
                        Dvalue = Dvalue.mul(ease);
                    else if (release_on_snap)
                        untarget();
                }
                if (target_t.isnan)
                    elapsed_time = 0f;
                return Dvalue.to;
            }
            public void retarget(V newtarget_v) {
                T newtarget = new T().from(newtarget_v);
                assert(newtarget.isgood);
                target_t = newtarget;
                elapsed_time = 0f;
            }
            public void untarget() {
                target_t = target_t.NAN;
            }
        }
        // holy fucking shit these generics actually fucking blow.


        // Rotations are special enough to get their own.
        private class SnapRotation {
            public float target_theta;
            public float target_phi;
            public float responsiveness;
            public float elapsed_time;
            public bool release_on_snap;
            public float max_time;
            public float atol;

            public SnapRotation(float responsiveness, float target_theta,
                    float target_phi, bool release_on_snap=true,
                    float max_time=NAN, float atol=1e-2f) {
                assert(isnan(target_theta) == isnan(target_phi));
                assert(noninf(target_theta));
                assert(noninf(target_phi));
                this.target_theta = target_theta;
                this.target_phi   = target_phi;
                this.responsiveness = responsiveness;
                this.elapsed_time = 0f;
                this.release_on_snap = release_on_snap;
                this.max_time = max_time;
                this.atol = atol;
            }

            public void Dvalue(float Dt, float theta, float phi,
                    out float Dtheta, out float Dphi) {
                elapsed_time += Dt;
                if (elapsed_time >= max_time)
                    untarget();

                if (isnan(target_theta)) {
                    Dtheta = 0f;
                    Dphi = 0f;
                } else {
                    // lowkey just snapping theta and phi individually has much
                    // better results around the poles than a true lerped
                    // rotation.
                    float ease = 1f - exp(-responsiveness * Dt);
                    Dtheta = wraprad(target_theta - theta);
                    Dphi = wraprad(target_phi - phi);
                    if (max(abs(Dtheta), abs(Dphi)) <= atol && release_on_snap)
                        untarget();
                    if (abs(Dtheta) > atol)
                        Dtheta *= ease;
                    if (abs(Dphi) > atol)
                        Dphi *= ease;
                }

                if (isnan(target_theta))
                    elapsed_time = 0f;
            }

            public void retarget(float new_target_theta, float new_target_phi) {
                assert(isgood(new_target_theta));
                assert(isgood(new_target_phi));
                new_target_theta = wraprad(new_target_theta, true);
                new_target_phi = clamp(new_target_phi, 0f, PI);
                target_theta = new_target_theta;
                target_phi   = new_target_phi;
                elapsed_time = 0f;
            }

            public void untarget() {
                target_theta = NAN;
                target_phi   = NAN;
            }


            public void retarget(Vec2 new_target_theta_phi) {
                retarget(new_target_theta_phi.X, new_target_theta_phi.Y);
            }
        }


        /* time between "frames"/updates. */
        private static System.Diagnostics.Stopwatch stopwatch = new();

        /* bounding box properties of canonical objects. */
        public static Vec3 size { get; private set; } = 100f*ONE3;
        public static Vec3 centre { get; private set; } = ZERO3;
        public static bool explicit_scene = false;
        public static bool resize() {
            // Use the reaaal bounding box (which picogk doesnt know about).
            if (!Geez._the_box_that_bounds_them_all(out BBox3 bbox))
                return false;
            set_size(bbox.vecSize(), expl: true);
            set_centre(bbox.vecCenter(), expl: true);
            return true;
        }
        public static void set_size(Vec3 newsize, bool expl=true) {
            explicit_scene |= expl;
            assert(isgood(newsize));
            size = newsize;
        }
        public static void set_centre(Vec3 newcentre, bool expl=true) {
            explicit_scene |= expl;
            assert(isgood(newcentre));
            centre = newcentre;
        }

        /* number of times to skip overriding picogk so they can make the static
           projection matrix. */
        private static int get_a_word_in_edgewise = 5;

        /* camera pos (relative to centre) and velocity. */
        public static Vec3 pos { get; private set; } = ZERO3;
        public static Vec3 vel { get; private set; } = ZERO3;
        public static float speed = 50f;
        public static float sprint = 3f;
        private static SnapTo<SnapVec3, Vec3> snap_vel = new(20f, NAN3, false);
        private static SnapTo<SnapVec3, Vec3> snap_pos
            = new(25f, NAN3, max_time: 0.3f /* don freeze */);
        public static Vec3 future_pos => nonnan(snap_pos.target)
                                       ? snap_pos.target
                                       : pos;

        public static void set_pos(in Vec3 newpos, bool instant=false,
                bool expl=true) {
            explicit_scene |= expl;
            assert(isgood(newpos));
            if (instant) {
                pos = newpos;
                snap_pos.untarget();
            } else {
                snap_pos.retarget(newpos);
            }
        }

        /* ortho zoom value (and speed dial). managed indepedantly to picogk, but
           we continuously read theirs purely for detecting scrolling input. */
        public static float zoom { get; private set; } = 1f;
        private static SnapTo<SnapFloat, float> snap_zoom = new(40f, 1f, false);
        public static float min_zoom => mag(size) * 0.01f;
        public static float max_zoom => mag(size) * 10f;
        // We leave picogk's zoom at this value and periodically check how far
        // its moved to detect scroll input.
        private const float zoom_picogk = 100f;

        public static float future_zoom => nonnan(snap_zoom.target)
                                         ? snap_zoom.target
                                         : zoom;

        public static void set_zoom(float newzoom, bool instant=false,
                bool expl=true) {
            explicit_scene |= expl;
            assert(isgood(newzoom));
            newzoom = clamp(newzoom, min_zoom, max_zoom);
            if (instant)
                zoom = newzoom;
            snap_zoom.retarget(newzoom);
        }

        /* looking vector. */
        public static float theta {
            // need picogk to enable mouse->camera. note picogk stores camera
            // theta, not looking theta.
            get => PI + torad(PICOGK_VIEWER.m_fOrbit);
            private set {
                value = todeg(value - PI);
                // wrap to [0,360), to match picogk.
                value = wrapdeg(value - 180f) + 180f;
                PICOGK_VIEWER.m_fOrbit = value;
            }
        }
        public static float phi {
            // need picogk to enable mouse->camera. note picogk stores camera
            // elevation, not looking phi. aaalso note that by handling our own
            // view angles (and splicing them into picogk) we fix picogk's broken
            // elevation clamp.
            get => PI_2 + torad(PICOGK_VIEWER.m_fElevation);
            private set {
                value = clamp(value, 0f, PI);
                PICOGK_VIEWER.m_fElevation = todeg(value - PI_2);
            }
        }
        private static SnapRotation snap_ang = new(25f, NAN, NAN, true, 0.25f);
        public static float dflt_theta => torad(225f); // matching picogk.
        public static float dflt_phi => torad(120f); // matching picogk.
        public static Vec3 get_looking() => fromsph(1f, theta, phi);

        public static float future_theta => nonnan(snap_ang.target_theta)
                                          ? snap_ang.target_theta
                                          : theta;
        public static float future_phi => nonnan(snap_ang.target_phi)
                                        ? snap_ang.target_phi
                                        : phi;
        public static Vec3 get_future_looking()
            => fromsph(1f, future_theta, future_phi);

        public static void set_ang(float newtheta, float newphi,
                bool instant=false, bool expl=true) {
            explicit_scene |= expl;
            assert(isgood(newtheta));
            assert(isgood(newphi));
            newtheta = wraprad(newtheta, true);
            newphi = clamp(newphi, 0f, PI);
            if (instant) {
                theta = newtheta;
                phi = newphi;
                snap_ang.untarget();
                last_theta = NAN;
                last_phi = NAN;
            } else {
                snap_ang.retarget(newtheta, newphi);
            }
        }

        /* free field of view. */
        public static float fov {
            // could bypass picogk, we dont.
            get => torad(Perv.get<float>(PICOGK_VIEWER, "m_fFov"));
            private set => Perv.set(PICOGK_VIEWER, "m_fFov", todeg(value));
        }
        private static SnapTo<SnapFloat, float> snap_fov
            = new(20f, dflt_fov, false);
        public static float dflt_fov => torad(60f);
        public static float min_fov => torad(10f);
        public static float max_fov => torad(170f);

        public static float future_fov => nonnan(snap_fov.target)
                                        ? snap_fov.target
                                        : zoom;

        public static void set_fov(float newfov, bool instant=false,
                bool expl=true) {
            explicit_scene |= expl;
            assert(isgood(newfov));
            newfov = clamp(newfov, min_fov, max_fov);
            if (instant)
                fov = newfov;
            snap_fov.retarget(newfov);
        }

        /* orbit/free mode + ortho/perspective proportion. */
        public static bool orbit = true;
        public static float ortho = 1f;
        private static SnapTo<SnapFloat, float> snap_ortho
                = new(25f, ortho, false);
        private static bool extra_focal_further = false;
        public static float get_focal_dist(float with_zoom=NAN)
            => (isnan(with_zoom) ? zoom : with_zoom) * 0.9f /* may rescale */;
        public static float get_ortho_dist() => max_zoom * 10f;

        public static void set_orbit(bool neworbit, bool about_focal,
                bool instant=false, bool expl=true) {
            explicit_scene |= expl;
            if (neworbit == orbit)
                return;
            extra_focal_further = false;
            if (about_focal) {
                if (neworbit) {
                    pos += get_looking() * get_focal_dist();
                    extra_focal_further = true;
                } else {
                    pos -= get_looking() * get_focal_dist();
                }
            }
            orbit = neworbit;
            if (instant) {
                ortho = neworbit ? 1f : 0f;
                snap_ortho.retarget(ortho);
            } else {
                snap_ortho.responsiveness = neworbit ? 18f : 25f;
                snap_ortho.retarget(neworbit ? 1f : 0f);
            }
        }

        /* cheeky roll. */
        private static int roll = 0;

        /* prevent moving/rotation/mode-switching of the viewer. note this is
           bypassable by all set methods, and only locks keybinds. */
        public static float locked_for { get; private set; } = 0f;
        public static void lock_for(float seconds, bool overriding=false) {
            assert(nonnan(seconds));
            assert(seconds >= 0f);
            if (!overriding)
                seconds = max(seconds, locked_for);
            locked_for = seconds;
        }
        public static IDisposable locked() {
            float prev = locked_for;
            locked_for = +INF;
            return Scoped.on_leave(() => locked_for = prev);
        }

        /* prev variable caches, to change the scaling of them over time. */
        private static float last_theta = NAN;
        private static float last_phi = NAN;

        /* key held-state. */
        private const int KEY_SHIFT = 0x1;
        private const int KEY_CTRL  = 0x2;
        private const int KEY_ALT   = 0x4;
        private const int KEY_SUPER = 0x8;
        private const int KEY_SPACE = 0x10;
        private const int KEY_W     = 0x20;
        private const int KEY_A     = 0x40;
        private const int KEY_S     = 0x80;
        private const int KEY_D     = 0x100;
        private static int held = 0;


        public static void initialise() {
            fov = snap_fov.target; // update picogk.
            Perv.set(PICOGK_VIEWER, "m_fZoom", zoom_picogk);
            PICOGK_VIEWER.AddKeyHandler(new ViewerHack());
            _ = make_shit_happen_continuously();
        }

        public static void shutdown() {
            stop_making_shit_happen_continuously();
        }

        public static void reset() {
            // Cheeky reset.

            stopwatch.Stop();

            size = 100f*ONE3;
            centre = ZERO3;
            explicit_scene = false;

            get_a_word_in_edgewise = 5;

            pos = ZERO3;
            vel = ZERO3;
            snap_pos.untarget();
            snap_vel.untarget();

            theta = dflt_theta;
            phi = dflt_phi;
            snap_ang.untarget();

            zoom = mag(size);
            snap_zoom.retarget(zoom);
            Perv.set(PICOGK_VIEWER, "m_fZoom", zoom_picogk);

            fov = dflt_fov;
            snap_fov.retarget(fov);

            orbit = true;
            ortho = 1f;
            snap_ortho.retarget(ortho);
            extra_focal_further = false;

            last_theta = NAN;
            last_phi = NAN;
        }

        public static void reframe(bool instant=false) {
            if (!resize())
                return; // cooked it.
            set_orbit(true, true, instant, expl: false);
            set_zoom(mag(size), instant, expl: false);
            set_pos(centre, instant, expl: false);
            set_ang(dflt_theta, dflt_phi, instant, expl: false);
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
            // Get Dt across calls for movement.
            float Dt = 0f; // pretend no time if atypical.
            if (!stopwatch.IsRunning) {
                stopwatch.Start();
            } else {
                Dt = (float)stopwatch.Elapsed.TotalSeconds;
                stopwatch.Restart();
            }

            // Recalc the scene if not explicit and there are objects.
            if (!explicit_scene && resize()) {
                // We did recalc, which means this is the first render after an
                // empty scene so lets go straight to centre and ortho.
                set_orbit(true, false, instant: true, expl: false);
                set_zoom(mag(size), instant: true, expl: false);
                set_pos(centre, instant: true, expl: false);
            }


            // Decrease lock timer.
            locked_for = max(locked_for - Dt, 0f);

            // dont do movement when locked.
            bool islocked = locked_for > 0f;


            // Peek if we've had any scroll input.
            {
                float curr_zoom = Perv.get<float>(PICOGK_VIEWER, "m_fZoom");
                float Dzoom = curr_zoom - zoom_picogk;
                if (islocked) // unlucky.
                    Dzoom = 0f;
                // Rescale zoom to be size-aware and also dont use the linear
                // scaling that picogk uses.
                Dzoom *= 5f*mag(size)*log(zoom/mag(size) + 1f);
                set_zoom(snap_zoom.target + Dzoom, expl: false);
                // now tuck picogk back into bed. there there.
                Perv.set(PICOGK_VIEWER, "m_fZoom", zoom_picogk);
            }

            // We handle our own sensitivity also.
            if (nonnan(last_theta) && nonnan(last_phi)) {
                // Since angles are typically wrapped before we see their change,
                // we cant get the raw difference. Instead we get the difference
                // which is at-most 1 half-turn.
                float Dtheta = islocked ? 0f : wraprad(theta - last_theta);
                float Dphi   = islocked ? 0f : wraprad(phi   - last_phi);
                // also its probably impossible for phi to wrap buuut.
                if (orbit) {
                    theta = last_theta + 0.85f*Dtheta;
                    phi   = last_phi   + 0.85f*Dphi;
                } else {
                    theta = last_theta + 0.5f*Dtheta;
                    phi   = last_phi   + 0.5f*Dphi;
                }
            }
            // yarr matey, dont save last_ yet, for there be more to come.


            // SNAP SNAPP.
            ortho += islocked ? 0f : snap_ortho.Dvalue(Dt, ortho);
            zoom  += islocked ? 0f : snap_zoom.Dvalue(Dt, zoom);
            fov   += islocked ? 0f : snap_fov.Dvalue(Dt, fov);

            { // cheeky scope.
                snap_ang.Dvalue(Dt, theta, phi, out float Dtheta,
                        out float Dphi);
                if (islocked) {
                    Dtheta = 0f;
                    Dphi = 0f;
                }
                theta += Dtheta;
                phi   += Dphi;
            }

            // Handle accel/vel/pos.
            Frame frame = new(ZERO3, fromcyl(1f, theta, 0f), uZ3);
            Vec3 target_vel = ZERO3;
            if (isset(held, KEY_SPACE)) target_vel += frame.Z;
            if (isset(held, KEY_SHIFT)) target_vel -= frame.Z;
            if (isset(held, KEY_W))     target_vel += frame.X;
            if (isset(held, KEY_S))     target_vel -= frame.X;
            if (isset(held, KEY_A))     target_vel += frame.Y;
            if (isset(held, KEY_D))     target_vel -= frame.Y;
            target_vel = normalise_nonzero(target_vel) * speed;
            if (isset(held, KEY_CTRL))
                target_vel *= sprint;
            // Dont let movement happen while super key held.
            if (isset(held, KEY_SUPER))
                target_vel *= 0f;
            snap_vel.retarget(target_vel);
            vel += islocked ? ZERO3 : snap_vel.Dvalue(Dt, vel);

            float by = 1f;
            // Scale velocity with zoom level (normalised to size).
            by *= zoom / mag(size);
            // Scale velocity with total viewed size.
            // https://www.desmos.com/calculator/jyn4eclrcf
            by *= 0.0190255f * mag(size) + 0.00951883f;
            // Move slower in perspective mode.
            by *= lerp(0.5f, 1f, ortho);
            pos += islocked ? ZERO3 : by*vel*Dt;

            // Do any position snapping (after movement keys). Also rescale
            // what it considers a snappable distance.
            snap_pos.atol = 1e-2f * mag(size);
            pos += islocked ? ZERO3 : snap_pos.Dvalue(Dt, pos);

            // Keep everyone informed.
            last_theta = theta;
            last_phi = phi;

            // Setup box to trick picogk into thinking its the origin (if it
            // actually gets a chance to generate any matrices lmao).
            BBox3 newbbox = new(-size/2f, size/2f);
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
            float aspect_x_on_y = (proj_static[0,0] == 0f)
                                ? 1f
                                : proj_static[1,1] / proj_static[0,0];

            // Now we create the mvp matrix, perhaps blending between perspective
            // and ortho.

            Vec3 camera = pos;
            Vec3 looking = get_looking();
            // Get an up vector which wont break the view matrix on perfectly
            // up/down.
            Vec3 up = nearvert(looking)
                    ? fromcyl(1f, theta + ((looking.Z < 0f) ? 0f : PI), 0f)
                    : uZ3;
            switch ((roll % 4 + 4) % 4) {
                case 0: break;
                case 1: up =  normalise(cross(looking, up)); break;
                case 2: up = -up; break;
                case 3: up = -normalise(cross(looking, up)); break;
            }

            // Get focal lengths. Note the extra dist baked in exists to allow a
            // smooth transition about focal point (by shifting the focal point).
            float focal_dist = get_focal_dist();
            if (extra_focal_further)
                focal_dist *= 2f - ortho;
            float focal_height = 2f * focal_dist * tan(fov/2f);

            Mat4 proj;
            if (ortho > 0.999f /* pretty unreal how far it can go */) {
                float ortho_dist = get_ortho_dist();

                float near = ortho_dist*0.05f;
                float far  = ortho_dist*50f;
                // thats farkin heaps deep enough jeff.

                camera -= looking * ortho_dist; // farkin loads far enough too.

                proj = Mat4.CreateOrthographic(
                    focal_height * aspect_x_on_y,
                    focal_height,
                    near,
                    far
                );
            } else {
                // Move camera back at the exact inverse of fov increase to keep
                // focal plane static.
                float real_fov = lerp(fov, 1e-2f, ortho);
                float real_focal_dist = focal_height / 2f / tan(real_fov/2f);

                // Want ultra deep clipping area when in perspective.
                float near = real_focal_dist*0.05f;
                float far  = real_focal_dist*50f;
                near *= lerp(0.05f*mag(size)/zoom, 1f, ortho);
                far  *= lerp(0.20f*mag(size)/zoom, 1f, ortho);

                // Move the camera s.t. the focal plane doesn't change.
                camera -= looking * lerp(
                    extra_focal_further ? get_focal_dist() : 0f,
                    real_focal_dist,
                    ortho
                );

                proj = Mat4.CreatePerspectiveFieldOfView(
                    real_fov,
                    aspect_x_on_y,
                    near,
                    far
                );
            }

            // Trusty view matrix.
            Mat4 view = Mat4.CreateLookTo(
                camera,
                looking,
                up
            );

            Perv.set( // important to keep updated for obvious purposes?
                PICOGK_VIEWER,
                "m_matModelViewProjection",
                view * proj
            );
            Perv.set( // important to keep updated for lighting purposes.
                PICOGK_VIEWER,
                "m_vecEye",
                camera
            );


            // Ok now we gotta handle the compass. so ready for another round of
            // mvp and picogk overwriting.
            float compass_fov = torad(25f);
            // choose focal dist s.t. focal height = 2.
            float compass_focal_dist = 1f/tan(compass_fov/2f);
            // choose camera s.t. focal plane passes through origin. tada, the
            // compass now has a size mapped to screen space (excluding
            // perspective effects).
            Vec3 compass_camera = -compass_focal_dist*looking;
            Mat4 compass_view = Mat4.CreateLookTo(
                compass_camera,
                looking,
                up
            );
            Mat4 compass_proj = Mat4.CreatePerspectiveFieldOfView(
                compass_fov,
                aspect_x_on_y,
                0.1f,
                10f
            );
            Mat4 compass = compass_view * compass_proj;
            // Then scale down and shift into corner. Note that (1,1) is the
            // top-right corner.
            float compass_size = 0.15f;
            compass *= Mat4.CreateScale(compass_size);
            Vec3 compass_shift = new(1f, 1f, 0f);
            compass_shift -= 1.5f*compass_size
                           * new Vec3(1f/aspect_x_on_y, 1f, 0f);
            compass *= Mat4.CreateTranslation(compass_shift);

            // SEND IT.
            Perv.set(PICOGK_VIEWER, "m_matStatic", compass);
            Perv.set(PICOGK_VIEWER, "m_vecEyeStatic", compass_camera);
        }

        public static void make_shit_happen_in_the_future() {
            var actions = Perv.get<Queue<Viewer.IViewerAction>>(
                PICOGK_VIEWER,
                "m_oActions"
            );
            lock (actions)
                actions.Enqueue(new ViewerHack());
        }

        private static volatile bool stop_making_shit_happen = false;
        private static async Task make_shit_happen_continuously() {
            stop_making_shit_happen = false;
            bool leave = false;
            while (!leave) {
                if (stop_making_shit_happen) {
                    stop_making_shit_happen = false;
                    break;
                }
                make_shit_happen_in_the_future();
                await Task.Delay(8); // dont sync w library poll of 5ms.
            }
        }
        private static void stop_making_shit_happen_continuously() {
            stop_making_shit_happen = true;
        }



        public static void snap_ang_to_axis_aligned(bool instant=false) {
            float newtheta = round(future_theta / PI_2) * PI_2;
            float newphi = (future_phi < PI/5f) ? 0f
                         : (future_phi > PI - PI/5f) ? PI
                         : PI_2;
            set_ang(newtheta, newphi, instant, false);
        }

        public static void snap_ang_to_nearest_isometric(bool instant=false) {
            float newtheta = future_theta;
            newtheta -= PI_4;
            newtheta = round(newtheta / PI_2) * PI_2;
            newtheta += PI_4;
            float newphi = future_phi;
            bool tophalf = newphi <= (PI_2 + 1e-2f);
            newphi -= tophalf ? PI_4 : -PI_4;
            newphi = round(newphi / PI_2) * PI_2;
            newphi += tophalf ? PI_4 : -PI_4;
            set_ang(newtheta, newphi, instant, false);
        }

        public static void snap_ang_to_origin(bool instant=false) {
            Vec3 looking = -future_pos;
            float newtheta = argxy(looking, ifzero: future_theta);
            float newphi = argphi(looking, ifzero: future_phi);
            set_ang(newtheta, newphi, instant, false);
        }

        public static void snap_pos_to_origin(bool instant=false) {
            set_pos(ZERO3, instant, false);
        }

        public static void snap_ang_to_centre(bool instant=false) {
            Vec3 looking = centre - future_pos;
            float newtheta = argxy(looking, ifzero: future_theta);
            float newphi = argphi(looking, ifzero: future_phi);
            set_ang(newtheta, newphi, instant, false);
        }

        public static void snap_pos_to_centre(bool instant=false) {
            set_pos(centre, instant, false);
        }

        public static void snap_ang_to_z_axis(bool instant=false) {
            // Vec2 axis_point = projxy(centre); // local z axis passes through.
            Vec2 axis_point = ZERO2;
            // could do about scene z, but nah its kinda shit.

            Vec2 to_axis = axis_point - projxy(future_pos);
            float newtheta = nearzero(to_axis) // dont change view if coincident.
                           ? future_theta
                           : arg(to_axis);
            set_ang(newtheta, future_phi, instant, false);
        }

        public static void snap_pos_to_z_axis(bool instant=false) {
            // Vec3 axis_point = centre * uXY3; // local z axis passes through.
            Vec3 axis_point = ZERO3;
            // same deal with scene z.

            float z = NAN;
            if (orbit) {
                // try to do a cheeky plane line intersection.
                Vec3 point = future_pos - axis_point;
                Vec3 looking = get_future_looking();
                Vec3 right = fromcyl(1f, future_theta - PI_2, 0f);
                Vec3 normal = cross(right, looking);
                // If looking perfectly vertical, cant do intersection.
                if (!nearhoriz(normal))
                    z = -dot(normal, -point) / dot(normal, uZ3);
            }
            // otherwise jus dont change z.
            if (isnan(z))
                z = dot(future_pos - axis_point, uZ3);

            set_pos(axis_point + uZ3*z, instant, false);
        }

        /* Viewer.IViewerAction */
        public void Do(Viewer viewer) {
            make_shit_happen();
        }

        /* Viewer.IKeyHandler */
        public bool bHandleEvent(Viewer viewer, VEK key, bool pressed,
                bool shift, bool ctrl, bool alt, bool cmd) {
            // couple of these keys are inexplicably unlabelled by picogk.
            const VEK VEK_Key_Shift    = (VEK)340;
            const VEK VEK_Key_Ctrl     = (VEK)341;
            const VEK VEK_Key_Alt      = (VEK)342;
            const VEK VEK_Key_Super    = (VEK)343;
            const VEK VEK_Key_Backtick = (VEK)'`';
            const VEK VEK_Key_Equals   = (VEK)'=';

            // Check the tracked keys.
            int keycode;
            switch (key) {
                case VEK.Key_W:     keycode = KEY_W;     break;
                case VEK.Key_S:     keycode = KEY_S;     break;
                case VEK.Key_A:     keycode = KEY_A;     break;
                case VEK.Key_D:     keycode = KEY_D;     break;
                case VEK.Key_Space: keycode = KEY_SPACE; break;
                case VEK_Key_Shift: keycode = KEY_SHIFT; break;
                case VEK_Key_Ctrl:  keycode = KEY_CTRL;  break;
                case VEK_Key_Alt:   keycode = KEY_ALT;   break;
                case VEK_Key_Super: keycode = KEY_SUPER; break;
                default: goto SKIP_TRACKED;
            }
            if (pressed)
                held |= keycode;
            else
                held &= ~keycode;
            return true;

          SKIP_TRACKED:;

            // Dont handle any messages if super key pressed.
            if (isset(held, KEY_SUPER))
                return true; // dont let picogk do it either lmao.

            // Disable keybinds when locked.
            bool islocked = locked_for > 0f;

            switch (key) {
              case VEK.Key_Tab:
              case VEK_Key_Backtick:
                if (pressed && !islocked)
                    set_orbit(!orbit, key == VEK.Key_Tab, expl: false);
                return true;

                // Override picogks bad view rotater to use our snap.
              case VEK.Key_Left:
              case VEK.Key_Right:
              case VEK.Key_Up:
              case VEK.Key_Down:
                if (pressed && !islocked) {
                    Vec2 ang = new(future_theta, future_phi);
                    float size_theta = ctrl ? PI_2 : PI_4;
                    float size_phi   = ctrl ? PI_2 : PI/6f;
                    bool round_inactive = false;
                    switch (key) {
                        case VEK.Key_Left:
                            ang.X = floor((ang.X + 5e-2f) / size_theta);
                            ang.X += 1f;
                            ang.X *= size_theta;
                            if (round_inactive)
                                ang.Y = round(ang.Y / size_phi) * size_phi;
                            break;
                        case VEK.Key_Right:
                            ang.X = ceil((ang.X - 5e-2f) / size_theta);
                            ang.X -= 1f;
                            ang.X *= size_theta;
                            if (round_inactive)
                                ang.Y = round(ang.Y / size_phi) * size_phi;
                            break;
                        case VEK.Key_Up:
                            ang.Y = ceil((ang.Y - 5e-2f) / size_phi);
                            ang.Y -= 1f;
                            ang.Y *= size_phi;
                            if (round_inactive)
                                ang.X = round(ang.X / size_theta) * size_theta;
                            break;
                        case VEK.Key_Down:
                            ang.Y = floor((ang.Y + 5e-2f) / size_phi);
                            ang.Y += 1f;
                            ang.Y *= size_phi;
                            if (round_inactive)
                                ang.X = round(ang.X / size_theta) * size_theta;
                            break;
                    }
                    snap_ang.retarget(ang);
                }
                return true;

              case VEK.Key_Q:
                // get snapping.
                if (pressed && !islocked) {
                    if (ctrl)
                        snap_ang_to_nearest_isometric();
                    else
                        snap_ang_to_axis_aligned();
                }
                return true;
              case VEK.Key_E:
                // snap snap.
                if (pressed && !islocked) {
                    if (ctrl) {
                        if (orbit)
                            snap_pos_to_origin();
                        else
                            snap_ang_to_origin();
                    } else {
                        if (orbit)
                            snap_pos_to_z_axis();
                        else
                            snap_ang_to_z_axis();
                    }
                }
                return true;
              case VEK.Key_R:
                // dujj.
                if (pressed && !islocked) {
                    if (ctrl == orbit)
                        snap_ang_to_centre();
                    else
                        snap_pos_to_centre();
                }
                return true;

              case VEK_Key_Equals:
                if (pressed && !islocked)
                    reframe();
                return true;
              case VEK.Key_Backspace:
                if (pressed && !islocked) {
                    if (ctrl)
                        reset();
                    else
                        get_a_word_in_edgewise = 5;
                }
                return true;

              case VEK.Key_K:
              case VEK.Key_L:
                if (pressed && !islocked && !orbit) {
                    float newfov = future_fov;
                    // scale s.t. decreasing changes at either end and s.t.
                    // repeated alternating applications settle at some value.
                    float scale_up   = dflt_fov/PI/8f;
                    float scale_down = (1f - dflt_fov/PI)/8f;
                    newfov += (key == VEK.Key_K)
                            ? (PI - newfov)*scale_up
                            : (0f - newfov)*scale_down;
                    set_fov(newfov, expl: false);
                }
                return true;

              case VEK.Key_O:
              case VEK.Key_P:
                if (pressed && !islocked) {
                    roll += (key == VEK.Key_O) ? -1 : 1;
                }
                return true;

              case VEK.Key_T:
                if (pressed && !islocked)
                    set_transparency(!transparent);
                return true;

              case VEK.Key_Y:
                if (pressed && !islocked) {
                    bool light = background_colour.Equals(COLOUR_WHITE);
                    set_background_colour(light);
                }
                return true;
            }
            return false;
        }

        public class Signal : Viewer.IViewerAction {
            private ManualResetEventSlim done { get; }
            public Signal(ManualResetEventSlim done) {
                this.done = done;
            }

            /* Viewer.IViewerAction */
            public void Do(Viewer viewer) {
                done.Set();
            }
        }
    }

    public static void initialise() {
        _initialise_compass();
        ViewerHack.initialise();
        print();
        print("[Geez] using a new hacked-in camera, keybinds:");
        print("     - W/A/S/D         move camera horizontally");
        print("     - space/shift     move camera up/down");
        print("     - arrow keys      rotate view");
        print("     - scroll up/down  orbit zoom in/out + move slower/faster");
        print("     - ctrl held       stronger movement + arrow key rotation");
        print("     - tab             toggle orbit/free mode at focal point");
        print("     - backtick [`/~]  toggle orbit/free mode at pivot point");
        print("     - Q               snap view along nearest axis");
        print("     - ctrl+Q          snap view to nearest isometric angle");
        print("     - E               snap pivot point in orbit or view in free "
                                   + "to scene Z axis");
        print("     - ctrl+E          snap pivot point in orbit or view in free "
                                   + "to origin");
        print("     - R               snap pivot point in orbit or view in free "
                                   + "to scene centre");
        print("     - ctrl+R          snap view in orbit or pivot point in free "
                                   + "to scene centre");
        print("     - equals [+/=]    reframe view to full scene");
        print("     - backspace       fix window aspect ratio");
        print("     - ctrl+backspace  reset view (+fix window aspect ratio)");
        print("     - K/L             dial up/down free fov");
        print("     - O/P             super hacked-in roll camera");
        print("     - T               toggle transparency");
        print("     - Y               toggle background dark-mode");
        print();
    }

    public static void shutdown() {
        ViewerHack.shutdown();
    }


    public static void lookat(BBox3 bbox, float theta=NAN, float phi=NAN) {
        lookat(
            pos: bbox.vecCenter(),
            theta: theta,
            phi: phi,
            zoom: mag(bbox.vecSize()),
            orbit: true,
            fov: NAN
        );
    }
    public static void lookat(Vec3? pos=null, float theta=NAN, float phi=NAN,
            float zoom=NAN, bool orbit=true, float fov=NAN) {
        pos = (pos != null) ? pos : ViewerHack.future_pos;
        zoom = nonnan(zoom) ? zoom : ViewerHack.future_zoom;
        theta = nonnan(theta) ? theta : ViewerHack.future_theta;
        phi = nonnan(phi) ? phi : ViewerHack.future_phi;
        fov = nonnan(fov) ? fov : ViewerHack.future_fov;

        ViewerHack.set_orbit(orbit, false,  instant: true);
        ViewerHack.set_zoom(zoom,           instant: true);
        ViewerHack.set_pos(pos.Value,       instant: true);
        ViewerHack.set_ang(theta, phi,      instant: true);
        ViewerHack.set_fov(fov,             instant: true);
    }

    public static IDisposable remember_current_layout() {
        bool expl = ViewerHack.explicit_scene;
        Vec3 size = ViewerHack.size;
        Vec3 centre = ViewerHack.centre;
        bool orbit = ViewerHack.orbit;
        float zoom = ViewerHack.future_zoom;
        Vec3 pos = ViewerHack.future_pos;
        float theta = ViewerHack.future_theta;
        float phi = ViewerHack.future_phi;
        float fov = ViewerHack.future_fov;
        return Scoped.on_leave(() => {
            ViewerHack.explicit_scene = expl;
            ViewerHack.set_size(size,                         expl: false);
            ViewerHack.set_centre(centre,                     expl: false);
            ViewerHack.set_orbit(orbit, false,  instant:true, expl: false);
            ViewerHack.set_zoom(zoom,           instant:true, expl: false);
            ViewerHack.set_pos(pos,             instant:true, expl: false);
            ViewerHack.set_ang(theta, phi,      instant:true, expl: false);
            ViewerHack.set_fov(fov,             instant:true, expl: false);
        });
    }

    public static void screenshot(string name, bool existok=true) {
        string path = fromroot($"exports/ss_{name}.tga");
        if (!existok && File.Exists(path))
            throw new Exception($"screenshot already exists: '{path}'");

        object req_ss = Perv.create_nested_type(
            typeof(Viewer),
            "RequestScreenShotAction",
            path
        );

        using ManualResetEventSlim done = new();
        ViewerHack.Signal signal = new(done);

        // Ensure the viewer is open.
        lock (Perv.get<object>(typeof(PicoGK.Library), "mtxRunOnce")) {
            bool running = Perv.get<bool>(typeof(PicoGK.Library), "bRunning");
            if (!running) {
                print("ERROR: failed to screenshot because window is closed.");
                return;
            }

            // Enqueue the actions, ensuring our matrices are set, then ss taken,
            // then stop blocking this thread.
            var actions = Perv.get<Queue<Viewer.IViewerAction>>(
                PICOGK_VIEWER,
                "m_oActions"
            );
            lock (actions) {
                actions.Enqueue(new ViewerHack());
                actions.Enqueue((Viewer.IViewerAction)req_ss);
                actions.Enqueue(signal);
            }
        }
        // Wait until ss saved.
        done.Wait();
    }

    public static void wipe_screenshot(string name, bool missingok=true) {
        string path = fromroot($"exports/ss_{name}.tga");
        if (!missingok && !File.Exists(path))
            throw new Exception($"screenshot doesn't exist: '{path}'");

        File.Delete(path);
    }




    private static Dictionary<int, object> _geezed = new();
    private static int _next = 1; // must leave <=0 as illegal.
    private static int _track(in List<PolyLine> lines, in List<Mesh> meshes) {
        // nothing added during lockdown.
        if (lockdown)
            return BLANK;
        int key = _next++;
        lock (_geezed)
            _geezed.Add(key, (lines, meshes));
        return key;
    }
    private static int _track(in List<int> group) {
        if (lockdown)
            return BLANK;
        int key = _next++;
        lock (_geezed)
            _geezed.Add(key, group);
        return key;
    }
    private static bool _the_box_that_bounds_them_all(out BBox3 bbox) {
        bbox = new();
        bool nonempty = false;
        lock (_geezed) {
            foreach (object item in _geezed.Values) {
                if (item is (List<PolyLine> lines, List<Mesh> meshes)) {
                    foreach (Mesh mesh in meshes) {
                        if (mesh.nVertexCount() > 0
                                && mesh.nTriangleCount() > 0) {
                            nonempty = true;
                            bbox.Include(mesh.oBoundingBox());
                        }
                    }
                    foreach (PolyLine line in lines) {
                        if (line.nVertexCount() > 0) {
                            nonempty = true;
                            bbox.Include(line.oBoundingBox());
                        }
                    }
                }
            }
        }
        return nonempty;
    }
    private const int _MATERIAL_DUMMY = int.MinValue;
    private const int _MATERIAL_COMPASS = int.MinValue + 1;
    private const int _MATERIAL_START = 1;
    private static int _material_next = _MATERIAL_START;
    private class _Material {
        public required Colour colour { get; init; }
        public required float metallic { get; init; }
        public required float roughness { get; init; }
    };
    private static List<_Material> _materials = new();
    // NOTE: theres a rly dumb picogk bug where they just ignore the metallic
    // and roughness values for polylines, despite those values affecting the
    // render of polylines..... however meshes set these uniforms and dont like
    // reset them so we just ensure a mesh is drawn in the group before it with
    // the right metallic/roughness.
    private static int _material(bool fixbug=false) {
        int group_id = _material_next++;
        Colour col = new(colour ?? dflt_colour, alpha ?? dflt_alpha);
        if (fixbug)
            col = COLOUR_BLANK;
        float metal = metallic ?? dflt_metallic;
        float rough = roughness ?? dflt_roughness;
        _materials.Add(new _Material{
            colour=col,
            metallic=metal,
            roughness=rough,
        });
        if (!transparent && !fixbug)
            col.A = 1f;
        PICOGK_VIEWER.SetGroupMaterial(group_id, col, metal, rough);
        return group_id;
    }
    private static List<PolyLine> _compass_lines = new();
    private const float _COMPASS_METALLIC = 0.42f;
    private const float _COMPASS_ROUGHNESS = 0.8f;
    private static void _initialise_compass() {
        // this mat metallic/roughness affects compass, since it comes just
        // before it in the non-static group rendering.
        assert(_MATERIAL_DUMMY == _MATERIAL_COMPASS - 1);
        PICOGK_VIEWER.SetGroupMaterial(
            _MATERIAL_DUMMY,
            COLOUR_BLANK,
            _COMPASS_METALLIC,
            _COMPASS_ROUGHNESS
        );
        // use the static grouping since thats where compass is.
        PICOGK_VIEWER.SetGroupStatic(_MATERIAL_DUMMY, true);
        // note group must be visible to be drawn before compass.
        PICOGK_VIEWER.Add(new Mesh(), _MATERIAL_DUMMY);

        PICOGK_VIEWER.SetGroupMaterial(
            _MATERIAL_COMPASS,
            COLOUR_BLACK,
            1f,
            1f
        );
        // compass gotta be in the static rendering groups, since it uses the
        // overridden static rendering matrix.
        PICOGK_VIEWER.SetGroupStatic(_MATERIAL_COMPASS, true);
        _compass_lines = new();
        _frame_lines(out _, _compass_lines, new Frame(),
                     size: 1f, mark_pos: false);
        foreach (PolyLine line in _compass_lines)
            PICOGK_VIEWER.Add(line, _MATERIAL_COMPASS);
    }

    private static void _view(in List<PolyLine> lines, in List<Mesh> meshes) {
        // nothing added during lockdown.
        if (lockdown)
            return;

        if (numel(meshes) > 0) {
            int group_id = _material();
            foreach (Mesh mesh in meshes)
                PICOGK_VIEWER.Add(mesh, group_id);
        }
        if (numel(lines) > 0) {
            // setup dummy group to fix picogk bug. basically they forget to set
            // the metallic and roughness uniforms when rendering lines, so we
            // render a dummy mesh just before them which correctly sets
            // metallic and roughness and then "leaks" these values into the line
            // render.
            int dummy_id = _material(fixbug: true);
            PICOGK_VIEWER.Add(new Mesh(), dummy_id);
            int group_id = _material();
            foreach (PolyLine line in lines)
                PICOGK_VIEWER.Add(line, group_id);
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



    public static int recent(int ignore=0) {
        int key = _next;
        lock (_geezed) {
            assert(numel(_geezed) > 0);
            assert(ignore >= 0);
            for (int i=-1; i<ignore; ++i) {
                while (!_geezed.ContainsKey(key - 1)) // O(n) skull emoji.
                    --key;
                --key;
            }
        }
        return key;
    }
    public static void pop(int count, int ignore=0) {
        assert(count >= 0);
        assert(ignore >= 0);
        while (count --> 0) // so glad down-to operator made it into c#.
            remove(recent(ignore));
    }
    public static void remove(int key) {
        if (key <= 0) // noop.
            return;

        object item;
        lock (_geezed) {
            item = _geezed[key];
            _geezed.Remove(key);
        }

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
        List<int> keys;
        lock (_geezed)
            keys = new(_geezed.Keys);
        remove(keys);
    }
    public static int group(List<int> keys) {
        return _track(keys);
    }


    public const int BLANK = 0; // due to locking.
    public const int CLEAR = -1;
    public const int NOOP = -2;

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
            assert(key > 0 || key == BLANK || key == CLEAR || key == NOOP);
            if (key == CLEAR) {
                clear();
                return;
            }
            if (key == NOOP)
                return;
            if (keys[i] != BLANK)
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

        public void voxels(in Voxels vox) {
            using (this.like())
                this.cycle(Geez.voxels(vox));
        }
        public void mesh(in Mesh mesh) {
            using (this.like())
                this.cycle(Geez.mesh(mesh));
        }
    }


    public static int voxels(in Voxels vox, Colour? colour=null) {
        if (lockdown) // nada.
            return BLANK;
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
        if (lockdown)
            return BLANK;
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
        if (lockdown)
            return BLANK;
        PolyLine line = new(colour ?? Geez.colour ?? COLOUR_GREEN);
        line.Add(points);
        if (arrow > 0f)
            line.AddArrow(arrow);
        return Geez.lines([line]);
    }
    public static int lines(in List<PolyLine> lines) {
        using (dflt_like(metallic: dflt_line_metallic,
                         roughness: dflt_line_roughness))
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
            Colour? pos_colour=null) {
        return Geez.frames(new FramesSequence([frame]), size: size,
                mark_pos: mark_pos, pos_colour: pos_colour);
    }
    public static int frames(in List<Frame> frames, float size=5f,
            bool mark_pos=false, Colour? pos_colour=null) {
        return Geez.frames(new FramesSequence(frames), size: size,
                mark_pos: mark_pos, pos_colour: pos_colour);
    }
    public static int frames(in Frames frames, float size=5f,
            bool mark_pos=false, Colour? pos_colour=null) {
        if (lockdown)
            return BLANK;
        List<PolyLine> lines = new();
        List<Mesh> meshes = new();
        for (int i=0; i<numel(frames); ++i) {
            Frame frame = frames.at(i);
            _frame_lines(out Mesh? mesh, lines, frame, size, mark_pos);
            if (mesh != null)
                meshes.Add(mesh);
        }
        int key_line;
        using (dflt_like(metallic: dflt_line_metallic,
                         roughness: dflt_line_roughness))
            key_line = _push(lines);
        if (!mark_pos)
            return key_line;
        int key_mesh;
        using (dflt_like(colour: COLOUR_WHITE, alpha: 1f, metallic: 0.1f,
                         roughness: 0.8f))
        using (like(colour: pos_colour))
            key_mesh = _push(meshes);
        return group([key_line, key_mesh]);
    }

    public static int dir(in Vec3 dir, Colour? colour=null, float arrow=1f) {
        return Geez.dir(new Frame(), dir, colour: colour, arrow: arrow);
    }
    public static int dir(in Vec3 from, in Vec3 to, Colour? colour=null,
            float arrow=1f) {
        return Geez.dir(new Frame(from), to - from, colour: colour,
                arrow: arrow);
    }
    public static int dir(in Frame frame, in Vec3 dir, Colour? colour=null,
            float arrow=1f) {
        if (lockdown)
            return BLANK;
        PolyLine line = new(colour ?? Geez.colour ?? COLOUR_WHITE);
        line.nAddVertex(frame * ZERO3);
        line.nAddVertex(frame * dir);
        if (arrow > 0f)
            line.AddArrow(arrow);
        using (dflt_like(metallic: dflt_line_metallic,
                         roughness: dflt_line_roughness))
            return _push([line]);
    }

    public static int bbox(in BBox3 bbox, Colour? colour=null) {
        return Geez.cuboid(new Cuboid(bbox), colour: colour);
    }

    public static int pipe(in Pipe pipe, Colour? colour=null, int rings=-1,
            int bars=6) {
        if (lockdown)
            return BLANK;
        float r = pipe.rhi;
        float L = pipe.Lz;
        Frame frame = pipe.centre;

        if (rings == -1)
            rings = max(3, (int)(L * 2.5f / (TWOPI/bars*r)));
        assert(rings >= 2 || rings == 0);
        assert(bars >= 2 || bars == 0);

        List<PolyLine> lines = new();
        PolyLine line;
        Colour col = colour ?? Geez.colour ?? COLOUR_BLUE;

        bool done_inner = false;
      DO:;

        // Rings.
        for (int n=0; n<rings; ++n) {
            float z = n*L/(rings - 1);
            line = new(col);
            int N = 100;
            for (int i=0; i<N; ++i) {
                float theta = i*TWOPI/(N - 1);
                line.nAddVertex(frame * fromcyl(r, theta, z));
            }
            lines.Add(line);
        }

        // Bars.
        for (int n=0; n<bars; ++n) {
            float theta = n*TWOPI/bars;
            line = new(col);
            line.Add([
                frame * fromcyl(r, theta, 0f),
                frame * fromcyl(r, theta, L),
            ]);
            lines.Add(line);
        }

        // Maybe do inner also.
        if (!done_inner) {
            done_inner = true;
            if (pipe.rlo > 0f) {
                // Add connections to the bars.
                for (int n=0; n<bars; ++n) {
                    float theta = n*TWOPI/bars;
                    line = new(col);
                    line.Add([
                        frame * fromcyl(pipe.rlo, theta, 0f),
                        frame * fromcyl(pipe.rhi, theta, 0f),
                    ]);
                    lines.Add(line);
                    line = new(col);
                    line.Add([
                        frame * fromcyl(pipe.rlo, theta, L),
                        frame * fromcyl(pipe.rhi, theta, L),
                    ]);
                    lines.Add(line);
                }

                r = pipe.rlo;
                goto DO;
            } else {
                // Add crosses on the end.
                assert((bars%2) == 0);
                for (int n=0; n<bars/2; ++n) {
                    float theta = n*TWOPI/bars;
                    line = new(col);
                    line.Add([
                        frame * fromcyl(r, theta, 0f),
                        frame * fromcyl(r, theta + PI, 0f),
                    ]);
                    lines.Add(line);
                    line = new(col);
                    line.Add([
                        frame * fromcyl(r, theta, L),
                        frame * fromcyl(r, theta + PI, L),
                    ]);
                    lines.Add(line);
                }
            }
        }

        using (dflt_like(metallic: dflt_line_metallic,
                         roughness: dflt_line_roughness))
            return _push(lines);
    }

    private static void _cuboid_lines(List<PolyLine> lines, in Cuboid cuboid,
            Colour? colour) {
        List<Vec3> corners = cuboid.get_corners();
        Colour col = colour ?? Geez.colour ?? COLOUR_BLUE;
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
        if (lockdown)
            return BLANK;
        List<PolyLine> lines = new();
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
        using (dflt_like(metallic: dflt_line_metallic,
                         roughness: dflt_line_roughness))
            return _push(lines);
    }




    static Geez() {
        // Create the unit origin ball mesh, starting with an icosahedron then
        // subdividing.

        float gr = 0.5f*(1f + sqrt(5f));
        List<Vec3> V = [
            new(-1,  gr,   0), new(  1, gr,   0), new( -1, -gr,  0),
            new( 1, -gr,   0), new(  0, -1,  gr), new(  0,   1, gr),
            new( 0,  -1, -gr), new(  0,  1, -gr), new( gr,   0, -1),
            new(gr,   0,   1), new(-gr,  0,  -1), new(-gr,   0,  1)
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
