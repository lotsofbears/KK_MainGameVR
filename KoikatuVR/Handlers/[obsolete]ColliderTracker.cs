//using BepInEx;
//using Illusion.Extensions;
//using KK_VR.Features;
//using KK_VR.Interpreters;
//using Manager;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using UniRx;
//using UnityEngine;
//using VRGIN.Core;
//using static ActionGame.ActionChangeUI;
//using static HandCtrl;
//using static RankingScene;
//using Random = UnityEngine.Random;

//namespace KK_VR.Handlers
//{
//    class ColliderTracker
//    {
//        internal bool IsBusy => _trackList.Count > 0;

//        //private readonly List<Collider> _colliderTrackingList = new List<Collider>();
//        //private readonly List<Body> bodyList = new List<Body>();

//        /// <summary>
//        /// Stores info about allowed colliders from all charas. Initiated once per scene if are trackers needed.
//        /// </summary>
//        private static readonly Dictionary<Collider, ColliderInfo> _referenceTrackDic = new Dictionary<Collider, ColliderInfo>();
//        private IDictionary<ChaControl, List<Body>> _blacklistDic;
//        private readonly List<Collider> _trackList = new List<Collider>();
//        private readonly List<Body> _reactOncePerTrack = new List<Body>();
//        private float _familiarity;
//        private float _lastTrack;

//        internal ColliderInfo colliderInfo;
//        //internal int trackCount;
//        internal bool firstTrack;
//        internal ReactionType reactionType;
//        //internal AibuColliderKind[] suggestedKind = new AibuColliderKind[2];
//        //internal Collider suggestedCollider;
//        internal ColliderTracker()
//        {
//            _blacklistDic = GraspController.Instance.GetBlacklistDic;
//        }
//        internal class ColliderInfo
//        {
//            internal ColliderInfo(Collider _collider, BodyBehavior _behavior, ChaControl _chara)
//            {
//                collider = _collider;
//                behavior = _behavior;
//                chara = _chara;
//            }
//            internal ChaControl chara;
//            internal Collider collider;
//            internal BodyBehavior behavior;
//        }

//        internal static void Initialize(IEnumerable<ChaControl> charas)
//        {
//            _referenceTrackDic.Clear();
//            foreach (var chara in charas)
//            {
//                if (chara == null) continue;
//                foreach (var collider in chara.GetComponentsInChildren<Collider>(includeInactive: true))
//                {
//                    if (_allowedColliders.TryGetValue(collider.name, out var bodyBehavior))
//                    {
//                        EnableCollider(collider);
//                        _referenceTrackDic.Add(collider, new ColliderInfo(collider, bodyBehavior, chara));
//                    }
//                }
//            }
//        }

//        private static void EnableCollider(Collider collider)
//        {
//            collider.enabled = true;

//            // Watch this, some are triggers by default, might break something ?
//            collider.isTrigger = false;
//            collider.gameObject.layer = 10;
//            collider.gameObject.SetActive(true);
//        }
//        private void GetFamiliarity()
//        {
//            // Add exp/weak point influence?
//            SaveData.Heroine heroine = null;
//            var chara = colliderInfo.chara;
//            if (HSceneInterpreter.hFlag != null)
//            {
//                heroine = HSceneInterpreter.hFlag.lstHeroine
//                    .Where(h => h.chaCtrl == chara)
//                    .FirstOrDefault();
//            }
//            heroine ??= Game.Instance.HeroineList
//                    .Where(h => h.chaCtrl == chara || 
//                    (h.chaCtrl != null 
//                    && h.chaCtrl.fileParam.fullname == chara.fileParam.fullname 
//                    && h.chaCtrl.fileParam.personality == chara.fileParam.personality))
//                    .FirstOrDefault();
//            if (heroine != null)
//            {
//                _familiarity = (0.55f + (0.15f * (int)heroine.HExperience)) * (HSceneInterpreter.hFlag != null && HSceneInterpreter.hFlag.isFreeH ? 1f : (0.5f + heroine.intimacy * 0.005f));
//            }
//            else
//            {
//                // Extra characters/player.
//                _familiarity = 0.75f;
//            }
//        }

//        public enum ReactionType
//        {
//            None,
//            Laugh,
//            Short,
//            HitReaction
//            // Slap Reaction? extra gotta modify reac dic for that 
//        }
//        private void SetReaction()
//        {
//            if (!IsBusy)
//            {
//                // The very start of a track.
//                GetFamiliarity();
//                firstTrack = true;
//                if (_lastTrack + (2f * _familiarity) > Time.time)
//                {
//                    // Consecutive touch within up to 2 seconds from the last touch.
//                    reactionType = Random.value < _familiarity - 0.5f ? ReactionType.Laugh : ReactionType.Short;
//                }
//                else
//                {
//                    reactionType = ReactionType.HitReaction;
//                }
//            }
//            else
//            {
//                firstTrack = false;

