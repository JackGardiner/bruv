from PyQt6.QtWidgets import QApplication
from main_gui import DAQWindow
import sys

if __name__ == "__main__":
    app = QApplication(sys.argv)
    window = DAQWindow()
    window.show()
    sys.exit(app.exec())