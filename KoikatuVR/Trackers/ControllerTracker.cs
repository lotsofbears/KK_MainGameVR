using BepInEx;
using Illusion.Extensions;
using KK_VR.Features;
using KK_VR.Interpreters;
using KK_VR.Trackers;
using Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UniRx;
using UnityEngine;
using VRGIN.Core;
using static ActionGame.ActionChangeUI;
using static HandCtrl;
using static RankingScene;
using Random = UnityEngine.Random;

namespace KK_VR.Trackers
{
    class ControllerTracker : Tracker
    {
        private readonly List<Body> _reactOncePerTrack = new List<Body>();
        private float _familiarity;
        private float _lastTrack;
        internal bool firstTrack;

        internal ReactionType reactionType;

        internal override bool AddCollider(Collider other)
        {
            if (_referenceTrackDic.TryGetValue(other, out var info))
            {
                if (info.chara.visibleAll && !IsInBlacklist(info.chara, info.behavior.part))
                {
                    colliderInfo = info;
                    SetReaction();
                    _trackList.Add(other);
                    return true;
                }
            }
            return false;
        }
        internal override bool RemoveCollider(Collider other)
        {
            if (_trackList.Remove(other))
            {
                if (!IsBusy)
                {
                    _lastTrack = Time.time;
                    _reactOncePerTrack.Clear();
                    colliderInfo = null;
                }
                else
                    colliderInfo = _referenceTrackDic[_trackList.Last()];

                return true;
            }
            return false;
        }
        private void GetFamiliarity()
        {
            // Add exp/weak point influence?
            SaveData.Heroine heroine = null;
            if (HSceneInterpreter.hFlag != null)
            {
                heroine = HSceneInterpreter.hFlag.lstHeroine
                    .Where(h => h.chaCtrl == colliderInfo.chara)
                    .FirstOrDefault();
            }
            heroine ??= Game.Instance.HeroineList
                    .Where(h => h.chaCtrl == colliderInfo.chara ||
                    (h.chaCtrl != null
                    && h.chaCtrl.fileParam.fullname == colliderInfo.chara.fileParam.fullname
                    && h.chaCtrl.fileParam.personality == colliderInfo.chara.fileParam.personality))
                    .FirstOrDefault();
            if (heroine != null)
            {
                _familiarity = (0.55f + (0.15f * (int)heroine.HExperience)) * 
                    (HSceneInterpreter.hFlag != null && HSceneInterpreter.hFlag.isFreeH ? 
                    1f : (0.5f + heroine.intimacy * 0.005f));
            }
            else
            {
                // Extra characters/player.
                _familiarity = 0.75f;
            }
        }

        private void SetReaction()
        {
            if (!IsBusy)
            {
                GetFamiliarity();
                firstTrack = true;
                if (_lastTrack + (2f * _familiarity) > Time.time)
                {
                    // Consecutive touch within up to 2 seconds from the last touch.
                    reactionType = Random.value < _familiarity - 0.5f ? ReactionType.Laugh : ReactionType.Short;
                }
                else
                {
                    reactionType = ReactionType.HitReaction;
                }
            }
            else
            {
                firstTrack = false;
                if (ReactOncePerTrack(colliderInfo.behavior.part))
                {
                    // Important part touch, once per track.
                    reactionType = Random.value < _familiarity - 0.5f ? ReactionType.Short : ReactionType.HitReaction;
                }
                else
                {
                    reactionType = ReactionType.None;
                }
            }
        }
        private bool ReactOncePerTrack(Body part)
        {
            if (part < Body.HandL && !_reactOncePerTrack.Contains(part))
            {
                _reactOncePerTrack.Add(part);
                return true;
            }
            return false;
        }
        internal void FlushLimbHandlers()
        {
            for (var i = 0; i < _trackList.Count; i++)
            {
                if (_trackList[i].name.EndsWith("Handler", StringComparison.Ordinal))
                {
                    _trackList.RemoveAt(i);
                    i--;
                }

            }
            SetState();
        }
        /// <param name="preferredSex">0 - male, 1 - female, -1 ignore</param>
        internal Body GetGraspBodyPart(ChaControl tryToAvoidChara = null, int preferredSex = -1)
        {
            return GetCollidersInfo()
                .OrderBy(info => info.chara.sex != preferredSex)
                .ThenBy(info => info.chara != tryToAvoidChara)
                .ThenBy(info => info.behavior.part)
                .First().behavior.part;
        }
        internal Body GetGraspBodyPart()
        {
            return GetCollidersInfo()
                .OrderBy(info => info.behavior.part)
                .First().behavior.part;
        }

    }
}
