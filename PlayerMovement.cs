using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float sprintMultiplier = 1.6f;
    [SerializeField, Range(0f, 1f)] private float airControl = 0.4f;
    [SerializeField] private float acceleration = 50f;     // как быстро набираем скорость
    [SerializeField] private float deceleration = 60f;     // как быстро останавливаемся

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.4f;
    [SerializeField] private float extraGravity = 20f;     // даёт «вес» при падении
    [SerializeField] private float coyoteTime = 0.12f;     // прыжок чуть после схода с края
    [SerializeField] private float jumpBuffer = 0.12f;     // приём прыжка чуть до приземления

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;        // пустой объект у ног капсулы
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Camera (optional)")]
    [Tooltip("Если задан — движение идёт относительно направления камеры (XZ).")]
    [SerializeField] private Transform cameraTransform;

    [Header("Rotation")]
    [Tooltip("Поворачивать игрока в сторону движения.")]
    [SerializeField] private bool rotateTowardsMovement = true;
    [Tooltip("Скорость поворота, градусов в секунду.")]
    [SerializeField] private float rotationSpeed = 720f;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputAsset;  // перетащи InputSystem_Actions сюда
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string sprintActionName = "Sprint";

    private Rigidbody rb;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction sprintAction;

    private Vector2 moveInput;
    private bool isGrounded;
    private float lastGroundedTime = -999f;
    private float lastJumpPressedTime = -999f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;          // капсула не валится
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (inputAsset == null)
        {
            Debug.LogError($"{nameof(PlayerMovement)}: InputActionAsset не назначен.", this);
            enabled = false;
            return;
        }

        var map = inputAsset.FindActionMap(actionMapName, throwIfNotFound: true);
        moveAction = map.FindAction(moveActionName, throwIfNotFound: true);
        jumpAction = map.FindAction(jumpActionName, throwIfNotFound: true);
        sprintAction = map.FindAction(sprintActionName); // может отсутствовать — не критично
    }

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        sprintAction?.Enable();

        jumpAction.performed += OnJumpPressed;
    }

    private void OnDisable()
    {
        jumpAction.performed -= OnJumpPressed;

        moveAction.Disable();
        jumpAction.Disable();
        sprintAction?.Disable();
    }

    private void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();

        // Ground check каждый кадр — дёшево и стабильно.
        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(
                groundCheck.position, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
            if (isGrounded) lastGroundedTime = Time.time;
        }
    }

    private void OnJumpPressed(InputAction.CallbackContext _)
    {
        lastJumpPressedTime = Time.time;
    }

    private void FixedUpdate()
    {
        ApplyMovement();
        TryJump();
        ApplyExtraGravity();
    }

    private void ApplyMovement()
    {
        // Базис движения: либо камера (XZ), либо мировые оси.
        // ВАЖНО: нельзя брать transform.forward/right самого игрока, если
        // он же поворачивается в сторону движения — получим обратную связь
        // и игрок начнёт ходить по кругу при удержании A/D.
        Vector3 forward, right;
        if (cameraTransform != null)
        {
            forward = cameraTransform.forward;
            right = cameraTransform.right;
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();
        }
        else
        {
            forward = Vector3.forward;
            right = Vector3.right;
        }

        Vector3 wishDir = (forward * moveInput.y + right * moveInput.x);
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        bool sprinting = sprintAction != null && sprintAction.IsPressed();
        float targetSpeed = moveSpeed * (sprinting ? sprintMultiplier : 1f);
        Vector3 targetVel = wishDir * targetSpeed;

        // Работаем только в горизонтальной плоскости — Y оставляем физике.
        Vector3 vel = rb.linearVelocity;
        Vector3 horizontal = new Vector3(vel.x, 0f, vel.z);

        float accel = (wishDir.sqrMagnitude > 0.01f) ? acceleration : deceleration;
        if (!isGrounded) accel *= airControl;

        Vector3 newHorizontal = Vector3.MoveTowards(horizontal, targetVel, accel * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(newHorizontal.x, vel.y, newHorizontal.z);

        if (rotateTowardsMovement)
            RotateTowards(wishDir);
    }

    private void RotateTowards(Vector3 wishDir)
    {
        // Порог защищает от «рывка» обратно к forward, когда стик отпущен,
        // и от нестабильного LookRotation на нулевом векторе.
        if (wishDir.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(wishDir, Vector3.up);
        Quaternion next = Quaternion.RotateTowards(
            rb.rotation, target, rotationSpeed * Time.fixedDeltaTime);

        // MoveRotation вместо transform.rotation — корректно работает с интерполяцией.
        rb.MoveRotation(next);
    }

    private void TryJump()
    {
        bool canCoyote = Time.time - lastGroundedTime <= coyoteTime;
        bool buffered = Time.time - lastJumpPressedTime <= jumpBuffer;

        if (canCoyote && buffered)
        {
            // v = sqrt(2 * g * h) — высота прыжка не зависит от массы.
            float g = Mathf.Abs(Physics.gravity.y) + extraGravity;
            float jumpVel = Mathf.Sqrt(2f * g * jumpHeight);

            Vector3 v = rb.linearVelocity;
            v.y = jumpVel;
            rb.linearVelocity = v;

            // «съедаем» оба окна, чтобы не было двойного прыжка.
            lastGroundedTime = -999f;
            lastJumpPressedTime = -999f;
        }
    }

    private void ApplyExtraGravity()
    {
        // Прыжок чувствуется «тяжелее» — стандартный приём (Better Jump).
        if (!isGrounded)
            rb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
