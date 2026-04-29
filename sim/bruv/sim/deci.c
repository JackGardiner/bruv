#include "br.h"

#include "assertion.h"
#include "maths.h"
#include "hash.h"




enum { TIMER_STRING_SIZE = 2+1+2+1+2+1+1 };
static void timer_string(char* rstr buf, f64 seconds) {
    i32 sec = (i32)seconds;
    i32 h = sec / 3600;
    i32 m = (sec % 3600) / 60;
    i32 s = sec % 60;
    i32 ms = (i32)(seconds * 1e3) % 1000;
    if (h > 99) {
        h = 99;
        m = 59;
        s = 59;
        ms = 999;
    }
    sprintf(buf, "%02d:%02d:%02d.%01d", h, m, s, ms / 100);
}

#ifndef __WIN32__
static void timer_init(void) { /* nothing */ }
static f64 timer_now(void) {
    struct timespec ts;
    timespec_get(&ts, TIME_UTC);
    return (f64)ts.tv_sec + 1e-9 * (f64)ts.tv_nsec;
}
#else
// windows function expose without the import cause fuck that.
__declspec(dllimport) i32 __stdcall QueryPerformanceCounter(i64* count);
__declspec(dllimport) i32 __stdcall QueryPerformanceFrequency(i64* freq);
static f64 timer_frequency_;
static void timer_init(void) {
    i64 freq;
    assert(QueryPerformanceFrequency(&freq), "failed");
    timer_frequency_ = (f64)freq;
}
static f64 timer_now(void) {
    i64 count;
    assert(QueryPerformanceCounter(&count), "failed");
    return (f64)count / timer_frequency_;
}
#endif




typedef struct ProgressBar {
    f64 start_time;
    i64 total;
    i64 freq;
    i64 last;
} ProgressBar;

enum { PROGRESS_BAR_WIDTH = 20 };

static void progressbar_init(ProgressBar* pb, i64 total, i64 touchpoints) {
    pb->start_time = timer_now();
    pb->total = total;
    pb->freq = max(1, (i64)(total / (f64)touchpoints));
    pb->last = -1;
}

static void progressbar_update(ProgressBar* pb, i64 curr) {
    if ((curr % pb->freq) != 0)
        return;
    if (curr == pb->last)
        return;
    pb->last = curr;
    assert(curr <= pb->total, "progress bar overflow");

    f64 elapsed = timer_now() - pb->start_time;
    f64 percent = (f64)curr / pb->total;
    i32 pos = (i32)(PROGRESS_BAR_WIDTH * percent);

    // Calculate ETA.
    f64 eta = 0.0;
    if (curr > 0) {
        f64 rate = (elapsed > 0.0) ? curr / elapsed : +INF;
        eta = (rate > 0.0) ? pb->total / rate : +INF;
    }

    char elapsed_str[TIMER_STRING_SIZE + 1];
    char eta_str[TIMER_STRING_SIZE + 1];
    timer_string(elapsed_str, elapsed);
    timer_string(eta_str, eta);

    // Print the bar.
    printf("\r[");
    for (i32 i=0; i<PROGRESS_BAR_WIDTH; ++i) {
        if (i < pos)
            putchar('=');
        else if (i == pos)
            putchar('>');
        else
            putchar(' ');
    }

    printf("] %5.1f%% | elapsed %s | eta %s", 100.0*percent, elapsed_str,
            eta_str);
    fflush(stdout);
}

static void progressbar_finish(ProgressBar* pb) {
    f64 elapsed = timer_now() - pb->start_time;
    char elapsed_str[TIMER_STRING_SIZE + 1];
    timer_string(elapsed_str, elapsed);

    printf("\r[");
    for (i32 i=0; i<PROGRESS_BAR_WIDTH; ++i)
        putchar('=');

    printf("] 100.0%% | elapsed %s           ", elapsed_str);
    for (i32 i=0; i<TIMER_STRING_SIZE; ++i)
        putchar(' ');

    printf("\n");
    fflush(stdout);
}



#define DECI_DO_TELEMETRY 1

typedef struct ZoneCounter {
    f64 total_time;
    i64 hit_count;
    const char* label;
} ZoneCounter;

enum {
    ZONE_DECIMATE,
    ZONE_PROGRESS_BAR,
    ZONE_ADJ_REFRESH,
    ZONE_HEAP_POP,
    ZONE_OPTIMAL_COLLAPSE,
    ZONE_COLLAPSE_WOULD_TEAR,
    ZONE_COLLAPSE_WOULD_FLIP,
    ZONE_COLLAPSE,
    ZONE_TRI_UPDATE,
    ZONE_TRI_SHIFT,
    ZONE_ADJ_APPEND,
    ZONE_NEIGHBOUR_FIND,
    ZONE_NEIGHBOUR_RECOMPUTE,
    ZONE_COUNT
};
static ZoneCounter telemetry[ZONE_COUNT] = {
    [ZONE_DECIMATE]             = {0.0, 0, "decimate"},
    [ZONE_PROGRESS_BAR]         = {0.0, 0, "  progress bar"},
    [ZONE_ADJ_REFRESH]          = {0.0, 0, "  adj refresh"},
    [ZONE_HEAP_POP]             = {0.0, 0, "  heap pop"},
    [ZONE_OPTIMAL_COLLAPSE]     = {0.0, 0, "  optimal collapse"},
    [ZONE_COLLAPSE_WOULD_TEAR]  = {0.0, 0, "  collapse would tear"},
    [ZONE_COLLAPSE_WOULD_FLIP]  = {0.0, 0, "  collapse would flip"},
    [ZONE_COLLAPSE]             = {0.0, 0, "  collapse"},
    [ZONE_TRI_UPDATE]           = {0.0, 0, "  tri update"},
    [ZONE_TRI_SHIFT]            = {0.0, 0, "    tri shift"},
    [ZONE_ADJ_APPEND]           = {0.0, 0, "  adj append"},
    [ZONE_NEIGHBOUR_FIND]       = {0.0, 0, "  neighbour find"},
    [ZONE_NEIGHBOUR_RECOMPUTE]  = {0.0, 0, "  neighbour recompute"},
};

#if DECI_DO_TELEMETRY
#define TIME_ZONE(zone)                                         \
    for (f64 _start=timer_now(), _once = 1;                     \
         _once;                                                 \
         telemetry[(zone)].total_time += timer_now()-_start,    \
         telemetry[(zone)].hit_count++,                         \
         _once = 0)
#else
#define TIME_ZONE(zone) for (i32 _once=1; _once; _once=0)
#endif

static void report_telemetry(void) {
  #if !DECI_DO_TELEMETRY
    return;
  #endif

    printf("\n--- performance report ---\n");
    printf("%-22s | %s | %s | %s\n", "zone", "total [s]", "ave. [us]", "share");
    printf("-------------------------------------------------------\n");

    f64 grand_total = telemetry[ZONE_DECIMATE].total_time;
    for (i32 i=0; i<ZONE_COUNT; ++i) {
        ZoneCounter* z = telemetry + i;
        f64 avg_ms = (z->total_time / z->hit_count) * 1.0e6;
        f64 share = (z->total_time / grand_total) * 100.0;

        if (z->hit_count > 1) {
            printf("%-22s | %9.4f | %9.4f | %5.1f%%\n", z->label, z->total_time,
                    avg_ms, share);
        } else {
            printf("%-22s | %9.4f |         - | %5.1f%%\n", z->label,
                    z->total_time, share);
        }
    }
}



static void print_numba(i64 value, i32 use_binary) {
    const char* suffixes[] = {"", "k", "M", "G", "T"};
    f64 base = use_binary ? 1024.0 : 1000.0;
    f64 val = (f64)value;
    i32 i = 0;
    while (val >= base && i < numel(suffixes) - 1) {
        val /= base;
        i++;
    }


    /*
       xxxx
          x.xx kB
         xx.x  kB
        xxx.x  kB
       xxxx.   kB
     */
    if (i == 0)
        printf("%4lld", value);
    else if (val < 10.0)
        printf("   %.2f %s", val, suffixes[i]);
    else if (val < 100.0)
        printf("  %.1f  %s", val, suffixes[i]);
    else if (val < 1000.0)
        printf(" %.1f  %s", val, suffixes[i]);
    else
        printf("%.0f.   %s", val, suffixes[i]);
    if (use_binary)
        printf("B");
}

static i32 is_space(char c) {
    return c == ' '
        || c == '\t'
        || c == '\n'
        || c == '\r'
        || c == '\v'
        || c == '\f';
}
static i32 is_digit(char c) {
    return '0' <= c && c <= '9';
}

