import numpy as np, sys
from PIL import Image, ImageFilter
T=Image.open('title_c.png').convert('RGBA'); R=Image.open('hero_clean.png').convert('RGBA')
rh=int(sys.argv[1]) if len(sys.argv)>1 else 1150
R=R.resize((int(R.width*rh/R.height),rh),Image.LANCZOS)
W=T.width; overlap=int(T.height*0.55)
H=rh+T.height-overlap
cv=Image.new('RGBA',(W,H),(0,0,0,0))
# teal reactor glow behind the robot
yy,xx=np.mgrid[0:H,0:W]; cx=W/2; cy=rh*0.45
d=np.sqrt(((xx-cx)/(W*0.36))**2+((yy-cy)/(rh*0.5))**2)
g=np.clip(1-d,0,1)**1.8*0.55
glow=np.zeros((H,W,4),np.uint8); glow[...,0]=40; glow[...,1]=200; glow[...,2]=180; glow[...,3]=(g*255).astype(np.uint8)
cv.alpha_composite(Image.fromarray(glow))
rx=(W-R.width)//2+int(W*0.02)
# atmospheric haze over the lower robot (it sits far away and huge)
Ra=np.asarray(R).astype(np.float32)
h=np.clip((np.arange(R.height)/R.height-0.55)/0.45,0,1)[:,None]*0.55
for k,c in enumerate((14,22,28)): Ra[...,k]=Ra[...,k]*(1-h)+c*h
cv.alpha_composite(Image.fromarray(Ra.astype(np.uint8)),(rx,0))
# title shadow then title
sh=Image.new('RGBA',T.size,(0,0,0,0)); sh.putalpha(T.getchannel('A').point(lambda a:int(a*0.7)))
sh=sh.filter(ImageFilter.GaussianBlur(22)); cv.alpha_composite(sh,(0,H-T.height+18))
cv.alpha_composite(T,(0,H-T.height))
cv.save('keyart.png'); bg=Image.new('RGBA',cv.size,(17,19,23,255)); bg.alpha_composite(cv); bg.convert('RGB').save('keyart_b.jpg',quality=92)
print(cv.size)
