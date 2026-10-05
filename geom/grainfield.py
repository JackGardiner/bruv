"""Parameters -> sampled implicit field for the gyroid grain scaffold."""

import dataclasses
import json
import os

import numpy as np

TWOPI = 2.0 * np.pi
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


@dataclasses.dataclass(frozen=True)
class GrainParams:
    # envelope
    D_grain: float
    D_port: float
    L_puck: float
    L_trim: float
    th_skin: float
    # gyroid
    th_gyr: float
    L_cell: float
    Lz_cell: float
    x0_gyr: float
    y0_gyr: float
    z0_gyr: float
    # axial grading towards the end faces, 0 for none
    th_anchor: float = 0.0     # 0 => no axial grading
    ramp_gyr: float = 0.0
    # radial taper, thin at the ID, thick at the OD. 0 uses th_gyr at that end
    th_id: float = 0.0
    th_od: float = 0.0
    expo_gyr: float = 1.0
    # carried for the record only, tier 1 doesn't model these
    blend_gyr: float = 0.0
    # chimneys
    no_chim: int = 0
    D_chim: float = 0.0
    r_chim: float = 0.0
    theta0_chim: float = 0.0
    # slits -- W_perf = 0 disables
    no_perf: int = 4
    W_perf: float = 0.0
    z0_perf: float = 0.0
    z1_perf: float = 0.0       # 0 => full height
    theta0_perf: float = 0.0
    # "slits" or "holes", only that one is cut
    perf_mode: str = "slits"
    # holes, D_hole = 0 for none
    D_hole: float = 0.0        # circle diameter; >= MIN_D_HOLE when on
    no_hole_ring: int = 22     # holes per ring; == 2 mod 4, see hole_grid()
    theta0_hole: float = 0.0   # grid phase, rad
    edge_hole: float = 3.0     # min distance from a hole to either puck end
    # base variant
    th_shelf: float = 0.0
    rho: float = 1040.0

    @property
    def R_grain(self): return 0.5 * self.D_grain

    @property
    def R_port(self): return 0.5 * self.D_port

    @property
    def R_skin(self): return self.R_grain - self.th_skin

    @property
    def L_print(self): return self.L_puck + self.L_trim

    def replace(self, **kw):
        return dataclasses.replace(self, **kw)

    @classmethod
    def from_config(cls, path=None):
        path = path or os.path.join(ROOT, "config", "all.json")
        with open(path) as fh:
            cfg = json.load(fh)["grain"]
        amend = os.path.join(os.path.dirname(path), "ammendments.json")
        if os.path.exists(amend):
            with open(amend) as fh:
                cfg.update(json.load(fh).get("grain", {}))
        known = {f.name for f in dataclasses.fields(cls)}
        return cls(**{k: v for k, v in cfg.items() if k in known})


def load_record(variant):
    """Most recent emitted parameter record for a variant."""
    d = os.path.join(ROOT, "config", "grain")
    names = [n for n in os.listdir(d)
              if n.startswith(f"grain-{variant}-") and n.endswith(".json")]
    if not names:
        raise FileNotFoundError(f"no grain-{variant}-*.json in {d}")
    names.sort(key=lambda n: os.path.getmtime(os.path.join(d, n)))
    with open(os.path.join(d, names[-1])) as fh:
        return json.load(fh)


@dataclasses.dataclass
class Field:
    solid: np.ndarray     # bool (nx, ny, nz)
    dist: np.ndarray      # float32, signed distance to the sheet mid-surface
    r: np.ndarray         # float32 radius
    x: np.ndarray
    y: np.ndarray
    z: np.ndarray
    h: float
    params: GrainParams
    # local sheet thickness, same grid as dist
    t: np.ndarray = None

    @property
    def thickness(self):
        if self.t is not None:
            return self.t
        return thickness_at(self.params, self.r, self.z).astype(np.float32)

    @property
    def dist_solid(self):
        """Signed distance to the SOLID boundary, not the sheet mid-surface."""
        return np.abs(self.dist) - 0.5 * self.thickness

    @property
    def void(self):
        """Void inside the annular envelope only -- outside air is not void."""
        p = self.params
        env = (self.r >= p.R_port) & (self.r <= p.R_grain)
        return env & ~self.solid


