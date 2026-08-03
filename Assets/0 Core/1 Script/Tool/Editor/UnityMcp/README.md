# Unity MCP

项目内 Unity 编辑器自动化工具的统一入口。各模块共享目录和命名，但保持独立进程与故障边界。

## 目录

| 模块 | 目录 | MCP 服务 | 用途 |
| --- | --- | --- | --- |
| Prefab | `Prefab/` | `unity_prefab` | Prefab、场景、Inspector、截图和 PlayMode |
| Compile | `Compile/` | `unity_compile` | 强制刷新并等待 Unity 脚本编译完成 |
| Source Code | `SourceCodeMcp~/` | `source_code`；也可由 Prefab 服务代理 | 搜索、读取、补丁和诊断 |

`Prefab` 与 `Compile` 位于同一模块下，但 Compile 不依赖 Prefab HTTP Bridge。这样即使桥接服务
不可用，仍可独立触发编译并读取 `Editor.log`。

`SourceCodeMcp~` 是源码工具的唯一实现。独立服务与 Prefab 代理共用其源码、schema、启动器和 `Library` 构建缓存；两种入口只保留进程与故障边界。
详细协议和维护说明分别参见：

- `Prefab/README.md`
- `Compile/README.md`
- `SourceCodeMcp~/README.md`

客户端配置由 `Prefab/Config/McpConfigInstaller.cs` 管理，项目级配置文件为 `.mcp.json` 和
`.codex/config.toml`。
