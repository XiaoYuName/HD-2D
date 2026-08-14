using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using XFramework;

/// <summary>
/// 按反射把一条配置记录画成 UIToolkit 控件。**不经过 SerializedProperty** ——
/// 数据是 Odin 序列化的普通 C# 对象（字典值、多态实例），这里直接读写字段，改完由外面 SetDirty。
///
/// 字段类型 → 控件的对照：
/// <list type="bullet">
/// <item>带 <c>[QuestRef(...)]</c> 的 long / List&lt;long&gt; → ID 选择器 <see cref="QuestRefField"/>（候选少走下拉，多走搜索）</item>
/// <item><see cref="LocKeyRef"/>（表 + Key，多语言字段统一用它）→ Key 选择器 <see cref="QuestLocField"/>（按记录类型限定 Key 前缀、分页）</item>
/// <item><see cref="AssetReferenceSprite"/> → Sprite 拖拽框</item>
/// <item>抽象类 / 接口字段与它们的 List → 类型下拉 ＋ 展开子字段（多态目标、触发、奖励走这条）</item>
/// </list>
/// </summary>
internal static class QuestEditorFields
{
    /// <summary>多语言 Key 前缀按记录类型走，和导入时补的前缀保持一致。</summary>
    static readonly Dictionary<Type, string> LocPrefixes = new()
    {
        { typeof(QuestData), QuestLocKey.Prefix.Quest },
        { typeof(QuestCategory), QuestLocKey.Prefix.Category },
        { typeof(QuestRewardPresentation), QuestLocKey.Prefix.RewardName },
    };

    /// <summary>嵌在别的字段里画：选择器一律收起，省得一屏塞进好几个候选列表。</summary>
    public static VisualElement Build(object target, Action changed)
    {
        VisualElement root = new();
        root.AddToClassList("quest-fields");
        foreach (FieldInfo field in Fields(target.GetType()))
            root.Add(BuildField(target, field, changed, false));
        return root;
    }

    #region 单个字段

    /// <summary>只画一个字段，表格的弹层编辑用；这时弹层就是为它开的，选择器直接摊开。</summary>
    public static VisualElement BuildOne(object target, FieldInfo field, Action changed)
        => BuildField(target, field, changed, true);

    static VisualElement BuildField(object target, FieldInfo field, Action changed, bool expand)
    {
        string label = Label(field);
        Type type = field.FieldType;
        object value = field.GetValue(target);

        void Apply(object newValue)
        {
            field.SetValue(target, newValue);
            changed();
        }

        if (type == typeof(long)) return LongControl(label, field, (long)value, Apply);
        if (type == typeof(int)) return IntControl(label, field, (int)value, Apply);
        if (type == typeof(float)) return Bind(new FloatField(label) { value = (float)value }, Apply);
        if (type == typeof(bool)) return Bind(new Toggle(label) { value = (bool)value }, Apply);
        if (type == typeof(string)) return Bind(new TextField(label) { value = (string)value ?? string.Empty }, Apply);
        if (type.IsEnum) return Bind(new EnumField(label, (Enum)value), Apply);

        if (type == typeof(LocKeyRef))
            return new QuestLocField(label, (LocKeyRef)value, LocPrefix(target), Apply, expand);

        if (type == typeof(AssetReferenceSprite)) return IconControl(label, (AssetReferenceSprite)value, Apply);

        if (IsList(type, out Type item)) return new QuestListField(label, target, field, item, changed);

        if (IsPolymorphic(type)) return new QuestPolymorphicField(label, type, value, Apply, changed);

        // 具体类（QuestItemRequirement 这种）：直接把它的字段接着画进来
        if (!type.IsPrimitive)
        {
            Foldout group = new() { text = label, value = true };
            group.AddToClassList("quest-group");
            if (value == null) Apply(value = Activator.CreateInstance(type));
            group.Add(Build(value, changed));
            return group;
        }

        return new Label($"{label}：暂不支持的类型 {type.Name}");
    }

