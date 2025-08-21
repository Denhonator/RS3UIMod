using RS3;
using MelonLoader;
using UnityEngine;
using static MelonLoader.MelonLogger;
using System.IO;
using System;
using System.Collections.Generic;

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
        state == GameCore.State.BATTLE ? 30 * Settings.GetGameSpeedByIndex(Settings.instance.battleSpeed) :
        state == GameCore.State.FIELD ? 60 * Settings.GetGameSpeedByIndex(Settings.instance.fieldSpeed) :
        state == GameCore.State.MENU ? 60 :
        state == GameCore.State.TITLE ? 60 :
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
public static class FPSFix4
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

[HarmonyLib.HarmonyPatch(typeof(MenuObjectCharacter), "Update")]
public static class FPSFix5
{
    public static bool Prefix(MenuManager __instance)
    {
        if ((Time.frameCount % 2) == 0 && Application.targetFrameRate > 30)
            return false;
        return true;
    }
}

[HarmonyLib.HarmonyPatch(typeof(FldObject), "Update")]
public static class FPSFix2
{
    public static bool Prefix(FldObject __instance)
    {
        if (!__instance.m_data_path.Contains("fire"))
            HarmonyLib.Traverse.Create(__instance).Field("m_frame_rate_speed").SetValue(Application.targetFrameRate > 30 ? 1 : 2);
        else if ((Time.frameCount % 2) == 0 && Application.targetFrameRate > 30)
            return false;
        return true;
    }
}

[HarmonyLib.HarmonyPatch(typeof(ScriptDrive), "Update")]
public static class FPSFix6
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
public static class FPSFix7
{
    public static void Prefix(ref int method, Window __instance)
    {
        if(method == 1)
            __instance.m_delta = Mathf.Sign(__instance.m_delta) * Time.deltaTime*3f;
    }
}

[HarmonyLib.HarmonyPatch(typeof(Field), "SetEventScroll")]
public static class FPSFix8
{
    public static void Prefix(ref int x, ref int y, ref int frame, Field __instance)
    {
        frame *= 2;
    }
}

[HarmonyLib.HarmonyPatch(typeof(ScriptDrive), "s_wait")]
public static class FPSFix9
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

[HarmonyLib.HarmonyPatch(typeof(ActionVM), "a_moveKeepDir")]
public static class FPSFix11
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
            if (code.opcode == new HarmonyLib.CodeInstruction(System.Reflection.Emit.OpCodes.Ldc_I4_8).opcode)
            {
                yield return new HarmonyLib.CodeInstruction(System.Reflection.Emit.OpCodes.Ldc_I4_S, (sbyte)16);
            }
            else
            {
                yield return code;
            }
        }
    }

    //public static void Prefix(ActionVM __instance, out int __state)
    //{
    //    int waitCounter = HarmonyLib.Traverse.Create(__instance).Field("waitCounter").GetValue<int>();
    //    int actionCounter = HarmonyLib.Traverse.Create(__instance).Field("actionCounter").GetValue<int>();
    //    string[] currentActionParameter = HarmonyLib.Traverse.Create(__instance).Field("currentActionParameter").GetValue<string[]>();
    //    if (!currentActionParameter[0].Contains(";"))
    //    {
    //        currentActionParameter[0] = (StringToInt(currentActionParameter[0])).ToString();
    //    }
    //    __state = waitCounter;
    //}

    //public static void Postfix(ActionVM __instance, int __state)
    //{
    //    int waitCounter = HarmonyLib.Traverse.Create(__instance).Field("waitCounter").GetValue<int>();
    //    if(waitCounter > __state)
    //    {
    //        HarmonyLib.Traverse.Create(__instance).Field("waitCounter").SetValue(waitCounter * 2 + 1);
    //    }
    //}
}

[HarmonyLib.HarmonyPatch(typeof(ActionVM), "a_stay")]
public static class FPSFix12
{
    public static void Prefix(ref int opt, ActionVM __instance)
    {
        int waitCounter = HarmonyLib.Traverse.Create(__instance).Field("waitCounter").GetValue<int>();
        if (waitCounter < 0)
        {
            string[] currentActionParameter = HarmonyLib.Traverse.Create(__instance).Field("currentActionParameter").GetValue<string[]>();
            if (!currentActionParameter[0].Contains(";"))
            {
                currentActionParameter[0] = (FPSFix11.StringToInt(currentActionParameter[0]) * 2).ToString();
            }
        }
    }
}

