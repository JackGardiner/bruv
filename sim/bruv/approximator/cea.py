"""
NASA Chemical Equilibrium with Applications lookups.
"""

import contextlib

import CEA_Wrap
import numpy as np

from . import cacher
from .. import paths

__all__ = ["CEA_Result", "CEA"]


def cea_result_correction(name, x):
    factor = 1.0
    if name in {"p", "t_p", "c_p"}:
        factor = 1e5 # bar -> Pa
    elif name in {"h", "t_h", "c_h"}:
        factor = 1e3 # kJ/kg -> J/kg
    elif name in {"mw", "t_mw", "c_mw"}:
        factor = 1e-3 # kg/kmol -> kg/mol
    elif name in {"cp", "t_cp", "c_cp"}:
        factor = 1e3 # kJ/(kg*K) -> J/(kg*K)
    return factor * x

CEA_Result = cacher.cls_Result([
    # All CEA_Wrap rocket result properties (note some units are changed by us to
    # be base si).
    #   t_* prefix indicates property value at throat.
    #   c_* prefix indicates property value at chamber.
    #   no prefix indicates property value at exhaust.

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
], correction=cea_result_correction)


@cacher.cls_Fetcher
def CEA(oxid, fuel):
    oxid = oxid.strip().casefold()
    fuel = fuel.strip().casefold()

    spacing_P = 0.005
    spacing_ofr = 0.02
    spacing_AEAT = 0.02
    spacing = np.array([spacing_P, spacing_ofr, spacing_AEAT])

    mat_oxid = None
    mat_fuel = None

    if oxid == "lox":
        oxid = "LOx"
        # lox properties from default input cards of RocketCEA.
        comp = CEA_Wrap.ChemicalRepresentation(
            " O 2", # need leading space to fix CEA_Wrap bug lmao.
            hf=-3.102, hf_unit="kc",
        )
        mat_oxid = CEA_Wrap.Oxidizer(
            "LOX", chemical_representation=comp,
            temp=90.18,
        )
    else:
        raise KeyError(f"unrecognised oxidiser: {repr(oxid)}")

    if fuel == "ipa":
        fuel = "IPA"
        comp = CEA_Wrap.ChemicalRepresentation(
            " C 3 H 8 O 1",
            hf=-65.133, hf_unit="kc",
        )
        mat_fuel = CEA_Wrap.Fuel(
            "IPA", chemical_representation=comp,
            temp=298.15
        )
    else:
        raise KeyError(f"unrecognised fuel: {repr(fuel)}")

    problem = CEA_Wrap.RocketProblem(materials=[mat_fuel, mat_oxid],
            # Dummy initial params.
            pressure=30, pressure_units="bar", o_f=5, ae_at=5,
        )
    def f(P, ofr, AEAT):
        problem.set_pressure(P * 10) # mpa -> bar
        problem.set_o_f(ofr)
        problem.set_ae_at(AEAT)
        cea = problem.run()
        return CEA_Result.from_obj(cea)

    return cacher.Cacher(
        f"CEA-{oxid}-{fuel}",
        spacing,
        f,
        CEA_Result.from_arr,
        CEA_Result.to_arr
    )
