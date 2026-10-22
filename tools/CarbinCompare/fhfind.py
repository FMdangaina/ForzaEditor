import cb, numpy as np, os, itertools
S=os.path.dirname(os.path.abspath(__file__))
def sx(v,bits): v=v&((1<<bits)-1); return np.where(v>=1<<(bits-1),v-(1<<bits),v).astype(float)
parts=['bumperRb','bumperRa','bumperFd','wingf','bumperRj','skirtRa','bumperFi','exhausta']
a,da=cb.parse(os.path.join(S,'fm4/toy_suprarz_98_lod0.carbin')); b,db=cb.parse(os.path.join(S,'fh1/TOY_SupraRZ_98_lod0.carbin'))
F=[];H=[]
for p in parts:
    s=[x for x in a['secs'] if x['name']==p][0]; t=[x for x in b['secs'] if x['name']==p][0]; n=s['vc0']
    F.append(np.frombuffer(da,'>u4',n,s['B'][2]).astype(np.int64)); H.append(np.frombuffer(db,'>u4',n,t['B'][2]).astype(np.int64))
F=np.concatenate(F);H=np.concatenate(H)
f=np.stack([sx(F>>22,10)/511,sx(F>>11,11)/1023,sx(F,11)/1023],1)   # FM4 10:11:11
print('FM4 10:11:11 len pct',np.percentile(np.linalg.norm(f,axis=1),[0,5,50,95,100]).round(3))
fu=f/np.linalg.norm(f,axis=1)[:,None]
res=[]
for w in itertools.product(range(4,13),repeat=3):
    if sum(w)>32: continue
    for gap in range(0,33-sum(w)):
        for order in itertools.permutations(range(3)):
            sh=32-gap; Fd=[]
            for bw in w: sh-=bw; Fd.append(sx(H>>sh,bw))
            v=np.stack([Fd[i] for i in order],1)
            L=np.linalg.norm(v,axis=1)
            if L.min()==0 and (L==0).mean()>0.3: continue
            u=v/np.maximum(L,1e-9)[:,None]
            res.append((float(np.median(np.abs((u*fu).sum(1)))),w,gap,order))
res.sort(reverse=True)
for r in res[:8]: print(r)
