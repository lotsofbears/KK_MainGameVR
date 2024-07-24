using ADV;
using ADV.Commands.Base;
using HarmonyLib;
using KKAPI.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Motion = Illusion.Game.Elements.EasyLoader.Motion;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using VRGIN.Core;
using KoikatuVR.Settings;
using static UnityEngine.Experimental.Director.FrameData;

namespace KoikatuVR
{
    public static class CrossFader
    {
        public enum CrossFaderMode
        {
            Disabled,
            OnlyInVr,
            OnlyOutsideVr,
            Always
        }
        public static void Initialize(ConfigFile config, bool vrActivated)
        {
            // Avoid clashing with KKS_CrossFader
            if (Chainloader.PluginInfos.ContainsKey("bero.crossfader"))
            {
                VRLog.Warn("Disabling the AnimationCrossFader feature because KKS_CrossFader is installed");
                return;
            }

            var enabled = config.Bind(SettingsManager.sectionGeneral, "Cross-fade character animations", CrossFaderMode.OnlyInVr,
                                      "Interpolate between animations/poses to make transitions look less jarring.\nChanges take effect after a scene change.");

            // Apply changes only after a scene change to avoid cutting off animations and possibly messing up state
            SceneManager.sceneLoaded += (arg0, mode) => ApplyHooks(IsEnabled(vrActivated, enabled.Value));
        }
        private static bool IsEnabled(bool vrActivated, CrossFaderMode mode)
        {
            switch (mode)
            {
                case CrossFaderMode.Disabled:
                    return false;
                case CrossFaderMode.OnlyInVr:
                    return vrActivated;
                case CrossFaderMode.OnlyOutsideVr:
                    return !vrActivated;
                case CrossFaderMode.Always:
                    return true;
                default:
                    VRLog.Warn($"Invalid CrossFaderMode [{mode}], defaulting to Disabled");
                    return false;
            }
        }
        private static Harmony _hi;
        private static void ApplyHooks(bool enable)
        {
            try
            {
                if (enable && _hi == null)
                {
                    _hi = new Harmony(typeof(CrossFader).FullName);
                    _hi.PatchAll(typeof(AdvHooks));
                    _hi.PatchAll(typeof(HSceneHooks));
                }
                else if (!enable && _hi != null)
                {
                    _hi.UnpatchSelf();
                    _hi = null;
                }
            }
            catch (Exception ex)
            {
                VRLog.Error($"Failed to apply AnimationCrossFader hooks (enable={enable}) with exception:\n{ex}");

                // Try to clean up
                try { _hi?.UnpatchSelf(); }
                catch (Exception eex) { UnityEngine.Debug.LogException(eex); }
                _hi = null;
            }
        }

        // CrossFade animations in ADV and TalkScene
        private static class AdvHooks
        {
            [HarmonyPrefix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(Motion), nameof(Motion.Play))]
            public static void AdvMotionAddCrossfadeHook(Motion __instance, Animator animator)
            {
                // Make the animation cross fade from the current one, uses stock game code
                __instance.isCrossFade = true;
                __instance.transitionDuration = Random.Range(0.2f, 0.5f);  //(0.1f, 0.3f);
            }

            #region Disable screen fade effect when ADV is changing character animations

            [HarmonyPrefix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(TalkScene), nameof(TalkScene.AnimePlay))]
            public static void TalkSceneAnimePlayRemoveFadeHook(TalkScene __instance)
            {
                // Disable fades inside TalkScene when touching
                __instance.crossFade = null;
            }

            [HarmonyPrefix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(CharaData), nameof(CharaData.MotionPlay))]
            public static void AdvMotionPlayRemoveFadeHook(ADV.Commands.Base.Motion.Data motion, ref bool isCrossFade)
            {
                if (isCrossFade)
                {
                    //VRLog.Debug("Disabling isCrossFade in MotionPlay");
                    isCrossFade = false;
                }
            }

            [HarmonyPrefix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(Text.Next), nameof(Text.Next.Play), typeof(TextScenario.IMotion[]))]
            public static void AdvNextPlayFadeOverridePre(Text.Next __instance, out CrossFade __state)
            {
                // Setting _crossFade to null effectively disables it, just restore it afterwards
                __state = __instance.scenario._crossFade;
                __instance.scenario._crossFade = null;
            }

            [HarmonyPostfix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(Text.Next), nameof(Text.Next.Play), typeof(TextScenario.IMotion[]))]
            public static void AdvNextPlayFadeOverridePost(Text.Next __instance, CrossFade __state)
            {
                __instance.scenario._crossFade = __state;
            }

            [HarmonyPrefix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(ADV.Commands.Chara.Motion), nameof(ADV.Commands.Chara.Motion.Do))]
            public static void AdvMotionDoFadeOverridePre(ADV.Commands.Chara.Motion __instance, out CrossFade __state)
            {
                // Setting _crossFade to null effectively disables it, just restore it afterwards
                __state = __instance.scenario._crossFade;
                __instance.scenario._crossFade = null;
            }

            [HarmonyPostfix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(ADV.Commands.Chara.Motion), nameof(ADV.Commands.Chara.Motion.Do))]
            public static void AdvMotionDoFadeOverridePost(ADV.Commands.Chara.Motion __instance, CrossFade __state)
            {
                __instance.scenario._crossFade = __state;
            }

#if DEBUG
            //[HarmonyPrefix]
            //[HarmonyWrapSafe]
            //[HarmonyPatch(typeof(CrossFade), nameof(CrossFade.FadeStart))]
            //public static void DebugCrossFadeStartHook(CrossFade __instance, float time)
            //{
            //    //if (__instance.texBase != null)
            //        //VRLog.Warn($"CrossFade.FadeStart called (obj={__instance.GetFullPath()} time={time}) from:\n{new StackTrace(2)}");
            //}
