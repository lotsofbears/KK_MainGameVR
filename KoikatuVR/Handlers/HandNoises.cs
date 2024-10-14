using BepInEx;
using KK_VR.Interpreters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine.Networking;
using UnityEngine;

namespace KK_VR.Handlers
{
    internal class HandNoises
    {
        private static List<HandHolder> _hands;
        internal static void Init()
        {
            PopulateDic();
            _hands = HandHolder.GetHands();
        }
        internal static void PlaySfx(int index, float volume, Sfx sfx, Surface surface)
        {
            return;
            var audioSource = _hands[index].GetAudioSource;
            if (audioSource.isPlaying) return;

            var audioClipList = sfxDic[(int)sfx][(int)sfx];
            VRPlugin.Logger.LogDebug($"AttemptToPlay:{sfx}:{sfx}:{volume}");
            var count = audioClipList.Count;
            if (count != 0)
            {
                audioSource.volume = Mathf.Clamp01(volume);
                audioSource.pitch = 0.9f + UnityEngine.Random.value * 0.2f;
                audioSource.clip = audioClipList[UnityEngine.Random.Range(0, count)];
                audioSource.Play();
            }
        }
        internal enum Sfx
        {
            Tap,
            Slap,
            Traverse,
            Undress
        }
        internal enum Surface
        {
            Skin,
            Cloth,
            Hair
        }
        //internal enum Intensity
        //{
        //    // Think about:
        //    //     Soft as something smallish and soft and on slower side of things, like boobs or ass.
        //    //     Rough as something flattish and big and at times intense, like tummy or thighs.
        //    //     Wet as.. I yet to mix something proper for it. WIP.
        //    Soft,
        //    Rough,
        //    Wet
        //}
        private static readonly Dictionary<int, List<List<AudioClip>>> sfxDic = new Dictionary<int, List<List<AudioClip>>>();
        //private static readonly Dictionary<Sfx, List<List<List<AudioClip>>>> sfxDic = new Dictionary<Sfx, List<List<List<AudioClip>>>>();
        private static void InitDic()
        {
            for (var i = 0; i < Enum.GetNames(typeof(Sfx)).Length; i++)
            {
                sfxDic.Add(i, new List<List<AudioClip>>());
                for (var j = 0; j < Enum.GetNames(typeof(Surface)).Length; j++)
                {
                    sfxDic[i].Add(new List<AudioClip>());
                    //for (var k = 0; k < Enum.GetNames(typeof(Intensity)).Length; k++)
                    //{
                    //    sfxDic[sfx][j].Add(new List<AudioClip>());
                    //}
                }
            }
        }
        private static void PopulateDic()
        {
            InitDic();
            for (var i = 0; i < sfxDic.Count; i++)
            {
                for (var j = 0; j < sfxDic[i].Count; j++)
                {
                    //for (var k = 0; k < sfxDic[i][j].Count; k++)
                    //{
                        var directory = BepInEx.Utility.CombinePaths(new string[]
                            {
                                Paths.PluginPath,
                                "SFX",
                                ((Sfx)i).ToString(),
                                ((Surface)j).ToString()
                                //((Intensity)k).ToString()
                            });
                        if (Directory.Exists(directory))
                        {
                            var dirInfo = new DirectoryInfo(directory);
                            var clipNames = new List<string>();
                            foreach (var file in dirInfo.GetFiles("*.wav"))
                            {
                                clipNames.Add(file.Name);
                            }
                            foreach (var file in dirInfo.GetFiles("*.ogg"))
                            {
                                clipNames.Add(file.Name);
                            }
                            if (clipNames.Count == 0) continue;
                            KoikatuInterpreter.Instance.StartCoroutine(LoadAudioFile(directory, clipNames, sfxDic[i][j]));
                        }
                    //}
                }
            }
        }

        private static IEnumerator LoadAudioFile(string path, List<string> clipNames, List<AudioClip> destination)
        {
            foreach (var name in clipNames)
            {
                UnityWebRequest audioFile;
                if (name.EndsWith(".wav"))
                {
                    audioFile = UnityWebRequest.GetAudioClip(Path.Combine(path, name), AudioType.WAV);
                }
                else
                {
                    audioFile = UnityWebRequest.GetAudioClip(Path.Combine(path, name), AudioType.OGGVORBIS);
                }
                //VRPlugin.Logger.LogDebug(Path.Combine(path, name));
                yield return audioFile.Send();//  SendWebRequest();
                if (audioFile.isError)
                {
                    VRPlugin.Logger.LogDebug(audioFile.error);
                    VRPlugin.Logger.LogDebug(Path.Combine(path, name));
                }
                else
                {
                    var clip = DownloadHandlerAudioClip.GetContent(audioFile);
                    clip.name = name;
                    destination.Add(clip);
                    //VRPlugin.Logger.LogDebug($"Loaded:SFX:{name}");
                }
            }
        }
    }
}
