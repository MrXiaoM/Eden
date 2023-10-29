<img align="right" src="docs/eden.png" width="128"/>

# 伊甸 Eden

未来框架版本信息自动提取工具。

# 声明

本项目仅作学习参考，探究低配置计算机反编译大尺寸安卓程序以及自动分析的可行性，请在下载后24小时内删除。  
禁止将该项目用于任何非法、违反道德的用途，本项目作者将不对使用本程序的任意部分产生的后果负责。使用本项目的源代码、发布的二进制文件等即代表你同意以上条款，并自愿承担所产生的后果。

# 需求

* Java 8
* .NET Core 7.0 Desktop Runtime
* 至少 2GB 空闲运行内存
* 至少 4GB 空闲存储空间

本程序使用的反编译策略为，将所有 classes.dex 依次转换为 jar 再合并。此方法在一定程度上可减少运行内存占用，避免反编译过程中出现 OOM。缺点是转换的总过程在作者的计算机上 (i5-5300U) 大约需要半小时来完成对某大型APP的转换，反编译更是花上了数小时。但这使得这个过程成为了可能，若直接转换整个安装包的 dex 为 jar，在低配置的计算机上很可能会出现 OOM。

本程序会在 dex2jar 转换结果中选择特定的类进行分析，寻找相关信息以生成协议信息。

# 鸣谢

* [pxb1988/dex2jar](https://github.com/pxb1988/dex2jar) 提供dex转jar方法 - Apache-2.0 License
* [mstrobel/procyon](https://github.com/mstrobel/procyon) 提供反编译工具 - Apache-2.0 License
