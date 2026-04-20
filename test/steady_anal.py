import re
from pathlib import Path

import matplotlib.pyplot as plt
import numpy as np
import pandas as pd


REQUIRED_COLUMNS = [
	"timestamp",
	"PT1_bar",
	"PT2_bar",
	"LC_total_g",
	"Flow1_gs",
	"Flow2_gs",
]

DERIVED_COLUMNS = [
	"Flow1_kgs",
	"Flow2_kgs",
	"Flow1_IPA_equiv",
	"Flow2_LOX_equiv",
	"dP_1",
	"dP_2",
	"mu_1",
	"mu_2",
]

REPORT_SECTION_PREFIX = {
	"Stage 1": "stage1",
	"Stage 2": "stage2",
	"Interactions": "inter",
	"Empirical corrections": "empirical",
}

REPORT_FILE_RE = re.compile(r"injector-sample-(\d+)-report\.txt$", re.IGNORECASE)
TRIMMED_FILE_RE = re.compile(r"(RS|S)(\d+)(?:\.(\d+))?-.+\.csv$", re.IGNORECASE)

# ----------------------------
# User-configurable run values
# ----------------------------
TRIMMED_DIR = "sample_data/trimmed"
REPORT_DIR = "injector_reports"
OUTPUT_DIR = "analysis_outputs"
SCALING_FILE = "analysis_outputs/flow_scaling.csv"
MODE = "both"  # "both", "S", or "RS"
MAKE_PLOTS = True
NEW_SAMPLE_INDEX_MIN = 8   # RS8+ = K_extra sweep (Family 2 onwards)
NEW_ROUND_INDEX_MIN   = 13  # RS13+ = new K_corr round (Family 3)
NEW_FAMILY4_INDEX_MIN = 18  # RS18+ = Family 4 resin round
SAMPLE_COLOR_S        = "tab:blue"
SAMPLE_COLOR_RS_OLD   = "tab:orange"   # Family 1: RS0-7  (baseline geometry)
SAMPLE_COLOR_RS_NEW   = "tab:green"    # Family 2: RS8-12 (K_extra sweep, old K_corr)
SAMPLE_COLOR_RS_NEW2  = "tab:purple"   # Family 3: RS13+  (new K_corr round)
SAMPLE_COLOR_RS_NEW3  = "tab:brown"    # Family 4: RS18+  (latest resin round)

# Optional targets (kg/s) — 12 injector elements
TARGET_COPPER_FLOW_STAGE2 = 0.7101464633719627  # Stage 2 (IPA) = Flow1, mdot_IPA
TARGET_COPPER_FLOW_STAGE1 = 1.2523519905736131  # Stage 1 (LOX) = Flow2, mdot_LOX

# Final-round design controls: use updated round-2 K values as base, then sweep K_extra.
DESIGN_BASE_K_SOURCE = "RS_NEW"  # "RS_NEW", "RS_BASELINE", "COPPER", "AUTO"
K_MDOT_EXTRA_SWEEP = [0.90, 0.95, 1.00, 1.05, 1.10]

# Fallback scaling constants used when flow_scaling.csv is absent.
_FLOW1_SCALE_DEFAULT = 0.993350
_FLOW2_SCALE_DEFAULT = 1.104725


def _load_flow_scales() -> tuple[float, float]:
	path = Path(SCALING_FILE)
	if not path.exists():
		print(f"[warn] {SCALING_FILE} not found — using hardcoded defaults ({_FLOW1_SCALE_DEFAULT}, {_FLOW2_SCALE_DEFAULT})")
		return _FLOW1_SCALE_DEFAULT, _FLOW2_SCALE_DEFAULT
	try:
		sf = pd.read_csv(path)
		fm1 = float(sf.loc[sf["meter"] == "FM1", "scale"].iloc[0])
		fm2 = float(sf.loc[sf["meter"] == "FM2", "scale"].iloc[0])
		print(f"[info] Loaded flow scales from {SCALING_FILE}: FM1={fm1:.6f}, FM2={fm2:.6f}")
		return fm1, fm2
	except Exception as exc:
		print(f"[warn] Could not parse {SCALING_FILE} ({exc}) — using hardcoded defaults")
		return _FLOW1_SCALE_DEFAULT, _FLOW2_SCALE_DEFAULT


FLOW1_SCALE, FLOW2_SCALE = _load_flow_scales()


def normalize_key(raw_key: str) -> str:
	key = raw_key.strip().lower().replace("-", "_")
	key = re.sub(r"[^a-z0-9_ ]", "", key)
	key = re.sub(r"\s+", "_", key)
	key = re.sub(r"_+", "_", key).strip("_")
	return key


def parse_sample_meta(file_path: Path) -> tuple[str, int, int | None]:
	match = TRIMMED_FILE_RE.search(file_path.name)
	if not match:
		raise ValueError(f"Unable to parse sample type/index from filename: {file_path.name}")
	sample_type = match.group(1).upper()
	sample_index = int(match.group(2))
	secondary_index = int(match.group(3)) if match.group(3) is not None else None
	return sample_type, sample_index, secondary_index


def format_sample_id(sample_type: str, sample_index: int, secondary_index: int | None = None) -> str:
	if secondary_index is None:
		return f"{sample_type}{sample_index}"
	return f"{sample_type}{sample_index}.{secondary_index}"


def format_sample_id_from_row(row: pd.Series) -> str:
	sample_type = str(row.get("sample_type", ""))
	sample_index = row.get("sample_index", None)
	secondary_index = row.get("sample_secondary_index", None)
	if pd.isna(sample_index):
		return sample_type
	secondary_index_int = None if pd.isna(secondary_index) else int(secondary_index)
	return format_sample_id(sample_type, int(sample_index), secondary_index_int)


def parse_report_file(file_path: Path) -> dict:
	data = {}
	section = None

	with file_path.open("r", encoding="utf-8") as f:
		for raw_line in f:
			line = raw_line.strip()
			if not line:
				continue

			if line.startswith("Stage 1"):
				section = "Stage 1"
				continue
			if line.startswith("Stage 2"):
				section = "Stage 2"
				continue
			if line.startswith("Interactions"):
				section = "Interactions"
				continue
			if line.startswith("Empirical corrections"):
				section = "Empirical corrections"
				continue

			if not line.startswith("-") or section is None or ":" not in line:
				continue

			key_part, value_part = line[1:].split(":", 1)
			key = normalize_key(key_part)
			value_tokens = value_part.strip().split()
			if not value_tokens:
				continue

			try:
				numeric_value = float(value_tokens[0])
			except ValueError:
				continue

			unit = value_tokens[1].lower() if len(value_tokens) > 1 else ""
			prefix = REPORT_SECTION_PREFIX[section]
			out_key = f"{prefix}_{key}"
			if unit in {"bar", "kg/s", "mm"}:
				out_key = f"{out_key}_{unit.replace('/', 'p')}"
			data[out_key] = numeric_value

	return data


def load_report_table(report_dir: Path) -> pd.DataFrame:
	rows = []
	for path in sorted(report_dir.glob("injector-sample-*-report.txt")):
		match = REPORT_FILE_RE.search(path.name)
		if not match:
			continue

		sample_index = int(match.group(1))
		parsed = parse_report_file(path)
		parsed["sample_index"] = sample_index
		parsed["report_file"] = path.name
		rows.append(parsed)

	if not rows:
		return pd.DataFrame(columns=["sample_index", "report_file"])

	report_df = pd.DataFrame(rows).sort_values("sample_index").reset_index(drop=True)
	return report_df


def derive_timeseries_columns(df: pd.DataFrame) -> pd.DataFrame:
	work = df.copy()

	for col in REQUIRED_COLUMNS:
		if col != "timestamp":
			work[col] = pd.to_numeric(work[col], errors="coerce")

	work["Flow1_gs"] = work["Flow1_gs"] * FLOW1_SCALE
	work["Flow2_gs"] = work["Flow2_gs"] * FLOW2_SCALE
	work["LC_total_kg"] = work["LC_total_g"] * 1e-3
	work["Flow1_kgs"] = work["Flow1_gs"] * 1e-3
	work["Flow2_kgs"] = work["Flow2_gs"] * 1e-3

	rho_water = 1000.0
	rho_lox = 1141.0
	rho_ipa = 785.0
	p_atm = 101325.0
	pipe_diam = 4.53e-3
	pipe_area = np.pi * (pipe_diam / 2.0) ** 2

	work["Flow1_IPA_equiv"] = work["Flow1_kgs"] * (rho_water / rho_ipa)
	work["Flow2_LOX_equiv"] = work["Flow2_kgs"] * (rho_water / rho_lox)

	work["Velocity1_ms"] = work["Flow1_kgs"] / rho_water / pipe_area
	work["Velocity2_ms"] = work["Flow2_kgs"] / rho_water / pipe_area
	work["P_q_1"] = 0.5 * rho_water * work["Velocity1_ms"] ** 2
	work["P_q_2"] = 0.5 * rho_water * work["Velocity2_ms"] ** 2
	work["P_s_1"] = work["PT1_bar"] * 1e5
	work["P_s_2"] = work["PT2_bar"] * 1e5
	work["dP_1"] = work["P_s_1"] + work["P_q_1"] - p_atm
	work["dP_2"] = work["P_s_2"] + work["P_q_2"] - p_atm

	denom_1 = np.sqrt(np.clip(2.0 * rho_water * work["dP_1"] * (pipe_area ** 2), a_min=1e-12, a_max=None))
	denom_2 = np.sqrt(np.clip(2.0 * rho_water * work["dP_2"] * (pipe_area ** 2), a_min=1e-12, a_max=None))
	work["mu_1"] = work["Flow1_kgs"] / denom_1
	work["mu_2"] = work["Flow2_kgs"] / denom_2

	return work


def summarize_columns(df: pd.DataFrame, columns: list[str]) -> dict:
	out = {}
	for col in columns:
		series = pd.to_numeric(df[col], errors="coerce")
		out[f"{col}_mean"] = float(series.mean())
		out[f"{col}_std"] = float(series.std())
	return out


def check_lc_fm_consistency(derived: pd.DataFrame, threshold_pct: float = 5.0) -> dict:
	"""
	Fit a linear regression to LC_total_g vs elapsed time to get the load-cell
	derived MFR (g/s), then compare to mean(Flow1_gs + Flow2_gs).

	Returns a dict with: lc_mfr_gs, fm_mfr_gs, error_pct, ok, msg.
	"""
	try:
		ts = pd.to_datetime(derived["timestamp"], format="%H:%M:%S.%f", errors="coerce")
		if ts.isna().all():
			ts = pd.to_datetime(derived["timestamp"], errors="coerce")
		t_sec = (ts - ts.iloc[0]).dt.total_seconds().values
	except Exception:
		return {"lc_mfr_gs": np.nan, "fm_mfr_gs": np.nan, "error_pct": np.nan,
				"ok": False, "msg": "Could not parse timestamps"}

	lc  = pd.to_numeric(derived["LC_total_g"], errors="coerce").values
	fm1 = pd.to_numeric(derived["Flow1_gs"],   errors="coerce").values
	fm2 = pd.to_numeric(derived["Flow2_gs"],   errors="coerce").values

	mask = ~(np.isnan(lc) | np.isnan(t_sec))
	if mask.sum() < 5:
		return {"lc_mfr_gs": np.nan, "fm_mfr_gs": np.nan, "error_pct": np.nan,
				"ok": False, "msg": "Insufficient data points"}

	t_vals = pd.Series(t_sec[mask], dtype=float).dropna().tolist()
	lc_vals = pd.Series(lc[mask], dtype=float).dropna().tolist()
	slope = float(np.polyfit(t_vals, lc_vals, 1)[0])
	lc_mfr = -slope  # LC decreases as propellant is consumed

	fm_mfr = float(np.nanmean(np.asarray(fm1, dtype=float) + np.asarray(fm2, dtype=float)))

	if abs(fm_mfr) < 1e-6:
		return {"lc_mfr_gs": lc_mfr, "fm_mfr_gs": fm_mfr, "error_pct": np.nan,
				"ok": False, "msg": "Flow meter total near zero"}

	error_pct = 100.0 * (lc_mfr - fm_mfr) / fm_mfr
	ok = abs(error_pct) <= threshold_pct
	tag = "OK   " if ok else "ALARM"
	msg = (f"{tag}  LC {lc_mfr:7.2f} g/s  vs  FM {fm_mfr:7.2f} g/s"
		   f"  ({error_pct:+.1f}%  threshold ±{threshold_pct:.0f}%)")
	return {"lc_mfr_gs": lc_mfr, "fm_mfr_gs": fm_mfr, "error_pct": error_pct,
			"ok": ok, "msg": msg}


