using RS3;
using MelonLoader;
using UnityEngine;
using static MelonLoader.MelonLogger;
using System.IO;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Linq;

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

    public static string FilePath => Application.persistentDataPath + "/UImodsettings.json";


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

    public static int GetGameSpeedByIndex(int idx) => 
        idx==1 ? instance.fastFps:
        idx==2 ? instance.turboFps:
        instance.normalFps;

    public static Settings instance = new Settings();

    public bool skipLogos = true;

    public int normalFps = 1;
    public int fastFps = 2;
    public int turboFps = 3;

    public int battleSpeed = 0;
    public int fieldSpeed = 0;
    public int otherSpeed = 0;

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
        state == GameCore.State.BATTLE ? 60 * Settings.GetGameSpeedByIndex(Settings.instance.battleSpeed) :
        state == GameCore.State.FIELD ? 60 * Settings.GetGameSpeedByIndex(Settings.instance.fieldSpeed) :
        state == GameCore.State.MENU ? 60 :
        state == GameCore.State.TITLE ? 60 :
        state == GameCore.State.OPENNING ? 60 * Settings.GetGameSpeedByIndex(Settings.instance.otherSpeed) :
                                        30 * Settings.GetGameSpeedByIndex(Settings.instance.otherSpeed);

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
        else
        {
            Settings.instance.otherSpeed = (Settings.instance.otherSpeed + 1) % 3;
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

[HarmonyLib.HarmonyPatch(typeof(Character), "Update")]
public static class FPSFixCharacterAnime
{
    public static void Prefix(Character __instance)
    {
        if (Character.m_speed_anime_step[0] == 8 && Application.targetFrameRate > 30)
        {
            for (int i = 0; i < Character.m_speed_anime_step.Length; i++)
            {
                Character.m_speed_anime_step[i] *= 2;
            }
        }
        else if (Character.m_speed_anime_step[0] > 8 && Application.targetFrameRate == 30)
        {
            for (int i = 0; i < Character.m_speed_anime_step.Length; i++)
            {
                Character.m_speed_anime_step[i] /= 2;
            }
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(FldObject), "Update")]
public static class FPSFixFldObject
{
    public static bool Prefix(FldObject __instance)
    {
        //Msg(__instance.m_data_path);
        if (!__instance.m_data_path.Contains("fire"))
            HarmonyLib.Traverse.Create(__instance).Field("m_frame_rate_speed").SetValue(Application.targetFrameRate > 30 ? 1 : 2);
        else if ((Time.frameCount % 2) == 0 && Application.targetFrameRate > 30)
            return false;
        return true;
    }
}

[HarmonyLib.HarmonyPatch(typeof(FldObject), "LoadEx")]
public static class FPSFixFldObjectSnow
{
    public static void Postfix(FldObject __instance, ref FldObject.FLD_MOTION[] ___m_pmv_mot, ref System.Collections.ArrayList ___m_seq_data)
    {
        if (__instance.m_name != null && RS3UI.prints)
            Msg(__instance.m_name);
        if (___m_pmv_mot == null || __instance.m_name == null || !__instance.m_name.Contains("snow"))
            return;
        //foreach (FldObject.FLD_MOTION mot in ___m_pmv_mot)
        //{
        //    foreach (FldObject.FLD_KEY key in mot.key)
        //    {
        //        key.frame *= 2;
        //    }
        //}
        foreach (FldObject.FLD_SEQ_DATA seq in ___m_seq_data)
        {
            foreach (FldObject.FLD_MOTION mot in seq.grp)
            {
                foreach (FldObject.FLD_SEQ_KEY key in mot.key)
                {
                    key.shape_id = 0;
                }
            }
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(ScriptDrive), "Update")]
public static class FPSFixKeyDelay
{
    public static void Prefix(ScriptDrive __instance)
    {
        //if ((Time.frameCount % 2) == 0 && Application.targetFrameRate > 30 && __instance.realFrameCount > 0)
        //    __instance.realFrameCount -= 1;
        //if ((Time.frameCount % 2) == 0 && Application.targetFrameRate > 30 && __instance.virtualFrameCount > 0)
        //    __instance.virtualFrameCount -= 1;
        int keyDelay = HarmonyLib.Traverse.Create(__instance).Field("keyDelay").GetValue<int>();
        if ((Time.frameCount % 2) == 0 && Application.targetFrameRate > 30 && keyDelay > 0)
            HarmonyLib.Traverse.Create(__instance).Field("keyDelay").SetValue(keyDelay + 1);
    }
}

[HarmonyLib.HarmonyPatch(typeof(Window), "updateSelectlineAlpha")]
public static class FPSFixDialogBlink
{
    private static Color32[] m_msg_color = new Color32[]
{
        new Color32(51, 0, 51, byte.MaxValue),
        new Color32(0, 0, 0, byte.MaxValue),
        new Color32(byte.MaxValue, 0, 0, byte.MaxValue),
        new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue),
        new Color32(0, byte.MaxValue, byte.MaxValue, byte.MaxValue)
};

    public static void Prefix(ref int method, Window __instance)
    {
        if (method == 1)
        {
            __instance.m_delta = Mathf.Sign(__instance.m_delta) * Time.deltaTime * 2f;
        }
    }

    public static void Postfix(ref int method, Window __instance, ref int ___m_spr_cursor_x, ref int ___m_spr_cursor_y, ref byte ___m_select_line_alpha)
    {
        foreach (Window.DrawText drawText in __instance.m_draw_text)
        {
            int type2 = __instance.m_type;
            Color color;
            if (__instance.m_type != 1)
                color = m_msg_color[drawText.m_color];
            else
                color = m_msg_color[3];
            color.a = __instance.m_text_alpha;
            if (drawText.m_color == 2)
                color = Color32.Lerp(Color.white, color, ___m_select_line_alpha / 255f);
            int num = GS.DrawString(drawText.m_str, __instance.m_x + __instance.m_text_x + drawText.m_x, __instance.m_y + __instance.m_text_y + drawText.m_y, 0, color, GS.FontEffect.SHADOW_WINDOW);
            ___m_spr_cursor_x = num;
            ___m_spr_cursor_y = __instance.m_y + __instance.m_text_y + drawText.m_y + 20;
        }
        __instance.m_draw_text.Clear();
    }
}

[HarmonyLib.HarmonyPatch(typeof(Field), "SetEventScroll")]
public static class FPSFixEventScroll
{
    public static void Prefix(ref int x, ref int y, ref int frame, Field __instance)
    {
        frame *= 2;
    }
}

[HarmonyLib.HarmonyPatch(typeof(Field.AbissEvent), "Update")]
public static class FPSFixAbiss
{
    static IEnumerable<HarmonyLib.CodeInstruction> Transpiler(IEnumerable<HarmonyLib.CodeInstruction> instructions)
    {
        foreach (var code in instructions)
        {
            if (code.opcode == new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4_2).opcode)
                yield return new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4_1);
            else
                yield return code;
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(Field.d10out3Evnet), "Update")]
public static class FPSFixd10out
{
    static void Prefix(ref int ___m_timer)
    {
        if(Application.targetFrameRate>30)
        {
            if (___m_timer < 30 && ___m_timer > 0 && Time.frameCount % 2 == 0)
                ___m_timer--;
            else if (___m_timer >= 30 && GameCore.m_field.m_bg_event_scroll_y < 0)
                GameCore.m_field.m_bg_event_scroll_y--;
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(ScriptDrive), "s_wait")]
public static class FPSFixWait2
{
    private static int HexStringToInt(string hexstr)
    {
        string text = hexstr.ToLower();
        return int.Parse(text, System.Globalization.NumberStyles.HexNumber);
    }

    public static void Prefix(ScriptDrive __instance)
    {
        string[] array = HarmonyLib.Traverse.Create(__instance).Field("currentParameter").GetValue<string[]>();
        if (array[0][0] == '$')
        {
            int w = HexStringToInt(array[0].Substring(1)) * 2;
            array[0] = "$" + w.ToString("X");
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(ScriptDrive), "DispatchCommand")]
public static class FPSFixTextFade
{
    public static void Prefix(string cmd, ScriptDrive __instance)
    {
        string[] array = HarmonyLib.Traverse.Create(__instance).Field("currentParameter").GetValue<string[]>();
        string s = "";
        foreach (string str in array)
            s += str + " ";
        if(RS3UI.prints)
            Msg(cmd + ": " + s);
        if (cmd.Contains("textFade") && array.Length > 0)
        {
            array[0] = (int.Parse(array[0]) * 2).ToString();
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(ActionVM), "DispatchAction")]
public static class FPSFixPrint
{
    public static void Prefix(ref string currentAction, ActionVM __instance)
    {
        if (RS3UI.prints)
            Msg("ActionVM: " + currentAction);
    }
}

[HarmonyLib.HarmonyPatch(typeof(GS), "UpdateFade")]
public static class FPSFixFade
{
    public static void Prefix()
    {
        int m_fade_frame = HarmonyLib.Traverse.Create(typeof(GS)).Field("m_fade_frame").GetValue<int>();
        HarmonyLib.Traverse.Create(typeof(GS)).Field("m_fade_frame").SetValue(Mathf.Max(m_fade_frame - 1, -1));
        int m_transition_frame = HarmonyLib.Traverse.Create(typeof(GS)).Field("m_transition_frame").GetValue<int>();
        HarmonyLib.Traverse.Create(typeof(GS)).Field("m_transition_frame").SetValue(Mathf.Max(m_transition_frame - 1, -1));
        int m_flash_frame = HarmonyLib.Traverse.Create(typeof(GS)).Field("m_flash_frame").GetValue<int>();
        HarmonyLib.Traverse.Create(typeof(GS)).Field("m_flash_frame").SetValue(Mathf.Max(m_flash_frame - 1, -1));
        int m_whiteout_frame = HarmonyLib.Traverse.Create(typeof(GS)).Field("m_whiteout_frame").GetValue<int>();
        HarmonyLib.Traverse.Create(typeof(GS)).Field("m_whiteout_frame").SetValue(Mathf.Max(m_whiteout_frame - 1, -1));
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleEffect.BattleFader), "Fader")]
public static class FPSFixFadeBattle
{
    public static void Prefix(ref int ___fade_end_frame)
    {
        int fade_frame = HarmonyLib.Traverse.Create(typeof(GS)).Field("fade_frame").GetValue<int>();
        HarmonyLib.Traverse.Create(typeof(GS)).Field("fade_frame").SetValue(fade_frame-1);
    }
}

[HarmonyLib.HarmonyPatch(typeof(ScriptDrive), "s_shootingStar")]
public static class FPSFixShootingStar
{
    public static void Prefix(ScriptDrive __instance)
    {
        if ((Time.frameCount % 2) == 0 && Application.targetFrameRate > 30)
        {
            int waitCounter = HarmonyLib.Traverse.Create(__instance).Field("waitCounter").GetValue<int>();
            if(waitCounter>0)
                HarmonyLib.Traverse.Create(__instance).Field("waitCounter").SetValue(waitCounter - 1);
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(ActionVM), "a_moveKeepDir")]
public static class FPSFixMoveKeepDir
{
    public static int StringToInt(string str)
    {
        int num;
        try
        {
            num = Convert.ToInt32(str.ToLower());
        }
        catch (Exception)
        {
            num = 0;
        }
        return num;
    }

    static IEnumerable<HarmonyLib.CodeInstruction> Transpiler(IEnumerable<HarmonyLib.CodeInstruction> instructions)
    {
        foreach (var code in instructions)
        {
            if (code.opcode == new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4_8).opcode)
                yield return new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)16);
            else
                yield return code;
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(ActionVM), "a_stay")]
public static class FPSFixStay2
{
    public static void Prefix(ref int opt, ActionVM __instance)
    {
        int waitCounter = HarmonyLib.Traverse.Create(__instance).Field("waitCounter").GetValue<int>();
        if (waitCounter < 0)
        {
            string[] currentActionParameter = HarmonyLib.Traverse.Create(__instance).Field("currentActionParameter").GetValue<string[]>();
            if (!currentActionParameter[0].Contains(";"))
            {
                currentActionParameter[0] = (FPSFixMoveKeepDir.StringToInt(currentActionParameter[0]) * 2).ToString();
            }
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleEffect.SSObject), "Update")]
public static class FPSFixSSObject
{
    public static bool Prefix(SSObject.Anime __instance)
    {
        if (Application.targetFrameRate > 30 && (Time.frameCount % 2) == 0)
            return false;
        return true;
    }
}

[HarmonyLib.HarmonyPatch(typeof(Monster), "Update")]
public static class FPSFixSSObject2
{
    public static bool Prefix(Monster __instance)
    {
        if (Application.targetFrameRate > 30 && (Time.frameCount % 2) == 0)
            return false;
        return true;
    }
}

[HarmonyLib.HarmonyPatch(typeof(SSObject), "Update")]
public static class FPSFixSSObjectSSMovie
{
    public static void Prefix(SSObject __instance)
    {
        if (__instance.GetType() == typeof(EventUtil.ScriptSSObject) && EventUtil.spriteMovieData[0] != null)
        {
            int curFrame = EventUtil.spriteMovieData[0].m_anime[EventUtil.spriteMovieData[0].m_cur_anim_idx].m_cur_frame;
            int endFrame = EventUtil.spriteMovieData[0].m_anime[EventUtil.spriteMovieData[0].m_cur_anim_idx].m_end_frame;
            if (Application.targetFrameRate > 30 && (Time.frameCount % 2) == 0 && curFrame > 0 && endFrame - curFrame > 10)
                EventUtil.spriteMovieData[0].m_anime[EventUtil.spriteMovieData[0].m_cur_anim_idx].m_cur_frame--;
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleEffect), "EffectLoader")]
public static class FPSFixCmdData
{
    public static void Postfix(string filepath, ref string[,] __result)
    {
        TextAsset textAsset = Resources.Load(filepath) as TextAsset;
        string[] array = textAsset.text.Split('\n');
        string[] array2 = array[0].Split(',');
        string[,] array3 = new string[array.Length, array2.Length*2-1];
        for (int i = 0; i < array.Length; i++)
        {
            string[] array4 = array[i].Replace(",", ",,").Replace("fadeout:200,,","fadeout:200,").Split(',');
            for (int j = 0; j < array4.Length; j++)
            {
                array3[i, j] = array4[j];
            }
        }
        Resources.UnloadAsset(textAsset);
        __result = array3;
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleEffect), "cmd_pcmodoru")]
public static class FPSFixAfterActionJump
{
    public static bool Prefix(BattleEffect __instance, ref int ___pcmodoru_frame_count)
    {
        if (Application.targetFrameRate > 30 && (Time.frameCount % 2) == 0)
            return false;
        return true;
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleEffect), "exec_cmd")]
public static class FPSFixExecCmd
{
    static string[] halfSpeedFunc = { "gliderspike", "winddart", "dmgskullcrash" };
    public static bool Prefix(ref string cmds, ref string cmds_arg, BattleEffect __instance, ref bool __result, ref int ___frame_cnt)
    {
        if (Application.targetFrameRate > 30 && (Time.frameCount % 2) == 0)
        {
            foreach(string s in halfSpeedFunc)
            {
                if(cmds.Contains(s) && cmds.Contains("calc"))
                {
                    Msg("Updating " + cmds + " at half rate");
                    cmds = "";
                }
            }
        }
        Msg(cmds + " : " + cmds_arg);
        if ((cmds.Contains("mv") || cmds.Contains("moncolor")) && !cmds.Contains("calc") && cmds_arg.Contains("_"))
        {
            string[] split = cmds_arg.Split('_');
            if (cmds_arg.StartsWith("me") || cmds_arg.StartsWith("you") || cmds_arg[0]=='0')
            {
                int frames = int.Parse(split[1]);
                split[1] = (frames*2).ToString();
                string s = "";
                for(int i = 0; i < split.Length; i++)
                {
                    s += split[i];
                    if (i + 1 < split.Length)
                        s += "_";
                }
                cmds_arg = s;
                Msg("Modified " + cmds + " to " + cmds_arg);   
            }
        }
        return true;
    }
}