static const char* subparse_i64(const char* s, i64* out) {
    *out = 0;

    if (!s || *s == '\0')
        return NULL;

    i32 has_digits = 0;
    i32 sign = 1;
    if (*s == '-') {
        sign = -1;
        ++s;
    } else if (*s == '+') {
        ++s;
    }
    for (; *s != '\0' && is_digit(*s); ++s) {
        i32 digit = *s - '0';
        if (mul_overflow(*out, *out, 10))
            return NULL;
        *out *= 10;
        if (add_overflow(*out, *out, digit))
            return NULL;
        *out += digit;
        has_digits = 1;
    }

    if (!has_digits)
        return NULL;

    if (mul_overflow(*out, *out, sign))
        return 0;
    *out *= sign;

    return s;
}

static f64 parse_f64(const char* s) {
    if (!s || *s == '\0')
        return NAN;

    while (is_space(*s))
        ++s;

    i32 has_digits = 0;
    i32 has_dot = 0;

    f64 value = 0.0;
    f64 divisor = 1.0;
    f64 sci = 1.0;

    for (; *s != '\0' && !is_space(*s); ++s) {
        if (is_digit(*s)) {
            i32 digit = *s - '0';
            if (!has_dot) {
                value *= 10.0;
                value += digit;
            } else {
                divisor *= 10.0;
                value += digit / divisor;
            }
            has_digits = 1;
        } else if (*s == '.') {
            if (has_dot)
                return NAN;
            has_dot = 1;
        } else if (*s == 'e' || *s == 'E') {
            i64 exp;
            s = subparse_i64(s + 1, &exp);
            if (!s)
                return NAN;
            sci = pow(10.0, exp);
            break;
        } else
            return NAN;
    }
    while (is_space(*s))
        ++s;
    if (*s != '\0')
        return NAN;

    if (!has_digits)
        return NAN;

    value *= sci;

    // allow inf returns.
    return value;
}





// 4x4 symmetric matrix:
//  [ q0  q1  q2  q3  ]
//  [     q4  q5  q6  ]
//  [         q7  q8  ]
//  [             q9  ]
typedef f64 Quadric[10];

// Is active?
#define DEAD ((u32)0x1)
// In current neighbours set?
#define IN_NEIGHBOURS ((u32)0x2)

typedef struct __attribute__((__aligned__(16))) Vertex {
    // Coordinates.
    union {
        /* coord-wise: */
        struct {
            f32 x;
            f32 y;
            f32 z;
        };
        /* arrayed: */
        f32 v[3];
    };
    // DEAD and IN_NEIGHBOURS flags.
    u32 flags;
    // Quadric.
    Quadric q;
} Vertex;


typedef struct __attribute__((__aligned__(16))) Tri {
    // Three vertex indices.
    union {
        /* vertex-wise: */
        struct {
            i32 a;
            i32 b;
            i32 c;
        };
        /* arrayed: */
        i32 i[3];
    };
    // Is active?
    i32 dead;
} Tri;



typedef f64 mat3[9]; // row-major 3x3 matrix.

static f64 det3(mat3 m) {
    return m[0] * (m[4]*m[8] - m[5]*m[7])
         - m[1] * (m[3]*m[8] - m[5]*m[6])
         + m[2] * (m[3]*m[7] - m[4]*m[6]);
}

static void solve3x3(mat3 A, f64 B[rstr 3], f64 out[rstr 3]) {
    f64 det = det3(A);
    if (abs(det) < 1e-12) {
        out[0] = NAN;
        out[1] = NAN;
        out[2] = NAN;
        return;
    }
    mat3 Ax = {
        B[0], A[1], A[2],
        B[1], A[4], A[5],
        B[2], A[7], A[8],
    };
    mat3 Ay = {
        A[0], B[0], A[2],
        A[3], B[1], A[5],
        A[6], B[2], A[8],
    };
    mat3 Az = {
        A[0], A[1], B[0],
        A[3], A[4], B[1],
        A[6], A[7], B[2],
    };
    out[0] = det3(Ax)/det;
    out[1] = det3(Ay)/det;
    out[2] = det3(Az)/det;
}

static void compute_quadric(Vertex* V, Tri* t) {
    f64 a[3] = {
        V[t->a].x,
        V[t->a].y,
        V[t->a].z,
    };
    f64 b[3] = {
        V[t->b].x,
        V[t->b].y,
        V[t->b].z,
    };
    f64 c[3] = {
        V[t->c].x,
        V[t->c].y,
        V[t->c].z,
    };

    f64 n[3] = {
        (b[1] - a[1]) * (c[2] - a[2]) - (b[2] - a[2]) * (c[1] - a[1]),
        (b[2] - a[2]) * (c[0] - a[0]) - (b[0] - a[0]) * (c[2] - a[2]),
        (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]),
    };
    f64 nn = n[0]*n[0] + n[1]*n[1] + n[2]*n[2];
    if (abs(nn) < 1e-14)
        return; // leave quadric unchanged.
    f64 nmag = sqrt(nn);
    n[0] /= nmag;
    n[1] /= nmag;
    n[2] /= nmag;

    f64 d = -(n[0]*a[0] + n[1]*a[1] + n[2]*a[2]);

    Quadric q;
    q[0] = (f64)(n[0]*n[0]);
    q[1] = (f64)(n[0]*n[1]);
    q[2] = (f64)(n[0]*n[2]);
    q[3] = (f64)(n[0]*d);

    q[4] = (f64)(n[1]*n[1]);
    q[5] = (f64)(n[1]*n[2]);
    q[6] = (f64)(n[1]*d);

    q[7] = (f64)(n[2]*n[2]);
    q[8] = (f64)(n[2]*d);

    // TODO: address the more general issue of this term being large and
    // potentially precision-capping the rest.
    q[9] = (f64)(   d*d);

    for (i32 i=0; i<numel(Quadric); ++i)
        V[t->a].q[i] += q[i];
    for (i32 i=0; i<numel(Quadric); ++i)
        V[t->b].q[i] += q[i];
    for (i32 i=0; i<numel(Quadric); ++i)
        V[t->c].q[i] += q[i];
}

static f64 quadric_cost(Quadric q, f64 v[3]) {
    f64 c = q[0]*v[0]*v[0] + 2*q[1]*v[0]*v[1] + 2*q[2]*v[0]*v[2] + 2*q[3]*v[0]
                           +   q[4]*v[1]*v[1] + 2*q[5]*v[1]*v[2] + 2*q[6]*v[1]
                                              +   q[7]*v[2]*v[2] + 2*q[8]*v[2]
                                                                 +   q[9];
    return max(0.0, c);
}

static f64 optimal_collapse(const Vertex* V, i32 n, i32 m, f32 vbar[rstr 3]) {
    // Find resultant quadric.
    Quadric q;
    for (i32 i=0; i<numel(Quadric); ++i)
        q[i] = V[n].q[i] + V[m].q[i];

    // Build the system and solve for optimal vbar.
    mat3 A = {
        q[0], q[1], q[2],
        q[1], q[4], q[5],
        q[2], q[5], q[7],
    };
    f64 B[3] = {
        -q[3], -q[6], -q[8],
    };
    f64 v[3];
    solve3x3(A, B, v);

    // If matrix is singular, pick the best of a few options.
    if (isnan(v[0]) || isnan(v[1]) || isnan(v[2])) {
        f64 v0[3] = {
            V[n].x,
            V[n].y,
            V[n].z,
        };
        f64 v1[3] = {
            0.5*V[n].x + 0.5*V[m].x,
            0.5*V[n].y + 0.5*V[m].y,
            0.5*V[n].z + 0.5*V[m].z,
        };
        f64 v2[3] = {
            V[m].x,
            V[m].y,
            V[m].z,
        };
        f64 c0 = quadric_cost(q, v0);
        f64 c1 = quadric_cost(q, v1);
        f64 c2 = quadric_cost(q, v2);
        if (c0 < min(c1, c2)) {
            vbar[0] = v0[0];
            vbar[1] = v0[1];
            vbar[2] = v0[2];
            return c0;
        }
        if (c2 < min(c0, c1)) {
            vbar[0] = v2[0];
            vbar[1] = v2[1];
            vbar[2] = v2[2];
            return c2;
        }
        vbar[0] = v1[0];
        vbar[1] = v1[1];
        vbar[2] = v1[2];
        return c1;
    }

    // Evaluate the quadric error at vbar.
    vbar[0] = v[0];
    vbar[1] = v[1];
    vbar[2] = v[2];
    f64 c = quadric_cost(q, v);
    return c;
}






