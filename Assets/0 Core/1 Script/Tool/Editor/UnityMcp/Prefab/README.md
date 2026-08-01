# Unity Prefab MCP

通过 Unity Editor API 分析和修改 Prefab / 场景，不把体积很大的 YAML 发给 AI。
所有 C# 在独立程序集 `UnityMcp.Editor`（命名空间 `UnityMcp`，`autoReferenced: false`，
`includePlatforms: Editor`）里，不依赖项目代码，删掉整个文件夹也不影响工程编译。

## 分层

```
McpServer/unity-prefab-mcp.ps1   MCP stdio 服务：只做 JSON-RPC ↔ HTTP 转发，不含任何契约
Core/BridgeServer.cs             127.0.0.1 HTTP 桥（裸 socket），后台收请求 → 主线程执行
Core/BridgeRouter.cs             「工具名 → 处理函数」一张表；action 就是 MCP 工具名
Core/BridgeJson.cs               全链路唯一的 token 裁剪层 + 请求参数严格校验
Core/CompileTracker.cs           跨程序集重载保留最近一次编译结果
Tools/ToolCatalog.cs             工具表与 inputSchema 的唯一来源（C# 里）
Tools/BridgeDto.cs               请求/响应 DTO
Tools/EditTarget.cs              统一 prefabAsset / prefabStage / openScene 三种目标
Tools/PrefabAddress.cs           objectId 寻址、字段类型推断、序列化值 ↔ 协议字符串
Tools/QueryTools.cs              只读查询
Tools/EditTools.cs               edit_prefab 事务批量编辑
Tools/SerializedValueWriter.cs   setValue 的值解析
Tools/UiElementFactory.cs        createUi 的 UI 预设
Tools/ScreenshotTool.cs          编辑器窗口 / PlayMode 标记截图
Tools/PlayModeInputTool.cs       受控的 uGUI / Input System 输入模拟
Config/                          项目级 SO 配置、设置面板、客户端配置写入
FEEDBACK.md                      AI 实际用这套工具时的摩擦点与对应改法
```

三条贯穿全局的设计约束：

1. **裁剪只在一处**。`BridgeJson` 序列化时丢掉 null 字段，服务端把字段置 null 就等于不发。
   ps1 不再按工具挑字段（那样服务端加字段忘了同步就会被静默丢掉）。
   数值字段一律照发：`componentIndex=0` 这种"默认值恰好有意义"的字段不能靠 DefaultValueHandling 猜。
2. **契约只有一份**。schema 写在 `ToolCatalog`，和处理函数同一个程序集、同一份命名；
   域重载时会校验「工具表 ↔ 路由表」是否一一对应，不匹配直接报 error。
3. **早失败**。请求走 `MissingMemberHandling.Error`：参数名拼错立即报错，
   而不是静默填默认值让 AI 拿到"看似成功却没生效"的结果。

响应信封没有 `ok` 字段：**`error` 非空即失败**，每条操作结果也是同一套约定。

## 工具一览（Prefab 15 个）

| 工具 | 用途 |
| --- | --- |
| `unity_prefab_status` | 确认 Unity 和目标项目；顺带报当前 Prefab 编辑态 |
| `refresh_unity_assets` | 刷新 AssetDatabase 并请求编译（改完磁盘上的脚本/资源后调） |
| `get_unity_compile_status` | 读编译状态与错误；**看 `resultStale`**，用 `waitSeconds` 等到新鲜 |
| `open_prefab_stage` | 等价于双击进入 Prefab 编辑态 |
| `capture_unity_screenshot` | 截 Scene View（离屏渲染，不要焦点）/ 编辑器窗口 / PlayMode 画面 |
| `control_unity_play_mode` | 不依赖窗口焦点地开始、停止、暂停或恢复 PlayMode |
| `simulate_playmode_input` | 在 PlayMode 模拟 uGUI 点击/拖拽或 Input System 键盘输入 |
| `get_prefab_mcp_settings` | 读项目配置（写入策略、备份、上限、端口） |
| `find_prefabs` | 找 Prefab，只回 `Assets/...` 路径数组 |
| `get_prefab_tree` | 读层级 + 组件索引 |
| `get_component_fields` | 读序列化字段，`targets` 可一次读多个组件 |
| `find_binding_candidates` | 一个引用字段能填什么，返回可直接用于 `setValue` 的 `value` |
| `edit_prefab` | 一次调用按顺序执行一批结构编辑（事务） |
| `restore_prefab_backup` | 列出/还原 `edit_prefab` 每次写入前留的备份（`prefabAsset` 的后悔药） |
| `validate_prefab` | 丢失脚本 + 未赋值引用体检 |
UnityMcp 还可选代理 `search_code`、`read_code`、`find_symbol`、`replace_symbol`、
`apply_patch`、`inspect_unity_code`、`get_diagnostics` 七个源码工具。
`UnityMcp/SourceCodeMcp~` 是唯一实现；独立 `source_code`
服务和 Prefab 代理启动同一份 schema、启动器与 .NET 10 程序，并复用
`Library/InspectorBridgeSourceCodeMcp` 中按源码指纹生成的构建缓存。两种入口仍使用独立 stdio
进程，互不传递运行状态。为避免模型同时加载两份相同 schema，Prefab 默认不公布源码工具；
只连接 Unity MCP 时给 `unity-prefab-mcp.ps1` 增加 `-IncludeSourceCodeTools`，并关闭独立
`source_code` 注册。

