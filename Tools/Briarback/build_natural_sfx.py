"""Build a free, recorded-foley Briarback audition set; never modifies Assets.

Run with Python and FFmpeg installed. Sources must already be downloaded.
Every render refuses an existing output and retains a reproducible receipt.
"""
import array
import hashlib
import json
import math
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Artifacts/NaturalSfx/Briarback/v003"
SRC = OUT / "source"
LAYERS = OUT / "layers"
CANDIDATES = OUT / "candidates"
CC0 = "https://creativecommons.org/publicdomain/zero/1.0/"


def run(cmd):
    result = subprocess.run(cmd, capture_output=True, stdin=subprocess.DEVNULL, timeout=30)
    if result.returncode:
        raise RuntimeError(result.stderr.decode(errors="replace")[-3000:])
    return result.stdout


def record(path):
    return {"file": str(path.relative_to(OUT)), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}


def save(path, data):
    with path.open("x", encoding="utf-8") as f:
        json.dump(data, f, indent=2)


def measure(path):
    probe = json.loads(run(["ffprobe", "-v", "error", "-show_streams", "-show_format", "-of", "json", str(path)]))
    pcm = array.array("f", run(["ffmpeg", "-v", "error", "-i", str(path), "-ac", "1", "-ar", "48000", "-f", "f32le", "-"]))
    peak = max(abs(x) for x in pcm)
    rms = math.sqrt(sum(x*x for x in pcm) / len(pcm))
    active = [i for i, x in enumerate(pcm) if abs(x) > .00316228]
    return {"duration_seconds": len(pcm)/48000, "sample_rate": int(probe["streams"][0]["sample_rate"]),
            "channels": probe["streams"][0]["channels"], "codec": probe["streams"][0]["codec_name"],
            "peak_dbfs": 20*math.log10(max(peak, 1e-12)), "rms_dbfs": 20*math.log10(max(rms, 1e-12)),
            "full_scale_samples": sum(abs(x) >= .999999 for x in pcm),
            "nonfinite_samples": sum(not math.isfinite(x) for x in pcm),
            "onset_at_minus50db_seconds": active[0]/48000 if active else None,
            "tail_below_minus50db_seconds": (len(pcm)-1-active[-1])/48000 if active else None}


def render(path, command, recipe):
    if path.exists() or Path(str(path)+".receipt.json").exists():
        raise FileExistsError(path)
    run(["ffmpeg", "-nostdin", "-v", "error", "-n"] + command + ["-ar", "48000", "-ac", "1", "-c:a", "pcm_s24le", "-map_metadata", "-1", str(path)])
    stats = measure(path)
    save(Path(str(path)+".receipt.json"), {"recipe": recipe, "ffmpeg_arguments": command,
          "output": record(path), "inspection": stats})
    return stats


def layer(name, filename, start=0, duration=None, rate=1, gain=-6, lowpass=None):
    source = SRC / filename
    if filename in ("1658.wav", "1659.wav"):
        original = array.array("f", run(["ffmpeg", "-v", "error", "-i", str(source), "-af",
            f"atrim=start={start}:duration={duration}", "-f", "f32le", "-"]))
        assert not any(abs(x) >= .999999 for x in original), (name, "Source excerpt has full-scale samples")
    path = LAYERS / (name+".wav")
    filters = [f"atrim=start={start}"+(f":duration={duration}" if duration else ""), "asetpts=PTS-STARTPTS", "aresample=48000"]
    if rate != 1:
        filters += [f"asetrate={round(48000*rate)}", "aresample=48000"]
    if lowpass:
        filters += [f"lowpass=f={lowpass}"]
    filters += ["highpass=f=45", f"volume={gain}dB"]
    length = (duration or measure(source)["duration_seconds"])/rate
    filters += ["afade=t=in:d=0.003", f"afade=t=out:st={max(0,length-.018)}:d=0.018"]
    render(path, ["-i", str(source), "-af", ",".join(filters)], {"source": record(source), "start": start,
           "duration": duration, "playback_rate": rate, "gain_db": gain, "lowpass_hz": lowpass})
    return path


