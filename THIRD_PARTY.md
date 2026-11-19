# 第三方组件

| 组件 | 来源与许可 |
| --- | --- |
| PecoFence v0.4.0 | [上游源码](https://github.com/DayuanJiang/PecoFence/tree/76fe196569b7e4f7c837f315e65fbf1470a68790)，Apache 2.0。保留上游 LICENSE 和 NOTICE，并在已修改的上游文件中标明。 |
| Everything 1.5.0.1409a x64 | [voidtools 下载页](https://www.voidtools.com/es-es/everything-1.5a/)。安装时向 voidtools 获取独立安装器，验证签名和固定的 SHA-256。许可随 voidtools 软件提供。 |
| Microsoft Edge WebView2 | [微软分发文档](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution)。缺少运行时才从微软获取 Evergreen Bootstrapper。随本品分发的 WebView2Loader.dll 适用[微软 Loader 声明](工程/package/PecoFenceNative/licenses/WebView2.txt)。 |
| Rust 依赖及 vendored Windows 封装 | 版本由 Cargo.lock 固定，适用[第三方许可清单](工程/package/PecoFenceNative/licenses/THIRD-PARTY-LICENSES.txt)及 `工程/native/vendor` 中各自的许可文件。 |
| Windows SDK Contracts | 构建时按固定版本和 SHA-256 从 NuGet 获取，适用 Microsoft.Windows.SDK.Contracts 的包许可。 |