//[HarmonyLib.HarmonyPatch(typeof(BattleEffect), "cmd_monswinddart")]
//public static class FPSFixSpecialEff
//{
//    public static void Prefix(BattleEffect __instance, ref int ___mons_winddart_frame_count)
//    {
//        if (Application.targetFrameRate > 30 && (Time.frameCount % 2) == 0)
//            ___mons_winddart_frame_count = Mathf.Max(___mons_winddart_frame_count - 1, 0);
//    }
//}

[HarmonyLib.HarmonyPatch(typeof(EventUtil), "UpdateFade")]
public static class FPSFixUpdateFade
{
    public static void Prefix()
    {
        int fadeFrame = HarmonyLib.Traverse.Create(typeof(EventUtil)).Field("fadeFrame").GetValue<int>();
        HarmonyLib.Traverse.Create(typeof(EventUtil)).Field("fadeFrame").SetValue(fadeFrame - 1);
    }
}

[HarmonyLib.HarmonyPatch(typeof(SpecialEffMonster), "draw")]
public static class FPSFixSpecialEff
{
    public static bool Prefix(SpecialEffMonster __instance)
    {
        if (Application.targetFrameRate > 30 && (Time.frameCount % 2) == 0)
            return false;
        return true;
    }
}

//[HarmonyLib.HarmonyPatch(typeof(BattleLogic.SSOExecter), "update")]
//public static class FPSFixSSOExecter
//{
//    public static bool Prefix(BattleLogic.SSOExecter __instance)
//    {
//        if (Application.targetFrameRate > 30 && (Time.frameCount % 2) == 0)
//            return false;
//        return true;
//    }
//}

