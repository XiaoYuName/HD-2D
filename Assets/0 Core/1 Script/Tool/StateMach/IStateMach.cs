
public interface IStateMach<T>
{
    T Owner { get; }
    void Enter() { }
    void Update() { }
    void FixedUpdate() { }
    void Exit() { }
}