def radial_thickness(p, r):
    """Sheet thickness as a function of radius alone -- the taper."""
    if p.th_id <= 0.0 and p.th_od <= 0.0:
        return np.full(np.shape(r), p.th_gyr, dtype=np.float64)
    t_id = p.th_id if p.th_id > 0.0 else p.th_gyr
    t_od = p.th_od if p.th_od > 0.0 else p.th_gyr
    span = max(p.R_skin - p.R_port, 1e-9)
    u = np.clip((np.asarray(r, dtype=np.float64) - p.R_port) / span, 0.0, 1.0)
    return t_id + (t_od - t_id) * u ** p.expo_gyr


def thickness_at(p, r, z):
    """Sheet thickness field: the radial taper, then the axial anchor."""
    shape = np.broadcast(r, z).shape
    t = np.broadcast_to(radial_thickness(p, r), shape).astype(np.float64)
    if p.th_anchor <= 0.0 or p.ramp_gyr <= 0.0:
        return t
    depth = np.minimum(np.asarray(z, dtype=np.float64), p.L_print - z)
    frac = np.clip(depth / p.ramp_gyr, 0.0, 1.0)
    return p.th_anchor + (t - p.th_anchor) * frac


def gyroid_distance(p, x, y, z):
    """First-order distance to the gyroid mid-surface."""
    wx = wy = TWOPI / p.L_cell
    wz = TWOPI / p.Lz_cell
    qx, qy, qz = (x - p.x0_gyr) * wx, (y - p.y0_gyr) * wy, (z - p.z0_gyr) * wz
    sx, sy, sz = np.sin(qx), np.sin(qy), np.sin(qz)
    cx, cy, cz = np.cos(qx), np.cos(qy), np.cos(qz)
    f = sx * cy + sy * cz + sz * cx
    gx = wx * (cx * cy - sz * sx)
    gy = wy * (cy * cz - sx * sy)
    gz = wz * (cz * cx - sy * sz)
    return f / np.maximum(np.sqrt(gx * gx + gy * gy + gz * gz), 1e-9)


# ------------------------------------------------------------- OD holes

MIN_D_HOLE = 4.0                  # mm, owner's floor -- bubbles must pass
SQRT2 = np.sqrt(2.0)


def teardrop_inside(u, v, a):
    """Circle of radius a plus a 45 degree pointed cap on top."""
    circle = u * u + v * v <= a * a
    kite = (np.abs(u) <= v) & (np.abs(u) + v <= a * SQRT2)
    return circle | kite


def teardrop_area(a):
    """Circle + kite - overlap."""
    return a * a * (0.75 * np.pi + 1.0)


def hole_row_pitch(p):
    """Rows are a quarter cell apart, the z half of the lattice's screw."""
    return 0.25 * p.Lz_cell


def hole_grid(p):
    """Every hole centre, (theta, z)."""
    if p.D_hole < MIN_D_HOLE:
        raise ValueError(f"D_hole={p.D_hole}mm is below the {MIN_D_HOLE}mm "
                         "floor (fuel_grain_holes.cs asserts the same)")
    a = 0.5 * p.D_hole
    n_t = int(p.no_hole_ring)
    dth = TWOPI / n_t
    pitch = hole_row_pitch(p)
    z_lo = p.edge_hole + a
    z_hi = p.L_puck - p.edge_hole - a * SQRT2
    if z_hi < z_lo:
        return np.zeros(0), np.zeros(0)
    n_z = int(np.floor((z_hi - z_lo) / pitch)) + 1
    z_first = z_lo + 0.5 * ((z_hi - z_lo) - (n_z - 1) * pitch)
    th, zc = [], []
    for k in range(n_z):
        off = 0.5 * dth if k % 2 else 0.0
        for j in range(n_t):
            th.append(p.theta0_hole + j * dth + off)
            zc.append(z_first + k * pitch)
    return np.array(th), np.array(zc)


def hole_sites(p):
    """The holes actually cut -- every point of the grid."""
    return hole_grid(p)


def holes_on(p):
    return p.perf_mode == "holes" and p.D_hole > 0