[HarmonyLib.HarmonyPatch(typeof(BattleEffect), "RollingJumpCharacter")]
public static class FPSFixBattleWinJump
{
    public static void Postfix(int actchar, BattleEffect __instance, ref bool __result)
    {
        if (__instance._frame_counter < 44)
            __result = false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleEffect), "Move")]
public static class FPSFixBattleMove2
{
    public static int[] cnt = new int[6];
    public static void Prefix(Vector2 s, Vector2 e, ref int frame, float height, int actchar, BattleEffect __instance)
    {
        if (actchar == -1)
            return;
        frame = frame*2;
        int[] appear_chmv_cnt = HarmonyLib.Traverse.Create(__instance).Field("appear_chmv_cnt").GetValue<int[]>();

        if (appear_chmv_cnt[actchar] == 0 && cnt[actchar] > 2)
            cnt[actchar] = 0;
        appear_chmv_cnt[actchar] = cnt[actchar];
        cnt[actchar]++;
    }

    public static void Postfix(Vector2 s, Vector2 e, ref int frame, float height, int actchar, BattleEffect __instance, ref bool __result)
    {
        if (actchar == -1)
            return;
        int[] appear_chmv_cnt = HarmonyLib.Traverse.Create(__instance).Field("appear_chmv_cnt").GetValue<int[]>();
        appear_chmv_cnt[actchar] = cnt[actchar] / 2;
        if (cnt[actchar] < frame + 4)
            __result = false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleEffect), "AppearMove")]
public static class FPSFixBattleMove
{
    public static int[] cnt = new int[6];
    public static void Prefix(Vector2 s, Vector2 e, ref int frame, float height, int actchar, BattleEffect __instance)
    {
        if (actchar == -1)
            return;
        frame *= 2;
        int[] appear_chmv_cnt = HarmonyLib.Traverse.Create(__instance).Field("appear_chmv_cnt").GetValue<int[]>();

        if (appear_chmv_cnt[actchar] == 0 && cnt[actchar] > 2)
            cnt[actchar] = 0;
        appear_chmv_cnt[actchar] = cnt[actchar];
        cnt[actchar]++;
    }

    public static void Postfix(Vector2 s, Vector2 e, ref int frame, float height, int actchar, BattleEffect __instance, ref bool __result)
    {
        if (actchar == -1)
            return;
        int[] appear_chmv_cnt = HarmonyLib.Traverse.Create(__instance).Field("appear_chmv_cnt").GetValue<int[]>();
        appear_chmv_cnt[actchar] = cnt[actchar] / 2;
        if (cnt[actchar] < frame + 12 && cnt[actchar] >= frame)
        {
            __instance.m_shape[actchar] = 26;
            __result = false;
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(MenuObjectCharacter), "FuncJumpMove")]
public static class FPSFixMenuFormationJump
{
    static IEnumerable<HarmonyLib.CodeInstruction> Transpiler(IEnumerable<HarmonyLib.CodeInstruction> instructions)
    {
        foreach (var code in instructions)
        {
            if (code.opcode == new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4_S).opcode && (sbyte)code.operand==20)
            {
                HarmonyLib.CodeInstruction newCode = new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)40);
                newCode.labels = code.labels;
                yield return newCode;
            }
            else
                yield return code;
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(MenuObjectCharacter), "FuncAppearMove")]
public static class FPSFixMenuFormationAppear
{
    static IEnumerable<HarmonyLib.CodeInstruction> Transpiler(IEnumerable<HarmonyLib.CodeInstruction> instructions)
    {
        foreach (var code in instructions)
        {
            if (code.opcode == new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4_S).opcode && (sbyte)code.operand == 15)
            {
                HarmonyLib.CodeInstruction newCode = new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)30);
                newCode.labels = code.labels;
                yield return newCode;
            }
            else
                yield return code;
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(MenuObjectCharacter), "Update")]
public static class FPSFixMenuWait
{
    public enum ANIME_TYPE
    {
        JUMP_MOVE,APPEAR_MOVE,GO_LEFT,GO_DOWN,GO_RETURN,TEC_POSE,ARTS_POSE,MASCON_POSE,TITLE_POSE,DASH,NONE
    }
    public static bool Prefix(MenuObjectCharacter __instance, ref int ___m_waitCount, ref ANIME_TYPE ___m_animeType)
    {
        if ((Time.frameCount % 2) == 0 && Application.targetFrameRate > 30)
        {
            if(___m_animeType >= ANIME_TYPE.GO_LEFT || ___m_waitCount > 0)
                return false;
        }
        return true;
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleEffect), "animation")]
public static class FPSFixBattleEffectStop
{
    static BattleEffect.DISP_PHASE lastPhase = BattleEffect.DISP_PHASE.WAIT;
    public static bool Prefix(BattleEffect __instance)
    {
        BattleEffect.DISP_PHASE phase = HarmonyLib.Traverse.Create(__instance).Field("m_disp_phase").GetValue<BattleEffect.DISP_PHASE>();
        bool slow = phase == BattleEffect.DISP_PHASE.SKILL_WINDOW
                    || phase == BattleEffect.DISP_PHASE.WAIT || phase == BattleEffect.DISP_PHASE.MIKIRI_HIRAMEKI_TIME
                    || phase == BattleEffect.DISP_PHASE.TOTAL_ECLIPSE
                    || phase == BattleEffect.DISP_PHASE.SERVANT_UP || phase == BattleEffect.DISP_PHASE.SERVANT_DOWN;

        if (slow && Application.targetFrameRate > 30 && (Time.frameCount % 2) == 0 && lastPhase == phase)
        {
            if (RS3UI.enemyName != null)
            {
                RS3UI.enemyName.Invoke();
                RS3UI.enemyName = null;
            }
            lastPhase = phase;
            return false;
        }
        lastPhase = phase;
        return true;
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleEffect), "StatusUP")]
public static class FPSFixGrowSpin
{
    public static bool Prefix(BattleEffect __instance)
    {
        if ((Time.frameCount % 2) == 0 && Application.targetFrameRate > 30)
            return false;
        return true;
    }
}