写操作只有 `edit_prefab` 一个入口。以前的 `assign_object_reference` / `assign_asset_reference` /
`create_ui_element` / `find_asset_candidates` 已删除：赋引用用 `setValue`，建 UI 用 `createUi` 操作，
找资产候选是 `find_binding_candidates` 的 `scope=asset`。工具少了，每次会话常驻的 schema token 也少了，
而且创建 + 配置能落在同一个事务里。

## 推荐调用顺序

1. `unity_prefab_status` 确认连的是本项目。
2. 改过磁盘上的脚本/资源 → `refresh_unity_assets`，再 `get_unity_compile_status` 带 `waitSeconds`（如 180）。
   **判断结果是否新鲜看 `compileStatus.resultStale`**：为 `true` 说明这份结果早于你刚才请求的刷新
   （编辑器在后台时会把编译推迟），此时 `errorCount=0` 不代表你的改动通过了编译。
   `waitSeconds` 会一直等到"不在编译且不 stale"或预算耗尽，别自己反复轮询；
   **新加的 `[SerializeField]` 字段要等这一步完成后才能被 `setValue` 写入。**
3. `find_prefabs` 拿路径。需要"像双击一样"进编辑态时用 `open_prefab_stage`
   （之后可用 `targetMode=prefabStage` 直接改实时对象，配合截图看效果）。
   若已有编辑态且有未保存改动，`open_prefab_stage` 会直接报错而不是切换 —— 否则 Unity 会弹模态框把桥卡住。
4. `get_prefab_tree` 只读需要的深度；大型 Prefab 先用 `nameFilter`/`componentTypeFilter` 定位，
   再传 `rootObjectId` 查子树。`compact` **默认 true**：省略 `hierarchyPath`、`depth`、组件全名，
   并剔除 `CanvasRenderer` 这类没有可读写字段的噪音组件（剔除不影响其余组件的 `componentIndex`，它随每条一起发）。
   用 `componentTypeFilter` 直接查噪音组件时会自动保留它。
5. `get_component_fields`：**要看多个组件时用 `targets` 一次读完**（每项 `{objectId, componentIndex}`，
   `componentIndex=-1` 表示该节点所有组件），比一个组件一次调用省得多。`compact` **默认 true**。
   可用 `fieldNameFilter`、`onlyObjectReferences`、`onlyUnassigned` 缩小结果；
   `fieldNameFilter` 支持逗号/竖线分隔多个关键词（命中任一即可），
   例如 `AnchoredPosition,SizeDelta,Sprite` 一次读齐；`get_prefab_tree` 的
   `nameFilter`/`componentTypeFilter` 同样支持多词。
   传 `propertyPath` 可展开嵌套结构或数组（返回 size + 各元素）。
6. `find_binding_candidates` 找引用候选（需要时），把候选的 `value` 原样交给下一步。
7. `edit_prefab` 一次执行一批改动：默认直接写入（一次备份、一次保存）；不确定时先用
   `dryRun=true`，拿返回的 `planId` 再提交，第二次不用重发整批 `operations`。
   目标文件尚不存在时加 `createIfMissing`，根节点创建、组件配置和保存仍是同一个事务。
8. `validate_prefab` 收尾体检。

