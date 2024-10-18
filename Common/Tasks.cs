using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace Eden
{
    public class Tasks
    {
        Action<string> info;
        public Tasks(Action<string> info)
        {
            this.info = info;
        }

        public void ExtractAPK()
        {
            if (!File.Exists("Eden.apk"))
            {
                info("(解包) Eden.apk 不存在!");
                return;
            }
            if (Directory.Exists("apk"))
            {
                info("(解包) 正在删除目录 ./apk...");
                Directory.Delete("apk", true);
            }
            Directory.CreateDirectory("apk");
            info("(解包) 正在解压 Eden.apk...");
            ZipFile.ExtractToDirectory("Eden.apk", "apk");
            info("(解包) 解压完成!");
        }

        public async Task SlowUnpackDex(DataReceivedEventHandler received)
        {
            var files = new List<string>();
            var di = new DirectoryInfo("apk");
            foreach (FileInfo file in di.GetFiles("*.dex"))
            {
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
                process.OutputDataReceived += received;
                process.ErrorDataReceived += received;
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
        }

        public void SlowExtractClasses()
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

        public async Task FastUnpackDex(DataReceivedEventHandler received)
        {
            var classes = new string[] {
                "com/tencent/mobileqq/dt/model/FEBound",
                "com/tencent/common/config/AppSetting",
                "oicq/wlogin_sdk/report/event/EventConstant",
                "oicq/wlogin_sdk/report/event/EventConstant$EventParams",
                "oicq/wlogin_sdk/report/event/EventConstant$EventType",
                "oicq/wlogin_sdk/tools/util",
                "oicq/wlogin_sdk/request/WtloginHelper",
                "cooperation/qzone/QUA"
            };
            var files = new List<string>();
            var di = new DirectoryInfo("apk");
            foreach (FileInfo file in di.GetFiles("*.dex"))
            {
                files.Add(@$"apk\{file.Name}");
            }
            files.Sort();
            info("正在执行 dex 转 jar (快速): " + string.Join(", ", files) + "\n");

            var workingDir = Environment.CurrentDirectory;
            int i = 1;
            var command = $@"""tools\d2j-dex2jar-partly"" {string.Join(' ', files)} --classes {string.Join(' ', classes)} --output classes";
            info("(dex2jar-partly) .");
            info($"(dex2jar-partly) 开始转换");
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
            process.OutputDataReceived += received;
            process.ErrorDataReceived += received;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();
            info($"(dex2jar-partly) 完成！(ExitCode: {process.ExitCode})");
            info(".");
            info("dex 转 jar (快速) 执行完毕");
        }

        public async Task<int> decompile(DataReceivedEventHandler received)
        {
            return await decompile(received, "classes",
                "com.tencent.mobileqq.dt.model.FEBound",
                "com.tencent.common.config.AppSetting",
                "oicq.wlogin_sdk.report.event.EventConstant",
                "oicq.wlogin_sdk.tools.util",
                "oicq.wlogin_sdk.request.WtloginHelper",
                "cooperation.qzone.QUA"
            );
        }

        /// <summary>
        /// 反编译 class 文件到 decompile 文件夹
        /// </summary>
        /// <param name="classes">class 文件路径列表，可用相对路径</param>
        /// <returns>Procyon 退出码</returns>
        public async Task<int> decompile(DataReceivedEventHandler received, string baseDir, params string[] classes)
        {
            info("(procyon) 开始反编译");
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
            process.OutputDataReceived += received;
            process.ErrorDataReceived += received;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();
            info("(procyon) 反编译结束");
            return process.ExitCode;
        }
    }
}
