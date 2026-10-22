import cb, sys, numpy as np, os
S=os.path.dirname(os.path.abspath(__file__))
a,da=cb.parse(os.path.join(S,'fm4/toy_suprarz_98_lod0.carbin')); b,db=cb.parse(os.path.join(S,'fh1/TOY_SupraRZ_98_lod0.carbin'))
def sx(v,bits): v=v&((1<<bits)-1); return np.where(v>=1<<(bits-1),v-(1<<bits),v).astype(float)
parts=sys.argv[1:]
for p in parts:
    s=[x for x in a['secs'] if x['name']==p][0]; t=[x for x in b['secs'] if x['name']==p][0]; n=s['vc0']
    F=np.frombuffer(da,'>u4',n,s['B'][2]).astype(np.int64); H=np.frombuffer(db,'>u4',n,t['B'][2]).astype(np.int64)
    f=np.stack([sx(F>>21,11),sx(F>>11,10),sx(F,11)],1)
    h=np.stack([sx(H>>21,11),sx(H>>11,10),sx(H,11)],1)
    print(p,'FH as 11:10:11: len median',np.median(np.linalg.norm(h,axis=1)).round(1),' field ranges',[(int(h[:,i].min()),int(h[:,i].max())) for i in range(3)])
    for i in range(3):
        c=np.corrcoef(f[:,i],h[:,i])[0,1]; k=np.polyfit(h[:,i],f[:,i],1)
        print('  field',i,'corr %.3f'%c,'fit f = %.2f*h + %.1f'%tuple(k))
    print('  first rows f|h:'); 
    for i in range(0,6): print('   ',f[i].astype(int),h[i].astype(int),hex(F[i]),hex(H[i]))
