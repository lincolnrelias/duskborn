"""Free CC0 source-based insect foley. No oscillators or synthesized noise."""
import sys, json, shutil, importlib.util, subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('foley',ROOT/'Tools/Briarback/build_natural_sfx.py')
foley=importlib.util.module_from_spec(spec); spec.loader.exec_module(foley)
OUT=ROOT/'Artifacts/NaturalSfx/Thornwing/v001'; OUT.mkdir(parents=True,exist_ok=True)
foley.OUT=OUT
for name in ['source','candidates','unity']: (OUT/name).mkdir(exist_ok=True)
library=ROOT/'Artifacts/NaturalSfx/Library'
sources=json.loads((library/'sources.json').read_text())
manifest={'enemy':'Thornwing','version':'v001','sources':[],'cues':[],
          'perceptual_validation':'Not auditioned; user must check naturalness, repetition and combat mix.'}
used={}
def source(pack,name):
    src=library/pack/'Audio'/name
    dst=OUT/'source'/name
    if name not in used:
        shutil.copyfile(src,dst)
        entry=next(p for p in sources['packs'] if p['name']==pack)
        manifest['sources'].append(dict(file=str(dst.relative_to(OUT)),sha256=foley.record(dst)['sha256'],
            creator=entry['creator'],source_page=entry['source_url'],license=entry['license'],
            license_url=entry['license_url'],required_attribution='None',
            fidelity='Original lossy Ogg Vorbis; 48 kHz PCM export does not restore lost information.'))
        for license_file in entry['license_files']:
            target=OUT/'source'/(pack+'-License.txt')
            if not target.exists(): shutil.copyfile(library/license_file,target)
        used[name]=dst
    return dst

# Separate recorded cloth/leather takes provide flutter and chitin-like exertion.
recipes=[('Windup',.58,'Tight dry membrane tension, a short leather rasp before spitting.',
          [('handleSmallLeather.ogg',0),('handleSmallLeather2.ogg',0)]),
         ('Spit',.28,'Small sharp organic contact and a brief papery flick; a weak thorn spit.',
          [('impactSoft_medium_000.ogg',0),('impactSoft_medium_001.ogg',0)]),
         ('Hurt',.32,'Brief chitin contact with a soft wing crumple.',
          [('impactPunch_medium_000.ogg',0),('impactPunch_medium_001.ogg',0)]),
         ('Death',.85,'Small soft body landing with dry membranes folding.',
          [('impactSoft_medium_000.ogg',0),('impactSoft_medium_001.ogg',0)]),
         ('Flutter',.38,'Quiet flutter of thin leathery wings from separate cloth performances.',
          [('cloth1.ogg',0),('cloth2.ogg',0),('cloth3.ogg',0),('cloth4.ogg',0)])]
for cue,duration,brief,takes in recipes:
    for i,(name,start) in enumerate(takes,1):
        pack='kenney_impact-sounds' if name.startswith('impact') else 'kenney_rpg-audio'
        primary=source(pack,name)
        if cue in ['Hurt','Death','Spit']: cloth=source('kenney_rpg-audio',f'cloth{i}.ogg')
        filename=f'TW_{cue}_{i:02d}.wav'; output=OUT/'candidates'/filename
        if output.exists():
            stats=foley.measure(output)
        else:
            args=['-i',str(primary)]
            graph=f'[0:a]atrim=start={start}:duration={duration},asetpts=PTS-STARTPTS,volume=-9dB[a]'
            if cue in ['Hurt','Death','Spit']:
                args+=['-i',str(cloth)]
                graph+=f';[1:a]atrim=duration={duration},asetpts=PTS-STARTPTS,volume=-19dB[b];[a][b]amix=inputs=2:normalize=0:duration=longest[m]'
                tag='m'
            else: tag='a'
            graph+=f';[{tag}]apad,atrim=duration={duration},afade=t=in:d=0.004,afade=t=out:st={duration-.04}:d=0.04[out]'
            stats=foley.render(output,args+['-filter_complex',graph,'-map','[out]'],{'brief':brief,'source_file':name,'take':i})
        if stats['full_scale_samples'] or stats['nonfinite_samples']: raise RuntimeError('Invalid audio '+filename)
        shutil.copyfile(output,OUT/'unity'/filename)
        manifest['cues'].append(dict(name=filename,brief=brief,inspection=stats,sha256=foley.record(output)['sha256']))
parts=[]; offset=0
for cue in manifest['cues']:
    parts.append((OUT/'candidates'/cue['name'],round(offset*1000),0))
    offset+=cue['inspection']['duration_seconds']+.35
audition=OUT/'candidates/Thornwing_Audition.wav'
if not audition.exists():
    args=[]; filters=[]
    for i,(path,delay,gain) in enumerate(parts):
        args+=['-i',str(path)];filters.append(f'[{i}:a]adelay={delay}|{delay}[a{i}]')
    filters.append(''.join(f'[a{i}]' for i in range(len(parts)))+f'amix=inputs={len(parts)}:normalize=0[out]')
    foley.render(audition,args+['-filter_complex',';'.join(filters),'-map','[out]'],{'brief':'All cue takes with silence, same mix gain as installed assets.'})
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2))
print(json.dumps({'cues':len(manifest['cues']),'max_peak_dbfs':max(c['inspection']['peak_dbfs'] for c in manifest['cues']),
                  'audition':str(audition)},indent=2))
