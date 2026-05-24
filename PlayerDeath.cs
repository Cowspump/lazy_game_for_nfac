using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// При касании объекта с заданным тегом — перезапускает текущую сцену.
/// Вешать на игрока (тот же объект, где CapsuleCollider).
/// </summary>
public class PlayerDeath : MonoBehaviour
{
    [Header("Death")]
    [Tooltip("Тег объектов, касание которых убивает игрока.")]
    [SerializeField] private string deadTag = "dead";

    [Tooltip("Задержка перед перезапуском (для проигрывания эффекта/звука смерти).")]
    [SerializeField] private float reloadDelay = 0.1f;

    private bool isDying;

    // Срабатывает на solid-коллайдеры (без Is Trigger).
    private void OnCollisionEnter(Collision collision)
    {
        TryKill(collision.collider);
    }

    // Срабатывает на коллайдеры с Is Trigger.
    private void OnTriggerEnter(Collider other)
    {
        TryKill(other);
    }

    private void TryKill(Collider other)
    {
        if (isDying) return;
        if (!other.CompareTag(deadTag)) return;

        isDying = true;
        Debug.Log($"[PlayerDeath] Касание '{other.name}' с тегом '{deadTag}'. Перезапуск сцены через {reloadDelay}s.", this);

        if (reloadDelay <= 0f) Reload();
        else Invoke(nameof(Reload), reloadDelay);
    }

    private void Reload()
    {
        var current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }
}
