import csv
import os
from datetime import datetime

class CSVLogger:

    def __init__(self, headers, folder="logs", prefix="daq_log"):

        os.makedirs(folder, exist_ok=True)

        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")

        self.path = os.path.join(folder, f"{prefix}_{timestamp}.csv")

        self.file = open(self.path, "w", newline="")
        self.writer = csv.writer(self.file)

        # Add timestamp column automatically
        self.headers = ["timestamp"] + headers
        self.writer.writerow(self.headers)

    def write_row(self, row):
        timestamp = datetime.now().strftime("%H:%M:%S.%f")

        self.writer.writerow([timestamp] + row)

        # Flush so data is not lost if program crashes
        self.file.flush()

    def close(self):

        self.file.flush()
        self.file.close()

        print(f"CSV log saved to {self.path}") 