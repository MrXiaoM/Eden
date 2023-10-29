<img align="right" src="docs/eden.png" width="128"/>

# 伊甸 Eden

未来框架版本信息自动提取工具。



# 声明

本项目仅作学习参考，探究低配置计算机反编译大尺寸安卓程序以及自动分析的可行性，请在下载后24小时内删除。  
请勿将该项目用于非法用途。

# 需求

* Java 8
* .NET Core 7.0 Desktop
* 至少 2GB 空闲运行内存

本程序使用的反编译策略为将所有 classes.dex 依次转换为 jar 再合并，在一定程度上可减少运行内存占用，避免反编译过程中出现 OOM。缺点是转换的总过程在作者的计算机上 (i5-5300U) 大约需要半小时来完成。

本程序会在 dex2jar 转换结果中选择特定的类进行分析，寻找相关信息以生成协议信息。

# 鸣谢

* [pxb1988/dex2jar](https://github.com/pxb1988/dex2jar) 提供dex转jar方法 - Apache-2.0 License
* [mstrobel/procyon](https://github.com/mstrobel/procyon) 提供反编译工具 - Apache-2.0 License
