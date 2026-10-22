import sys, os, numpy as np
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
import io, contextlib
with contextlib.redirect_stdout(io.StringIO()):
    from world import world, sx, a, da, b, db
print('%-14s %6s %5s  %-4s %7s %7s %7s  %s'%('part','n','pos%','axis','thr','lo','hi','acc'))
for s in a['secs']:
    t=[x for x in b['secs'] if x['name']==s['name']]
    if not t: continue
    t=t[0]; n=s['vc0']
    if t['vc0']!=n or s['B'][0]!=n or t['B'][0]!=n or n<200: continue
    F=np.frombuffer(da,'>u4',n,s['B'][2]).astype(np.int64); H=np.frombuffer(db,'>u4',n,t['B'][2]).astype(np.int64)
    f=np.stack([sx(F>>21,11),sx(F>>11,10),sx(F,11)],1); h=np.stack([sx(H>>21,11),sx(H>>10,11),sx(H,10)],1)
    ok=np.linalg.norm(h,axis=1)>20; W=world(s,da)[ok]; y=((f*h).sum(1)>0)[ok]
    if y.mean()<0.03 or y.mean()>0.97: print('%-14s %6d %5.0f  (no flip)'%(s['name'],ok.sum(),100*y.mean())); continue
    best=(0,)
    for ax in range(3):
        for thr in np.percentile(W[:,ax],np.arange(2,99,1)):
            acc=max(((W[:,ax]>thr)==y).mean(),((W[:,ax]<=thr)==y).mean())
            if acc>best[0]: best=(acc,ax,thr)
    print('%-14s %6d %5.0f  %-4s %7.3f %7.3f %7.3f  %.3f'%(s['name'],ok.sum(),100*y.mean(),'xyz'[best[1]],best[2],W[:,best[1]].min(),W[:,best[1]].max(),best[0]))
