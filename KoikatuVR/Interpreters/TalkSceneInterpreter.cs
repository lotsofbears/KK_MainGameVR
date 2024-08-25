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

namespace KK_VR.Interpreters
{
    class TalkSceneInterpreter : SceneInterpreter
    {
        Canvas _canvasBack;
        public static float TalkDistance = 0.55f;
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

            var talkScene = GameObject.FindObjectOfType<TalkScene>();
            if (talkScene == null)
            {
                VRLog.Warn("TalkScene object not found");
                return;
            }

            talkScene.otherInitialize += () =>
            {
                VRLog.Warn("talkScene.otherInitialize");

                AdjustPosition(talkScene);
            };

            _canvasBack = talkScene.canvasBack;// new Traverse(talkScene).Field<Canvas>("canvasBack").Value;
        }

        public static void AdjustPosition(TalkScene talkScene)
        {
            if (talkScene == null) return;

            // The default camera location is a bit too far for a friendly
            // conversation.
            var heroine = talkScene.targetHeroine;
            TalkDistance = 0.35f + (heroine.isGirlfriend ? 0f : 0.1f) + (0.15f - (int)heroine.HExperience * 0.05f); //  + Random.value * 0.25f;
            VRMover.Instance.MoveTo(
                heroine.transform.TransformPoint(new Vector3(0, ActionCameraControl.GetPlayerHeight(), TalkDistance)),
                heroine.transform.rotation * Quaternion.Euler(0, 180f, 0),
                false);
        }
        public override void OnUpdate()
        {
            // We don't need the background image because we directly see
            // background objects.
            if (_canvasBack != null)
            {
                _canvasBack.enabled = false;
            }
        }
    }
}
