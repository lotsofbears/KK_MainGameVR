using Illusion.Component.Correct;
using KK_VR.Fixes;
using KK_VR.Handlers;
using KK_VR.Interpreters;
using KK_VR.Settings;
using KK_VR.Trackers;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using VRGIN.Core;
using static KK_VR.Interactors.GraspController;
using static KK_VR.Interactors.HitReaction;

namespace KK_VR.Interactors
{
    internal class GraspHelper : MonoBehaviour
    {
        internal static GraspHelper Instance => _instance;
        private static GraspHelper _instance;
        private bool _transition;
        private bool _animChange;
        private bool _handChange;
        private readonly List<OffsetPlay> _transitionList = [];
        private readonly Dictionary<ChaControl, string> _animChangeDic = [];
        private static Dictionary<ChaControl, List<BodyPart>> _bodyPartsDic;
        private readonly List<HandScroll> _handScrollList = [];
        private bool _baseHold;
        private readonly List<BaseHold> _baseHoldList = [];
        private static readonly List<OrigOrient> _origOrientList = [];

        private class OrigOrient
        {
            internal OrigOrient(ChaControl chara)
            {
                _chara = chara.transform;
                _position = _chara.position;
                _rotation = _chara.rotation;
            }
            private readonly Transform _chara;
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;

            internal void Restore() => _chara.SetPositionAndRotation(_position, _rotation);
        }
        internal void Init(IEnumerable<ChaControl> charas, Dictionary<ChaControl, List<BodyPart>> bodyPartsDic)
        {
            _instance = this;
            _bodyPartsDic = bodyPartsDic;
            foreach (var chara in charas)
            {
                AddChara(chara);
                _origOrientList.Add(new(chara));
            }
        }

        private void AddChara(ChaControl chara)
        {
            var ik = chara.objAnim.GetComponent<FullBodyBipedIK>();
            if (ik == null) return;
            _bodyPartsDic.Add(chara,
            [
                new(
                    _name:       PartName.Body,
                    _effector:   ik.solver.bodyEffector,
                    _origTarget: ik.solver.bodyEffector.target,
                    _targetBD:   ik.solver.bodyEffector.target.GetComponent<BaseData>(),
                    _chain:      ik.solver.chain[0]
                    ),

                new(
                    _name:       PartName.LeftShoulder,
                    _effector:   ik.solver.leftShoulderEffector,
                    _origTarget: ik.solver.leftShoulderEffector.target,
                    _targetBD:   ik.solver.leftShoulderEffector.target.GetComponent<BaseData>()
                    ),

                new(
                    _name:       PartName.RightShoulder,
                    _effector:   ik.solver.rightShoulderEffector,
                    _origTarget: ik.solver.rightShoulderEffector.target,
                    _targetBD:   ik.solver.rightShoulderEffector.target.GetComponent<BaseData>()
                    ),

                new(
                    _name:       PartName.LeftThigh,
                    _effector:   ik.solver.leftThighEffector,
                    _origTarget: ik.solver.leftThighEffector.target,
                    _targetBD:   ik.solver.leftThighEffector.target.GetComponent<BaseData>()
                    ),

                new(
                    _name:       PartName.RightThigh,
                    _effector:   ik.solver.rightThighEffector,
                    _origTarget: ik.solver.rightThighEffector.target,
                    _targetBD:   ik.solver.rightThighEffector.target.GetComponent<BaseData>()
                    ),

                new(
                    _name:       PartName.LeftHand,
                    _effector:   ik.solver.leftHandEffector,
                    _origTarget: ik.solver.leftHandEffector.target,
                    _targetBD:   ik.solver.leftHandEffector.target.GetComponent<BaseData>(),
                    _chain:      ik.solver.leftArmChain
                    ),

                new(
                    _name:       PartName.RightHand,
                    _effector:   ik.solver.rightHandEffector,
                    _origTarget: ik.solver.rightHandEffector.target,
                    _targetBD:   ik.solver.rightHandEffector.target.GetComponent<BaseData>(),
                    _chain:      ik.solver.rightArmChain
                    ),

                new(
                    _name:       PartName.LeftFoot,
                    _effector:   ik.solver.leftFootEffector,
                    _origTarget: ik.solver.leftFootEffector.target,
                    _targetBD:   ik.solver.leftFootEffector.target.GetComponent<BaseData>(),
                    _chain:      ik.solver.leftLegChain
                    ),

                new(
                    _name:       PartName.RightFoot,
                    _effector:   ik.solver.rightFootEffector,
                    _origTarget: ik.solver.rightFootEffector.target,
                    _targetBD:   ik.solver.rightFootEffector.target.GetComponent<BaseData>(),
                    _chain:      ik.solver.rightLegChain
                    ),
            ]);
            AddExtraColliders(chara);
            foreach (var bodyPart in _bodyPartsDic[chara])
            {
                if (KoikatuInterpreter.settings.DebugShowIK)
                {
                    Util.CreatePrimitive(PrimitiveType.Sphere, new Vector3(0.12f, 0.12f, 0.12f), bodyPart.beforeIK, Color.blue, 0.4f);
                    Util.CreatePrimitive(PrimitiveType.Sphere, new Vector3(0.12f, 0.12f, 0.12f), bodyPart.afterIK, Color.yellow, 0.4f);
                }
                bodyPart.guide = bodyPart.visual.gameObject.AddComponent<BodyPartGuide>();
                bodyPart.guide.Init(bodyPart, chara);
                if (bodyPart.name > PartName.RightThigh)
                {
                    bodyPart.colliders = FindColliders(chara, bodyPart.name);
                }
                else
                {
                    bodyPart.colliders = [];
                }

            }
            SetWorkingState(chara);
        }
        //for (var i = 5; i < 9; i++)
        //{
        //    var bodyPart = _bodyPartsDic[chara][i];
        //    var holder = new GameObject(bodyPart.name + "Handler").transform;
        //    //holder.SetParent(chara.transform, false);
        //    //bodyPart.handler = holder.gameObject.AddComponent<BodyPartHandler>();
        //    bodyPart.handler.Init(bodyPart);