    static VisualElement LongControl(string label, FieldInfo field, long value, Action<object> apply)
    {
        QuestRefKind? kind = QuestRefField.KindOf(field);
        if (kind != null) return new QuestRefField(label, kind.Value, value, id => apply(id));

        return Bind(new LongField(label) { value = value }, apply);
    }

    static VisualElement IntControl(string label, FieldInfo field, int value, Action<object> apply)
    {
        int min = MinValue(field);
        IntegerField control = new(label) { value = value };
        control.RegisterValueChangedCallback(evt => apply(Mathf.Max(min, evt.newValue)));
        return control;
    }

    static VisualElement IconControl(string label, AssetReferenceSprite value, Action<object> apply)
    {
        ObjectField control = new(label)
        {
            objectType = typeof(Sprite),
            allowSceneObjects = false,
            value = QuestIconEditorUtility.GetSprite(value),
        };
        control.RegisterValueChangedCallback(evt => apply(QuestIconEditorUtility.Create(evt.newValue as Sprite)));
        return control;
    }

    static VisualElement Bind<TValue>(BaseField<TValue> control, Action<object> apply)
    {
        control.RegisterValueChangedCallback(evt => apply(evt.newValue));
        return control;
    }

    #endregion

    #region 反射

    /// <summary>Unity 序列化口径：public 或带 [SerializeField]，基类字段排在前面。</summary>
    public static IEnumerable<FieldInfo> Fields(Type type)
    {
        if (type == null || type == typeof(object)) return Array.Empty<FieldInfo>();

        List<FieldInfo> result = new(Fields(type.BaseType));
        foreach (FieldInfo field in type.GetFields(
                     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (field.IsInitOnly || field.IsNotSerialized) continue;
            if (field.IsDefined(typeof(NonSerializedAttribute))) continue;
            if (!field.IsPublic && !field.IsDefined(typeof(SerializeField))) continue;
            if (field.IsDefined(typeof(QuestHiddenAttribute))) continue;

            result.Add(field);
        }
        return result;
    }

    public static string Label(FieldInfo field)
        => field.GetCustomAttribute<QuestLabelAttribute>()?.Text ?? field.Name;

    /// <summary>类型下拉和表格摘要统一用「枚举名 - 中文说明」，两处对得上。</summary>
    public static string TypeLabel(Type type)
    {
        string info = type.GetCustomAttribute<QuestTypeInfoAttribute>()?.Text;
        return string.IsNullOrEmpty(info) ? EnumName(type) : $"{EnumName(type)} - {info}";
    }

    static readonly Dictionary<Type, string> EnumNames = new();

    /// <summary>
    /// 这种目标/触发/奖励对应的枚举名。触发和奖励自己带 <c>Type</c> 枚举属性，直接读；
    /// 目标没有（<see cref="QuestObjData"/> 只按族分类），就用类名去掉后缀 —— 结果和 QuestObjType 的名字一致。
    /// </summary>
    public static string EnumName(Type type)
    {
        if (EnumNames.TryGetValue(type, out string cached)) return cached;

        string name = FromTypeProperty(type) ?? TrimSuffix(type.Name);
        EnumNames[type] = name;
        return name;
    }

    static string FromTypeProperty(Type type)
    {
        PropertyInfo property = type.GetProperty("Type", BindingFlags.Instance | BindingFlags.Public);
        if (property == null || !property.PropertyType.IsEnum) return null;
        if (type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null) return null;

        // Type 都是写死的常量属性，造个临时实例读一次就行（结果已缓存）
        try { return property.GetValue(Activator.CreateInstance(type))?.ToString(); }
        catch (Exception) { return null; }
    }

    static readonly string[] Suffixes = { "ObjData", "QuestTrigger", "QuestReward", "Data" };

    static string TrimSuffix(string name)
    {
        foreach (string suffix in Suffixes)
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length)
                return name[..^suffix.Length];
        }
        return name;
    }

    public static IReadOnlyList<Type> Implementations(Type baseType)
        => TypeCache.GetTypesDerivedFrom(baseType)
            .Where(type => !type.IsAbstract && !type.IsInterface && type.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(EnumName, StringComparer.Ordinal)
            .ToArray();

    public static bool IsPolymorphic(Type type) => type.IsAbstract || type.IsInterface;

    public static bool IsList(Type type, out Type item)
    {
        item = null;
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(List<>)) return false;

        item = type.GetGenericArguments()[0];
        return true;
    }

    static int MinValue(FieldInfo field)
    {
        QuestMinAttribute min = field.GetCustomAttribute<QuestMinAttribute>();
        return min?.Value ?? int.MinValue;
    }

    static string LocPrefix(object target)
        => LocPrefixes.TryGetValue(target.GetType(), out string prefix) ? prefix : QuestLocKey.Prefix.Objective;

    /// <summary>
    /// 表格单元格里的摘要文字。列表把每一项的名字都列出来（多到放不下才省略），
    /// 不用「N 项」那种看不出内容的写法。
    /// </summary>
    public static string Summary(object target, FieldInfo field)
    {
        object value = field.GetValue(target);
        Type type = field.FieldType;

        if (IsList(type, out Type itemType))
        {
            if (value is not IList list || list.Count == 0) return "未配置";

            List<string> parts = new();
            for (int i = 0; i < list.Count && i < 4; i++) parts.Add(ItemLabel(list[i], itemType, field));
            string text = string.Join("、", parts);
            return list.Count > 4 ? $"{text} …（共 {list.Count}）" : text;
        }

        return ItemLabel(value, type, field);
    }

    /// <summary>一个值显示成什么：ID 给「ID | 备注」、多语言给中文原文、图标给资源名、多态给「枚举名 - 中文名」。</summary>
    static string ItemLabel(object value, Type type, FieldInfo field)
    {
        if (type == typeof(LocKeyRef))
        {
            LocKeyRef loc = (LocKeyRef)value;
            string preview = QuestLocalizationEditorUtility.GetPreview(loc);
            return string.IsNullOrEmpty(preview) ? QuestLocalizationEditorUtility.GetDisplayPath(loc) : preview;
        }

        if (type == typeof(AssetReferenceSprite))
        {
            Sprite sprite = QuestIconEditorUtility.GetSprite((AssetReferenceSprite)value);
            return sprite == null ? "未配置" : sprite.name;
        }

        if (type == typeof(long))
        {
            QuestRefKind? kind = QuestRefField.KindOf(field);
            return kind == null ? value.ToString() : QuestRefField.Text(kind.Value, (long)value);
        }

        if (value == null) return "未配置";
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum) return value.ToString();

        // 多态项和具体类都按「枚举名 - 中文说明」显示，和点开后的类型下拉一致
        return TypeLabel(value.GetType());
    }

    #endregion
}

