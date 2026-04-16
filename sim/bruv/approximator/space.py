"""
Array/grid creation functions.
"""

import matplotlib.tri
import numpy as np

__all__ = ["concentrate", "Masker", "Evenspace", "Surfspace"]


def concentrate(X, c, s):
    # https://www.desmos.com/calculator/nncnokstzq
    lo = X[0]
    hi = X[-1]
    assert lo <= hi
    if c is None:
        return X
    elif c == "hi":
        c = hi
    elif c == "lo":
        c = lo
    elif c == "mid":
        c = (lo + hi)/2
    assert lo <= c and c <= hi
    assert -1.0 <= s and s <= 1.0
    X = (X - lo) / (hi - lo)
    c = (c - lo) / (hi - lo)
    p = 1+s if s <= 0 else 1/(1-s)
    mask = (X <= c)
    if c == 1.0:
        X = c - c*((c-X)/c)**p
    elif c == 0.0:
        X = c + (1-c)*((X-c)/(1-c))**p
    else:
        with np.errstate(invalid="ignore"):
            X = np.where((X <= c)
                    , c - c*((c-X)/c)**p
                    , c + (1-c)*((X-c)/(1-c))**p
                )
    return X*(hi - lo) + lo


class Masker:
    def __init__(self, f):
        self.f = f

    def points(self, P):
        return self.f(*np.moveaxis(P, -1, 0))
    def coords(self, *C):
        return self.f(*C)
    def triang(self, triang):
        x0 = triang.x[triang.triangles[:, 0]]
        x1 = triang.x[triang.triangles[:, 1]]
        x2 = triang.x[triang.triangles[:, 2]]
        y0 = triang.y[triang.triangles[:, 0]]
        y1 = triang.y[triang.triangles[:, 1]]
        y2 = triang.y[triang.triangles[:, 2]]
        xc = (x0 + x1 + x2) / 3.0
        yc = (y0 + y1 + y2) / 3.0
        mask = self.f(xc, yc)
        mask &= self.f(x0, y0)
        mask &= self.f(x1, y1)
        mask &= self.f(x2, y2)
        triang.set_mask(~mask)


class Evenspace:
    def __init__(self, xlo, xhi, ylo=None, yhi=None):
        assert (ylo is None) == (yhi is None)
        self.ndim = 1 if yhi is None else 2
        self.xlo = float(xlo)
        self.xhi = float(xhi)
        self.ylo = float(ylo) if ylo is not None else None
        self.yhi = float(yhi) if yhi is not None else None

    def points(self, N, flatten=True):
        if self.ndim == 1:
            return np.linspace(self.xlo, self.xhi, N)
        n = np.sqrt(N)
        assert np.allclose(n, int(n))
        X = np.linspace(self.xlo, self.xhi, int(n))
        Y = np.linspace(self.ylo, self.yhi, int(n))
        X, Y = np.meshgrid(X, Y)
        if flatten:
            X = X.ravel()
            Y = Y.ravel()
        return X, Y

    def bounds(self):
        b = (self.xlo, self.xhi)
        if self.ndim == 1:
            return b
        return b + (self.ylo, self.yhi)


