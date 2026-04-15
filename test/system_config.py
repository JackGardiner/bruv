PT1 = "AIN0"
PT2 = "AIN6"

LC1 = "AIN2" # -ve, AIN3 +ve
LC2 = "AIN4" # -ve, AIN5 +ve

FLOW1 = "DIO0_EF_READ_A_F"
FLOW2 = "DIO1_EF_READ_A_F"


SAMPLE_RATE = 100
DATA_FOLDER = "data/test_runs"

# CSV Headers
headers = [
    "PT1_bar",
    "PT2_bar",
    "LC1_g",
    "LC2_g",
    "LC_total_g",
    "Flow1_gs",
    "Flow2_gs"
]
