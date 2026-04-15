from labjack import ljm
import system_config
import time

import random

class DAQ:

    def __init__(self):
        try:
            self.handle = ljm.openS("T7", "USB", "ANY")
            self.connected = True
            print("LabJack connected")
        except Exception as e:
            print("LabJack not found, running in sim mode")
            self.connected = False
            self.handle = None

        if self.connected:
            # Load cells
            # Configure differential load cell channel
            ljm.eWriteName(self.handle, "AIN2_NEGATIVE_CH", 3)
            ljm.eWriteName(self.handle, "AIN4_NEGATIVE_CH", 5)
            # Small range for better resolution
            ljm.eWriteName(self.handle, "AIN2_RANGE", 0.01) # +/- 0.1V
            ljm.eWriteName(self.handle, "AIN4_RANGE", 0.01)

            # Higher resolution
            ljm.eWriteName(self.handle, "AIN_ALL_RESOLUTION_INDEX", 12)
            # Stream safe settling time
            ljm.eWriteName(self.handle, "AIN_ALL_SETTLING_US", 100)

            # Flow meters
            ljm.eWriteName(self.handle, "DIO_EF_CLOCK0_ENABLE", 1)

            ljm.eWriteName(self.handle, "DIO0_EF_ENABLE", 0)
            ljm.eWriteName(self.handle, "DIO1_EF_ENABLE", 0)
            ljm.eWriteName(self.handle, "DIO0_EF_INDEX", 3)
            ljm.eWriteName(self.handle, "DIO1_EF_INDEX", 3)
            ljm.eWriteName(self.handle, "DIO0_EF_ENABLE", 1)
            ljm.eWriteName(self.handle, "DIO1_EF_ENABLE", 1)


        self.names = [
            system_config.PT1,
            system_config.PT2,
            system_config.LC1,
            system_config.LC2,
            system_config.FLOW1,
            system_config.FLOW2
        ]

        self.last_flow1 = []
        self.last_flow2 = []

    # Labjack read tasks
    def read_sensors(self):

        if not self.connected:
            pt1 = 1.2 + random.uniform(-0.02, 0.02)
            pt2 = 1.1 + random.uniform(-0.02, 0.02)
            lc1 = random.uniform(0, 5)
            lc2 = random.uniform(0, 5)

            flow1 = random.uniform(0, 10)
            flow2 = random.uniform(0, 10)

            flow1 = random.uniform(0, 10)
            flow2 = random.uniform(0, 10)

            lc_total = lc1 + lc2

            return pt1, pt2, lc1, lc2, lc_total, flow1, flow2

        values = ljm.eReadNames(self.handle, len(self.names), self.names)
        pt1_v, pt2_v, lc1_v, lc2_v, flow1_v, flow2_v = values

        # Convert readings. Gauge -> absolute readings
        P_atm = 1.013 # atmospheric pressure in bar
        pt1 = ((pt1_v - 0.5)*150/4)*0.06895 + P_atm # psi to bar
        pt2 = ((pt2_v - 0.5)*150/4)*0.06895 + P_atm

        # CALIBRATE
        lc1 = lc1_v*10000
        lc2 = lc2_v*10000

        lc_total = lc1_v + lc2_v
        lc_total = 1098.6309573550288*lc_total - 11.092479241981106 + 0.34
        lc1 = lc2 = lc_total

        flow1 = flow1_v
        flow2 = flow2_v

        if flow1 == 0.0:
            flow1 = self.last_flow1[-1] if self.last_flow1 else 0.0
        self.last_flow1.append(flow1)
        if len(self.last_flow1) > 15:
            self.last_flow1.pop(0)

        if flow2 == 0.0:
            flow2 = self.last_flow2[-1] if self.last_flow2 else 0.0
        self.last_flow2.append(flow2)
        if len(self.last_flow2) > 15:
            self.last_flow2.pop(0)

        # Check if flow has been the exact same for a while, if so
        # assume it's actually zero.
        if all(f == flow1 for f in self.last_flow1):
            flow1 = 0.0
        else:
            flow1 = 1/max(flow1, 1e-6) # convert to Hz, avoid div by zero
            flow1 /= 11 # Hz -> L/min
            flow1 *= 1e3/60 # L/min -> g/s
            flow1 = min(flow1, 100e3)
        if all(f == flow2 for f in self.last_flow2):
            flow2 = 0.0
        else:
            flow2 = 1/max(flow2, 1e-6) # convert to Hz, avoid div by zero
            flow2 /= 11 # Hz -> L/min
            flow2 *= 1e3/60 # L/min -> g/s
            flow2 = min(flow2, 100e3)

        # Conv to grams to allow auto prefixing.
        lc1 *= 1e3
        lc2 *= 1e3
        lc_total *= 1e3


        return pt1, pt2, lc1, lc2, lc_total, flow1, flow2

    # Labjack write tasks
    def set_valve(self, channel, state):

        try:
            ljm.eWriteName(self.handle, channel, int(state))
        except Exception as e:
            print(f"Valve write failed: {e}")

    def close(self):
        try:
            ljm.close(self.handle)
        except:
            pass