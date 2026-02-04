using static br.Br;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Mesh = PicoGK.Mesh;

namespace br {

// i <3 vertices

public static class Polygon {

    public static float area(in Slice<Vec2> vertices, bool signed=false) {
        int N = numel(vertices);
        float A = 0f;
        for (int i=0; i<N; ++i) {
            int j = (i + 1 == N) ? 0 : i + 1;
            Vec2 a = vertices[i];
            Vec2 b = vertices[j];
            A += cross(a, b);
        }
        if (!signed)
            A = abs(A);
        return 0.5f*A;
    }

    public static float perimeter(in Slice<Vec2> vertices) {
        int N = numel(vertices);
        float ell = 0f;
        for (int i=0; i<N; ++i) {
            int j = (i + 1 == N) ? 0 : i + 1;
            Vec2 a = vertices[i];
            Vec2 b = vertices[j];
            ell += mag(b - a);
        }
        return ell;
    }

    public static bool is_simple(in Slice<Vec2> vertices, out string why) {
        int N = numel(vertices);
        if (N < 3) {
            why = "fewer than three vertices";
            return false;
        }

        static string vecstr(Vec2 v) => $"({v.X}, {v.Y})";

        // Reject duplicate points.
        for (int i=0; i<N; ++i) {
            int i1 = (i + 1) % N;
            Vec2 a = vertices[i];
            if (nearto(a, vertices[i1])) {
                why = $"duplicate vertices: {vecstr(a)}";
                return false;
            }
            for (int j=i + 1; j<N; ++j) {
                if (nearto(a, vertices[j])) {
                    why = $"duplicate vertices: {vecstr(a)}";
                    return false;
                }
            }
        }

        float orient(Vec2 a, Vec2 b, Vec2 c) => cross(b - a, c - a);

        bool on_seg(Vec2 p, Vec2 a, Vec2 b)
            => p.X >= min(a.X, b.X)
            && p.X <= max(a.X, b.X)
            && p.Y >= min(a.Y, b.Y)
            && p.Y <= max(a.Y, b.Y);

        // Check segment intersection test.
        for (int i=0; i<N; ++i) {
            int i1 = (i + 1) % N;
            Vec2 a0 = vertices[i];
            Vec2 a1 = vertices[i1];

            for (int j=i + 1; j<N; ++j) {
                int j1 = (j + 1) % N;

                // Skip adjacent edges.
                if (i == j || i1 == j || j1 == i)
                    continue;

                Vec2 b0 = vertices[j];
                Vec2 b1 = vertices[j1];

                float o1 = orient(a0, a1, b0);
                float o2 = orient(a0, a1, b1);
                float o3 = orient(b0, b1, a0);
                float o4 = orient(b0, b1, a1);

                bool o1z = nearzero(o1);
                bool o2z = nearzero(o2);
                bool o3z = nearzero(o3);
                bool o4z = nearzero(o4);

                // Proper intersection.
                if (!o1z && !o2z && !o3z && !o4z
                        && ((o1 > 0f) != (o2 > 0f))
                        && ((o3 > 0f) != (o4 > 0f))) {
                    why = $"self-intersecting: {vecstr(a0)} -> {vecstr(a1)} & "
                                           + $"{vecstr(b0)} -> {vecstr(b1)}";
                    return false;
                }

                // Collinear/touching.
                if ((o1z && on_seg(b0, a0, a1))
                        || (o2z && on_seg(b1, a0, a1))
                        || (o3z && on_seg(a0, b0, b1))
                        || (o4z && on_seg(a1, b0, b1))) {
                    why = $"collinear edges: {vecstr(a0)} -> {vecstr(a1)} & "
                                         + $"{vecstr(b0)} -> {vecstr(b1)}";
                    return false;
                }
            }
        }

        // No flat corners either.
        for (int i=0; i<N; ++i) {
            Vec2 a = vertices[(i - 1 + N) % N];
            Vec2 b = vertices[i];
            Vec2 c = vertices[(i + 1) % N];
            Vec2 e1 = b - a;
            Vec2 e2 = c - b;
            if (nearzero(cross(e1, e2))) { // collinear.
                if (dot(e1, e2) < 0f) { // turn-back.
                    why = $"flat corner: {vecstr(a)} -> {vecstr(b)} -> "
                                     + $"{vecstr(c)}";
                    return false;
                }
            }
        }

        why = "";
        return true;
    }


