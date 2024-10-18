using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Eden.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Tasks tasks;
        public MainWindow()
        {
            InitializeComponent();
            tasks = new Tasks(info);
            Title += $" {System.Reflection.Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString(3)}";
        }

        private async void BtnExtractAPK_Click(object sender, RoutedEventArgs e)
        {
            ControlPanel.IsEnabled = false;
            await Task.Run(tasks.ExtractAPK);
            ControlPanel.IsEnabled = true;
        }

        private async void BtnUnPack_Click(object sender, RoutedEventArgs e)
        {
            ControlPanel.IsEnabled = false;
            await tasks.SlowUnpackDex(dex2jar_OutputDataReceived);
            ControlPanel.IsEnabled = true;
        }

        private async void BtnExtractClasses_Click(object sender, RoutedEventArgs e)
        {
            ControlPanel.IsEnabled = false;
            await Task.Run(tasks.SlowExtractClasses);
            ControlPanel.IsEnabled = true;
        }


        private async void BtnQuickUnPack_Click(object sender, RoutedEventArgs e)
        {
            ControlPanel.IsEnabled = false;
            await tasks.FastUnpackDex(dex2jarPartly_OutputDataReceived);
            ControlPanel.IsEnabled = true;
        }
        private async void BtnDecompile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button) return;
            ControlPanel.IsEnabled = false;
            await tasks.decompile(procyon_OutputDataReceived);
            ControlPanel.IsEnabled = true;
        }

        private async void BtnReadCode_Click(object sender, RoutedEventArgs e)
        {
            ControlPanel.IsEnabled = false;
            bool readFromAndroidManifest = CheckReadFromXML.IsChecked ?? false;
            await Task.Run(() => CodeReader.Run(info, "decompile", readFromAndroidManifest));
            ControlPanel.IsEnabled = true;
        }

        private void BtnExportLog_Click(object sender, RoutedEventArgs e)
        {
            File.WriteAllText("eden.log", logBox.Text, Encoding.UTF8);
            MessageBox.Show("日志已保存");
        }

        private void dex2jar_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                info($"(dex2jar) {e.Data}");
            }
        }
        private void dex2jarPartly_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                info($"(dex2jar-partly) {e.Data}");
            }
        }
        private void procyon_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                info($"(procyon) {e.Data}");
            }
        }

        private void info(string s)
        {
            Dispatcher.Invoke(() =>
            {
                logBox.Text += $"{DateTime.Now.ToString("[HH:mm:ss]")} {s}\n";
                logBox.ScrollToEnd();
            });
        }

        private void Image_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start("explorer", "https://github.com/MrXiaoM/Eden");
        }
    }
}