//                if (ReactOncePerTrack(colliderInfo.behavior.part))
//                {
//                    // Important part touch, once per track.
//                    reactionType = Random.value < _familiarity - 0.5f ? ReactionType.Short : ReactionType.HitReaction;
//                }
//                else
//                {
//                    reactionType = ReactionType.None;
//                }
//            }
//        }
//        private bool ReactOncePerTrack(Body part)
//        {
//            if (part < Body.HandL && !_reactOncePerTrack.Contains(part))
//            {
//                _reactOncePerTrack.Add(part);
//                return true;
//            }
//            return false;
//        }

//        private IEnumerable<ColliderInfo> GetCollidersInfo()
//        {
//            return _referenceTrackDic
//                .Where(kv => _trackList.Any(collider => collider.Equals(kv.Key)))
//                .Select (kv => kv.Value);
//        }

//        //internal void SetSuggestedInfo()
//        //{
//        //    var infoList = GetCollidersInfo()
//        //        .OrderBy(info => info.behavior.part);
//        //    //if (_blacklistDic.Count != 0)
//        //    //{
//        //    //    infoList = infoList
//        //    //        .Where(info => !_blacklistDic.ContainsKey(info.chara) || !_blacklistDic[info.chara].Contains(info.behavior.part));
//        //    //    if (infoList.Count() == 0)
//        //    //    {
//        //    //        colliderInfo = null;
//        //    //        return;
//        //    //    }
//        //    //}
//        //    //infoList = infoList.OrderBy(info => info.behavior.part);
//        //    colliderInfo = infoList.FirstOrDefault(info => info.behavior.touch != AibuColliderKind.none) ?? infoList.First();
            
//        //}
//        internal void SetSuggestedInfoNoBlacks()
//        {
//            var infoList = GetCollidersInfo();
//            if (_blacklistDic.Count != 0)
//            {
//                foreach (var kv in _blacklistDic)
//                {
//                    kv.Value.ForEach(b => VRPlugin.Logger.LogDebug($"Blacklist:{kv.Key.name} - {b}"));
//                }
//                infoList = infoList
//                    .Where(info => !_blacklistDic.ContainsKey(info.chara) || (!_blacklistDic[info.chara].Contains(Body.None) && !_blacklistDic[info.chara].Contains(info.behavior.part)));
//                if (infoList.Count() == 0)
//                {
//                    colliderInfo = null;
//                    return;
//                }
//            }
//            colliderInfo = infoList
//                .OrderBy(info => info.behavior.part)
//                .First();
//        }
//        internal void SetSuggestedInfo(ChaControl tryToAvoid = null)
//        {
//            var infoList = GetCollidersInfo();

//            if (tryToAvoid == null)
//            {
//                infoList = infoList
//                    .OrderBy(info => info.chara == tryToAvoid)
//                    .ThenBy(info => info.behavior.part);
//            }
//            else
//            {
//                infoList = infoList
//                    .OrderBy(info => info.behavior.part);
//            }

//            colliderInfo = infoList.FirstOrDefault(info => info.behavior.touch != AibuColliderKind.none) ?? infoList.First();
//        }
//        internal void FlushBlackTracks()
//        {
//            // Turns out flush is nice only for synced limbs, for the rest we want not flush but suppression.

