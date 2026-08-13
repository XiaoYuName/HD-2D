using System;
using System.Collections.Generic;
using System.IO;
using Drama.Runtime;
using Newtonsoft.Json;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 已读标记：哪些台词玩家已经看过。「跳过」只跳已读靠它判断。
    ///
    /// <b>跨存档共享</b>，所以不在 <c>GameSaveData</c> 里，而是存档目录下单独一个文件。
    /// 二周目、换存档槽、甚至删档重来都还认这些已读 —— 主流 AVG 的做法，
    /// 玩家不会因为开了个新档就得把看过的再跳一遍。
    ///
    /// <b>身份是「剧本ID + 正文的多语言键」，不是指令下标。</b>
    /// 下标是导出产物的编号，剧本一重导就全变了（见 <c>DramaRestorePoint</c> 的注释），
    /// 拿它当已读身份的话，策划改一次图，玩家的已读记录就整段错位。
    /// 正文键是策划填的、跟着这句话走，改剧本顺序不影响它。
    /// 带上剧本ID 是为了避免两个剧本共用同一条文本（"……" 这种）时互相算作已读。
    ///
    /// 存的是 64 位哈希而不是原文键：条数上万时长度固定、文件小，
    /// 而且不用管键里有什么字符。碰撞概率在十万条量级是 ~1e-10，
    /// 真撞了的后果也只是某一句被当成已读，不影响存档。
    /// </summary>
    public sealed class DramaReadMarks
    {
        /// <summary>文件格式版本。以后要改结构时靠它分辨。</summary>
        private const int FormatVersion = 1;

        private const string FileName = "DramaRead.json";

        /// <summary>
        /// 落在存档目录下，但<b>不</b>经过 <see cref="SaveGameManager"/> ——
        /// 它那条路是按存档槽走的，而已读是全局的。这里也不依赖它初始化过没有。
        /// </summary>
        private static string FilePath =>
            Path.Combine(Application.persistentDataPath, SaveGameManager.SaveFolderName, FileName);

        private readonly HashSet<ulong> keys = new HashSet<ulong>();

        /// <summary>文件读过了没有。第一次用到时才读，不跟游戏的初始化顺序纠缠。</summary>
        private bool loaded;

        /// <summary>有没有还没落盘的新标记。</summary>
        private bool dirty;

        public int Count
        {
            get
            {
                EnsureLoaded();
                return keys.Count;
            }
        }

        // ==================================================== 查询 / 标记

        /// <summary>
        /// 这句读过没有。
        ///
        /// <b>没有正文的台词一律算已读</b>：它们的键是空的、会全撞在一起，
        /// 记进去没有意义；而且要是把它判成"未读"，跳过会莫名其妙地停在一条空台词上。
        /// </summary>
        public bool IsRead(long dramaId, in LocalizedRef text)
        {
            if (text.IsEmpty)
            {
                return true;
            }

            EnsureLoaded();
            return keys.Contains(KeyOf(dramaId, text));
        }

        /// <summary>标记为已读。</summary>
        /// <returns>true = 这次才第一次读到（之前没记过）。</returns>
        public bool Mark(long dramaId, in LocalizedRef text)
        {
            if (text.IsEmpty)
            {
                return false;
            }

            EnsureLoaded();

            if (!keys.Add(KeyOf(dramaId, text)))
            {
                return false;
            }

            dirty = true;
            return true;
        }

        /// <summary>清空所有已读。给"设置里重置已读记录"用。</summary>
        public void ClearAll()
        {
            EnsureLoaded();

            if (keys.Count == 0)
            {
                return;
            }

            keys.Clear();
            dirty = true;
        }

        /// <summary>
        /// 一条台词的已读身份。FNV-1a 64。
        ///
        /// 每个字符按两个字节喂进去 —— 只取低字节的话，中文键之间会撞得很厉害。
        /// </summary>
        public static ulong KeyOf(long dramaId, in LocalizedRef text)
        {
            const ulong offset = 14695981039346656037UL;

            ulong hash = offset;
            ulong id = unchecked((ulong)dramaId);

            for (int i = 0; i < 8; i++)
            {
                hash = Mix(hash, (byte)(id >> (i * 8)));
            }

            hash = Mix(hash, text.Table);

            // 表和键之间塞个分隔符：不塞的话 ("ab","c") 和 ("a","bc") 会算出同一个值
            hash = Mix(hash, (byte)'\n');

            hash = Mix(hash, text.Key);
            return hash;
        }

        private static ulong Mix(ulong hash, byte value)
        {
            const ulong prime = 1099511628211UL;
            return (hash ^ value) * prime;
        }

        private static ulong Mix(ulong hash, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return hash;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                hash = Mix(hash, (byte)(c & 0xFF));
                hash = Mix(hash, (byte)(c >> 8));
            }

            return hash;
        }

        // ==================================================== 落盘

        /// <summary>有新标记才写文件。每记一条就写一次太浪费，调用方在段落收尾 / 存档时调它。</summary>
        public void SaveIfDirty()
        {
            if (!dirty)
            {
                return;
            }

            Save();
        }

        public void Save()
        {
            EnsureLoaded();

            try
            {
                string path = FilePath;
                string dir = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                Payload payload = new Payload
                {
                    Version = FormatVersion,
                    Keys = new List<ulong>(keys),
                };

                File.WriteAllText(path, JsonConvert.SerializeObject(payload));
                dirty = false;
            }
            catch (Exception e)
            {
                // 写不进去不该把剧情打断，下次还会再试（dirty 保持 true）
                Debug.LogError($"[Drama] 已读记录保存失败 path={FilePath}\n{e}");
            }
        }

        /// <summary>重新从文件读一次。一般不用手动调，第一次查询时会自己读。</summary>
        public void Reload()
        {
            loaded = false;
            dirty = false;
            keys.Clear();
            EnsureLoaded();
        }

        private void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            // 先立起标志：下面无论成功失败都不该再来一次，
            // 失败时反复读一个坏文件只会每帧刷一屏错误日志
            loaded = true;

            try
            {
                string path = FilePath;
                if (!File.Exists(path))
                {
                    return;
                }

                Payload payload = JsonConvert.DeserializeObject<Payload>(File.ReadAllText(path));
                if (payload?.Keys == null)
                {
                    return;
                }

                // 版本高于当前的文件是以后的版本写的，读不了就当没有已读记录，
                // 别去猜它的结构。丢的只是"哪些看过"，玩家最多多跳一遍
                if (payload.Version > FormatVersion)
                {
                    Debug.LogWarning($"[Drama] 已读记录的格式版本 {payload.Version} 高于当前支持的 {FormatVersion}，本次忽略");
                    return;
                }

                for (int i = 0; i < payload.Keys.Count; i++)
                {
                    keys.Add(payload.Keys[i]);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Drama] 已读记录读取失败（文件可能已损坏），按空记录处理 path={FilePath}\n{e}");
            }
        }

        [Serializable]
        private class Payload
        {
            public int Version = FormatVersion;
            public List<ulong> Keys = new List<ulong>();
        }
    }
}
