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
            public float retarget;        // 距离下次换向的剩余时间
            public float baseAbsScaleX;   // 原始 |scale.x|，用于翻转时保持大小
            public float baseRotZ;        // 原始 Z 旋转，挣扎结束后复原
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
        const float SeekSpeedMul = 1.6f;   // 奔向鱼钩时的加速倍率
        const float FaceRightSign = 1f;    // 鱼美术默认朝右；若默认朝左把此改为 -1

        // 上钩挣扎（溜鱼）动画参数：绕鱼钩做小幅摇摆旋转 + 位置抖动
        const float StruggleSwayFreq = 14f, StruggleSwayDeg = 16f;   // 摇摆旋转
        const float StruggleShakeFreqX = 23f, StruggleShakeAmpX = 3f; // 水平抖动
        const float StruggleShakeFreqY = 19f, StruggleShakeAmpY = 4f; // 垂直抖动

        public FishPondSwimmer(IEnumerable<RectTransform> fish, RectTransform bounds,
            float minSpeed = 30f, float maxSpeed = 70f, float turnDeg = 120f)
        {
            this.bounds = bounds;
            this.minSpeed = minSpeed;
            this.maxSpeed = maxSpeed;
            this.turnDeg = turnDeg;

            foreach (var rt in fish)
            {
                if (rt == null)
                    continue;
                var e = new Entry
                {
                    rt = rt,
                    half = rt.rect.size * 0.5f,
                    headingDeg = Random.Range(0f, 360f),
                    speed = Random.Range(minSpeed, maxSpeed),
                    retarget = Random.Range(1f, 3f),
                    baseAbsScaleX = Mathf.Abs(rt.localScale.x < 0.0001f ? 1f : rt.localScale.x),
                    baseRotZ = rt.localEulerAngles.z,
                };
                e.targetDeg = e.headingDeg;
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
                if (e.struggling)
                {
                    e.struggling = false;
                    var euler = e.rt.localEulerAngles;
                    euler.z = e.baseRotZ;
                    e.rt.localEulerAngles = euler;
                }
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

                // 上钩挣扎：在鱼钩处原地摇摆旋转 + 抖动，模拟溜鱼手感
                if (e.struggling)
                {
                    e.struggleTime += dt;
                    float t = e.struggleTime;
                    Vector2 jitter = new(
                        Mathf.Sin(t * StruggleShakeFreqX) * StruggleShakeAmpX,
                        Mathf.Sin(t * StruggleShakeFreqY) * StruggleShakeAmpY);
                    e.rt.localPosition = e.struggleAnchor + jitter;
                    var euler = e.rt.localEulerAngles;
                    euler.z = e.baseRotZ + Mathf.Sin(t * StruggleSwayFreq) * StruggleSwayDeg;
                    e.rt.localEulerAngles = euler;
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
                        Face(e, dir.x);
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

                // 平滑转向到目标角
                e.retarget -= dt;
                if (e.retarget <= 0f)
                {
                    e.targetDeg = e.headingDeg + Random.Range(-90f, 90f);
                    e.speed = Random.Range(minSpeed, maxSpeed);
                    e.retarget = Random.Range(1.5f, 3.5f);
                }
                e.headingDeg = Mathf.MoveTowardsAngle(e.headingDeg, e.targetDeg, turnDeg * dt);

                float rad = e.headingDeg * Mathf.Deg2Rad;
                Vector2 heading = new(Mathf.Cos(rad), Mathf.Sin(rad));
                pos += heading * (e.speed * dt);

                // 撞壁反弹：贴边并把朝向反射回场内
                float xMin = b.xMin + e.half.x, xMax = b.xMax - e.half.x;
                float yMin = b.yMin + e.half.y, yMax = b.yMax - e.half.y;
                if (pos.x < xMin || pos.x > xMax)
                {
                    pos.x = Mathf.Clamp(pos.x, xMin, xMax);
                    e.headingDeg = 180f - e.headingDeg;
                    e.targetDeg = e.headingDeg;
                }
                if (pos.y < yMin || pos.y > yMax)
                {
                    pos.y = Mathf.Clamp(pos.y, yMin, yMax);
                    e.headingDeg = -e.headingDeg;
                    e.targetDeg = e.headingDeg;
                }

                Face(e, heading.x);
                e.rt.localPosition = pos;
            }
        }

        // 依据水平移动方向翻转朝向
        void Face(Entry e, float dirX)
        {
            if (Mathf.Abs(dirX) < 0.01f)
                return;
            float sign = dirX >= 0f ? FaceRightSign : -FaceRightSign;
            var s = e.rt.localScale;
            s.x = e.baseAbsScaleX * sign;
            e.rt.localScale = s;
        }
    }
}
