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