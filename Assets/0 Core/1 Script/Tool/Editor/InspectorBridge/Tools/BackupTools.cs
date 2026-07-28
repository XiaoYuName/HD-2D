using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMcp
{
    /// <summary>
    /// restore_prefab_backup：列出 / 还原 edit_prefab 每次写入前留下的备份。
    /// 备份文件名形如 <c>yyyyMMdd_HHmmss_fff_{名字}_{guid8}.prefab</c>（见
    /// <see cref="PrefabMcpSettings.CreatePrefabBackup"/>），按 guid 归属到具体 Prefab，重名文件不会混。
    /// 还原前会先把当前文件再备份一次，所以「还原错了」同样可以还原回来。
    /// </summary>
    static class BackupTools
    {
        public static string Restore(BackupRequest command)
        {
            string error = PrefabAddress.ValidatePrefabPath(command.prefabPath);
            if (error != null)
                return BridgeJson.Fail(error);

            PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
            string projectPath = BridgeRouter.ProjectPath();
            string backupRoot = Path.GetFullPath(Path.Combine(projectPath, settings.BackupDirectory));
            List<BackupInfo> backups = Collect(backupRoot, projectPath, command.prefabPath);

            bool listOnly = command.listOnly ?? true;
            if (listOnly || string.IsNullOrWhiteSpace(command.backupPath))
            {
                int limit = PrefabMcpSettings.Limit(command.maxResults, 200);
                BackupInfo[] listed = backups.Take(limit).ToArray();
                if (!listOnly)
                    return BridgeJson.Serialize(new BackupsResponse
                    {
                        error = listed.Length == 0
                            ? $"{command.prefabPath} 没有备份（备份目录: {settings.BackupDirectory}）"
                            : "还原需要 backupPath；下面是可选的备份，最新的在最前面",
                        backups = listed.Length == 0 ? null : listed,
                    });
                return BridgeJson.Serialize(new BackupsResponse
                {
                    message = listed.Length == 0
                        ? $"{command.prefabPath} 没有备份（备份目录: {settings.BackupDirectory}）"
                        : $"{command.prefabPath} 有 {backups.Count} 份备份，返回最新的 {listed.Length} 份；" +
                          "还原请带 backupPath 且 listOnly=false",
                    backups = listed.Length == 0 ? null : listed,
                });
            }

            if (!settings.AllowPrefabWrites)
                return BridgeJson.Fail("写入已被项目配置禁止。请在 Project Settings > Unity Prefab MCP 中启用；" +
                    $"配置资产: {PrefabMcpSettings.AssetPath}");
            if (!settings.IsPrefabWritePathAllowed(command.prefabPath))
                return BridgeJson.Fail("目标不在允许写入的目录中: " + command.prefabPath);

            string requested = PrefabAddress.NormalizeSlashes(command.backupPath);
            BackupInfo chosen = backups.FirstOrDefault(item =>
                string.Equals(item.backupPath, requested, StringComparison.OrdinalIgnoreCase));
            if (chosen == null)
                return BridgeJson.Serialize(new BackupsResponse
                {
                    error = $"{requested} 不是 {command.prefabPath} 的备份（只能还原本 Prefab 自己的备份）",
                    backups = backups.Take(10).ToList().OrNull(),
                });

            string sourceFullPath = Path.GetFullPath(Path.Combine(projectPath, chosen.backupPath));
            string targetFullPath = Path.GetFullPath(Path.Combine(projectPath, command.prefabPath));
            // 还原本身也是一次覆盖写：先给当前内容留一份，免得还原错了没有退路。
            string safetyBackup = settings.CreatePrefabBackup(command.prefabPath);
            File.Copy(sourceFullPath, targetFullPath, true);
            AssetDatabase.ImportAsset(command.prefabPath, ImportAssetOptions.ForceUpdate);

            return BridgeJson.Serialize(new BackupsResponse
            {
                message = $"已用 {chosen.backupPath}（{chosen.savedAtUtc} UTC）还原 {command.prefabPath}" +
                    (safetyBackup == null ? "" : "；还原前的内容已另存为 backupPath"),
                restoredFrom = chosen.backupPath,
                backupPath = safetyBackup,
            });
        }

        /// <summary>按 guid 前 8 位匹配该 Prefab 的备份，最新的在前。</summary>
        static List<BackupInfo> Collect(string backupRoot, string projectPath, string prefabPath)
        {
            var backups = new List<BackupInfo>();
            if (!Directory.Exists(backupRoot))
                return backups;

            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            string guidPart = string.IsNullOrEmpty(guid) ? "noguid" : guid.Substring(0, Math.Min(8, guid.Length));
            string suffix = "_" + guidPart + ".prefab";

            foreach (string file in Directory.GetFiles(backupRoot, "*" + suffix))
            {
                var info = new FileInfo(file);
                backups.Add(new BackupInfo
                {
                    backupPath = PrefabAddress.NormalizeSlashes(
                        file.Substring(projectPath.Length).TrimStart('\\', '/')),
                    savedAtUtc = info.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    sizeBytes = info.Length,
                });
            }

            backups.Sort((a, b) => string.CompareOrdinal(b.backupPath, a.backupPath));
            return backups;
        }
    }
}
