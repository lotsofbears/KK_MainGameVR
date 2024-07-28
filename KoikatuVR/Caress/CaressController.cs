﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using VRGIN.Core;
using VRGIN.Controls;
using VRGIN.Helpers;
using HarmonyLib;
using UnityEngine;
using KoikatuVR.Interpreters;
using KoikatuVR.Settings;
using static SteamVR_Controller;

namespace KoikatuVR.Caress
{
    /// <summary>
    /// An extra component to be attached to each controller, providing the caress
    /// functionality in H scenes.
    ///
    /// This component is designed to exist only for the duration of an H scene.
    /// </summary>
    class CaressController : ProtectedBehaviour
    {
        // Basic plan:
        //
        // * Keep track of the potential caress points
        //   near this controller. _aibuTracker is responsible for this.
        // * While there is at least one such point, lock the controller
        //   to steal any trigger events.
        // * When the trigger is pulled, initiate caress.
        // * Delay releasing of the lock until the trigger is released.

        KoikatuSettings _settings;
        Controller _controller;
        AibuColliderTracker _aibuTracker;
        Undresser _undresser;
        Controller.Lock _lock; // may be null but never invalid
        bool _triggerPressed; // Whether the trigger is currently pressed. false if _lock is null.
        Util.ValueTuple<ChaControl, ChaFileDefine.ClothesKind, Vector3>? _undressing;
        private HandCtrl _hand;

        protected override void OnAwake()
        {
            base.OnAwake();
            _settings = VR.Context.Settings as KoikatuSettings;
            _controller = GetComponent<Controller>();
            var proc = GameObject.FindObjectOfType<HSceneProc>();
            if (proc == null)
            {
                VRLog.Warn("HSceneProc not found");
                return;
            }
            _hand = Traverse.Create(proc).Field("hand").GetValue<HandCtrl>();   
            VRLog.Debug($"Controller[{_controller}]");
            _aibuTracker = new AibuColliderTracker(proc, referencePoint: transform);
            _undresser = new Undresser(proc);
        }

        private void OnDestroy()
        {
            if (_lock != null)
            {
                ReleaseLock();
            }
        }

        protected override void OnUpdate()
        {
            if (_lock != null)
            {
                HandleTrigger();
                HandleToolChange();
                HandleUndress();
            }
            UpdateLock();
        }

        protected void OnTriggerEnter(Collider other)
        {
            //if (VRMouth._lickCoShouldEnd != null)
            //{
            //    return;
            //}
            try
            {
                if (Manager.Scene.Instance.NowSceneNames[0].Equals("HPointMove"))
                {
                    return;
                }
                bool wasIntersecting = _aibuTracker.IsIntersecting();
                if (_aibuTracker.AddIfRelevant(other))
                {
                    if (!wasIntersecting && _aibuTracker.IsIntersecting())
                    {
                        _controller.StartRumble(new RumbleImpulse(1000));
                        if (_settings.AutomaticTouching)
                        {
                            var colliderKind = _aibuTracker.GetCurrentColliderKind(out int femaleIndex);
                            if (HandCtrl.AibuColliderKind.reac_head <= colliderKind &&
                                !CaressUtil.IsSpeaking(_aibuTracker.Proc, femaleIndex))
                            {
                                VRLog.Debug($"OnTriggerEnter[{other}]{colliderKind}");
                                //CaressUtil.SetSelectKindTouch(_aibuTracker.Proc, femaleIndex, colliderKind);
                                //StartCoroutine(CaressUtil.ClickCo());
                                _hand.Reaction(colliderKind);
                            }
                        }
                    }

                    _undresser.Enter(other);
                    UpdateLock();
                    _undresser.Enter(other);
                }
            }
            catch (Exception e)
            {
                VRLog.Error(e);
            }
        }

        protected void OnTriggerExit(Collider other)
        {
            try
            {
                _aibuTracker.RemoveIfRelevant(other);
                _undresser.Exit(other);
                UpdateLock();
            }
            catch (Exception e)
            {
                VRLog.Error(e);
            }
        }

        private void UpdateLock()
        {
            bool shouldHaveLock = (_aibuTracker.IsIntersecting() || _undressing != null) &&
                    Manager.Scene.Instance.NowSceneNames[0] != "HPointMove";
            if (shouldHaveLock && _lock == null)
            {
                _controller.TryAcquireFocus(out _lock);
            }
            else if (!shouldHaveLock && _lock != null && !_triggerPressed)
            {
                ReleaseLock();
            }
        }

        private void HandleTrigger()
        {
            var device = _controller.Input; //SteamVR_Controller.Input((int)_controller.Tracking.index);
            if (!_triggerPressed && device.GetPressDown(ButtonMask.Trigger))
            {
                UpdateSelectKindTouch();
                //VRLog.Debug($"HandleTrigger[Press] {_hand.selectKindTouch}");
                HandCtrlHooks.InjectMouseButtonDown(0);
                _controller.StartRumble(new RumbleImpulse(1000));
                _triggerPressed = true;
            }
            else if (_triggerPressed && device.GetPressUp(ButtonMask.Trigger))
            {
                //VRLog.Debug($"HandleTrigger[Release] {_hand.selectKindTouch}");
                HandCtrlHooks.InjectMouseButtonUp(0);
                _triggerPressed = false;
                UpdateLock();
            }
        }

        private void HandleToolChange()
        {
            var device = _controller.Input; // SteamVR_Controller.Input((int)_controller.Tracking.index);
            if (device.GetPressUp(ButtonMask.ApplicationMenu))
            {
                UpdateSelectKindTouch();
                HandCtrlHooks.InjectMouseScroll(1f);
            }

        }

        private void HandleUndress()
        {
            var device = SteamVR_Controller.Input((int)_controller.Tracking.index);
            var proc = _aibuTracker.Proc;
            if (_undressing == null && device.GetPressDown(ButtonMask.Touchpad))
            {
                var females = new Traverse(proc).Field<List<ChaControl>>("lstFemale").Value;
                var toUndress = _undresser.ComputeUndressTarget(females, out int femaleIndex);
                if (toUndress is ChaFileDefine.ClothesKind kind)
                {
                    _undressing = Util.ValueTuple.Create(females[femaleIndex], kind, transform.position);
                }
            }
            if (_undressing is Util.ValueTuple<ChaControl, ChaFileDefine.ClothesKind, Vector3> undressing
                && device.GetPressUp(ButtonMask.Touchpad))
            {
                if (0.3f * 0.3f < (transform.position - undressing.Field3).sqrMagnitude)
                {
                    undressing.Field1.SetClothesState((int)undressing.Field2, 3);
                }
                else
                {
                    undressing.Field1.SetClothesStateNext((int)undressing.Field2);
                }
                _undressing = null;
            }
        }

        private void ReleaseLock()
        {
            //VRLog.Debug("ReleaseLock");
            CaressUtil.SetSelectKindTouch(_aibuTracker.Proc, 0, HandCtrl.AibuColliderKind.none);
            if (_triggerPressed)
                HandCtrlHooks.InjectMouseButtonUp(0);
            _triggerPressed = false;
            _undressing = null;
            _lock.Release();
            _lock = null;
        }

        private void UpdateSelectKindTouch()
        {
            var colliderKind = _aibuTracker.GetCurrentColliderKind(out int femaleIndex);
            //VRLog.Debug($"UpdateSelectKindTouch[{colliderKind}]");
            CaressUtil.SetSelectKindTouch(_aibuTracker.Proc, femaleIndex, colliderKind);
        }
    }
}