using KK_VR.Interpreters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using VRGIN.Core;
using static HandCtrl;
using Random = UnityEngine.Random;

namespace KK_VR.Handlers
{
    class ColliderTracker
    {
        public bool IsBusy => colliderTrackingList.Count > 0;

        private readonly List<Collider> colliderTrackingList = new List<Collider>();
        //private readonly List<Body> bodyList = new List<Body>();
        private static readonly Dictionary<string, BodyBehavior> activeColliders = new Dictionary<string, BodyBehavior>();
        private Body _trackingBody;
        private readonly List<Body> _reactOncePerTrack = new List<Body>();
        public static void Initialize(ChaControl chara, bool hScene)
        {
            activeColliders.Clear();
            var colliders = chara.GetComponentsInChildren<Collider>(includeInactive: true);
            foreach (var collider in colliders)
            {
                if (allPossibleColliders.TryGetValue(collider.name, out var bodyBehavior))
                {
                    activeColliders.Add(collider.name, bodyBehavior);
                    EnableCollider(collider);
                    VRPlugin.Logger.LogDebug($"Tracker:Start:Collider:Add:{collider.name}");
                }
                else
                {
                    VRPlugin.Logger.LogDebug($"Tracker:Start:Collider:Skip:{collider.name}");
                }
            }
            var charas = (hScene ? HSceneInterpreter.hFlag.lstHeroine : Manager.Game.Instance.HeroineList)
               .Where(h => h.chaCtrl != null && h.chaCtrl.objTop.activeSelf)
               .Select(h => h.chaCtrl)
               .ToList();
            charas.Add(hScene ? HSceneInterpreter.male : Manager.Game.Instance.Player.chaCtrl);

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
                        if (activeColliders.ContainsKey(collider.name))
                        {
                            EnableCollider(collider);
                        }
                    }
                }
            }
        }

        private static void EnableCollider(Collider collider)
        {
            // Layers are necessary? Didn't work with non-chara ones.
            collider.enabled = true;
            collider.gameObject.layer = 10;
            collider.gameObject.SetActive(true);
        }
        public ChaControl GetChara()
        {
            return colliderTrackingList[0].gameObject.GetComponentInParent<ChaControl>();
        }
        public bool AddCollider(Collider other, out AibuColliderKind[] colliderKind)
        {
            colliderKind = new AibuColliderKind[2];
            if (activeColliders.ContainsKey(other.name))
            {
                var trackingInfo = activeColliders[other.name];
                //var wasBusy = IsBusy && _lastTrack > Time.time;
                var shouldReact = (!IsBusy && _lastTrack < Time.time) || ShouldReact(trackingInfo.part);
                colliderTrackingList.Add(other);
                _trackingBody = trackingInfo.part;

                var bestCandidate = activeColliders
                    .Where(kv => colliderTrackingList.Any(t => t.name.Equals(kv.Key)))
                    .Select(kv => kv.Value)
                    .OrderBy(kv => kv.part)
                    .First();


                VRPlugin.Logger.LogDebug($"Tracking:Add:{other.name}:[{colliderTrackingList.Count}]:{bestCandidate.react}:{bestCandidate.touch}:{shouldReact}");
                colliderKind[0] = bestCandidate.react;
                colliderKind[1] = bestCandidate.touch; 
                return shouldReact;// !wasBusy || ShouldReact(trackingKv.part);
            }
            return false;
        }

        private bool ShouldReact(Body part)
        {
            if (part > Body.HandL && !_reactOncePerTrack.Contains(part))
            {
                _reactOncePerTrack.Add(part);
                return true;
            }
            return false;
        }
        private float _lastTrack;
        public bool RemoveCollider(Collider other, out AibuColliderKind[] colliderKind)
        {
            colliderKind = new AibuColliderKind[2];
            if (colliderTrackingList.Remove(other))
            {
                VRPlugin.Logger.LogDebug($"Tracking:Remove:{other.name}:[{colliderTrackingList.Count}]");
                if (colliderTrackingList.Count == 0)
                {
                    _trackingBody = Body.None;
                    _reactOncePerTrack.Clear();
                    _lastTrack = Time.time;
                }
                else
                {
                    var trackingKv = activeColliders
                        .Where(kv => colliderTrackingList.Any(t => t.name.Equals(kv.Key)))
                        .Select(kv => kv.Value)
                        .OrderBy(kv => kv.part)
                        .First();
                    colliderKind[0] = trackingKv.react;
                    colliderKind[1] = trackingKv.touch;
                }
                return true;
            }
            return false;
        }

        public AibuColliderKind[] GetReactionKind(out ChaControl chara)
        {
            //VRPlugin.Logger.LogDebug($"Tracker:Reaction:Part[{_currentPart}]:TrackCount[{trackingColliders.Count}]");
            chara = colliderTrackingList[0].gameObject.GetComponentInParent<ChaControl>();

            var trackingKv = activeColliders
                .Where(kv => colliderTrackingList.Any(t => t.name.Equals(kv.Key)))
                .OrderByDescending(kv => kv.Value.react)
                .ToList();

            var touch = trackingKv
                .Where(kv => kv.Value.touch != AibuColliderKind.none)
                .OrderBy(kv => kv.Value.touch)
                .ToList();

            if (touch.Count > 0)
            {
                var touchArg = AibuKindAllowed(touch[0].Value.touch, chara) ? touch[0].Value.touch : AibuColliderKind.none;
                AibuColliderKind[] array = { touch[0].Value.react, touchArg };
                return array;
            }
            else
            {
                AibuColliderKind[] array = { trackingKv[0].Value.react, AibuColliderKind.none };
                return array;
            }
        }

        public Body GetUndressKind(out ChaControl chara)
        {
            //VRPlugin.Logger.LogDebug($"Tracker:Undress:Part[{_currentPart}]:TrackCount[{trackingColliders.Count}]");
            chara = GetChara();
            var part = activeColliders
                .Where(kv => colliderTrackingList.Any(t => t.name.Equals(kv.Key)))
                .Select(kv => kv.Value)
                .OrderBy(v => v.part)
                .First();
            return ConvertToUndress(part.part);
        }

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
            {
                "aibu_hit_head", new BodyBehavior
                {
                    part = Body.Head,
                    react = AibuColliderKind.reac_head,
                }
            },
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
            {
                "cf_hit_siri_L", new BodyBehavior
                {
                    part = Body.Groin,
                    react = AibuColliderKind.reac_bodydown,
                    touch = AibuColliderKind.siriL
                }
            },
            {
                "cf_hit_siri_R", new BodyBehavior
                {
                    part = Body.Groin,
                    react = AibuColliderKind.reac_bodydown,
                    touch = AibuColliderKind.siriR
                }
            },
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
            {
                "cf_hit_thigh02_L", new BodyBehavior
                {
                    part = Body.LegL,
                    react = AibuColliderKind.reac_legL
                }
            },
            {
                "cf_hit_leg01_L", new BodyBehavior
                {
                    part = Body.LegL,
                    react = AibuColliderKind.reac_legL
                }
            },
            {
                "aibu_reaction_legL", new BodyBehavior
                {
                    part = Body.LegL,
                    react = AibuColliderKind.reac_legL
                }
            },
            {
                "cf_hit_thigh02_R", new BodyBehavior
                {
                    part = Body.LegR,
                    react = AibuColliderKind.reac_legR
                }
            },
            {
                "cf_hit_leg01_R", new BodyBehavior
                {
                    part = Body.LegR,
                    react = AibuColliderKind.reac_legR
                }
            },
            {
                "aibu_reaction_legR", new BodyBehavior
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
