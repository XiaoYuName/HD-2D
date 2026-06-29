using UnityEngine;
using XFramework;

public interface ICharacterFunctionHandler
{
    FunctionType FunctionType { get; }
    void Execute(CharacterData characterData);
}
