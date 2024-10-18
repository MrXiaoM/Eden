# 发布所需文件

首先，使用 Visual Studio 分别运行 `Eden.GUI` 和 `Eden.CLI` 项目的 FolderProfile.pubxml 发布，构建产物会出现在项目目录的 `out` 文件夹里。

运行发布本程序后，在 `out` 文件夹里创建 `tools` 文件夹，将本目录内除 README 的所有文件复制到该文件夹。  
+ 将 [dex2jar](https://github.com/pxb1988/dex2jar/releases) v2.4 的构建，解压放入 `tools` 文件夹。要确保 `tools` 文件夹里有 `lib` 文件夹。
+ 将 [procyon](https://github.com/mstrobel/procyon/releases) v0.6.0 的构建，放入 `tools/lib` 文件夹。
+ 将 [AXMLPrinter2](https://storage.googleapis.com/google-code-archive-downloads/v2/code.google.com/android4me/AXMLPrinter2.jar) 的构建，放入 `tools/lib` 文件夹。

然后，将构建产物 (`.exe`, `.dll`, `.json`) 与 `tools` 文件夹一同打包发布。
