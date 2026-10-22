import cb, sys, numpy as np
a,da=cb.parse(sys.argv[1]); b,db=cb.parse(sys.argv[2])
FBs=[];HBs=[]
for part in sys.argv[3:]:
    s=[x for x in a['secs'] if x['name']==part][0]; t=[x for x in b['secs'] if x['name']==part][0]
    n=s['vc0']; assert n==t['vc0']
    FBs.append(np.frombuffer(da,dtype='>u4',count=n,offset=s['B'][2]).astype(np.int64))
    HBs.append(np.frombuffer(db,dtype='>u4',count=n,offset=t['B'][2]).astype(np.int64))
FB=np.concatenate(FBs); HB=np.concatenate(HBs); print('vertices',len(FB))
def sx(v,bits): v=v&((1<<bits)-1); return np.where(v>=1<<(bits-1),v-(1<<bits),v).astype(float)
f=np.stack([sx(FB>>21,11),sx(FB>>11,10),sx(FB,11)],1)      # FM4 11:10:11
h=np.stack([sx(HB>>21,11),sx(HB>>10,11),sx(HB,10)],1)      # FH 11:11:10
print('FM4 field ranges',[(int(f[:,i].min()),int(f[:,i].max())) for i in range(3)])
print('FH  field ranges',[(int(h[:,i].min()),int(h[:,i].max())) for i in range(3)])
# per-field linear relation FH ~ k * FM4 ?
for i in range(3):
    c=np.corrcoef(f[:,i],h[:,i])[0,1]; print('field',i,'corr',round(c,4))
# scale: FM4 as direction with weights
for wy in (1.0,2.0):
    fv=f*np.array([1,wy,1]); l=np.linalg.norm(fv,axis=1); print('FM4 len w_y=',wy,'median',np.median(l),'p5/p95',np.percentile(l,[5,95]))
fl=np.linalg.norm(f*np.array([1,2,1]),axis=1); hl=np.linalg.norm(h*np.array([1,1,2]),axis=1)
fu=(f*np.array([1,2,1]))/fl[:,None]; hu=(h*np.array([1,1,2]))/np.maximum(hl,1e-9)[:,None]
cos=(fu*hu).sum(1); print('cos with y/z weights 2: median',np.median(cos),'p5',np.percentile(cos,5))
fu=f/np.linalg.norm(f,axis=1)[:,None]; hu=h/np.maximum(np.linalg.norm(h,axis=1),1e-9)[:,None]
cos=(fu*hu).sum(1); print('cos unweighted: median',np.median(cos),'p5',np.percentile(cos,5))
print('FH length distribution (weighted y,z):',np.percentile(hl,[0,5,50,95,100]))
print('example rows (FM4 f | FH h):'); 
for i in range(0,len(FB),len(FB)//10): print(f[i],h[i],'ratio',h[i]/np.where(f[i]==0,1,f[i]))
