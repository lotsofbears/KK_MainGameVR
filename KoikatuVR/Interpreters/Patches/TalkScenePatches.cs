﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KK_VR.Interpreters.Patches
{
    [HarmonyPatch]
    internal class TalkScenePatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(TalkScene), nameof(TalkScene.Awake))]
        public static void TalkSceneAwakePrefix(TalkScene __instance)
        {
            // A cheap surefire way to differentiate between TalkScene/ADV.
            VRPlugin.Logger.LogDebug($"TalkScene:Awake:{KoikatuInterpreter.CurrentScene}");
            if (KoikatuInterpreter.CurrentScene == KoikatuInterpreter.SceneType.TalkScene)
            {
                ((TalkSceneInterpreter)KoikatuInterpreter.SceneInterpreter).OverrideAdv();
            }
            else
            {
                KoikatuInterpreter.StartScene(KoikatuInterpreter.SceneType.TalkScene, __instance);
            }
        }
    }
}
