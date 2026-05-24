using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Имя сцены, на которую переходим. Сцена должна быть добавлена в Build Settings (File → Build Profiles → Scene List).")]
    [SerializeField] private string sceneName;

    [Header("Button (optional)")]
    [Tooltip("Если задана — скрипт сам подпишется на её OnClick. Иначе вызывай LoadScene() вручную из OnClick кнопки в инспекторе.")]
    [SerializeField] private Button button;

    private void OnEnable()
    {
        if (button != null)
            button.onClick.AddListener(LoadScene);
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(LoadScene);
    }

    // Публичный — чтобы можно было дёргать прямо из OnClick() кнопки в инспекторе.
    public void LoadScene()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"{nameof(SceneLoader)}: Scene Name не задан.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"{nameof(SceneLoader)}: сцена '{sceneName}' не найдена в Build Settings.", this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    // Удобный overload — если хочешь повесить несколько кнопок на один объект
    // и каждая ведёт в свою сцену: в OnClick укажи LoadSceneByName и впиши имя.
    public void LoadSceneByName(string name)
    {
        sceneName = name;
        LoadScene();
    }
}
