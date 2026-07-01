#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using Unity.InternalBridge;
using UnityEditor;
using UnityTcp.Editor.Helpers;
using UnityEngine;

namespace Cn.Tuanjie.Codely.Editor
{
    /// <summary>
    /// Analytics event tracker. Sends events from Unity to the Tauri backend via IPC,
    /// which forwards them to the remote server. Tauri already holds the Unity user
    /// credentials (access token + user ID) and is the natural place to handle
    /// remote data submission.
    /// </summary>
    public static class CodelyEventTracker
    {
        public const string WelcomeWindowShown   = "welcome_window_shown";
        public const string WelcomeWindowClosed  = "welcome_window_closed";
        public const string WelcomeButtonClick   = "welcome_button_clicked";
        public const string CodelyPanelOpened    = "codely_panel_opened";
        public const string CodelyPanelClosed    = "codely_panel_closed";

        private static readonly HashSet<string> s_eventsRequiringUserId =
            new HashSet<string>
            {
                WelcomeWindowShown,
                WelcomeWindowClosed,
                WelcomeButtonClick,
                CodelyPanelOpened,
                CodelyPanelClosed,
            };

        private static readonly HttpClient s_httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        private static bool? s_isTuanjie;

        public static void Track(string eventType, string data = null)
        {
            Debug.Log($"[CodelyEventTracker] Track called: {eventType}, s_isTuanjie={s_isTuanjie}");

            if (!s_isTuanjie.HasValue)
            {
                string editorType = TauriUtils.GetEditorType();
                s_isTuanjie = editorType == "tuanjie";
                Debug.Log($"[CodelyEventTracker] GetEditorType() returned '{editorType}', s_isTuanjie={s_isTuanjie}");
            }
            if (!s_isTuanjie.Value)
                return;

            try
            {
                string userId = s_eventsRequiringUserId.Contains(eventType)
                    ? UnityConnectSession.GetUserId()
                    : null;
                string json = BuildPayload(eventType, data, userId);
                bool sent = CodelyIpcManager.TrySend(IpcMessageType.TrackEvent, json);
                if (sent)
                {
                    CodelyLogger.Log($"Codely EventTracker: {eventType}");
                }
                else
                {
                    CodelyLogger.LogWarning(
                        $"Codely EventTracker: {eventType} buffered or dropped (IPC not connected)");
                }
            }
            catch (Exception ex)
            {
                CodelyLogger.LogWarning($"Codely EventTracker error: {ex.Message}");
            }
        }

        /// <summary>
        /// Track a lifecycle close event via IPC, with HTTP invoke fallback when IPC is
        /// already disconnected (e.g. OnDisable runs ReleaseIpcClient before OnDestroy).
        /// </summary>
        public static void TryTrackClose(
            string eventType,
            string httpMessageType,
            int tauriServerPort,
            ref bool alreadySent)
        {
            if (alreadySent)
            {
                return;
            }

            Track(eventType);
            if (CodelyIpcManager.IsConnected)
            {
                alreadySent = true;
                return;
            }

            if (SendHttpInvoke(httpMessageType, tauriServerPort, synchronous: true))
            {
                alreadySent = true;
            }
        }

        private static bool SendHttpInvoke(
            string messageType,
            int tauriServerPort,
            bool synchronous = false)
        {
            int port = ResolveTauriPort(tauriServerPort);
            if (port <= 0)
            {
                return false;
            }

            try
            {
                string workspaceDir = TauriUtils.GetWorkspaceDirectory();
                string editorType   = TauriUtils.GetEditorType();
                string messageId    = Guid.NewGuid().ToString();
                string payload      = BuildInvokePayload(
                    messageType,
                    messageId,
                    workspaceDir,
                    editorType);
                string url = $"http://127.0.0.1:{port}/api/tauri/invoke";
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                AddInvokeRoutingHeaders(request, workspaceDir, editorType);

                if (synchronous)
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                    {
                        using (var response = s_httpClient.SendAsync(request, cts.Token).GetAwaiter().GetResult())
                        {
                            return response.IsSuccessStatusCode;
                        }
                    }
                }

                _ = s_httpClient.SendAsync(request);
                return true;
            }
            catch (Exception ex)
            {
                CodelyLogger.LogWarning($"Codely EventTracker: {messageType} invoke failed: {ex.Message}");
                return false;
            }
        }

        private static int ResolveTauriPort(int tauriServerPort)
        {
            if (tauriServerPort > 0)
            {
                return tauriServerPort;
            }

            return SessionState.GetInt(TauriUtils.SESSION_STATE_TAURI_PORT_KEY, -1);
        }

        private static void AddInvokeRoutingHeaders(
            HttpRequestMessage request,
            string workspaceDir,
            string editorType)
        {
            if (!string.IsNullOrEmpty(workspaceDir))
            {
                request.Headers.TryAddWithoutValidation(
                    "X-Workspace-Dir",
                    Uri.EscapeDataString(workspaceDir));
            }

            if (!string.IsNullOrEmpty(editorType))
            {
                request.Headers.TryAddWithoutValidation("X-Editor-Type", editorType);
            }
        }

        private static string BuildInvokePayload(
            string messageType,
            string messageId,
            string workspaceDir,
            string editorType)
        {
            string typeEscaped   = EscapeJson(messageType);
            string idEscaped     = EscapeJson(messageId);
            string dirEscaped    = EscapeJson(workspaceDir ?? string.Empty);
            string editorEscaped = EscapeJson(editorType ?? "unity");
            return $"{{\"message\":{{\"messageType\":\"{typeEscaped}\",\"messageId\":\"{idEscaped}\",\"data\":null}},\"workspaceDir\":\"{dirEscaped}\",\"editorType\":\"{editorEscaped}\"}}";
        }

        private static string BuildPayload(string eventType, string data, string userId = null)
        {
            var sb = new StringBuilder();
            sb.Append("{\"event_type\":\"").Append(EscapeJson(eventType)).Append("\"");
            sb.Append(",\"timestamp\":\"").Append(DateTimeOffset.UtcNow.ToString("o")).Append("\"");
            if (!string.IsNullOrEmpty(userId))
            {
                sb.Append(",\"user_id\":\"").Append(EscapeJson(userId)).Append("\"");
            }
            if (!string.IsNullOrEmpty(data))
            {
                sb.Append(",\"data\":\"").Append(EscapeJson(data)).Append("\"");
            }
            sb.Append("}");
            return sb.ToString();
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
#endif
