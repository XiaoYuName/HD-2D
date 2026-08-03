using System;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework
{
    public readonly struct SprayPaintGameResult
    {
        public readonly long ClothingId;
        public readonly bool IsPassed;

        public SprayPaintGameResult(long clothingId, bool isPassed)
        {
            ClothingId = clothingId;
            IsPassed = isPassed;
        }
    }

    /// <summary>
    /// 喷漆小游戏：点服饰片 → 右侧弹出喷枪颜色 → 选色即判定该片对错。
    /// 选错立刻失败弹窗；所有片都喷对才算通关。
    /// </summary>
    public class DressMakingSprayPaintGamePanel : UIBase
    {
        [SerializeField] Button closeBtn;
        [SerializeField] Image referencePicture;
        [SerializeField] RectTransform clothContainerArea;
        [SerializeField] GameObject colorSelectionArea;
        [SerializeField] Transform paintSprayGunButtonContainer;
        [SerializeField] PaintSprayGunButton paintSprayGunButtonPrefab;
        [SerializeField] TextMeshProUGUI progressText;
        [SerializeField] CanvasGroup completeTipCg;
        [SerializeField] CanvasGroup errorTipCg;
        [SerializeField] Image preview;
        [SerializeField, Min(0f)] float tipDuration = 0.9f;
        [SerializeField, Min(0f)] float failDelay = 1.2f;
        [SerializeField, Min(0f)] float successDelay = 0.8f;

        readonly List<PaintSprayGunButton> pigmentButtons = new();
        readonly List<SprayPaintClothPiece> pieces = new();
        readonly List<int> pieceAnswers = new();

        long clothingId;
        SprayPaintGameData gameData;
        CharacterBag characterBag;
        ClothingBag clothingBag;
        Action<SprayPaintGameResult> completedCallback;
        GameObject clothInstance;
        int selectedPieceIndex = -1;
        int resolvedCount;
        bool isFinished;

        public override void Init()
        {
            closeBtn.onClick.AddListener(Close);
            colorSelectionArea.SetActive(false);
            SetFeedbackVisible(completeTipCg, false);
            SetFeedbackVisible(errorTipCg, false);
        }

        /// <summary>
        /// 供服装制作模块传入当前服装，一件服装对应配表里的一条喷漆配置。
        /// </summary>
        public void SetData(CharacterBag character, ClothingBag clothing,
            Action<SprayPaintGameResult> onCompleted = null)
        {
            characterBag = character;
            clothingBag = clothing;
            clothingId = clothing.clothingID;
            completedCallback = onCompleted;
            StartTask();
        }

        /// <summary>
        /// 无角色/服装上下文的直接调用（临时测试入口），通关不会走解锁服装。
        /// </summary>
        public bool SetClothing(long targetClothingId, Action<SprayPaintGameResult> onCompleted = null)
        {
            characterBag = null;
            clothingBag = null;
            clothingId = targetClothingId;
            completedCallback = onCompleted;
            return StartTask();
        }

        public override void Close()
        {
            completedCallback = null;
            ClearRound();
            base.Close();
        }

        bool StartTask()
        {
            gameData = LubanManager.Instance.TbSprayPaintGameData.GetOrDefault(clothingId);
            if (!IsGameDataValid(gameData))
            {
                Debug.LogError($"[SprayPaint] 未找到或配置错误的喷漆配置。ClothingId={clothingId}");
                return false;
            }

            ClearRound();
            RefreshReferencePicture();
            RefreshPigmentButtons();
            return SpawnCloth();
        }

        static bool IsGameDataValid(SprayPaintGameData data)
        {
            return data != null
                   && !string.IsNullOrWhiteSpace(data.ReferenceSpritePath)
                   && !string.IsNullOrWhiteSpace(data.ClothPrefabPath)
                   && data.PigmentColorList is { Count: > 0 }
                   && data.PieceAnswerList is { Count: > 0 };
        }

        void ClearRound()
        {
            pieces.Clear();
            pieceAnswers.Clear();
            if (clothInstance != null)
            {
                Destroy(clothInstance);
                clothInstance = null;
            }

            ResetProgress();
        }

        void ResetProgress()
        {
            isFinished = false;
            selectedPieceIndex = -1;
            resolvedCount = 0;
            colorSelectionArea.SetActive(false);
            SetFeedbackVisible(completeTipCg, false);
            SetFeedbackVisible(errorTipCg, false);
            RefreshPigmentSelection(-1);
            RefreshProgressText();
        }

        /// <summary>底部进度：已喷对的片数 / 总片数。</summary>
        void RefreshProgressText()
        {
            int correctCount = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].IsResolved && pieces[i].IsCorrect)
                {
                    correctCount++;
                }
            }

            progressText.text = $"{correctCount}/{pieces.Count}";
        }

        void RefreshReferencePicture()
        {
            referencePicture.sprite = LoadAsset<Sprite>(gameData.ReferenceSpritePath);
        }

        bool SpawnCloth()
        {
            GameObject prefab = LoadAsset<GameObject>(gameData.ClothPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[SprayPaint] 服饰片预制体加载失败。服装={gameData.ClothingID}, Path={gameData.ClothPrefabPath}");
                return false;
            }

            clothInstance = Instantiate(prefab, clothContainerArea);
            RectTransform clothRtf = (RectTransform)clothInstance.transform;
            clothRtf.anchoredPosition = Vector2.zero;
            clothRtf.localScale = Vector3.one;

            SprayPaintClothGroup group = clothInstance.GetComponent<SprayPaintClothGroup>();
            if (group == null || group.Pieces == null || group.Pieces.Length == 0)
            {
                Debug.LogError($"[SprayPaint] 服饰片预制体缺少 SprayPaintClothGroup 或未配置服饰片。Path={gameData.ClothPrefabPath}");
                return false;
            }

            if (group.Pieces.Length != gameData.PieceAnswerList.Count)
            {
                Debug.LogError($"[SprayPaint] 服装 {gameData.ClothingID} 的 PieceAnswerList({gameData.PieceAnswerList.Count}) 与预制体服饰片数量({group.Pieces.Length})不一致，按较小值处理。");
            }

            int pieceCount = Mathf.Min(group.Pieces.Length, gameData.PieceAnswerList.Count);
            for (int i = 0; i < pieceCount; i++)
            {
                int answer = gameData.PieceAnswerList[i];
                if (answer < 0 || answer >= gameData.PigmentColorList.Count)
                {
                    Debug.LogError($"[SprayPaint] 服装 {gameData.ClothingID} 第 {i} 片的正确颜料索引 {answer} 越界。");
                    return false;
                }

                SprayPaintClothPiece piece = group.Pieces[i];
                piece.SetData(i, SelectPiece);
                pieces.Add(piece);
                pieceAnswers.Add(answer);
            }

            RefreshProgressText();
            return true;
        }

        void RefreshPigmentButtons()
        {
            int pigmentCount = gameData.PigmentColorList.Count;
            while (pigmentButtons.Count < pigmentCount)
            {
                PaintSprayGunButton button = Instantiate(paintSprayGunButtonPrefab, paintSprayGunButtonContainer);
                pigmentButtons.Add(button);
            }

            for (int i = 0; i < pigmentButtons.Count; i++)
            {
                bool isAvailable = i < pigmentCount;
                PaintSprayGunButton button = pigmentButtons[i];
                button.gameObject.SetActive(isAvailable);
                if (isAvailable)
                {
                    button.SetData(i, ToColor(gameData.PigmentColorList[i]), SelectPigment);
                }
            }
        }

        /// <summary>
        /// 配表里颜料填 0-255 的 RGB。
        /// </summary>
        static Color ToColor(vector3 value)
        {
            return new Color(value.X / 255f, value.Y / 255f, value.Z / 255f, 1f);
        }

        /// <summary>
        /// 点服饰片：弹出右侧喷枪颜色面板，等待选色。
        /// </summary>
        void SelectPiece(int pieceIndex)
        {
            if (isFinished || pieceIndex < 0 || pieceIndex >= pieces.Count || pieces[pieceIndex].IsResolved)
            {
                return;
            }

            selectedPieceIndex = pieceIndex;
            pieces[pieceIndex].PlaySelectedFeedback();
            RefreshPigmentSelection(-1);
            SetFeedbackVisible(completeTipCg, false);
            SetFeedbackVisible(errorTipCg, false);
            colorSelectionArea.SetActive(true);
        }

        /// <summary>
        /// 选喷枪颜色：立刻判定当前服饰片对错，不可回改。
        /// </summary>
        void SelectPigment(int pigmentIndex)
        {
            if (isFinished || selectedPieceIndex < 0)
            {
                return;
            }

            int pieceIndex = selectedPieceIndex;
            selectedPieceIndex = -1;
            RefreshPigmentSelection(pigmentIndex);

            int answer = pieceAnswers[pieceIndex];
            bool isCorrect = pigmentIndex == answer;
            pieces[pieceIndex].ShowResult(isCorrect, ToColor(gameData.PigmentColorList[pigmentIndex]));

            resolvedCount++;
            RefreshProgressText();
            colorSelectionArea.SetActive(false);
            ShowTip(isCorrect);

            if (!isCorrect)
            {
                // 喷错一片就整局失败，留一点时间看清错误表现再弹窗。
                isFinished = true;
                Tween.Delay(this, failDelay, _ => Settle(false));
                return;
            }

            if (resolvedCount >= pieces.Count)
            {
                isFinished = true;
                Tween.Delay(this, successDelay, _ => Settle(true));
            }
        }

        void RefreshPigmentSelection(int pigmentIndex)
        {
            for (int i = 0; i < pigmentButtons.Count; i++)
            {
                pigmentButtons[i].SwitchState(i == pigmentIndex);
            }
        }

        void ShowTip(bool isCorrect)
        {
            CanvasGroup tipCg = isCorrect ? completeTipCg : errorTipCg;
            SetFeedbackVisible(completeTipCg, false);
            SetFeedbackVisible(errorTipCg, false);
            SetFeedbackVisible(tipCg, true);
            Tween.Delay(this, tipDuration, _ => SetFeedbackVisible(tipCg, false));
        }

        void Settle(bool isPassed)
        {
            SetFeedbackVisible(completeTipCg, false);
            SetFeedbackVisible(errorTipCg, false);

            completedCallback?.Invoke(new SprayPaintGameResult(gameData.ClothingID, isPassed));

            if (!isPassed)
            {
                // 失败窗自带黑底遮罩挡住下面的点击，玩法面板不用关：重试时窗口自己会关。
                UIUtility.PopFailWindow(true, RetryGame, Close);
                return;
            }

            CharacterManager.Instance.ClothingUlock(characterBag.CharacterID, clothingBag.clothingID);
            UIUtility.PopClothingMinGameComplete(characterBag, clothingBag,ClothingMinGameType.SprayPaint,Close);
            UISystem.Instance.GetUI<GarmentMakingUI>(nameof(GarmentMakingUI)).OptionClothing();
        }

        /// <summary>
        /// 重试：配置和服饰片不变，只把所有片恢复成未上色状态。
        /// </summary>
        void RetryGame()
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                pieces[i].ResetPiece();
            }

            ResetProgress();
        }

        static void SetFeedbackVisible(CanvasGroup canvasGroup, bool isVisible)
        {
            canvasGroup.gameObject.SetActive(isVisible);
            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
