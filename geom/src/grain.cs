/* Hybrid fuel grain scaffold: a gyroid annulus with an OD skin, feed
   chimneys and an optional base shelf. Everything comes from the "grain"
   block of config/all.json. */

using static br.Br;
using br;
using TPIAP = TwoPeasInAPod;

using Vec3 = System.Numerics.Vector3;

using JsonOptions = System.Text.Json.JsonSerializerOptions;
using JsonSerializer = System.Text.Json.JsonSerializer;
using SHA256 = System.Security.Cryptography.SHA256;

using Voxels = PicoGK.Voxels;
using Mesh = PicoGK.Mesh;
using IImplicit = PicoGK.IImplicit;
using BBox3 = PicoGK.BBox3;

public partial class Grain : TPIAP.Pea {

    public string name => "grain";


    /* ENVELOPE */
    public required float D_grain { get; init; } // outer diameter.
    public required float D_port { get; init; } // port (mandrel) diameter.
    public required float L_puck { get; init; } // finished, i.e. stacked length.
    public required float L_trim { get; init; } // sacrificial, added on top.
    public required float th_skin { get; init; } // OD skin thickness.

    /* GYROID */
    public required float th_gyr { get; init; } // sheet wall thickness.
    public required float L_cell { get; init; } // cell period, x and y.
    public required float Lz_cell { get; init; } // cell period, z.
    public required float x0_gyr { get; init; } // lattice origin, i.e. the
    public required float y0_gyr { get; init; } // phase of the cell pattern
    public required float z0_gyr { get; init; } // relative to the puck.

    // sheet grading. th_anchor = 0 for a constant th_gyr.
    public required float th_anchor { get; init; }
    public required float ramp_gyr { get; init; }

    // fillet where the lattice meets the annulus. 0 for a hard edge.
    public required float blend_gyr { get; init; }

    // fillet where the skin meets the lattice. 0 for none.
    public required float fillet_skin { get; init; }

    /* FEED CHIMNEYS */
    public required int no_chim { get; init; } // 0 for none.
    public required float D_chim { get; init; }
    public required float r_chim { get; init; } // centres, from the axis.

    // first chimney from +x. Off multiples of 90 deg, where the bore is
    // tangent to the sheet and the mesher leaves non-manifold edges.
    public required float theta0_chim { get; init; }

    /* BASE VARIANT */
    public required float th_shelf { get; init; }

    /* MATERIAL */
    public required float rho { get; init; } // kg/m3.


    // printed over-length, the top is faced off after casting.
    public float L_print => L_puck + L_trim;

    public float R_grain => 0.5f*D_grain;
    public float R_port => 0.5f*D_port;
    public float R_skin => R_grain - th_skin; // skin inner face.
    public float A_skin => TWOPI*R_grain*L_puck;

    // annular envelope, the fill fraction's denominator.
    public float vol_envelope => envelope_over(L_print);
    public float vol_envelope_fin => envelope_over(L_puck);
    protected float envelope_over(float Lz)
        => Lz*PI*(sqed(R_grain) - sqed(R_port));


    public bool take_screenshots = false;
    public void set_modifiers(int mods) {
        take_screenshots = popbits(ref mods, TPIAP.TAKE_SCREENSHOTS);
        _                = popbits(ref mods, TPIAP.MINIMISE_MEM);
        _                = popbits(ref mods, TPIAP.LOOKIN_FANCY);
        assert(mods == 0, $"disallowed modifiers: 0x{mods:X}");
    }


