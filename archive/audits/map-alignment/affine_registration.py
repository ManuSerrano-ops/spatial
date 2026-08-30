from pathlib import Path
import json
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent / 'alignment-final'
SCALE = 4

def mask(path):
    rgb=np.asarray(Image.open(path).convert('RGB').resize((600,424)),dtype=np.int16)
    lum=rgb.mean(2); chroma=rgb.max(2)-rgb.min(2)
    m=(lum<145)|((lum<185)&(chroma>38))
    p=np.pad(m,1); ins=m.copy()
    for y,x in ((0,1),(0,-1),(1,0),(-1,0),(1,1),(1,-1),(-1,1),(-1,-1)):
       ins &= p[1+y:1+y+m.shape[0],1+x:1+x+m.shape[1]]
    return m & ~ins

def dilate(m,r=2):
    p=np.pad(m,r); out=np.zeros_like(m)
    for y in range(2*r+1):
      for x in range(2*r+1): out |= p[y:y+m.shape[0],x:x+m.shape[1]]
    return out

results={}
for key in ['norte','nivel3','sur']:
  d=mask(ROOT/f'{key}-dark.png'); l=mask(ROOT/f'{key}-light.png'); target=dilate(d)
  ys,xs=np.where(l)
  # evenly cap the representation, retaining whole-plan coverage.
  step=max(1,len(xs)//3000); xs=xs[::step]; ys=ys[::step]
  best=(0,None)
  for s in np.arange(.45,1.81,.05):
    bx=np.rint(xs*s).astype(int); by=np.rint(ys*s).astype(int)
    for dx in range(-240,241,12):
      tx=bx+dx; validx=(tx>=0)&(tx<600)
      for dy in range(-160,161,12):
        ty=by+dy; ok=validx&(ty>=0)&(ty<424)
        if ok.sum()<len(xs)*.4: continue
        score=target[ty[ok],tx[ok]].mean()
        if score>best[0]: best=(float(score),(float(s),dx*SCALE,dy*SCALE,int(ok.sum())))
  results[key]={'bestLightToDarkEdgeMatchWithin8px':round(best[0],5),'uniformScale_offsetX_offsetY_validPoints':best[1]}
print(json.dumps(results,indent=2))
(ROOT/'affine-registration.json').write_text(json.dumps(results,indent=2))
