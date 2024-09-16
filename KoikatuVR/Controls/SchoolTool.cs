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
        //private KoikatuSettings _Settings;
        //private KeySet _KeySet;
        //private int _KeySetIndex = 0;
        private int _index;
        private Controller.Lock _lock;

        private Controller.TrackpadDirection _lastDirection;
        //private Controller.TrackpadDirection? _lastPressDirection;
        private GrabAction _grab;
        public bool IsGrab => _grab != null;
        public override Texture2D Image => _image;
        private readonly Texture2D _image = new Texture2D(512, 512);
        private readonly Texture2D _schoolTexture = UnityHelper.LoadImage("icon_school.png");
        //private readonly Texture2D _school1Texture = UnityHelper.LoadImage("icon_school_1.png");
        // readonly Texture2D _school2Texture = UnityHelper.LoadImage("icon_school_2.png");
        private readonly Texture2D _handTexture = UnityHelper.LoadImage("icon_hand.png");
        //private readonly Texture2D _hand1Texture = UnityHelper.LoadImage("icon_hand_1.png");
        //private readonly Texture2D _hand2Texture = UnityHelper.LoadImage("icon_hand_2.png");


        //private void ChangeKeySet()
        //{
        //    List<KeySet> keySets = KeySets();

        //    _KeySetIndex = (_KeySetIndex + 1) % keySets.Count;
        //    _KeySet = keySets[_KeySetIndex];
        //    UpdateIcon();
        //}

        //private List<KeySet> KeySets()
        //{
        //    return _InHScene ? _Settings.HKeySets : _Settings.KeySets;
        //}

        //private void ResetKeys()
        //{
        //    SetScene();
        //}


        private void SetScene()
        {
            //_InHScene = inHScene;
            //var keySets = KeySets();
            //KeySetIndex = 0;
            //_KeySet = keySets[0];
            UpdateIcon();
        }

        private void UpdateIcon()
        {
            Texture2D icon =
                KoikatuInterpreter.CurrentScene == KoikatuInterpreter.SceneType.HScene ? _handTexture : _schoolTexture;
            Graphics.CopyTexture(icon, _image);
        }


        protected override void OnStart()
        {
            base.OnStart();

            // Actual controller/handler indexes:
            // 0 - Headset;
            // 1 - Left controller;
            // 2 - Right controller;
            // But for clarity of button interpretation, as there is no buttons on headset to interpret, we shift them to -1;
            // Headset still has it's own handler(s), so conversion when calling handlers has to be accounted for.

            _index = (int)Controller.index - 1;
            SetScene();

            //_Settings = (KoikatuSettings)VR.Context.Settings;
            //_Settings.AddListener("KeySets", (_, _1) => ResetKeys());
            //_Settings.AddListener("HKeySets", (_, _1) => ResetKeys());
        }

        protected override void OnDisable()
        {
            DestroyGrab();
            base.OnDisable();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }
        protected override void OnUpdate()
        {
            if (_grab != null)
            {
                if (_grab.HandleGrabbing() != GrabAction.Status.Continue)
                {
                    DestroyGrab();
                }
            }
            else
            {
                HandleButtons();
            }
        }
        public void DestroyGrab()
        {
            _grab?.Destroy();
            _grab = null;
            _lock?.Release();
            _lock = null;
        }
        //private void UpdateLock()
        //{
        //    //bool wantLock = _grab != null || _buttonsSubtool?.WantLock() == true;
        //    if (_grab != null)
        //    {
        //        if (_lock == null)
        //        {
        //            Owner.TryAcquireFocus(out _lock, keepTool: true); // AcquireFocus(keepTool: true);
        //        }
        //    }
        //    else if (_lock != null)
        //    {
        //        _lock.Release();
        //        _lock = null;
        //    }
        //}

        private void HandleButtons()
        {
            // Degrees are handled separately when needed by the handlers.
            var direction = Owner.GetTrackpadDirection();

            if (Controller.GetPressDown(ButtonMask.Trigger))
            {
                KoikatuInterpreter.SceneInterpreter.OnButtonDown(EVRButtonId.k_EButton_SteamVR_Trigger, direction, _index);
            }

            else if (Controller.GetPressUp(ButtonMask.Trigger))
            {
                KoikatuInterpreter.SceneInterpreter.OnButtonUp(EVRButtonId.k_EButton_SteamVR_Trigger, direction, _index);
            }

            if (Controller.GetPressDown(ButtonMask.Grip))
            {
                // If particular interpreter doesn't want grip move right now, it will be blocked.
                // We still want all the inputs tho, so locking controller is not an option.
                if (!KoikatuInterpreter.SceneInterpreter.OnButtonDown(EVRButtonId.k_EButton_Grip, direction, _index))
                {
                    Owner.TryAcquireFocus(out _lock, keepTool: true);
                    _grab = new GrabAction(Owner, Controller, ButtonMask.Grip);
                    KoikatuInterpreter.SceneInterpreter.OnControllerLock(_index);
                }
            }
            else if (Controller.GetPressUp(ButtonMask.Grip))
            {
                KoikatuInterpreter.SceneInterpreter.OnButtonUp(EVRButtonId.k_EButton_SteamVR_Trigger, direction, _index);
            }

            if (Controller.GetPressDown(ButtonMask.Touchpad))
            {
                KoikatuInterpreter.SceneInterpreter.OnButtonDown(EVRButtonId.k_EButton_SteamVR_Touchpad, Owner.GetTrackpadDirection(), _index);
            }
            else if (Controller.GetPressUp(ButtonMask.Touchpad))
            {
                KoikatuInterpreter.SceneInterpreter.OnButtonUp(EVRButtonId.k_EButton_SteamVR_Touchpad, Owner.GetTrackpadDirection(), _index);
            }

            if (_lastDirection != direction)
            {
                if (_lastDirection != VRGIN.Controls.Controller.TrackpadDirection.Center)
                {
                    KoikatuInterpreter.SceneInterpreter.OnDirectionUp(_lastDirection, _index);
                }
                if (direction != VRGIN.Controls.Controller.TrackpadDirection.Center)
                {
                    KoikatuInterpreter.SceneInterpreter.OnDirectionDown(direction, _index);
                }
                _lastDirection = direction;
            }
        }


        //public override List<HelpText> GetHelpTexts()
        //{
        //    return new List<HelpText>(new[] {
        //        ToolUtil.HelpTrigger(Owner, DescriptionFor(_KeySet.Trigger)),
        //        ToolUtil.HelpGrip(Owner, DescriptionFor(_KeySet.Grip)),
        //        ToolUtil.HelpTrackpadCenter(Owner, DescriptionFor(_KeySet.Center)),
        //        ToolUtil.HelpTrackpadLeft(Owner, DescriptionFor(_KeySet.Left)),
        //        ToolUtil.HelpTrackpadRight(Owner, DescriptionFor(_KeySet.Right)),
        //        ToolUtil.HelpTrackpadUp(Owner, DescriptionFor(_KeySet.Up)),
        //        ToolUtil.HelpTrackpadDown(Owner, DescriptionFor(_KeySet.Down)),
        //    }.Where(x => x != null));
        //}

        //private static string DescriptionFor(AssignableFunction fun)
        //{
        //    var member = typeof(AssignableFunction).GetMember(fun.ToString()).FirstOrDefault();
        //    var descr = member?.GetCustomAttributes(typeof(DescriptionAttribute), false).Cast<DescriptionAttribute>().FirstOrDefault()?.Description;
        //    return descr ?? fun.ToString();
        //}
    }
}