/// <summary>
/// ID 选择器：显示「ID | 备注」，写回的仍然是 long。选项来自 <see cref="QuestRefCatalog"/>。
///
/// 候选不多（任务、目标、条件、NPC、场景）就是一个下拉框，一行搞定；
/// 道具这种成百上千的才换成搜索框，候选列表平时收着、搜的时候才铺开。
/// </summary>
internal sealed class QuestRefField : VisualElement
{
    /// <summary>候选超过这么多就别塞下拉菜单了，改用搜索。</summary>
    const int DropdownLimit = 200;

    /// <summary>这个字段是不是 ID 引用。</summary>
    public static QuestRefKind? KindOf(FieldInfo field) => field.GetCustomAttribute<QuestRefAttribute>()?.Kind;

    /// <summary>某个 ID 显示成什么，表格摘要用。</summary>
    public static string Text(QuestRefKind kind, long value)
        => Text(QuestRefCatalog.Options(kind), value);

    static string Text(IReadOnlyList<QuestRefOption> options, long value)
    {
        foreach (QuestRefOption option in options)
        {
            if (option.Id == value) return option.Label;
        }
        return $"{value} | 未找到";
    }

    /// <summary>候选够少，值可以在表格单元格里直接改（接受条件这一列就靠它）。</summary>
    public static bool FitsInCell(QuestRefKind kind) => QuestRefCatalog.Options(kind).Count <= DropdownLimit;

