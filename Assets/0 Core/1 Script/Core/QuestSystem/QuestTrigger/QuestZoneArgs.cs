namespace XFramework
{
    /// <summary>
    /// 一次区域事件的参数。场景是两级的（大场景 <see cref="SceneData.WordMapSceneID"/> ＋
    /// 小场景 <see cref="SceneData.SceneID"/>），两张配表的 ID 区间重叠，所以两级都要带上。
    /// </summary>
    public readonly struct QuestZoneArgs
    {
        /// <summary>大场景ID（<c>TbWordMapSceneData</c>）。</summary>
        public readonly long MapSceneId;

        /// <summary>小场景ID（<c>TbGameSceneData</c>）；0 = 这次事件说的是大场景本身。</summary>
        public readonly long SceneId;

        /// <summary>已停留秒数，只有 <see cref="QuestTriggerType.EnterZoneStay"/> 用。</summary>
        public readonly int StaySeconds;

        public QuestZoneArgs(long mapSceneId, long sceneId, int staySeconds = 0)
        {
            MapSceneId = mapSceneId;
            SceneId = sceneId;
            StaySeconds = staySeconds;
        }

        public override string ToString() => $"大场景{MapSceneId}/小场景{SceneId}/{StaySeconds}s";
    }
}