def summarize_trimmed_file(file_path: Path) -> tuple[dict, pd.DataFrame]:
	sample_type, sample_index, secondary_index = parse_sample_meta(file_path)
	df = pd.read_csv(file_path)

	missing = [col for col in REQUIRED_COLUMNS if col not in df.columns]
	if missing:
		raise ValueError(f"Missing columns in {file_path.name}: {missing}")
	if df.empty:
		raise ValueError(f"Trimmed file is empty: {file_path.name}")

	derived = derive_timeseries_columns(df)
	channel_stats = summarize_columns(derived, ["PT1_bar", "PT2_bar", "LC_total_g", "Flow1_gs", "Flow2_gs"])
	derived_stats = summarize_columns(derived, DERIVED_COLUMNS)
	lc_check = check_lc_fm_consistency(derived)

	row = {
		"sample_type": sample_type,
		"sample_index": sample_index,
		"sample_secondary_index": secondary_index,
		"sample_id": format_sample_id(sample_type, sample_index, secondary_index),
		"source_file": file_path.name,
		"row_count": int(len(derived)),
		"lc_fm_check_ok":       lc_check["ok"],
		"lc_fm_lc_mfr_gs":     lc_check["lc_mfr_gs"],
		"lc_fm_fm_mfr_gs":     lc_check["fm_mfr_gs"],
		"lc_fm_error_pct":     lc_check["error_pct"],
		"lc_fm_msg":           lc_check["msg"],
	}
	row.update(channel_stats)
	row.update(derived_stats)
	return row, derived


def load_trimmed_summaries(trimmed_dir: Path) -> tuple[pd.DataFrame, dict[str, pd.DataFrame]]:
	rows = []
	raw_runs = {}
	for path in sorted(trimmed_dir.glob("*.csv")):
		if TRIMMED_FILE_RE.search(path.name) is None:
			continue
		row, derived = summarize_trimmed_file(path)
		rows.append(row)
		raw_runs[path.name] = derived

	if not rows:
		return pd.DataFrame(), raw_runs

	df = pd.DataFrame(rows).sort_values(["sample_type", "sample_index", "sample_secondary_index", "source_file"]).reset_index(drop=True)
	return df, raw_runs


def make_paired_table(collated: pd.DataFrame) -> pd.DataFrame:
	s_df = collated[collated["sample_type"] == "S"].copy()
	rs_df = collated[collated["sample_type"] == "RS"].copy()

	if s_df.empty or rs_df.empty:
		return pd.DataFrame()

	s_df = s_df.add_suffix("_s")
	rs_df = rs_df.add_suffix("_rs")
	paired = s_df.merge(rs_df, left_on="sample_index_s", right_on="sample_index_rs", how="inner")

	if paired.empty:
		return paired

	paired = paired.rename(columns={"sample_index_s": "sample_index"})

	metric_bases = [
		"Flow1_kgs_mean",
		"Flow2_kgs_mean",
		"mu_1_mean",
		"mu_2_mean",
		"PT1_bar_mean",
		"PT2_bar_mean",
	]
	for base in metric_bases:
		s_col = f"{base}_s"
		rs_col = f"{base}_rs"
		if s_col not in paired.columns or rs_col not in paired.columns:
			continue

		delta_col = f"{base}_delta_rs_minus_s"
		pct_col = f"{base}_pct_delta_vs_s"
		paired[delta_col] = paired[rs_col] - paired[s_col]
		denom = paired[s_col].replace(0, np.nan)
		paired[pct_col] = 100.0 * paired[delta_col] / denom

	return paired.sort_values("sample_index").reset_index(drop=True)


def split_sample_groups_by_recency(group: pd.DataFrame) -> list[tuple[str, pd.DataFrame, str, str]]:
	if group.empty:
		return []

	if "sample_type" not in group.columns:
		return [("sample", group, "gray", "o")]

	sample_type = str(group["sample_type"].iloc[0])
	if sample_type == "S":
		return [("S", group, SAMPLE_COLOR_S, "o")]

	if sample_type == "RS" and "sample_index" in group.columns:
		fam1 = group[group["sample_index"] < NEW_SAMPLE_INDEX_MIN]
		fam2 = group[(group["sample_index"] >= NEW_SAMPLE_INDEX_MIN) & (group["sample_index"] < NEW_ROUND_INDEX_MIN)]
		fam3 = group[(group["sample_index"] >= NEW_ROUND_INDEX_MIN) & (group["sample_index"] < NEW_FAMILY4_INDEX_MIN)]
		fam4 = group[group["sample_index"] >= NEW_FAMILY4_INDEX_MIN]
		plot_groups: list[tuple[str, pd.DataFrame, str, str]] = []
		if not fam1.empty:
			plot_groups.append(("RS Family 1 (RS0–7)", fam1, SAMPLE_COLOR_RS_OLD, "o"))
		if not fam2.empty:
			plot_groups.append(("RS Family 2 (RS8–12)", fam2, SAMPLE_COLOR_RS_NEW, "D"))
		if not fam3.empty:
			plot_groups.append(("RS Family 3 (RS13–17)", fam3, SAMPLE_COLOR_RS_NEW2, "s"))
		if not fam4.empty:
			plot_groups.append(("RS Family 4 (RS18+)", fam4, SAMPLE_COLOR_RS_NEW3, "^"))
		return plot_groups

	return [(sample_type, group, "gray", "o")]


def write_lc_fm_report(collated: pd.DataFrame, out_dir: Path, threshold_pct: float = 5.0) -> None:
	"""Print and save a per-sample LC vs flow-meter consistency check."""
	if "lc_fm_msg" not in collated.columns:
		return

	lines = []
	lines.append("=" * 80)
	lines.append("LOAD CELL vs FLOW METER CONSISTENCY CHECK")
	lines.append(f"Threshold: ±{threshold_pct:.0f}%   (LC total MFR vs Flow1+Flow2 mean)")
	lines.append("=" * 80)

	alarms = []
	for _, row in collated.sort_values(["sample_type", "sample_index", "sample_secondary_index"]).iterrows():
		tag  = format_sample_id_from_row(row)
		msg  = str(row.get("lc_fm_msg", "N/A"))
		ok   = bool(row.get("lc_fm_check_ok", False))
		lines.append(f"  {tag}  {msg}")
		if not ok:
			alarms.append(tag)

	lines.append("")
	if alarms:
		lines.append(f"  *** {len(alarms)} ALARM(S): {', '.join(alarms)} ***")
	else:
		lines.append("  All samples within threshold.")
	lines.append("")

	text = "\n".join(lines)
	print(text)
	(out_dir / "lc_fm_consistency.txt").write_text(text, encoding="utf-8")
	print(f"Saved LC/FM check: {out_dir}/lc_fm_consistency.txt")


def write_new_sample_mfr_error_report(collated: pd.DataFrame, out_dir: Path) -> None:
	if collated.empty:
		return

	stage_map = {
		"stage2": {
			"stage_name": "Stage 2 (IPA)",
			"mdot_theory": "stage2_mass_flow_rate_kgps",
			"mdot_exp": "Flow1_kgs_mean",
		},
		"stage1": {
			"stage_name": "Stage 1 (LOX)",
			"mdot_theory": "stage1_mass_flow_rate_kgps",
			"mdot_exp": "Flow2_kgs_mean",
		},
	}

	work = collated.copy()
	for stage_info in stage_map.values():
		for col_name in [stage_info["mdot_theory"], stage_info["mdot_exp"]]:
			if col_name in work.columns:
				work[col_name] = pd.to_numeric(work[col_name], errors="coerce")

	baseline = work[(work["sample_type"] == "RS") & (work["sample_index"] < NEW_SAMPLE_INDEX_MIN)].copy()
	new_samples = work[(work["sample_type"] == "RS") & (work["sample_index"] >= NEW_SAMPLE_INDEX_MIN)].copy()
	new_samples = new_samples.sort_values("sample_index")
	new_index_list = set(new_samples["sample_index"].dropna().astype(int).tolist()) if not new_samples.empty else set()

	lines = []
	lines.append("=" * 90)
	lines.append("NEW SAMPLE MFR ERROR REPORT")
	lines.append("=" * 90)
	lines.append(f"New sample index threshold: RS{NEW_SAMPLE_INDEX_MIN}+")
	lines.append(f"New samples found: {len(new_samples)}")
	lines.append(f"Baseline resin samples used for scaling: {len(baseline)} (RS0-RS{NEW_SAMPLE_INDEX_MIN - 1})")
	lines.append("")

	missing_indices = [idx for idx in range(NEW_SAMPLE_INDEX_MIN, NEW_SAMPLE_INDEX_MIN + 5) if idx not in new_index_list]
	if missing_indices:
		lines.append(f"Missing expected new samples: {', '.join(f'RS{i}' for i in missing_indices)}")
		lines.append("")

	if baseline.empty:
		lines.append("No baseline RS samples available, so scaled error cannot be computed.")
	else:
		for stage_key, stage_info in stage_map.items():
			stage_name = stage_info["stage_name"]
			mdot_theory = stage_info["mdot_theory"]
			mdot_exp = stage_info["mdot_exp"]
			if mdot_theory not in baseline.columns or mdot_exp not in baseline.columns:
				continue

			baseline_k = pd.to_numeric(baseline[mdot_exp], errors="coerce") / pd.to_numeric(baseline[mdot_theory], errors="coerce").replace(0, np.nan)
			baseline_k = baseline_k.dropna()
			if baseline_k.empty:
				continue

			baseline_mean = float(baseline_k.mean())
			baseline_std = float(baseline_k.std())
			lines.append(stage_name)
			lines.append("-" * 90)
			lines.append(f"Baseline K_mdot from old RS samples: {baseline_mean:.6f} +/- {baseline_std:.6f}")
			lines.append("")

			stage_rows = []
			for _, row in new_samples.iterrows():
				if mdot_theory not in row.index or mdot_exp not in row.index:
					continue
				theory_mdot = row[mdot_theory]
				actual_mdot = row[mdot_exp]
				if pd.isna(theory_mdot) or pd.isna(actual_mdot):
					continue
				expected_mdot = theory_mdot * baseline_mean
				pct_error = 100.0 * (actual_mdot - expected_mdot) / expected_mdot if abs(expected_mdot) > 0 else np.nan
				stage_rows.append((format_sample_id_from_row(row), theory_mdot, expected_mdot, actual_mdot, pct_error))

			if not stage_rows:
				lines.append("No new-sample data available for this stage.")
				lines.append("")
				continue

			for sample_id, theory_mdot, expected_mdot, actual_mdot, pct_error in stage_rows:
				lines.append(
					f"{sample_id}: theory={theory_mdot:.6f} kg/s, expected(scaled)={expected_mdot:.6f} kg/s, "
					f"actual={actual_mdot:.6f} kg/s, error={pct_error:.2f}%"
				)
			lines.append("")

	text = "\n".join(lines)
	print(text)
	out_path = out_dir / "new_sample_mfr_error_report.txt"
	out_path.write_text(text, encoding="utf-8")
	print(f"Saved new-sample error report: {out_path}")