    readonly IReadOnlyList<QuestRefOption> options;
    readonly Action<long> changed;

    public QuestRefField(string label, QuestRefKind kind, long value, Action<long> changed)
    {
        this.changed = changed;
        options = QuestRefCatalog.Options(kind);

        AddToClassList("quest-ref");
        if (options.Count <= DropdownLimit) BuildDropdown(label, value);
        else BuildSearch(label, value);
    }

    void BuildDropdown(string label, long value)
    {
        List<string> choices = options.Select(option => option.Label).ToList();
        string current = Text(options, value);
        if (!choices.Contains(current)) choices.Insert(0, current);

        DropdownField dropdown = new(label) { choices = choices, value = current };
        dropdown.AddToClassList("quest-ref-dropdown");
        dropdown.RegisterValueChangedCallback(evt =>
        {
            foreach (QuestRefOption option in options)
            {
                if (option.Label != evt.newValue) continue;

                changed(option.Id);
                return;
            }
        });
        Add(dropdown);
    }

    void BuildSearch(string label, long value)
    {
        AddToClassList("quest-reference-search");

        Label current = new(Text(options, value));
        VisualElement header = new();
        header.AddToClassList("quest-reference-search-header");
        Label title = new(label);
        title.AddToClassList("quest-reference-search-label");
        current.AddToClassList("quest-reference-search-current");
        header.Add(title);
        header.Add(current);
        Add(header);

        ToolbarSearchField search = new();
        search.AddToClassList("quest-reference-search-input");
        search.tooltip = "输入 ID 或名称";
        VisualElement searchRow = new();
        searchRow.AddToClassList("quest-reference-search-toolbar");
        Label hint = new("搜索 ID / 名称");
        hint.AddToClassList("quest-reference-search-hint");
        searchRow.Add(hint);
        searchRow.Add(search);
        Add(searchRow);

        List<QuestRefOption> filtered = new();
        ListView results = new()
        {
            fixedItemHeight = 22,
            selectionType = SelectionType.Single,
            itemsSource = filtered,
            makeItem = () => new Label(),
        };
        results.bindItem = (element, index) => ((Label)element).text = filtered[index].Label;
        results.AddToClassList("quest-reference-search-results");
        results.AddToClassList("is-hidden");
        Add(results);

        void Filter(string query)
        {
            filtered.Clear();
            IEnumerable<QuestRefOption> source = options;
            if (!string.IsNullOrWhiteSpace(query))
            {
                string text = query.Trim();
                source = source.Where(option => option.Label.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            filtered.AddRange(source.Take(200));
            results.ClearSelection();
            results.RefreshItems();
            results.EnableInClassList("is-hidden", filtered.Count == 0);
        }

        void Pick(QuestRefOption option)
        {
            current.text = option.Label;
            search.SetValueWithoutNotify(string.Empty);
            results.ClearSelection();
            results.AddToClassList("is-hidden");
            changed(option.Id);
        }

        results.selectionChanged += items =>
        {
            if (items.FirstOrDefault() is QuestRefOption option) Pick(option);
        };
        search.RegisterCallback<FocusInEvent>(_ => Filter(search.value));
        search.RegisterValueChangedCallback(evt => Filter(evt.newValue));
        search.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Escape) results.AddToClassList("is-hidden");
            else if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter && filtered.Count > 0) Pick(filtered[0]);
            else return;

            evt.StopPropagation();
        });
    }
}

