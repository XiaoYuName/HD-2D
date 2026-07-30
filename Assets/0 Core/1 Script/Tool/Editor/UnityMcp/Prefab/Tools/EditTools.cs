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
    /// 任一步失败即中止且不保存；dryRun=true 时全部在内存中预演后丢弃，是真正的 dry-run。
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
            "createObject", "createUi", "instantiatePrefab", "addComponent", "ensureComponent",
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
            public Component TargetComponent;
            public string ComponentAlias;
        }

        public static string Edit(EditRequest command) =>
            PrefabEditProtocol.Execute(command, EditCore);

        static string EditCore(EditRequest command)
        {
            if (command.operations == null || command.operations.Length == 0)
                return BridgeJson.Fail("operations 不能为空");
            if (!EditTarget.TryResolve(command, out EditTarget target, out string error))
                return BridgeJson.Fail(error);

            // prefabAsset 走离屏副本，dryRun=true 是真正的内存预演；
            // prefabStage/openScene 直接改实时对象，无法丢弃预演结果，故不支持预演，靠 Undo 组保证可回滚。
            bool live = !target.IsAsset;
            bool apply = command.ShouldApply;
            int undoGroup = -1;
            using (target)
            {
                if (live)
                {
                    if (!apply)
                        return BridgeJson.Fail("prefabStage/openScene 是实时对象，不支持 dryRun 内存预演；" +
                            "去掉 dryRun 直接执行（批次失败会自动 Undo 回滚）");
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
                    if (record.TargetComponent == null)
                        continue;
                    record.Result.componentIndex = PrefabAddress.GetComponentIndex(record.TargetComponent);
                    if (record.Result.op == "addComponent" || record.Result.op == "ensureComponent")
                    {
                        record.Result.alias = record.ComponentAlias;
                        record.Result.componentRef =
                            $"object:{record.Result.objectId}#{record.Result.componentIndex}";
                    }
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

                if (apply)
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
                    message = apply
                        ? $"已执行 {edit.completed} 个操作并保存" +
                          (target.IsNewAsset ? "（已创建新 Prefab）" : string.Empty) +
                          (live ? "（已标脏，Ctrl+S 保存；Ctrl+Z 可整批撤销）" : string.Empty)
                        : $"{edit.completed} 个操作预演成功；dryRun=true，未保存" +
                          (target.IsNewAsset ? "（正式提交将创建新 Prefab）" : string.Empty),
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
                    if (!SetComponentAlias(record, op.componentRef, priorRecords, out error))
                        return Failed(record, error);
                    Component added = live
                        ? Undo.AddComponent(targetTf.gameObject, type)
                        : targetTf.gameObject.AddComponent(type);
                    if (added == null)
                        return Failed(record, $"AddComponent {type.FullName} 失败" +
                            "（可能与现有组件冲突，如 DisallowMultipleComponent）");
                    error = SetInlineValues(added, op, rootTf, live, priorRecords, out string valueDetail);
                    if (error != null)
                        return Failed(record, error);
                    result.detail = "已添加 " + type.FullName +
                        (string.IsNullOrEmpty(valueDetail) ? string.Empty : "; " + valueDetail);
                    return Ok(record, added, rootTf);
                }
                case "ensureComponent":
                {
                    if (!TryResolve(rootTf, op.objectId, priorRecords, out Transform targetTf, out error))
                        return Failed(record, error);
                    if (!TryGetComponentType(op.componentType, out Type type, out error))
                        return Failed(record, error);
                    if (!SetComponentAlias(record, op.componentRef, priorRecords, out error))
                        return Failed(record, error);

                    Component[] matches = targetTf.GetComponents<Component>()
                        .Where(component => component != null && type.IsInstanceOfType(component))
                        .ToArray();
                    if (matches.Length > 1)
                        return Failed(record, $"节点 {targetTf.name} 上有 {matches.Length} 个 {type.Name}，" +
                            "ensureComponent 无法确定应复用哪一个");
                    bool created = matches.Length == 0;
                    Component ensured = created
                        ? (live ? Undo.AddComponent(targetTf.gameObject, type) : targetTf.gameObject.AddComponent(type))
                        : matches[0];
                    if (ensured == null)
                        return Failed(record, $"AddComponent {type.FullName} 失败" +
                            "（可能与现有组件冲突，如 DisallowMultipleComponent）");
                    error = SetInlineValues(ensured, op, rootTf, live, priorRecords, out string valueDetail);
                    if (error != null)
                        return Failed(record, error);
                    result.detail = (created ? "已添加 " : "已复用 ") + type.FullName +
                        (string.IsNullOrEmpty(valueDetail) ? string.Empty : "; " + valueDetail);
                    return Ok(record, ensured, rootTf);
                }
                case "removeComponent":
                {
                    if (!GetTargetComponent(rootTf, op, priorRecords,
                            out Transform targetTf, out Component component, out error))
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
                    if (!GetTargetComponent(rootTf, op, priorRecords,
                            out Transform targetTf, out Component component, out error))
                        return Failed(record, error);
                    var serializedObject = new SerializedObject(component);
                    error = WriteProperty(serializedObject, component, rootTf, op.propertyPath, op.value,
                        NodeLookupFor(rootTf, priorRecords), ComponentLookupFor(rootTf, priorRecords),
                        out string detail, out bool changed);
                    if (error != null)
                        return Failed(record, error);
                    if (!op.setIfDifferent || changed)
                        ApplySerialized(serializedObject, live);
                    else
                        serializedObject.Update();
                    result.detail = detail;
                    return Ok(record, component, rootTf);
                }
                case "setValues":
                {
                    if (!GetTargetComponent(rootTf, op, priorRecords,
                            out Transform targetTf, out Component component, out error))
                        return Failed(record, error);
                    if (op.values == null || op.values.Count == 0)
                        return Failed(record, "setValues 需要 values（{\"propertyPath\": 值} 对象）");

                    error = SetValues(component, op.values, op.setIfDifferent, rootTf, live, priorRecords,
                        out string detail);
                    if (error != null)
                        return Failed(record, error);
                    result.detail = detail;
                    return Ok(record, component, rootTf);
                }
                default:
                    return Failed(record, $"op {opName} 已登记在 SupportedOps 但没有实现分支");
            }
        }

        // ---- setValue / setValues 的写入 ----

        static string SetInlineValues(Component component, EditOp op, Transform rootTf, bool live,
            List<OpRecord> priorRecords, out string detail)
        {
            detail = null;
            return op.values == null || op.values.Count == 0
                ? null
                : SetValues(component, op.values, op.setIfDifferent, rootTf, live, priorRecords, out detail);
        }

        static string SetValues(Component component, JObject values, bool setIfDifferent, Transform rootTf, bool live,
            List<OpRecord> priorRecords, out string detail)
        {
            var serializedObject = new SerializedObject(component);
            var details = new List<string>();
            bool changed = false;
            SerializedValueWriter.NodeLookup nodeLookup = NodeLookupFor(rootTf, priorRecords);
            SerializedValueWriter.ComponentLookup componentLookup = ComponentLookupFor(rootTf, priorRecords);
            foreach (KeyValuePair<string, JToken> pair in values)
            {
                string error = WriteProperty(serializedObject, component, rootTf, pair.Key,
                    TokenToValueText(pair.Value), nodeLookup, componentLookup,
                    out string fieldDetail, out bool fieldChanged);
                if (error != null)
                {
                    detail = null;
                    return $"{pair.Key}: {error}（本批次不会保存）";
                }
                changed |= fieldChanged;
                details.Add(fieldDetail);
            }

            if (!setIfDifferent || changed)
                ApplySerialized(serializedObject, live);
            else
                serializedObject.Update();
            detail = string.Join("; ", details);
            return null;
        }

        /// <summary>写一个字段并生成 before -> after 说明。value 以 <c>[</c> 开头且字段是数组时整体覆盖数组。</summary>
        static string WriteProperty(SerializedObject serializedObject, Component component, Transform rootTf,
            string propertyPath, string value, SerializedValueWriter.NodeLookup nodeLookup,
            SerializedValueWriter.ComponentLookup componentLookup, out string detail, out bool changed)
        {
            detail = null;
            changed = false;
            SerializedProperty property = string.IsNullOrEmpty(propertyPath)
                ? null
                : serializedObject.FindProperty(propertyPath);
            if (property == null)
                return DescribeMissingField(serializedObject, component, propertyPath);

            string previous = PrefabAddress.GetPropertyValueText(property, rootTf);
            string previousFingerprint = PropertyFingerprint(property, rootTf);
            bool wholeArray = property.isArray &&
                property.propertyType != SerializedPropertyType.String &&
                (value ?? string.Empty).TrimStart().StartsWith("[", StringComparison.Ordinal);
            string error = wholeArray
                ? WriteWholeArray(property, component, rootTf, value, nodeLookup, componentLookup)
                : SerializedValueWriter.Write(property, component, rootTf, value, nodeLookup, componentLookup);
            if (error != null)
                return error;

            string current = PrefabAddress.GetPropertyValueText(property, rootTf);
            changed = !string.Equals(previousFingerprint, PropertyFingerprint(property, rootTf),
                StringComparison.Ordinal);
            detail = changed
                ? $"{component.GetType().Name}.{propertyPath}: {previous} -> {current}"
                : $"{component.GetType().Name}.{propertyPath}: unchanged";
            return null;
        }

        /// <summary>批内 $n 也能用在 object: 引用里，所以把解析器透传给值写入层。</summary>
        static SerializedValueWriter.NodeLookup NodeLookupFor(Transform rootTf, List<OpRecord> priorRecords) =>
            (string objectId, out Transform result, out string error) =>
                TryResolve(rootTf, objectId, priorRecords, out result, out error);

        static SerializedValueWriter.ComponentLookup ComponentLookupFor(
            Transform rootTf, List<OpRecord> priorRecords) =>
            (string componentRef, out Component result, out string error) =>
                GetComponentReference(rootTf, componentRef, priorRecords, out result, out error);

        static string PropertyFingerprint(SerializedProperty property, Transform rootTf)
        {
            if (!property.isArray || property.propertyType == SerializedPropertyType.String)
                return PrefabAddress.GetPropertyValueText(property, rootTf);
            return property.arraySize + "[" + string.Join("|", Enumerable.Range(0, property.arraySize)
                .Select(index => PropertyFingerprint(property.GetArrayElementAtIndex(index), rootTf))) + "]";
        }

        /// <summary>字段找不到：区分"名字写错"和"脚本还没编译"，两者处置相反。</summary>
        static string DescribeMissingField(SerializedObject serializedObject, Component component, string propertyPath)
        {
            string message = $"组件 {component.GetType().Name} 上找不到序列化字段 {propertyPath}";
            string[] near = NearFieldNames(serializedObject, PrefabAddress.RootFieldName(propertyPath));
            if (near.Length > 0)
                message += "；相近字段: " + string.Join(", ", near);

            CompileSnapshot snapshot = CompileTracker.GetSnapshot(0);
            if (snapshot.isCompiling || snapshot.resultStale)
                message += "；脚本尚未编译完（新加的字段编译后才存在），" +
                    "先用 get_unity_compile_status 的 waitSeconds 等到结果新鲜再重试";
            return message;
        }

        /// <summary>顶层字段里挑名字相近的：忽略大小写的包含关系，或编辑距离 <= 2。</summary>
        static string[] NearFieldNames(SerializedObject serializedObject, string name)
        {
            var near = new List<string>();
            SerializedProperty iterator = serializedObject.GetIterator();
            if (!iterator.NextVisible(true))
                return near.ToArray();
            do
            {
                string candidate = iterator.propertyPath;
                if (candidate == "m_Script" || candidate.IndexOf('.') >= 0)
                    continue;
                if (candidate.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    EditDistance(candidate, name) <= 2)
                    near.Add(candidate);
            } while (iterator.NextVisible(false) && near.Count < 8);
            return near.ToArray();
        }

        static int EditDistance(string a, string b)
        {
            a = a.ToLowerInvariant();
            b = b.ToLowerInvariant();
            var row = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++)
                row[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                int diagonal = row[0];
                row[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int previous = row[j];
                    row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1),
                        diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                    diagonal = previous;
                }
            }
            return row[b.Length];
        }

        /// <summary>JSON 数组整体写入：先定 size 再逐个写元素，省掉 Array.size + 每元素一条 setValue 的啰嗦写法。</summary>
        static string WriteWholeArray(SerializedProperty property, Component component, Transform rootTf, string value,
            SerializedValueWriter.NodeLookup nodeLookup, SerializedValueWriter.ComponentLookup componentLookup)
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
                string error = SerializedValueWriter.Write(element, component, rootTf,
                    TokenToValueText(items[i]), nodeLookup, componentLookup);
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

        static bool SetComponentAlias(OpRecord record, string requestedAlias, List<OpRecord> priorRecords,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(requestedAlias))
                return true;

            string alias = requestedAlias.Trim();
            if (alias[0] == '$' || alias.StartsWith("object:", StringComparison.OrdinalIgnoreCase) ||
                alias.StartsWith("component:", StringComparison.OrdinalIgnoreCase))
            {
                error = "componentRef 别名不能使用保留前缀 $n、object: 或 component: " + alias;
                return false;
            }
            if (priorRecords.Any(item => string.Equals(item.ComponentAlias, alias,
                    StringComparison.OrdinalIgnoreCase)))
            {
                error = "componentRef 别名在本批次中重复: " + alias;
                return false;
            }
            record.ComponentAlias = alias;
            return true;
        }

        /// <summary>
        /// 组件目标优先级：componentRef（批内别名/$n/跨调用 object:id#index）、
        /// 唯一 componentType、最后才是易漂移的 componentIndex。
        /// </summary>
        static bool GetTargetComponent(Transform rootTf, EditOp op, List<OpRecord> priorRecords,
            out Transform targetTf, out Component component, out string error)
        {
            if (!string.IsNullOrWhiteSpace(op.componentRef))
            {
                if (!GetComponentReference(rootTf, op.componentRef, priorRecords, out component, out error))
                {
                    targetTf = null;
                    return false;
                }
                targetTf = component.transform;
                return true;
            }
            if (!TryResolve(rootTf, op.objectId, priorRecords, out targetTf, out error))
            {
                component = null;
                return false;
            }
            return string.IsNullOrWhiteSpace(op.componentType)
                ? PrefabAddress.TryGetComponentAt(targetTf, op.componentIndex, out component, out error)
                : TryGetComponentByType(targetTf, op.componentType, out component, out error);
        }

        static bool GetComponentReference(Transform rootTf, string reference, List<OpRecord> priorRecords,
            out Component component, out string error)
        {
            component = null;
            string value = (reference ?? string.Empty).Trim();
            if (value.StartsWith("component:", StringComparison.OrdinalIgnoreCase))
                value = value.Substring("component:".Length).Trim();
            if (value.Length == 0)
            {
                error = "componentRef 不能为空";
                return false;
            }

            if (value[0] == '$')
            {
                if (!int.TryParse(value.Substring(1), out int opNumber) ||
                    opNumber < 1 || opNumber > priorRecords.Count)
                {
                    error = $"组件 $n 引用无效: {value}（n 是已执行操作序号，从 1 开始）";
                    return false;
                }
                component = priorRecords[opNumber - 1].TargetComponent;
                if (component == null)
                {
                    error = $"{value} 没有产出可用组件；请引用 addComponent/ensureComponent 或组件字段操作";
                    return false;
                }
                error = null;
                return true;
            }

            if (value.StartsWith("object:", StringComparison.OrdinalIgnoreCase))
            {
                string spec = value.Substring("object:".Length);
                int hash = spec.LastIndexOf('#');
                if (hash <= 0)
                {
                    error = "跨调用 componentRef 需要 object:<objectId>#<componentIndex>: " + value;
                    return false;
                }
                string indexText = spec.Substring(hash + 1);
                int colon = indexText.IndexOf(':');
                if (colon >= 0)
                    indexText = indexText.Substring(0, colon);
                if (!int.TryParse(indexText, out int index) || index < 0)
                {
                    error = "componentRef 的 componentIndex 无效: " + value;
                    return false;
                }
                if (!TryResolve(rootTf, spec.Substring(0, hash), priorRecords,
                        out Transform targetTf, out error))
                    return false;
                return PrefabAddress.TryGetComponentAt(targetTf, index, out component, out error);
            }

            OpRecord match = priorRecords.LastOrDefault(item =>
                string.Equals(item.ComponentAlias, value, StringComparison.OrdinalIgnoreCase));
            if (match == null || match.TargetComponent == null)
            {
                string aliases = string.Join(", ", priorRecords
                    .Where(item => item.TargetComponent != null && !string.IsNullOrEmpty(item.ComponentAlias))
                    .Select(item => item.ComponentAlias));
                error = "找不到批内 componentRef 别名: " + value +
                    (aliases.Length == 0 ? "（本批次尚未声明组件别名）" : "；可选: " + aliases);
                return false;
            }
            component = match.TargetComponent;
            error = null;
            return true;
        }

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

        /// <summary>按类型名定位唯一组件：多个同类型不猜、报出各自 index；找不到就列出现有组件，省一次读树。</summary>
        static bool TryGetComponentByType(Transform targetTf, string typeName,
            out Component component, out string error)
        {
            component = null;
            if (!TryGetComponentType(typeName, out Type type, out error))
                return false;

            Component[] components = targetTf.GetComponents<Component>();
            var matches = new List<int>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && type.IsInstanceOfType(components[i]))
                    matches.Add(i);
            }

            if (matches.Count == 0)
            {
                error = $"节点 {targetTf.name} 上没有组件 {type.FullName}。现有组件: " +
                    string.Join(", ", components.Select((item, index) =>
                        $"{index}={(item == null ? "<丢失脚本>" : item.GetType().Name)}"));
                return false;
            }
            if (matches.Count > 1)
            {
                error = $"节点 {targetTf.name} 上有 {matches.Count} 个 {type.Name}，" +
                    "请改用 componentIndex 指定: " + string.Join(", ", matches);
                return false;
            }
            component = components[matches[0]];
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

        static OpRecord Ok(OpRecord record, Component component, Transform rootTf)
        {
            record.TargetComponent = component;
            return Ok(record, component.transform, rootTf);
        }
    }
}
