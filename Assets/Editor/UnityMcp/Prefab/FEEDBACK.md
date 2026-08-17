# 使用反馈与改进记录

AI 实际用这套 MCP 干活时踩到的摩擦点。每条记录「现象 → 为什么疼 → 怎么改」，
已经改掉的写明改法，没改的写明为什么不改。

来源场景：给 `DressMakingSprayPaintGamePanel` 底部加一个 `0/4` 进度文本
（读布局 → 复制现有文本节点 → 改位置字号 → 绑 Inspector 字段）。

## 已改

### 1. 布局改动没有视觉闭环（最要紧）

新节点摆在 `(20, -498)` 是靠读一堆 RectTransform 算出来的：服饰区底边 -468、右边按钮 x=644，
于是推断中下方是空的。但**改完看不到结果**，压没压到别的东西、字号合不合适只能让人去看。
`capture_unity_screenshot` 原先只有 `focusedWindow`（AI 没法让 Unity 获得焦点）和 `gameView`（要 PlayMode）。

改法：新增 `captureTarget=sceneView`，直接把 Scene View 的相机渲到离屏 RenderTexture 再回图。
不依赖窗口焦点、不依赖 PlayMode、不读桌面像素，Unity 在后台也能出图。
配合 `frameContent=true` 把视角对准当前 Prefab 编辑态的内容，
于是 `open_prefab_stage` → `edit_prefab` → `capture_unity_screenshot` 成为一个自校验闭环。

### 2. 等编译这件事没有工具支持

新加的 `[SerializeField]` 字段要等 Unity 编译完才能写入，第一次 `setValue` 必然失败。
而 `resultStale=true` 时的提示是"请切到 Unity 窗口" —— AI 切不了窗口，只能反复轮询或者把活甩回给人。

改法：`get_unity_compile_status` 新增 `waitSeconds`。服务端判断"还在编译/结果还旧"时，
在响应里挂 `retryAfterSeconds`，由 MCP 服务层（ps1）在预算内自动续等并重发，
其间连不上（域重载会把桥打断）也算可重试。于是一次调用就能等到新鲜结果。
措辞同步改掉：不再首推"切窗口"，改为推荐 `waitSeconds`。

`retryAfterSeconds` 是**通用**机制：ps1 不认识 `get_unity_compile_status` 的语义，
只认"请求带 `waitSeconds` + 响应带 `retryAfterSeconds` 就重发"，以后别的工具要等也能直接用。

### 3. "找不到序列化字段" 没说出路

报错只有 `组件 X 上找不到序列化字段 progressText`，看不出是**字段名拼错**还是**脚本还没编译**，
这两种情况的处置完全相反（改参数 vs 等一等再重试）。

改法：报错时列出该组件相近的字段名（前缀/包含/编辑距离），并在检测到
`isCompiling || resultStale` 时补一句"脚本可能尚未编译完，用 waitSeconds 等一下再重试"。

### 4. `removeComponent` 只能按 index

删 `LocalizeStringEvent` 只能写 `componentIndex: 3`，那是从**源节点**的组件表推出来的，
`duplicate` 之后 index 是否还对只能假设。按名字删才是意图本身。

改法：`removeComponent` 接受 `componentType`（短名或全名，与 `addComponent` 同一套解析）。
同一节点上有多个同类型组件时报错并列出各自的 `componentIndex`；
按类型找不到时列出该节点现有组件（带 index），省一次读树。

### 5. `object:$n` 不能用，创建 + 绑定被迫拆成两次调用

想在一批里 `duplicate` 出节点后直接把它绑到根组件的字段上，但 `setValue` 的 `object:` 只认
sibling/path 寻址，文档也没写 `$n` 能不能进去，只好拆成两次调用 —— 结果第二次就撞上了 2 的编译问题。

改法：`setValue`/`setValues` 的 `object:` 引用也支持 `$n`，例如 `object:$1#2`。
`$n` 绑的是 Transform 实例，批内重排也不会指错。
（`asset:xxx.prefab@objectId` 指向的是别的 Prefab 内部，那里没有 `$n` 语义，保持不支持。）

### 6. dry-run 确认后要把整批操作再发一遍

预演本来是为了安全，但大批 `operations` 在确认时完整重发，输入 token 和参数出错面都会翻倍；
两次调用之间 Prefab 还可能已被别人修改。

改法：成功预演返回 `planId + revision`，计划在 `Library` 保存一小时。确认时只传 `planId`，
服务端在同一目标锁内重新比对内容 revision 后执行原操作；版本变了就拒绝，不覆盖新内容。

### 7. 写入超时后不知道该不该重试

客户端断线可能发生在 Prefab 已保存、响应尚未送达之间。盲目重试会重复建节点；
不重试又只能把不确定状态留给人。

改法：写请求可带 `idempotencyKey`。请求意图先以 pending 原子写入 `Library`，成功后保存完整响应；
同键同请求直接重放，同键不同请求冲突。目标锁固定使用规范化完整路径，创建前后不会因 GUID 出现而换锁；
不同目标误用同一 key 也由第二层 key Mutex 串行化，锁序固定为 target → idempotency。

### 8. 成功批次逐条回全部路径和说明，结果比结论贵

大量 `setValue` 同时返回 `objectId + hierarchyPath + detail`，模型真正需要的通常只有是否成功、
新节点地址和新组件句柄。

改法：`responseMode=summary` 成为默认，只保留事务计数、plan/revision、备份路径及创建结果；
`changed` 返回操作明细但省略重复层级路径，`full` 保留旧响应。失败额外给稳定 `errorCode`
和 `failedOpIndex`，内部异常的完整堆栈只进 Unity Console。

## 不改

- **`find_binding_candidates` 没被用上**：这次全程手拼 `object:0/10#2` 没调它。工具本身没问题，
  是"读出来的值能原样回填"这个设计太顺手，反而让候选查询显得多余。属于好事，不动。
- **sibling index 会漂移**：已经有 `path:` 寻址和 `$n` 两条稳定路线，够用。
