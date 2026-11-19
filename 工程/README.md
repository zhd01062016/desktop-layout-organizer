# 桌面美化工具的构建与发布

日常修改在本目录进行。capture 存放截图和贴图源码，installer 存放安装与依赖处理，native 是桌面主程序、CLI 和守护进程。tests 存放行为回归脚本。package 提供原始安装材料和默认配置。从迁移包解压的工程会直接使用上一级目录的安装材料。

## 工具链

在 64 位 Windows 上运行 Windows PowerShell 5.1。C# 使用系统 .NET Framework 4.x 编译器和 Microsoft.Windows.SDK.Contracts 10.0.22621.2428。构建脚本校验 SDK 包的 SHA-256，支持本地缓存或从 NuGet 获取。普通用户安装和运行软件无需编译工具。

Rust 工程来自 DayuanJiang/PecoFence 的 v0.4.0，提交 76fe196569b7e4f7c837f315e65fbf1470a68790，叠加该软件迁移包内的本地修改。使用 Rust 1.95.0、x86_64-pc-windows-msvc、Visual Studio 2022 C++ Build Tools 与 Windows SDK 10.0.22621。Cargo.lock 固定依赖。Rust 工具链可由 rustup 安装；本项目也支持 .build 内的本地工具链。

## 构建、验证、打包

在工程目录执行以下命令。每步成功后再执行下一步。

    powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Managed.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-Managed.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Native.ps1 -Test
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Package.ps1

Package.ps1 在 .build/releases 的独立目录生成迁移包，验证安装器、默认配置并逐文件比对 ZIP 内容。打包时自动运行 tests/verify-install.ps1，在临时桌面校验安装配置、启动具名实例并检查实际分区、规则和退出状态。桌面集成测试可用 native/scripts/cli-smoke.ps1 -Profile release 运行。正式发布只向现有安装更新程序文件。运行中的个人配置保存在安装目录的 config 中。

## 登录启动

主程序通过当前用户的零延迟交互登录任务启动，普通权限、正常进程优先级，电池供电允许运行且无执行时限。自启开关统一注册或删除任务，并清除旧 Run 入口。安装器在布局完成后启用自启。启动后延迟一秒检查任务以让首帧先显示，具名测试实例跳过该检查。壁纸查询和解码共用 150 毫秒等待预算，超时先显示分区，后台完成后更新背景。日志记录启动、首帧与任务操作耗时。

Everything 联动：桌面软件启动时启动 D:\home_bug\Everything\Everything.exe 和其索引服务，正常退出时关闭程序并停止索引服务。Everything 不再独立开机自启，服务按需启动。安装时会请求管理员确认，为当前用户授予该服务的启动、停止和状态查询权限，之后日常启动无需管理员权限。

桌面和开始菜单的“桌面美化分区”快捷方式均指向当前安装并使用本地配置。程序旁存在 config 目录时，直接双击主程序也自动使用该目录，保持同一套分区样式、位置和规则。隔离安装验证覆盖无参数模式的本地配置选择及两处快捷方式的目标、参数和工作目录。

