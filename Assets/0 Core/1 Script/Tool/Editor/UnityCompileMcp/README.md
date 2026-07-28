# Unity Compile MCP

让 AI 能主动触发 Unity 编译并读回结果的独立 MCP 服务。

**和 InspectorBridge 完全独立**：没有 Unity 侧脚本、不占端口、不共享任何代码。
删掉这个文件夹，InspectorBridge 照常工作；删掉 InspectorBridge，这个也照常工作。

## 为什么不做进 InspectorBridge

编辑器在后台时 `EditorApplication.update` 其实还在跑（InspectorBridge 在后台照常响应请求），
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
| `force_unity_compile` | 切前台 + 发 Ctrl+R，等到 `Library/ScriptAssemblies` 真的被重写，然后把焦点还给原窗口，返回错误和警告。参数：`timeoutSeconds`（默认 120）、`restoreFocus`（默认 true）、`sendRefreshHotkey`（默认 true）、`maxResults`（默认 50）。 |
| `read_unity_compile_log` | 只读 `Editor.log` 末尾的编译诊断，不碰编辑器、不抢焦点。参数：`tailKilobytes`（默认 256）、`maxResults`。 |

**成功判据是程序集有没有被重写**，不是日志长度：日志里的 `Refreshing native plugins` 之类
普通刷新也会打，用它当收尾标记会把"只刷新没编译"误报成"编译完成"。返回里因此带三个状态字段：

- `compiled`：这次调用期间程序集确实被重写了。
- `pendingChanges`：还有 `.cs` 比程序集新 —— 为 true 说明这份诊断不代表磁盘上代码的最新状态。
- `focused`：Unity 窗口是否真的被切到前台。为 false 就是抢焦点失败（Unity 以管理员身份运行而
  本进程不是、屏幕锁定、远程会话断开等），这时什么都不会发生，只能人工点一下窗口。
- `hotkeySent`：Ctrl+R 是否真的发出去了（`focused=false` 时必为 false）。

`compiled=false` + `pendingChanges=true` 就是"Unity 拒绝编译"：可能仍在导入资源，也可能处于
Play 模式或 Reload Assemblies 被锁；报错里会这么说，而不是假报成功。诊断只取**调用之后**
新追加的日志，不会把历史结果误当本次结果。

改完这个 ps1 不用重连客户端：它每次请求前比对自身 mtime，变了就重新 dot-source（改 schema 仍需重连）。

## 注意

- 会短暂抢走窗口焦点（这正是它起作用的方式），默认结束后归还。
- 通过命令行里的 `-projectpath` 匹配对应项目的编辑器主进程，自动排除 `-batchMode` 的资源导入 worker，
  多开 Unity 时不会切错窗口。
- **`.ps1` 统一存为「带 BOM 的 UTF-8」**（`.claude/hooks/ensure-ps1-bom.ps1` 会自动补）：Windows PowerShell 5.1
  读无 BOM 文件时按系统 ANSI 代码页解码，GBK 双字节配对会吃掉中文注释后的换行，把下一行代码并进注释后静默不执行。
  服务现在由 `pwsh`（PowerShell 7，默认 UTF-8）启动，BOM 不再是硬要求，但保留下来能防住被 5.1 手工跑到的情况。

## 配置

已注册在项目级 `.mcp.json`（Claude Code）和 `.codex/config.toml`（Codex）里，服务名 `unity_compile`，`command` 为 `pwsh`。
两处都写在 InspectorBridge 的 generated 块之外，InspectorBridge 的配置生成按钮不会覆盖，
所以这个模块的解释器要改时得手工同步两份配置。
