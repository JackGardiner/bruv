import os
import glob
import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.dates as mdates
from matplotlib.widgets import SpanSelector, Button, RadioButtons

# ── Load data ────────────────────────────────────────────────────────────────

pattern = os.path.join('sample_data', '*S*-daq_log_*.csv')
file_paths = sorted(glob.glob(pattern))

if not file_paths:
    raise FileNotFoundError("No matching files found in sample_data/")

def load_file(path):
    return pd.read_csv(path, parse_dates=['timestamp'])

data = {
    os.path.splitext(os.path.basename(f))[0]: load_file(f)
    for f in file_paths
}

names = list(data.keys())
TIME_COL = 'timestamp'

# ── State ────────────────────────────────────────────────────────────────────

state = {
    'sample': names[0],
    't_start': None,
    't_end':   None,
}

# ── Figure layout ─────────────────────────────────────────────────────────────
#
#   [ radio buttons ]  [ main plot         ]
#                      [ save | clear | scope | status ]

fig = plt.figure(figsize=(12, 5))
fig.canvas.manager.set_window_title('CSV Time Trimmer')

ax        = fig.add_axes([0.28, 0.18, 0.68, 0.68])
ax_radio  = fig.add_axes([0.01, 0.18, 0.20, 0.68])
ax_save   = fig.add_axes([0.28, 0.04, 0.15, 0.09])
ax_clear  = fig.add_axes([0.44, 0.04, 0.10, 0.09])
ax_scope  = fig.add_axes([0.56, 0.04, 0.22, 0.09])
ax_status = fig.add_axes([0.80, 0.04, 0.18, 0.09])

# frameon must be set after construction
ax_radio.set_frame_on(False)
ax_scope.set_frame_on(False)
ax_status.set_frame_on(False)
ax_status.axis('off')

status_txt = ax_status.text(0, 0.5, '', va='center', fontsize=9, color='green',
                             wrap=True)

# ── Status helper (defined early, used by plot_sample) ───────────────────────

def update_status(msg, color='green'):
    status_txt.set_text(msg)
    status_txt.set_color(color)
    fig.canvas.draw_idle()

# ── Plot helpers ──────────────────────────────────────────────────────────────

def plot_sample(name):
    df = data[name]
    ax.cla()

    numeric_cols = df.select_dtypes('number').columns.tolist()
    for col in numeric_cols:
        ax.plot(df[TIME_COL], df[col], lw=0.8, label=col)

    ax.xaxis.set_major_formatter(mdates.DateFormatter('%H:%M:%S'))
    fig.autofmt_xdate(rotation=30)
    ax.set_title(f'{name}   —   drag to select trim region', fontsize=10)
    ax.legend(fontsize=7, loc='upper right')
    ax.set_xlabel('timestamp')

    state['t_start'] = None
    state['t_end']   = None
    update_status('Drag on the plot to select a time range.')
    fig.canvas.draw_idle()

def highlight_selection(t0, t1):
    for coll in ax.collections:
        coll.remove()
    ax.axvspan(t0, t1, alpha=0.25, color='steelblue', zorder=0)
    fig.canvas.draw_idle()

# ── Span selector ─────────────────────────────────────────────────────────────

def on_select(xmin, xmax):
    if xmax - xmin < 1e-6:
        return
    t0 = mdates.num2date(xmin).replace(tzinfo=None)
    t1 = mdates.num2date(xmax).replace(tzinfo=None)
    state['t_start'] = pd.Timestamp(t0)
    state['t_end']   = pd.Timestamp(t1)
    highlight_selection(xmin, xmax)
    update_status(
        f"Selected:  {t0.strftime('%H:%M:%S.%f')[:-3]}"
        f"  →  {t1.strftime('%H:%M:%S.%f')[:-3]}"
    )

span = SpanSelector(ax, on_select, 'horizontal',
                    useblit=True,
                    props=dict(alpha=0.3, facecolor='steelblue'),
                    interactive=True)

# ── Radio buttons (sample selector) ──────────────────────────────────────────

radio = RadioButtons(ax_radio, names, activecolor='steelblue')

def on_radio(label):
    state['sample'] = label
    plot_sample(label)

radio.on_clicked(on_radio)

# ── Scope toggle ──────────────────────────────────────────────────────────────

scope_radio = RadioButtons(ax_scope, ['Save this sample', 'Save all samples'],
                           activecolor='steelblue')

# ── Buttons ───────────────────────────────────────────────────────────────────

btn_save = Button(ax_save, 'Trim & Save', color='steelblue', hovercolor='#1a5fa8')
btn_save.label.set_color('white')

btn_clear = Button(ax_clear, 'Clear', color='#888888', hovercolor='#555555')
btn_clear.label.set_color('white')

def do_save(_):
    if state['t_start'] is None:
        update_status('Select a region first!', color='red')
        return

    t0, t1 = state['t_start'], state['t_end']
    save_all = scope_radio.value_selected == 'Save all samples'
    targets = data if save_all else {state['sample']: data[state['sample']]}

    out_dir = os.path.join('sample_data', 'trimmed')
    os.makedirs(out_dir, exist_ok=True)

    saved = []
    for name, df in targets.items():
        mask = (df[TIME_COL] >= t0) & (df[TIME_COL] <= t1)
        trimmed = df[mask].copy()
        trimmed[TIME_COL] = trimmed[TIME_COL].dt.strftime('%H:%M:%S.%f')
        out_path = os.path.join(out_dir, f'{name}.csv')
        trimmed.to_csv(out_path, index=False)
        saved.append(f'{name}: {len(trimmed)} rows')

    update_status(f"Saved {len(saved)} file(s) to sample_data/trimmed/")
    print('\n'.join(saved))

def do_clear(_):
    state['t_start'] = None
    state['t_end']   = None
    plot_sample(state['sample'])   # full redraw clears all patches

btn_save.on_clicked(do_save)
btn_clear.on_clicked(do_clear)

# ── Initial plot ──────────────────────────────────────────────────────────────

plot_sample(names[0])
plt.show()