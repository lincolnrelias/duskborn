"""Original Duskborn low-poly wooden bow/arrow geometry and deterministic Unity wiring.
No downloaded models, paid providers, Blender session, or Unity process required.
Run from the repository root with Python 3. Standard library only.
"""
from pathlib import Path
import hashlib, math, re, struct, json

ROOT = Path(__file__).resolve().parents[2]
ART = 'Assets/_Duskborn/Art/Models/WoodenBow'
DATA = 'Assets/_Duskborn/Resources/Weapons'
CODE = 'Assets/_Duskborn/Gameplay'
HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'

def guid(path):
    meta = ROOT / (path + '.meta')
    if meta.exists(): return re.search(r'^guid: (\w+)', meta.read_text(), re.M)[1]
    return hashlib.md5(('duskborn-ranged:' + path).encode()).hexdigest()

def write(path, text, importer=''):
    p = ROOT / path
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding='utf-8')
    meta = Path(str(p) + '.meta')
    if not meta.exists(): meta.write_text(f'fileFormatVersion: 2\nguid: {guid(path)}\n'+importer)
    for parent in p.parents:
        if parent == ROOT / 'Assets' or not parent.is_relative_to(ROOT / 'Assets'): break
        rel = parent.relative_to(ROOT).as_posix()
        m = Path(str(parent) + '.meta')
        if not m.exists(): m.write_text(f'fileFormatVersion: 2\nguid: {guid(rel)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n')

def ref(path, fid=11400000, kind=2): return f'{{fileID: {fid}, guid: {guid(path)}, type: {kind}}}'
def vec(v): return '{'+', '.join(f'{k}: {x:.8g}' for k,x in zip('xyz',v))+'}'
def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def norm(a):
    d = math.sqrt(sum(x*x for x in a))
    return tuple(x/d for x in a)

parts = {}
def tube(name, points, radii, sides=6):
    triangles = parts.setdefault(name, [])
    rings=[]
    for i,(p,r) in enumerate(zip(points,radii)):
        tangent=norm(sub(points[min(i+1,len(points)-1)],points[max(i-1,0)]))
        u=norm(cross(tangent,(1,0,0) if abs(tangent[0]) < .9 else (0,1,0)))
        v=cross(tangent,u)
        rings.append([tuple(p[k]+r*(u[k]*math.cos(a*2*math.pi/sides)+v[k]*math.sin(a*2*math.pi/sides)) for k in range(3)) for a in range(sides)])
    for i in range(len(rings)-1):
        for j in range(sides):
            a,b,c,d=rings[i][j],rings[i][(j+1)%sides],rings[i+1][(j+1)%sides],rings[i+1][j]
            triangles += [(a,b,c),(a,c,d)]
    for j in range(1,sides-1):
        triangles += [(rings[0][0],rings[0][j+1],rings[0][j]),(rings[-1][0],rings[-1][j],rings[-1][j+1])]

points=[]; radii=[]
for i in range(25):
    y=-.68+i*1.36/24
    points.append((0,y,-.22*(abs(y)/.68)**1.65))
    radii.append(.027-.014*(abs(y)/.68))
tube('BowWood',points,radii,8)
tube('BowGrip',[(0,-.105,0),(0,.105,0)],[.033,.033],8)
for i in range(10):
    y=-.10+i*.021
    tube('BowBinding',[(0,y,0),(0,y+.006,0)],[.036,.036],8)
tube('BowCord',[(0,-.68,-.22),(0,0,-.22),(0,.68,-.22)],[.0025]*3,4)
tube('ArrowShaft',[(0,0,-.79),(0,0,-.09)],[.008,.008],6)
tube('ArrowHead',[(0,0,-.11),(0,0,-.065),(0,0,0)],[.009,.026,.0005],4)
tube('ArrowNock',[(0,0,-.81),(0,0,-.775)],[.010,.010],6)
for i in range(3):
    a=i*2*math.pi/3
    def f(r,z): return (math.cos(a)*r, math.sin(a)*r, z)
    vertices=[f(.007,-.765),f(.043,-.735),f(.034,-.63),f(.007,-.60)]
    parts.setdefault('ArrowFeathers',[]).extend([(vertices[0],vertices[1],vertices[2]),(vertices[0],vertices[2],vertices[3]),(vertices[2],vertices[1],vertices[0]),(vertices[3],vertices[2],vertices[0])])

