using CommandLine;
using Eden;
using System.Diagnostics;


class Program
{
    class Options
    {
        [Option("working-dir", Required = false, HelpText = "Eden 工作路径")]
        public string? WorkingDir { get; set; }

        [Option("eden-apk", Required = false, HelpText = "Eden.apk 路径，要相对于工作路径")]
        public string? EdenAPK {  get; set; }

        [Option("fast-dex", Default = true, HelpText = "是否使用快速解包方法")]
        public bool FastDex { get; set; }

        [Option("config-override", Required = false, HelpText = "config.json 输出路径重写")]
        public string? ConfigOverride { get; set; }

        [Option("pad-override", Required = false, HelpText = "android_pad.json 输出路径重写")]
        public string? PadOverride { get; set; }

        [Option("phone-override", Required = false, HelpText = "android_phone.json 输出路径重写")]
        public string? PhoneOverride { get; set; }

        [Option("from-manifest", Default = true, HelpText = "是否从 AndroidManifest.xml 读取协议 app id")]
        public bool readProtocolFromManifest { get; set; }

        [Option("start-pos", Default = 0, HelpText = "起始步骤，0=解压APK，1=解包Dex，2=反编译class，3=分析代码")]
        public int StartPos { get; set; }

        public bool can(int pos)
        {
            if (pos < StartPos) return false;
            return true;
        }
    }
    static void Main(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args)
            .WithNotParsed(Exit)
            .WithParsedAsync(RunMain)
            .Wait();
    }
    static void Exit(IEnumerable<Error> errs)
    {
        foreach (var error in errs)
        {
            Console.WriteLine(error);
        }
    }
    static async Task RunMain(Options options)
    {
        Tasks tasks = new Tasks(info, options.WorkingDir ?? Environment.CurrentDirectory);
        var skiped = options.StartPos;
        if (options.can(0))
        {
            await Task.Run(tasks.ExtractAPK);
        }
        if (options.can(1))
        {
            if (options.FastDex)
            {
                await tasks.FastUnpackDex(dex2jarPartly_OutputDataReceived);
            }
            else
            {
                await tasks.SlowUnpackDex(dex2jar_OutputDataReceived);
                await Task.Run(tasks.SlowExtractClasses);
            }
        }
        if (options.can(2))
        {
            await tasks.decompile(procyon_OutputDataReceived);
        }
        if (options.can(3))
        {
            await Task.Run(() => CodeReader.Run(info, tasks.workDir, options.EdenAPK ?? "Eden.apk", options.readProtocolFromManifest));
        }
    }

    private static void dex2jar_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            info($"(dex2jar) {e.Data}");
        }
    }
    private static void dex2jarPartly_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            info($"(dex2jar-partly) {e.Data}");
        }
    }
    private static void procyon_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            info($"(procyon) {e.Data}");
        }
    }
    static void info(string s)
    {
        Console.WriteLine($"{DateTime.Now.ToString("[HH:mm:ss]")} {s}\n");
    }
}
