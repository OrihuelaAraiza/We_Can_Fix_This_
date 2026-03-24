using UnityEngine;
using TMPro;

[CreateAssetMenu(fileName = "UIStyleConfig", menuName = "WCFTThis/UIStyleConfig")]
public class UIStyleConfig : ScriptableObject
{
    [Header("Fonts")]
    public TMP_FontAsset vt323;
    public TMP_FontAsset caveat;

    [Header("Panel Colors")]
    public Color panelBg      = HexColor("#161a1e");
    public Color panelBorder  = HexColor("#252a2f");
    public Color cornerAccent = HexColor("#3a4048");
    public Color labelText    = HexColor("#3a4048");

    [Header("Health Colors")]
    public Color healthOrange = HexColor("#c07010");
    public Color healthText   = HexColor("#c8880a");
    public Color powerTeal    = HexColor("#287898");
    public Color powerLabel   = HexColor("#4a6a8a");
    public Color hullRed      = HexColor("#a02828");
    public Color hullLabel    = HexColor("#682020");

    [Header("CoreX Colors")]
    public Color coreXFill       = HexColor("#a82020");
    public Color coreXText       = HexColor("#b02020");
    public Color coreXLabel      = HexColor("#581818");
    public Color coreXBorderTint = HexColor("#2a1a1a");

    [Header("Status Colors")]
    public Color colorOK       = HexColor("#287040");
    public Color colorWarning  = HexColor("#907818");
    public Color colorCritical = HexColor("#a02020");

    [Header("Player Colors")]
    public Color[] playerBorder     = { HexColor("#182618"), HexColor("#182030"), HexColor("#282008"), HexColor("#2a2a2a") };
    public Color[] playerLabel      = { HexColor("#284820"), HexColor("#20304a"), HexColor("#483808"), HexColor("#333333") };
    public Color[] playerName       = { HexColor("#8aaa88"), HexColor("#88a0c8"), HexColor("#c8b870"), HexColor("#333333") };
    public Color[] playerIconStroke = { HexColor("#2a6030"), HexColor("#204878"), HexColor("#706010"), HexColor("#2a2a2a") };

    [Header("Endgame Colors")]
    public Color victoryText   = HexColor("#2a8040");
    public Color victoryBorder = HexColor("#182a18");
    public Color defeatText    = HexColor("#a02828");
    public Color defeatBorder  = HexColor("#2a1818");

    [Header("Decoration Colors")]
    public Color tapeColor    = HexColor("#8a8278");
    public Color screwColor   = HexColor("#252830");
    public Color patchColor   = HexColor("#323c28");
    public Color bandaidColor = HexColor("#b07840");
    public Color postitBg     = HexColor("#e8d84a");
    public Color postitText   = HexColor("#2a2600");
    public Color hwGold       = HexColor("#e8c840");
    public Color hwRed        = HexColor("#e05050");

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
