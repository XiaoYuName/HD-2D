using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace UnityMcp
{
    /// <summary>
    /// edit_prefab 的跨请求事务状态：内容版本、预演计划、幂等结果与跨进程写锁。
    /// 状态只写 Library，不污染项目资产；计划保存 1 小时，幂等结果保存 24 小时。
    /// </summary>
    static class PrefabEditTransactionStore
    {
        const int PlanLifetimeHours = 1;
        const int IdempotencyLifetimeHours = 24;
        const int RecordVersion = 1;

        static readonly string RootDirectory =
            Path.Combine(BridgeRouter.ProjectPath(), "Library", "PrefabMcpTransactions");
        static readonly string PlanDirectory = Path.Combine(RootDirectory, "Plans");
        static readonly string IdempotencyDirectory = Path.Combine(RootDirectory, "Idempotency");
        static bool cleaned;

        public sealed class TargetState
        {
            public string key;
            public string revision;
            public bool isAsset;
        }

        public sealed class Plan
        {
            public string id;
            public string revision;
            public EditRequest request;
        }

        public enum IdempotencyState
        {
            New,
            Replay,
            Conflict,
            Pending,
        }

        public sealed class IdempotencyResult
        {
            public IdempotencyState state;
            public string response;
            public string error;
            public string errorCode;
        }

        sealed class PlanRecord
        {
            public int version;
            public string id;
            public string revision;
            public string requestJson;
            public DateTime createdAtUtc;
        }

        sealed class IdempotencyRecord
        {
            public int version;
            public string requestHash;
            public string targetKey;
            public string revisionBefore;
            public string response;
            public string state;
            public DateTime createdAtUtc;
            public DateTime updatedAtUtc;
        }

        sealed class WriteLease : IDisposable
        {
            Mutex mutex;

            public WriteLease(Mutex mutex) => this.mutex = mutex;

            public void Dispose()
            {
                if (mutex == null)
                    return;
                mutex.ReleaseMutex();
                mutex.Dispose();
                mutex = null;
            }
        }

        public static bool TryGetTargetState(EditRequest request, out TargetState state, out string error)
        {
            state = null;
            error = null;
            string mode = string.IsNullOrEmpty(request.targetMode)
                ? EditTarget.SupportedModes[0]
                : request.targetMode.Trim();
            switch (mode)
            {
                case "prefabAsset":
                {
                    if (!TryGetAssetPath(request.prefabPath, out string assetPath, out string fullPath, out error))
                        return false;
                    UnityEditor.SceneManagement.PrefabStage openStage =
                        UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                    if (openStage != null && openStage.scene.isDirty &&
                        PrefabAddress.TryNormalizePrefabPath(
                            openStage.assetPath, out string stagePath, out _) &&
                        string.Equals(assetPath, stagePath, StringComparison.OrdinalIgnoreCase))
                    {
                        error = "目标 Prefab 当前已在 Prefab Stage 打开；请改用 targetMode=prefabStage，" +
                                "或关闭编辑态后再写 prefabAsset";
                        return false;
                    }
                    string guid = AssetDatabase.AssetPathToGUID(assetPath);
                    state = new TargetState
                    {
                        // 始终按路径锁：missing 保存后会获得 GUID，若切换锁键会留下创建期间的竞态窗口。
                        key = "asset:" + fullPath.ToLowerInvariant(),
                        revision = GetAssetRevision(fullPath, guid),
                        isAsset = true,
                    };
                    return true;
                }
                case "prefabStage":
                {
                    UnityEditor.SceneManagement.PrefabStage stage =
                        UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                    if (stage == null)
                    {
                        error = "当前没有打开 Prefab 编辑模式";
                        return false;
                    }
                    if (!TryGetAssetPath(stage.assetPath, out _, out string fullPath, out error))
                        return false;
                    state = new TargetState
                    {
                        key = "asset:" + fullPath.ToLowerInvariant(),
                    };
                    return true;
                }
                case "openScene":
                    if (string.IsNullOrWhiteSpace(request.sceneRootName))
                    {
                        error = "targetMode=openScene 需要 sceneRootName";
                        return false;
                    }
                    state = new TargetState
                    {
                        key = "scene:" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path +
                              "#" + request.sceneRootName,
                    };
                    return true;
                default:
                    error = $"未知 targetMode: {request.targetMode}";
                    return false;
            }
        }

        public static bool CanUseExpectedRevision(TargetState target, string expectedRevision, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(expectedRevision))
                return true;
            if (!target.isAsset)
            {
                error = "expectedRevision 仅支持 targetMode=prefabAsset";
                return false;
            }
            if (string.Equals(expectedRevision, target.revision, StringComparison.Ordinal))
                return true;
            error = $"Prefab 已变化：期望 {expectedRevision}，当前 {target.revision}";
            return false;
        }

        public static bool TryStartWrite(string targetKey, out IDisposable lease, out string error)
            => TryStartLease("UnityPrefabMcp_Target_", targetKey,
                "目标 Prefab 正被另一个 MCP 请求写入", out lease, out error);

        public static bool TryStartIdempotency(string key, out IDisposable lease, out string error)
        {
            if (string.IsNullOrEmpty(key))
            {
                lease = null;
                error = null;
                return true;
            }
            return TryStartLease("UnityPrefabMcp_Idempotency_", key,
                "同一 idempotencyKey 正被另一个 MCP 请求处理", out lease, out error);
        }

        static bool TryStartLease(
            string prefix, string value, string busyError, out IDisposable lease, out string error)
        {
            lease = null;
            error = null;
            var mutex = new Mutex(false, prefix + HashText(value, 16));
            try
            {
                bool acquired;
                try
                {
                    acquired = mutex.WaitOne(0);
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }
                if (!acquired)
                {
                    mutex.Dispose();
                    error = busyError;
                    return false;
                }
                lease = new WriteLease(mutex);
                return true;
            }
            catch
            {
                mutex.Dispose();
                throw;
            }
        }

        public static string CreatePlan(EditRequest request, string revision)
        {
            PrepareDirectories();
            string id = "p_" + Guid.NewGuid().ToString("N").Substring(0, 20);
            JObject snapshot = JObject.FromObject(request);
            snapshot.Remove("idempotencyKey");
            snapshot.Remove("planId");
            snapshot.Remove("responseMode");
            snapshot["dryRun"] = false;
            snapshot["apply"] = true;
            snapshot["expectedRevision"] = revision;
            var record = new PlanRecord
            {
                version = RecordVersion,
                id = id,
                revision = revision,
                requestJson = snapshot.ToString(Formatting.None),
                createdAtUtc = DateTime.UtcNow,
            };
            WriteRecord(Path.Combine(PlanDirectory, id + ".json"), record);
            return id;
        }

        public static bool TryGetPlan(string planId, out Plan plan, out string errorCode, out string error)
        {
            plan = null;
            errorCode = null;
            error = null;
            if (!IsSafeId(planId, "p_"))
            {
                errorCode = "PLAN_ID_INVALID";
                error = "planId 格式无效";
                return false;
            }
            PrepareDirectories();
            string path = Path.Combine(PlanDirectory, planId + ".json");
            PlanRecord record = ReadRecord<PlanRecord>(path);
            if (record == null || record.version != RecordVersion)
            {
                errorCode = "PLAN_NOT_FOUND";
                error = "找不到 planId，可能已过期或 Unity 的 Library 已被清理";
                return false;
            }
            if (DateTime.UtcNow - record.createdAtUtc > TimeSpan.FromHours(PlanLifetimeHours))
            {
                DeleteRecord(path);
                errorCode = "PLAN_EXPIRED";
                error = "planId 已过期，请重新 dryRun";
                return false;
            }
            plan = new Plan
            {
                id = record.id,
                revision = record.revision,
                request = JsonConvert.DeserializeObject<EditRequest>(record.requestJson),
            };
            return true;
        }

        public static string GetRequestHash(EditRequest request)
        {
            JObject payload = JObject.FromObject(request);
            payload.Remove("idempotencyKey");
            payload.Remove("responseMode");
            payload.Remove("expectedProjectPath");
            return HashText(payload.ToString(Formatting.None), 24);
        }

        public static IdempotencyResult GetIdempotency(
            string key, string requestHash, string curRevision)
        {
            if (string.IsNullOrEmpty(key))
                return new IdempotencyResult { state = IdempotencyState.New };
            if (key.Length > 200 || HasControlCharacter(key))
            {
                return new IdempotencyResult
                {
                    state = IdempotencyState.Conflict,
                    errorCode = "IDEMPOTENCY_KEY_INVALID",
                    error = "idempotencyKey 必须为 1-200 个非控制字符",
                };
            }
            PrepareDirectories();
            IdempotencyRecord record = ReadRecord<IdempotencyRecord>(GetIdempotencyPath(key));
            if (record == null)
                return new IdempotencyResult { state = IdempotencyState.New };
            if (record.version != RecordVersion)
            {
                return new IdempotencyResult
                {
                    state = IdempotencyState.Conflict,
                    errorCode = "IDEMPOTENCY_RECORD_VERSION",
                    error = "idempotencyKey 存在不兼容的旧记录；为避免重复写入，本次已拒绝",
                };
            }
            if (DateTime.UtcNow - record.updatedAtUtc > TimeSpan.FromHours(IdempotencyLifetimeHours))
            {
                DeleteRecord(GetIdempotencyPath(key));
                return new IdempotencyResult { state = IdempotencyState.New };
            }
            if (!string.Equals(record.requestHash, requestHash, StringComparison.Ordinal))
            {
                return new IdempotencyResult
                {
                    state = IdempotencyState.Conflict,
                    errorCode = "IDEMPOTENCY_KEY_CONFLICT",
                    error = "idempotencyKey 已用于不同请求",
                };
            }
            if (record.state == "complete")
            {
                return new IdempotencyResult
                {
                    state = IdempotencyState.Replay,
                    response = record.response,
                };
            }
            return new IdempotencyResult
            {
                state = IdempotencyState.Pending,
                errorCode = "IDEMPOTENCY_STATE_UNKNOWN",
                error = string.Equals(record.revisionBefore, curRevision, StringComparison.Ordinal)
                    ? "同一幂等请求仍在处理或上次未完成，请稍后重试"
                    : "上次请求可能已写入，但结果未能持久化；为避免重复修改，本次已拒绝",
            };
        }

        public static void StartIdempotency(
            string key, string requestHash, string targetKey, string revisionBefore)
        {
            if (string.IsNullOrEmpty(key))
                return;
            PrepareDirectories();
            DateTime now = DateTime.UtcNow;
            WriteRecord(GetIdempotencyPath(key), new IdempotencyRecord
            {
                version = RecordVersion,
                requestHash = requestHash,
                targetKey = targetKey,
                revisionBefore = revisionBefore,
                state = "pending",
                createdAtUtc = now,
                updatedAtUtc = now,
            });
        }

        public static void EndIdempotency(string key, string response)
        {
            if (string.IsNullOrEmpty(key))
                return;
            string path = GetIdempotencyPath(key);
            IdempotencyRecord record = ReadRecord<IdempotencyRecord>(path);
            if (record == null)
                return;
            record.response = response;
            record.state = "complete";
            record.updatedAtUtc = DateTime.UtcNow;
            WriteRecord(path, record);
        }

        static bool TryGetAssetPath(string path, out string assetPath, out string fullPath, out string error)
        {
            if (!PrefabAddress.TryNormalizePrefabPath(path, out assetPath, out error))
            {
                fullPath = null;
                return false;
            }
            fullPath = null;
            string projectPath = BridgeRouter.ProjectPath();
            fullPath = Path.GetFullPath(Path.Combine(projectPath, assetPath));
            string prefix = projectPath + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                error = "prefabPath 超出当前 Unity 项目";
                return false;
            }
            return true;
        }

        static string GetAssetRevision(string fullPath, string guid)
        {
            if (!File.Exists(fullPath))
                return "missing";
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] fileHash = sha.ComputeHash(stream);
                return HashText((guid ?? string.Empty) + ":" + ToHex(fileHash, fileHash.Length), 24);
            }
        }

        static string HashText(string value, int length)
        {
            using (SHA256 sha = SHA256.Create())
                return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)), length / 2);
        }

        static string ToHex(byte[] bytes, int count)
        {
            var builder = new StringBuilder(count * 2);
            for (int i = 0; i < count; i++)
                builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }

        static bool IsSafeId(string value, string prefix)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith(prefix, StringComparison.Ordinal) ||
                value.Length != prefix.Length + 20)
                return false;
            for (int i = prefix.Length; i < value.Length; i++)
            {
                char c = value[i];
                if ((c < '0' || c > '9') && (c < 'a' || c > 'f'))
                    return false;
            }
            return true;
        }

        static bool HasControlCharacter(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsControl(value[i]))
                    return true;
            }
            return false;
        }

        static string GetIdempotencyPath(string key) =>
            Path.Combine(IdempotencyDirectory, "i_" + HashText(key, 32) + ".json");

        static void PrepareDirectories()
        {
            Directory.CreateDirectory(PlanDirectory);
            Directory.CreateDirectory(IdempotencyDirectory);
            if (cleaned)
                return;
            cleaned = true;
            DeleteExpired(PlanDirectory, TimeSpan.FromHours(PlanLifetimeHours));
            DeleteExpired(IdempotencyDirectory, TimeSpan.FromHours(IdempotencyLifetimeHours));
        }

        static void DeleteExpired(string directory, TimeSpan lifetime)
        {
            foreach (string path in Directory.GetFiles(directory, "*.json"))
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > lifetime)
                    DeleteRecord(path);
            }
        }

        static T ReadRecord<T>(string path) where T : class
        {
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"Prefab MCP 忽略损坏的事务记录 {path}: {e.Message}");
                return null;
            }
        }

        static void WriteRecord(string path, object record)
        {
            string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(tempPath, JsonConvert.SerializeObject(record), new UTF8Encoding(false));
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tempPath, path, null);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(tempPath, path, true);
                    File.Delete(tempPath);
                    return;
                }
            }
            File.Move(tempPath, path);
        }

        static void DeleteRecord(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
                // 正在被另一个请求读取的过期记录留到下次清理。
            }
        }
    }
}
