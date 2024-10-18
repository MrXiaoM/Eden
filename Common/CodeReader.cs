using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace Eden
{
    /// <summary>
    /// 自动阅读分析代码示例
    /// </summary>
    public class CodeReader
    {
        public static async void Run(Action<string> info, string root, bool readFromAndroidManifest)
        {

            var read = (string path) =>
            {
                path = $"{root}\\{path.Replace(".", "\\")}.java";
                if (!File.Exists(path))
                {
                    info($"(分析) 文件不存在: {path}");
                    return null;
                }
                return File.ReadAllText(path, Encoding.UTF8);
            };
            info("(分析) 开始分析反编译结果");
            var sFEBound = read("com.tencent.mobileqq.dt.model.FEBound");
            var sAppSetting = read("com.tencent.common.config.AppSetting");
            var sEventConstant = read("oicq.wlogin_sdk.report.event.EventConstant");
            var sUtil = read("oicq.wlogin_sdk.tools.util");
            var sWtLoginHelper = read("oicq.wlogin_sdk.request.WtLoginHelper");
            var sQUA = read("cooperation.qzone.QUA");
            var protocol = new MiraiProtocol();
            var version = "unknown";
            if (sAppSetting != null)
            {
                int subVersion = 0;
                bool staticBlock = false;
                int level = 0;
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
                        if (line.Contains(".append(\""))
                        {
                            var s = line.Substring(line.IndexOf(".append(") + 9).Trim().TrimEnd(';').TrimEnd(')').TrimEnd('"');
                            if (!s.StartsWith("V") && s.EndsWith("."))
                            {
                                version = s.TrimEnd('.');
                                protocol.sortVersionName = $"{version}.{subVersion}";
                                break;
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
            var dir = $"out/{version}";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            bool fekit = false;
            if (File.Exists("apk/lib/arm64-v8a/libfekit.so"))
            {
                File.Copy("apk/lib/arm64-v8a/libfekit.so", $"{dir}/libfekit.so", true);
                fekit = true;
            }

            if (File.Exists("Eden.apk"))
            {
                var extractCert = !File.Exists("apk/META-INF/ANDROIDR.RSA");
                var extractAndroidManifest = readFromAndroidManifest && !File.Exists("apk/AndroidManifest.xml");
                if (!fekit || extractCert || extractAndroidManifest)
                {
                    using (var archive = ZipFile.OpenRead("Eden.apk"))
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
                                entryCert.ExtractToFile("apk/META-INF/ANDROIDR.RSA");
                            }
                        }
                        if (extractAndroidManifest)
                        {
                            var entryAM = archive.GetEntry("AndroidManifest.xml");
                            if (entryAM != null)
                            {
                                entryAM.ExtractToFile("apk/AndroidManifest.xml");
                            }
                        }
                    }
                }
            }
            if (File.Exists("apk/META-INF/ANDROIDR.RSA"))
            {
                try
                {
                    var cert = X509Certificate.CreateFromCertFile("apk/META-INF/ANDROIDR.RSA");
                    protocol.apkSign = cert.GetCertHashString(HashAlgorithmName.MD5).ToLower();
                    info(protocol.apkSign);
                }
                catch (Exception e)
                {
                    info("(分析) 获取APK签名时发生了一个异常");
                    info($"(分析) {e.GetType().FullName}:{e.Message}");
                }
            }
            else
            {
                info("(分析) 未找到签名文件，将使用默认 apk_sign");
            }
            if (readFromAndroidManifest)
            {
                if (File.Exists("apk/AndroidManifest.xml"))
                {
                    if (!File.Exists("decompile/AndroidManifest.xml"))
                    {
                        var command = @"""tools\xml-decode"" apk\AndroidManifest.xml > decompile\AndroidManifest.xml";
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
                        process.Start();
                        await process.WaitForExitAsync();
                    }
                    if (File.Exists("decompile/AndroidManifest.xml"))
                    {
                        var xml = new XmlDocument();
                        xml.Load("decompile/AndroidManifest.xml");
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
                write($"{dir}/config.json", config);
            }
            var phone = protocol.json(false);
            var pad = protocol.json(true);
            write($"{dir}/android_phone.json", phone);
            write($"{dir}/android_pad.json", pad);

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
                write($"{dir}/dtconfig.json", dtconfig);
            }
            info($"(分析) android_phone.json:\n{phone}");
            info($"(分析) android_pad.json:\n{pad}");
            info($"(分析) 分析结束，已生成数据到 {dir}: android_phone.json, android_pad.json{(protocol.qua == null ? "" : ", config.json")}{(sFEBound == null ? "" : ", dtconfig.json")}");
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
