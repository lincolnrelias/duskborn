import array, hashlib, json, math, re, shutil, subprocess, uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[3]
LIB = PROJECT/'Artifacts/NaturalSfx/Library'
DEST = PROJECT/'Assets/_Duskborn/Resources/SFX/Harvesting'
for folder in ['source','candidates','recipes']:
    (ROOT/folder).mkdir(exist_ok=True)
index = json.loads((LIB/'sources.json').read_text())
lookup = {str((LIB/f['path']).resolve()):(p,f) for p in index['packs'] for f in p['files']}
manifest = {'brief':'Louder dry harvest contacts; snapping timber with leafy collapse; eight physically textured crystal banks; short, rounded material and bag pickup foley. No oscillator/noise synthesis or rarity jingles.', 'sources':[], 'candidates':[], 'perceptual_quality':'Not auditioned by agent; live playback unverified.'}
cache = {}
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def decode(path):
    if str(path) not in cache:
        raw = subprocess.run(['ffmpeg','-v','error','-i',str(path),'-ac','1','-ar','48000','-f','f32le','-'],capture_output=True,check=True).stdout
        a=array.array('f'); a.frombytes(raw); cache[str(path)]=a
    return cache[str(path)]
def pool(pattern):
    seen=set(); result=[]
    for p in sorted(LIB.rglob(pattern)):
        h=digest(p)
        if h not in seen: result.append(p); seen.add(h)
    return result
def stage(path):
    out=ROOT/'source'/path.name
    if str(path.resolve()) in lookup:
        pack,f=lookup[str(path.resolve())]; assert digest(path)==f['sha256']
        if not out.exists(): shutil.copy2(path,out)
        lic=LIB/pack['license_files'][0]; shutil.copy2(lic,ROOT/'source'/(pack['name']+'-License.txt'))
        receipt={'file':out.name,'creator':pack['creator'],'license':pack['license'],'source_url':pack['source_url'],'license_url':pack['license_url'],'sha256':digest(path),'fidelity':'Original lossy OGG; converted mono 48k WAV does not restore lost fidelity.'}
    else:
        urls={'1299':'broken-twigs-1','1300':'broken-twigs-2','3322':'fire-foley','1529':'splash-small-1'}
        number=path.stem
        receipt={'file':path.name,'creator':'Joseph SARDIN & Axeline T.' if number=='3322' else 'Joseph SARDIN','license':'CC0-1.0','source_url':f'https://bigsoundbank.com/{urls[number]}-s{number}.html','license_url':'https://creativecommons.org/publicdomain/zero/1.0/','sha256':digest(path),'license_evidence':number+'-page.html','fidelity':'Original 48k 24bit mono WAV retained.'}
    if not any(s['file']==receipt['file'] for s in manifest['sources']): manifest['sources'].append(receipt)
    return out
