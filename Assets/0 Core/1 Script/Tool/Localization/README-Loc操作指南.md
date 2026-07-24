# 多语言（Localization）操作指南 — 供 AI / 开发者使用

本项目多语言的**唯一数据源是各功能目录下的 Loc CSV**（如 `Assets/0 Core/1 Script/Data/Common/CommonLoc.csv`），
StringTable 资产（如 `Assets/AddressableAssets/Local/LocalizationTable/StringTable/CommonUI/CommonUI.asset`）由 CSV 导入生成。

## 组织约定：一张表 ↔ 一个 CSV 目录，每个面板一份 CSV

一张字符串表对应一个 CSV 目录（如 `Data/Factory` ↔ Factory 表），目录下**每个面板一份 CSV**，
全部合并进同一张表。映射关系存在 `Editor/LocWindow/LocWorkbenchConfig.asset`。约定：

- **Key 必须带面板前缀**（如 `FishShop_Title`），因为多份 CSV 合入同一张表，重名 Key 会互相覆盖（工作台导入前会检测并拦截）；
- 跨面板共用的文案（确定/取消/返回等）放 CommonLoc（Common 表），不要复制进各面板 CSV；
- 从 CSV 删掉的 Key 不会自动从表里消失（合并只增不删），用工作台的「重建导入」清理。

## 可视化工作台（人工操作首选，唯一入口）

菜单 `Tools/Loc/多语言工作台`：
- 「工作台」页：左侧选表 → 绑定 CSV 目录 → 右侧可视化编辑各 CSV（加行/删行/改文案/搜索），
  保存自动导入；支持「增量导入全部 CSV」「重建导入（清空表后导入，可清孤儿 Key）」「检测重复 / 孤儿 Key」「新建 CSV」。
  任意 CSV → 字符串表的导入、向 CSV 追加条目都在这里完成，不再有独立的导入/追加窗口。
- 「工具」页：给所有 String 表集合自动标记 Smart String（含 `{}` 占位符的文案批量勾选 IsSmart）。

## 三条铁律

1. **禁止直接修改 StringTable 的 `.asset` / `.asset` 同目录的 SharedTableData**。它们是 Unity 序列化文件，词条散落在多个文件、靠 keyId 关联，手改极易损坏，且下次从 CSV 导入时会被覆盖。
2. **禁止用 Read/Edit 整份读写 Loc CSV**。浪费上下文（token），且逗号/引号转义容易被改坏。
3. **一律通过本目录的 `LocCsv.ps1` 命令行工具做行级增删改查**，改完后在 Unity 里跑一次对应的导入菜单让改动生效。

## LocCsv.ps1 用法

纯文本操作 CSV，不依赖 Unity，编辑器开着时也可安全使用。
**务必用 `&` 调用操作符直接调脚本**，不要再套一层 `powershell -File ...`（多进程转发会打乱带引号/逗号的参数）。

```powershell
# 查（只打印该 Key 各语言值，不用整份读文件）
& "Assets/0 Core/1 Script/Tool/Localization/LocCsv.ps1" -Action Get    -Csv <CSV路径> -Key <Key>

# 增（Key 已存在会报错，不会覆盖）
& "Assets/0 Core/1 Script/Tool/Localization/LocCsv.ps1" -Action Add    -Csv <CSV路径> -Key <Key> -Set 'zh-CN=中文','en=English','ja-JP=日本語'

# 改（只改指定语言列，其余列保持原值）
& "Assets/0 Core/1 Script/Tool/Localization/LocCsv.ps1" -Action Update -Csv <CSV路径> -Key <Key> -Set 'en=Only change English'

# 删
& "Assets/0 Core/1 Script/Tool/Localization/LocCsv.ps1" -Action Remove -Csv <CSV路径> -Key <Key>
```

- `-Csv` 支持绝对路径，或相对于**仓库根目录**（脚本向上查找含 `Assets/` 或 `.git` 的目录）的相对路径，如 `Assets/0 Core/1 Script/Data/Factory/FactoryMainPanel.csv`。
- `-Set` 多个语言必须作为**一个数组**传（一个 `-Set` 后跟逗号分隔的多个 `'code=值'`），不能重复写多个 `-Set`。
- 常用语言代码：`zh-CN` `zh-TW` `en` `ja-JP` `ko` `th` `vi`（以目标 CSV 表头括号内代码为准）。

## 批量操作（≥3 条时优先用）

不要手写循环反复起进程，用 Batch + JSON：

```powershell
& "Assets/0 Core/1 Script/Tool/Localization/LocCsv.ps1" -Action Batch -File <JSON文件路径>
```

JSON 是一个数组，每项一条操作：

```json
[
  { "action": "Remove", "csv": "Assets/.../XxxLoc.csv", "key": "OldKey" },
  { "action": "Add",    "csv": "Assets/.../XxxLoc.csv", "key": "NewKey",
    "set": { "zh-CN": "中文", "zh-TW": "繁中", "en": "English", "ja-JP": "日本語", "ko": "한국어", "th": "ไทย", "vi": "Việt" } },
  { "action": "Update", "csv": "Assets/.../XxxLoc.csv", "key": "SomeKey", "set": { "en": "Only change English" } }
]
```

JSON 文件用任何工具写都行（含中日韩泰越字符没问题），Batch 模式按 UTF-8 显式读取，
从机制上避免了 Windows PowerShell 5.1 按系统代码页解析临时 .ps1 源码导致的乱码坑。
单条失败不会中断整批，输出里标 `FAIL` 并继续，最后汇总成功/失败数（有失败退出码为 1）。

## 改完 CSV 后：导入进 StringTable

脚本只改 CSV 源文件，要在游戏里生效需在 Unity 编辑器跑一次对应导入菜单：

| 表 | 菜单 |
|---|---|
| 通用（CommonLoc → CommonUI 表） | `Tools/Loc/Common 一键创建并导入` |
| GameEnterPanel | `Tools/Loc/GameEnterPanel 一键创建并导入` |
| 工厂小游戏（Factory 表） | `Tools/工厂小游戏/导入「工厂」全部多语言 → Factory 表` |
| 其他任意 CSV | `Tools/Loc/多语言工作台`（选中表 →「增量导入全部 CSV」/「重建导入」） |

导入后若 Addressable 登记异常，可跑 `Tools/Loc/修复表的 Addressable 登记`。

## 人工手动加条目

不走命令行时用 `Tools/Loc/多语言工作台`：选中表和 CSV → 「＋ 加行」填 Key/译文 → 「保存 CSV」（默认保存后自动导入）。

## CSV 格式约定（与 LocCsvEditor.cs / LocCsvMerger.cs 一致）

- 表头：`Key,Id,语言名(代码),...`；Key 与非空语言值加引号，Id 与空语言列留空不加引号。
- 引号转义：内部 `"` 写成 `""`；保留原文件 BOM 与换行风格。
- 这些规则由 LocCsv.ps1 自动处理，**这也是不要手工编辑 CSV 的原因**。
