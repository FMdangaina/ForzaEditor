import ctypes,sys,re,os
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from gr2sections import parse
dll=ctypes.CDLL('C:/Users/vmvision/Desktop/fm4_dev/ForzaTechStudio/ForzaTechStudio/Native/granny2_x64.dll')
dll.GrannyDecompressData.restype=ctypes.c_bool
dll.GrannyDecompressData.argtypes=[ctypes.c_int,ctypes.c_bool,ctypes.c_int,ctypes.c_char_p,ctypes.c_int,ctypes.c_int,ctypes.c_int,ctypes.c_char_p]
def sections(p):
    b,h=parse(p); out=[]
    for s in h['secs']:
        raw=b[s['off']:s['off']+s['size']]
        if s['comp']==0: out.append(raw); continue
        buf=ctypes.create_string_buffer(s['exp'])
        # stops: first16bit / first8bit offsets recorded in section header words 5,6
        ok=dll.GrannyDecompressData(s['comp'],False,len(raw),raw,s['f16'],s['f8'],s['exp'],buf) if s['exp'] else True
        out.append(buf.raw if s['exp'] else b'')
        if not ok: print('decompress failed',p,s)
    return b,h,out
