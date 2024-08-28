using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRGIN.Core;
using VRGIN.Controls;
using UnityEngine;
using HarmonyLib;
using System.Collections;
using KK_VR.Camera;
using System.Runtime.InteropServices;
using Manager;
using KK_VR.Caress;
using ADV.Commands.Camera;
using System.Diagnostics;
using KoikatuVR.Camera;

namespace KK_VR.Interpreters
{
    class TalkSceneInterpreter : SceneInterpreter
    {
        Canvas _canvasBack;
        public static float TalkDistance = 0.55f;
        public static float Height;
        private bool _adjustmentRequired;
        private static TalkScene _talkScene;
        public override void OnDisable()
        {
            DestroyControllerComponent<Controls.TalkSceneHandler>();
            if (_canvasBack != null)
            {
                _canvasBack.enabled = true;
            }
        }

        public override void OnStart()
        {
            AddControllerComponent<Controls.TalkSceneHandler>();

            if (_talkScene == null)
            {
                _talkScene = GameObject.FindObjectOfType<TalkScene>();

                if (_talkScene == null)
                {
                    VRLog.Warn("TalkScene object not found");
                    return;
                }
            }

            _talkScene.otherInitialize += () =>
            {
                VRPlugin.Logger.LogDebug($"talkScene.otherInitialize");
                _adjustmentRequired = true;
            };

            _canvasBack = _talkScene.canvasBack;
        }
        /// <summary>
        /// We wait for TalkScene to load up to the point where chara is ready, and then adjust everything.
        /// </summary>
        private void AdjustHeroine()
        {
            _adjustmentRequired = false;

            if (_talkScene == null) return;
            var head = VR.Camera.Head;
            var origin = VR.Camera.Origin;
            var heroine = _talkScene.targetHeroine.transform;
            var headsetPos = head.position;

            Height = headsetPos.y - heroine.position.y;
            headsetPos.y = heroine.position.y;
            TalkDistance = 0.4f + (_talkScene.targetHeroine.isGirlfriend ? 0f : 0.1f) + (0.1f - _talkScene.targetHeroine.intimacy * 0.001f);

            var offset = 0f;
            if (_sittingAnimations.Contains(_talkScene.targetHeroine.charaBase.motion.state))
            //(_sittingAnimations.Any(anim => _talkScene.targetHeroine.charaBase.motion.state.Equals(anim)))
            {
                offset = 0.25f;
            }

            var rotation = Quaternion.LookRotation(headsetPos - heroine.position);
            var distance = Vector3.Distance(headsetPos, heroine.position);
            var vec = headsetPos - heroine.position;
            heroine.rotation = rotation;
            heroine.position += vec * (offset / distance);

            headsetPos = vec * (TalkDistance / distance) + heroine.position;

            Game.Instance.Player.transform.rotation = rotation * Quaternion.Euler(0f, 180f, 0f);
            Game.Instance.Player.transform.position = headsetPos;

            headsetPos.y = head.position.y;
            origin.rotation = rotation * Quaternion.Euler(0f, 180f, 0f);
            origin.position += headsetPos - head.position;
            VRPlugin.Logger.LogDebug($"TalkScene:Adjust:{_talkScene.targetHeroine.charaBase.motion.state}:{TalkDistance}:{head.position.y}:{heroine.position.y + Height}");
        }
        private static readonly List<string> _sittingAnimations = new List<string>()
        {
            "Reading",
            "Appearance6",
            "Game",
            "ChangeMind6",
            "Phone3",
        };

        private void AdjustPosition()
        {
            if (_talkScene == null) return;
            Height = VR.Camera.Head.position.y - Game.Instance.Player.transform.position.y;
            VRPlugin.Logger.LogDebug($"Interpreter:TalkScene:Height:{Height}");
            // The default camera location is a bit too far for a friendly
            // conversation.
            var heroine = _talkScene.targetHeroine;

            TalkDistance = 0.4f + (heroine.isGirlfriend ? 0f : 0.1f) + (0.15f - heroine.intimacy * 0.0015f); //  + Random.value * 0.25f;
            //TalkDistance = 0.35f + (heroine.isGirlfriend ? 0f : 0.1f) + (0.15f - (int)heroine.HExperience * 0.05f); //  + Random.value * 0.25f;
            var position = heroine.chaCtrl.objHeadBone.transform.TransformPoint(new Vector3(0f, 0f, TalkDistance));
            //var relativeHeight = heroine.transform.TransformPoint(new Vector3(0f, Height, 0f));
            position.y = Height;
            var rotation = heroine.chaCtrl.objHeadBone.transform.rotation * Quaternion.Euler(0, 180f, 0);
            VRMover.Instance.MoveTo(position, rotation, false);
            //VRMover.Instance.MoveTo(
            //    heroine.transform.TransformPoint(new Vector3(0, Height, TalkDistance)),
            //    heroine.transform.rotation * Quaternion.Euler(0, 180f, 0),
            //    false);
        }
        public override void OnUpdate()
        {
            // We don't need the background image because we directly see
            // background objects.
            if (_canvasBack != null)
            {
                _canvasBack.enabled = false;
            }
            if (_adjustmentRequired && Singleton<Communication>.Instance.isInit && _talkScene.targetHeroine.transform.position != Vector3.zero)
            {
                AdjustHeroine(); 
            }
        }
    }
}
