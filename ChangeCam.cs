using UnityEngine;

/// <summary>
/// Триггер-«дверь»: при входе игрока меняет местами активное состояние двух объектов.
/// Удобно для переключения камер между комнатами.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ChangeCam : MonoBehaviour
{
    [Header("Objects to swap")]
    [Tooltip("Объект, который сейчас включён (например, камера комнаты A).")]
    [SerializeField] private GameObject objectA;

    [Tooltip("Объект, который сейчас выключен (например, камера комнаты B).")]
    [SerializeField] private GameObject objectB;

    [Header("Player detection")]
    [Tooltip("Тег игрока. По умолчанию 'Player' — поставь этот тег на капсулу.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Перезаряжать триггер, чтобы нельзя было дважды свапнуть подряд внутри коллайдера.")]
    [SerializeField] private float retriggerCooldown = 0.3f;

    private float lastTriggerTime = -999f;

    private void Reset()
    {
        // Авто-настройка коллайдера при добавлении компонента.
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Awake()
    {
        // Защита от типичной ошибки: коллайдер не помечен как Trigger.
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
            Debug.LogWarning($"{nameof(ChangeCam)} на '{name}': коллайдер должен быть Is Trigger.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (Time.time - lastTriggerTime < retriggerCooldown) return;

        Swap();
        lastTriggerTime = Time.time;
    }

    private void Swap()
    {
        // Меняем местами текущие состояния — естественно работает «туда и обратно».
        if (objectA != null) objectA.SetActive(!objectA.activeSelf);
        if (objectB != null) objectB.SetActive(!objectB.activeSelf);
    }
}
