using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace UnityMcp
{
    /// <summary>
    /// 常驻的本地 HTTP 服务：编辑器外的进程（MCP 服务 / AI 助手）通过它调用 Unity Editor API。
    /// 只监听 127.0.0.1。socket 在后台线程收请求，真正的 Editor API 调用必须切回主线程执行。
    ///
    /// 这里用裸 <see cref="Socket"/> 而不是 HttpListener，原因是踩过的一个坑：
    /// 监听 socket 的句柄默认可被子进程继承，而 Unity 会不断 spawn 子进程
    /// （AssetImportWorker、双击脚本时拉起的外部编辑器……）。这些子进程一旦继承了句柄，
    /// 就会在桥自己关闭后继续占着端口 —— 域重载后新的 Start() 拿到 AddressAlreadyInUse，
    /// 端口还在监听却没人处理请求，所有调用挂到超时。HttpListener 走 http.sys 且是独占绑定，
    /// 既没法把句柄设为不可继承，也没法用 SO_REUSEADDR 抢回来（实测 WSAEACCES）。
    /// 裸 socket 两件事都能做：SetHandleInformation 断掉继承（从根上不再泄漏），
    /// SO_REUSEADDR 允许抢占历史残留。协议侧只需要处理 POST + JSON，自己解析成本很低。
    /// </summary>
    [InitializeOnLoad]
    public static class BridgeServer
    {
        public static bool IsRunning => running;
        public static int ActivePort => activePort;

        /// <summary>上一次启动失败的原因；启动成功后为 null。</summary>
        public static string LastStartError => lastStartError;

        /// <summary>本次实例的随机标识，写进端口文件、也由 mcp.ping 回显，用来识别"是不是我在服务这个端口"。</summary>
        public static string InstanceToken => instanceToken;

        /// <summary>配置端口实在抢不回来时，往后顺延尝试的端口个数。</summary>
        const int PortScanRange = 15;
        const int StartRetryLimit = 10;
        const int HealthShiftLimit = 5;
        const int ProbeAttempts = 3;
        const int ProbeTimeoutMilliseconds = 10000;

        /// <summary>实际监听端口写在这里，供 MCP 客户端读取；Library 不入版本库，天然每机器一份。</summary>
        public const string ActivePortFileName = "Library/PrefabMcpPort.txt";

        const int HandleFlagInherit = 1;
        const int SocketIoTimeoutMilliseconds = 15000;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetHandleInformation(IntPtr hObject, int dwMask, int dwFlags);

        static Socket listenSocket;
        static Thread listenerThread;
        static volatile bool running;
        static int activePort;
        static int activeMaxRequestBytes;
        static int activeRequestTimeoutMilliseconds;
        static int startRetryCount;
        static string lastStartError;
        static string instanceToken;
        /// <summary>Pump 每次执行都自增：探测线程借它判断"主线程到底有没有在处理本实例的队列"。</summary>
        static volatile int pumpTicks;
        /// <summary>0=未定/编辑器忙，1=确认是自己在服务，2=端口被别的实例占着。</summary>
        static volatile int probeVerdict;
        static int portFloorOffset;
        static int healthShiftCount;
        static string activePortFilePath;
        static readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

        static BridgeServer()
        {
            // 只有真正的编辑器进程能开桥。资源导入 worker（AssetImportWorker）是带 -batchMode 的完整 Unity 进程，
            // 同样会加载编辑器程序集并执行 InitializeOnLoad —— 不拦住的话它也会起一份桥、和编辑器抢同一个端口，
            // 而 worker 里没有编辑器的 update 循环来处理主线程队列，被它 accept 到的请求只能挂到超时。
            // 这就是"端口在监听却没人处理请求"的真正来源（worker 还会被回收重建，所以占用者 pid 看起来飘忽不定）。
            if (Application.isBatchMode)
                return;

            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
            McpConfigInstaller.InstallOrUpdateOnStartup(settings.Port);
            if (settings.AutoStartServer)
                Start();
        }

        [MenuItem(BridgeMenu.Root + "/启动")]
        public static void Start()
        {
            if (running)
                return;

            PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
            activeMaxRequestBytes = settings.MaxRequestBytes;
            activeRequestTimeoutMilliseconds = settings.RequestTimeoutMilliseconds;

            string firstError = null;
            for (int offset = portFloorOffset; offset <= portFloorOffset + PortScanRange; offset++)
            {
                int port = settings.Port + offset;
                if (port > 65535)
                    break;
                if (TryBind(port, out string bindError))
                {
                    activePort = port;
                    running = true;
                    startRetryCount = 0;
                    lastStartError = null;
                    instanceToken = Guid.NewGuid().ToString("N").Substring(0, 8);
                    WriteActivePortFile(port);
                    listenerThread = new Thread(ListenLoop) { IsBackground = true, Name = "InspectorBridge" };
                    listenerThread.Start();
                    EditorApplication.update += Pump;
                    EditorApplication.update += HealthWatch;
                    QueueSelfProbe(port, instanceToken);
                    if (offset > 0)
                        Debug.LogWarning($"[InspectorBridge] 端口 {settings.Port} 被占用，已改用 {port}" +
                            $"（实际端口写入 {ActivePortFileName}，MCP 客户端会自动读取）");
                    return;
                }
                firstError ??= bindError;
            }

            running = false;
            activePort = 0;
            lastStartError = firstError;

            // 域重载瞬间旧 socket 可能还没释放完，退避重试；连整段端口都占满才报错。
            if (startRetryCount++ < StartRetryLimit)
                EditorApplication.delayCall += Start;
            else
                Debug.LogWarning($"[InspectorBridge] 启动失败：{settings.Port}~{settings.Port + PortScanRange} 全部无法绑定" +
                    $"（已重试 {StartRetryLimit} 次）: {firstError}");
        }

        /// <summary>绑定一个端口；成功返回 true。REUSEADDR 用来抢占历史残留的非独占 socket。</summary>
        static bool TryBind(int port, out string error)
        {
            error = null;
            Socket socket = null;
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                // 断掉句柄继承：之后 Unity spawn 的子进程不会再拿到这个 socket，端口不会被它们吊住。
                SetHandleInformation(socket.Handle, HandleFlagInherit, 0);
                socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                socket.Listen(32);
                listenSocket = socket;
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                try { socket?.Close(); } catch { /* 忽略绑定失败后的清理异常 */ }
                return false;
            }
        }

        [MenuItem(BridgeMenu.Root + "/停止")]
        public static void Stop()
        {
            // 不按 running 提前返回：启动失败留下的半初始化 socket 也要清掉。
            running = false;
            probeVerdict = 0;
            EditorApplication.update -= Pump;
            EditorApplication.update -= HealthWatch;
            EditorApplication.delayCall -= Start;
            try { listenSocket?.Close(); } catch { /* 忽略关闭期间的异常 */ }
            listenSocket = null;
            int stoppedPort = activePort;
            activePort = 0;
            // 确认监听线程真的退出，否则它会带着旧 socket 活下去。
            if (listenerThread != null && listenerThread.IsAlive)
            {
                listenerThread.Join(2000);
                if (listenerThread.IsAlive)
                {
                    // 它可能仍卡在 Accept 上、并仍持有那个端口：REUSEADDR 会让下一次 Start 绑上同一个端口，
                    // 于是两个实例在同一端口上抢连接，被旧实例接到的请求永远等不到处理。直接放弃这个端口。
                    portFloorOffset = Math.Max(portFloorOffset,
                        stoppedPort - PrefabMcpSettings.GetOrCreate().Port + 1);
                    Debug.LogWarning($"[InspectorBridge] 旧监听线程没能在 2 秒内退出，放弃端口 {stoppedPort} " +
                        "以免两个实例抢同一端口（下次启动会顺延端口，MCP 客户端自动读取实际端口）。");
                }
            }
            listenerThread = null;
        }

        public static void Restart()
        {
            // 手动重启视为"从配置端口重新试一遍"；置位要在 Stop 之前 ——
            // Stop 发现旧线程没退出时会再把下限抬起来，那个判断优先。
            portFloorOffset = 0;
            healthShiftCount = 0;
            Stop();
            startRetryCount = 0;
            Start();
        }

        [MenuItem(BridgeMenu.Root + "/状态")]
        static void ShowStatus()
        {
            EditorUtility.DisplayDialog("Inspector Bridge",
                running
                    ? $"运行中，端口 {activePort}"
                    : "未运行" + (lastStartError == null ? string.Empty : $"\n\n上次启动失败：{lastStartError}"),
                "确定");
        }

        /// <summary>端口文件内容是「端口 TAB 实例标识」；客户端只读第一段，标识供本类判断自己是否已被取代。</summary>
        static void WriteActivePortFile(int port)
        {
            try
            {
                activePortFilePath = Path.Combine(BridgeRouter.ProjectPath(),
                    ActivePortFileName.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(activePortFilePath));
                File.WriteAllText(activePortFilePath, port + "\t" + instanceToken, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InspectorBridge] 写入端口文件失败，客户端需要手动指定 -Port {port}: {e.Message}");
            }
        }

        /// <summary>
        /// 端口文件里的实例标识已经不是自己 —— 说明本实例是被新实例取代后残留的监听（域重载后最常见），
        /// 它 accept 到的请求会进一个没人 Pump 的队列，只能超时。纯文件 IO，可以在 socket 线程上调用。
        /// </summary>
        static bool IsSupersededInstance()
        {
            try
            {
                if (string.IsNullOrEmpty(instanceToken) || activePortFilePath == null)
                    return false;
                string[] parts = File.ReadAllText(activePortFilePath).Split('\t');
                return parts.Length > 1 && parts[1].Trim().Length > 0 && parts[1].Trim() != instanceToken;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 绑定成功不等于"这个端口是我在服务"：残留实例可能仍在同一端口上 accept，把请求抢走。
        /// 所以启动后自己发一次 mcp.ping —— 它必须穿过主线程队列才能回来，回来的标识还得是自己的。
        /// </summary>
        static void QueueSelfProbe(int port, string token)
        {
            probeVerdict = 0;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                for (int attempt = 0; attempt < ProbeAttempts; attempt++)
                {
                    Thread.Sleep(300);
                    int ticksBefore = pumpTicks;
                    string response = TryPostLocal(port, "{\"action\":\"mcp.ping\"}");
                    if (response != null && response.Contains(token))
                    {
                        probeVerdict = 1;
                        return;
                    }
                    // 拿到别人的回应，或者超时但期间本实例的 Pump 明明在跑：两种都说明端口不是我在服务。
                    if (response != null || ticksBefore != pumpTicks)
                    {
                        probeVerdict = 2;
                        return;
                    }
                    // 超时且 Pump 也没动：编辑器在忙（编译/导入），下一轮再试，不误判。
                }
            });
        }

        /// <summary>探测失败时在主线程上换端口重启。挂在 update 上，所以只有活着的实例会执行到。</summary>
        static void HealthWatch()
        {
            if (probeVerdict != 2)
                return;

            int contestedPort = activePort;
            int configuredPort = PrefabMcpSettings.GetOrCreate().Port;
            Stop();
            if (healthShiftCount++ >= HealthShiftLimit)
            {
                lastStartError = $"端口 {contestedPort} 被另一个监听实例抢着 accept，连续换 {HealthShiftLimit} 次端口仍未摆脱；请重启 Unity。";
                Debug.LogWarning("[InspectorBridge] " + lastStartError);
                return;
            }

            portFloorOffset = Math.Max(portFloorOffset, contestedPort - configuredPort + 1);
            Debug.LogWarning($"[InspectorBridge] 端口 {contestedPort} 上有另一个监听实例（多半是域重载后残留的）在抢连接，" +
                $"请求进不到本实例的队列；正在顺延端口重启（实际端口写入 {ActivePortFileName}，MCP 客户端会自动读取）。");
            Start();
        }

        /// <summary>给自检用的极简 HTTP 客户端；失败返回 null。</summary>
        static string TryPostLocal(int port, string json)
        {
            try
            {
                using (var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    client.ReceiveTimeout = ProbeTimeoutMilliseconds;
                    client.SendTimeout = ProbeTimeoutMilliseconds;
                    client.Connect(IPAddress.Loopback, port);
                    byte[] payload = Encoding.UTF8.GetBytes(json);
                    client.Send(Encoding.ASCII.GetBytes(
                        "POST / HTTP/1.1\r\nHost: 127.0.0.1\r\n" +
                        "Content-Type: application/json; charset=utf-8\r\n" +
                        $"Content-Length: {payload.Length}\r\nConnection: close\r\n\r\n"));
                    client.Send(payload);

                    var buffer = new MemoryStream();
                    var chunk = new byte[4096];
                    while (true)
                    {
                        int read = client.Receive(chunk);
                        if (read <= 0)
                            break;
                        buffer.Write(chunk, 0, read);
                    }
                    return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
                }
            }
            catch
            {
                return null;
            }
        }

        static void ListenLoop()
        {
            while (running)
            {
                Socket client;
                try
                {
                    client = listenSocket.Accept();
                }
                catch
                {
                    break; // socket 已关闭
                }
                // 交给线程池：单个请求最长阻塞 activeRequestTimeoutMilliseconds，
                // 在本线程同步处理会既不响应其他请求也不再 accept。
                ThreadPool.QueueUserWorkItem(state => HandleClient((Socket)state), client);
            }
        }

        static void HandleClient(Socket client)
        {
            try
            {
                client.ReceiveTimeout = SocketIoTimeoutMilliseconds;
                client.SendTimeout = SocketIoTimeoutMilliseconds;
                if (!TryReadRequest(client, out string method, out string body, out string readError))
                {
                    WriteResponse(client, BridgeJson.Fail(readError));
                    return;
                }
                if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    WriteResponse(client, BridgeJson.Fail("Only POST is supported"));
                    return;
                }

                WriteResponse(client, Dispatch(body));
            }
            catch { /* 客户端可能已断开 */ }
            finally
            {
                try { client.Close(); } catch { /* 忽略关闭异常 */ }
            }
        }

        /// <summary>把请求丢给主线程执行并等结果；超时返回错误 JSON。</summary>
        static string Dispatch(string body)
        {
            // 不在等待线程退出时 Dispose：如果编辑器恰好阻塞超过超时时间，队列动作稍后仍会 Set。
            var done = new ManualResetEventSlim(false);
            string responseJson = null;
            mainThreadActions.Enqueue(() =>
            {
                try
                {
                    responseJson = BridgeRouter.Dispatch(body);
                }
                catch (Exception e)
                {
                    responseJson = BridgeJson.Fail(e.ToString());
                }
                finally
                {
                    done.Set();
                }
            });

            // 主线程只在编辑器有前台活动（有 update 回调）时才会处理队列；
            // 若编辑器被长时间挂起（编译、导入、模态对话框），超时后返回错误，避免请求方无限挂起。
            if (done.Wait(activeRequestTimeoutMilliseconds))
                return responseJson;

            return BridgeJson.Fail(IsSupersededInstance()
                ? $"timeout: 这个端口上服务你的是**已被取代的残留监听实例**（本实例 {instanceToken} 已不是端口文件记录的当前实例），" +
                  "它 accept 到的请求进的是没人处理的队列。请在 Project Settings > Unity Prefab MCP 里点「重启服务」，或重启 Unity。"
                : $"timeout: 编辑器主线程 {activeRequestTimeoutMilliseconds / 1000} 秒内未处理请求" +
                  "（Unity 可能正在编译、导入资源，或处于后台未运行 update）");
        }

        /// <summary>读一个 HTTP/1.1 请求：请求行 + 头 + 按 Content-Length 读取的 body。</summary>
        static bool TryReadRequest(Socket client, out string method, out string body, out string error)
        {
            method = null;
            body = null;
            error = null;

            var buffer = new MemoryStream();
            var chunk = new byte[4096];
            int headerEnd = -1;
            while (headerEnd < 0)
            {
                int read = client.Receive(chunk);
                if (read <= 0)
                {
                    error = "连接在读取请求头前被关闭";
                    return false;
                }
                buffer.Write(chunk, 0, read);
                headerEnd = IndexOfHeaderEnd(buffer.GetBuffer(), (int)buffer.Length);
                if (headerEnd < 0 && buffer.Length > activeMaxRequestBytes)
                {
                    error = $"请求头过大（上限 {activeMaxRequestBytes} 字节）";
                    return false;
                }
            }

            byte[] raw = buffer.GetBuffer();
            int total = (int)buffer.Length;
            string header = Encoding.UTF8.GetString(raw, 0, headerEnd);
            string[] headerLines = header.Split('\n');
            string[] requestLine = headerLines[0].Trim().Split(' ');
            method = requestLine[0];

            int contentLength = 0;
            foreach (string line in headerLines)
            {
                int colon = line.IndexOf(':');
                if (colon < 0)
                    continue;
                if (!line.Substring(0, colon).Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                    continue;
                int.TryParse(line.Substring(colon + 1).Trim(), out contentLength);
                break;
            }
            if (contentLength > activeMaxRequestBytes)
            {
                error = $"请求体过大（上限 {activeMaxRequestBytes} 字节）";
                return false;
            }

            int bodyStart = headerEnd + 4;
            var bodyBytes = new byte[contentLength];
            int already = Math.Min(contentLength, Math.Max(0, total - bodyStart));
            Array.Copy(raw, bodyStart, bodyBytes, 0, already);
            int filled = already;
            while (filled < contentLength)
            {
                int read = client.Receive(bodyBytes, filled, contentLength - filled, SocketFlags.None);
                if (read <= 0)
                {
                    error = "连接在读完请求体前被关闭";
                    return false;
                }
                filled += read;
            }

            body = Encoding.UTF8.GetString(bodyBytes);
            return true;
        }

        static int IndexOfHeaderEnd(byte[] data, int length)
        {
            for (int i = 0; i + 3 < length; i++)
            {
                if (data[i] == (byte)'\r' && data[i + 1] == (byte)'\n' &&
                    data[i + 2] == (byte)'\r' && data[i + 3] == (byte)'\n')
                    return i;
            }
            return -1;
        }

        static void WriteResponse(Socket client, string json)
        {
            byte[] payload = Encoding.UTF8.GetBytes(json ?? "{}");
            byte[] head = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                $"Content-Length: {payload.Length}\r\n" +
                "Connection: close\r\n\r\n");
            try
            {
                client.Send(head);
                client.Send(payload);
            }
            catch { /* 客户端可能已断开 */ }
        }

        static void Pump()
        {
            pumpTicks++; // 自检线程靠它区分"编辑器在忙"和"端口不是我在服务"
            int budget = 20; // 每帧最多处理 20 条，避免单帧请求过多卡顿编辑器
            while (budget-- > 0 && mainThreadActions.TryDequeue(out Action act))
                act();
        }
    }
}
