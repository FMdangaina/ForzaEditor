import struct,sys,re,glob,os
def parse(p):
    b=open(p,'rb').read(); u=lambda o:struct.unpack_from('<I',b,o)[0]
    h=dict(size=len(b),hdr=u(0x10),fmt=u(0x14),ver=u(0x20),fsize=u(0x24),crc=u(0x28),secoff=u(0x2c),nsec=u(0x30),typ=(u(0x34),u(0x38)),root=(u(0x3c),u(0x40)),tag=u(0x44))
    base=0x20+h['secoff']; secs=[]
    for i in range(h['nsec']):
        o=base+i*44; f=struct.unpack_from('<11I',b,o); secs.append(dict(comp=f[0],off=f[1],size=f[2],exp=f[3],al=f[4],f16=f[5],f8=f[6],reloc=(f[7],f[8]),marsh=(f[9],f[10])))
    h['secs']=secs; return b,h
if __name__=='__main__':
    for p in sys.argv[1:]:
        b,h=parse(p); print(os.path.basename(p),{k:(hex(v) if isinstance(v,int) and k not in('size','nsec','ver') else v) for k,v in h.items() if k!='secs'})
        for i,s in enumerate(h['secs']): print('  sec',i,{k:v for k,v in s.items()})
