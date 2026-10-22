import cb, sys, numpy as np, os
S=os.path.dirname(os.path.abspath(__file__))
a,da=cb.parse(os.path.join(S,'fm4/toy_suprarz_98_lod0.carbin')); b,db=cb.parse(os.path.join(S,'fh1/TOY_SupraRZ_98_lod0.carbin'))
def sx(v,bits): v=v&((1<<bits)-1); return np.where(v>=1<<(bits-1),v-(1<<bits),v).astype(float)
rows=[]
for p in ['bumperRb','bumperRa','wingf','bumperRc','bumperRj','wingi','winga','wingg']:
    s=[x for x in a['secs'] if x['name']==p][0]; t=[x for x in b['secs'] if x['name']==p][0]
    n=s['vc0']
    F=np.frombuffer(da,'>u4',n,s['B'][2]).astype(np.int64); H=np.frombuffer(db,'>u4',n,t['B'][2]).astype(np.int64)
    V=np.frombuffer(da,np.uint8,n*32,s['v0off']).reshape(n,32)
    pos=np.frombuffer(da,'>i2',n*16,s['v0off']).reshape(n,16)[:,:3]
    qt=np.frombuffer(da,'>i2',n*16,s['v0off']).reshape(n,16)[:,8:12]
    f=np.stack([sx(F>>21,11),sx(F>>11,10),sx(F,11)],1); h=np.stack([sx(H>>21,11),sx(H>>10,11),sx(H,10)],1)
    sg=np.sign(f[:,0]*h[:,0]); 
    print(p,'n',n,'sign(f_x h_x)>0:',round((sg>0).mean(),3),'| sign agree with pos.x>0:',round(((sg>0)==(pos[:,0]>0)).mean(),3),'pos.y',round(((sg>0)==(pos[:,1]>0)).mean(),3),'pos.z',round(((sg>0)==(pos[:,2]>0)).mean(),3),
          '| with qt.w>0:',round(((sg>0)==(qt[:,3]>0)).mean(),3),'| f_x<0 vs pos.x<0',round(((f[:,0]<0)==(pos[:,0]<0)).mean(),3))
# Extra words of the FH tables: print for sections with damage and their relation to the min/max of the unsigned fields
print('FH Extra (gapB) vs range of vertex words (unsigned 11:11:10 fields):')
for p in ['bumperRb','wingf','bumperRa','bumperRc','winga']:
    t=[x for x in b['secs'] if x['name']==p][0]; n=t['vc0']
    H=np.frombuffer(db,'>u4',n,t['B'][2]).astype(np.int64)
    u=np.stack([H>>21,(H>>10)&2047,H&1023],1)
    g=t['gapB']; w0=int.from_bytes(g[:4],'big'); w1=int.from_bytes(g[4:],'big')
    print(p,'gapB',g.hex(),'w0 fields',(w0>>21,(w0>>10)&2047,w0&1023),'w1 fields',(w1>>21,(w1>>10)&2047,w1&1023),'| data min',u.min(0),'max',u.max(0))
