using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using VRGIN.Core;
using VRGIN.Controls;
using VRGIN.Helpers;
using HarmonyLib;
using UnityEngine;
using KK_VR.Interpreters;
using KK_VR.Settings;
using static SteamVR_Controller;
using KK_VR.Fixes;
using KK_VR.Features;
using KK_VR.Controls;

namespace KK_VR.Handlers
{
    /// <summary>
    /// An extra component to be attached to each controller, providing the caress
    /// functionality in H scenes.
    ///
    /// This component is designed to exist only for the duration of an H scene.
    /// </summary>
    class HSceneHandler : ProtectedBehaviour
    {
        // Basic plan:
        //
        // * Keep track of the potential caress points
        //   near this controller. _aibuTracker is responsible for this.
        // * While there is at least one such point, lock the controller
        //   to steal any trigger events.
        // * When the trigger is pulled, initiate caress.
        // * Delay releasing of the lock until the trigger is released.

        private KoikatuSettings _settings;
        private Controller _controller;
        private ColliderTracker _tracker;
        private Vector3 GetVelocity => _controller.Input.velocity;

        protected override void OnAwake()
        {
            _settings = VR.Context.Settings as KoikatuSettings;
            _controller = GetComponentInParent<Controller>();// GetComponent<Controller>(); 
            
        }

        protected void OnEnable()
        {
            _tracker = new ColliderTracker();
        }

        protected void OnDisable()
        {
            _tracker = null;
        }

        protected void OnTriggerEnter(Collider other)
        {
            if (_tracker.AddCollider(other, out var colliderKind))
            {
                //VRPlugin.Logger.LogDebug($"Handler:TriggerEnter:{other.name}");//:Velocity - {GetVelocity}");
                DoReaction(triggerPress: false, colliderKind);
                HSceneInterpreter.handCtrl.selectKindTouch = colliderKind[1] == HandCtrl.AibuColliderKind.none ? colliderKind[0] : colliderKind[1];
                _controller.StartRumble(new RumbleImpulse(1000));
            }
        }

        protected void OnTriggerExit(Collider other)
        {
            if (_tracker.RemoveCollider(other, out var colliderKind))
            {
                //VRPlugin.Logger.LogDebug($"Handler:TriggerExit:{other.name}");
                HSceneInterpreter.handCtrl.selectKindTouch = colliderKind[1] == HandCtrl.AibuColliderKind.none ? colliderKind[0] : colliderKind[1];
            }
        }
        public bool DoUndress(bool decrease)
        {
            if (!_tracker.IsBusy)
            {
                //VRPlugin.Logger.LogDebug($"Handler:Undress:Tracker[{_tracker.IsBusy}]");
                return false;
            }
            var bodyKind = _tracker.GetUndressKind(out var chara);
            VRPlugin.Logger.LogDebug($"Handler:Undress:Part[{bodyKind}]");
            if (bodyKind != ColliderTracker.Body.None && ClothesHandler.Undress(chara, bodyKind, decrease))
            {
                _controller.StartRumble(new RumbleImpulse(1000));
                return true;
            }
            return false;

        }

        // We either don't have reaction(only touch option) for breast collider, or have a cooldown.
        // Otherwise they bounce too much for tracking to be stable, thus resulting in reaction spam.
        private float _lastReaction;
        public bool DoReaction(bool triggerPress, HandCtrl.AibuColliderKind[] colliderKind)
        {
            if (!_tracker.IsBusy || (!triggerPress && (_settings.AutomaticTouching < KoikatuSettings.SceneType.HScene))) // _lastReaction > Time.time || 
            {
                //VRPlugin.Logger.LogDebug($"Handler:Reaction:Tracker[{_tracker.IsBusy}]");
                return false;
            }
            //var aibuKind = _tracker.GetReactionKind(out var chara);
            var chara = _tracker.GetChara();
            VRPlugin.Logger.LogDebug($"Handler:Reaction:React[{colliderKind[0]}]:Touch[{colliderKind[1]}]");
            if (colliderKind[1] !=  HandCtrl.AibuColliderKind.none && triggerPress && HSceneInterpreter.hFlag.lstHeroine[0].chaCtrl == chara)
            {
                // Moved to the interpreter with native mouse clicks.
                // There is no native implementation for 3p. Don't really care to look into it. In vr even one chara is a lot.
                HSceneInterpreter.handCtrl.selectKindTouch = colliderKind[1];
                StartCoroutine(Caress.CaressUtil.ClickCo(() => HSceneInterpreter.handCtrl.selectKindTouch = 0));
            }
            else
            {
                HSceneInterpreter.HitReactionPlay(colliderKind[0], chara);
            }
            //_lastReaction = Time.time + 0.5f;
            _controller.StartRumble(new RumbleImpulse(1000));
            return true;
        }

        //private void HandleToolChange()
        //{
        //    var device = _controller.Input; // SteamVR_Controller.Input((int)_controller.Tracking.index);
        //    if (device.GetPressUp(ButtonMask.ApplicationMenu))
        //    {
        //        UpdateSelectKindTouch();
        //        HandCtrlHooks.InjectMouseScroll(1f);
        //    }
        //}
    }
}