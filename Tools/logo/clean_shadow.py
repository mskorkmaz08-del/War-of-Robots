import sys, numpy as np
from PIL import Image
im=np.asarray(Image.open(sys.argv[1]).convert('RGBA')).astype(np.float32)
a=im[...,3]/255
H,W=a.shape
solid=a>0.97
ys,xs=np.nonzero(solid)
feet_y=ys.max(); cx=xs[ys>feet_y-0.08*H].mean()
yy,xx=np.mgrid[0:H,0:W]
# keep the contact shadow only near the feet, fading out
d=np.sqrt(((xx-cx)/(0.38*W))**2+((yy-feet_y)/(0.05*H))**2)
m=np.clip(1.2-d,0,1)**1.5
a2=np.where(solid,a,a*m*0.8)
im[...,3]=a2*255
out=Image.fromarray(im.astype(np.uint8))
bb=out.getchannel('A').point(lambda v:255 if v>6 else 0).getbbox(); out=out.crop(bb)
out.save(sys.argv[2]); print(out.size, feet_y)
