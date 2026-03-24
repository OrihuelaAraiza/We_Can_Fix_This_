using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FailureItemUI : MonoBehaviour
{
    [SerializeField] Image    statusDot;
    [SerializeField] TMP_Text failureName;
    [SerializeField] TMP_Text failureSub;

    static readonly Color DotCritical  = HexColor("#a02020");
    static readonly Color NameCritical = HexColor("#a02020");
    static readonly Color SubCritical  = HexColor("#4a1818");

    static readonly Color DotWarning   = HexColor("#907818");
    static readonly Color NameWarning  = HexColor("#908020");
    static readonly Color SubWarning   = HexColor("#484010");

    static readonly Color DotOK        = HexColor("#287040");
    static readonly Color NameOK       = HexColor("#88a888");
    static readonly Color SubOK        = HexColor("#405040");

    Coroutine blinkRoutine;

    public void Setup(FailureData data, UIStyleConfig style)
    {
        if (failureName != null)
            failureName.text = data.systemName;
        if (failureSub != null)
            failureSub.text = data.location;

        Color dotColor, nameColor, subColor;

        switch (data.severity)
        {
            case FailureSeverity.CRITICAL:
                dotColor  = DotCritical;
                nameColor = NameCritical;
                subColor  = SubCritical;
                // Blink dot on critical
                if (statusDot != null)
                    blinkRoutine = StartCoroutine(HUDAnimations.BlinkCoroutine(statusDot, 0.6f));
                break;
            case FailureSeverity.WARNING:
                dotColor  = DotWarning;
                nameColor = NameWarning;
                subColor  = SubWarning;
                break;
            default:
                dotColor  = DotOK;
                nameColor = NameOK;
                subColor  = SubOK;
                break;
        }

        if (statusDot != null)   statusDot.color   = dotColor;
        if (failureName != null) failureName.color  = nameColor;
        if (failureSub != null)  failureSub.color   = subColor;

        // Apply fonts from style if available
        if (style != null && style.vt323 != null)
        {
            if (failureName != null) failureName.font = style.vt323;
            if (failureSub != null)  failureSub.font  = style.vt323;
        }
    }

    void OnDestroy()
    {
        if (blinkRoutine != null)
            StopCoroutine(blinkRoutine);
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
