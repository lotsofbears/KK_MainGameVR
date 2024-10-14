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
    internal class Handler : MonoBehaviour
    {
        protected virtual Tracker BaseTracker { get; set; }
        /// <summary>
        /// True if something is being tracked. Track for recently blacklisted items continues, but new ones don't get added.
        /// </summary>
        internal virtual bool IsBusy => BaseTracker.IsBusy;
        /// <summary>
        /// Can be true only after 'UpdateNoBlacks()' if every item in track is blacklisted.
        /// </summary>
        internal bool InBlack => BaseTracker.colliderInfo == null;
        internal Transform GetTrackTransform => BaseTracker.colliderInfo.collider.transform;

        protected virtual void OnEnable()
        {
            BaseTracker = new Tracker();
        }

        protected virtual void OnDisable()
        {
            BaseTracker = null;
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            BaseTracker.AddCollider(other);
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            BaseTracker.RemoveCollider(other);
        }
        internal void FlushBlacks()
        {
            BaseTracker.RemoveBlacks();
        }
        internal void FlushTracker()
        {
            BaseTracker.FlushTracker();
        }
        internal void UpdateTrackerNoBlacks()
        {
            BaseTracker.SetSuggestedInfoNoBlacks();
        }
        internal void UpdateTracker(ChaControl tryToAvoid = null)
        {
            BaseTracker.SetSuggestedInfo(tryToAvoid);
        }

    }
}