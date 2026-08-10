using Drama.Runtime.Flow;
using Drama.Runtime.Services;

namespace XFramework
{
    /// <summary>
    /// <see cref="IDramaContext"/> 的本工程实现。
    ///
    /// 就是个服务容器——Handler 碰外部世界全部经过它。
    /// <b>不要往这里塞逻辑</b>，它存在的意义就是让 Drama 包不认识本工程任何类型。
    /// 装配在 <see cref="DramaDirector"/> 的构造函数里。
    /// </summary>
    public sealed class DramaContext : IDramaContext
    {
        /// <summary>播放模式。Director 每段开播前按当前设置（自动播放 / 跳过）刷一次。</summary>
        public EDramaPlaybackMode Mode { get; set; }

        public IDialogueView       Dialogue     { get; set; }
        public IChoiceView         Choice       { get; set; }
        public IActorStage         Actors       { get; set; }
        public IDramaScreen        Screen       { get; set; }
        public IDramaBackground    Background   { get; set; }
        public IDramaLocalization  Localization { get; set; }
        public IDramaAssetProvider Assets       { get; set; }
        public IDramaAudio         Audio        { get; set; }
        public IDramaGameBridge    Game         { get; set; }
    }
}
