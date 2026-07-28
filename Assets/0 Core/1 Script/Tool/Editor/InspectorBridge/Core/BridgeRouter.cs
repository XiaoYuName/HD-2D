using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityMcp
{
    /// <summary>
    /// 请求路由：一张「MCP 工具名 → 处理函数」表。action 直接就是工具名，
    /// 不再维护 MCP 名到内部 action 的映射（那是第三份需要手工同步的契约）。
    /// 工具的 schema 由 <see cref="ToolCatalog"/> 提供，和这里的表项一一对应。
    /// </summary>
    static class BridgeRouter
    {
        delegate string Handler(JObject payload);

        static Handler For<T>(Func<T, string> handler) => payload => handler(BridgeJson.ToRequest<T>(payload));

        static readonly Dictionary<string, Handler> Handlers = new Dictionary<string, Handler>(StringComparer.Ordinal)
        {
            // mcp.* 是内部管理接口，不出现在 MCP 工具表里。
            // 工具表由服务端提供，schema 与实现同居一处；ping 用于桥启动后的端口自检
            // （必须穿过主线程队列才能回来，回显的实例标识证明"这个端口是我在服务"）。
            ["mcp.tools"] = _ => ToolCatalog.Respond(),
            ["mcp.ping"] = _ => BridgeJson.Serialize(new PingResponse { token = BridgeServer.InstanceToken }),

            ["unity_prefab_status"] = For<BridgeRequest>(_ => StatusTools.Status()),
            ["open_prefab_stage"] = For<OpenStageRequest>(StatusTools.OpenStage),
            ["refresh_unity_assets"] = For<RefreshRequest>(StatusTools.Refresh),
            ["get_unity_compile_status"] = For<CompileStatusRequest>(StatusTools.CompileStatus),
            ["get_prefab_mcp_settings"] = For<BridgeRequest>(_ => StatusTools.Settings()),
            ["capture_unity_screenshot"] = For<ScreenshotRequest>(ScreenshotTool.Capture),
            ["control_unity_play_mode"] = For<PlayModeRequest>(PlayModeInputTool.ControlPlayMode),
            ["simulate_playmode_input"] = For<PlayModeInputRequest>(PlayModeInputTool.Simulate),

            ["find_prefabs"] = For<FindPrefabsRequest>(QueryTools.FindPrefabs),
            ["get_prefab_tree"] = For<PrefabTreeRequest>(QueryTools.GetTree),
            ["get_component_fields"] = For<ComponentFieldsRequest>(QueryTools.GetComponentFields),
            ["find_binding_candidates"] = For<CandidatesRequest>(QueryTools.FindCandidates),
            ["validate_prefab"] = For<ValidateRequest>(QueryTools.Validate),

            ["edit_prefab"] = For<EditRequest>(EditTools.Edit),
        };

        public static IEnumerable<string> ToolActions
        {
            get
            {
                foreach (string key in Handlers.Keys)
                {
                    if (!key.StartsWith("mcp.", StringComparison.Ordinal))
                        yield return key;
                }
            }
        }

        public static string Dispatch(string requestJson)
        {
            JObject payload;
            try
            {
                payload = JObject.Parse(requestJson ?? string.Empty);
            }
            catch (Exception e)
            {
                return BridgeJson.Fail("请求 JSON 解析失败: " + e.Message);
            }

            string projectError = ValidateProject((string)payload["expectedProjectPath"]);
            if (projectError != null)
                return BridgeJson.Fail(projectError);

            string action = (string)payload["action"];
            if (string.IsNullOrEmpty(action) || !Handlers.TryGetValue(action, out Handler handler))
                return BridgeJson.Fail($"未知 action: {action}。支持: {string.Join(", ", Handlers.Keys)}");

            try
            {
                return handler(payload);
            }
            catch (JsonSerializationException e)
            {
                // MissingMemberHandling.Error 的报错走这里：多半是参数名拼错或类型不符。
                return BridgeJson.Fail($"{action} 的参数不合法（字段名拼错或类型不符）: {e.Message}");
            }
            catch (Exception e)
            {
                return BridgeJson.Fail(e.ToString());
            }
        }

        /// <summary>端口可能被另一个 Unity 项目占用，写操作前先确认连的是同一个项目。</summary>
        static string ValidateProject(string expectedProjectPath)
        {
            if (string.IsNullOrEmpty(expectedProjectPath))
                return null;
            string expected = Path.GetFullPath(expectedProjectPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(expected, ProjectPath(), StringComparison.OrdinalIgnoreCase)
                ? null
                : $"连接到了错误的 Unity 项目。期望: {expected}，实际: {ProjectPath()}";
        }

        public static string ProjectPath() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