[HarmonyLib.HarmonyPatch]
public static class FPSFixBgFader
{
    public static System.Reflection.MethodBase TargetMethod()
    {
        Type type = HarmonyLib.AccessTools.FirstInner(typeof(BattleEffect), t => t.Name.Contains("BattleEffectBgFader"));
        return HarmonyLib.AccessTools.FirstMethod(type, method => method.Name.Contains("UpdateBgFader"));
    }

    public static void Prefix()
    {
        Msg("BgFader");
    }

    static IEnumerable<HarmonyLib.CodeInstruction> Transpiler(IEnumerable<HarmonyLib.CodeInstruction> instructions)
    {
        foreach (var code in instructions)
        {
            if (code.opcode == new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4_2).opcode)
                yield return new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4_1);
            else
                yield return code;
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(Field.ShipMoveEvnet), "Update")]
public static class FPSFixShip
{
    public static void Prefix(Field.ShipMoveEvnet __instance)
    {
        if (Application.targetFrameRate > 30)
        {
            __instance.m_rad -= 0.1308997f * 0.5f;

            int finalSpeed = 0;
            if (__instance.m_spd_x+__instance.m_acc_x < 0)
                __instance.m_spd_x = 0;
            if (__instance.m_spd_x == 0)
                return;
            if ((__instance.m_ascr & 4) != 0)
                finalSpeed = (__instance.m_spd_x + __instance.m_acc_x) >> 8; //1 for every 256
            if ((__instance.m_ascr & 8) != 0)
                finalSpeed = -((__instance.m_spd_x + __instance.m_acc_x) >> 8);

            if (Mathf.Abs(finalSpeed) % 2 == 1 && Time.frameCount % 2 == 0)
                __instance.m_bg_x -= finalSpeed / Mathf.Abs(finalSpeed);
            if (Mathf.Abs(finalSpeed) >= 2)
                __instance.m_bg_x -= finalSpeed / 2;
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(Field.ShipMoveEvnet_f14), "Update")]
public static class FPSFixShip3
{
    public static void Prefix(Field.ShipMoveEvnet_f14 __instance)
    {
        if (Application.targetFrameRate > 30)
        {
            __instance.m_rad -= 0.1308997f * 0.5f;
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(Field.ShipMoveEvnet_f14), "Update")]
public static class FPSFixVanguard
{
    public static void Prefix(Field.ShipMoveEvnet_f14 __instance)
    {
        if (Application.targetFrameRate > 30)
        {
            __instance.m_rad -= 0.08726647f * 0.5f;
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(Field.ShipMoveEvnet), "Init")]
public static class FPSFixShip2
{
    public static void Postfix(Field.ShipMoveEvnet __instance)
    {
        Field.ShipMoveEvnet shipevent = GameCore.m_field.m_field_event as Field.ShipMoveEvnet;
        shipevent.m_acc_x /= 2;
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleLogic.BattleScene), "draw")]
public static class FPSFixWobble
{
    public static void Prefix(BattleLogic.BattleScene __instance)
    {
        BattleLogic.BattleScene._raster_parameter_vy = RS3UI.rasterparameter.y * Time.deltaTime * -15f;
        BattleLogic.BattleScene._raster_parameter_vx = RS3UI.rasterparameter.x * Time.deltaTime * 15f;
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleLogic.BattleScene), "change_last_bg_to_eclipse")]
public static class RasterParameter
{
    public static void Postfix(BattleLogic.BattleScene __instance)
    {
        RS3UI.rasterparameter = new Vector2(BattleLogic.BattleScene._raster_parameter_vx, BattleLogic.BattleScene._raster_parameter_vy);
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleLogic.BattleScene), "change_last_bg_to_normal")]
public static class RasterParameter2
{
    public static void Postfix(BattleLogic.BattleScene __instance)
    {
        RS3UI.rasterparameter = new Vector2(BattleLogic.BattleScene._raster_parameter_vx, BattleLogic.BattleScene._raster_parameter_vy);
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleEffect), "TisouPlay", new Type[] {  })]
public static class FPSFixWeatherEffect
{
    public static bool Prefix(BattleEffect __instance)
    {
        BattleEffect.SSObject tisou_u = HarmonyLib.Traverse.Create(__instance).Field("tisou_u").GetValue<BattleEffect.SSObject>();
        BattleEffect.SSObject tisou_d = HarmonyLib.Traverse.Create(__instance).Field("tisou_d").GetValue<BattleEffect.SSObject>();
        bool isTisou = HarmonyLib.Traverse.Create(__instance).Field("isTisou").GetValue<bool>();
        int before_id = HarmonyLib.Traverse.Create(__instance).Field("before_id").GetValue<int>();

        int num = BattleWork.current_chisou_chi_id;
        if (BattleWork.current_chisou_ten_id == 8)
        {
            num = 8;
        }
        if (num == 0 || !isTisou)
            return false;
        if (num != before_id)
            return true;

        if(Time.frameCount % (Application.targetFrameRate / 30) == 0)
        {
            tisou_d.Update();
            tisou_u.Update();
        }

        tisou_d.Draw();
        GS.BeginRenderTexture(0);
        tisou_u.Draw();
        GS.EndRenderTexture(0);
        if (num == 8)
        {
            __instance.raster_param_x[0].z += 0.01f * Time.deltaTime * 30f;
            __instance.raster_param_y[0].z += 0.02f * Time.deltaTime * 30f;
            GS.DrawRasterTexture(0, 0, GS.GetRenderTexture(0), Color.white, __instance.raster_param_x[0], __instance.raster_param_y[0], 2700);
        }
        else
        {
            __instance.raster_param_x[num].z += 0.01f * Time.deltaTime * 30f;
            __instance.raster_param_y[num].z += 0.02f * Time.deltaTime * 30f;
            GS.DrawRasterTexture(0, 0, GS.GetRenderTexture(0), Color.white, __instance.raster_param_x[num], __instance.raster_param_y[num], 2700);
        }

        return false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleLogic.BattleScene), "act_battle_anim")]
public static class FPSFixBattleAnim
{
    public static bool Prefix(BattleLogic.BattleScene __instance)
    {
        if ((Time.frameCount % 2) == 0 && Application.targetFrameRate > 30)
            return false;
        return true;
    }
}

//[HarmonyLib.HarmonyPatch(typeof(BattleEffect), "ResultAnim")]
//public static class FPSFixResultAnim
//{
//    public static bool Prefix(BattleEffect __instance)
//    {
//        if ((Time.frameCount % 2) == 0 && Application.targetFrameRate > 30)
//        {
//            if (__instance._frame_counter > 0)
//                __instance._frame_counter--;
//            return false;
//        }
//        return true;
//    }
//}

