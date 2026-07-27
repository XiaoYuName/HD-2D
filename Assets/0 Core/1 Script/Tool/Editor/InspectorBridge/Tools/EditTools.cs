using System;
using System.Collections.Generic;
using System.Linq;
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
            "removeComponent", "removeMissingScripts", "setValue",
        };

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

                var results = new List<EditOpResult>();
                foreach (EditOp op in command.operations)
                {
                    EditOpResult result = Execute(target, op, live, results);
                    results.Add(result);
                    if (result.error != null)
                        break;
                }

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

        static EditOpResult Execute(EditTarget target, EditOp op, bool live, List<EditOpResult> priorResults)
        {
            var result = new EditOpResult { op = op.op };
            try
            {
                return ExecuteCore(target, op, live, priorResults, result);
            }
            catch (Exception e)
            {
                return Failed(result, e.Message);
            }
        }

        static EditOpResult ExecuteCore(EditTarget target, EditOp op, bool live,
            List<EditOpResult> priorResults, EditOpResult result)
        {
            Transform rootTf = target.RootTf;
            string error;
            string opName = (op.op ?? string.Empty).Trim();
            // 先按词表挡一道：新增分支忘了登记会在这里报错，而不是悄悄多出一个 AI 看不见的 op。
            if (System.Array.IndexOf(SupportedOps, opName) < 0)
                return Failed(result, $"未知 op: {op.op}。支持: {string.Join(", ", SupportedOps)}");
            switch (opName)
            {
                case "rename":
                {
                    if (!TryResolve(rootTf, op.objectId, priorResults, out Transform targetTf, out error))
                        return Failed(result, error);
                    if (string.IsNullOrWhiteSpace(op.newName))
                        return Failed(result, "rename 需要 newName");
                    RecordUndo(targetTf.gameObject, live);
                    result.detail = targetTf.name + " -> " + op.newName.Trim();
                    targetTf.name = op.newName.Trim();
                    return Ok(result, targetTf, rootTf);
                }
                case "setActive":
                {
                    if (!TryResolve(rootTf, op.objectId, priorResults, out Transform targetTf, out error))
                        return Failed(result, error);
                    if (!PrefabAddress.TryParseBool(op.active, out bool active))
                        return Failed(result, "setActive 需要 active=\"true\"|\"false\"");
                    RecordUndo(targetTf.gameObject, live);
                    targetTf.gameObject.SetActive(active);
                    result.detail = "activeSelf=" + (active ? "true" : "false");
                    return Ok(result, targetTf, rootTf);
                }
                case "reparent":
                {
                    if (!TryResolve(rootTf, op.objectId, priorResults, out Transform targetTf, out error))
                        return Failed(result, error);
                    if (targetTf == rootTf)
                        return Failed(result, "不能移动根节点");
                    if (string.IsNullOrEmpty(op.parentObjectId))
                        return Failed(result, "reparent 需要 parentObjectId（根节点为 0）");
                    if (!TryResolve(rootTf, op.parentObjectId, priorResults, out Transform parentTf, out error))
                        return Failed(result, error);
                    if (parentTf == targetTf || parentTf.IsChildOf(targetTf))
                        return Failed(result, "不能把节点移动到它自己或它的子级下");
                    string previous = PrefabAddress.GetHierarchyPath(targetTf, rootTf);
                    if (live)
                        Undo.SetTransformParent(targetTf, parentTf, false, UndoGroupName);
                    else
                        targetTf.SetParent(parentTf, false);
                    error = SetSiblingIndex(targetTf, op.siblingIndex, live);
                    if (error != null)
                        return Failed(result, error);
                    result.detail = previous + " -> " + PrefabAddress.GetHierarchyPath(targetTf, rootTf);
                    return Ok(result, targetTf, rootTf);
                }
                case "setSiblingIndex":
                {
                    if (!TryResolve(rootTf, op.objectId, priorResults, out Transform targetTf, out error))
                        return Failed(result, error);
                    if (targetTf == rootTf)
                        return Failed(result, "不能调整根节点");
                    if (string.IsNullOrWhiteSpace(op.siblingIndex))
                        return Failed(result, "setSiblingIndex 需要 siblingIndex");
                    error = SetSiblingIndex(targetTf, op.siblingIndex, live);
                    if (error != null)
                        return Failed(result, error);
                    result.detail = "siblingIndex=" + targetTf.GetSiblingIndex();
                    return Ok(result, targetTf, rootTf);
                }
                case "delete":
                {
                    if (!TryResolve(rootTf, op.objectId, priorResults, out Transform targetTf, out error))
                        return Failed(result, error);
                    if (targetTf == rootTf)
                        return Failed(result, "不能删除根节点");
                    result.hierarchyPath = PrefabAddress.GetHierarchyPath(targetTf, rootTf);
                    result.detail = "已删除";
                    Remove(targetTf.gameObject, live);
                    return result;
                }
                case "duplicate":
                {
                    if (!TryResolve(rootTf, op.objectId, priorResults, out Transform targetTf, out error))
                        return Failed(result, error);
                    if (targetTf == rootTf)
                        return Failed(result, "不能复制根节点");
                    GameObject copyGo = UnityEngine.Object.Instantiate(targetTf.gameObject, targetTf.parent);
                    copyGo.name = string.IsNullOrWhiteSpace(op.newName) ? targetTf.name : op.newName.Trim();
                    RegisterCreated(copyGo, live);
                    copyGo.transform.SetSiblingIndex(targetTf.GetSiblingIndex() + 1);
                    error = SetSiblingIndex(copyGo.transform, op.siblingIndex, live);
                    if (error != null)
                    {
                        Remove(copyGo, live);
                        return Failed(result, error);
                    }
                    result.detail = "复制自 " + PrefabAddress.GetHierarchyPath(targetTf, rootTf);
                    return Ok(result, copyGo.transform, rootTf);
                }
                case "createObject":
                {
                    if (!TryResolve(rootTf, op.parentObjectId, priorResults, out Transform parentTf, out error))
                        return Failed(result, error);
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
                    error = SetSiblingIndex(createdGo.transform, op.siblingIndex, live);
                    if (error != null)
                    {
                        Remove(createdGo, live);
                        return Failed(result, error);
                    }
                    result.detail = parentTf is RectTransform ? "已创建（含 RectTransform）" : "已创建";
                    return Ok(result, createdGo.transform, rootTf);
                }
                case "createUi":
                {
                    if (!TryResolve(rootTf, op.parentObjectId, priorResults, out Transform parentTf, out error))
                        return Failed(result, error);
                    if (!(parentTf is RectTransform))
                        return Failed(result, $"父节点 {PrefabAddress.GetHierarchyPath(parentTf, rootTf)} " +
                            "没有 RectTransform，不适合作为 UI 父节点");
                    if (!UiElementFactory.IsSupported(op.elementType))
                        return Failed(result, "不支持的 elementType。可选: " +
                            string.Join(", ", UiElementFactory.SupportedTypes));

                    PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
                    if (!TryParseSize(op.width, settings.DefaultUiSize.x, out float width, out error) ||
                        !TryParseSize(op.height, settings.DefaultUiSize.y, out float height, out error))
                        return Failed(result, error);
                    string name = string.IsNullOrWhiteSpace(op.newName)
                        ? UiElementFactory.DefaultName(op.elementType)
                        : op.newName.Trim();

                    GameObject createdGo = UiElementFactory.Build(op.elementType, name, op.label,
                        parentTf, width, height, settings);
                    RegisterCreated(createdGo, live);
                    error = SetSiblingIndex(createdGo.transform, op.siblingIndex, live);
                    if (error != null)
                    {
                        Remove(createdGo, live);
                        return Failed(result, error);
                    }
                    result.detail = $"已创建 {op.elementType}（{width}x{height}）";
                    return Ok(result, createdGo.transform, rootTf);
                }
                case "instantiatePrefab":
                {
                    if (!TryResolve(rootTf, op.parentObjectId, priorResults, out Transform parentTf, out error))
                        return Failed(result, error);
                    string sourceError = PrefabAddress.ValidatePrefabPath(op.sourcePrefabPath);
                    if (sourceError != null)
                        return Failed(result, sourceError);
                    // 用 PolicyPath 而不是请求里的 prefabPath：prefabStage 模式下 prefabPath 为空，
                    // 拿它比对会让「把当前 Prefab 嵌套进它自己」漏过检查。
                    if (string.Equals(PrefabAddress.NormalizeSlashes(op.sourcePrefabPath),
                            PrefabAddress.NormalizeSlashes(target.PolicyPath), StringComparison.OrdinalIgnoreCase))
                        return Failed(result, "不能把 Prefab 嵌套进它自己");
                    GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(op.sourcePrefabPath);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab, parentTf);
                    if (instance == null)
                        return Failed(result, "实例化失败: " + op.sourcePrefabPath);
                    if (!string.IsNullOrWhiteSpace(op.newName))
                        instance.name = op.newName.Trim();
                    RegisterCreated(instance, live);
                    error = SetSiblingIndex(instance.transform, op.siblingIndex, live);
                    if (error != null)
                    {
                        Remove(instance, live);
                        return Failed(result, error);
                    }
                    result.detail = "来自 " + op.sourcePrefabPath;
                    return Ok(result, instance.transform, rootTf);
                }
                case "addComponent":
                {
                    if (!TryResolve(rootTf, op.objectId, priorResults, out Transform targetTf, out error))
                        return Failed(result, error);
                    if (!TryGetComponentType(op.componentType, out Type type, out error))
                        return Failed(result, error);
                    Component added = live
                        ? Undo.AddComponent(targetTf.gameObject, type)
                        : targetTf.gameObject.AddComponent(type);
                    if (added == null)
                        return Failed(result, $"AddComponent {type.FullName} 失败" +
                            "（可能与现有组件冲突，如 DisallowMultipleComponent）");
                    result.componentIndex = PrefabAddress.GetComponentIndex(added);
                    result.detail = "已添加 " + type.FullName;
                    return Ok(result, targetTf, rootTf);
                }
                case "removeComponent":
                {
                    if (!TryResolve(rootTf, op.objectId, priorResults, out Transform targetTf, out error))
                        return Failed(result, error);
                    if (!PrefabAddress.TryGetComponentAt(targetTf, op.componentIndex, out Component component, out error))
                        return Failed(result, error + "（丢失脚本请用 removeMissingScripts）");
                    if (component is Transform)
                        return Failed(result, "不能移除 Transform/RectTransform");
                    string typeName = component.GetType().FullName;
                    Remove(component, live);
                    if (component != null)
                        return Failed(result, $"移除 {typeName} 失败（可能被其他组件 RequireComponent 依赖）");
                    result.detail = "已移除 " + typeName;
                    return Ok(result, targetTf, rootTf);
                }
                case "removeMissingScripts":
                {
                    if (!TryResolve(rootTf, op.objectId, priorResults, out Transform targetTf, out error))
                        return Failed(result, error);
                    RecordUndo(targetTf.gameObject, live);
                    int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(targetTf.gameObject);
                    result.detail = $"移除了 {removed} 个丢失脚本";
                    return Ok(result, targetTf, rootTf);
                }
                case "setValue":
                {
                    if (!TryResolve(rootTf, op.objectId, priorResults, out Transform targetTf, out error))
                        return Failed(result, error);
                    if (!PrefabAddress.TryGetComponentAt(targetTf, op.componentIndex, out Component component, out error))
                        return Failed(result, error);
                    var serializedObject = new SerializedObject(component);
                    SerializedProperty property = string.IsNullOrEmpty(op.propertyPath)
                        ? null
                        : serializedObject.FindProperty(op.propertyPath);
                    if (property == null)
                        return Failed(result, $"组件 {component.GetType().Name} 上找不到序列化字段 {op.propertyPath}");
                    string previous = PrefabAddress.GetPropertyValueText(property, rootTf);
                    error = SerializedValueWriter.Write(property, component, rootTf, op.value);
                    if (error != null)
                        return Failed(result, error);
                    if (live)
                        serializedObject.ApplyModifiedProperties();
                    else
                        serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    result.componentIndex = op.componentIndex;
                    result.detail = $"{component.GetType().Name}.{op.propertyPath}: {previous} -> " +
                        PrefabAddress.GetPropertyValueText(property, rootTf);
                    return Ok(result, targetTf, rootTf);
                }
                default:
                    return Failed(result, $"op {opName} 已登记在 SupportedOps 但没有实现分支");
            }
        }

        /// <summary>
        /// 解析 objectId，额外支持 <c>$n</c>：批内第 n 条操作（1 起）返回的节点。
        /// 结构改动会让后面的 sibling index 全部平移，让 AI 自己推演是这套协议最容易出错的地方，
        /// 用 $n 就不必推演。
        /// </summary>
        static bool TryResolve(Transform rootTf, string objectId, List<EditOpResult> priorResults,
            out Transform result, out string error)
        {
            if (!string.IsNullOrEmpty(objectId) && objectId[0] == '$')
            {
                if (!int.TryParse(objectId.Substring(1).Trim(), out int opNumber) ||
                    opNumber < 1 || opNumber > priorResults.Count)
                {
                    result = null;
                    error = $"$n 引用无效: {objectId}（n 是本批次中已执行操作的序号，从 1 开始）";
                    return false;
                }
                EditOpResult prior = priorResults[opNumber - 1];
                if (prior.error != null || string.IsNullOrEmpty(prior.objectId))
                {
                    result = null;
                    error = $"{objectId} 指向的操作没有返回可用节点（op={prior.op}）";
                    return false;
                }
                objectId = prior.objectId;
            }
            return PrefabAddress.TryGetObject(rootTf, objectId, out result, out error);
        }

        static bool TryParseSize(string text, float fallback, out float value, out string error)
        {
            error = null;
            value = fallback;
            if (string.IsNullOrWhiteSpace(text))
                return true;
            if (!float.TryParse(text.Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value) || value <= 0f)
            {
                error = "width/height 必须是大于 0 的数字: " + text;
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

        static EditOpResult Failed(EditOpResult result, string error)
        {
            result.error = error;
            return result;
        }

        static EditOpResult Ok(EditOpResult result, Transform targetTf, Transform rootTf)
        {
            result.objectId = PrefabAddress.GetObjectId(targetTf, rootTf);
            result.hierarchyPath = PrefabAddress.GetHierarchyPath(targetTf, rootTf);
            return result;
        }
    }
}
