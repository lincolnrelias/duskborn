"""Offline software preview of the exact generated mesh triangles; not a Unity render."""
from pathlib import Path
import json, math
from PIL import Image, ImageDraw, ImageFont

root=Path(__file__).resolve().parents[2]
data=json.loads((root/'Temp/RangedCombatValidation/geometry.json').read_text())
scale=2
im=Image.new('RGB',(1100*scale,780*scale),(27,31,34))
draw=ImageDraw.Draw(im)
mapping={'BowWood':'Wood','BowGrip':'Leather','BowBinding':'Binding','BowCord':'Cord','ArrowShaft':'Wood','ArrowHead':'Iron','ArrowNock':'Binding','ArrowFeathers':'Feather'}
faces=[]
for name, triangles in data['parts'].items():
    bow=name.startswith('Bow')
    for tri in triangles:
        points=[]; depth=0
        for x,y,z in tri:
            if not bow: x,y,z=z+.4,x,y
            # Oblique view exposes bow depth and arrow feather silhouette.
            xx=x*.866+z*.5; zz=-x*.5+z*.866
            xx,yy=xx,y*.97-zz*.24
            points.append(((345 if bow else 820)+xx*660,420-yy*370))
            depth+=zz
        a,b,c=tri
        u=[b[i]-a[i] for i in range(3)]; v=[c[i]-a[i] for i in range(3)]
        n=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
        length=math.sqrt(sum(x*x for x in n)) or 1
        light=.55+.45*abs(sum(x*y for x,y in zip(n,(-.3,.6,-.74)))/length)
        color=tuple(int(min(255,c*255*light*1.35)) for c in data['colors'][mapping[name]])
        faces.append((depth,points,color))
for depth,points,color in sorted(faces,reverse=True):
    draw.polygon([(int(x*scale),int(y*scale)) for x,y in points],fill=color)
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',20*scale)
title=ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf',32*scale)
draw.text((50*scale,35*scale),'WOODEN BOW + ARROW',font=title,fill=(234,221,199))
draw.text((50*scale,86*scale),'Duskborn / first ranged weapon',font=font,fill=(159,166,163))
draw.text((240*scale,700*scale),'1.36 m  /  724 triangles',font=font,fill=(185,187,177))
draw.text((675*scale,480*scale),'0.81 m  /  72 triangles',font=font,fill=(185,187,177))
draw.text((50*scale,747*scale),'Offline geometry preview - Unity hand fit and animation still require review',font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',15*scale),fill=(129,143,143))
output=root/'Temp/RangedCombatValidation/wooden-bow-preview.png'
im.resize((1100,780),Image.Resampling.LANCZOS).save(output)
print(output)
