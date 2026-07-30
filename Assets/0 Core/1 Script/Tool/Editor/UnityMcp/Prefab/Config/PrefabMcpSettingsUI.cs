using System;
using UnityEditor;
using UnityEngine;

namespace UnityMcp
{
    /// <summary>
    /// 配置界面：Project Settings 页、SO 的 Inspector，以及服务/客户端配置按钮。
    /// 两个入口共用同一套控件，逻辑只写一份。
    /// </summary>
    static class PrefabMcpSettingsUI
    {
        static readonly string[] PropertyNames =
        {
            "mcpScriptAsset", null,
            "autoStartServer", "port", "requestTimeoutSeconds", "maxRequestBytes", null,
            "allowPrefabWrites", "createBackupBeforeWrite", "backupDirectory", "allowedPrefabWriteRoots", null,
            "defaultQueryLimit", "maximumQueryLimit", "defaultAssetSearchFolders", null,
            "defaultUiSize", "defaultTmpFont", "defaultTmpFontSize", "defaultTextColor", "defaultImageColor",
        };

        [SettingsProvider]
        static SettingsProvider CreateSettingsProvider()
        {
            SerializedObject serializedSettings = null;
            return new SettingsProvider(PrefabMcpSettings.ProjectSettingsPath, SettingsScope.Project)
            {
                label = "Unity Prefab MCP",
                keywords = new[] { "Unity", "Prefab", "MCP", "AI", "UnityMcp" },
                guiHandler = _ =>
                {
                    PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
                    if (serializedSettings == null || serializedSettings.targetObject != settings)
                        serializedSettings = new SerializedObject(settings);

                    serializedSettings.Update();
                    EditorGUILayout.HelpBox(
                        "端口或自动启动设置变更后，请重启 Unity MCP。写入受下面的总开关和目录白名单限制。",
                        MessageType.Info);
                    foreach (string propertyName in PropertyNames)
                    {
                        if (propertyName == null)
                            EditorGUILayout.Space();
                        else
                            EditorGUILayout.PropertyField(serializedSettings.FindProperty(propertyName), true);
                    }
                    serializedSettings.ApplyModifiedProperties();

                    EditorGUILayout.Space();
                    DrawServerControls();
                    DrawClientConfigControls(settings);
                    if (GUILayout.Button("在 Project 中选中配置资产"))
                        Selection.activeObject = settings;
                },
            };
        }

        [MenuItem(BridgeMenu.Root + "/设置")]
        static void OpenSettings() => SettingsService.OpenProjectSettings(PrefabMcpSettings.ProjectSettingsPath);

        public static void DrawServerControls()
        {
            bool running = BridgeServer.IsRunning;
            string status = running
                ? $"服务运行中 · 127.0.0.1:{BridgeServer.ActivePort}"
                : BridgeServer.LastStartError == null
                    ? "服务已停止"
                    : $"服务启动失败：{BridgeServer.LastStartError}\n" +
                      "端口被另一个 Unity 实例占用时，改端口或关掉那个实例；" +
                      "若是本实例重载后残留的监听线程，重启 Unity 可清除。";
            EditorGUILayout.HelpBox(status, running ? MessageType.Info : MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(running))
            {
                if (GUILayout.Button("启动服务"))
                    BridgeServer.Start();
            }
            using (new EditorGUI.DisabledScope(!running))
            {
                if (GUILayout.Button("停止服务"))
                    BridgeServer.Stop();
                if (GUILayout.Button("重启服务"))
                    BridgeServer.Restart();
            }
            EditorGUILayout.EndHorizontal();
        }

        public static void DrawClientConfigControls(PrefabMcpSettings settings)
        {
            EditorGUILayout.Space();
            DrawPowerShell7Controls();

            bool codexConfigured = McpConfigInstaller.IsCodexConfigured();
            bool claudeConfigured = McpConfigInstaller.IsClaudeConfigured();
            EditorGUILayout.HelpBox(
                $"Codex（.codex/config.toml）：{(codexConfigured ? "已配置" : "未配置")}\n" +
                $"Claude Code（.mcp.json）：{(claudeConfigured ? "已配置" : "未配置")}",
                codexConfigured && claudeConfigured ? MessageType.Info : MessageType.Warning);

            if (!GUILayout.Button("初始化/更新 MCP 配置（Codex + Claude Code）"))
                return;

            try
            {
                McpConfigInstaller.InstallOrUpdateAll(settings.Port);
                EditorUtility.DisplayDialog("Unity MCP",
                    "已写入 .codex/config.toml 和 .mcp.json。\n\n请重启 Codex / Claude Code 会话以加载或刷新 MCP 工具。",
                    "确定");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Unity MCP", $"MCP 配置失败：\n{e.Message}", "确定");
            }
        }

        static void DrawPowerShell7Controls()
        {
            string pwshPath = PowerShell7Locator.Find();
            if (pwshPath != null)
            {
                EditorGUILayout.HelpBox($"PowerShell 7：已安装（{pwshPath}）", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "PowerShell 7 未安装。MCP 配置用 pwsh 启动脚本，缺了它客户端起不来服务。\n" +
                "Windows 自带的 powershell.exe 是 5.1，读不带 BOM 的文件会按 GBK 解码，中文注释会吃掉下一行代码。",
                MessageType.Error);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("用 winget 安装 PowerShell 7"))
                PowerShell7Locator.InstallViaWinget();
            if (GUILayout.Button("打开下载页"))
                Application.OpenURL(PowerShell7Locator.DownloadUrl);
            EditorGUILayout.EndHorizontal();
        }
    }

    [CustomEditor(typeof(PrefabMcpSettings))]
    sealed class PrefabMcpSettingsInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            PrefabMcpSettingsUI.DrawServerControls();
            PrefabMcpSettingsUI.DrawClientConfigControls((PrefabMcpSettings)target);
            EditorGUILayout.HelpBox("端口变更后请重启服务；MCP 客户端会自动读取实际端口，无需改配置。", MessageType.None);
        }
    }
}