#endif

            #endregion

            #region Fix cross fading not working properly because game constantly reloads the runtimeAnimatorController in ADV, resulting in the start animation being lost and replaced by some other animation

            private static readonly Dictionary<RuntimeAnimatorController, string> _AnimationControllerLookup = new Dictionary<RuntimeAnimatorController, string>();

            [HarmonyPrefix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(Motion), nameof(Motion.LoadAnimator), typeof(Animator))]
            public static bool LoadAnimatorOverridePre(Motion __instance, Animator animator, out bool __state)
            {
                __state = false;

                var animatorController = animator.runtimeAnimatorController;
                if (animatorController == null) return true;

                // If the currently loaded controller was loaded from the same asset, skip loading it
                if (_AnimationControllerLookup.TryGetValue(animatorController, out var hash))
                {
                    var newHash = __instance.bundle + "|" + __instance.asset;
                    if (newHash == hash)
                    {
                        VRLog.Debug($"Skipping loading already loaded animator controller from [{newHash}] on [{animator.GetFullPath()}]");
                        return false;
                    }
                    else _AnimationControllerLookup.Remove(animatorController);
                }

                __state = true;
                return true;
            }

            [HarmonyPostfix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(Motion), nameof(Motion.LoadAnimator), typeof(Animator))]
            public static void LoadAnimatorOverridePost(Motion __instance, Animator animator, bool __state)
            {
                if (__state)
                {
                    var newHash = __instance.bundle + "|" + __instance.asset;
                    // Need to save this in the postfix to get the newly loaded controller
                    _AnimationControllerLookup.Add(animator.runtimeAnimatorController, newHash);
                }
            }

            #endregion
        }

        // CrossFade animations in HScenes, same as the KKS_CrossFader plugin but more compact
        private static class HSceneHooks
        {
            private static HFlag _hflag;

            [HarmonyPostfix]
            [HarmonyPatch(typeof(HSceneProc), nameof(HSceneProc.MapSameObjectDisable))]
            public static void HSceneProcLoadPost(HSceneProc __instance)
            {
                _hflag = __instance.flags;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(CrossFade), nameof(CrossFade.FadeStart), new[] { typeof(float) }, null)]
            public static bool HSceneFadeStartOverrideHook()
            {
                return _hflag == null;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.setPlay), new Type[] { typeof(string), typeof(int)}, null)]
            public static bool HSceneSetPlayHook(string _strAnmName, int _nLayer, ChaControl __instance, ref bool __result)
            {
                if (!KKAPI.MainGame.GameAPI.InsideHScene) return true;
                //if (_hflag == null) _hflag = Object.FindObjectOfType<HFlag>();
                if (_hflag == null) return true;

                //VRLog.Debug($"syncPlay hflag={_hflag} namehash={_strAnmName} nlayer={_nLayer} chara={__instance}");

                switch (_hflag.mode)
                {
                    case HFlag.EMode.peeping:
                        __instance.animBody.CrossFadeInFixedTime(_strAnmName, 0f, _nLayer);
                        __result = true;
                        return false;

                    case HFlag.EMode.houshi:
                    case HFlag.EMode.houshi3P:
                    case HFlag.EMode.houshi3PMMF:
                        {
                            if (_strAnmName == "Oral_Idle_IN" || _strAnmName == "M_OUT_Start")
                            {
                                __instance.animBody.CrossFadeInFixedTime(_strAnmName, 0.2f, _nLayer);
                                __result = true;
                                return false;
                            }
                            break;
                        }
                }

                if ((_strAnmName == "M_Idle" && __instance.animBody.GetCurrentAnimatorStateInfo(0).IsName("M_Touch"))
                    || (_strAnmName == "A_Idle" && __instance.animBody.GetCurrentAnimatorStateInfo(0).IsName("A_Touch"))
                    || (_strAnmName == "S_Idle" && __instance.animBody.GetCurrentAnimatorStateInfo(0).IsName("S_Touch")))
                    return true;

                __instance.animBody.CrossFadeInFixedTime(_strAnmName, Random.Range(0.5f, 1f), _nLayer);
                __result = true;
                return false;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(HMasturbation), nameof(HMasturbation.Proc))]
            [HarmonyPatch(typeof(HLesbian), nameof(HLesbian.Proc))]
            [HarmonyPatch(typeof(HPeeping), nameof(HPeeping.Proc))] // TODO Does this work? Interference with other plugins?)
            [HarmonyPatch(typeof(HAibu), nameof(HAibu.Proc))]
            [HarmonyPatch(typeof(HHoushi), nameof(HHoushi.Proc))]
            [HarmonyPatch(typeof(HSonyu), nameof(HSonyu.Proc))]
            [HarmonyPatch(typeof(H3PHoushi), nameof(H3PHoushi.Proc))]
            [HarmonyPatch(typeof(H3PSonyu), nameof(H3PSonyu.Proc))]
            [HarmonyPatch(typeof(H3PDarkHoushi), nameof(H3PDarkHoushi.Proc))]
            [HarmonyPatch(typeof(H3PDarkSonyu), nameof(H3PDarkSonyu.Proc))]
            public static bool HSceneProcOverrideHook(HActionBase __instance)
            {
                var inTransition = !__instance.female.animBody.GetCurrentAnimatorStateInfo(0).IsName(__instance.flags.nowAnimStateName);
                return !inTransition;
            }
        }
    }
}
