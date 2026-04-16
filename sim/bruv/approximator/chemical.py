"""
thermo.Chemical lookups.
"""

import contextlib

import numpy as np
import thermo

from . import cacher
from .. import paths

__all__ = ["IPA_Properties", "IPA"]


IPA_Properties = cacher.cls_Result([
    "Psat", # Saturation pressure, Pa
    "rho",  # Mass density, kg/m^3
    "rhom", # Molar mass, mol/m^3
    "Cp",   # Mass heat capacity, J/(kg*K)
    "U",    # Internal energy, J/kg
    "mu",   # Dynamic viscosity, Pa*s
    "k",    # Thermal conductivity, W/(m*K)
    "Pr",   # Prandtl number, -
    "Z",    # Compressibility factor, -
])


@cacher.cls_Fetcher
def IPA():

    spacing_T = 0.5
    spacing_P = 0.005
    spacing = np.array([spacing_T, spacing_P])

    def f(T, P):
        chem = thermo.Chemical("isopropanol", T, P*1e6)
        return IPA_Properties.from_obj(chem)

    return cacher.Cacher(
        "IPA",
        spacing,
        f,
        IPA_Properties.from_arr,
        IPA_Properties.to_arr
    )
