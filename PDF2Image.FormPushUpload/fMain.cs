using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Windows.Forms;

namespace PDF2Image.FormPushUpload
{
    public partial class fMain : Form
    {
        public fMain()
        {
            InitializeComponent();
        }

        private void fMain_Load(object sender, EventArgs e)
        {
            this.Left = Screen.PrimaryScreen.WorkingArea.Width - 48;
            this.Top = Screen.PrimaryScreen.WorkingArea.Height - 48;
        }

        private void btnUpload_Click(object sender, EventArgs e)
        {
            var d = new OpenFileDialog
            {
                InitialDirectory = @"D:\",
                Title = "Browse Text Files",

                CheckFileExists = true,
                CheckPathExists = true,

                DefaultExt = "pdf",
                Filter = "All files (*.*)|*.*" +
                "|Pdf files (*.pdf)|*.pdf" +
                "|Txt files (*.txt)|*.txt" +
                "|Images (*.BMP;*.JPG;*.GIF,*.PNG,*.TIFF)|*.BMP;*.JPG;*.GIF;*.PNG;*.TIFF",
                FilterIndex = 2,
                RestoreDirectory = true,

                ReadOnlyChecked = true,
                ShowReadOnly = true,

                Multiselect = true
            };

            if (d.ShowDialog() == DialogResult.OK)
            {
                if (d.FileNames.Length > 0)
                {
                    string path = "";
                    var ls = new List<string>();
                    for(int i = 0; i < d.FileNames.Length;i++)
                    {
                        if (i == 0) path = Path.GetDirectoryName(d.FileNames[i]);
                        ls.Add(Path.GetFileName(d.FileNames[i]));
                    }
                    string url = "http://localhost:29605/local/push-files?path=" + path + "&files=" + string.Join("|", ls.ToArray());
                    try
                    {
                        new WebClient().DownloadString(url);
                    }
                    catch(Exception ex) {
                        MessageBox.Show(ex.Message + Environment.NewLine + url, "Push Files");
                    }
                }
            }
        }
    }
}
