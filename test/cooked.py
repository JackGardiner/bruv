
import pandas as pd
import numpy as np

## LINEAR REGR
if False:
    X = 1e-4 * np.array([
        108.0635349,
        112.0093109,
        115.6717814,
        119.1667068,
        122.7823,
        126.5099,
        129.9702,
        133.7567,
    ])
    Y = np.array([
        0.8,
        1.2,
        1.6,
        2.0,
        2.4,
        2.8,
        3.2,
        3.6,
    ])

    m, b = np.polyfit(X, Y, 1)
    print(m, b)

    quit()


data = pd.read_csv("logs/daq_log_20260309_150643.csv")

t = data["timestamp"]
x = data["LC_total_kg"]

t = pd.to_datetime(t, format="%H:%M:%S.%f")
t = (t - t.iloc[0]).dt.total_seconds().to_numpy()

idxs = np.where(np.diff(t) > 1)[0] + 1
idxs = np.insert(idxs, 0, 0)
idxs = np.append(idxs, len(t))

readings = []
for i in range(len(idxs) - 1):
    y = x[idxs[i]:idxs[i + 1]]
    readings.append(float(np.sum(y) / len(y)))

print(0.4* np.arange(len(readings)))
print()
for r in readings:
    print(r)
