using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityMcp
{
    /// <summary>状态/编译/配置/编辑态这类不涉及 Prefab 内容的工具。</summary>
    static class StatusTools
    {
        public static string Status()
        {
            return BridgeJson.Serialize(new StatusResponse
            {
                message = "Unity MCP bridge is ready",
                unityVersion = Application.unityVersion,
                projectPath = BridgeRouter.ProjectPath(),
                compiling = EditorApplication.isCompiling.OrNull(),
                prefabStage = GetStageInfo(),
            });
        }

        public static StageInfo GetStageInfo()
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage == null
                ? null
                : new StageInfo
                {
                    prefabPath = stage.assetPath,
                    rootName = stage.prefabContentsRoot.name,
                    dirty = stage.scene.isDirty.OrNull(),
                };
        }

        /// <summary>
        /// 打开 Prefab 编辑态，等价于在 Project 窗口里双击这个 Prefab。
        /// 打开后即可用 targetMode=prefabStage 直接改实时对象，并配合截图看效果。
        /// </summary>
        public static string OpenStage(OpenStageRequest command)
        {
            string error = PrefabAddress.ValidatePrefabPath(command.prefabPath);
            if (error != null)
                return BridgeJson.Fail(error);

            StageInfo current = GetStageInfo();
            string requested = PrefabAddress.NormalizeSlashes(command.prefabPath);
            if (current != null && string.Equals(PrefabAddress.NormalizeSlashes(current.prefabPath), requested,
                    StringComparison.OrdinalIgnoreCase))
                return BridgeJson.Serialize(new StatusResponse
                {
                    message = "该 Prefab 已处于编辑态，无需重新打开",
                    prefabStage = current,
                });

            // 带未保存改动时切换 Prefab 会弹模态保存对话框，那会阻塞主线程直到用户点按钮，
            // 桥的这次请求必然超时。所以这里直接拒绝，把决定权交回用户。
            if (current != null && current.dirty == true)
                return BridgeJson.Fail($"当前 Prefab 编辑态 {current.prefabPath} 有未保存的改动。" +
                    "请先在编辑器里 Ctrl+S 保存或退出编辑态，再打开新的 Prefab（否则 Unity 会弹模态对话框卡住桥）");

            if (PrefabStageUtility.OpenPrefab(command.prefabPath) == null)
                return BridgeJson.Fail("打开 Prefab 编辑模式失败: " + command.prefabPath);

            return BridgeJson.Serialize(new StatusResponse
            {
                message = "已进入 Prefab 编辑态，可用 targetMode=prefabStage 直接编辑实时对象",
                prefabStage = GetStageInfo(),
            });
        }

        public static string Refresh(RefreshRequest command)
        {
            CompileTracker.RequestRefresh(!command.refreshOnly);
            return BridgeJson.Serialize(new BridgeResponse
            {
                message = command.refreshOnly
                    ? "已安排 Unity 刷新 AssetDatabase"
                    : "已安排 Unity 刷新 AssetDatabase 并请求脚本编译",
            });
        }

        public static string CompileStatus(CompileStatusRequest command)
        {
            int limit = UnityMcpSettings.Limit(command.maxResults, 200);
            CompileSnapshot snapshot = CompileTracker.GetSnapshot(command.excludeMessages ? 0 : limit);
            // 结果不新鲜就挂重试提示，交给 MCP 服务层在 waitSeconds 内续等（域重载会卸掉桥的线程，服务端等不了）。
            bool pending = snapshot.isCompiling || snapshot.resultStale;
            return BridgeJson.Serialize(new CompileStatusResponse
            {
                retryAfterSeconds = pending && command.waitSeconds > 0 ? 1d : (double?)null,
                message = snapshot.isCompiling
                    ? "Unity 正在编译脚本"
                    : snapshot.resultStale
                        ? "编译结果早于你请求的刷新，Unity 还没有重新编译（在后台时会推迟到重新获得焦点或下一次编辑器循环）；" +
                          "用 waitSeconds（如 180）让本工具等到结果新鲜再返回，不要靠自己反复轮询"
                        : snapshot.hasResult
                            ? snapshot.succeeded ? "最近一次脚本编译成功" : "最近一次脚本编译失败"
                            : "当前会话尚无可用的脚本编译结果",
                compileStatus = new CompileStatusInfo
                {
                    isCompiling = snapshot.isCompiling.OrNull(),
                    isUpdating = EditorApplication.isUpdating.OrNull(),
                    hasResult = snapshot.hasResult,
                    succeeded = snapshot.succeeded,
                    errorCount = snapshot.errorCount,
                    warningCount = snapshot.warningCount,
                    domainReloadCount = CompileTracker.DomainReloadCount,
                    finishedAtUtc = snapshot.finishedAtUtc,
                    refreshRequestedAtUtc = snapshot.refreshRequestedAtUtc,
                    resultStale = snapshot.resultStale,
                    messages = snapshot.messages,
                },
            });
        }

        public static string Settings()
        {
            UnityMcpSettings settings = UnityMcpSettings.GetOrCreate();
            return BridgeJson.Serialize(new SettingsResponse
            {
                message = "Prefab MCP 项目配置",
                settings = new SettingsInfo
                {
                    autoStartServer = settings.AutoStartServer,
                    port = settings.Port,
                    requestTimeoutMilliseconds = settings.RequestTimeoutMilliseconds,
                    maxRequestBytes = settings.MaxRequestBytes,
                    allowPrefabWrites = settings.AllowPrefabWrites,
                    createBackupBeforeWrite = settings.CreateBackupBeforeWrite,
                    backupDirectory = settings.BackupDirectory,
                    allowedPrefabWriteRoots = settings.AllowedPrefabWriteRoots,
                    defaultQueryLimit = settings.DefaultQueryLimit,
                    maximumQueryLimit = settings.MaximumQueryLimit,
                    defaultAssetSearchFolders = settings.DefaultAssetSearchFolders,
                    assetPath = UnityMcpSettings.AssetPath,
                    defaultUiWidth = settings.DefaultUiSize.x,
                    defaultUiHeight = settings.DefaultUiSize.y,
                    defaultTmpFont = settings.DefaultTmpFont == null
                        ? null
                        : AssetDatabase.GetAssetPath(settings.DefaultTmpFont),
                    defaultTmpFontSize = settings.DefaultTmpFontSize,
                    defaultTextColor = ColorUtility.ToHtmlStringRGBA(settings.DefaultTextColor),
                    defaultImageColor = ColorUtility.ToHtmlStringRGBA(settings.DefaultImageColor),
                },
            });
        }
    }
}