### 两种寻址写法

- **sibling index 路径**：`0/2/1`，读树拿到什么就用什么；插入/重排/删除之后会整体平移，要重新读树。
- **名字路径**：`path:Panel2Paint/Pen/star1`（可带根节点名，即 `hierarchyPath` 原样可用）。
  不受同级顺序影响，跨调用稳定；同名兄弟写 `star2[1]` 选第几个，有歧义时会报错并列出候选 objectId。

两种写法在所有工具的 `objectId`/`parentObjectId`/`rootObjectId`/`candidateRootObjectId` 上通用。

## 布局视觉验证（不需要 PlayMode）

改完 UI 位置尺寸后自己看一眼，别让人当眼睛：

1. `open_prefab_stage` 进编辑态 → `edit_prefab`（`targetMode=prefabStage` 可边改边看）。
2. `capture_unity_screenshot` 带 `captureTarget=sceneView, frameContent=true`。
   走 Scene View 相机的离屏渲染：不要窗口焦点、不要 PlayMode、不读桌面像素，Unity 在后台也能出图。
   `frameContent` 会把视角对准当前编辑态的 Prefab（没有编辑态时对准场景选中物体）。
3. 不满意就接着改，再截一次。

## PlayMode 视觉验证

1. 用 `control_unity_play_mode` 进入 PlayMode，再调用 `capture_unity_screenshot`：
   `captureTarget=gameView, annotateUi=true`。图中编号与 `uiElements` 一一对应；
   元素的 `path`、`centerX`、`centerY` 可直接用于下一步。
2. 调用 `simulate_playmode_input`：
   - `click` / `drag` 走当前 `EventSystem` 的射线命中与 pointer handler；若目标被遮挡会报出遮挡物。
   - `keyPress` / `keyDown` / `keyUp` 走项目已有的 Input System，不修改 Player Settings。
3. 再截一次 `gameView` 验证结果。只需要元素元数据时使用 `elementsOnly=true`，避免传输图片。

输入工具只开放固定动作，不执行动态 C#，也不向运行时场景挂常驻 Overlay。坐标统一为
PlayMode 屏幕像素、左上角原点；桌面窗口截图坐标不与输入混用。

## 编辑目标 targetMode

`get_prefab_tree`、`get_component_fields`、`find_binding_candidates`、`edit_prefab`、`validate_prefab`
都支持 `targetMode`：

- `prefabAsset`（默认）：读写磁盘上的 Prefab，需要 `prefabPath`。走离屏副本，
  `dryRun=true` 是真正的内存预演，默认（不传）一次备份、一次落盘。
- `prefabStage`：当前双击进入的 Prefab 编辑态。直接改实时对象，写入后仅标脏，由你 `Ctrl+S` 保存；不备份。
- `openScene`：当前打开的场景，需配合 `sceneRootName`（场景里某个根物体的名字），`objectId=0` 即该根物体。

`prefabStage`/`openScene` 是实时对象、无离屏副本，因此 `edit_prefab` 对它们不支持 `dryRun`。
这两种模式下所有写操作都通过 `Undo` API 注册：批次中途失败自动 `Undo.RevertAllDownToGroup` 回滚，
成功后整批合并成一个 Undo 步骤，可以 `Ctrl+Z` 一次撤销全部改动。

## 跨请求事务：plan / revision / idempotency

`prefabAsset` 的预演会返回一小时有效的 `planId` 和内容 `revision`：

```json
{
  "prefabPath": "Assets/UI/Panel.prefab",
  "dryRun": true,
  "operations": [
    { "op": "createObject", "parentObjectId": "0", "newName": "Content" }
  ]
}
```

默认 `responseMode=summary`，成功响应只保留事务状态和后续还要引用的创建结果：

```json
{
  "edit": {
    "operationCount": 1,
    "completed": 1,
    "planId": "p_0123456789abcdef0123",
    "revision": "89abcdef0123456789abcdef",
    "results": [{ "op": "createObject", "objectId": "0/0" }]
  }
}
```

确认后只传计划和一个调用方生成的幂等键：

```json
{
  "planId": "p_0123456789abcdef0123",
  "idempotencyKey": "panel-content-20260730-01"
}
```

