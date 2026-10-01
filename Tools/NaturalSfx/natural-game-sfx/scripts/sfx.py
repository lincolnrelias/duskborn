"""Free source-based game audio editing and validation."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile


def run(args):
    result = subprocess.run(args, capture_output=True, text=True)
    if result.returncode:
        raise ValueError(result.stderr[-3000:])
    return result.stdout


def probe(path):
    data = json.loads(run(["ffprobe", "-v", "error", "-show_streams", "-show_format", "-of", "json", str(path)]))
    if not any(s["codec_type"] == "audio" for s in data["streams"]):
        raise ValueError("No audio stream")
    return data


def source(path):
    path = Path(path).resolve(strict=True)
    return {"path": str(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}


def targets(path):
    path = Path(path).resolve()
    receipt = Path(str(path) + ".receipt.json")
    if path.exists() or receipt.exists():
        raise ValueError("Output or receipt already exists; choose a new destination")
    path.parent.mkdir(parents=True, exist_ok=True)
    return path, receipt


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2), encoding="utf-8")


def finite(value):
    value = float(value)
    if not math.isfinite(value):
        raise ValueError("Numeric settings must be finite")
    return value


def render(args):
    out, receipt = targets(args.output)
    if out.suffix.lower() != ".wav":
        raise ValueError("Edited masters must use .wav")
    command = ["ffmpeg", "-hide_banner", "-v", "error", "-n"]
    record = {"operation": args.command, "sources": []}
    if args.command == "export":
        record["sources"] = [source(args.input)]
        command += ["-i", str(Path(args.input).resolve())]
        duration = args.duration
        if args.start < 0 or (duration is not None and duration <= 0) or args.fade_ms < 0:
            raise ValueError("Invalid trim or fade settings")
        info = probe(args.input)
        duration = duration or (float(info["format"]["duration"]) - args.start)
        if duration <= 0:
            raise ValueError("Trim starts beyond the clip")
        fade = min(args.fade_ms / 1000, duration / 2)
        filters = [f"atrim=start={args.start}:duration={duration}", "asetpts=PTS-STARTPTS", f"volume={args.gain_db}dB"]
        if fade:
            filters += [f"afade=t=in:d={fade}", f"afade=t=out:st={duration-fade}:d={fade}"]
        command += ["-af", ",".join(filters)]
        record["settings"] = vars(args)
    else:
        recipe_path = Path(args.recipe).resolve()
        recipe = json.loads(recipe_path.read_text(encoding="utf-8"))
        layers = recipe["layers"]
        if not layers or len(layers) > 16:
            raise ValueError("Mix requires 1-16 layers")
        filters = []
        for index, layer in enumerate(layers):
            path = (recipe_path.parent / layer["path"]).resolve()
            record["sources"].append(source(path))
            command += ["-i", str(path)]
            delay = finite(layer.get("delay_ms", 0))
            gain = finite(layer.get("gain_db", 0))
            if delay < 0:
                raise ValueError("Delay must be nonnegative")
            filters.append(f"[{index}:a]aresample=48000,volume={gain}dB,adelay={round(delay)}:all=1[a{index}]")
        labels = "".join(f"[a{i}]" for i in range(len(layers)))
        filters.append(f"{labels}amix=inputs={len(layers)}:duration=longest:normalize=0[out]")
        command += ["-filter_complex", ";".join(filters), "-map", "[out]"]
        record["recipe"] = recipe
    record["channels"] = args.channels
    with tempfile.TemporaryDirectory(dir=out.parent) as temp:
        staged = Path(temp) / "master.wav"
        run(command + ["-ar", "48000", "-ac", str(args.channels), "-c:a", "pcm_s24le", str(staged)])
        probe(staged)
        with out.open("xb") as dest:
            dest.write(staged.read_bytes())
    record["inspection"] = probe(out)
    record["output"] = source(out)
    write_json(receipt, record)
    print(json.dumps({"output": str(out), "receipt": str(receipt)}))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("doctor")
    inspect = sub.add_parser("inspect")
    inspect.add_argument("input")
    export = sub.add_parser("export")
    export.add_argument("input")
    export.add_argument("output")
    export.add_argument("--start", type=finite, default=0)
    export.add_argument("--duration", type=finite)
    export.add_argument("--gain-db", type=finite, default=0)
    export.add_argument("--fade-ms", type=finite, default=3)
    mix = sub.add_parser("mix")
    mix.add_argument("recipe")
    mix.add_argument("output")
    for item in (export, mix):
        item.add_argument("--channels", type=int, choices=[1, 2], default=1)
    args = parser.parse_args()
    if args.command == "doctor":
        print(json.dumps({"python": sys.executable, "ffmpeg": shutil.which("ffmpeg"), "ffprobe": shutil.which("ffprobe")}, indent=2))
    elif args.command == "inspect":
        print(json.dumps(probe(args.input), indent=2))
        result = subprocess.run(["ffmpeg", "-hide_banner", "-i", args.input, "-af", "astats=metadata=0:reset=0", "-f", "null", "-"], capture_output=True, text=True)
        if result.returncode:
            raise ValueError(result.stderr[-3000:])
        print("\n".join(line for line in result.stderr.splitlines() if any(k in line for k in ("Peak level dB", "RMS level dB", "DC offset", "Number of NaNs", "Number of Infs"))))
    else:
        render(args)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, KeyError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
