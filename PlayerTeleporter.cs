using UnityEngine;

/// <summary>
/// Корректный телепорт игрока (Rigidbody + CapsuleCollider).
/// Вешать на тот же объект, где PlayerMovement.
/// Вызывать TeleportTo(Transform) — например, из Signal Receiver в Timeline.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerTeleporter : MonoBehaviour
{
    private Rigidbody rb;

    private void Awake() => rb = GetComponent<Rigidbody>();

    /// <summary>Телепорт в позицию и поворот заданного Transform.</summary>
    public void TeleportTo(Transform target)
    {
        if (target == null) return;
        TeleportRaw(target.position, target.rotation);
    }

    /// <summary>Телепорт только по позиции (поворот не трогаем).</summary>
    public void TeleportToPosition(Transform target)
    {
        if (target == null) return;
        TeleportRaw(target.position, rb.rotation);
    }

    private void TeleportRaw(Vector3 pos, Quaternion rot)
    {
        // Гасим скорость — иначе игрок «вылетит» с прежним импульсом из новой точки.
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Для интерполированного Rigidbody обычное присваивание transform даёт «размазанный»
        // визуальный скачок. Move-методы переносят чисто, без интерполяции от старой точки.
        rb.position = pos;
        rb.rotation = rot;

        // Физика-движок кеширует трансформ — синхронизируем, чтобы коллизии не сработали
        // на «отрезке» между старой и новой позицией в этом же физическом шаге.
        Physics.SyncTransforms();
    }
}
