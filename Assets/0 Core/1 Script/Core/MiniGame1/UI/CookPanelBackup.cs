// using TMPro;
// using System.Collections;
// using UnityEngine;
// using UnityEngine.InputSystem;
// using UnityEngine.UI;
// using System;
// using UnityEngine.Localization.Components;

// public class CookPanelBackup : MonoBehaviour
// {   // 做菜面板
//     [SerializeField] RectTransform moveIndicator;
//     [SerializeField] RectTransform greenArea;
//     [SerializeField] GameObject opTip;
//     [SerializeField] LocalizeStringEvent countDownText;
//     [SerializeField] Image progressBar;
//     [SerializeField] int countDownTime;
//     [SerializeField] float indicatorMoveSpeed;
//     [SerializeField] float greenMoveSpeed;
//     [SerializeField] float greenMinWidth;
//     [SerializeField] float greenMaxWidth;
//     [SerializeField] float greenWidthSpeed;
//     [SerializeField] float startProgressScore;
//     [SerializeField] float maxProgressScore = 100f;
//     [SerializeField] float targetProgressScore = 100f;
//     [SerializeField] float greenAddScore = 10f;
//     [SerializeField] float orangeSubScore = 5f;
    
//     public event Action<bool, CookQuality> OnEnd;
     
//     Coroutine cookCt;
//     MiniGameCookConfig config;
//     float progressValue;
//     float timeLeft;
//     int moveDir = 1;
//     int greenDir = -1;
//     int greenWidthDir = 1;
//     bool isEnded;

//     public void Init(MiniGameCookConfig config)
//     {
//         this.config = config;
//         countDownTime = config.CountDownTime;
//         indicatorMoveSpeed = config.IndicatorMoveSpeed;
//         greenMoveSpeed = config.GreenMoveSpeed;
//         greenMinWidth = config.GreenMinWidth;
//         greenMaxWidth = config.GreenMaxWidth;
//         greenWidthSpeed = config.GreenWidthSpeed;
//         startProgressScore = config.StartProgressScore;
//         maxProgressScore = config.MaxProgressScore;
//         targetProgressScore = config.TargetProgressScore;
//         greenAddScore = config.GreenAddScore;
//         orangeSubScore = config.OrangeSubScore;

//         gameObject.SetActive(true);
//         opTip.SetActive(true);
//         progressValue = startProgressScore;
//         isEnded = false;

//         RefreshProgress();
//         RefreshCountDown(countDownTime);
//         ResetRound();

//         if(cookCt != null)
//             StopCoroutine(cookCt);

//         cookCt = StartCoroutine(CookCt());
//     }

//     void OnDisable()
//     {
//         if(cookCt != null)
//             StopCoroutine(cookCt);
//         cookCt = null;
//     }

//     IEnumerator CookCt()
//     {
//         timeLeft = countDownTime;
//         RectTransform moveArea = (RectTransform)moveIndicator.parent;
//         float leftX = GetMoveLeftX(moveArea);
//         float rightX = GetMoveRightX(moveArea);

//         while(timeLeft > 0f && !isEnded)
//         {
//             MoveIndicator(leftX, rightX);
//             ChangeGreenWidth();
//             MoveGreenArea(moveArea);

//             if(Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
//             {
//                 ChangeProgress(IsIndicatorInGreenArea() ? greenAddScore : -orangeSubScore);
//                 if(isEnded)
//                     yield break;

//                 // 无论是否击中绿色，按下空格后都重新开始一轮
//                 ResetRound();
//             }

//             timeLeft -= Time.deltaTime;
//             RefreshCountDown(Mathf.CeilToInt(Mathf.Max(timeLeft, 0f)));

//             yield return null;
//         }

//         EndCook(false);
//     }

//     void MoveIndicator(float leftX, float rightX)
//     {
//         Vector2 pos = moveIndicator.anchoredPosition;
//         pos.x += indicatorMoveSpeed * moveDir * Time.deltaTime;

//         // 指示器在左右边界间来回移动（碰到边界反向）
//         if(pos.x >= rightX)
//         {
//             pos.x = rightX;
//             moveDir = -1;
//         }
//         else if(pos.x <= leftX)
//         {
//             pos.x = leftX;
//             moveDir = 1;
//         }

//         moveIndicator.anchoredPosition = pos;
//     }

//     void ChangeGreenWidth()
//     {
//         // 绿色区域宽度在最小/最大之间来回变化（碰到边界反向）
//         Vector2 size = greenArea.sizeDelta;
//         size.x += greenWidthSpeed * greenWidthDir * Time.deltaTime;
//         if(size.x >= greenMaxWidth)
//         {
//             size.x = greenMaxWidth;
//             greenWidthDir = -1;
//         }
//         else if(size.x <= greenMinWidth)
//         {
//             size.x = greenMinWidth;
//             greenWidthDir = 1;
//         }
//         greenArea.sizeDelta = size;
//     }

