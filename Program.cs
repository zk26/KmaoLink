using System;
using System.Windows.Forms;

namespace KmaoLink
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            
            // 检查是否已有实例运行
            using var mutex = new System.Threading.Mutex(true, "KmaoLink_SingleInstance", out bool isNewInstance);
            if (!isNewInstance)
            {
                MessageBox.Show("程序已在运行中，请检查系统托盘。", "KmaoLink", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.Run(new MainForm());
        }
    }
}
