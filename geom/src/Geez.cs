using static Br;
using Vec3 = System.Numerics.Vector3;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using PolyLine = PicoGK.PolyLine;
using BBox3 = PicoGK.BBox3;
using Colour = PicoGK.ColorFloat;

using LocalFrame = Leap71.ShapeKernel.LocalFrame;

public class Geez {
    public static Colour? colour = null;
    public static float? alpha = null;
    public static float? metallic = null;
    public static float? roughness = null;

    public static Colour dflt_colour = new Colour("#501F14"); // copperish.
    public static float dflt_alpha = 0.75f;
    public static float dflt_metallic = 0.3f;
    public static float dflt_roughness = 0.7f;

    public static bool sectioned = false;

    public static IDisposable like(Colour? colour=null, float? alpha=null,
            float? metallic=null, float? roughness=null, bool? sectioned=null) {
        var colour0 = Geez.colour;
        var alpha0 = Geez.alpha;
        var metallic0 = Geez.metallic;
        var roughness0 = Geez.roughness;
        var sectioned0 = Geez.sectioned;
        Geez.colour = colour ?? colour0;
        Geez.alpha = alpha ?? alpha0;
        Geez.metallic = metallic ?? metallic0;
        Geez.roughness = roughness ?? roughness0;
        Geez.sectioned = sectioned ?? sectioned0;
        return new OnLeave(() => {
                Geez.colour = colour0;
                Geez.alpha = alpha0;
                Geez.metallic = metallic0;
                Geez.roughness = roughness0;
                Geez.sectioned = sectioned0;
            });
    }
    public static IDisposable dflt_like(Colour? colour=null, float? alpha=null,
            float? metallic=null, float? roughness=null, bool? sectioned=null) {
        var colour0 = Geez.dflt_colour;
        var alpha0 = Geez.dflt_alpha;
        var metallic0 = Geez.dflt_metallic;
        var roughness0 = Geez.dflt_roughness;
        var sectioned0 = Geez.sectioned;
        Geez.dflt_colour = colour ?? colour0;
        Geez.dflt_alpha = alpha ?? alpha0;
        Geez.dflt_metallic = metallic ?? metallic0;
        Geez.dflt_roughness = roughness ?? roughness0;
        Geez.sectioned = sectioned ?? sectioned0;
        return new OnLeave(() => {
                Geez.dflt_colour = colour0;
                Geez.dflt_alpha = alpha0;
                Geez.dflt_metallic = metallic0;
                Geez.dflt_roughness = roughness0;
                Geez.sectioned = sectioned0;
            });
    }
    private class OnLeave : IDisposable {
        protected readonly Action _func;
        public OnLeave(Action func) { _func = func; }
        public void Dispose() { _func(); }
    }


    protected static Dictionary<int, object> _geezed = new();
    protected static int _next = 1;
    protected static int _push(object obj) {
        int key = _next++;
        _geezed.Add(key, obj);
        return key;
    }
    protected static int _push_mesh(in Mesh mesh) {
        Leap71.ShapeKernel.Sh.PreviewMesh(
            mesh,
            clrColor: colour ?? dflt_colour,
            fTransparency: alpha ?? dflt_alpha,
            fMetallic: metallic ?? dflt_metallic,
            fRoughness: roughness ?? dflt_roughness
        );
        return _push(mesh);
    }
    protected static int _push_meshes(in List<Mesh> meshes) {
        foreach (Mesh mesh in meshes) {
            Leap71.ShapeKernel.Sh.PreviewMesh(
                mesh,
                clrColor: colour ?? dflt_colour,
                fTransparency: alpha ?? dflt_alpha,
                fMetallic: metallic ?? dflt_metallic,
                fRoughness: roughness ?? dflt_roughness
            );
        }
        return _push(meshes);
    }
    protected static int _push_line(in PolyLine line) {
        PicoGK.Library.oViewer().Add(line);
        return _push(line);
    }
    protected static int _push_lines(in List<PolyLine> lines) {
        foreach (PolyLine line in lines)
            PicoGK.Library.oViewer().Add(line);
        return _push(lines);
    }