/// <summary>
/// 多语言 Key 选择器：上面是当前 Key ＋ 中文预览，下面是本前缀下的候选（搜索 ＋ 翻页）。
/// 候选平时收着，点「选择…」才展开；弹层里只有这一个字段时直接展开。
/// </summary>
internal sealed class QuestLocField : VisualElement
{
    const int PageSize = 8;

    readonly IReadOnlyList<QuestLocalizationOption> options;
    readonly Action<object> changed;

    readonly Label currentKey = new();
    readonly Label currentPreview = new();
    readonly VisualElement picker = new();
    readonly VisualElement rows = new();
    readonly ToolbarSearchField search = new();
    readonly Label pageLabel = new();
    readonly Button previous;
    readonly Button next;

    string selectedKey;
    int page;

    public QuestLocField(string label, LocKeyRef value, string prefix, Action<object> changed, bool expanded)
    {
        this.changed = changed;
        options = QuestLocalizationEditorUtility.GetOptions(prefix);

        AddToClassList("quest-loc");

        VisualElement header = new();
        header.AddToClassList("quest-loc-row");
        Label title = new(label);
        title.AddToClassList("quest-loc-label");
        Button toggle = new(TogglePicker) { text = "选择…" };
        toggle.AddToClassList("quest-btn");
        header.Add(title);
        header.Add(CurrentCard());
        header.Add(toggle);
        Add(header);

        Label scope = new($"可选范围：{LocTableSet.QuestSystem}/{prefix}*");
        scope.AddToClassList("quest-localization-scope");
        picker.Add(scope);

        search.AddToClassList("quest-localization-search");
        search.tooltip = "可按多语言 Key 或中文内容搜索";
        search.RegisterValueChangedCallback(_ =>
        {
            page = 0;
            RefreshRows();
        });
        picker.Add(search);

        rows.AddToClassList("quest-localization-list");
        picker.Add(rows);

        VisualElement pager = new();
        pager.AddToClassList("quest-localization-pager");
        previous = new Button(() => Turn(-1)) { text = "上一页" };
        next = new Button(() => Turn(1)) { text = "下一页" };
        previous.AddToClassList("quest-btn");
        next.AddToClassList("quest-btn");
        pageLabel.AddToClassList("quest-localization-page-label");
        pager.Add(previous);
        pager.Add(pageLabel);
        pager.Add(next);
        picker.Add(pager);

        picker.AddToClassList("quest-loc-picker");
        picker.EnableInClassList("is-hidden", !expanded);
        Add(picker);

        Show(value);
        RefreshRows();
    }

    VisualElement CurrentCard()
    {
        VisualElement card = new();
        card.AddToClassList("quest-localization-current");
        currentKey.AddToClassList("quest-localization-current-key");
        currentPreview.AddToClassList("quest-localization-current-preview");
        card.Add(currentKey);
        card.Add(currentPreview);
        return card;
    }

    void TogglePicker() => picker.ToggleInClassList("is-hidden");

    void Turn(int delta)
    {
        page += delta;
        RefreshRows();
    }

    void Show(LocKeyRef value)
    {
        selectedKey = QuestLocalizationEditorUtility.GetKey(value);
        currentKey.text = QuestLocalizationEditorUtility.GetDisplayPath(value);
        string preview = QuestLocalizationEditorUtility.GetPreview(value);
        currentPreview.text = string.IsNullOrEmpty(preview) ? "（无中文内容）" : preview;
    }