def compute_and_summarize_scaling_factors(collated: pd.DataFrame, out_dir: Path) -> None:
	"""
	Compute multiplicative scaling factors (K_mdot, K_Cd) by sample type and stage.
	Print key results to terminal and save to text file.
	"""
	if collated.empty:
		return

	stage_config = {
		"stage2_ipa": {
			"cd_theory": "stage2_cd_2",
			"cd_exp": "mu_1_mean",
			"mdot_theory": "stage2_mass_flow_rate_kgps",
			"mdot_exp": "Flow1_kgs_mean",
			"stage_name": "Stage 2 (IPA)",
		},
		"stage1_lox": {
			"cd_theory": "stage1_cd_1",
			"cd_exp": "mu_2_mean",
			"mdot_theory": "stage1_mass_flow_rate_kgps",
			"mdot_exp": "Flow2_kgs_mean",
			"stage_name": "Stage 1 (LOX)",
		},
	}

	sample_groups = [
		("Copper", collated[collated["sample_type"] == "S"].copy()),
		(
			"Resin baseline",
			collated[(collated["sample_type"] == "RS") & (collated["sample_index"] < NEW_SAMPLE_INDEX_MIN)].copy(),
		),
		(
			"Resin new",
			collated[(collated["sample_type"] == "RS") & (collated["sample_index"] >= NEW_SAMPLE_INDEX_MIN)].copy(),
		),
	]

	work = collated.copy()
	lines = []

	# Header
	lines.append("=" * 80)
	lines.append("BAZAROV SCALING FACTORS SUMMARY")
	lines.append("=" * 80)
	lines.append("")

	for stage_key, stage_info in stage_config.items():
		stage_name = stage_info["stage_name"]
		cd_theory = stage_info["cd_theory"]
		cd_exp = stage_info["cd_exp"]
		mdot_theory = stage_info["mdot_theory"]
		mdot_exp = stage_info["mdot_exp"]

		# Check if all required columns exist
		required_cols = [cd_theory, cd_exp, mdot_theory, mdot_exp]
		if not all(col in work.columns for col in required_cols):
			continue

		# Ensure columns are numeric
		for col in required_cols:
			work[col] = pd.to_numeric(work[col], errors="coerce")

		# Compute multipliers
		work[f"{stage_key}_k_mdot"] = work[mdot_exp] / work[mdot_theory].replace(0, np.nan)
		work[f"{stage_key}_k_cd"] = work[cd_exp] / work[cd_theory].replace(0, np.nan)

		lines.append(f">>> {stage_name}")
		lines.append("-" * 80)

		# Summarize by sample group
		for sample_name, subset in sample_groups:
			if subset.empty:
				continue

			subset = subset.copy()
			for col in required_cols:
				subset[col] = pd.to_numeric(subset[col], errors="coerce")
			subset[f"{stage_key}_k_mdot"] = subset[mdot_exp] / subset[mdot_theory].replace(0, np.nan)
			subset[f"{stage_key}_k_cd"] = subset[cd_exp] / subset[cd_theory].replace(0, np.nan)

			k_mdot_vals = subset[f"{stage_key}_k_mdot"].dropna()
			k_cd_vals = subset[f"{stage_key}_k_cd"].dropna()

			lines.append(f"  {sample_name} (n={len(subset)}):")
			lines.append("")

			# K_mdot stats
			if not k_mdot_vals.empty:
				k_mdot_mean = float(k_mdot_vals.mean())
				k_mdot_std = float(k_mdot_vals.std())
				k_mdot_min = float(k_mdot_vals.min())
				k_mdot_max = float(k_mdot_vals.max())
				lines.append(f"    K_mdot (scaling factor for mass flow):")
				lines.append(f"      Mean:  {k_mdot_mean:.6f}  +/-  {k_mdot_std:.6f}")
				lines.append(f"      Range: [{k_mdot_min:.6f}, {k_mdot_max:.6f}]")
				lines.append("")

			# K_Cd stats
			if not k_cd_vals.empty:
				k_cd_mean = float(k_cd_vals.mean())
				k_cd_std = float(k_cd_vals.std())
				k_cd_min = float(k_cd_vals.min())
				k_cd_max = float(k_cd_vals.max())
				lines.append(f"    K_Cd (scaling factor for discharge coefficient):")
				lines.append(f"      Mean:  {k_cd_mean:.6f}  +/-  {k_cd_std:.6f}")
				lines.append(f"      Range: [{k_cd_min:.6f}, {k_cd_max:.6f}]")
				lines.append("")

		lines.append("")

	# Print to terminal
	terminal_output = "\n".join(lines)
	print(terminal_output)

	# Save to text file
	text_file = out_dir / "scaling_factors_summary.txt"
	with open(text_file, "w") as f:
		f.write(terminal_output)
	print(f"Saved scaling factors summary: {text_file}")


def write_bazarov_calibration_outputs(collated: pd.DataFrame, out_dir: Path) -> None:
	if collated.empty:
		return

	stage_map = {
		"stage2_ipa": {
			"cd_theory": "stage2_cd_2",
			"cd_exp": "mu_1_mean",
			"mdot_theory": "stage2_mass_flow_rate_kgps",
			"mdot_exp": "Flow1_kgs_mean",
		},
		"stage1_lox": {
			"cd_theory": "stage1_cd_1",
			"cd_exp": "mu_2_mean",
			"mdot_theory": "stage1_mass_flow_rate_kgps",
			"mdot_exp": "Flow2_kgs_mean",
		},
	}

	bazarov = collated.copy()
	for stage_name, cols in stage_map.items():
		for col_name in cols.values():
			if col_name not in bazarov.columns:
				bazarov[col_name] = np.nan

		cd_theory = cols["cd_theory"]
		cd_exp = cols["cd_exp"]
		mdot_theory = cols["mdot_theory"]
		mdot_exp = cols["mdot_exp"]

		bazarov[f"{stage_name}_cd_ratio_exp_over_theory"] = bazarov[cd_exp] / bazarov[cd_theory].replace(0, np.nan)
		bazarov[f"{stage_name}_mdot_ratio_exp_over_theory"] = bazarov[mdot_exp] / bazarov[mdot_theory].replace(0, np.nan)
		bazarov[f"{stage_name}_cd_error_pct"] = 100.0 * (bazarov[cd_exp] - bazarov[cd_theory]) / bazarov[cd_theory].replace(0, np.nan)
		bazarov[f"{stage_name}_mdot_error_pct"] = 100.0 * (bazarov[mdot_exp] - bazarov[mdot_theory]) / bazarov[mdot_theory].replace(0, np.nan)

	cal_cols = [
		"sample_type",
		"sample_index",
		"source_file",
		"stage2_cd_2",
		"mu_1_mean",
		"stage2_mass_flow_rate_kgps",
		"Flow1_kgs_mean",
		"stage2_ipa_cd_ratio_exp_over_theory",
		"stage2_ipa_mdot_ratio_exp_over_theory",
		"stage1_cd_1",
		"mu_2_mean",
		"stage1_mass_flow_rate_kgps",
		"Flow2_kgs_mean",
		"stage1_lox_cd_ratio_exp_over_theory",
		"stage1_lox_mdot_ratio_exp_over_theory",
	]
	cal_cols = [c for c in cal_cols if c in bazarov.columns]
	bazarov[cal_cols].to_csv(out_dir / "steady_bazarov_calibration.csv", index=False)

	# Parity plots anchored to Bazarov outputs
	fig, axes = plt.subplots(2, 2, figsize=(12, 10))
	panels = [
		("stage2_cd_2", "mu_1_mean", "Stage 2 (IPA) Cd Parity", axes[0, 0]),
		("stage1_cd_1", "mu_2_mean", "Stage 1 (LOX) Cd Parity", axes[0, 1]),
		("stage2_mass_flow_rate_kgps", "Flow1_kgs_mean", "Stage 2 (IPA) mdot Parity", axes[1, 0]),
		("stage1_mass_flow_rate_kgps", "Flow2_kgs_mean", "Stage 1 (LOX) mdot Parity", axes[1, 1]),
	]
	for x_col, y_col, title, ax in panels:
		if x_col not in bazarov.columns or y_col not in bazarov.columns:
			ax.axis("off")
			continue

		keep_cols = [c for c in ["sample_type", "sample_index", x_col, y_col] if c in bazarov.columns]
		plot_df = bazarov[keep_cols].dropna(subset=[x_col, y_col])
		if plot_df.empty:
			ax.axis("off")
			continue

		for sample_type, grp in plot_df.groupby("sample_type"):
			for sample_label, plot_group, color, marker in split_sample_groups_by_recency(grp):
				ax.scatter(
					plot_group[x_col],
					plot_group[y_col],
					label=sample_label,
					color=color,
					s=80,
					alpha=0.9,
					marker=marker,
				)
				if "sample_type" in plot_group.columns and "sample_index" in plot_group.columns:
					for _, row in plot_group.iterrows():
						stype = str(row.get("sample_type", ""))
						sidx  = row.get("sample_index", "")
						tag   = f"{stype}{int(sidx)}" if pd.notna(sidx) else stype
						xval  = row.get(x_col)
						yval  = row.get(y_col)
						if pd.notna(xval) and pd.notna(yval):
							ax.annotate(tag, (xval, yval), xytext=(4, 4),
							            textcoords="offset points", fontsize=7.5, color=color)

		all_vals = pd.concat([plot_df[x_col], plot_df[y_col]])
		vmin = float(all_vals.min())
		vmax = float(all_vals.max())
		ax.plot([vmin, vmax], [vmin, vmax], "k--", linewidth=1.2, label="y=x")
		ax.set_xlabel(f"Theory ({x_col})")
		ax.set_ylabel(f"Experiment ({y_col})")
		ax.set_title(title)
		ax.grid(True, alpha=0.3)
		ax.legend(fontsize=8)

	fig.suptitle("Bazarov-Informed Theory vs Experiment Parity")
	fig.tight_layout()
	fig.savefig(out_dir / "bazarov_parity_overview.png", dpi=150)
	plt.close(fig)

	# Plot 4: Overlaid mass flow vs Cd (theoretical + experimental) for S and RS
	fig, (ax_ipa, ax_lox) = plt.subplots(1, 2, figsize=(15, 6))

	def plot_overlay_stage(
		ax,
		x_col: str,
		y_exp_col: str,
		y_theory_col: str,
		title: str,
		xlabel: str,
		ylabel: str,
	) -> None:
		eq_row = 0
		for sample_type, group in collated.groupby("sample_type"):
			for sample_label, plot_group, color, marker in split_sample_groups_by_recency(group):
				# Experimental series
				exp_df = pd.DataFrame({"x": plot_group[x_col], "y": plot_group[y_exp_col]}).dropna()
				ax.scatter(
					exp_df["x"],
					exp_df["y"],
					label=f"{sample_label} experimental",
					alpha=0.9,
					s=90,
					marker=marker,
					color=color,
				)
				if len(exp_df) > 1 and float(exp_df["x"].nunique()) > 1:
					m_exp, b_exp = np.polyfit(exp_df["x"], exp_df["y"], 1)
					x_line = np.linspace(exp_df["x"].min(), exp_df["x"].max(), 100)
					ax.plot(x_line, m_exp * x_line + b_exp, "-", color=color, linewidth=2)
					ax.text(
						0.02,
						0.96 - 0.06 * eq_row,
						f"{sample_label} exp: y = {m_exp:.4g}x + {b_exp:.4g}",
						transform=ax.transAxes,
						fontsize=8.5,
						color=color,
					)
					eq_row += 1

			# Theoretical series
				th_df = pd.DataFrame({"x": plot_group[x_col], "y": plot_group[y_theory_col]}).dropna()
				ax.scatter(
					th_df["x"],
					th_df["y"],
					label=f"{sample_label} theoretical",
					alpha=0.95,
					s=95,
					marker="x",
					color=color,
				)
				if len(th_df) > 1 and float(th_df["x"].nunique()) > 1:
					m_th, b_th = np.polyfit(th_df["x"], th_df["y"], 1)
					x_line = np.linspace(th_df["x"].min(), th_df["x"].max(), 100)
					ax.plot(x_line, m_th * x_line + b_th, "--", color=color, linewidth=1.7)
					ax.text(
						0.02,
						0.96 - 0.06 * eq_row,
						f"{sample_label} th: y = {m_th:.4g}x + {b_th:.4g}",
						transform=ax.transAxes,
						fontsize=8.5,
						color=color,
					)
					eq_row += 1

		ax.set_title(title)
		ax.set_xlabel(xlabel)
		ax.set_ylabel(ylabel)
		ax.grid(True, alpha=0.3)
		ax.legend(fontsize=8)

	if all(col in collated.columns for col in ["Flow1_kgs_mean", "mu_1_mean", "stage2_cd_2"]):
		plot_overlay_stage(
			ax_ipa,
			x_col="Flow1_kgs_mean",
			y_exp_col="mu_1_mean",
			y_theory_col="stage2_cd_2",
			title="Stage 2 (IPA): Mass Flow vs Cd",
			xlabel="Mass Flow Rate (kg/s)",
			ylabel="Cd",
		)

	if all(col in collated.columns for col in ["Flow2_kgs_mean", "mu_2_mean", "stage1_cd_1"]):
		plot_overlay_stage(
			ax_lox,
			x_col="Flow2_kgs_mean",
			y_exp_col="mu_2_mean",
			y_theory_col="stage1_cd_1",
			title="Stage 1 (LOX): Mass Flow vs Cd",
			xlabel="Mass Flow Rate (kg/s)",
			ylabel="Cd",
		)

	fig.suptitle("Overlaid Mass Flow vs Cd (Theoretical + Experimental, S + RS)")
	fig.tight_layout()
	out_path = out_dir / "cd_vs_mdot_overlay.png"
	fig.savefig(out_path, dpi=150)
	plt.close(fig)