//     void MoveGreenArea(RectTransform moveArea)
//     {
//         Vector2 pos = greenArea.anchoredPosition;
//         pos.x += greenMoveSpeed * greenDir * Time.deltaTime;

//         // 绿色区域在左右边界间来回移动（碰到边界反向），考虑自身宽度使其紧贴边界
//         float rightLimit = GetGreenRightLimit(moveArea);
//         float leftLimit = GetGreenLeftLimit(moveArea);
//         if(pos.x >= rightLimit)
//         {
//             pos.x = rightLimit;
//             greenDir = -1;
//         }
//         else if(pos.x <= leftLimit)
//         {
//             pos.x = leftLimit;
//             greenDir = 1;
//         }

//         greenArea.anchoredPosition = pos;
//     }

//     bool IsIndicatorInGreenArea()
//     {
//         float indicatorX = moveIndicator.anchoredPosition.x;
//         float greenLeftX = greenArea.anchoredPosition.x - greenArea.rect.width * greenArea.pivot.x;
//         float greenRightX = greenArea.anchoredPosition.x + greenArea.rect.width * (1f - greenArea.pivot.x);
//         return indicatorX >= greenLeftX && indicatorX <= greenRightX;
//     }

//     void ChangeProgress(float value)
//     {
//         progressValue = Mathf.Clamp(progressValue + value, 0f, maxProgressScore);
//         RefreshProgress();

//         if(progressValue >= targetProgressScore)
//             EndCook(true);
//     }

//     void RefreshProgress()
//     {
//         float progress = targetProgressScore <= 0f ? 0f : progressValue / targetProgressScore;
//         progress = Mathf.Clamp01(progress);
//         progressBar.fillAmount = progress;

//         RectTransform progressRect = progressBar.rectTransform;
//         progressRect.anchorMin = Vector2.zero;
//         progressRect.anchorMax = new Vector2(1f, progress);
//         progressRect.offsetMin = Vector2.zero;
//         progressRect.offsetMax = Vector2.zero;
//     }

//     void RefreshCountDown(int time)
//     {
//         countDownText.SetVar(LocalizeVarSet.MiniGame1Kitchen.CountDownTime, time);
//     }

//     void ResetRound()
//     {
//         RectTransform moveArea = (RectTransform)moveIndicator.parent;

//         // 指示器回到最左侧，向右开始移动
//         Vector2 indicatorPos = moveIndicator.anchoredPosition;
//         indicatorPos.x = GetMoveLeftX(moveArea);
//         moveIndicator.anchoredPosition = indicatorPos;
//         moveDir = 1;

//         // 绿色区域宽度回到最小，向变宽方向开始变化
//         Vector2 greenSize = greenArea.sizeDelta;
//         greenSize.x = greenMinWidth;
//         greenArea.sizeDelta = greenSize;
//         greenWidthDir = 1;

//         // 绿色区域回到最右侧，向左开始移动
//         Vector2 greenPos = greenArea.anchoredPosition;
//         greenPos.x = GetGreenRightLimit(moveArea);
//         greenArea.anchoredPosition = greenPos;
//         greenDir = -1;
//     }

//     float GetGreenRightLimit(RectTransform moveArea)
//     {
//         // 绿色区域右沿紧贴移动区域右边界
//         float areaRightX = moveArea.rect.width * (1f - moveArea.pivot.x);
//         float greenRightExtent = greenArea.rect.width * (1f - greenArea.pivot.x);
//         return areaRightX - greenRightExtent;
//     }

//     float GetGreenLeftLimit(RectTransform moveArea)
//     {
//         // 绿色区域左沿紧贴移动区域左边界
//         float areaLeftX = -moveArea.rect.width * moveArea.pivot.x;
//         float greenLeftExtent = greenArea.rect.width * greenArea.pivot.x;
//         return areaLeftX + greenLeftExtent;
//     }

//     float GetMoveLeftX(RectTransform moveArea)
//     {
//         return -moveArea.rect.width * moveArea.pivot.x + moveIndicator.rect.width * moveIndicator.pivot.x;
//     }

//     float GetMoveRightX(RectTransform moveArea)
//     {
//         return moveArea.rect.width * (1f - moveArea.pivot.x) - moveIndicator.rect.width * (1f - moveIndicator.pivot.x);
//     }

//     void EndCook(bool isSuccess)
//     {
//         if(isEnded)
//             return;

//         isEnded = true;
//         if(cookCt != null)
//         {
//             StopCoroutine(cookCt);
//             cookCt = null;
//         }

//         opTip.SetActive(false);
//         if(!isSuccess)
//             RefreshCountDown(0);

//         gameObject.SetActive(false);
//         float timeLeftRate = countDownTime <= 0 ? 0f : Mathf.Clamp01(timeLeft / countDownTime);
//         OnEnd?.Invoke(isSuccess, config.GetCookQuality(isSuccess, timeLeftRate));
//     }
// }