    void RefreshRows()
    {
        rows.Clear();

        string query = search.value?.Trim();
        List<QuestLocalizationOption> matched = options
            .Where(option => string.IsNullOrEmpty(query) ||
                             option.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        int pageCount = Mathf.Max(1, (matched.Count + PageSize - 1) / PageSize);
        page = Mathf.Clamp(page, 0, pageCount - 1);

        foreach (QuestLocalizationOption option in matched.Skip(page * PageSize).Take(PageSize))
            rows.Add(Row(option));

        if (matched.Count == 0)
        {
            Label empty = new("没有符合前缀与搜索条件的多语言 Key");
            empty.AddToClassList("quest-localization-empty");
            rows.Add(empty);
        }

        pageLabel.text = $"第 {page + 1} / {pageCount} 页 · 共 {matched.Count} 项";
        previous.SetEnabled(page > 0);
        next.SetEnabled(page + 1 < pageCount);
    }

    Button Row(QuestLocalizationOption option)
    {
        bool isCurrent = string.Equals(option.Key, selectedKey, StringComparison.Ordinal);
        Button row = new(() => Pick(option));
        row.AddToClassList("quest-localization-row");
        row.EnableInClassList("is-current", isCurrent);

        VisualElement text = new();
        text.AddToClassList("quest-localization-row-text");
        Label key = new(option.Key);
        key.AddToClassList("quest-localization-key");
        Label preview = new(string.IsNullOrEmpty(option.Preview) ? "（中文内容为空）" : option.Preview);
        preview.AddToClassList("quest-localization-preview");
        text.Add(key);
        text.Add(preview);
        row.Add(text);
        return row;
    }

    void Pick(QuestLocalizationOption option)
    {
        LocKeyRef value = QuestLocalizationEditorUtility.Create(option.Key);
        Show(value);
        changed(value);
        RefreshRows();
    }
}

/// <summary>抽象类/接口字段：先选实现类型（枚举名 - 中文名），再画它自己的字段。多态目标、触发、奖励都走这里。</summary>
internal sealed class QuestPolymorphicField : VisualElement
{
    readonly VisualElement body = new();
    readonly IReadOnlyList<Type> types;
    readonly Action<object> apply;
    readonly Action changed;

    object value;

    public QuestPolymorphicField(string label, Type baseType, object value, Action<object> apply, Action changed)
    {
        this.value = value;
        this.apply = apply;
        this.changed = changed;
        types = QuestEditorFields.Implementations(baseType);

        AddToClassList("quest-poly");

        List<string> choices = new() { "（不配）" };
        choices.AddRange(types.Select(QuestEditorFields.TypeLabel));

        DropdownField dropdown = new(label) { choices = choices, index = IndexOf(value) };
        dropdown.RegisterValueChangedCallback(evt => Switch(choices.IndexOf(evt.newValue)));
        Add(dropdown);

        body.AddToClassList("quest-poly-body");
        Add(body);
        Rebuild();
    }

    int IndexOf(object target) => target == null ? 0 : types.ToList().IndexOf(target.GetType()) + 1;

    void Switch(int index)
    {
        value = index <= 0 ? null : Activator.CreateInstance(types[index - 1]);
        apply(value);
        Rebuild();
    }

    void Rebuild()
    {
        body.Clear();
        if (value != null) body.Add(QuestEditorFields.Build(value, changed));
    }
}

/// <summary>
/// List 字段。ID / 数值这类一项占一行（配合上下移和删除），
/// 多态项和结构体项占一张卡，底下一个新增按钮。
/// </summary>
internal sealed class QuestListField : VisualElement
{
    readonly VisualElement items = new();
    readonly Type itemType;
    readonly FieldInfo field;
    readonly object owner;
    readonly Action changed;

    IList List => (IList)field.GetValue(owner);

    /// <summary>一行放得下的项类型，不用套卡片。</summary>
    bool Compact => itemType == typeof(long) || itemType == typeof(int)
        || itemType == typeof(string) || itemType.IsEnum;

    public QuestListField(string label, object owner, FieldInfo field, Type itemType, Action changed)
    {
        this.owner = owner;
        this.field = field;
        this.itemType = itemType;
        this.changed = changed;

        AddToClassList("quest-list-field");
        if (field.GetValue(owner) == null) field.SetValue(owner, Activator.CreateInstance(field.FieldType));

        VisualElement header = new();
        header.AddToClassList("quest-list-header");
        Label title = new(label);
        title.AddToClassList("quest-list-title");
        Button add = new(AddItem) { text = "＋ 添加一项" };
        add.AddToClassList("quest-btn");
        add.AddToClassList("quest-btn-primary");
        header.Add(title);
        header.Add(add);
        Add(header);

        items.AddToClassList("quest-list-items");
        Add(items);
        Rebuild();
    }

