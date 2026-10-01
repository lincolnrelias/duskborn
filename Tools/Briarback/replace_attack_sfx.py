"""Render v004 Briarback attacks from new CC0 vocal performances, without wood.

Sources 2111/2112 must be downloaded into v004/source first. Preserves v003.
"""
import importlib.util
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("sfx", Path(__file__).with_name("build_natural_sfx.py"))
sfx = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sfx)
sfx.OUT = ROOT / "Artifacts/NaturalSfx/Briarback/v004"
sfx.SRC = sfx.OUT / "source"
sfx.LAYERS = sfx.OUT / "layers"
sfx.CANDIDATES = sfx.OUT / "candidates"


def main():
    sfx.LAYERS.mkdir()
    sfx.CANDIDATES.mkdir()
    old = ROOT / "Artifacts/NaturalSfx/Briarback/v003"
    sources = []
    for number in (2111, 2112):
        source = sfx.SRC / f"{number}.wav"
        assert "CC0" in (sfx.SRC / f"{number}-license-page.html").read_text(encoding="utf-8")
        sources.append({**sfx.record(source), "creator": "Joseph Sardin", "license": "CC0 1.0", "license_url": sfx.CC0,
            "source_page": f"https://bigsoundbank.com/zombie-{number-2105}-s{number}.html",
            "download_url": f"https://bigsoundbank.com/UPLOAD/bwf-en/{number}.wav", "download_date": "2026-10-01",
            "source_fidelity": "48 kHz mono PCM 24-bit", "performance": "Recorded unprocessed human creature growl, not a live boar",
            "required_attribution": "None", "inspection": sfx.measure(source)})
    prior = json.loads((old / "manifest.json").read_text())
    filenames = [f"{stem}_{i:03d}.ogg" for stem in ["footstep_grass", "impactSoft_heavy", "impactPunch_medium"] for i in range(2)]
    for filename in filenames:
        shutil.copyfile(old / "source" / filename, sfx.SRC / filename)
        provenance = next(x for x in prior["sources"] if Path(x["file"]).name == filename)
        sources.append({**provenance, **sfx.record(sfx.SRC / filename)})
    shutil.copyfile(old / "source/kenney_impact-sounds-License.txt", sfx.SRC / "kenney_impact-sounds-License.txt")
    clips = []
    for take in (1, 2):
        suffix = f"{take:02d}"
        vocal = f"{2110+take}.wav"
        start = [.16, .24][take-1]  # Excludes the isolated source full-scale peak.
        body = sfx.layer("body_"+suffix, f"impactSoft_heavy_{take-1:03d}.ogg", gain=-13, lowpass=1600)
        punch = sfx.layer("punch_"+suffix, f"impactPunch_medium_{take-1:03d}.ogg", gain=-13, lowpass=1800)
        grass = sfx.layer("grass_"+suffix, f"footstep_grass_{take-1:03d}.ogg", gain=-17, lowpass=2800)
        warning = sfx.layer("warning_voice_"+suffix, vocal, start=[.28,.66][take-1], duration=.73, rate=.94, gain=-8, lowpass=3500)
        clips.append(sfx.mix("BB_Windup_"+suffix, "Rough throat growl and restrained planted earth contact; no creak, wooden resonance, nasal pig imitation or added reverb.", [(warning,0,0),(grass,0,-3)], .88))
        head_warning = sfx.layer("head_warning_voice_"+suffix, vocal, start=start, duration=.31, rate=.94, gain=-8, lowpass=3500)
        clips.append(sfx.mix("BB_HeadbuttWindup_"+suffix, "Short rough intake/effort before the close-range strike; no wood or chair-like creak.", [(head_warning,0,0),(grass,0,-6)], .38))
        charge = sfx.layer("charge_voice_"+suffix, vocal, start=start, duration=.67, rate=.94, gain=-8, lowpass=3500)
        clips.append(sfx.mix("BB_Charge_"+suffix, "Sustained coarse throat effort above dry hoof thuds and muted grass contact. No wooden plates, creaks or pitched synthetic layer.", [(charge,0,0),(body,5,-3),(grass,5,0),(body,250,-5),(grass,265,-2),(body,480,-7),(grass,485,-4)], .78))
        butt = sfx.layer("headbutt_voice_"+suffix, vocal, start=start, duration=.22, rate=.94, gain=-8, lowpass=3200)
        clips.append(sfx.mix("BB_Headbutt_"+suffix, "Abrupt coarse effort, compact dull body impact at +60 ms and a short earth scuff; no wood creak or squeaky pig imitation.", [(butt,0,0),(body,60,0),(punch,60,0),(grass,75,-4)], .38))
    order = ["Windup", "HeadbuttWindup", "Charge", "Headbutt"]
    clips.sort(key=lambda c: (order.index(c["name"].split("_")[1]), c["name"]))
    parts, offset = [], 0
    for c in clips:
        pair = [x for x in clips if x["name"].split("_")[1] == c["name"].split("_")[1]]
        gain = min(sum(x["rms_dbfs"] for x in pair)/2-c["rms_dbfs"], -3-c["peak_dbfs"])
        c["audition_start_seconds"] = round(offset,3)
        c["audition_gain_db"] = gain
        parts.append((sfx.OUT/c["file"], round(offset*1000), gain))
        offset += c["duration_seconds"] + .6
    preview = sfx.mix("Briarback_Attacks_v004", "Two each: charge warning, headbutt warning, charge, headbutt. Pairwise gain-matched preview with 0.6-second gaps.", parts, offset)
    sfx.save(sfx.OUT / "manifest.json", {"version": "v004", "reason": "User heard chair-like squeaks in charging/headbutting; replaced vocal source and removed all wood/creak from attack cues including warnings",
        "sources": sources, "candidates": clips, "audition": preview,
        "validation": "Mono 48 kHz PCM 24-bit, decoding, finite samples, zero output full-scale samples, peaks below -1 dBFS. Two different original vocal performances. No paid or synthesized source.",
        "perceptual_status": "Agent cannot listen; naturalness and gameplay mix remain unverified",
        "unchanged": "Hurt, death and hoof cues retain v003 sources and recipes"})
    print(f"Rendered {len(clips)} replacement attack cues and matched preview.")


if __name__ == "__main__":
    main()
