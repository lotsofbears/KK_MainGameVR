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
using RootMotion.FinalIK;
using static HandCtrl;
using KK_VR.Caress;

namespace KK_VR.Handlers
{
    /// <summary>
    /// An extra component to be attached to each controller, providing the caress
    /// functionality in H scenes.
    ///
    /// This component is designed to exist only for the duration of an H scene.
    /// </summary>
    class HSceneHandler : MonoBehaviour
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
        private ModelHandler.ItemType _item;
        private TravelDistanceRumble _travelRumble;
        private int _index;
        private float _timer;
        private float _anotherTimer;
        private bool _sleep;
        private Coroutine _clickCo;
        private bool _triggerPress;
        internal bool IsBusy => _tracker.IsBusy;
        private Vector3 GetVelocity => _controller.Input.velocity;
        //private Vector3 GetAngVelocity => _controller.Input.angularVelocity;

        private void Start()
        {
            _settings = VR.Context.Settings as KoikatuSettings;
            _item = ModelHandler.GetItem(this);
            _controller = _item.controller.GetComponent<Controller>();
            _index = (int)_controller.Tracking.index;
            _sleep = true;

            _travelRumble = new TravelDistanceRumble(1000, 0.075f, this.transform);
            //_travelRumble.UseLocalPosition = true;
        }

        private void OnEnable()
        {
            _tracker = new ColliderTracker();
        }

        private void OnDisable()
        {
            _tracker = null;
        }
        private void Update()
        {
            if (_sleep && _timer != 0f)
            {
                _timer = Mathf.Clamp01(_timer - Time.deltaTime);
                _item.rigidBody.velocity *= _timer;
            }
        }
        private void PlayTraverseSfx()
        {
            if (!_item.audioSource.isPlaying)
            {
                if (GetVelocity.sqrMagnitude > 0.1f)
                {
                    ModelHandler.PlaySfx(_item, 1f, this.transform, ModelHandler.Sfx.Traverse, ModelHandler.Object.Skin, ModelHandler.Intensity.Soft);
                }
            }
        }
        // Starts GC like a clock.
        //private void IncreaseButtBlush()
        //{
        //    var skinEffects = _tracker.chara.GetComponent<KK_SkinEffects.SkinEffectsController>();
        //    if (skinEffects != null) skinEffects.ButtLevel++;
        //}
        private static bool AibuKindAllowed(AibuColliderKind kind, ChaControl chara)
        {
            if (KoikatuInterpreter.CurrentScene != KoikatuInterpreter.SceneType.HScene)
            {
                return true;
            }
            var heroine = HSceneInterpreter.hFlag.lstHeroine
                .Where(h => h.chaCtrl == chara)
                .FirstOrDefault();
            if (heroine == null)
            {
                return true;
            }
            return kind switch
            {
                AibuColliderKind.mouth => heroine.isGirlfriend || heroine.isKiss || heroine.denial.kiss,
                AibuColliderKind.anal => heroine.hAreaExps[3] > 0f || heroine.denial.anal,
                _ => true
            };
        }

