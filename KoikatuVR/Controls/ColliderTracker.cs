using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KK_VR.Interpreters;
using HarmonyLib;
using VRGIN.Core;
using KK_VR.Fixes;
using static HandCtrl;
using Random = UnityEngine.Random;

namespace KK_VR.Controls
{
    /// <summary>
    /// An object that tracks the set of aibu colliders that we are
    /// currently intersecting.
    ///
    /// Each instance only concerns one H scene. A fresh instance should be
    /// created for each H scene.
    /// </summary>
    class ColliderTracker
    {
        private readonly List<Collider> trackingColliders = new List<Collider>();
        private static readonly IDictionary<string, AibuColliderKind> activeColliders = new Dictionary<string, AibuColliderKind>();
        public bool IsBusy => trackingColliders.Count > 0;
        private AibuColliderKind _currentKind;



        public ColliderTracker(ChaControl chara)
        {
            if (activeColliders.Count != 0)
            { 
                return;
            }
            var colliders = chara.GetComponentsInChildren<Collider>(includeInactive: true);
            foreach (var collider in colliders)
            {
                if (colliderNameDic.TryGetValue(collider.name, out var colliderKind))
                {
                    activeColliders.Add(collider.name, colliderKind);
                    VRPlugin.Logger.LogDebug($"Tracker:Start:Collider:Add:{collider.name}");
                }
                else
                {
                    VRPlugin.Logger.LogDebug($"Tracker:Start:Collider:Skip:{collider.name}");
                }
            }
        }

        /// <summary>
        /// Adds tracking and informs if something new popped up.
        /// </summary>
        public bool AddCollider(Collider other)
        {
            if (!activeColliders.ContainsKey(other.name))
            {
                return false;
            }
            if (!trackingColliders.Contains(other))
            {
                VRPlugin.Logger.LogDebug($"Tracking:Add:{other.name}");
                trackingColliders.Add(other);
                if (_currentKind != activeColliders[other.name])
                {
                    _currentKind = activeColliders[other.name];
                    return true;
                }
                else
                    return false;
            }
            return false;
        }

        /// <summary>
        /// Remove collider tracking.
        /// </summary>
        public bool RemoveCollider(Collider other)
        {
            if (trackingColliders.Remove(other))
            {
                VRPlugin.Logger.LogDebug($"Tracking:Remove:{other.name}");
                if (trackingColliders.Count == 0)
                {
                    _currentKind = AibuColliderKind.none;
                }
                return true;
            }
            return false;
        }
        /// <summary>
        /// Get The kind of the collider we should be interacting with.
        /// Also outputs the female who is the owner of the collider.
        /// </summary>
        public AibuColliderKind GetColliderKind(bool triggerPress, out ChaControl chara, out string tag)
        {
            tag = "";
            if (!IsBusy)
            {
                chara = null;
                return AibuColliderKind.none;
            }
            chara = trackingColliders[0].gameObject.GetComponentInParent<ChaControl>();


            var touch = trackingColliders
                .Where(c => !c.tag.Equals("Untagged"))
                .ToList();
            if (touch.Count > 0)
            {
                touch.Sort();
                var index = triggerPress ? 0 : Random.Range(0, touch.Count);
                tag = touch[index].tag.Replace("Com/Hit/", "");
                return activeColliders
                    .Where(kv => kv.Key.Equals(touch[index].name))
                    .Select(kv => kv.Value)
                    .FirstOrDefault();
            }
            else
            {
                var index = triggerPress ? trackingColliders.Count - 1 : Random.Range(0, trackingColliders.Count);
                return activeColliders
                    .Where(kv => kv.Key.Equals(trackingColliders[index].name))
                    .Select(kv => kv.Value)
                    .FirstOrDefault();
            }
        }

        ///// <summary>
        ///// Return whether there is any collider we should be interacting with.
        ///// This is equivalent to this.GetCurrentColliderKind() != none, but is more efficient.
        ///// </summary>
        ///// <returns></returns>
        //public bool IsIntersecting()
        //{
        //    return _currentlyIntersecting.Count > 0;
        //}

        //private static readonly IDictionary<string, AibuColliderKind[]> aibuTagTable = new Dictionary<string, AibuColliderKind[]>
        //    {
        //        { "mouth", new[] { AibuColliderKind.mouth, AibuColliderKind.reac_head } },
        //        { "muneL", new[] { AibuColliderKind.muneL, AibuColliderKind.reac_bodyup } },
        //        { "muneR", new[] { AibuColliderKind.muneR, AibuColliderKind.reac_bodyup } },
        //        { "kokan", new[] { AibuColliderKind.kokan, AibuColliderKind.reac_bodydown } },
        //        { "anal",  new[] { AibuColliderKind.anal,  AibuColliderKind.reac_bodydown } },
        //        { "siriL", new[] { AibuColliderKind.siriL, AibuColliderKind.reac_bodydown } },
        //        { "siriR", new[] { AibuColliderKind.siriR, AibuColliderKind.reac_bodydown } },
        //        { "Reaction/head", new[] { AibuColliderKind.reac_head } },
        //        { "Reaction/bodyup", new[] { AibuColliderKind.reac_bodyup } },
        //        { "Reaction/bodydown", new[] { AibuColliderKind.reac_bodydown } },
        //        { "Reaction/armL", new[] { AibuColliderKind.reac_armL } },
        //        { "Reaction/armR", new[] { AibuColliderKind.reac_armR } },
        //        { "Reaction/legL", new[] { AibuColliderKind.reac_legL } },
        //        { "Reaction/legR", new[] { AibuColliderKind.reac_legR } },
        //    };


