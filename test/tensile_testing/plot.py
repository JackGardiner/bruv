import pandas as pd
import numpy as np
import matplotlib.pyplot as plt

import geez

from tensile_plot import plot_tensile_results


def main():
    samples = "x1 x2 x3 y1 y2 y3 z1 z2 z3".split()

    Dx = []
    F = []
    for sample in samples:
        df = pd.read_csv(f"data/{sample}.csv", skiprows=10, header=None,
                names=["<empty>", "Time", "Displacement", "Force"])
        df = df.drop(columns=["<empty>"])
        df = df.apply(pd.to_numeric)
        Dx.append(df["Displacement"].to_numpy() * 1e-3)
        F.append(df["Force"].to_numpy())

    A = [17.7304e-6, 17.7896e-6, 17.8200e-6,
         17.3156e-6, 17.7304e-6, 17.7896e-6,
         17.8502e-6, 17.9982e-6, 17.8800e-6]
    Lx = [30e-3] * 9


    colours = ["#90E0EF", "#0077B6", "#03045E",
               "#FF8A8A", "#E63946", "#7A0010",
               "#99E2B4", "#40916C", "#1B4332"]

    E = [250e6/1e-2] * 9
    strain = [Dx[i] / Lx[i] for i in range(len(Dx))]
    stress = [F[i] / A[i] for i in range(len(F))]


    # Reshape into the dict format
    def pack(flat_list):
        it = iter(flat_list)
        return {ori: [next(it), next(it), next(it)] for ori in ("X", "Y", "Z")}

    plot_tensile_results(
        stress   = pack(stress),
        strain   = pack(strain),
        # E_manual = pack(E),
        save_path="plot.png"
    )
    # analyze_orientation_tensile_tests(stress, strain, E)
    return




    fig = plt.figure(figsize=(20, 30), layout="constrained")
    for i in range(3):
        ax = fig.add_subplot(3, 1, 1 + i, axes_class=geez.BrAxes)
        ax.set_title(f"Tensile testing {"XYZ"[i]} print-orientation")
        ax.set_xlabel("Strain [%]")
        ax.set_ylabel("Stress [MPa]")
        ax.set_xlim([0, 20])
        ax.set_ylim([0, 600])
        # ax.set_aspect(1 / 150)
        for j in range(3):
            plot = lambda x, y, *args, **kwargs: ax.plot(
                    np.asarray(x)*1e2,
                    np.asarray(y)*1e-6,
                    *args, **kwargs)
            idx = 3*i + j
            strain = Dx[idx] / Lx[idx]
            stress = F[idx] / A[idx]
            ultimate = stress.max()
            line, = plot(strain, stress, color=colours[idx],
                    label=f"{samples[idx]} (UTS {ultimate*1e-6:.1f} MPa)")
            plot(strain, E[idx]*strain, ls="--",
                    color=line.get_color())
            # plot([-0.01, 0.21], [ultimate]*2, label=,
            #         inlined_at_x=[16.0, 19.0, 22.0][j], ls="--",
            #         color=line.get_color())
    plt.show()


if __name__ == "__main__":
    main()
