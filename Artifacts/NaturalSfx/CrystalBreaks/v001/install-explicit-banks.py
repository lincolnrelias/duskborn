import hashlib,json,re,shutil,uuid
from pathlib import Path
root=Path(__file__).resolve().parent
project=root.parents[3]
dest=project/'Assets/_Duskborn/Resources/SFX/CrystalBreaksV2'
dest.mkdir(exist_ok=True)
foldermeta=dest.with_suffix('.meta')
if not foldermeta.exists():
    foldermeta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
template=(project/'Assets/_Duskborn/Resources/SFX/Harvesting/harvest_wood_01.wav.meta').read_text()
refs=[]; receipt=[]
for element in ['flame','nature','storm','earth','frost','blood','dark','light']:
    refs.extend(['    - element: '+element,'      clips:'])
    for i in range(4):
        src=root/'candidates'/f'crystal_{element}_break_{i+1:02d}.wav'
        out=dest/f'crystal_{element}_break_live_v2_{i+1:02d}.wav'
        shutil.copy2(src,out)
        meta=Path(str(out)+'.meta')
        if not meta.exists(): meta.write_text(re.sub(r'guid: \w+','guid: '+uuid.uuid4().hex,template))
        guid=re.search(r'guid: (\w+)',meta.read_text())[1]
        refs.append('      - {fileID: 8300000, guid: '+guid+', type: 3}')
        receipt.append({'element':element,'source':str(src.relative_to(project)),'installed':str(out.relative_to(project)),'guid':guid,'sha256':hashlib.sha256(out.read_bytes()).hexdigest()})
db=project/'Assets/_Duskborn/Resources/Audio/AudioDatabase.asset'
text=db.read_text()
text=re.sub(r'    crystalBreakBanks:\n.*?(?=    woodHarvestClips:)', '',text,flags=re.S)
text=text.replace('  resources:\n','  resources:\n    crystalBreakBanks:\n'+'\n'.join(refs)+'\n',1)
db.write_text(text)
(root/'installed-banks.json').write_text(json.dumps(receipt,indent=2))
print('Installed 8 explicit crystal banks with 32 fresh clip identities.')
