# Source Code MCP

这是项目内 SourceCodeMcp 的唯一实现。目录名以 `~` 结尾，Unity AssetDatabase 会忽略其中的 .NET 10 源码，因此它不参与 Unity 程序集编译，也不依赖 Unity Editor。

独立 `source_code` 服务直接启动本目录的 `source-code-mcp.ps1`；只连接 Unity MCP 的客户端也可由 Prefab 服务代理同一启动器。两种入口共用源码、schema 和按源码指纹生成的构建缓存，但各自保持独立 stdio 进程与故障边界。

构建产物位于 `Library/InspectorBridgeSourceCodeMcp`，测试夹具位于 `Library/InspectorBridgeSourceCodeMcpTests`。项目根不再保留第二份 `Tools/SourceCodeMcp`。

## 工具

- `search_code`
  - 按公共路径和文件分组压缩结果。
  - 每个结果块返回精确命中偏移和稳定 `matchId`。
  - `matchId` 可直接交给 `read_code`，无需重新传路径和行范围。
- `read_code`
  - 支持 `path` 或 `matchId` 两种读取方式。
  - 返回完整文件 SHA-256。
  - `includeLineNumbers=true` 时额外返回紧凑的 `[line,text]` 数组。
- `find_symbol`
  - 使用 Roslyn 语义模型查声明和精确引用；Unity 工程文件过期时显式回退语法模式。
- `replace_symbol`
  - 按 `find_symbol` 返回的稳定 `symbolId` 替换声明或方法体。
  - 写入前校验 SHA-256 和 C# 语法，支持 `dryRun`。
- `apply_patch`
  - 保留原有多文件原子行编辑。
  - 行编辑可携带 `expectedOldText`，避免正确行号对应了错误代码。
  - 支持唯一 `oldText/newText` 锚点替换，避免依赖裸行号。
  - 支持 `dryRun`、C# Roslyn 语法校验和哈希冲突后的显式 `allowRebase`。
  - 错误结果包含 edit index、当前 SHA、变更范围或语法诊断等结构化详情。
- `inspect_unity_code`
  - 检查 Unity 可序列化字段、`SerializeReference` 和 `FormerlySerializedAs`。
  - 返回脚本 GUID 及引用该 GUID 的 Prefab、Scene、Asset。
  - 对非 C# 资源返回常用 Importer 配置，例如纹理 Wrap、Filter、Read/Write。
- `get_diagnostics`
  - 读取 UnityCompileMcp 的最新诊断和过期状态。

## 推荐修改流程

简单修改优先使用唯一文本锚点：

```json
{
  "files": [{
    "path": "Assets/Scripts/Foo.cs",
    "expectedSha256": "...",
    "replacements": [{
      "oldText": "唯一的旧代码",
      "newText": "新代码"
    }]
  }],
  "dryRun": true
}
```

复杂 C# 修改优先使用：

```text
find_symbol → replace_symbol(dryRun=true) → replace_symbol
```

仍需按行编辑时，应从同一次 `read_code` 获取 SHA，并提供 `expectedOldText`。工具会在任何文件落盘前完成所有文件的范围、锚点、哈希和语法校验。

## 验证

```powershell
dotnet build SourceCodeMcp.csproj -c Release
./source-code-mcp-smoke-test.ps1 -ServerDll ./bin/Release/net10.0/UnitySourceCodeMcp.dll
```
