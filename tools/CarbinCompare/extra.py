import cb, numpy as np, os
S=os.path.dirname(os.path.abspath(__file__))
def sx(v,bits): v=v&((1<<bits)-1); return np.where(v>=1<<(bits-1),v-(1<<bits),v).astype(float)
o,d=cb.parse(os.path.join(S,'fh1/TOY_SupraRZ_98_lod0.carbin'))
print('%-14s %-10s %-10s | max-len vertex h | mean h (len>20) | median h'%('part','extra0','extra1'))
for s in o['secs']:
    n=s['vc0']
    if s['B'][0]!=n or n<100: continue
    H=np.frombuffer(d,'>u4',n,s['B'][2]).astype(np.int64); h=np.stack([sx(H>>21,11),sx(H>>10,11),sx(H,10)],1); L=np.linalg.norm(h,axis=1)
    g=s['gapB']; e=[np.array([sx(int.from_bytes(g[i:i+4],'big')>>sh,bw) for sh,bw in ((21,11),(10,11),(0,10))]) for i in (0,4)]
    m=L>20
    print('%-14s %-18s %-18s | %-16s | %-18s | %s'%(s['name'],e[0].astype(int),e[1].astype(int),h[L.argmax()].astype(int),h[m].mean(0).round(0).astype(int) if m.any() else '-',np.median(h[m],axis=0).astype(int) if m.any() else '-'))
