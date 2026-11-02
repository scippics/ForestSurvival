using Unity.Netcode.Components;

// Положением управляет сам игрок: физика ног считается у него, а сервер только рассылает результат.
public class OwnerNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}
