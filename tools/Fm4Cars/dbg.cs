using System;
using System.IO;
using System.Reflection;
using ForzaStudio;
class D { static int Main() {
 string exe = @"C:\Users\Dan\Desktop\ForzaEditor\bin\Debug\ForzaEditor.exe";
 AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, ResolveEventArgs e){ string p=Path.Combine(Path.GetDirectoryName(exe), new AssemblyName(e.Name).Name+".dll"); return File.Exists(p)?Assembly.LoadFrom(p):null; };
 return Go(); }
 static int Go() {
 foreach (InstalledDlc d in InstalledDlc.Scan(ForzaGame.Motorsport4, @"F:\Xenia\Emulators\Xenia Canary\content\0000000000000000\4D530910\00000002", null)) Console.WriteLine(d.Name+" | merge="+(d.Merge!=null)+" | "+d.Problem);
 using (SqliteDatabase db = SqliteDatabase.FromBytes(File.ReadAllBytes(@"F:\Xenia\Games\FM4_Extracted_DEV\Media\db\gamedb.slt"))) Console.WriteLine(db.Query("SELECT DisplayName FROM Data_Car LIMIT 2").Rows[0][0]);
 return 0; } }
