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
        protected virtual Tracker tracker { get; set; }
        /// <summary>
        /// True if something is being tracked. Track for recently blacklisted items continues, but new ones don't get added.
        /// </summary>
        internal virtual bool IsBusy => tracker.IsBusy;
        /// <summary>
        /// Can be true only after 'UpdateNoBlacks()' if every item in track is blacklisted.
        /// </summary>
        internal bool InBlack => tracker.colliderInfo == null;
        internal Transform GetTrackTransform => tracker.colliderInfo.collider.transform;
        internal ChaControl GetChara => tracker.colliderInfo.chara;


        protected virtual void OnDisable()
        {
            tracker.ClearTracker();
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            tracker.AddCollider(other);
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            tracker.RemoveCollider(other);
        }
        internal void ClearBlacks()
        {
            tracker.RemoveBlacks();
        }
        internal void ClearTracker()
        {
            tracker.ClearTracker();
        }
        internal void UpdateTrackerNoBlacks()
        {
            tracker.SetSuggestedInfoNoBlacks();
        }
        internal void UpdateTracker(ChaControl tryToAvoid = null)
        {
            tracker.SetSuggestedInfo(tryToAvoid);
            tracker.DebugShowActive();
        }

    }
}