typedef struct SEdge {
    // 10 bits for robin hood secret.
    // 27 bits each for n and m.
    u64 v;
} SEdge;
enum {
    SEDGE_COUNT_SECRET = 10,
    SEDGE_COUNT_N = 27,
    SEDGE_COUNT_M = 27,

    SEDGE_OFF_SECRET = 0,
    SEDGE_OFF_N = SEDGE_COUNT_SECRET,
    SEDGE_OFF_M = SEDGE_OFF_N + SEDGE_COUNT_N,
};

static i32 sedge_secret(const SEdge* edge) {
    return (edge->v >> SEDGE_OFF_SECRET) & lobits(SEDGE_COUNT_SECRET);
}
static i32 sedge_n(const SEdge* edge) {
    return (edge->v >> SEDGE_OFF_N) & lobits(SEDGE_COUNT_N);
}
static i32 sedge_m(const SEdge* edge) {
    return (edge->v >> SEDGE_OFF_M) & lobits(SEDGE_COUNT_M);
}

static void sedge_set_secret(SEdge* edge, i32 secret) {
    assert(secret <= lobits(SEDGE_COUNT_SECRET), "oob");
    edge->v &= ~(lobits(SEDGE_COUNT_SECRET) << SEDGE_OFF_SECRET);
    edge->v |= ((u64)secret << SEDGE_OFF_SECRET);
}
static void sedge_set_n(SEdge* edge, i32 n) {
    assert(n <= lobits(SEDGE_COUNT_N), "oob");
    edge->v &= ~(lobits(SEDGE_COUNT_N) << SEDGE_OFF_N);
    edge->v |= ((u64)n << SEDGE_OFF_N);
}
static void sedge_set_m(SEdge* edge, i32 m) {
    assert(m <= lobits(SEDGE_COUNT_M), "oob");
    edge->v &= ~(lobits(SEDGE_COUNT_M) << SEDGE_OFF_M);
    edge->v |= ((u64)m << SEDGE_OFF_M);
}

static u64 sedge_hash(i32 n, i32 m) {
    return hash_u64((u64)n | ((u64)m << 32));
}

typedef struct EdgeSet {
    // Robin hood hashtable.
    SEdge* bkts;
    i64 cap;
    i64 count;
} EdgeSet;


static void edgeset_init(EdgeSet* s, i64 cap, i64* rstr out_size) {
    assert(ispow2(cap), "invalid capacity");
    i64 size = sizeof(SEdge) * cap;
    s->bkts = malloc(size);
    assert(s->bkts, "allocation failure");
    memset(s->bkts, 0, size);
    s->cap = cap;
    s->count = 0;

    if (out_size)
        *out_size = size;
}

static void edgeset_free(EdgeSet* s) {
    free(s->bkts);
    s->bkts = NULL;
    s->cap = 0;
    s->count = 0;
}

UNUSED static void edgeset_clear(EdgeSet* s) {
    i64 size = sizeof(SEdge) * s->cap;
    memset(s->bkts, 0, size);
    s->count = 0;
}

static i32 edgeset_get(const EdgeSet* s, i64 i, i32* rstr n, i32* rstr m) {
    SEdge* edge = s->bkts + i;
    if (sedge_secret(edge) == 0)
        return 0;
    *n = sedge_n(edge);
    *m = sedge_m(edge);
    return 1;
}

static i64 edgeset_find(const EdgeSet* s, i32 n, i32 m) {
    u64 key_hash = sedge_hash(n, m);
    i64 idx = key_hash & (s->cap - 1);
    i32 dist = 1;

    while (dist <= sedge_secret(s->bkts + idx)) {
        if (sedge_n(s->bkts + idx) == n && sedge_m(s->bkts + idx) == m)
            return idx;
        ++dist;
        idx = (idx + 1) & (s->cap - 1);
    }
    return -1;
}

static void edgeset_add(EdgeSet* s, i32 n, i32 m) {
    assert(s->count * (i64)8 < s->cap * (i64)7, "too full");
    SEdge* bkt = &(SEdge){0};

    sedge_set_secret(bkt, 1);
    sedge_set_n(bkt, n);
    sedge_set_m(bkt, m);

    i64 idx = sedge_hash(n, m) & (s->cap - 1);

    while (sedge_secret(s->bkts + idx)) {
        if (sedge_secret(bkt) > sedge_secret(s->bkts + idx))
            swap(*bkt, s->bkts[idx]);

        i32 dist = sedge_secret(bkt);
        assert(dist < lobits(SEDGE_COUNT_SECRET) - 1, "too full");
        sedge_set_secret(bkt, dist + 1);
        idx = (idx + 1) & (s->cap - 1);
    }

    s->bkts[idx] = *bkt;
    ++s->count;
}

UNUSED static void edgeset_remove(EdgeSet* s, i64 idx) {
    assert(s->count > 0, "empty");
    // Robin hood backward shift deletion.
    for (;;) {
        i64 next = (idx + 1) & (s->cap - 1);

        if (sedge_secret(s->bkts + next) <= 1) {
            sedge_set_secret(s->bkts + idx, 0);
            break;
        }

        s->bkts[idx] = s->bkts[next];
        i32 dist = sedge_secret(s->bkts + idx);
        sedge_set_secret(s->bkts + idx, dist - 1);

        idx = next;
    }
    --s->count;
}




static i32 count_edges(const Tri* T, i32 Tcount) {
    EdgeSet* edgeset = &(EdgeSet){0};
    edgeset_init(edgeset, uptopow2((i64)4*Tcount), NULL);

    i32 Ecount = 0;

    ProgressBar* pb = &(ProgressBar){0};
    progressbar_init(pb, Tcount, 10);
    for (i32 t=0; t<Tcount; ++t) {
        progressbar_update(pb, t);
        if (T[t].dead)
            continue;
        for (i32 i=0; i<3; ++i) {
            i32 j = (i + 1) % 3;
            i32 n = min(T[t].i[i], T[t].i[j]);
            i32 m = max(T[t].i[i], T[t].i[j]);

            if (edgeset_find(edgeset, n, m) >= 0)
                continue;
            edgeset_add(edgeset, n, m);
            ++Ecount;
        }
    }
    progressbar_finish(pb);

    edgeset_free(edgeset);
    return Ecount;
}






static i32 is_closed_mani(const Vertex* V, const Tri* T, i32 Tcount,
        i32 Ecount) {
    (void)V;

    EdgeSet* edgeset0 = &(EdgeSet){0};
    EdgeSet* edgeset1 = &(EdgeSet){0};
    edgeset_init(edgeset0, uptopow2(Ecount + Ecount/2), NULL);
    edgeset_init(edgeset1, uptopow2(Ecount + Ecount/2), NULL);

    ProgressBar* pb = &(ProgressBar){0};
    progressbar_init(pb, Tcount + edgeset0->cap, 10);
    for (i32 t=0; t<Tcount; ++t) {
        progressbar_update(pb, t);
        if (T[t].dead)
            continue;
        for (i32 i=0; i<3; ++i) {
            i32 j = (i + 1) % 3;
            i32 n = min(T[t].i[i], T[t].i[j]);
            i32 m = max(T[t].i[i], T[t].i[j]);

            if (edgeset_find(edgeset0, n, m) < 0) {
                edgeset_add(edgeset0, n, m);
                continue;
            }
            if (edgeset_find(edgeset1, n, m) < 0) {
                edgeset_add(edgeset1, n, m);
                continue;
            }
            edgeset_free(edgeset0);
            edgeset_free(edgeset1);
            return 0; // non-manifold.
        }
    }

    for (i64 i=0; i<edgeset0->cap; ++i) {
        progressbar_update(pb, Tcount + i);
        i32 n, m;
        if (!edgeset_get(edgeset0, i, &n, &m))
            continue;
        if (edgeset_find(edgeset1, n, m) >= 0)
            continue;
        edgeset_free(edgeset0);
        edgeset_free(edgeset1);
        return 0; // non-watertight.
    }

    progressbar_finish(pb);

    edgeset_free(edgeset0);
    edgeset_free(edgeset1);
    return 1;
}








typedef struct Neighbours {
    i32* v;
    i32 cap;
    i32 count;
} Neighbours;

static void neighbours_init(Neighbours* n, i32 cap, i64* rstr out_size) {
    i64 size = sizeof(*n->v) * cap;
    n->v = malloc(size);
    assert(n->v, "allocation failure");
    n->cap = cap;
    n->count = 0;

    if (out_size)
        *out_size = size;
}

