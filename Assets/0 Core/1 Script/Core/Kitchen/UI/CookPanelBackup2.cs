// using TMPro;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.InputSystem;
// using UnityEngine.UI;
// using System;
// using UnityEngine.Localization.Components;

// namespace XFramework
// {
//     public class CookPanel : MonoBehaviour
//     {   // 做菜面板
//         [SerializeField] RectTransform moveIndicator;
//         [SerializeField] RectTransform greenArea;       // 绿色判定区域模板
//         [SerializeField] GameObject opTip;
//         [SerializeField] LocalizeStringEvent countDownText;
//         [SerializeField] Image progressBar;
//         [SerializeField] int countDownTime;
//         [SerializeField] float indicatorMoveSpeed;
//         [SerializeField] int greenAreaCount = 4;
//         [SerializeField] float greenMinWidth;
//         [SerializeField] float greenMaxWidth;
//         [SerializeField] float startProgressScore;
//         [SerializeField] float maxProgressScore = 100f;
//         [SerializeField] float targetProgressScore = 100f;
//         [SerializeField] float greenAddScore = 10f;
//         [SerializeField] float orangeSubScore = 5f;

//         public event Action<bool, CookQuality> OnEnd;

//         readonly List<RectTransform> greenAreas = new ();
//         Coroutine cookCt;
//         MiniGameCookConfig config;
//         float progressValue;
//         float timeLeft;
//         int moveDir = 1;
//         bool isEnded;

//         public void Init(MiniGameCookConfig config)
//         {
//             this.config = config;
//             countDownTime = config.CountDownTime;
//             indicatorMoveSpeed = config.IndicatorMoveSpeed;
//             greenAreaCount = config.GreenAreaCount;
//             greenMinWidth = config.GreenMinWidth;
//             greenMaxWidth = config.GreenMaxWidth;
//             startProgressScore = config.StartProgressScore;
//             maxProgressScore = config.MaxProgressScore;
//             targetProgressScore = config.TargetProgressScore;
//             greenAddScore = config.GreenAddScore;
//             orangeSubScore = config.OrangeSubScore;

//             gameObject.SetActive(true);
//             opTip.SetActive(true);
//             progressValue = startProgressScore;
//             isEnded = false;

//             RefreshProgress();
//             RefreshCountDown(countDownTime);
//             ResetIndicator();
//             SpawnGreenAreas();

//             if(cookCt != null)
//                 StopCoroutine(cookCt);

//             cookCt = StartCoroutine(CookCt());
//         }

//         void OnDisable()
//         {
//             if(cookCt != null)
//                 StopCoroutine(cookCt);
//             cookCt = null;
//         }

//         IEnumerator CookCt()
//         {
//             timeLeft = countDownTime;
//             RectTransform blockArea = (RectTransform)moveIndicator.parent;
//             float leftX = GetMoveLeftX(blockArea);
//             float rightX = GetMoveRightX(blockArea);

//             while(timeLeft > 0f && !isEnded)
//             {
//                 MoveIndicator(leftX, rightX);

//                 if(Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
//                 {
//                     OnSpacePressed();
//                     if(isEnded)
//                         yield break;
//                 }

//                 timeLeft -= Time.deltaTime;
//                 RefreshCountDown(Mathf.CeilToInt(Mathf.Max(timeLeft, 0f)));

//                 yield return null;
//             }

//             EndCook(false);
//         }

//         void MoveIndicator(float leftX, float rightX)
//         {
//             Vector2 pos = moveIndicator.anchoredPosition;
//             pos.x += indicatorMoveSpeed * moveDir * Time.deltaTime;

//             // 指示器在左右边界间来回移动（碰到边界反向），按空格不重置
//             if(pos.x >= rightX)
//             {
//                 pos.x = rightX;
//                 moveDir = -1;
//             }
//             else if(pos.x <= leftX)
//             {
//                 pos.x = leftX;
//                 moveDir = 1;
//             }

//             moveIndicator.anchoredPosition = pos;
//         }

//         void OnSpacePressed()
//         {
//             RectTransform hitArea = GetIndicatorGreenArea();
//             if(hitArea != null)
//             {
//                 // 指示器在绿色判定区域内：该区域消失，进度上升
//                 greenAreas.Remove(hitArea);
//                 Destroy(hitArea.gameObject);
//                 ChangeProgress(greenAddScore);

//                 // 立即在别处补一条，保持数量不变
//                 if(!isEnded)
//                     SpawnOneGreen();
//             }
//             else
//             {
//                 // 未命中：进度下降
//                 ChangeProgress(-orangeSubScore);
//             }
//         }

//         RectTransform GetIndicatorGreenArea()
//         {
//             float indicatorX = moveIndicator.anchoredPosition.x;
//             for(int i = 0; i < greenAreas.Count; i++)
//             {
//                 RectTransform area = greenAreas[i];
//                 if(area == null)
//                     continue;

//                 float leftX = area.anchoredPosition.x - area.rect.width * area.pivot.x;
//                 float rightX = area.anchoredPosition.x + area.rect.width * (1f - area.pivot.x);
//                 if(indicatorX >= leftX && indicatorX <= rightX)
//                     return area;
//             }
//             return null;
//         }

