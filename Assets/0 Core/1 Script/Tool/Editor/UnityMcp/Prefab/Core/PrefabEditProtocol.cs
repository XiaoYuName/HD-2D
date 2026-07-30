using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityMcp
{
    /// <summary>
    /// edit_prefab 的协议外壳。核心编辑器只负责一次事务，本类负责跨请求的
    /// dry-run 计划、乐观并发、幂等重放、写锁和响应裁剪。
    /// </summary>
    static class PrefabEditProtocol
    {
        static readonly HashSet<string> ResponseModes =
            new HashSet<string>(StringComparer.Ordinal) { "summary", "changed", "full" };

        static readonly HashSet<string> ReferenceOps =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "createObject", "createUi", "duplicate", "instantiatePrefab",
                "addComponent", "ensureComponent",
            };

        public static string Execute(EditRequest command, Func<EditRequest, string> executeCore)
        {
            try
            {
                return ExecuteRequest(command, executeCore);
            }
            catch (Exception e)
            {
                return InternalError(e);
            }
        }

        static string ExecuteRequest(EditRequest command, Func<EditRequest, string> executeCore)
        {
            string responseMode = string.IsNullOrEmpty(command.responseMode)
                ? "summary"
                : command.responseMode.Trim();
            if (!ResponseModes.Contains(responseMode))
                return Error("RESPONSE_MODE_INVALID",
                    $"responseMode 无效: {command.responseMode}；可选 summary/changed/full");

            EditRequest effectiveCommand = command;
            string requestHash = PrefabEditTransactionStore.GetRequestHash(command);
            string planId = null;
            if (!string.IsNullOrEmpty(command.planId))
            {
                if (command.operations != null && command.operations.Length > 0)
                    return Error("PLAN_OPERATIONS_CONFLICT", "提交 planId 时不要再传 operations");
                if (!PrefabEditTransactionStore.TryGetPlan(
                        command.planId, out PrefabEditTransactionStore.Plan plan,
                        out string planErrorCode, out string planError))
                    return Error(planErrorCode, planError);
                if (!string.IsNullOrEmpty(command.expectedRevision) &&
                    !string.Equals(command.expectedRevision, plan.revision, StringComparison.Ordinal))
                {
                    return Error("PLAN_REVISION_MISMATCH",
                        $"expectedRevision 与计划版本不一致：计划 {plan.revision}，请求 {command.expectedRevision}",
                        plan.revision);
                }
                if (!string.IsNullOrEmpty(command.prefabPath))
                {
                    if (!PrefabAddress.TryNormalizePrefabPath(
                            command.prefabPath, out string requestPath, out string pathError))
                        return Error("TARGET_INVALID", pathError);
                    PrefabAddress.TryNormalizePrefabPath(
                        plan.request.prefabPath, out string planPath, out _);
                    if (!string.Equals(requestPath, planPath, StringComparison.OrdinalIgnoreCase))
                        return Error("PLAN_TARGET_MISMATCH", "planId 与请求的 prefabPath 不一致");
                }

                effectiveCommand = JsonConvert.DeserializeObject<EditRequest>(
                    JsonConvert.SerializeObject(plan.request));
                effectiveCommand.dryRun = false;
                effectiveCommand.apply = true;
                effectiveCommand.planId = null;
                effectiveCommand.expectedRevision = plan.revision;
                effectiveCommand.idempotencyKey = command.idempotencyKey;
                effectiveCommand.responseMode = responseMode;
                effectiveCommand.expectedProjectPath = command.expectedProjectPath;
                planId = plan.id;
            }
            else if (command.operations == null || command.operations.Length == 0)
            {
                return Error("OPERATIONS_REQUIRED", "operations 不能为空；或只传 planId 提交先前的 dryRun");
            }

            if (!PrefabEditTransactionStore.TryGetTargetState(
                    effectiveCommand, out PrefabEditTransactionStore.TargetState target, out string targetError))
                return Error("TARGET_INVALID", targetError);

            PrefabEditTransactionStore.IdempotencyResult idempotency =
                PrefabEditTransactionStore.GetIdempotency(
                    command.idempotencyKey, requestHash, target.revision);
            if (idempotency.state == PrefabEditTransactionStore.IdempotencyState.Replay)
                return FormatResponse(idempotency.response, responseMode, true);
            if (idempotency.state == PrefabEditTransactionStore.IdempotencyState.Conflict ||
                idempotency.state == PrefabEditTransactionStore.IdempotencyState.Pending)
                return Error(idempotency.errorCode, idempotency.error, target.revision);

            if (!effectiveCommand.ShouldApply)
            {
                if (!PrefabEditTransactionStore.TryStartWrite(
                        target.key, out IDisposable previewLease, out string previewLockError))
                    return Error("TARGET_LOCKED", previewLockError, target.revision, true);
                using (previewLease)
                {
                    if (!PrefabEditTransactionStore.TryGetTargetState(
                            effectiveCommand, out target, out targetError))
                        return Error("TARGET_INVALID", targetError);
                    if (!PrefabEditTransactionStore.CanUseExpectedRevision(
                            target, effectiveCommand.expectedRevision, out string revisionError))
                        return Error("REVISION_CONFLICT", revisionError, target.revision);
                    return ExecuteDryRun(
                        command, effectiveCommand, executeCore, requestHash, target, responseMode);
                }
            }

            if (!PrefabEditTransactionStore.TryStartWrite(
                    target.key, out IDisposable writeLease, out string lockError))
                return Error("WRITE_LOCKED", lockError, target.revision, true);
            using (writeLease)
            {
                // 固定锁顺序 target -> idempotency，避免不同目标复用同一 key 时各自越过 Get/Start。
                if (!PrefabEditTransactionStore.TryStartIdempotency(
                        command.idempotencyKey, out IDisposable idempotencyLease, out string keyLockError))
                    return Error("IDEMPOTENCY_LOCKED", keyLockError, target.revision, true);
                using (idempotencyLease)
                {
                    // 锁外读到的 revision 只能用于快速重放判断；写入前必须在双锁内重读。
                    if (!PrefabEditTransactionStore.TryGetTargetState(
                            effectiveCommand, out target, out targetError))
                        return Error("TARGET_INVALID", targetError);

                    idempotency = PrefabEditTransactionStore.GetIdempotency(
                        command.idempotencyKey, requestHash, target.revision);
                    if (idempotency.state == PrefabEditTransactionStore.IdempotencyState.Replay)
                        return FormatResponse(idempotency.response, responseMode, true);
                    if (idempotency.state == PrefabEditTransactionStore.IdempotencyState.Conflict ||
                        idempotency.state == PrefabEditTransactionStore.IdempotencyState.Pending)
                        return Error(idempotency.errorCode, idempotency.error, target.revision);
                    if (!PrefabEditTransactionStore.CanUseExpectedRevision(
                            target, effectiveCommand.expectedRevision, out string revisionError))
                        return Error("REVISION_CONFLICT", revisionError, target.revision);

                    PrefabEditTransactionStore.StartIdempotency(
                        command.idempotencyKey, requestHash, target.key, target.revision);
                    string response = ExecuteCoreSafely(effectiveCommand, executeCore);
                    string revision = target.revision;
                    if (PrefabEditTransactionStore.TryGetTargetState(
                            effectiveCommand, out PrefabEditTransactionStore.TargetState after, out _))
                        revision = after.revision;
                    response = DecorateResponse(response, planId, revision, false);
                    PrefabEditTransactionStore.EndIdempotency(command.idempotencyKey, response);
                    return FormatResponse(response, responseMode, false);
                }
            }
        }

        static string ExecuteDryRun(EditRequest originalCommand, EditRequest effectiveCommand,
            Func<EditRequest, string> executeCore, string requestHash,
            PrefabEditTransactionStore.TargetState target, string responseMode)
        {
            if (!PrefabEditTransactionStore.TryStartIdempotency(
                    originalCommand.idempotencyKey,
                    out IDisposable idempotencyLease, out string keyLockError))
                return Error("IDEMPOTENCY_LOCKED", keyLockError, target.revision, true);
            using (idempotencyLease)
            {
                PrefabEditTransactionStore.IdempotencyResult idempotency =
                    PrefabEditTransactionStore.GetIdempotency(
                        originalCommand.idempotencyKey, requestHash, target.revision);
                if (idempotency.state == PrefabEditTransactionStore.IdempotencyState.Replay)
                    return FormatResponse(idempotency.response, responseMode, true);
                if (idempotency.state == PrefabEditTransactionStore.IdempotencyState.Conflict ||
                    idempotency.state == PrefabEditTransactionStore.IdempotencyState.Pending)
                    return Error(idempotency.errorCode, idempotency.error, target.revision);

                string response = ExecuteCoreSafely(effectiveCommand, executeCore);
                JObject payload = ParseResponse(response);
                string planId = null;
                string resultRevision = target.revision;
                if (payload["error"] == null)
                {
                    if (!PrefabEditTransactionStore.TryGetTargetState(
                            effectiveCommand, out PrefabEditTransactionStore.TargetState after, out _) ||
                        !string.Equals(target.revision, after.revision, StringComparison.Ordinal))
                    {
                        resultRevision = after?.revision ?? target.revision;
                        response = Error("REVISION_CHANGED_DURING_PREVIEW",
                            "Prefab 在 dryRun 期间发生变化，未生成 plan；请重试",
                            resultRevision, true);
                    }
                    else
                    {
                        planId = PrefabEditTransactionStore.CreatePlan(effectiveCommand, target.revision);
                    }
                }
                response = DecorateResponse(response, planId, resultRevision, false);
                PrefabEditTransactionStore.StartIdempotency(
                    originalCommand.idempotencyKey, requestHash, target.key, target.revision);
                PrefabEditTransactionStore.EndIdempotency(originalCommand.idempotencyKey, response);
                return FormatResponse(response, responseMode, false);
            }
        }

        static string ExecuteCoreSafely(EditRequest command, Func<EditRequest, string> executeCore)
        {
            try
            {
                return executeCore(command);
            }
            catch (Exception e)
            {
                return InternalError(e);
            }
        }

        static string InternalError(Exception error)
        {
            string debugId = Guid.NewGuid().ToString("N").Substring(0, 12);
            Debug.LogError($"Prefab MCP edit_prefab 内部错误 [{debugId}]\n{error}");
            return Error("INTERNAL_ERROR", "编辑事务发生内部错误；详情见 Unity Console",
                debugId: debugId);
        }

        static string DecorateResponse(
            string response, string planId, string revision, bool replayed)
        {
            JObject payload = ParseResponse(response);
            var edit = payload["edit"] as JObject ?? new JObject();
            if (!string.IsNullOrEmpty(planId))
                edit["planId"] = planId;
            if (!string.IsNullOrEmpty(revision))
                edit["revision"] = revision;
            if (replayed)
                edit["replayed"] = true;
            if (edit.HasValues)
                payload["edit"] = edit;
            AddFailureLocation(payload);
            return payload.ToString(Newtonsoft.Json.Formatting.None);
        }

        static string FormatResponse(string response, string responseMode, bool replayed)
        {
            JObject payload = ParseResponse(response);
            if (replayed)
            {
                var replayEdit = payload["edit"] as JObject ?? new JObject();
                replayEdit["replayed"] = true;
                payload["edit"] = replayEdit;
            }
            AddFailureLocation(payload);
            if (responseMode == "full")
                return payload.ToString(Newtonsoft.Json.Formatting.None);

            payload.Remove("message");
            if (!(payload["edit"] is JObject edit) || !(edit["results"] is JArray results))
                return payload.ToString(Newtonsoft.Json.Formatting.None);

            if (responseMode == "changed")
            {
                foreach (JObject result in results.OfType<JObject>())
                    result.Remove("hierarchyPath");
                return payload.ToString(Newtonsoft.Json.Formatting.None);
            }

            var references = new JArray();
            bool failed = payload["error"] != null;
            foreach (JObject result in results.OfType<JObject>())
            {
                string op = (string)result["op"];
                if (failed && result["error"] == null)
                    continue;
                if (!failed && result["error"] == null && !ReferenceOps.Contains(op) &&
                    result["componentRef"] == null && result["alias"] == null)
                    continue;
                var compact = new JObject { ["op"] = op };
                Copy(result, compact, "error");
                Copy(result, compact, "objectId");
                Copy(result, compact, "componentIndex");
                Copy(result, compact, "componentRef");
                Copy(result, compact, "alias");
                references.Add(compact);
            }
            if (references.Count == 0)
                edit.Remove("results");
            else
                edit["results"] = references;
            return payload.ToString(Newtonsoft.Json.Formatting.None);
        }

        static void AddFailureLocation(JObject payload)
        {
            if (payload["error"] == null)
                return;
            if (payload["errorCode"] == null)
                payload["errorCode"] = "EDIT_FAILED";
            if (payload["failedOpIndex"] != null ||
                !(payload["edit"]?["results"] is JArray results))
                return;
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i]?["error"] == null)
                    continue;
                payload["failedOpIndex"] = i;
                return;
            }
        }

        static void Copy(JObject source, JObject target, string name)
        {
            if (source[name] != null)
                target[name] = source[name].DeepClone();
        }

        static JObject ParseResponse(string response)
        {
            try
            {
                return JObject.Parse(response ?? string.Empty);
            }
            catch
            {
                return new JObject
                {
                    ["error"] = "编辑事务返回了无法解析的响应",
                    ["errorCode"] = "RESPONSE_INVALID",
                };
            }
        }

        static string Error(string code, string message, string revision = null,
            bool? retryable = null, string debugId = null)
        {
            var payload = new JObject
            {
                ["error"] = message,
                ["errorCode"] = code,
            };
            if (!string.IsNullOrEmpty(revision))
                payload["currentRevision"] = revision;
            if (retryable.HasValue)
                payload["retryable"] = retryable.Value;
            if (!string.IsNullOrEmpty(debugId))
                payload["debugId"] = debugId;
            return payload.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