def write_design_sweep_outputs(collated: pd.DataFrame, out_dir: Path) -> None:
	if collated.empty:
		return

	work = collated.copy()
	# Geometric flow-capacity terms based on nozzle area and measured mu/Cd.
	if "stage2_nozzle_inner_radius_mm" in work.columns and "mu_1_mean" in work.columns:
		r2_m = work["stage2_nozzle_inner_radius_mm"] * 1e-3
		work["stage2_area_m2"] = np.pi * (r2_m ** 2)
		work["stage2_cda_exp"] = work["mu_1_mean"] * work["stage2_area_m2"]
	if "stage1_nozzle_inner_radius_mm" in work.columns and "mu_2_mean" in work.columns:
		r1_m = work["stage1_nozzle_inner_radius_mm"] * 1e-3
		work["stage1_area_m2"] = np.pi * (r1_m ** 2)
		work["stage1_cda_exp"] = work["mu_2_mean"] * work["stage1_area_m2"]

	fig, axes = plt.subplots(2, 2, figsize=(13, 10))

	def _scatter_with_labels(ax, plot_group, x_col, y_col, color, marker, label):
		"""Scatter points and annotate each with its sample id (e.g. S3, RS10)."""
		xv = plot_group[x_col]
		yv = plot_group[y_col]
		ax.scatter(xv, yv, label=label, color=color, marker=marker, s=90, alpha=0.9)
		if "sample_type" in plot_group.columns and "sample_index" in plot_group.columns:
			for _, row in plot_group.iterrows():
				stype = str(row.get("sample_type", ""))
				sidx  = row.get("sample_index", "")
				tag   = f"{stype}{int(sidx)}" if pd.notna(sidx) else stype
				xval  = row.get(x_col)
				yval  = row.get(y_col)
				if pd.notna(xval) and pd.notna(yval):
					ax.annotate(tag, (xval, yval), xytext=(4, 4),
					            textcoords="offset points", fontsize=7.5, color=color)

	# Stage 2 (IPA): CdA_exp vs nozzle radius
	if all(col in work.columns for col in ["stage2_nozzle_inner_radius_mm", "stage2_cda_exp"]):
		ax = axes[0, 0]
		for sample_type, grp in work.groupby("sample_type"):
			for label, plot_group, color, marker in split_sample_groups_by_recency(grp):
				_scatter_with_labels(ax, plot_group, "stage2_nozzle_inner_radius_mm", "stage2_cda_exp", color, marker, label)
		ax.set_title("Stage 2 (IPA): CdA_exp vs nozzle radius")
		ax.set_xlabel("Nozzle inner radius (mm)")
		ax.set_ylabel("CdA_exp (m²)")
		ax.grid(True, alpha=0.3)
		ax.legend()
	else:
		axes[0, 0].axis("off")

	# Stage 1 (LOX): CdA_exp vs nozzle radius
	if all(col in work.columns for col in ["stage1_nozzle_inner_radius_mm", "stage1_cda_exp"]):
		ax = axes[0, 1]
		for sample_type, grp in work.groupby("sample_type"):
			for label, plot_group, color, marker in split_sample_groups_by_recency(grp):
				_scatter_with_labels(ax, plot_group, "stage1_nozzle_inner_radius_mm", "stage1_cda_exp", color, marker, label)
		ax.set_title("Stage 1 (LOX): CdA_exp vs nozzle radius")
		ax.set_xlabel("Nozzle inner radius (mm)")
		ax.set_ylabel("CdA_exp (m²)")
		ax.grid(True, alpha=0.3)
		ax.legend()
	else:
		axes[0, 1].axis("off")

	# Stage 2 (IPA): mdot vs inlet radius
	if all(col in work.columns for col in ["stage2_inlet_radius_mm", "Flow1_kgs_mean"]):
		ax = axes[1, 0]
		for sample_type, grp in work.groupby("sample_type"):
			for label, plot_group, color, marker in split_sample_groups_by_recency(grp):
				_scatter_with_labels(ax, plot_group, "stage2_inlet_radius_mm", "Flow1_kgs_mean", color, marker, label)
		ax.set_title("Stage 2 (IPA): mdot vs inlet radius")
		ax.set_xlabel("Inlet radius (mm)")
		ax.set_ylabel("mdot_exp (kg/s)")
		ax.grid(True, alpha=0.3)
		ax.legend()
	else:
		axes[1, 0].axis("off")

	# Stage 1 (LOX): mdot vs inlet radius
	if all(col in work.columns for col in ["stage1_inlet_radius_mm", "Flow2_kgs_mean"]):
		ax = axes[1, 1]
		for sample_type, grp in work.groupby("sample_type"):
			for label, plot_group, color, marker in split_sample_groups_by_recency(grp):
				_scatter_with_labels(ax, plot_group, "stage1_inlet_radius_mm", "Flow2_kgs_mean", color, marker, label)
		ax.set_title("Stage 1 (LOX): mdot vs inlet radius")
		ax.set_xlabel("Inlet radius (mm)")
		ax.set_ylabel("mdot_exp (kg/s)")
		ax.grid(True, alpha=0.3)
		ax.legend()
	else:
		axes[1, 1].axis("off")

	fig.suptitle("Design Sweep Plots (for new resin iteration)")
	fig.tight_layout()
	fig.savefig(out_dir / "design_sweep_overview.png", dpi=150)
	plt.close(fig)


def write_resin_to_copper_guidance(
	collated: pd.DataFrame,
	out_dir: Path,
	target_copper_flow_stage2: float | None,
	target_copper_flow_stage1: float | None,
) -> None:
	if collated.empty:
		return

	work = collated.copy()
	required = {
		"stage2": ("Flow1_kgs_mean", "stage2_mass_flow_rate_kgps"),
		"stage1": ("Flow2_kgs_mean", "stage1_mass_flow_rate_kgps"),
	}

	for stage, (exp_col, theory_col) in required.items():
		if exp_col in work.columns and theory_col in work.columns:
			work[f"{stage}_k_mdot"] = work[exp_col] / work[theory_col].replace(0, np.nan)

	# Compute copper/resin transfer multipliers from available S and RS sets.
	available_k_cols = [c for c in ["stage2_k_mdot", "stage1_k_mdot"] if c in work.columns]
	if not available_k_cols:
		return

	transfer_rows = []
	target_map = {
		"stage2": target_copper_flow_stage2,
		"stage1": target_copper_flow_stage1,
	}
	for stage in ["stage2", "stage1"]:
		k_col = f"{stage}_k_mdot"
		if k_col not in work.columns:
			continue
		k_s = float(work.loc[work["sample_type"] == "S", k_col].mean()) if (work["sample_type"] == "S").any() else np.nan
		k_rs = float(work.loc[work["sample_type"] == "RS", k_col].mean()) if (work["sample_type"] == "RS").any() else np.nan
		cu_over_rs = k_s / k_rs if pd.notna(k_s) and pd.notna(k_rs) and abs(k_rs) > 0 else np.nan

		target_cu = target_map.get(stage)
		req_theory = target_cu / k_s if target_cu is not None and pd.notna(k_s) and abs(k_s) > 0 else np.nan
		expected_resin = k_rs * req_theory if pd.notna(k_rs) and pd.notna(req_theory) else np.nan
		gate_resin = target_cu / cu_over_rs if target_cu is not None and pd.notna(cu_over_rs) and abs(cu_over_rs) > 0 else np.nan

		transfer_rows.append({
			"stage": stage,
			"k_copper_mean": k_s,
			"k_resin_mean": k_rs,
			"copper_over_resin_multiplier": cu_over_rs,
			"target_copper_mdot": target_cu if target_cu is not None else np.nan,
			"required_theory_mdot_for_copper": req_theory,
			"expected_resin_mdot_at_that_theory": expected_resin,
			"resin_gate_mdot_to_hit_copper_target": gate_resin,
		})

	pd.DataFrame(transfer_rows).to_csv(out_dir / "resin_copper_guidance.csv", index=False)


def write_kextra_design_sweep(
	collated: pd.DataFrame,
	out_dir: Path,
	target_copper_flow_stage2: float | None,
	target_copper_flow_stage1: float | None,
	base_source: str,
	k_extra_values: list[float],
) -> None:
	"""
	Create final-round design table using updated base K_mdot and a K_extra sweep.
	mdot_design = mdot_target / (K_base * K_extra)
	"""
	if collated.empty:
		return

	work = collated.copy()
	stage_cfg = {
		"stage2": {
			"stage_name": "Stage 2 (IPA)",
			"exp_col": "Flow1_kgs_mean",
			"theory_col": "stage2_mass_flow_rate_kgps",
			"target": target_copper_flow_stage2,
		},
		"stage1": {
			"stage_name": "Stage 1 (LOX)",
			"exp_col": "Flow2_kgs_mean",
			"theory_col": "stage1_mass_flow_rate_kgps",
			"target": target_copper_flow_stage1,
		},
	}

	for cfg in stage_cfg.values():
		if cfg["exp_col"] in work.columns and cfg["theory_col"] in work.columns:
			work[cfg["exp_col"]] = pd.to_numeric(work[cfg["exp_col"]], errors="coerce")
			work[cfg["theory_col"]] = pd.to_numeric(work[cfg["theory_col"]], errors="coerce")

	def group_mean_k(df: pd.DataFrame, exp_col: str, theory_col: str) -> float:
		if exp_col not in df.columns or theory_col not in df.columns:
			return np.nan
		vals = df[exp_col] / df[theory_col].replace(0, np.nan)
		vals = pd.to_numeric(vals, errors="coerce").dropna()
		return float(vals.mean()) if not vals.empty else np.nan

	rs_new = work[(work["sample_type"] == "RS") & (work["sample_index"] >= NEW_SAMPLE_INDEX_MIN)].copy()
	rs_baseline = work[(work["sample_type"] == "RS") & (work["sample_index"] < NEW_SAMPLE_INDEX_MIN)].copy()
	copper = work[work["sample_type"] == "S"].copy()

	def choose_base_k(exp_col: str, theory_col: str, requested: str) -> tuple[float, str]:
		requested_u = requested.upper().strip()
		k_rs_new = group_mean_k(rs_new, exp_col, theory_col)
		k_rs_base = group_mean_k(rs_baseline, exp_col, theory_col)
		k_copper = group_mean_k(copper, exp_col, theory_col)

		if requested_u == "RS_NEW":
			if pd.notna(k_rs_new):
				return k_rs_new, "RS_NEW"
		elif requested_u == "RS_BASELINE":
			if pd.notna(k_rs_base):
				return k_rs_base, "RS_BASELINE"
		elif requested_u == "COPPER":
			if pd.notna(k_copper):
				return k_copper, "COPPER"

		# AUTO or fallback path: prefer RS new, then RS baseline, then copper.
		if pd.notna(k_rs_new):
			return k_rs_new, "RS_NEW"
		if pd.notna(k_rs_base):
			return k_rs_base, "RS_BASELINE"
		if pd.notna(k_copper):
			return k_copper, "COPPER"
		return np.nan, "UNAVAILABLE"

	# Compute K factors for both stages
	stage2_target = stage_cfg["stage2"]["target"]
	stage1_target = stage_cfg["stage1"]["target"]
	stage2_k, stage2_source = choose_base_k(stage_cfg["stage2"]["exp_col"], stage_cfg["stage2"]["theory_col"], base_source)
	stage1_k, stage1_source = choose_base_k(stage_cfg["stage1"]["exp_col"], stage_cfg["stage1"]["theory_col"], base_source)

	# Check validity
	if (stage2_target is None or pd.isna(stage2_target) or pd.isna(stage2_k) or abs(stage2_k) < 1e-12 or
		stage1_target is None or pd.isna(stage1_target) or pd.isna(stage1_k) or abs(stage1_k) < 1e-12):
		return

	rows = []
	for k_extra in k_extra_values:
		stage2_eff_k = stage2_k * k_extra
		stage1_eff_k = stage1_k * k_extra
		stage2_design = stage2_target / stage2_eff_k if abs(stage2_eff_k) > 1e-12 else np.nan
		stage1_design = stage1_target / stage1_eff_k if abs(stage1_eff_k) > 1e-12 else np.nan
		rows.append({
			"k_mdot_extra": k_extra,
			"k_base_source": stage2_source,
			"stage2_target_mdot_kgps": stage2_target,
			"stage2_k_base": stage2_k,
			"stage2_k_effective": stage2_eff_k,
			"stage2_mdot_design_kgps": stage2_design,
			"stage1_target_mdot_kgps": stage1_target,
			"stage1_k_base": stage1_k,
			"stage1_k_effective": stage1_eff_k,
			"stage1_mdot_design_kgps": stage1_design,
		})

	if not rows:
		return

	out_df = pd.DataFrame(rows)
	out_csv = out_dir / "final_round_kextra_design_sweep.csv"
	out_df.to_csv(out_csv, index=False)

	lines = [
		"=" * 100,
		"FINAL ROUND K_EXTRA DESIGN SWEEP (Stage 2 IPA and Stage 1 LOX paired)",
		"=" * 100,
		"Formula:",
		"  mdot_design = target_mdot / (K_base * K_mdot_extra)",
		f"",
		f"Requested base source: {base_source}",
		f"K_mdot_extra sweep: {k_extra_values}",
		f"Target flows: Stage2 IPA={stage2_target:.6f} kg/s, Stage1 LOX={stage1_target:.6f} kg/s",
		"",
		out_df.to_string(index=False),
		"",
	]
	out_txt = out_dir / "final_round_kextra_design_sweep.txt"
	out_txt.write_text("\n".join(lines), encoding="utf-8")

	print(f"Saved K_extra design sweep: {out_csv}")