        private static readonly IDictionary<string, AibuColliderKind> colliderNameDic = new Dictionary<string, AibuColliderKind>
        {
            // Siri for hand grab.
            // Mune for boobs grab.
            // Kokan for head pat.
            // Mouth for cheek grab
            // No tags outside of H, so we go with names.
            
            { "com_hit_head", AibuColliderKind.reac_head },
            { "com_hit_cheek", AibuColliderKind.reac_head },
            { "cf_hit_spine01",  AibuColliderKind.reac_bodyup },
            { "cf_hit_spine03", AibuColliderKind.reac_bodyup },
            { "cf_hit_bust02_L", AibuColliderKind.muneL },
            { "cf_hit_bust02_R", AibuColliderKind.muneR },
            { "cf_hit_arm_L", AibuColliderKind.reac_armL },
            { "cf_hit_wrist_L", AibuColliderKind.reac_armL },
            { "com_hit_hand_L", AibuColliderKind.reac_armL },
            { "cf_hit_arm_R", AibuColliderKind.reac_armR },
            { "cf_hit_wrist_R", AibuColliderKind.reac_armR },
            { "com_hit_hand_R", AibuColliderKind.reac_armR },
            { "cf_hit_berry", AibuColliderKind.reac_bodydown },
            { "cf_hit_waist_L", AibuColliderKind.reac_bodydown },
            { "cf_hit_siri_L", AibuColliderKind.reac_bodydown },
            { "cf_hit_siri_R", AibuColliderKind.reac_bodydown },
            { "cf_hit_waist02", AibuColliderKind.reac_bodydown },
            { "cf_hit_thigh01_L", AibuColliderKind.reac_bodydown },
            { "cf_hit_thigh02_L", AibuColliderKind.reac_legL },
            { "cf_hit_leg01_L", AibuColliderKind.reac_legL },
            { "cf_hit_thigh01_R", AibuColliderKind.reac_bodydown },
            { "cf_hit_thigh02_R", AibuColliderKind.reac_legR },
            { "cf_hit_leg01_R", AibuColliderKind.reac_legR },
        };

        ///// <summary>
        /////  Check whether a particular body interaction is allowed.
        ///// </summary>
        //private bool ColliderKindAllowed(HandCtrl.AibuColliderKind kind)
        //{

        //    HandCtrl.ReactionInfo rinfo;
        //    switch (kind)
        //    {
        //        case HandCtrl.AibuColliderKind.none:
        //            return true;
        //        case HandCtrl.AibuColliderKind.mouth:
        //            return hand.nowMES.isTouchAreas[0] &&
        //                (hand.flags.mode == HFlag.EMode.aibu || heroine.isGirlfriend || heroine.isKiss || heroine.denial.kiss);
        //        case HandCtrl.AibuColliderKind.muneL:
        //            return hand.nowMES.isTouchAreas[1];
        //        case HandCtrl.AibuColliderKind.muneR:
        //            return hand.nowMES.isTouchAreas[2];
        //        case HandCtrl.AibuColliderKind.kokan:
        //            return hand.nowMES.isTouchAreas[3];
        //        case HandCtrl.AibuColliderKind.anal:
        //            return hand.nowMES.isTouchAreas[4] &&
        //                (hand.flags.mode == HFlag.EMode.aibu || heroine.hAreaExps[3] > 0f || heroine.denial.anal);
        //        case HandCtrl.AibuColliderKind.siriL:
        //            return hand.nowMES.isTouchAreas[5];
        //        case HandCtrl.AibuColliderKind.siriR:
        //            return hand.nowMES.isTouchAreas[6];
        //        case HandCtrl.AibuColliderKind.reac_head:
        //            return dicNowReaction.TryGetValue(0, out rinfo) && rinfo.isPlay;
        //        case HandCtrl.AibuColliderKind.reac_bodyup:
        //            return dicNowReaction.TryGetValue(1, out rinfo) && rinfo.isPlay;
        //        case HandCtrl.AibuColliderKind.reac_bodydown:
        //            return dicNowReaction.TryGetValue(2, out rinfo) && rinfo.isPlay;
        //        case HandCtrl.AibuColliderKind.reac_armL:
        //            return dicNowReaction.TryGetValue(3, out rinfo) && rinfo.isPlay;
        //        case HandCtrl.AibuColliderKind.reac_armR:
        //            return dicNowReaction.TryGetValue(4, out rinfo) && rinfo.isPlay;
        //        case HandCtrl.AibuColliderKind.reac_legL:
        //            return dicNowReaction.TryGetValue(5, out rinfo) && rinfo.isPlay;
        //        case HandCtrl.AibuColliderKind.reac_legR:
        //            return dicNowReaction.TryGetValue(6, out rinfo) && rinfo.isPlay;
        //    }
        //    VRLog.Warn("AibuKindAllowed: undefined kind: {0}", kind);
        //    return false;
        //}
    }
}
