using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 常驻的本地 HTTP 服务，让编辑器外的脚本（PowerShell / AI 助手）能在 Unity 编辑器开着的情况下，
/// 直接把某个资源/场景对象拖拽赋值到某个组件的 Inspector 字段，无需人工在编辑器里点选拖拽。
///
/// 只监听 127.0.0.1，不对外网开放。HttpListener 在后台线程接收请求，
/// 真正的 Unity API 调用必须切回主线程（EditorApplication.update）执行，
/// 详见 <see cref="InspectorBridgeCommands"/>。
/// </summary>
[InitializeOnLoad]
public static class InspectorBridgeServer
{
    public static int Port => PrefabMcpSettings.GetOrCreate().Port;
    public static bool IsRunning => running;
    public static int ActivePort => activePort;

    static HttpListener listener;
    static Thread listenerThread;
    static volatile bool running;
    static int activePort;
    static int activeMaxRequestBytes;
    static int activeRequestTimeoutMilliseconds;
    static readonly ConcurrentQueue<Action> mainThreadActions = new();

    static InspectorBridgeServer()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.quitting += Stop;
        if (PrefabMcpSettings.GetOrCreate().AutoStartServer)
            Start();
    }

    [MenuItem(InspectorBridgeMenu.Root + "/启动")]
    public static void Start()
    {
        if (running)
            return;
        try
        {
            PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
            listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{settings.Port}/");
            listener.Start();
            activePort = settings.Port;
            activeMaxRequestBytes = settings.MaxRequestBytes;
            activeRequestTimeoutMilliseconds = settings.RequestTimeoutMilliseconds;
            running = true;
            listenerThread = new Thread(ListenLoop) { IsBackground = true, Name = "InspectorBridge" };
            listenerThread.Start();
            EditorApplication.update += Pump;
            // Debug.Log($"[InspectorBridge] 已启动，监听 127.0.0.1:{Port}");
        }
        catch (Exception e)
        {
            running = false;
            activePort = 0;
            try { listener?.Close(); } catch { /* 忽略启动失败后的清理异常 */ }
            listener = null;
            Debug.LogWarning($"[InspectorBridge] 启动失败（可能端口被占用，或已有一个 Unity 实例占用了该端口）: {e.Message}");
        }
    }

    [MenuItem(InspectorBridgeMenu.Root + "/停止")]
    public static void Stop()
    {
        if (!running)
            return;
        running = false;
        EditorApplication.update -= Pump;
        try { listener?.Stop(); } catch { /* 忽略关闭期间的异常 */ }
        try { listener?.Close(); } catch { /* 忽略关闭期间的异常 */ }
        listener = null;
        activePort = 0;
    }

    public static void Restart()
    {
        Stop();
        Start();
    }

    [MenuItem(InspectorBridgeMenu.Root + "/状态")]
    static void ShowStatus()
    {
        EditorUtility.DisplayDialog("Inspector Bridge",
            running ? $"运行中，端口 {activePort}" : "未运行", "确定");
    }

    static void ListenLoop()
    {
        while (running)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = listener.GetContext();
            }
            catch
            {
                break; // listener 已 Stop
            }
            HandleContext(ctx);
        }
    }

    static void HandleContext(HttpListenerContext ctx)
    {
        if (!string.Equals(ctx.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            WriteResponse(ctx, 405, JsonUtility.ToJson(new InspectorResult
            {
                ok = false,
                error = "Only POST is supported",
            }));
            return;
        }

        if (ctx.Request.ContentLength64 > activeMaxRequestBytes)
        {
            WriteResponse(ctx, 413, JsonUtility.ToJson(new InspectorResult
            {
                ok = false,
                error = "Request body is too large",
            }));
            return;
        }

        string body;
        using (var reader = new System.IO.StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding ?? Encoding.UTF8))
            body = reader.ReadToEnd();

        // 不在等待线程退出时 Dispose：如果编辑器恰好阻塞超过超时时间，队列动作稍后仍会 Set。
        // 旧实现的 using 会导致这种情况下在 Unity 主线程抛 ObjectDisposedException。
        var done = new ManualResetEventSlim(false);
        string responseJson = null;

        mainThreadActions.Enqueue(() =>
        {
            try
            {
                responseJson = InspectorBridgeCommands.Dispatch(body);
            }
            catch (Exception e)
            {
                responseJson = JsonUtility.ToJson(new InspectorResult { ok = false, error = e.ToString() });
            }
            finally
            {
                done.Set();
            }
        });

        // 主线程只在编辑器有前台活动（有 update 回调）时才会处理队列；
        // 若编辑器被长时间挂起（例如正在编译或阻塞对话框），最长等待 20 秒后返回超时错误，避免请求方无限挂起。
        bool signaled = done.Wait(activeRequestTimeoutMilliseconds);
        byte[] buf = Encoding.UTF8.GetBytes(signaled
            ? responseJson
            : JsonUtility.ToJson(new InspectorResult { ok = false, error = "timeout: 编辑器主线程 20 秒内未处理请求" }));

        WriteResponse(ctx, signaled ? 200 : 503, Encoding.UTF8.GetString(buf));
    }

    static void WriteResponse(HttpListenerContext ctx, int statusCode, string json)
    {
        byte[] buf = Encoding.UTF8.GetBytes(json ?? "{}");
        try
        {
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentEncoding = Encoding.UTF8;
            ctx.Response.ContentLength64 = buf.Length;
            ctx.Response.OutputStream.Write(buf, 0, buf.Length);
            ctx.Response.OutputStream.Close();
        }
        catch { /* 客户端可能已断开 */ }
    }

    static void Pump()
    {
        int budget = 20; // 每帧最多处理 20 条，避免单帧请求过多卡顿编辑器
        while (budget-- > 0 && mainThreadActions.TryDequeue(out var act))
            act();
    }
}