    public void initialise() {
        assert(R_grain > R_skin, $"th_skin={th_skin}");
        assert(R_skin > R_port, $"D_port={D_port}, R_skin={R_skin}");
        assert(L_puck > 0f, $"L_puck={L_puck}");
        assert(L_trim >= 0f, $"L_trim={L_trim}");
        assert(th_gyr > 0f, $"th_gyr={th_gyr}");
        assert(th_anchor >= 0f, $"th_anchor={th_anchor}");
        assert(ramp_gyr >= 0f, $"ramp_gyr={ramp_gyr}");
        if (th_anchor > 0f)
            assert(ramp_gyr > 0f, "th_anchor set but ramp_gyr is 0");
        assert(blend_gyr >= 0f, $"blend_gyr={blend_gyr}");
        assert(fillet_skin >= 0f, $"fillet_skin={fillet_skin}");
        assert(th_shelf >= 0f && th_shelf < L_puck, $"th_shelf={th_shelf}");
        assert(no_chim >= 0, $"no_chim={no_chim}");

        // Chimneys have to clear the port and the skin.
        if (no_chim > 0) {
            float Ir_chim = r_chim - 0.5f*D_chim;
            float Or_chim = r_chim + 0.5f*D_chim;
            assert(Ir_chim > R_port, $"Ir_chim={Ir_chim}, R_port={R_port}");
            assert(Or_chim < R_skin, $"Or_chim={Or_chim}, R_skin={R_skin}");

            // only 2 and 4 put every bore on the same lattice cross section.
            if (no_chim != 2 && no_chim != 4) {
                print($"WARNING: no_chim={no_chim} is not 2 or 4, so the bores");
                print( "         do not sit on equivalent lattice sections and");
                print( "         some will be noticeably more obstructed.");
                print();
            }
        }

        assert_slits();
        assert_holes();

        // whole number of cells over the finished length.
        float no_cells = L_puck/Lz_cell;
        if (!nearto(no_cells, round(no_cells), rtol: 1e-3f)) {
            print($"WARNING: {no_cells:F3} cells per puck, not whole, so");
            print( "         the top and bottom faces will not match.");
            print();
        }

        // faces off the voxel grid round inward by up to a voxel.
        float no_vox = L_print/VOXEL_SIZE;
        if (!nearto(no_vox, round(no_vox), rtol: 1e-4f)) {
            print($"WARNING: {no_vox:F2} voxels over the printed length, not");
            print( "         whole, so the top edge will round in about twice");
            print( "         as far as the bottom. Pick a dividing voxel size.");
            print();
        }

        // thin features need about 3 voxels across.
        (float th, string what)[] thins = [
            (th_gyr, "gyroid sheet"),
            (th_skin, "OD skin"),
        ];
        foreach (var (th, what) in thins) {
            float across = th/VOXEL_SIZE;
            if (across < 3f) {
                print($"WARNING: only {across:F1} voxels across the {th}mm");
                print($"         {what}. It will not resolve properly.");
                print();
            }
        }
    }


    protected IImplicit gyroid() {
        Gyroid lattice = new(new Frame(new Vec3(x0_gyr, y0_gyr, z0_gyr)),
                th_gyr, L_cell, Lz_cell);
        if (th_anchor <= 0f || ramp_gyr <= 0f)
            return lattice;
        // anchor on the skin and end faces, not the port.
        Rod anchor = new(new(), L_print, R_skin);
        return new GradedGyroid(lattice, anchor, th_gyr, th_anchor, ramp_gyr);
    }

    protected Frame chimney(int i) {
        float theta = theta0_chim + i*TWOPI/no_chim;
        return new Frame(fromcyl(r_chim, theta, 0f));
    }


    /* CONSTRUCTION */

