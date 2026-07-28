using Newtonsoft.Json.Linq;

namespace UnityMcp
{
    // 请求 DTO 由 Json.NET 反序列化赋值，CS0649「字段从未被赋值」是误报。
#pragma warning disable CS0649

    class BridgeRequest
    {
        public string action;
        public string expectedProjectPath;
    }

    /// <summary>支持 targetMode 的请求基类：prefabAsset（默认，需要 prefabPath）/ prefabStage / openScene。</summary>
    class TargetRequest : BridgeRequest
    {
        public string targetMode;
        public string sceneRootName;
        public string prefabPath;
    }

    sealed class OpenStageRequest : BridgeRequest
    {
        public string prefabPath;
    }

    sealed class RefreshRequest : BridgeRequest
    {
        public bool refreshOnly;
    }

    sealed class CompileStatusRequest : BridgeRequest
    {
        public bool excludeMessages;
        public int maxResults;
    }

    sealed class ScreenshotRequest : BridgeRequest
    {
        public string captureTarget;
        public bool annotateUi;
        public bool elementsOnly;
        public int maxUiElements;
        public int x;
        public int y;
        public int widthPixels;
        public int heightPixels;
        public int maxWidth;
        public int maxHeight;
        public int jpegQuality;
    }

    sealed class PlayModeInputRequest : BridgeRequest
    {
        public string inputAction;
        public string targetPath;
        public float? x;
        public float? y;
        public float? fromX;
        public float? fromY;
        public string key;
        public string button;
        public float durationSeconds;
    }

    sealed class PlayModeRequest : BridgeRequest
    {
        public string playModeAction;
    }

    sealed class FindPrefabsRequest : BridgeRequest
    {
        public string query;
        public string[] searchFolders;
        public int maxResults;
    }

    sealed class PrefabTreeRequest : TargetRequest
    {
        public string rootObjectId;
        public int maxDepth;
        /// <summary>nullable：缺省即 true。MCP 客户端不会替模型补 schema 里的 default，
        /// 用 bool 会把「没传」变成 false，只能被迫写成 excludeComponents 这种反向语义。</summary>
        public bool? includeComponents;
        public bool? compact;
        public string nameFilter;
        public string componentTypeFilter;
        public int maxResults;
    }

    /// <summary>get_component_fields 的一个读取目标；componentIndex=-1 表示该节点上的所有组件。</summary>
    sealed class ComponentFieldsTarget
    {
        public string objectId;
        public int componentIndex;
        public string propertyPath;
    }

    sealed class ComponentFieldsRequest : TargetRequest
    {
        public string objectId;
        public int componentIndex;
        public string propertyPath;
        public ComponentFieldsTarget[] targets;
        public string fieldNameFilter;
        public bool onlyObjectReferences;
        public bool onlyUnassigned;
        public bool? compact;
        public int maxResults;
    }

    sealed class CandidatesRequest : TargetRequest
    {
        public string objectId;
        public int componentIndex;
        public string propertyPath;
        /// <summary>auto（默认，按字段类型选）/ prefab（当前目标层级内）/ asset（项目资产）。</summary>
        public string scope;
        public string query;
        public string[] searchFolders;
        public string candidateRootObjectId;
        public int maxResults;
    }

    sealed class ValidateRequest : TargetRequest
    {
        public int maxResults;
        public bool includeUnityComponents;
    }

    sealed class EditRequest : TargetRequest
    {
        public bool apply;
        public EditOp[] operations;
    }

    /// <summary>一条结构编辑操作。字段是所有 op 的并集，标量一律用字符串承载（协议只有 string 一种标量写法）。</summary>
    sealed class EditOp
    {
        public string op;
        public string objectId;
        public string parentObjectId;
        public string newName;
        public string active;        // "true" | "false"
        public string siblingIndex;  // 非空时解析为 int
        public string componentType;
        public int componentIndex;
        public string propertyPath;
        public string value;
        public string sourcePrefabPath;
        public string elementType;   // createUi
        public string label;         // createUi
        public string width;         // createUi
        public string height;        // createUi
    }

#pragma warning restore CS0649

    /// <summary>
    /// 所有响应的信封。没有 ok 字段：<see cref="error"/> 为空即成功，
    /// 空字段又不上线（见 <see cref="BridgeJson"/>），少一个恒定字段就少一份 token。
    /// </summary>
    class BridgeResponse
    {
        public string error;
        public string message;
    }

    sealed class ToolsResponse : BridgeResponse
    {
        public JArray tools;
    }

    /// <summary>mcp.ping：回显服务这个端口的桥实例标识，用于启动自检。</summary>
    sealed class PingResponse : BridgeResponse
    {
        public string token;
    }

    sealed class StatusResponse : BridgeResponse
    {
        public string unityVersion;
        public string projectPath;
        public bool? compiling;
        public StageInfo prefabStage;
    }

    /// <summary>当前 Prefab 编辑态（没有打开时整个对象为 null，因此不会出现在响应里）。</summary>
    sealed class StageInfo
    {
        public string prefabPath;
        public string rootName;
        public bool? dirty;
    }

