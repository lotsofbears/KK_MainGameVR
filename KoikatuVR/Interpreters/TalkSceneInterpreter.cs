using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRGIN.Core;
using VRGIN.Controls;
using UnityEngine;
using HarmonyLib;
using System.Collections;

namespace KoikatuVR.Interpreters
{
    class TalkSceneInterpreter : SceneInterpreter
    {
        Canvas _canvasBack;
        public static float TalkDistance = 0.55f; // 0.65f;
        public static float _height = 1.4f;
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
                // The default camera location is a bit too far for a friendly
                // conversation.
                var heroine = talkScene.targetHeroine.transform;
                //var dude = Manager.Game.instance.Player.chaCtrl;
               // var dudeTop = dude.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/a_n_headtop");

                //_height = dudeTop.position.y * 0.94f - dude.transform.position.y;
                TalkDistance = 0.35f + UnityEngine.Random.value * 0.25f;
                VRLog.Debug($"StartTalkScene {_height} | {TalkDistance}");
                Camera.VRMover.Instance.MoveTo(
                    heroine.TransformPoint(new Vector3(0, _height, TalkDistance)),
                    heroine.rotation * Quaternion.Euler(0, 180f, 0),
                    keepHeight: true);
            };

            _canvasBack = new Traverse(talkScene).Field<Canvas>("canvasBack").Value;
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