- 提交计划时会重新比对 `revision`；目标已变化就拒绝，不会把旧预演覆盖到新内容上。
- 不走计划也可在普通写请求里传 `expectedRevision` 做乐观并发检查。
- 同一个 `idempotencyKey` + 同一个请求会重放已保存的响应，不会重复建节点；同键不同请求直接冲突。
  Unity 若在“可能已经保存、但结果还没记完”时中断，重试会安全拒绝而不是冒险重复写。
- 每个目标按规范化完整路径持有跨进程 Mutex；创建前后的锁键不会因新 GUID 出现而切换。
- `prefabAsset` 与同资源的 `prefabStage` 共用一把锁；若该 Stage 有未保存内容，
  离屏资产编辑会被拒绝，避免两份内容互相覆盖。
- 计划保存在 `Library/PrefabMcpTransactions/Plans` 1 小时；幂等记录保留 24 小时，均不进入版本库。
- `responseMode=changed` 返回全部操作结果但省略重复层级路径；`full` 保留原来的完整响应。
  出错时三种模式都会保留 `errorCode` 和 `failedOpIndex`。

## edit_prefab 操作一览

整批事务式执行：任一步失败即中止、不保存（实时目标则 Undo 回滚）；`dryRun=true` 全程内存预演。

| op | 关键参数 | 说明 |
| --- | --- | --- |
| `rename` | `objectId`, `newName` | 重命名节点 |
| `setActive` | `objectId`, `active`("true"/"false") | 显隐 |
| `reparent` | `objectId`, `parentObjectId`, `siblingIndex?` | 移动节点（保持局部变换），禁止移到自身子级 |
| `setSiblingIndex` | `objectId`, `siblingIndex` | 调整同级顺序 |
| `delete` | `objectId` | 删除节点（禁止根） |
| `duplicate` | `objectId`, `newName?`, `siblingIndex?` | 复制到原节点旁（嵌套 Prefab 连接会被打散） |
| `createObject` | `parentObjectId`, `newName?`, `siblingIndex?` | 新建空节点；父节点是 RectTransform 时自动带 RectTransform |
| `createUi` | `parentObjectId`, `elementType`, `newName?`, `label?`, `width?`, `height?` | 建 UI 预设：container/image/button/tmpText/verticalLayout/scrollView，带项目默认字体与颜色 |
| `instantiatePrefab` | `parentObjectId`, `sourcePrefabPath`, `newName?` | 实例化另一个 Prefab 为嵌套子节点 |
| `addComponent` | `objectId`, `componentType`, `values?`, `componentRef?` | 添加并可内联配置；`componentRef` 声明批内别名 |
| `ensureComponent` | `objectId`, `componentType`, `values?`, `componentRef?` | 没有就添加、唯一一个就复用；多个同类型时报错，不会猜 |
| `removeComponent` | 组件选择器 | 支持 `componentRef` / 唯一 `componentType` / `componentIndex`；禁止移除 Transform，RequireComponent 依赖会失败 |
| `removeMissingScripts` | `objectId` | 清理该节点上的丢失脚本 |
| `setValue` | 组件选择器、`propertyPath`, `value` | 通用序列化值写入，见下方格式 |
| `setValues` | 组件选择器、`values` | 一次写同一组件多个字段；`values` 是 `{propertyPath: 值}` |

组件选择器：

- `componentRef`：批内优先用命名别名或 `$n`（第 n 条产出的组件），结构变化也不会漂移；
  上次响应的 `object:<objectId>#<componentIndex>` 也可回填，但跨调用发生结构变化后应重新查询。
- `objectId + componentType`：跨调用优先，按唯一类型定位；没有或同类型多个都会明确报错。
- `objectId + componentIndex`：兼容旧调用，结构变化后可能漂移。

`setValue` / `setValues` / `addComponent.values` / `ensureComponent.values` 可加
`setIfDifferent=true`：序列化结果完全相同时不 Apply，不产生无意义脏标记或 Undo。

**创建/移动类 op 可直接摆 RectTransform**：`createObject`/`createUi`/`duplicate`/`instantiatePrefab`/`reparent`
都接受 `anchor`（center/stretch/top/bottom/left/right/topLeft/topRight/bottomLeft/bottomRight/
stretchTop/stretchBottom/stretchLeft/stretchRight，pivot 取 anchorMin/Max 中点，拉伸预设先把 offset 归零）、
`anchoredPosition`（`"x,y"`）、`width`/`height`（`createUi` 之外写 `sizeDelta`）。省掉建完再补几条 `setValue`。

