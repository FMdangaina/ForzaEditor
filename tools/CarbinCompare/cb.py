import struct, sys
class R:
    def __init__(s,d): s.d=d; s.p=0
    def u32(s): v,=struct.unpack_from('>I',s.d,s.p); s.p+=4; return v
    def i32(s): v,=struct.unpack_from('>i',s.d,s.p); s.p+=4; return v
    def f32(s): v,=struct.unpack_from('>f',s.d,s.p); s.p+=4; return v
    def u8(s): v=s.d[s.p]; s.p+=1; return v
    def ascii(s,n): v=s.d[s.p:s.p+n].decode('latin1'); s.p+=n; return v

def parse(path):
    """Parses FM4 (typeId 1/2/3) and Horizon (typeId 5) carbins; returns dict with header spans and sections."""
    d=open(path,'rb').read(); r=R(d)
    o={'size':len(d),'spans':{}}
    t=r.u32(); o['typeId']=t
    if t==2:   # FM4 lod0/base
        r.p+=0x15c; n=r.u32(); o['hdr_n140']=n; r.p+=4; a=r.p; r.p+=n*140; o['spans']['tbl140']=(a,r.p); a=r.p; r.p+=0x340; o['spans']['blk340']=(a,r.p)
    elif t==5: # Horizon
        a=r.p; r.p+=0x150; o['spans']['blk150']=(a,r.p); n0=r.u32(); o['hdr_n20']=n0; a=r.p; r.p+=n0*0x20; o['spans']['tbl20']=(a,r.p); r.p+=12
        n=r.u32(); o['hdr_n140']=n; r.p+=4; a=r.p; r.p+=n*140; o['spans']['tbl140']=(a,r.p); a=r.p; r.p+=0x340; o['spans']['blk340']=(a,r.p)
    elif t==3:
        r.p+=920
    elif t==1:
        r.p+=860; n=r.u32(); r.p+=n*4; r.p+=8
    if t in (2,5,3):
        nl=r.u32()*2; o['nlists']=nl; a=r.p; L=[]
        for _ in range(nl):
            m=r.u32(); L.append(m); r.p+=m*4
        o['lists']=L; o['spans']['lists']=(a,r.p); r.p+=4
    ns=r.u32(); o['nsec']=ns; o['hdr_end']=r.p; secs=[]
    for si in range(ns):
        s={'start':r.p}
        n=r.u32()
        if n not in (0x2c9,2,5): r.p-=4; s['lead']=None
        else: s['lead']=n
        s['offset']=[r.f32() for _ in range(3)]
        s['tmin']=[r.f32() for _ in range(3)]
        s['tmax']=[r.f32() for _ in range(3)]
        s['pre28']=d[r.p:r.p+0x1c]; r.p+=0x1c
        n=r.u32(); s['n16']=n; r.p+=n*0x10; s['u4a']=r.u32(); n=r.u32(); s['n2']=n; r.p+=n*2
        s['pad4']=r.u32(); s['vb1']=r.u32(); s['vb2']=r.u32()
        s['name']=r.ascii(r.u8()); s['after_name']=r.u32()
        s['lodVC']=r.i32(); s['lodVS']=r.u32()
        if s['lodVS']==0x1c: s['lodpre8']=d[r.p:r.p+8]; r.p+=8
        s['lodoff']=r.p; r.p+=s['lodVC']*s['lodVS']
        r.p+=4
        if t==5 and s['lodVS']!=0x1c: r.p+=4
        nss=r.u32(); subs=[]
        for k in range(nss):
            ss={'start':r.p}; r.p+=5
            ss['uv']=[r.f32() for _ in range(8)]; r.p+=0x24
            ss['name']=r.ascii(r.i32()); ss['lod']=r.i32(); ss['itype']=r.u32()
            r.p+=4*6+4*8
            mk=r.u32(); ss['mk']=mk
            if t==5:
                sc=r.u32(); ss['strip']=sc
                if sc==2: r.p+=8; cnt=0; sz=0
                else: cnt=r.i32(); sz=r.i32()
            else:
                cnt=r.i32(); sz=r.i32()
            ss['icount']=cnt; ss['isize']=sz; ss['ioff']=r.p
            import os
            if os.environ.get('CBDBG'): print('sub',ss['name'],ss['lod'],ss['itype'],mk,cnt,sz,d[r.p-24:r.p+16].hex(),file=sys.stderr)
            r.p+=cnt*sz; r.p+=4; subs.append(ss)
        s['subs']=subs
        r.p+=4
        s['vc0']=r.i32(); s['vs0']=r.u32()
        if s['vs0']==0x1c: s['v0pre8']=d[r.p:r.p+8]; r.p+=8
        s['v0off']=r.p; r.p+=s['vc0']*s['vs0']
        s['tail_start']=r.p
        import os
        if os.environ.get('CBDBG'): print('tail',r.p,s['name'],s['vc0'],s['vs0'],d[r.p-8:r.p+64].hex(),file=sys.stderr)
        if t==5:
            r.p+=9
            if s['lodVS']==0x1c: r.p+=4
            a=r.u32(); b=r.u32(); fl=r.u32(); s['Aid']=r.u32() if fl else None; s['A']=(a,b,r.p); r.p+=a*b; s['gapA']=d[r.p:r.p+4]; r.p+=4
            a=r.u32(); b=r.u32(); fl=r.u32(); s['Bid']=r.u32() if fl else None; s['B']=(a,b,r.p); r.p+=a*b; s['gapB']=b''
        else:
            r.p+=9
            a=r.u32(); b=r.u32(); s['A']=(a,b,r.p); r.p+=a*b; s['gapA']=d[r.p:r.p+4]; r.p+=4
            a=r.u32(); b=r.u32(); s['B']=(a,b,r.p); r.p+=a*b
        s['end']=r.p
        secs.append(s)
    o['secs']=secs; o['end']=r.p
    return o,d

if __name__=='__main__':
    o,d=parse(sys.argv[1])
    print({k:v for k,v in o.items() if k not in ('secs','lists')})
    for s in o['secs'][:int(sys.argv[2]) if len(sys.argv)>2 else 5]:
        print(s['name'],'lod',s['lodVC'],s['lodVS'],'v0',s['vc0'],s['vs0'],'A',s['A'][:2],'B',s['B'][:2],'gapA',s['gapA'].hex(),'vb',s['vb1'],s['vb2'])