def segment(path, seconds=.5, gain=0, start=None, rate=1, lowpass=None):
    path=stage(path); src=decode(path)
    if start is None:
        peak=max(map(abs,src)); onset=next((i for i,v in enumerate(src) if abs(v)>max(.008,peak*.05)),0)
        start=max(0,onset/48000-.003)
    src=src[int(start*48000):int((start+seconds*rate)*48000)]
    values=array.array('f',(src[min(len(src)-1,int(i*rate))] for i in range(int(len(src)/rate))))
    scale=10**(gain/20); last=0
    alpha=1-math.exp(-2*math.pi*lowpass/48000) if lowpass else 1
    for i,v in enumerate(values):
        last += alpha*(v-last)
        fade=min(1,i/96,(len(values)-1-i)/max(96,min(1440,len(values)//5)))
        values[i]=last*scale*fade
    return values, {'source':path.name,'start':start,'duration':seconds,'gain_db':gain,'playback_rate':rate,'lowpass_hz':lowpass,'fade_in_ms':2,'fade_out_ms':min(30,len(values)/240)}
def make(name,layers,peak_db=-1.2,max_gain_db=8):
    length=max(len(v)+int(delay*48000) for v,r,delay in layers)
    mix=array.array('f',[0])*length
    recipes=[]
    for values,receipt,delay in layers:
        offset=int(delay*48000)
        for i,v in enumerate(values): mix[offset+i]+=v
        recipes.append(dict(receipt,delay_seconds=delay))
    peak=max(map(abs,mix)); gain=min(10**(max_gain_db/20),10**(peak_db/20)/peak)
    mix=array.array('f',(v*gain for v in mix))
    out=ROOT/'candidates'/(name+'.wav')
    subprocess.run(['ffmpeg','-v','error','-y','-f','f32le','-ar','48000','-ac','1','-i','-','-c:a','pcm_s24le',str(out)],input=mix.tobytes(),check=True)
    peak=max(map(abs,mix)); rms=math.sqrt(sum(v*v for v in mix)/len(mix))
    assert peak<.99 and not any(abs(v)>=1 for v in mix)
    recipe={'layers':recipes,'final_gain_db':20*math.log10(gain),'sample_rate':48000,'channels':1,'encoding':'PCM24'}
    (ROOT/'recipes'/(name+'.json')).write_text(json.dumps(recipe,indent=2))
    manifest['candidates'].append({'file':out.name,'sha256':digest(out),'duration':len(mix)/48000,'peak_dbfs':20*math.log10(peak),'rms_dbfs':20*math.log10(rms),'recipe':recipe})
    shutil.copy2(out,DEST/out.name)
    meta=DEST/(out.name+'.meta')
    if not meta.exists():
        template=(DEST/'harvest_wood_01.wav.meta').read_text()
        meta.write_text(re.sub(r'guid: \w+', 'guid: '+uuid.uuid4().hex,template))
    return out
def layer(path,delay=0,**kw):
    v,r=segment(path,**kw); return v,r,delay

glass=pool('impactGlass_light_*.ogg')+pool('impactGlass_medium_*.ogg')
heavyglass=pool('impactGlass_heavy_*.ogg')
wood=pool('impactWood_heavy_*.ogg')
stone=pool('impactMining_*.ogg')
metal=pool('impactMetal_light_*.ogg')
tin=pool('impactTin_medium_*.ogg')
plate=pool('impactPlate_light_*.ogg')
grass=pool('footstep_grass_*.ogg')
cloth=pool('cloth[1-4].ogg')
soft=pool('impactSoft_medium_*.ogg')
assert min(len(glass),len(heavyglass),len(wood),len(stone),len(metal),len(tin),len(plate),len(grass),len(cloth),len(soft))>=4
twigs=[ROOT/'source/1299.wav',ROOT/'source/1300.wav']
fire=ROOT/'source/3322.wav'; water=ROOT/'source/1529.wav'
# Louder contacts retain the already distinct performances and preserve their GUIDs.
for material in ['wood','stone','ore','foliage']:
    for i in range(4):
        source=ROOT.parent/'v001/candidates'/f'harvest_{material}_{i+1:02d}.wav'
        data=decode(source)
        receipt={'source':str(source.relative_to(PROJECT)),'sha256':digest(source),'gain_db':0,'prior_provenance':'../v001/manifest.json'}
        make(source.stem,[(data,receipt,0)],max_gain_db=5.5)
for i in range(2):
    out=make(f'tree_break_{i+1:02d}',[layer(twigs[i],seconds=.95,rate=.78),layer(wood[i],seconds=.55,gain=-5,delay=.08),layer(grass[i],seconds=.65,gain=-10,delay=.25)])
    old='tree_fall'+('_02' if i else '')+'.wav'
    for folder in ['Art/SFX','Resources/SFX']:
        path=PROJECT/'Assets/_Duskborn'/folder/old
        if path.exists(): shutil.copy2(out,path)

# Each element has distinct performances, contact/body and restrained natural debris.
for element in ['flame','nature','storm','earth','frost','blood','dark','light']:
    for role in ['hit','break']:
        for i in range(4):
            broken=role=='break'; length=.65 if broken else .32
            primary=heavyglass[i] if broken or element=='dark' else glass[i]
            rate=.8 if element in ['dark','blood'] else 1
            layers=[layer(primary,seconds=length,rate=rate,gain=-2,lowpass=6500 if element=='dark' else None)]
            if element=='flame': layers.append(layer(fire,seconds=length,start=2+i*4,gain=-9,delay=.015))
            elif element=='nature': layers.append(layer(grass[i],seconds=length,gain=-8,delay=.02))
            elif element=='storm': layers.append(layer(tin[i],seconds=length,gain=-8,delay=.008))
            elif element=='earth': layers.append(layer(stone[i],seconds=length,gain=-5,delay=.01))
            elif element=='frost': layers.append(layer(glass[(i+2)%len(glass)],seconds=.22,gain=-11,delay=.045))
            elif element=='blood': layers.append(layer(water,seconds=length,gain=-9,rate=.85,delay=.02))
            elif element=='dark': layers.append(layer(soft[i],seconds=length,gain=-6,rate=.8,delay=.01))
            elif element=='light': layers.append(layer(plate[i],seconds=length,gain=-10,delay=.01))
            if broken: layers.append(layer(glass[(i+1)%len(glass)],seconds=.4,gain=-10,delay=.14))
            make(f'crystal_{element}_{role}_{i+1:02d}',layers)
for material,body in [('wood',wood),('stone',stone),('crystal',glass),('soft',soft)]:
    for i in range(4):
        make(f'collect_{material}_{i+1:02d}',[layer(body[i],seconds=.19,gain=-12,lowpass=4200),layer(cloth[i],seconds=.24,gain=-8,lowpass=4200,delay=.01)],peak_db=-7,max_gain_db=3)
(ROOT/'manifest.json').write_text(json.dumps(manifest,indent=2))
# Compact listening sequence: tree break, each element hit/break, four pickup materials.
audition=array.array('f')
for name in ['tree_break_01']+[f'crystal_{e}_{r}_01' for e in ['flame','nature','storm','earth','frost','blood','dark','light'] for r in ['hit','break']]+[f'collect_{m}_01' for m in ['wood','stone','crystal','soft']]:
    audition.extend(decode(ROOT/'candidates'/(name+'.wav'))); audition.extend(array.array('f',[0])*19200)
subprocess.run(['ffmpeg','-v','error','-y','-f','f32le','-ar','48000','-ac','1','-i','-','-c:a','pcm_s24le',str(ROOT/'audition.wav')],input=audition.tobytes(),check=True)
print(json.dumps({'candidates':len(manifest['candidates']),'sources':len(manifest['sources']),'maximum_peak_dbfs':max(x['peak_dbfs'] for x in manifest['candidates'])}))
