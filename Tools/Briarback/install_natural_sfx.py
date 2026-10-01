"""Install the selected CC0 Briarback auditions without launching Unity.

Preserves existing audio GUIDs and changes only Briarback's audio references.
The builder consumes the same staged unity/ files so rebuilds keep these sounds.
"""
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[2]
PACK = ROOT / "Artifacts/NaturalSfx/Briarback/v003"
REVISION = ROOT / "Artifacts/NaturalSfx/Briarback/v004"
DEST = ROOT / "Assets/_Duskborn/Art/Models/Briarback"
CUES = ["Windup", "Charge", "HeadbuttWindup", "Headbutt", "Hurt", "Death", "Hoof"]


def filename(cue, take):
    suffix = "" if take == 1 and cue in ("Windup", "Charge", "Hurt", "Death") else f"_{take:02d}"
    return "BB_" + cue + suffix + ".wav"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    manifest = json.loads((PACK / "manifest.json").read_text())
    for clip in manifest["candidates"]:
        assert sha(PACK / clip["file"]) == clip["sha256"], clip["name"]
    replacements = json.loads((REVISION / "manifest.json").read_text())
    for clip in replacements["candidates"]:
        assert sha(REVISION / clip["file"]) == clip["sha256"], clip["name"]
    staging = REVISION / "unity"
    staging.mkdir(exist_ok=True)
    template = (DEST / "BB_Windup.wav.meta").read_text()
    template = template.replace("  normalize: 1", "  normalize: 0").replace("    preloadAudioData: 0", "    preloadAudioData: 1")
    imports, refs = [], {}
    for cue in CUES:
        refs[cue] = []
        for take in (1, 2):
            name = filename(cue, take)
            source = PACK / "candidates" / f"BB_{cue}_{take:02d}.wav"
            revised = REVISION / "candidates" / f"BB_{cue}_{take:02d}.wav"
            if revised.exists(): source = revised
            staged = staging / name
            if cue == "HeadbuttWindup" and not revised.exists():
                source = PACK / "candidates" / f"BB_Windup_{take:02d}.wav"
                command = ["ffmpeg", "-nostdin", "-v", "error", "-n", "-i", str(source), "-af",
                    "atrim=duration=0.38,asetpts=PTS-STARTPTS,afade=t=out:st=0.30:d=0.08",
                    "-ar", "48000", "-ac", "1", "-c:a", "pcm_s24le", "-map_metadata", "-1", str(staged)]
                receipt = Path(str(staged) + ".receipt.json")
                if not staged.exists():
                    subprocess.run(command, check=True, stdin=subprocess.DEVNULL, timeout=30)
                    receipt.write_text(json.dumps({"source": str(source.relative_to(ROOT)), "source_sha256": sha(source),
                        "command": command, "output_sha256": sha(staged), "license": "CC0 1.0"}, indent=2))
                else:
                    saved = json.loads(receipt.read_text())
                    assert saved["source_sha256"] == sha(source) and saved["output_sha256"] == sha(staged)
            else:
                shutil.copyfile(source, staged)
            target = DEST / name
            shutil.copyfile(staged, target)
            meta = Path(str(target) + ".meta")
            if meta.exists():
                text = meta.read_text()
                guid = re.search(r"^guid: (\w+)$", text, re.M)[1]
                text = re.sub(r"normalize: \d+", "normalize: 0", text)
                text = re.sub(r"preloadAudioData: \d+", "preloadAudioData: 1", text)
            else:
                guid = uuid.uuid4().hex
                text = re.sub(r"^guid: \w+$", "guid: " + guid, template, flags=re.M)
            meta.write_text(text, encoding="utf-8")
            refs[cue].append(guid)
            imports.append({"file": str(target.relative_to(ROOT)), "sha256": sha(target), "guid": guid,
                "staged_source": str(staged.relative_to(ROOT)), "license": "CC0 1.0"})
    prefab = ROOT / "Assets/_Duskborn/Prefabs/Enemies/Briarback.prefab"
    text = prefab.read_text()
    for cue in CUES:
        field = cue[0].lower() + cue[1:] + "Clips"
        text = re.sub(r"^  " + field + r":\n(?:  - \{[^\n]+\}\n)*", "", text, flags=re.M)
    anchor = re.search(r"^  deathClip: \{[^\n]+\}\n", text, re.M)
    assert anchor, "Missing legacy death reference"
    arrays = "".join("  " + cue[0].lower() + cue[1:] + "Clips:\n" +
        "".join("  - {fileID: 8300000, guid: " + guid + ", type: 3}\n" for guid in refs[cue]) for cue in CUES)
    text = text[:anchor.end()] + arrays + text[anchor.end():]
    prefab.write_text(text, encoding="utf-8")
    (DEST / "BB_Audio_Credits.txt").write_text(
        "Briarback natural sound effects for Duskborn\n\n"
        "Vocal foley: Joseph Sardin / BigSoundBank. Human mouth performances imitating pigs.\n"
        "https://bigsoundbank.com/grumpy-pig-1-s1658.html\n"
        "https://bigsoundbank.com/cochon-qui-grogne-2-s1659.html\n"
        "Attack growl performances: Joseph Sardin (human creature growls), replacing pig imitation/creaks.\n"
        "https://bigsoundbank.com/zombie-6-s2111.html\nhttps://bigsoundbank.com/zombie-7-s2112.html\n"
        "Material recordings: Kenney, Impact Sounds and RPG Audio.\n"
        "https://kenney.nl/assets/impact-sounds\nhttps://kenney.nl/assets/rpg-audio\n"
        "All source recordings: CC0 1.0. Attribution optional.\n"
        "https://creativecommons.org/publicdomain/zero/1.0/\n"
        "Edited/layered; shortened headbutt warnings. Sources, hashes and recipes:\n"
        "Artifacts/NaturalSfx/Briarback/v003/manifest.json (hurt/death/hoof)\n"
        "Artifacts/NaturalSfx/Briarback/v004/manifest.json (attacks and warnings).\n", encoding="utf-8")
    credits_meta = DEST / "BB_Audio_Credits.txt.meta"
    if not credits_meta.exists():
        credits_meta.write_text("fileFormatVersion: 2\nguid: " + uuid.uuid4().hex +
            "\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    (REVISION / "unity-integration.json").write_text(json.dumps({"clips": imports,
        "prefab": str(prefab.relative_to(ROOT)), "headbutt_warning_seconds": .38,
        "unity_import_validation": "Pending: Unity is open; installed files and GUIDs checked offline"}, indent=2))
    print(f"Installed {len(imports)} clips, preserving original GUIDs; wired seven variant pairs.")


if __name__ == "__main__":
    main()
