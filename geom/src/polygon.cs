using static br.Br;

using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

using Mesh = PicoGK.Mesh;

namespace br {

// i <3 vertices

public static class Polygon {

    public static float area(in List<Vec2> points, bool signed=false) {
        int N = numel(points);
        float A = 0f;
        for (int i=0; i<N; ++i) {
            int j = (i + 1 == N) ? 0 : i + 1;
            Vec2 a = points[i];
            Vec2 b = points[j];
            A += cross(a, b);
        }
        if (!signed)
            A = abs(A);
        return 0.5f*A;
    }

    public static float perimeter(in List<Vec2> points) {
        int N = numel(points);
        float ell = 0f;
        for (int i=0; i<N; ++i) {
            int j = (i + 1 == N) ? 0 : i + 1;
            Vec2 a = points[i];
            Vec2 b = points[j];
            ell += mag(b - a);
        }
        return ell;
    }

    public static bool is_simple(in List<Vec2> points) {
        int N = numel(points);
        if (N < 3)
            return false;

        for (int i=0; i<N; ++i) {
            int i_1 = (i + 1 == N) ? 0 : i + 1;
            Vec2 a0 = points[i];
            Vec2 a1 = points[i_1];

            for (int j=i + 1; j<N; ++j) {
                int j_1 = (j + 1 == N) ? 0 : j + 1;
                Vec2 b0 = points[j];
                Vec2 b1 = points[j_1];

                // Skip adjacent edges.
                if (i == j || i_1 == j || j_1 == i)
                    continue;

                float o1 = cross(a1 - a0, b0 - a0);
                float o2 = cross(a1 - a0, b1 - a0);
                float o3 = cross(b1 - b0, a0 - b0);
                float o4 = cross(b1 - b0, a1 - b0);
                if ((o1*o2 < 0) && (o3*o4 < 0))
                    return false;
            }
        }
        return true;
    }


    public static bool tri_contains(Vec2 p, Vec2 a, Vec2 b, Vec2 c) {
        float ab = cross(b - a, p - a);
        float bc = cross(c - b, p - b);
        float ca = cross(a - c, p - c);
        return ab >= 0f && bc >= 0f && ca >= 0f;
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


    public static List<Vec2> resample(List<Vec2> vertices, int divisions,
            bool closed=false) {
        assert(numel(vertices) >= 2);
        assert(divisions >= 2);
        float Ell = 0f;
        for (int i=1; i<numel(vertices); ++i)
            Ell += mag(vertices[i] - vertices[i - 1]);
        float L_seg;
        if (closed) {
            Ell += mag(vertices[^1] - vertices[0]);
            L_seg = Ell / divisions;
        } else {
            L_seg = Ell / (divisions - 1);
        }
        List<Vec2> new_vertices = new(divisions){vertices[0]};
        float accum = 0f;
        int seg = 1;
        Vec2 prev = vertices[0];
        while (numel(new_vertices) < divisions) {
            if (numel(new_vertices) == divisions - 1 && !closed) {
                new_vertices.Add(vertices[^1]);
                continue;
            }
            Vec2 upto = vertices[seg % numel(vertices)];
            float ell = mag(upto - prev);
            if (accum + ell >= L_seg) {
                float t = (L_seg - accum) / ell;
                new_vertices.Add(prev + t*(upto - prev));
                prev = new_vertices[^1];
                accum = 0f;
            } else {
                seg = (seg + 1) % (closed
                                 ? numel(vertices) + 1
                                 : numel(vertices));
                prev = upto;
                accum += ell;
            }
        }
        return new_vertices;
    }

    public static void fillet(List<Vec2> vertices, int i, float Fr, float prec=1f,
            int? divisions=null, bool only_this_vertex=false) {
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
        float beta = acos(clamp(dot(BA, BC), -1f, 1f));
        // Insanely sharp point.
        assert(!nearzero(beta));
        // Already a straight line.
        if (closeto(abs(beta), PI))
            return;
        // Find the two points which will be joined by a circular arc.
        float ell = Fr / tan(0.5f*beta);
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
        float off = Fr / sin(0.5f*beta);
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
            replace_b.Add(centre + frompol(Fr, theta));
        }
        vertices.RemoveAt(i);
        vertices.InsertRange(i, replace_b);
    }


    private static int mesh_divided_into(int count, int by) {
        assert(count > 0);
        assert(by > 0);
        assert(count % by == 0);
        return count / by;
    }

