using System.Collections.Generic;
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

    [Header("First Person Mode")]
    [Tooltip("Включить управление от первого лица: мышь крутит игрока (yaw) и камеру (pitch).")]
    [SerializeField] private bool firstPersonMode = false;
    [Tooltip("FPS-камера. Должна быть child этого игрока (помещается внутри головы).")]
    [SerializeField] private Transform fpsCamera;
    [Tooltip("Чувствительность мыши.")]
    [SerializeField] private float lookSensitivity = 0.15f;
    [Tooltip("Ограничение наклона камеры вверх/вниз, в градусах.")]
    [SerializeField] private float pitchClamp = 85f;
    [Tooltip("Блокировать курсор в центре экрана в FPS-режиме.")]
    [SerializeField] private bool lockCursorInFps = true;

    [SerializeField] private string lookActionName = "Look";

    private InputAction lookAction;
    private float currentPitch = 0f;
    private bool fpsInitialized = false;

    [Header("Rotation")]
    [Tooltip("Поворачивать игрока в сторону движения.")]
    [SerializeField] private bool rotateTowardsMovement = true;
    [Tooltip("Скорость поворота, градусов в секунду.")]
    [SerializeField] private float rotationSpeed = 720f;

    [Header("Pickup")]
    [Tooltip("Тег объектов, которые можно подобрать.")]
    [SerializeField] private string pickupTag = "obj";
    [Tooltip("Куда крепить подобранный предмет. Если пусто — крепится к самому игроку.")]
    [SerializeField] private Transform holdPoint;
    [Tooltip("Локальная позиция предмета в руке, если HoldPoint не задан.")]
    [SerializeField] private Vector3 holdLocalPosition = new Vector3(0.5f, 1f, 0.5f);

    [Header("Input")]
    [SerializeField] private InputActionAsset inputAsset;  // перетащи InputSystem_Actions сюда
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string sprintActionName = "Sprint";
    [SerializeField] private string interactActionName = "Interact";

    private Rigidbody rb;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction interactAction;

    private Vector2 moveInput;
    private bool isGrounded;
    private float lastGroundedTime = -999f;
    private float lastJumpPressedTime = -999f;

    // Pickup state
    private readonly HashSet<GameObject> inRange = new();
    private GameObject heldObject;
    private Rigidbody heldRigidbody;

    // ── Хуки для аниматора ────────────────────────────────────────────
    /// <summary>Текущая горизонтальная скорость в м/с (для Walk-параметра).</summary>
    public float HorizontalSpeed
    {
        get
        {
            if (rb == null) return 0f;
            Vector3 v = rb.linearVelocity; v.y = 0f;
            return v.magnitude;
        }
    }

    /// <summary>На земле ли игрок сейчас.</summary>
    public bool IsGrounded => isGrounded;

    /// <summary>Срабатывает в момент успешного подбора предмета.</summary>
    public event System.Action OnPickedUp;

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
        sprintAction = map.FindAction(sprintActionName);     // может отсутствовать
        interactAction = map.FindAction(interactActionName); // может отсутствовать
        lookAction = map.FindAction(lookActionName);         // может отсутствовать
    }

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        sprintAction?.Enable();
        interactAction?.Enable();
        lookAction?.Enable();

        jumpAction.performed += OnJumpPressed;
        if (interactAction != null) interactAction.performed += OnInteractPressed;
    }

    private void OnDisable()
    {
        jumpAction.performed -= OnJumpPressed;
        if (interactAction != null) interactAction.performed -= OnInteractPressed;

        moveAction.Disable();
        jumpAction.Disable();
        sprintAction?.Disable();
        interactAction?.Disable();
        lookAction?.Disable();
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

        if (firstPersonMode) HandleFpsLook();
    }

    private void HandleFpsLook()
    {
        if (!fpsInitialized) InitFps();
        if (lookAction == null || fpsCamera == null) return;

        Vector2 look = lookAction.ReadValue<Vector2>() * lookSensitivity;

        // Yaw на тело (поворот вокруг Y) — через Rigidbody, чтобы не ломать интерполяцию.
        if (Mathf.Abs(look.x) > 0.0001f)
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, look.x, 0f));

        // Pitch на камеру (наклон вверх-вниз), с ограничением.
        currentPitch = Mathf.Clamp(currentPitch - look.y, -pitchClamp, pitchClamp);
        fpsCamera.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
    }

    private void InitFps()
    {
        if (fpsCamera != null)
        {
            currentPitch = fpsCamera.localEulerAngles.x;
            if (currentPitch > 180f) currentPitch -= 360f; // нормализация в [-180, 180]
        }

        if (lockCursorInFps)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        fpsInitialized = true;
    }

    /// <summary>Включить/выключить FPS-режим (для триггера комнаты).</summary>
    public void SetFirstPerson(bool enabled)
    {
        firstPersonMode = enabled;

        if (enabled)
        {
            InitFps();
        }
        else
        {
            fpsInitialized = false;
            if (lockCursorInFps)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
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
        // Базис движения:
        //   FPS-режим → forward/right самого тела (его yaw уже задан мышью, петли нет).
        //   Иначе    → камера, если задана, иначе мировые оси.
        Vector3 forward, right;
        if (firstPersonMode)
        {
            forward = transform.forward;
            right = transform.right;
        }
        else if (cameraTransform != null)
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

        // В FPS-режиме тело поворачивается мышью (HandleFpsLook), а не за движением.
        if (rotateTowardsMovement && !firstPersonMode)
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

    // ────────────────────────────────────────────────────────────────────
    //  PICKUP
    //  Триггер-коллайдер на этом же объекте копит объекты с тегом pickupTag.
    //  По кнопке Interact подбираем ближайший / бросаем тот, что в руке.
    // ────────────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(pickupTag))
            inRange.Add(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(pickupTag))
            inRange.Remove(other.gameObject);
    }

    private void OnInteractPressed(InputAction.CallbackContext _)
    {
        if (heldObject != null) { Drop(); return; }
        PickupClosest();
    }

    private void PickupClosest()
    {
        GameObject best = null;
        float bestSqr = float.PositiveInfinity;

        // Чистим уничтоженные объекты, если такие есть (OnTriggerExit для них не вызывается).
        inRange.RemoveWhere(go => go == null);

        foreach (var go in inRange)
        {
            float sqr = (go.transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = go; }
        }

        if (best == null) return;

        heldObject = best;
        heldRigidbody = best.GetComponent<Rigidbody>();

        if (heldRigidbody != null)
        {
            heldRigidbody.isKinematic = true;
            heldRigidbody.detectCollisions = false;
        }

        Transform parent = holdPoint != null ? holdPoint : transform;
        heldObject.transform.SetParent(parent, worldPositionStays: false);
        heldObject.transform.localPosition = holdPoint != null ? Vector3.zero : holdLocalPosition;
        heldObject.transform.localRotation = Quaternion.identity;

        // Пока в руке — не считаем его «в зоне», чтобы не подобрать повторно.
        inRange.Remove(heldObject);

        OnPickedUp?.Invoke();
    }

    private void Drop()
    {
        if (heldObject == null) return;

        heldObject.transform.SetParent(null, worldPositionStays: true);

        if (heldRigidbody != null)
        {
            heldRigidbody.isKinematic = false;
            heldRigidbody.detectCollisions = true;
        }

        heldObject = null;
        heldRigidbody = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
