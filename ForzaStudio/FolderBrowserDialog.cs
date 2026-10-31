using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ForzaStudio
{
    // The folder chooser of the app: the Windows file dialog in folder mode (address bar, a box to paste or type a
    // path, quick access, search), not the old tree. It is named like the WinForms one and lives in this namespace,
    // so every 'new FolderBrowserDialog()' of the app gets it with the same members. Where the dialog cannot be
    // created the WinForms tree answers instead.
    public sealed class FolderBrowserDialog : IDisposable
    {
        public string Description = "";
        public string SelectedPath = "";
        public bool ShowNewFolderButton = true;

        public DialogResult ShowDialog()
        {
            return this.ShowDialog(null);
        }

        public DialogResult ShowDialog(IWin32Window owner)
        {
            IntPtr handle = (owner != null) ? owner.Handle : IntPtr.Zero;
            try
            {
                return this.ShowModern(handle);
            }
            catch (COMException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
            catch (DllNotFoundException)
            {
            }
            using (System.Windows.Forms.FolderBrowserDialog old = new System.Windows.Forms.FolderBrowserDialog())
            {
                old.Description = this.Description;
                old.SelectedPath = this.SelectedPath;
                old.ShowNewFolderButton = this.ShowNewFolderButton;
                DialogResult result = old.ShowDialog(owner);
                if (result == DialogResult.OK)
                {
                    this.SelectedPath = old.SelectedPath;
                }
                return result;
            }
        }

        public void Dispose()
        {
        }

        private DialogResult ShowModern(IntPtr owner)
        {
            IFileOpenDialog dialog = (IFileOpenDialog) new FileOpenDialogCom();
            try
            {
                uint options;
                dialog.GetOptions(out options);
                dialog.SetOptions(options | FosPickFolders | FosForceFileSystem | FosPathMustExist);
                if (!string.IsNullOrEmpty(this.Description))
                {
                    dialog.SetTitle(this.Description);
                }
                if (!string.IsNullOrEmpty(this.SelectedPath))
                {
                    string start = this.SelectedPath;
                    while (start.Length > 0 && !Directory.Exists(start))
                    {
                        start = Path.GetDirectoryName(start);
                        if (start == null)
                        {
                            start = "";
                        }
                    }
                    IShellItem item;
                    if (start.Length > 0 && SHCreateItemFromParsingName(start, IntPtr.Zero, ref ShellItemId, out item) == 0)
                    {
                        dialog.SetFolder(item);
                        Marshal.ReleaseComObject(item);
                    }
                }
                int hr = dialog.Show(owner);
                if (hr == unchecked((int) 0x800704C7))
                {
                    return DialogResult.Cancel;
                }
                if (hr != 0)
                {
                    throw new COMException("The folder dialog failed", hr);
                }
                IShellItem result;
                dialog.GetResult(out result);
                IntPtr text;
                result.GetDisplayName(SigdnFileSysPath, out text);
                this.SelectedPath = Marshal.PtrToStringUni(text);
                Marshal.FreeCoTaskMem(text);
                Marshal.ReleaseComObject(result);
                return DialogResult.OK;
            }
            finally
            {
                Marshal.ReleaseComObject(dialog);
            }
        }

        private const uint FosPickFolders = 0x20;
        private const uint FosForceFileSystem = 0x40;
        private const uint FosPathMustExist = 0x800;
        private const uint SigdnFileSysPath = 0x80058000;
        private static Guid ShellItemId = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid riid, out IShellItem item);

        [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        private class FileOpenDialogCom
        {
        }

        [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr owner);
            void SetFileTypes(uint count, IntPtr types);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem item);
            void SetFolder(IShellItem item);
            void GetFolder(out IShellItem item);
            void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            void GetResult(out IShellItem item);
        }

        [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr context, ref Guid handler, ref Guid riid, out IntPtr result);
            void GetParent(out IShellItem item);
            void GetDisplayName(uint form, out IntPtr name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem other, uint hint, out int order);
        }
    }
}