    public static void pop(int count, int ignore=0) {
        assert(count <= _geezed.Count());
        assert(count >= 0);
        assert(ignore >= 0);
        while (count --> 0) { // so glad down-to operator made it into c#.
            int key = _next;
            for (int i=-1; i<ignore; ++i) {
                while (!_geezed.ContainsKey(key - 1)) // O(n) skull emoji.
                    --key;
                --key;
            }
            remove(key);
        }
    }
    public static void remove(int key) {
        object obj = _geezed[key];
        _geezed.Remove(key);

        if (obj is Mesh mesh) {
            PicoGK.Library.oViewer().Remove(mesh);
        } else if (obj is List<Mesh> meshes) {
            foreach (Mesh m in meshes)
                PicoGK.Library.oViewer().Remove(m);
        } else if (obj is PolyLine line) {
            PicoGK.Library.oViewer().Remove(line);
        } else if (obj is List<PolyLine> lines) {
            foreach (PolyLine l in lines)
                PicoGK.Library.oViewer().Remove(l);
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


    public static int voxels(in Voxels vox, Colour? colour=null) {
        // Cross section if requested.
        Voxels new_vox = vox;
        if (sectioned) {
            BBox3 bounds = vox.oCalculateBoundingBox();
            bounds.vecMin.X = 0f;
            // dont trim the original.
            Voxels box = new(PicoGK.Utils.mshCreateCube(bounds));
            new_vox = new_vox.voxBoolIntersect(box);
        }
        Mesh mesh = new Mesh(new_vox);
        using (like(colour: colour))
            return _push_mesh(mesh);
    }

    public static int point(in Vec3 p, float r=2f, Colour? colour=null) {
        Mesh mesh = new Mesh(new Ball(p, r).voxels());
        using (like(colour: colour))
            return _push_mesh(mesh);
    }

    public static int points(in List<Vec3> ps, float r=2f, Colour? colour=null) {
        List<Mesh> meshes = new();
        foreach (Vec3 p in ps) {
            Mesh mesh = new Mesh(new Ball(p, r).voxels());
            meshes.Add(mesh);
        }
        using (like(colour: colour))
            return _push_meshes(meshes);
    }

    public static int line(in PolyLine line) {
        return _push_line(line);
    }
    public static int lines(in List<PolyLine> lines) {
        return _push_lines(lines);
    }

    protected static void _frame_lines(List<PolyLine> lines, in LocalFrame frame,
            float size) {
        Vec3 p = frame.vecGetPosition();

        PolyLine X = new(COLOUR_RED);
        X.nAddVertex(p);
        X.nAddVertex(p + size*frame.vecGetLocalX());
        X.AddArrow(0.2f*size);

        PolyLine Y = new(COLOUR_GREEN);
        Y.nAddVertex(p);
        Y.nAddVertex(p + size*frame.vecGetLocalY());
        Y.AddArrow(0.2f*size);

        PolyLine Z = new(COLOUR_BLUE);
        Z.nAddVertex(p);
        Z.nAddVertex(p + size*frame.vecGetLocalZ());
        Z.AddArrow(0.2f*size);

        lines.Add(X);
        lines.Add(Y);
        lines.Add(Z);
    }
    public static int frame(in LocalFrame frame, float size=5f) {
        List<PolyLine> lines = new();
        _frame_lines(lines, frame, size);
        return _push_lines(lines);
    }
    public static int frames(in List<LocalFrame> frames, float size=5f) {
        List<PolyLine> lines = new();
        foreach (LocalFrame frame in frames)
            _frame_lines(lines, frame, size);
        return _push_lines(lines);
    }

    protected static void _bbox_lines(List<PolyLine> lines, in BBox3 bbox,
            Colour? colour) {
        Colour col = colour ?? Geez.colour ?? Geez.dflt_colour;
        Vec3 p000 = bbox.vecMin;
        Vec3 p111 = bbox.vecMax;
        Vec3 p100 = p000 + uX3*(p111 - p000);
        Vec3 p010 = p000 + uY3*(p111 - p000);
        Vec3 p001 = p000 + uZ3*(p111 - p000);
        Vec3 p011 = p000 + (uY3 + uZ3)*(p111 - p000);
        Vec3 p101 = p000 + (uX3 + uZ3)*(p111 - p000);
        Vec3 p110 = p000 + (uX3 + uY3)*(p111 - p000);
        PolyLine l;

        // Each corner (+3+3), then the zigzag joining (+6).

        l = new(col);
        l.nAddVertex(p000);
        l.nAddVertex(p100);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p000);
        l.nAddVertex(p010);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p000);
        l.nAddVertex(p001);
        lines.Add(l);

        l = new(col);
        l.nAddVertex(p111);
        l.nAddVertex(p011);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p111);
        l.nAddVertex(p101);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p111);
        l.nAddVertex(p110);
        lines.Add(l);

        l = new(col);
        l.nAddVertex(p100);
        l.nAddVertex(p101);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p101);
        l.nAddVertex(p001);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p001);
        l.nAddVertex(p011);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p011);
        l.nAddVertex(p010);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p010);
        l.nAddVertex(p110);
        lines.Add(l);
        l = new(col);
        l.nAddVertex(p110);
        l.nAddVertex(p100);
        lines.Add(l);
    }
    public static int bbox(in BBox3 bbox, Colour? colour=null) {
        List<PolyLine> lines = new();
        _bbox_lines(lines, bbox, colour);
        return _push_lines(lines);
    }
    public static int bboxes(in List<BBox3> bboxes, Colour? colour=null) {
        List<PolyLine> lines = new();
        foreach (BBox3 bbox in bboxes)
            _bbox_lines(lines, bbox, colour);
        return _push_lines(lines);
    }
}