template=(ROOT/'Assets/_Duskborn/Art/Models/HollowWarden/RootCluster.asset').read_text()
for name, triangles in parts.items():
    verts=[p for t in triangles for p in t]
    normals=[norm(cross(sub(t[1],t[0]),sub(t[2],t[0]))) for t in triangles]
    data=b''.join(struct.pack('<6f',*p,*n) for t,n in zip(triangles,normals) for p in t)
    minimum=[min(p[k] for p in verts) for k in range(3)]
    maximum=[max(p[k] for p in verts) for k in range(3)]
    center=[(a+b)/2 for a,b in zip(minimum,maximum)]
    extent=[(b-a)/2 for a,b in zip(minimum,maximum)]
    text=template.replace('m_Name: RootCluster',f'm_Name: {name}')
    text=re.sub(r'(indexCount|vertexCount|m_VertexCount): \d+',lambda m:f'{m[1]}: {len(verts)}',text)
    text=re.sub(r'm_IndexBuffer: [0-9a-f]+','m_IndexBuffer: '+struct.pack('<'+'H'*len(verts),*range(len(verts))).hex(),text)
    text=re.sub(r'm_DataSize: \d+',f'm_DataSize: {len(data)}',text)
    text=re.sub(r'_typelessdata: [0-9a-f]+','_typelessdata: '+data.hex(),text)
    text=re.sub(r'm_Center: \{[^}]+\}','m_Center: '+vec(center),text)
    text=re.sub(r'm_Extent: \{[^}]+\}','m_Extent: '+vec(extent),text)
    write(f'{ART}/{name}.asset',text,'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 4300000\n')

colors={'Wood':(.42,.21,.085),'Leather':(.15,.075,.04),'Binding':(.27,.145,.07),'Cord':(.77,.65,.42),'Iron':(.24,.29,.32),'Feather':(.8,.71,.49)}
wood=(ROOT/'Assets/_Duskborn/Art/Models/Wood.mat').read_text()
for name,color in colors.items():
    text=re.sub(r'm_Name: .*','m_Name: Bow_'+name,wood,count=1)
    text=re.sub(r'(_BaseColor|_Color): \{[^}]+\}',lambda m:m[1]+f': {{r: {color[0]}, g: {color[1]}, b: {color[2]}, a: 1}}',text)
    write(f'{ART}/{name}.mat',text)

def prefab(name, mesh_names, mats):
    text=HEADER
    for i,n in enumerate([name]+mesh_names):
        fid=100+i*10; parent=0 if i==0 else 101
        comps=[fid+1] if i==0 else [fid+1,fid+2,fid+3]
        text+=f'--- !u!1 &{fid}\nGameObject:\n  m_ObjectHideFlags: 0\n  serializedVersion: 6\n  m_Component:\n'+''.join(f'  - component: {{fileID: {x}}}\n' for x in comps)+f'  m_Layer: 0\n  m_Name: {n}\n  m_TagString: Untagged\n  m_IsActive: 1\n'
        children=' []\n' if i else '\n'+''.join(f'  - {{fileID: {111+j*10}}}\n' for j in range(len(mesh_names)))
        text+=f'--- !u!4 &{fid+1}\nTransform:\n  m_ObjectHideFlags: 0\n  m_GameObject: {{fileID: {fid}}}\n  serializedVersion: 2\n  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n  m_LocalPosition: {{x: 0, y: 0, z: 0}}\n  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_Children:{children}  m_Father: {{fileID: {parent}}}\n'
        if not i: continue
        text+=f'--- !u!33 &{fid+2}\nMeshFilter:\n  m_ObjectHideFlags: 0\n  m_GameObject: {{fileID: {fid}}}\n  m_Mesh: {ref(ART+"/"+n+".asset",4300000)}\n'
        text+=f'--- !u!23 &{fid+3}\nMeshRenderer:\n  m_ObjectHideFlags: 0\n  m_GameObject: {{fileID: {fid}}}\n  m_Enabled: 1\n  m_CastShadows: 1\n  m_ReceiveShadows: 1\n  m_RenderingLayerMask: 1\n  m_Materials:\n  - {ref(ART+"/"+mats[i-1]+".mat",2100000)}\n  m_SortingLayerID: 0\n  m_SortingOrder: 0\n'
    write(f'{ART}/{name}.prefab',text)

prefab('WoodenBow',['BowWood','BowGrip','BowBinding','BowCord'],['Wood','Leather','Binding','Cord'])
prefab('WoodenArrow',['ArrowShaft','ArrowHead','ArrowNock','ArrowFeathers'],['Wood','Iron','Binding','Feather'])
p=ROOT/ART/'WoodenBow.prefab'
text=p.read_text().replace('  - component: {fileID: 101}', '  - component: {fileID: 101}\n  - component: {fileID: 11400000}',1)
text+=f'''--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_GameObject: {{fileID: 100}}
  m_Enabled: 1
  m_Script: {ref(CODE+'/Equipment/BowPresentation.cs',11500000,3)}
  m_EditorClassIdentifier:
  arrowPrefab: {ref(ART+'/WoodenArrow.prefab',100,3)}
  cordMaterial: {ref(ART+'/Cord.mat',2100000)}
'''
p.write_text(text)

def asset(name,script,fields):
    path=f'{DATA}/{name}.asset'
    text=HEADER+f'--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_GameObject: {{fileID: 0}}\n  m_Enabled: 1\n  m_Script: {ref(script,11500000,3)}\n  m_Name: {name}\n  m_EditorClassIdentifier: \n'+fields
    write(path,text,'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n')
    return path