        //}

        internal void OnSpotChangePre()
        {
            foreach (var orient in _origOrientList)
            {
                orient.Restore();
            }
            _origOrientList.Clear();
        }
        internal void OnSpotChangePost()
        {
            VRPlugin.Logger.LogDebug($"Helper:Grasp:OnSpotChange");
            foreach (var kv in _bodyPartsDic)
            {
                _origOrientList.Add(new(kv.Key));
            }
        }
        private readonly List<string> _extraColliders =
        [
            "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_L/cf_j_leg01_L/cf_j_leg03_L/cf_j_foot_L/cf_hit_leg02_L",
            "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_R/cf_j_leg01_R/cf_j_leg03_R/cf_j_foot_R/cf_hit_leg02_R",
        ];

        private void AddFeetCollider(Transform bone)
        {
            var collider = bone.gameObject.AddComponent<CapsuleCollider>();
            collider.radius = 0.1f;
            collider.height = 0.5f;
            collider.direction = 2;
            bone.localPosition = new Vector3(bone.localPosition.x, 0f, 0.06f);
        }
        private void AddExtraColliders(ChaControl chara)
        {
            foreach (var path in _extraColliders)
            {
                AddFeetCollider(chara.objBodyBone.transform.Find(path));
            }
        }
        private Dictionary<Collider, bool> FindColliders(ChaControl chara, PartName partName)
        {
            var dic = new Dictionary<Collider, bool>();
            foreach (var str in _limbColliders[partName])
            {
                var collider = chara.objBodyBone.transform.Find(str).GetComponent<Collider>();
                if (collider != null)
                {
                    dic.Add(collider, collider.isTrigger);
                }
            }
            return dic;
        }

        internal static void SetWorkingState(ChaControl chara)
        {
            // By default only limbs are used, the rest is limited to offset play by hitReaction.
            VRPlugin.Logger.LogDebug($"Helper:Grasp:SetWorkingState:{chara}");
            if (_bodyPartsDic != null && _bodyPartsDic.ContainsKey(chara))
            {
                foreach (var bodyPart in _bodyPartsDic[chara])
                {
                    if (!bodyPart.IsLimb())
                        bodyPart.targetBaseData.bone = bodyPart.effector.bone;
                    bodyPart.effector.target = bodyPart.anchor;
                    if (bodyPart.chain == null) continue;
                    bodyPart.chain.bendConstraint.weight = bodyPart.state == State.Default ? 1f : 0f;
                }
            }
        }

