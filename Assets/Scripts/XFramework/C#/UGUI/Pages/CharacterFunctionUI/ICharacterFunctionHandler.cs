using UnityEngine;
using XFramework;

public interface ICharacterFunctionHandler
{
    FunctionGrpup FunctionType { get; }
    void Execute(CharacterData characterData);
}
