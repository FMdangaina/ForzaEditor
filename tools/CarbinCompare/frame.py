import cb, sys, numpy as np, os
S=os.path.dirname(os.path.abspath(__file__))
a,da=cb.parse(os.path.join(S,'fm4/toy_suprarz_98_lod0.carbin')); b,db=cb.parse(os.path.join(S,'fh1/TOY_SupraRZ_98_lod0.carbin'))
def sx(v,bits): v=v&((1<<bits)-1); return np.where(v>=1<<(bits-1),v-(1<<bits),v).astype(float)
def rot(q):  # q = x,y,z,w
    x,y,z,w=q.T
    R=np.empty((len(q),3,3))
    R[:,0,0]=1-2*(y*y+z*z); R[:,0,1]=2*(x*y+z*w); R[:,0,2]=2*(x*z-y*w)
    R[:,1,0]=2*(x*y-z*w); R[:,1,1]=1-2*(x*x+z*z); R[:,1,2]=2*(y*z+x*w)
    R[:,2,0]=2*(x*z+y*w); R[:,2,1]=2*(y*z-x*w); R[:,2,2]=1-2*(x*x+y*y)
    return R
def unit(v): return v/np.maximum(np.linalg.norm(v,axis=1),1e-9)[:,None]
for p in sys.argv[1:]:
    s=[x for x in a['secs'] if x['name']==p][0]; t=[x for x in b['secs'] if x['name']==p][0]; n=s['vc0']
    F=np.frombuffer(da,'>u4',n,s['B'][2]).astype(np.int64); H=np.frombuffer(db,'>u4',n,t['B'][2]).astype(np.int64)
    raw=np.frombuffer(da,'>i2',n*16,s['v0off']).reshape(n,16)
    q=raw[:,8:12]/32767.0; R=rot(q)
    N=R[:,0,:]; T=R[:,1,:]; Bt=R[:,2,:]
    f=unit(np.stack([sx(F>>21,11),sx(F>>11,10),sx(F,11)],1)); h=unit(np.stack([sx(H>>21,11),sx(H>>10,11),sx(H,10)],1))
    print(p,n)
    for nm,v in (('FM4 f',f),('FH h',h)):
        print('  ',nm,'mean |dot| with N %.2f T %.2f B %.2f'%tuple(np.abs((v*X).sum(1)).mean() for X in (N,T,Bt)),
              '| mean dot with axes',np.round(np.abs(v).mean(0),2))
    s_=np.sign((f*h).sum(1))
    for nm,X in (('N',N),('T',T),('B',Bt)):
        d=(f*X).sum(1); print('   sign(f.h)>0 agrees with sign(f.%s)>0: %.3f ; with sign(h.%s)>0: %.3f'%(nm,((s_>0)==(d>0)).mean(),nm,((s_>0)==((h*X).sum(1)>0)).mean()))