    public static bool tri_contains(Vec2 p, Vec2 a, Vec2 b, Vec2 c, bool ccw) {
        float ab = cross(b - a, p - a);
        float bc = cross(c - b, p - b);
        float ca = cross(a - c, p - c);
        return ccw
             ? (ab >= 0f && bc >= 0f && ca >= 0f)
             : (ab <= 0f && bc <= 0f && ca <= 0f);
    }


    public static Vec2 closest_on_segment(Vec2 a, Vec2 b, Vec2 p) {
        Vec2 ap = p - a;
        Vec2 ab = b - a;
        float t = clamp(dot(ap, ab) / mag2(ab), 0f, 1f);
        return ab*t;
    }

    public static float dist_to_segment(Vec2 a, Vec2 b, Vec2 p) {
        return mag(p - closest_on_segment(a, b, p));
    }


    public static Vec2 line_intersection(Vec2 a0, Vec2 a1, Vec2 b0, Vec2 b1,
            out bool outside) {
        Vec2 Da = a1 - a0;
        Vec2 Db = b1 - b0;
        float den = cross(Da, Db);
        assert(!nearzero(den));
        float t = cross(b0 - a0, Db) / den;
        outside = within(t, 0f, 1f);
        return a0 + t*Da;
    }

    public static Vec2 corner_normal(Vec2 a, Vec2 b, Vec2 c, float dist) {
        // a -> b -> c.
        // find the point `dist` away from `b` s.t. it is bisecting the angle
        // <abc (measured ccw about b).

        Vec2 ba = a - b;
        Vec2 bc = c - b;
        float beta = arg(bc) - arg(ba);
        beta = wraprad(beta, true); // enforce ccw.
        return b + frompol(dist, arg(ba) + beta/2f);
    }
    public static Vec2 corner_thicken(Vec2 a, Vec2 b, Vec2 c, float th) {
        // a -> b -> c.
        // find the point out from `b` s.t. it is bisecting the angle <abc
        // (measured ccw about b) and s.t. the normal thickness at a and c is
        // `th`.
        Vec2 ba = a - b;
        Vec2 bc = c - b;
        float beta = arg(bc) - arg(ba);
        beta = wraprad(beta, true); // enforce ccw.
        float dist = th / sin(beta / 2f);
        return b + frompol(dist, arg(ba) + beta/2f);
    }


    public static List<Vec2> resample(Slice<Vec2> vertices, int divisions,
            bool closed=false) {
        assert(numel(vertices) >= 2);
        assert(divisions >= 2);

        float[] ell = new float[numel(vertices)]; // of each segment.
        float Ell = 0f;
        for (int i=0; i<numel(vertices); ++i) {
            Vec2 a = vertices[i];
            Vec2 b = vertices[(i + 1) % numel(vertices)];
            ell[i] = mag(a - b);
            assert(!nearzero(ell[i]), "zero length edge");
            Ell += ell[i];
        }
        float interval = closed
                       ? sum(ell) / divisions
                       : (sum(ell) - ell[^1]) / (divisions - 1);

        float accumulated = 0f;
        int seg = 0;
        List<Vec2> resampled = new(divisions);
        for (int i=0; i<divisions; ++i) {
            float target = i*interval;

            while (target > accumulated + ell[seg]) {
                accumulated += ell[seg];
                seg = (seg + 1) % numel(vertices);
            }

            float t = (target - accumulated) / ell[seg];
            Vec2 a = vertices[seg];
            Vec2 b = vertices[(seg + 1) % numel(vertices)];
            resampled.Add(Br.lerp(a, b, t));
        }
        return resampled;
    }


