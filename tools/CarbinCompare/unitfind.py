import cb, numpy as np, os, itertools, sys
S=os.path.dirname(os.path.abspath(__file__))
def sx(v,bits): v=v&((1<<bits)-1); return np.where(v>=1<<(bits-1),v-(1<<bits),v).astype(float)
def words(path,parts):
    o,d=cb.parse(os.path.join(S,path)); out=[]
    for s in o['secs']:
        if s['name'] in parts and s['B'][0]==s['vc0']: out.append(np.frombuffer(d,'>u4',s['vc0'],s['B'][2]).astype(np.int64))
    return np.concatenate(out)
parts={'bumperRb','bumperRa','bumperFd','wingf','bumperRj','skirtRa','bumperFi','exhausta'}
for nm,path in (('FM4','fm4/toy_suprarz_98_lod0.carbin'),('FH','fh1/TOY_SupraRZ_98_lod0.carbin')):
    W=words(path,parts); res=[]
    for k in (3,4):
        for w in itertools.product(range(5,13),repeat=k):
            if sum(w)>32 or sum(w)<26: continue
            for gap in range(0,33-sum(w)):
                for unsigned_off in (0,):
                    sh=32-gap; F=[]
                    for bw in w: sh-=bw; F.append(sx(W>>sh,bw))
                    v=np.stack(F[:3],1) if k==3 else np.stack(F[:3],1)
                    sc=[(1<<(bw-1))-1 for bw in w[:3]]
                    vn=v/np.array(sc)
                    L=np.linalg.norm(vn,axis=1)
                    res.append((L.std()/max(L.mean(),1e-9),w,gap,round(float(L.mean()),3)))
    res.sort(); print(nm,'most constant-length layouts (cv, widths, gap, mean len when each field scaled to +-1):')
    for r in res[:6]: print('  ',r)