    void AddItem()
    {
        List.Add(QuestEditorFields.IsPolymorphic(itemType) ? null : Default());
        changed();
        Rebuild();
    }

    object Default()
        => itemType == typeof(string) ? string.Empty : Activator.CreateInstance(itemType);

    void Rebuild()
    {
        items.Clear();
        IList list = List;
        if (list.Count == 0)
        {
            Label empty = new("当前没有配置项");
            empty.AddToClassList("quest-empty-hint");
            items.Add(empty);
            return;
        }

        for (int i = 0; i < list.Count; i++) items.Add(Compact ? CompactRow(i) : Card(i));
    }

    /// <summary>一行：控件 ＋ 上移下移 ＋ 删除。</summary>
    VisualElement CompactRow(int index)
    {
        VisualElement row = new();
        row.AddToClassList("quest-list-row");
        row.Add(BuildItem(index, $"#{index + 1}"));
        AddOperations(row, index);
        return row;
    }

    VisualElement Card(int index)
    {
        VisualElement card = new();
        card.AddToClassList("quest-list-card");

        VisualElement header = new();
        header.AddToClassList("quest-list-card-header");
        Label title = new($"#{index + 1}");
        title.AddToClassList("quest-array-index");
        header.Add(title);
        AddOperations(header, index);
        card.Add(header);

        card.Add(BuildItem(index, "类型"));
        return card;
    }

    void AddOperations(VisualElement parent, int index)
    {
        Button up = new(() => Move(index, -1)) { text = "↑" };
        Button down = new(() => Move(index, 1)) { text = "↓" };
        up.AddToClassList("quest-icon-btn");
        down.AddToClassList("quest-icon-btn");
        up.SetEnabled(index > 0);
        down.SetEnabled(index < List.Count - 1);

        Button remove = new(() => Remove(index)) { text = "删除" };
        remove.AddToClassList("quest-btn");
        remove.AddToClassList("quest-btn-danger");
        remove.AddToClassList("quest-list-remove");

        parent.Add(up);
        parent.Add(down);
        parent.Add(remove);
    }

    VisualElement BuildItem(int index, string label)
    {
        void Apply(object value)
        {
            List[index] = value;
            changed();
        }

        object item = List[index];

        if (QuestEditorFields.IsPolymorphic(itemType))
            return new QuestPolymorphicField(label, itemType, item, Apply, changed);

        if (itemType == typeof(long))
        {
            QuestRefKind? kind = QuestRefField.KindOf(field);
            return kind != null
                ? new QuestRefField(label, kind.Value, (long)item, id => Apply(id))
                : Bind(new LongField(label) { value = (long)item }, Apply);
        }
        if (itemType == typeof(int)) return Bind(new IntegerField(label) { value = (int)item }, Apply);
        if (itemType == typeof(string))
            return Bind(new TextField(label) { value = (string)item ?? string.Empty }, Apply);
        if (itemType.IsEnum) return Bind(new EnumField(label, (Enum)item), Apply);

        if (item == null) Apply(item = Activator.CreateInstance(itemType));
        return QuestEditorFields.Build(item, changed);
    }

    static VisualElement Bind<TValue>(BaseField<TValue> control, Action<object> apply)
    {
        control.AddToClassList("quest-list-field-control");
        control.RegisterValueChangedCallback(evt => apply(evt.newValue));
        return control;
    }

    void Move(int index, int delta)
    {
        int target = index + delta;
        IList list = List;
        if (target < 0 || target >= list.Count) return;

        (list[index], list[target]) = (list[target], list[index]);
        changed();
        Rebuild();
    }

    void Remove(int index)
    {
        List.RemoveAt(index);
        changed();
        Rebuild();
    }
}
