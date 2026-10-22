import cb, sys, struct, collections
a,da=cb.parse(sys.argv[1]); b,db=cb.parse(sys.argv[2])
na={s['name']:s for s in a['secs']}; nb={s['name']:s for s in b['secs']}
tot=collections.Counter()
for n in na:
    if n not in nb: continue
    s,t=na[n],nb[n]
    if s['vc0']!=t['vc0'] or s['vc0']==0: continue
    c=s['vc0']
    # per-field equality, vertex by vertex (same order assumed)
    eq=collections.Counter()
    for i in range(c):
        v=da[s['v0off']+i*32:s['v0off']+(i+1)*32]; w=db[t['v0off']+i*28:t['v0off']+(i+1)*28]
        eq['pos']+= v[0:8]==w[0:8]; eq['uv0']+= v[8:12]==w[8:12]; eq['uv1']+= v[12:16]==w[12:16]; eq['qtan']+= v[16:24]==w[16:24]
        eq['tail4_a']+= v[24:28]==w[24:28]; eq['fm4_last4_vs_fh_last4']+= v[28:32]==w[24:28]
    # B arrays
    Bs=s['B'];Bt=t['B']
    for i in range(c):
        x=da[Bs[2]+i*4:Bs[2]+i*4+4]; y=db[Bt[2]+i*4:Bt[2]+i*4+4]
        eq['B']+= x==y
    # FM4 tail word vs B word
    for i in range(c):
        if da[s['v0off']+i*32+28:s['v0off']+i*32+32]==da[Bs[2]+i*4:Bs[2]+i*4+4]: eq['fm4_last4==fm4_B']+=1
        if db[t['v0off']+i*28+24:t['v0off']+i*28+28]==db[Bt[2]+i*4:Bt[2]+i*4+4]: eq['fh_last4==fh_B']+=1
    print(n,c,dict(eq))
    for k,v in eq.items(): tot[k]+=v
    tot['n']+=c
print('TOTAL',dict(tot))
