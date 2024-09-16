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
    class TalkSceneHandler : MonoBehaviour
    {
        private Controller _controller;
        private ColliderTracker _tracker;
        private static KoikatuSettings _settings;
        private int _index;
        private ModelHandler.ItemType _item;
        private TravelDistanceRumble _travelRumble;

        internal bool IsBusy => _tracker.IsBusy;
        private bool _sleep;
        private float _timer;
        private Vector3 GetVelocity => _controller.Input.velocity;

        private void Start()
        {
            _settings = (KoikatuSettings)VR.Context.Settings;
            _item = ModelHandler.GetItem(this);
            _controller = _item.controller.GetComponent<Controller>();
            _index = (int)_controller.Tracking.index;
            _sleep = true;
            _travelRumble = new TravelDistanceRumble(1000, 0.075f, this.transform);
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
        public bool DoUndress(bool decrease, out ChaControl chara)
        {
            if (!_tracker.IsBusy)
            {
                chara = null;
                return false;
            }
            chara = _tracker.chara;
            var bodyKind = _tracker.GetUndressKind();
            _controller.StartRumble(new RumbleImpulse(1000));
            if (bodyKind != ColliderTracker.Body.None && ClothesHandler.Undress(chara, bodyKind, decrease))
            {
                return true;
            }
            return false;
        }

        public bool DoReaction(bool triggerPress)
        {
            if (!IsBusy
                || (!triggerPress && ( _settings.AutomaticTouching == KoikatuSettings.SceneType.Disabled || _settings.AutomaticTouching == KoikatuSettings.SceneType.HScene)))
            {
                return false;
            }
            _controller.StartRumble(new RumbleImpulse(1000));
            var suggestedKinds = _tracker.GetSuggestedKinds();
            if (TalkSceneInterpreter.talkScene != null 
                && suggestedKinds[1] != HandCtrl.AibuColliderKind.none 
                && !CrossFader.AdvHooks.Reaction 
                && _tracker.chara == TalkSceneInterpreter.talkScene.targetHeroine.chaCtrl
                && (triggerPress || UnityEngine.Random.value < 0.3f))
            {
                // TODO Null ref in adv. Mimic TouchFunc() in adv?
                // Seems quite easy, we just need to grab corresponding assets.
                TalkSceneInterpreter.talkScene.TouchFunc(TouchReaction(suggestedKinds[1]), Vector3.zero);
            }
            else
            {
                TalkSceneInterpreter.HitReactionPlay(suggestedKinds[0], _tracker.chara);
            }
            return true;
        }

        private string TouchReaction(HandCtrl.AibuColliderKind colliderKind)
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
            if (_tracker.AddCollider(other))
            {
                _sleep = false;
                if (_tracker.reactionType > ColliderTracker.ReactionType.None)
                {
                    DoReaction(triggerPress: false);
                }
                if (_tracker.firstTrack)
                {
                    PlayFirstSfx();
                    _controller.StartRumble(new RumbleImpulse(1000));
                    _controller.StartRumble(_travelRumble);
                }
                //if (shouldReact)
                //{
                //    var velocity = GetVelocity.sqrMagnitude;
                //    if (velocity > 1f)
                //    {
                //        ModelHandler.PlaySlap(_index, 0.4f + velocity * 0.1f, other.transform);
                //        DoReaction(triggerPress: false);
                //    }
                //    else if (colliderKind[1] > 0 && colliderKind[1] < HandCtrl.AibuColliderKind.siriL)
                //    {
                //        DoReaction(triggerPress: false);
                //    }
                //    else
                //    {
                //        HSceneInterpreter.PlayShort(_tracker.GetChara());
                //    }
                //}
                _controller.StartRumble(new RumbleImpulse(1000));
            }
        }

        protected void OnTriggerExit(Collider other)
        {

            if (_tracker.RemoveCollider(other))
            {
                if (!IsBusy)
                {
                    _sleep = true;
                    _timer = 1f;
                    _controller.StopRumble(_travelRumble);
                    _travelRumble.Reset();
                }
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
    }

}
