using Illusion.Extensions;
using KK_VR.Interpreters;
using Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using VRGIN.Core;
using static ActionGame.ActionChangeUI;
using static HandCtrl;
using static RankingScene;
using Random = UnityEngine.Random;

namespace KK_VR.Handlers
{
    class ColliderTracker
    {
        public bool IsBusy => trackCount > 0;

        private readonly List<Collider> _colliderTrackingList = new List<Collider>();
        //private readonly List<Body> bodyList = new List<Body>();
        private static readonly Dictionary<string, BodyBehavior> _activeColliders = new Dictionary<string, BodyBehavior>();
        private readonly List<Body> _reactOncePerTrack = new List<Body>();
        private float _familiarity;
        private float _lastTrack;

        internal Body bodyPart;
        internal int trackCount;
        internal bool firstTrack;
        internal ReactionType reactionType;
        internal ChaControl chara;
        //internal AibuColliderKind[] suggestedKind = new AibuColliderKind[2];
        //internal Collider suggestedCollider;
        internal AibuColliderKind[] actualKind = new AibuColliderKind[2];

        public static void Initialize(ChaControl chara, bool hScene)
        {
            _activeColliders.Clear();
            var colliders = chara.GetComponentsInChildren<Collider>(includeInactive: true);
            foreach (var collider in colliders)
            {
                if (allPossibleColliders.TryGetValue(collider.name, out var bodyBehavior))
                {
                    _activeColliders.Add(collider.name, bodyBehavior);
                    EnableCollider(collider);
                    VRPlugin.Logger.LogDebug($"Tracker:Start:Collider:Add:{collider.name}");
                }
                else
                {
                    VRPlugin.Logger.LogDebug($"Tracker:Start:Collider:Skip:{collider.name}:{collider.gameObject.layer}");
                }
            }
            var charas = (hScene ? HSceneInterpreter.hFlag.lstHeroine : Game.Instance.HeroineList)
               .Where(h => h.chaCtrl != null && h.chaCtrl.objTop.activeSelf)
               .Select(h => h.chaCtrl)
               .ToList();
            charas.Add(hScene ? HSceneInterpreter.male : Game.Instance.Player.chaCtrl);

            if (charas.Count > 1)
            {
                VRPlugin.Logger.LogDebug($"Tracker:Start:ExtraCharas:{charas.Count - 1}");
                foreach (var c in charas)
                {
                    if (c == chara)
                    {
                        continue;
                    }
                    colliders = c.GetComponentsInChildren<Collider>(includeInactive : true);
                    foreach(var collider in colliders)
                    {
                        if (_activeColliders.ContainsKey(collider.name))
                        {
                            EnableCollider(collider);
                        }
                    }
                }
            }
        }

        private void GetFamiliarity()
        {
            // Add exp/weak point influence?


            chara = _colliderTrackingList[0].gameObject.GetComponentInParent<ChaControl>();
            SaveData.Heroine heroine = null;
            if (HSceneInterpreter.hFlag != null)
            {
                heroine = HSceneInterpreter.hFlag.lstHeroine
                    .Where(h => h.chaCtrl == chara)
                    .FirstOrDefault();
            }
            if (heroine == null)
            {
                heroine = Game.Instance.HeroineList
                    .Where(h => h.chaCtrl == chara || 
                    (h.chaCtrl != null 
                    && h.chaCtrl.fileParam.fullname == chara.fileParam.fullname 
                    && h.chaCtrl.fileParam.personality == chara.fileParam.personality))
                    .FirstOrDefault();
            }
            if (heroine != null)
            {
                _familiarity = (0.55f + (0.15f * (int)heroine.HExperience)) * (HSceneInterpreter.hFlag != null && HSceneInterpreter.hFlag.isFreeH ? 1f : (0.5f + heroine.intimacy * 0.005f));
            }
            else
            {
                // Extra characters/player.
                _familiarity = 0.75f;
            }
        }