        internal static void SetDefaultState(ChaControl chara, string stateName)
        {
            VRPlugin.Logger.LogDebug($"Helper:Grasp:SetDefaultState:{chara}");
            if (_bodyPartsDic != null && _bodyPartsDic.ContainsKey(chara))
            {
                if (stateName != null && chara.objTop.activeSelf && chara.visibleAll)
                {
                    _instance.StartAnimChange(chara, stateName);
                }
                foreach (var bodyPart in _bodyPartsDic[chara])
                {
                    bodyPart.effector.target = bodyPart.beforeIK;
                    if (bodyPart.chain != null)
                    {
                        bodyPart.chain.bendConstraint.weight = 1f;
                    }
                }
            }

        }
        private void StartAnimChange(ChaControl chara, string stateName)
        {
            VRPlugin.Logger.LogDebug($"Helper:Grasp:StartAnimChange:{chara}");
            for (var i = 5; i < 7; i++)
            {
                var bodyPart = _bodyPartsDic[chara][i];
                if (bodyPart.state == State.Active)
                {
                    //var parent = GetParent(bodyPart.name);
                    //VRPlugin.Logger.LogDebug($"AnimChange:Add:{bodyPart.name} -> {parent} -> {_bodyPartsDic[chara][(int)parent].origTarget}");
                    if (!_animChangeDic.ContainsKey(chara))
                    {
                        _animChangeDic.Add(chara, stateName);
                        _animChange = true;
                    }
                    bodyPart.anchor.parent = _bodyPartsDic[chara][(int)GetParent(bodyPart.name)].anchor;
                }
            }
        }
        private PartName GetParent(PartName partName)
        {
            return partName switch
            {
                PartName.LeftHand => PartName.LeftShoulder,
                PartName.RightHand => PartName.RightShoulder,
                PartName.LeftFoot => PartName.LeftThigh,
                PartName.RightFoot => PartName.RightThigh,
                _ => PartName.Body
            };
        }
        private void DoAnimChange()
        {
            foreach (var kv in _animChangeDic)
            {
                VRPlugin.Logger.LogDebug($"AnimChangeWait:{kv.Key}:{kv.Value}");
                if (kv.Key.animBody.GetCurrentAnimatorStateInfo(0).IsName(kv.Value))
                {
                    OnAnimChangeEnd(kv.Key);
                    return;
                }
            }
        }
        //internal void AddAttach(BodyPart bodyPart, Transform attachPoint)
        //{
        //    _attach = true;
        //    _attachedList.Add(new BodyPartOffset(bodyPart, attachPoint));
        //}
        //internal void RemoveAttach(BodyPartOffset offset)
        //{
        //    if (_attachedList.Remove(offset) && _attachedList.Count == 0)
        //    {
        //        _attach = false;
        //    }
        //}
        //internal void RemoveAttach(BodyPart bodyPart)
        //{
        //    if (_attachedList.Count != 0)
        //    {
        //        var offset = _attachedList.Where(o => o.bodyPart == bodyPart).FirstOrDefault();
        //        if (offset != null)
        //        {
        //            _attachedList.Remove(offset);
        //            if (_attachedList.Count == 0)
        //            {
        //                _attach = false;
        //            }
        //        }
        //    }
        //}
        internal void ScrollHand(PartName partName, ChaControl chara, bool increase)
        {
            _handChange = true;
            _handScrollList.Add(new HandScroll(partName, chara, increase));
        }
        internal void StopScroll()
        {
            _handChange = false;
            _handScrollList.Clear();
        }
        internal void OnPoseChange()
        {
            VRPlugin.Logger.LogDebug($"Helper:Grasp:OnPoseChange");
            StopTransition();
            StopAnimChange();
            foreach (var orig in _origOrientList)
            {
                orig.Restore();
            }
            foreach (var bodyPartList in _bodyPartsDic.Values)
            {
                foreach (var bodyPart in bodyPartList)
                {
                    bodyPart.Reset();
                }
            }

        }
        private void Update()
        {
            if (_baseHold) DoBaseHold();
            if (_transition) DoTransition();
            if (_animChange) DoAnimChange();
            if (_handChange) DoHandChange();
        }
        private void OnAnimChangeEnd(ChaControl chara)
        {
            VRPlugin.Logger.LogDebug($"Helper:Grasp:OnAnimChangeEnd");
            for (var i = 5; i < 7; i++)
            {
                var bodyPart = _bodyPartsDic[chara][i];
                if (bodyPart.state == State.Active)
                {
                    bodyPart.anchor.SetParent(bodyPart.beforeIK, worldPositionStays: true);
                }
            }
            _animChangeDic.Remove(chara);
            _animChange = _animChangeDic.Count != 0;
        }

