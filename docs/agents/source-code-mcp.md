# Source Code MCP 规则

适用场景：搜索、局部读取或安全修改项目内文本与源码。

1. MCP 可用时优先使用 `source_code` 的结构化工具，避免把重复路径和大段终端输出带入上下文。
2. 按 `search_code` → `read_code` → `apply_patch` 顺序工作；读取范围保持最小。
3. `apply_patch` 必须使用同一次 `read_code` 返回的 `sha256`，文件发生变化后重新读取，不绕过冲突。
4. 多文件相关修改放在同一个事务中；所有行号均基于修改前的快照。
5. `get_diagnostics` 只读上次 Unity 编译状态；`stale=true` 时按
   [unity-compile.md](unity-compile.md) 使用 `force_unity_compile`。
6. MCP 不可用或任务超出工具范围时，才退回 `rg`、局部文件读取和标准补丁工具。
7. `UnityMcp/SourceCodeMcp~` 是唯一实现；独立 `source_code` 与 UnityMcp 代理共用其源码、
   schema、启动器和构建缓存。源码任务默认使用独立服务；客户端只连接 Unity MCP 时才传
   `-IncludeSourceCodeTools`，并关闭独立注册，禁止同时公布两份相同工具 schema。
8. 不把 Huffman、gzip、Base64 等压缩内容直接交给模型。token 优化使用路径去重、元组、偏移量、
   分页、预算和稳定 ID，响应必须保持可直接理解。