static void neighbours_push(Neighbours* n, Vertex* V, i32 v) {
    assert(n->count < n->cap, "full neighbours");
    if (V[v].flags & IN_NEIGHBOURS)
        return;
    V[v].flags |= IN_NEIGHBOURS;
    n->v[n->count++] = v;
}

static i32 neighbours_pop(Neighbours* n, Vertex* V) {
    assert(n->count > 0, "empty neighbours");
    i32 v = n->v[--n->count];
    V[v].flags &= ~IN_NEIGHBOURS;
    return v;
}





typedef struct HIndex {
    // 10 bits for robin hood secret.
    // 27 bits each for n and m.
    u32 a;
    u32 b;
    i32 idx;
} HIndex;
enum {
    HINDEX_COUNT_SECRET = 10,
    HINDEX_COUNT_N = 27,
    HINDEX_COUNT_M = 27,

    HINDEX_OFF_SECRET = 0,
    HINDEX_OFF_N = HINDEX_COUNT_SECRET,
    HINDEX_OFF_M = HINDEX_OFF_N + HINDEX_COUNT_N,
};

static i32 hindex_secret(const HIndex* index) {
    u64 v = index->a | ((u64)index->b << 32);
    return (v >> HINDEX_OFF_SECRET) & lobits(HINDEX_COUNT_SECRET);
}
static i32 hindex_n(const HIndex* index) {
    u64 v = index->a | ((u64)index->b << 32);
    return (v >> HINDEX_OFF_N) & lobits(HINDEX_COUNT_N);
}
static i32 hindex_m(const HIndex* index) {
    u64 v = index->a | ((u64)index->b << 32);
    return (v >> HINDEX_OFF_M) & lobits(HINDEX_COUNT_M);
}

static void hindex_set_secret(HIndex* index, i32 secret) {
    assert(secret <= lobits(HINDEX_COUNT_SECRET), "oob");
    u64 v = index->a | ((u64)index->b << 32);
    v &= ~(lobits(HINDEX_COUNT_SECRET) << HINDEX_OFF_SECRET);
    v |= ((u64)secret << HINDEX_OFF_SECRET);
    index->a = (u32)v;
    index->b = (u32)(v >> 32);
}
static void hindex_set_n(HIndex* index, i32 n) {
    assert(n <= lobits(HINDEX_COUNT_N), "oob");
    u64 v = index->a | ((u64)index->b << 32);
    v &= ~(lobits(HINDEX_COUNT_N) << HINDEX_OFF_N);
    v |= ((u64)n << HINDEX_OFF_N);
    index->a = (u32)v;
    index->b = (u32)(v >> 32);
}
static void hindex_set_m(HIndex* index, i32 m) {
    assert(m <= lobits(HINDEX_COUNT_M), "oob");
    u64 v = index->a | ((u64)index->b << 32);
    v &= ~(lobits(HINDEX_COUNT_M) << HINDEX_OFF_M);
    v |= ((u64)m << HINDEX_OFF_M);
    index->a = (u32)v;
    index->b = (u32)(v >> 32);
}

static u64 hindex_hash(i32 n, i32 m) {
    return hash_u64((u32)n | ((u64)m << 32));
}

typedef struct HeapqIndexer {
    HIndex* bkts;
    i32 cap;
    i32 count;
} HeapqIndexer;


static void indexer_init(HeapqIndexer* i, i32 cap, i64* rstr out_size) {
    assert(ispow2(cap), "invalid capacity");
    i64 size = sizeof(HIndex) * cap;
    i->bkts = malloc(size);
    assert(i->bkts, "allocation failure");
    memset(i->bkts, 0, size);
    i->cap = cap;
    i->count = 0;

    if (out_size)
        *out_size = size;
}

static i32 indexer_find(const HeapqIndexer* i, i32 n, i32 m) {
    i32 idx = hindex_hash(n, m) & (i->cap - 1);
    i32 dist = 1;

    while (dist <= hindex_secret(i->bkts + idx)) {
        if (hindex_n(i->bkts + idx) == n && hindex_m(i->bkts + idx) == m)
            return idx;
        ++dist;
        idx = (idx + 1) & (i->cap - 1);
    }
    return -1;
}

static i32 indexer_add(HeapqIndexer* i, i32 n, i32 m, i32 index) {
    assert(i->count * (i64)8 < i->cap * (i64)7, "too full");
    HIndex* bkt = &(HIndex){0};

    hindex_set_secret(bkt, 1);
    hindex_set_n(bkt, n);
    hindex_set_m(bkt, m);
    bkt->idx = index;

    i32 idx = hindex_hash(n, m) & (i->cap - 1);
    i32 at = -1;

    while (hindex_secret(i->bkts + idx)) {
        if (hindex_secret(bkt) > hindex_secret(i->bkts + idx)) {
            swap(*bkt, i->bkts[idx]);
            if (at < 0)
                at = idx;
        }

        i32 dist = hindex_secret(bkt);
        assert(dist < lobits(HINDEX_COUNT_SECRET) - 1, "too clumped?");
        hindex_set_secret(bkt, dist + 1);
        idx = (idx + 1) & (i->cap - 1);
    }

    i->bkts[idx] = *bkt;
    if (at < 0)
        at = idx;

    ++i->count;
    return at;
}

static void indexer_remove(HeapqIndexer* i, i32 idx) {
    assert(i->count > 0, "empty");
    for (;;) {
        i32 next = (idx + 1) & (i->cap - 1);

        if (hindex_secret(i->bkts + next) <= 1) {
            hindex_set_secret(i->bkts + idx, 0);
            break;
        }

        i->bkts[idx] = i->bkts[next];
        i32 dist = hindex_secret(i->bkts + idx);
        hindex_set_secret(i->bkts + idx, dist - 1);

        idx = next;
    }
    --i->count;
}


typedef struct HEdge {
    f64 cost;
    i32 n;
    i32 m;
} HEdge;

typedef struct Heapq {
    HEdge* data;
    i32 count;
    i32 maxcount;
    HeapqIndexer idxr;
} Heapq;

static i32 heapq_parent(i32 i)      { return (i - 1)/2; }
static i32 heapq_left_child(i32 i)  { return 2*i + 1; }
static i32 heapq_right_child(i32 i) { return 2*i + 2; }
static i32 heapq_lt(const HEdge* rstr a, const HEdge* rstr b) {
    return a->cost < b->cost;
}

static void heapq_swap(Heapq* h, i32 i, i32 j, i32 idxr_i) {
    i32 idxr_j = indexer_find(&h->idxr, h->data[j].n, h->data[j].m);
    swap(h->data[i], h->data[j]);
    h->idxr.bkts[idxr_i].idx = j;
    h->idxr.bkts[idxr_j].idx = i;
}

static void heapq_sift_up(Heapq* h, i32 i, i32 idxr_i) {
    while (i > 0 && heapq_lt(h->data + i, h->data + heapq_parent(i))) {
        heapq_swap(h, i, heapq_parent(i), idxr_i);
        i = heapq_parent(i);
    }
}

static void heapq_sift_down(Heapq* h, i32 i, i32 idxr_i) {
    for (;;) {
        i32 smallest = i;
        i32 l = heapq_left_child(i);
        i32 r = heapq_right_child(i);

        if (l < h->count && heapq_lt(h->data + l, h->data + smallest))
            smallest = l;
        if (r < h->count && heapq_lt(h->data + r, h->data + smallest))
            smallest = r;

        if (smallest == i)
            break;

        heapq_swap(h, i, smallest, idxr_i);
        i = smallest;
        // loop.
    }
}

static void heapq_init(Heapq* h, i32 maxcount, i64* rstr out_heap_size,
        i64* rstr out_idxr_size) {
    i64 size = sizeof(*h->data) * maxcount;
    h->data = malloc(size);
    assert(h->data, "allocation failed");
    memset(h->data, 0, size);
    h->count = 0;
    h->maxcount = maxcount;

    maxcount += maxcount / 2;
    maxcount = max(16, maxcount);
    maxcount = uptopow2(maxcount);
    indexer_init(&h->idxr, maxcount, out_idxr_size);

    if (out_heap_size)
        *out_heap_size = size;
}