        internal void StartTransition(BodyPart bodyPart)
        {
            VRPlugin.Logger.LogDebug($"Transition:Add{bodyPart.name}");
            _transitionList.Add(new OffsetPlay(bodyPart, bodyPart.anchor.localPosition, bodyPart.anchor.localRotation, bodyPart.chain != null));
            _transition = true;
        }
        private void DoTransition()
        {
            for (var i = 0; i < _transitionList.Count; i++)
            {
                var offsetPlay = _transitionList[i];
                offsetPlay.current += Time.deltaTime * offsetPlay.coef;
                if (offsetPlay.constrain)
                    offsetPlay.bodyPart.chain.bendConstraint.weight = offsetPlay.current;
                offsetPlay.bodyPart.anchor.localRotation = Quaternion.Lerp(offsetPlay.offsetRot, Quaternion.identity, offsetPlay.current);
                offsetPlay.bodyPart.anchor.localPosition = Vector3.Lerp(offsetPlay.offsetPos, Vector3.zero, offsetPlay.current);
                if (offsetPlay.current >= 1f)
                {
                    offsetPlay.current = 0f;
                    offsetPlay.bodyPart.state = State.Default;
                    _transitionList.RemoveAt(i);
                    VRPlugin.Logger.LogDebug($"Transition:Finish:{_transitionList.Count}");
                    if (_transitionList.Count == 0)
                    {
                        _transition = false;
                        return;
                    }
                    i--;
                }
            }
        }

        internal class BaseHold
        {
            internal BaseHold(BodyPart _bodyPart, ChaControl _chara, Transform _attachPoint)
            {
                bodyPart = _bodyPart;
                chara = _chara;
                attachPoint = _attachPoint;
                offsetPos = _attachPoint.InverseTransformDirection(_chara.transform.position - _attachPoint.position);
                offsetRot = Quaternion.Inverse(_attachPoint.rotation) * _chara.transform.rotation;
            }
            internal BodyPart bodyPart;
            internal ChaControl chara;
            internal Transform attachPoint;
            internal Quaternion offsetRot;
            internal Vector3 offsetPos;
            internal int scrollDir;
            internal bool scrollInc;
        }
        internal BaseHold StartBaseHold(BodyPart bodyPart, ChaControl chara, Transform attachPoint)
        {
            _baseHold = true;
            var baseHold = new BaseHold(bodyPart, chara, attachPoint);
            _baseHoldList.Add(baseHold);
            return baseHold;
        }
        private void DoBaseHold()
        {
            foreach (var hold in _baseHoldList)
            {
                if (hold.scrollDir != 0)
                {
                    if (hold.scrollDir == 1)
                    {
                        DoBaseHoldVerticalScroll(hold, hold.scrollInc);
                    }
                    else
                    {
                        DoBaseHoldHorizontalScroll(hold, hold.scrollInc);
                    }
                }
                hold.chara.transform.SetPositionAndRotation(
                    hold.attachPoint.position + hold.attachPoint.TransformDirection(hold.offsetPos),
                    hold.attachPoint.rotation * hold.offsetRot
                    );
            }
        }
        internal void StopBaseHold(BaseHold baseHold)
        {
            _baseHoldList.Remove(baseHold);
            if (_baseHoldList.Count == 0)
            {
                _baseHold = false;
            }
        }
        private void DoHandChange()
        {
            foreach (var scroll in _handScrollList)
            {
                scroll.Scroll();
            }
        }
        internal void StartBaseHoldScroll(BaseHold baseHold, int direction, bool increase)
        {
            baseHold.scrollDir = direction;
            baseHold.scrollInc = increase;
        }
        internal void StopBaseHoldScroll(BaseHold baseHold)
        {
            baseHold.scrollDir = 0;
        }
        private void DoBaseHoldVerticalScroll(BaseHold baseHold, bool increase)
        {
            baseHold.offsetPos += VR.Camera.Head.forward * (Time.deltaTime * (increase ? 10f : -10f));
        }
        private Quaternion _left = Quaternion.Euler(0f, 1f, 0f);
        private Quaternion _right = Quaternion.Euler(0f, -1f, 0f);
        private void DoBaseHoldHorizontalScroll(BaseHold baseHold, bool left)
        {
            baseHold.offsetRot *= (left ? _left : _right);
        }
        internal void StopTransition(BodyPart bodyPart)
        {
            VRPlugin.Logger.LogDebug($"Transition:Stop{_transitionList.Count}");
            _transitionList.Remove(_transitionList
                .Where(o => o.bodyPart == bodyPart)
                .First());
            bodyPart.state = State.Active;
            if (_transitionList.Count == 0)
                _transition = false;
        }
        private void StopTransition()
        {
            _transition = false;
            _transitionList.Clear();
        }

