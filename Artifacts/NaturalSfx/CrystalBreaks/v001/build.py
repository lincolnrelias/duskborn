import array, json, math, shutil, wave
from pathlib import Path

shared=Path(__file__).resolve().parents[2]/'Harvesting/v002/build.py'
exec(shared.read_text().split('glass=pool(')[0])
manifest['brief']='Eight crystal destruction identities led by different physical sources, attack rhythms and decays. Flame: crackle burst; nature: splinter/leaf release; storm: thunder crack and short rumble; earth: dense rubble collapse; frost: tightly clustered brittle fractures; blood: wet rupture; dark: inward fabric rush and damped collapse; light: bright plate fracture and ringing debris. Four takes each, no synthesized tones/noise.'
old=PROJECT/'Artifacts/NaturalSfx/Harvesting/v002/source'
old_manifest=json.loads((old.parent/'manifest.json').read_text())
external={s['file']:s for s in old_manifest['sources'] if s['file'].endswith('.wav')}
for number in ['1299','1300','3322','1529']:
    shutil.copy2(old/(number+'.wav'),ROOT/'source'/(number+'.wav'))
    shutil.copy2(old/(number+'-page.html'),ROOT/'source'/(number+'-page.html'))
for number in ['3180','3181','3182','3183']:
    p=ROOT/'source'/(number+'.wav')
    with wave.open(str(p),'rb') as w:
        assert (w.getframerate(),w.getnchannels(),w.getsampwidth())==(48000,1,3)
    assert 'CC0' in (ROOT/'source'/(number+'-page.html')).read_text()
    external[p.name]={'file':p.name,'creator':'Joseph SARDIN & Axeline T.','license':'CC0-1.0','source_url':f'https://bigsoundbank.com/thunder-{int(number)-3173}-s{number}.html','license_url':'https://creativecommons.org/publicdomain/zero/1.0/','sha256':digest(p),'license_evidence':number+'-page.html','fidelity':'Original mono 48k 24bit WAV retained.'}
library_stage=stage
def stage(path):
    if path.name in external:
        receipt=external[path.name]
        assert digest(path)==receipt['sha256']
        if not any(s['file']==path.name for s in manifest['sources']): manifest['sources'].append(receipt)
        return path
    return library_stage(path)

glass=pool('impactGlass_light_*.ogg')+pool('impactGlass_medium_*.ogg')
wood=pool('impactWood_heavy_*.ogg')
mining=pool('impactMining_*.ogg')
grit=pool('footstep_concrete_*.ogg')
grass=pool('footstep_grass_*.ogg')
tin=pool('impactTin_medium_*.ogg')
plate=pool('impactPlate_light_*.ogg')
metal=pool('impactMetal_light_*.ogg')
soft=pool('impactSoft_heavy_*.ogg')
punch=pool('impactPunch_medium_*.ogg')
cloth=pool('cloth[1-4].ogg')
assert min(map(len,[glass,wood,mining,grit,grass,tin,plate,metal,soft,punch,cloth]))>=4
fire=ROOT/'source/3322.wav'
water=ROOT/'source/1529.wav'
twigs=[ROOT/'source/1299.wav',ROOT/'source/1300.wav']
thunder=[ROOT/'source'/f'{number}.wav' for number in ['3180','3181','3182','3183']]
def reverse_layer(path,delay=0,**kw):
    values,receipt=segment(path,**kw)
    values.reverse(); receipt['reverse']=True
    # A reverse rustle leads into a physical collapse, rather than a generated sub boom.
    return values,receipt,delay
def loud_event(path,window=.7,search_start=0):
    values=decode(path); block=1200
    energy=[sum(v*v for v in values[o:o+block]) for o in range(int(search_start*48000),len(values)-block,block)]
    best=max(range(len(energy)),key=energy.__getitem__)
    threshold=energy[best]*.28
    attack=best
    while attack>0 and energy[attack-1]>threshold: attack-=1
    return max(search_start,search_start+attack*.025-.012)