def write_final_round_recommendation(
	collated: pd.DataFrame,
	out_dir: Path,
	target_copper_flow_stage2: float | None,
	target_copper_flow_stage1: float | None,
) -> dict:
	"""
	Recommend K_extra and Bazarov mdot_theory inputs for the final resin round.

	Prediction chain
	----------------
	  transfer_ratio   = K_copper_mean / K_rs_baseline_mean     (from identical S vs RS0-7 pairs)
	  pred_copper_MFR  = mdot_resin_exp × transfer_ratio        (for each RS8+ sample)

	K_extra (the "Additional correction factor" in the report) is read directly
	from the parsed report columns (empirical_additional_correction_factor).
	A linear fit across the 5-point sweep gives the K_extra that hits each target.
	Because K_extra is applied identically to both stages, a paired optimum is also
	computed (minimises combined squared error).

	Outputs
	-------
	  final_round_recommendation.txt
	  final_round_recommendation.csv
	  final_round_sweep.png
	"""
	if collated.empty:
		return {}

	stage_cfg = {
		"stage2_ipa": {
			"stage_name": "Stage 2 (IPA)",
			"exp_col": "Flow1_kgs_mean",
			"theory_col": "stage2_mass_flow_rate_kgps",
			"target": target_copper_flow_stage2,
			"color": "tab:red",
			"bazarov_k_corr_col": "empirical_ipa_correction_factor",
			"bazarov_k_corr_name": "IPA correction factor",
		},
		"stage1_lox": {
			"stage_name": "Stage 1 (LOX)",
			"exp_col": "Flow2_kgs_mean",
			"theory_col": "stage1_mass_flow_rate_kgps",
			"target": target_copper_flow_stage1,
			"color": "tab:purple",
			"bazarov_k_corr_col": "empirical_lox_correction_factor",
			"bazarov_k_corr_name": "LOX correction factor",
		},
	}

	KEXTRA_COL = "empirical_additional_correction_factor"

	work = collated.copy()
	for cfg in stage_cfg.values():
		for col in [cfg["exp_col"], cfg["theory_col"]]:
			if col in work.columns:
				work[col] = pd.to_numeric(work[col], errors="coerce")
	if KEXTRA_COL in work.columns:
		work[KEXTRA_COL] = pd.to_numeric(work[KEXTRA_COL], errors="coerce")

	copper   = work[work["sample_type"] == "S"].copy()
	rs_base  = work[(work["sample_type"] == "RS") & (work["sample_index"] < NEW_SAMPLE_INDEX_MIN)].copy()
	rs_new   = work[(work["sample_type"] == "RS") & (work["sample_index"] >= NEW_SAMPLE_INDEX_MIN)].copy().sort_values("sample_index")

	if rs_new.empty:
		print("[warn] No new RS samples found — cannot generate final round recommendation.")
		return {}

	lines: list[str] = []
	lines.append("=" * 100)
	lines.append("FINAL ROUND RESIN RECOMMENDATION")
	lines.append("=" * 100)
	lines.append("")
	lines.append("Prediction chain:")
	lines.append("  transfer_ratio  = K_copper_mean / K_rs_baseline_mean  (identical-geometry S vs RS0-7 pairs)")
	lines.append("  pred_copper_MFR = mdot_resin_exp × transfer_ratio")
	lines.append("  optimal K_extra = interpolated from linear fit of (K_extra, pred_copper_MFR) per stage")
	lines.append("")
	lines.append(f"  New RS samples in sweep (RS{NEW_SAMPLE_INDEX_MIN}+): {sorted(rs_new['sample_index'].dropna().astype(int).tolist())}")
	if KEXTRA_COL in rs_new.columns:
		kextra_vals = rs_new[KEXTRA_COL].dropna().tolist()
		lines.append(f"  K_extra values parsed from reports: {kextra_vals}")
	lines.append("")

	rec_rows: list[dict] = []
	stage_sweep: dict[str, dict] = {}  # for plotting

	for stage_key, cfg in stage_cfg.items():
		stage_name = cfg["stage_name"]
		exp_col    = cfg["exp_col"]
		theory_col = cfg["theory_col"]
		target     = cfg["target"]
		color      = cfg["color"]

		lines.append(f">>> {stage_name}")
		lines.append("-" * 100)

		if exp_col not in work.columns or theory_col not in work.columns:
			lines.append(f"  Missing columns ({exp_col}, {theory_col}) — skipped.")
			lines.append("")
			continue

		def k_series(df: pd.DataFrame) -> pd.Series:
			return (pd.to_numeric(df[exp_col], errors="coerce") /
					pd.to_numeric(df[theory_col], errors="coerce").replace(0, np.nan)).dropna()

		k_cu   = k_series(copper)
		k_rb   = k_series(rs_base)

		k_cu_mean  = float(k_cu.mean())  if not k_cu.empty  else np.nan
		k_cu_std   = float(k_cu.std())   if len(k_cu)  > 1  else 0.0
		k_rb_mean  = float(k_rb.mean())  if not k_rb.empty  else np.nan
		k_rb_std   = float(k_rb.std())   if len(k_rb)  > 1  else 0.0

		lines.append(f"  K_mdot copper (S):        {k_cu_mean:.6f} ± {k_cu_std:.6f}  (n={len(k_cu)})")
		lines.append(f"  K_mdot resin baseline:    {k_rb_mean:.6f} ± {k_rb_std:.6f}  (n={len(k_rb)})")

		if pd.isna(k_cu_mean) or pd.isna(k_rb_mean) or abs(k_rb_mean) < 1e-12:
			lines.append("  Cannot compute transfer ratio — skipped.")
			lines.append("")
			continue

		transfer = k_cu_mean / k_rb_mean
		if abs(k_cu_mean) > 1e-12 and abs(k_rb_mean) > 1e-12:
			transfer_unc = transfer * np.sqrt((k_cu_std / k_cu_mean) ** 2 + (k_rb_std / k_rb_mean) ** 2)
		else:
			transfer_unc = np.nan

		lines.append(f"  Transfer ratio (Cu/RS_base): {transfer:.6f} ± {transfer_unc:.6f}")
		lines.append("")

		# --- Build sweep table ---
		# Infer per-element target from RS new report mdot_theory values.
		# Bazarov was run with target_total/N as the per-element design target, so
		# mdot_theory in the report = target_total / N_elements. Infer N from the data.
		sweep_rows: list[dict] = []
		theory_vals_for_n: list[float] = []
		for _, row in rs_new.iterrows():
			exp_v    = float(pd.to_numeric(row.get(exp_col,    np.nan), errors="coerce"))
			th_v     = float(pd.to_numeric(row.get(theory_col, np.nan), errors="coerce"))
			kextra_v = float(pd.to_numeric(row.get(KEXTRA_COL, np.nan), errors="coerce")) if KEXTRA_COL in row.index else np.nan
			if pd.isna(exp_v) or pd.isna(th_v) or abs(th_v) < 1e-12:
				continue
			theory_vals_for_n.append(th_v)
			sweep_rows.append({
				"sample_index": int(row["sample_index"]),
				"sample_id": format_sample_id_from_row(row),
				"k_extra":      kextra_v,
				"mdot_theory":  th_v,
				"mdot_exp":     exp_v,
			})

		if not sweep_rows:
			lines.append("  No valid RS new data — skipped.")
			lines.append("")
			continue

		# Per-element target: infer N from mean(mdot_theory) since mdot_theory = target_total/N
		mean_theory = float(np.mean(theory_vals_for_n))
		if target is not None and not pd.isna(target) and abs(mean_theory) > 1e-12:
			n_elements = max(1, round(target / mean_theory))
			target_per_elem = target / n_elements
		else:
			n_elements = 1
			target_per_elem = target if target is not None else np.nan

		lines.append(f"  Inferred N elements from reports: {n_elements}  "
					 f"(target_total={target:.6f} / mean_theory={mean_theory:.6f})")
		lines.append(f"  Per-element target: {target_per_elem:.6f} kg/s")
		lines.append("")

		# Compute pred_cu and error using per-element target
		for r in sweep_rows:
			r["k_mdot"]           = r["mdot_exp"] / r["mdot_theory"]
			r["pred_copper"]      = r["mdot_exp"] * transfer
			r["pct_err_vs_target"] = (
				100.0 * (r["pred_copper"] - target_per_elem) / target_per_elem
				if not pd.isna(target_per_elem) and abs(target_per_elem) > 0 else np.nan
			)

		# Stats on K_mdot across new samples (K relative to per-element theory)
		k_rn_vals = [r["k_mdot"] for r in sweep_rows]
		k_rn_mean = float(np.mean(k_rn_vals))
		k_rn_std  = float(np.std(k_rn_vals))
		lines.append(f"  K_mdot resin new (RS{NEW_SAMPLE_INDEX_MIN}+): {k_rn_mean:.6f} ± {k_rn_std:.6f}  (n={len(k_rn_vals)})")
		lines.append("")

		# Sweep table
		hdr = f"  {'RS':>4}  {'K_extra':>8}  {'mdot_theory':>12}  {'mdot_exp':>10}  {'pred_Cu/elem':>13}  {'err% vs tgt/elem':>17}"
		lines.append(hdr)
		lines.append("  " + "-" * 72)
		for r in sweep_rows:
			ke_s  = f"{r['k_extra']:.2f}" if not pd.isna(r["k_extra"]) else "  N/A"
			err_s = f"{r['pct_err_vs_target']:+.2f}%" if not pd.isna(r["pct_err_vs_target"]) else "  N/A"
			sample_label = str(r.get("sample_id") or f"RS{int(r['sample_index'])}")
			lines.append(
				f"  {sample_label:>8}  {ke_s:>8}  {r['mdot_theory']:>12.6f}  "
				f"{r['mdot_exp']:>10.6f}  {r['pred_copper']:>13.6f}  {err_s:>17}"
			)
		lines.append("")

		# Linear fit: pred_copper_per_elem vs k_extra — interpolate to target_per_elem
		ke_arr   = np.array([r["k_extra"]   for r in sweep_rows if not pd.isna(r["k_extra"])])
		pred_arr = np.array([r["pred_copper"] for r in sweep_rows if not pd.isna(r["k_extra"])])
		best_k_extra = np.nan
		coeffs = None
		if len(ke_arr) >= 2 and not pd.isna(target_per_elem):
			coeffs = np.asarray(np.polyfit(ke_arr.tolist(), pred_arr.tolist(), 1), dtype=float)
			slope, intercept = float(coeffs[0]), float(coeffs[1])
			if abs(slope) > 1e-12:
				best_k_extra = (target_per_elem - intercept) / slope

		# Residual scatter from fit as proxy for uncertainty on K_extra
		k_extra_unc = np.nan
		if coeffs is not None and len(ke_arr) >= 3:
			residuals = np.asarray(pred_arr, dtype=float) - np.asarray(np.polyval(coeffs, ke_arr.tolist()), dtype=float)
			pred_rmse = float(np.sqrt(np.mean(residuals ** 2)))
			# Propagate RMSE in pred_cu to uncertainty in K_extra via fit slope
			if abs(float(coeffs[0])) > 1e-12:
				k_extra_unc = pred_rmse / abs(float(coeffs[0]))

		if coeffs is not None and not pd.isna(best_k_extra):
			unc_str = f" ± {k_extra_unc:.4f}" if not pd.isna(k_extra_unc) else ""
			lines.append(f"  Linear fit: pred_Cu/elem = {coeffs[0]:.6f} × K_extra + {coeffs[1]:.6f}")
			lines.append(f"  Per-element target:    {target_per_elem:.6f} kg/s  (= {target:.6f} / {n_elements} elements)")
			lines.append(f"  RECOMMENDED K_extra:   {best_k_extra:.4f}{unc_str}")
			lines.append(f"  Bazarov mdot_theory:   {target_per_elem:.6f} kg/s  (unchanged — K_extra only changes geometry)")
			lines.append(f"  Transfer ratio unc:    ± {transfer_unc:.6f}  (adds systematic error to pred_Cu/elem)")
			lines.append("")

		rec_rows.append({
			"stage":                       stage_key,
			"stage_name":                  stage_name,
			"target_total_mdot_kgps":      target,
			"n_elements":                  n_elements,
			"target_per_elem_mdot_kgps":   target_per_elem,
			"k_copper_mean":               k_cu_mean,
			"k_copper_std":                k_cu_std,
			"k_rs_baseline_mean":          k_rb_mean,
			"k_rs_baseline_std":           k_rb_std,
			"transfer_ratio":              transfer,
			"transfer_ratio_unc":          transfer_unc,
			"k_rs_new_mean":               k_rn_mean,
			"k_rs_new_std":                k_rn_std,
			"recommended_k_extra":         best_k_extra,
			"k_extra_fit_unc":             k_extra_unc,
			"bazarov_mdot_theory_kgps":    target_per_elem,
		})

		stage_sweep[stage_key] = {
			"cfg":             cfg,
			"ke_arr":          ke_arr,
			"pred_arr":        pred_arr,
			"target_per_elem": target_per_elem,
			"best_k_extra":    best_k_extra,
			"coeffs":          coeffs if len(ke_arr) >= 2 else None,
			"color":           color,
			"stage_name":      stage_name,
			"sweep_rows":      sweep_rows,
			"n_elements":      n_elements,
		}

	# --- Paired optimum (same K_extra applied to both stages) ---
	if len(stage_sweep) == 2:
		lines.append(">>> PAIRED OPTIMUM (single K_extra for both stages)")
		lines.append("-" * 100)
		results = list(stage_sweep.values())
		targets_pe = [r["target_per_elem"] for r in results]  # per-element targets
		coeff_list = [r["coeffs"] for r in results]

		if all(c is not None for c in coeff_list) and all(t is not None and not pd.isna(t) for t in targets_pe):
			# Sweep K_extra on a fine grid and minimise combined squared % error (per-element)
			k_extra_grid = np.linspace(
				min(r["ke_arr"].min() for r in results) - 0.1,
				max(r["ke_arr"].max() for r in results) + 0.1,
				2000,
			)
			combined_err = np.zeros_like(k_extra_grid)
			for res, tgt_pe in zip(results, targets_pe):
				pred_grid = np.polyval(res["coeffs"], k_extra_grid)
				combined_err += ((pred_grid - tgt_pe) / tgt_pe * 100) ** 2

			best_idx = int(np.argmin(combined_err))
			best_k_paired = float(k_extra_grid[best_idx])
			lines.append(f"  Best paired K_extra (min combined squared % error, per-element): {best_k_paired:.4f}")
			lines.append("")
			lines.append(f"  {'Stage':<20}  {'Tgt/elem (kg/s)':>16}  {'Pred/elem @ K*':>16}  {'Error %':>8}")
			lines.append("  " + "-" * 68)
			for res, tgt_pe in zip(results, targets_pe):
				pred_at_best = float(np.polyval(res["coeffs"], best_k_paired))
				err_pct = 100.0 * (pred_at_best - tgt_pe) / tgt_pe if abs(tgt_pe) > 0 else np.nan
				lines.append(
					f"  {res['stage_name']:<20}  {tgt_pe:>16.6f}  {pred_at_best:>16.6f}  {err_pct:>+8.2f}%"
				)
			lines.append("")
			lines.append(f"  >>> COMMIT K_extra = {best_k_paired:.4f} for final resin round")
			lines.append(f"      Bazarov mdot_theory per element (unchanged for both stages):")
			for rec in rec_rows:
				if rec["stage"] in stage_sweep:
					lines.append(
						f"        {rec['stage_name']}: {rec['bazarov_mdot_theory_kgps']:.6f} kg/s"
					)
			lines.append("")

	# --- New Bazarov correction factors for the final round ---
	#
	# Strategy: update K_corr per stage independently so that the nominal design
	# point (K_extra = 1.0) is centred on the copper per-element target.
	#
	# At K_extra = 1.0, nozzle area ∝ 1/K_corr, so actual flow ∝ 1/K_corr.
	# To shift pred_copper from current_nominal to target:
	#   K_corr_new = K_corr_old × (pred_copper_nominal / target_per_elem)
	#
	# Nominal pred_copper is read directly from the K_extra=1.0 data point (most
	# reliable) with the linear-fit value at K=1.0 shown alongside for reference.
	# Non-linearity between the two is flagged — large divergence means the fit
	# is unreliable and the direct measurement should be trusted more.
	lines.append(">>> NEW BAZAROV CORRECTION FACTORS FOR FINAL ROUND")
	lines.append("-" * 100)
	lines.append("  Formula: K_corr_new = K_corr_old × (pred_copper_at_Kextra1 / target_per_elem)")
	lines.append("  This centres K_extra = 1.0 on the per-element copper target.")
	lines.append("")

	new_k_corr_results: dict[str, dict] = {}

	for stage_key, res in stage_sweep.items():
		cfg           = res["cfg"]
		stage_name    = res["stage_name"]
		sweep_rows_s  = res["sweep_rows"]
		target_pe     = res["target_per_elem"]
		n_elem        = res["n_elements"]
		k_corr_col    = cfg["bazarov_k_corr_col"]
		k_corr_name   = cfg["bazarov_k_corr_name"]

		# Current K_corr from the parsed RS new report data
		k_corr_vals = pd.to_numeric(
			rs_new[k_corr_col] if k_corr_col in rs_new.columns else pd.Series(dtype=float),
			errors="coerce",
		).dropna()
		if k_corr_vals.empty:
			lines.append(f"  {stage_name}: {k_corr_col} not found in reports — skipped.")
			lines.append("")
			continue
		k_corr_old = float(k_corr_vals.mean())

		# Direct measurement: find sweep_row with K_extra closest to 1.0
		valid_rows = [r for r in sweep_rows_s if not pd.isna(r["k_extra"])]
		if not valid_rows:
			lines.append(f"  {stage_name}: no sweep data — skipped.")
			lines.append("")
			continue
		nominal_row = min(valid_rows, key=lambda r: abs(r["k_extra"] - 1.0))
		pred_nominal_direct = nominal_row["pred_copper"]
		k_extra_nominal = nominal_row["k_extra"]

		if abs(target_pe) < 1e-12 or pd.isna(target_pe):
			lines.append(f"  {stage_name}: target per element is zero/NaN — skipped.")
			lines.append("")
			continue

		# K_corr_new = K_corr_old × K_extra_recommended
		#
		# Rationale: K_total = K_corr × K_extra is the governing variable (total Bazarov
		# correction).  The fit already found K_extra* where ratio=1.0 in K_extra space.
		# Centring K_extra=1.0 on that point means scaling K_corr by K_extra*, so:
		#   K_corr_new = K_corr_old × K_extra*  →  K_total_new_nominal = K_corr_new × 1.0
		# This is identical to K_total_cross from the K_total-space fit, so the star and
		# dashed line in the funnel plot coincide by construction.
		best_k_extra_stage = stage_sweep[stage_key]["best_k_extra"]
		k_corr_new = k_corr_old * best_k_extra_stage if not pd.isna(best_k_extra_stage) else np.nan

		# Reference: pred_copper at current K_extra=1.0 (RS sample closest to K=1)
		pred_nominal_direct = nominal_row["pred_copper"]
		pred_nominal_fit    = float(np.polyval(res["coeffs"], 1.0)) if res["coeffs"] is not None else np.nan

		lines.append(f"  {stage_name}:")
		lines.append(f"    Current {k_corr_name}:    {k_corr_old:.6f}")
		lines.append(f"    Recommended K_extra*:      {best_k_extra_stage:.4f}  (from per-stage fit)")
		lines.append(f"    NEW {k_corr_name}:         {k_corr_new:.6f}  (= K_corr_old × K_extra*)")
		lines.append(f"    K_total nominal (new×1.0): {k_corr_new:.6f}")
		lines.append(f"    --- reference: pred_copper/elem at current K_extra=1.0 ---")
		direct_label = str(nominal_row.get("sample_id") or f"RS{int(nominal_row['sample_index'])}")
		lines.append(f"      Direct {direct_label}:  {pred_nominal_direct:.6f} kg/s  (ratio={pred_nominal_direct/target_pe:.3f})")
		if not pd.isna(pred_nominal_fit):
			lines.append(f"      Linear fit:  {pred_nominal_fit:.6f} kg/s  (ratio={pred_nominal_fit/target_pe:.3f})")
		lines.append(f"    Target/elem:               {target_pe:.6f} kg/s  (= {res['cfg']['target']:.6f} / {n_elem})")
		lines.append("")

		new_k_corr_results[stage_key] = {
			"stage_name":          stage_name,
			"k_corr_name":         k_corr_name,
			"k_corr_old":          k_corr_old,
			"k_corr_new":          k_corr_new,
			"pred_nominal_direct": pred_nominal_direct,
			"pred_nominal_fit":    pred_nominal_fit,
			"target_per_elem":     target_pe,
			"n_elements":          n_elem,
			"best_k_extra":        best_k_extra_stage,
			"k_total_star":        k_corr_old * best_k_extra_stage if not pd.isna(best_k_extra_stage) else np.nan,
			"k_total_new_nominal": k_corr_new,  # = K_corr_new × 1.0 = K_total_cross
		}

	if new_k_corr_results:
		lines.append("  Summary — enter into Bazarov for the final resin round:")
		lines.append(f"  {'Factor':<28}  {'Old':>10}  {'New  (= Old × K_extra*)':>24}  {'K_extra*':>10}  {'K_total nom (new×1.0)':>22}")
		lines.append("  " + "-" * 102)
		for res_k in new_k_corr_results.values():
			ke_str  = f"{res_k['best_k_extra']:.4f}" if not pd.isna(res_k["best_k_extra"]) else "   N/A"
			new_str = f"{res_k['k_corr_new']:.6f}"   if not pd.isna(res_k["k_corr_new"])   else "     N/A"
			ktnom   = f"{res_k['k_total_new_nominal']:.6f}" if not pd.isna(res_k["k_total_new_nominal"]) else "     N/A"
			lines.append(
				f"  {res_k['k_corr_name']:<28}  {res_k['k_corr_old']:>10.6f}  "
				f"{new_str:>24}  {ke_str:>10}  {ktnom:>22}"
			)
		lines.append("")
		lines.append("  Formula: K_corr_new = K_corr_old × K_extra*  (shifts K_extra=1.0 onto the fit crossing point)")
		lines.append("  K_total nom = K_corr_new × 1.0 = K_total where ratio=1.0 from the sweep fit.")
		lines.append("  With new K_corr applied, sweep K_extra = 0.8–1.2 around the new nominal.")
		lines.append("")

	# --- Plot ---
	fig, (ax_sweep, ax_err) = plt.subplots(1, 2, figsize=(14, 6))

	for stage_key, res in stage_sweep.items():
		if res["coeffs"] is None:
			continue
		ke_arr      = res["ke_arr"]
		pred_arr    = res["pred_arr"]
		target_pe   = res["target_per_elem"]
		color       = res["color"]
		label       = res["stage_name"]

		# Scatter + fit line
		ax_sweep.scatter(ke_arr, pred_arr, color=color, s=120, zorder=5, label=f"{label} data")
		k_fit = np.linspace(ke_arr.min() - 0.15, ke_arr.max() + 0.15, 200)
		ax_sweep.plot(k_fit, np.polyval(res["coeffs"], k_fit), "--", color=color, linewidth=1.8)

		if target_pe is not None and not pd.isna(target_pe):
			ax_sweep.axhline(target_pe, color=color, linewidth=2, linestyle=":", alpha=0.7,
							 label=f"{label} target/elem")

		if not pd.isna(res["best_k_extra"]):
			ax_sweep.axvline(res["best_k_extra"], color=color, linewidth=1.4, linestyle="-.",
							 label=f"{label} optimal K={res['best_k_extra']:.3f}")

	# Paired optimum vertical line + error plot (using per-element targets)
	if len(stage_sweep) == 2:
		results_list = list(stage_sweep.values())
		tgts_pe = [r["target_per_elem"] for r in results_list]
		cl = [r["coeffs"] for r in results_list]
		if all(c is not None for c in cl) and all(t is not None and not pd.isna(t) for t in tgts_pe):
			k_extra_grid2 = np.linspace(
				min(r["ke_arr"].min() for r in results_list) - 0.1,
				max(r["ke_arr"].max() for r in results_list) + 0.1,
				2000,
			)
			comb_err2 = sum(
				((np.polyval(c, k_extra_grid2) - t) / t * 100) ** 2 for c, t in zip(cl, tgts_pe)
			)
			best_k_paired2 = float(k_extra_grid2[int(np.argmin(comb_err2))])
			ax_sweep.axvline(best_k_paired2, color="black", linewidth=2, linestyle="-",
							 label=f"Paired optimum K={best_k_paired2:.3f}")

			# Error plot: % error vs K_extra for each stage (per-element) and combined RMS
			for res, tgt_pe in zip(results_list, tgts_pe):
				pct_err_grid = (np.polyval(res["coeffs"], k_extra_grid2) - tgt_pe) / tgt_pe * 100
				ax_err.plot(k_extra_grid2, pct_err_grid, color=res["color"],
							linewidth=2, label=res["stage_name"])
			ax_err.plot(k_extra_grid2, np.sqrt(comb_err2 / 2), "k--",
						linewidth=1.8, label="RMS error (both stages)")
			ax_err.axvline(best_k_paired2, color="black", linewidth=2, linestyle="-",
						   label=f"Paired optimum K={best_k_paired2:.3f}")
			ax_err.axhline(0, color="gray", linewidth=1, linestyle=":")
			ax_err.set_title("Predicted Copper MFR Error vs K_extra (per element)")
			ax_err.set_xlabel("K_extra")
			ax_err.set_ylabel("Error vs per-element target (%)")
			ax_err.grid(True, alpha=0.3)
			ax_err.legend(fontsize=9)

	ax_sweep.set_title(f"Predicted Copper MFR/elem vs K_extra (RS{NEW_SAMPLE_INDEX_MIN}+ sweep)")
	ax_sweep.set_xlabel("K_extra")
	ax_sweep.set_ylabel("Predicted copper MFR per element (kg/s)")
	ax_sweep.grid(True, alpha=0.3)
	ax_sweep.legend(fontsize=9)

	fig.suptitle("Final Round Resin Decision — Sweep Calibration")
	fig.tight_layout()
	fig.savefig(out_dir / "final_round_sweep.png", dpi=150)
	plt.close(fig)

	text = "\n".join(lines)
	print(text)
	(out_dir / "final_round_recommendation.txt").write_text(text, encoding="utf-8")
	if rec_rows:
		pd.DataFrame(rec_rows).to_csv(out_dir / "final_round_recommendation.csv", index=False)
	print(f"Saved final round recommendation: {out_dir}/final_round_recommendation.*")
	return new_k_corr_results


