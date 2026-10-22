import cb, sys, numpy as np
a,da=cb.parse(sys.argv[1]); b,db=cb.parse(sys.argv[2]); part=sys.argv[3]
s=[x for x in a['secs'] if x['name']==part][0]; t=[x for x in b['secs'] if x['name']==part][0]
n=s['vc0']; assert n==t['vc0']
V=np.frombuffer(da,dtype=np.uint8,count=n*32,offset=s['v0off']).reshape(n,32)
W=np.frombuffer(db,dtype=np.uint8,count=n*28,offset=t['v0off']).reshape(n,28)
FB=np.frombuffer(da,dtype=np.uint8,count=n*4,offset=s['B'][2]).reshape(n,4)
HB=np.frombuffer(db,dtype=np.uint8,count=n*4,offset=t['B'][2]).reshape(n,4)
fm=np.hstack([V[:,24:32],FB]).astype(float); fh=np.hstack([W[:,24:28],HB]).astype(float)
fn=['F%d'%i for i in range(8)]+['FB%d'%i for i in range(4)]; hn=['H%d'%i for i in range(4)]+['HB%d'%i for i in range(4)]
print('      '+' '.join('%6s'%h for h in hn))
for i,x in enumerate(fn):
    row=[np.corrcoef(fm[:,i],fh[:,j])[0,1] if fm[:,i].std()>0 and fh[:,j].std()>0 else 0 for j in range(8)]
    print('%5s '%x+' '.join('%6.2f'%r for r in row))
# relations to geometry: correlation of tail bytes with position / qtangent / uv bytes
geo=V[:,0:24].astype(float)
gn=['pos%d'%i for i in range(8)]+['uv0_%d'%i for i in range(4)]+['uv1_%d'%i for i in range(4)]+['qt%d'%i for i in range(8)]
print('FM4 tail words vs geometry bytes (max |corr|):')
for i,x in enumerate(fn):
    best=sorted([(abs(np.corrcoef(fm[:,i],geo[:,j])[0,1]) if geo[:,j].std()>0 and fm[:,i].std()>0 else 0,gn[j]) for j in range(24)],reverse=True)[:2]
    print(x,[(round(c,2),g) for c,g in best])
geo=W[:,0:24].astype(float)
print('FH tail bytes vs geometry:')
for i,x in enumerate(hn):
    best=sorted([(abs(np.corrcoef(fh[:,i],geo[:,j])[0,1]) if geo[:,j].std()>0 and fh[:,i].std()>0 else 0,gn[j]) for j in range(24)],reverse=True)[:2]
    print(x,[(round(c,2),g) for c,g in best])