    public void make(bool based, PartMaker part) {
        float z0 = based ? th_shelf : 0f;

        Rod annulus = new(new(), L_print, R_port, R_grain);
        if (blend_gyr <= 0f) {
            part.voxels = (Voxels)annulus;
            part.voxels.IntersectImplicit(gyroid());
        } else {
            part.voxels = _SDF.voxels(
                    new SmoothIntersect(annulus, gyroid(), blend_gyr),
                    annulus.bounds);
        }
        part.substep("intersected gyroid with the annulus.", view_part: true);
        part.step("created scaffold.");

        if (fillet_skin <= 0f) {
            Voxels? skin = new Rod(new(), L_print, R_grain).shelled(-th_skin);
            part.add(ref skin);
            part.step("added OD skin.");
        } else {
            // smooth union, for the seam SmoothIntersect can't reach.
            Rod skin_shape = new Rod(new(), L_print, R_grain).shelled(-th_skin);
            Voxels? skin = _SDF.voxels(
                    new SmoothUnion(gyroid(), skin_shape, fillet_skin),
                    annulus.bounds);
            skin.IntersectImplicit(annulus);
            part.add(ref skin);
            part.step($"added OD skin with a {fillet_skin}mm fillet.");
        }

        if (no_chim > 0) {
            Voxels? chim = new();
            for (int i=0; i<no_chim; ++i)
                chim.BoolAdd(new Rod(chimney(i).transz(z0), L_print - z0,
                        0.5f*D_chim));
            part.sub(ref chim);
            part.step($"subtracted {no_chim} feed chimneys, {D_chim}mm.");
        } else {
            part.no_step("skipping feed chimneys (no_chim=0).");
        }

        // One OD perforation or the other, chosen by perf_mode.
        Voxels? perf = slits_on ? slits() : holes();
        if (perf is null) {
            part.no_step($"skipping OD perforation (perf_mode={perf_mode}, "
                    + "switched off).");
        } else if (slits_on) {
            part.sub(ref perf);
            part.step($"subtracted {no_perf} vent slits, {W_perf}mm wide "
                    + $"({frac_open:P2} of skin).");
        } else {
            part.sub(ref perf);
            part.step($"subtracted {no_hole} teardrop vent holes, "
                    + $"{D_hole}mm ({frac_open:P2} of skin).");
        }

        if (based) {
            Voxels? shelf = new Rod(new(), th_shelf, R_port, R_grain);
            part.add(ref shelf);
            part.step("added base shelf, port open through it.");
        }
    }


    // bounds are needed for screenshots.
    protected PartMaker part_maker(string variant) {
        PartMaker part = new(L_print, R_grain, 0.5f*L_print);
        if (!take_screenshots) {
            part.screenshotta = null;
        } else {
            BBox3 bbox = new(new Vec3(-R_grain, -R_grain, 0f),
                    new Vec3(R_grain, R_grain, L_print));
            part.screenshotta = new(new Geez.ViewAs(
                pos: bbox.vecCenter(),
                theta: torad(135f),
                phi: torad(105f),
                zoom: 1.5f*mag(bbox.vecSize()),
                orbit: true,
                bgcol: Geez.BACKGROUND_COLOUR_LIGHT
            ));
        }
        part.name = $"grain ({variant})";
        part.total_volume_to_calc_volumetric_fill = vol_envelope;
        part.density = rho;
        return part;
    }


    public Voxels? voxels() {
        using PartMaker part = part_maker("standard");
        make(based: false, part);
        return part.voxels;
    }


    public void anything() {
        // three standard pucks and one with the base shelf.
        emit("std", based: false);
        emit("base", based: true);
    }

    protected void emit(string variant, bool based) {
        Voxels vox;
        float vol_mm3;
        BBox3 bounds;
        int no_steps;

        using (PartMaker part = part_maker(variant)) {
            make(based, part);
            vox = part.voxels;
            no_steps = part.step_count;
            vox.CalculateProperties(out vol_mm3, out bounds);
        }

        // also measure the finished (faced off) part.
        float vol_fin_mm3 = vol_mm3;
        if (L_trim > 0f) {
            Voxels finished = vox.voxDuplicate();
            finished.IntersectImplicit(new Space(new(), -INF, L_puck));
            finished.CalculateProperties(out vol_fin_mm3, out _);
        }

        // separate screenshot names per variant.
        for (int i=1; take_screenshots && i<=no_steps; ++i)
            rename(fromroot($"exports/ss-{i}.tga"),
                    fromroot($"exports/ss-grain-{variant}-{i}.tga"));

        // stl named by a hash of its parameters, record saved beside it and in
        // config/grain.
        var record = record_of(variant, based, vol_mm3, vol_fin_mm3, bounds);
        string stem = $"grain-{variant}-{hash_of(record)}";

        Mesh mesh = new(vox);
        TPIAP.save_mesh_only(stem, mesh);
        Geez.mesh(mesh);
        save_record(stem, record);
    }


    /* PARAMETER RECORD */

