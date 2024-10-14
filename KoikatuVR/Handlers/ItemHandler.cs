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
using ADV.Commands.Game;
using KK_VR.Trackers;

namespace KK_VR.Handlers
{
    class ItemHandler : Handler
    {
        protected ControllerTracker _tracker;
        protected override Tracker BaseTracker
        {
            get => _tracker; 
            set => _tracker = value is ControllerTracker t ? t : null;
        }
        private
        protected KoikatuSettings _settings;
        protected Controller _controller;
        //protected ModelHandler.ItemType _item;
        protected int _index;
        private bool _unwind;
        private float _timer;
        private Rigidbody _rigidBody;
        internal override bool IsBusy => _tracker.colliderInfo != null && _tracker.colliderInfo.chara != null;
        internal ChaControl GetChara => _tracker.colliderInfo.chara;

        // Default velocity is in controller or origin local space.
        protected Vector3 GetVelocity => _controller.Input.velocity;
        protected override void OnEnable()
        {
            _tracker = new ControllerTracker();
        }
        internal void Init(int index, Rigidbody rigidBody)
        {
            _index = index;
            _rigidBody = rigidBody;
        }
        protected virtual void Start()
        {
            _settings = VR.Context.Settings as KoikatuSettings;
            _controller = _index == 0 ? VR.Mode.Left : VR.Mode.Right;
        }
        protected virtual void Update()
        {
            if (_unwind)
            {
                _timer = Mathf.Clamp01(_timer - Time.deltaTime);
                _rigidBody.velocity *= _timer;
                if (_timer == 0f)
                {
                    _unwind = false;
                }
            }
        }

        //protected void PlayTraverseSfx()
        //{
        //    if (!_item.audioSource.isPlaying)
        //    {
        //        if (GetVelocity.sqrMagnitude > 0.1f)
        //        {
        //            ModelHandler.PlaySfx(_item, 1f, this.transform, ModelHandler.Sfx.Traverse, ModelHandler.Object.Skin, ModelHandler.Intensity.Soft);
        //        }
        //    }
        //}


        protected override void OnTriggerEnter(Collider other)
        {
            if (_tracker.AddCollider(other))
            {
                var velocity = GetVelocity.sqrMagnitude;
                if (velocity > 1f || _tracker.reactionType != Tracker.ReactionType.None)
                {
                    DoReaction(velocity);
                }
                PlaySfx(velocity);
            }
        }
        protected void PlaySfx(float velocity)
        {
            return;
            var fast = velocity > 1f;
            HandNoises.PlaySfx(
                _index,
                fast ? 0.4f + velocity * 0.2f : 1f,
                fast ? HandNoises.Sfx.Slap : _tracker.firstTrack ? HandNoises.Sfx.Tap : HandNoises.Sfx.Traverse,
                GetSurfaceType(_tracker.colliderInfo.behavior.part)
                );
        }

        protected HandNoises.Surface GetSurfaceType(Tracker.Body part)
        {
            // Add better hair handling, not whole head is hair.
            if (part == Tracker.Body.Head)
                return HandNoises.Surface.Hair;

            if (Interactors.Undresser.IsBodyPartClothed(_tracker.colliderInfo.chara, part))
                return HandNoises.Surface.Cloth;

            return HandNoises.Surface.Skin;
        }

        protected override void OnTriggerExit(Collider other)
        {
            if (_tracker.RemoveCollider(other))
            {
                if (!IsBusy)
                {
                    // RigidBody is being rigid, unwind it.
                    _unwind = true;
                    _timer = 1f;
                    // Do we need this?
                    HSceneInterpreter.SetSelectKindTouch(AibuColliderKind.none);
                }
            }
        }


        internal Tracker.Body GetTrackPartName(ChaControl tryToAvoidChara = null, int preferredSex = -1)
        {
            return tryToAvoidChara == null && preferredSex == -1 ? _tracker.GetGraspBodyPart() : _tracker.GetGraspBodyPart(tryToAvoidChara, preferredSex);
        }
        internal void RemoveHandlerColliders()
        {
            _tracker.FlushLimbHandlers();
        }
        internal void RemoveCollider(Collider other)
        {
            _tracker.RemoveCollider(other);
        }
        internal void DebugShowActive()
        {
            _tracker.DebugShowActive();
        }
        protected virtual void DoReaction(float velocity)
        {

        }
    }
}