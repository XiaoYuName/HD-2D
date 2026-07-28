using UnityEngine;
using XFramework;

public partial class MedicinalSolutionGameDataInfoUI : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    public void SetData(MedicinalSolutionData medicinalSolutionData, MedicinalSolutionSettingData setting)
    {
        if (medicinalSolutionData == null || setting == null)
        {
            Debug.LogError("配方数据或配置为空,无法刷新配方步骤提示");
            return;
        }

        // 步骤1:所需颜料。面板上只有两个图标位(yanliao1 + 号 yanliao5)
        var colorList = medicinalSolutionData.PaintTubeColorList;
        int colorCount = colorList != null ? colorList.Count : 0;

        yanliao1.gameObject.SetActive(colorCount > 0);
        if (colorCount > 0)
        {
            yanliao1.sprite = setting.GetColorIcon(colorList[0]);
        }

        // bindItem 就是 "+" 号,只有两种颜料时才显示
        bindItem.gameObject.SetActive(colorCount > 1);
        yanliao5.gameObject.SetActive(colorCount > 1);
        if (colorCount > 1)
        {
            yanliao5.sprite = setting.GetColorIcon(colorList[1]);
        }

        if (colorCount > 2)
        {
            Debug.LogWarning($"配方需要 {colorCount} 种颜料,但配方步骤提示只有 2 个图标位,多余的不会显示");
        }

        // 步骤2:所需毫升
        mlText.SetText($"{medicinalSolutionData.Ml}ml");

        // 步骤3:所需模具
        var moldIcon = setting.GetMoldIcon(medicinalSolutionData.MoldClass);
        ySIcon.gameObject.SetActive(moldIcon != null);
        ySIcon.sprite = moldIcon;
    }
}