//         void SpawnGreenAreas()
//         {
//             // 清理上一批
//             for(int i = 0; i < greenAreas.Count; i++)
//             {
//                 if(greenAreas[i] != null)
//                     Destroy(greenAreas[i].gameObject);
//             }
//             greenAreas.Clear();

//             // greenArea 作为模板，自身隐藏
//             greenArea.gameObject.SetActive(false);

//             for(int i = 0; i < greenAreaCount; i++)
//                 SpawnOneGreen();

//             // 指示器置于最上层，避免被绿色区域遮挡
//             moveIndicator.SetAsLastSibling();
//         }

//         // 随机生成一条绿色判定区域（长短随机，尽量避开已有区域，出现在别的位置）
//         void SpawnOneGreen()
//         {
//             RectTransform blockArea = (RectTransform)moveIndicator.parent;
//             greenArea.gameObject.SetActive(false);

//             float areaWidth = blockArea.rect.width;
//             float areaLeftX = -areaWidth * blockArea.pivot.x;
//             float maxWidth = Mathf.Min(greenMaxWidth, areaWidth);
//             float minWidth = Mathf.Min(greenMinWidth, maxWidth);

//             float width = UnityEngine.Random.Range(minWidth, maxWidth);
//             float centerX = 0f;
//             for(int t = 0; t < 20; t++)
//             {
//                 width = UnityEngine.Random.Range(minWidth, maxWidth);
//                 float half = width * 0.5f;
//                 centerX = UnityEngine.Random.Range(areaLeftX + half, areaLeftX + areaWidth - half);
//                 if(!OverlapsExisting(centerX, width))
//                     break;
//             }

//             RectTransform area = Instantiate(greenArea, blockArea);
//             area.gameObject.SetActive(true);

//             Vector2 size = area.sizeDelta;
//             size.x = width;
//             area.sizeDelta = size;

//             Vector2 pos = area.anchoredPosition;
//             pos.x = centerX;
//             area.anchoredPosition = pos;

//             greenAreas.Add(area);

//             // 保持指示器在最上层
//             moveIndicator.SetAsLastSibling();
//         }

//         bool OverlapsExisting(float centerX, float width)
//         {
//             float half = width * 0.5f;
//             float left = centerX - half;
//             float right = centerX + half;
//             for(int i = 0; i < greenAreas.Count; i++)
//             {
//                 RectTransform a = greenAreas[i];
//                 if(a == null)
//                     continue;

//                 float aLeft = a.anchoredPosition.x - a.rect.width * a.pivot.x;
//                 float aRight = a.anchoredPosition.x + a.rect.width * (1f - a.pivot.x);
//                 if(right >= aLeft && left <= aRight)
//                     return true;
//             }
//             return false;
//         }

//         void ChangeProgress(float value)
//         {
//             progressValue = Mathf.Clamp(progressValue + value, 0f, maxProgressScore);
//             RefreshProgress();

//             if(progressValue >= targetProgressScore)
//                 EndCook(true);
//         }

//         void RefreshProgress()
//         {
//             float progress = targetProgressScore <= 0f ? 0f : progressValue / targetProgressScore;
//             progress = Mathf.Clamp01(progress);
//             progressBar.fillAmount = progress;

//             RectTransform progressRect = progressBar.rectTransform;
//             progressRect.anchorMin = Vector2.zero;
//             progressRect.anchorMax = new Vector2(1f, progress);
//             // progressRect.offsetMin = Vector2.zero;
//             // progressRect.offsetMax = Vector2.zero;
//         }

//         void RefreshCountDown(int time)
//         {
//             countDownText.SetVar(LocVarSet.MiniGame.CountDownTime, time);
//         }

//         void ResetIndicator()
//         {
//             RectTransform blockArea = (RectTransform)moveIndicator.parent;
//             Vector2 pos = moveIndicator.anchoredPosition;
//             pos.x = GetMoveLeftX(blockArea);
//             moveIndicator.anchoredPosition = pos;
//             moveDir = 1;
//         }

//         float GetMoveLeftX(RectTransform moveArea)
//         {
//             return -moveArea.rect.width * moveArea.pivot.x + moveIndicator.rect.width * moveIndicator.pivot.x;
//         }

//         float GetMoveRightX(RectTransform moveArea)
//         {
//             return moveArea.rect.width * (1f - moveArea.pivot.x) - moveIndicator.rect.width * (1f - moveIndicator.pivot.x);
//         }

//         void EndCook(bool isSuccess)
//         {
//             if(isEnded)
//                 return;

//             isEnded = true;
//             if(cookCt != null)
//             {
//                 StopCoroutine(cookCt);
//                 cookCt = null;
//             }

//             opTip.SetActive(false);
//             if(!isSuccess)
//                 RefreshCountDown(0);

//             gameObject.SetActive(false);
//             float timeLeftRate = countDownTime <= 0 ? 0f : Mathf.Clamp01(timeLeft / countDownTime);
//             OnEnd?.Invoke(isSuccess, config.GetCookQuality(isSuccess, timeLeftRate));
//         }
//     }
// }
