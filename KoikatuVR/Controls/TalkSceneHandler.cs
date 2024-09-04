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

namespace KK_VR.Controls
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
        private Controller.Lock _lock; // null or valid
        private ColliderTracker _tracker;
        //private KoikatuSettings _settings;

        protected override void OnStart()
        {
            base.OnStart();

            //_settings = VR.Context.Settings as KoikatuSettings;
            _controller = GetComponent<Controller>();
        }

        protected void OnEnable()
        {
            _tracker = new ColliderTracker(TalkSceneInterpreter.TalkScene.targetHeroine.chaCtrl);
        }

        protected void OnDisable()
        {
            UpdateLock();
            _tracker = null;
        }
        protected override void OnUpdate()
        {
            if (_lock != null)
            {
                HandleTrigger();
            }
        }

        private void HandleTrigger()
        {
            if (_controller.Input.GetPressDown(Trigger))
            {
                PerformAction(triggerPress: true);
            }
        }

        public void PerformAction(bool triggerPress)
        {
            var aibuKind = _tracker.GetColliderKind(triggerPress, out var chara, out var tag);
            if (aibuKind == HandCtrl.AibuColliderKind.none)
            {
                return;
            }
            VRPlugin.Logger.LogDebug($"TalkScene:Handler:PerformAction:{aibuKind}:{tag}");
            if (!tag.Equals("") && (triggerPress || UnityEngine.Random.value < 0.67f))
            {
                TalkSceneInterpreter.TalkScene.TouchFunc(tag, Vector3.zero);
            }
            else
            {
                TalkSceneInterpreter.HitReactionPlay(aibuKind, chara);
            }
        }

        protected void OnTriggerEnter(Collider other)
        {
            if (_tracker.AddCollider(other))
            {
                PerformAction(triggerPress: false);
                _controller.StartRumble(new RumbleImpulse(1000));
            }
            UpdateLock();
        }

        protected void OnTriggerExit(Collider other)
        {
            if (_tracker.RemoveCollider(other))
            {
                UpdateLock();
            }
        }

        private void UpdateLock()
        {
            if (_lock == null)
            {
                if (_tracker.IsBusy)
                {
                    _controller.TryAcquireFocus(out _lock);
                }
            }
            else
            {
                if (!_tracker.IsBusy)
                {
                    _lock.Release();
                    _lock = null;
                }
            }
        }
    }

}
