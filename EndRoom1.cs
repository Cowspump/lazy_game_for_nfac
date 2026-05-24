using UnityEngine;

/// <summary>
/// Триггер: при входе игрока телепортирует его в заданную точку и выключает заданный объект.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EndRoom1 : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Teleport")]
    [Tooltip("Куда переместить игрока. Позиция и поворот этого Transform будут применены.")]
    [SerializeField] private Transform teleportTarget;

    [Tooltip("Использовать также поворот цели (иначе сохранится текущий поворот игрока).")]
    [SerializeField] private bool applyRotation = true;

    [Header("Disable")]
    [Tooltip("Объект, который выключится при срабатывании триггера.")]
    [SerializeField] private GameObject objectToDisable;

    [Header("Behaviour")]
    [SerializeField] private bool triggerOnce = true;

    private bool hasTriggered;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        if (!GetComponent<Collider>().isTrigger)
            Debug.LogWarning($"[EndRoom1] на '{name}': коллайдер должен быть Is Trigger.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (triggerOnce && hasTriggered) return;

        // Телепорт. Если на игроке есть PlayerTeleporter — используем его (он гасит скорость
        // и синхронизирует физику). Иначе двигаем напрямую через Rigidbody.
        if (teleportTarget != null)
        {
            var teleporter = other.GetComponentInParent<PlayerTeleporter>();
            if (teleporter != null)
            {
                if (applyRotation) teleporter.TeleportTo(teleportTarget);
                else teleporter.TeleportToPosition(teleportTarget);
            }
            else
            {
                FallbackTeleport(other.attachedRigidbody, other.transform);
            }
            Debug.Log($"[EndRoom1] Игрок телепортирован в '{teleportTarget.name}'.", this);
        }
        else
        {
            Debug.LogWarning("[EndRoom1] teleportTarget не назначен.", this);
        }

        // Выключаем объект.
        if (objectToDisable != null)
        {
            objectToDisable.SetActive(false);
            Debug.Log($"[EndRoom1] Объект '{objectToDisable.name}' выключен.", this);
        }

        hasTriggered = true;
    }

    private void FallbackTeleport(Rigidbody rb, Transform playerTransform)
    {
        Vector3 pos = teleportTarget.position;
        Quaternion rot = applyRotation ? teleportTarget.rotation : playerTransform.rotation;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = pos;
            rb.rotation = rot;
            Physics.SyncTransforms();
        }
        else
        {
            playerTransform.SetPositionAndRotation(pos, rot);
        }
    }
}
