// Read-only inspection of the imported FBX source, without opening/modifying Blender.
const fs = require('fs'), zlib = require('zlib');
const b = fs.readFileSync('Assets/_Duskborn/Art/Models/Forge/Fresco_Furnace.fbx');
const version = b.readUInt32LE(23); let pos = 27;
const wide = version >= 7500;
function uint() { const n = wide ? Number(b.readBigUInt64LE(pos)) : b.readUInt32LE(pos); pos += wide ? 8 : 4; return n; }
function prop() {
  const t = String.fromCharCode(b[pos++]);
  const scalar = {Y:[2,'readInt16LE'],C:[1,'readUInt8'],I:[4,'readInt32LE'],F:[4,'readFloatLE'],D:[8,'readDoubleLE']};
  if (scalar[t]) { const [n,f]=scalar[t], v=b[f](pos); pos+=n; return v; }
  if(t==='L') {const v=b.readBigInt64LE(pos).toString();pos+=8;return v;}
  if(t==='S'||t==='R') {const n=b.readUInt32LE(pos);pos+=4;const v=b.subarray(pos,pos+n);pos+=n;return t==='S'?v.toString():v;}
  const n=b.readUInt32LE(pos),enc=b.readUInt32LE(pos+4),len=b.readUInt32LE(pos+8);pos+=12;
  let data=b.subarray(pos,pos+len);pos+=len;if(enc)data=zlib.inflateSync(data);
  const type={d:[8,'readDoubleLE'],f:[4,'readFloatLE'],i:[4,'readInt32LE'],l:[8,'readBigInt64LE'],b:[1,'readUInt8'],c:[1,'readUInt8']}[t];
  if(!type)throw Error(t);return Array.from({length:n},(_,i)=>Number(data[type[1]](i*type[0])));
}
function node(){const end=uint(),count=uint();uint();const len=b[pos++];if(!end)return null;const name=b.toString('utf8',pos,pos+len);pos+=len;const p=Array.from({length:count},prop),children=[];while(pos<end){const c=node();if(!c)break;children.push(c);}pos=end;return {name,p,children};}
let roots=[];while(pos<b.length){const n=node();if(!n)break;roots.push(n);}
const objects=roots.find(n=>n.name==='Objects');
for(const o of objects.children){if(o.name==='Geometry'){const v=o.children.find(n=>n.name==='Vertices')?.p[0];if(v){const min=[Infinity,Infinity,Infinity],max=[-Infinity,-Infinity,-Infinity];v.forEach((x,i)=>{min[i%3]=Math.min(min[i%3],x);max[i%3]=Math.max(max[i%3],x);});console.log(o.name,o.p, {min,max});}}else if(o.name==='Model')console.log(o.name,o.p,JSON.stringify(o.children.filter(n=>n.name==='Properties70')));}
console.log(JSON.stringify(roots.find(n=>n.name==='GlobalSettings')));
