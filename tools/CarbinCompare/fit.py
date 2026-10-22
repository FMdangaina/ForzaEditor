import cb, numpy as np, os
S=os.path.dirname(os.path.abspath(__file__))
a,da=cb.parse(os.path.join(S,'fm4/toy_suprarz_98_lod0.carbin')); b,db=cb.parse(os.path.join(S,'fh1/TOY_SupraRZ_98_lod0.carbin'))
def sx(v,bits): v=v&((1<<bits)-1); return np.where(v>=1<<(bits-1),v-(1<<bits),v).astype(float)
for p in ['bumperRb','wingf','bumperRa','bumperFd','body','skirtRa']:
    s=[x for x in a['secs'] if x['name']==p][0]; t=[x for x in b['secs'] if x['name']==p][0]; n=s['vc0']
    F=np.frombuffer(da,'>u4',n,s['B'][2]).astype(np.int64); H=np.frombuffer(db,'>u4',n,t['B'][2]).astype(np.int64)
    f=np.stack([sx(F>>21,11),sx(F>>11,10),sx(F,11)],1); h=np.stack([sx(H>>21,11),sx(H>>10,11),sx(H,10)],1)
    L=np.linalg.norm(h,axis=1); print(p,'FH len pct 0/5/50/95/100',np.percentile(L,[0,5,50,95,100]).round(0),'FM4 len',np.percentile(np.linalg.norm(f,axis=1),[0,5,50,95,100]).round(0))
    X=np.hstack([h,np.ones((n,1))])
    A,res,_,_=np.linalg.lstsq(X,f,rcond=None); r=f-X@A
    print('  lstsq f = A h + b:\n',A.T.round(2),'\n  rms residual per field',r.std(0).round(1),'  f std',f.std(0).round(1))
    # proportional to unit(h)?
    u=h/np.maximum(L,1e-9)[:,None]
    for i in range(3):
        k=(f[:,i]*u[:,i]).sum()/(u[:,i]**2).sum(); print('  field',i,'f ~ k*unit(h): k=%.0f  corr %.4f'%(k,np.corrcoef(f[:,i],u[:,i])[0,1]))
