import numpy as np, os, struct
S=os.path.dirname(os.path.abspath(__file__))
def rd(p): return open(os.path.join(S,p),'rb').read()
for f4,fh in (('damage.xds','Damage.xds'),('damage_lod0.xds','damage_LOD0.xds'),('lights.xds','Lights.xds'),('lights_lod0.xds','lights_LOD0.xds')):
    x=np.frombuffer(rd('fm4/'+f4),np.uint8); y=np.frombuffer(rd('fh1/'+fh),np.uint8)
    d=np.nonzero(x!=y)[0]
    print(f4,len(x),len(y),'bytes differing',len(d),'(%.1f%%)'%(100*len(d)/len(x)),'first',d[:5],'header equal first 0x1000:',bool((x[:4096]==y[:4096]).all()))
c=rd('fh1/stripped_toy_suprarz_98.carbin'); print('stripped base head',c[:48].hex(),len(c))
c=rd('fh1/stripped_toy_suprarz_98_lod0.carbin'); print('stripped lod0 head',c[:48].hex(),len(c))
c=rd('fh1/physicsdefinition.bin'); print('physicsdefinition.bin',len(c),c[:48].hex())
for p in ('fm4/toy_suprarz_98_rotorlf_lod0.carbin','fh1/TOY_SupraRZ_98_rotorLF_LOD0.carbin'):
    c=rd(p); print(p.split('/')[-1],len(c),'head',c[:24].hex())
