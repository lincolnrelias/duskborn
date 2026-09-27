// Reproducible source for the four painted smoke silhouettes and Unity asset wiring.
// Uses only Node built-ins; it never touches the furnace model or fresco atlas.
const fs=require('fs'), crypto=require('crypto'), zlib=require('zlib');
const root='Assets/_Duskborn/Effects/Furnace';
function guid(path){return crypto.createHash('md5').update('duskborn-furnace:'+path).digest('hex');}
function meta(path,body=''){if(!fs.existsSync(path+'.meta'))fs.writeFileSync(path+'.meta',`fileFormatVersion: 2\nguid: ${guid(path)}\n${body}`);}
meta(root,'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n');
for(const name of ['FurnaceEffects.cs','FurnacePaint.shader'])meta(root+'/'+name);
function crc(buf){let c=0xffffffff;for(const v of buf){c^=v;for(let k=0;k<8;k++)c=(c>>>1)^((c&1)?0xedb88320:0);}return (c^0xffffffff)>>>0;}
function chunk(type,data){const t=Buffer.from(type),out=Buffer.alloc(data.length+12);out.writeUInt32BE(data.length);t.copy(out,4);data.copy(out,8);out.writeUInt32BE(crc(Buffer.concat([t,data])),out.length-4);return out;}
const size=512, raw=Buffer.alloc((size*4+1)*size);
for(let y=0;y<size;y++)for(let x=0;x<size;x++){
 const frame=(x>=256?1:0)+(y>=256?2:0),u=(x%256)/255*2-1,v=(y%256)/255*2-1;
 const a=frame*1.91,dx=u+.13*Math.sin(v*4+a),dy=v+.10*Math.cos(u*3+a);
 const theta=Math.atan2(dy,dx),r=Math.sqrt(dx*dx+dy*dy);
 const boundary=.66+.10*Math.sin(theta*3+a)+.065*Math.cos(theta*5-a);
 const feather=Math.max(0,Math.min(1,(boundary-r)/.22));
 const alpha=feather*feather*(3-2*feather)*(.7+.18*Math.sin(u*5+v*3+a)+.12*Math.cos(v*7-u*2));
 const value=.83+.12*Math.sin(u*3-v*4+a);
 const p=y*(size*4+1)+1+x*4;
 raw[p]=raw[p+1]=raw[p+2]=Math.round(value*255);raw[p+3]=Math.round(alpha*255);
}
const ihdr=Buffer.alloc(13);ihdr.writeUInt32BE(size);ihdr.writeUInt32BE(size,4);ihdr[8]=8;ihdr[9]=6;
const texture=root+'/SmokePaint.png';
fs.writeFileSync(texture,Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',ihdr),chunk('IDAT',zlib.deflateSync(raw)),chunk('IEND',Buffer.alloc(0))]));
meta(texture,`TextureImporter:\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 1\n    sRGBTexture: 1\n  isReadable: 0\n  textureFormat: 1\n  maxTextureSize: 512\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 1\n    aniso: 1\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  textureType: 0\n  textureShape: 1\n  alphaSource: 1\n  alphaIsTransparency: 1\n`);
for(const [name,mode] of [['Fire',0],['Smoke',1],['Coals',2]]){
 const path=root+'/'+name+'.mat';
 fs.writeFileSync(path,`%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n  m_ObjectHideFlags: 0\n  m_Name: Furnace_${name}\n  m_Shader: {fileID: 4800000, guid: ${guid(root+'/FurnacePaint.shader')}, type: 3}\n  m_ValidKeywords: []\n  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 0\n  m_CustomRenderQueue: ${mode===2?2000:3000}\n  stringTagMap: {}\n  disabledShaderPasses: []\n  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs:\n    - _BaseMap:\n        m_Texture: {fileID: 2800000, guid: ${guid(texture)}, type: 3}\n        m_Scale: {x: 1, y: 1}\n        m_Offset: {x: 0, y: 0}\n    m_Ints: []\n    m_Floats:\n    - _Mode: ${mode}\n    - _Heat: 0\n    - _Phase: 0\n    - _SrcBlend: ${mode===2?1:5}\n    - _DstBlend: ${mode===2?0:10}\n    - _ZWrite: ${mode===2?1:0}\n    m_Colors:\n    - _Tint: {r: 1, g: 0.62, b: 0.18, a: 1}\n`); meta(path);
}
const prefab=root+'/FurnaceEffects.prefab';
let yaml='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n';
function obj(id,name,parent,pos,children,script=false){yaml+=`--- !u!1 &${id}\nGameObject:\n  m_ObjectHideFlags: 0\n  serializedVersion: 6\n  m_Component:\n  - component: {fileID: ${id+1}}\n${script?'  - component: {fileID: 11400000}\n':''}  m_Layer: 0\n  m_Name: ${name}\n  m_TagString: Untagged\n  m_IsActive: 1\n--- !u!4 &${id+1}\nTransform:\n  m_ObjectHideFlags: 0\n  m_GameObject: {fileID: ${id}}\n  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: ${pos[0]}, y: ${pos[1]}, z: ${pos[2]}}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_Children:${children.length?'\n'+children.map(x=>'  - {fileID: '+x+'}\n').join(''):' []\n'}  m_Father: {fileID: ${parent}}\n`;}
obj(100,'FurnaceEffects',0,[0,0,0],[201,301],true);obj(200,'FireAnchor',101,[0,.84,.26],[]);obj(300,'SmokeAnchor',101,[0,2.275,0],[]);
yaml+=`--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_GameObject: {fileID: 100}\n  m_Enabled: 1\n  m_Script: {fileID: 11500000, guid: ${guid(root+'/FurnaceEffects.cs')}, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n  fireMaterial: {fileID: 2100000, guid: ${guid(root+'/Fire.mat')}, type: 2}\n  smokeMaterial: {fileID: 2100000, guid: ${guid(root+'/Smoke.mat')}, type: 2}\n  coalMaterial: {fileID: 2100000, guid: ${guid(root+'/Coals.mat')}, type: 2}\n  fireAnchor: {fileID: 201}\n  smokeAnchor: {fileID: 301}\n  startupSeconds: 0.6\n  cooldownSeconds: 1.5\n  smokeDelay: 0.3\n  smokeDensity: 5\n  intensity: 1\n  flameTint: {r: 1, g: 0.62, b: 0.18, a: 1}\n  smokeTint: {r: 0.43, g: 0.40, b: 0.36, a: 0.24}\n  particleDistance: 40\n  lightDistance: 10\n  lightIntensity: 0.7\n`;
fs.writeFileSync(prefab,yaml);meta(prefab);
const def='Assets/_Duskborn/Resources/Building/Build_forge.asset';let d=fs.readFileSync(def,'utf8');
const ref=`  operatingEffect: {fileID: 11400000, guid: ${guid(prefab)}, type: 3}`;
d=d.includes('  operatingEffect:')?d.replace(/  operatingEffect:.*$/,ref):d.replace(/(  prefab:[^\r\n]*)/,'$1\n'+ref);fs.writeFileSync(def,d);
console.log('Wrote effect prefab, shared materials and four-frame smoke atlas.');