//[HarmonyLib.HarmonyPatch(typeof(BattleLogic.BattleScene), "act_battle_anim")]
//public static class FPSFix2
//{
//    public static bool Prefix(BattleLogic.BattleScene __instance)
//    {
//        if ((Time.frameCount % 2) == 0 && Application.targetFrameRate > 30)
//        {
//            return false;
//        }
//        return true;
//    }
//}

//[HarmonyLib.HarmonyPatch(typeof(BattleLogic.BattleScene), "act_result_anim")]
//public static class FPSFix6
//{
//    public static bool Prefix(BattleLogic.BattleScene __instance)
//    {
//        if ((Time.frameCount % 2) == 0 && Application.targetFrameRate > 30)
//        {
//            return false;
//        }
//        return true;
//    }
//}

//[HarmonyLib.HarmonyPatch(typeof(BattleLogic.BattleScene), "act_appear_anim")]
//public static class FPSFix7
//{
//    public static bool Prefix(BattleLogic.BattleScene __instance)
//    {
//        if ((Time.frameCount % 2) == 0 && Application.targetFrameRate > 30)
//        {
//            return false;
//        }
//        return true;
//    }
//}

//[HarmonyLib.HarmonyPatch(typeof(BattleEffect), "animation")]
//public static class FPSFix8
//{
//    public static bool Prefix(BattleEffect __instance)
//    {
//        if ((Time.frameCount % 2) == 0 && Application.targetFrameRate > 30)
//        {
//            //if (__instance._mess_queue.Count != 0)
//            //{
//            //    if (__instance._mess_queue.Peek().is_mess_end())
//            //    {
//            //        Utility_T_H.BattleMess battleMess = __instance._mess_queue.Dequeue();
//            //        battleMess.Release();
//            //    }
//            //    else
//            //    {
//            //        __instance._mess_queue.Peek().Draw();
//            //    }
//            //}
//            List<BattleAction> m_cmd_task = HarmonyLib.Traverse.Create(__instance).Field("m_cmd_task").GetValue<List<BattleAction>>();
//            List<Monster> m_monsters = HarmonyLib.Traverse.Create(__instance).Field("m_monsters").GetValue<List<Monster>>();
//            BattleEffect.DISP_PHASE m_disp_phase = HarmonyLib.Traverse.Create(__instance).Field("m_disp_phase").GetValue<BattleEffect.DISP_PHASE>();
//            if (m_cmd_task[__instance.m_act_cnt]._me.Count != 0 && m_disp_phase == BattleEffect.DISP_PHASE.SKILL_WINDOW)
//            {
//                BattleLogic.BattleUnitManager enemy_mng = GameCore.m_battle._enemy_mng;
//                if (m_cmd_task[__instance.m_act_cnt]._me[0] >= 10)
//                {
//                    if (!BattleWork.op_ev_btl_flag)
//                    {
//                        for (int k = 0; k < m_cmd_task[__instance.m_act_cnt]._me.Count; k++)
//                        {
//                            string acter_name = m_cmd_task[__instance.m_act_cnt]._acter_name;
//                            if (acter_name != null)
//                            {
//                                if (!(acter_name == string.Empty))
//                                {
//                                    int num9 = m_cmd_task[__instance.m_act_cnt]._me[k] - 10;
//                                    if (BattleWork.nezumi_event_flag)
//                                    {
//                                        GS.DrawStringMenu(MenuListText.GetText(1, m_monsters[num9].m_monster_id, -1), (int)__instance.m_position[18].x - 60, (int)__instance.m_position[18].y + 135, k, Color.white, GS.FontEffect.SHADOW, 2, 3, 0.85f);
//                                    }
//                                    else
//                                    {
//                                        GS.DrawStringMenu(MenuListText.GetText(1, m_monsters[num9].m_monster_id, -1), (int)__instance.m_position[m_cmd_task[__instance.m_act_cnt]._me[k]].x, (int)__instance.m_position[m_cmd_task[__instance.m_act_cnt]._me[k]].y, k, Color.white, GS.FontEffect.SHADOW, 2, 3, 0.85f);
//                                    }
//                                }
//                            }
//                        }
//                    }
//                }
//            }
//            return false;
//        }
//        return true;
//    }
//}

