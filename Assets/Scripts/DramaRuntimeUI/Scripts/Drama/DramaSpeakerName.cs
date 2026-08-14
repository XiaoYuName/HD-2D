using Drama.Runtime;
using Drama.Runtime.Services;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 说话人名字该去多语言表的哪一条取。
    ///
    /// 包里的 <see cref="DialogueLine"/> 只交出<b>寻址方式</b>（旁白 / 主角 / 自定义 / 指定角色），
    /// 不交名字本身 —— 四个来源里主角是玩家昵称、指定角色要查宿主的角色表，
    /// 这些包都不认识（见 <c>DialogueLine</c> 的注释）。所以这一步的分支归宿主，
    /// 而对话框和对话历史都要走同一份，抽在这儿只写一遍。
    ///
    /// <b>返回的还是引用不是字符串</b>：四路（包括主角昵称）都走
    /// <c>LocalizeStringEvent</c> 绑定，玩家中途切语言名字能跟着刷新。
    /// </summary>
    public static class DramaSpeakerName
    {
        /// <summary>
        /// 主角名在多语言表里的位置。表里的值写成 <c>{global.PlayerName}</c>，
        /// 由 <c>GameDataManager.SetGlobalVariablesSource("global", "PlayerName", ...)</c> 灌进去。
        ///
        /// 这样主角名也能跟着语言变（日文版可以写成「{global.PlayerName}さん」），
        /// 而且少一个"字面量而不是引用"的特例分支。
        /// </summary>
        public const string HeroNameTable = "UIText";
        public const string HeroNameKey = "Character/Hero_Name";

        /// <summary>
        /// 取名字的多语言位置。
        /// </summary>
        /// <returns>false = 这一句不显示名字（旁白，或者角色表里没配名字）。</returns>
        public static bool TryResolve(in DialogueLine line, out string table, out string key)
        {
            table = null;
            key = null;

            switch (line.Speaker)
            {
                case ESpeakerKind.Aside:
                    // 旁白不显示名字
                    return false;

                case ESpeakerKind.Hero:
                    table = HeroNameTable;
                    key = HeroNameKey;
                    return true;

                case ESpeakerKind.Custom:
                    table = line.SpeakerNameRef.Table;
                    key = line.SpeakerNameRef.Key;
                    return !string.IsNullOrEmpty(key);

                case ESpeakerKind.Actor:
                    NpcData npcData = LubanManager.Instance.TbNpcData.GetOrDefault(line.ActorId);
                    if (npcData?.Name == null)
                    {
                        Debug.LogWarning($"[Drama] 角色 {line.ActorId} 没有名字配置，名字栏不显示");
                        return false;
                    }

                    table = npcData.Name.Table;
                    key = npcData.Name.Value;
                    return true;

                default:
                    return false;
            }
        }
    }
}