[HarmonyLib.HarmonyPatch(typeof(BattleEffect), "SpinCharacter")]
public static class FPSFixBetweenTurnSpin
{
    public static bool Prefix(BattleEffect __instance)
    {
        if ((Time.frameCount % 2) == 0 && Application.targetFrameRate > 30)
            return false;
        return true;
    }
}

[HarmonyLib.HarmonyPatch(typeof(Field), "CharaUpdate")]
public static class FPSFixMovement
{
    static int repeat = 0;

    static bool btst(int a, int b)
    {
        return (a & b) != 0;
    }
    public static bool Prefix(ref Character ch, Field __instance)
    {
        if (DebugMenu.m_hide_npc && !ch.IsPlayer())
        {
            return false;
        }
        if (!ch.m_flag_ok)
        {
            return false;
        }
        int fpsmult = Application.targetFrameRate > 30 ? 2 : 1;
        repeat = 0;

        if (ch.m_dash && ((ch.m_x % 2) != 0) || ((ch.m_y % 2) != 0))
            ch.m_dash = false;

        while (repeat >= 0)
        {
            int num = ch.GetSpeedDot();
            int prevx = ch.m_x;

            int attr = __instance.m_bg.GetAttr(ch.m_cell_x, ch.m_cell_y);
            int m_jump_attr = HarmonyLib.Traverse.Create(__instance).Field("m_jump_attr").GetValue<int>();
            if (ch.m_jump_cnt > 0 && m_jump_attr == 8192)
            {
                ch.m_dash = true;
            }
            if (ch.m_dash && (ch.m_x & 3) == 0 && (ch.m_y & 3) == 0)
            {
                num = 4;
            }
            if (ch.m_jump_cnt > 0 && m_jump_attr == 4096)
            {
                ch.m_dash = false;
                num = 2;
            }
            if (ch.m_jump_cnt > 0 && m_jump_attr == 8192)
            {
                num = 4;
            }
            ch.m_flags &= -129;
            if ((ch.m_bg_attr & 8388608) == 0)
            {
                if (ch.m_moving)
                {
                    if (btst(ch.m_bg_attr, 1048576) && num >= 2)
                    {
                        ch.m_flags |= 128;
                        num /= 2;
                        if (ch.m_dash && (ch.m_x & 1) == 0 && (ch.m_y & 1) == 0)
                        {
                            num = 2;
                        }
                    }
                }
                else if (btst(attr, 1048576) && num >= 2)
                {
                    ch.m_flags |= 128;
                    num /= 2;
                    if (ch.m_dash && (ch.m_x & 1) == 0 && (ch.m_y & 1) == 0)
                    {
                        num = 2;
                    }
                }
            }
            if (ch.m_ch != null)
            {
                if (btst(attr, 4194304) && ch.m_ofs_y == 0)
                {
                    if (btst(ch.m_flags, 4))
                    {
                        ch.m_water_draw = true;
                        ch.m_ch.m_half_alpha = true;
                    }
                }
                else
                {
                    ch.m_water_draw = false;
                    ch.m_ch.m_half_alpha = false;
                }
            }
            ch.m_force_dir = 0;
            if (btst(attr, 52428800))
            {
                int num2 = attr & 52428800;
                if (num2 != 2097152)
                {
                    if (num2 != 18874368)
                    {
                        if (num2 != 35651584)
                        {
                            if (num2 == 52428800)
                            {
                                ch.m_force_dir = 4;
                            }
                        }
                        else
                        {
                            ch.m_force_dir = 3;
                        }
                    }
                    else
                    {
                        ch.m_force_dir = 2;
                    }
                }
                else
                {
                    ch.m_force_dir = 1;
                }
            }
            if(repeat==0 && !(fpsmult >= 2 && (Time.frameCount % 2) == 0 && ch.m_dash))
                ch.Update();
            if (ch.m_script_move)
            {
                return false;
            }
            short[] m_jump_kidou = HarmonyLib.Traverse.Create(__instance).Field("m_jump_kidou").GetValue<short[]>();
            short[] m_jump_kidou2 = HarmonyLib.Traverse.Create(__instance).Field("m_jump_kidou2").GetValue<short[]>();
            System.Reflection.MethodInfo dynMethod = __instance.GetType().GetMethod("test_hit_npc",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (ch.m_jump_cnt > 0 && (Time.frameCount % fpsmult)==0)
            {
                if (m_jump_attr == 8192)
                {
                    __instance.CharaMove(ch, ch.m_dir, true, false);
                    ch.m_ofs_y = (int)m_jump_kidou[m_jump_kidou.Length - ch.m_jump_cnt];
                    ch.m_jump_cnt--;
                    if (ch.m_jump_cnt == 0)
                    {
                        ch.m_jump_cnt = -1;
                        dynMethod.Invoke(__instance, new object[] { ch });
                    }
                }
                if (m_jump_attr == 4096)
                {
                    __instance.CharaMove(ch, ch.m_dir, true, false);
                    ch.m_ofs_y = (int)m_jump_kidou2[m_jump_kidou2.Length - ch.m_jump_cnt];
                    ch.m_jump_cnt--;
                    if (ch.m_jump_cnt == 0)
                    {
                        dynMethod.Invoke(__instance, new object[] { ch });
                    }
                }
            }
            int num3 = ch.m_cell_x * 8;
            int num4 = ch.m_cell_y * 8;
            if (btst(attr, 67108864) || ch.m_cmd_opt == 1)
            {
                if (ch.m_dash && (ch.m_x & 3) == 0)
                {
                    num = 4;
                }

                if (num >= 2 && !ch.m_dash)
                    num /= fpsmult;
                else if(fpsmult >= 2 && (Time.frameCount % 2) != 0 && !ch.m_dash)
                {
                    return false;
                }

                if (ch.m_x != num3)
                {
                    ch.m_moving = true;
                    if (ch.m_x < num3)
                    {
                        ch.m_x += num;
                    }
                    else
                    {
                        ch.m_x -= num;
                    }
                    if (ch.m_y < num4)
                    {
                        ch.m_y += num / 2;
                    }
                    else
                    {
                        ch.m_y -= num / 2;
                    }
                }
                else
                {
                    ch.m_moving = false;
                }
                if ((ch.m_dir == 0 || ch.m_dir == 1) && ch.m_y != num4)
                {
                    if (ch.m_y < num4)
                    {
                        ch.m_y += num;
                    }
                    else
                    {
                        ch.m_y -= num;
                    }
                }
            }
            else
            {
                if (num >= 2)
                    num /= fpsmult;
                else if (fpsmult >= 2 && (Time.frameCount % 2) == 0)
                {
                    return false;
                }
                if (ch.m_dash)
                    num = 2;

                if (ch.m_x != num3)
                {
                    ch.m_moving = true;
                    if (ch.m_x < num3)
                    {
                        ch.m_x += num;
                    }
                    else
                    {
                        ch.m_x -= num;
                    }
                }
                if (ch.m_y != num4)
                {
                    ch.m_moving = true;
                    if (ch.m_y < num4)
                    {
                        ch.m_y += num;
                    }
                    else
                    {
                        ch.m_y -= num;
                    }
                }
            }
            bool flag = (ch.m_cmd_opt & 4) != 0;
            if (flag)
            {
                if (ch.m_cmd_target_x == ch.m_cell_x && ch.m_cmd_target_y == ch.m_cell_y)
                {
                    ch.m_cmd_nmove = 0;
                    ch.m_cmd_opt = 0;
                }
                else
                {
                    ch.m_cmd_nmove++;
                }
            }
            if (ch.m_cmd_nmove > 0)
            {
                if (ch.m_x == num3 && (ch.m_y == num4 || ch.m_cmd_opt == 1))
                {
                    if (ch.m_x == num3 && ch.m_y == num4)
                    {
                        ch.m_cmd_nmove--;
                    }
                    ch.m_moving = false;
                    int dir = ch.m_dir;
                    __instance.CharaMove(ch, ch.m_cmd_dir, ch.m_cmd_force, false);
                    if (ch.m_cmd_opt == 2)
                    {
                        ch.SetDir(dir);
                    }
                    ch.m_moving = true;
                    if (ch.m_cmd_nmove == 0)
                    {
                        ch.m_cmd_dir = -1;
                        ch.m_flags &= -17;
                    }
                }
            }
            else
            {
                ch.m_cmd_dir = -1;
                if (ch.m_x == num3 && ch.m_cmd_opt == 1)
                {
                    ch.m_moving = false;
                }
            }
            if (repeat == 1)
                break;
            repeat -= 1;
        }
        return false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(Image), "reload_texture")]
public static class ReplaceCharacterTexture
{
    public static void Postfix(ref Image __instance)
    {
        try
        {
            if (__instance.m_name != "alpha" && File.Exists("ReplaceTexture/" + __instance.m_name))
            {
                try
                {
                    __instance.m_index_tex.LoadImage(File.ReadAllBytes("ReplaceTexture/" + __instance.m_name));
                    __instance.m_index_tex.Apply(false, false);
                }
                catch
                {
                    Msg("Failed to replace" + __instance.m_name);
                }
            }
            else if (__instance.m_name != "alpha")
            {
                if (!Directory.Exists(Path.GetDirectoryName("Extract/" + __instance.m_name)))
                    Directory.CreateDirectory(Path.GetDirectoryName("Extract/" + __instance.m_name));
                File.WriteAllBytes("Extract/" + __instance.m_name, ImageConversion.EncodeToPNG(__instance.m_index_tex));
            }
        }
        catch
        {
            Msg("Failed to save " + __instance.m_name);
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(Image), "reload_palette")]
public static class ReplaceCharacterPalette
{
    public static void Postfix(ref Image __instance)
    {
        try
        {
            if (__instance.m_name != "alpha" && File.Exists("ReplaceTexture/" + __instance.m_palette_tex.name+".png"))
            {
                try
                {
                    __instance.m_palette_tex.LoadImage(File.ReadAllBytes("ReplaceTexture/" + __instance.m_palette_tex.name + ".png"));
                    __instance.m_palette_tex.Apply(false, false);
                }
                catch
                {
                    Msg("Failed to replace" + __instance.m_name);
                }
            }
            else if (__instance.m_name != "alpha")
            {
                if (!Directory.Exists(Path.GetDirectoryName("Extract/" + __instance.m_palette_tex.name + ".png")))
                    Directory.CreateDirectory(Path.GetDirectoryName("Extract/" + __instance.m_palette_tex.name + ".png"));
                File.WriteAllBytes("Extract/" + __instance.m_palette_tex.name + ".png", ImageConversion.EncodeToPNG(__instance.m_palette_tex));
            }
        }
        catch
        {
            Msg("Failed to save " + __instance.m_name);
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
            HarmonyLib.Traverse.Create(__instance).Field("posY").SetValue(11);
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
            HarmonyLib.Traverse.Create(__instance).Field("posY").SetValue(11);
        }
        catch (Exception e)
        {
            Msg("Failed to load " + path + ": " + e.Message);
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
        GS.DrawStringMenu(string.Format("{0} / {1}", num, num2), 920, 29, 0, Color.white, GS.FontEffect.RIM, 1, 3, 0.8f);
        return false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(BattleResetWindow), "StringColorUpdate")]
public static class TextFlashingResetWindow
{
    public static void Prefix(BattleResetWindow __instance)
    {
        float strColorAdd = HarmonyLib.Traverse.Create(__instance).Field("strColorAdd").GetValue<float>();
        float strColor = HarmonyLib.Traverse.Create(__instance).Field("strColor").GetValue<float>();
        HarmonyLib.Traverse.Create(__instance).Field("strColorAdd").SetValue(Mathf.Sign(strColorAdd) * 0.1f / (Application.targetFrameRate / 30f));
    }
}

[HarmonyLib.HarmonyPatch(typeof(SarahCommander), "StrFlashingUpdate")]
public static class TextFlashingSarah
{
    public static void Prefix(CommandMode __instance)
    {
        RS3UI.windowType = "CommandSelect";
        float strColorAdd = HarmonyLib.Traverse.Create(__instance).Field("strColorAdd").GetValue<float>();
        float strColor = HarmonyLib.Traverse.Create(__instance).Field("strColor").GetValue<float>();
        HarmonyLib.Traverse.Create(__instance).Field("strColorAdd").SetValue(Mathf.Sign(strColorAdd) * 0.1f / (Application.targetFrameRate / 30f));
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommandMode), "StrFlashingUpdate")]
public static class TextFlashingCommand
{
    public static void Prefix(CommandMode __instance)
    {
        RS3UI.windowType = "CommandSelect";
        float strColorAdd = HarmonyLib.Traverse.Create(__instance).Field("strColorAdd").GetValue<float>();
        float strColor = HarmonyLib.Traverse.Create(__instance).Field("strColor").GetValue<float>();
        HarmonyLib.Traverse.Create(__instance).Field("strColorAdd").SetValue(Mathf.Sign(strColorAdd) * 0.1f / (Application.targetFrameRate / 30f));
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommanderMode), "StrFlashingUpdate")]
public static class TextFlashingCommander
{
    public static void Prefix(CommandMode __instance)
    {
        RS3UI.windowType = "CommandSelect";
        float strColorAdd = HarmonyLib.Traverse.Create(__instance).Field("strColorAdd").GetValue<float>();
        float strColor = HarmonyLib.Traverse.Create(__instance).Field("strColor").GetValue<float>();
        HarmonyLib.Traverse.Create(__instance).Field("strColorAdd").SetValue(Mathf.Sign(strColorAdd) * 0.1f / (Application.targetFrameRate / 30f));
    }
}

[HarmonyLib.HarmonyPatch(typeof(Window), "AddString")]
public static class WhiteText
{
    public static void Prefix(ref string str, ref int tx, ref int ty, ref int col, ref Window __instance)
    {
        if (col == 0)
            col = 3;
    }
}

[HarmonyLib.HarmonyPatch(typeof(MessageWindow), HarmonyLib.MethodType.Constructor, new Type[] { typeof(int) })]
public static class CompactDialog
{
    public static void Postfix(ref MessageWindow __instance)
    {
        HarmonyLib.Traverse.Create(__instance).Field("height1Line").SetValue(30);
    }
}

[HarmonyLib.HarmonyPatch(typeof(Window), HarmonyLib.MethodType.Constructor)]
public static class WhiteText2
{
    public static void Prefix(ref Window __instance)
    {
        __instance.m_frame_alpha = 1.0f;
    }
}

[HarmonyLib.HarmonyPatch(typeof(ScriptDrive), "SetMessageWindowByNPC")]
public static class TextBoxHeight2
{
    public static void Prefix(ref int id, ref int mapinfoNpcNo, ref int dotwidth, ref int row, ref ScriptDrive __instance)
    {
        __instance.messageWindow[id].lowerMargin = -row * 5;
    }
}

[HarmonyLib.HarmonyPatch(typeof(ScriptDrive), "AddTouch", new Type[] { typeof(int),typeof(int),typeof(int),typeof(int) })]
public static class TextBoxHeight3
{
    public static void Prefix(ref int element, ref int px, ref int py, ref int width)
    {
        py -= 5;
    }
    static IEnumerable<HarmonyLib.CodeInstruction> Transpiler(IEnumerable<HarmonyLib.CodeInstruction> instructions)
    {
        foreach (var code in instructions)
        {
            if (code.opcode == new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4_S).opcode && (sbyte)code.operand==35)
            {
                yield return new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)30);
            }
            else
            {
                yield return code;
            }
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(CVariableMessagePlus), "SetWindowSize_English")]
public static class CompactUI
{
    public static void Postfix(ref string Message, CVariableMessagePlus __instance)
    {
        CVariableWindow m_Window = HarmonyLib.Traverse.Create(__instance).Field("m_Window").GetValue<CVariableWindow>();
        int m_WordCountY = HarmonyLib.Traverse.Create(__instance).Field("m_WordCountY").GetValue<int>();
        int m_WindowSizeX = 0;
        foreach (string text in __instance.m_Message)
        {
            if (m_WindowSizeX < GS.StrDot(text))
                m_WindowSizeX = GS.StrDot(text);
        }
        int m_WindowSizeY = GS.StrDot("M") * (m_WordCountY + 1);
        m_Window.SetSize(Mathf.Max(m_WindowSizeX+8, 80), m_WindowSizeY);
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
        commandWindow.SetWindowPos(55, RS3UI.commandY - 9);
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
            commandWindow.SetWindowSize(260, 172);
        }
        else
        {
            commandWindow.SetWindowSize(260, 16 + menuElement * 26);
        }

