import sys
from pathlib import Path
from PIL import Image

def shortpath(path):
    path = Path(path).resolve()
    cd = Path.cwd().resolve()
    try:
        path = path.relative_to(cd)
    except ValueError:
        pass
    return str(path)

always = None

def convert(input_path, output_path):
    global always
    print(f"converting '{shortpath(input_path)}' -> '{shortpath(output_path)}'")
    if output_path.is_dir():
        print("  error: output exists as directory.")
        return False
    if output_path.exists():
        if always is True:
            print("  note: output already exists, overwriting...")
        elif always is False:
            print("  note: output already exists, ignoring...")
            return False
        else:
            print("  error: output already exists, overwrite? (y/n)[!]: ",
                    end="")
            while True:
                s = input().strip().casefold()
                if s in {"y", "n", "y!", "n!"}:
                    break
                #     "  error: output already exists, overwrite? (y/n)[!]: "
                print("                                try again, (y/n)[!]: ",
                        end="")
            if s[-1] == "!":
                always = (s[0] == "y")
            if s[0] == "n":
                return False
            print("overwriting...")
        output_path.unlink()

    img = Image.open(input_path)
    img.save(output_path)
    return True

def main(argv):
    if len(argv) == 0:
        print("defaulting to all 'exports/**/*.tga'.")
        this = Path(__file__).resolve()
        here = this.parent
        input_paths = list((here / "exports").rglob("*.tga"))
    else:
        input_paths = [Path(argv[0])]
    if len(argv) > 2:
        print("  error: unrecognised arguments, only accepts one input path and "
                "one (optional) output path, or no arguments")
        return

    if len(argv) == 2:
        output_paths = [Path(argv[1])]
    else:
        output_paths = [path.with_suffix(".png") for path in input_paths]

    for input_path, output_path in zip(input_paths, output_paths):
        _ = convert(input_path, output_path)

if __name__ == "__main__":
    main(sys.argv[1:])
