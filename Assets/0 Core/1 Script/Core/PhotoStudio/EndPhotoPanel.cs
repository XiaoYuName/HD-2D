using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

// 拍照结算（闪白）面板：
//   1. 闪白模拟拍照效果（whiteImage alpha 1 → 0）
//   2. 展示拍出的照片（评价用图/角色图）与质量档位精灵图（多语言图片）
//   3. 闪白结束后绑定空格/点击输入，玩家关闭后回调 OnClosed（由 PhotoStudioManager 切到结算面板）
public class EndPhotoPanel : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] PhotoSceneConfigPanel photoSceneConfigPanel;
    [SerializeField] Image whiteImage;                  // 全屏闪白层
    [SerializeField] TextMeshProUGUI dateText;          // 日期/署名（可选）
    [SerializeField] LocalizeSpriteEvent qualityTierSprite; // 质量档位多语言精灵（OnUpdateAsset 在 Inspector 里连到目标 Image）
    [SerializeField] TextMeshProUGUI hintText;

    [Header("配置")]
    [SerializeField] float flashDuration = 0.6f;        // 闪白时长

    public event Action OnClose;

    bool waitingClose;
    Coroutine flashCt;

    // 由 PhotoStudioManager 在进入 Capture 状态时调用
    public void Init(PhotoStudioPhotoResult result)
    {
        gameObject.SetActive(true);
        waitingClose = false;

        // 用配置面板按四项ID还原拍出的画面（背景 + 角色 + 光影）作为照片
        PhotoSceneConfigInfo cfg = result.config;
        photoSceneConfigPanel.Init(cfg.bgId, cfg.lightingId, cfg.postureId, cfg.clothesId);

        // 质量档位精灵图（多语言图片）
        SetQualityTierSprite(result.qualityTierConfig);
        hintText.gameObject.SetActive(false);
        // 开始闪白
        if(flashCt != null)
            StopCoroutine(flashCt);
        flashCt = StartCoroutine(FlashCt());
    }

    // 按档位名 key 切换多语言精灵；LocalizeSpriteEvent 会自动异步加载并在语言切换时刷新
    void SetQualityTierSprite(PhotoQualityTierConfig tierCfg)
    {
        qualityTierSprite.AssetReference.SetReference(LocTableSet.PhotoStudioSprite, tierCfg.name);
    }

    IEnumerator FlashCt()
    {
        whiteImage.gameObject.SetActive(true);
        float t = 0f;
        Color c = whiteImage.color;
        while(t < flashDuration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Clamp01(1f - t / flashDuration);
            whiteImage.color = c;
            yield return null;
        }
        c.a = 0f;
        whiteImage.color = c;
        whiteImage.gameObject.SetActive(false);
        flashCt = null;
        EnableCloseInput();
    }

    // 闪白结束后才允许关闭：绑定空格/点击
    void EnableCloseInput()
    {
        waitingClose = true;
        hintText.gameObject.SetActive(true); 

        PlayerInputManager.Instance.OnSpace += Close;
        PlayerInputManager.Instance.OnClick += Close;
    }

    void DisableCloseInput()
    {
        PlayerInputManager.Instance.OnSpace -= Close;
        PlayerInputManager.Instance.OnClick -= Close;
    }

    // 玩家按空格/点击关闭，或外部直接调用
    public void Close()
    {
        if(!waitingClose)
            return;
        waitingClose = false;
        DisableCloseInput();

        gameObject.SetActive(false);
        OnClose?.Invoke();
    }

    void OnDisable()
    {
        // 兜底解绑，避免面板被外部关闭时残留订阅
        DisableCloseInput();
        waitingClose = false;
        if(flashCt != null)
        {
            StopCoroutine(flashCt);
            flashCt = null;
        }
    }
}