**批内引用前序结果：`objectId`/`parentObjectId` 可以写 `$n`**（n 是本批次操作序号，从 1 开始），
指向第 n 条操作作用的节点。结构改动会让后面的 sibling index 全部平移，让 AI 自己推演是这套协议
最容易出错的地方；`createObject`/`createUi`/`duplicate`/`instantiatePrefab` 之后一律用 `$n`。

`$n` **绑的是 Transform 实例，不是当时的 objectId**：批内后续的 `setSiblingIndex`/`reparent`
不会让它改指到别的节点上。同理，响应里每条结果的 `objectId`/`hierarchyPath` 都按**批次结束后的最终层级**重算，
拿回去可以接着用。

组件也有同样的稳定句柄：`addComponent` / `ensureComponent` 的结果可由后续操作用
`componentRef="$n"` 直接选中；给生产操作声明 `componentRef="board"` 后，也可用
`componentRef="board"`。响应会把它重算成最终的 `object:0/…#index`，可跨调用回填。

```json
[
  { "op": "createObject", "parentObjectId": "0", "newName": "Board" },
  { "op": "ensureComponent", "objectId": "$1", "componentType": "CanvasGroup",
    "componentRef": "boardGroup",
    "values": { "m_Alpha": "0.8", "m_Interactable": "true" },
    "setIfDifferent": true },
  { "op": "setValue", "componentRef": "boardGroup",
    "propertyPath": "m_BlocksRaycasts", "value": "false", "setIfDifferent": true }
]
```

### 创建全新 Prefab

`targetMode=prefabAsset`（默认）且 `prefabPath` 不存在时，传：

```json
{
  "prefabPath": "Assets/UI/Generated/EmbroideryBoard.prefab",
  "createIfMissing": {
    "rootName": "EmbroideryBoard",
    "transformType": "RectTransform"
  },
  "operations": [
    {
      "op": "ensureComponent",
      "objectId": "0",
      "componentType": "CanvasGroup",
      "values": { "m_Alpha": "1" }
    }
  ]
}
```

`rootName` 默认取文件名，`transformType` 默认 `Transform`，也可用 `RectTransform`。
父目录不存在时会在正式提交阶段创建；`dryRun=true` 连目录也不会落盘。若目标已经存在，
`createIfMissing` 不会覆盖或重建它，而是按普通编辑继续执行。

`setValue` 的 `value` 一律是字符串：数字/字符串直接写；bool `true|false`；枚举名或整数；Color `#RRGGBBAA`；
Vector2 `x,y`；Vector3 `x,y,z`；Vector4/Quaternion `x,y,z,w`（Quaternion 也接受欧拉角 `x,y,z`）；
Rect `x,y,w,h`；对象引用四种写法：

- `null`
- `asset:Assets/路径[#子资产名]` —— ScriptableObject、Sprite（图集里用子资产名区分）
- `asset:Assets/某个.prefab@objectId[#componentIndex]` —— 引用**别的 Prefab 内部**的节点或组件
- `object:<objectId|$n>[#componentIndex]` —— 当前目标层级内（`-1` 表示 GameObject，省略时按字段类型自动取组件）；
  `$n` 同样可用，于是「建节点 + 绑到别的组件字段上」能落在同一批里：`object:$1#2`
- `component:<componentRef|$n>` —— 直接引用前序 `addComponent` / `ensureComponent` 产出的组件，
  无需知道它添加后的 index，例如 `component:boardGroup`

**数组/List 整体覆盖**：`value` 写成 JSON 数组即可（`size` 自动跟着变），元素各自沿用上面的标量写法：

```json
{ "op": "setValue", "objectId": "path:Root/Sparkle", "componentIndex": 1,
  "propertyPath": "sparkleSprites",
  "value": "[\"asset:Assets/UI/star1.png\", \"asset:Assets/UI/star2.png\"]" }
```

也可以只改某一个元素（`propertyPath=xxx.Array.data[2]`）或只改长度（`xxx.Array.size`）。

**读出来的值可以原样回填**：`get_component_fields` 返回 `object:0/0/3#4:Button` 这种形式，
末尾的 `:类型名` 只是给人看的可读后缀，`setValue` 会忽略它；`find_binding_candidates` 的 `value` 同理。

## 写坏了怎么办

