"""Replace the stepped page poses with a continuously sampled turn; preserve geometry."""
import bpy, math
from math import sin, cos, pi

leaf=bpy.data.objects['Spellbook_Turning_Page']
book=bpy.data.objects['Spellbook_Hover']
scene=bpy.context.scene
NU,NV=8,4
NN=(NU+1)*(NV+1)

def page(theta,sink=0):
    out=[]
    for layer in (0,1):
        for j in range(NV+1):
            y=-.311+.622*j/NV
            for i in range(NU+1):
                u=i/NU
                curl=.075*sin(pi*u)*sin(theta)
                out.append((.497*u*cos(theta)-curl*sin(theta),
                    y+.017*sin(pi*u)*sin(theta)*sin(j*pi/NV),
                    .063+.13*.497*u/.54+.497*u*sin(theta)+curl*cos(theta)-sink+.0015*layer))
    return out

def mix(a,b,t):
    t=t*t*(3-2*t)
    return [tuple(x*(1-t)+y*t for x,y in zip(p,q)) for p,q in zip(a,b)]

left=page(pi);right=page(0)
left_hidden=page(pi,.025);right_hidden=page(0,.025)
spine=[(p[0]*.002,p[1],-.01+.0015*(i//NN)) for i,p in enumerate(right)]
def pose(frame):
    if frame<=113:
        t=(frame-1)/112
        return page(pi*(.5-.5*cos(pi*t)))
    if frame<=115:return mix(left,left_hidden,(frame-113)/2)
    if frame<=117:return mix(left_hidden,spine,(frame-115)/2)
    if frame<=119:return mix(spine,right_hidden,(frame-117)/2)
    return mix(right_hidden,right,(frame-119)/2)

leaf.shape_key_clear()
for v,co in zip(leaf.data.vertices,right):v.co=co
leaf.shape_key_add(name='Basis')
# Relative shape keys remain exportable; each triangular weight crossfades only
# adjacent samples. LINEAR interpolation avoids easing to a stop at each sample.
sample_frames=[1]+list(range(3,114,2))+list(range(114,122))
for idx,f in enumerate(sample_frames[1:-1],1):
    key=leaf.shape_key_add(name='Flow_%03d'%f)
    for v,co in zip(key.data,pose(f)):v.co=co
    marks={1:0.0,121:0.0,sample_frames[idx-1]:0.0,f:1.0,sample_frames[idx+1]:0.0}
    for time,value in sorted(marks.items()):
        key.value=value;key.keyframe_insert('value',frame=time)

def curves(idblock):
    ad=idblock.animation_data
    for layer in ad.action.layers:
        for strip in layer.strips:
            bag=strip.channelbag(ad.action_slot)
            if bag:
                yield from bag.fcurves

keydata=leaf.data.shape_keys
keydata.animation_data.action.name='Moonwell_Continuous_Page_Turn'
for fc in curves(keydata):
    for k in fc.keyframe_points:k.interpolation='LINEAR'

# A sampled sinusoid has continuous motion and an identical loop seam.
book.animation_data_clear()
for f in range(1,122):
    book.location.z=1.59+.0275*sin(2*pi*(f-1)/120)
    book.keyframe_insert('location',index=2,frame=f)
book.animation_data.action.name='Moonwell_Smooth_Book_Hover'
for fc in curves(book):
    for k in fc.keyframe_points:k.interpolation='LINEAR'
book['animation']='Continuous 4-second page turn and sinusoidal hover; 30 fps; frames 1-120; seam at 121'
scene.frame_start=1;scene.frame_end=120;scene.render.fps=30
scene.frame_set(57)
result={'shape_samples':len(sample_frames),'turn_frames':[1,113],'hidden_reset_frames':[113,121],'fps':30}
