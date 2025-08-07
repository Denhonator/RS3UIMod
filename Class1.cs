using RS3;
using Harmony;
using MelonLoader;
using UnityEngine;
using static MelonLoader.MelonLogger;
using System.IO;
using System;

//[HarmonyLib.HarmonyPatch(typeof(Il2CppMakimono.AnimationDirector), "GetCurrentState", new Type[] { typeof(int) })]
//class PlayTime
//{
//    static string lastresult = "";
//    private static void Postfix(int layerindex, ref string __result, ref Il2CppMakimono.AnimationDirector __instance)
//    {
//        __instance.UpdateSpecifiedAnim();
//    }
//}

[Serializable]
public class Settings
{
    static Settings()
    {
        LoadSettings();
    }

    public static string FilePath => Application.persistentDataPath + "/__modsettings.json";


    public static void LoadSettings()
    {
        if (File.Exists(FilePath))
        {
            instance = JsonUtility.FromJson<Settings>(File.ReadAllText(FilePath));
            return;
        }

        instance = new Settings();
        WriteSettings();
    }
    public static void WriteSettings()
    {
        File.WriteAllText(FilePath, JsonUtility.ToJson(instance, true));
    }

    private static int sanitizeSpeed(int fps) => fps < 1 ? 30 : fps;

    public static int GetGameSpeedByIndex(int idx) => sanitizeSpeed(
        idx==0 ? instance.normalFps:
        idx==1 ? instance.fastFps:
        idx==2 ? instance.turboFps:
        instance.normalFps);

    public static Settings instance = new Settings();

    public bool skipLogos = true;

    public int normalFps = 30;
    public int fastFps = 60;
    public int turboFps = 90;

    public int battleSpeed = 0;
    public int fieldSpeed = 0;

    public int enTextSpeed = 1;
    public int jpTextSpeed = 2;

    public bool mapAnywhere = false;
    public int fastGrow = 0;
    public int fastGrowSkill = 0;
    public int fastGrowEnemy = 0;
    public float sparkRateMultiplier = 1.0f;
    public float acquireRateMultiplier = 1.0f;

    public bool speedrun = false;
}

[HarmonyLib.HarmonyPatch(typeof(GameCore), "Update")]
public static class TrackGameStateChanges
{
    public static bool IgnoreNextStateChange { get; set; } = false;

    public static void SetGameSpeedByState(GameCore.State state) =>
        Application.targetFrameRate =
        state == GameCore.State.BATTLE ? Settings.GetGameSpeedByIndex(Settings.instance.battleSpeed) :
        state == GameCore.State.FIELD ? Settings.GetGameSpeedByIndex(Settings.instance.fieldSpeed) :
                                        30;

    public static void IncrementCurrentGameStateSpeed()
    {
        if (Settings.instance.speedrun)
            return;
        GameCore.State s = GameCore.m_state;
        if (s == GameCore.State.BATTLE)
        {
            Settings.instance.battleSpeed = (Settings.instance.battleSpeed + 1) % 3;
            Settings.WriteSettings();

            SetGameSpeedByState(s);
        }
        else if (s == GameCore.State.FIELD)
        {
            Settings.instance.fieldSpeed = (Settings.instance.fieldSpeed + 1) % 3;
            Settings.WriteSettings();

            SetGameSpeedByState(s);
        }
    }

    public static GameCore.State prevState;

    public static void Prefix(GameCore __instance)
    {
        prevState = GameCore.m_state;
    }

    public static void Postfix(GameCore __instance, ref int __state)
    {
        if (Settings.instance.speedrun)
            return;
        GameCore.State currState = GameCore.m_state;

        if (prevState != currState && !IgnoreNextStateChange)
        {
            //System.IO.File.AppendAllText("test.txt", $"Detected state change {{{oldState} => {newState}}}\n");
            SetGameSpeedByState(currState);
        }

        IgnoreNextStateChange = false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(GameMain), "Update")]
