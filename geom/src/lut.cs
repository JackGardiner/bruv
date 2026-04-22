using static br.Br;

namespace br {

// honestly these so useful why they not in the standard. lowk maybe it is im
// bad at looking.

public class LUT {
    public float[] tbl;
    public float xlo;
    public float xhi;
    public float Dx => (xhi - xlo) / (N - 1);
    public float x(int i) => lerp(xlo, xhi, i, N);
    public int N => numel(tbl);

    public LUT() {
        this.tbl = [];
        this.xlo = NAN;
        this.xhi = NAN;
    }
    public LUT(float[] tbl, float xlo, float xhi) {
        this.tbl = tbl;
        this.xlo = xlo;
        this.xlo = xhi;
    }
    public LUT(float[] X, float[] V, int N=1000) {
        for (int i=0; i<numel(X) - 1; ++i)
            assert(X[i + 1] > X[i], $"i={i}");

        this.tbl = new float[N];
        this.xlo = X[0];
        this.xhi = X[numel(X) - 1];

        for (int i=0; i<N; ++i) {
            float x = lerp(xlo, xhi, i, N);

            // Find surrounding sample points via binary search.
            int lo = 0;
            int hi = numel(X) - 1;
            while (hi - lo > 1) {
                int mid = (lo + hi) / 2;
                if (X[mid] <= x) lo = mid;
                else             hi = mid;
            }

            // Lerp between the two neighbours.
            float t = invlerp(X[lo], X[hi], x);
            tbl[i] = lerp(V[lo], V[hi], t);
        }
    }

    // no fucking call operator?
    public float this[float x] {
        get {
            // cheeky lookup table mate. could write em in my sleep.
            x = invlerp(xlo, xhi, x);
            x = clamp(x, 0f, 1f);
            x *= N - 1;
            int i = clamp(ifloor(x), 0, N - 2);
            float t = x - i;
            return lerp(tbl[i], tbl[i + 1], t);
        }
    }
}

}
