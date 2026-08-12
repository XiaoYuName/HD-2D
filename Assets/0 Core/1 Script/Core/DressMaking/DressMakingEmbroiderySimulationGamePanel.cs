using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework
{
    public readonly struct EmbroideryGameResult
    {
        public readonly long ClothingId;
        public readonly bool IsPassed;

        public EmbroideryGameResult(long clothingId, bool isPassed)
        {
            ClothingId = clothingId;
            IsPassed = isPassed;
        }
    }

    /// <summary>刺绣小游戏面板：加载关卡配置、挂载运行时棋盘并向调用方回传结果。</summary>
    public class DressMakingEmbroiderySimulationGamePanel : UIBase
    {
        [SerializeField] Button exitButton;
        [SerializeField] DressMakingEmbroiderySimulationGameConfig config;
        [SerializeField] RectTransform boardContainer;
        [SerializeField] Image preview;

        [ShowInInspector] DressMakingEmbroiderySimulationGameBoard curBoard;
        [ShowInInspector] DressMakingEmbroiderySimulationGameBoard currentBoardPrefab;
        Action<bool> simpleCompletedCallback;
        Action<EmbroideryGameResult> completedCallback;
        long clothingId;

        public long ClothingId => clothingId;
        public bool IsPlaying => curBoard != null && !curBoard.IsFinished;

        public override void Init()
        {
            exitButton?.onClick.AddListener(Close);
        }

        /// <summary>
        /// 以服装 Id 开一局，结算回调带完整结果。
        /// 小游戏已经不参与服装解锁（解锁走服装打板 + 服装上身），所以不需要角色 / 服装背包数据。
        /// </summary>
        public void SetData(long targetClothingId, Action<EmbroideryGameResult> onCompleted = null)
        {
            clothingId = targetClothingId;
            completedCallback = onCompleted;
            simpleCompletedCallback = null;
            StartGame(config.DataDict[clothingId]);
        }

        /// <summary>以服装 Id 开始对应的刺绣关卡。</summary>
        public void SetClothing(long targetClothingId, Action<bool> onCompleted = null)
        {
            clothingId = targetClothingId;
            simpleCompletedCallback = onCompleted;
            completedCallback = null;
            StartGame(config.DataDict[clothingId]);
        }

        /// <summary>直接注入关卡数据，便于测试或未使用总配置的调用方。</summary>
        public void SetLevel(DressMakingEmbroideryLevelData level, Action<bool> onCompleted = null)
        {
            clothingId = level.ClothingId;
            simpleCompletedCallback = onCompleted;
            completedCallback = null;
            StartGame(level);
        }

        public override void Close()
        {
            simpleCompletedCallback = null;
            completedCallback = null;
            if (curBoard != null)
            {
                curBoard.StopGame();
            }

            base.Close();
        }

        void StartGame(DressMakingEmbroideryLevelData level)
        {
            RefreshPreview(level);
            DressMakingEmbroiderySimulationGameBoard board = GetBoard(level);
            if (config != null)
                board.MustStartFromNumberBlock = config.MustStartFromNumberBlock;
            if (!board.StartGame(level, CompleteGame, GetUICamera()))
                return;

            FitBoard(board);
        }

        /// <summary>预览图取自关卡数据（配置字典按服装 Id 一一对应），未配置时隐藏。</summary>
        void RefreshPreview(DressMakingEmbroideryLevelData level)
        {
            Sprite sprite = string.IsNullOrEmpty(level?.PreviewSpritePath)
                ? null
                : LoadAsset<Sprite>(level.PreviewSpritePath);
            preview.sprite = sprite;
            preview.gameObject.SetActive(sprite != null);
        }

        DressMakingEmbroiderySimulationGameBoard GetBoard(DressMakingEmbroideryLevelData level)
        {
            DressMakingEmbroiderySimulationGameBoard desiredPrefab = level.LevelPrefab.GetComponent<DressMakingEmbroiderySimulationGameBoard>();

            if (curBoard != null && currentBoardPrefab == desiredPrefab)
                return curBoard;

            if (curBoard != null)
                Destroy(curBoard.gameObject);

  
            currentBoardPrefab = desiredPrefab;
            curBoard = Instantiate(desiredPrefab, boardContainer);
            return curBoard;
        }

        static void FitBoard(DressMakingEmbroiderySimulationGameBoard board)
        {
            if (board.transform is not RectTransform boardRect
                || boardRect.parent is not RectTransform container
                || boardRect.rect.width <= 0f
                || boardRect.rect.height <= 0f)
                return;

            Canvas.ForceUpdateCanvases();
            float scale = Mathf.Min(
                container.rect.width / boardRect.rect.width,
                container.rect.height / boardRect.rect.height);
            boardRect.anchorMin = new Vector2(0.5f, 0.5f);
            boardRect.anchorMax = new Vector2(0.5f, 0.5f);
            boardRect.pivot = new Vector2(0.5f, 0.5f);
            boardRect.anchoredPosition = Vector2.zero;
            boardRect.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        }

        Camera GetUICamera()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

        void CompleteGame(bool isPassed)
        {
            QuestEventBus.ReportMiniGameFinished(MiniGameType.Embroidery, isPassed);   // 任务系统：本局结算上报

            simpleCompletedCallback?.Invoke(isPassed);
            completedCallback?.Invoke(new (clothingId, isPassed));

            if (isPassed)
            {
                UIUtility.PopClothingMinGameComplete(Close);
            }
        }
    }
}
