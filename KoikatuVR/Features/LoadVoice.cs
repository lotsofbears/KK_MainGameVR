using Illusion.Game;
using KK_VR.Features.Extras;
using KK_VR.Interpreters;
using Manager;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using UnityEngine;
using static SaveData;
using static UnityEngine.Experimental.Director.FrameData;
using Random = UnityEngine.Random;

namespace KK_VR.Features
{
    public static class LoadVoice
    {
        public enum VoiceType
        {
            Laugh,
            Short
        }
        private static string Path = "sound/data/pcm/c**/";
        private static readonly Dictionary<ChaControl, float> voiceCooldown = new Dictionary<ChaControl, float>();
        private static readonly Dictionary<int, string> extraPersonalities = new Dictionary<int, string>()
        {
            { 30, "14" },
            { 31, "15" },
            { 32, "16" },
            { 33, "17" },
            { 34, "20" },
            { 35, "20" },
            { 36, "20" },
            { 37, "20" },
            { 38, "50" }
        };
        public static void Play(VoiceType type, ChaControl chara)//, bool setCooldown)
        {
            VRPlugin.Logger.LogDebug($"Voice:Play:{type}:{chara}");
            // TODO try simultaneous play ?
            //if (voiceCooldown.ContainsKey(chara) & voiceCooldown[chara] > Time.time)
            //{
            //    return;
            //}

            var voiceList = GetVoiceList(type);

            if (voiceList == null)
            {
                return;
            }
            var hExp = Game.Instance.HeroineList
                .Where(h => h.chaCtrl == chara)
                .Select(h => h.HExperience)
                .FirstOrDefault();

            var personalityId = chara.fileParam.personality;

            if (hExp == Heroine.HExperienceKind.不慣れ)
            {
                // They often use the same asset.
                // Hook for this? 
                hExp = Heroine.HExperienceKind.初めて;
            }
            var bundle = Path + voiceList[Random.Range(0, voiceList.Count)];

            // Replace personality id.
            bundle = bundle.Replace("**", (personalityId < 10 ? "0" : "") + personalityId.ToString());

            // Replace hExp if there is any.
            bundle = bundle.Replace("^", ((int)hExp).ToString());
            var index = bundle.LastIndexOf('/');

            // Extract Asset from the string at the end.
            var asset = bundle.Substring(index + 1);

            // Remove it from the string.
            bundle = bundle.Remove(index + 1);

            var h = bundle.EndsWith("h/", StringComparison.OrdinalIgnoreCase);
            bundle = bundle + GetBundle(personalityId, hVoice: h);

            VRPlugin.Logger.LogDebug($"{bundle} + {asset}");
            var setting = new Utils.Voice.Setting
            {
                no = personalityId,
                assetBundleName = bundle,
                assetName = asset,
                pitch = chara.fileParam.voicePitch,
                voiceTrans = chara.objHead.transform

            };
            //chara.ChangeMouthPtn(0, true);
            chara.SetVoiceTransform(Utils.Voice.OnecePlayChara(setting));

            // Graceful treatment for original HVoice?

            //if (setCooldown)
            //{
            //    if (!voiceCooldown.ContainsKey(chara))
            //    {
            //        voiceCooldown.Add(chara, Time.time + 1f);
            //    }
            //}
            //if (KoikatuInterpreter.CurrentScene == KoikatuInterpreter.SceneType.HScene)
            //{
            //    if (HSceneInterpreter.lstChaControl[0] == chara)
            //    {
            //        HSceneInterpreter.hVoice.nowVoices[0].state = HVoiceCtrl.VoiceKind.breathShort;
            //        HSceneInterpreter.hVoice.nowVoices[0].notOverWrite = HSceneInterpreter.hVoice.nowVoices[0].shortInfo.notOverwrite;
            //        HSceneInterpreter.hVoice.nowVoices[0].voiceInfo.isPlay = true;
            //    }
            //    else if (HSceneInterpreter.lstChaControl.Count > 1 && HSceneInterpreter.lstChaControl[1] == chara)
            //    {
            //        HSceneInterpreter.hVoice.nowVoices[1].state = HVoiceCtrl.VoiceKind.breathShort;
            //        HSceneInterpreter.hVoice.nowVoices[1].notOverWrite = HSceneInterpreter.hVoice.nowVoices[1].shortInfo.notOverwrite;
            //        HSceneInterpreter.hVoice.nowVoices[1].voiceInfo.isPlay = true;
            //    }
            //}
        }
        private static string GetBundle(int id, bool hVoice)
        {
            var bundle = "00";
            if (extraPersonalities.ContainsKey(id))
            {
                bundle = extraPersonalities[id];
            }
            if (hVoice)
            {
                return bundle + "_00.unity3d";
            }
            else
                return bundle + ".unity3d";
        }
        private static List<string> GetVoiceList(VoiceType type)
        {
            return type switch
            {
                VoiceType.Laugh => VoiceBundles.Laughs,
                VoiceType.Short => VoiceBundles.Shorts,
                _ => null
            };

        }
    }
}
