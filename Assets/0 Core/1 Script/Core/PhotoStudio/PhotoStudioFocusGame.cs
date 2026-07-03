using System;
using System.Collections;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PhotoStudioFocusGame : MonoBehaviour
{   // 对焦小游戏：屏幕生成动态移动的瞄准器，玩家用鼠标跟随，每秒判定鼠标与瞄准器的重合度加减分
    [Header("Ref")]
    [SerializeField] RectTransform playArea;        // 瞄准器活动区域
    [SerializeField] RectTransform reticle;         // 动态移动的瞄准器
    [SerializeField] RectTransform focusCursor;     // 跟随鼠标的对焦光标（可选，纯表现）
    [SerializeField] CountDownPop countDownText; // 倒计时文本
    [SerializeField] TextMeshProUGUI scoreText;     // 对焦积分文本
    [SerializeField] Image scoreBar;                // 对焦积分进度条（可选）
    [SerializeField] Material blurMaterial;          // 组合模糊材质（可选）；留空则自动用 UI/UIBlurPremul（带透明边精灵不黑框）。若要手动指定请用预乘版材质
    [SerializeField] Transform blurGroupRoot;        // 组合图片模糊的父物体
    [SerializeField] PhotoSceneConfigPanel photoSceneConfigPanel;
    [Header("Config")]
    [SerializeField] PhotoStudioGameConfig config;
    [SerializeField] int countDownTime = 30;            // 限时（秒）
    [SerializeField] int successScore = 5;              // 每秒在瞄准区域内得分
    [SerializeField] int failScore = -5;                // 每秒不在瞄准区域内得分
    [SerializeField] int targetScore = 100;             // 目标得分
    [SerializeField] float reticleMoveSpeed = 300f;     // 瞄准器移动速度（像素/秒）
    [SerializeField] float reticleReachDist = 8f;       // 到达目标点的判定距离
    [SerializeField] float maxBlur = 12f;               // 0 分时的最大模糊强度（像素），越大越模糊
    [SerializeField] Image[] blurGroup;           // 运行时缓存：从 blurGroupRoot 下自动获取的全部 Image
    public event Action<bool, int> OnEnd;   // (是否达到目标得分, 最终得分)

    Canvas canvas;
    Coroutine gameCt;
    Vector2 reticleTarget;
    float timeLeft;
    int score;
    bool isEnded;
    Material groupBlurMatInst;   // 组合图片共享的模糊材质实例（一份实例统一驱动整组）

    float scoreBarFullWidth;    // 进度条满宽度（取自父物体宽度）
    static readonly int BlurSizeID = Shader.PropertyToID("_BlurSize");

    public int Score => score;

    [Button]
    // 点击【拍摄】按钮调用，开始对焦小游戏
    public void StartGame()
    {
        photoSceneConfigPanel.Init(PhotoStudioManager.St.CurConfig.bgId, PhotoStudioManager.St.CurConfig.lightingId, PhotoStudioManager.St.CurConfig.postureId, PhotoStudioManager.St.CurConfig.clothesId);
        gameObject.SetActive(true);
        isEnded = false;
        score = 0;
        timeLeft = countDownTime;

        PickNewReticleTarget();

        scoreBarFullWidth = (scoreBar.rectTransform.parent as RectTransform).rect.width;
        RefreshScore();
        countDownText.SetText(countDownTime);

        if(gameCt != null)
            StopCoroutine(gameCt);
        gameCt = StartCoroutine(GameCt());
    }

    void OnDisable()
    {
        if(gameCt != null)
            StopCoroutine(gameCt);
        gameCt = null;
    }

    IEnumerator GameCt()
    {
        float scoreTick = 0f;   // 每满 1 秒判定一次重合度

        while(timeLeft > 0f && !isEnded)
        {
            MoveReticle();
            UpdateFocusCursor();

            scoreTick += Time.deltaTime;
            if(scoreTick >= 1f)
            {
                scoreTick -= 1f;
                EvaluateFocus();
                if(isEnded)
                    yield break;
            }

            timeLeft -= Time.deltaTime;
            countDownText.SetTime(Mathf.Max(timeLeft, 0f));

            yield return null;
        }

        // 倒计时结束
        End(false);
    }

    // 瞄准器朝随机目标点移动，到达后再随机选取新目标点，形成动态移动
    void MoveReticle()
    {
        Vector2 pos = Vector2.MoveTowards(reticle.anchoredPosition, reticleTarget, reticleMoveSpeed * Time.deltaTime);
        reticle.anchoredPosition = pos;

        if((pos - reticleTarget).sqrMagnitude <= reticleReachDist * reticleReachDist)
            PickNewReticleTarget();
    }

    void PickNewReticleTarget()
    {
        float halfW = Mathf.Max(0f, playArea.rect.width * 0.5f - reticle.rect.width * 0.5f);
        float halfH = Mathf.Max(0f, playArea.rect.height * 0.5f - reticle.rect.height * 0.5f);
        reticleTarget = new Vector2(
            UnityEngine.Random.Range(-halfW, halfW),
            UnityEngine.Random.Range(-halfH, halfH));
    }

    // 对焦光标跟随鼠标
    void UpdateFocusCursor()
    {
        RectTransform parent = focusCursor.parent as RectTransform;
        if(parent == null)
            return;

        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, GetMouseScreenPos(), GetCanvasCamera(), out Vector2 local))
            focusCursor.anchoredPosition = local;
    }

    void EvaluateFocus()
    {
        ChangeScore(IsMouseOnReticle() ? successScore : failScore);
    }

    bool IsMouseOnReticle()
    {
        return RectTransformUtility.RectangleContainsScreenPoint(reticle, GetMouseScreenPos(), GetCanvasCamera());
    }

    void ChangeScore(int value)
    {
        score = Mathf.Clamp(score + value, 0, targetScore);
        RefreshScore();

        // 达到目标得分，提前结束
        if(score >= targetScore)
            End(true);
    }

    void RefreshScore()
    {
        scoreText.text = $"{score}/{targetScore}";
        UpdateScoreBar();
        UpdateGroupBlur();
    }

    // 用 width 表现进度条：以父物体宽度为满值，按得分进度设置进度条宽度（左对齐增长）
    void UpdateScoreBar()
    {
        RectTransform rt = scoreBar.rectTransform;
        // if(!scoreBarReady)
        // {
        //     RectTransform parent = rt.parent as RectTransform;
        //     scoreBarFullWidth = parent != null ? parent.rect.width : rt.rect.width;

        //     // 改为左对齐，使进度条按 width 从左向右增长
        //     rt.anchorMin = new Vector2(0f, 0f);
        //     rt.anchorMax = new Vector2(0f, 1f);
        //     rt.pivot = new Vector2(0f, 0.5f);
        //     rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
        //     scoreBarReady = true;
        // }
        float progress = targetScore <= 0 ? 0f : Mathf.Clamp01((float)score / targetScore);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, scoreBarFullWidth * progress);
    }

    // 组合图片模糊：把一份共享模糊材质挂到整组图片上，用同一模糊强度统一驱动，
    // 使叠在一起的多张图片（背景+角色等）表现为对合成画面的整体模糊。0 分最模糊，达到目标分时清晰。
    void UpdateGroupBlur()
    {
        EnsureGroupBlurMaterial();
        if(groupBlurMatInst == null)
            return;

        float progress = targetScore <= 0 ? 1f : Mathf.Clamp01((float)score / targetScore);
        groupBlurMatInst.SetFloat(BlurSizeID, Mathf.Lerp(maxBlur, 0f, progress));
    }
    // 懒加载组合模糊的共享材质实例，并挂到 blurGroup 内每一张图片上（共用一份实例 => 改一次全组生效）
    void EnsureGroupBlurMaterial()
    {
        if(groupBlurMatInst == null)
        {
            if(blurMaterial != null)
            {
                groupBlurMatInst = new Material(blurMaterial);
            }
            else
            {
                // 用预乘 Alpha 的模糊 shader：模糊带透明边的精灵（如角色）时不会出现黑框
                Shader sh = Shader.Find("UI/UIBlurPremul");
                if(sh == null)
                {
                    Debug.LogWarning("[PhotoStudioFocusGame] 未找到 UI/UIBlurPremul 着色器，组合图片模糊不可用。");
                    return;
                }
                groupBlurMatInst = new Material(sh);
            }
            groupBlurMatInst.hideFlags = HideFlags.DontSave;
        }

        for(int i = 0; i < blurGroup.Length; i++)
        {
            Image img = blurGroup[i];
            if(img != null && img.material != groupBlurMatInst)
                img.material = groupBlurMatInst;
        }
    }

    void OnDestroy()
    {
        if(groupBlurMatInst != null)
        {
            if(blurGroup != null)
            {
                for(int i = 0; i < blurGroup.Length; i++)
                {
                    Image img = blurGroup[i];
                    if(img != null && img.material == groupBlurMatInst)
                        img.material = null;    // 恢复默认 UI 材质
                }
            }
            Destroy(groupBlurMatInst);
            groupBlurMatInst = null;
        }
    }
    Vector2 GetMouseScreenPos()
    {
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
    }

    Camera GetCanvasCamera()
    {
        if(canvas == null)
            canvas = GetComponentInParent<Canvas>();
        if(canvas == null)
            return null;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    void End(bool reachedTarget)
    {
        if(isEnded)
            return;

        isEnded = true;
        if(gameCt != null)
        {
            StopCoroutine(gameCt);
            gameCt = null;
        }

        if(!reachedTarget)
            countDownText.SetText(0);

        gameObject.SetActive(false);
        OnEnd?.Invoke(reachedTarget, score);
    }
}
