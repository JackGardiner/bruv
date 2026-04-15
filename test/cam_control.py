# Source - https://stackoverflow.com/a/47783101
# Posted by asylumax
# Retrieved 2026-03-25, License - CC BY-SA 3.0

import argparse
import datetime
import os
import re
import shutil
import subprocess
import sys
import time


CAMERA_CMD = r"C:\Program Files (x86)\digiCamControl\CameraControlCmd.exe"
IMAGE_EXTENSIONS = {'.jpg', '.jpeg', '.nef', '.raw', '.png', '.tif', '.tiff'}


def _run_camera_command(command):
    process = subprocess.run(
        command,
        capture_output=True,
        text=True,
        shell=False,
        check=False,
    )
    print('Command output: ' + str(process.stdout))
    print('Command err: ' + str(process.stderr))
    if process.returncode != 0:
        raise RuntimeError(f'Camera command failed with exit code {process.returncode}.')
    return process


def run_camera_capture(filename, destination_dir, iso, shutter, aperture, autofocus):
    capture_command = '/capture' if autofocus else '/capturenoaf'
    command = [
        CAMERA_CMD,
        '/folder',
        destination_dir,
        '/filename',
        filename,
        capture_command,
        '/iso',
        str(iso),
        '/shutter',
        str(shutter),
        '/aperture',
        str(aperture),
    ]
    print('camera details = ' + ' '.join(command[1:]))
    _run_camera_command(command)


def capture_burst_on_camera_storage(
    count,
    interval,
    iso=None,
    shutter=None,
    aperture=None,
    autofocus=False,
    compression=None,
):
    if count < 1:
        raise ValueError('count must be >= 1')
    if interval < 0:
        raise ValueError('interval must be >= 0')

    capture_command = '/capture' if autofocus else '/capturenoaf'
    for index in range(1, count + 1):
        command = [CAMERA_CMD, capture_command]
        if iso is not None:
            command.extend(['/iso', str(iso)])
        if shutter is not None:
            command.extend(['/shutter', str(shutter)])
        if aperture is not None:
            command.extend(['/aperture', str(aperture)])
        if compression:
            command.extend(['/compression', str(compression)])
        print(f'[{index}/{count}] camera details = ' + ' '.join(command[1:]))
        _run_camera_command(command)
        if index < count and interval > 0:
            time.sleep(interval)


def transfer_images_with_prefix(source_dir, destination_dir, prefix):
    if not os.path.isdir(source_dir):
        raise FileNotFoundError(f'Source directory does not exist: {source_dir}')
    os.makedirs(destination_dir, exist_ok=True)

    normalized_prefix = prefix.strip()
    if not normalized_prefix:
        raise ValueError('prefix cannot be empty')

    existing_indices = []
    prefix_re = re.compile(rf'^{re.escape(normalized_prefix)}_(\d+)')
    for name in os.listdir(destination_dir):
        match = prefix_re.match(name)
        if match:
            existing_indices.append(int(match.group(1)))
    next_index = (max(existing_indices) + 1) if existing_indices else 1

    candidates = []
    for root, _, files in os.walk(source_dir):
        for name in files:
            ext = os.path.splitext(name)[1].lower()
            if ext in IMAGE_EXTENSIONS:
                full_path = os.path.join(root, name)
                candidates.append(full_path)

    candidates.sort(key=lambda p: os.path.getmtime(p))

    transferred = []
    for src in candidates:
        ext = os.path.splitext(src)[1].lower()
        dst_name = f'{normalized_prefix}_{next_index:04d}{ext}'
        dst = os.path.join(destination_dir, dst_name)
        shutil.copy2(src, dst)
        transferred.append(dst)
        next_index += 1

    return transferred


def build_filename(base_name, shot_index, timestamp):
    stem, ext = os.path.splitext(base_name)
    ext = ext if ext else '.jpg'
    return f'{timestamp}_{stem}_{shot_index:03d}{ext}'


def parse_args(argv):
    parser = argparse.ArgumentParser(
        description='Capture a burst with digiCamControl and transfer images to camera_snaps after completion.'
    )
    parser.add_argument('filename', nargs='?', default='test.jpg', help='Base output filename, e.g. snap.jpg')
    parser.add_argument('--count', type=int, default=5, help='Number of images in burst (default: 5)')
    parser.add_argument('--interval', type=float, default=0.2, help='Seconds between shots (default: 0.2)')
    parser.add_argument('--iso', default='500', help='ISO value (default: 500)')
    parser.add_argument('--shutter', default='1/30', help='Shutter speed (default: 1/30)')
    parser.add_argument('--aperture', default='1.8', help='Aperture value (default: 1.8)')
    parser.add_argument(
        '--af',
        action='store_true',
        help='Use autofocus before each shot (slower). Default is no autofocus for faster bursts.',
    )
    parser.add_argument(
        '--dest',
        default=os.path.join(os.getcwd(), 'camera_snaps'),
        help='Destination directory for transferred images (default: ./camera_snaps)',
    )
    args = parser.parse_args(argv)

    if args.count < 1:
        parser.error('--count must be >= 1')
    if args.interval < 0:
        parser.error('--interval must be >= 0')
    return args


def main(argv):
    if not os.path.exists(CAMERA_CMD):
        raise FileNotFoundError(f'CameraControlCmd not found at: {CAMERA_CMD}')

    args = parse_args(argv)
    os.makedirs(args.dest, exist_ok=True)
    timestamp = datetime.datetime.now().strftime('%Y%m%d_%H%M%S')
    print('Current date time = ' + timestamp)
    print(f'Capture mode: {"autofocus" if args.af else "no autofocus"}')
    print('Output directory:', args.dest)

    captured_files = []
    for index in range(1, args.count + 1):
        image_name = build_filename(args.filename, index, timestamp)
        print(f'[{index}/{args.count}] Capturing: {image_name}')
        run_camera_capture(
            image_name,
            args.dest,
            args.iso,
            args.shutter,
            args.aperture,
            args.af,
        )
        captured_files.append(image_name)
        if index < args.count and args.interval > 0:
            time.sleep(args.interval)

    print('Burst capture complete. Files transferred by camera command to:', args.dest)
    existing = []
    for name in captured_files:
        path = os.path.join(args.dest, name)
        if os.path.exists(path):
            existing.append(path)
    print(f'Capture complete. {len(existing)}/{len(captured_files)} files found.')
    for path in existing:
        print(path)


if __name__ == '__main__':
    try:
        main(sys.argv[1:])
    except Exception as exc:
        print(f'Error: {exc}')
        sys.exit(1)