impact=asset('ArrowEmbed',CODE+'/Projectiles/ArrowImpactBehaviour.cs','')
projectile=asset('WoodenArrow',CODE+'/Projectiles/ProjectileDefinition.cs',f'  visualPrefab: {ref(ART+"/WoodenArrow.prefab",100,3)}\n  impact: {ref(impact)}\n  speed: 28\n  gravityScale: 1\n  radius: 0.025\n  lifetime: 8\n  embeddedLifetime: 15\n  damageMultiplier: 1\n  impactImpulse: 1\n  collisionMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n')
behaviour=asset('WoodenBowRanged',CODE+'/Equipment/RangedWeaponBehaviour.cs',f'  projectile: {ref(projectile)}\n  preferredRange: 15\n')
attachment=asset('WoodenBowLeftHand',CODE+'/Equipment/ItemAttachmentProfile.cs','  bone: 17\n  positionOffset: {x: 0, y: 0.025, z: 0}\n  rotationOffset: {x: 0, y: 90, z: 90}\n  scale: {x: 1, y: 1, z: 1}\n')
clip='Assets/ThirdPartyAssets/Kevin Iglesias/Archer Animations/Animations/Combat/Archer@BowShot01.fbx'
bow=asset('weapon_wooden_bow',CODE+'/Equipment/WeaponDefinition.cs',f'''  id: weapon_wooden_bow
  displayName: Wooden Bow
  description: Training bow. Fires physical arrows; unlimited ammunition in this test version.
  icon: {ref('Assets/Inventory/Textures/Weapons & Tools/Bow.png',2800000,3)}
  dropPrefab: {{fileID: 0}}
  rarity: 0
  prefab: {ref(ART+'/WoodenBow.prefab',100,3)}
  bonuses: []
  typeModifiers: []
  behaviour: {ref(behaviour)}
  actions:
  - BowAnimations: {ref(DATA+'/WoodenBowAnimations.asset')}
    BaseSpeed: 1
    PreserveLocomotion: 1
    ComboChain: 0
    ComboResetTime: 0.8
    Entries:
    - Clip: {ref(clip,7400000,3)}
      DamageMultiplier: 1
      Events:
      - Type: 2
        NormalizedTime: 0.67418647
    Clips: []
    Events: []
    ComboDamageMultipliers: []
  skills: []
  audioProfile: {{fileID: 0}}
  effectProfile: {{fileID: 0}}
  attachmentProfile: {ref(attachment)}
  actionMask: {ref('Assets/_Duskborn/Prefabs/Player/BowMask.mask', 31900000)}
''')
# Idempotently expose the test bow without replacing any of the user's inventory.
p=ROOT/'Assets/_Duskborn/Resources/Inventory/InitialInventoryDatabase.asset'
text=p.read_text()
if guid(bow) not in text: text=text.replace('  startingGear:',f'  - item: {ref(bow)}\n    quantity: 1\n  startingGear:')
p.write_text(text)
p=ROOT/'Assets/_Duskborn/Resources/Crafting/Recipe_SimpleBow.asset'
p.write_text(re.sub(r'  outputItem: .*',f'  outputItem: {ref(bow)}',p.read_text()))
# A separate archer test prefab; normal Swarmer waves remain as configured.
path='Assets/_Duskborn/Prefabs/Enemies/ArcherTest.prefab'
text=(ROOT/'Assets/_Duskborn/Prefabs/Enemies/ArcherTest.prefab').read_text()
text=re.sub(r'  weapon: .*',f'  weapon: {ref(bow)}',text)
text=re.sub(r'  <AssetPathHash>k__BackingField: \d+','  <AssetPathHash>k__BackingField: '+str(int(guid(path)[:15],16)),text)
write(path,text)
for path in ['Assets/DefaultPrefabObjects.asset','Assets/_Duskborn/Network/DefaultPrefabObjects.asset']:
    p=ROOT/path; text=p.read_text(); entry=ref('Assets/_Duskborn/Prefabs/Enemies/ArcherTest.prefab',388004312244409988,3)
    if entry not in text: p.write_text(text.rstrip()+'\n  - '+entry+'\n')
for directory in ['Projectiles','Equipment','Enemies','Player']:
    for p in (ROOT/CODE/directory).glob('*.cs'):
        rel=p.relative_to(ROOT).as_posix()
        if not Path(str(p)+'.meta').exists(): write(rel,p.read_text())
test_path='Assets/_Duskborn/Editor/RangedCombatTests.cs'
if not (ROOT/(test_path+'.meta')).exists(): write(test_path,(ROOT/test_path).read_text())
preview_dir=ROOT/'Temp/RangedCombatValidation'
preview_dir.mkdir(parents=True,exist_ok=True)
(preview_dir/'geometry.json').write_text(json.dumps({'parts':parts,'colors':colors,'provenance':'Original procedural geometry authored for Duskborn; no external model inputs.'}))
print('Created bow and arrow meshes, materials, prefabs, projectile/weapon assets, starter and recipe wiring, and ArcherTest prefab.')
print('Triangles:', {k:len(v) for k,v in parts.items()})
