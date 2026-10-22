import cb, sys, numpy as np, os, itertools
S=os.path.dirname(os.path.abspath(__file__))
a,da=cb.parse(os.path.join(S,'fm4/toy_suprarz_98_lod0.carbin')); b,db=cb.parse(os.path.join(S,'fh1/TOY_SupraRZ_98_lod0.carbin'))
FF=[];HH=[]
for p in ['bumperRb','bumperRa','wingf','bumperRc','bumperRj','wingi','winga','wingg']:
    s=[x for x in a['secs'] if x['name']==p][0]; t=[x for x in b['secs'] if x['name']==p][0]; n=s['vc0']
    FF.append(np.frombuffer(da,'>u4',n,s['B'][2]).astype(np.int64)); HH.append(np.frombuffer(db,'>u4',n,t['B'][2]).astype(np.int64))
F=np.concatenate(FF); H=np.concatenate(HH)
def sx(v,bits): v=v&((1<<bits)-1); return np.where(v>=1<<(bits-1),v-(1<<bits),v).astype(float)
def fields(w,starts_widths): return np.stack([sx(w>>sh,bw) for sh,bw in starts_widths],1)
def unit(v): return v/np.maximum(np.linalg.norm(v,axis=1),1e-9)[:,None]
# layouts: three fields (shift,width) contiguous from some top offset; allow gap bits at bottom
def layouts():
    for w in itertools.product(range(8,13),repeat=3):
        if sum(w)>32: continue
        for gap in range(0,32-sum(w)+1):
            sh=32-gap; out=[]
            for bw in w: sh-=bw; out.append((sh,bw))
            yield w,gap,out
ref=unit(fields(H,[(21,11),(10,11),(0,10)]))
mask=np.linalg.norm(fields(H,[(21,11),(10,11),(0,10)]),axis=1)>40
res=[]
for w,gap,L in layouts():
    v=unit(fields(F,L))
    c=np.abs((v[mask]*ref[mask]).sum(1))
    res.append((float(np.median(c)),float(np.mean(c>0.97)),w,gap))
res.sort(reverse=True)
print('FM4 layouts vs FH 11:11:10 reference (median |cos|, frac>0.97):'); [print(r) for r in res[:8]]
fb=res[0]
# now fix FM4 best and search FH layouts
w,gap=fb[2],fb[3]; sh=32-gap; L=[]
for bw in w: sh-=bw; L.append((sh,bw))
ref=unit(fields(F,L)); res=[]
for w,gap,L2 in layouts():
    v=unit(fields(H,L2)); m=np.linalg.norm(fields(H,L2),axis=1)>0
    c=np.abs((v*ref).sum(1)); res.append((float(np.median(c)),float(np.mean(c>0.97)),w,gap))
res.sort(reverse=True)
print('FH layouts vs best FM4:'); [print(r) for r in res[:8]]