static void heapq_push(Heapq* h, const HEdge* rstr edge) {
    // Only update the heap if the edge already exists.
    assert(edge->n < edge->m, "unsorted edge");

    i32 idxr_i = indexer_find(&h->idxr, edge->n, edge->m);

    if (idxr_i >= 0) {
        i32 i = h->idxr.bkts[idxr_i].idx;
        h->data[i].cost = edge->cost;
        heapq_sift_up(h, i, idxr_i);
        heapq_sift_down(h, i, idxr_i);
    } else {
        assert(h->count < h->maxcount, "heap overflow");
        i32 i = h->count++;
        h->data[i] = *edge;
        idxr_i = indexer_add(&h->idxr, edge->n, edge->m, i);
        heapq_sift_up(h, i, idxr_i);
    }
}

static void heapq_pop(Heapq* h, HEdge* rstr out) {
    assert(h->count > 0, "empty heap");
    *out = h->data[0];

    i32 idxr_i = indexer_find(&h->idxr, out->n, out->m);
    indexer_remove(&h->idxr, idxr_i);

    h->data[0] = h->data[--h->count];
    if (h->count > 0) {
        i32 idxr_j = indexer_find(&h->idxr, h->data[0].n, h->data[0].m);
        h->idxr.bkts[idxr_j].idx = 0;
        heapq_sift_down(h, 0, idxr_j);
    }
}

static void heapq_remove(Heapq* h, i32 n, i32 m) {
    i32 idxr_i = indexer_find(&h->idxr, n, m);
    if (idxr_i < 0) // ignore if not present.
        return;

    i32 i = h->idxr.bkts[idxr_i].idx;
    indexer_remove(&h->idxr, idxr_i);

    --h->count;
    if (i < h->count) {
        h->data[i] = h->data[h->count];
        idxr_i = indexer_find(&h->idxr, h->data[i].n, h->data[i].m);
        assert(idxr_i >= 0, "not in idxr?");
        h->idxr.bkts[idxr_i].idx = i;
        heapq_sift_up(h, i, idxr_i);
        // note this is kinda dodge bc `idxr_i` may not be correct now but in the
        // case that it is incorrect it means sift down will do nothing smile.
        heapq_sift_down(h, i, idxr_i);
    }
}



UNUSED static i32 heapq_check(const Heapq* h) {

    // Validate heap ordering.
    for (i32 i=1; i<h->count; ++i) {
        i32 p = heapq_parent(i);
        if (heapq_lt(h->data + i, h->data + p))
            return -1;
    }

    // Check indexer and Robin Hood invariants.
    i32 idxr_count = 0;
    for (i32 i=0; i<h->idxr.cap; ++i) {
        const HIndex* bkt = h->idxr.bkts + i;
        i32 secret = hindex_secret(bkt);
        if (secret == 0)
            continue;

        ++idxr_count;

        i32 n = hindex_n(bkt);
        i32 m = hindex_m(bkt);
        i32 idx = bkt->idx;

        if (idx < 0 || idx >= h->count)
            return -2;
        if (h->data[idx].n != n || h->data[idx].m != m)
            return -3;

        // Check Robin Hood distance.
        i32 ideal_idx = hindex_hash(n, m) & (h->idxr.cap - 1);
        i32 expected_secret = ((i - ideal_idx) & (h->idxr.cap - 1)) + 1;
        if (secret != expected_secret)
            return -4;
    }

    if (idxr_count != h->idxr.count)
        return -5;

    // Check indexer is complete.
    for (i32 i=0; i<h->count; ++i) {
        i32 n = h->data[i].n;
        i32 m = h->data[i].m;

        i32 idxr_i = indexer_find(&h->idxr, n, m);
        if (idxr_i < 0)
            return -6;
        if (h->idxr.bkts[idxr_i].idx != i)
            return -7;
    }

    // Cheeky great success.
    return 0;
}




// wrap in structs to prevent compiler thinking they're aliasing.
typedef struct AdjTri { i32 _; } AdjTri;
typedef struct AdjOff { i32 _; } AdjOff;
typedef struct AdjCursor { i32 _; } AdjCursor;

typedef struct Adj {
    // Contiguous array of entries. Each entry is:
    //   i32 tri_index_0
    //   i32 tri_index_1
    //   ...
    //   i32 tri_index_n
    //   i32 next
    // If `next` is >0, it is another index into `off` which is also the adjacent
    // faces of this vertex.
    AdjTri* tri;
    // Maps vertex index into an index into `tri`.
    AdjOff* off;
    // Temp memory during creation/refresh.
    AdjCursor* cursor;

    i32 total;
} Adj;

static void adj_refresh(Adj* a, const Vertex* V, const Tri* T, i32 Vcount,
        i32 Tcount) {
    (void)V;

    i64 off_size = sizeof(*a->off) * (Vcount + 1);
    if (!a->off)
        a->off = malloc(off_size);
    assert(a->off, "allocation failure");

    // Take initial adj counts.
    memset(a->off, 0, off_size);
    for (i32 t=0; t<Tcount; ++t) {
        if (T[t].dead)
            continue;
        ++a->off[T[t].a]._;
        ++a->off[T[t].b]._;
        ++a->off[T[t].c]._;
    }

    // Turn into cumulative (and add next spacing).
    i32 total = 0;
    for (i32 v=0; v<Vcount + 1; ++v) {
        i32 c = a->off[v]._ + 1;
        a->off[v]._ = total;
        assert(!add_overflow(total, total, c), "too many triangles");
        total += c;
    }
    if (a->total < 0)
        a->total = total;
    assert(total <= a->total, "edge count increased?");

    // Fill out entries.
    i64 tri_size = sizeof(*a->tri) * a->total;
    if (!a->tri)
        a->tri = malloc(tri_size);
    assert(a->tri, "allocation failure");
    i64 cursor_size = sizeof(*a->cursor) * Vcount;
    if (!a->cursor)
        a->cursor = malloc(cursor_size);
    assert(a->cursor, "allocation failure");

    memset(a->cursor, 0, cursor_size);

    for (i32 t=0; t<Tcount; ++t) {
        if (T[t].dead)
            continue;
        for (i32 i=0; i<3; ++i) {
            i32 v = T[t].i[i];
            a->tri[a->off[v]._ + a->cursor[v]._]._ = t;
            ++a->cursor[v]._;
        }
    }

    // Set all `next`s as tails.
    for (i32 v=0; v<Vcount; ++v) {
        i32 entrysize = a->off[v + 1]._ - a->off[v]._;
        a->tri[a->off[v]._ + entrysize - 1]._ = -1;
    }
}


static void adj_init(Adj* a, const Vertex* V, const Tri* T, i32 Vcount,
        i32 Tcount, i64* rstr out_tri_size, i64* rstr out_off_size,
        i64* rstr out_cursor_size) {
    a->tri = NULL;
    a->off = NULL;
    a->cursor = NULL;
    a->total = -1;
    adj_refresh(a, V, T, Vcount, Tcount);

    // Out alloc sizes.
    i64 off_size = sizeof(*a->off) * (Vcount + 1);
    i64 tri_size = sizeof(*a->tri) * a->total;
    i64 cursor_size = sizeof(*a->cursor) * Vcount;
    if (out_tri_size)
        *out_tri_size = tri_size;
    if (out_off_size)
        *out_off_size = off_size;
    if (out_cursor_size)
        *out_cursor_size = cursor_size;
}



static i32 adj_entry_count(const Adj* a, i32 v) {
    return a->off[v + 1]._ - a->off[v]._ - 1; // dont include next.
}
static i32 adj_entry_at(const Adj* a, i32 v, i32 i) {
    return a->tri[a->off[v]._ + i]._;
}
static i32 adj_entry_next(const Adj* a, i32 v) {
    i32 size = a->off[v + 1]._ - a->off[v]._;
    return a->tri[a->off[v]._ + size - 1]._;
}

static void adj_append(Adj* a, i32 add_it_here, i32 add_this) {
    assert(add_it_here != add_this, "recursive append");
    i32 v = add_it_here;
    while (adj_entry_next(a, v) >= 0)
        v = adj_entry_next(a, v);
    i32 size = a->off[v + 1]._ - a->off[v]._;
    a->tri[a->off[v]._ + size - 1]._ = add_this;
}


typedef struct AdjIter {
    const Adj* a;
    i32 v;
    i32 i;
    i32 count;
} AdjIter;

#define adj_iter(a, v) ( adj_iter_impl(&(AdjIter){0}, (a), (v)) )
static AdjIter* adj_iter_impl(AdjIter* i, const Adj* a, i32 v) {
    i->a = a;
    i->v = v;
    i->i = 0;
    i->count = adj_entry_count(a, i->v);
    while (i->count <= 0) {
        i->v = adj_entry_next(a, i->v);
        if (i->v < 0)
            break;
        i->count = adj_entry_count(a, i->v);
    }
    return i;
}

