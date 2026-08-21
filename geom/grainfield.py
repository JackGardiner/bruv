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
    # grading -- defaults reproduce a constant-thickness sheet.
    th_anchor: float = 0.0     # 0 => same as th_gyr, i.e. no grading
    ramp_gyr: float = 0.0
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
    names = sorted(n for n in os.listdir(d)
                   if n.startswith(f"grain-{variant}-") and n.endswith(".json"))
    if not names:
        raise FileNotFoundError(f"no grain-{variant}-*.json in {d}")
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

    @property
    def void(self):
        """Void inside the annular envelope only -- outside air is not void."""
        p = self.params
        env = (self.r >= p.R_port) & (self.r <= p.R_grain)
        return env & ~self.solid


def thickness_at(p, r, z):
    """Sheet thickness, thickened towards the skin and end faces."""
    t_mid = np.full(np.broadcast(r, z).shape, p.th_gyr, dtype=np.float64)
    if p.th_anchor <= 0.0:
        return t_mid
    frac = np.ones_like(t_mid)
    if p.ramp_gyr > 0:
        frac = np.minimum(frac, np.clip((p.R_skin - r) / p.ramp_gyr, 0.0, 1.0))
        frac = np.minimum(frac, np.clip(z / p.ramp_gyr, 0.0, 1.0))
        frac = np.minimum(
            frac, np.clip((p.L_print - z) / p.ramp_gyr, 0.0, 1.0))
    return p.th_anchor + (p.th_gyr - p.th_anchor) * frac


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

    if p.W_perf > 0 and p.no_perf > 0:
        z1 = p.z1_perf if p.z1_perf > 0 else p.L_print
        band = (Z >= p.z0_perf) & (Z <= z1)
        half = 0.5 * p.W_perf
        for i in range(p.no_perf):
            th = p.theta0_perf + i * TWOPI / p.no_perf
            # local frame: u along the slot's outward normal, v across it
            u = X * np.cos(th) + Y * np.sin(th)
            v = -X * np.sin(th) + Y * np.cos(th)
            slot = (np.abs(v) < half) & (u > 0) & (r >= p.R_skin) & band
            solid &= ~slot

    if based and p.th_shelf > 0:
        solid |= in_env & (Z < p.th_shelf)

    solid &= in_env
    return Field(solid=solid, dist=d, r=r, x=ax, y=ax, z=az, h=h, params=p)