        private void StopAnimChange()
        {
            _animChange = false;
            _animChangeDic.Clear();
        }
        //internal class BodyPartOffset
        //{
        //    internal BodyPartOffset(BodyPart _bodyPart, Transform _target)
        //    {
        //        bodyPart = _bodyPart;
        //        target = _target;
        //        offset = _target.InverseTransformPoint(_bodyPart.anchor.position);
        //    }
        //    internal BodyPart bodyPart;
        //    internal Transform target;
        //    internal Vector3 offset;
        //}
        private static readonly Dictionary<PartName, List<string>> _limbColliders = new()
        {
            {
                PartName.LeftHand, new List<string>()
                {
                    "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_L/cf_j_shoulder_L/" +
                    "cf_j_arm00_L/cf_j_forearm01_L/cf_d_forearm02_L/cf_s_forearm02_L/cf_hit_wrist_L",

                    "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_L/cf_j_shoulder_L/cf_j_arm00_L/cf_j_forearm01_L/cf_j_hand_L/com_hit_hand_L",
                }
            },
            {
                PartName.RightHand, new List<string>()
                {
                    "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_R/cf_j_shoulder_R/" +
                    "cf_j_arm00_R/cf_j_forearm01_R/cf_d_forearm02_R/cf_s_forearm02_R/cf_hit_wrist_R",

                    "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_R/cf_j_shoulder_R/cf_j_arm00_R/cf_j_forearm01_R/cf_j_hand_R/com_hit_hand_R",
                }
            },
            {
                PartName.LeftFoot, new List<string>()
                {
                    "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_L/cf_j_leg01_L/cf_s_leg01_L/cf_hit_leg01_L/aibu_reaction_legL",
                    "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_L/cf_j_leg01_L/cf_j_leg03_L/cf_j_foot_L/cf_hit_leg02_L",
                }
            },
            {
                PartName.RightFoot, new List<string>()
                {
                    "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_R/cf_j_leg01_R/cf_s_leg01_R/cf_hit_leg01_R/aibu_reaction_legR",
                    "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_R/cf_j_leg01_R/cf_j_leg03_R/cf_j_foot_R/cf_hit_leg02_R",
                }
            }
        };
        //private static readonly Dictionary<PartName, List<ColliderParam>> _limbColliders = new Dictionary<PartName, List<ColliderParam>>()
        //{
        //    {
        //        PartName.LeftHand, new List<ColliderParam>()
        //        {
        //            new ColliderParam
        //            {
        //                path = "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_L/cf_j_shoulder_L/" +
        //                "cf_j_arm00_L/cf_j_forearm01_L/cf_d_forearm02_L/cf_s_forearm02_L/cf_hit_wrist_L",
        //                normalHeight = 0.24f,
        //                activeHeight = 0.15f
        //            },
        //            new ColliderParam
        //            {
        //                path = "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_L/cf_j_shoulder_L/cf_j_arm00_L/cf_j_forearm01_L/cf_j_hand_L/com_hit_hand_L",
        //                activeHeight = 0f
        //            }

        //        }
        //    },
        //    {
        //        PartName.RightHand, new List<ColliderParam>()
        //        {
        //            new ColliderParam
        //            {
        //                path = "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_R/cf_j_shoulder_R/" +
        //                "cf_j_arm00_R/cf_j_forearm01_R/cf_d_forearm02_R/cf_s_forearm02_R/cf_hit_wrist_R",
        //                normalHeight = 0.24f,
        //                activeHeight = 0.15f
        //            },
        //            new ColliderParam
        //            {
        //                path = "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_R/cf_j_shoulder_R/cf_j_arm00_R/cf_j_forearm01_R/cf_j_hand_R/com_hit_hand_R",
        //                activeHeight = 0f
        //            }

        //        }
        //    },
        //    {
        //        PartName.LeftFoot, new List<ColliderParam>()
        //        {
        //            new ColliderParam
        //            {
        //                path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_L/cf_j_leg01_L/cf_s_leg01_L/cf_hit_leg01_L/aibu_reaction_legL",
        //                normalHeight = 0.4f,
        //                activeHeight = 0.35f
        //            },
        //            new ColliderParam
        //            {
        //                path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_L/cf_j_leg01_L/cf_j_leg03_L/cf_j_foot_L/cf_hit_leg02_L",
        //                activeHeight = 0f
        //            }

        //        }
        //    },
        //    {
        //        PartName.RightFoot, new List<ColliderParam>()
        //        {
        //            new ColliderParam
        //            {
        //                path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_R/cf_j_leg01_R/cf_s_leg01_R/cf_hit_leg01_R/aibu_reaction_legR",
        //                normalHeight = 0.4f,
        //                activeHeight = 0.35f
        //            },
        //            new ColliderParam
        //            {
        //                path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_R/cf_j_leg01_R/cf_j_leg03_R/cf_j_foot_R/cf_hit_leg02_R",
        //                activeHeight = 0f
        //            }

        //        }
        //    }
        //};
    }
}