    public static List<Vec2> lerp(List<Vec2> a, List<Vec2> b, float t) {
        int N = numel(a);
        assert(N == numel(b));
        List<Vec2> vertices = new(N);
        for (int i=0; i<N; ++i)
            vertices.Add(Br.lerp(a[i], b[i], t));
        return vertices;
    }


    public static void fillet(List<Vec2> vertices, int i, float FR,
            float prec=1f, int? divisions=null, bool only_this_vertex=false) {
      TRY_AGAIN:;
        int N = numel(vertices);
        assert_idx(i, N);
        int i_n1 = (i - 1 + N) % N;
        int i_p1 = (i + 1) % N;
        assert_idx(i_n1, N);
        assert_idx(i_p1, N);
        Vec2 a = vertices[i_n1];
        Vec2 b = vertices[i];
        Vec2 c = vertices[i_p1];
        float mag_ba = mag(a - b);
        float mag_bc = mag(c - b);
        // Points too close.
        assert(!nearzero(mag_ba));
        assert(!nearzero(mag_bc));
        Vec2 BA = (a - b) / mag_ba;
        Vec2 BC = (c - b) / mag_bc;
        float beta = argbeta(BA, BC);
        // Insanely sharp point.
        assert(!nearzero(beta));
        // Already a straight line.
        if (nearto(abs(beta), PI))
            return;
        // Find the two points which will be joined by a circular arc.
        float ell = FR / tan(0.5f*beta);
        Vec2 d = b + BA*ell;
        // Radius may be too great for these points.
        if (ell > min(mag_ba, mag_bc)) {
            if (only_this_vertex)
                assert(false, "fillet radius too great for this corner");
            // Remove the shorter segment and replace it by extending the segment
            // just before that.
            assert(N >= 4, "too few points to do fillet (or too strange a "
                         + "shape)");
            if (mag_ba <= mag_bc) {
                // extend segment ab.
                int i_n2 = (i - 2 + N) % N;
                assert_idx(i_n2, N);
                Vec2 new_a = line_intersection(vertices[i_n2], a, b, c, out _);
                vertices[i_n1] = new_a;
                vertices.RemoveAt(i);
                i = i_n1;
            } else {
                // extend segment bc.
                int i_p2 = (i + 2 + N) % N;
                assert_idx(i_p2, N);
                Vec2 new_c = line_intersection(vertices[i_p2], c, b, a, out _);
                vertices[i_p1] = new_c;
                vertices.RemoveAt(i);
                // i unchanged.
            }
            // Removal may require another wrap around.
            if (i == numel(vertices))
                i = 0;
            goto TRY_AGAIN;
        }
        // Find the centre of the circular arc.
        float off = FR / sin(0.5f*beta);
        Vec2 centre = b + normalise(BA + BC)*off;
        // Angles of each arc endpoint.
        float theta0 = arg(b - centre);
        float Ltheta = theta0 - arg(d - centre);
        if (abs(Ltheta) > PI)
            Ltheta += (Ltheta < 0) ? TWOPI : -TWOPI;
        theta0 -= Ltheta;
        Ltheta *= 2f;
        // Calc segment count.
        int divs = divisions ?? (int)(abs(Ltheta) / TWOPI * 50f * prec);
        assert(divs > 0);
        // Trace the arc.
        List<Vec2> replace_b = new(divs);
        for (int j=0; j<divs; ++j) {
            float theta = theta0 + j*Ltheta/(divs - 1);
            replace_b.Add(centre + frompol(FR, theta));
        }
        vertices.RemoveAt(i);
        vertices.InsertRange(i, replace_b);
    }