    private static void mesh_face_indices(ref Mesh mesh, in List<Vec2> vertices,
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
                    if (tri_contains(d, a, b, c))
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
            in Frame frame,
            in List<Vec2> vertices, /* (z,r), any winding (but direction of
                                       travel will be taken s.t. winding is cw
                                       about it) */
            bool donut=false, float theta0=0f, int slicesize=-1,
            int slicecount=-1
        ) {
        bool sliced = slicesize >= 0;

        int repcount;
        if (sliced) {
            assert(slicecount == -1, "slicecount must be inferred");
            slicecount = mesh_divided_into(numel(vertices), slicesize);
            repcount = 1;
        } else {
            slicesize = numel(vertices);
            repcount = (slicecount == -1) ? 200 : slicecount;
            slicecount = 1;
        }
        int slicestep = slicesize;
        if (!donut) {
            assert(slicesize >= 2);
            slicesize += 2;
        }
        assert(slicesize >= 3);
        assert(slicecount*repcount >= 3);

        // Make the full list of vertices for sweep.
        List<Vec2> slice_vertices;
        if (donut) {
            slice_vertices = vertices;
        } else {
            slice_vertices = new(slicecount * slicesize);
            for (int n=0; n<slicecount; ++n) {
                Vec2 start = new(vertices[n*slicestep].X, 0f);
                Vec2 end   = new(vertices[n*slicestep + slicestep - 1].X, 0f);
                slice_vertices.AddRange([
                    start,
                    ..vertices.GetRange(n*slicestep, slicestep),
                    end,
                ]);
            }
        }
        List<Vec2> swept_vertices;
        if (repcount == 1) {
            swept_vertices = slice_vertices;
        } else {
            swept_vertices = new(repcount * slicesize);
            for (int n=0; n<repcount; ++n)
                swept_vertices.AddRange(slice_vertices);
        }

        // Make the full list of frames for sweep.
        bool ccw = area(swept_vertices.GetRange(0, slicesize), true) >= 0f;
        List<Frame> swept_frames = new(slicecount * repcount);
        for (int n=0; n<slicecount*repcount; ++n) {
            float theta = theta0
                        + (ccw ? 1f : -1f) * n*TWOPI/slicecount/repcount;
            swept_frames.Add(new(
                frame.pos,
                uZ3,
                fromcyl(1f, theta + PI_2, 0f)
            ));
        }

        return mesh_swept(swept_frames, swept_vertices, false);
    }



    public static Mesh mesh_extruded(
            Frame frame,
            float Lz,
            in List<Vec2> vertices, /* (x,y), any winding */
            bool at_middle=false, float extend_by=0f,
            int extend_direction=EXTEND_UPDOWN
        ) {
        // yeah this could be done as forward to swept but nah.

        assert(is_simple(vertices), "cooked it");
        bool ccw = area(vertices, true) >= 0f;
        int N = numel(vertices);

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
        float[] z = at_middle ? [-Lz/2f, +Lz/2f] : [0f, Lz];
        z[1] += extend_by;
        float Dz = 0f;
        switch (extend_direction) {
            case EXTEND_DOWN: Dz = -extend_by; break;
            case EXTEND_UPDOWN: Dz = -extend_by/2f; break;
        }
        z[0] += Dz;
        z[1] += Dz;
        for (int j=0; j<2; ++j)
        for (int i=0; i<N; ++i) {
            V.Add(frame * rejxy(vertices[i], z[j]));
        }
        mesh.AddVertices(V, out _);
        return mesh;
    }



    public static Mesh mesh_swept(
            in List<Frame> frames,
            in List<Vec2> vertices, /* (x,y), winding ccw */
            bool closed=true
        ) {

        int N = numel(vertices);
        int slicecount = numel(frames);
        int slicesize = mesh_divided_into(N, slicecount);
        assert(slicesize >= 3);
        assert(slicecount > 1);

        Mesh mesh = new();
        assert(is_simple(vertices.GetRange(0, slicesize)), "come on man");

        // Mesh bottom and top faces.
        if (closed) {
            List<Vec2> bot = vertices.GetRange(0, slicesize);
            List<Vec2> top = vertices.GetRange(N - slicesize, slicesize);
            mesh_face_indices(ref mesh, bot, 0, true, false);
            mesh_face_indices(ref mesh, top, N - slicesize, true, true);
        }

        // Mesh sides as quads.
        for (int n=0; n<slicecount; ++n) {
            if (closed && n >= slicecount - 1)
                break;

            int i = n*slicesize;
            int j = ((n + 1) % slicecount)*slicesize;

            if (n < slicecount - 1) {
                List<Vec2> V1 = vertices.GetRange(j, slicesize);
                assert(is_simple(V1), "cooked it");
            }

            for (int q0=0; q0<slicesize; ++q0) {
                // Join as quads.
                int q1 = (q0 == slicesize - 1) ? 0 : q0 + 1;
                int a0 = i + q0;
                int a1 = i + q1;
                int b0 = j + q0;
                int b1 = j + q1;
                mesh.nAddTriangle(a0, b1, b0);
                mesh.nAddTriangle(a0, a1, b1);
            }
        }

        // Make all the vertices.
        List<Vec3> V = new();
        for (int n=0; n<slicecount; ++n) {
            Frame frame = frames[n];
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
