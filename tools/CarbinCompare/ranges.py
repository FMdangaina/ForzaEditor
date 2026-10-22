import cb, numpy as np, os
S=os.path.dirname(os.path.abspath(__file__))
def sx(v,bits): v=v&((1<<bits)-1); return np.where(v>=1<<(bits-1),v-(1<<bits),v).astype(float)
for nm,path,lay in (('FM4','fm4/toy_suprarz_98_lod0.carbin',((21,11),(11,10),(0,11))),('FH','fh1/TOY_SupraRZ_98_lod0.carbin',((21,11),(10,11),(0,10)))):
    o,d=cb.parse(os.path.join(S,path)); W=[]
    for s in o['secs']:
        n=s['vc0']
        if s['B'][0]==n and n: W.append(np.frombuffer(d,'>u4',n,s['B'][2]).astype(np.int64))
    W=np.concatenate(W); v=np.stack([sx(W>>sh,bw) for sh,bw in lay],1); L=np.linalg.norm(v,axis=1)
    print(nm,len(W),'field min',v.min(0),'max',v.max(0),'| len pct 0/1/50/99/100',np.percentile(L,[0,1,50,99,100]).round(0),
          '| |y|>511 count',(abs(v[:,1])>=500).sum(),'| len<8:',(L<8).sum(),' len<30:',(L<30).sum())
    if nm=='FM4': print('  FM4 |x| min among all',np.abs(v[:,0]).min(),' frac |x|>0.5*len',(np.abs(v[:,0])>0.5*L).mean().round(3))