//            VRPlugin.Logger.LogDebug($"Tracker:FlushBlackTracks:{_trackList.Count}");
//            for (var i = 0; i < _trackList.Count; i++)
//            {
//                var info = _referenceTrackDic[_trackList[i]];
//                if (_blacklistDic.ContainsKey(info.chara) && _blacklistDic[info.chara].Contains(info.behavior.part))
//                {
//                    _trackList.Remove(_trackList[i]);
//                    i--;
//                }
//            }
//        }
//        /// <param name="preferredSex">0 - male, 1 - female, -1 ignore</param>
//        internal Body GetGraspBodyPart(ChaControl tryToAvoidChara = null, int preferredSex = -1)
//        {
//            return GetCollidersInfo()
//                .OrderBy(info => info.chara.sex != preferredSex)
//                .ThenBy(info => info.chara != tryToAvoidChara)
//                .ThenBy(info => info.behavior.part)
//                .First().behavior.part;
//        }
//        internal Body GetGraspBodyPart()
//        {
//            return GetCollidersInfo()
//                .OrderBy(info => info.behavior.part)
//                .First().behavior.part;
//        }
//        internal bool AddCollider(Collider other)
//        {
//            if (_referenceTrackDic.TryGetValue(other, out var info))
//            {
//                if (info.chara.visibleAll && !IsInBlacklist(info.chara, info.behavior.part))
//                {
//                    colliderInfo = info;
//                    SetReaction();
//                    _trackList.Add(other);
//                    return true;
//                }
//            }
//            return false;
//        }
//        internal void RemoveBlacklistedTracks()
//        {
//            if (_blacklistDic == null) return;
//            for (var i = 0; i < _trackList.Count; i++)
//            {
//                var colliderInfo = _referenceTrackDic[_trackList[i]];
//                var chara = colliderInfo.chara;
//                if (_blacklistDic.ContainsKey(chara) && _blacklistDic[chara].Contains(colliderInfo.behavior.part))
//                {
//                    _trackList.Remove(_trackList[i]);
//                    // On removal index shifts for -1.
//                    i--;
//                }
//            }
//        }
//        internal bool RemoveCollider(Collider other)
//        {
//            if (_trackList.Remove(other))
//            {
//                if (!IsBusy)
//                {
//                    _lastTrack = Time.time;
//                    _reactOncePerTrack.Clear();
//                }
//                else
//                    colliderInfo = _referenceTrackDic[_trackList.Last()];

//                return true;
//            }
//            return false;
//        }
//        private bool IsInBlacklist(ChaControl chara, Body part)
//        {
//            if (_blacklistDic.Count != 0 && _blacklistDic.ContainsKey(chara) && _blacklistDic[chara].Contains(part | Body.None))
//            {
//                return true;
//            }
//            return false;
//        }

//        internal struct BodyBehavior
//        {
//            public Body part;
//            public AibuColliderKind react;
//            public AibuColliderKind touch;
//        }

