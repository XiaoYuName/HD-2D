using UnityEngine;
using XFramework;

public partial class SewingMachineUI : UIBase
{
    private SewingMachineGameData Setting;
    private Sprite CursorTexture;
    
    public override void Init()
    {
        InitAutoBind();

        Setting = LoadAsset<SewingMachineGameData>(AssetKeys.SewingMachineGameDataPath);
        CursorTexture = LoadAsset<Sprite>(AssetKeys.ShouPath);
        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(btnTuichu,Close,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        Cursor.SetCursor(CursorTexture.texture, Vector2.zero, CursorMode.Auto);
    }


    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }


    public void SetData(CharacterBag characterBag,ClothingBag clothingBag)
    {
        GenerateSlotParent();
    }

    private void GenerateSlotParent()
    {
        // mouseParent 是拉伸锚点(0,0)-(1,1)，rect 需要布局跑完才有正确尺寸，
        // 否则刚 Open 的第一帧拿到的是 0，随机范围会全部塌到中心
        Canvas.ForceUpdateCanvases();
        Rect area = mouseParent.rect;

        for (int i = 0; i < Setting.GenerateNumber; i++)
        {
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.SewingMachineSlotParentPath);
            var rt = (RectTransform)obj.transform;

            // worldPositionStays = false，避免对象池复用时把世界缩放/位置带进来
            rt.SetParent(mouseParent, false);
            rt.localScale = Vector3.one;

            // 统一锚点与轴心到中心，这样 anchoredPosition 就是"相对父物体中心的偏移"
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

            RandomizeInsideParent(rt, area);
        }
    }

    /// <summary>
    /// 在父物体矩形范围内随机一个位置与旋转，保证旋转后的四个角都不越界
    /// </summary>
    private void RandomizeInsideParent(RectTransform rt, Rect area)
    {
        // 1. 先随机旋转
        float angle = Random.Range(Setting.RotationRange.x, Setting.RotationRange.y);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);

        // 2. 求旋转后的外接矩形(AABB)尺寸：
        //    w' = w*|cos| + h*|sin| ，h' = w*|sin| + h*|cos|
        float rad = angle * Mathf.Deg2Rad;
        float cos = Mathf.Abs(Mathf.Cos(rad));
        float sin = Mathf.Abs(Mathf.Sin(rad));
        Vector2 size = rt.rect.size;
        float rotatedW = size.x * cos + size.y * sin;
        float rotatedH = size.x * sin + size.y * cos;

        // 3. 可放置半径 = (父物体尺寸 - 旋转后尺寸)/2 - 内边距；放不下就夹到 0（居中）
        float pad = Setting.BorderPadding;
        float rangeX = Mathf.Max(0f, (area.width - rotatedW) * 0.5f - pad);
        float rangeY = Mathf.Max(0f, (area.height - rotatedH) * 0.5f - pad);

        rt.anchoredPosition = new Vector2(
            Random.Range(-rangeX, rangeX),
            Random.Range(-rangeY, rangeY));
    }
}
