using System; using System.IO; using System.Collections.Generic; using ForzaStudio;
class Q {
  static string H(byte[] b){ return b==null?"null":BitConverter.ToString(b).Replace("-",""); }
  static void Main(string[] a) {
    var f = new CarbinModel(File.ReadAllBytes(a[0])); var s = new CarbinModel(File.ReadAllBytes(a[1]));
    Console.WriteLine("sections full {0} stripped {1}", f.Sections.Count, s.Sections.Count);
    var map = new Dictionary<string, CarbinModel.Section>(); foreach (var x in f.Sections) map[x.Name] = x;
    var diffs = new Dictionary<string,int>(); int n=0;
    foreach (var t in s.Sections) {
      CarbinModel.Section x; if (!map.TryGetValue(t.Name, out x)) { Console.WriteLine("only in stripped: " + t.Name); continue; }
      n++;
      Action<string,string,string> c = (k,u,v) => { if (u!=v) { int d; diffs.TryGetValue(k, out d); diffs[k]=d+1; if (d<2) Console.WriteLine("  {0} {1}: full={2} stripped={3}", t.Name, k, u.Length>60?u.Substring(0,60):u, v.Length>60?v.Substring(0,60):v);} };
      c("Marker",H(x.Marker),H(t.Marker)); c("Bounds",string.Join(",",x.Bounds),string.Join(",",t.Bounds));
      c("Unk1c",H(x.Unk1c),H(t.Unk1c)); c("ACount",""+x.ACount,""+t.ACount); c("A",H(x.A),H(t.A)); c("AfterA",H(x.AfterA),H(t.AfterA));
      c("BCount",""+x.BCount,""+t.BCount); c("B",H(x.B),H(t.B)); c("Pre",H(x.Pre),H(t.Pre));
      c("VbTest",x.VbTest1+"/"+x.VbTest2,t.VbTest1+"/"+t.VbTest2); c("PostName",H(x.PostName),H(t.PostName));
      c("Lod.Count/Stride",x.Lod.Count+"/"+x.Lod.Stride,t.Lod.Count+"/"+t.Lod.Stride); c("Lod0.Count/Stride",x.Lod0.Count+"/"+x.Lod0.Stride,t.Lod0.Count+"/"+t.Lod0.Stride);
      c("AfterLod",H(x.AfterLod),H(t.AfterLod)); c("AfterSubs",H(x.AfterSubs),H(t.AfterSubs)); c("Post9",H(x.Post9),H(t.Post9)); c("Mid",H(x.Mid),H(t.Mid));
      c("DmgLod.Count/Stride",x.DamageLod.Count+"/"+x.DamageLod.Stride,t.DamageLod.Count+"/"+t.DamageLod.Stride);
      c("DmgLod0.Count/Stride",x.DamageLod0.Count+"/"+x.DamageLod0.Stride,t.DamageLod0.Count+"/"+t.DamageLod0.Stride);
      c("SubCount",""+x.SubSections.Count,""+t.SubSections.Count);
      for (int i=0;i<Math.Min(x.SubSections.Count,t.SubSections.Count);i++){ var p=x.SubSections[i]; var q=t.SubSections[i];
        c("Sub.Name",p.Name,q.Name); c("Sub.Uv",string.Join(",",p.Uv),string.Join(",",q.Uv)); c("Sub.Fixed36",H(p.Fixed36),H(q.Fixed36)); c("Sub.Fixed56",H(p.Fixed56),H(q.Fixed56));
        c("Sub.Lod/IndexType",p.Lod+"/"+p.IndexType,q.Lod+"/"+q.IndexType); c("Sub.Tail",H(p.Tail),H(q.Tail));
        c("Sub.IndexCount", ""+p.Indices.Count, q.Stripped==null?"nostrip":""+(q.Stripped[0]*16777216L+q.Stripped[1]*65536L+q.Stripped[2]*256L+q.Stripped[3])); }
    }
    Console.WriteLine("compared {0}; differing fields:", n); foreach (var k in diffs) Console.WriteLine("  {0}: {1} sections", k.Key, k.Value);
  }
}