elements=['flame','nature','storm','earth','frost','blood','dark','light']
designs={}
for element in elements:
    for i in range(4):
        if element=='flame':
            layers=[layer(fire,seconds=.68,start=2+i*4,gain=0),layer(wood[i],seconds=.18,gain=-14),layer(fire,seconds=.24,start=18+i*3,gain=-5,delay=.13)]
            identity='Abrupt fire crackle with a short sizzling release; woody pop tucked underneath.'
        elif element=='nature':
            twig=twigs[i%2]
            layers=[layer(twig,seconds=.42,start=None if i<2 else .65,gain=-1),layer(wood[i],seconds=.28,gain=-6,delay=.012),layer(grass[i],seconds=.48,gain=-3,delay=.12)]
            identity='Dry plant snap followed by a loose leafy scattering; uneven organic decay.'
        elif element=='storm':
            start=loud_event(thunder[i])
            layers=[layer(thunder[i],seconds=.95,start=start,gain=0),layer(tin[i],seconds=.13,gain=-8,delay=.006),layer(metal[i],seconds=.12,gain=-15,delay=.047)]
            identity='Compact thunder crack with a rolling tail and two tiny metallic crackles.'
        elif element=='earth':
            layers=[layer(mining[i],seconds=.3,gain=0,rate=.86),layer(grit[i],seconds=.47,gain=-4,delay=.09),layer(mining[(i+1)%len(mining)],seconds=.25,gain=-10,rate=.9,delay=.22)]
            identity='Dense rock fracture followed by staggered gravel chunks falling.'
        elif element=='frost':
            layers=[layer(glass[i],seconds=.22,gain=-1),layer(glass[(i+2)%len(glass)],seconds=.16,gain=-5,delay=.032),layer(glass[(i+4)%len(glass)],seconds=.2,gain=-9,delay=.075)]
            identity='Fast brittle fractures and small high glass chips; short, dry stop.'
        elif element=='blood':
            layers=[layer(water,seconds=.56,start=loud_event(water)+i*.022,gain=0,rate=.92),layer(punch[i],seconds=.19,gain=-8,lowpass=2500),layer(water,seconds=.22,start=.9+i*.06,gain=-5,delay=.17)]
            identity='Wet splitting splash and fleshy contact, with a few thick drops after.'
        elif element=='dark':
            layers=[reverse_layer(cloth[i],seconds=.2,gain=-2,lowpass=3200),layer(soft[i],seconds=.3,gain=0,rate=.8,lowpass=1800,delay=.14),layer(cloth[(i+1)%4],seconds=.3,gain=-8,lowpass=2200,delay=.2)]
            identity='Short inward rush into a muffled collapse, with a dark rustling tail.'
        else:
            layers=[layer(plate[i],seconds=.64,gain=0),layer(metal[i],seconds=.35,gain=-9,delay=.035),layer(plate[(i+1)%len(plate)],seconds=.38,gain=-10,delay=.16)]
            identity='Clear bright fracture with staggered ringing fragments and a longer decay.'
        out=make(f'crystal_{element}_break_{i+1:02d}',layers,peak_db=-1.5,max_gain_db=8)
        manifest['candidates'][-1]['element']=element
        manifest['candidates'][-1]['audible_design']=identity
        designs[element]=identity
manifest['designs']=designs
(ROOT/'manifest.json').write_text(json.dumps(manifest,indent=2))
audition=array.array('f')
for element in elements:
    for i in range(2):
        data=decode(ROOT/'candidates'/f'crystal_{element}_break_{i+1:02d}.wav')
        audition.extend(data); audition.extend(array.array('f',[0])*19200)
    audition.extend(array.array('f',[0])*24000)
subprocess.run(['ffmpeg','-v','error','-y','-f','f32le','-ar','48000','-ac','1','-i','-','-c:a','pcm_s24le',str(ROOT/'audition.wav')],input=audition.tobytes(),check=True)
print(json.dumps({'masters':len(manifest['candidates']),'max_peak_dbfs':max(c['peak_dbfs'] for c in manifest['candidates']),'duration_range':[min(c['duration'] for c in manifest['candidates']),max(c['duration'] for c in manifest['candidates'])]}))
