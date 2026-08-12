# 多语言（Localization）简要说明 — 供 AI 快速知晓

本项目多语言只改 **CSV 源文件**（各功能目录下的 `XxxLoc.csv`），不要用 Read/Edit 整份读写 CSV，
更不要直接改 StringTable 的 `.asset`（Unity 序列化文件，手改极易损坏）。

改 CSV 用命令行工具 `Assets/0 Core/1 Script/Tool/Localization/LocCsv.ps1`：

```powershell
& "Assets/0 Core/1 Script/Tool/Localization/LocCsv.ps1" -Action Add    -Csv <CSV路径> -Key <Key> -Set 'zh-CN=中文','en=English',...
& "Assets/0 Core/1 Script/Tool/Localization/LocCsv.ps1" -Action Update -Csv <CSV路径> -Key <Key> -Set 'en=Only change English'
& "Assets/0 Core/1 Script/Tool/Localization/LocCsv.ps1" -Action Remove -Csv <CSV路径> -Key <Key>
& "Assets/0 Core/1 Script/Tool/Localization/LocCsv.ps1" -Action Get    -Csv <CSV路径> -Key <Key>
```

- `-Csv` 相对路径以**仓库根目录**为基准。
- 改完 CSV 后要在游戏里生效，需提醒用户在 Unity 编辑器里手动跑一次对应的导入菜单，把 CSV 合并进 StringTable（脚本不做这一步，避免跟已打开的 Editor 抢工程锁）。

需要更多细节（批量 JSON 格式、Key 命名前缀约定、导入菜单对照表等）时，
再看同目录下完整版 `README-Loc操作指南.md`。

## 代码里存「一条多语言文案」用 LocKeyRef

配表数据和业务代码**不要直接存 `LocalizedString`**，存 `LocKeyRef`（表名 ＋ Key 两个字符串）：

```csharp
[SerializeField] LocKeyRef name = new();      // Inspector 里是表下拉 ＋ Key 搜索 ＋ 文本预览

name.IsValid()                                 // Key 配了没有（编辑器里也能判，不用跑多语言）
name.Get()                                     // 当前语言的文本
desc.Get(new LocVars().Set("ItemName", "鱼").Set("Value", 2))   // 带占位符
```

`LocKeyRef.Get` 是**唯一**把「表 + Key」交给多语言插件的地方，换插件（或搬到别的项目）只改这一处；
占位符攒在 `LocVars` 里传进去，业务侧因此完全不认识插件的类型。
要随语言切换自动刷新 TMP 的场合仍然用 `LocRef` / `LocText` 那套组件。