        protected void OnTriggerEnter(Collider other)
        {
            if (_tracker.AddCollider(other))
            {
                if (_tracker.reactionType > ColliderTracker.ReactionType.None)
                {
                    DoReaction();
                }
                if (_tracker.firstTrack)
                {
                    PlayFirstSfx();
                    _controller.StartRumble(new RumbleImpulse(1000));
                    _controller.StartRumble(_travelRumble);
                }
                //SetHandCtrl();
                //VRPlugin.Logger.LogDebug($"Handler:TriggerEnter:{other.name}:{GetVelocity.sqrMagnitude}");//:{GetAngVelocity}");
            }
        }
        private void PlayFirstSfx()
        {
            var velocity = GetVelocity.sqrMagnitude;
            var intensity = _tracker.bodyPart < ColliderTracker.Body.LowerBody ? ModelHandler.Intensity.Soft : ModelHandler.Intensity.Rough;
            if (velocity > 1f)
            {
                ModelHandler.PlaySfx(_item, 0.4f + velocity * 0.2f, this.transform, ModelHandler.Sfx.Slap, ModelHandler.Object.Skin, intensity);
            }
            else
            {
                ModelHandler.PlaySfx(_item, 1f, this.transform, ModelHandler.Sfx.Tap, ModelHandler.Object.Skin, intensity);
            }
        }
        private void PlaySFX()
        {
            //ModelHandler.PlaySlap(_index, 1f, this.transform);
            //var velocity = GetVelocity.sqrMagnitude;
            //if (velocity > 1f)
            //{
            //    ModelHandler.PlaySlap(_index, 0.4f + velocity * 0.1f, other.transform);
            //}
            //else if (_tracker.firstTrack)
            //{

            //}
            //if (colliderKind == AibuColliderKind.reac_head)
            //{
            //    // PlayHeadpat
            //}
        }
        //private void SetHandCtrl() => HSceneInterpreter.handCtrl.selectKindTouch = _tracker.suggestedKind[1] == AibuColliderKind.none ? _tracker.suggestedKind[0] : _tracker.suggestedKind[1];
        protected void OnTriggerExit(Collider other)
        {
            if (_tracker.RemoveCollider(other))
            {
                //VRPlugin.Logger.LogDebug($"Handler:TriggerExit:{other.name}");
                //SetHandCtrl();
                if (!IsBusy)
                {
                    _timer = 1f;
                    _controller.StopRumble(_travelRumble);
                    _travelRumble.Reset();
                }
            }
        }
        public bool DoUndress(bool decrease)
        {
            if (!_tracker.IsBusy)
            {
                return false;
            }
            var bodyKind = _tracker.GetUndressKind();
            VRPlugin.Logger.LogDebug($"Handler:Undress:Part[{bodyKind}]");
            _controller.StartRumble(new RumbleImpulse(1000));
            if (bodyKind != ColliderTracker.Body.None && ClothesHandler.Undress(_tracker.chara, bodyKind, decrease))
            {
                return true;
            }
            return false;

        }
        public bool TriggerPress()
        {
            if (!IsBusy) return false;
            var suggestedKinds = _tracker.GetSuggestedKinds();
            if (suggestedKinds[1] == AibuColliderKind.none || suggestedKinds[1] > AibuColliderKind.siriR)
            {
                HSceneInterpreter.HitReactionPlay(suggestedKinds[0], _tracker.chara);
            }
            else if (_tracker.chara == HSceneInterpreter.lstFemale[0])
            {
                VRPlugin.Logger.LogDebug($"TriggerPress{suggestedKinds[1]}:{HSceneInterpreter.handCtrl.selectKindTouch}");
                if (!VRMouth.NoActionAllowed && HSceneInterpreter.handCtrl.GetUseAreaItemActive() != -1)
                {
                    // If VRMouth isn't active but automatic caress is going.
                    // Otherwise VRMouth has it's own hooks for trigger - halt (consolidate ?)
                    CaressHelper.StopMoMiOnSensibleHSide();
                }
                else
                {
                    HSceneInterpreter.handCtrl.selectKindTouch = suggestedKinds[1];
                    HandCtrlHooks.InjectMouseButtonDown(0);
                    _triggerPress = true;
                }
                return true;
            }
            return false;
        }
        public void TriggerRelease()
        {
            if (_triggerPress)
            {
                HSceneInterpreter.handCtrl.selectKindTouch = AibuColliderKind.none;
                HandCtrlHooks.InjectMouseButtonUp(0);
                _triggerPress = false;
            }
        }
        public bool DoReaction()
        {
            VRPlugin.Logger.LogDebug($"AttemptReaction:{IsBusy}");
            if (!IsBusy || (_settings.AutomaticTouching < KoikatuSettings.SceneType.HScene)) return false;

            HSceneInterpreter.HitReactionPlay(_tracker.GetSuggestedKinds()[0], _tracker.chara);
            _controller.StartRumble(new RumbleImpulse(1000));
            return true;
        }
    }
}