def slits_on(p):
    return p.perf_mode == "slits" and p.W_perf > 0 and p.no_perf > 0


def frac_perf(p):
    """Open fraction of the skin over the finished length, either mode."""
    A_skin = TWOPI * p.R_grain * p.L_puck
    if holes_on(p):
        return len(hole_sites(p)[0]) * teardrop_area(0.5 * p.D_hole) / A_skin
    if slits_on(p):
        z1 = p.z1_perf if p.z1_perf > 0 else p.L_puck
        return p.no_perf * p.W_perf * (z1 - p.z0_perf) / A_skin
    return 0.0


def sample(p, h, based=False):
    """Sample the whole part on a regular grid of spacing h."""
    R = p.R_grain
    n_xy = int(np.ceil(2 * R / h))
    n_z = int(np.ceil(p.L_print / h))
    ax = (np.arange(n_xy) + 0.5) * h - R
    az = (np.arange(n_z) + 0.5) * h
    X, Y, Z = np.meshgrid(ax, ax, az, indexing="ij")
    r = np.sqrt(X * X + Y * Y).astype(np.float32)

    d = gyroid_distance(p, X, Y, Z).astype(np.float32)
    t = thickness_at(p, r, Z).astype(np.float32)

    in_env = (r >= p.R_port) & (r <= p.R_grain)
    lattice = (np.abs(d) < 0.5 * t) & (r < p.R_skin) & in_env
    skin = (r >= p.R_skin) & (r <= p.R_grain)
    solid = lattice | skin

    if p.no_chim > 0 and p.D_chim > 0:
        z_floor = p.th_shelf if based else 0.0
        for i in range(p.no_chim):
            th = p.theta0_chim + i * TWOPI / p.no_chim
            cx, cy = p.r_chim * np.cos(th), p.r_chim * np.sin(th)
            bore = ((X - cx) ** 2 + (Y - cy) ** 2
                    < (0.5 * p.D_chim) ** 2) & (Z >= z_floor)
            solid &= ~bore

    if slits_on(p):
        # full height is L_puck, same as fuel_grain_slits.cs
        z1 = p.z1_perf if p.z1_perf > 0 else p.L_puck
        band = (Z >= p.z0_perf) & (Z <= z1)
        half = 0.5 * p.W_perf
        for i in range(p.no_perf):
            th = p.theta0_perf + i * TWOPI / p.no_perf
            # local frame: u along the slot's outward normal, v across it
            u = X * np.cos(th) + Y * np.sin(th)
            v = -X * np.sin(th) + Y * np.cos(th)
            slot = (np.abs(v) < half) & (u > 0) & (r >= p.R_skin) & band
            solid &= ~slot

    if holes_on(p):
        # skin only, and only the bounding box round each hole
        a = 0.5 * p.D_hole
        n_xy, n_z = X.shape[0], X.shape[2]
        for th, zc in zip(*hole_sites(p)):
            c, s_ = np.cos(th), np.sin(th)
            cx, cy = p.R_grain * c, p.R_grain * s_
            reach = a * SQRT2 + p.th_skin + 2 * h
            ix = slice(max(0, int((cx - reach + R) / h)),
                       min(n_xy, int((cx + reach + R) / h) + 2))
            iy = slice(max(0, int((cy - reach + R) / h)),
                       min(n_xy, int((cy + reach + R) / h) + 2))
            iz = slice(max(0, int((zc - a) / h) - 1),
                       min(n_z, int((zc + a * SQRT2) / h) + 2))
            xs, ys, zs = X[ix, iy, iz], Y[ix, iy, iz], Z[ix, iy, iz]
            u = -xs * s_ + ys * c
            rad = xs * c + ys * s_
            cut = (teardrop_inside(u, zs - zc, a) & (rad > 0)
                   & (r[ix, iy, iz] >= p.R_skin))
            solid[ix, iy, iz] &= ~cut

    if based and p.th_shelf > 0:
        solid |= in_env & (Z < p.th_shelf)

    solid &= in_env
    return Field(solid=solid, dist=d, t=t, r=r, x=ax, y=ax, z=az, h=h,
                 params=p)