public static class SpeedOptions
{
    static GameObject gui = null;
    public static void Prefix()
    {
        if (Settings.instance.speedrun)
            return;
        if (Input.GetKeyDown(KeyCode.PageDown))
        {
            TrackGameStateChanges.IncrementCurrentGameStateSpeed();
        }
        if (Input.GetKeyDown(KeyCode.Home))
        {
            if (!gui)
            {
                gui = new GameObject();
                gui.AddComponent<SeadCategoryGUIController>();
            }
            else
            {
                GameObject.Destroy(gui);
            }
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(Util), "LoadTexture", new Type[] { typeof(string), typeof(byte[]) })]
public static class ReplaceTexture
{
    public static void Postfix(ref string fname, ref byte[] data, ref Texture2D __result)
    {
        try
        {
            if (File.Exists("ReplaceTexture/" + fname))
            {
                try
                {
                    data = File.ReadAllBytes("ReplaceTexture/" + fname);
                    __result.LoadImage(data, true);
                }
                catch
                {
                    Msg("Failed to replace" + fname);
                }
            }
            else
            {
                if (!Directory.Exists(Path.GetDirectoryName("Extract/" + fname)))
                    Directory.CreateDirectory(Path.GetDirectoryName("Extract/" + fname));
                if (data == null)
                    data = Util.falloc(fname);
                if (data != null)
                    File.WriteAllBytes("Extract/" + fname, data);
            }
        }
        catch
        {
            Msg("Failed to save " + fname);
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleArtPointWindow), "Initialize", new Type[] {})]
public static class ReplaceTexture2
{
    public static void Postfix(BattleArtPointWindow __instance)
    {
        string[] path = HarmonyLib.Traverse.Create(__instance).Field("path").GetValue<string[]>();
        Texture2D[] textures = HarmonyLib.Traverse.Create(__instance).Field("textures").GetValue<Texture2D[]>();
        GS.Sprite[] sprites = HarmonyLib.Traverse.Create(__instance).Field("sprites").GetValue<GS.Sprite[]>();
        int[] posX = HarmonyLib.Traverse.Create(__instance).Field("posX").GetValue<int[]>();
        try
        {
            byte[] data = File.ReadAllBytes("ReplaceTexture/" + path[0] + ".png");
            textures[0].LoadImage(data, true);
            sprites[0].SetTexture(textures[0], false);
            sprites[0].SetScale(0.5f, 0.5f);
            posX[0] = 670;
        }
        catch
        {
            Msg("Failed to load " + path[0]);
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleArtPointWindow), "Draw", new Type[] { })]
public static class DisablePointWindow
{
    public static bool Prefix(BattleArtPointWindow __instance)
    {
        //x 686+   y 28 
        if (!HarmonyLib.Traverse.Create(__instance).Field("isVisible").GetValue<bool>())
            return false;
        int num = HarmonyLib.Traverse.Create(__instance).Field("currentDrawPoint").GetValue<int>();
        int num2 = HarmonyLib.Traverse.Create(__instance).Field("maxDrawPoint").GetValue<int>();
        GS.DrawStringMenu(string.Format("{0} / {1}", num, num2), 920, 28, 0, Color.white, GS.FontEffect.CURSOR, 1, 3, 0.8f);
        return false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleCharaNameWindow), "Initialize", new Type[] { })]
public static class ReplaceTexture3
{
    public static void Postfix(BattleCharaNameWindow __instance)
    {
        string path = HarmonyLib.Traverse.Create(__instance).Field("path").GetValue<string>();
        Texture2D texture = HarmonyLib.Traverse.Create(__instance).Field("texture").GetValue<Texture2D>();
        GS.Sprite sprite = HarmonyLib.Traverse.Create(__instance).Field("sprite").GetValue<GS.Sprite>();
        try
        {
            byte[] data = File.ReadAllBytes("ReplaceTexture/" + path + ".png");
            texture.LoadImage(data, true);
            sprite.SetTexture(texture, false);
            HarmonyLib.Traverse.Create(__instance).Field("posX").SetValue(630);
        }
        catch(Exception e)
        {
            Msg("Failed to load " + path + ": " + e.Message);
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleCharaNameWindow), "InitCommander", new Type[] { })]
public static class ReplaceTexture4
{
    public static void Postfix(BattleCharaNameWindow __instance)
    {
        string path = HarmonyLib.Traverse.Create(__instance).Field("path").GetValue<string>();
        Texture2D texture = HarmonyLib.Traverse.Create(__instance).Field("texture").GetValue<Texture2D>();
        GS.Sprite sprite = HarmonyLib.Traverse.Create(__instance).Field("sprite").GetValue<GS.Sprite>();
        try
        {
            byte[] data = File.ReadAllBytes("ReplaceTexture/" + path + ".png");
            texture.LoadImage(data, true);
            sprite.SetTexture(texture, false);
            HarmonyLib.Traverse.Create(__instance).Field("posX").SetValue(630);
        }
        catch (Exception e)
        {
            Msg("Failed to load " + path + ": " + e.Message);
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommandMode), "SetWindowSize", new Type[] { })]
public static class CompactUI2
{
    public static bool Prefix(CommandMode __instance)
    {
        int menuElement = HarmonyLib.Traverse.Create(__instance).Field("menuElement").GetValue<int>();
        BattleCommandWindow commandWindow = HarmonyLib.Traverse.Create(__instance).Field("window").GetValue<BattleCommandWindow>();
        if (6 < menuElement)
            commandWindow.SetWindowSize(270, 172);
        else
            commandWindow.SetWindowSize(270, 16 + menuElement * 26);
        commandWindow.SetWindowPos(55, RS3UI.commandY-10);
        return false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommanderMode), "SetWindowSize", new Type[] { })]
