using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerSlot : MonoBehaviour
{
    [SerializeField] TMP_Text playerLabel;
    [SerializeField] Image    roleIconBG;
    [SerializeField] Image    roleIcon;
    [SerializeField] TMP_Text roleNameText;
    [SerializeField] Image    panelBorder;

    [Header("Empty State")]
    [SerializeField] float emptyAlpha = 0.20f;

    CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void SetPlayer(int index, string roleName, Sprite icon, UIStyleConfig style)
    {
        gameObject.SetActive(true);   // always show when a player is present

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        if (playerLabel != null)
            playerLabel.text = $"P{index + 1}";

        if (roleNameText != null)
            roleNameText.text = roleName.ToUpper();

        if (roleIcon != null && icon != null)
        {
            roleIcon.sprite  = icon;
            roleIcon.enabled = true;
        }

        // Apply player colors from style
        if (style != null && index < 4)
        {
            if (playerLabel != null && style.playerLabel.Length > index)
                playerLabel.color = style.playerLabel[index];
            if (roleNameText != null && style.playerName.Length > index)
                roleNameText.color = style.playerName[index];
            if (panelBorder != null && style.playerBorder.Length > index)
                panelBorder.color = style.playerBorder[index];
            if (roleIconBG != null && style.playerIconStroke.Length > index)
                roleIconBG.color = style.playerIconStroke[index];
        }
    }

    public void SetEmpty(UIStyleConfig style)
    {
        gameObject.SetActive(false);  // hide entirely — no ghost boxes
    }
}
