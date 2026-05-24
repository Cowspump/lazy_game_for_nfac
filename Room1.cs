using UnityEngine;
using UnityEngine.Playables;

// Подключаем нашу систему движения, чтобы выключать ввод на время катсцены.

/// <summary>
/// Триггер, запускающий Timeline-катсцену при входе игрока.
/// Вешать на объект с Collider (Is Trigger).
/// </summary>
[RequireComponent(typeof(Collider))]
public class Room1 : MonoBehaviour
{
    [Header("Cutscene")]
    [Tooltip("PlayableDirector с привязанным Timeline-ассетом.")]
    [SerializeField] private PlayableDirector director;

    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Behaviour")]
    [Tooltip("Запускать только один раз (не повторять при повторном входе).")]
    [SerializeField] private bool playOnce = true;

    [Tooltip("Отключать управление игроком (PlayerMovement) на время катсцены.")]
    [SerializeField] private bool freezePlayer = true;

    [Tooltip("Гасить скорость Rigidbody игрока при старте катсцены, чтобы он не ехал по инерции.")]
    [SerializeField] private bool zeroVelocityOnStart = true;

    private bool hasPlayed;
    private PlayerMovement frozenMovement;   // кого разморозить в конце
    private Rigidbody frozenRigidbody;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        Debug.Log($"[Room1] Awake on '{name}'. Collider={col.GetType().Name}, isTrigger={col.isTrigger}, director={(director != null ? director.name : "NULL")}", this);

        if (!col.isTrigger)
            Debug.LogWarning($"[Room1] на '{name}': коллайдер НЕ Is Trigger — OnTriggerEnter не сработает.", this);

        if (director == null)
            Debug.LogError($"[Room1] на '{name}': PlayableDirector не назначен в инспекторе.", this);
        else if (director.playableAsset == null)
            Debug.LogError($"[Room1] на '{name}': у PlayableDirector '{director.name}' не назначен Timeline-ассет (поле Playable).", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[Room1] OnTriggerEnter: other='{other.name}', tag='{other.tag}', ожидаемый тег='{playerTag}'", this);

        if (!other.CompareTag(playerTag))
        {
            Debug.Log($"[Room1] Игнор — тег '{other.tag}' не совпадает с '{playerTag}'.", this);
            return;
        }

        if (playOnce && hasPlayed)
        {
            Debug.Log("[Room1] Игнор — playOnce и уже играли.", this);
            return;
        }

        if (director == null)
        {
            Debug.LogError("[Room1] director == null, нечего запускать.", this);
            return;
        }

        Debug.Log($"[Room1] ▶ Запускаю Timeline '{director.playableAsset?.name}' на директоре '{director.name}'.", this);

        if (freezePlayer)
        {
            frozenMovement = other.GetComponentInParent<PlayerMovement>();
            if (frozenMovement != null)
            {
                frozenMovement.enabled = false;
                Debug.Log("[Room1] Управление игрока отключено на время катсцены.", this);
            }

            if (zeroVelocityOnStart)
            {
                frozenRigidbody = other.GetComponentInParent<Rigidbody>();
                if (frozenRigidbody != null)
                {
                    frozenRigidbody.linearVelocity = Vector3.zero;
                    frozenRigidbody.angularVelocity = Vector3.zero;
                }
            }
        }

        // Подписываемся ОДИН раз: при остановке Timeline вернём управление.
        director.stopped -= OnCutsceneStopped;
        director.stopped += OnCutsceneStopped;

        director.Play();
        hasPlayed = true;
    }

    private void OnCutsceneStopped(PlayableDirector pd)
    {
        director.stopped -= OnCutsceneStopped;

        if (frozenMovement != null)
        {
            frozenMovement.enabled = true;
            Debug.Log("[Room1] Управление игрока возвращено.", this);
            frozenMovement = null;
        }

        frozenRigidbody = null;
    }

    private void OnDestroy()
    {
        // Защита от утечки подписки при выгрузке сцены посреди катсцены.
        if (director != null) director.stopped -= OnCutsceneStopped;
    }

    // На случай если триггер не enter, а stay (бывает, что игрок уже стоял внутри при старте сцены).
    private void OnTriggerStay(Collider other)
    {
        if (hasPlayed || !playOnce) return; // чтобы не спамило
        // ничего не делаем — просто индикатор, что коллизия вообще есть
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log($"[Room1] OnTriggerExit: other='{other.name}'", this);
    }
}
