"""Free recorded-source goblin voices and wooden club foley; never overwrite masters."""
from pathlib import Path
import subprocess, json, hashlib, array, math, shutil, wave
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Artifacts/NaturalSfx/Bramblekin/v002'
SOURCE=OUT/'source'; MASTERS=OUT/'masters'; UNITY=OUT/'unity'
for folder in (SOURCE,MASTERS,UNITY): folder.mkdir(parents=True,exist_ok=True)
LIB=ROOT/'Artifacts/NaturalSfx/Library'
def run(args): return subprocess.run(args,check=True,capture_output=True).stdout
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def samples(p):
    data=array.array('f'); data.frombytes(run(['ffmpeg','-v','error','-i',str(p),'-ar','48000','-ac','1','-f','f32le','-']))
    return data
def inspect(p):
    x=samples(p); peak=max(abs(v) for v in x)
    audible=[i for i,v in enumerate(x) if abs(v)>.003]
    return {'duration':len(x)/48000,'sample_rate':48000,'channels':1,'peak_dbfs':20*math.log10(max(peak,1e-10)),
            'rms_dbfs':20*math.log10(max(math.sqrt(sum(v*v for v in x)/len(x)),1e-10)),
            'clipped_samples':sum(abs(v)>=0.99999 for v in x),'onset_seconds':audible[0]/48000 if audible else None,
            'tail_silence_seconds':(len(x)-audible[-1])/48000 if audible else None}
sources=[]; receipts=[]
def stage(p,pack):
    dest=SOURCE/p.name
    if not dest.exists(): shutil.copyfile(p,dest)
    if not any(s['file']==dest.name for s in sources):
        sources.append({'file':dest.name,'sha256':sha(dest),'pack':pack})
    return dest
def export(name,inputs,filters):
    dest=MASTERS/(name+'.wav')
    if dest.exists(): raise RuntimeError('Keep previous master: '+str(dest))
    args=['ffmpeg','-v','error','-n']
    for p in inputs: args+=['-i',str(p)]
    args+=['-filter_complex',filters,'-map','[out]','-ar','48000','-ac','1','-c:a','pcm_s24le',str(dest)]
    run(args); stats=inspect(dest)
    assert stats['clipped_samples']==0 and stats['onset_seconds']<.05,(name,stats)
    shutil.copyfile(dest,UNITY/dest.name)
    receipt={'cue':name,'sources':[{'file':p.name,'sha256':sha(p)} for p in inputs],
             'filter_graph':filters,'output_sha256':sha(dest),'inspection':stats,'format':'48 kHz mono PCM 24-bit WAV'}
    (MASTERS/(name+'.receipt.json')).write_text(json.dumps(receipt,indent=2)); receipts.append(receipt)
voices={'Windup':[1,2,5,6],'Hurt':[7,8,10,11],'Death':[3,4,12,15]}
for cue,takes in voices.items():
    for take,number in enumerate(takes,1):
        src=stage(SOURCE/f'goblins/goblins/goblin-{number}.wav','artisticdude-goblins')
        x=samples(src); active=[i for i,v in enumerate(x) if abs(v)>.0015]
        start=max(0,active[0]/48000-.005); end=min(len(x)/48000,active[-1]/48000+.035)
        duration=end-start; gain=min(-2,-7-20*math.log10(max(abs(v) for v in x)))
        filters=f'[0:a]atrim=start={start}:end={end},asetpts=PTS-STARTPTS,highpass=f=90,lowpass=f=9000,volume={gain}dB,afade=t=in:d=0.002,afade=t=out:st={max(0,duration-.006)}:d=0.006[out]'
        export(f'BK_{cue}_{take:02}',[src],filters)
for take in range(1,5):
    cloth=stage(LIB/f'kenney_rpg-audio/Audio/cloth{take}.ogg','kenney-rpg-audio')
    export(f'BK_Swing_{take:02}',[cloth],'[0:a]atempo=1.7,atrim=duration=0.16,asetpts=PTS-STARTPTS,highpass=f=140,volume=-5dB,afade=t=in:d=0.002,afade=t=out:st=0.14:d=0.02[out]')
    wood=stage(LIB/f'kenney_impact-sounds/Audio/impactWood_medium_{take-1:03}.ogg','kenney-impact-sounds')
    contact=stage(LIB/f'kenney_impact-sounds/Audio/impactPunch_medium_{take-1:03}.ogg','kenney-impact-sounds')
    export(f'BK_Hit_{take:02}',[wood,contact],
           '[0:a]volume=-6dB[w];[1:a]volume=-11dB,adelay=7:all=1[b];[w][b]amix=inputs=2:normalize=0,atrim=duration=0.30,afade=t=out:st=0.26:d=0.04[out]')
    step=stage(LIB/f'kenney_impact-sounds/Audio/footstep_grass_{take-1:03}.ogg','kenney-impact-sounds')
    export(f'BK_Step_{take:02}',[step],'[0:a]volume=-7dB,afade=t=in:d=0.002[out]')
packs=[{'id':'artisticdude-goblins','creator':'artisticdude','source_url':'https://opengameart.org/content/goblins-sound-pack',
        'download_url':'https://opengameart.org/sites/default/files/goblins_0.zip','archive_sha256':sha(SOURCE/'goblins_0.zip'),
        'license':'CC0-1.0','license_url':'https://creativecommons.org/publicdomain/zero/1.0/','date':'2026-10-03',
        'source_fidelity':'Original 44.1 kHz stereo PCM 24-bit vocal performances; no pitch shift; role selection awaits listening.'},
       {'id':'kenney-impact-sounds','creator':'Kenney','source_url':'https://kenney.nl/assets/impact-sounds','license':'CC0-1.0'},
       {'id':'kenney-rpg-audio','creator':'Kenney','source_url':'https://kenney.nl/assets/rpg-audio','license':'CC0-1.0'}]
for pack in ('kenney_impact-sounds','kenney_rpg-audio'):
    shutil.copyfile(LIB/pack/'License.txt',SOURCE/(pack+'-License.txt'))
(SOURCE/'LICENSE-Goblins.txt').write_text('Goblins Sound Pack by artisticdude\nhttps://opengameart.org/content/goblins-sound-pack\nCC0-1.0, verified on the original asset page 2026-10-03.\nhttps://creativecommons.org/publicdomain/zero/1.0/\nAttribution is not required; credit retained for provenance.\n')
manifest={'brief':'Small woodland goblin: short squeaky effort, raspy hurt bursts, longer dying vocal releases; dry wooden club contact with soft body, sleeve swish and grass steps.',
          'packs':packs,'sources':sources,'takes':receipts,'perceptual_validation':'Not auditioned by the agent; check role selection, naturalness and in-game mix.'}
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2))
with wave.open(str(OUT/'audition.wav'),'wb') as audition:
    audition.setnchannels(1); audition.setsampwidth(2); audition.setframerate(48000)
    for cue in ('Windup','Swing','Hit','Hurt','Death','Step'):
        for take in range(1,5):
            audition.writeframes(run(['ffmpeg','-v','error','-i',str(MASTERS/f'BK_{cue}_{take:02}.wav'),'-ar','48000','-ac','1','-f','s16le','-']))
            audition.writeframes(bytes(48000*2//3))
print(json.dumps({'takes':len(receipts),'clipped_samples':sum(r['inspection']['clipped_samples'] for r in receipts),'output':str(OUT)}))