[HarmonyLib.HarmonyPatch(typeof(Field), "CharaUpdate")]
public static class FPSFix3
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
        int num = ch.GetSpeedDot();
        repeat = 0;

        while (repeat >= 0)
        {
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
            if(repeat==0)
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

                if (num >= 2)
                    num /= fpsmult;
                else if(fpsmult >= 2 && (Time.frameCount % 2) != 0)
                {
                    return false;
                }
                if (fpsmult == 2 && num == 2 && repeat == 0 && ch.m_jump_cnt <= 0)
                {
                    repeat = 2;
                    ch.m_time -= 1;
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
                if (fpsmult == 2 && num == 2 && repeat == 0 && ch.m_jump_cnt <= 0)
                {
                    repeat = 2;
                    ch.m_time -= 1;
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

//[HarmonyLib.HarmonyPatch(typeof(ScriptDrive), "s_winSize")]
//public static class TextBoxHeight
//{
//    public static void Postfix(ref ScriptDrive __instance)
//    {
//        __instance.nextWinRow = Mathf.Min(__instance.nextWinRow, 3);
//    }
//}

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
            if (code.opcode == new HarmonyLib.CodeInstruction(System.Reflection.Emit.OpCodes.Ldc_I4_S, (sbyte)35).opcode)
            {
                yield return new HarmonyLib.CodeInstruction(System.Reflection.Emit.OpCodes.Ldc_I4_S, (sbyte)30);
            }
            else
            {
                yield return code;
            }
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
            commandWindow.SetWindowSize(250, 172);
        else
            commandWindow.SetWindowSize(250, 16 + menuElement * 26);
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
            commandWindow.SetWindowSize(240, 172);
        }
        else
        {
            commandWindow.SetWindowSize(240, 16 + menuElement * 26);
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
        cVariableWindow.SetSize(160, 32);
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
        cVariableWindow.SetSize(340, 32);
    }
}

[HarmonyLib.HarmonyPatch(typeof(CVariableMessagePlus), "SetWindowSize", new Type[] { typeof(int),typeof(int) })]
public static class CompactUI7
{
    public static void Postfix(int WordCountX, int WordCountY, ref CVariableMessagePlus __instance)
    {
        CVariableWindow m_Window = HarmonyLib.Traverse.Create(__instance).Field("m_Window").GetValue<CVariableWindow>();
        m_Window.SetSize(GS.StrDot("M") * WordCountX * 3 / 2, (WordCountY + 1) * GS.StrDot("M") * 3 / 2);
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
        m_Window.SetSize(GS.StrDot("M") * m_WordCountX * 3 / 2, (m_WordCountY+1) * GS.StrDot("M") * 3 / 2);
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
        if (descText.Length > 90)
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
        if (descText.Length > 90)
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
                _x -= 180;
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

//[HarmonyLib.HarmonyPatch(typeof(BattleLogic.BattleScene), "font_reset", new Type[] { })]
//public static class FontSize
//{
//    public static void Postfix()
//    {
//        if (GameCore.m_userProfile.language == 0)
//        {
//            GS.FontSize = 30f;
//        }
//        else
//        {
//            GS.FontSize = 20f;
//        }
//    }
//}

//[HarmonyLib.HarmonyPatch(typeof(GameCore), "InitUserProfile", new Type[] { })]
//public static class FontSize2
//{
//    public static void Postfix()
//    {
//        if (GameCore.m_userProfile.language == 0)
//        {
//            GS.FontSize = 30f;
//        }
//        else
//        {
//            GS.FontSize = 20f;
//        }
//    }
//}

[HarmonyLib.HarmonyPatch(typeof(GS), "DrawString")]
public static class TextOutline
{
    public static void Prefix(ref Color32 color, ref GS.FontEffect effect)
    {
        //if (effect == GS.FontEffect.SHADOW)
        //    effect = GS.FontEffect.RIM;
        if (effect == GS.FontEffect.SHADOW_WINDOW)
            effect = GS.FontEffect.RIM_WINDOW;
        if(color.r < 50 && effect == GS.FontEffect.RIM)
        {
            color = new Color32(255, 255, 255, 255);
        }
        if(color.r < 50 && effect == GS.FontEffect.CURSOR)
        {
            color = new Color32(255, 255, 255, color.a);
        }
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
        public override void OnUpdate()
        {
            if(Input.GetKeyDown(KeyCode.F1))
            {
                ;
            }
        }
    }
}