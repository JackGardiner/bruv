from PyQt6.QtWidgets import (
    QMainWindow, QHBoxLayout, QWidget,
    QVBoxLayout, QLabel, QPushButton, QGroupBox,
    QApplication, QStackedWidget, QTextEdit, QLineEdit,
    QCheckBox, QFileDialog
)
from PyQt6.QtCore import QTimer, Qt, pyqtSignal
from PyQt6.QtGui import QPixmap, QKeySequence, QShortcut
import pyqtgraph as pg

import numpy as np
import os
import threading
import datetime

from labjack_read_write import DAQ
from csv_logger import CSVLogger
import system_config
import time
from cam_control import (
    capture_burst_on_camera_storage,
    transfer_images_with_prefix,
    CAMERA_CMD,
)

pg.setConfigOption("background", "#434343")
pg.setConfigOption("foreground", "#DBDBDB")

class DAQWindow(QMainWindow):
    capture_success = pyqtSignal(str)
    transfer_success = pyqtSignal(str)
    capture_error = pyqtSignal(str)


    def __init__(self):
        super().__init__()

        self.setWindowTitle("Flow Test GUI")

        self.logger = CSVLogger(system_config.headers)

        self.logging_enabled = False # Log data initially off
        self.capture_in_progress = False
        self.transfer_in_progress = False
        self.capture_index = 0
        self.camera_dest = os.path.join(os.getcwd(), 'camera_snaps')

        self.timer = QTimer()
        self.timer.timeout.connect(self.update_gui)
        self.timer.start(50) # 20 Hz

        # Central widget
        central = QWidget()
        self.setCentralWidget(central)

        main_layout = QHBoxLayout()
        central.setLayout(main_layout)

        # ----------- LEFT PANEL (Labels, LJ status) -------------
        left_layout = QVBoxLayout()



        # Add image :)
        logo_label = QLabel()
        pixmap = QPixmap("images/pingu.png")
        pixmap = pixmap.scaledToWidth(250, Qt.TransformationMode.SmoothTransformation)
        logo_label.setPixmap(pixmap)
        logo_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        left_layout.addWidget(logo_label)

        # Start/stop logging buttons
        log_box = QGroupBox("Data Logging")
        log_layout = QVBoxLayout()
        log_box.setLayout(log_layout)

        self.start_log_button = QPushButton("Start Logging")
        self.stop_log_button = QPushButton("Stop Logging")

        self.stop_log_button.setEnabled(False)

        log_layout.addWidget(self.start_log_button)
        log_layout.addWidget(self.stop_log_button)

        self.start_log_button.clicked.connect(self.start_logging)
        self.stop_log_button.clicked.connect(self.stop_logging)

        left_layout.addWidget(log_box)

        # Camera controls
        camera_box = QGroupBox("Camera")
        camera_layout = QVBoxLayout()
        camera_box.setLayout(camera_layout)

        self.capture_button = QPushButton("Capture 1 (Ctrl+Shift+C)")
        self.capture_burst_button = QPushButton("Capture Burst")
        self.transfer_button = QPushButton("Transfer From SD")

        self.burst_count_input = QLineEdit("10")
        self.transfer_prefix_input = QLineEdit("sample")
        self.camera_source_dir_input = QLineEdit("E:/DCIM")
        self.browse_source_button = QPushButton("Browse SD Folder")

        self.camera_status_label = QLabel("Camera: READY")
        self.camera_status_label.setStyleSheet("color: #00E676; font-weight: bold")

        camera_layout.addWidget(self.capture_button)
        camera_layout.addWidget(QLabel("Burst Count"))
        camera_layout.addWidget(self.burst_count_input)
        camera_layout.addWidget(self.capture_burst_button)
        camera_layout.addWidget(QLabel("SD Source Folder"))
        camera_layout.addWidget(self.camera_source_dir_input)
        camera_layout.addWidget(self.browse_source_button)
        camera_layout.addWidget(QLabel("Transfer Prefix (example: sample)"))
        camera_layout.addWidget(self.transfer_prefix_input)
        camera_layout.addWidget(self.transfer_button)
        camera_layout.addWidget(self.camera_status_label)

        self.capture_button.clicked.connect(self.trigger_capture)
        self.capture_burst_button.clicked.connect(self.trigger_burst_capture)
        self.transfer_button.clicked.connect(self.trigger_transfer)
        self.browse_source_button.clicked.connect(self.pick_source_directory)

        self.capture_shortcut = QShortcut(QKeySequence("Ctrl+Shift+C"), self)
        self.capture_shortcut.setContext(Qt.ShortcutContext.ApplicationShortcut)
        self.capture_shortcut.activated.connect(self.trigger_capture)

        left_layout.addWidget(camera_box)

        # Status box
        status_box = QGroupBox("Sensor Values")
        status_layout = QVBoxLayout()
        status_box.setLayout(status_layout)

        self.pt1_label = QLabel("PT1: -- bar")
        self.pt2_label = QLabel("PT2: -- bar")
        self.lc1_label = QLabel("LC1: -- g")
        self.lc2_label = QLabel("LC2: -- g")
        self.lc_total_label = QLabel("LC Total: -- g")
        self.flow1_label = QLabel("Flow meter 1: -- g/s")
        self.flow2_label = QLabel("Flow meter 2: -- g/s")

        self.lj_status_label = QLabel("LabJack: UNKNOWN")
        self.lj_status_label.setStyleSheet("color: orange; font-weight: bold")

        status_layout.addWidget(self.pt1_label)
        status_layout.addWidget(self.pt2_label)
        status_layout.addWidget(self.lc1_label)
        status_layout.addWidget(self.lc2_label)
        status_layout.addWidget(self.flow1_label)
        status_layout.addWidget(self.flow2_label)
        status_layout.addWidget(self.lc_total_label)

        status_layout.addWidget(self.lj_status_label)

        left_layout.addWidget(status_box)


        # setpoint box
        setpoint_box = QGroupBox("Setpoints")
        setpoint_layout = QVBoxLayout()
        setpoint_box.setLayout(setpoint_layout)
        self.pt1_setpoint = QLineEdit()
        self.pt2_setpoint = QLineEdit()
        setpoint_layout.addWidget(QLabel("PT1 Setpoint (bar)"))
        setpoint_layout.addWidget(self.pt1_setpoint)
        setpoint_layout.addWidget(QLabel("PT2 Setpoint (bar)"))
        setpoint_layout.addWidget(self.pt2_setpoint)

        self.flow1_target = QLineEdit()
        self.flow2_target = QLineEdit()
        self.flow1_target.setPlaceholderText("e.g. 47.98")
        self.flow2_target.setPlaceholderText("e.g. 84.01")
        setpoint_layout.addWidget(QLabel("Flow1 Target — resin (g/s)"))
        setpoint_layout.addWidget(self.flow1_target)
        setpoint_layout.addWidget(QLabel("Flow2 Target — resin (g/s)"))
        setpoint_layout.addWidget(self.flow2_target)
        left_layout.addWidget(setpoint_box)

        main_layout.addLayout(left_layout, 1)
        left_layout.addStretch()



        # --------- RIGHT PANEL (Graphs) -------------------
        right_layout = QVBoxLayout()
        main_layout.addLayout(right_layout, 3)

        # Pressure plot
        self.pressure_plot = pg.PlotWidget(title="Pressure")
        self.pressure_plot.setLabel("left", "Pressure", units="bar")
        self.pressure_plot.setLabel("bottom", "Time", units="s")
        self.pressure_plot.setXRange(-60, 0)
        self.pressure_curve1 = self.pressure_plot.plot(pen=pg.mkPen("r", width=2), name="PT1")
        self.pressure_curve2 = self.pressure_plot.plot(pen=pg.mkPen("g", width=2), name="PT2")
        self.pressure_setpoint1 = self.pressure_plot.plot(pen=pg.mkPen("r", style=Qt.PenStyle.DashLine), name="PT1 Setpoint")
        self.pressure_setpoint2 = self.pressure_plot.plot(pen=pg.mkPen("g", style=Qt.PenStyle.DashLine), name="PT2 Setpoint")

        pressure_section = QHBoxLayout()
        pressure_section.addWidget(self.pressure_plot, 4)
        legend_layout = QVBoxLayout()
        pressure_section.addLayout(legend_layout, 1)

        self.pt1_checkbox = QCheckBox("PT1")
        self.pt1_checkbox.setChecked(True)

        self.pt2_checkbox = QCheckBox("PT2")
        self.pt2_checkbox.setChecked(True)

        legend_layout.addWidget(self.pt1_checkbox)
        legend_layout.addWidget(self.pt2_checkbox)

        legend_layout.addStretch()

        right_layout.addLayout(pressure_section)

        self.pt1_checkbox.stateChanged.connect(
            lambda state: self.pressure_curve1.setVisible(state == 2)
        )

        self.pt2_checkbox.stateChanged.connect(
            lambda state: self.pressure_curve2.setVisible(state == 2)
        )

        self.pressure_plot.enableAutoRange(axis='y')

        # Load cell plot
        self.load_plot = pg.PlotWidget(title="Load Cells")
        self.load_plot.setLabel("left", "Mass", units='g')
        self.load_plot.setLabel("bottom", 'Time', units='s')
        self.load_plot.setXRange(-60, 0)
        self.load_curve1 = self.load_plot.plot(pen=pg.mkPen("c", width=2), name="LC1")
        self.load_curve2 = self.load_plot.plot(pen=pg.mkPen("y", width=2), name="LC2")
        self.load_curve_total = self.load_plot.plot(pen=pg.mkPen("b", width=2), name="LC_Total")

        load_cell_section = QHBoxLayout()
        load_cell_section.addWidget(self.load_plot, 4)
        legend_layout_LC = QVBoxLayout()
        load_cell_section.addLayout(legend_layout_LC,1)

        self.lc1_checkbox = QCheckBox("LC1")
        self.lc1_checkbox.setChecked(True)

        self.lc2_checkbox = QCheckBox("LC2")
        self.lc2_checkbox.setChecked(True)

        self.lc_total_checkbox = QCheckBox("LC_Total")
        self.lc_total_checkbox.setChecked(True)

        legend_layout_LC.addWidget(self.lc1_checkbox)
        legend_layout_LC.addWidget(self.lc2_checkbox)
        legend_layout_LC.addWidget(self.lc_total_checkbox)

        legend_layout_LC.addStretch()

        right_layout.addLayout(load_cell_section)

        self.lc1_checkbox.stateChanged.connect(
            lambda state: self.load_curve1.setVisible(state == 2)
        )

        self.lc2_checkbox.stateChanged.connect(
            lambda state: self.load_curve2.setVisible(state == 2)
        )

        self.lc_total_checkbox.stateChanged.connect(
            lambda state: self.load_curve_total.setVisible(state == 2)
        )

        self.load_plot.enableAutoRange(axis='y')

        # Flow meter plot
        self.flow_plot = pg.PlotWidget(title="Flow Meter")
        self.flow_plot.setLabel("left", "Flowrate", units="g/s")
        self.flow_plot.setLabel("bottom", "Time", units="s")
        self.flow_plot.setXRange(-60, 0)
        self.flow_curve1 = self.flow_plot.plot(pen=pg.mkPen("r", width=2), name="Flow1")
        self.flow_curve2 = self.flow_plot.plot(pen=pg.mkPen("g", width=2), name="Flow2")
        self.flow_target1_line = self.flow_plot.plot(
            pen=pg.mkPen("r", width=1.5, style=Qt.PenStyle.DashLine), name="Flow1 Target"
        )
        self.flow_target2_line = self.flow_plot.plot(
            pen=pg.mkPen("g", width=1.5, style=Qt.PenStyle.DashLine), name="Flow2 Target"
        )

        flow_section = QHBoxLayout()
        flow_section.addWidget(self.flow_plot,4)
        legend_layout_flow = QVBoxLayout()
        flow_section.addLayout(legend_layout_flow, 1)

        self.flow1_checkbox = QCheckBox("Flow1")
        self.flow1_checkbox.setChecked(True)

        self.flow2_checkbox = QCheckBox("Flow2")
        self.flow2_checkbox.setChecked(True)

        legend_layout_flow.addWidget(self.flow1_checkbox)
        legend_layout_flow.addWidget(self.flow2_checkbox)

        legend_layout_flow.addStretch()

        right_layout.addLayout(flow_section)

        self.flow1_checkbox.stateChanged.connect(
            lambda state: self.flow_curve1.setVisible(state == 2)
        )

        self.flow2_checkbox.stateChanged.connect(
            lambda state: self.flow_curve2.setVisible(state == 2)
        )

        self.flow_plot.enableAutoRange(axis='y')


        # Data storage
        self.start_time = time.time()

        self.time_data = np.array([])
        self.pt1_data = []
        self.pt2_data = []

        self.lc1_data = []
        self.lc2_data = []
        self.lc_total_data = []

        self.flow1_data = []
        self.flow2_data = []


        # DAQ - Read labjacks
        self.daq = DAQ()

        self.capture_success.connect(self.on_capture_success)
        self.transfer_success.connect(self.on_transfer_success)
        self.capture_error.connect(self.on_capture_error)

        if not os.path.exists(CAMERA_CMD):
            self.camera_status_label.setText("Camera: Command tool missing")
            self.camera_status_label.setStyleSheet("color: red; font-weight: bold")
            self.capture_button.setEnabled(False)
            self.capture_burst_button.setEnabled(False)

    def trigger_capture(self):
        if self.capture_in_progress or self.transfer_in_progress:
            return

        self.capture_in_progress = True
        self.capture_button.setEnabled(False)
        self.capture_burst_button.setEnabled(False)
        self.transfer_button.setEnabled(False)
        self.camera_status_label.setText("Camera: CAPTURING...")
        self.camera_status_label.setStyleSheet("color: #FFD166; font-weight: bold")

        worker = threading.Thread(target=self._capture_worker, daemon=True)
        worker.start()

    def trigger_burst_capture(self):
        if self.capture_in_progress or self.transfer_in_progress:
            return

        try:
            count = int(self.burst_count_input.text().strip())
            if count < 1:
                raise ValueError
        except ValueError:
            self.camera_status_label.setText("Camera error: burst count must be a positive integer")
            self.camera_status_label.setStyleSheet("color: red; font-weight: bold")
            return

        self.capture_in_progress = True
        self.capture_button.setEnabled(False)
        self.capture_burst_button.setEnabled(False)
        self.transfer_button.setEnabled(False)
        self.camera_status_label.setText(f"Camera: BURST CAPTURE ({count})...")
        self.camera_status_label.setStyleSheet("color: #FFD166; font-weight: bold")

        worker = threading.Thread(target=self._capture_burst_worker, args=(count,), daemon=True)
        worker.start()

    def trigger_transfer(self):
        if self.capture_in_progress or self.transfer_in_progress:
            return

        source_dir = self.camera_source_dir_input.text().strip()
        prefix = self.transfer_prefix_input.text().strip()
        if not source_dir:
            self.camera_status_label.setText("Transfer error: source folder is empty")
            self.camera_status_label.setStyleSheet("color: red; font-weight: bold")
            return

        self.transfer_in_progress = True
        self.capture_button.setEnabled(False)
        self.capture_burst_button.setEnabled(False)
        self.transfer_button.setEnabled(False)
        self.camera_status_label.setText("Transfer: COPYING FROM SD...")
        self.camera_status_label.setStyleSheet("color: #FFD166; font-weight: bold")

        worker = threading.Thread(target=self._transfer_worker, args=(source_dir, prefix), daemon=True)
        worker.start()

    def pick_source_directory(self):
        selected_dir = QFileDialog.getExistingDirectory(self, "Select SD Card Source Folder")
        if selected_dir:
            self.camera_source_dir_input.setText(selected_dir)

    def _capture_worker(self):
        try:
            self.capture_index += 1
            capture_burst_on_camera_storage(
                count=1,
                interval=0.0,
                iso=None,
                shutter=None,
                aperture=None,
                autofocus=False,
                compression=None,
            )
            self.capture_success.emit(f'Shot {self.capture_index:04d} captured to SD')
        except Exception as exc:
            self.capture_error.emit(str(exc))

    def _capture_burst_worker(self, count):
        try:
            capture_burst_on_camera_storage(
                count=count,
                interval=0.0,
                iso=None,
                shutter=None,
                aperture=None,
                autofocus=False,
                compression=None,
            )
            self.capture_success.emit(f'Burst complete: {count} shot(s) on camera storage')
        except Exception as exc:
            self.capture_error.emit(str(exc))

    def _transfer_worker(self, source_dir, prefix):
        try:
            transferred = transfer_images_with_prefix(source_dir, self.camera_dest, prefix)
            self.transfer_success.emit(f'Transferred {len(transferred)} file(s) to {self.camera_dest}')
        except Exception as exc:
            self.capture_error.emit(str(exc))

    def on_capture_success(self, output_path):
        self.capture_in_progress = False
        self.capture_button.setEnabled(True)
        self.capture_burst_button.setEnabled(True)
        self.transfer_button.setEnabled(True)
        self.camera_status_label.setText(f"Camera: {output_path}")
        self.camera_status_label.setStyleSheet("color: #00E676; font-weight: bold")

    def on_transfer_success(self, message):
        self.transfer_in_progress = False
        self.capture_button.setEnabled(True)
        self.capture_burst_button.setEnabled(True)
        self.transfer_button.setEnabled(True)
        self.camera_status_label.setText(message)
        self.camera_status_label.setStyleSheet("color: #00E676; font-weight: bold")

    def on_capture_error(self, err):
        self.capture_in_progress = False
        self.transfer_in_progress = False
        self.capture_button.setEnabled(True)
        self.capture_burst_button.setEnabled(True)
        self.transfer_button.setEnabled(True)
        self.camera_status_label.setText(f"Camera error: {err}")
        self.camera_status_label.setStyleSheet("color: red; font-weight: bold")

    def update_gui(self):

        try:
            pt1, pt2, lc1, lc2, lc_total, flow1, flow2 = self.daq.read_sensors()
            if self.daq.connected:
                self.lj_status_label.setText("Labjack CONNECTED")
                self.lj_status_label.setStyleSheet("color: #00E676; font-weight: bold")
            else:
                self.lj_status_label.setText("LabJack DISCONNECTED: Simulation Mode")
                self.lj_status_label.setStyleSheet("color: orange; font-weight: bold")

        except Exception as e:

            print("ERROR:", e)

            self.lj_status_label.setText("LabJack DISCONNECTED")
            self.lj_status_label.setStyleSheet("color: red; font-weight: bold")

            return

        now = time.time()
        t = now - self.start_time
        self.start_time = now

        # Append to current data storage
        self.time_data -= t
        self.time_data = np.append(self.time_data, 0.0)
        self.pt1_data.append(pt1)
        self.pt2_data.append(pt2)

        self.lc1_data.append(lc1)
        self.lc2_data.append(lc2)
        self.lc_total_data.append(lc_total)

        self.flow1_data.append(flow1)
        self.flow2_data.append(flow2)

        # Limit data size
        MAX_POINTS = 1000

        if len(self.time_data) > MAX_POINTS:
            self.time_data = self.time_data[-MAX_POINTS:]
            self.pt1_data = self.pt1_data[-MAX_POINTS:]
            self.pt2_data = self.pt2_data[-MAX_POINTS:]
            self.lc1_data = self.lc1_data[-MAX_POINTS:]
            self.lc2_data = self.lc2_data[-MAX_POINTS:]
            self.lc_total_data = self.lc_total_data[-MAX_POINTS:]
            self.flow1_data = self.flow1_data[-MAX_POINTS:]
            self.flow2_data = self.flow2_data[-MAX_POINTS:]


        # Write to CSV (only if logging data enabled)
        if self.logging_enabled:
            self.logger.write_row([pt1, pt2, lc1, lc2, lc_total, flow1, flow2])

        # Update plots
        self.pressure_curve1.setData(self.time_data, self.pt1_data)
        self.pressure_curve2.setData(self.time_data, self.pt2_data)

        self.load_curve1.setData(self.time_data, self.lc1_data)
        self.load_curve2.setData(self.time_data, self.lc2_data)
        self.load_curve_total.setData(self.time_data, self.lc_total_data)

        self.flow_curve1.setData(self.time_data, self.flow1_data)
        self.flow_curve2.setData(self.time_data, self.flow2_data)


        # update setpoint lines
        try:
            pt1_setpoint = float(self.pt1_setpoint.text())
            self.pressure_setpoint1.setData([-60, 0], [pt1_setpoint, pt1_setpoint])
        except ValueError:
            self.pressure_setpoint1.setData([], [])

        try:
            pt2_setpoint = float(self.pt2_setpoint.text())
            self.pressure_setpoint2.setData([-60, 0], [pt2_setpoint, pt2_setpoint])
        except ValueError:
            self.pressure_setpoint2.setData([], [])

        # update flow target lines
        try:
            flow1_tgt = float(self.flow1_target.text())
            self.flow_target1_line.setData([-60, 0], [flow1_tgt, flow1_tgt])
        except ValueError:
            self.flow_target1_line.setData([], [])

        try:
            flow2_tgt = float(self.flow2_target.text())
            self.flow_target2_line.setData([-60, 0], [flow2_tgt, flow2_tgt])
        except ValueError:
            self.flow_target2_line.setData([], [])

        # Update data labels
        self.pt1_label.setText(f"PT1: {pt1:.2f} bar")
        self.pt2_label.setText(f"PT2: {pt2:.2f} bar")

        self.lc1_label.setText(f"LC1: {lc1:.1f} g")
        self.lc2_label.setText(f"LC2: {lc2:.1f} g")
        self.lc_total_label.setText(f"LC Total: {lc_total:.1f} g")

        self.flow1_label.setText(f"Flow meter: {flow1:.2f} g/s")
        self.flow2_label.setText(f"Flow meter: {flow2:.2f} g/s")

    def start_logging(self):
        self.logging_enabled = True
        self.start_log_button.setEnabled(False)
        self.stop_log_button.setEnabled(True)
        print("Logging started")

    def stop_logging(self):
        self.logging_enabled = False
        self.start_log_button.setEnabled(True)
        self.stop_log_button.setEnabled(False)
        print("Logging stopped")