    public static void cull_adjacent_duplicates(List<Vec2> vertices) {
        for (int i=0; i<numel(vertices) - 1; /* nuthin */) {
            Vec2 a = vertices[i];
            Vec2 b = vertices[i + 1];
            if (nearto(a, b))
                vertices.RemoveAt(i);
            else
                ++i;
        }
    }


    public static List<Vec2> circle(int no, float r, float theta0=0f) {
        List<Vec2> points = new();
        for (int i=0; i<no; ++i)
            points.Add(frompol(r, theta0 + i*TWOPI/no));
        return points;
    }
    public static List<Vec2> circle(Slice<int> no, Slice<float> r,
            Slice<float>? theta0=null) {
        if (theta0 == null)
            theta0 = Slice<float>.filled(0f, numel(no));
        assert(numel(no) == numel(r));
        assert(numel(no) == numel(theta0));
        List<Vec2> points = new();
        for (int j=0; j<numel(no); ++j) {
            for (int i=0; i<no[j]; ++i)
                points.Add(frompol(r[j], theta0[j] + i*TWOPI/no[j]));
        }
        return points;

    }

    public static List<Vec3> circle(int no, float r, float theta0, float z)
        // i guess close enough to python list comprehension?
        => circle(no, r, theta0).Select((p) => rejxy(p, z)).ToList();

    public static List<Vec3> circle(Slice<int> no, Slice<float> r,
            Slice<float> theta0, Slice<float> z) {
        int[] N = cumsum(no.toarray());
        int jof(int i) {
            int j = 0;
            while (j < numel(N) && i >= N[j])
                ++j;
            return j;
        }
        return [..circle(no, r, theta0).Select((p, i) => rejxy(p, z[jof(i)]))];
    }



    private static int mesh_divided_into(int count, int by) {
        assert(count > 0);
        assert(by > 0);
        assert(count % by == 0);
        return count / by;
    }

    private static void mesh_face_indices(ref Mesh mesh, in Slice<Vec2> vertices,
            int trioff, bool ccw, bool top) {
        void triangle(ref Mesh mesh, int A, int B, int C) {
            if (ccw != top)
                swap(ref B, ref C);
            mesh.nAddTriangle(trioff + A, trioff + B, trioff + C);
        }

        List<int> I = new(numel(vertices));
        for (int i=0; i<numel(vertices); ++i)
            I.Add(i);
        int remaining = numel(I);
        while (remaining > 3) {
            bool found = false;
            for (int i=0; i<remaining; ++i) {
                int A = I[(i - 1 + remaining) % remaining];
                int B = I[i];
                int C = I[(i + 1) % remaining];

                Vec2 a = vertices[A];
                Vec2 b = vertices[B];
                Vec2 c = vertices[C];

                float triwinding = cross(c - b, a - b);
                if (triwinding == 0f || (triwinding < 0f) == ccw)
                    continue;

                for (int j=0; j<remaining; ++j) {
                    // Only checking points other than a,b,c.
                    int D = I[j];
                    if (D == A || D == B || D == C)
                        continue;
                    Vec2 d = vertices[D];
                    if (tri_contains(d, a, b, c, ccw))
                        goto NEXT;
                }

                triangle(ref mesh, A, B, C);
                I.RemoveAt(i);
                remaining -= 1;
                found = true;
                break;
              NEXT:;
            }
            assert(found, "no ear found?");
        }
        triangle(ref mesh, I[0], I[1], I[2]);
    }