public static class CompactUI4
{
    public static bool Prefix(CommanderMode __instance)
    {
        int menuElement = HarmonyLib.Traverse.Create(__instance).Field("menuElement").GetValue<int>();
        BattleCommandWindow commandWindow = HarmonyLib.Traverse.Create(__instance).Field("commandWindow").GetValue<BattleCommandWindow>();

        if (6 < menuElement)
        {
            commandWindow.SetWindowSize(270, 172);
        }
        else
        {
            commandWindow.SetWindowSize(270, 16 + menuElement * 26);
        }

        commandWindow.SetWindowPos(55, RS3UI.commandY - 10);
        return false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommanderPageNameWin), "Draw", new Type[] { typeof(bool) })]
public static class CompactUI5
{
    public static void Prefix(ref bool canScroll, CommanderPageNameWin __instance)
    {
        GS.FillRectZ(55, 20, 4000, 360, 48, 0.5f);
        RS3UI.windowType = "PageName";
    }
    public static void Postfix()
    {
        RS3UI.windowType = "";
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommandMode), "NormalUIUpdate", new Type[] { })]
public static class CompactUI3
{
    public static void Prefix(CommandMode __instance)
    {
        CommandPageNameWindow commandPageNameWindow = HarmonyLib.Traverse.Create(__instance).Field("commandPageNameWindow").GetValue<CommandPageNameWindow>();
        CVariableWindow cVariableWindow = HarmonyLib.Traverse.Create(commandPageNameWindow).Field("cVariableWindow").GetValue<CVariableWindow>();
        cVariableWindow.SetPos(55, 20);
        cVariableWindow.SetSize(180, 32);
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommanderMode), "CommanderNormalUI", new Type[] { })]
public static class CompactUI6
{
    public static void Prefix(CommanderMode __instance)
    {
        CommanderPageNameWin cmdPageNameWindow = HarmonyLib.Traverse.Create(__instance).Field("cmdPageNameWindow").GetValue<CommanderPageNameWin>();
        CVariableWindow cVariableWindow = HarmonyLib.Traverse.Create(cmdPageNameWindow).Field("cVariableWindow").GetValue<CVariableWindow>();
        cVariableWindow.SetPos(55, 20);
        cVariableWindow.SetSize(380, 32);
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommandCursor), "SetCursor", new Type[] { typeof(string[]), typeof(string) })]
public static class CursorPosition
{
    public static void Postfix(ref string[] _array, ref string _name, CommandCursor __instance)
    {
        int num = Array.IndexOf(_array, _name);
        __instance.commandCursor.SetPos(18, RS3UI.commandY-6 + num * 26);
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommanderCursor), "SetCursor", new Type[] { typeof(string[]), typeof(string) })]
public static class CursorPosition2
{
    public static void Postfix(ref string[] _array, ref string _name, CommanderCursor __instance)
    {
        int num = Array.IndexOf(_array, _name);
        __instance.commandCursor.SetPos(18, RS3UI.commandY - 6 + num * 26);
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommandDescText), "DescTextUpdate", new Type[] {})]
public static class DisableTextScroll
{
    public static void Prefix(CommandDescText __instance)
    {
        HarmonyLib.Traverse.Create(__instance).Field("helpScroll").SetValue(0);
        string descText = HarmonyLib.Traverse.Create(__instance).Field("descText").GetValue<string>();
        string[] descriptions = descText.Split('.');
        HarmonyLib.Traverse.Create(__instance).Field("descText").SetValue(descriptions[0]+'.');
        GS.m_font_scale_x = 0.5f;
        GS.m_font_scale_y = 0.5f;
        if(descriptions.Length > 1 && descriptions[1].Length > 3)
            GS.DrawString(descriptions[1]+'.', 170, 495, 0, Color.white, GS.FontEffect.SHADOW_WINDOW);
    }
    public static void Postfix()
    {
        GS.m_font_scale_x = 1f;
        GS.m_font_scale_y = 1f;
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommanderDescText), "DescTextUpdate", new Type[] { })]
public static class DisableTextScroll2
{
    public static void Prefix(CommanderDescText __instance)
    {
        HarmonyLib.Traverse.Create(__instance).Field("helpScroll").SetValue(0);
        string descText = HarmonyLib.Traverse.Create(__instance).Field("descText").GetValue<string>();
        string line2 = "";
        GS.m_font_scale_x = 0.5f;
        GS.m_font_scale_y = 0.5f;
        if (descText.Length > 70)
        {
            try
            {
                if (descText.IndexOf('.') < descText.Length - 5)
                {
                    line2 = descText.Substring(descText.IndexOf('.') + 2);
                    descText = descText.Substring(0, descText.IndexOf('.')+1);
                }
                else
                {
                    line2 = descText.Substring(descText.LastIndexOf(' ', 70) + 1);
                    descText = descText.Substring(0, descText.LastIndexOf(' ', 70));
                }
                HarmonyLib.Traverse.Create(__instance).Field("descText").SetValue(descText);
                if (line2 != "")
                {
                    GS.DrawString(line2, 170, 495, 0, Color.white, GS.FontEffect.WINDOW);
                }
            }
            catch
            {
                ;
            }
        }
    }
    public static void Postfix()
    {
        GS.m_font_scale_x = 1f;
        GS.m_font_scale_y = 1f;
    }
}

