namespace ForzaStudio
{
    using System;
    using System.Windows.Forms;

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            TempCleanup.RunInBackground();
            Application.Run(new ArchiveEditorForm());
            TempCleanup.Run();
        }
    }
}

