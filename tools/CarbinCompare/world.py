import cb, sys, numpy as np, os
S=os.path.dirname(os.path.abspath(__file__))
a,da=cb.parse(os.path.join(S,'fm4/toy_suprarz_98_lod0.carbin')); b,db=cb.parse(os.path.join(S,'fh1/TOY_SupraRZ_98_lod0.carbin'))
def sx(v,bits): v=v&((1<<bits)-1); return np.where(v>=1<<(bits-1),v-(1<<bits),v).astype(float)
def world(s,d):
    n=s['vc0']; raw=np.frombuffer(d,'>i2',n*16,s['v0off']).reshape(n,16)
    pos=raw[:,:3].astype(float)/32767.0*(raw[:,3:4].astype(float)/32767.0)
    tmin=np.array(s['tmin']); tmax=np.array(s['tmax'])
    if np.linalg.norm(tmin)>0 and np.linalg.norm(tmax)>0:
        lo=pos.min(0); hi=pos.max(0); pos=tmin+(pos-lo)/np.maximum(hi-lo,1e-12)*(tmax-tmin)
    return pos+np.array(s['offset'])
allP=[];allY=[];allN=[]
for s in a['secs']:
    t=[x for x in b['secs'] if x['name']==s['name']]
    if not t: continue
    t=t[0]; n=s['vc0']
    if t['vc0']!=n or s['B'][0]!=n or t['B'][0]!=n or n<100: continue
    F=np.frombuffer(da,'>u4',n,s['B'][2]).astype(np.int64); H=np.frombuffer(db,'>u4',n,t['B'][2]).astype(np.int64)
    f=np.stack([sx(F>>21,11),sx(F>>11,10),sx(F,11)],1); h=np.stack([sx(H>>21,11),sx(H>>10,11),sx(H,10)],1)
    ok=np.linalg.norm(h,axis=1)>20
    W=world(s,da); y=((f*h).sum(1)>0)
    allP.append(W[ok]); allY.append(y[ok]); allN+= [s['name']]*ok.sum()
P=np.concatenate(allP); Y=np.concatenate(allY); N=np.array(allN)
print('vertices',len(P),'sign>0 frac',Y.mean().round(3),'world ranges',P.min(0).round(2),P.max(0).round(2))
for ax in range(3):
    for thr in (-0.1,0,0.1):
        for flip in (0,1):
            pred=(P[:,ax]>thr)^bool(flip); print('axis','xyz'[ax],'thr',thr,'flip',flip,'acc %.3f'%(pred==Y).mean())
best=(0,)
for ax in range(3):
    for thr in np.linspace(P[:,ax].min(),P[:,ax].max(),400):
        acc=max(((P[:,ax]>thr)==Y).mean(),((P[:,ax]<=thr)==Y).mean())
        if acc>best[0]: best=(acc,ax,thr)
print('best global plane',best)
