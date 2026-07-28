using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMcp
{
    /// <summary>
    /// edit_prefab：单次请求内按顺序执行一批结构化编辑（重命名、增删移动节点、增删组件、建 UI、改序列化值）。
    /// 任一步失败即中止且不保存；apply=false 时全部在内存中预演后丢弃，是真正的 dry-run。
    /// 目标是让 AI 用最少的往返和 token 完成一组改动：一次调用、一次备份、一次保存。
    ///
    /// prefabStage/openScene 这类实时目标没有离屏副本，改动直接落在编辑器对象上，
    /// 因此所有写操作都走 Undo API 注册：批次失败时 <see cref="Undo.RevertAllDownToGroup"/> 回滚，
    /// 成功时合并为一个 Undo 组，用户可以 Ctrl+Z 整批撤销。
    /// </summary>
    static class EditTools
    {
        const string UndoGroupName = "Prefab MCP 批量编辑";

        /// <summary>op 词表的唯一来源，工具表的 enum 直接引用；未登记的 op 在下面统一挡掉。</summary>
        public static readonly string[] SupportedOps =
        {
            "rename", "setActive", "reparent", "setSiblingIndex", "delete", "duplicate",
            "createObject", "createUi", "instantiatePrefab", "addComponent",
            "removeComponent", "removeMissingScripts", "setValue", "setValues",
        };

        /// <summary>anchor 预设词表的唯一来源；pivot 取 anchorMin/Max 的中点。</summary>
        public static readonly string[] SupportedAnchors =
        {
            "center", "stretch", "top", "bottom", "left", "right",
            "topLeft", "topRight", "bottomLeft", "bottomRight",
            "stretchTop", "stretchBottom", "stretchLeft", "stretchRight",
        };

        /// <summary>
        /// 一条操作的执行记录：结果 DTO + 它实际作用的节点。
        /// $n 直接绑这个 Transform 实例，而不是回头再解析一次 objectId ——
        /// 批内的 setSiblingIndex/reparent/delete 会让先前记下的 sibling 路径指到别的节点上。
        /// </summary>
        sealed class OpRecord
        {
            public EditOpResult Result;
            public Transform TargetTf;
        }

        public static string Edit(EditRequest command)
        {
            if (command.operations == null || command.operations.Length == 0)
                return BridgeJson.Fail("operations 不能为空");
            if (!EditTarget.TryResolve(command, out EditTarget target, out string error))
                return BridgeJson.Fail(error);

            // prefabAsset 走离屏副本，apply=false 是真正的内存预演；
            // prefabStage/openScene 直接改实时对象，无法丢弃预演结果，故要求 apply=true，靠 Undo 组保证可回滚。
            bool live = !target.IsAsset;
            int undoGroup = -1;
            using (target)
            {
                if (live)
                {
                    if (!command.apply)
                        return BridgeJson.Fail("prefabStage/openScene 是实时对象，不支持 apply=false 内存预演；" +
                            "确认无误后直接用 apply=true（批次失败会自动 Undo 回滚）");
                    error = target.ValidateWriteAllowed();
                    if (error != null)
                        return BridgeJson.Fail(error);
                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName(UndoGroupName);
                    undoGroup = Undo.GetCurrentGroup();
                }

                var records = new List<OpRecord>();
                foreach (EditOp op in command.operations)
                {
                    OpRecord record = Execute(target, op, live, records);
                    records.Add(record);
                    if (record.Result.error != null)
                        break;
                }

                // objectId 按最终层级重算：批内后续的重排/移动会让操作当时记下的 sibling 路径过期，
                // 回一个「当时对、现在错」的 id 比不回更糟——AI 会拿它接着用。
                foreach (OpRecord record in records)
                {
                    if (record.TargetTf == null)
                        continue;
                    record.Result.objectId = PrefabAddress.GetObjectId(record.TargetTf, target.RootTf);
                    record.Result.hierarchyPath = PrefabAddress.GetHierarchyPath(record.TargetTf, target.RootTf);
                }

                List<EditOpResult> results = records.Select(record => record.Result).ToList();
                var edit = new EditInfo
                {
                    operationCount = command.operations.Length,
                    completed = results.Count(item => item.error == null),
                    results = results.ToArray(),
                };

                EditOpResult last = results[results.Count - 1];
                if (last.error != null)
                {
                    if (live)
                        Undo.RevertAllDownToGroup(undoGroup);
                    return BridgeJson.Serialize(new EditResponse
                    {
                        error = $"操作 {results.Count}/{command.operations.Length}（{last.op}）失败: {last.error}；批次已中止" +
                            (live ? "，已通过 Undo 组回滚，未标脏保存" : "，Prefab 未保存"),
                        edit = edit,
                    });
                }

                if (command.apply)
                {
                    if (!live)
                    {
                        error = target.ValidateWriteAllowed();
                        if (error != null)
                            return BridgeJson.Fail(error);
                    }
                    error = target.Commit(out string backupPath);
                    if (error != null)
                        return BridgeJson.Fail(error);
                    edit.backupPath = backupPath;
                    edit.applied = true;
                }
                if (live)
                    Undo.CollapseUndoOperations(undoGroup);

                return BridgeJson.Serialize(new EditResponse
                {
                    message = command.apply
                        ? $"已执行 {edit.completed} 个操作并保存" +
                          (live ? "（已标脏，Ctrl+S 保存；Ctrl+Z 可整批撤销）" : string.Empty)
                        : $"{edit.completed} 个操作预演成功；apply=false，未保存",
                    edit = edit,
                });
            }
        }

        static OpRecord Execute(EditTarget target, EditOp op, bool live, List<OpRecord> priorRecords)
        {
            var record = new OpRecord { Result = new EditOpResult { op = op.op } };
            try
            {
                return ExecuteCore(target, op, live, priorRecords, record);
            }
            catch (Exception e)
            {
                return Failed(record, e.Message);
            }
        }

        static OpRecord ExecuteCore(EditTarget target, EditOp op, bool live,
            List<OpRecord> priorRecords, OpRecord record)
        {
            Transform rootTf = target.RootTf;
            EditOpResult result = record.Result;
            string error;
            string opName = (op.op ?? string.Empty).Trim();
            // 先按词表挡一道：新增分支忘了登记会在这里报错，而不是悄悄多出一个 AI 看不见的 op。
            if (System.Array.IndexOf(SupportedOps, opName) < 0)
                return Failed(record, $"未知 op: {op.op}。支持: {string.Join(", ", SupportedOps)}");
            switch (opName)
            {
                case "rename":
                {
                    if (!TryResolve(rootTf, op.objectId, priorRecords, out Transform targetTf, out error))
                        return Failed(record, error);
                    if (string.IsNullOrWhiteSpace(op.newName))
                        return Failed(record, "rename 需要 newName");
                    RecordUndo(targetTf.gameObject, live);
                    result.detail = targetTf.name + " -> " + op.newName.Trim();
                    targetTf.name = op.newName.Trim();
                    return Ok(record, targetTf, rootTf);
                }
                case "setActive":
                {
                    if (!TryResolve(rootTf, op.objectId, priorRecords, out Transform targetTf, out error))
                        return Failed(record, error);
                    if (!PrefabAddress.TryParseBool(op.active, out bool active))
                        return Failed(record, "setActive 需要 active=\"true\"|\"false\"");
                    RecordUndo(targetTf.gameObject, live);
                    targetTf.gameObject.SetActive(active);
                    result.detail = "activeSelf=" + (active ? "true" : "false");
                    return Ok(record, targetTf, rootTf);
                }
                case "reparent":
                {
                    if (!TryResolve(rootTf, op.objectId, priorRecords, out Transform targetTf, out error))
                        return Failed(record, error);
                    if (targetTf == rootTf)
                        return Failed(record, "不能移动根节点");
                    if (string.IsNullOrEmpty(op.parentObjectId))
                        return Failed(record, "reparent 需要 parentObjectId（根节点为 0）");
                    if (!TryResolve(rootTf, op.parentObjectId, priorRecords, out Transform parentTf, out error))
                        return Failed(record, error);
                    if (parentTf == targetTf || parentTf.IsChildOf(targetTf))
                        return Failed(record, "不能把节点移动到它自己或它的子级下");
                    string previous = PrefabAddress.GetHierarchyPath(targetTf, rootTf);
                    if (live)
                        Undo.SetTransformParent(targetTf, parentTf, false, UndoGroupName);
                    else
                        targetTf.SetParent(parentTf, false);
                    error = SetSiblingIndex(targetTf, op.siblingIndex, live)
                        ?? ApplyRect(targetTf, op, live, true);
                    if (error != null)
                        return Failed(record, error);
                    result.detail = previous + " -> " + PrefabAddress.GetHierarchyPath(targetTf, rootTf);
                    return Ok(record, targetTf, rootTf);
                }
                case "setSiblingIndex":
                {
                    if (!TryResolve(rootTf, op.objectId, priorRecords, out Transform targetTf, out error))
                        return Failed(record, error);
                    if (targetTf == rootTf)
                        return Failed(record, "不能调整根节点");
                    if (string.IsNullOrWhiteSpace(op.siblingIndex))
                        return Failed(record, "setSiblingIndex 需要 siblingIndex");
                    error = SetSiblingIndex(targetTf, op.siblingIndex, live);
                    if (error != null)
                        return Failed(record, error);
                    result.detail = "siblingIndex=" + targetTf.GetSiblingIndex();
                    return Ok(record, targetTf, rootTf);
                }
                case "delete":
                {
                    if (!TryResolve(rootTf, op.objectId, priorRecords, out Transform targetTf, out error))
                        return Failed(record, error);
                    if (targetTf == rootTf)
                        return Failed(record, "不能删除根节点");
                    result.hierarchyPath = PrefabAddress.GetHierarchyPath(targetTf, rootTf);
                    result.detail = "已删除";
                    Remove(targetTf.gameObject, live);
                    return record;
                }
                case "duplicate":
                {
                    if (!TryResolve(rootTf, op.objectId, priorRecords, out Transform targetTf, out error))
                        return Failed(record, error);
                    if (targetTf == rootTf)
                        return Failed(record, "不能复制根节点");
                    GameObject copyGo = UnityEngine.Object.Instantiate(targetTf.gameObject, targetTf.parent);
                    copyGo.name = string.IsNullOrWhiteSpace(op.newName) ? targetTf.name : op.newName.Trim();
                    RegisterCreated(copyGo, live);
                    copyGo.transform.SetSiblingIndex(targetTf.GetSiblingIndex() + 1);
                    error = SetSiblingIndex(copyGo.transform, op.siblingIndex, live)
                        ?? ApplyRect(copyGo.transform, op, live, true);
                    if (error != null)
                    {
                        Remove(copyGo, live);
                        return Failed(record, error);
                    }
                    result.detail = "复制自 " + PrefabAddress.GetHierarchyPath(targetTf, rootTf);
                    return Ok(record, copyGo.transform, rootTf);
                }
                case "createObject":
                {
                    if (!TryResolve(rootTf, op.parentObjectId, priorRecords, out Transform parentTf, out error))
                        return Failed(record, error);
                    string name = string.IsNullOrWhiteSpace(op.newName) ? "GameObject" : op.newName.Trim();
                    GameObject createdGo;
                    if (parentTf is RectTransform)
                    {
                        PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
                        createdGo = UiElementFactory.CreateUiObject(name, parentTf,
                            settings.DefaultUiSize.x, settings.DefaultUiSize.y);
                    }
                    else
                    {
                        createdGo = new GameObject(name);
                        createdGo.transform.SetParent(parentTf, false);
                    }
                    RegisterCreated(createdGo, live);
                    error = SetSiblingIndex(createdGo.transform, op.siblingIndex, live)
                        ?? ApplyRect(createdGo.transform, op, live, true);
                    if (error != null)
                    {
                        Remove(createdGo, live);
                        return Failed(record, error);
                    }
                    result.detail = parentTf is RectTransform ? "已创建（含 RectTransform）" : "已创建";
                    return Ok(record, createdGo.transform, rootTf);
                }
                case "createUi":
                {
                    if (!TryResolve(rootTf, op.parentObjectId, priorRecords, out Transform parentTf, out error))
                        return Failed(record, error);
                    if (!(parentTf is RectTransform))
                        return Failed(record, $"父节点 {PrefabAddress.GetHierarchyPath(parentTf, rootTf)} " +
                            "没有 RectTransform，不适合作为 UI 父节点");
                    if (!UiElementFactory.IsSupported(op.elementType))
                        return Failed(record, "不支持的 elementType。可选: " +
                            string.Join(", ", UiElementFactory.SupportedTypes));

                    PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
                    if (!TryParseSize(op.width, settings.DefaultUiSize.x, out float width, out error) ||
                        !TryParseSize(op.height, settings.DefaultUiSize.y, out float height, out error))
                        return Failed(record, error);
                    string name = string.IsNullOrWhiteSpace(op.newName)
                        ? UiElementFactory.DefaultName(op.elementType)
                        : op.newName.Trim();

                    GameObject createdGo = UiElementFactory.Build(op.elementType, name, op.label,
                        parentTf, width, height, settings);
                    RegisterCreated(createdGo, live);
                    // width/height 已经在 Build 里用掉了，这里只补 anchor/anchoredPosition。
                    error = SetSiblingIndex(createdGo.transform, op.siblingIndex, live)
                        ?? ApplyRect(createdGo.transform, op, live, false);
                    if (error != null)
                    {
                        Remove(createdGo, live);
                        return Failed(record, error);
                    }
                    result.detail = $"已创建 {op.elementType}（{width}x{height}）";
                    return Ok(record, createdGo.transform, rootTf);
                }
                case "instantiatePrefab":
                {
                    if (!TryResolve(rootTf, op.parentObjectId, priorRecords, out Transform parentTf, out error))
                        return Failed(record, error);
                    string sourceError = PrefabAddress.ValidatePrefabPath(op.sourcePrefabPath);
                    if (sourceError != null)
                        return Failed(record, sourceError);
                    // 用 PolicyPath 而不是请求里的 prefabPath：prefabStage 模式下 prefabPath 为空，
                    // 拿它比对会让「把当前 Prefab 嵌套进它自己」漏过检查。
                    if (string.Equals(PrefabAddress.NormalizeSlashes(op.sourcePrefabPath),
                            PrefabAddress.NormalizeSlashes(target.PolicyPath), StringComparison.OrdinalIgnoreCase))
                        return Failed(record, "不能把 Prefab 嵌套进它自己");
                    GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(op.sourcePrefabPath);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab, parentTf);
                    if (instance == null)
                        return Failed(record, "实例化失败: " + op.sourcePrefabPath);
                    if (!string.IsNullOrWhiteSpace(op.newName))
                        instance.name = op.newName.Trim();
                    RegisterCreated(instance, live);
                    error = SetSiblingIndex(instance.transform, op.siblingIndex, live)
                        ?? ApplyRect(instance.transform, op, live, true);
                    if (error != null)
                    {
                        Remove(instance, live);
                        return Failed(record, error);
                    }
                    result.detail = "来自 " + op.sourcePrefabPath;
                    return Ok(record, instance.transform, rootTf);
                }
                case "addComponent":
                {
                    if (!TryResolve(rootTf, op.objectId, priorRecords, out Transform targetTf, out error))
                        return Failed(record, error);
                    if (!TryGetComponentType(op.componentType, out Type type, out error))
                        return Failed(record, error);
                    Component added = live
                        ? Undo.AddComponent(targetTf.gameObject, type)
                        : targetTf.gameObject.AddComponent(type);
                    if (added == null)
                        return Failed(record, $"AddComponent {type.FullName} 失败" +
                            "（可能与现有组件冲突，如 DisallowMultipleComponent）");
                    result.componentIndex = PrefabAddress.GetComponentIndex(added);
                    result.detail = "已添加 " + type.FullName;
                    return Ok(record, targetTf, rootTf);
                }
                case "removeComponent":
                {
                    if (!TryResolve(rootTf, op.objectId, priorRecords, out Transform targetTf, out error))
                        return Failed(record, error);
                    if (!PrefabAddress.TryGetComponentAt(targetTf, op.componentIndex, out Component component, out error))
                        return Failed(record, error + "（丢失脚本请用 removeMissingScripts）");
                    if (component is Transform)
                        return Failed(record, "不能移除 Transform/RectTransform");
                    string typeName = component.GetType().FullName;
                    Remove(component, live);
                    if (component != null)
                        return Failed(record, $"移除 {typeName} 失败（可能被其他组件 RequireComponent 依赖）");
                    result.detail = "已移除 " + typeName;
                    return Ok(record, targetTf, rootTf);
                }
                case "removeMissingScripts":
                {
                    if (!TryResolve(rootTf, op.objectId, priorRecords, out Transform targetTf, out error))
                        return Failed(record, error);
                    RecordUndo(targetTf.gameObject, live);
                    int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(targetTf.gameObject);
                    result.detail = $"移除了 {removed} 个丢失脚本";
                    return Ok(record, targetTf, rootTf);
                }
                case "setValue":
                {
                    if (!TryResolve(rootTf, op.objectId, priorRecords, out Transform targetTf, out error))
                        return Failed(record, error);
                    if (!PrefabAddress.TryGetComponentAt(targetTf, op.componentIndex, out Component component, out error))
                        return Failed(record, error);
                    var serializedObject = new SerializedObject(component);
                    error = WriteProperty(serializedObject, component, rootTf, op.propertyPath, op.value, out string detail);
                    if (error != null)
                        return Failed(record, error);
                    ApplySerialized(serializedObject, live);
                    result.componentIndex = op.componentIndex;
                    result.detail = detail;
                    return Ok(record, targetTf, rootTf);
                }
                case "setValues":
                {
                    if (!TryResolve(rootTf, op.objectId, priorRecords, out Transform targetTf, out error))
                        return Failed(record, error);
                    if (!PrefabAddress.TryGetComponentAt(targetTf, op.componentIndex, out Component component, out error))
                        return Failed(record, error);
                    if (op.values == null || op.values.Count == 0)
                        return Failed(record, "setValues 需要 values（{\"propertyPath\": 值} 对象）");

                    var serializedObject = new SerializedObject(component);
                    var details = new List<string>();
                    foreach (KeyValuePair<string, JToken> pair in op.values)
                    {
                        error = WriteProperty(serializedObject, component, rootTf, pair.Key,
                            TokenToValueText(pair.Value), out string detail);
                        if (error != null)
                            return Failed(record, $"{pair.Key}: {error}（本批次不会保存）");
                        details.Add(detail);
                    }
                    ApplySerialized(serializedObject, live);
                    result.componentIndex = op.componentIndex;
                    result.detail = string.Join("; ", details);
                    return Ok(record, targetTf, rootTf);
                }
                default:
                    return Failed(record, $"op {opName} 已登记在 SupportedOps 但没有实现分支");
            }
        }

        // ---- setValue / setValues 的写入 ----

        /// <summary>写一个字段并生成 before -> after 说明。value 以 <c>[</c> 开头且字段是数组时整体覆盖数组。</summary>
        static string WriteProperty(SerializedObject serializedObject, Component component, Transform rootTf,
            string propertyPath, string value, out string detail)
        {
            detail = null;
            SerializedProperty property = string.IsNullOrEmpty(propertyPath)
                ? null
                : serializedObject.FindProperty(propertyPath);
            if (property == null)
                return $"组件 {component.GetType().Name} 上找不到序列化字段 {propertyPath}";

            string previous = PrefabAddress.GetPropertyValueText(property, rootTf);
            bool wholeArray = property.isArray &&
                property.propertyType != SerializedPropertyType.String &&
                (value ?? string.Empty).TrimStart().StartsWith("[", StringComparison.Ordinal);
            string error = wholeArray
                ? WriteWholeArray(property, component, rootTf, value)
                : SerializedValueWriter.Write(property, component, rootTf, value);
            if (error != null)
                return error;

            detail = $"{component.GetType().Name}.{propertyPath}: {previous} -> " +
                PrefabAddress.GetPropertyValueText(property, rootTf);
            return null;
        }

        /// <summary>JSON 数组整体写入：先定 size 再逐个写元素，省掉 Array.size + 每元素一条 setValue 的啰嗦写法。</summary>
        static string WriteWholeArray(SerializedProperty property, Component component, Transform rootTf, string value)
        {
            JArray items;
            try
            {
                items = JArray.Parse(value);
            }
            catch (Exception e)
            {
                return "数组值必须是合法的 JSON 数组: " + e.Message;
            }

            property.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                string error = SerializedValueWriter.Write(element, component, rootTf, TokenToValueText(items[i]));
                if (error != null)
                    return $"第 {i} 个元素写入失败: {error}";
            }
            return null;
        }

        /// <summary>JSON 值 → 协议里的字符串标量；数组/对象保持紧凑 JSON 文本，交给数组分支再解析。</summary>
        static string TokenToValueText(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return "null";
            if (token.Type == JTokenType.String)
                return (string)token;
            return token.ToString(Formatting.None);
        }

        static void ApplySerialized(SerializedObject serializedObject, bool live)
        {
            if (live)
                serializedObject.ApplyModifiedProperties();
            else
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- 寻址与 RectTransform ----

        /// <summary>
        /// 解析 objectId，额外支持 <c>$n</c>：批内第 n 条操作（1 起）作用的节点。
        /// $n 绑定的是 Transform 实例而不是当时的 objectId —— 后续的 setSiblingIndex/reparent
        /// 会让旧路径指向另一个节点，那正是这套协议最容易出错、且出错时最难察觉的地方。
        /// </summary>
        static bool TryResolve(Transform rootTf, string objectId, List<OpRecord> priorRecords,
            out Transform result, out string error)
        {
            if (!string.IsNullOrEmpty(objectId) && objectId[0] == '$')
            {
                result = null;
                if (!int.TryParse(objectId.Substring(1).Trim(), out int opNumber) ||
                    opNumber < 1 || opNumber > priorRecords.Count)
                {
                    error = $"$n 引用无效: {objectId}（n 是本批次中已执行操作的序号，从 1 开始）";
                    return false;
                }
                OpRecord prior = priorRecords[opNumber - 1];
                if (prior.Result.error != null || prior.TargetTf == null)
                {
                    error = $"{objectId} 指向的节点不可用（op={prior.Result.op}，可能失败、已被删除或本来就不产出节点）";
                    return false;
                }
                result = prior.TargetTf;
                error = null;
                return true;
            }
            return PrefabAddress.TryGetObject(rootTf, objectId, out result, out error);
        }

        /// <summary>创建/移动类操作可选的 RectTransform 摆放：anchor 预设 + anchoredPosition + 尺寸。未指定时不动。</summary>
        static string ApplyRect(Transform targetTf, EditOp op, bool live, bool applySize)
        {
            bool hasSize = applySize && (!string.IsNullOrWhiteSpace(op.width) || !string.IsNullOrWhiteSpace(op.height));
            bool hasAnchor = !string.IsNullOrWhiteSpace(op.anchor);
            bool hasPosition = !string.IsNullOrWhiteSpace(op.anchoredPosition);
            if (!hasSize && !hasAnchor && !hasPosition)
                return null;
            if (!(targetTf is RectTransform rtf))
                return "anchor/anchoredPosition/width/height 只能用于带 RectTransform 的节点: " + targetTf.name;

            RecordUndo(rtf, live);
            if (hasAnchor)
            {
                if (!TryGetAnchorPreset(op.anchor, out Vector2 min, out Vector2 max))
                    return $"未知 anchor: {op.anchor}。可选: {string.Join(", ", SupportedAnchors)}";
                rtf.anchorMin = min;
                rtf.anchorMax = max;
                rtf.pivot = (min + max) * 0.5f;
                // 拉伸方向上 sizeDelta 是「相对父级的边距」，先归零，再让 width/height 覆盖非拉伸方向。
                rtf.offsetMin = Vector2.zero;
                rtf.offsetMax = Vector2.zero;
            }
            if (hasSize)
            {
                if (!TryParseSize(op.width, rtf.sizeDelta.x, out float width, out string error) ||
                    !TryParseSize(op.height, rtf.sizeDelta.y, out float height, out error))
                    return error;
                rtf.sizeDelta = new Vector2(width, height);
            }
            if (hasPosition)
            {
                if (!PrefabAddress.TryParseFloats(op.anchoredPosition, 2, out float[] position, out string error))
                    return "anchoredPosition 需要 x,y: " + error;
                rtf.anchoredPosition = new Vector2(position[0], position[1]);
            }
            return null;
        }

        static bool TryGetAnchorPreset(string anchor, out Vector2 min, out Vector2 max)
        {
            min = max = new Vector2(0.5f, 0.5f);
            switch ((anchor ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "center": return true;
                case "stretch": min = Vector2.zero; max = Vector2.one; return true;
                case "top": min = max = new Vector2(0.5f, 1f); return true;
                case "bottom": min = max = new Vector2(0.5f, 0f); return true;
                case "left": min = max = new Vector2(0f, 0.5f); return true;
                case "right": min = max = new Vector2(1f, 0.5f); return true;
                case "topleft": min = max = new Vector2(0f, 1f); return true;
                case "topright": min = max = Vector2.one; return true;
                case "bottomleft": min = max = Vector2.zero; return true;
                case "bottomright": min = max = new Vector2(1f, 0f); return true;
                case "stretchtop": min = new Vector2(0f, 1f); max = Vector2.one; return true;
                case "stretchbottom": min = Vector2.zero; max = new Vector2(1f, 0f); return true;
                case "stretchleft": min = Vector2.zero; max = new Vector2(0f, 1f); return true;
                case "stretchright": min = new Vector2(1f, 0f); max = Vector2.one; return true;
                default: return false;
            }
        }

        static bool TryParseSize(string text, float fallback, out float value, out string error)
        {
            error = null;
            value = fallback;
            if (string.IsNullOrWhiteSpace(text))
                return true;
            if (!float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value < 0f)
            {
                error = "width/height 必须是不小于 0 的数字: " + text;
                return false;
            }
            return true;
        }

        static bool TryGetComponentType(string name, out Type type, out string error)
        {
            type = null;
            error = null;
            string trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                error = "componentType 不能为空";
                return false;
            }

            // TypeCache 是 Unity 预建的类型索引，比遍历 AppDomain 的 GetTypes() 快，
            // 也不用兜 ReflectionTypeLoadException。
            var matches = new List<Type>();
            foreach (Type candidate in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (candidate.IsAbstract || candidate.IsGenericTypeDefinition)
                    continue;
                if (candidate.FullName == trimmed)
                {
                    type = candidate;
                    return true;
                }
                if (candidate.Name == trimmed)
                    matches.Add(candidate);
            }

            if (matches.Count == 0)
            {
                error = "找不到组件类型: " + trimmed;
                return false;
            }
            if (matches.Count > 1)
            {
                error = "组件类型名有歧义，请用全名: " +
                    string.Join(", ", matches.Take(5).Select(PrefabAddress.FriendlyTypeName));
                return false;
            }
            type = matches[0];
            return true;
        }

        // ---- 实时目标（prefabStage/openScene）的 Undo 注册；离屏副本不入 Undo 栈 ----

        static void RecordUndo(UnityEngine.Object obj, bool live)
        {
            if (live)
                Undo.RecordObject(obj, UndoGroupName);
        }

        static void RegisterCreated(GameObject go, bool live)
        {
            if (live)
                Undo.RegisterCreatedObjectUndo(go, UndoGroupName);
        }

        static void Remove(UnityEngine.Object obj, bool live)
        {
            if (live)
                Undo.DestroyObjectImmediate(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
        }

        /// <summary>siblingIndex 为空时不动；返回错误信息或 null。</summary>
        static string SetSiblingIndex(Transform targetTf, string siblingIndex, bool live)
        {
            if (string.IsNullOrWhiteSpace(siblingIndex))
                return null;
            if (!int.TryParse(siblingIndex.Trim(), out int index) || index < 0)
                return "siblingIndex 必须是不小于 0 的整数: " + siblingIndex;
            // 同级顺序记在父节点上，两边都要记录才能被 Undo 完整还原。
            RecordUndo(targetTf, live);
            if (targetTf.parent != null)
                RecordUndo(targetTf.parent, live);
            int maxIndex = targetTf.parent == null ? 0 : targetTf.parent.childCount - 1;
            targetTf.SetSiblingIndex(Mathf.Clamp(index, 0, Math.Max(0, maxIndex)));
            return null;
        }

        static OpRecord Failed(OpRecord record, string error)
        {
            record.Result.error = error;
            return record;
        }

        static OpRecord Ok(OpRecord record, Transform targetTf, Transform rootTf)
        {
            record.TargetTf = targetTf;
            record.Result.objectId = PrefabAddress.GetObjectId(targetTf, rootTf);
            record.Result.hierarchyPath = PrefabAddress.GetHierarchyPath(targetTf, rootTf);
            return record;
        }
    }
}
