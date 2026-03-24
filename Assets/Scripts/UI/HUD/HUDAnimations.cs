using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class HUDAnimations
{
    public static IEnumerator BlinkCoroutine(Graphic target, float interval = 0.55f)
    {
        if (target == null) yield break;
        while (true)
        {
            target.canvasRenderer.SetAlpha(1f);
            yield return new WaitForSeconds(interval);
            target.canvasRenderer.SetAlpha(0f);
            yield return new WaitForSeconds(interval);
        }
    }

    public static IEnumerator ShakeCoroutine(RectTransform target, float magnitude = 1f)
    {
        if (target == null) yield break;
        Vector2 origin = target.anchoredPosition;
        float period = 0.1f;
        while (true)
        {
            target.anchoredPosition = origin + new Vector2(magnitude, 0f);
            yield return new WaitForSeconds(period);
            target.anchoredPosition = origin + new Vector2(-magnitude, 0f);
            yield return new WaitForSeconds(period);
        }
    }

    public static void StopBlink(Graphic target)
    {
        if (target != null)
            target.canvasRenderer.SetAlpha(1f);
    }
}
