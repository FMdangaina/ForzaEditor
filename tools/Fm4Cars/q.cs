using System;
using System.IO;
using System.Reflection;
using ForzaStudio;
class Q { static int Main(string[] a) {
 string exe = @"C:\Users\Dan\Desktop\ForzaEditor\bin\Debug\ForzaEditor.exe";
 AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, ResolveEventArgs e){ string p=Path.Combine(Path.GetDirectoryName(exe), new AssemblyName(e.Name).Name+".dll"); return File.Exists(p)?Assembly.LoadFrom(p):null; };
 return Go(a); }
 static int Go(string[] a) {
 using (SqliteDatabase db = SqliteDatabase.FromBytes(File.ReadAllBytes(@"F:\Xenia\Games\FM4_Extracted\Media\db\gamedb.slt")))
 for (int i = 0; i < a.Length; i++) {
  SqliteDatabase.Result r = db.Query(a[i]);
  Console.WriteLine("-- " + a[i]); Console.WriteLine(string.Join(" | ", r.Columns));
  foreach (object[] row in r.Rows) { string[] c = new string[row.Length]; for (int k=0;k<c.Length;k++) c[k]=Convert.ToString(row[k], System.Globalization.CultureInfo.InvariantCulture); Console.WriteLine(string.Join(" | ", c)); }
 } return 0; } }
