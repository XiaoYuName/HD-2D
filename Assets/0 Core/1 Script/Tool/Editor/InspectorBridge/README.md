# Unity Prefab MCP

这个 MCP 通过 Unity Editor API 分析和修改 Prefab，不把体积很大的 Prefab YAML 发给 AI。

## 架构

- `PrefabMcpCommands.cs`：在 Unity 主线程中使用 `AssetDatabase`、`PrefabUtility` 和 `SerializedObject`。
- `InspectorBridgeServer.cs`：只监听 `127.0.0.1:58732` 的项目内 HTTP 桥。
- `unity-prefab-mcp.ps1`：MCP stdio 服务，把 MCP 工具调用转发给当前 Unity 项目。

Unity 必须打开本项目且完成脚本编译。MCP 服务会校验项目绝对路径，避免端口被另一个 Unity 项目占用时误改资源。

项目共享配置使用 `PrefabMcpSettings` ScriptableObject，首次编译后自动创建在：

```text
Assets/0 Core/1 Script/Tool/Editor/InspectorBridge/Settings/PrefabMcpSettings.asset
```

可从 `Edit > Project Settings > Unity Prefab MCP` 或 `Tools > Inspector Bridge > 设置` 编辑。端口修改后，MCP 命令的 `-Port` 参数也要使用相同值。

## MCP 配置

当前项目机器已验证可使用 Windows PowerShell 5.1：

```text
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "D:\Unity\Project\AFramework\Assets\0 Core\1 Script\Tool\Editor\InspectorBridge\McpServer\unity-prefab-mcp.ps1" -ProjectPath D:\Unity\Project\AFramework
```

在支持 stdio MCP 的客户端中，将 `command` 设为 `powershell.exe`，参数设为：

```json
[
  "-NoLogo",
  "-NoProfile",
  "-ExecutionPolicy",
  "Bypass",
  "-File",
  "D:\\Unity\\Project\\AFramework\\Assets\\0 Core\\1 Script\\Tool\\Editor\\InspectorBridge\\McpServer\\unity-prefab-mcp.ps1",
  "-ProjectPath",
  "D:\\Unity\\Project\\AFramework"
]
```

## 推荐调用顺序

1. `unity_prefab_status`：确认 Unity 和目标项目。
2. `find_prefabs`：获得 Prefab 的 `Assets/...` 路径。
3. `get_prefab_tree`：只读取需要深度的层级和组件索引；大型 Prefab 优先传 `nameFilter` 或 `componentTypeFilter`。
4. `get_component_fields`：读取指定组件的序列化字段；可用 `fieldNameFilter`、`onlyObjectReferences`、`onlyUnassigned` 缩小结果。
5. `find_binding_candidates`：让 Unity 按字段类型过滤候选组件。
6. `assign_object_reference`：先以 `apply=false` 预检，再以 `apply=true` 保存。
7. `find_asset_candidates` / `assign_asset_reference`：查询并绑定 ScriptableObject、Sprite、Prefab 或 Prefab 组件资产。
8. `create_ui_element`：创建 Container、Image、Button、TMP Text、Vertical Layout 或 Scroll View；仍应先 `apply=false`。
9. `validate_prefab`：检查丢失脚本及未赋值引用。

`objectId` 使用 sibling index，例如 `0/2/1`，不会因为兄弟节点重名而选错。层级发生变化后应重新调用 `get_prefab_tree`。
写入工具在 `clear=false` 时要求显式提供 `sourceObjectId`，不会把缺失参数误当成根节点。

## 当前范围

- 支持 Prefab 内的 `GameObject`/`Component` 对象引用。
- 支持项目资产和其他 Prefab 组件引用。
- `apply=true` 受 SO 中写入总开关和目录白名单限制；启用备份时，原 Prefab 会先复制到 `Library/PrefabMcpBackups`。
- UI 默认尺寸、TMP 字体、字号和颜色由同一个 SO 管理。
- 字段发现以 Unity 的 `SerializedProperty` 为准，包括 `public` 和 `[SerializeField] private` 字段。
- 候选类型推断当前支持顶层对象引用字段。
- `validate_prefab` 报告的 null 引用可能是业务允许的可选字段，需要 AI 或开发者结合上下文判断。
- 当前 UI 创建以通用结构为主；锚点模板、主题皮肤、现有 UI Prefab 实例化和 UnityEvent 绑定可继续扩展。
