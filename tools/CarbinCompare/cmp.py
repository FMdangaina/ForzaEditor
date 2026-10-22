import cb, sys, struct, collections
fm4f, fhf = sys.argv[1], sys.argv[2]
a,da=cb.parse(fm4f); b,db=cb.parse(fhf)
print('FM4',fm4f.split('\\')[-1],'type',a['typeId'],'size',a['size'],'nsec',a['nsec'],'| FH type',b['typeId'],'size',b['size'],'nsec',b['nsec'])
# header blocks
for k in ('blk340','lists'):
    sa=a['spans'].get(k); sb=b['spans'].get(k)
    x=da[sa[0]:sa[1]]; y=db[sb[0]:sb[1]]
    print('hdr',k,'len',len(x),len(y),'equal' if x==y else 'DIFF (%d bytes differ)'%sum(1 for i in range(min(len(x),len(y))) if x[i]!=y[i]))
print('FM4 lists',a['lists'],' FH lists',b['lists'])
# FM4 header before sections vs FH: print raw first block words
def words(d,s,e): return [struct.unpack_from('>I',d,i)[0] for i in range(s,e,4)]
print('FM4 first 0x15c words nonzero:',[(i*4+4,hex(w)) for i,w in enumerate(words(da,4,4+0x15c)) if w][:40])
print('FH  first 0x150 words nonzero:',[(i*4+4,hex(w)) for i,w in enumerate(words(db,4,4+0x150)) if w][:40])
na={s['name']:s for s in a['secs']}; nb={s['name']:s for s in b['secs']}
print('only FM4:',[n for n in na if n not in nb]); print('only FH :',[n for n in nb if n not in na])
fields=['lead','offset','tmin','tmax','pre28','n16','u4a','n2','pad4','vb1','vb2','after_name','lodVC','lodVS','vc0','vs0']
diffc=collections.Counter(); ex={}
for n in na:
    if n not in nb: continue
    s,t=na[n],nb[n]
    for f in fields:
        if s.get(f)!=t.get(f):
            diffc[f]+=1; ex.setdefault(f,[]).append((n,s.get(f),t.get(f)))
print('section field differences (count of',len([n for n in na if n in nb]),'common sections):')
for f,c in diffc.items(): print(' ',f,c,[(n,(x.hex() if isinstance(x,bytes) else x),(y.hex() if isinstance(y,bytes) else y)) for n,x,y in ex[f][:2]])
# subsections
sd=collections.Counter(); sex={}
for n in na:
    if n not in nb: continue
    A=na[n]['subs']; B=nb[n]['subs']
    if [x['name'] for x in A]!=[x['name'] for x in B]: sd['subnames']+=1; sex.setdefault('subnames',[]).append((n,[x['name'] for x in A],[x['name'] for x in B]))
    for x,y in zip(A,B):
        for f in ('uv','lod','itype','icount','isize'):
            if x[f]!=y[f]: sd[f]+=1; sex.setdefault(f,[]).append((n,x['name'],x[f],y[f]))
print('subsection differences:')
for f,c in sd.items(): print(' ',f,c,sex[f][:2])
