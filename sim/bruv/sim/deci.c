#include "br.h"

#include "assertion.h"
#include "maths.h"
#include "hash.h"



typedef f32 mat3[9]; // row-major 3x3 matrix.

static f32 det3(mat3 m) {
    return m[0] * (m[4]*m[8] - m[5]*m[7])
         - m[1] * (m[3]*m[8] - m[5]*m[6])
         + m[2] * (m[3]*m[7] - m[4]*m[6]);
}

static vec3 solve3x3(mat3 A, f32 B[3]) {
    f32 det = det3(A);
    if (abs(det) < 1e-6f)
        return v3NAN;
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
    return vec3(det3(Ax)/det, det3(Ay)/det, det3(Az)/det);
}





// 4x4 symmetric matrix:
//  [ q0  q1  q2  q3  ]
//  [     q4  q5  q6  ]
//  [         q7  q8  ]
//  [             q9  ]
typedef f32 Quadric[10];

typedef struct __attribute__((__aligned__(16))) Vertex  {
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
    // Is active?
    i32 dead;
    // Quadric.
    Quadric q;
    // In current neighbour set?
    i32 in_neighbours;

    i32 _; // padding.
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


static void compute_quadric(Vertex* V, Tri* t) {
    vec3 a = vec3_from_array(V[t->a].v);
    vec3 b = vec3_from_array(V[t->b].v);
    vec3 c = vec3_from_array(V[t->c].v);

    vec3 normal = cross(b - a, c - a);
    f32 normal_mag = mag(normal);
    if (nearzero(normal_mag))
        return; // leave quadric unchanged.
    normal /= normal_mag;

    f32 d = -dot(normal, a);
    vec4 p = vec4(normal, d);

    Quadric q;
    q[0] = p[0]*p[0];
    q[1] = p[0]*p[1];
    q[2] = p[0]*p[2];
    q[3] = p[0]*p[3];

    q[4] = p[1]*p[1];
    q[5] = p[1]*p[2];
    q[6] = p[1]*p[3];

    q[7] = p[2]*p[2];
    q[8] = p[2]*p[3];

    q[9] = p[3]*p[3];

    for (i32 i=0; i<numel(Quadric); ++i)
        V[t->a].q[i] += q[i];
    for (i32 i=0; i<numel(Quadric); ++i)
        V[t->b].q[i] += q[i];
    for (i32 i=0; i<numel(Quadric); ++i)
        V[t->c].q[i] += q[i];
}

static vec4 optimal_collapse(const Vertex* V, i32 n, i32 m) {
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
    f32 B[3] = {
        -q[3], -q[6], -q[8],
    };
    vec3 v = solve3x3(A, B);
    if (isnan(v)) {
        v = vec3(
            0.5f*(V[n].x + V[m].x),
            0.5f*(V[n].y + V[m].y),
            0.5f*(V[n].z + V[m].z)
        );
    }

    // Evaluate the quadric error at vbar:
    f32 c = q[0]*v[0]*v[0] + 2*q[1]*v[0]*v[1] + 2*q[2]*v[0]*v[2] + 2*q[3]*v[0]
                           +   q[4]*v[1]*v[1] + 2*q[5]*v[1]*v[2] + 2*q[6]*v[1]
                                              +   q[7]*v[2]*v[2] + 2*q[8]*v[2]
                                                                 +   q[9];

    // Package return.
    return vec4(v, c);
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
    i32 cap;
    i32 count;
} EdgeSet;


UNUSED static void edgeset_init(EdgeSet* s, i32 cap, i64* rstr out_size) {
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

UNUSED static void edgeset_clear(EdgeSet* s) {
    i64 size = sizeof(SEdge) * s->cap;
    memset(s->bkts, 0, size);
    s->count = 0;
}

UNUSED static i32 edgeset_find(const EdgeSet* s, i32 n, i32 m) {
    u64 key_hash = sedge_hash(n, m);
    i32 idx = key_hash & (s->cap - 1);
    i32 dist = 1;

    while (dist <= sedge_secret(s->bkts + idx)) {
        if (sedge_n(s->bkts + idx) == n && sedge_m(s->bkts + idx) == m)
            return idx;
        ++dist;
        idx = (idx + 1) & (s->cap - 1);
    }
    return -1;
}

UNUSED static void edgeset_add(EdgeSet* s, i32 n, i32 m) {
    assert(s->count * 8 < s->cap * 7, "too full");
    SEdge* bkt = &(SEdge){0};

    sedge_set_secret(bkt, 1);
    sedge_set_n(bkt, n);
    sedge_set_m(bkt, m);

    i32 idx = sedge_hash(n, m) & (s->cap - 1);

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

UNUSED static void edgeset_remove(EdgeSet* s, i32 idx) {
    assert(s->count > 0, "empty");
    // Robin hood backward shift deletion.
    for (;;) {
        i32 next = (idx + 1) & (s->cap - 1);

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

static void neighbours_add(Neighbours* n, Vertex* V, i32 v) {
    assert(n->count < n->cap, "full neighbours");
    if (V[v].in_neighbours)
        return;
    V[v].in_neighbours = 1;
    n->v[n->count++] = v;
}

static i32 neighbours_pop(Neighbours* n, Vertex* V) {
    assert(n->count > 0, "empty neighbours");
    i32 v = n->v[--n->count];
    V[v].in_neighbours = 0;
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

static void indexer_add(HeapqIndexer* i, i32 n, i32 m, i32 index) {
    assert(i->count * 8 < i->cap * 7, "too full");
    HIndex* bkt = &(HIndex){0};

    hindex_set_secret(bkt, 1);
    hindex_set_n(bkt, n);
    hindex_set_m(bkt, m);
    bkt->idx = index;

    i32 idx = hindex_hash(n, m) & (i->cap - 1);

    while (hindex_secret(i->bkts + idx)) {
        if (hindex_secret(bkt) > hindex_secret(i->bkts + idx))
            swap(*bkt, i->bkts[idx]);

        i32 dist = hindex_secret(bkt);
        assert(dist < lobits(SEDGE_COUNT_SECRET) - 1, "too clumped?");
        hindex_set_secret(bkt, dist + 1);
        idx = (idx + 1) & (i->cap - 1);
    }

    i->bkts[idx] = *bkt;
    ++i->count;
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
    f32 cost;
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
static i32 heapq_lt(const HEdge* a, const HEdge* b) {
    return a->cost < b->cost;
}

static void heapq_swap(Heapq* h, i32 i, i32 j) {
    swap(h->data[i], h->data[j]);

    i32 idxr_i = indexer_find(&h->idxr, h->data[i].n, h->data[i].m);
    i32 idxr_j = indexer_find(&h->idxr, h->data[j].n, h->data[j].m);
    assert(idxr_i >= 0, "not in idxr?");
    assert(idxr_j >= 0, "not in idxr?");

    h->idxr.bkts[idxr_i].idx = i;
    h->idxr.bkts[idxr_j].idx = j;
}

static void heapq_sift_up(Heapq* h, i32 i) {
    while (i > 0 && heapq_lt(h->data + i, h->data + heapq_parent(i))) {
        heapq_swap(h, i, heapq_parent(i));
        i = heapq_parent(i);
    }
}

static void heapq_sift_down(Heapq* h, i32 i) {
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

        heapq_swap(h, i, smallest);
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

static void heapq_push(Heapq* h, const HEdge* edge) {
    // Only update the heap if the edge already exists.
    assert(edge->n < edge->m, "unsorted edge");

    i32 idxr_i = indexer_find(&h->idxr, edge->n, edge->m);

    if (idxr_i >= 0) {
        i32 i = h->idxr.bkts[idxr_i].idx;
        h->data[i].cost = edge->cost;
        heapq_sift_up(h, i);
        heapq_sift_down(h, i);
    } else {
        assert(h->count < h->maxcount, "heap overflow");
        i32 i = h->count++;
        h->data[i] = *edge;
        indexer_add(&h->idxr, edge->n, edge->m, i);
        heapq_sift_up(h, i);
    }
}

static void heapq_pop(Heapq* h, HEdge* out) {
    assert(h->count > 0, "empty heap");
    *out = h->data[0];

    i32 idxr_i = indexer_find(&h->idxr, out->n, out->m);
    indexer_remove(&h->idxr, idxr_i);

    h->data[0] = h->data[--h->count];
    if (h->count > 0) {
        i32 idxr_j = indexer_find(&h->idxr, h->data[0].n, h->data[0].m);
        h->idxr.bkts[idxr_j].idx = 0;
        heapq_sift_down(h, 0);
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

        i32 idxr_j = indexer_find(&h->idxr, h->data[i].n, h->data[i].m);
        assert(idxr_j >= 0, "not in idxr?");
        h->idxr.bkts[idxr_j].idx = i;
        heapq_sift_up(h, i);
        heapq_sift_down(h, i);
    }
}




// wrap in structs to prevent compiler thinking they're aliasing.
typedef struct AdjTri { i32 _; } AdjTri;
typedef struct AdjOff { i32 _; } AdjOff;

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
} Adj;

static void adj_init(Adj* a, const Tri* T, i32 Vcount, i32 Tcount,
        i64* rstr out_tri_size, i64* rstr out_off_size) {
    i64 off_size = sizeof(*a->off) * (Vcount + 1);
    a->off = malloc(off_size);
    assert(a->off, "allocation failure");

    // Take initial adj counts.
    memset(a->off, 0, off_size);
    for (i32 t=0; t<Tcount; ++t) {
        ++a->off[T[t].a]._;
        ++a->off[T[t].b]._;
        ++a->off[T[t].c]._;
    }

    // Turn into cumulative (and add next spacing).
    i32 total = 0;
    for (i32 v=0; v<Vcount + 1; ++v) {
        i32 c = a->off[v]._ + 1;
        a->off[v]._ = total;
        total += c;
    }

    // Fill out entries.
    i64 tri_size = sizeof(*a->tri) * total;
    a->tri = malloc(tri_size);
    assert(a->tri, "allocation failure");
    i64 cursor_size = sizeof(i32) * Vcount;
    i32* cursor = malloc(cursor_size); // current entry sizes.
    assert(cursor, "allocation failure");

    memset(cursor, 0, cursor_size);

    for (i32 t=0; t<Tcount; ++t) {
        for (i32 i=0; i<3; ++i) {
            i32 v = T[t].i[i];
            a->tri[a->off[v]._ + cursor[v]]._ = t;
            ++cursor[v];
        }
    }

    free(cursor);

    // Set all `next`s as tails.
    for (i32 v=0; v<Vcount; ++v) {
        i32 entrysize = a->off[v + 1]._ - a->off[v]._;
        a->tri[a->off[v]._ + entrysize - 1]._ = -1;
    }

    // Out alloc sizes.
    if (out_tri_size)
        *out_tri_size = tri_size;
    if (out_off_size)
        *out_off_size = off_size;
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





typedef struct InternedPoint {
    f32 x;
    f32 y;
    f32 z;
    i32 idx; // index into V. <0 if empty.
} InternedPoint;

typedef struct Mesh {
    Vertex* V;
    Tri* T;
    i32 Vcount;
    i32 Tcount;
    InternedPoint* pset;
    u64 pset_cap;
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
    // linear probing on collision.
    while (m->pset[h].idx >= 0) {
        i32 same = 1;
        same &= (point_snap(m->pset[h].x) == sx);
        same &= (point_snap(m->pset[h].y) == sy);
        same &= (point_snap(m->pset[h].z) == sz);
        if (same)
            return m->pset[h].idx;
        h = (h + 1) & (m->pset_cap - 1);
    }
    i32 idx = m->Vcount++;
    m->pset[h] = (InternedPoint){ x, y, z, idx };
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
    return 84 + ((i64)50) * Tcount;
}

static void read_stl(Mesh* m, const char* rstr path) {
    FILE* f = fopen(path, "rb");
    assert(f, "file open failure");

    fseek(f, 80, SEEK_SET); // skip header.
    fread(&m->Tcount, sizeof(m->Tcount), 1, f);

    // Allocate worst case.
    m->V = malloc(sizeof(Vertex) * 3*m->Tcount);
    m->T = malloc(sizeof(Tri) * m->Tcount);
    assert(m->V, "allocation failure");
    assert(m->T, "allocation failure");
    m->pset_cap = 1;
    while (m->pset_cap < 6*m->Tcount) // worst-case 50% loading.
        m->pset_cap <<= 1;
    m->pset = malloc(sizeof(InternedPoint) * m->pset_cap);
    assert(m->pset, "allocation failure");

    // Initialise vertex set to empty.
    for (i32 i=0; i<m->pset_cap; ++i)
        m->pset[i].idx = -1;
    m->Vcount = 0;

    // Read all triangles.
    STLTriangle tri;
    i32 t = 0;
    while (t < m->Tcount) {
        if (fread(&tri, STL_TRI_SIZE, 1, f) < 1)
            break;
        vec3 a = vec3_from_array(tri.a);
        vec3 b = vec3_from_array(tri.b);
        vec3 c = vec3_from_array(tri.c);
        vec3 normal = cross(b - a, c - a); // ccw normal.
        if (mag(normal) < 1e-6f)
            continue;
        vec3 stl_normal = vec3_from_array(tri.normal);
        if (dot(normal, stl_normal) < 0)
            swap(b, c);
        i32 ia = point_intern(m, a[0], a[1], a[2]);
        i32 ib = point_intern(m, b[0], b[1], b[2]);
        i32 ic = point_intern(m, c[0], c[1], c[2]);
        if (ia == ib || ia == ic || ib == ic)
            continue;
        m->T[t].a = ia;
        m->T[t].b = ib;
        m->T[t].c = ic;
        m->T[t].dead = 0;
        ++t;
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
    FILE* f = fopen(path, "wb");
    assert(f, "file open failure");

    char header[80];
    memset(header, ' ', sizeof(header));
    fwrite(header, sizeof(header), 1, f);

    i32 count = 0;
    for (i32 t=0; t<m->Tcount; ++t)
        count += !m->T[t].dead;

    fwrite(&count, sizeof(count), 1, f);
    for (i32 t=0; t<m->Tcount; ++t) {
        if (m->T[t].dead)
            continue;
        Vertex* va = m->V + m->T[t].a;
        Vertex* vb = m->V + m->T[t].b;
        Vertex* vc = m->V + m->T[t].c;
        assert(!va->dead, "what");
        assert(!vb->dead, "what");
        assert(!vc->dead, "what");
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

    fclose(f);
}



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
    i32 total;
    i32 freq;
    i32 last;
} ProgressBar;

enum { PROGRESS_BAR_WIDTH = 20 };

static void progressbar_init(ProgressBar* pb, i32 total, i32 touchpoints) {
    pb->start_time = timer_now();
    pb->total = total;
    pb->freq = max(1, (i32)(total / (f64)touchpoints));
    pb->last = -1;
}

static void progressbar_update(ProgressBar* pb, i32 curr) {
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

    printf("] %5.1f%% | elapsed: %s | eta: %s", 100.0*percent, elapsed_str,
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

    printf("] 100.0%% | elapsed: %s        ", elapsed_str);
    for (i32 i=0; i<TIMER_STRING_SIZE; ++i)
        putchar(' ');

    printf("\n");
    fflush(stdout);
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
    if (i == 0)
        printf("%lld", value);
    else if (val < 10.0)
        printf("  %.2f %s", val, suffixes[i]);
    else if (val < 100.0)
        printf(" %.1f  %s", val, suffixes[i]);
    else
        printf("%.1f  %s", val, suffixes[i]);
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
    i32 sign = 0;
    if (*s == '-') {
        sign = 1;
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

    printf("decimating mesh...\n");
    if (isinf(cutoff_cost)) {
        printf("         in: %s\n", path_in);
        printf("        out: %s\n", path_out);
        printf("  reduction: %.2f %%\n", 100.0*reduction);
    } else {
        printf("           in: %s\n", path_in);
        printf("          out: %s\n", path_out);
        printf("    reduction: %.2f %%\n", 100.0*reduction);
        printf("  cutoff cost: %.4g\n", cutoff_cost);
    }


    // Read da mesh.
    Mesh mesh;
    read_stl(&mesh, path_in);
    Vertex* V = mesh.V;
    Tri* T = mesh.T;
    i32 Vcount = mesh.Vcount;
    i32 Tcount = mesh.Tcount;
    i32 Ecount = Vcount + Tcount - 2; // eulers formula for a closed shape.

    printf("\n--- mesh stats (input) ---");
    printf("\nvertex count  = "); print_numba(Vcount, 0);
    printf("\ntri count     = "); print_numba(Tcount, 0);
    printf("\nedge count    = "); print_numba(Ecount, 0);
    printf("\nfile size     = "); print_numba(stl_size(Tcount), 1);
    printf("\n");


    printf("\n--- allocations ---");
    i64 size_vertex = Vcount * sizeof(Vertex);
    i64 size_index = Tcount * sizeof(Tri);
    printf("\nvertex      = "); print_numba(size_vertex, 1);
    printf("\nindex       = "); print_numba(size_index, 1);


    // Setup adjacency lists.
    Adj* adj = &(Adj){0};
    i64 size_adj_tri;
    i64 size_adj_off;
    adj_init(adj, T, Vcount, Tcount, &size_adj_tri, &size_adj_off);
    printf("\nadj tri     = "); print_numba(size_adj_tri, 1);
    printf("\nadj off     = "); print_numba(size_adj_off, 1);


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
    neighbours_init(neighbours, 100 * 1000, &size_neighbours);
    printf("\nneighbours  = "); print_numba(size_neighbours, 1);



    printf("\nTOTAL       = "); print_numba((i64)0
        + size_vertex + size_index
        + size_adj_tri + size_adj_off
        + size_heap_bkts + size_heap_idxr
        + size_neighbours
        , 1);
    printf("\n");


    // Compute initial quadrics.
    printf("\ninitial quadrics:\n");
    ProgressBar* pb_quadrics = &(ProgressBar){0};
    progressbar_init(pb_quadrics, Tcount, 10);
    for (i32 t=0; t<Tcount; ++t) {
        progressbar_update(pb_quadrics, t);
        compute_quadric(V, T + t);
    }
    progressbar_finish(pb_quadrics);

    // Find cost of all edges.
    printf("\ninitial edge costs:\n");
    ProgressBar* pb_initial_edge = &(ProgressBar){0};
    progressbar_init(pb_initial_edge, Tcount, 10);
    for (i32 t=0; t<Tcount; ++t) {
        progressbar_update(pb_initial_edge, t);
        for (i32 i=0; i<3; ++i) {
            i32 n = min(T[t].i[i], T[t].i[(i + 1) % 3]);
            i32 m = max(T[t].i[i], T[t].i[(i + 1) % 3]);
            vec4 vbar_cost = optimal_collapse(V, n, m);
            HEdge edge = { vbar_cost[3], n, m };
            heapq_push(heap, &edge);
        }
    }
    progressbar_finish(pb_initial_edge);


    // Pop edges.
    i32 limit = max(3 /* closed solid */, Tcount * reduction);
    i32 active = Tcount;
    printf("\nprimary edge popping:\n");

    ProgressBar* pb_edge = &(ProgressBar){0};
    // https://www.desmos.com/calculator/8xkwvol4j6
    i32 pb_edge_touchpoints = (i32)(1.0 + 0.36332*sqrt((f64)(active - limit)));
    pb_edge_touchpoints = min(max(pb_edge_touchpoints, 5), 300);
    progressbar_init(pb_edge, active - limit, pb_edge_touchpoints);
    while ((active > limit) && (heap->count > 0)) {
        progressbar_update(pb_edge, Tcount - active);

        HEdge edge;

        // Peep first.
        edge = heap->data[0];
        if (edge.cost > cutoff_cost)
            break;

        // Pop now.
        heapq_pop(heap, &edge);
        i32 n = edge.n;
        i32 m = edge.m;

        if (V[n].dead || V[m].dead)
            continue;

        vec4 vbar_cost = optimal_collapse(V, n, m);

        // Overwrite index `n` with the new optimal position and mark `m` dead.

        V[n].x = vbar_cost[0];
        V[n].y = vbar_cost[1];
        V[n].z = vbar_cost[2];
        for (i32 i=0; i<numel(Quadric); ++i)
            V[n].q[i] += V[m].q[i];

        V[m].dead = 1;

        // Kill faces containing both `n` and `m`, and update faces containing
        // `m` to point to `n`.
        for (i32 adj_v = m; adj_v >= 0; adj_v = adj_entry_next(adj, adj_v)) {
            i32 entry_count = adj_entry_count(adj, adj_v);
            for (i32 i=0; i<entry_count; ++i) {
                i32 t = adj_entry_at(adj, adj_v, i);
                if (T[t].dead)
                    continue;

                // Contains both, kill.
                if (n == T[t].a || n == T[t].b || n == T[t].c) {
                    T[t].dead = 1;
                    --active;
                }
                // Else point to `n`.
                else {
                    for (i32 j=0; j<3; ++j) {
                        if (T[t].i[j] == m) {
                            T[t].i[j] = n;
                            i32 jj = (j + 1) % 3;
                            i32 jjj = (j + 2) % 3;
                            i32 nn = min(m, T[t].i[jj]);
                            i32 mm = max(m, T[t].i[jj]);
                            heapq_remove(heap, nn, mm);
                            nn = min(m, T[t].i[jjj]);
                            mm = max(m, T[t].i[jjj]);
                            heapq_remove(heap, nn, mm);
                            break;
                        }
                    }
                }
            }
        }

        // Move all `m`'s adjacencies to `n`.
        adj_append(adj, n, m);


        // Recompute edges touching `n`.

        for (i32 adj_v = n; adj_v >= 0; adj_v = adj_entry_next(adj, adj_v)) {
            i32 entry_count = adj_entry_count(adj, adj_v);
            for (i32 i=0; i<entry_count; ++i) {
                i32 t = adj_entry_at(adj, adj_v, i);
                if (T[t].dead)
                    continue;

                for (i32 j=0; j<3; ++j) {
                    i32 v = T[t].i[j];
                    if (V[v].dead)
                        continue;
                    if (v == n)
                        continue;
                    neighbours_add(neighbours, V, v);
                }
            }
        }

        while (neighbours->count > 0) {
            i32 neighbour = neighbours_pop(neighbours, V);
            i32 nn = min(n, neighbour);
            i32 mm = max(n, neighbour);
            vec4 vbar_cost = optimal_collapse(V, nn, mm);
            HEdge edge = { vbar_cost[3], nn, mm };
            heapq_push(heap, &edge);
        }
    }
    progressbar_finish(pb_edge);

    i32 new_Vcount = 0;
    for (i32 v=0; v<Vcount; ++v)
        new_Vcount += !V[v].dead;
    i32 new_Tcount = active; // we "happened" to already be tracking it.
    i32 new_Ecount = new_Vcount + new_Tcount - 2;

    printf("\n--- mesh stats (output) ---");
    printf("\nvertex count  = "); print_numba(new_Vcount, 0);
    printf("\ntri count     = "); print_numba(new_Tcount, 0);
    printf("\nedge count    = "); print_numba(new_Ecount, 0);
    printf("\nfile size     = "); print_numba(stl_size(new_Tcount), 1);
    f64 file_size_percent = stl_size(new_Tcount) * 100.0 / stl_size(Tcount);
    printf("\n               (%6.2f %% )", file_size_percent);
    printf("\n");

    write_stl(&mesh, path_out);

    return 0;
}
