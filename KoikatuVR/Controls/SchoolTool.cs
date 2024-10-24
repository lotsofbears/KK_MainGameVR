using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRGIN.Controls;
using VRGIN.Controls.Tools;
using VRGIN.Core;
using VRGIN.Helpers;
using static SteamVR_Controller;
using WindowsInput.Native;
using KK_VR.Interpreters;
using System.ComponentModel;
using KK_VR.Settings;
using static KK_VR.Interpreters.HSceneInterpreter;
using Valve.VR;
using ADV.Commands.Camera;
using System.Diagnostics;

namespace KK_VR.Controls
{
    public class SchoolTool : Tool
    {
        private int _index;
        private KoikatuMenuTool _menu;

        private Controller.TrackpadDirection _lastDirection;
        private GripMove _grab;
        public override Texture2D Image
        {
            get;
        }

        protected override void OnStart()
        {
            base.OnStart();

            VRPlugin.Logger.LogDebug($"SchoolTool:Init:{(int)Tracking.index}");
            //SetScene();
            _index = Owner == VR.Mode.Left ? 0 : 1;
            _menu = new KoikatuMenuTool(_index);
        }

        protected override void OnDisable()
        {
            //_index = (int)Tracking.index - 1;
            DestroyGrab();
            base.OnDisable();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }
        protected override void OnUpdate()
        {
            HandleButtons();
            _grab?.HandleGrabbing();
        }

        internal void DestroyGrab()
        {
            KoikatuInterpreter.SceneInterpreter.OnGripMove(_index, active: false);
            _grab?.Destroy();
            _grab = null;
        }
        internal void LazyGripMove(int avgFrame)
        {
            // In all honesty tho, the proper name would be retarded, not lazy as it does way more in this mode and lags behind.
            _grab?.StartLag(avgFrame);
        }
        internal void AttachGripMove(Transform attachPoint)
        {
            _grab?.AttachGripMove(attachPoint);
        }
        internal void UnlazyGripMove()
        {
            _grab?.StopLag();
        }
        internal bool IsGripMove() => _grab != null;

        private void HandleButtons()
        {
            var direction = Owner.GetTrackpadDirection();

            if (Controller.GetPressDown(EVRButtonId.k_EButton_ApplicationMenu))
            {
                if (!KoikatuInterpreter.SceneInterpreter.OnButtonDown(_index, EVRButtonId.k_EButton_ApplicationMenu, direction))
                {
                    _menu.ToggleState();
                }
            }
            if (Controller.GetPressDown(ButtonMask.Trigger))
            {
                if (!KoikatuInterpreter.SceneInterpreter.OnButtonDown(_index, EVRButtonId.k_EButton_SteamVR_Trigger, direction))
                {
                    _grab?.OnTrigger(true);
                }
                
            }
            else if (Controller.GetPressUp(ButtonMask.Trigger))
            {
                _grab?.OnTrigger(false);
                KoikatuInterpreter.SceneInterpreter.OnButtonUp(_index, EVRButtonId.k_EButton_SteamVR_Trigger, direction);
            }

            if (Controller.GetPressDown(ButtonMask.Grip))
            {
                // If particular interpreter doesn't want grip move right now, it will be blocked.
                if (_menu.attached)
                {
                    _menu.AbandonGUI();
                }
                else if (!KoikatuInterpreter.SceneInterpreter.OnButtonDown(_index, EVRButtonId.k_EButton_Grip, direction))
                {
                    _grab = new GripMove(Owner);
                    if (KoikatuInterpreter.SceneInterpreter.IsTriggerPress(_index))
                    {
                        _grab.OnTrigger(true);
                    }
                    if (KoikatuInterpreter.SceneInterpreter.IsTouchpadPress(_index))
                    {
                        _grab.OnTouchpad(true);
                    }
                    KoikatuInterpreter.SceneInterpreter.OnGripMove(_index, active: true);
                }
            }
            else if (Controller.GetPressUp(ButtonMask.Grip))
            {
                if (_grab != null)
                {
                    DestroyGrab();
                }
                else
                {
                    KoikatuInterpreter.SceneInterpreter.OnButtonUp(_index, EVRButtonId.k_EButton_Grip, direction);
                }
            }
            if (Controller.GetPressDown(ButtonMask.Touchpad))
            {
                if (!KoikatuInterpreter.SceneInterpreter.OnButtonDown(_index, EVRButtonId.k_EButton_SteamVR_Touchpad, direction))
                {
                    _grab?.OnTouchpad(true);
                }
            }
            else if (Controller.GetPressUp(ButtonMask.Touchpad))
            {
                _grab?.OnTouchpad(false);
                KoikatuInterpreter.SceneInterpreter.OnButtonUp(_index, EVRButtonId.k_EButton_SteamVR_Touchpad, direction);
            }

            if (_lastDirection != direction)
            {
                if (_lastDirection != VRGIN.Controls.Controller.TrackpadDirection.Center)
                {
                    KoikatuInterpreter.SceneInterpreter.OnDirectionUp(_index, _lastDirection);
                }
                if (direction != VRGIN.Controls.Controller.TrackpadDirection.Center)
                {
                    KoikatuInterpreter.SceneInterpreter.OnDirectionDown(_index, direction);
                }
                _lastDirection = direction;
            }
        }
    }
}
