# Unity Compile MCP

让 AI 能主动触发 Unity 编译并读回结果的独立 MCP 服务。

**和 Prefab Bridge 完全独立**：不占端口、不依赖桥服务、不共享代码。
同目录的 `UnityCompileStateReporter.cs` 只监听 `CompilationPipeline`，并把状态原子写入
`Library/UnityCompileMcp/compile-state-<UnityPid>.json`。
删掉这个文件夹，Prefab Bridge 照常工作；删掉 Prefab 模块，这个也照常工作。

## 为什么保持独立运行

编辑器在后台时 `EditorApplication.update` 其实还在跑（Prefab Bridge 在后台照常响应请求），
但 `AssetDatabase.Refresh()` + `CompilationPipeline.RequestScriptCompilation()` 是空转 ——
实测在后台调完连新脚本的 `.meta` 都不生成，编译被推迟到窗口重新获得焦点。
也就是说「让 Unity 开始编译」这件事本质上必须**从进程外**解决：先把焦点给它。

所以这里走纯客户端路线，两步：

1. **把 Unity 主窗口切到前台**（关键难点，见下）；
2. 确认前台窗口真的是 Unity 之后，发一次 Ctrl+R（`Assets > Refresh`），强制刷新 + 重新编译。
   发按键前先比对前台句柄，抢不到焦点就不发，免得 Ctrl+R 打到别的窗口（浏览器的 Ctrl+R 是刷新页面）。

### 抢焦点：为什么曾经"整个服务像没用一样"

后台进程调 `SetForegroundWindow` / `SwitchToThisWindow` 会被 Windows **前台锁**静默拒绝
（返回 false 或干脆无事发生，实测两者都拿不到前台）。于是 `hotkeySent=false`、Ctrl+R 不敢发、
Unity 也拿不到焦点 —— 表现就是"必须人工点一下 Unity 窗口才编译"。

现在 `UnityFg::Force` 按前台锁的放行条件来做，能稳定抢到：

1. `SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0)` 把前台锁超时置 0；
2. `AttachThreadInput` 挂到**当前前台窗口所属线程**，借它的输入队列，本线程于是持有前台输入状态；
3. 然后 `BringWindowToTop` + `SetForegroundWindow` + `SetActiveWindow`，轮询确认前台句柄真的变了；
4. 第一轮失败再来一轮，并在其中轻点一下 **Ctrl**（给本线程盖上"刚有用户输入"的戳）。
   用 Ctrl 而不是传统的 Alt：Alt 单击会在部分程序里激活菜单栏，Ctrl 单击对所有程序都无副作用。

实测第 1 步成功后 Unity 一般就自动完成 refresh + 编译了，Ctrl+R 只是防"只刷新不编译"的保险。

## 工具

| 工具 | 说明 |
| --- | --- |
| `force_unity_compile` | 有待编译脚本时，按项目获取进程间编译锁、切前台、发 Ctrl+R，然后等待 Unity 报告该编译 generation 结束并返回诊断；成功和失败都无需等待 DLL 更新时间。同一源码 generation 的并发请求复用第一个结果。参数：`timeoutSeconds`（默认 120）、`restoreFocus`（默认 true）、`sendRefreshHotkey`（默认 true）、`maxResults`（默认 50）。 |
| `read_unity_compile_log` | 只读 `Editor.log` 末尾的编译诊断，不碰编辑器、不抢焦点。参数：`tailKilobytes`（默认 256）、`maxResults`。 |

调用开始时先把 `.cs` 映射到最近的 asmdef/asmref 或 Unity 默认程序集，再分别比较目标
`Library/ScriptAssemblies/*.dll`：没有待编译改动会直接返回，
不会等待 Mutex、查找 Unity 窗口、抢焦点或发送按键。

有改动时，**首选完成判据是 Unity 的 `CompilationPipeline.compilationFinished` 事件**。
状态文件带持久递增的 `generation`、成功状态、错误/警告计数和最多 200 条诊断，因此编译失败、
DLL 完全不更新时也能在 Unity 实际结束后立即返回。报告器还记录该 generation 开始时的源码
指纹；成功结果可覆盖“源码时间较新但产物内容相同、Unity 复用了 DLL”的时间戳假阳性。
返回里的主要状态字段：

