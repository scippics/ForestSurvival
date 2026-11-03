using UnityEngine;

// Вешается на шланг. Мини-игру ведёт PlayerHands из FixedUpdate: сюда приходит только
// факт удержания кнопки, поэтому состояние можно будет считать на сервере.
public class FuelSiphon : MonoBehaviour
{
    const float PressureRate = 40f;
    const float SuccessMin = 75f;
    const float SuccessMax = 95f;
    const float MaxPressure = 100f;
    const float SourceReach = 3f;

    static readonly Collider[] OverlapBuffer = new Collider[32];

    public float SiphonPressure { get; private set; }
    public bool IsPumping { get; private set; }

    public void BeginPump()
    {
        IsPumping = true;
        SiphonPressure = 0f;
    }

    public void CancelPump()
    {
        IsPumping = false;
        SiphonPressure = 0f;
    }

    public void TickPump(bool buttonHeld, float deltaTime, FuelCan can)
    {
        if (buttonHeld)
        {
            SiphonPressure = Mathf.Min(SiphonPressure + PressureRate * deltaTime, MaxPressure);
            if (SiphonPressure >= MaxPressure)
            {
                StoryUI.Instance.Say("Подавился бензином!");
                ForestAudio.Play(ForestSound.Cough, 0.8f);
                CancelPump();
            }
            return;
        }

        if (SiphonPressure >= SuccessMin && SiphonPressure <= SuccessMax)
        {
            can.SetLiters(FuelCan.Capacity);
            StoryDirector.Instance.OnFuelInHands();
            if (WorldSync.Instance != null)
                WorldSync.Instance.SendItem(can.GetComponent<Rigidbody>());
            StoryUI.Instance.Say("Пошло. Канистра полная: " + FuelCan.Capacity + " л.");
            ForestAudio.Play(ForestSound.Paper, 0.2f);
        }
        else
        {
            StoryUI.Instance.Say("Не пошло: слишком рано отпустил.");
        }
        CancelPump();
    }

    public bool IsNearFuelSource()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, SourceReach, OverlapBuffer,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (OverlapBuffer[i].CompareTag(InteractionRay.FuelSourceTag))
                return true;
        }
        return false;
    }
}