static i32 adj_end(AdjIter* i) {
    return (i->v <= 0);
}

static void adj_next(AdjIter* i) {
    assert(i->v >= 0, "dangling iter");
    ++i->i;
    while (i->i >= i->count) {
        i->v = adj_entry_next(i->a, i->v);
        if (i->v < 0)
            return;
        i->i = 0;
        i->count = adj_entry_count(i->a, i->v);
    }
}

static i32 adj_t(AdjIter* i) {
    assert(i->v >= 0, "dangling iter");
    return adj_entry_at(i->a, i->v, i->i);
}

#define adj_iterate(adj, start, itervar)                \
        AdjIter* itervar = adj_iter((adj), (start));    \
        !adj_end(itervar);                              \
        adj_next(itervar)





UNUSED static i32 mesh_check(const Vertex* V, const Tri* T, i32 Tcount) {
    for (i32 t=0; t<Tcount; ++t) {
        if (T[t].dead)
            continue;
        i32 a = T[t].a;
        i32 b = T[t].b;
        i32 c = T[t].c;
        if (a == b || a == c || b == c)
            return -1;
        if ((V[a].flags | V[b].flags | V[c].flags) & DEAD)
            return -2;
    }
    return 0;
}






// Returns 1 if collapsing edge (n->m, merged at vbar) would flip any surviving
// triangle, 0 if the collapse is geometrically safe.
static i32 collapse_would_flip(const Vertex* V, const Tri* T, Adj* adj,
        i32 n, i32 m, vec3 vbar) {

    // We only need to inspect triangles adjacent to m, because those are
    // the ones whose vertex `m` will move to `vbar`. Triangles only touching
    // `n` already have `n` at its current position, which stays fixed for
    // this flip test (n will be moved to vbar too, but its current neighbours
    // have already been validated in a previous step - we re-validate them
    // here as well to be safe).
    //
    // Walk the full adjacency chain for both n and m.
    for (i32 root=0; root<2; ++root) {
        i32 start = (root == 0) ? m : n;

        for (adj_iterate(adj, start, it)) {
            i32 t = adj_t(it);
            if (T[t].dead)
                continue;

            // Triangles containing both are being killed.
            i32 has_n = (T[t].a == n || T[t].b == n || T[t].c == n);
            i32 has_m = (T[t].a == m || T[t].b == m || T[t].c == m);
            if (has_n && has_m)
                continue;

            i32 moving = has_m ? m : n; // the vertex that will become vbar

            // Collect the three current positions.
            vec3 p[3];
            for (i32 j=0; j<3; ++j)
                p[j] = vec3_from_array(V[T[t].i[j]].v);

            // Compute old normal.
            vec3 old_normal = cross(p[1] - p[0], p[2] - p[0]);
            f32 old_mag = mag(old_normal);
            if (old_mag < 1e-7f)
                continue;
            old_normal /= old_mag;

            // Substitute the moving vertex with vbar.
            vec3 q[3];
            for (i32 j=0; j<3; ++j)
                q[j] = (T[t].i[j] == moving) ? vbar : p[j];

            // Compute new normal.
            vec3 new_normal = cross(q[1] - q[0], q[2] - q[0]);
            f32 new_mag = mag(new_normal);
            if (new_mag < 1e-7f)
                return 1; // stop degen triangles.
            new_normal /= new_mag;

            // Check angle between normals.
            if (dot(old_normal, new_normal) < 0.1f)
                return 1;
        }
    }
    return 0;
}




// Returns 1 if collapsing edge (n,m) would violate the link condition (unsafe,
// would create a non-manifold mesh or change topology). Returns 0 if the
// collapse is topologically valid.
static i32 collapse_would_tear(Vertex* V, const Tri* T, Adj* adj, Neighbours* nb,
        i32 n, i32 m) {

    // Walk n's 1-ring, tagging them and recording the third vertex of every
    // triangle containing BOTH n and m.

    i32 edge_link[2]; // manifold has at-most 2.
    i32 edge_link_count = 0;
    i32 shared_faces = 0;

    for (adj_iterate(adj, n, it)) {
        i32 t = adj_t(it);
        if (T[t].dead)
            continue;

        i32 has_m = (T[t].a == m || T[t].b == m || T[t].c == m);
        if (has_m)
            ++shared_faces;

        for (i32 i=0; i<3; ++i) {
            i32 v = T[t].i[i];
            if (v == n || v == m)
                continue;

            neighbours_push(nb, V, v); // tags via in_neighbours, deduplicates

            if (has_m) {
                // v is the third vertex of a shared face: record it.
                i32 dup = 0;
                for (i32 k=0; k<edge_link_count; ++k) {
                    if (edge_link[k] == v) {
                        dup = 1;
                        break;
                    }
                }
                if (!dup) {
                    assert(edge_link_count < numel(edge_link),
                            "non-manifold input?");
                    edge_link[edge_link_count++] = v;
                }
            }
        }
    }

    // Should never happen (i.e. no edge from n to m) but might as well.
    if (shared_faces == 0) {
        while (nb->count > 0)
            neighbours_pop(nb, V);
        return 1;
    }

    // Now walk m's 1-ring. Every vertex that appears in both rings (i.e. is
    // tagged) needs to be one of the recorded edge-link vertices.

    i32 safe = 1;
    for (adj_iterate(adj, m, it)) {
        i32 t = adj_t(it);
        if (T[t].dead)
            continue;

        for (i32 i=0; i<3; ++i) {
            i32 v = T[t].i[i];
            if (v == n || v == m)
                continue;
            if (!(V[v].flags & IN_NEIGHBOURS)) // not in n's 1-ring.
                continue;

            // `v` is shared. Verify it is an edge-link vertex.
            i32 in_edge_link = 0;
            for (i32 k=0; k<edge_link_count; ++k) {
                if (edge_link[k] == v) {
                    in_edge_link = 1;
                    break;
                }
            }
            if (!in_edge_link)
                safe = 0;

            // Manual untag to prevent double-counting.
            V[v].flags &= ~IN_NEIGHBOURS;
        }
    }

    while (nb->count > 0)
        neighbours_pop(nb, V);
    return !safe;
}


typedef struct InternedPoint {
    i32 idx; // index into V. <0 if empty.
} InternedPoint;

typedef struct Mesh {
    Vertex* V;
    Tri* T;
    i32 Vcount;
    i32 Tcount;
    InternedPoint* pset;
    i64 pset_cap;
} Mesh;

static f32 point_snap(f32 c) {
  #if 1
    return c;
  #else
    const f32 eps = 1e-5f;
    return floor(c / eps + 0.5f) * eps;
  #endif
}
static u64 point_hash(f32 x, f32 y, f32 z) {
    u64 a = 0;
    memcpy(&a, &x, sizeof(x));
    memcpy((u8*)&a + 4, &y, sizeof(y));
    u64 b = 0;
    memcpy(&b, &z, sizeof(z));
    return hash_aug(hash_u64(a), hash_u64(b));
}

static i32 point_intern(Mesh* m, f32 x, f32 y, f32 z) {
    f32 sx = point_snap(x);
    f32 sy = point_snap(y);
    f32 sz = point_snap(z);
    u64 h = point_hash(sx, sy, sz) & (m->pset_cap - 1);
    i32 idx = m->pset[h].idx;
    // linear probing on collision.
    while (idx >= 0) {
        i32 same = 1;
        same &= (point_snap(m->V[idx].x) == sx);
        same &= (point_snap(m->V[idx].y) == sy);
        same &= (point_snap(m->V[idx].z) == sz);
        if (same)
            return m->pset[h].idx;
        h = (h + 1) & (m->pset_cap - 1);
        idx = m->pset[h].idx;
    }
    idx = m->Vcount++;
    assert(m->Vcount <= lobits(min(HINDEX_COUNT_N, HINDEX_COUNT_M)),
            "too many vertices");
    m->pset[h].idx = idx;
    memset(m->V + idx, 0, sizeof(Vertex));
    m->V[idx].x = x;
    m->V[idx].y = y;
    m->V[idx].z = z;
    return idx;
}

typedef struct STLTriangle {
    f32 normal[3];
    f32 a[3];
    f32 b[3];
    f32 c[3];
    u16 attr;
} STLTriangle;

enum { STL_TRI_SIZE = 50 };

