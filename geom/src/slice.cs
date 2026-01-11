using static br.Br;

using IEnumerator = System.Collections.IEnumerator;
using IEnumerable = System.Collections.IEnumerable;

namespace br {

// honestly these so useful why they not in the standard. lowk maybe it is im
// bad at looking.

public class Slice<T> : IReadOnlyList<T> {
    private IReadOnlyList<T> backing;
    private int off;   // {0} U [0,numel)
    private int count; // >=0
    private int step;  // !=0
    private int tile;  // >0
    private int rep;   // >0
    private List<T>? mem; // to faciliate .Add
    private int totalcount; // =count*tile*rep

    public Slice() {
        // The only time mem is nonnull (maintained through .Add tho).
        mem = [];
        backing = mem;
        off = 0;
        count = 0;
        step = 1;
        tile = 1;
        rep = 1;
        totalcount = 0;
    }

    public Slice(IReadOnlyList<T> of, KeywordOnly? _=null,
            int step=1, int tile=1, int rep=1)
        : this(of, 0, numel(of), step, tile, rep) {}

    public Slice(IReadOnlyList<T> of, int off, int count, int step=1, int tile=1,
            int rep=1) {
        assert(of != null);
        if (of == null) throw new Exception(); // warnings shuddup.

        assert(tile >= 0);
        assert(rep >= 0);

        // Wrap.
        if (off < 0)
            off += numel(of);

        // Treat count<0 as starting one step previous and travelling reverse.
        if (count < 0) {
            off -= step; // dont re-wrap.
            step = -step;
            count = -count;
        }

        if (count == 0 || step == 0 || tile == 0 || rep == 0) {
            // allow any input start, since all are technically oob.
            // allow any step, since why not.
            off = 0;
            count = 0;
            step = 1;
            tile = 1;
            rep = 1;
        } else {
            assert_idx(off, numel(of));
            assert_idx(off + (count - 1)*step, numel(of));
        }

        // see if we dont need to create a recursive structure and can instead
        // skip to the backing array.
        if (of is Slice<T> ofslice) {
            // check they dont actually change the wrapping behaviour.
            if (ofslice.tile == 1 && ofslice.rep == 1) {
                // YIPPEE. go straight to the source.
                of = ofslice.backing;
                step = step * ofslice.step;
                off = ofslice.off + off * ofslice.step;
            }
        }

        this.backing = of;
        this.off = off;
        this.count = count;
        this.step = step;
        this.tile = tile;
        this.rep = rep;
        this.mem = null;
        this.totalcount = count*tile*rep;
    }

    public static implicit operator Slice<T>(T[] arr)
        => new Slice<T>(arr);
    public static implicit operator Slice<T>(List<T> list)
        => new Slice<T>(list);

    public Slice<T> subslice(KeywordOnly? _=null,
            int step=1, int tile=1, int rep=1)
        => new(this, step: step, tile: tile, rep: rep);
    public Slice<T> subslice(int off, int count, int step=1, int tile=1,
            int rep=1)
        => new(this, off, count, step, tile, rep);

    public Slice<T> reversed()
        => subslice(-1, numel(this), -1);
    public Slice<T> stepped(int substep)
        => subslice(step: substep);
    public Slice<T> tiled(int subtile)
        => subslice(tile: subtile);
    public Slice<T> repeated(int subrep)
        => subslice(rep: subrep);


    public T this[int idx] {
        get {
            // Allow negative indexing :).
            assert_idx(idx, numel(this), true);
            // dude for some reason this numel call was RIDICULOUSLY slow when
            // `Count` wasn't cached and `numel` wasn't explicitly defined for
            // Slices. i have no idea why.
            // oh my fucking god IEnumerable.Count() evaluates every single
            // element in the enumerable. unreal.
            if (idx < 0)
                idx += numel(this);
            idx /= rep;
            idx %= count;
            idx *= step;
            idx += off;
            return backing[idx];
        }
    }

    // pretty dodgy considering slice is meant to be readonly, but i want some
    // clean [1,2,3] syntax to initialise them.
    public void Add(T item) {
        if (mem == null)
            throw new Exception(".Add is only intended to be used to faciliate "
                              + "the language builtin [x, x, ...] "
                              + "initialisation. do not use it elsewhere, or i "
                              + "will find you.");
        // note that the fresh check is not a catch-all, only a catch-some. like
        // it wont catch:
        //  Slice<int> s = [0,1,2];
        //  s.Add(3); // succeeds.
        // but honestly im completely fine with that.

        mem.Add(item);
        count += 1;
        totalcount += 1;
    }


    /* contractual obligations. */
    public int Count => totalcount;
    public IEnumerator<T> GetEnumerator() {
        for (int t=0; t<tile; ++t) {
            for (int i=0; i<count; ++i) {
                for (int r=0; r<rep; ++r) {
                    yield return backing[off + step*i];
                }
            }
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public override string ToString()
        => $"<slice, off={off}, count={count}, step={step}, tile={tile}, "
         + $"rep={rep}, backing: ({backing})>";
}

public static partial class Br {
    public static int numel<T>(Slice<T> slice) => slice.Count;
}

}
