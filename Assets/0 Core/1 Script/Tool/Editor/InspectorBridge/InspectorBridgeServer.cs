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
    public const int Port = 58732;

    static HttpListener listener;
    static Thread listenerThread;
    static volatile bool running;
    static readonly ConcurrentQueue<Action> mainThreadActions = new();

    static InspectorBridgeServer()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.quitting += Stop;
        Start();
    }

    [MenuItem(EditorMenuSet.InspectorBridge + "/启动")]
    static void Start()
    {
        if (running)
            return;
        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            listener.Start();
            running = true;
            listenerThread = new Thread(ListenLoop) { IsBackground = true, Name = "InspectorBridge" };
            listenerThread.Start();
            EditorApplication.update += Pump;
            // Debug.Log($"[InspectorBridge] 已启动，监听 127.0.0.1:{Port}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[InspectorBridge] 启动失败（可能端口被占用，或已有一个 Unity 实例占用了该端口）: {e.Message}");
        }
    }

    [MenuItem(EditorMenuSet.InspectorBridge + "/停止")]
    static void Stop()
    {
        if (!running)
            return;
        running = false;
        EditorApplication.update -= Pump;
        try { listener?.Stop(); } catch { /* 忽略关闭期间的异常 */ }
        try { listener?.Close(); } catch { /* 忽略关闭期间的异常 */ }
        listener = null;
    }

    [MenuItem(EditorMenuSet.InspectorBridge + "/状态")]
    static void ShowStatus()
    {
        EditorUtility.DisplayDialog("Inspector Bridge",
            running ? $"运行中，端口 {Port}" : "未运行", "确定");
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
        string body;
        using (var reader = new System.IO.StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding ?? Encoding.UTF8))
            body = reader.ReadToEnd();

        using var done = new ManualResetEventSlim(false);
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
        bool signaled = done.Wait(20000);
        byte[] buf = Encoding.UTF8.GetBytes(signaled
            ? responseJson
            : JsonUtility.ToJson(new InspectorResult { ok = false, error = "timeout: 编辑器主线程 20 秒内未处理请求" }));

        try
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
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
