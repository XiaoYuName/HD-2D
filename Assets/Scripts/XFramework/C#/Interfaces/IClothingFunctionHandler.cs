using UnityEngine;
using XFramework;

public interface IClothingFunctionHandler
{
    ClothingMinGameType MinGameType { get; }
    void Execute(CharacterBag characterBag,ClothingBag clothingBag);
}

/// <summary>
/// 熨斗小游戏
/// </summary>
public class ISewingMachineFunctionHandler : IClothingFunctionHandler
{
    public ClothingMinGameType MinGameType => ClothingMinGameType.SewingMachine;

    public void Execute(CharacterBag characterBag, ClothingBag clothingBag)
    {
       var ui =  UISystem.Instance.OpenUI<SewingMachineUI>("SewingMachineUI");
       if (ui != null)
       {
           ui.SetData(characterBag,clothingBag);
       }
    }
}

public class IPuzzleFunctionHandler : IClothingFunctionHandler
{
    public ClothingMinGameType MinGameType => ClothingMinGameType.Puzzle;
    public void Execute(CharacterBag characterBag, ClothingBag clothingBag)
    {
        var ui = UISystem.Instance.OpenUI<PuzzleUI>("PuzzleUI");
        if (ui != null)
        {
            ui.SetData(characterBag,clothingBag);
        }
    }
}

public class IMedicinalSolutionHandler : IClothingFunctionHandler
{
    public ClothingMinGameType MinGameType => ClothingMinGameType.MedicinalSolution;
    public void Execute(CharacterBag characterBag, ClothingBag clothingBag)
    {
        var ui = UISystem.Instance.OpenUI<MedicinalSolutionUI>("MedicinalSolutionUI");
        if (ui != null)
        {
            ui.SetData(characterBag, clothingBag);
        }
    }
}

/// <summary>
/// 喷漆小游戏。
/// </summary>
public class SprayPaintFunctionHandler : IClothingFunctionHandler
{
    public ClothingMinGameType MinGameType => ClothingMinGameType.SprayPaint;

    public void Execute(CharacterBag characterBag, ClothingBag clothingBag)
    {
        // 结算(解锁服装/成功失败弹窗)在面板内部处理。
        DressMakingSprayPaintGamePanel ui = UISystem.Instance.OpenUI<DressMakingSprayPaintGamePanel>(
            "DressMakingSprayPaintGamePanel");
        if (!ui.SetData(characterBag, clothingBag))
        {
            ui.Close();
        }
    }
}