        commandWindow.SetWindowPos(55, RS3UI.commandY - 9);
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
        cVariableWindow.SetSize(370, 32);
    }
}

[HarmonyLib.HarmonyPatch(typeof(CVariableMessagePlus), "SetWindowSize", new Type[] { typeof(int),typeof(int) })]
public static class CompactUI7
{
    public static void Postfix(int WordCountX, int WordCountY, ref CVariableMessagePlus __instance)
    {
        CVariableWindow m_Window = HarmonyLib.Traverse.Create(__instance).Field("m_Window").GetValue<CVariableWindow>();
        m_Window.SetSize(GS.StrDot("M") * Mathf.Max(WordCountX,10) * 3 / 2, (WordCountY + 1) * GS.StrDot("M") * 3 / 2);
    }
}

[HarmonyLib.HarmonyPatch(typeof(CVariableMessagePlus), "SetWindowSize", new Type[] { })]
public static class CompactUI8
{
    public static void Postfix(ref CVariableMessagePlus __instance)
    {
        CVariableWindow m_Window = HarmonyLib.Traverse.Create(__instance).Field("m_Window").GetValue<CVariableWindow>();
        int m_WordCountX = HarmonyLib.Traverse.Create(__instance).Field("m_WordCountX").GetValue<int>();
        int m_WordCountY = HarmonyLib.Traverse.Create(__instance).Field("m_WordCountY").GetValue<int>();
        m_Window.SetSize(GS.StrDot("M") * Mathf.Max(m_WordCountX, 10) * 3 / 2, (m_WordCountY+1) * GS.StrDot("M") * 3 / 2);
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommandCursor), "SetCursor", new Type[] { typeof(string[]), typeof(string) })]
public static class CursorPosition
{
    public static void Postfix(ref string[] _array, ref string _name, CommandCursor __instance)
    {
        int num = Array.IndexOf(_array, _name);
        __instance.commandCursor.SetPos(18, RS3UI.commandY - 7 + num * 26);
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommanderCursor), "SetCursor", new Type[] { typeof(string[]), typeof(string) })]
public static class CursorPosition2
{
    public static void Postfix(ref string[] _array, ref string _name, CommanderCursor __instance)
    {
        int num = Array.IndexOf(_array, _name);
        __instance.commandCursor.SetPos(18, RS3UI.commandY - 7 + num * 26);
    }
}

