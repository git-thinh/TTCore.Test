using System;
using System.Windows.Forms;
namespace PDF2Image.FormPushUpload
{
    static class Program
    {
        public const string HOST = "http://localhost:8080";
        //public const string HOST = "http://localhost:29605";
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new fMain());
        }
    }
}
