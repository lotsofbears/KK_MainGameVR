using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRGIN.Core;
using VRGIN.Controls;
using VRGIN.Helpers;
using UnityEngine;
using HarmonyLib;
using KK_VR.Fixes;
using KK_VR.Interpreters;
using static SteamVR_Controller.ButtonMask;
using KK_VR.Settings;
using KK_VR.Features;
using KK_VR.Controls;

namespace KK_VR.Handlers
{
    /// <summary>
    /// A handler component to be attached to a controller, providing touch/look
    /// functionalities in talk scenes.
    /// 
    /// This component is meant to remain disabled outside talk scenes.
    /// </summary>
    class TalkSceneHandler : ProtectedBehaviour
    {
        private Controller _controller;
        private ColliderTracker _tracker;
        private static KoikatuSettings _settings;

        protected override void OnStart()
        {
            _settings = (KoikatuSettings)VR.Context.Settings;
            _controller = GetComponent<Controller>();
        }

        protected void OnEnable()
        {
            _tracker = new ColliderTracker();
        }

        protected void OnDisable()
        {
            _tracker = null;
        }

        public bool DoUndress(bool decrease)
        {
            if (!_tracker.IsBusy)
            {
                //VRPlugin.Logger.LogDebug($"Handler:Undress:Tracker[{_tracker.IsBusy}]");
                return false;
            }
            var bodyKind = _tracker.GetUndressKind(out var chara);
            //VRPlugin.Logger.LogDebug($"Handler:Undress:Part[{bodyKind}]");
            if (bodyKind != ColliderTracker.Body.None && ClothesHandler.Undress(chara, bodyKind, decrease))
            {
                _controller.StartRumble(new RumbleImpulse(1000));
                return true;
            }
            return false;
        }

        public bool DoReaction(bool triggerPress)
        {
            if (!_tracker.IsBusy || (!triggerPress 
                && (_settings.AutomaticTouching == KoikatuSettings.SceneType.Disabled || _settings.AutomaticTouching == KoikatuSettings.SceneType.HScene)))
            {
                //VRPlugin.Logger.LogDebug($"Handler:Reaction:Tracker[{_tracker.IsBusy}]");
                return false;
            }
            var aibuKind = _tracker.GetReactionKind(out var chara);
            var adv = TalkSceneInterpreter.talkScene == null;
            //VRPlugin.Logger.LogDebug($"Handler:Reaction:Part[{aibuKind}]:Tag[{tag}]");
            _controller.StartRumble(new RumbleImpulse(1000));
            if (aibuKind[1] != 0 && !adv && !CrossFader.AdvHooks.Reaction && (triggerPress || UnityEngine.Random.value < 0.3f))
            {
                // TODO Null ref in adv. Mimic TouchFunc() in adv?
                // Seems quite easy, we just need to grab corresponding assets.
                TalkSceneInterpreter.talkScene.TouchFunc(ConvertReaction(aibuKind[1]), Vector3.zero);
            }
            else
            {
                TalkSceneInterpreter.HitReactionPlay(aibuKind[0], chara);
            }
            return true;
        }

        private static string ConvertReaction(HandCtrl.AibuColliderKind colliderKind)
        {
            return colliderKind switch
            {
                HandCtrl.AibuColliderKind.mouth => "Cheek",
                HandCtrl.AibuColliderKind.muneL => "MuneL",
                HandCtrl.AibuColliderKind.muneR => "MuneR",
                HandCtrl.AibuColliderKind.reac_head => "Head",
                HandCtrl.AibuColliderKind.reac_armL => "HandL",
                HandCtrl.AibuColliderKind.reac_armR => "HandR",
                _ => ""

            };
        }
        protected void OnTriggerEnter(Collider other)
        {
            if (_tracker.AddCollider(other, out _))
            {
                DoReaction(triggerPress: false);
                _controller.StartRumble(new RumbleImpulse(1000));
            }
        }

        protected void OnTriggerExit(Collider other)
        {
            _tracker.RemoveCollider(other, out _);
        }
    }

}
