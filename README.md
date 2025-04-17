<img align="right" src="docs/eden.png" width="180"/>

# 伊甸 Eden

未来/go-cqhttp框架版本信息提取工具。

## 声明

本项目仅作学习参考，探究低配置计算机反编译大尺寸安卓程序以及自动分析的可行性，请在下载后24小时内删除。  
禁止将该项目用于任何非法、违反道德的用途，本项目作者将不对使用本程序的任意部分产生的后果负责。使用本项目的源代码、发布的二进制文件等即代表你同意以上条款，并自愿承担所产生的后果。

## 需求

* Java 8
* .NET Core 6.0 Runtime
* 至少 2GB 空闲运行内存
* 至少 4GB 空闲存储空间

本程序使用的反编译策略为，将所有 classes.dex 依次转换为 jar，再分别反编译。此方法在一定程度上可减少运行内存占用，避免反编译过程中出现 OOM。缺点是转换的总过程很耗时，在作者的旧计算机上 (i5-5300U) 大约需要半小时来完成对某大型APP的转换，反编译更是花上了数小时。但这使得这个过程成为了可能，若直接转换整个安装包的 dex 为 jar，在低配置的计算机上很可能会出现 OOM。

本程序会在 dex2jar 转换结果中选择特定的类进行分析，寻找相关信息以生成协议信息。

在手动对某大型 APP 进行过完整分析后，本程序可以调用 dex2jar 的接口，读取所有 dex 的文件列表进行筛选（通常最多只需要几秒即可读取完一个 dex 文件）。

这样就可以使用极短的时间，定点分析抽离 APK 中我们所需的 class 单独进行反编译并进行静态分析，而不需要将整个 APK 反编译耗费这么长时间。在作者的旧计算机上，只需使用大约10秒时间就可以将所需的 class 文件从 dex 中导出。

如需完整反编译（用于完整分析，方便编写 `CodeReader`），请在第二步慢速转换dex为jar之后执行
```shell
mkdir decompile-full
tools\procyon.cmd -o decompile-full cache/*
```
可能需要*非常久*的时间，大概几个小时，并且及其占用系统资源。

## CLI 使用方法

安装运行环境: https://learn.microsoft.com/zh-cn/dotnet/core/install/linux

运行
```shell
dotnet Eden.CLI.dll 参数
```
可用参数
```shell
  --working-dir          Eden 工作路径
  --eden-apk             Eden.apk 路径，要相对于工作路径
  --fast-dex             (Default: true) 是否使用快速解包方法
  --output-override      输出文件夹(out/版本号/)路径重写
  --config-override      文件 config.json 输出路径重写，会覆盖 output-override
  --dtconfig-override    文件 dtconfig.json 输出路径重写，会覆盖 output-override
  --pad-override         文件 android_pad.json 输出路径重写，会覆盖 output-override
  --phone-override       文件 android_phone.json 输出路径重写，会覆盖 output-override
  --from-manifest        (Default: true) 是否从 AndroidManifest.xml 读取协议 app id
  --start-pos            (Default: 0) 起始步骤，0=解压APK，1=解包Dex，2=反编译class，3=分析代码
  --help                 Display this help screen.
  --version              Display version information.
```
示例如下
```shell
mkdir -p protocol-versions/android_phone protocol-versions/android_pad
dotnet Eden.CLI.dll --working-dir v9065 --eden-apk Android_9.0.65_64.apk --phone-override protocol-versions/android_phone/9.0.65.json --pad-override protocol-versions/android_pad/9.0.65.json
```

## 构建

请阅读 files 文件夹内的说明。

## 鸣谢

* [pxb1988/dex2jar](https://github.com/pxb1988/dex2jar) 提供dex转jar方法 - Apache-2.0 License
* [mstrobel/procyon](https://github.com/mstrobel/procyon) 提供反编译工具 - Apache-2.0 License
* [googlecode/android4me](https://code.google.com/archive/p/android4me) 提供安卓XML解密方法 - Apache-2.0 License
