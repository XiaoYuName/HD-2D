using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Localization.Components;
using XFramework;

public class CookPanel : MonoBehaviour
{   // 做菜面板
    [SerializeField] RectTransform moveIndicator;
    [SerializeField] RectTransform greenArea;
    [SerializeField] GameObject opTip;
    [SerializeField] LocalizeStringEvent countDownText;
    [SerializeField] Image progressBar;
    [SerializeField] int countDownTime;
    float indicatorMoveSpeed => config.IndicatorMoveSpeed;
    float greenMoveSpeed => config.GreenMoveSpeed;
    float greenMinWidth => config.GreenMinWidth;
    float greenMaxWidth => config.GreenMaxWidth;
    float greenWidthSpeed=> config.GreenWidthSpeed;
    [SerializeField] float startProgressScore;
    [SerializeField] float maxProgressScore = 100f;
    [SerializeField] float targetProgressScore = 100f;
    [SerializeField] float greenAddScore = 10f;
    [SerializeField] float orangeSubScore = 5f;
    
    public event Action<bool, CookQuality> OnEnd;
     
    Coroutine cookCt;
    Coroutine registerInputCt;
    Coroutine endCt;
    MiniGameCookConfig config;
    float progressValue;
    float timeLeft;
    int moveDir = 1;
    int greenDir = -1;
    int greenWidthDir = 1;
    bool isEnded;
    bool isInputRegistered;

    public void Init(MiniGameCookConfig config)
    {
        this.config = config;
        countDownTime = config.CountDownTime;

        startProgressScore = config.StartProgressScore;
        maxProgressScore = config.MaxProgressScore;
        targetProgressScore = config.TargetProgressScore;
        greenAddScore = config.GreenAddScore;
        orangeSubScore = config.OrangeSubScore;

        gameObject.SetActive(true);
        opTip.SetActive(true);
        progressValue = startProgressScore;
        isEnded = false;

        RefreshProgress();
        RefreshCountDown(countDownTime);
        ResetRound();

        if(cookCt != null)
            StopCoroutine(cookCt);

        cookCt = StartCoroutine(CookCt());

        // 打开面板所依赖的输入（点击/空格）若同帧注册，会被打开面板的同一次输入立刻触发；延后一帧再注册
        if(registerInputCt != null)
            StopCoroutine(registerInputCt);
        registerInputCt = StartCoroutine(RegisterInputNextFrame());
    }

    void OnDisable()
    {
        if(cookCt != null)
            StopCoroutine(cookCt);
        cookCt = null;

        if(registerInputCt != null)
        {
            StopCoroutine(registerInputCt);
            registerInputCt = null;
        }

        if(endCt != null)
        {
            StopCoroutine(endCt);
            endCt = null;
        }

        UnregisterInput();
    }

    IEnumerator RegisterInputNextFrame()
    {
        yield return null;
        RegisterInput();
    }

    void RegisterInput()
    {
        if(isInputRegistered)
            return;

        PlayerInputManager.Instance.OnSpace += OnHitInput;
        PlayerInputManager.Instance.OnClick += OnHitInput;
        isInputRegistered = true;
    }

    void UnregisterInput()
    {
        if(!isInputRegistered)
            return;

        PlayerInputManager.Instance.OnSpace -= OnHitInput;
        PlayerInputManager.Instance.OnClick -= OnHitInput;
        isInputRegistered = false;
    }

    void OnHitInput()
    {
        if(isEnded)
            return;

        ChangeProgress(IsIndicatorInGreenArea() ? greenAddScore : -orangeSubScore);
    }

    IEnumerator CookCt()
    {
        timeLeft = countDownTime;
        RectTransform moveArea = (RectTransform)moveIndicator.parent;
        float leftX = GetMoveLeftX(moveArea);
        float rightX = GetMoveRightX(moveArea);

        while(timeLeft > 0f && !isEnded)
        {
            MoveIndicator(leftX, rightX);
            ChangeGreenWidth();
            MoveGreenArea(moveArea);

            timeLeft -= Time.deltaTime;
            RefreshCountDown(Mathf.CeilToInt(Mathf.Max(timeLeft, 0f)));

            yield return null;
        }

        if(!isEnded)
            EndCook(false);
    }

    void MoveIndicator(float leftX, float rightX)
    {
        Vector2 pos = moveIndicator.anchoredPosition;
        pos.x += indicatorMoveSpeed * moveDir * Time.deltaTime;

        // 指示器在左右边界间来回移动（碰到边界反向）
        if(pos.x >= rightX)
        {
            pos.x = rightX;
            moveDir = -1;
        }
        else if(pos.x <= leftX)
        {
            pos.x = leftX;
            moveDir = 1;
        }

        moveIndicator.anchoredPosition = pos;
    }

    void ChangeGreenWidth()
    {
        // 绿色区域宽度在最小/最大之间来回变化（碰到边界反向）
        Vector2 size = greenArea.sizeDelta;
        size.x += greenWidthSpeed * greenWidthDir * Time.deltaTime;
        if(size.x >= greenMaxWidth)
        {
            size.x = greenMaxWidth;
            greenWidthDir = -1;
        }
        else if(size.x <= greenMinWidth)
        {
            size.x = greenMinWidth;
            greenWidthDir = 1;
        }
        greenArea.sizeDelta = size;
    }