[HarmonyLib.HarmonyPatch(typeof(CommandDescText), "DescTextUpdate", new Type[] {})]
public static class DisableTextScroll
{
    public static void Prefix(CommandDescText __instance)
    {
        HarmonyLib.Traverse.Create(__instance).Field("helpScroll").SetValue(0);
        CVariableWindow cVariableWindow = HarmonyLib.Traverse.Create(__instance).Field("cVariableWindow").GetValue<CVariableWindow>();
        string descText = HarmonyLib.Traverse.Create(__instance).Field("descText").GetValue<string>();
        string[] descriptions = descText.Split('.');
        if (descriptions.Length > 1 && descriptions[0].Length > 77)
        {
            int space = descText.IndexOf(' ', 66);
            descriptions[0] = descText.Substring(0, space);
            descriptions[1] = descText.Substring(space);
            HarmonyLib.Traverse.Create(__instance).Field("descText").SetValue(descriptions[0]);
        }
        else
        {
            descriptions[1] += '.';
            HarmonyLib.Traverse.Create(__instance).Field("descText").SetValue(descriptions[0] + '.');
        }
        GS.m_font_scale_x = 0.6f;
        GS.m_font_scale_y = 0.6f;
        if(descriptions.Length > 1 && descriptions[1].Length > 3)
            GS.DrawString(descriptions[1], 172, 495, 0, Color.white, GS.FontEffect.SHADOW_WINDOW);
        cVariableWindow.SetPos(155, 462);
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
    static IEnumerable<HarmonyLib.CodeInstruction> Transpiler(IEnumerable<HarmonyLib.CodeInstruction> instructions)
    {
        foreach (var code in instructions)
        {
            if (code.opcode == new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4_2).opcode)
            {
                yield return new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4_3);
            }
            else
            {
                yield return code;
            }
        }
    }

    public static void Prefix(CommanderDescText __instance)
    {
        HarmonyLib.Traverse.Create(__instance).Field("helpScroll").SetValue(0);
        string descText = HarmonyLib.Traverse.Create(__instance).Field("descText").GetValue<string>();
        string line2 = "";
        GS.m_font_scale_x = 0.6f;
        GS.m_font_scale_y = 0.6f;

        if (descText.Length > 80)
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
                    GS.DrawString(line2, 172, 495, 0, Color.white, GS.FontEffect.SHADOW_WINDOW);
                }
                CVariableWindow cVariableWindow = HarmonyLib.Traverse.Create(__instance).Field("cVariableWindow").GetValue<CVariableWindow>();
                cVariableWindow.SetPos(155, 462);
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
        GS.m_font_scale_x = 0.6f;
        GS.m_font_scale_y = 0.6f;

        if (descText.Length > 80)
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
                    GS.DrawString(line2, 175, 495, 0, Color.white, GS.FontEffect.RIM);
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
    public static bool Prefix(string str, ref int _x, ref int _y, int _z, Color32 color, ref GS.FontEffect effect, int base_point_x, int base_pont_y, ref float scale)
    {
        if (RS3UI.windowType.Contains("Command"))
        {
            scale = 1.0f;

            if (RS3UI.windowType != "CommandSelect")
                effect = GS.FontEffect.RIM;

            if (_x == 465)
                return false;
            else if (_x >= 573 && _x <= 593)
                _x -= 160;
            _x -= 125;
            for (int i = 0; i < 8; i++)
            {
                if (_y == 126 + i * 40)
                {
                    _y = RS3UI.commandY + i * 26;
                }
                else if (_y == 128 + i * 40)
                {
                    _y = RS3UI.commandY + 2 + i * 26;
                }
            }
            if (str.Length >= 18)
            {
                scale = 0.8f;
                _y += 1;
            }
        }
        else if (RS3UI.windowType == "PageName")
        {
            effect = GS.FontEffect.RIM;
            _x = 75;
            _y -= 28;
        }
        else if (_x == 586)
        {
            effect = GS.FontEffect.RIM;
            _x = 650;
        }
        else
        {
            int x = _x; int y = _y; GS.FontEffect eff = effect; float s = scale;
            RS3UI.enemyName = () => GS.DrawStringMenu(str, x, y, _z, color,  eff, base_point_x, base_pont_y, s);
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

//[HarmonyLib.HarmonyPatch]
//public static class FormationMenuUI
//{
//    public static System.Reflection.MethodBase TargetMethod()
//    {
//        Type type = HarmonyLib.AccessTools.TypeByName("MenuFormation");
//        return HarmonyLib.AccessTools.FirstMethod(type, method => method.Name.Contains("Draw"));
//    }

//    public static void Prefix()
//    {
//        RS3UI.windowType = "Formation";
//    }
//}

//[HarmonyLib.HarmonyPatch]
//public static class FormationMenuUI2
//{
//    public static System.Reflection.MethodBase TargetMethod()
//    {
//        Type type = HarmonyLib.AccessTools.TypeByName("MenuFormation");
//        return HarmonyLib.AccessTools.FirstMethod(type, method => method.Name.Contains("Initialize"));
//    }

//    public static void Postfix(ref CVariableWindow[] ___m_window)
//    {
//        ___m_window[0].SetPos(55, 20);
//        ___m_window[0].SetSize(215, 32);
//        ___m_window[1].SetPos(55, RS3UI.commandY);
//        ___m_window[1].SetSize(215, 32);
//        //___m_window[2].SetPos(565, 20);
//    }
//}

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

[HarmonyLib.HarmonyPatch(typeof(GS), "DrawString")]
public static class TextOutline
{
    public static void Prefix(ref Color32 color, ref GS.FontEffect effect)
    {
        if (RS3UI.windowType == "CommandSelect")
            return;
        //if (effect == GS.FontEffect.SHADOW)
        //    effect = GS.FontEffect.RIM;
        if (effect == GS.FontEffect.SHADOW_WINDOW && color.r > 0 && color.a > 0)
        {
            effect = GS.FontEffect.RIM_WINDOW;
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(MenuGuide.GuideInfo), "AddFont")]
public static class TextColorSwap
{
    public static void Postfix(MenuGuide.GuideInfo __instance)
    {
        MenuObjectFont[] mob = __instance.main.GetMenuObjects<MenuObjectFont>();
        mob[mob.Length - 1].SetColor(1);
    }
}

[HarmonyLib.HarmonyPatch(typeof(GS), "InitFont")]
public static class FontChange
{
    public static bool Prefix(ref GS.FontType type, ref string name)
    {
        if (GS.m_font[(int)type] != null)
        {
            Resources.UnloadAsset(GS.m_font[(int)type]);
            Util.Destroy(GS.m_font_mtl[(int)type]);
            Util.Destroy(GS.m_shadow_mtl[(int)type]);
            Util.Destroy(GS.m_rim_mtl[(int)type]);
            Util.Destroy(GS.m_font_mtl_w[(int)type]);
            Util.Destroy(GS.m_shadow_mtl_w[(int)type]);
            Util.Destroy(GS.m_rim_mtl_w[(int)type]);
            Util.Destroy(GS.m_d_font_mtl[(int)type]);
            Util.Destroy(GS.m_d_shadow_mtl[(int)type]);
        }
        if (File.Exists("rs3font.ttf"))
        {
            AssetBundle ab = AssetBundle.LoadFromFile("rs3font");
            foreach (string s in ab.GetAllAssetNames())
                Msg(s);
            GS.m_font[(int)type] = ab.LoadAsset<Font>("rs3font.ttf");
            Msg("Loaded rs3font.ttf");
        }
        else
            GS.m_font[(int)type] = (Font)Resources.Load(name);

        GS.m_font_mtl[(int)type] = ShaderUtil.CreateMaterial(ShaderUtil.Type.FONT);
        GS.m_font_mtl[(int)type].mainTexture = GS.m_font[(int)type].material.mainTexture;
        GS.m_font_mtl[(int)type].color = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
        GS.m_font_mtl[(int)type].renderQueue = 6001;
        ShaderUtil.SetDepthTest(GS.m_font_mtl[(int)type], UnityEngine.Rendering.CompareFunction.Always);
        GS.m_shadow_mtl[(int)type] = new Material(GS.m_font_mtl[(int)type]);
        ShaderUtil.SetDepthTest(GS.m_shadow_mtl[(int)type], UnityEngine.Rendering.CompareFunction.Always);
        GS.m_shadow_mtl[(int)type].renderQueue = 6000;
        GS.m_shadow_mtl[(int)type].color = new Color32(0, 0, 0, 128);
        GS.m_rim_mtl[(int)type] = new Material(GS.m_font_mtl[(int)type]);
        GS.m_rim_mtl[(int)type].color = new Color32(0, 0, 0, 128);
        ShaderUtil.SetDepthTest(GS.m_rim_mtl[(int)type], UnityEngine.Rendering.CompareFunction.Always);
        GS.m_rim_mtl[(int)type].renderQueue = 6000;
        GS.m_font_mtl_w[(int)type] = new Material(GS.m_font_mtl[(int)type]);
        ShaderUtil.SetDepthTest(GS.m_font_mtl_w[(int)type], UnityEngine.Rendering.CompareFunction.Equal);
        ShaderUtil.SetDepth(GS.m_font_mtl_w[(int)type], 0.5f);
        GS.m_font_mtl_w[(int)type].renderQueue = 6001;
        GS.m_shadow_mtl_w[(int)type] = new Material(GS.m_font_mtl_w[(int)type]);
        ShaderUtil.SetDepthTest(GS.m_shadow_mtl_w[(int)type], UnityEngine.Rendering.CompareFunction.Equal);
        ShaderUtil.SetDepth(GS.m_shadow_mtl_w[(int)type], 0.5f);
        GS.m_shadow_mtl_w[(int)type].renderQueue = 6000;
        GS.m_shadow_mtl_w[(int)type].color = new Color32(0, 0, 0, 128);
        GS.m_rim_mtl_w[(int)type] = new Material(GS.m_font_mtl[(int)type]);
        GS.m_rim_mtl_w[(int)type].color = new Color32(0, 0, 0, 128);
        ShaderUtil.SetDepthTest(GS.m_rim_mtl_w[(int)type], UnityEngine.Rendering.CompareFunction.Equal);
        ShaderUtil.SetDepth(GS.m_rim_mtl_w[(int)type], 0.5f);
        GS.m_rim_mtl_w[(int)type].renderQueue = 6000;
        GS.m_d_font_mtl[(int)type] = new Material(GS.m_font_mtl[(int)type]);
        GS.m_d_font_mtl[(int)type].renderQueue = 8501;
        ShaderUtil.SetDepthTest(GS.m_d_font_mtl[(int)type], UnityEngine.Rendering.CompareFunction.Always);
        GS.m_d_shadow_mtl[(int)type] = new Material(GS.m_font_mtl[(int)type]);
        ShaderUtil.SetDepthTest(GS.m_d_shadow_mtl[(int)type], UnityEngine.Rendering.CompareFunction.Always);
        GS.m_d_shadow_mtl[(int)type].renderQueue = 8500;
        GS.m_d_shadow_mtl[(int)type].color = new Color32(0, 0, 0, 255);

        return false;
    }
}

namespace RS3
{
    public class RS3UI : MelonMod
    {
        public static string windowType = "";
        public static string touchType = "";
        public static int commandY = 83;
        public static int frame = 0;
        public static string replace = "";
        public static Vector2 rasterparameter = new Vector2(0f,0.02f);
        public static Action enemyName = null;
        public static bool prints = false;

        public override void OnUpdate()
        {
            if(Input.GetKeyDown(KeyCode.F1))
            {
                prints = !prints;
            }
        }
    }
}