`prefabAsset` 模式每次落盘前都会把原文件复制到 `Library/PrefabMcpBackups/`
（文件名带时间戳与 Prefab guid 前 8 位），路径在响应的 `edit.backupPath` 里。
用 `restore_prefab_backup` 列出或还原：默认只列（最新在前），带 `backupPath` 且 `listOnly=false` 才真的覆盖，
且**还原前会先给当前内容再备份一份**，所以还原错了还能还原回来。
`prefabStage`/`openScene` 不走备份，用 `Ctrl+Z`（整批一个 Undo 步骤）。

## 工具表从哪来（Unity 关着也能列出工具）

`tools/list` 时 ps1 向桥请求 `action=mcp.tools`；Unity 没开就退回
`Library/PrefabMcpTools.json` —— 那是 Unity 每次域重载导出的同一份内容（内容没变不重写，
以保持 mtime 稳定）。ps1 每次请求前比对这个文件的 mtime，变了就补发
`notifications/tools/list_changed`（`initialize` 已声明 `tools.listChanged=true`）。
也就是说**改 C# 里的 schema 不需要重连客户端**，只需等 Unity 编译完。

改 ps1 也不用重启客户端：MCP 协议没有"重启服务端"这种请求，所以服务端每次请求前比对自身 mtime，
变了就把自己重新 dot-source 一遍，函数逻辑的改动下一次工具调用即生效；重载失败保留旧定义并把原因写到 stderr。
（首次引入热重载本身要重启一次客户端才生效。）

stdin 首行的 BOM 会被剥掉：从 PowerShell 手工灌请求时宿主的 UTF8 编码会带前导 BOM，否则第一条请求必然 Parse error。

## 解释器：PowerShell 7（`pwsh`）

配置里的 `command` 是 **`pwsh`，不是 `powershell.exe`**，所以每台开发机都要装 PowerShell 7。
`Edit > Project Settings > Unity Prefab MCP` 会检测本机是否装了 pwsh，没装就给出「用 winget 安装」和「打开下载页」两个按钮
（检测不只看 PATH：Unity 的环境变量是启动时的快照，刚装完的 pwsh 要重启 Unity 才会出现在 PATH 里）。

原因是 Windows 自带的 `powershell.exe` 永久停在 5.1，它读不带 BOM 的文件时按系统 ANSI 代码页（简中机器是 cp936/GBK）解码，
中文注释行尾的字节配对会吃掉换行、把下一行代码并进注释——那行代码静默不执行，不报任何错。PowerShell 7 默认 UTF-8，没有这个问题。

项目内 `.ps1` 仍然统一存为**带 BOM 的 UTF-8**（`.claude/hooks/ensure-ps1-bom.ps1` 自动补），这样脚本被 5.1 手工跑到时也安全。

## 端口与"幽灵监听"（反复踩了三次才找对根因）

症状：**每个 MCP 调用都挂到 20 秒超时**，报错说"编辑器主线程未处理请求"，但 Unity 明明是响应的
（`Get-NetTCPConnection` 显示端口在 `Listen`，`OwningProcess` 有时还指向一个已经不存在的 pid）。

**真正的根因：资源导入 worker 也在跑同一份桥。** `AssetImportWorker` 是带 `-batchMode` 的完整
Unity 进程，同样会加载编辑器程序集、执行 `[InitializeOnLoad]`，于是它也 `Start()` 了一份桥。
`SO_REUSEADDR` 让它能和编辑器共存在同一个端口上，谁抢到 accept 是不确定的；而 worker 里没有
编辑器的 update 循环来处理主线程队列，**被它接到的请求只能挂到超时**。worker 还会被回收重建，
所以"占用者 pid 已经不存在"这种迷惑现象也来自这里。

（早先误判成"子进程继承了监听 socket 的句柄"，按那个思路做的 `SetHandleInformation` 因此一直没解决问题。）

现在四层防御：

1. **worker 不开桥**：`Application.isBatchMode` 为真直接 return。这是根治。
2. **启动自检**（`QueueSelfProbe` + 内部 action `mcp.ping`）：绑定成功不等于"这个端口是我在服务"。
   桥启动后自己发一个 ping，它必须穿过主线程队列才能回来、回显的实例标识还得是自己的；
   拿到别人的标识、或超时但期间自己的 `Pump` 明明在跑，都判定端口被别的实例抢着 accept，
   于是自动顺延端口重启。超时且 `Pump` 也没动则判为"编辑器在忙"，重试而不误判。