    protected Dictionary<string, object> record_of(string variant, bool based,
            float vol_mm3, float vol_fin_mm3, in BBox3 bounds) {
        Vec3 size = bounds.vecSize();
        return new() {
            ["variant"] = variant,
            ["D_grain"] = D_grain,
            ["D_port"] = D_port,
            ["L_puck"] = L_puck,
            ["L_trim"] = L_trim,
            ["th_skin"] = th_skin,
            ["th_gyr"] = th_gyr,
            ["L_cell"] = L_cell,
            ["Lz_cell"] = Lz_cell,
            ["x0_gyr"] = x0_gyr,
            ["y0_gyr"] = y0_gyr,
            ["z0_gyr"] = z0_gyr,
            ["th_anchor"] = th_anchor,
            ["ramp_gyr"] = ramp_gyr,
            ["blend_gyr"] = blend_gyr,
            ["fillet_skin"] = fillet_skin,
            ["no_perf"] = no_perf,
            ["W_perf"] = W_perf,
            ["z0_perf"] = z0_perf,
            ["z1_perf"] = z1_perf,
            ["theta0_perf"] = theta0_perf,
            ["perf_mode"] = perf_mode,
            ["D_hole"] = D_hole,
            ["no_hole_ring"] = no_hole_ring,
            ["theta0_hole"] = theta0_hole,
            ["edge_hole"] = edge_hole,
            // derived, but belongs in the hash.
            ["no_hole"] = holes_on ? no_hole : 0,
            ["frac_perf"] = frac_open,
            ["no_chim"] = no_chim,
            ["D_chim"] = D_chim,
            ["r_chim"] = r_chim,
            ["theta0_chim"] = theta0_chim,
            ["th_shelf"] = based ? th_shelf : 0f,
            ["rho"] = rho,
            ["voxel_size"] = VOXEL_SIZE,
            // measured. out_ is as printed, fin_ is after facing off.
            ["out_volume_mL"] = vol_mm3*1e-3f,
            ["out_fill"] = vol_mm3/vol_envelope,
            ["out_mass_g"] = vol_mm3*1e-6f*rho,
            ["fin_volume_mL"] = vol_fin_mm3*1e-3f,
            ["fin_fill"] = vol_fin_mm3/vol_envelope_fin,
            ["fin_mass_g"] = vol_fin_mm3*1e-6f*rho,
            ["out_bounds_mm"] = new[] { size.X, size.Y, size.Z },
            // slicer profile, so a print traces back to its process too.
            ["slicer_profile"] = "",
        };
    }

    protected static string canonical(Dictionary<string, object> record)
        => JsonSerializer.Serialize(record, new JsonOptions {
            WriteIndented = true,
        });

    protected static string hash_of(Dictionary<string, object> record) {
        // hash the inputs only.
        Dictionary<string, object> inputs = new();
        foreach (var (key, value) in record) {
            if (key.StartsWith("out_") || key.StartsWith("fin_"))
                continue;
            inputs[key] = value;
        }
        byte[] text = System.Text.Encoding.UTF8.GetBytes(canonical(inputs));
        string digest = Convert.ToHexString(SHA256.HashData(text));
        return digest[..8].ToLowerInvariant();
    }

    protected static void save_record(string stem,
            Dictionary<string, object> record) {
        string text = canonical(record);
        foreach (string path in new[] {
            fromroot($"exports/{stem}.json"),
            fromroot($"../config/grain/{stem}.json"),
        }) {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
                print($"Saved config: '{path}'");
            } catch (Exception e) {
                print($"Failed to save config at '{path}'.");
                print("Exception log:");
                print(e);
            }
        }
        print();
    }

    protected static void rename(string from, string to) {
        if (!File.Exists(from))
            return;
        try {
            File.Move(from, to, overwrite: true);
            print($"Saved screenshot: '{to}'");
        } catch (Exception e) {
            print($"Failed to rename screenshot at '{from}'.");
            print("Exception log:");
            print(e);
        }
    }


    public Voxels? cutaway(in Voxels part) {
        // Quarter section, to eyeball the cells and the chimney bores.
        return Sectioner.pie(0f, PI_2).cut(part);
    }

    public void drawings(in Voxels part)
        => throw new NotImplementedException();
}