//        private static readonly Dictionary<string, BodyBehavior> _allowedColliders = new Dictionary<string, BodyBehavior>()
//        {
//            {
//                "com_hit_head", new BodyBehavior
//                {
//                    part = Body.Head,
//                    react = AibuColliderKind.reac_head,
//                    touch = AibuColliderKind.reac_head
//                }
//            },
//            {
//                "com_hit_cheek", new BodyBehavior
//                {
//                    part = Body.Head,
//                    react = AibuColliderKind.reac_head,
//                    touch = AibuColliderKind.mouth
//                }
//            },
//            {
//                "aibu_hit_mouth", new BodyBehavior
//                {
//                    part = Body.Head,
//                    react = AibuColliderKind.reac_head,
//                    touch = AibuColliderKind.mouth
//                }
//            },
//            // Far too big
//            //{
//            //    "aibu_hit_head", new BodyBehavior
//            //    {
//            //        part = Body.Head,
//            //        react = AibuColliderKind.reac_head
//            //    }
//            //},
//            {
//                "cf_hit_spine01", new BodyBehavior
//                {
//                    part = Body.UpperBody,
//                    react = AibuColliderKind.reac_bodyup,
//                }
//            },
//            {
//                "cf_hit_spine03", new BodyBehavior
//                {
//                    part = Body.UpperBody,
//                    react = AibuColliderKind.reac_bodyup,
//                }
//            },
//            {
//                "cf_hit_bust02_L", new BodyBehavior
//                {
//                    part = Body.MuneL,
//                    react = AibuColliderKind.reac_bodyup,
//                    touch = AibuColliderKind.muneL
//                }
//            },
//            {
//                "cf_hit_bust02_R", new BodyBehavior
//                {
//                    part = Body.MuneR,
//                    react = AibuColliderKind.reac_bodyup,
//                    touch = AibuColliderKind.muneR
//                }
//            },
//            {
//                "cf_hit_arm_L", new BodyBehavior
//                {
//                    part = Body.ArmL,
//                    react = AibuColliderKind.reac_armL,
//                }
//            },
//            {
//                "cf_hit_wrist_L", new BodyBehavior
//                {
//                    part = Body.ForearmL,
//                    react = AibuColliderKind.reac_armL,
//                }
//            },
//            {
//                "cf_hit_arm_R", new BodyBehavior
//                {
//                    part = Body.ArmR,
//                    react = AibuColliderKind.reac_armR,
//                }
//            },
//            {
//                "cf_hit_wrist_R", new BodyBehavior
//                {
//                    part = Body.ForearmR,
//                    react = AibuColliderKind.reac_armR,
//                }
//            },
//            {
//                "com_hit_hand_L", new BodyBehavior
//                {
//                    part = Body.HandL,
//                    react = AibuColliderKind.reac_armL,
//                    touch = AibuColliderKind.reac_armL
//                }
//            },
//            {
//                "com_hit_hand_R", new BodyBehavior
//                {
//                    part = Body.HandR,
//                    react = AibuColliderKind.reac_armR,
//                    touch = AibuColliderKind.reac_armR
//                }
//            },
//            {
//                "cf_hit_berry", new BodyBehavior
//                {
//                    part = Body.LowerBody,
//                    react = AibuColliderKind.reac_bodydown
//                }
//            },
//            {
//                "cf_hit_waist_L", new BodyBehavior
//                {
//                    part = Body.LowerBody,
//                    react = AibuColliderKind.reac_bodydown
//                }
//            },
//            //{
//            //    "cf_hit_siri_L", new BodyBehavior
//            //    {
//            //        part = Body.Groin,
//            //        react = AibuColliderKind.reac_bodydown,
//            //        touch = AibuColliderKind.siriL
//            //    }
//            //},
//            //{
//            //    "cf_hit_siri_R", new BodyBehavior
//            //    {
//            //        part = Body.Groin,
//            //        react = AibuColliderKind.reac_bodydown,
//            //        touch = AibuColliderKind.siriR
//            //    }
//            //},
//            {
//                "cf_hit_waist02", new BodyBehavior
//                {
//                    part = Body.Groin,
//                    react = AibuColliderKind.reac_bodydown
//                }
//            },
//            {
//                "aibu_hit_siri_L", new BodyBehavior
//                {
//                    part = Body.Groin,
//                    react = AibuColliderKind.reac_bodydown,
//                    touch = AibuColliderKind.siriL
//                }
//            },
//            {
//                "aibu_hit_siri_R", new BodyBehavior
//                {
//                    part = Body.Groin,
//                    react = AibuColliderKind.reac_bodydown,
//                    touch = AibuColliderKind.siriR
//                }
//            },
//            {
//                "aibu_hit_kokan", new BodyBehavior
//                {
//                    part = Body.Asoko,
//                    react = AibuColliderKind.reac_bodydown,
//                    touch = AibuColliderKind.kokan
//                }
//            },
//            {
//                "aibu_hit_ana", new BodyBehavior
//                {
//                    part = Body.Asoko,
//                    react = AibuColliderKind.reac_bodydown,
//                    touch = AibuColliderKind.anal
//                }
//            },
//            {
//                "cf_hit_thigh01_L", new BodyBehavior
//                {
//                    part = Body.ThighL,
//                    react = AibuColliderKind.reac_bodydown
//                }
//            },
//            {
//                "cf_hit_thigh01_R", new BodyBehavior
//                {
//                    part = Body.ThighR,
//                    react = AibuColliderKind.reac_bodydown
//                }
//            },
//            //{
//            //    // Test
//            //    "cf_hit_thigh02_L", new BodyBehavior
//            //    {
//            //        part = Body.LegL,
//            //        react = AibuColliderKind.reac_legL
//            //    }
//            //},
//            //{
//            //    "cf_hit_leg01_L", new BodyBehavior
//            //    {
//            //        part = Body.LegL,
//            //        react = AibuColliderKind.reac_legL
//            //    }
//            //},
//            {
//                "aibu_reaction_legL", new BodyBehavior
//                {
//                    part = Body.LegL,
//                    react = AibuColliderKind.reac_legL
//                }
//            },
//            //{
//            //    // Test
//            //    "cf_hit_thigh02_R", new BodyBehavior
//            //    {
//            //        part = Body.LegR,
//            //        react = AibuColliderKind.reac_legR
//            //    }
//            //},
//            //{
//            //    "cf_hit_leg01_R", new BodyBehavior
//            //    {
//            //        part = Body.LegR,
//            //        react = AibuColliderKind.reac_legR
//            //    }
//            //},
//            {
//                "aibu_reaction_legR", new BodyBehavior
//                {
//                    part = Body.LegR,
//                    react = AibuColliderKind.reac_legR
//                }
//            },
//            {
//                "aibu_reaction_thighL", new BodyBehavior
//                {
//                    part = Body.LegL,
//                    react = AibuColliderKind.reac_legL
//                }
//            },
//            {
//                "aibu_reaction_thighR", new BodyBehavior
//                {
//                    part = Body.LegR,
//                    react = AibuColliderKind.reac_legR
//                }
//            }
//        };

//        internal enum Body
//        {
//            None,
//            Head,
//            MuneL,
//            MuneR,
//            Asoko,
//            HandL,
//            HandR,
//            ForearmL,
//            ForearmR,
//            ArmL,
//            ArmR,
//            LowerBody,
//            UpperBody,
//            Groin,
//            ThighL,
//            ThighR,
//            LegL,
//            LegR,
//        }
//    }
//}
