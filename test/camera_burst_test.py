# Source - https://stackoverflow.com/a/47783101
# Posted by asylumax
# Retrieved 2026-03-25, License - CC BY-SA 3.0

import sys
import os
import subprocess
import datetime

def func_TakeNikonPicture(input_filename):
    camera_command = 'C:\Program Files (x86)\digiCamControl\CameraControlCmd.exe'
    camera_command_details = '/filename ./' + input_filename + ' /capture /iso 500 /shutter 1/30 /aperture 1.8'
    print('camera details = ',camera_command_details)
    full_command=camera_command + ' ' + camera_command_details
    p = subprocess.Popen(full_command, stdout=subprocess.PIPE, universal_newlines=True, shell=False)
    (output, err) = p.communicate()

    #This makes the wait possible
    p_status = p.wait(1)
    # print(p.stdout.readline())

    #This will give you the output of the command being executed
    print('Command output: ' + str(output))
    print('Command err: ' + str(err))

    print('done')


if(len(sys.argv) < 2):
    rawimagename = 'test.jpg'
else:
    # sys.argv[0] is the program name, sys.argv[1] is the first file, etc.
    # need to shift this over
    files = sys.argv[1:len(sys.argv)]
    # Read the image
    rawimagename = files[0]
    if(os.path.isfile(rawimagename) is True):
        print("File exists...not overwriting.")
        sys.exit()

# Store date/time for file uniqueness
current_dt=datetime.datetime.now().strftime('%Y%m%d_%H%M%S')
print("Current date time = " + current_dt)
rawimagename=current_dt + '_' + rawimagename

print('Name of raw image will be: ', rawimagename)

# take picture
func_TakeNikonPicture(rawimagename)
