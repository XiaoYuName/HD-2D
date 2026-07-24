using System;

/// <summary>
/// HTTP 桥的统一结果信封（成功/失败），用 Newtonsoft.Json 序列化。
/// <see cref="InspectorBridgeServer"/> 在解析失败/超时等场景直接构造它返回。
/// </summary>
[Serializable]
public class InspectorResult
{
    public bool ok;
    public string error;
    public string info;
}

/// <summary>
/// HTTP 桥的命令入口。所有命令都转发给面向 MCP 的 <see cref="BridgeCommands"/> 处理
/// （action 形如 prefab.* / unity.*）。旧的 setField/getField/listFields 直连协议已废弃移除。
/// </summary>
public static class InspectorBridgeCommands
{
    public static string Dispatch(string requestJson) => BridgeCommands.Dispatch(requestJson);
}
