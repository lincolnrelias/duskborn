import array, json, shutil
from pathlib import Path

# Reuse the source-based edit/export functions from the previous gathering pass.
# This executes the helper definitions only, never regenerating its earlier assets.
shared = Path(__file__).resolve().parents[2]/'Harvesting/v002/build.py'
exec(shared.read_text().split("glass=pool(")[0])
manifest['brief'] = 'Six tactile pickup tiers: common cloth and wooden pack contact; uncommon adds a small metal clasp; rare adds clear glass contact; epic adds a brief resonant plate tail; legendary adds staggered glass and plate closure; cursed uses a dark, dry wood and soft-body closure. Four recorded performances per bank, no electronic chime. Material-specific contacts remain audible.'
cloth=pool('cloth[1-4].ogg')
wood=pool('impactWood_light_*.ogg')
stone=pool('impactMining_*.ogg')
glass=pool('impactGlass_light_*.ogg')+pool('impactGlass_medium_*.ogg')
metal=pool('impactMetal_light_*.ogg')
plate=pool('impactPlate_light_*.ogg')
soft=pool('impactSoft_medium_*.ogg')
heavywood=pool('impactWood_heavy_*.ogg')
assert min(map(len,[cloth,wood,stone,glass,metal,plate,soft])) >= 4
tiers=['common','uncommon','rare','epic','legendary','cursed']
for tier_index,tier in enumerate(tiers):
    for material,body in [('item',wood),('wood',wood),('stone',stone),('crystal',glass),('soft',soft)]:
        for i in range(4):
            layers=[layer(body[i],seconds=.2,gain=-10,lowpass=4500),layer(cloth[i],seconds=.23,gain=-8,lowpass=4500,delay=.012)]
            if tier=='cursed':
                layers.append(layer(heavywood[i],seconds=.3,gain=-12,lowpass=2300,rate=.88,delay=.025))
                layers.append(layer(soft[i],seconds=.24,gain=-13,lowpass=3000,delay=.07))
            else:
                if tier_index>=1: layers.append(layer(metal[i],seconds=.19,gain=-15,lowpass=5500,delay=.025))
                if tier_index>=2: layers.append(layer(glass[(i+2)%len(glass)],seconds=.23,gain=-10,lowpass=6500,delay=.04))
                if tier_index>=3: layers.append(layer(plate[i],seconds=.36,gain=-13,lowpass=6000,delay=.055))
                if tier_index>=4: layers.append(layer(glass[(i+3)%len(glass)],seconds=.24,gain=-13,lowpass=6500,delay=.13))
            name=f'pickup_{tier}_{i+1:02d}' if material=='item' else f'collect_{material}_{tier}_{i+1:02d}'
            out=make(name,layers,peak_db=-6+tier_index*.75,max_gain_db=6)
            if material=='item' and i==0:
                for folder in ['Art/SFX','Resources/SFX']:
                    old=PROJECT/'Assets/_Duskborn'/folder/f'pickup_{tier}.wav'
                    if old.exists(): shutil.copy2(out,old)
                if tier=='common':
                    old=PROJECT/'Assets/_Duskborn/Art/SFX/item_pickup.wav'
                    if old.exists(): shutil.copy2(out,old)
(ROOT/'manifest.json').write_text(json.dumps(manifest,indent=2))
audition=array.array('f')
for tier in tiers:
    for i in range(2):
        audition.extend(decode(ROOT/'candidates'/f'pickup_{tier}_{i+1:02d}.wav'))
        audition.extend(array.array('f',[0])*16800)
    audition.extend(array.array('f',[0])*24000)
subprocess.run(['ffmpeg','-v','error','-y','-f','f32le','-ar','48000','-ac','1','-i','-','-c:a','pcm_s24le',str(ROOT/'audition.wav')],input=audition.tobytes(),check=True)
print(json.dumps({'masters':len(manifest['candidates']),'sources':len(manifest['sources']),'max_peak_dbfs':max(c['peak_dbfs'] for c in manifest['candidates']),'longest_seconds':max(c['duration'] for c in manifest['candidates'])}))