    sealed class CompileStatusResponse : BridgeResponse
    {
        public CompileStatusInfo compileStatus;
    }

    sealed class CompileStatusInfo
    {
        public bool? isCompiling;
        public bool? isUpdating;
        public bool hasResult;
        public bool succeeded;
        public int errorCount;
        public int warningCount;
        public int domainReloadCount;
        public string finishedAtUtc;
        public string refreshRequestedAtUtc;
        public bool resultStale;
        public CompileMessage[] messages;
    }

    sealed class SettingsResponse : BridgeResponse
    {
        public SettingsInfo settings;
    }

    sealed class SettingsInfo
    {
        public bool autoStartServer;
        public int port;
        public int requestTimeoutMilliseconds;
        public int maxRequestBytes;
        public bool allowPrefabWrites;
        public bool createBackupBeforeWrite;
        public string backupDirectory;
        public string[] allowedPrefabWriteRoots;
        public int defaultQueryLimit;
        public int maximumQueryLimit;
        public string[] defaultAssetSearchFolders;
        public string assetPath;
        public float defaultUiWidth;
        public float defaultUiHeight;
        public string defaultTmpFont;
        public float defaultTmpFontSize;
        public string defaultTextColor;
        public string defaultImageColor;
    }

    /// <summary>find_prefabs 只回路径：名字能从路径看出来，guid 没有下游工具会用。</summary>
    sealed class PrefabListResponse : BridgeResponse
    {
        public string[] prefabs;
    }

    sealed class TreeResponse : BridgeResponse
    {
        public NodeInfo[] nodes;
    }

    sealed class NodeInfo
    {
        public string objectId;
        public string hierarchyPath; // compact 模式为 null：可由 name + 父链推出
        public string name;
        public int? depth;           // compact 模式为 null：objectId 的斜杠数就是深度
        public bool? activeSelf;     // 只在 false 时出现
        public ComponentInfo[] components;
    }

    sealed class ComponentInfo
    {
        public int componentIndex;
        public string type;      // compact 模式为 null，只留 shortType
        public string shortType;
        public bool? missing;
    }

    sealed class FieldsResponse : BridgeResponse
    {
        public FieldDto[] fields;                    // 单目标模式
        public ComponentFieldsInfo[] componentFields; // targets 批量模式
    }

    sealed class FieldDto
    {
        public string propertyPath;
        public string displayName;    // compact 模式为 null
        public string propertyType;
        public string serializedType; // compact 模式为 null
        public string fieldType;      // compact 模式只对对象引用字段保留
        public string value;
    }

    sealed class ComponentFieldsInfo
    {
        public string objectId;
        public int componentIndex;
        public string componentType;
        public string error;
        public FieldDto[] fields;
    }

    sealed class CandidatesResponse : BridgeResponse
    {
        public CandidateInfo[] candidates;
    }

    /// <summary>
    /// 候选项只回三样：<see cref="value"/> 是可直接塞给 edit_prefab setValue 的字符串，
    /// name/type 供人和 AI 判断选哪个。旧实现回 objectId + componentIndex + hierarchyPath，
    /// 调用方还得自己拼成引用写法。
    /// </summary>
    sealed class CandidateInfo
    {
        public string value;
        public string name;
        public string type;
    }

    sealed class IssuesResponse : BridgeResponse
    {
        public IssueInfo[] issues;
    }

    sealed class IssueInfo
    {
        public string kind; // missingScript | unassignedReference
        public string objectId;
        public string hierarchyPath;
        public int componentIndex;
        public string componentType;
        public string propertyPath;
    }

    sealed class EditResponse : BridgeResponse
    {
        public EditInfo edit;
    }

    sealed class EditInfo
    {
        public int operationCount;
        public int completed;
        public bool? applied;
        public string backupPath;
        public EditOpResult[] results;
    }

    /// <summary>逐条操作的结果；error 为空即成功（与信封同一套约定）。</summary>
    sealed class EditOpResult
    {
        public string op;
        public string error;
        public string objectId;
        public string hierarchyPath;
        public int? componentIndex;
        public string detail;
    }

    sealed class ScreenshotResponse : BridgeResponse
    {
        public string imageBase64;
        public string imageMimeType;
        public int imageWidth;
        public int imageHeight;
        public string coordinateSystem;
        public int? screenWidth;
        public int? screenHeight;
        public UiMarker[] uiElements;
    }

    sealed class PlayModeInputResponse : BridgeResponse
    {
        public string inputAction;
        public string targetPath;
        public string key;
        public float? x;
        public float? y;
    }

    sealed class PlayModeResponse : BridgeResponse
    {
        public string playModeAction;
        public bool? playing;
        public bool? paused;
    }

    sealed class UiMarker
    {
        public string label;
        public string path;
        public string type;
        public string interaction;
        public string color;
        public float centerX;
        public float centerY;
        public float minX;
        public float minY;
        public float maxX;
        public float maxY;
    }
}
