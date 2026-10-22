import cb, sys, numpy as np, collections, glob, os
S=os.path.dirname(os.path.abspath(__file__))
def load(p): return cb.parse(os.path.join(S,p))
# 1. which sections carry a damage table (B) with stride, lod0 file
a,da=load('fm4/toy_suprarz_98_lod0.carbin'); b,db=load('fh1/TOY_SupraRZ_98_lod0.carbin')
def dmg(o): return {s['name']:(s['vc0'],s['B'][0],s['B'][1],s['A'][0],s['A'][1]) for s in o['secs']}
da_,db_=dmg(a),dmg(b)
print('lod0 file: sections with damage table (B count==vc0, stride):')
fm=[n for n,v in da_.items() if v[1]==v[0] and v[0]>0]; fh=[n for n,v in db_.items() if v[1]==v[0] and v[0]>0]
print(' FM4',len(fm),'of',len(da_),' FH',len(fh),'of',len(db_))
print(' deformable in FM4 only:',[n for n in fm if n not in fh]); print(' deformable in FH only:',[n for n in fh if n not in fm])
print(' strides FM4',collections.Counter(v[2] for v in da_.values()),'FH',collections.Counter(v[2] for v in db_.values()))
print(' gapA/gapB FM4',collections.Counter((s['gapA'].hex()) for s in a['secs']),'FH',collections.Counter((s['gapA'].hex(),s['gapB'].hex()) for s in b['secs']))
# 2. base carbin
a2,da2=load('fm4/toy_suprarz_98.carbin'); b2,db2=load('fh1/TOY_SupraRZ_98.carbin')
print('base: type',a2['typeId'],b2['typeId'],'nsec',a2['nsec'],b2['nsec'],'lists FM4',a2['lists'],'FH',b2['lists'])
print(' lodVS FM4',collections.Counter(s['lodVS'] for s in a2['secs']),'FH',collections.Counter(s['lodVS'] for s in b2['secs']))
print(' A stride FM4',collections.Counter(s['A'][1] for s in a2['secs']),'FH',collections.Counter(s['A'][1] for s in b2['secs']))
# 3. stripped carbin
try:
    c,dc=load('fh1/stripped_toy_suprarz_98.carbin'); print('stripped base: type',c['typeId'],'nsec',c['nsec'],'parsed end',c['end'],'/',c['size'])
    print(' first sections',[(s['name'],s['lodVC'],s['lodVS'],s['vc0'],len(s['subs'])) for s in c['secs'][:6]])
except Exception as e: print('stripped parse failed',type(e).__name__,e)
# 4. sign pattern of FM4 vs FH word
def sx(v,bits): v=v&((1<<bits)-1); return np.where(v>=1<<(bits-1),v-(1<<bits),v).astype(float)
parts=['bumperRb','bumperRa','wingf','bumperRc','bumperRj','wingi']
F=[];H=[];P=[]
for p in parts:
    s=[x for x in a['secs'] if x['name']==p][0]; t=[x for x in b['secs'] if x['name']==p][0]
    n=s['vc0']
    F.append(np.frombuffer(da,'>u4',n,s['B'][2]).astype(np.int64)); H.append(np.frombuffer(db,'>u4',n,t['B'][2]).astype(np.int64))
    pos=np.frombuffer(da,'>i2',n*16,s['v0off']).reshape(n,16)[:,:4].astype(float)  # not exact, only for sign test
    P.append(pos[:,:3]/32767*pos[:,3:4]/32767)
F=np.concatenate(F);H=np.concatenate(H);P=np.concatenate(P)
f=np.stack([sx(F>>21,11),sx(F>>11,10),sx(F,11)],1); h=np.stack([sx(H>>21,11),sx(H>>10,11),sx(H,10)],1)
hn=np.linalg.norm(h,axis=1); ok=hn>20
cos=(f[ok]/np.linalg.norm(f[ok],axis=1)[:,None]*h[ok]/hn[ok][:,None]).sum(1)
print('vertices with |h|>20:',ok.sum(),'of',len(F),'; cos>0.9:',(cos>0.9).mean().round(3),' cos<-0.9:',(cos<-0.9).mean().round(3),' middle:',((abs(cos)<=0.9)).mean().round(3))
print(' FH h_x sign: negative fraction',(h[ok][:,0]<0).mean().round(3),' FM4 f_x sign negative fraction',(f[ok][:,0]<0).mean().round(3))
sg=np.sign(f[ok][:,0]*h[ok][:,0])
print(' sign(f_x*h_x)=+1 fraction',(sg>0).mean().round(3))