static i64 stl_size(i32 Tcount) {
    return 84 + (i64)50 * Tcount;
}

static void read_stl(Mesh* m, const char* rstr path) {
    printf("\n--- reading stl ---\n");

    FILE* f = fopen(path, "rb");
    assert(f, "file open failure");

    fseek(f, 80, SEEK_SET); // skip header.
    u32 ucount;
    fread(&ucount, sizeof(ucount), 1, f);
    assert(ucount <= intmax(i32), "too many triangles");
    m->Tcount = (i32)ucount;

    // Allocate worst case.
    m->V = malloc(sizeof(Vertex) * (i64)3*m->Tcount);
    m->T = malloc(sizeof(Tri) * m->Tcount);
    assert(m->V, "allocation failure");
    assert(m->T, "allocation failure");
    m->pset_cap = 1;
    while (m->pset_cap < (i64)6*m->Tcount) // worst-case 50% loading.
        m->pset_cap <<= 1;
    m->pset = malloc(sizeof(InternedPoint) * m->pset_cap);
    assert(m->pset, "allocation failure");

    // Initialise vertex set to empty.
    for (i64 i=0; i<m->pset_cap; ++i)
        m->pset[i].idx = -1;
    m->Vcount = 0;

    // Read all triangles.
    i32 degen_triangles = 0;
    STLTriangle tri;
    i32 t = 0;
    ProgressBar* pb = &(ProgressBar){0};
    progressbar_init(pb, m->Tcount, 20);
    for (i32 i=0; i<m->Tcount; ++i) {
        progressbar_update(pb, t);
        if (fread(&tri, STL_TRI_SIZE, 1, f) < 1)
            break;
        vec3 a = vec3_from_array(tri.a);
        vec3 b = vec3_from_array(tri.b);
        vec3 c = vec3_from_array(tri.c);
        vec3 normal = cross(b - a, c - a); // ccw normal.
        f32 nmag = mag(normal);
        if (nmag == 0.0f) {
            ++degen_triangles;
            continue;
        }
        // let tiny triangles fly bc otherwise it would cause a non-closed
        // manifold :/.
        vec3 stl_normal = vec3_from_array(tri.normal);
        if (dot(normal, stl_normal) < 0.0f)
            swap(b, c);
        i32 ia = point_intern(m, a[0], a[1], a[2]);
        i32 ib = point_intern(m, b[0], b[1], b[2]);
        i32 ic = point_intern(m, c[0], c[1], c[2]);
        if (ia == ib || ia == ic || ib == ic) {
            ++degen_triangles;
            continue;
        }
        m->T[t].a = ia;
        m->T[t].b = ib;
        m->T[t].c = ic;
        m->T[t].dead = 0;
        ++t;
    }
    progressbar_finish(pb);

    if (degen_triangles > 0) {
        printf("warning: input stl had %d degenerate triangle%s\n",
                degen_triangles, (degen_triangles > 1) ? "s" : "");
    }

    m->Tcount = t;

    free(m->pset);
    m->pset = NULL;
    m->pset_cap = 0;
    fclose(f);

    // Shrink arrays.
    m->V = realloc(m->V, sizeof(Vertex) * m->Vcount);
    m->T = realloc(m->T, sizeof(Tri) * m->Tcount);
}


static void write_stl(const Mesh* m, const char* rstr path) {
    printf("\n--- writing stl ---\n");

    FILE* f = fopen(path, "wb");
    assert(f, "file open failure");

    char header[80];
    memset(header, ' ', sizeof(header));
    header[0] = 'd';
    header[1] = 'e';
    header[2] = 'c';
    header[3] = 'i';
    header[5] = '(';
    header[6] = 's';
    header[7] = 't';
    header[8] = 'u';
    header[9] = ')';
    fwrite(header, sizeof(header), 1, f);

    i32 count = 0;
    for (i32 t=0; t<m->Tcount; ++t)
        count += !m->T[t].dead;
    fwrite(&count, sizeof(count), 1, f);

    ProgressBar* pb = &(ProgressBar){0};
    progressbar_init(pb, m->Tcount, 20);
    for (i32 t=0; t<m->Tcount; ++t) {
        progressbar_update(pb, t);
        if (m->T[t].dead)
            continue;
        Vertex* va = m->V + m->T[t].a;
        Vertex* vb = m->V + m->T[t].b;
        Vertex* vc = m->V + m->T[t].c;
        assert(!(va->flags & DEAD), "dead vertex in alive tri");
        assert(!(vb->flags & DEAD), "dead vertex in alive tri");
        assert(!(vc->flags & DEAD), "dead vertex in alive tri");
        vec3 a = vec3_from_array(va->v);
        vec3 b = vec3_from_array(vb->v);
        vec3 c = vec3_from_array(vc->v);
        vec3 normal = cross(b - a, c - a); // ccw normal.
        normal = normalise(normal);

        STLTriangle tri;
        tri.normal[0] = normal[0];
        tri.normal[1] = normal[1];
        tri.normal[2] = normal[2];
        tri.a[0] = a[0];
        tri.a[1] = a[1];
        tri.a[2] = a[2];
        tri.b[0] = b[0];
        tri.b[1] = b[1];
        tri.b[2] = b[2];
        tri.c[0] = c[0];
        tri.c[1] = c[1];
        tri.c[2] = c[2];
        tri.attr = 0;
        fwrite(&tri, STL_TRI_SIZE, 1, f);
    }
    progressbar_finish(pb);

    fclose(f);
}