    void MoveGreenArea(RectTransform moveArea)
    {
        Vector2 pos = greenArea.anchoredPosition;
        pos.x += greenMoveSpeed * greenDir * Time.deltaTime;

        // 绿色区域在左右边界间来回移动（碰到边界反向），考虑自身宽度使其紧贴边界
        float rightLimit = GetGreenRightLimit(moveArea);
        float leftLimit = GetGreenLeftLimit(moveArea);
        if(pos.x >= rightLimit)
        {
            pos.x = rightLimit;
            greenDir = -1;
        }
        else if(pos.x <= leftLimit)
        {
            pos.x = leftLimit;
            greenDir = 1;
        }

        greenArea.anchoredPosition = pos;
    }

    bool IsIndicatorInGreenArea()
    {
        float indicatorX = moveIndicator.anchoredPosition.x;
        float greenLeftX = greenArea.anchoredPosition.x - greenArea.rect.width * greenArea.pivot.x;
        float greenRightX = greenArea.anchoredPosition.x + greenArea.rect.width * (1f - greenArea.pivot.x);
        return indicatorX >= greenLeftX && indicatorX <= greenRightX;
    }

    void ChangeProgress(float value)
    {
        progressValue = Mathf.Clamp(progressValue + value, 0f, maxProgressScore);
        RefreshProgress();

        if(progressValue >= targetProgressScore)
            EndCook(true);
    }

    void RefreshProgress()
    {
        float progress = targetProgressScore <= 0f ? 0f : progressValue / targetProgressScore;
        progressBar.fillAmount = Mathf.Clamp01(progress);
    }

    void RefreshCountDown(int time)
    {
        countDownText.SetVar(LocVarSet.MiniGame.CountDownTime, time);
    }

    void ResetRound()
    {
        RectTransform moveArea = (RectTransform)moveIndicator.parent;

        // 指示器回到最左侧，向右开始移动
        Vector2 indicatorPos = moveIndicator.anchoredPosition;
        indicatorPos.x = GetMoveLeftX(moveArea);
        moveIndicator.anchoredPosition = indicatorPos;
        moveDir = 1;

        // 绿色区域宽度回到最小，向变宽方向开始变化
        Vector2 greenSize = greenArea.sizeDelta;
        greenSize.x = greenMinWidth;
        greenArea.sizeDelta = greenSize;
        greenWidthDir = 1;

        // 绿色区域回到最右侧，向左开始移动
        Vector2 greenPos = greenArea.anchoredPosition;
        greenPos.x = GetGreenRightLimit(moveArea);
        greenArea.anchoredPosition = greenPos;
        greenDir = -1;
    }

    float GetGreenRightLimit(RectTransform moveArea)
    {
        // 绿色区域右沿紧贴移动区域右边界
        float areaRightX = moveArea.rect.width * (1f - moveArea.pivot.x);
        float greenRightExtent = greenArea.rect.width * (1f - greenArea.pivot.x);
        return areaRightX - greenRightExtent;
    }

    float GetGreenLeftLimit(RectTransform moveArea)
    {
        // 绿色区域左沿紧贴移动区域左边界
        float areaLeftX = -moveArea.rect.width * moveArea.pivot.x;
        float greenLeftExtent = greenArea.rect.width * greenArea.pivot.x;
        return areaLeftX + greenLeftExtent;
    }

    float GetMoveLeftX(RectTransform moveArea)
    {
        return -moveArea.rect.width * moveArea.pivot.x + moveIndicator.rect.width * moveIndicator.pivot.x;
    }

    float GetMoveRightX(RectTransform moveArea)
    {
        return moveArea.rect.width * (1f - moveArea.pivot.x) - moveIndicator.rect.width * (1f - moveIndicator.pivot.x);
    }

    void EndCook(bool isSuccess)
    {
        if(isEnded)
            return;

        isEnded = true;
        UnregisterInput();

        if(cookCt != null)
        {
            StopCoroutine(cookCt);
            cookCt = null;
        }

        opTip.SetActive(false);
        if(!isSuccess)
            RefreshCountDown(0);

        float timeLeftRate = countDownTime <= 0 ? 0f : Mathf.Clamp01(timeLeft / countDownTime);
        CookQuality quality = config.GetCookQuality(isSuccess, timeLeftRate);

        // 结束时延迟一帧再关闭面板并派发事件，避免与触发结束的这次输入（点击/空格）同帧被下一个面板注册的输入监听冲突
        if(endCt != null)
            StopCoroutine(endCt);
        endCt = StartCoroutine(EndCookNextFrame(isSuccess, quality));
    }

    IEnumerator EndCookNextFrame(bool isSuccess, CookQuality quality)
    {
        yield return null;
        gameObject.SetActive(false);
        OnEnd?.Invoke(isSuccess, quality);
    }
}
