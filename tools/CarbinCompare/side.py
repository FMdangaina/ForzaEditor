import cb, sys, numpy as np, os
S=os.path.dirname(os.path.abspath(__file__))
a,da=cb.parse(os.path.join(S,'fm4/toy_suprarz_98_lod0.carbin')); b,db=cb.parse(os.path.join(S,'fh1/TOY_SupraRZ_98_lod0.carbin'))
def sx(v,bits): v=v&((1<<bits)-1); return np.where(v>=1<<(bits-1),v-(1<<bits),v).astype(float)
names=sys.argv[1:] or [s['name'] for s in a['secs'] if s['vc0']>200 and s['B'][0]==s['vc0'] and s['name'] in {x['name'] for x in b['secs']}]
for p in names:
    s=[x for x in a['secs'] if x['name']==p][0]; t=[x for x in b['secs'] if x['name']==p][0]; n=s['vc0']
    if t['vc0']!=n or s['B'][0]!=n or t['B'][0]!=n: continue
    F=np.frombuffer(da,'>u4',n,s['B'][2]).astype(np.int64); H=np.frombuffer(db,'>u4',n,t['B'][2]).astype(np.int64)
    f=np.stack([sx(F>>21,11),sx(F>>11,10),sx(F,11)],1); h=np.stack([sx(H>>21,11),sx(H>>10,11),sx(H,10)],1)
    ok=np.linalg.norm(h,axis=1)>20
    raw=np.frombuffer(da,'>i2',n*16,s['v0off']).reshape(n,16)
    pos=raw[:,:3].astype(float)*raw[:,3:4]/32767.0
    y=((f*h).sum(1)>0)[ok]; P=pos[ok]
    best=(0,)
    for ax in range(3):
        for thr in np.percentile(P[:,ax],np.arange(2,99,2)):
            acc=max(((P[:,ax]>thr)==y).mean(),((P[:,ax]<=thr)==y).mean()) 
            if acc>best[0]: best=(acc,ax,thr)
    print('%-14s n=%6d  sign>0 frac %.2f  best single-axis split: acc %.3f axis %s thr %.3f (range %.2f..%.2f)'%(p,ok.sum(),y.mean(),best[0],'xyz'[best[1]],best[2],P[:,best[1]].min(),P[:,best[1]].max()))
