"""
Function cacher/inferrer.
"""

import contextlib
import itertools
import math
import warnings

import numpy as np

from .. import paths

__all__ = ["Cacher", "cls_Result", "cls_Fetcher"]


class Cacher:
    def __init__(self, name, spacing, function, obj_from_arr, obj_to_arr):
        self.name = name
        self.f = function
        self.obj_from_arr = obj_from_arr
        self.obj_to_arr = obj_to_arr
        self.spacing = spacing
        self._cache = {}
        self._changed = False

    def _path_cache(self):
        return paths.BIN_APPROXIMATOR / f"{self.name}.npz"
    def _path_lock(self):
        return paths.BIN_APPROXIMATOR / f"{self.name}.lock"
    def _lock(self):
        return paths.FileLock(self._path_lock())

    def _get(self, idx):
        if idx not in self._cache:
            params = np.array(idx) * self.spacing
            self._cache[idx] = self.f(*params)
            self._changed = True
        return self._cache[idx]

    def get_cached_proportion(self, *bounds):
        assert len(bounds) == len(self.spacing)
        ranges = []
        for i, (lo, hi) in enumerate(bounds):
            s = self.spacing[i]
            i_lo = math.floor(lo / s)
            i_hi = math.ceil(hi / s)
            ranges.append(range(i_lo, i_hi + 1))
        total = 1
        for r in ranges:
            total *= len(r)

        if total == 0:
            return 1.0

        cached = sum(
            1 for idx in itertools.product(*ranges)
            if tuple(idx) in self._cache
        )
        return cached / total

    def __call__(self, *params):
        # 3d interpolate.

        # Compute surrounding integer grid indices
        idx = np.array(list(params)) / self.spacing
        i0 = np.floor(idx).astype(int)
        i1 = i0 + 1
        t = idx - i0

        def interp(corner):
            dim = len(corner)
            if dim == len(params):
                return self._get(tuple(corner))
            idx0 = i0[dim] if t[dim] < 1-1e-8 else i1[dim]
            idx1 = i1[dim] if t[dim] > 1e-8 else i0[dim]
            c0 = interp(corner + [idx0])
            c1 = interp(corner + [idx1])
            return c0.lerp(c1, t[dim])
        return interp([])

    def __getitem__(self, name):
        def f(*params):
            return getattr(self(*params), name)
        f.__name__ = f"{self.name}.{name}"
        return np.vectorize(f)

    def load(self, _lock=True):
        with self._lock() if _lock else contextlib.nullcontext():
            if not self._path_cache().is_file():
                return
            data = np.load(str(self._path_cache()))
        spacing = data["spacing"]
        keys = data["keys"]
        values = data["values"]
        if not (spacing == self.spacing).all():
            warnings.warn(f"WARNING: spacing in {self.name} cache does not "
                           "match")
            return
        cache = {tuple(k): self.obj_from_arr(v)
                 for k, v in zip(keys, values)}
        self._cache = cache | self._cache

    def save(self):
        if not self._changed:
            print(f"{self.name} cache unchanged.")
            return
        print(f"{self.name} cache saving...", end="\r")
        keys = list(self._cache.keys())
        values = list(self.obj_to_arr(x) for x in self._cache.values())
        with self._lock():
            self.load(_lock=False)
            self._path_cache().parent.mkdir(parents=True, exist_ok=True)
            np.savez(self._path_cache(), allow_pickle=False,
                spacing=self.spacing,
                keys=np.array(keys, dtype=np.int32),
                values=np.array(values, dtype=np.float32),
            )
        self._changed = False
        print(f"{self.name} cache saved.   ")


def cls_Result(names, correction=lambda name, x: x):
    class Result:

        def __init__(self, data):
            self.data = data

        @classmethod
        def from_arr(cls, arr):
            return Result(arr)
        def to_arr(self):
            return self.data

        @classmethod
        def from_obj(cls, obj):
            data = np.empty(len(Result.NAMES), dtype=np.float32)
            for name, i in Result.MAPPING.items():
                data[i] = getattr(obj, name)
                data[i] = correction(name, data[i])
            return Result(data)
        def __getattr__(self, name):
            if name not in type(self).NAMES:
                return super().__getattribute__(name)
            return self.data[type(self).MAPPING[name]]

        def __repr__(self):
            maxlen = max(map(len, map(str, self.NAMES)))
            return "\n".join(f"{k:>{maxlen}}: {getattr(self, k)}"
                            for k in self.NAMES)

        def lerp(self, other, t):
            return Result(self.data + t*(other.data - self.data))

    Result.NAMES = list(names)
    Result.MAPPING = {n: i for i, n in enumerate(Result.NAMES)}

    return Result


def cls_Fetcher(get_cacher):
    class Fetcher:
        def __init__(self):
            self._cacher = None

        def need_configured(self):
            if self._cacher is None:
                raise RuntimeError("not configured (use .configure)")

        @contextlib.contextmanager
        def configure(self, *args, **kwargs):
            if self._cacher is not None:
                raise RuntimeError("already configured")
            self._cacher = get_cacher(*args, **kwargs)
            self._cacher.load()
            try:
                yield
            finally:
                self._cacher.save()
                self._cacher = None

        def get_cached_proportion(self, *bounds):
            self.need_configured()
            return self._cacher.get_cached_proportion(*bounds)
        def __call__(self, *args, **kwargs):
            self.need_configured()
            return self._cacher(*args, **kwargs)
        def __getitem__(self, name):
            self.need_configured()
            return self._cacher[name]

    # make singleton.
    Fetcher = Fetcher()
    def _Fetcher_new(*args, **kwargs):
        raise Exception("cannot create new instance of singleton")
    type(Fetcher).__new__ = _Fetcher_new
    return Fetcher