__attribute((__hot__))
i32 main(i32 argc, char** argv);
i32 main(i32 argc, char** argv) {
    if (assertion_has_failed()) {
        const char* msg = assertion_message();
        msg += numel("ERROR, ") - 1;
        printf("\nerror: %s\n", msg);
        return 1;
    }

    timer_init();


    assert(argc == 4 || argc == 5,
            "usage: deci <input.stl> <output.stl> <final size proportion> "
                        "[max cutoff cost]");
    const char* path_in = argv[1];
    const char* path_out = argv[2];
    f64 reduction = parse_f64(argv[3]);
    assert(notnan(reduction), "failed to parse final size");
    assert(0.0 < reduction && reduction < 1.0, "invalid final size "
                                               "(must be >0 and <1)");
    f64 cutoff_cost = (argc > 4) ? parse_f64(argv[4]) : +INF;
    assert(notnan(cutoff_cost), "failed to parse cutoff cost");

    printf("--- DECI ---\n");
    if (isinf(cutoff_cost)) {
        printf("in         = %s\n", path_in);
        printf("out        = %s\n", path_out);
        printf("reduction  = %.1f%%\n", 100.0*reduction);
    } else {
        printf("in           = %s\n", path_in);
        printf("out          = %s\n", path_out);
        printf("reduction    = %.2f %%\n", 100.0*reduction);
        printf("cutoff cost  = %.4g\n", cutoff_cost);
    }


    // Read da mesh.
    Mesh mesh;
    read_stl(&mesh, path_in);
    Vertex* V = mesh.V;
    Tri* T = mesh.T;
    i32 Vcount = mesh.Vcount;
    i32 Tcount = mesh.Tcount;
    printf("\n--- counting edges (input) ---\n");
    i32 Ecount = count_edges(T, Tcount);
    printf("\n--- analysing topology (input) ---\n");
    i32 closed_mani = is_closed_mani(V, T, Tcount, Ecount);

    printf("\n--- mesh stats (input) ---");
    printf("\nvertex count  = "); print_numba(Vcount, 0);
    printf("\ntri count     = "); print_numba(Tcount, 0);
    printf("\nedge count    = "); print_numba(Ecount, 0);
    printf("\neuler char.   = %4d", Vcount - Ecount + Tcount);
    printf("\nclosed mani.  = %4d", closed_mani);
    printf("\nfile size     = "); print_numba(stl_size(Tcount), 1);
    printf("\n");

    assert(closed_mani, "input mesh must be a closed manifold");


    printf("\n--- allocations ---");
    i64 size_vertex = Vcount * sizeof(Vertex);
    i64 size_index = Tcount * sizeof(Tri);
    printf("\nvertex      = "); print_numba(size_vertex, 1);
    printf("\nindex       = "); print_numba(size_index, 1);


    // Setup adjacency lists.
    Adj* adj = &(Adj){0};
    i64 size_adj_tri;
    i64 size_adj_off;
    i64 size_adj_cursor;
    adj_init(adj, V, T, Vcount, Tcount, &size_adj_tri, &size_adj_off,
            &size_adj_cursor);
    printf("\nadj tri     = "); print_numba(size_adj_tri, 1);
    printf("\nadj off     = "); print_numba(size_adj_off, 1);
    printf("\nadj cursor  = "); print_numba(size_adj_cursor, 1);


    // Setup edge heap.
    Heapq* heap = &(Heapq){0};
    i64 size_heap_bkts;
    i64 size_heap_idxr;
    heapq_init(heap, Ecount, &size_heap_bkts, &size_heap_idxr);
    printf("\nheap bkts   = "); print_numba(size_heap_bkts, 1);
    printf("\nheap idxr   = "); print_numba(size_heap_idxr, 1);


    // Setup neighbours set.
    Neighbours* neighbours = &(Neighbours){0};
    i64 size_neighbours;
    neighbours_init(neighbours, Ecount, &size_neighbours);
    // Note this ^ is a huuuuge oversize in everything except the absolute worst-
    // case but its fine since it wont be physically backed (its just contiguous
    // array space) to in reality its dynamically upsized-only by the os.
    printf("\nneighbours  = "); print_numba(size_neighbours, 1);



    printf("\nTOTAL       = "); print_numba((i64)0
        + size_vertex + size_index
        + size_adj_tri + size_adj_off + size_adj_cursor
        + size_heap_bkts + size_heap_idxr
        + size_neighbours
        , 1);
    printf("\n");


    // Compute initial quadrics.
    printf("\n--- initial quadrics ---\n");
    ProgressBar* pb_quadrics = &(ProgressBar){0};
    progressbar_init(pb_quadrics, Tcount, 10);
    for (i32 t=0; t<Tcount; ++t) {
        progressbar_update(pb_quadrics, t);
        compute_quadric(V, T + t);
    }
    progressbar_finish(pb_quadrics);

    // Find cost of all edges.
    printf("\n--- initial edge costs ---\n");
    ProgressBar* pb_initial_edge = &(ProgressBar){0};
    progressbar_init(pb_initial_edge, Tcount, 30);
    for (i32 t=0; t<Tcount; ++t) {
        progressbar_update(pb_initial_edge, t);
        for (i32 i=0; i<3; ++i) {
            i32 j = (i + 1) % 3;
            i32 n = min(T[t].i[i], T[t].i[j]);
            i32 m = max(T[t].i[i], T[t].i[j]);
            f64 cost = optimal_collapse(V, n, m, (f32[3]){0});
            HEdge edge = { cost, n, m };
            heapq_push(heap, &edge);
        }
    }
    progressbar_finish(pb_initial_edge);


    // Pop edges.
    i32 limit = max(4 /* closed solid */, Tcount * reduction);
    i32 active = Tcount;
    i32 last_refreshed_adj = active;
    printf("\n--- decimating ---\n");

    ProgressBar* pb_deci = &(ProgressBar){0};
    // https://www.desmos.com/calculator/8xkwvol4j6
    i32 pb_deci_touchpoints = (i32)(1.0 + 0.2*sqrt((f64)(active - limit)));
    pb_deci_touchpoints = min(max(pb_deci_touchpoints, 5), 300);
    progressbar_init(pb_deci, active - limit, pb_deci_touchpoints);
    TIME_ZONE(ZONE_DECIMATE)
    while ((active > limit) && (heap->count > 0)) {
        TIME_ZONE(ZONE_PROGRESS_BAR)
            progressbar_update(pb_deci, Tcount - active);

        // As we kill more triangles, the adjacency list becomes more and more
        // deadspace and each vertex requires a longer and longer walk. Because
        // of this, its much faster if we recompute the adj list every so-often.
        TIME_ZONE(ZONE_ADJ_REFRESH)
        if (last_refreshed_adj - active > 1000000) {
            adj_refresh(adj, V, T, Vcount, Tcount);
            last_refreshed_adj = active;
        }


        HEdge edge;

        // Peep first.
        edge = heap->data[0];
        if (edge.cost > cutoff_cost)
            break;

        // Pop now.
        TIME_ZONE(ZONE_HEAP_POP)
            heapq_pop(heap, &edge);
        i32 n = edge.n;
        i32 m = edge.m;


        // If this would open the manifold, dont do it.
        TIME_ZONE(ZONE_COLLAPSE_WOULD_TEAR)
        if (collapse_would_tear(V, T, adj, neighbours, n, m))
            goto NEXT;


        f32 vbar[3];
        TIME_ZONE(ZONE_OPTIMAL_COLLAPSE)
            (void)optimal_collapse(V, n, m, vbar);


        // If this would flip a triangle, dont do it.
        TIME_ZONE(ZONE_COLLAPSE_WOULD_FLIP)
        if (collapse_would_flip(V, T, adj, n, m, vec3_from_array(vbar)))
            goto NEXT;


        // Overwrite index `n` with the new optimal position and mark `m` dead.
        TIME_ZONE(ZONE_COLLAPSE) {
            V[n].x = vbar[0];
            V[n].y = vbar[1];
            V[n].z = vbar[2];
            for (i32 i=0; i<numel(Quadric); ++i)
                V[n].q[i] += V[m].q[i];

            V[m].flags |= DEAD;
        }

        // Kill faces containing both `n` and `m`, and update faces containing
        // `m` to point to `n`.
        TIME_ZONE(ZONE_TRI_UPDATE)
        for (adj_iterate(adj, m, it)) {
            i32 t = adj_t(it);
            if (T[t].dead)
                continue;

            // Contains both, kill.
            if (n == T[t].a || n == T[t].b || n == T[t].c) {
                T[t].dead = 1;
                --active;
            }
            // Else point to `n`.
            else {
                TIME_ZONE(ZONE_TRI_SHIFT)
                for (i32 i=0; i<3; ++i) {
                    if (T[t].i[i] != m)
                        continue;
                    T[t].i[i] = n;
                    i32 ii = (i + 1) % 3;
                    i32 iii = (i + 2) % 3;
                    i32 nn = min(m, T[t].i[ii]);
                    i32 mm = max(m, T[t].i[ii]);
                    heapq_remove(heap, nn, mm);
                    nn = min(m, T[t].i[iii]);
                    mm = max(m, T[t].i[iii]);
                    heapq_remove(heap, nn, mm);
                    break;
                }
            }
        }

        // Move all `m`'s adjacencies to `n`.
        TIME_ZONE(ZONE_ADJ_APPEND)
            adj_append(adj, n, m);


        // Recompute edges touching `n`.

        TIME_ZONE(ZONE_NEIGHBOUR_FIND)
        for (adj_iterate(adj, n, it)) {
            i32 t = adj_t(it);
            if (T[t].dead)
                continue;
            for (i32 i=0; i<3; ++i) {
                i32 v = T[t].i[i];
                if (v == n)
                    continue;
                neighbours_push(neighbours, V, v);
            }
        }

        TIME_ZONE(ZONE_NEIGHBOUR_RECOMPUTE)
        while (neighbours->count > 0) {
            i32 neighbour = neighbours_pop(neighbours, V);
            i32 nn = min(n, neighbour);
            i32 mm = max(n, neighbour);
            f64 cost = optimal_collapse(V, nn, mm, (f32[3]){0});
            HEdge edge = { cost, nn, mm };
            heapq_push(heap, &edge);
        }

      NEXT:;
    }
    progressbar_finish(pb_deci);

    report_telemetry();

    write_stl(&mesh, path_out);

    i32 new_Vcount = 0;
    for (i32 v=0; v<Vcount; ++v)
        new_Vcount += !(V[v].flags & DEAD);
    i32 new_Tcount = active; // we "happened" to already be tracking it.
    printf("\n--- counting edges (output) ---\n");
    i32 new_Ecount = count_edges(T, Tcount);
    printf("\n--- analysing topology (output) ---\n");
    i32 new_closed_mani = is_closed_mani(V, T, Tcount, new_Ecount);

    printf("\n--- mesh stats (output) ---");
    printf("\nvertex count  = "); print_numba(new_Vcount, 0);
    printf("\ntri count     = "); print_numba(new_Tcount, 0);
    printf("\nedge count    = "); print_numba(new_Ecount, 0);
    printf("\neuler char.   = %4d", new_Vcount - new_Ecount + new_Tcount);
    printf("\nclosed mani.  = %4d", new_closed_mani);
    printf("\nfile size     = "); print_numba(stl_size(new_Tcount), 1);
    f64 file_size_percent = stl_size(new_Tcount) * 100.0 / stl_size(Tcount);
    printf("\n               (%7.2f %% )", file_size_percent);
    printf("\n");

    return 0;
}