    public static Mesh mesh_revolved(
            in Frame revolve_about,
            in Slice<Vec2> vertices, /* (z,r), any winding */
            float by=TWOPI,
            int slicesize=-1, int slicecount=-1, bool donut=false,
            bool checksimple=true
        ) {
        string why;

        // Clamp to at-most a full revolve.
        by = clamp(by, -TWOPI, TWOPI);
        assert(abs(by) > 0f, "skinnyyyyyy");

        // Closed if a full revolve. Ringed only if donut.
        bool closed = abs(by) == TWOPI;
        bool ringed = donut;

        int tilecount;
        if (slicesize >= 0) {
            int count = mesh_divided_into(numel(vertices), slicesize);
            assert(slicecount == -1 || slicecount == count,
                   $"supplied slicecount doesn't match (got {slicecount}, "
                 + $"expected {count}), consider leaving it inferred?");
            slicecount = count;
            tilecount = 1;
        } else {
            slicesize = numel(vertices);
            tilecount = (slicecount == -1)
                      ? max((int)(abs(by)*200/TWOPI), 10)
                      : slicecount;
            slicecount = 1;
        }
        assert(slicesize >= 3, "each slice is not a polygon");
        assert(slicecount*tilecount >= (closed ? 4 : 3), "too few slices to "
                                                       + "create a solid");

        // Checkme.
        if (checksimple) {
            for (int n=0; n<slicecount; ++n) {
                Slice<Vec2> v = vertices.subslice(n*slicesize, slicesize);
                if (!is_simple(v, out why))
                    assert(false, $"cooked it: {why}");
            }
        }

        // Ensure legal revolve.
        int idx_first_start = 0;
        int idx_first_end = slicesize - 1;
        int idx_last_start = (slicecount - 1)*slicesize;
        int idx_last_end = slicecount*slicesize - 1;
        if (!donut) {
            float z_first_start = vertices[idx_first_start].X;
            float z_last_start  = vertices[idx_last_start].X;
            float z_first_end   = vertices[idx_first_end].X;
            float z_last_end    = vertices[idx_last_end].X;
            assert(nearto(z_first_start, z_last_start),
                    "revolve would not be closed");
            assert(nearto(z_first_end, z_last_end),
                    "revolve would not be closed");

            for (int n=0; n<slicecount; ++n) {
                assert(nearzero(vertices[n*slicesize].Y),
                       "revolve would not be closed");
                assert(nearzero(vertices[n*slicesize + slicesize - 1].Y),
                       "revolve would not be closed");
            }
        }

        // Make the full list of vertices for sweep (trying not to copy if
        // unneeded).
        bool ccw = area(vertices, true) >= 0f;
        Slice<Vec2> swept_vertices = vertices;
        if (!donut) {
            List<Vec2> copy = [..vertices];
            // Set to perfectly along axis.
            for (int n=0; n<slicecount; ++n) {
                copy[n*slicesize] *= uX2;
                copy[n*slicesize + slicesize - 1] *= uX2;
            }
            // Set to join first and last slices' top/bot.
            copy[idx_last_start] = new(
                copy[idx_first_start].X,
                copy[idx_last_start].Y
            );
            copy[idx_last_end] = new(
                copy[idx_first_end].X,
                copy[idx_last_end].Y
            );

            swept_vertices = new(copy);
        }
        if (ccw != (by > 0f)) // ccw when travelling +circum.
            swept_vertices = swept_vertices.reversed();
        swept_vertices = swept_vertices.tiled(tilecount);

        // Get the spinning frame for sweep.
        FramesSpin swept_frames = new FramesSpin(
            slicecount * tilecount,
            revolve_about.cyclecw(),
            about: uX3,
            by: by
        );
        if (closed)
            swept_frames = swept_frames.excluding_end();

        // Forward to sweep to do the heavy lifting.
        return mesh_swept(swept_frames, swept_vertices, closed: closed,
                          ringed: ringed, checksimple: false);
    }



