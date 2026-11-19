# 桌面美化分区

面向 Windows 10 / 11 x64 的桌面整理工具，基于 [DayuanJiang/PecoFence v0.4.0](https://github.com/DayuanJiang/PecoFence/tree/v0.4.0) 独立维护。预设包含应用快捷方式、文件夹、办公文档、工程图档、图片与其他五个分类分区，以及一个桌面分区。

## 安装与使用

1. 从 [GitHub Releases](https://github.com/zhd01062016/desktop-layout-organizer/releases) 下载 Windows x64 迁移包并完整解压。可用同页的 `SHA256SUMS.txt` 校验 ZIP。
2. 确认电脑有 D 盘，双击 **一键安装.exe**。安装器使用 `D:\home_bug`。如果缺少 Everything 或 WebView2，安装器会从各自的官网下载并验证数字签名。配置 Everything 索引服务时会请求管理员确认。
3. 登录 Windows 后分区会自动加载。退出后，从桌面或开始菜单的 **桌面美化分区** 重新打开。

安装器用于首次安装。已有安装时，请保留 `D:\home_bug\PecoFenceNative\config` 里的个人配置，再进行迁移。

| 操作 | 默认快捷键 |
| --- | --- |
| 搜索桌面和本机文件 | Alt + Q |
| 截图、标注和查看截图历史 | F1 |
| 将剪贴板内容贴到屏幕 | F2 |

Everything 1.5 随应用启动，正常退出时搜索程序及索引服务一同停止。截图历史最多保留 10 张，24 小时后清理。手动保存的图片由用户管理。

## 构建

在 Windows x64 上安装 Rust 1.95.0、Visual Studio 2022 C++ Build Tools 和 Windows SDK。使用 Windows PowerShell 5.1，在仓库根目录运行：

```powershell
cd 工程
.\Build-Managed.ps1
.\Build-Native.ps1 -Test
.\Test-Managed.ps1
.\Package.ps1
```

打包与界面回归需要已登录的 Windows 桌面。打包脚本会验证隔离安装及 ZIP 内容。编译工具、缓存与个人设置都存于本地。默认配置模板位于 `工程/package/PecoFenceNative/config`。

| 目录 | 内容 |
| --- | --- |
| 工程/native | 桌面分区、命令行与守护进程 |
| 工程/capture | 截图、标注、贴图与历史 |
| 工程/installer | 安装、依赖下载和快捷方式 |
| 工程/package | 默认配置和随包许可文件 |
| 工程/tests | 安装和截图功能的回归验证 |

## 来源与许可

本项目基于 PecoFence v0.4.0，源代码基线为提交 `76fe196569b7e4f7c837f315e65fbf1470a68790`。公开版采用独立名称和图标。版本下载及反馈使用此项目的 GitHub 页面。上游应用内版本更新、统计上传和反馈上传在公开版中停用。

PecoFence 衍生代码采用 Apache License 2.0，第三方组件适用各自的许可。参见 [LICENSE](LICENSE)、[NOTICE](NOTICE)、[上游修改清单](工程/package/PecoFenceNative/licenses/Modifications.txt) 和 [第三方组件清单](THIRD_PARTY.md)。
