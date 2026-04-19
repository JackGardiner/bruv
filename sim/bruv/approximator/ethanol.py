"""
thermo.Chemical isopropanol lookups.
"""

import contextlib

import CoolProp.CoolProp as CoolProp
import numpy as np

from . import cacher
from .. import paths

__all__ = ["Ethanol_Properties", "Ethanol"]


Ethanol_Properties = cacher.cls_Result([
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
def Ethanol():

    spacing_T = 0.5
    spacing_P = 0.005
    spacing = np.array([spacing_T, spacing_P])

    def f(T, P):
        data = [
            CoolProp.PropsSI("P", "T", T, "Q", 0, "Ethanol"),
            CoolProp.PropsSI("D", "T", T, "P", P*1e6, "Ethanol"),
            CoolProp.PropsSI("Dmolar", "T", T, "P", P*1e6, "Ethanol"),
            CoolProp.PropsSI("C", "T", T, "P", P*1e6, "Ethanol"),
            CoolProp.PropsSI("U", "T", T, "P", P*1e6, "Ethanol"),
            CoolProp.PropsSI("V", "T", T, "P", P*1e6, "Ethanol"),
            CoolProp.PropsSI("L", "T", T, "P", P*1e6, "Ethanol"),
            CoolProp.PropsSI("PRANDTL", "T", T, "P", P*1e6, "Ethanol"),
            CoolProp.PropsSI("Z", "T", T, "P", P*1e6, "Ethanol"),
        ]
        return Ethanol_Properties.from_arr(np.array(data))

    return cacher.Cacher(
        "Ethanol",
        spacing,
        f,
        Ethanol_Properties.from_arr,
        Ethanol_Properties.to_arr,
        Ethanol_Properties.NAN
    )
