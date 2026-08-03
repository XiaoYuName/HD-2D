# CSV 驱动配置管线（CsvSyncedConfig）说明

一套「CSV → ScriptableObject」的通用配置管线：把一张 CSV 拖到配置资产上，表格一改就自动同步进 SO，
**新增一个配置零编辑器代码**。参考实现见 `FishConfig` / `FishRodConfig` / `FactoryEquipConfig`。

## 涉及文件


| 文件                                      | 作用                                            |
| ----------------------------------------- | ----------------------------------------------- |
| `Tool/Common/CsvSyncedConfigAttribute.cs` | `[CsvSyncedConfig]` 标记特性，挂在配置类上      |
| `Tool/Common/CsvTool.cs`                  | 通用 CSV 解析（引号感知切分、共享读、BOM 处理） |
| `Tool/Common/CsvFormat.cs`                | 分隔符/引号/BOM 等字符常量                      |
| `Tool/Editor/CsvConfigAutoSync.cs`        | 导入器：CSV 变更自动同步 + 齿轮菜单手动导入     |
| `Tool/Editor/CsvConfigCodeGen.cs`         | 从 CSV 表头一键生成配置类代码                   |

## 一、新增一个 CSV 配置的完整流程

1. **写 CSV**（推荐四行表头，旧三行表头仍兼容，见下）放到 `0 Core/1 Script/Data/<模块>/` 下。
2. **生成配置类**：在 Project 里选中该 CSV → 右键 `CSV 生成配置类`，会生成 `XxxConfig` + `XxxItemData` 两个类
   （已挂 `[CsvSyncedConfig]`，含 `dataDict` / `csvTable` 两字段）。也可参考 `FishConfig` 手写。
3. **建资产**：`Create → Configs/XxxConfig` 创建 SO。
4. **绑表**：把 CSV 拖到资产的 `csvTable` 字段。
5. 之后 CSV 一改就**自动同步**；也可在资产 Inspector 右上角齿轮菜单点 `从 CSV 导入配置` 手动导一次。

> 手写配置类时只需满足约定：类挂 `[CsvSyncedConfig]`，含一个 `Dictionary<TKey, TData> dataDict`
> 和一个 `UnityEngine.Object csvTable` 序列化字段。`TKey` 支持 `string/long/int` 等基础类型。

## 二、CSV 表头约定（推荐 4 行表头 + 数据）

```
第1行  字段名（PascalCase，需与数据类字段名一致，忽略大小写）
第2行  纯类型（str/int/float/bool/long/double/Color/枚举名/List<T>/Dictionary<K,V>/自定义类型）
第3行  可选格式（普通列留空，多值/复合列写 sep=+；字典还可写 kvsep=:）
第4行  中文标签（仅给人看，代码生成时作字段注释；解析时忽略）
第5行~ 数据行，每行一条；Id 列作字典 key，Id 为空的行跳过
```

推荐示例：

```csv
ID,Remark,Name,FavorMax
long,string,TbLocalzationKeyData,List<long>
,,sep=+,sep=+
唯一ID,备注,名字,好感度上限
10001,马吉,CharacterNames+Character_NPC_02,100+100+100
```

这样类型和编码规则各占一行，在 Excel/WPS 中能直接按行区分。没有格式行的旧三行 CSV 仍按原方式读取。
同时兼容 Luban 写法 `TbLocalzationKeyData#sep=+` 和 `"(list#sep=+),long"`，便于迁移，但新表建议使用上面的四行格式。

- 列的**顺序随意**，靠列名匹配字段名（忽略大小写：字段 `nameKey` 能对上列 `NameKey`）。
- 数据类里有、但 CSV 没有的列，保持字段默认值；CSV 有、类里没有的列，忽略。

## 三、⚠️ 必须遵守的坑（本项目实际踩过）

### 1. CSV 必须存成「UTF-8 带 BOM」

导入器用 `new UTF8Encoding(true)` 读文件。**无 BOM 的 UTF-8** 在中文系统上被 Excel/WPS 打开会
被当成 GBK，一保存就把中文写乱（Inspector 里看到乱码即此因）。

- 用 Excel/WPS 另存时选「CSV UTF-8（带 BOM）」。
- 程序写 CSV 一律用 `new UTF8Encoding(true)`（`CsvConfigCodeGen` 已如此）。
- 用一般文本编辑器/工具写出的文件常常**丢 BOM**，需手动补 `EF BB BF` 文件头，例如 PowerShell：
  ```powershell
  $p = "路径.csv"; $b = [IO.File]::ReadAllBytes($p)
  if (-not ($b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF)) {
      [IO.File]::WriteAllBytes($p, [byte[]](0xEF,0xBB,0xBF) + $b)
  }
  ```

### 2. 多值列显式声明分隔符

`List<T>` / `T[]` / 自定义复合类型这类一格多值的列，建议在格式行显式写 `sep=+`；分隔符也可以换成
`|`、`;` 等任意字符串。未写格式行时，为兼容旧表，列表仍会识别 `+`、`、`、`;`。

字典默认写法为 `Key1:Value1;Key2:Value2`。需要定制时，格式格可写 `sep=|#kvsep=:`：
`sep` 控制条目之间的分隔，`kvsep` 控制键和值之间的分隔。

### 3. 首行必须是字段名，不能多出标题行

行数是硬约定（第 1 行字段名 / 第 2 行类型 / 第 3 行可选格式行 / 随后中文标签）。Excel、WPS 从
「表格」对象另存 CSV 时会在最前面多插一行 `Column1,Column2,...`，多这一行后整表读不到 `Id` 列，
全部数据行被跳过 → 导入 0 条（FishConfig 实际踩过）。

现在导入器会拦截这两种情况并报错，不会再用空数据覆盖 SO：

- 表头缺 `Id` 列 → `表头没有 Id 列…请删掉多余的标题行`；
- 解析结果为 0 条 → `已放弃导入（保留 SO 原有内容）`。

看到这两条报错，先把 CSV 首行改回字段名行。

## 四、支持的单元格类型


| 类型                         | 单元格写法                         | 示例                              |
| ---------------------------- | ---------------------------------- | --------------------------------- |
| `string`                     | 原样文本                           | `伺服注塑机`                      |
| `int/long/float/double/bool` | 直接写值                           | `100` / `true`                    |
| `Color`                      | `#RRGGBB` / `#RRGGBBAA`            | `#FF8800`                         |
| 枚举                         | 枚举项名（忽略大小写）             | `Yield`                           |
| `List<T>` / `T[]`            | 多值按格式行的`sep` 分隔           | `100+200+300`                     |
| `Dictionary<K,V>`            | 项间按`sep`、键值间按 `kvsep` 分隔 | `1001:2;1002:5`                   |
| 自定义复合类型               | 按`sep` 拆分后依字段声明顺序赋值   | `CharacterNames+Character_NPC_02` |

- `List<T>` / `Dictionary<K,V>` 单元格为空 → 得到空集合（不报错）。
- 某格解析失败（如 `List<int>` 里混入非数字）→ 该字段保持默认值，并在 Console 打 `Debug.LogError` 提示行/列。

## 五、导入行为

- **自动同步**：任意 `.csv` 被重新导入时，`CsvConfigAutoSync` 找到 `csvTable` 引用了它的 SO 自动重导（整体覆盖 `dataDict`）。
- **手动导入**：SO 资产 Inspector 右上角齿轮菜单 → `从 CSV 导入配置`。
- **表头变更**后可对同一文件重新 `CSV 生成配置类` 覆盖（注意会连 `CreateAssetMenu` 等手工改动一起覆盖，谨慎）。
