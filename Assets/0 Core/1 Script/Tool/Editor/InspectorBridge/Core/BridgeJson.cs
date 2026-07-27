using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityMcp
{
    /// <summary>
    /// 桥的 JSON 出入口，也是全链路**唯一**的 token 裁剪层。
    ///
    /// 响应侧只有一条规则：null 字段不上线。服务端把某个字段置 null 就等于不发它，
    /// MCP 侧因此不需要再按工具挑字段（旧实现在 ps1 里维护了一张 per-tool 白名单，
    /// 服务端新加字段忘了同步就会被静默丢掉）。数值字段一律照发：componentIndex=0
    /// 这种"默认值恰好有意义"的字段不能靠 DefaultValueHandling 猜。
    ///
    /// 请求侧 MissingMemberHandling.Error：参数名拼错直接报错，而不是静默填默认值。
    /// AI 拿到"看似成功却没生效"的结果，比拿到一条报错贵得多。
    /// </summary>
    static class BridgeJson
    {
        static readonly JsonSerializerSettings ResponseSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None,
        };

        static readonly JsonSerializer RequestSerializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
        });

        public static string Serialize(object response) => JsonConvert.SerializeObject(response, ResponseSettings);

        public static string Fail(string error) => Serialize(new BridgeResponse { error = error });

        public static T ToRequest<T>(JObject payload) => payload.ToObject<T>(RequestSerializer);

        /// <summary>空集合序列化出来的 [] 也是 token；一律转成 null 让整条字段消失。</summary>
        public static T[] OrNull<T>(this List<T> items) =>
            items == null || items.Count == 0 ? null : items.ToArray();

        /// <summary>false 无信息量的开关用它转成 null（只在 true 时出现在响应里）。</summary>
        public static bool? OrNull(this bool value) => value ? true : (bool?)null;
    }
}