3. **`Stop()` 不交还可疑端口**：旧监听线程 2 秒没退出（可能仍卡在 `Accept` 上并持有端口），
   就抬高端口下限，避免下一次 `Start()` 靠 `SO_REUSEADDR` 绑回同一个端口、两个实例抢连接。
4. **绑不上就换端口**：配置端口不可用时顺延最多 15 个，并把**实际端口**写入
   `Library/PrefabMcpPort.txt`（格式「端口 TAB 实例标识」）。MCP 客户端每次请求都优先读这个文件，
   `-Port` 只作为退路，所以换端口不需要改任何配置。`Library` 不入版本库，天然每台机器一份。
   超时报错还会比对文件里的标识：不是自己就直接告诉你"你连到的是已被取代的残留实例"。

裸 `Socket` 仍然保留（而不是 `HttpListener`）：它能设 `SO_REUSEADDR` 抢回历史残留的非独占 socket，
`HttpListener` 走 http.sys、独占绑定，抢不动（实测 `WSAEACCES`），协议侧自己解析 POST + JSON 也只有几十行。

排查命令：

```powershell
# 实际在用哪个端口（第二列是实例标识）
Get-Content Library\PrefabMcpPort.txt
# 谁在占端口
Get-NetTCPConnection -LocalPort 58732 | Select-Object State, OwningProcess
# 占用者是不是导入 worker（命令行里有 AssetImportWorker 就是）
(Get-CimInstance Win32_Process -Filter "ProcessId=<pid>").CommandLine
```

## 配置

项目共享配置是 `PrefabMcpSettings` ScriptableObject，首次编译后自动创建在
`UnityMcp/Prefab/Settings/PrefabMcpSettings.asset`，可从 `Edit > Project Settings > Unity Prefab MCP`
或 `Tools > Unity MCP > 设置` 编辑。

客户端配置点面板里的「初始化/更新 MCP 配置（Codex + Claude Code）」，它会：

- `.codex/config.toml`：只替换 `[mcp_servers.unity_prefab]` 段，保留其他配置。
- `.mcp.json`：只替换 `mcpServers.unity_prefab`，保留其他服务。

`.mcp.json` 里的 `-File` 用项目相对路径，客户端以项目根为工作目录启动；
ps1 会从自身位置向上找 Unity 项目根，所以换机器、换克隆目录都不用改这个文件。手动配置其他 stdio MCP 客户端：

```json
["-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass",
 "-File", "Assets/0 Core/1 Script/Tool/Editor/UnityMcp/Prefab/McpServer/unity-prefab-mcp.ps1"]
```

## 冒烟测试

`McpServer/mcp-smoke-test.ps1` 另起服务端进程灌真实 JSON-RPC，逐条校验响应：改完 ps1 不重启客户端就能验证。
除一次无内容变化的可恢复 plan 提交外，用例均只读或 `dryRun=true`；该提交用于验证
`dryRun → planId → 幂等提交/重放` 的真实协议。无论后续断言是否成功，`finally` 都优先使用 MCP 自动备份还原，
再用测试前原始字节兜底，并要求 Prefab SHA-256 完全一致。其中还有故意的失败路径
（revision 冲突、路径穿越、事务中止、参数名拼错必须报错）。

## 当前范围

- 支持新建 Prefab、Prefab / 场景内的 `GameObject`/`Component` 引用、项目资产引用、别的 Prefab 内部组件引用。
- 写入受 SO 中写入总开关和目录白名单限制；启用备份时，原 Prefab 会先复制到 `Library/PrefabMcpBackups`。
- 字段发现以 Unity 的 `SerializedProperty` 为准，包括 `public` 和 `[SerializeField] private` 字段。
- 候选类型推断沿 `propertyPath` 解析（含数组元素），但只对对象引用字段有意义。
- `validate_prefab` 只报告能映射回 C# 字段的顶层对象引用；null 仍可能是业务允许的可选字段。
- 编译结果通过 `SessionState` 跨程序集重载保留，退出 Unity 后清空；最多保存最近 200 条错误和警告。
- Prefab 写入按规范化路径加跨进程锁；跨请求的数据竞争由 `revision` 前置条件挡住。
