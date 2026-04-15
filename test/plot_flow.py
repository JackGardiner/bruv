import numpy as np
import glob
import os
import pandas as pd
import matplotlib.pyplot as plt

# Path to CSV
file_path = r"sample_data\RS8-daq_log_20260409_143625.csv"

# read all csv inside sample data that start with S#
file_paths = glob.glob(r"sample_data\S*-daq_log_*.csv")

# Load all matching files into a dictionary keyed by filename stem
data = {
    os.path.splitext(os.path.basename(f))[0]: pd.read_csv(f)
    for f in sorted(file_paths)
}

print(f"Loaded {len(data)} files: {list(data.keys())}")

# Load CSV
df = pd.read_csv(file_path)
rho_water = 1000  # [kg/m^3]
rho_lox = 1141  # [kg/m^3]
rho_ipa = 785  # [kg/m^3]
P_atm = 101325  # [Pa]
pipe_diam = 4.53e-3  # [m]
pipe_area = np.pi * (pipe_diam / 2) ** 2

# From LC calibration output (lc_total_scale_meter_to_lc).
FLOW1_SCALE = 0.993350
FLOW2_SCALE = 1.104725

# Correct flowmeter reading.
df["Flow1_gs"] *= FLOW1_SCALE
df["Flow2_gs"] *= FLOW2_SCALE

df["LC_total_kg"] = df["LC_total_g"] * 1e-3
df["Flow1_kgs"] = df["Flow1_gs"] * 1e-3
df["Flow2_kgs"] = df["Flow2_gs"] * 1e-3

df["Flow1_IPA_equiv"] = df["Flow1_kgs"] * (rho_water / rho_ipa)
df["Flow2_LOX_equiv"] = df["Flow2_kgs"] * (rho_water / rho_lox)

df["Velocity1_ms"] = df["Flow1_kgs"] / rho_water / pipe_area
df["Velocity2_ms"] = df["Flow2_kgs"] / rho_water / pipe_area

# dynamic pressure
df["P_q_1"] = 0.5 * rho_water * df["Velocity1_ms"] ** 2
df["P_q_2"] = 0.5 * rho_water * df["Velocity2_ms"] ** 2

# static pressure (PT reading)
df["P_s_1"] = df["PT1_bar"] * 1e5
df["P_s_2"] = df["PT2_bar"] * 1e5

# dP (total pressure upstream - atmospheric)
df["dP_1"] = df["P_s_1"] + df["P_q_1"] - P_atm
df["dP_2"] = df["P_s_2"] + df["P_q_2"] - P_atm

# mu (soviet Cd)
Rn1 = 1.6310363 # stage 1 nozzle radius
Rn2 = 3.7514746 # stage 2 nozzle radius
df["mu_1"] = df["Flow1_kgs"] / np.sqrt(2 * rho_water * df["dP_1"] * pipe_area**2)
df["mu_2"] = df["Flow2_kgs"] / np.sqrt(2 * rho_water * df["dP_2"] * pipe_area**2)

# Parse timestamp
df["timestamp"] = pd.to_datetime(df["timestamp"], format="%H:%M:%S.%f")

# Time relative to start
t0 = df["timestamp"].iloc[0]
df["t_sec"] = (df["timestamp"] - t0).dt.total_seconds()

# Flowmeter scaling
# df["Flow1_kgs"] = df["Flow1_kgs"] / 11 / 11

# ---- Smooth mass before differentiation ----
df["LC_total_smooth"] = df["LC_total_kg"].rolling(1, center=True).mean()

# Derived mass flow
df["flow_from_mass"] = -df["LC_total_smooth"].diff() / df["t_sec"].diff()

# Additional smoothing of flow signal
df["flow_from_mass"] = df["flow_from_mass"].rolling(20, center=True).mean()


plt.figure()
plt.plot(df["t_sec"], df["Flow1_kgs"], label="Flow meter IPA")
plt.plot(df["t_sec"], df["Flow2_kgs"], label="Flow meter LOx")
plt.plot(
    df["t_sec"],
    df["Flow1_kgs"] + df["Flow2_kgs"],
    label="Flow meter Net",
)
plt.plot(df["t_sec"], df["flow_from_mass"], label="Derived from Mass")
plt.xlabel("Time (s)")
plt.ylabel("Flow (kg/s)")
plt.title(f"Flow vs Time")
plt.legend()
plt.grid(True)
plt.tight_layout()


plt.figure()
plt.plot(df["t_sec"], df["mu_1"], label="mu IPA")
plt.plot(df["t_sec"], df["mu_2"], label="mu LOx")
plt.xlabel("Time (s)")
plt.ylabel("Pressure [bar]")
plt.title("Pressure vs Time")
plt.legend()
plt.grid(True)
plt.tight_layout()
plt.show()
