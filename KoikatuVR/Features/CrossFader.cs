using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using ADV;
using ADV.Commands.Base;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using KK_VR.Settings;
using KK_VR;
using KKAPI.MainGame;
using KKAPI.Utilities;
using Manager;
using Unity.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Motion = Illusion.Game.Elements.EasyLoader.Motion;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using Illusion.Extensions;
using Animator = UnityEngine.Animator;
using static Illusion.Utils;
using static TalkScene;

namespace KK_VR.Features
{
    /// <summary>
    /// Based on KKS_CrossFader by Sabakan
    /// </summary>
    public static class CrossFader
    {
        public static bool IsInTransition => _inTransition;
        private static bool _inTransition;
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
                VRPlugin.Logger.LogWarning("Disabling the AnimationCrossFader feature because KK_CrossFader is installed");
                return;
            }
            var enabled = config.Bind(SettingsManager.SectionGeneral, "Cross-fade character animations", CrossFaderMode.OnlyInVr,
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
                    VRPlugin.Logger.LogWarning($"Invalid CrossFaderMode [{mode}], defaulting to Disabled");
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
                VRPlugin.Logger.LogError($"Failed to apply AnimationCrossFader hooks (enable={enable}) with exception:\n{ex}");

                // Try to clean up
                try { _hi?.UnpatchSelf(); }
                catch (Exception eex) { UnityEngine.Debug.LogException(eex); }
                _hi = null;
            }
        }

        // CrossFade animations in ADV and TalkScene
        private static class AdvHooks
        {
            private static bool _reaction;
            private static readonly string _animReaction = "f_reaction_";

            [HarmonyPrefix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(Motion), nameof(Motion.Play))]
            public static void AdvMotionAddCrossfadeHook(Motion __instance, Animator animator)
            {
                // Make the animation cross fade from the current one, uses stock game code
                __instance.isCrossFade = true;
                if (Manager.Scene.Instance.AddSceneName.Equals("Talk") || (Game.IsInstance() && GameAPI.GetADVScene().isActiveAndEnabled))
                {
                    // We use extra long fades for talk scenes and tiny after interactions.
                    // Change of Asset Bundles haunts us.
                    // Capture state (no clue how) before change of asset and immediately adjust it after?
                    // Or feed to crossfader start point from previous asset's state?

                    //var timing = animTimings.Where(kv => __instance.state.StartsWith(kv.Key)).FirstOrDefault().Value;

                    __instance.transitionDuration = _reaction ? Random.Range(0.1f, 0.2f) : Random.Range(0.5f, 1f);
                    _reaction = false;
                    if (__instance.state.StartsWith(_animReaction, StringComparison.Ordinal))
                    {
                        _reaction = true;
                    }
                    VRPlugin.Logger.LogInfo($"CrossFade:Motion:Play:{__instance.state}:{__instance.transitionDuration}");

                }
                else
                    __instance.transitionDuration = Random.Range(0.3f, 0.6f);
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
            //    if (__instance.texBase != null)
            //        VRPlugin.Logger.LogWarning($"CrossFade.FadeStart called (obj={__instance.GetFullPath()} time={time}) from:\n{new StackTrace(2)}");
            //}
#endif

            #endregion

            #region Fix cross fading not working properly because game constantly reloads the runtimeAnimatorController in ADV, resulting in the start animation being lost and replaced by some other animation
            private static bool _talkScene;
            private static readonly Dictionary<RuntimeAnimatorController, string> _AnimationControllerLookup = new Dictionary<RuntimeAnimatorController, string>();
            [HarmonyPrefix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(Motion), nameof(Motion.LoadAnimator), typeof(Animator))]
            public static bool LoadAnimatorPrefix(Motion __instance, Animator animator, out bool __state)
            {
                __state = false;
                var animatorController = animator.runtimeAnimatorController;
                if (animatorController == null) return true;
                //VRPlugin.Logger.LogWarning($"CrossFade:Motion:LoadAnimator:Prefix:{animatorController}:{__instance.state}:{__instance.bundle + "|" + __instance.asset}");

                // TODO Add this as an option, to swap poses > 29 for smooth crossFade.
                // Finish modified AssetBundle.

                //if (!_talkScene && Manager.Scene.Instance.AddSceneName.Equals("Talk"))
                //{
                //    _talkScene = true;
                //    SwapDic();
                //}
                //else if (_talkScene && !Manager.Scene.Instance.AddSceneName.Equals("Talk"))
                //{
                //    _talkScene = false;
                //    RevertDic();
                //}

                // If the currently loaded controller was loaded from the same asset, skip loading it
                if (_AnimationControllerLookup.TryGetValue(animatorController, out var hash))
                {
                    var newHash = __instance.bundle + "|" + __instance.asset;
                    if (newHash == hash)
                    {
                        VRPlugin.Logger.LogDebug($"Skipping loading already loaded animator controller from [{newHash}] on [{animator.GetFullPath()}]");
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
            [HarmonyPostfix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(TalkScene), nameof(TalkScene.TouchFunc))]
            public static void TalkSceneTouchFuncPostfix()
            {
                VRPlugin.Logger.LogDebug($"CrossFader:TalkScene:TouchFunc");
            }
            public static Dictionary<int, Dictionary<string, string[]>> ModDicPoseChara;
            public static Dictionary<int, Dictionary<string, string[]>> OriginalDicPoseChara;
            private static void SwapDic()
            {
                // Once setting is in, mod all charas in question.
                // No need to revert anything then.
                if (ModDicPoseChara.Count == 0)
                {
                    ModDicPoseChara = Communication.instance.dicPoseChara.DeepCopy();
                    VRPlugin.Logger.LogDebug($"CrossFader:DicSwap:Cold");
                }
                else
                {
                    VRPlugin.Logger.LogDebug($"CrossFader:DicSwap:Hot");
                }
                OriginalDicPoseChara = Communication.instance.dicPoseChara;
                var index = Object.FindObjectOfType<TalkScene>().targetHeroine.FixCharaIDOrPersonality;
                var rand = Random.Range(0, 30).ToString();
                if (rand.Count() == 1)
                {
                    rand = "0" + rand;
                }
                var kv = ModDicPoseChara[index].Values.ElementAt(0);
                var kv2 = ModDicPoseChara[index].Values.ElementAt(ModDicPoseChara[index].Count - 1);
                kv[0] = kv2[0] = "adv/motion/controller/adv/00.unity3d";
                kv[1] = kv[4] = kv2[1] = kv2[4] = "cf_adv_00_00";
                kv[2] = kv2[2] = "Stand_" + rand + "_00";
                kv[3] = kv2[3] = "adv/motion/iklist/00.unity3d";
                Communication.instance.dicPoseChara = ModDicPoseChara;
            }
            private static void RevertDic()
            {
                VRPlugin.Logger.LogDebug($"CrossFader:DicRevert");
                Communication.instance.dicPoseChara = OriginalDicPoseChara;
            }
            #endregion
        }

        // CrossFade animations in HScenes, same as the KKS_CrossFader plugin but more compact
        internal static class HSceneHooks
        {
            internal static void SetFlag(HFlag flag) => _hflag = flag;
            private static HFlag _hflag;

            [HarmonyPrefix]
            [HarmonyPatch(typeof(CrossFade), nameof(CrossFade.FadeStart), new[] { typeof(float) }, null)]
            public static bool HSceneFadeStartOverrideHook()
            {
                return _hflag == null;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.setPlay), new System.Type[] { typeof(string), typeof(int) }, null)]
            public static bool HSceneSetPlayHook(string _strAnmName, int _nLayer, ChaControl __instance, ref bool __result)
            {
                if (!GameAPI.InsideHScene) return true;
                if (_hflag == null) _hflag = Object.FindObjectOfType<HFlag>();
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
                        if (_strAnmName == "Oral_Idle_IN" || _strAnmName == "M_OUT_Start")
                        {
                            __instance.animBody.CrossFadeInFixedTime(_strAnmName, 0.2f, _nLayer);
                            __result = true;
                            return false;
                        }
                        break;
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
            //[HarmonyPatch(typeof(HPeeping), nameof(HPeeping.Proc))] // TODO Does this work? Interference with other plugins?)
            [HarmonyPatch(typeof(HAibu), nameof(HAibu.Proc))]
            [HarmonyPatch(typeof(HHoushi), nameof(HHoushi.Proc))]
            [HarmonyPatch(typeof(HSonyu), nameof(HSonyu.Proc))]
            [HarmonyPatch(typeof(H3PHoushi), nameof(H3PHoushi.Proc))]
            [HarmonyPatch(typeof(H3PSonyu), nameof(H3PSonyu.Proc))]
            [HarmonyPatch(typeof(H3PDarkHoushi), nameof(H3PDarkHoushi.Proc))]
            [HarmonyPatch(typeof(H3PDarkSonyu), nameof(H3PDarkSonyu.Proc))]
            public static bool HSceneProcOverrideHook(HActionBase __instance)
            {
                _inTransition = !__instance.female.animBody.GetCurrentAnimatorStateInfo(0).IsName(__instance.flags.nowAnimStateName);
                return !_inTransition;
            }
        }
    }
}