[HarmonyLib.HarmonyPatch(typeof(ScrollMessage), "Update", new Type[] { })]
public static class DisableTextScroll3
{
    static string prevString = "";
    public static void Prefix(ScrollMessage __instance)
    {
        if (!HarmonyLib.Traverse.Create(__instance).Field("m_isvisible").GetValue<bool>())
            return;

        __instance.ScrollReset();
        string descText = HarmonyLib.Traverse.Create(__instance).Field("m_Messgae").GetValue<string>();
        prevString = descText;
        string line2 = "";
        GS.m_font_scale_x = 0.5f;
        GS.m_font_scale_y = 0.5f;
        if (descText.Length > 70)
        {
            try
            {
                if (descText.IndexOf('.') < descText.Length - 5)
                {
                    line2 = descText.Substring(descText.IndexOf('.') + 2);
                    descText = descText.Substring(0, descText.IndexOf('.') + 1);
                }
                else
                {
                    line2 = descText.Substring(descText.LastIndexOf(' ', 70) + 1);
                    descText = descText.Substring(0, descText.LastIndexOf(' ', 70));
                }
                if (line2 != "")
                {
                    __instance.SetMessgae(descText);
                    GS.DrawString(line2, 175, 495, 0, Color.white, GS.FontEffect.SHADOW_WINDOW);
                }
            }
            catch
            {
                ;
            }
        }
    }
    public static void Postfix(ScrollMessage __instance)
    {
        GS.m_font_scale_x = 1f;
        GS.m_font_scale_y = 1f;
        __instance.SetMessgae(prevString);
    }
}

[HarmonyLib.HarmonyPatch(typeof(GS), "DrawStringMenu")]
public static class TextPosition
{
    public static bool Prefix(ref string str, ref int _x, ref int _y, ref int _z, ref Color32 color, ref GS.FontEffect effect)
    {
        if (RS3UI.windowType == "Command")
        {
            if (_x == 465)
                return false;
            else if (_x >= 573 && _x <= 593)
                _x -= 170;
            _x -= 125;
            for (int i = 0; i < 8; i++)
            {
                if (_y == 126 + i * 40)
                {
                    _y = RS3UI.commandY + i * 26;
                }
                else if (_y == 128 + i * 40)
                {
                    _y = RS3UI.commandY+2 + i * 26;
                }
            }
        }
        else if (RS3UI.windowType == "PageName") {
            _x = 75;
            _y -= 28;
        }
        else if (_x == 586)
        {
            _x = 650;
        }
        return true;
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleMenu.BattleMenuTouch), "AddTouchCommand")]
public static class TextPosition2
{
    public static bool Prefix(ref string[] array, ref int elem)
    {
        for (int i = 0; i < elem; i++)
        {
            InputManager.add_touch_rect2(75, RS3UI.commandY + 26 * i, 200, 30, array[i], true, true, 0);
        }
        return false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleButtonTri), "Init")]
