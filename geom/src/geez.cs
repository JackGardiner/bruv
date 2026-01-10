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

    public static Colour BACKGROUND_COLOUR_DARK => new("#202020");
    public static Colour BACKGROUND_COLOUR_LIGHT => new("#FFFFFF");
    public static Colour background_colour {
        get => Perv.get<Colour>(PICOGK_VIEWER, "m_clrBackground");
        set => PICOGK_VIEWER.SetBackgroundColor(value);
    }
    public static void set_background_colour(bool dark)
        => background_colour = dark
                             ? BACKGROUND_COLOUR_DARK
                             : BACKGROUND_COLOUR_LIGHT;

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
        private class Snapper /* fishy */ {
            public float[] target;
            public float[] elapsed_time;
            public bool snapped;
            public float responsiveness;
            public bool release_on_snap;
            public float max_time;
            public float rtol;
            public float atol;
            public int N => numel(target);

            public float target1 {
                get { assert(N == 1); return tovec1(target); }
            }
            public Vec2 target2 {
                get { assert(N == 2); return tovec2(target); }
            }
            public Vec3 target3 {
                get { assert(N == 3); return tovec3(target); }
            }


            public Snapper(float responsiveness, float target,
                    bool release_on_snap=true, float max_time=NAN,
                    float rtol=5e-3f, float atol=5e-3f)
                : this(responsiveness, toarr(target), release_on_snap, max_time,
                    rtol, atol) {}
            public Snapper(float responsiveness, Vec2 target,
                    bool release_on_snap=true, float max_time=NAN,
                    float rtol=5e-3f, float atol=5e-3f)
                : this(responsiveness, toarr(target), release_on_snap, max_time,
                    rtol, atol) {}
            public Snapper(float responsiveness, Vec3 target,
                    bool release_on_snap=true, float max_time=NAN,
                    float rtol=5e-3f, float atol=5e-3f)
                : this(responsiveness, toarr(target), release_on_snap, max_time,
                    rtol, atol) {}
            public Snapper(float responsiveness, float[] target,
                    bool release_on_snap=true, float max_time=NAN,
                    float rtol=5e-3f, float atol=5e-3f) {
                foreach (float t in target)
                    assert(noninf(t));
                this.target = target;
                assert(within(N, 1, 3));
                this.elapsed_time = new float[N]; // zeroed.
                this.snapped = false;
                this.responsiveness = responsiveness;
                this.release_on_snap = release_on_snap;
                this.max_time = max_time;
                this.rtol = rtol;
                this.atol = atol;
            }

            public float[] Dvalue(float Dt, float[] value) {
                assert(numel(value) == N);
                float ease = 1f - exp(-responsiveness * Dt);

                for (int i=0; i<N; ++i) {
                    elapsed_time[i] += Dt;
                    if (elapsed_time[i] >= max_time)
                        untarget(i);
                }

                // Map down to an indexed view of just the targeted (nonnan)
                // elements.
                int n = 0;
                Span<int> iin = stackalloc int[3];
                Span<int> iout = stackalloc int[3];
                for (int i=0; i<N; ++i) {
                    if (nonnan(target[i])) {
                        iin[i] = n;
                        iout[n] = i;
                        n += 1;
                    }
                }

                float[] Dvalue = new float[N]; // zeroed.
                assert(within(n, 0, 3));
                switch (n) {
                    case 3: {
                        // Treat targets as vector in multidim space.
                        Vec3 t = tovec3(target);
                        Vec3 v = tovec3(value);
                        Vec3 D = t - v;
                        if (!snapped && !nearto(v, t, rtol, atol))
                            D *= ease;
                        else {
                            snapped = true;
                            if (release_on_snap)
                                untarget();
                        }
                        // Unpack to Dvalue.
                        Dvalue = toarr(D);
                    } break;
                    case 2: {
                        Vec2 t = new(target[iout[0]], target[iout[1]]);
                        Vec2 v = new(value[iout[0]], value[iout[1]]);
                        Vec2 D = t - v;
                        if (!snapped && !nearto(v, t, rtol, atol))
                            D *= ease;
                        else {
                            snapped = true;
                            if (release_on_snap) {
                                untarget(iout[0]);
                                untarget(iout[1]);
                            }
                        }
                        Dvalue[iout[0]] = D[0];
                        Dvalue[iout[1]] = D[1];
                    } break;
                    case 1: {
                        float t = target[iout[0]];
                        float v = value[iout[0]];
                        float D = t - v;
                        if (!snapped && !nearto(v, t, rtol, atol))
                            D *= ease;
                        else {
                            snapped = true;
                            if (release_on_snap)
                                untarget(iout[0]);
                        }
                        Dvalue[iout[0]] = D;
                    } break;
                }

                // If in future impl switches to Dvalue holding packed nonnan
                // deltas:
              #if false
                // Unpack to full. Note that iin is strictly increasing, so
                // reverse order will not overwrite future elements.
                for (int i = N - 1; i >= 0; --i)
                    Dvalue[i] = Dvalue[iin[i]];
              #endif

                for (int i=0; i<N; ++i) {
                    if (isnan(target[i]))
                        elapsed_time[i] = 0f;
                }

                return Dvalue;
            }
            public float Dvalue(float Dt, float value) {
                assert(N == 1);
                return tovec1(Dvalue(Dt, toarr(value)));
            }
            public Vec2 Dvalue(float Dt, Vec2 value) {
                assert(N == 2);
                return tovec2(Dvalue(Dt, toarr(value)));
            }
            public Vec3 Dvalue(float Dt, Vec3 value) {
                assert(N == 3);
                return tovec3(Dvalue(Dt, toarr(value)));
            }

            public void retarget(int i, float newtarget) {
                assert_idx(i, N);
                if (isnan(newtarget))
                    return;
                assert(isgood(newtarget));
                target[i] = newtarget;
                elapsed_time[i] = 0f;
                snapped = false;
            }
            public void retarget(float[] newtarget) {
                assert(numel(newtarget) == N);
                for (int i=0; i<N; ++i)
                    retarget(i, newtarget[i]);
            }
            public void retarget(float newtarget) => retarget(toarr(newtarget));
            public void retarget(Vec2 newtarget)  => retarget(toarr(newtarget));
            public void retarget(Vec3 newtarget)  => retarget(toarr(newtarget));

            public void untarget(int i) {
                assert_idx(i, N);
                target[i] = NAN;
                elapsed_time[i] = 0f;
                snapped = false;
            }
            public void untarget() {
                for (int i=0; i<N; ++i)
                    untarget(i);
            }
        }


        // Rotations are special enough to get their own.
        private class SnapperRotation /* i barely know her */ {
            public float target_theta;
            public float target_phi;
            public float elapsed_time_theta;
            public float elapsed_time_phi;
            public bool snapped;
            public float responsiveness;
            public bool release_on_snap;
            public float max_time;
            public float atol;
            // no rtol, since rotation has a fixed concept of magnitude.

            public SnapperRotation(float responsiveness,
                    bool release_on_snap=true, float max_time=NAN,
                    float atol=1e-2f) {
                this.target_theta = NAN;
                this.target_phi   = NAN;
                this.elapsed_time_theta = 0f;
                this.elapsed_time_phi   = 0f;
                this.snapped = false;
                this.responsiveness = responsiveness;
                this.release_on_snap = release_on_snap;
                this.max_time = max_time;
                this.atol = atol;
            }

            public void Dvalue(float Dt, float theta, float phi,
                    out float Dtheta, out float Dphi) {
                float ease = 1f - exp(-responsiveness * Dt);

                elapsed_time_theta += Dt;
                elapsed_time_phi   += Dt;
                if (elapsed_time_theta >= max_time)
                    untarget_theta();
                if (elapsed_time_phi >= max_time)
                    untarget_phi();

                // lowkey just snapping theta and phi individually has much
                // better results around the poles than a true lerped rotation.
                // but note we still take the shortest split rotation to get
                // there.

                Dtheta = 0f;
                Dphi = 0f;

                // Different snapping on both via only one axis.
                if (nonnan(target_theta) && nonnan(target_phi)) {
                    Dtheta = wraprad(target_theta - theta);
                    Dphi = wraprad(target_phi - phi);
                    // Only snap when both are close, measured by "euclidean"
                    // (on a sphere instead of manhatten.
                    if (hypot(Dtheta, Dphi) > atol) {
                        Dtheta *= ease;
                        Dphi *= ease;
                    } else {
                        snapped = true;
                        if (release_on_snap) {
                            untarget_theta();
                            untarget_phi();
                        }
                    }
                } else if (nonnan(target_theta)) {
                    Dtheta = wraprad(target_theta - theta);
                    if (abs(Dtheta) > atol)
                        Dtheta *= ease;
                    else {
                        snapped = true;
                        if (release_on_snap)
                            untarget_theta();
                    }
                } else if (nonnan(target_phi)) {
                    Dphi = wraprad(target_phi - phi);
                    if (abs(Dphi) > atol)
                        Dphi *= ease;
                    else {
                        snapped = true;
                        if (release_on_snap)
                            untarget_phi();
                    }
                }

                if (isnan(target_theta))
                    elapsed_time_theta = 0f;
                if (isnan(target_phi))
                    elapsed_time_phi = 0f;
            }

            public void retarget_theta(float new_target_theta) {
                if (isnan(new_target_theta))
                    return;
                assert(isgood(new_target_theta));
                new_target_theta = wraprad(new_target_theta);
                target_theta = new_target_theta;
                elapsed_time_theta = 0f;
                snapped = false;
            }
            public void retarget_phi(float new_target_phi) {
                if (isnan(new_target_phi))
                    return;
                assert(isgood(new_target_phi));
                new_target_phi = clamp(new_target_phi, 0f, PI);
                target_phi = new_target_phi;
                elapsed_time_phi = 0f;
                snapped = false;
            }
            public void retarget(float new_target_theta, float new_target_phi) {
                retarget_theta(new_target_theta);
                retarget_phi(new_target_phi);
            }

            public void untarget_theta() {
                target_theta = NAN;
                elapsed_time_theta = 0f;
                snapped = false;
            }
            public void untarget_phi() {
                target_phi = NAN;
                elapsed_time_phi = 0f;
                snapped = false;
            }
        }


        // Well cant leave frames out.
        private class SnapperFrame {
            public Frame? target;
            public float elapsed_time;
            public bool snapped;
            public float responsiveness;
            public bool release_on_snap;
            public float max_time;
            public float atol;
            // no rtol, since frames have a fixed concept of magnitude.

            public SnapperFrame(float responsiveness, bool release_on_snap=true,
                    float max_time=NAN, float atol=1e-2f) {
                this.target = null;
                this.elapsed_time = 0f;
                this.snapped = false;
                this.responsiveness = responsiveness;
                this.release_on_snap = release_on_snap;
                this.max_time = max_time;
                this.atol = atol;
            }

            public Frame Dvalue(float Dt, Frame value) {
                float ease = 1f - exp(-responsiveness * Dt);

                elapsed_time += Dt;
                if (elapsed_time >= max_time)
                    untarget();

                Frame Dframe = new();

                // Different snapping on both via only one axis.
                if (target != null) {
                    (target - value).get_rotation(out Vec3 about, out float by);
                    if (abs(by) > atol) {
                        by *= ease;
                    } else {
                        snapped = true;
                        if (release_on_snap)
                            untarget();
                    }
                    Dframe = Frame.rotation(about, by);
                }

                if (target == null)
                    elapsed_time = 0f;

                return Dframe;
            }

            public void retarget(Frame? newtarget) {
                if (newtarget == null)
                    return;
                assert(nearzero(newtarget.pos));
                target = new(ZERO3, newtarget); // move to origin.
                elapsed_time = 0f;
                snapped = false;
            }

            public void untarget() {
                target = null;
                elapsed_time = 0f;
                snapped = false;
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
            assert(isgood(newsize));
            explicit_scene |= expl;
            size = newsize;
        }
        public static void set_centre(Vec3 newcentre, bool expl=true) {
            assert(isgood(newcentre));
            explicit_scene |= expl;
            centre = newcentre;
        }

        /* number of times to skip overriding picogk so they can make the static
           projection matrix. */
        private static int get_a_word_in_edgewise = 10;
        private static void lemme_get_a_word_in_edgewise()
            => get_a_word_in_edgewise = 10;

        /* camera pos (relative to centre) and velocity. */
        public static Vec3 pos { get; private set; } = ZERO3;
        public static Vec3 vel { get; private set; } = ZERO3;
        public static float speed = 50f;
        public static float sprint = 3f;
        private static Snapper snap_vel = new(20f, NAN3, false);
        private static Snapper snap_pos
            = new(25f, NAN3, max_time: 0.3f /* don freeze */,
                  rtol: 0f /* we manually tweak atol */);
        public static Vec3 future_pos => ifnanelem(snap_pos.target3, pos);

        public static void set_pos(in Vec3 newpos, bool instant=false,
                bool expl=true) {
            assert(noninf(newpos));
            if (nonnan(newpos.X) || nonnan(newpos.Y) || nonnan(newpos.Z))
                explicit_scene |= expl;
            if (instant) {
                pos = ifnanelem(newpos, pos);
                for (int i=0; i<3; ++i) {
                    if (nonnan(newpos[i]))
                        snap_pos.untarget(i);
                }
            } else {
                snap_pos.retarget(newpos);
            }
        }

        /* ortho zoom value (and speed dial). managed indepedantly to picogk, but
           we continuously read theirs purely for detecting scrolling input. */
        public static float zoom { get; private set; } = 1f;
        private static Snapper snap_zoom = new(40f, 1f, false);
        public static float min_zoom => mag(size) * 0.01f;
        public static float max_zoom => mag(size) * 10f;
        public static float orbit_zoom_sensitivity => 0.02f;
        public static float free_zoom_sensitivity  => 0.03f;
        // We leave picogk's zoom at this value and periodically check how far
        // its moved to detect scroll input.
        private const float picogk_zoom = 100f;

        public static float future_zoom => ifnan(snap_zoom.target1, zoom);

        public static void set_zoom(float newzoom, bool instant=false,
                bool expl=true) {
            assert(noninf(newzoom));
            if (isnan(newzoom))
                return;
            explicit_scene |= expl;
            newzoom = clamp(newzoom, min_zoom, max_zoom);
            if (instant)
                zoom = newzoom;
            snap_zoom.retarget(newzoom);
        }

        /* camera/movement rotations. managed in the same way as zoom, where we
          decouple  from picogk and only use their theta/phi to detect mouse
          movement. */
        public static float dflt_theta => torad(225f);
        public static float dflt_phi => torad(120f);
        public static Frame dflt_orient => new();
        private static float _theta = dflt_theta;
        private static float _phi = dflt_phi;
        private static Frame _orient = dflt_orient;
        public static float theta {
            get => _theta;
            private set => _theta = wraprad(value);
        }
        public static float phi {
            get => _phi;
            // note we fix picogks broken phi clamp.
            private set => _phi = clamp(value, 0f, PI);
        }
        // Note orient is just used to store an arbitrary rotation about an
        // arbitrary axis. Now if only there were some higher dimensional vector
        // type that could do that.... alas, we'll have to use a frame.
        public static Frame orient {
            get => _orient;
            private set {
                assert(nearzero(value.pos));
                _orient = new(ZERO3, value); // move to origin.
            }
        }
        private static SnapperRotation snap_ang = new(25f, true, 0.25f);
        private static SnapperFrame snap_orient = new(20f, false);

        // Looking frame s.t. +X is direction of looking, +Z is perp upwards.
        public static Frame get_looking() => get_looking(theta, phi, orient);
        public static Frame get_looking(float theta, float phi, Frame orient) {
            Vec3 looking = orient * fromsph(1f, theta, phi);
            Vec3 up      = orient * fromsph(1f, theta, phi - PI_2);
            return new(ZERO3, looking, up);
        }
        // Flying frame s.t. +X is direction on W press, +Z is space press.
        public static Frame get_flying() => get_flying(theta, phi, orient);
        public static Frame get_flying(float theta, float phi, Frame orient) {
            Vec3 forwards = orient * fromcyl(1f, theta, 0f);
            Vec3 upwards  = orient * uZ3;
            return new(ZERO3, forwards, upwards);
        }

        // Same thing as with zoom we reset picogks theta/phi to this each time
        // and only use the difference between each update to check difference.
        // These values are chosen as the midpoints between picogks intended
        // clamps (intended because their phi clamp is broken due to a typo).
        private static float picogk_theta = 180f;
        private static float picogk_phi = 90f;
        public static float orbit_looking_sensitivity => torad(0.425f);
        public static float free_looking_sensitivity  => torad(0.25f);

        public static float future_theta => ifnan(snap_ang.target_theta, theta);
        public static float future_phi   => ifnan(snap_ang.target_phi,   phi);
        public static Frame future_orient => snap_orient.target ?? orient;
        public static Frame get_future_looking()
            => get_looking(future_theta, future_phi, future_orient);
        public static Frame get_future_flying()
            => get_flying(future_theta, future_phi, future_orient);

        public static void set_ang(float newtheta, float newphi,
                bool instant=false, bool expl=true) {
            assert(noninf(newtheta));
            assert(noninf(newphi));
            if (nonnan(newtheta) || nonnan(newphi))
                explicit_scene |= expl;
            if (instant) {
                theta = ifnan(newtheta, theta);
                phi   = ifnan(newphi, phi);
                if (nonnan(newtheta))
                    snap_ang.untarget_theta();
                if (nonnan(newphi))
                    snap_ang.untarget_phi();
            } else {
                snap_ang.retarget(newtheta, newphi);
            }
        }

        public static void set_orient(Frame? neworient, bool instant=false,
                bool expl=true) {
            if (neworient == null)
                return;
            explicit_scene |= expl;
            neworient = new(ZERO3, neworient);
            if (instant)
                orient = neworient;
            snap_orient.retarget(neworient);
        }


        /* free field of view. handled as two parts: genuine settable fov and
           "tweak" fov due to zoom/movespeed. */
        private static float _fov = dflt_fov;
        public static float fov {
            get => _fov;
            private set => _fov = clamp(value, min_fov, max_fov);
        }
        // In perspective, show speed by fov change. Note since fov is unused in
        // ortho we can just still apply it then also.
        public static float fov_tweak() {
            float t = (zoom - min_zoom) / (max_zoom - min_zoom);
            // Concentrate at [s]lower end.
            t = sqrt(t);
            float scale_up   = fov/PI/3f;
            float scale_down = (1f - fov/PI)/3f;
            float low  = (0f - fov)*scale_down;
            float high = (PI - fov)*scale_up;
            return lerp(low, high, t);
        }
        private static float fov_full()
            => clamp(fov + fov_tweak(), min_fov, max_fov);
        private static Snapper snap_fov = new(20f, dflt_fov, false);
        public static float dflt_fov => torad(60f);
        public static float min_fov => torad(10f);
        public static float max_fov => torad(170f);

        public static float future_fov => ifnan(snap_fov.target1, fov);

        public static void set_fov(float newfov, bool instant=false,
                bool expl=true) {
            assert(noninf(newfov));
            if (isnan(newfov))
                return;
            explicit_scene |= expl;
            newfov = clamp(newfov, min_fov, max_fov);
            if (instant)
                fov = newfov;
            snap_fov.retarget(newfov);
        }

        /* orbit/free mode + ortho/perspective proportion. */
        public static bool orbit { get; private set; } = true;
        public static float ortho { get; private set; } = 1f;
        private static Snapper snap_ortho
            = new(25f, ortho, false, rtol: 0f, atol: 1e-3f);
        private static bool extra_focal_further = false;
        public static float get_focal_dist(float with_zoom=NAN)
            => ifnan(with_zoom, zoom) * 0.9f /* may rescale */;
        public static float get_ortho_dist() => max_zoom * 10f;

        public static void set_orbit(bool neworbit, bool about_focal,
                bool instant=false, bool expl=true) {
            explicit_scene |= expl;
            if (neworbit == orbit)
                return;
            extra_focal_further = false;
            if (about_focal) {
                if (neworbit) {
                    pos += get_looking().X * get_focal_dist();
                    extra_focal_further = true;
                } else {
                    pos -= get_looking().X * get_focal_dist();
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

        /* key held-state. */
        private const int KEY_SHIFT     = 1 << 0;
        private const int KEY_CTRL      = 1 << 1;
        private const int KEY_ALT       = 1 << 2;
        private const int KEY_SUPER     = 1 << 3;
        private const int KEY_SPACE     = 1 << 4;
        private const int KEY_W         = 1 << 5;
        private const int KEY_A         = 1 << 6;
        private const int KEY_S         = 1 << 7;
        private const int KEY_D         = 1 << 8;
        private const int KEY_MOVE_CTRL = 1 << 9;
        private const int KEY_Q         = 1 << 10;
        private const int KEY_E         = 1 << 11;
        private const int KEY_R         = 1 << 12;
        private const int KEY_QER_CTRL  = 1 << 13;
        private static int held = 0;
        private const int KEY_MOVE = KEY_SHIFT | KEY_SPACE
                                   | KEY_W | KEY_A | KEY_S | KEY_D;
        private const int KEY_QER = KEY_Q | KEY_E | KEY_R;


        public static void initialise() {
            // Initialise picogk to the fixed values.
            Perv.set(PICOGK_VIEWER, "m_fZoom", picogk_zoom);
            PICOGK_VIEWER.m_fOrbit     = picogk_theta;
            PICOGK_VIEWER.m_fElevation = picogk_phi;
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

            lemme_get_a_word_in_edgewise();

            pos = ZERO3;
            vel = ZERO3;
            snap_pos.untarget();
            snap_vel.untarget();

            theta = dflt_theta;
            phi = dflt_phi;
            orient = dflt_orient;
            snap_ang.untarget_theta();
            snap_ang.untarget_phi();
            snap_orient.retarget(orient);
            PICOGK_VIEWER.m_fOrbit     = picogk_theta;
            PICOGK_VIEWER.m_fElevation = picogk_phi;

            zoom = mag(size);
            snap_zoom.retarget(zoom);
            Perv.set(PICOGK_VIEWER, "m_fZoom", picogk_zoom);

            fov = dflt_fov;
            snap_fov.retarget(fov);

            orbit = true;
            ortho = 1f;
            snap_ortho.retarget(ortho);
            extra_focal_further = false;
        }

        public static void reframe(bool instant=false) {
            if (!resize())
                return; // cooked it.
            set_orbit(true, true, instant, expl: false);
            set_zoom(mag(size), instant, expl: false);
            set_pos(centre, instant, expl: false);
            set_ang(dflt_theta, dflt_phi, instant, expl: false);
            set_orient(dflt_orient, instant, expl: false);
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
            float Dscroll;
            {
                float curr_zoom = Perv.get<float>(PICOGK_VIEWER, "m_fZoom");
                // now tuck picogk back into bed. there there.
                Perv.set(PICOGK_VIEWER, "m_fZoom", picogk_zoom);
                // Invert picogks scroll->zoom function.
                Dscroll = 50f * (curr_zoom - picogk_zoom);
            }

            // Peek if we've had any mouse movement. Note this only detects mouse
            // movement that occurs while any mouse button is held.
            float Dmx; // + = right
            float Dmy; // + = down
            {
                float curr_theta = PICOGK_VIEWER.m_fOrbit;
                float curr_phi   = PICOGK_VIEWER.m_fElevation;
                // nightie.
                PICOGK_VIEWER.m_fOrbit     = picogk_theta;
                PICOGK_VIEWER.m_fElevation = picogk_phi;
                // Invert transform.
                Dmx = -2f*(curr_theta - picogk_theta);
                Dmy = +2f*(curr_phi   - picogk_phi);
            }


            // Handle movement/viewer changes due to input.
            if (!islocked) {

                { // Zoom on scroll.
                    float sens = lerp(free_zoom_sensitivity,
                                      orbit_zoom_sensitivity,
                                      ortho);
                    float Dzoom = sens * Dscroll;
                    // Rescale zoom to be size-aware and also dont use the linear
                    // scaling that picogk uses.
                    Dzoom *= 5f*mag(size)*log(zoom/mag(size) + 1f);
                    set_zoom(future_zoom + Dzoom, expl: false);
                }

                { // Rotate on mouse move.
                    float sens = lerp(free_looking_sensitivity,
                                      orbit_looking_sensitivity,
                                      ortho);
                    float Dtheta = sens * -Dmx;
                    float Dphi   = sens * +Dmy;
                    // Dont allow mouse movement if that angle is snapping.
                    if (nonnan(snap_ang.target_theta))
                        Dtheta = 0f;
                    if (nonnan(snap_ang.target_phi))
                        Dphi = 0f;
                    theta += Dtheta;
                    phi += Dphi;
                }

                { // Snap rotate.
                    snap_ang.Dvalue(Dt, theta, phi,
                            out float Dtheta, out float Dphi);
                    theta += Dtheta;
                    phi   += Dphi;
                }

                { // Snap orient.
                    Frame Dorient = snap_orient.Dvalue(Dt, orient);
                    // frame snapping is kinda imprecise, do it explicitly.
                    if (snap_orient.snapped)
                        orient = snap_orient.target!;
                    else
                        orient += Dorient;
                }

                // Handle hotkeys being down. Note these are mutually exclusive
                // (enforced when detecting if held).

                // View align snap.
                if (isset(held, KEY_Q)) {
                    if (isset(held, KEY_QER_CTRL))
                        snap_ang_to_nearest_isometric();
                    else
                        snap_ang_to_axis_aligned();
                }
                // Pos/view origin snap.
                if (isset(held, KEY_E)) {
                    if (isset(held, KEY_QER_CTRL)) {
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
                // Pos/view scene centre snap.
                if (isset(held, KEY_R)) {
                    if (isset(held, KEY_QER_CTRL) == orbit)
                        snap_ang_to_centre();
                    else
                        snap_pos_to_centre();
                }
            }

            // Now that new view has been calced determine camera/movement.
            Frame looking = get_looking();
            Frame flying = get_flying();

            // Continue updating.
            if (!islocked) {

                { // Handle accel/vel.
                    Vec3 target_vel = ZERO3;
                    if (isset(held, KEY_SPACE)) target_vel += flying.Z;
                    if (isset(held, KEY_SHIFT)) target_vel -= flying.Z;
                    if (isset(held, KEY_W))     target_vel += flying.X;
                    if (isset(held, KEY_S))     target_vel -= flying.X;
                    if (isset(held, KEY_A))     target_vel += flying.Y;
                    if (isset(held, KEY_D))     target_vel -= flying.Y;
                    target_vel = normalise_nonzero(target_vel) * speed;
                    if (isset(held, KEY_MOVE_CTRL))
                        target_vel *= sprint;
                    // Dont let movement happen while super key held.
                    if (isset(held, KEY_SUPER))
                        target_vel = ZERO3;
                    snap_vel.retarget(target_vel);
                    Vec3 Dvel = snap_vel.Dvalue(Dt, vel);
                    // If snapping pos, instantly snap vel in that axis to 0.
                    if (nonnan(snap_pos.target3.X)) Dvel.X = -vel.X;
                    if (nonnan(snap_pos.target3.Y)) Dvel.Y = -vel.Y;
                    if (nonnan(snap_pos.target3.Z)) Dvel.Z = -vel.Z;
                    vel += Dvel;
                }

                { // Handle pos.
                    float sens = 1f;
                    // Scale velocity with zoom level (normalised to size).
                    sens *= zoom / mag(size);
                    // Scale velocity with total viewed size.
                    // https://www.desmos.com/calculator/jyn4eclrcf
                    sens *= 0.0190255f * mag(size) + 0.00951883f;
                    // Move slower in perspective mode.
                    sens *= lerp(0.5f, 1f, ortho);
                    pos += sens*vel*Dt;

                    // Do any position snapping (after movement keys). Also
                    // rescale what it considers a snappable distance.
                    snap_pos.atol = 1e-3f * mag(size);
                    pos += snap_pos.Dvalue(Dt, pos);
                }

                // SNAP SNAPP. note these come after that which is dependent on
                // them.
                ortho += snap_ortho.Dvalue(Dt, ortho);
                zoom += snap_zoom.Dvalue(Dt, zoom);
                fov += snap_fov.Dvalue(Dt, fov);
            }


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
                Perv.invoke(PICOGK_VIEWER, "RecalculateBoundingBox");
                PICOGK_VIEWER.RequestUpdate();
                return;
            } else {
                Perv.set(PICOGK_VIEWER, "m_oBBox", new BBox3());
            }


            // Get aspect ratio by backing it out of the static projection
            // matrix that picogk calculates (since its actually able to listen
            // for window size cbs, whereas we must scrape it off the ground as
            // dust).
            Mat4 proj_static = Perv.get<Mat4>(PICOGK_VIEWER,
                                              "m_matProjectionStatic");
            float aspect_x_on_y = (proj_static[0,0] == 0f)
                                ? 1f
                                : proj_static[1,1] / proj_static[0,0];


            // Now we create the mvp matrix, perhaps blending between perspective
            // and ortho.

            Vec3 camera = pos; // for view matrix.
            Vec3 eye; // for eye opengl uniform.

            // Get focal lengths. Note the extra dist baked in exists to allow a
            // smooth transition about focal point (by shifting the focal point).
            float focal_dist = get_focal_dist();
            if (extra_focal_further)
                focal_dist *= 2f - ortho;
            float focal_height = 2f * focal_dist * tan(fov_full()/2f);

            Mat4 proj;
            float near;
            float far;
            if (ortho >= 1f - snap_ortho.atol) {
                float ortho_dist = get_ortho_dist();

                near = -ortho_dist;
                far  = +ortho_dist;
                // thats farkin heaps deep enough jeff.

                // leave camera.
                eye = pos - looking.X * ortho_dist;
                // farkin loads far enough too.

                proj = Mat4.CreateOrthographic(
                    focal_height * aspect_x_on_y,
                    focal_height,
                    near,
                    far
                );
            } else {
                // Move camera back at the exact inverse of fov increase to keep
                // focal plane static.
                float real_fov = lerp(fov_full(), 1e-4f, ortho);
                float real_focal_dist = focal_height / 2f / tan(real_fov/2f);

                // Want ultra deep clipping area when in perspective. Also
                // independent of zoom when in perspective.
                near = real_focal_dist*0.1f;
                far  = max(focal_dist*50.0f, real_focal_dist*1.2f);
                near *= lerp(0.1f*mag(size)/zoom, 1f, ortho);
                far  *= lerp(1.0f*mag(size)/zoom, 1f, ortho);

                // Move the camera s.t. the focal plane doesn't change.
                camera -= looking.X * lerp(
                    extra_focal_further ? get_focal_dist() : 0f,
                    real_focal_dist,
                    ortho
                );
                eye = camera;

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
                looking.X,
                looking.Z
            );

            Mat4 objects = view * proj;


            // Ok now we gotta handle the compass. so ready for another round of
            // mvp and picogk overwriting.
            float compass_fov = torad(25f);
            // choose focal dist s.t. focal height = 2.
            float compass_focal_dist = 1f/tan(compass_fov/2f);
            // choose camera s.t. focal plane passes through origin. tada, the
            // compass now has a size mapped to screen space (excluding
            // perspective effects).
            Vec3 compass_camera = -looking.X * compass_focal_dist;
            Vec3 compass_eye = compass_camera;
            Mat4 compass_view = Mat4.CreateLookTo(
                compass_camera,
                looking.X,
                looking.Z
            );
            float camera_near = 0.1f;
            float camera_far  = 10f;
            Mat4 compass_proj = Mat4.CreatePerspectiveFieldOfView(
                compass_fov,
                aspect_x_on_y,
                camera_near,
                camera_far
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

            // Also setup the dashed compass to show local orientation, aka
            // ignoring `orient`.
            Mat4 compass_dashed = new Transformer().to_global(orient).mat_T;
            // Also push the dashed compass slightly towards camera, to make it
            // appear on-top of other compass.
            compass_dashed *= Mat4.CreateTranslation(-0.05f*looking.X);

            // Aaaand now we gotta remap the clip-space depths of all objects and
            // compass to ensure the compass is rendered on top of all other
            // rendered objects.
            Mat4 remap_depth(float mindepth, float maxdepth) {
                Mat4 remap = Mat4.Identity;
                remap.M33 = (maxdepth - mindepth) * 0.5f;
                remap.M43 = (maxdepth + mindepth) * 0.5f;
                return remap;
            }
            // Lower depth is closer to cam. Dont want to lose precision on
            // objects, and compass can easily get away with very low prec.
            compass *= remap_depth(0f, 0.0499f);
            objects *= remap_depth(0.05f, 1f);

            // SEND IT. Note that updating eye is important for lighting
            // purposes, and updating mat is important for the entire purpose?
            // like thats the sole function of ViewerHack. pull it together.
            Perv.set(PICOGK_VIEWER, "m_matModelViewProjection", objects);
            Perv.set(PICOGK_VIEWER, "m_vecEye", eye);
            Perv.set(PICOGK_VIEWER, "m_matStatic", compass);
            Perv.set(PICOGK_VIEWER, "m_vecEyeStatic", compass_eye);
            PICOGK_VIEWER.SetGroupMatrix(
                _MATERIAL_COMPASS_DASHED,
                compass_dashed
            );
        }

        public static void make_shit_happen_in_the_future() {
            var actions = Perv.get<Queue<Viewer.IViewerAction>>(
                PICOGK_VIEWER,
                "m_oActions"
            );
            lock (actions)
                actions.Enqueue(new ViewerHack());
            PICOGK_VIEWER.RequestUpdate(); // takes its own lock.
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
            float newphi = (abs(future_phi)      < PI/5f) ? 0f
                         : (abs(future_phi - PI) < PI/5f) ? PI
                         : PI_2;
            set_ang(newtheta, newphi, instant, false);
        }

        public static void snap_ang_to_nearest_isometric(bool instant=false) {
            float isometric_phi = atan(SQRT2); // baffling.
            float newtheta = future_theta;
            newtheta -= PI_4;
            newtheta = round(newtheta / PI_2) * PI_2;
            newtheta += PI_4;
            float newphi = (abs(future_phi) >= PI_2) // tophalf.
                         ? PI - isometric_phi
                         : isometric_phi;
            set_ang(newtheta, newphi, instant, false);
        }

        public static void snap_ang_to_origin(bool instant=false) {
            Vec3 lookto = future_orient / (-future_pos);
            float newtheta = argxy(lookto, ifzero: future_theta);
            float newphi = argphi(lookto, ifzero: future_phi);
            set_ang(newtheta, newphi, instant, false);
        }

        public static void snap_pos_to_origin(bool instant=false) {
            set_pos(ZERO3, instant, false);
        }

        public static void snap_ang_to_centre(bool instant=false) {
            Vec3 lookto = future_orient / (centre - future_pos);
            float newtheta = argxy(lookto, ifzero: future_theta);
            float newphi = argphi(lookto, ifzero: future_phi);
            set_ang(newtheta, newphi, instant, false);
        }

        public static void snap_pos_to_centre(bool instant=false) {
            set_pos(centre, instant, false);
        }

        public static void snap_ang_to_z_axis(bool instant=false) {
            Vec2 line_point = ZERO2;
            Vec2 to_axis = line_point - projxy(future_orient / future_pos);
            float newtheta = nearzero(to_axis) // dont change view if coincident.
                           ? NAN
                           : arg(to_axis);
            set_ang(newtheta, NAN, instant, false);
        }

        public static void snap_pos_to_z_axis(bool instant=false) {
            Vec3 line_point = ZERO3;
            Vec3 line_dir = future_orient.Z;

            float z = dot(pos, line_dir);
            float newz = NAN;
            if (orbit) {
                // try to do a cheeky plane line intersection.
                Vec3 point = future_pos;
                Vec3 normal = get_future_looking().Z;
                // If looking perfectly vertical, cant do intersection.
                float denom = dot(normal, line_dir);
                if (!nearzero(denom)) {
                    newz = dot(line_point, line_dir)
                         + dot(normal, point - line_point) / denom;
                }
                // else leave nan.
            }
            // If not moving at all, dont target.
            if (nearto(newz, z))
                newz = NAN;
            // note leaving newz as nan means unchanged.

            Vec3 newpos = line_point;
            // mm need to preserve the nan-ness. requires a clean element assign.
            if (nearpara(line_dir, uX3))
                newpos.X = newz;
            else if (nearpara(line_dir, uY3))
                newpos.Y = newz;
            else if (nearpara(line_dir, uZ3))
                newpos.Z = newz;
            else {
                // otherwise dw about letting glide up/down local z. our snapper
                // cant handle it (rn).
                newpos += line_dir * ifnan(newz - z, 0f);
            }
            set_pos(newpos, instant, false);
        }

        public static void snap_upright(bool instant=false) {
            Vec3 newlookto = fromsph(1f, future_theta, future_phi);
            Vec3 newforwards = fromcyl(1f, future_theta, 0f);
            newlookto = future_orient * newlookto;
            newforwards = future_orient * newforwards;
            float newtheta = argxy(newlookto, ifzero: argxy(newforwards));
            float newphi = argphi(newlookto);
            // Note this wont necessarily look smooth but it will end up at the
            // correct result.
            set_ang(newtheta, newphi, instant, false);
            set_orient(dflt_orient, instant, false);
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
                case VEK_Key_Shift: keycode = KEY_SHIFT; break;
                case VEK_Key_Ctrl:  keycode = KEY_CTRL;  break;
                case VEK_Key_Alt:   keycode = KEY_ALT;   break;
                case VEK_Key_Super: keycode = KEY_SUPER; break;
                case VEK.Key_Space: keycode = KEY_SPACE; break;
                case VEK.Key_W:     keycode = KEY_W;     break;
                case VEK.Key_S:     keycode = KEY_S;     break;
                case VEK.Key_A:     keycode = KEY_A;     break;
                case VEK.Key_D:     keycode = KEY_D;     break;
                case VEK.Key_Q:     keycode = KEY_Q;     break;
                case VEK.Key_E:     keycode = KEY_E;     break;
                case VEK.Key_R:     keycode = KEY_R;     break;

                default: goto SKIP_TRACKED;
            }
            // Enforce mutually exclusive qer.
            if (pressed && !isclr(held, KEY_QER) && !isclr(keycode, KEY_QER))
                keycode = 0;
            // Copy press message to move/qer control.
            if (pressed && isset(keycode, KEY_CTRL))
                keycode |= KEY_MOVE_CTRL | KEY_QER_CTRL;
            // Update state.
            if (pressed)
                held |= keycode;
            else
                held &= ~keycode;
            // If all qer/move keysets released, clear their ctrl hold.
            if (isclr(held, KEY_CTRL)) {
                if (isclr(held, KEY_MOVE))
                    held &= ~KEY_MOVE_CTRL;
                if (isclr(held, KEY_QER))
                    held &= ~KEY_QER_CTRL;
            }
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

                // Override picogks view rotater to use our snap.
              case VEK.Key_Left:
              case VEK.Key_Right:
              case VEK.Key_Up:
              case VEK.Key_Down: {
                if (!pressed || islocked)
                    return true;
                float newtheta = NAN;
                float newphi = NAN;
                float size_theta = ctrl ? PI_2 : PI_4;
                float size_phi   = ctrl ? PI_2 : PI/6f;
                bool round_inactive = false;
                switch (key) {
                  case VEK.Key_Left:
                    newtheta = floor((future_theta + 5e-2f) / size_theta);
                    newtheta += 1f;
                    newtheta *= size_theta;
                    if (round_inactive) {
                        newphi = round(future_phi / size_phi)
                                * size_phi;
                    }
                    break;
                  case VEK.Key_Right:
                    newtheta = ceil((future_theta - 5e-2f) / size_theta);
                    newtheta -= 1f;
                    newtheta *= size_theta;
                    if (round_inactive) {
                        newphi = round(future_phi / size_phi)
                                * size_phi;
                    }
                    break;
                  case VEK.Key_Up:
                    newphi = ceil((future_phi - 5e-2f) / size_phi);
                    newphi -= 1f;
                    newphi *= size_phi;
                    if (round_inactive) {
                        newtheta = round(future_theta / size_theta)
                                    * size_theta;
                    }
                    break;
                  case VEK.Key_Down:
                    newphi = floor((future_phi + 5e-2f) / size_phi);
                    newphi += 1f;
                    newphi *= size_phi;
                    if (round_inactive) {
                        newtheta = round(future_theta / size_theta)
                                    * size_theta;
                    }
                    break;
                }
                set_ang(newtheta, newphi, false, expl: false);
            } return true;

              case VEK_Key_Equals:
                if (pressed && !islocked)
                    reframe();
                return true;
              case VEK.Key_Backspace:
                if (pressed && !islocked) {
                    if (ctrl) {
                        reset();
                    } else {
                        lemme_get_a_word_in_edgewise();
                        _ = resize();
                    }
                }
                return true;

              case VEK.Key_F:
              case VEK.Key_G: {
                if (!pressed || islocked || orbit)
                    return true;
                float newfov = future_fov;
                // scale s.t. decreasing changes at either end and s.t.
                // repeated alternating applications settle at some value.
                float scale_up   = dflt_fov/PI/8f;
                float scale_down = (1f - dflt_fov/PI)/8f;
                newfov += (key == VEK.Key_F)
                        ? (PI - newfov)*scale_up
                        : (0f - newfov)*scale_down;
                set_fov(newfov, expl: false);
            } return true;

              case VEK.Key_Z:
              case VEK.Key_X: {
                if (!pressed || islocked)
                    return true;
                // roll about looking axis.
                Vec3 about = get_looking().X;
                Frame neworient;
                if (ctrl) {
                    // Kinda dodgy it into being a stepped increment. im not even
                    // sure if this concept is extensible to 3d, but in 2d its
                    // (done up in the on-arrow-key logic) achived by first
                    // rounding down (against the direction of increment) to the
                    // nearest step and then jumping to the next step. So like
                    // idk maybe this will work.
                    float by = (key == VEK.Key_X) ? +PI_2 : -PI_2;
                    neworient = future_orient;
                    neworient = neworient.rotate(about, -0.4f*by, false);
                    neworient = neworient.closest_parallel(dflt_orient);
                    neworient = neworient.rotate(about, by, false);
                    neworient = neworient.closest_parallel(dflt_orient);
                    // holy shit it basically worked.
                } else {
                    float by = (key == VEK.Key_X) ? +PI/12f : -PI/12f;
                    neworient = future_orient.rotate(about, by, false);
                }

                // Keep looking vector in the same direction. Note that this
                // correction doesnt lead to the correct travel path but ends at
                // the right spot.
                Vec3 lookto = fromsph(1f, future_theta, future_phi);
                Vec3 forwards = fromcyl(1f, future_theta, 0f);
                lookto = future_orient * lookto;
                forwards = future_orient * forwards;
                Vec3 newlookto = neworient / lookto;
                Vec3 newforwards = neworient / forwards;
                float newtheta = argxy(newlookto, ifzero: argxy(newforwards));
                float newphi = argphi(newlookto);

                set_ang(newtheta, newphi, expl: false);
                set_orient(neworient, expl: false);
            } return true;
              case VEK.Key_C:
                if (pressed && !islocked)
                    snap_upright();
                return true;


              case VEK.Key_T:
                if (pressed && !islocked)
                    Geez.set_transparency(!transparent);
                return true;

              case VEK.Key_Y:
                if (pressed && !islocked) {
                    bool light = background_colour.Equals(COLOUR_WHITE);
                    Geez.set_background_colour(light);
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
        print("  RESETTING");
        print("   - equals (+/=)     reframe view to full upright scene");
        print("   - backspace        recalc scene (+fix window aspect ratio)");
        print("   - ctrl+backspace   reset view (+fix window aspect ratio)");
        print("  VIEW");
        print("   - click+drag       rotate view");
        print("   - Z/X              roll view left/right");
        print("               [ctrl] snaps to axis aligned");
        print("   - C                roll to upright");
        print("   - arrow keys       rotate view to fixed increments");
        print("               [ctrl] increased increments");
        print("  MOVEMENT");
        print("   - W/A/S/D          move camera horizontally");
        print("   - space/shift      move camera up/down");
        print("               [ctrl] faster movement");
        print("  FREE/ORBIT MODE");
        print("   - tab              toggle mode around focal point");
        print("   - backtick (`/~)   toggle mode around pivot point");
        print("  ZOOM/SPEED/FOV");
        print("   - scroll up/down   move slower/faster");
        print("              [orbit] also zooms in/out");
        print("   - F/G       [free] dial up/down fov");
        print("  SNAPPING");
        print("   - Q                snap view along nearest axis");
        print("               [ctrl] instead snap to nearest isometric angle");
        print("   - E                snap horizontally to origin");
        print("              [orbit] snaps pivot point");
        print("               [free] snaps view");
        print("               [ctrl] completely to origin");
        print("   - R                snap to scene centre");
        print("              [orbit] snaps pivot point");
        print("               [free] snaps view");
        print("               [ctrl] swap pivot point/view target");
        print("  VIEWING OPTIONS");
        print("   - T                toggle transparency");
        print("   - Y                toggle background dark-mode");
        print();
    }

    public static void shutdown() {
        ViewerHack.shutdown();
    }


    public class ViewAs {
        public Vec3? pos { get; set; }
        public float theta { get; set; }
        public float phi { get; set; }
        public Frame? orient { get; set; }
        public float zoom { get; set; }
        public float fov { get; set; }
        public bool? orbit { get; set; }
        public Colour? bgcol { get; set; }
        public bool? transparent { get; set; }

        public ViewAs(BBox3 bbox, float theta=NAN, float phi=NAN,
                Frame? orient=null, Colour? bgcol=null, bool? transparent=null)
            : this(
                pos: bbox.vecCenter(),
                theta: theta,
                phi: phi,
                orient: orient,
                zoom: mag(bbox.vecSize()),
                orbit: true,
                fov: NAN,
                bgcol: bgcol,
                transparent: transparent
            ) {}

        public ViewAs(Vec3? pos=null, float theta=NAN, float phi=NAN,
                Frame? orient=null, float zoom=NAN, float fov=NAN,
                bool? orbit=null, Colour? bgcol=null, bool? transparent=null) {
            this.pos = pos;
            this.theta = theta;
            this.phi = phi;
            this.orient = orient;
            this.zoom = zoom;
            this.fov = fov;
            this.orbit = orbit;
            this.bgcol = bgcol;
            this.transparent = transparent;
        }

        public void now() {
            if (pos != null)
                ViewerHack.set_pos(pos.Value, instant: true);
            if (nonnan(theta) || nonnan(phi)) {
                theta = ifnan(theta, ViewerHack.future_theta);
                phi   = ifnan(phi,   ViewerHack.future_phi);
                ViewerHack.set_ang(theta, phi, instant: true);
            }
            if (orient != null)
                ViewerHack.set_orient(orient, instant: true);
            if (nonnan(zoom))
                ViewerHack.set_zoom(zoom, instant: true);
            if (nonnan(fov))
                ViewerHack.set_fov(fov, instant: true);
            if (orbit != null)
                ViewerHack.set_orbit(orbit.Value, false, instant: true);
            if (bgcol != null)
                background_colour = bgcol.Value;
            if (transparent != null)
                set_transparency(transparent.Value);
        }

        public IDisposable now_but_not_forever() {
            IDisposable ret = remember_setup();
            now();
            return ret;
        }
    }

    public static IDisposable remember_setup() {
        Colour bgcol = background_colour;
        bool transparent = Geez.transparent;
        bool expl    = ViewerHack.explicit_scene;
        Vec3 size    = ViewerHack.size;
        Vec3 centre  = ViewerHack.centre;
        bool orbit   = ViewerHack.orbit;
        float zoom   = ViewerHack.future_zoom;
        Vec3 pos     = ViewerHack.future_pos;
        float theta  = ViewerHack.future_theta;
        float phi    = ViewerHack.future_phi;
        Frame orient = ViewerHack.future_orient;
        float fov    = ViewerHack.future_fov;
        return Scoped.on_leave(() => {
            background_colour = bgcol;
            Geez.transparent = transparent;
            ViewerHack.explicit_scene = expl;
            ViewerHack.set_size(size,                        expl: false);
            ViewerHack.set_centre(centre,                    expl: false);
            ViewerHack.set_orbit(orbit, false, instant:true, expl: false);
            ViewerHack.set_zoom(zoom,          instant:true, expl: false);
            ViewerHack.set_pos(pos,            instant:true, expl: false);
            ViewerHack.set_ang(theta, phi,     instant:true, expl: false);
            ViewerHack.set_orient(orient,      instant:true, expl: false);
            ViewerHack.set_fov(fov,            instant:true, expl: false);
        });
    }


    public class Screenshotta {
        public ViewAs view_as { get; }

        public Screenshotta(in ViewAs view_as) {
            this.view_as = view_as;
        }

        public void take(string name, bool existok=true) {
            using (remember_setup())
            using (ViewerHack.locked())
            using (view_as.now_but_not_forever())
                screenshot(name, existok: existok);
        }
    }

    public static void screenshot(string name, bool existok=true) {
        if (lockdown)
            return;

        string path = fromroot($"exports/ss-{name}.tga");
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

    public static void wipe_screenshot(string name, bool missingok=true,
            bool alsopng=true, bool missingpngok=true) {
        string path = fromroot($"exports/ss-{name}.tga");
        if (!missingok && !File.Exists(path))
            throw new Exception($"screenshot doesn't exist: '{path}'");

        File.Delete(path);

        // also kill any png, if requested.
        if (alsopng) {
            string pngpath = fromroot($"exports/ss-{name}.png");
            if (!missingpngok && !File.Exists(pngpath))
                throw new Exception("png screenshot doesn't exist: "
                                 + $"'{pngpath}'");

            File.Delete(pngpath);
        }
    }




    private static Dictionary<int, object> _geezed = new();
    private static int _next = 1; // must leave <=0 as illegal.
    private static int _track(in List<PolyLine> lines, in List<Mesh> meshes) {
        // nothing added during lockdown.
        if (lockdown)
            return BLANK;
        int key;
        lock (_geezed) {
            key = _next++;
            _geezed.Add(key, (lines, meshes));
        }
        return key;
    }
    private static int _track(in Slice<int> group) {
        if (lockdown)
            return BLANK;
        int key;
        lock (_geezed) {
            key = _next++;
            _geezed.Add(key, group);
        }
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
    private const int _MATERIAL_COMPASS_DASHED = int.MinValue + 2;
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
        List<PolyLine> _compass_lines = new();
        _frame_lines(out _, _compass_lines, new Frame(),
                     size: 1f, mark_pos: false);
        foreach (PolyLine line in _compass_lines)
            PICOGK_VIEWER.Add(line, _MATERIAL_COMPASS);

        // basically copy above for a dashed compass also.
        PICOGK_VIEWER.SetGroupMaterial(
            _MATERIAL_COMPASS_DASHED,
            COLOUR_BLACK,
            1f,
            1f
        );
        PICOGK_VIEWER.SetGroupStatic(_MATERIAL_COMPASS_DASHED, true);
        List<PolyLine> _compass_dashed_lines = new();
        _frame_axis_dashed(_compass_dashed_lines, uX3, COLOUR_RED);
        _frame_axis_dashed(_compass_dashed_lines, uY3, COLOUR_GREEN);
        _frame_axis_dashed(_compass_dashed_lines, uZ3, COLOUR_BLUE);
        foreach (PolyLine line in _compass_dashed_lines)
            PICOGK_VIEWER.Add(line, _MATERIAL_COMPASS_DASHED);
    }
    private static void _frame_axis_dashed(List<PolyLine> lines, Vec3 axis,
            Colour col) {
        void add(Vec3 a, Vec3 b) {
            PolyLine l = new(col);
            l.nAddVertex(a);
            l.nAddVertex(b);
            lines.Add(l);
        }
        add(ZERO3, 0.25f*axis);
        add(0.4f*axis, 0.65f*axis);

        Vec3 bottom = 0.8f*axis;
        Vec3 U = nearunit(dot(axis, uY3))
               ? uX3
               : uY3;
        Vec3 V = nearunit(dot(axis, uZ3))
               ? uX3
               : uZ3;
        add(1/3f*(axis + 2f*(bottom + 0.1f*U)), bottom + 0.1f*U);
        add(1/3f*(axis + 2f*(bottom - 0.1f*U)), bottom - 0.1f*U);
        add(1/3f*(axis + 2f*(bottom + 0.1f*V)), bottom + 0.1f*V);
        add(1/3f*(axis + 2f*(bottom - 0.1f*V)), bottom - 0.1f*V);
        add(1/3f*(2f*axis + bottom + 0.1f*U), axis);
        add(1/3f*(2f*axis + bottom - 0.1f*U), axis);
        add(1/3f*(2f*axis + bottom + 0.1f*V), axis);
        add(1/3f*(2f*axis + bottom - 0.1f*V), axis);
        add(bottom - 0.05f*U, bottom + 0.05f*U);
        add(bottom - 0.05f*V, bottom + 0.05f*V);
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



    private static void _remove(int key, bool recursive=true) {
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
        } else if (item is Slice<int> group) {
            if (recursive) {
                foreach (int subkey in group)
                    _remove(subkey);
            }
        } else {
            assert(false);
        }
    }
    public static void _expand(in HashSet<int> keys, int key) {
        object item = _geezed[key];
        if (item is (List<PolyLine>, List<Mesh>)) {
            keys.Add(key);
        } else if (item is Slice<int> group) {
            foreach (int subkey in group)
                _expand(keys, subkey);
        } else {
            assert(false);
        }
    }
    public static void _remove(Slice<int> keys) {
        HashSet<int> newkeys = new(numel(keys));
        foreach (int subkey in keys)
            _expand(newkeys, subkey);
        foreach (int subkey in newkeys)
            _remove(subkey, false /* shouldnt encounter groups */);
    }

    public static void remove(int key) {
        lock (_geezed)
            _remove(key);
    }
    public static void remove(Slice<int> keys) {
        lock (_geezed)
            _remove(keys);
    }

    public static void clear() {
        List<int> keys;
        lock (_geezed) {
            keys = new(_geezed.Keys);
            foreach (int key in keys)
                _remove(key, false);
        }
    }

    public static int group(Slice<int> keys) {
        return _track(keys);
    }

    public static IDisposable temporary() {
        int last;
        lock (_geezed)
            last = _next - 1;
        return Scoped.on_leave(() => {
            lock (_geezed) {
                for (int i = _next - 1; i >= last; --i) {
                    if (_geezed.ContainsKey(i))
                        _remove(i, false);
                }
            }
        });
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
        return Geez.bar(new Bar(bbox), colour: colour);
    }


    private static void _rod_lines(List<PolyLine> lines, in Rod rod, int rings,
            int columns, Colour col) {
        assert(rod.isfilled);
        float r = rod.outer_r;
        float Lz = rod.Lz;
        Frame frame = rod.centre;

        if (rings == -1)
            rings = max(3, (int)(Lz * 2.5f / (TWOPI/columns*r)));
        assert(rings >= 2 || rings == 0);
        assert(columns >= 2 || columns == 0);

        PolyLine l;

        // Rings.
        for (int n=0; n<rings; ++n) {
            float z = lerp(-Lz/2f, +Lz/2f, n, rings);
            l = new(col);
            int N = 100;
            for (int i=0; i<N; ++i) {
                float theta = i*TWOPI/(N - 1);
                l.nAddVertex(frame * fromcyl(r, theta, z));
            }
            lines.Add(l);
        }

        // Columns.
        for (int n=0; n<columns; ++n) {
            float theta = n*TWOPI/columns;
            l = new(col);
            l.Add([
                frame * fromcyl(r, theta, -Lz/2f),
                frame * fromcyl(r, theta, +Lz/2f),
            ]);
            lines.Add(l);
        }
    }

    public static int rod(in Rod rod, Colour? colour=null, int rings=-1,
            int columns=6) {
        if (lockdown)
            return BLANK;

        // Note this is not a particularly efficient vectorisation of a rod but
        // its fine.

        List<PolyLine> lines = new();
        Colour col = colour ?? Geez.colour ?? COLOUR_BLUE;

        _rod_lines(lines, rod.positive, rings, columns, col);

        if (rod.isshelled) {
            _rod_lines(lines, rod.negative, rings, columns, col);

            // Add connections to the columns.
            for (int n=0; n<columns; ++n) {
                float theta = n*TWOPI/columns;
                PolyLine l = new(col);
                l.Add([
                    rod.centre * fromcyl(rod.inner_r, theta, -rod.Lz/2f),
                    rod.centre * fromcyl(rod.outer_r, theta, -rod.Lz/2f),
                ]);
                lines.Add(l);
                l = new(col);
                l.Add([
                    rod.centre * fromcyl(rod.inner_r, theta, +rod.Lz/2f),
                    rod.centre * fromcyl(rod.outer_r, theta, +rod.Lz/2f),
                ]);
                lines.Add(l);
            }
        } else {
            // Add crosses on the end.
            assert((columns%2) == 0);
            for (int n=0; n<columns/2; ++n) {
                float theta = n*TWOPI/columns;
                PolyLine l = new(col);
                l.Add([
                    rod.centre * fromcyl(rod.outer_r, theta, -rod.Lz/2f),
                    rod.centre * fromcyl(rod.outer_r, theta + PI, -rod.Lz/2f),
                ]);
                lines.Add(l);
                l = new(col);
                l.Add([
                    rod.centre * fromcyl(rod.outer_r, theta, +rod.Lz/2f),
                    rod.centre * fromcyl(rod.outer_r, theta + PI, +rod.Lz/2f),
                ]);
                lines.Add(l);
            }
        }

        using (dflt_like(metallic: dflt_line_metallic,
                         roughness: dflt_line_roughness))
            return _push(lines);
    }


    private static void _bar_lines(List<PolyLine> lines, in Bar bar,
            Colour col) {
        Vec3[] corners = bar.get_corners();
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

    public static int bar(in Bar bar, Colour? colour=null, int divide_x=1,
            int divide_y=1, int divide_z=1) {
        if (lockdown)
            return BLANK;

        List<PolyLine> lines = new();
        Colour col = colour ?? Geez.colour ?? COLOUR_BLUE;

        Vec3 div = new(divide_x, divide_y, divide_z);

        if (div == ONE3) {
            // Undivided bar may be shelled.

            _bar_lines(lines, bar.positive, col);
            if (bar.isshelled) {
                _bar_lines(lines, bar.negative, col);
                // Connect corners.
                Vec3[] pos_corners = bar.positive.get_corners();
                Vec3[] neg_corners = bar.negative.get_corners();
                for (int i=0; i<numel(pos_corners); ++i) {
                    PolyLine l = new(col);
                    l.Add([pos_corners[i], neg_corners[i]]);
                    lines.Add(l);
                }
            }
        } else {
            // Divided bar must be filled.
            assert(bar.isfilled, "cannot be shelled and divide");

            Bar divbar = bar.sized(bar.size / div);
            for (int x=0; x<divide_x; ++x)
            for (int y=0; y<divide_y; ++y)
            for (int z=0; z<divide_z; ++z) {
                Vec3 shift = -bar.size/2f
                           + new Vec3(x, y, z) * divbar.size
                           + divbar.size/2f;
                _bar_lines(lines, divbar.translate(shift), col);
            }
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