    public static Mesh mesh_extruded(
            Frame bbase,
            float Lz,
            in Slice<Vec2> vertices, /* (x,y), any winding */
            bool at_middle=false,
            float extend_by=NAN, Extend extend_dir=Extend.NONE,
            bool checksimple=true
        ) {
        string why;
        // yeah this could be done as forward to swept but nah.

        int N = numel(vertices);

        if (checksimple) {
            if (!is_simple(vertices, out why))
                assert(false, $"cooked it: {why}");
        }

        bool ccw = area(vertices, true) >= 0f;

        Mesh mesh = new();
        // Mesh bottom and top faces.
        mesh_face_indices(ref mesh, vertices, 0, ccw, false);
        mesh_face_indices(ref mesh, vertices, N, ccw, true);
        // Mesh sides as quads.
        for (int i=0; i<N; ++i) {
            int j = (i == N - 1) ? 0 : i + 1;
            int a0 = i;
            int a1 = j;
            int b0 = i + N;
            int b1 = j + N;
            if (ccw) {
                mesh.nAddTriangle(a0, b1, b0);
                mesh.nAddTriangle(a0, a1, b1);
            } else {
                mesh.nAddTriangle(a0, b0, b1);
                mesh.nAddTriangle(a0, b1, a1);
            }
        }
        // Make all points.
        List<Vec3> V = new();
        float z = at_middle ? -Lz/2f : 0f;
        assert(isnan(extend_by) == (extend_dir == Extend.NONE));
        if (isset((int)extend_dir, (int)Extend.DOWN))
            z -= extend_by;
        if (extend_dir == Extend.UPDOWN)
            extend_by *= 2f;
        Lz += ifnan(extend_by, 0f);
        for (int j=0; j<2; ++j) {
            if (j > 0)
                z += Lz;
            for (int i=0; i<N; ++i)
                V.Add(bbase * rejxy(vertices[i], z));
        }
        mesh.AddVertices(V, out _);
        return mesh;
    }



    public static Mesh mesh_swept(
            in Frames frames,
            in Slice<Vec2> vertices, /* (x,y), winding ccw */
            bool closed=false, bool ringed=true,
            bool checksimple=true
        ) {
        string why;

        int N = numel(vertices);
        int slicecount = numel(frames);
        int slicesize = mesh_divided_into(N, slicecount);
        assert(slicesize >= 3);
        assert(slicecount > 1);

        Mesh mesh = new();

        // Check polys. Note this doesnt check anything about the sides, since
        // thats significantly more complicated (i think?).
        if (checksimple) {
            for (int n=0; n<slicecount; ++n) {
                Slice<Vec2> v = vertices.subslice(n*slicesize, slicesize);
                if (!is_simple(v, out why))
                    assert(false, $"cooked it: {why}");
            }
        }

        // Mesh bottom and top faces.
        if (!closed) {
            Slice<Vec2> bot = vertices.subslice(0, slicesize);
            Slice<Vec2> top = vertices.subslice(N - slicesize, slicesize);
            mesh_face_indices(ref mesh, bot, 0, true, false);
            mesh_face_indices(ref mesh, top, N - slicesize, true, true);
        }

        // Mesh sides as quads.
        for (int n=0; n<slicecount; ++n) {
            if (!closed && n >= slicecount - 1)
                break;

            int i = n*slicesize;
            int j = ((n + 1) % slicecount)*slicesize;

            for (int q0=0; q0<slicesize; ++q0) {
                // not closed if not ringed.
                if (!ringed && q0 >= slicesize - 1)
                    break;
                // Join as quads.
                int q1 = (q0 == slicesize - 1) ? 0 : q0 + 1;
                int a0 = i + q0;
                int a1 = i + q1;
                int b0 = j + q0;
                int b1 = j + q1;
                // Note the seam line of the quad will always be the same
                // direction. switching midway through may cause some vertices to
                // become enclosed when they normally would have been on the
                // surface.
                mesh.nAddTriangle(a0, b1, b0);
                mesh.nAddTriangle(a0, a1, b1);
            }
        }

        // Make all the vertices.
        List<Vec3> V = new();
        for (int n=0; n<slicecount; ++n) {
            Frame frame = frames.at(n);
            for (int i=0; i<slicesize; ++i) {
                int idx = n*slicesize + i;
                V.Add(frame * rejxy(vertices[idx], 0f));
            }
        }
        mesh.AddVertices(V, out _);
        return mesh;
    }
}

}