- `compiled`：本次请求对应的编译 generation 已实际结束；编译有错误时也为 true，并同时返回 `isError=true`。
- `pendingChanges`：还有 `.cs` 未被成功 generation 或所属程序集覆盖 —— 为 true 说明磁盘代码仍未全部编译成功。
- `focused`：Unity 窗口是否真的被切到前台。为 false 就是抢焦点失败（Unity 以管理员身份运行而
  本进程不是、屏幕锁定、远程会话断开等）；无改动早退、复用并发结果和 Unity 已经在编译时也为 false。
- `hotkeySent`：Ctrl+R 是否真的发出去了（`focused=false` 时必为 false）。
- `completionSource`：通常为 `unity-event`；首次安装报告器时可能为 `tundra-log`、
  `editor-log` 或 `assembly-settled`。
- `compilationGeneration`：Unity 状态报告器记录的 generation。

`compiled=false` + `pendingChanges=true` 说明超时前没有收到结束事件：可能仍在导入资源，也可能
处于 Play 模式或 Reload Assemblies 被锁。诊断只取对应 generation 的状态；兼容回退也只读取
**调用之后**新追加的日志，不会把历史结果误当本次结果。

### 首次安装与并发

- 第一次加入 `UnityCompileStateReporter.cs` 时，旧的 Editor 程序集尚未包含它，无法报告自己的
  首轮编译。Unity 6 首轮改读 `Library/Bee/tundra.log.json` 中 Csc 节点的退出码与 stdout，
  连失败且 DLL 不更新的情况也能返回；只在 mtime/长度指纹变化并稳定 2 秒后读取一次共享尾部
  （最多 16MB），不会在循环中全量读取大日志。Bee 日志不可用时才使用 Editor.log 或
  `ScriptAssemblies` 稳定指纹。首轮成功后自动切换到 Unity 事件路径。
- 请求进入 Mutex 前会保存源码指纹、状态 generation、程序集指纹和日志偏移；拿到锁后重新检查。
  若前一个请求已经编译了同一源码指纹，直接返回它的成功或失败诊断并标记
  `mergedWithConcurrentCompile=true`，不再次抢焦点或编译。
- 等锁期间如果磁盘上又出现新一代源码，指纹不同，当前请求会继续编译新 generation，不会误复用旧结果。
- `AssetImportWorker` 同样会执行 `[InitializeOnLoad]`。报告器在 `Application.isBatchMode` 下直接
  返回，且状态按 PID 隔离；外部服务只接受它已经定位到的主 Unity PID。旧版 worker 即使仍向
  `compile-state.json` 写入，也会因为 PID 不匹配而被忽略，首次升级期间自动走 Bee fallback。

改完这个 ps1 不用重连客户端：它每次请求前比对自身 mtime，变了就重新 dot-source（改 schema 仍需重连）。

## 注意

- 会短暂抢走窗口焦点（这正是它起作用的方式），默认结束后归还。
- 同一项目的多个 `unity_compile` 服务通过按项目路径生成的命名 Mutex 串行化，并在锁后重查 generation，
  避免同时抢焦点、发送 Ctrl+R、互相恢复焦点或重复编译。
- 通过命令行里的 `-projectpath` 匹配对应项目的编辑器主进程，同时排除 `-batchMode` 和
  `AssetImportWorker`，
  多开 Unity 时不会切错窗口。
- **`.ps1` 统一存为「带 BOM 的 UTF-8」**（`.claude/hooks/ensure-ps1-bom.ps1` 会自动补）：Windows PowerShell 5.1
  读无 BOM 文件时按系统 ANSI 代码页解码，GBK 双字节配对会吃掉中文注释后的换行，把下一行代码并进注释后静默不执行。
  服务现在由 `pwsh`（PowerShell 7，默认 UTF-8）启动，BOM 不再是硬要求，但保留下来能防住被 5.1 手工跑到的情况。

## 配置

已注册在项目级 `.mcp.json`（Claude Code）和 `.codex/config.toml`（Codex）里，服务名 `unity_compile`，`command` 为 `pwsh`。
Compile 与 Prefab 的启动路径统一位于 `UnityMcp` 下；调整目录时必须同步更新两份客户端配置。