def write_optimization_overview_plots(
	collated: pd.DataFrame,
	out_dir: Path,
	target_copper_flow_stage2: float | None,
	target_copper_flow_stage1: float | None,
	new_k_corr: dict | None = None,
) -> None:
	"""
	Two diagnostic figures for the manual optimisation process.

	Figure 1 — optimization_parity_sweep.png  (2 × 2)
	  Row 0: Bazarov theory vs actual MFR parity — IPA and LOX.
	          Slopes reveal K_mdot per group; RS8-12 annotated with K_extra.
	  Row 1: pred_copper/target (ratio to per-element target) vs K_extra for RS8-12.
	          Shows convergence toward 1.0.  Both stages on each panel for easy reading.

	Figure 2 — optimization_summary.png  (1 × 2)
	  Left:  Transfer ratio (K_Cu/K_RS) per identical-geometry pair (S0-7 vs RS0-7).
	          Validates that the transfer ratio is geometry-independent.
	  Right: Convergence funnel — pred_copper/target_per_elem vs K_extra, both stages,
	          with projection of where new K_corr centres K_extra = 1.0.
	"""
	if collated.empty:
		return

	KEXTRA_COL = "empirical_additional_correction_factor"

	stage_cfg = {
		"stage2_ipa": {
			"stage_name":  "Stage 2 (IPA)",
			"exp_col":     "Flow1_kgs_mean",
			"theory_col":  "stage2_mass_flow_rate_kgps",
			"k_corr_col":  "empirical_ipa_correction_factor",
			"target":      target_copper_flow_stage2,
			"color_s":     SAMPLE_COLOR_S,
			"color_rb":    SAMPLE_COLOR_RS_OLD,
			"color_sweep": "tab:red",
		},
		"stage1_lox": {
			"stage_name":  "Stage 1 (LOX)",
			"exp_col":     "Flow2_kgs_mean",
			"theory_col":  "stage1_mass_flow_rate_kgps",
			"k_corr_col":  "empirical_lox_correction_factor",
			"target":      target_copper_flow_stage1,
			"color_s":     "tab:cyan",
			"color_rb":    "tab:orange",
			"color_sweep": "tab:purple",
		},
	}

	work = collated.copy()
	for cfg in stage_cfg.values():
		for col in [cfg["exp_col"], cfg["theory_col"], cfg["k_corr_col"]]:
			if col in work.columns:
				work[col] = pd.to_numeric(work[col], errors="coerce")
	if KEXTRA_COL in work.columns:
		work[KEXTRA_COL] = pd.to_numeric(work[KEXTRA_COL], errors="coerce")

	copper  = work[work["sample_type"] == "S"].copy()
	rs_base = work[(work["sample_type"] == "RS") & (work["sample_index"] < NEW_SAMPLE_INDEX_MIN)].copy()
	rs_new  = work[(work["sample_type"] == "RS") & (work["sample_index"] >= NEW_SAMPLE_INDEX_MIN)].copy().sort_values("sample_index")

	# Fallback injector element count for cases where one stage has missing theory fields.
	n_elem_fallback = 12
	for cfg in stage_cfg.values():
		target = cfg["target"]
		theory_col = cfg["theory_col"]
		if theory_col not in rs_new.columns or target is None or pd.isna(target):
			continue
		th = pd.to_numeric(rs_new[theory_col], errors="coerce").dropna()
		if th.empty:
			continue
		cand = max(1, round(float(target) / float(th.mean())))
		n_elem_fallback = int(cand)
		break

	# Pre-compute transfer ratio and per-element info per stage
	stage_meta: dict[str, dict] = {}
	for stage_key, cfg in stage_cfg.items():
		exp_col    = cfg["exp_col"]
		theory_col = cfg["theory_col"]
		target     = cfg["target"]
		if exp_col not in work.columns or theory_col not in work.columns:
			stage_meta[stage_key] = {}
			continue
		k_cu = (copper[exp_col] / copper[theory_col].replace(0, np.nan)).dropna()
		k_rb = (rs_base[exp_col] / rs_base[theory_col].replace(0, np.nan)).dropna()
		k_cu_mean = float(k_cu.mean()) if not k_cu.empty else np.nan
		k_rb_mean = float(k_rb.mean()) if not k_rb.empty else np.nan
		k_cu_std  = float(k_cu.std())  if len(k_cu) > 1  else 0.0
		k_rb_std  = float(k_rb.std())  if len(k_rb) > 1  else 0.0
		transfer  = (k_cu_mean / k_rb_mean
					 if not pd.isna(k_cu_mean) and not pd.isna(k_rb_mean) and abs(k_rb_mean) > 1e-12
					 else np.nan)
		th_rn = pd.to_numeric(rs_new[theory_col], errors="coerce").dropna()
		if target is not None and not pd.isna(target) and not th_rn.empty:
			n_elem    = max(1, round(target / float(th_rn.mean())))
			target_pe = target / n_elem
		elif target is not None and not pd.isna(target):
			n_elem    = n_elem_fallback
			target_pe = target / n_elem
		else:
			n_elem    = 1
			target_pe = target
		stage_meta[stage_key] = {
			"k_cu_mean": k_cu_mean, "k_cu_std": k_cu_std,
			"k_rb_mean": k_rb_mean, "k_rb_std": k_rb_std,
			"transfer":  transfer,
			"n_elem":    n_elem,    "target_pe": target_pe,
		}

	# ------------------------------------------------------------------ Figure 1
	fig1, axes1 = plt.subplots(1, 2, figsize=(14, 6))

	for col_idx, (stage_key, cfg) in enumerate(stage_cfg.items()):
		meta       = stage_meta.get(stage_key, {})
		stage_name = cfg["stage_name"]
		exp_col    = cfg["exp_col"]
		theory_col = cfg["theory_col"]
		target     = cfg["target"]
		ax_ratio   = axes1[col_idx]
		is_ipa_side = stage_key == "stage2_ipa"
		rs_new_plot = rs_new[rs_new["sample_index"] >= NEW_FAMILY4_INDEX_MIN].copy() if is_ipa_side else rs_new

		if not meta or exp_col not in work.columns or theory_col not in work.columns:
			ax_ratio.axis("off")
			continue

		k_cu_mean = meta["k_cu_mean"]
		k_rb_mean = meta["k_rb_mean"]
		transfer  = meta["transfer"]
		target_pe = meta["target_pe"]
		# -- pred_copper / target_pe vs K_total = K_corr * K_extra --
		if pd.isna(transfer) or target_pe is None or pd.isna(target_pe):
			ax_ratio.axis("off")
			continue

		k_corr_col = cfg["k_corr_col"]
		kt_vals, ratio_vals, si_vals = [], [], []
		for _, row in rs_new_plot.iterrows():
			ex_v     = float(pd.to_numeric(row.get(exp_col,     np.nan), errors="coerce"))
			ke_v     = float(pd.to_numeric(row.get(KEXTRA_COL,  np.nan), errors="coerce")) if KEXTRA_COL in row.index else np.nan
			kcorr_v  = float(pd.to_numeric(row.get(k_corr_col,  np.nan), errors="coerce")) if k_corr_col in row.index else np.nan
			if pd.isna(ex_v) or pd.isna(ke_v) or pd.isna(kcorr_v) or abs(kcorr_v) < 1e-12:
				continue
			k_total = kcorr_v * ke_v
			pred_cu = ex_v * transfer
			ratio   = pred_cu / target_pe
			kt_vals.append(k_total); ratio_vals.append(ratio); si_vals.append(int(row["sample_index"]))

		if kt_vals:
			# Plot RS new by family on the convergence ratio panel as well.
			for fam_label, fam_group, fam_color, fam_marker in split_sample_groups_by_recency(rs_new_plot):
				fam_kt, fam_ratio, fam_si = [], [], []
				for _, row in fam_group.iterrows():
					ex_v    = float(pd.to_numeric(row.get(exp_col,    np.nan), errors="coerce"))
					ke_v    = float(pd.to_numeric(row.get(KEXTRA_COL, np.nan), errors="coerce")) if KEXTRA_COL in row.index else np.nan
					kcorr_v = float(pd.to_numeric(row.get(k_corr_col, np.nan), errors="coerce")) if k_corr_col in row.index else np.nan
					if pd.isna(ex_v) or pd.isna(ke_v) or pd.isna(kcorr_v) or abs(kcorr_v) < 1e-12:
						continue
					fam_kt.append(kcorr_v * ke_v)
					fam_ratio.append((ex_v * transfer) / target_pe)
					fam_si.append(int(row["sample_index"]))

				if not fam_kt:
					continue
				ax_ratio.scatter(fam_kt, fam_ratio, color=fam_color, s=110, marker=fam_marker, zorder=5,
							 label=fam_label)
				for _, row in fam_group.iterrows():
					ex_v    = float(pd.to_numeric(row.get(exp_col,    np.nan), errors="coerce"))
					ke_v    = float(pd.to_numeric(row.get(KEXTRA_COL, np.nan), errors="coerce")) if KEXTRA_COL in row.index else np.nan
					kcorr_v = float(pd.to_numeric(row.get(k_corr_col, np.nan), errors="coerce")) if k_corr_col in row.index else np.nan
					if pd.isna(ex_v) or pd.isna(ke_v) or pd.isna(kcorr_v) or abs(kcorr_v) < 1e-12:
						continue
					k_total = kcorr_v * ke_v
					ratio   = (ex_v * transfer) / target_pe
					ax_ratio.annotate(format_sample_id_from_row(row), (k_total, ratio), xytext=(5, 3),
							  textcoords="offset points", fontsize=8, color=fam_color)
			if len(kt_vals) >= 2:
				coeffs = np.polyfit(kt_vals, ratio_vals, 1)
				k_fit  = np.linspace(min(kt_vals) - 0.1, max(kt_vals) + 0.1, 200)
				ax_ratio.plot(k_fit, np.polyval(coeffs, k_fit), "--",
							  color=cfg["color_sweep"], lw=1.8, label="Linear fit (all RS new)")
				slope, intercept = float(coeffs[0]), float(coeffs[1])
				if abs(slope) > 1e-12:
					k_opt = (1.0 - intercept) / slope
					if min(kt_vals) * 0.5 <= k_opt <= max(kt_vals) * 2.0:
						ax_ratio.axvline(k_opt, color=cfg["color_sweep"], lw=1.6,
										 linestyle="-.", label=f"Interpolated K_total* = {k_opt:.3f}")

		ax_ratio.axhline(1.0, color="red", lw=2.0, linestyle="-", label="Target  (ratio = 1.0)")

		ax_ratio.set_xlabel("K_total  =  K_corr × K_extra  (Bazarov input)")
		ax_ratio.set_ylabel("pred_copper_per_elem / target_per_elem")
		ax_ratio.set_title(f"{stage_name}: convergence ratio vs K_total")
		ax_ratio.grid(True, alpha=0.3)
		ax_ratio.legend(fontsize=7.5)

	fig1.suptitle("Optimisation overview: Bazarov parity and K_total sweep convergence", fontsize=13)
	fig1.tight_layout()
	fig1.savefig(out_dir / "optimization_parity_sweep.png", dpi=150)
	plt.close(fig1)

	# ------------------------------------------------------------------ Figure 2
	fig2, (ax_tr, ax_funnel) = plt.subplots(1, 2, figsize=(14, 6))

	# -- Left: transfer ratio per identical-geometry pair --
	for stage_key, cfg in stage_cfg.items():
		meta       = stage_meta.get(stage_key, {})
		exp_col    = cfg["exp_col"]
		theory_col = cfg["theory_col"]
		if not meta or exp_col not in work.columns or theory_col not in work.columns:
			continue

		merged = copper.merge(rs_base, on="sample_index", suffixes=("_s", "_rs"))
		pairs = []
		for _, row in merged.iterrows():
			th_s  = float(pd.to_numeric(row.get(f"{theory_col}_s",  np.nan), errors="coerce"))
			ex_s  = float(pd.to_numeric(row.get(f"{exp_col}_s",     np.nan), errors="coerce"))
			th_rs = float(pd.to_numeric(row.get(f"{theory_col}_rs", np.nan), errors="coerce"))
			ex_rs = float(pd.to_numeric(row.get(f"{exp_col}_rs",    np.nan), errors="coerce"))
			if any(pd.isna(v) or abs(v) < 1e-12 for v in [th_s, th_rs]):
				continue
			if pd.isna(ex_s) or pd.isna(ex_rs):
				continue
			k_s  = ex_s  / th_s
			k_rs = ex_rs / th_rs
			if abs(k_rs) < 1e-12:
				continue
			pairs.append({"idx": int(row["sample_index"]), "sample_id": format_sample_id_from_row(row), "transfer": k_s / k_rs})

		if not pairs:
			continue
		tr_df  = pd.DataFrame(pairs).sort_values("idx")
		tr_mean = float(tr_df["transfer"].mean())
		tr_std  = float(tr_df["transfer"].std()) if len(tr_df) > 1 else 0.0
		color_tr = cfg["color_sweep"]
		ax_tr.scatter(tr_df["idx"], tr_df["transfer"], color=color_tr, s=85,
					  label=f"{cfg['stage_name']}  mean={tr_mean:.3f} ± {tr_std:.3f}")
		ax_tr.axhline(tr_mean, color=color_tr, lw=1.5, linestyle="--")
		x_band = [float(tr_df["idx"].min()) - 0.4, float(tr_df["idx"].max()) + 0.4]
		ax_tr.fill_between(x_band, tr_mean - tr_std, tr_mean + tr_std,
							color=color_tr, alpha=0.12)

	ax_tr.axhline(1.0, color="black", lw=1.0, linestyle=":", alpha=0.5, label="1.0  (no Cu/RS difference)")
	ax_tr.set_xlabel("Sample index  (identical-geometry pairs S0–7 vs RS0–7)")
	ax_tr.set_ylabel("Transfer ratio  K_copper / K_resin_baseline")
	ax_tr.set_title("Transfer ratio consistency across geometry sweep")
	ax_tr.grid(True, alpha=0.3)
	ax_tr.legend(fontsize=8)

	# -- Right: convergence funnel — pred/target vs K_total = K_corr * K_extra, both stages --
	for stage_key, cfg in stage_cfg.items():
		meta        = stage_meta.get(stage_key, {})
		exp_col     = cfg["exp_col"]
		k_corr_col  = cfg["k_corr_col"]
		target_pe   = meta.get("target_pe")
		transfer    = meta.get("transfer", np.nan)
		color       = cfg["color_sweep"]
		if not meta or pd.isna(transfer) or target_pe is None or pd.isna(target_pe):
			continue

		kt_vals, ratio_vals, sample_ids = [], [], []
		for _, row in rs_new.iterrows():
			ex_v    = float(pd.to_numeric(row.get(exp_col,    np.nan), errors="coerce"))
			ke_v    = float(pd.to_numeric(row.get(KEXTRA_COL, np.nan), errors="coerce")) if KEXTRA_COL in row.index else np.nan
			kcorr_v = float(pd.to_numeric(row.get(k_corr_col, np.nan), errors="coerce")) if k_corr_col in row.index else np.nan
			if pd.isna(ex_v) or pd.isna(ke_v) or pd.isna(kcorr_v) or abs(kcorr_v) < 1e-12:
				continue
			kt_vals.append(kcorr_v * ke_v)
			ratio_vals.append((ex_v * transfer) / target_pe)
			sample_ids.append(format_sample_id_from_row(row))

		if not kt_vals:
			continue
		ax_funnel.scatter(kt_vals, ratio_vals, color=color, s=100, marker="D",
						  label=cfg["stage_name"], zorder=5)
		for kt, ra, sample_id in zip(kt_vals, ratio_vals, sample_ids):
			ax_funnel.annotate(sample_id, (kt, ra), xytext=(5, 3),
							   textcoords="offset points", fontsize=7.5, color=color)
		if len(kt_vals) >= 2:
			coeffs = np.asarray(np.polyfit(list(map(float, kt_vals)), list(map(float, ratio_vals)), 1), dtype=float)
			k_fit  = np.linspace(min(kt_vals) - 0.1, max(kt_vals) + 0.1, 200)
			ax_funnel.plot(k_fit, np.asarray(np.polyval(coeffs, k_fit.tolist()), dtype=float), "--", color=color, lw=1.8)
			# Mark where fit crosses target (ratio = 1.0)
			slope, intercept = float(coeffs[0]), float(coeffs[1])
			if abs(slope) > 1e-12:
				k_target = (1.0 - intercept) / slope
				if min(kt_vals) * 0.5 <= k_target <= max(kt_vals) * 2.0:
					ax_funnel.scatter([k_target], [1.0], color=color, s=200, marker="*", zorder=7,
									  label=f"{cfg['stage_name']} K_total* = {k_target:.3f}")

	ax_funnel.axhline(1.0, color="red", lw=2.0, linestyle="-",
					  label="Target  (pred = copper target)")

	ax_funnel.set_xlabel("K_total  =  K_corr × K_extra  (Bazarov input)")
	ax_funnel.set_ylabel("pred_copper_per_elem / target_per_elem")
	ax_funnel.set_title("Convergence funnel — both stages\n(dashed = new nominal K_total after K_corr update, band = new sweep)")
	ax_funnel.grid(True, alpha=0.3)
	ax_funnel.legend(fontsize=8)

	fig2.suptitle("Optimisation summary: transfer ratio validation and convergence funnel", fontsize=13)
	fig2.tight_layout()
	fig2.savefig(out_dir / "optimization_summary.png", dpi=150)
	plt.close(fig2)

	print(f"Saved optimisation plots: {out_dir}/optimization_parity_sweep.png, optimization_summary.png")


