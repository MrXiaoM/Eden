using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Eden
{
    /// <summary>
    /// 自动阅读分析代码示例
    /// </summary>
    public class CodeReader
    {
        public static async Task Run(Action<string> info, string root, string apkName, bool readFromAndroidManifest,
            string? outputOverride = null, string? configOverride = null, string? dtconfigOverride = null, string? phoneOverride = null, string? padOverride = null)
        {
            var read = (string className) =>
            {
                var path = root + $"decompile/{className.Replace(".", "/")}.java";
                if (!File.Exists(path))
                {
                    info($"(分析) {className} 未找到!!");
                    return null;
                }
                var s = File.ReadAllText(path, Encoding.UTF8);
                info($"(分析) {className} 状态良好");
                return s;
            };
            info("(分析) 开始分析反编译结果");
            var sFEBound = read("com.tencent.mobileqq.dt.model.FEBound");
            var sAppSetting = read("com.tencent.common.config.AppSetting");
            var sEventConstant = read("oicq.wlogin_sdk.report.event.EventConstant");
            var sUtil = read("oicq.wlogin_sdk.tools.util");
            var sWtLoginHelper = read("oicq.wlogin_sdk.request.WtloginHelper");
            var sQUA = read("cooperation.qzone.QUA");
            var protocol = new MiraiProtocol();
            var version = "unknown";

            if (sAppSetting != null)
            {
                int subVersion = 0;
                bool staticBlock = false;
                int level = 0;
                StringBuilder sb = new StringBuilder();
                Regex versionPattern = new Regex("([0-9]+\\.[0-9]+\\.[0-9]+)\\.([0-9]+)");
                foreach (string line in sAppSetting.Split('\n'))
                {
                    // 非 static { } 中获取 subVersion
                    if (!staticBlock)
                    {
                        if (line.Trim().StartsWith("static {"))
                        {
                            staticBlock = true;
                            continue;
                        }
                        if (subVersion > 0 || !line.Contains('=')) continue;
                        // 获取字段默认值，由于该类混淆，故只关心顺序不关心字段名
                        var s = line.Substring(line.IndexOf('=') + 1).Trim().TrimEnd(';');
                        // String
                        if (s.StartsWith("\"") && s.EndsWith("\""))
                        {
                            if (int.TryParse(s.Trim('"'), out int result))
                            {
                                subVersion = result;
                            }
                        }
                    }
                    else
                    {
                        // static { } 中获取 mainVersion 以及 appId
                        if (line.Contains('{'))
                        {
                            level++;
                        }
                        if (line.Contains('}'))
                        {
                            level--;
                            // 读取到 static { } 尽头结束
                            if (level < 0) break;
                        }
                        // 赋值操作，读取两个 appId
                        if (line.Contains('=') && !line.Contains('"') && (protocol.appIdPhone == null || protocol.appIdPad == null))
                        {
                            var s = line.Substring(line.IndexOf("=") + 1).Trim().TrimEnd(';').TrimEnd('L');
                            if (int.TryParse(s, out int result))
                            {
                                if (protocol.appIdPhone == null)
                                {
                                    protocol.appIdPhone = result.ToString();
                                    continue;
                                }
                                if (protocol.appIdPad == null)
                                {
                                    protocol.appIdPad = result.ToString();
                                    continue;
                                }
                            }
                        }
                        // 读取 sb2.append(""); 获取主版本
                        if (version == "unknown")
                        {
                            if (line.Contains(".append(\""))
                            {
                                var s = line.Substring(line.IndexOf(".append(") + 9).Trim().TrimEnd(';').TrimEnd(')').TrimEnd('"');
                                sb.Append(s);
                            }
                            else if (sb.Length > 0)
                            {
                                var s = sb.ToString();
                                sb.Clear();
                                var m = versionPattern.Match(s);
                                if (m.Success)
                                {
                                    version = m.Groups[1].Value;
                                    protocol.sortVersionName = m.Groups[1].Value + "." + m.Groups[2].Value;
                                }
                            }
                        }
                    }
                }
            }
            if (sEventConstant != null)
            {
                foreach (string line in sEventConstant.Split('\n'))
                {
                    if (line.Contains("String EVENT_WT_LOGIN_PASSWORD") && line.Contains('='))
                    {
                        var s = line.Substring(line.IndexOf("=") + 1).Trim().Trim(';').Trim('"');
                        protocol.appKey = s.Substring(0, s.IndexOf("_"));
                        break;
                    }
                }
            }
            if (sUtil != null)
            {
                foreach (string line in sUtil.Split('\n'))
                {
                    if (protocol.buildTime != null && protocol.sdkVersion != null && protocol.ssoVersion != null) break;
                    if (line.Contains('='))
                    {
                        var s = line.Substring(line.IndexOf("=") + 1).Trim().Trim(';').TrimEnd('L').Trim('"');
                        if (line.Contains("long BUILD_TIME"))
                        {
                            protocol.buildTime = s;
                            continue;
                        }
                        if (line.Contains("String SDK_VERSION"))
                        {
                            protocol.sdkVersion = s;
                            continue;
                        }
                        if (line.Contains("int SSO_VERSION"))
                        {
                            protocol.ssoVersion = s;
                            continue;
                        }
                    }
                }
            }
            if (sWtLoginHelper != null)
            {
                foreach (string line in sWtLoginHelper.Split('\n'))
                {
                    if (protocol.miscBitmap != null && protocol.mainSigMap != null && protocol.subSigMap != null) break;
                    if (line.Contains('='))
                    {
                        var s = line.Substring(line.IndexOf("=") + 1).Trim().Trim(';').TrimEnd('L');
                        if (line.Contains("this.mMainSigMap"))
                        {
                            protocol.mainSigMap = s;
                            continue;
                        }
                        if (line.Contains("this.mSubSigMap"))
                        {
                            protocol.subSigMap = s;
                            continue;
                        }
                        if (line.Contains("this.mMiscBitmap"))
                        {
                            protocol.miscBitmap = s;
                            continue;
                        }
                    }
                }
            }
            if (sQUA != null)
            {
                foreach (string line in sQUA.Split('\n'))
                {
                    if (protocol.qua != null) break;
                    if (line.Contains("String QUA ") && line.Contains('='))
                    {
                        protocol.qua = line.Substring(line.IndexOf("=") + 1).Trim().Trim(';').Trim('"');
                    }
                }
            }
            var dir = outputOverride ?? (root + $"out/{version}");
            dir = dir.Replace("\\", "/");
            if (dir.EndsWith("/")) dir = dir[..^1];
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            bool fekit = false;
            if (File.Exists(root + "apk/lib/arm64-v8a/libfekit.so"))
            {
                File.Copy(root + "apk/lib/arm64-v8a/libfekit.so", $"{dir}/libfekit.so", true);
                fekit = true;
            }

            if (File.Exists(root + apkName))
            {
                var extractCert = !File.Exists(root + "apk/META-INF/ANDROIDR.RSA");
                var extractAndroidManifest = readFromAndroidManifest && !File.Exists(root + "apk/AndroidManifest.xml");
                if (!fekit || extractCert || extractAndroidManifest)
                {
                    using (var archive = ZipFile.OpenRead(root + apkName))
                    {
                        if (!fekit)
                        {
                            var entry = archive.GetEntry("lib/arm64-v8a/libfekit.so");
                            if (entry != null)
                            {
                                entry.ExtractToFile($"{dir}/libfekit.so");
                                fekit = true;
                            }
                        }
                        if (extractCert)
                        {
                            var entryCert = archive.GetEntry("META-INF/ANDROIDR.RSA");
                            if (entryCert != null)
                            {
                                entryCert.ExtractToFile(root + "apk/META-INF/ANDROIDR.RSA");
                            }
                        }
                        if (extractAndroidManifest)
                        {
                            var entryAM = archive.GetEntry("AndroidManifest.xml");
                            if (entryAM != null)
                            {
                                entryAM.ExtractToFile(root + "apk/AndroidManifest.xml");
                            }
                        }
                    }
                }
            }
            if (File.Exists(root + "apk/META-INF/ANDROIDR.RSA"))
            {
                info($"(分析) 签名文件: {root}apk/META-INF/ANDROIDR.RSA");
                try
                {
                    var collection = new X509Certificate2Collection();
                    collection.Import(File.ReadAllBytes(root + "apk/META-INF/ANDROIDR.RSA"));
                    var cert = collection.Cast<X509Certificate2>().First();
                    //var cert = X509Certificate2.CreateFromCertFile(root + "apk/META-INF/ANDROIDR.RSA");
                    protocol.apkSign = cert.GetCertHashString(HashAlgorithmName.MD5).ToLower();
                    info($"(分析) APK 签名: {protocol.apkSign}");
                }
                catch (Exception e)
                {
                    info("(分析) 获取APK签名时发生了一个异常");
                    info($"(分析) {e.GetType().FullName}: {e.Message}");
                }
            }
            else
            {
                info("(分析) 未找到签名文件，将使用默认 apk_sign");
            }
            if (readFromAndroidManifest)
            {
                if (File.Exists(root + "apk/AndroidManifest.xml"))
                {
                    if (!File.Exists(root + "decompile/AndroidManifest.xml"))
                    {
                        info("(分析) 正在解码 AndroidManifest.xml");
                        bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                        var command = @$"""{Tasks.currentDir}tools/xml-decode{(isWin ? ".bat" : ".sh")}"" apk/AndroidManifest.xml";
                        var process = new Process
                        {
                            StartInfo = new(isWin ? "cmd.exe" : "sh")
                            {
                                Arguments = isWin ? $"/C {command}" : command,
                                WorkingDirectory = root,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                StandardOutputEncoding = Encoding.UTF8,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };
                        List<string> list= new List<string>();
                        DataReceivedEventHandler received = (sender, e) => {
                            if (e.Data != null)
                            {
                                if (list.Count == 0)
                                {
                                    info("(分析) 解码完成，正在保存");
                                }
                                list.Add(e.Data);
                            }
                        };
                        process.OutputDataReceived += received;
                        process.ErrorDataReceived += received;
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        await process.WaitForExitAsync();
                        await File.WriteAllLinesAsync(root + "decompile/AndroidManifest.xml", list, new UTF8Encoding(false));
                    }
                    if (File.Exists(root + "decompile/AndroidManifest.xml"))
                    {
                        info("(分析) 正在分析 AndroidManifest.xml");
                        var xml = new XmlDocument();
                        xml.Load(root + "decompile/AndroidManifest.xml");
                        var application = xml["manifest"]?["application"]?.ChildNodes;
                        int tmpAppId = -1, tmpAppIdPad = -1;
                        if (application != null)
                        {
                            foreach (XmlNode node in application)
                            {
                                if (node.Name != "meta-data") continue;
                                var metaName = node.Attributes?["android:name"];
                                var metaValue = node.Attributes?["android:value"];
                                if (metaName == null || metaValue == null) continue;
                                if (metaName.Value == "AppSetting_params" && int.TryParse(metaValue.Value.Split('#')[0], out int result))
                                {
                                    info("(分析) " + metaName.Value + " = " + metaValue.Value);
                                    tmpAppId = result;
                                }
                                if (metaName.Value == "AppSetting_params_pad" && int.TryParse(metaValue.Value.Split('#')[0], out int result1))
                                {
                                    info("(分析) " + metaName.Value + " = " + metaValue.Value);
                                    tmpAppIdPad = result1;
                                }
                            }
                        }
                        if (tmpAppId == -1 || tmpAppIdPad == -1)
                        {
                            info("(分析) AndroidManifest.xml 读取失败，无法找到目标节点，将使用反编译获得的 apk_id");
                        }
                        else
                        {
                            protocol.appIdPhone = tmpAppId.ToString();
                            protocol.appIdPad = tmpAppIdPad.ToString();
                            info("(分析) 成功从 AndroidManifest.xml 取得 apk_id");
                        }
                    }
                    else
                    {
                        info("(分析) AndroidManifest.xml 转换失败，将使用反编译获得的 apk_id");
                    }
                }
                else
                {
                    info("(分析) 未找到 AndroidManifest.xml，将使用反编译获得的 apk_id");
                }
            }
            if (protocol.qua != null)
            {
                var s = protocol.qua.Split("_");
                string config = $@"{"{"}
  ""server"": {"{"}
    ""host"": ""0.0.0.0"",
    ""port"": 8080
  {"}"},
  ""share_token"": false,
  ""key"": ""Eden"",
  ""auto_register"": true,
  ""protocol"": {"{"}
    ""package_name"": ""com.tencent.mobileqq"",
    ""qua"": ""{protocol.qua}"",
    ""version"": ""{s[3]}"",
    ""code"": ""{s[4]}""
  {"}"},
  ""unidbg"": {"{"}
    ""dynarmic"": false,
    ""unicorn"": true,
    ""kvm"": false,
    ""debug"": true
  {"}"}
{"}"}
";
                write(configOverride ?? $"{dir}/config.json", config);
            }
            var phone = protocol.json(false);
            var pad = protocol.json(true);
            write(phoneOverride ?? $"{dir}/android_phone.json", phone);
            write(padOverride ?? $"{dir}/android_pad.json", pad);

            // TODO: dtconfig.json
            if (sFEBound != null)
            {
                string? En = null;
                string? De = null;
                foreach (string line in sFEBound.Split('\n'))
                {
                    if (En != null && De != null) break;
                    if (line.Contains('=') && line.Contains("byte[][]"))
                    {
                        var s = line.Substring(line.IndexOf("byte[][]") + 8).Trim().TrimEnd(';').Trim('{').Trim('}').Trim();
                        if (line.Contains("FEBound.mConfigEnCode"))
                        {
                            En = s.Replace("{", "\n    [").Replace("}", "]");
                            continue;
                        }
                        if (line.Contains("FEBound.mConfigDeCode"))
                        {
                            De = s.Replace("{", "\n    [").Replace("}", "]");
                            continue;
                        }
                    }
                }
                string dtconfig = @$"{"{"}
  ""en"": [{En}
  ],
  ""de"": [{De}
  ]
{"}"}
";
                write(dtconfigOverride ?? $"{dir}/dtconfig.json", dtconfig);
            }
            info($"(分析) android_phone.json:\n{phone}");
            info($"(分析) android_pad.json:\n{pad}");
            info($"(分析) 分析结束，已生成数据到 {dir}");
            if (sAppSetting == null || sEventConstant == null || sUtil == null || sWtLoginHelper == null || sQUA == null)
            {
                info("(分析) 部分类不存在 (详见日志)，生成的文件内容可能不完整，请自行补齐文件中的 null 值");
            }
            else if (protocol.IsAnyMiss)
            {
                info("(分析) 分析结果中部分信息缺失，可能是代码已更新，请自行补齐文件中的 null 值");
            }
            if (!fekit)
            {
                info("(分析) 未找到 libfekit.so，请自行从安装包中提取该库文件");
            }
        }

        private static void write(string path, string contents)
        {
            File.WriteAllText(path, contents, new UTF8Encoding(false));
        }
    }
}
