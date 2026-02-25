"""
NASAS Chemical Equilibrium with Applications wrapper/cacher.
"""

import contextlib

import CEA_Wrap
import numpy as np

from .. import paths

__all__ = ["CEA_Result", "CEA"]


class CEA_Result:

    # All CEA_Wrap rocket result properties (note some units are changed by us to
    # be base si):
    NAMES = [
        # t_* prefix indicates property value at throat.
        # c_* prefix indicates property value at chamber.
        # no prefix indicates property value at exhaust.

        "p", # Pressure, Pa
        "t_p",
        "c_p",
        "t", # Temperature, Kelvin
        "t_t",
        "c_t",
        "h", # Enthalpy, J/kg
        "t_h",
        "c_h",
        "rho", # Density, kg/m^3
        "t_rho",
        "c_rho",
        "son", # Sonic velocity, m/s
        "t_son",
        "c_son",
        "visc", # Viscosity, Pa*s
        "t_visc",
        "c_visc",
        "cond", # Thermal conductivity, W/(m*K)
        "t_cond",
        "c_cond",
        "pran", # Prandtl number
        "t_pran",
        "c_pran",
        "mw", # Molecular weight of all products, kg/mol
        "t_mw",
        "c_mw",
        "cp", # Constant-pressure specific heat capacity, J/(kg*K)
        "t_cp",
        "c_cp",
        "gammas", # isentropic exponent (name from nasacea paper p1)
                  # isentropic ratio of specific heats (name from cea_wrap)
        "t_gammas",
        "c_gammas",
        "gamma", # Ratio of specific heats
        "t_gamma",
        "c_gamma",
        "dLV_dLP_t", # (dLV/dLP)_t
        "t_dLV_dLP_t",
        "c_dLV_dLP_t",
        "dLV_dLT_p", # (dLV/dLT)_p
        "t_dLV_dLT_p",
        "c_dLV_dLT_p",
        "isp", # Ideal ISP (ambient pressure = exit pressure), s
        "ivac", # Vacuum ISP, s
        "cf", # Ideally expanded thrust coefficient
        "cstar", # Characteristic velocity in chamber, m/s
        "mach", # Exhaust mach number
    ]
    def __init__(self, data):
        self.data = data
    @classmethod
    def from_cea(cls, cea):
        data = np.empty(len(cls.NAMES), dtype=np.float32)
        for name, i in cls.MAPPING.items():
            # fix some stupid non-si values.
            data[i] = getattr(cea, name)
            if name in {"p", "t_p", "c_p"}:
                data[i] *= 1e5 # bar -> Pa
            elif name in {"h", "t_h", "c_h"}:
                data[i] *= 1e3 # kJ/kg -> J/kg
            elif name in {"mw", "t_mw", "c_mw"}:
                data[i] *= 1e-3 # kg/kmol -> kg/mol
            elif name in {"cp", "t_cp", "c_cp"}:
                data[i] *= 1e3 # kJ/(kg*K) -> J/(kg*K)
        return cls(data)
    def __getattr__(self, name):
        if name not in type(self).NAMES:
            return super().__getattribute__(name)
        return self.data[type(self).MAPPING[name]]
    def __repr__(self):
        maxlen = max(map(len, map(str, self.NAMES)))
        return "\n".join(f"{k:>{maxlen}}: {getattr(self, k)}"
                         for k in self.NAMES)
CEA_Result.MAPPING = {n: i for i, n in enumerate(CEA_Result.NAMES)}



