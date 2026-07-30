# Source Code MCP

这是项目内 SourceCodeMcp 的唯一实现。目录名以 `~` 结尾，Unity AssetDatabase 会忽略其中的 .NET 10 源码，因此它不参与 Unity 程序集编译，也不依赖 Unity Editor。

独立 `source_code` 服务直接启动本目录的 `source-code-mcp.ps1`；只连接 Unity MCP 的客户端也可由 Prefab 服务代理同一启动器。两种入口共用源码、schema 和按源码指纹生成的构建缓存，但各自保持独立 stdio 进程与故障边界。

构建产物位于 `Library/InspectorBridgeSourceCodeMcp`，测试夹具位于 `Library/InspectorBridgeSourceCodeMcpTests`。项目根不再保留第二份 `Tools/SourceCodeMcp`。
