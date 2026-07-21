using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Fish
{
    /// <summary>
    /// 池塘游鱼模拟：驱动一组鱼 RectTransform 在指定范围内做“有方向的随机游动”（平滑转向 + 撞壁反弹），
    /// 并根据移动方向翻转朝向。非 MonoBehaviour，由外部（FishGamePanel）持有并每帧 Update 驱动，无需在预制上挂载/连线。
    /// 另提供 SeekTo：让指定鱼直线游向目标点（如鱼钩）并朝向目标，用于咬钩阶段。
    /// 全部坐标基于各鱼 parent 的 localPosition 空间（与 bounds 一致）。
    /// </summary>
    public class FishPondSwimmer
    {
        class Entry
        {
            public RectTransform rt;
            public Vector2 half;          // 鱼自身半尺寸，用于留边
            public float headingDeg;      // 当前朝向角
            public float targetDeg;       // 目标朝向角（平滑转向到此）
            public float speed;
            public float targetSpeed;     // 目标速度（平滑逼近）
            public float headingVel;      // SmoothDampAngle 转向角速度缓存
            public float speedVel;        // SmoothDamp 速度缓存
            public float retarget;        // 距离下次换向的剩余时间
            public float baseAbsScaleX;   // 原始 |scale.x|，初始化时归正
            public float baseAbsScaleY;   // 原始 |scale.y|，初始化时归正
            public bool seeking;          // true=直奔目标点
            public Vector2 seekTarget;
            public bool seekReached;
            public bool struggling;       // true=上钩后在原地摇摆抖动（溜鱼）
            public float struggleTime;
            public Vector2 struggleAnchor;
        }

        readonly RectTransform bounds;
        readonly List<Entry> entries = new();
        bool active;

        readonly float minSpeed, maxSpeed, turnDeg;
        readonly float faceRotZ;           // 鱼头朝向的基础角偏移：美术默认朝上时为 -90（若鱼头反了改 +90）
        const float SeekSpeedMul = 1.6f;   // 奔向鱼钩时的加速倍率

        // 自然游动（平滑蜿蜒 + 避壁转向）调参
        const float WanderDeltaDeg = 45f;      // 目标朝向每次随机漂移幅度
        const float WanderIntervalMin = 0.5f;  // 漂移间隔（秒）
        const float WanderIntervalMax = 1.5f;
        const float TurnSmoothTime = 0.55f;    // 转向缓动时间，越大越平滑
        const float SpeedSmoothTime = 0.4f;    // 速度缓动时间
        const float EdgeMargin = 100f;         // 距池壁多近开始转向避让
        const float EdgeSteerLerp = 4f;        // 避壁转向强度
        const float TurnSlowdown = 0.55f;      // 急转时的最低速度系数

        // 上钩挣扎（溜鱼）：仅做位置抖动，鱼身摆动交给 Spine idle 动画，算法不再旋转摆动
        const float StruggleShakeFreqX = 23f, StruggleShakeAmpX = 3f; // 水平抖动
        const float StruggleShakeFreqY = 19f, StruggleShakeAmpY = 4f; // 垂直抖动

        /// <param name="halfSizes">每条鱼的碰撞半尺寸(x=水平半宽, y=竖直半高)，与 fish 同序。
        /// Spine 鱼的 rect.size 已不等于视觉大小，用固定值算边界避免出界；某项为 0 时回退到 rect。</param>
        public FishPondSwimmer(IReadOnlyList<RectTransform> fish, RectTransform bounds,
            float minSpeed = 30f, float maxSpeed = 70f, float turnDeg = 120f, float faceRotZ = -90f,
            IReadOnlyList<Vector2> halfSizes = null)
        {
            this.bounds = bounds;
            this.minSpeed = minSpeed;
            this.maxSpeed = maxSpeed;
            this.turnDeg = turnDeg;
            this.faceRotZ = faceRotZ;

            for (int i = 0; i < fish.Count; i++)
            {
                var rt = fish[i];
                if (rt == null)
                    continue;
                bool hasFixed = halfSizes != null && i < halfSizes.Count
                        && halfSizes[i].x > 0f && halfSizes[i].y > 0f;
                var e = new Entry
                {
                    rt = rt,
                    half = hasFixed ? halfSizes[i] : rt.rect.size * 0.5f,
                    headingDeg = Random.Range(0f, 360f),
                    speed = Random.Range(minSpeed, maxSpeed),
                    retarget = Random.Range(1f, 3f),
                    baseAbsScaleX = Mathf.Abs(rt.localScale.x < 0.0001f ? 1f : rt.localScale.x),
                    baseAbsScaleY = Mathf.Abs(rt.localScale.y < 0.0001f ? 1f : rt.localScale.y),
                };
                e.targetDeg = e.headingDeg;
                e.targetSpeed = e.speed;
                // 归正缩放（清除历史左右翻转残留），并把鱼头转到初始朝向
                e.rt.localScale = new Vector3(e.baseAbsScaleX, e.baseAbsScaleY, e.rt.localScale.z);
                FaceHeading(e, e.headingDeg);
                entries.Add(e);
            }
        }

        public void SetActive(bool on)
        {
            active = on;
            if (on)
                ResetSeek();
        }

        /// <summary>清除奔向鱼钩/上钩挣扎状态，让所有鱼恢复自由游动（并复原挣扎期间的旋转）。</summary>
        public void ResetSeek()
        {
            foreach (var e in entries)
            {
                e.seeking = false;
                e.seekReached = false;
                // 结束挣扎；朝向会在下一帧自由游动时按移动方向重新对齐，无需在此复原
                e.struggling = false;
            }
        }

        /// <summary>让第 index 条鱼进入上钩挣扎：停在当前位置做摇摆抖动动画（溜鱼阶段）。</summary>
        public void Struggle(int index)
        {
            if (index < 0 || index >= entries.Count)
                return;
            var e = entries[index];
            e.seeking = false;
            e.seekReached = false;
            if (!e.struggling)
            {
                e.struggling = true;
                e.struggleTime = 0f;
                e.struggleAnchor = e.rt.localPosition;
            }
        }

        /// <summary>让第 index 条鱼直线游向目标点（parent 本地坐标），并朝向目标。</summary>
        public void SeekTo(int index, Vector2 localTarget)
        {
            if (index < 0 || index >= entries.Count)
                return;
            entries[index].seeking = true;
            entries[index].seekReached = false;
            entries[index].seekTarget = localTarget;
        }

        /// <summary>指定鱼是否已经到达 SeekTo 设置的目标点。</summary>
        public bool HasReachedSeekTarget(int index)
            => index >= 0 && index < entries.Count && entries[index].seekReached;

        public void Update(float dt)
        {
            if (!active || bounds == null || dt <= 0f)
                return;

            Rect b = bounds.rect;
            foreach (var e in entries)
            {
                Vector2 pos = e.rt.localPosition;

                // 上钩挣扎：仅在鱼钩处做位置抖动（鱼身摆动由 Spine idle 动画负责），保持当前朝向
                if (e.struggling)
                {
                    e.struggleTime += dt;
                    float t = e.struggleTime;
                    Vector2 jitter = new(
                        Mathf.Sin(t * StruggleShakeFreqX) * StruggleShakeAmpX,
                        Mathf.Sin(t * StruggleShakeFreqY) * StruggleShakeAmpY);
                    e.rt.localPosition = e.struggleAnchor + jitter;
                    continue;
                }

                if (e.seeking)
                {
                    Vector2 to = e.seekTarget - pos;
                    float dist = to.magnitude;
                    float moveDistance = e.speed * SeekSpeedMul * dt;
                    if (dist > Mathf.Max(1f, moveDistance))
                    {
                        Vector2 dir = to / dist;
                        pos += dir * moveDistance;
                        FaceHeading(e, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                        e.rt.localPosition = pos;
                    }
                    else
                    {
                        // 精确停在目标点，避免步长越过目标后在两侧来回震荡。
                        e.rt.localPosition = e.seekTarget;
                        e.seekReached = true;
                    }
                    continue;
                }

                // 平滑蜿蜒：目标朝向做小幅随机漂移（而非每次挑一个大角度硬转）
                e.retarget -= dt;
                if (e.retarget <= 0f)
                {
                    e.targetDeg += Random.Range(-WanderDeltaDeg, WanderDeltaDeg);
                    e.targetSpeed = Random.Range(minSpeed, maxSpeed);
                    e.retarget = Random.Range(WanderIntervalMin, WanderIntervalMax);
                }

                float xMin = b.xMin + e.half.x, xMax = b.xMax - e.half.x;
                float yMin = b.yMin + e.half.y, yMax = b.yMax - e.half.y;

                // 避壁转向：靠近池壁时把目标朝向平滑拨向池内（越贴边拨得越急），替代硬反弹
                Vector2 steer = Vector2.zero;
                if (pos.x < xMin + EdgeMargin) steer.x += (xMin + EdgeMargin - pos.x) / EdgeMargin;
                else if (pos.x > xMax - EdgeMargin) steer.x -= (pos.x - (xMax - EdgeMargin)) / EdgeMargin;
                if (pos.y < yMin + EdgeMargin) steer.y += (yMin + EdgeMargin - pos.y) / EdgeMargin;
                else if (pos.y > yMax - EdgeMargin) steer.y -= (pos.y - (yMax - EdgeMargin)) / EdgeMargin;
                if (steer.sqrMagnitude > 0.0001f)
                {
                    float awayDeg = Mathf.Atan2(steer.y, steer.x) * Mathf.Rad2Deg;
                    float w = Mathf.Clamp01(steer.magnitude);
                    e.targetDeg = Mathf.LerpAngle(e.targetDeg, awayDeg, EdgeSteerLerp * w * dt);
                }

                // 缓入缓出地转向到目标角；转得越急速度越低（自然减速）
                e.headingDeg = Mathf.SmoothDampAngle(
                    e.headingDeg, e.targetDeg, ref e.headingVel, TurnSmoothTime, turnDeg, dt);
                float turnMag = Mathf.Clamp01(Mathf.Abs(e.headingVel) / Mathf.Max(1f, turnDeg));
                float speedTarget = e.targetSpeed * Mathf.Lerp(1f, TurnSlowdown, turnMag);
                e.speed = Mathf.SmoothDamp(e.speed, speedTarget, ref e.speedVel, SpeedSmoothTime, Mathf.Infinity, dt);

                float rad = e.headingDeg * Mathf.Deg2Rad;
                Vector2 heading = new(Mathf.Cos(rad), Mathf.Sin(rad));
                pos += heading * (e.speed * dt);

                // 位置钳制：避壁转向已让鱼自然折返，这里只兜底防止极端情况越界
                pos.x = Mathf.Clamp(pos.x, xMin, xMax);
                pos.y = Mathf.Clamp(pos.y, yMin, yMax);

                FaceHeading(e, e.headingDeg);
                e.rt.localPosition = pos;
            }
        }

        // 旋转鱼头对准移动方向（真正转身，不再左右镜像翻转）。
        // 美术默认朝上，faceRotZ 默认 -90：headingDeg=90(上)→z=0；headingDeg=0(右)→z=-90。
        // 鱼身的摆动由 Spine idle 动画负责，这里只设朝向。挣扎期间锁定朝向不动。
        void FaceHeading(Entry e, float headingDeg)
        {
            if (e.struggling)
                return;
            var euler = e.rt.localEulerAngles;
            euler.z = headingDeg + faceRotZ;
            e.rt.localEulerAngles = euler;
        }
    }
}