class Surfspace:
    def __init__(self, f, xlo, xhi, ylo=None, yhi=None, *, N0=None, masker=None):
        assert (ylo is None) == (yhi is None)
        self.ndim = 1 if yhi is None else 2
        self.f = f
        self.xlo = float(xlo)
        self.xhi = float(xhi)
        self.ylo = float(ylo) if ylo is not None else None
        self.yhi = float(yhi) if yhi is not None else None
        self.masker = masker

        if N0 is None:
            N0 = 100 if self.ndim == 2 else 2000

        # Compute some thing for 1D points.
        if self.ndim == 1:
            assert self.masker is None
            X0 = np.linspace(self.xlo, self.xhi, N0)
            Y0 = f(X0)
            xspan = self.xhi - self.xlo
            yspan = Y0.max() - Y0.min()
            scale = xspan / yspan if yspan != 0.0 else 1.0
            dy_dx = np.gradient(Y0, X0)
            integrand = np.sqrt(1 + (scale * dy_dx)**2)
            S0 = np.zeros_like(X0)
            S0[1:] = np.cumsum(0.5*(integrand[1:] + integrand[:-1])*np.diff(X0))

            self.X0 = X0
            self.S0 = S0
            return

        # Compute some thing for 2D points.
        self._rng = np.random.default_rng(1)

        Xf = np.linspace(self.xlo, self.xhi, N0)
        Yf = np.linspace(self.ylo, self.yhi, N0)
        Xf, Yf = np.meshgrid(Xf, Yf)
        Xf = Xf.ravel()
        Yf = Yf.ravel()
        if masker is not None:
            mask = masker.coords(Xf, Yf)
            Xf = Xf[mask]
            Yf = Yf[mask]
        Zf = f(Xf, Yf)

        # Find z range.
        self.zhi = Zf.max()
        self.zlo = Zf.min()

        # Find surface area, and therefore minimum spacing.
        def surface_area(X, Y, Z):
            triang = matplotlib.tri.Triangulation(
                X * (self.xhi - self.xlo) + self.xlo,
                Y * (self.yhi - self.ylo) + self.ylo,
            )
            if masker is not None:
                masker.triang(triang)

            tris = triang.triangles
            if triang.mask is not None:
                tris = tris[~triang.mask]

            # Vertex positions in 3D.
            P0 = np.stack([X[tris[:, 0]], Y[tris[:, 0]], Z[tris[:, 0]]], axis=-1)
            P1 = np.stack([X[tris[:, 1]], Y[tris[:, 1]], Z[tris[:, 1]]], axis=-1)
            P2 = np.stack([X[tris[:, 2]], Y[tris[:, 2]], Z[tris[:, 2]]], axis=-1)

            # Triangle edges.
            E1 = P1 - P0
            E2 = P2 - P0

            # Cross product (triangle normal * 2 area).
            cross = np.cross(E1, E2)

            # True triangle area in 3D.
            area = 0.5 * np.linalg.norm(cross, axis=-1)

            # Extract normal components.
            nx, ny, nz = cross[:, 0], cross[:, 1], cross[:, 2]

            # Weight from gradient.
            grad = np.sqrt(nx**2 + ny**2) / (np.abs(nz) + 1e-8)

            # Apply weighting.
            area *= 1.0 + np.sqrt(grad)

            return np.sum(area)

        self._surface_area = surface_area(
            (Xf - self.xlo) / (self.xhi - self.xlo),
            (Yf - self.ylo) / (self.yhi - self.ylo),
            (Zf - self.zlo) / (self.zhi - self.zlo),
        )


    def points(self, N, **kwargs):
        return self._points2D(N, **kwargs) if self.ndim == 2 else \
               self._points1D(N, **kwargs)

    def bounds(self):
        b = (self.xlo, self.xhi)
        if self.ndim == 1:
            return b
        return b + (self.ylo, self.yhi)

    def _points1D(self, N):
        assert self.ndim == 1
        S = np.linspace(self.S0[0], self.S0[-1], N)
        X = np.interp(S, self.S0, self.X0)
        return X, self.f(X)

    def _points2D(self, N, *, spacing=1.2, batch_size=20):
        assert self.ndim == 2

        min_dist = np.sqrt(self._surface_area / N / np.pi) * spacing

        # Add an explicit border.
        n = int(0.16*N)
        n -= (n % 4)
        assert n >= 4
        side = np.linspace(0.0, 1.0, n//4 + 1)[:-1]
        points = np.empty(shape=(n, 3))
        points[:n//4, 0] = side
        points[:n//4, 1] = 0.0
        points[n//4:n//2, 0] = 1.0
        points[n//4:n//2, 1] = side
        points[n//2:-n//4, 0] = 1.0 - side
        points[n//2:-n//4, 1] = 1.0
        points[-n//4:, 0] = 0.0
        points[-n//4:, 1] = 1.0 - side

        x = points[:, 0] * (self.xhi - self.xlo) + self.xlo
        y = points[:, 1] * (self.yhi - self.ylo) + self.ylo
        z = self.f(x, y)
        w = (z - self.zlo) / (self.zhi - self.zlo)
        points[:, 2] = w

        def mask(P):
            assert len(P.shape) == 2 and P.shape[1] == 3
            if self.masker is None:
                return P
            X = P[:, 0]*(self.xhi - self.xlo) + self.xlo
            Y = P[:, 1]*(self.yhi - self.ylo) + self.ylo
            return P[self.masker.coords(X, Y)]

        points = mask(points)

        while points.shape[0] < N:
            s = f"{points.shape[0]:<{len(str(N))}}/{N}"
            print(s, end="\r")

            # Randomly sample.
            u, v = self._rng.random((2, batch_size))
            x = u * (self.xhi - self.xlo) + self.xlo
            y = v * (self.yhi - self.ylo) + self.ylo
            z = self.f(x, y)
            w = (z - self.zlo) / (self.zhi - self.zlo)
            batch = np.vstack([u, v, w]).T
            batch = mask(batch)
            if points.shape[0] > 0:
                # Ensure its not close to existing points.
                diff = points[np.newaxis, :, :] - batch[:, np.newaxis, :]
                # diff is (batch points, existing points, coord)
                dist_sq = np.sum(diff**2, axis=-1)
                # scale by gradient.
                dx, dy, dz = diff[:, :, 0], diff[:, :, 1], diff[:, :, 2]
                grad = np.sqrt(dx**2 + dy**2) / (np.abs(dz) + 1e-8)
                dist_sq *= 1.0 + 1.0/(np.sqrt(grad) + 1e-8)
                batch = batch[np.min(dist_sq, axis=-1) >= min_dist**2]

            if batch.shape[0] > 0:
                points = np.vstack([points, batch[0, :]])

        print(" "*len(s), end="\r")

        points = points[:N]
        U = points[:, 0]
        V = points[:, 1]
        W = points[:, 2]
        X = U*(self.xhi - self.xlo) + self.xlo
        Y = V*(self.yhi - self.ylo) + self.ylo
        Z = W*(self.zhi - self.zlo) + self.zlo
        return X, Y, Z
