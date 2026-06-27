using UnityEngine;
using XFramework;

public interface ICharacterFunctionHandler
{
    FunctionGroup FunctionType { get; }
    void Execute(CharacterData characterData);
}

public class DialogueFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.Dialogue;

    public void Execute(CharacterData characterData)
    {
        var dramaUI = UISystem.Instance.OpenUI<DramaUI>("DramaUI");
        if (dramaUI != null)
        {
            dramaUI.StartDrama(characterData.DailyDialogue[Random.Range(0, characterData.DailyDialogue.Count)]);
        }
    }
}

public class GoodwillFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType  => FunctionGroup.Goodwill;
    public void Execute(CharacterData characterData)
    {
        
    }
}

public class GiftGivingFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.GiftGiving;

    public void Execute(CharacterData characterData)
    {
        UISystem.Instance.OpenUI<InventoryUI>("InventoryUI");
    }
}

public class KitchenFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.Kitchen;

    public void Execute(CharacterData characterData)
    {
        UISystem.Instance.OpenUI("KitchenPanel");
    }
}

public class ClawMachineFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.ClawMachine;

    public void Execute(CharacterData characterData)
    {
        
    }
}

public class ExplosiveGamesFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.ExplosiveGames;

    public void Execute(CharacterData characterData)
    {
        UISystem.Instance.OpenUI("CrashSprintPanel");
    }
}

public class WitchPoisonFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.WitchPoison;

    public void Execute(CharacterData characterData)
    {
        UISystem.Instance.OpenUI("WitchPoisonPanel");
    }
}

public class ExhibitionFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.Exhibition;

    public void Execute(CharacterData characterData)
    {
        
    }
}

public class ManuscriptFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.Manuscript;

    public void Execute(CharacterData characterData)
    {
        
    }
}

public class SupermarketFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.Supermarket;

    public void Execute(CharacterData characterData)
    {
        UISystem.Instance.OpenUI("SuperMarketUI");
    }
}

public class FruitShopFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.FruitShop;

    public void Execute(CharacterData characterData)
    {
        
    }
}

/// <summary>
/// 布料店接口
/// </summary>
public class FabricStoreFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.FabricStore;
    public void Execute(CharacterData characterData)
    {
        UISystem.Instance.OpenUI<ClothShopUI>("ClothShopUI");
    }
}

public class SexToyStoreFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.SexToyStore;
    public void Execute(CharacterData characterData)
    {
        
    }
}

public class FishingFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.Fishing;

    public void Execute(CharacterData characterData)
    {
        
    }
}

public class FishingBaitShopFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.FishingBaitShop;
    public void Execute(CharacterData characterData)
    {
        
    }
}

public class PhotographyFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.Photography;
    public void Execute(CharacterData characterData)
    {
        
    }
}

public class CoffeeShopFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.CoffeeShop;

    public void Execute(CharacterData characterData)
    {
        
    }
}

public class BarFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGroup FunctionType => FunctionGroup.Bar;

    public void Execute(CharacterData characterData)
    {
        
    }
}