class CEA:
    """
    CEA wrapping object (singleton). Evaluates NASA CEA when a fuel has been
    selected via `with CEA.configure(oxid, fuel):`.
    """
    def __init__(self):
        self._problem = None
        self._oxid = None
        self._fuel = None
        self._cache = {}
        self._changed = False

    def _path_cache(self):
        assert self._oxid is not None
        assert self._fuel is not None
        return paths.BIN_APPROXIMATOR / f"cea_{self._oxid}_{self._fuel}.npz"
    def _path_lock(self):
        assert self._fuel is not None
        assert self._oxid is not None
        return paths.BIN_APPROXIMATOR / f"cea_{self._oxid}_{self._fuel}.lock"


    @contextlib.contextmanager
    def configure(self, oxid, fuel):
        oxid = oxid.strip().casefold()
        fuel = fuel.strip().casefold()

        obj_oxid = None
        obj_fuel = None

        if oxid == "lox":
            # lox properties from default input cards of RocketCEA.
            comp = CEA_Wrap.ChemicalRepresentation(
                " O 2", # need leading space to fix CEA_Wrap bug lmao.
                hf=-3.102, hf_unit="kc",
            )
            obj_oxid = CEA_Wrap.Oxidizer(
                "LOX", chemical_representation=comp,
                temp=90.18,
            )
        else:
            raise KeyError(f"unrecognised oxidiser: {repr(oxid)}")

        if fuel == "ipa":
            comp = CEA_Wrap.ChemicalRepresentation(
                " C 3 H 8 O 1",
                hf=-65.133, hf_unit="kc",
            )
            obj_fuel = CEA_Wrap.Fuel(
                "IPA", chemical_representation=comp,
                temp=298.15
            )
        else:
            raise KeyError(f"unrecognised fuel: {repr(fuel)}")


        self._problem = CEA_Wrap.RocketProblem(materials=[obj_fuel, obj_oxid],
                # Dummy initial params.
                pressure=30, pressure_units="bar", o_f=5, ae_at=5,
            )
        self._oxid = oxid
        self._fuel = fuel
        self._cache = {}
        self.load()
        try:
            yield
        finally:
            self.save()
            self._problem = None
            self._oxid = None
            self._fuel = None
            self._cache = {}

    def _keyof(self, P, ofr, eps):
        P = round(float(P), 4)
        ofr = round(float(ofr), 3)
        eps = round(float(eps), 3)
        return P, ofr, eps

    def __call__(self, P, ofr, eps):
        """
        Returns the CEA_Result of the given state. May only be called when
        configured with an oxidiser and fuel.
        - Expects P in MPa.
        """
        if self._problem is None:
            raise RuntimeError("CEA is not configured (use .configure)")
        key = self._keyof(P, ofr, eps)
        if key not in self._cache:
            P, ofr, eps = key
            self._problem.set_pressure(P * 10) # mpa -> bar
            self._problem.set_o_f(ofr)
            self._problem.set_ae_at(eps)
            print("running problem", P, ofr, eps)
            cea = self._problem.run()
            self._cache[key] = CEA_Result.from_cea(cea)
            self._changed = True
        return self._cache[key]

    def __getitem__(self, name):
        """
        Returns a numpy vectorised function which gets the given property and
        accepts `P, ofr, eps` as arguments. May only be called when configured
        with an oxidiser and fuel.
        - Expects P in MPa.
        """
        def f(P, ofr, eps):
            return getattr(self(P, ofr, eps), name)
        f.__name__ = f"cea.{name}"
        return np.vectorize(f)

    def load(self, _lock=True):
        filelock = contextlib.nullcontext()
        if _lock:
            filelock = paths.FileLock(self._path_lock())
        with filelock:
            if self._path_cache().is_file():
                data = np.load(str(self._path_cache()))
                keys = data["keys"]
                values = data["values"]
                cache = {self._keyof(*k): CEA_Result(v)
                         for k, v in zip(keys, values)}
                self._cache = cache | self._cache
        self._changed = False

    def save(self):
        if not self._changed:
            print("CEA cache unchanged.")
            return
        with paths.FileLock(self._path_lock()):
            print("CEA cache saving...", end="\r")
            self.load(_lock=False)
            keys = list(self._cache.keys())
            values = list(x.data for x in self._cache.values())
            self._path_cache().parent.mkdir(parents=True, exist_ok=True)
            np.savez(self._path_cache(), allow_pickle=False,
                keys=np.array(keys, dtype=np.float32),
                values=np.array(values, dtype=np.float32),
            )
            print("CEA cache saved.   ")

# make singleton.
CEA = CEA()
def _CEA_new(*args, **kwargs):
    raise Exception("cannot create new instance of singleton")
type(CEA).__new__ = _CEA_new
