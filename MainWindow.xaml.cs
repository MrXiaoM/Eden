using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Eden
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void BtnUnPack_Click(object sender, RoutedEventArgs e)
        {
            ControlPanel.IsEnabled = false;

            var files = new List<string>();
            var di = new DirectoryInfo("apk");
            foreach (FileInfo file in di.GetFiles("*.dex")) {
                files.Add(file.Name);
            }
            files.Sort();
            info("正在执行 dex 转 jar: " + string.Join(", ", files) + "\n");

            var workingDir = Environment.CurrentDirectory;
            int i = 1;
            foreach (string file in files)
            {
                var command = $@"""tools\d2j-dex2jar"" apk\{file} -o cache\{file[..^4]}.jar";
                info("(dex2jar) .");
                info($"(dex2jar) 开始转换 {i}/{files.Count} {command}");
                var process = new Process
                {
                    StartInfo = new("cmd.exe")
                    {
                        Arguments = $"/C {command}",
                        WorkingDirectory = workingDir,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.OutputDataReceived += dex2jar_OutputDataReceived;
                process.ErrorDataReceived += dex2jar_OutputDataReceived;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();
                info($"(dex2jar) 完成！(ExitCode: {process.ExitCode})");
                info("(提取类) 正在打开压缩包…");
                await Task.Run(() =>
                {
                    try
                    {
                        using (var archive = ZipFile.OpenRead($"cache\\{file[..^4]}.jar"))
                        {
                            extractClasses(archive);
                        }
                        info("(提取类) 完成");
                    }
                    catch (Exception e)
                    {
                        info($"(提取类) 打开压缩包失败: {e.Message}");
                    }
                });
                i++;
            }
            info(".");
            info("dex 转 jar 执行完毕");

            ControlPanel.IsEnabled = true;
        }

        private async void BtnExtractClasses_Click(object sender, RoutedEventArgs e)
        {
            ControlPanel.IsEnabled = false;
            await Task.Run(() =>
            {
                var files = new List<string>();
                var di = new DirectoryInfo("cache");
                foreach (FileInfo file in di.GetFiles("*.jar"))
                {
                    files.Add(file.Name);
                }
                files.Sort();

                int i = 1;
                foreach (string file in files)
                {
                    info($"(提取类) 正在打开压缩包 {i}/{files.Count} {file}");
                    try
                    {
                        using (var archive = ZipFile.OpenRead($"cache\\{file}"))
                        {
                            extractClasses(archive);
                        }
                        info("(提取类) 完成");
                    }
                    catch (Exception e)
                    {
                        info($"(提取类) 打开压缩包失败: {e.Message}");
                    }
                    i++;
                }
            });
            ControlPanel.IsEnabled = true;
        }

        private void extractClasses(ZipArchive archive)
        {
            var extract = (string entryName) =>
            {
                var entry = archive.GetEntry(entryName);
                if (entry != null)
                {
                    FileInfo fi = new FileInfo("classes/" + entry.FullName);
                    if (!(fi.Directory?.Exists ?? false)) fi.Directory?.Create();
                    entry.ExtractToFile(fi.FullName, true);
                    info($"(提取类) 已导出 {entry.FullName}");
                }
            };
            extract("com/tencent/mobileqq/dt/model/FEBound.class");
            extract("com/tencent/common/config/AppSetting.class");
            extract("oicq/wlogin_sdk/report/event/EventConstant.class");
            extract("oicq/wlogin_sdk/report/event/EventConstant$EventParams.class");
            extract("oicq/wlogin_sdk/report/event/EventConstant$EventType.class");
            extract("oicq/wlogin_sdk/tools/util.class");
            extract("oicq/wlogin_sdk/request/WtloginHelper.class");
            extract("cooperation/qzone/QUA.class");
        }

        private async void BtnDecompile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button) return;
            ControlPanel.IsEnabled = false;
            info("(procyon) 开始反编译");
            await decompile("classes",
                "com.tencent.mobileqq.dt.model.FEBound",
                "com.tencent.common.config.AppSetting",
                "oicq.wlogin_sdk.report.event.EventConstant",
                "oicq.wlogin_sdk.tools.util",
                "oicq.wlogin_sdk.request.WtloginHelper",
                "cooperation.qzone.QUA"
            );
            info("(procyon) 反编译结束");

            ControlPanel.IsEnabled = true;
        }

        private async void BtnReadCode_Click(object sender, RoutedEventArgs e)
        {
            ControlPanel.IsEnabled = false;
            await Task.Run(() => CodeReader.Run(info, "decompile"));
            ControlPanel.IsEnabled = true;
        }

        private void BtnExportLog_Click(object sender, RoutedEventArgs e)
        {
            File.WriteAllText("eden.log", logBox.Text, Encoding.UTF8);
            MessageBox.Show("日志已保存");
        }


        /// <summary>
        /// 反编译 class 文件到 decompile 文件夹
        /// </summary>
        /// <param name="classes">class 文件路径列表，可用相对路径</param>
        /// <returns>Procyon 退出码</returns>
        public async Task<int> decompile(string baseDir, params string[] classes)
        {
            var files = new List<string>();
            foreach (string s in classes)
            {
                files.Add($"{baseDir}\\{s.Replace(".", "\\")}.class");
            }
            var command = $@"""tools\procyon"" {string.Join(" ", files)} -o decompile";
            var process = new Process
            {
                StartInfo = new("cmd.exe")
                {
                    Arguments = $"/C {command}",
                    WorkingDirectory = Environment.CurrentDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.OutputDataReceived += procyon_OutputDataReceived;
            process.ErrorDataReceived += procyon_OutputDataReceived;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();
            return process.ExitCode;
        }

        private void dex2jar_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                info($"(dex2jar) {e.Data}");
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
    }
}