using System.Collections;
using UnityEngine;

/// <summary>
/// Clase base abstracta para todos los paneles de UI del proyecto.
/// Provee acceso al UIStyleKit centralizado y animación de Show/Hide via CanvasGroup.
/// </summary>
/// <setup>
/// SETUP EN UNITY EDITOR:
/// 1. Añadir un CanvasGroup al mismo GameObject que hereda de UIBase (o en canvasGroup field)
/// 2. Llamar a BuildUI() desde Start() en la subclase, o sobrescribir Start()
/// 3. Asegurarse de que Assets/Resources/UIStyleKit.asset exista antes de entrar en Play Mode
/// </setup>
[RequireComponent(typeof(CanvasGroup))]
public abstract class UIBase : MonoBehaviour
{
    [Header("UIBase")]
    [SerializeField] protected CanvasGroup canvasGroup;

    UIStyleKit _style;
    protected UIStyleKit Style
    {
        get
        {
            if (_style == null)
                _style = Resources.Load<UIStyleKit>("UIStyleKit");
            return _style;
        }
    }

    protected virtual void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    protected virtual void Start()
    {
        BuildUI();
    }

    /// <summary>Construye o inicializa la UI. Llamado en Start().</summary>
    protected abstract void BuildUI();

    /// <summary>Muestra el panel con fade-in.</summary>
    public virtual void Show()
    {
        gameObject.SetActive(true);
        if (canvasGroup != null)
            StartCoroutine(FadeTo(1f, 0.15f));
    }

    /// <summary>Oculta el panel con fade-out y desactiva el GO al terminar.</summary>
    public virtual void Hide()
    {
        if (canvasGroup != null)
            StartCoroutine(FadeOutAndDeactivate(0.15f));
        else
            gameObject.SetActive(false);
    }

    IEnumerator FadeTo(float target, float duration)
    {
        float start   = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed            += Time.unscaledDeltaTime;
            canvasGroup.alpha   = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = target;
    }

    IEnumerator FadeOutAndDeactivate(float duration)
    {
        yield return FadeTo(0f, duration);
        gameObject.SetActive(false);
    }
}
