import cb, sys, numpy as np, os, itertools
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from world import world, sx, a, da, b, db
rows=[]
for s in a['secs']:
    t=[x for x in b['secs'] if x['name']==s['name']]
    if not t: continue
    t=t[0]; n=s['vc0']
    if t['vc0']!=n or s['B'][0]!=n or t['B'][0]!=n or n<100: continue
    F=np.frombuffer(da,'>u4',n,s['B'][2]).astype(np.int64); H=np.frombuffer(db,'>u4',n,t['B'][2]).astype(np.int64)
    f=np.stack([sx(F>>21,11),sx(F>>11,10),sx(F,11)],1); h=np.stack([sx(H>>21,11),sx(H>>10,11),sx(H,10)],1)
    ok=np.linalg.norm(h,axis=1)>20
    rows.append((s['name'],world(s,da)[ok],f[ok],h[ok]))
C=np.array([0,0.4,0.0])
def score(which):
    out=[]
    for perm in itertools.permutations(range(3)):
        for sg in itertools.product((1,-1),repeat=3):
            m=np.array(sg)
            tot=0;cnt=0
            for nm,P,f,h in rows:
                v=(f if which=='fm4' else h)[:,list(perm)]*m
                d=((P-C)*v).sum(1)
                tot+=(d>0).sum(); cnt+=len(d)
            out.append((tot/cnt,perm,sg))
    out.sort(key=lambda r:-abs(r[0]-0.5)); return out[:4]
for w in ('fm4','fh'): print(w,[(round(r[0],3),r[1],r[2]) for r in score(w)])
# per-part consistency for best fm4 perm
best=score('fm4')[0]
print('per part fraction outward with FM4 best perm',best[1],best[2])
for nm,P,f,h in rows[:40]:
    v=f[:,list(best[1])]*np.array(best[2]); print('  %-14s %.3f'%(nm,(((P-C)*v).sum(1)>0).mean()))