def mix(name, brief, parts, duration):
    path = CANDIDATES / (name+".wav")
    command, filters, recipe = [], [], []
    for i, (part, delay, gain) in enumerate(parts):
        command += ["-i", str(part)]
        filters += [f"[{i}:a]volume={gain}dB,adelay={delay}:all=1[a{i}]"]
        recipe.append({**record(part), "delay_ms": delay, "gain_db": gain})
    labels = "".join(f"[a{i}]" for i in range(len(parts)))
    filters += [labels+f"amix=inputs={len(parts)}:normalize=0:duration=longest,apad=whole_dur={duration},atrim=duration={duration},afade=t=out:st={duration-.025}:d=0.025[out]"]
    command += ["-filter_complex", ";".join(filters), "-map", "[out]", "-t", str(duration)]
    stats = render(path, command, {"brief": brief, "layers": recipe, "duration_seconds": duration})
    assert stats["peak_dbfs"] < -1, (name, stats)
    assert stats["full_scale_samples"] == 0 and stats["nonfinite_samples"] == 0
    assert stats["sample_rate"] == 48000 and stats["codec"] == "pcm_s24le" and stats["channels"] == 1
    return {"name": name, "brief": brief, **record(path), **stats}


def main():
    if (OUT/"manifest.json").exists() or CANDIDATES.exists():
        raise FileExistsError("Choose a fresh version folder before rebuilding")
    LAYERS.mkdir(parents=True)
    CANDIDATES.mkdir()
    SRC.mkdir()
    previous = OUT.parent / "v001/source"
    for filename in ["1658.wav", "1659.wav", "1658-license-page.html", "1659-license-page.html", "BigSoundBank-licenses.html"]:
        shutil.copyfile(previous / filename, SRC / filename)
    sources = []
    for number, page in [(1658,"grumpy-pig-1-s1658.html"), (1659,"cochon-qui-grogne-2-s1659.html")]:
        path = SRC/f"{number}.wav"
        assert "CC0" in (SRC/f"{number}-license-page.html").read_text(encoding="utf-8")
        sources.append({**record(path), "creator": "Joseph Sardin", "license": "CC0 1.0", "license_url": CC0,
          "source_page": "https://bigsoundbank.com/"+page, "download_url": f"https://bigsoundbank.com/UPLOAD/bwf-en/{number}.wav",
          "download_date": "2026-10-01", "recording": "Human mouth performance imitating pig grunts; not a live animal recording",
          "source_fidelity": "48 kHz mono PCM 24-bit", "inspection": measure(path), "required_attribution": "None"})
    packs = [("kenney_impact-sounds", "https://kenney.nl/assets/impact-sounds", [f"{stem}_{i:03d}.ogg" for stem in ["footstep_grass", "impactWood_heavy", "impactSoft_heavy", "impactPunch_medium"] for i in range(2)]),
             ("kenney_rpg-audio", "https://kenney.nl/assets/rpg-audio", ["creak1.ogg", "creak2.ogg", "cloth1.ogg", "cloth2.ogg"])]
    for pack, url, names in packs:
        folder = ROOT/"Artifacts/NaturalSfx/Library"/pack
        shutil.copyfile(folder/"License.txt", SRC/(pack+"-License.txt"))
        for name in names:
            path = SRC/name
            if path.exists():
                raise FileExistsError(path)
            shutil.copyfile(folder/"Audio"/name, path)
            sources.append({**record(path), "creator": "Kenney", "license": "CC0 1.0", "license_url": CC0,
              "source_page": url, "original_file": str((folder/"Audio"/name).relative_to(ROOT)),
              "source_fidelity": "Lossy Ogg Vorbis; WAV conversion does not recover detail", "required_attribution": "None"})
    candidates = []
    for take in range(2):
        suffix = f"{take+1:02d}"
        creak = layer("creak_"+suffix, f"creak{take+1}.ogg", gain=-15, lowpass=4500)
        grass = layer("grass_"+suffix, f"footstep_grass_{take:03d}.ogg", gain=-10)
        wood = layer("wood_"+suffix, f"impactWood_heavy_{take:03d}.ogg", gain=-15, lowpass=4500)
        body = layer("body_"+suffix, f"impactSoft_heavy_{take:03d}.ogg", gain=-10, lowpass=3000)
        punch = layer("punch_"+suffix, f"impactPunch_medium_{take:03d}.ogg", gain=-12)
        cloth = layer("cloth_"+suffix, f"cloth{take+1}.ogg", gain=-18)
        wind = layer("warning_voice_"+suffix, "1659.wav", start=[.57,1.30][take], duration=[.59,.55][take], rate=[.92,.94][take], gain=-7, lowpass=6500)
        candidates.append(mix("BB_Windup_"+suffix, "Close guttural pig effort with quiet bark creak and a planted grass scrape; warning begins immediately, completes within 0.9 seconds.", [(wind,0,0),(creak,35,0),(grass,60,-6)], .88))
        rush = layer("charge_voice_"+suffix, "1659.wav", start=[2.73,5.70][take], duration=[.25,.37][take], rate=.92, gain=-7, lowpass=7000)
        candidates.append(mix("BB_Charge_"+suffix, "Forceful breathy grunt over a short burst of hooves on earth and woody armor chatter; one-shot rush cue, no bass boom.", [(rush,0,0),(grass,10,0),(body,20,-5),(wood,200,-5),(grass,380,-3),(cloth,45,0)], .78))
        butt = layer("headbutt_voice_"+suffix, "1659.wav", start=[.57,1.30][take], duration=[.22,.27][take], rate=.96, gain=-7)
        candidates.append(mix("BB_Headbutt_"+suffix, "Short nasal effort and dense head strike with a dry bark knock; physical impact at 0.06 seconds after the strike phase begins.", [(butt,0,0),(punch,60,0),(wood,65,-5),(cloth,20,0)], .38))
        hurt = layer("hurt_voice_"+suffix, "1658.wav" if take == 0 else "1659.wav", start=[1.32,.57][take], duration=[.24,.22][take], rate=.98, gain=-7)
        candidates.append(mix("BB_Hurt_"+suffix, "Brief interrupted throaty grunt with a restrained bark contact; fast readable pain response without a long scream.", [(hurt,0,0),(wood,8,-9)], .34))
        death = layer("death_voice_"+suffix, "1659.wav", start=[4.60,3.74][take], duration=[.69,.58][take], rate=[.88,.90][take], gain=-8, lowpass=6000)
        candidates.append(mix("BB_Death_"+suffix, "Uneven grumbling exhale trailing off into a soft heavy body landing, woody plates settling and a grass rustle; intended for ragdoll collapse.", [(death,0,0),(body,520,0),(wood,565,-3),(grass,620,-6),(cloth,760,0)], 1.45))
        candidates.append(mix("BB_Hoof_"+suffix, "One compact heavy hoof plant on grass and soil, subtle hard contact and wood armor movement; no resonant metal.", [(grass,0,0),(body,3,-4),(wood,10,-10)], .28))
    # Sequence for quick comparison: two takes per cue, separated by silence.
    ordered = sorted(candidates, key=lambda c: (["Windup","Charge","Headbutt","Hurt","Death","Hoof"].index(c["name"].split("_")[1]), c["name"]))
    audition_parts, offset = [], 0
    for cue in ordered:
        family = cue["name"].split("_")[1]
        pair = [c for c in ordered if c["name"].split("_")[1] == family]
        target_rms = sum(c["rms_dbfs"] for c in pair)/len(pair)
        preview_gain = min(target_rms-cue["rms_dbfs"], -3-cue["peak_dbfs"])
        audition_parts.append((OUT/cue["file"], round(offset*1000), preview_gain))
        cue["audition_start_seconds"] = round(offset, 3)
        cue["audition_gain_db"] = round(preview_gain, 3)
        offset += cue["duration_seconds"]+ .65
    # Simple per-pair gain matching, without compression or changing masters.
    preview = mix("Briarback_Audition_Matched", "Windup 1/2, charge 1/2, headbutt 1/2, hurt 1/2, death 1/2, hoof 1/2; 0.65-second gaps. Gain matched within each pair by RMS with at least 3 dB headroom; no compression.", audition_parts, offset)
    manifest = {"enemy": "Briarback", "version": "v003", "created_date": "2026-10-01", "sources": sources,
      "candidates": ordered, "audition": preview, "license": "All source recordings are CC0 1.0; attribution optional",
      "design": "Recorded pig vocal foley and CC0 material samples only; no oscillators, synthesized noise, paid generation, reverb, limiter, or denoising. Small playback-rate shifts for body size. Two different performance excerpts per voiced cue; material takes also differ.",
      "validation": "All masters decode as mono 48 kHz PCM 24-bit, finite samples, zero full-scale samples, peaks below -1 dBFS. Original vocal files have full-scale samples; selected unprocessed excerpts were individually checked to exclude them. This check cannot prove absence of all source distortion.",
      "perceptual_status": "Not auditioned by the agent; naturalness, tail cuts, repeated playback, distance and crowded combat mix remain unverified",
      "integration_status": "Audition only, outside Assets. No prefab or runtime changes. Current Briarback presentation shares charge/headbutt cues and has no hoof event; dedicated headbutt and hoof playback requires wiring.",
      "reference": "Existing BB_Windup placeholder measured -6.07 dBFS peak / -20.62 dBFS RMS; candidates retain conservative voice gain and quieter material layers instead of forced equal normalization"}
    save(OUT/"manifest.json", manifest)
    print(json.dumps({"candidate_count": len(candidates), "audition": preview["file"], "peak_range_dbfs": [min(c["peak_dbfs"] for c in candidates), max(c["peak_dbfs"] for c in candidates)]}, indent=2))


if __name__ == "__main__":
    main()
