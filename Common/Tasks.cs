using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace Eden
{
    public class Tasks
    {
        public static string currentDir
        {
            get
            {
                var s = Environment.CurrentDirectory.Replace("\\", "/");
                return s.EndsWith("/") ? s : (s + "/");
            }
        }
        bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public string workDir;
        public string edenApk = "Eden.apk";
        Action<string> info;
        public bool error = false;
        public Tasks(Action<string> info, string workDir)
        {
            this.info = info;
            this.workDir = workDir.EndsWith("/") ? workDir : (workDir + "/");
        }

        public void ExtractAPK()
        {
            error = false;
            if (!File.Exists(workDir + edenApk))
            {
                info($"(解包) {edenApk} 不存在!");
                error = true;
                return;
            }
            if (Directory.Exists(workDir + "apk"))
            {
                info("(解包) 正在删除目录 ./apk...");
                Directory.Delete(workDir + "apk", true);
            }
            Directory.CreateDirectory(workDir + "apk");
            info($"(解包) 正在解压 {edenApk}...");
            ZipFile.ExtractToDirectory(workDir + edenApk, workDir + "apk");
            info("(解包) 解压完成!");
        }

        public async Task SlowUnpackDex(DataReceivedEventHandler received)
        {
            error = false;
            var files = new List<string>();
            var di = new DirectoryInfo(workDir + "apk");
            foreach (FileInfo file in di.GetFiles("*.dex"))
            {
                files.Add(file.Name);
            }
            files.Sort();
            info("正在执行 dex 转 jar: " + string.Join(", ", files) + "\n");

            int i = 1;
            foreach (string file in files)
            {
                var command = $@"""{currentDir}tools/d2j-dex2jar{(isWin ? ".bat" : ".sh")}"" apk/{file} -o cache/{file[..^4]}.jar";
                info("(dex2jar) .");
                info($"(dex2jar) 开始转换 {i}/{files.Count} {command}");
                var process = new Process
                {
                    StartInfo = new(isWin ? "cmd.exe" : "sh")
                    {
                        Arguments = isWin ? $"/C {command}" : command,
                        WorkingDirectory = workDir,
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
                error = process.ExitCode != 0;
                info($"(dex2jar) 完成！(ExitCode: {process.ExitCode})");
                info("(提取类) 正在打开压缩包…");
                await Task.Run(() =>
                {
                    try
                    {
                        using (var archive = ZipFile.OpenRead(workDir + $"cache/{file[..^4]}.jar"))
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
            error = false;
            var files = new List<string>();
            var di = new DirectoryInfo(workDir + "cache");
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
                    using (var archive = ZipFile.OpenRead(workDir + $"cache/{file}"))
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
            error = false;
            var extract = (string entryName) =>
            {
                var entry = archive.GetEntry(entryName);
                if (entry != null)
                {
                    FileInfo fi = new FileInfo(workDir + "classes/" + entry.FullName);
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
            error = false;
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
            var di = new DirectoryInfo(workDir + "apk");
            foreach (FileInfo file in di.GetFiles("*.dex"))
            {
                files.Add(@$"apk/{file.Name}");
            }
            files.Sort();
            info("正在执行 dex 转 jar (快速): " + string.Join(", ", files) + "\n");

            var command = $@"""{currentDir}tools/d2j-dex2jar-partly{(isWin ? ".bat" : ".sh")}"" {string.Join(' ', files)} --classes {string.Join(' ', classes)} --output classes";
            info("(dex2jar-partly) .");
            info($"(dex2jar-partly) 开始转换");
            var process = new Process
            {
                StartInfo = new(isWin ? "cmd.exe" : "sh")
                {
                    Arguments = isWin ? $"/C {command}" : command,
                    WorkingDirectory = workDir,
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
            error = process.ExitCode != 0;
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
            error = false;
            info("(procyon) 开始反编译");
            var files = new List<string>();
            foreach (string s in classes)
            {
                files.Add($"{baseDir}/{s.Replace(".", "/")}.class");
            }
            var command = $@"""{currentDir}tools/procyon{(isWin ? ".bat" : ".sh")}"" {string.Join(" ", files)} -o decompile";
            var process = new Process
            {
                StartInfo = new(isWin ? "cmd.exe" : "sh")
                {
                    Arguments = isWin ? $"/C {command}" : command,
                    WorkingDirectory = workDir,
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
            info($"(procyon) 反编译结束 (ExitCode: {process.ExitCode})");
            error = process.ExitCode != 0;
            return process.ExitCode;
        }
    }
}