public static class HideExtraButtons
{
    public static void Postfix(BattleButtonTri __instance)
    {
        //private int[] posX = new int[] { -325, 45, -105 };
        //private int[] posY = new int[] { 196, 196, -130 };
        __instance.battleButton[0].SetPosition(-425f, 196);
        __instance.battleButton[1].SetPosition(-55f, 196);
        __instance.battleButton[2].SetPosition(-205f, -130);
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleFormationWindow), "Init")]
public static class FormationChooseWindow
{
    public static void Postfix(BattleFormationWindow __instance)
    {
        CVariableWindow window = HarmonyLib.Traverse.Create(__instance).Field("window").GetValue<CVariableWindow>();
        window.SetPos(55, 20);
        window.SetSize(400, 32);
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleFormationWindow), "Update")]
public static class FormationChooseWindow2
{
    public static void Prefix(BattleFormationWindow __instance)
    {
        RS3UI.windowType = "PageName";
    }

    public static void Postfix(BattleFormationWindow __instance)
    {
        RS3UI.windowType = "";
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleButtonTri), "SetHorizontalButton")]
public static class HideExtraButtons2
{
    public static void Prefix(ref bool _isVisible, BattleButtonTri __instance)
    {
        _isVisible = false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleButtonTri), "SetDownButton")]
public static class HideExtraButtons3
{
    public static void Prefix(ref bool _isVisible, BattleButtonTri __instance)
    {
        _isVisible = false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommandPageNameWindow), "Draw")]
public static class CommandDraw3
{
    public static void Prefix()
    {
        RS3UI.windowType = "PageName";
    }
    public static void Postfix()
    {
        RS3UI.windowType = "";
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommanderMode), "CommanderFormationText")]
public static class CommandDraw6
{
    public static void Prefix()
    {
        RS3UI.windowType = "Command";
    }
    public static void Postfix()
    {
        RS3UI.windowType = "";
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommanderMode), "CommanderTacticsText")]
public static class CommandDraw7
{
    public static void Prefix()
    {
        RS3UI.windowType = "Command";
    }
    public static void Postfix()
    {
        RS3UI.windowType = "";
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommanderMode), "CommanderTacticsDraw")]
public static class CommandDraw8
{
    public static void Prefix()
    {
        RS3UI.windowType = "Command";
    }
    public static void Postfix()
    {
        RS3UI.windowType = "";
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommandMode), "NormalCommandFixedDraw")]
public static class CommandDraw2
{
    public static void Prefix()
    {
        RS3UI.windowType = "Command";
    }
    public static void Postfix()
    {
        RS3UI.windowType = "";
    }
}

[HarmonyLib.HarmonyPatch(typeof(SarahCommander), "CommanderNormalText")]
public static class CommandDraw4
{
    public static void Prefix()
    {
        RS3UI.windowType = "Command";
    }
    public static void Postfix()
    {
        RS3UI.windowType = "";
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommanderMode), "CommanderNormalText")]
public static class CommandDraw5
{
    public static void Prefix()
    {
        RS3UI.windowType = "Command";
    }
    public static void Postfix()
    {
        RS3UI.windowType = "";
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleLogic.BattleScene), "font_reset", new Type[] { })]
public static class FontSize
{
    public static void Postfix()
    {
        if (GameCore.m_userProfile.language == 0)
        {
            GS.FontSize = 30f;
        }
        else
        {
            GS.FontSize = 20f;
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(GameCore), "InitUserProfile", new Type[] { })]
public static class FontSize2
{
    public static void Postfix()
    {
        if (GameCore.m_userProfile.language == 0)
        {
            GS.FontSize = 30f;
        }
        else
        {
            GS.FontSize = 20f;
        }
    }
}

namespace RS3
{
    public class RS3UI : MelonMod
    {
        public static string windowType = "";
        public static string touchType = "";
        public static int commandY = 83;
        public override void OnUpdate()
        {
            if(Input.GetKeyDown(KeyCode.F1))
            {
                ;
            }
        }
    }
}