        public enum ReactionType
        {
            None,
            Laugh,
            Short,
            HitReaction
            // Slap Reaction? extra gotta modify reac dic for that 
        }
        private void SetReaction()
        {
            if (!IsBusy)
            {
                // Start of track.
                firstTrack = true;
                GetFamiliarity();
                if (_lastTrack + (2f * _familiarity) > Time.time)
                {
                    // Consecutive touch within ~2 seconds from the last touch.
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

                if (bodyPart < Body.HandL && !_reactOncePerTrack.Contains(bodyPart))
                {
                    // Important part touch, once per track.
                    _reactOncePerTrack.Add(bodyPart);
                    reactionType = Random.value < _familiarity - 0.5f ? ReactionType.Short : ReactionType.HitReaction;
                }
                else
                {
                    reactionType = ReactionType.None;
                }
            }
        }
        private static void EnableCollider(Collider collider)
        {
            collider.enabled = true;
            collider.gameObject.layer = 10;
            collider.gameObject.SetActive(true);
        }
        private BodyBehavior GetSuggestedBehavior()
        {
            var bodyParts = _activeColliders
                    .Where(kv => _colliderTrackingList.Any(t => t.name.Equals(kv.Key)))
                    .Select(kv => kv.Value)
                    .OrderBy(kv => kv.part);
            var touch = bodyParts.FirstOrDefault(b => b.touch != AibuColliderKind.none);
            if (touch.part == Body.None)
            {
                touch = bodyParts.First();
            }
            return touch;
        }
        internal AibuColliderKind[] GetSuggestedKinds()
        {
            var bodyParts = _activeColliders
                    .Where(kv => _colliderTrackingList.Any(t => t.name.Equals(kv.Key)))
                    .Select(kv => kv.Value)
                    .OrderBy(kv => kv.part);
            var touch = bodyParts.FirstOrDefault(b => b.touch != AibuColliderKind.none);
            if (touch.part == Body.None)
            {
                touch = bodyParts.First();
            }
            AibuColliderKind[] array = { touch.react, touch.touch };
            return array;
        }
        //private void SetSuggestedKinds()
        //{
        //    var bestCandidate = GetSuggestedKind();
        //    VRPlugin.Logger.LogDebug($"BestCandidate:{bestCandidate.part}:{bestCandidate.react}:{bestCandidate.touch}");
        //    suggestedKind[0] = bestCandidate.react;
        //    suggestedKind[1] = bestCandidate.touch;
        //}
        public bool AddCollider(Collider other)
        {
            // Can't think of a better way to check visibility of ChaControl this collider is parented to.
            if (_activeColliders.ContainsKey(other.name) && other.gameObject.GetComponentInParent<ChaControl>().visibleAll)
            {
                _colliderTrackingList.Add(other);

                SetCurrentCollider(other);

                //SetSuggestedKinds();
                SetReaction();
                trackCount++;
                return true;
            }
            return false;
        }
        private void SetCurrentCollider(Collider other)
        {
            var trackingInfo = _activeColliders[other.name];

            bodyPart = trackingInfo.part;
            actualKind[0] = trackingInfo.react;
            actualKind[1] = trackingInfo.touch;
        }
        public bool RemoveCollider(Collider other)
        {
            if (_colliderTrackingList.Remove(other))
            {
                trackCount--;
                //VRPlugin.Logger.LogDebug($"Tracking:Remove:{other.name}:[{colliderTrackingList.Count}]");
                if (trackCount == 0)
                {
                    bodyPart = Body.None;
                    _reactOncePerTrack.Clear();
                    _lastTrack = Time.time;

                    actualKind[0] = AibuColliderKind.none;
                    actualKind[1] = AibuColliderKind.none;
                    //suggestedKind[0] = AibuColliderKind.none;
                    //suggestedKind[1] = AibuColliderKind.none;
                }
                else
                {
                    SetCurrentCollider(_colliderTrackingList.Last());
                    //SetSuggestedKinds();
                }
                return true;
            }
            return false;
        }

        //public AibuColliderKind[] GetReactionKind(out ChaControl chara)
        //{
        //    //VRPlugin.Logger.LogDebug($"Tracker:Reaction:Part[{_currentPart}]:TrackCount[{trackingColliders.Count}]");
        //    chara = colliderTrackingList[0].gameObject.GetComponentInParent<ChaControl>();

        //    var trackingKv = activeColliders
        //        .Where(kv => colliderTrackingList.Any(t => t.name.Equals(kv.Key)))
        //        .OrderByDescending(kv => kv.Value.react)
        //        .ToList();

        //    var touch = trackingKv
        //        .Where(kv => kv.Value.touch != AibuColliderKind.none)
        //        .OrderBy(kv => kv.Value.touch)
        //        .ToList();

        //    if (touch.Count > 0)
        //    {
        //        var touchArg = AibuKindAllowed(touch[0].Value.touch, chara) ? touch[0].Value.touch : AibuColliderKind.none;
        //        AibuColliderKind[] array = { touch[0].Value.react, touchArg };
        //        return array;
        //    }
        //    else
        //    {
        //        AibuColliderKind[] array = { trackingKv[0].Value.react, AibuColliderKind.none };
        //        return array;
        //    }
        //}

        public Body GetUndressKind()
        {
            //var part = _activeColliders
            //    .Where(kv => _colliderTrackingList.Any(t => t.name.Equals(kv.Key)))
            //    .Select(kv => kv.Value)
            //    .OrderBy(v => v.part)
            //    .First();
            return ConvertToUndress(GetSuggestedBehavior().part);
        }
        private Body ConvertToUndress(Body body)
        {
            return body switch
            {
                Body.Head => Body.None,
                Body.HandR => Body.HandL,
                Body.ArmR => Body.ArmL,
                Body.MuneR => Body.MuneL,
                Body.Groin => Body.Asoko,
                _ => body
            };
        }

        struct BodyBehavior
        {
            public Body part;
            public AibuColliderKind react;
            public AibuColliderKind touch;
        }

        private static readonly Dictionary<string, BodyBehavior> allPossibleColliders = new Dictionary<string, BodyBehavior>()
        {
            {
                "com_hit_head", new BodyBehavior
                {
                    part = Body.Head,
                    react = AibuColliderKind.reac_head,
                    touch = AibuColliderKind.reac_head
                }
            },
            {
                "com_hit_cheek", new BodyBehavior
                {
                    part = Body.Head,
                    react = AibuColliderKind.reac_head,
                    touch = AibuColliderKind.mouth
                }
            },
            {
                "aibu_hit_mouth", new BodyBehavior
                {
                    part = Body.Head,
                    react = AibuColliderKind.reac_head,
                    touch = AibuColliderKind.mouth
                }
            },
            // Far too big
            //{
            //    "aibu_hit_head", new BodyBehavior
            //    {
            //        part = Body.Head,
            //        react = AibuColliderKind.reac_head
            //    }
            //},
            {
                "cf_hit_spine01", new BodyBehavior
                {
                    part = Body.UpperBody,
                    react = AibuColliderKind.reac_bodyup,
                }
            },
            {
                "cf_hit_spine03", new BodyBehavior
                {
                    part = Body.UpperBody,
                    react = AibuColliderKind.reac_bodyup,
                }
            },
            {
                "cf_hit_bust02_L", new BodyBehavior
                {
                    part = Body.MuneL,
                    react = AibuColliderKind.reac_bodyup,
                    touch = AibuColliderKind.muneL
                }
            },
            {
                "cf_hit_bust02_R", new BodyBehavior
                {
                    part = Body.MuneR,
                    react = AibuColliderKind.reac_bodyup,
                    touch = AibuColliderKind.muneR
                }
            },
            {
                "cf_hit_arm_L", new BodyBehavior
                {
                    part = Body.ArmL,
                    react = AibuColliderKind.reac_armL,
                }
            },
            {
                "cf_hit_wrist_L", new BodyBehavior
                {
                    part = Body.ArmL,
                    react = AibuColliderKind.reac_armL,
                }
            },
            {
                "cf_hit_arm_R", new BodyBehavior
                {
                    part = Body.ArmR,
                    react = AibuColliderKind.reac_armR,
                }
            },
            {
                "cf_hit_wrist_R", new BodyBehavior
                {
                    part = Body.ArmR,
                    react = AibuColliderKind.reac_armR,
                }
            },
            {
                "com_hit_hand_L", new BodyBehavior
                {
                    part = Body.HandL,
                    react = AibuColliderKind.reac_armL,
                    touch = AibuColliderKind.reac_armL
                }
            },
            {
                "com_hit_hand_R", new BodyBehavior
                {
                    part = Body.HandR,
                    react = AibuColliderKind.reac_armR,
                    touch = AibuColliderKind.reac_armR
                }
            },
            {
                "cf_hit_berry", new BodyBehavior
                {
                    part = Body.LowerBody,
                    react = AibuColliderKind.reac_bodydown
                }
            },
            {
                "cf_hit_waist_L", new BodyBehavior
                {
                    part = Body.LowerBody,
                    react = AibuColliderKind.reac_bodydown
                }
            },
            //{
            //    "cf_hit_siri_L", new BodyBehavior
            //    {
            //        part = Body.Groin,
            //        react = AibuColliderKind.reac_bodydown,
            //        touch = AibuColliderKind.siriL
            //    }
            //},
            //{
            //    "cf_hit_siri_R", new BodyBehavior
            //    {
            //        part = Body.Groin,
            //        react = AibuColliderKind.reac_bodydown,
            //        touch = AibuColliderKind.siriR
            //    }
            //},
            {
                "cf_hit_waist02", new BodyBehavior
                {
                    part = Body.Groin,
                    react = AibuColliderKind.reac_bodydown
                }
            },
            {
                "aibu_hit_siri_L", new BodyBehavior
                {
                    part = Body.Groin,
                    react = AibuColliderKind.reac_bodydown,
                    touch = AibuColliderKind.siriL
                }
            },
            {
                "aibu_hit_siri_R", new BodyBehavior
                {
                    part = Body.Groin,
                    react = AibuColliderKind.reac_bodydown,
                    touch = AibuColliderKind.siriR
                }
            },
            {
                "aibu_hit_kokan", new BodyBehavior
                {
                    part = Body.Asoko,
                    react = AibuColliderKind.reac_bodydown,
                    touch = AibuColliderKind.kokan
                }
            },
            {
                "aibu_hit_ana", new BodyBehavior
                {
                    part = Body.Asoko,
                    react = AibuColliderKind.reac_bodydown,
                    touch = AibuColliderKind.anal
                }
            },
            {
                "cf_hit_thigh01_L", new BodyBehavior
                {
                    part = Body.Thigh,
                    react = AibuColliderKind.reac_bodydown
                }
            },
            {
                "cf_hit_thigh01_R", new BodyBehavior
                {
                    part = Body.Thigh,
                    react = AibuColliderKind.reac_bodydown
                }
            },
            //{
            //    "cf_hit_thigh02_L", new BodyBehavior
            //    {
            //        part = Body.LegL,
            //        react = AibuColliderKind.reac_legL
            //    }
            //},
            //{
            //    "cf_hit_leg01_L", new BodyBehavior
            //    {
            //        part = Body.LegL,
            //        react = AibuColliderKind.reac_legL
            //    }
            //},
            {
                "aibu_reaction_legL", new BodyBehavior
                {
                    part = Body.LegL,
                    react = AibuColliderKind.reac_legL
                }
            },
            //{
            //    "cf_hit_thigh02_R", new BodyBehavior
            //    {
            //        part = Body.LegR,
            //        react = AibuColliderKind.reac_legR
            //    }
            //},
            //{
            //    "cf_hit_leg01_R", new BodyBehavior
            //    {
            //        part = Body.LegR,
            //        react = AibuColliderKind.reac_legR
            //    }
            //},
            {
                "aibu_reaction_legR", new BodyBehavior
                {
                    part = Body.LegR,
                    react = AibuColliderKind.reac_legR
                }
            },
            {
                "aibu_reaction_thighL", new BodyBehavior
                {
                    part = Body.LegL,
                    react = AibuColliderKind.reac_legL
                }
            },
            {
                "aibu_reaction_thighR", new BodyBehavior
                {
                    part = Body.LegR,
                    react = AibuColliderKind.reac_legR
                }
            }
        };

        public enum Body
        {
            None,
            Head,
            MuneL,
            MuneR,
            Asoko,
            HandL,
            HandR,
            ArmL,
            ArmR,
            //ForearmL,
            //ForearmR,
            LowerBody,
            UpperBody,
            Groin,
            Thigh,
            LegL,
            LegR,
        }
    }
}
