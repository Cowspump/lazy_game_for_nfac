using UnityEngine;

/// <summary>
/// Слушает события PlayerMovement и переводит их в параметры Animator'а.
/// Вешать на тот же объект (или родителя), где живёт PlayerMovement.
/// Animator может быть на дочернем объекте — поле AnimatorOverride.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Если пусто — ищется в children.")]
    [SerializeField] private Animator animator;
    [Tooltip("Если пусто — ищется на этом же объекте или родителе.")]
    [SerializeField] private PlayerMovement movement;

    [Header("Parameter Names (как в Animator Controller)")]
    [SerializeField] private string speedParam = "Speed";        // Float — для Walking/Idle blend
    [SerializeField] private string groundedParam = "Grounded";  // Bool
    [SerializeField] private string spawnTrigger = "Spawn";      // Trigger — SpawnAir, одноразово при появлении
    [SerializeField] private string pickupTrigger = "PickUp";    // Trigger — анимация подбора

    [Header("Smoothing")]
    [Tooltip("Сглаживание изменения параметра Speed (секунды).")]
    [SerializeField] private float speedDamp = 0.1f;

    // Кешируем хеши — поиск по строке каждый кадр дорогой.
    private int speedHash, groundedHash, spawnHash, pickupHash;
    private bool hasSpeed, hasGrounded, hasSpawn, hasPickup;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (movement == null) movement = GetComponentInParent<PlayerMovement>();

        if (animator == null)
        {
            Debug.LogError($"{nameof(PlayerAnimator)}: Animator не найден.", this);
            enabled = false;
            return;
        }
        if (movement == null)
        {
            Debug.LogError($"{nameof(PlayerAnimator)}: PlayerMovement не найден.", this);
            enabled = false;
            return;
        }

        CacheParameters();
    }

    /// <summary>Проверяет, какие параметры реально существуют в контроллере, чтобы не сыпать варнингами.</summary>
    private void CacheParameters()
    {
        speedHash = Animator.StringToHash(speedParam);
        groundedHash = Animator.StringToHash(groundedParam);
        spawnHash = Animator.StringToHash(spawnTrigger);
        pickupHash = Animator.StringToHash(pickupTrigger);

        foreach (var p in animator.parameters)
        {
            if (p.nameHash == speedHash) hasSpeed = true;
            if (p.nameHash == groundedHash) hasGrounded = true;
            if (p.nameHash == spawnHash) hasSpawn = true;
            if (p.nameHash == pickupHash) hasPickup = true;
        }
    }

    private void Start()
    {
        // SpawnAir — одноразовая анимация появления при загрузке сцены.
        if (hasSpawn) animator.SetTrigger(spawnHash);
    }

    private void OnEnable()
    {
        if (movement == null) return;
        movement.OnPickedUp += HandlePickedUp;
    }

    private void OnDisable()
    {
        if (movement == null) return;
        movement.OnPickedUp -= HandlePickedUp;
    }

    private void Update()
    {
        if (movement == null || animator == null) return;

        if (hasSpeed)
            animator.SetFloat(speedHash, movement.HorizontalSpeed, speedDamp, Time.deltaTime);

        if (hasGrounded)
            animator.SetBool(groundedHash, movement.IsGrounded);
    }

    private void HandlePickedUp()
    {
        if (hasPickup) animator.SetTrigger(pickupHash);
    }
}