def run_analysis(
	trimmed_dir: Path,
	report_dir: Path,
	output_dir: Path,
	mode: str,
	target_copper_flow_stage2: float | None,
	target_copper_flow_stage1: float | None,
	make_plots: bool,
) -> None:
	report_df = load_report_table(report_dir)
	summary_df, _raw_runs = load_trimmed_summaries(trimmed_dir)

	if summary_df.empty:
		raise RuntimeError(f"No valid trimmed files found in: {trimmed_dir}")

	if mode in {"S", "RS"}:
		summary_df = summary_df[summary_df["sample_type"] == mode].copy()

	collated = summary_df.merge(report_df, on="sample_index", how="left")
	collated["has_report_match"] = ~collated["report_file"].isna()

	output_dir.mkdir(parents=True, exist_ok=True)
	collated_path = output_dir / f"steady_collated_{mode.lower()}.csv"
	collated.to_csv(collated_path, index=False)

	paired = make_paired_table(collated)
	paired_path = output_dir / "steady_paired_s_vs_rs.csv"
	paired.to_csv(paired_path, index=False)

	write_lc_fm_report(collated, output_dir)
	write_new_sample_mfr_error_report(collated, output_dir)
	new_k_corr = write_final_round_recommendation(
		collated,
		output_dir,
		target_copper_flow_stage2=target_copper_flow_stage2,
		target_copper_flow_stage1=target_copper_flow_stage1,
	)
	write_kextra_design_sweep(
		collated,
		output_dir,
		target_copper_flow_stage2=target_copper_flow_stage2,
		target_copper_flow_stage1=target_copper_flow_stage1,
		base_source=DESIGN_BASE_K_SOURCE,
		k_extra_values=K_MDOT_EXTRA_SWEEP,
	)

	if make_plots:
		compute_and_summarize_scaling_factors(collated, output_dir)
		write_bazarov_calibration_outputs(collated, output_dir)
		write_design_sweep_outputs(collated, output_dir)
		write_resin_to_copper_guidance(
			collated,
			output_dir,
			target_copper_flow_stage2=target_copper_flow_stage2,
			target_copper_flow_stage1=target_copper_flow_stage1,
		)
		write_optimization_overview_plots(
			collated,
			output_dir,
			target_copper_flow_stage2=target_copper_flow_stage2,
			target_copper_flow_stage1=target_copper_flow_stage1,
			new_k_corr=new_k_corr,
		)

	n_s = int((collated["sample_type"] == "S").sum())
	n_rs = int((collated["sample_type"] == "RS").sum())
	missing_reports = int((~collated["has_report_match"]).sum())
	print("\n--- Analysis complete ---")
	print(f"  Samples: {n_s} copper (S), {n_rs} resin (RS)  |  paired: {len(paired)}  |  missing reports: {missing_reports}")
	print(f"  Outputs in: {output_dir}/")
	print(f"    {collated_path.name}")
	print(f"    {paired_path.name}")
	print(f"    new_sample_mfr_error_report.txt")
	print(f"    final_round_recommendation.txt / .csv / .png")
	print(f"    final_round_kextra_design_sweep.txt / .csv")
	if make_plots:
		print(f"    optimization_parity_sweep.png")
		print(f"    optimization_summary.png")
		print(f"    scaling_factors_summary.txt")
		print(f"    steady_bazarov_calibration.csv")
		print(f"    resin_copper_guidance.csv")
		print(f"    bazarov_parity_overview.png")
		print(f"    cd_vs_mdot_overlay.png")
		print(f"    design_sweep_overview.png")


def main() -> None:
	mode = MODE if MODE in {"S", "RS"} else "both"

	run_analysis(
		trimmed_dir=Path(TRIMMED_DIR),
		report_dir=Path(REPORT_DIR),
		output_dir=Path(OUTPUT_DIR),
		mode=mode,
		target_copper_flow_stage2=TARGET_COPPER_FLOW_STAGE2,
		target_copper_flow_stage1=TARGET_COPPER_FLOW_STAGE1,
		make_plots=MAKE_PLOTS,
	)


if __name__ == "__main__":
	main()