using UnityEngine;

// Доска или камень под колесом: колесо перестаёт вязнуть и получает хороший зацеп.
// Пока на подкладке стоит колесо, она не двигается, иначе её выстрелило бы из-под протектора.
public class TractionPad : MonoBehaviour
{
    const float HoldSeconds = 0.6f;

    Rigidbody body;
    float heldUntil;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public void Press()
    {
        heldUntil = Time.time + HoldSeconds;
        if (!body.isKinematic)
            body.isKinematic = true;
    }

    void FixedUpdate()
    {
        if (body.isKinematic && Time.time > heldUntil)
            body.isKinematic = false;
    }
}
