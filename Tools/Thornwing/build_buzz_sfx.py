"""Recorded insect loops and stronger recorded chitin-collapse cues; no synthesis."""
import array, hashlib, importlib.util, json, math, shutil, struct, wave
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Artifacts/NaturalSfx/Thornwing/v002'
spec = importlib.util.spec_from_file_location('foley', ROOT/'Tools/Briarback/build_natural_sfx.py')
foley = importlib.util.module_from_spec(spec); spec.loader.exec_module(foley); foley.OUT = OUT
for folder in ['source','layers','candidates','unity']: (OUT/folder).mkdir(parents=True, exist_ok=True)
if (OUT/'manifest.json').exists(): raise FileExistsError('Use a fresh version for a new render')
manifest = {'version':'v002', 'sources':[], 'cues':[], 'perceptual_validation':'Not auditioned; naturalness, loop repetition and combat mix remain unverified.'}
source = OUT/'source/0759.mp3'
manifest['sources'].append({'file':'source/0759.mp3','sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
    'creator':'Joseph SARDIN','source_page':'https://bigsoundbank.com/fly-and-glass-s0759.html',
    'download_url':'https://bigsoundbank.com/UPLOAD/mp3/0759.mp3','license':'CC0',
    'license_url':'https://creativecommons.org/publicdomain/zero/1.0/','required_attribution':'None',
    'fidelity':'Public MP3, mono 48 kHz; WAV export does not recover lossy detail',
    'description':'Recorded big fly wing buzz, adapted into a wasp-like fantasy flight loop'})
pcm = array.array('f',foley.run(['ffmpeg','-v','error','-i',str(source),'-af',
    'highpass=f=120,lowpass=f=5000,asetrate=55200,aresample=48000','-f','f32le','-']))
rate=48000; duration=4.25; count=int(duration*rate); overlap=int(.25*rate)
# Favor sustained wing activity over isolated glass contacts; separate excerpts are distinct takes.
windows=[]
for start in range(1,int(len(pcm)/rate-duration)):
    segment=pcm[start*rate:start*rate+count]
    energies=[math.sqrt(sum(x*x for x in segment[i:i+4800])/4800) for i in range(0,count-4800,4800)]
    mean=sum(energies)/len(energies); variation=math.sqrt(sum((x-mean)**2 for x in energies)/len(energies))
    peak=max(abs(x) for x in segment)
    windows.append((mean/(1+variation/max(mean,1e-9)*5+peak/max(mean,1e-9)*.2),start))
selected=[]
for _,start in sorted(windows,reverse=True):
    if all(abs(start-other)>=5 for other in selected): selected.append(start)
    if len(selected)==2: break
for take,start in enumerate(selected,1):
    samples=pcm[start*rate:start*rate+count]
    # Circular overlap: tail flows into the next repetition's head, no fade to silence.
    loop=array.array('f', (samples[-overlap+i]*(1-i/overlap)+samples[i]*(i/overlap) for i in range(overlap)))
    loop.extend(samples[overlap:-overlap])
    gain=10**(-8/20)/max(abs(x) for x in loop)
    prepared=OUT/f'layers/Buzz_{take:02d}.f32'
    prepared.write_bytes(array.array('f',(x*gain for x in loop)).tobytes())
    path=OUT/f'candidates/TW_Buzz_{take:02d}.wav'
    stats=foley.measure(path) if path.exists() else foley.render(path,['-f','f32le','-ar','48000','-ac','1','-i',str(prepared)],
        {'brief':'Close, restless recorded wing buzz with circular crossfade, no synthetic oscillator',
         'source':'source/0759.mp3','processed_start_seconds':start,'source_rate_multiplier':1.15,
         'crossfade_seconds':.25,'peak_target_dbfs':-8})
    stats['loop_seam_jump']=abs(loop[0]-loop[-1])*gain
    manifest['cues'].append({'name':path.name,'inspection':stats,'sha256':foley.record(path)['sha256']})
    shutil.copyfile(path,OUT/'unity'/path.name)
library=ROOT/'Artifacts/NaturalSfx/Library'
catalog=json.loads((library/'sources.json').read_text())
for take in [1,2]:
    names=[('kenney_impact-sounds',f'impactPunch_medium_{take-1:03d}.ogg'),
           ('kenney_impact-sounds',f'impactSoft_medium_{take-1:03d}.ogg'),
           ('kenney_rpg-audio',f'cloth{take}.ogg')]
    args=[]
    for pack,name in names:
        src=library/pack/'Audio'/name; dst=OUT/'source'/name
        if not dst.exists():
            shutil.copyfile(src,dst); entry=next(p for p in catalog['packs'] if p['name']==pack)
            manifest['sources'].append({'file':'source/'+name,'sha256':foley.record(dst)['sha256'],
                'creator':entry['creator'],'source_page':entry['source_url'],'license':entry['license'],
                'license_url':entry['license_url'],'required_attribution':'None','fidelity':'Original lossy Ogg Vorbis'})
            for license_file in entry['license_files']: shutil.copyfile(library/license_file,OUT/'source'/(pack+'-License.txt'))
        args+=['-i',str(dst)]
    graph='[0:a]volume=-3dB[a];[1:a]volume=-7dB,adelay=90:all=1[b];[2:a]volume=-9dB,adelay=35:all=1[c];[a][b][c]amix=inputs=3:normalize=0,apad,atrim=duration=1.05,afade=t=in:d=0.003,afade=t=out:st=0.95:d=0.1[out]'
    path=OUT/f'candidates/TW_Death_{take:02d}.wav'
    stats=foley.render(path,args+['-filter_complex',graph,'-map','[out]'],
        {'brief':'Sharper dry chitin crack, body impact and leathery wing collapse','layers':names,'gains_db':[-3,-7,-9]})
    manifest['cues'].append({'name':path.name,'inspection':stats,'sha256':foley.record(path)['sha256']})
    shutil.copyfile(path,OUT/'unity'/path.name)
for cue in manifest['cues']:
    stats=cue['inspection']
    if stats['full_scale_samples'] or stats['nonfinite_samples'] or stats['peak_dbfs']>-1: raise RuntimeError(cue)
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2))
print(json.dumps(manifest['cues'],indent=2))

