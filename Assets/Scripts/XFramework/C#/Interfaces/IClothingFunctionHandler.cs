using UnityEngine;
using UnityEngine.Scripting;
using XFramework;

/// <summary>
/// 服装小游戏入口。实现类由 CharacterManager 反射自动注册，所以：
/// 1. 必须有无参构造；
/// 2. 必须挂 [Preserve] —— 本工程用 IL2CPP，只被反射引用的类型会被托管代码剥离干掉；
/// 3. 一个 MinGameType 只能有一个实现。
/// 新增小游戏：在 Luban 的 __enums__ 表加枚举值，再加一个 Handler 类，其它地方不用动。
/// </summary>
public interface IClothingFunctionHandler
{
    ClothingMinGameType MinGameType { get; }
    void Execute(CharacterBag characterBag,ClothingBag clothingBag);
}

/// <summary>
/// 熨斗小游戏
/// </summary>
[Preserve]
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

/// <summary>
/// 拼图小游戏
/// </summary>
[Preserve]
public class IPuzzleFunctionHandler : IClothingFunctionHandler
{
    public ClothingMinGameType MinGameType => ClothingMinGameType.Puzzle;
    public void Execute(CharacterBag characterBag, ClothingBag clothingBag)
    {
        UISystem.Instance.OpenUI<PuzzleUI>("PuzzleUI").SetData(characterBag,clothingBag);
    }
}

/// <summary>
/// 滴胶固化小游戏
/// </summary>
[Preserve]
public class IMedicinalSolutionHandler : IClothingFunctionHandler
{
    public ClothingMinGameType MinGameType => ClothingMinGameType.MedicinalSolution;
    public void Execute(CharacterBag characterBag, ClothingBag clothingBag)
    {
        UISystem.Instance.OpenUI<MedicinalSolutionUI>("MedicinalSolutionUI").SetData(characterBag, clothingBag);
    }
}


/// <summary>
/// 喷漆小游戏。
/// </summary>
[Preserve]
public class SprayPaintFunctionHandler : IClothingFunctionHandler
{
    public ClothingMinGameType MinGameType => ClothingMinGameType.SprayPaint;

    public void Execute(CharacterBag characterBag, ClothingBag clothingBag)
    {
        UISystem.Instance.OpenUI<DressMakingSprayPaintGamePanel>(nameof(DressMakingSprayPaintGamePanel)).SetData(characterBag, clothingBag);
    }
}

/// <summary>
/// 刺绣填色小游戏。
/// </summary>
[Preserve]
public class EmbroideryFunctionHandler : IClothingFunctionHandler
{
    public ClothingMinGameType MinGameType => ClothingMinGameType.Embroidery;

    public void Execute(CharacterBag characterBag, ClothingBag clothingBag)
    {
        UISystem.Instance.OpenUI<DressMakingEmbroiderySimulationGamePanel>(nameof(DressMakingEmbroiderySimulationGamePanel)).SetData(characterBag, clothingBag);
    }
}
