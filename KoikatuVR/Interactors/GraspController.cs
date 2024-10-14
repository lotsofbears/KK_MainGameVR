using ActionGame.Chara;
using ADV.Commands.Object;
using HarmonyLib;
using Illusion.Component.Correct;
using Illusion.Game.Extensions;
using IllusionUtility.GetUtility;
using KK_VR.Features;
using KK_VR.Handlers;
using KK_VR.Interpreters;
using KK_VR.Settings;
using KK_VR.Trackers;
using KKAPI.Utilities;
using MessagePack;
using NodeCanvas.Tasks.Actions;
using RootMotion.FinalIK;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using UniRx;
using Unity.Linq;
using UnityEngine;
using UnityEngine.Assertions.Must;
using VRGIN.Controls;
using VRGIN.Core;
using static ActionGame.FixEventScheduler;
using static HFlag;
using static KK_VR.Interactors.GraspController;
using static UnityEngine.UI.Image;

namespace KK_VR.Interactors
{
    // Named Grasp so there is less confusion with GrabMove. 
    internal class GraspController
    {
        private HandHolder _hand;
        private static GraspHelper _helper;
        private GraspVisualizer _visual;
        private KoikatuSettings _settings => VR.Context.Settings as KoikatuSettings;
        private static readonly Dictionary<ChaControl, List<BodyPart>> _bodyPartsDic = new Dictionary<ChaControl, List<BodyPart>>();

        private static readonly IDictionary<ChaControl, List<Tracker.Body>> _blackListDic = new Dictionary<ChaControl, List<Tracker.Body>>();
        private static readonly List<List<PartName>> _jointGroupList = new List<List<PartName>>()
        {
            new List<PartName> { PartName.LeftShoulder, PartName.RightShoulder },
            new List<PartName> { PartName.LeftThigh, PartName.RightThigh }
        };

        private ChaControl _heldChara;
        private ChaControl _syncedChara;

        private static readonly List<BodyPart> _attachedBodyParts = new List<BodyPart>();
        //private readonly Dictionary<ChaControl, string> _animChangeDic = new Dictionary<ChaControl, string>();
        // private readonly Dictionary<BodyPart, List<bool>> _disabledCollidersDic = new Dictionary<BodyPart, List<bool>>();
        // For Grip.
        private readonly List<BodyPart> _heldBodyParts = new List<BodyPart>();
        // For Trigger conditional long press. 
        private readonly List<BodyPart> _tempHeldBodyParts = new List<BodyPart>();
        // For Touchpad.
        private readonly List<BodyPart> _syncedBodyParts = new List<BodyPart>();

        private static readonly List<Vector3> _limbPosOffsets = new List<Vector3>()
        {
            new Vector3(-0.005f, 0.015f, -0.04f),
            new Vector3(0.005f, 0.015f, -0.04f),
            Vector3.zero,
            Vector3.zero
        };
        private static readonly List<Quaternion> _limbRotOffsets = new List<Quaternion>()
        {
            Quaternion.Euler(0f, 90f, 0f),
            Quaternion.Euler(0f, -90f, 0f),
            Quaternion.identity,
            Quaternion.identity
        };
        
        internal class ColliderParam
        {
            internal Collider collider;
            internal string path;

            // Collider.height's value, 0 for disabled state.
            internal float activeHeight;
            internal float normalHeight;
        }

        // Add held items too once implemented. All bodyParts have black list entries, dic is sufficient.
        internal bool IsBusy => _blackListDic.Count != 0;
        internal static IDictionary<ChaControl, List<Tracker.Body>> GetBlacklistDic => _blackListDic;
        internal List<BodyPart> GetFullBodyPartList(ChaControl chara) => _bodyPartsDic[chara];
        internal enum State
        {
            Default,     // Follows animation, no offsets, no rigidBodies.
            Transition,  // Is being returned to default/??? state.
            Active,      // Has offset and rigidBody(for Limbs) or specialHandler(for Joints/Head. Not implemented). 
            Grasped,     // Is being held.
            Synced,      // Follows some weird transform, rigidBody disabled. For now only limbs, later joints/head.
            Attached,    // 
            //Grounded     // Not implemented. Is attached to floor/some map item collider. 
        }

        internal class BodyPart
        {
            internal PartName name;
            internal Transform anchor;
            internal IKEffector effector;
            internal FBIKChain chain;
            internal Transform origTarget;
            internal BaseData targetBaseData;
            internal State state;
            internal List<ColliderParam> colliderParams;
            internal LimbHandler handler;
            internal Vector3 offset;
            internal Transform attachTarget;

            internal BodyPart(PartName _name, IKEffector _effector, Transform _origTarget,
                BaseData _targetBD, FBIKChain _chain = null, IKEffector _parentJointEffector = null,
                Transform _parentJointAnimPos = null)
            {
                name = _name;
                effector = _effector;
                origTarget = _origTarget;
                targetBaseData = _targetBD;
                chain = _chain;
            }
        }
        internal class OffsetPlay
        {
            internal OffsetPlay(BodyPart _bodyPart, Vector3 _offsetVec, Quaternion _offsetRot, bool _constrain)
            {
                bodyPart = _bodyPart;
                offsetPos = _offsetVec;
                offsetRot = _offsetRot;
                constrain = _constrain;
                coef = 0.35f / _offsetVec.magnitude;
                _bodyPart.state = State.Transition;
            }
            internal BodyPart bodyPart;
            internal Vector3 offsetPos;
            internal Quaternion offsetRot;
            internal float coef;
            internal float current;
            internal bool constrain;
        }
        public enum PartName
        {
            Body,
            LeftShoulder,
            RightShoulder,
            LeftThigh,
            RightThigh,
            LeftHand,
            RightHand,
            LeftFoot,
            RightFoot,
            UpperBody,
            LowerBody,
            Everything
        }
        internal GraspController(HandHolder hand)
        {
            _hand = hand;
            _visual = GraspVisualizer.Instance;
        }
        internal static void Init(IEnumerable<ChaControl> charas)
        {
            _bodyPartsDic.Clear();
            _blackListDic.Clear();
            if (_helper == null)
            {
                _helper = charas.First().gameObject.AddComponent<GraspHelper>();
                _helper.Init(_bodyPartsDic, _attachedBodyParts);
            }
            foreach (var chara in charas)
            {
                AddChara(chara);
            }
            GraspVisualizer.Init(_bodyPartsDic);
        }
        private static void AddChara(ChaControl chara)
        {
            var ik = chara.objAnim.GetComponent<FullBodyBipedIK>();
            if (ik == null) return;
            _bodyPartsDic.Add(chara, new List<BodyPart>()
            {
                new BodyPart(
                    _name:       PartName.Body,
                    _effector:   ik.solver.bodyEffector,
                    _origTarget: ik.solver.bodyEffector.target,
                    _targetBD:   ik.solver.bodyEffector.target.GetComponent<BaseData>(),
                    _chain:      ik.solver.chain[0]
                    ),

                new BodyPart(
                    _name:       PartName.LeftShoulder,
                    _effector:   ik.solver.leftShoulderEffector,
                    _origTarget: ik.solver.leftShoulderEffector.target,
                    _targetBD:   ik.solver.leftShoulderEffector.target.GetComponent<BaseData>()
                    ),

                new BodyPart(
                    _name:       PartName.RightShoulder,
                    _effector:   ik.solver.rightShoulderEffector,
                    _origTarget: ik.solver.rightShoulderEffector.target,
                    _targetBD:   ik.solver.rightShoulderEffector.target.GetComponent<BaseData>()
                    ),

                new BodyPart(
                    _name:       PartName.LeftThigh,
                    _effector:   ik.solver.leftThighEffector,
                    _origTarget: ik.solver.leftThighEffector.target,
                    _targetBD:   ik.solver.leftThighEffector.target.GetComponent<BaseData>()
                    ),

                new BodyPart(
                    _name:       PartName.RightThigh,
                    _effector:   ik.solver.rightThighEffector,
                    _origTarget: ik.solver.rightThighEffector.target,
                    _targetBD:   ik.solver.rightThighEffector.target.GetComponent<BaseData>()
                    ),

                new BodyPart(
                    _name:       PartName.LeftHand,
                    _effector:   ik.solver.leftHandEffector,
                    _origTarget: ik.solver.leftHandEffector.target,
                    _targetBD:   ik.solver.leftHandEffector.target.GetComponent<BaseData>(),
                    _chain:      ik.solver.leftArmChain
                    ),

                new BodyPart(
                    _name:       PartName.RightHand,
                    _effector:   ik.solver.rightHandEffector,
                    _origTarget: ik.solver.rightHandEffector.target,
                    _targetBD:   ik.solver.rightHandEffector.target.GetComponent<BaseData>(),
                    _chain:      ik.solver.rightArmChain
                    ),

                new BodyPart(
                    _name:       PartName.LeftFoot,
                    _effector:   ik.solver.leftFootEffector,
                    _origTarget: ik.solver.leftFootEffector.target,
                    _targetBD:   ik.solver.leftFootEffector.target.GetComponent<BaseData>(),
                    _chain:      ik.solver.leftLegChain
                    ),

                new BodyPart(
                    _name:       PartName.RightFoot,
                    _effector:   ik.solver.rightFootEffector,
                    _origTarget: ik.solver.rightFootEffector.target,
                    _targetBD:   ik.solver.rightFootEffector.target.GetComponent<BaseData>(),
                    _chain:      ik.solver.rightLegChain
                    ),
            });
            foreach (var bodyPart in _bodyPartsDic[chara])
            {
                bodyPart.anchor = new GameObject(bodyPart.name + "Anchor").transform;
                bodyPart.anchor.SetParent(bodyPart.origTarget, worldPositionStays: false);
            }
            GraspHelper.SetWorkingState(chara);
            AddExtraColliders(chara);
            for (var i = 5; i < 9; i++)
            {
                var bodyPart = _bodyPartsDic[chara][i];
                var holder = new GameObject(bodyPart.name + "Handler").transform;
                holder.SetParent(chara.transform, false);
                bodyPart.handler = holder.gameObject.AddComponent<LimbHandler>();
                bodyPart.handler.Init(i, bodyPart.origTarget, chara);
                bodyPart.colliderParams = FindColliders(chara, bodyPart.name);

            }
        }

        private static readonly List<string> _extraColliders = new List<string>()
        {
            "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_L/cf_j_leg01_L/cf_j_leg03_L/cf_j_foot_L/cf_hit_leg02_L",
            "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_R/cf_j_leg01_R/cf_j_leg03_R/cf_j_foot_R/cf_hit_leg02_R",
        };

        private static void AddFeetCollider(Transform bone)
        {
            var collider = bone.gameObject.AddComponent<CapsuleCollider>();
            collider.radius = 0.1f;
            collider.height = 0.5f;
            collider.direction = 2;
            bone.localPosition = new Vector3(bone.localPosition.x, 0f, 0.06f);
        }
        private static void AddExtraColliders(ChaControl chara)
        {
            foreach (var path in _extraColliders)
            {
                AddFeetCollider(chara.objBodyBone.transform.Find(path));
            }
        }
        private static List<ColliderParam> FindColliders(ChaControl chara, PartName partName)
        {
            var list = new List<ColliderParam>();
            foreach (var param in _limbColliders[partName])
            {
                var col = chara.objBodyBone.transform.Find(param.path).GetComponent<Collider>();
                if (col != null)
                {
                    list.Add(new ColliderParam
                    {
                        collider = col,
                        activeHeight = param.activeHeight,
                        normalHeight = param.normalHeight
                    });
                }
            }
            return list;
        }
        private void StopGrasp()
        {
            if (_visual.Active)
                SetGuideObjects(false, _heldBodyParts);

            _heldBodyParts.Clear();
            if (_heldChara != null)
            {
                _blackListDic.Remove(_heldChara);
                _heldChara = null;
                if (_tempHeldBodyParts.Count != 0)
                {
                    _tempHeldBodyParts.Clear();
                }
                UpdateBlackList();
            }
        }

        private void UpdateGrasp(BodyPart bodyPart)
        {
            //if (bodyPart == null)
            //{
            //    if (_visual.Active) 
            //        SetGuideObjects(false, _heldBodyParts);

            //    _heldBodyParts.Clear();
            //    if (_heldChara != null)
            //    {
            //        _blackListDic.Remove(_heldChara);
            //        _heldChara = null;
            //        if (_tempHeldBodyParts.Count != 0)
            //        {
            //            _tempHeldBodyParts.Clear();
            //        }
            //        UpdateBlackList();
            //    }
            //}
            //else
            {
                _heldBodyParts.Add(bodyPart);
                UpdateBlackList();
                if (_settings.AutoShowGuideObjects) 
                    SetGuideObjects(true, _heldBodyParts);
            }
        }
        private void UpdateGrasp(IEnumerable<BodyPart> bodyPart)
        {
            //if (bodyPart == null)
            //{
            //    if (_visual.Active)
            //        SetGuideObjects(false, _heldBodyParts);

            //    _heldBodyParts.Clear();
            //    if (_heldChara != null)
            //    {
            //        _blackListDic.Remove(_heldChara);
            //        _heldChara = null;
            //        if (_tempHeldBodyParts.Count != 0)
            //        {
            //            _tempHeldBodyParts.Clear();
            //        }
            //        UpdateBlackList();
            //    }
            //}
            //else
            {
                _heldBodyParts.AddRange(bodyPart);
                UpdateBlackList();
                if (_settings.AutoShowGuideObjects)
                    SetGuideObjects(true, _heldBodyParts);
            }
        }
        private void UpdateTempGrasp(BodyPart bodyPart)
        {
            //if (bodyPart == null)
            //{
            //    if (_visual.Active) 
            //        SetGuideObjects(false, _tempHeldBodyParts);

            //    _tempHeldBodyParts.Clear();
            //    UpdateBlackList();
            //}
            //else
            {
                _tempHeldBodyParts.Add(bodyPart);
                UpdateBlackList();
                if (_settings.AutoShowGuideObjects)
                    SetGuideObjects(true, _tempHeldBodyParts);
            }
        }
        private void StopTempGrasp()
        {
            {
                if (_visual.Active)
                    SetGuideObjects(false, _tempHeldBodyParts);

                _tempHeldBodyParts.Clear();
                UpdateBlackList();
            }
        }
        private void UpdateSync(BodyPart bodyPart)
        {
            //if (bodyPart == null)
            //{
            //    _syncedBodyParts.Clear();
            //    if (_syncedChara != null)
            //    {
            //        _blackListDic.Remove(_syncedChara);
            //        _syncedChara = null;
            //        UpdateBlackList();
            //    }
            //}
            //else
            {
                _syncedBodyParts.Add(bodyPart);
                UpdateBlackList();
            }
        }
        private void StopSync()
        {
            _syncedBodyParts.Clear();
            if (_syncedChara != null)
            {
                _blackListDic.Remove(_syncedChara);
                _syncedChara = null;
                UpdateBlackList();
            }
        }
        private PartName ConvertTrackerToIK(Tracker.Body part)
        {
            switch (part)
            {
                case Tracker.Body.ArmL:
                    return PartName.LeftShoulder;
                case Tracker.Body.ArmR:
                    return PartName.RightShoulder;

                case Tracker.Body.MuneL:
                case Tracker.Body.MuneR:
                    return PartName.UpperBody;
                case Tracker.Body.LowerBody:
                    return PartName.Body;

                case Tracker.Body.LegL:
                    return PartName.LeftFoot;
                case Tracker.Body.LegR:
                    return PartName.RightFoot;
                case Tracker.Body.ThighL:
                    return PartName.LeftThigh;
                case Tracker.Body.ThighR:
                    return PartName.RightThigh;

                case Tracker.Body.HandL:
                case Tracker.Body.ForearmL:
                    return PartName.LeftHand;

                case Tracker.Body.HandR:
                case Tracker.Body.ForearmR:
                    return PartName.RightHand;

                case Tracker.Body.Groin:
                case Tracker.Body.Asoko:
                    return PartName.LowerBody;

                default: // actual UpperBody
                    return PartName.Body;
            }
        }
        /*
         * Plan.
         * - Attach currently hooked BodyPart to collider after long Trigger (on release of Trigger)
         * - On Grip, flush + repurpose tracker, reparent handler to held BodyPart, 
         * enable big collider on handler, set BodyPart of that character to blacklist alongside our limb (if active).
         * If trigger pressed while BodyPart is being held with grip and tracker is busy, parent BodyPart to collider.
         * 
         * When grabbing body, remove targets from thighs, and put gravity driven rigidBodie + collider on each feet 
         * and autoTracker-attacher for floor (extra object if ever implemented?)
         * 
         * ToLookUp:
         *     Effector's positionOffset as means to work with underlying animation.
         *     Re: Doesn't work if we want effector to actually function.
         * 
         * 
         * ToResolve:
         *   - HitReaction works by default with anim, we need effectors. Repurpose for effector targets?
         *     kPlug implements something alike, lookUp.
         *     
         *     
         * IKPartsDefinitions:
         *     Limb - hand/foot
         *     Joint - shoulder/thigh
         *     Core - body
         *     
         * How we define what to grab.
         *     - if collider of a hand/forearm or foot/calf:
         *         * init grab   - we go for Limb,
         *         * add trigger -
         *         
         *     - if collider of thigh/upperArm or groin/upperChest(boobs actually, given how big colliders for them are)
         *         * init grab   - we go for Joint and corresponding Limb,
         *         * add trigger - we also add Core to it (its pair too?)
         *         
         *     - if body
         *         * init grab   - based on distance to, we also grab upper/lower joints and their limbs
         *         * add trigger - we grab everything
         *         
         * On joystick click -> reset + turn off (set back to orig target)        
         * 
         * Fix broken hitReaction with patch for HitReactionPlay of handCtrl and HitsEffector of type with same name. 
         * Catch AibuColliderKind that is about to play, and apply corresponding offset at next prefix if bodyPart is active.
         * 
         * How to handle head ?
         */
        private PartName GetChild(PartName parent)
        {
            // Shoulders/thighs found separately based on the distance.
            return parent switch
            {
                PartName.LeftThigh => PartName.LeftFoot,
                PartName.RightThigh => PartName.RightFoot,
                PartName.LeftShoulder => PartName.LeftHand,
                PartName.RightShoulder => PartName.RightHand,
                _ => parent
            };
        }

        private PartName FindJoints(List<BodyPart> lstBodyPart, Vector3 pos)
        {
            // Finds joint pair that was closer to the core and returns it as abnormal index for further processing.
            var list = new List<float>();
            foreach (var partNames in _jointGroupList)
            {
                // Avg distance to both joints
                list.Add(
                    (Vector3.Distance(lstBodyPart[(int)partNames[0]].effector.bone.position, pos)
                    + Vector3.Distance(lstBodyPart[(int)partNames[1]].effector.bone.position, pos))
                    * 0.5f);
            }
            return list[0] > list[1] ? PartName.LowerBody : PartName.UpperBody;
        }

        private List<PartName> FindJoint(List<BodyPart> lstBodyPart, List<PartName> partNames, Vector3 pos)
        {
            // Works with abnormal index, returns closer joint or both based on the distance.
            var a = Vector3.Distance(lstBodyPart[(int)partNames[0]].effector.bone.position, pos);
            var b = Vector3.Distance(lstBodyPart[(int)partNames[1]].effector.bone.position, pos);
            if ((a > b && a * 0.85f < b)
                || (a < b && a > b * 0.85f))
            {
                // Meaning they are approx equal.
                return partNames;
            }
            else
            {
                // Nope, they weren't.
                return new List<PartName>() { a < b ? partNames[0] : partNames[1] };
            }
        }

        /// <summary>
        /// Returns 1 .. 3 names that we should start interaction with.
        /// </summary>
        private List<BodyPart> GetTargetParts(List<BodyPart> lstBodyPart, PartName target, Vector3 pos)
        {
            // Finds PartName(s) that we should initially target. 
            var bodyPartList = new List<BodyPart>();
            if (target == PartName.Body)
            {
                bodyPartList.Add(lstBodyPart[(int)target]);
                target = FindJoints(lstBodyPart, pos);
            }
            // i.e. pair of joints
            if (target > PartName.RightFoot)
            {
                FindJoint(lstBodyPart, _jointGroupList[target == PartName.UpperBody ? 0 : 1], pos)
                    .ForEach(name => bodyPartList.Add(lstBodyPart[(int)name]));
            }
            else
            {
                bodyPartList.Add(lstBodyPart[(int)target]);
            }
            return bodyPartList;
        }
        /// <summary>
        /// Returns name of corresponding parent.
        /// </summary>
        private PartName GetParent(PartName childName)
        {
            return childName switch
            {
                PartName.Body => PartName.Everything,
                PartName.Everything => childName,
                PartName.LeftHand => PartName.LeftShoulder,
                PartName.RightHand => PartName.RightShoulder,
                PartName.LeftFoot => PartName.LeftThigh,
                PartName.RightFoot => PartName.RightThigh,
                // For shoulders/thighs  
                _ => PartName.Body
            };
        }
        internal bool OnTriggerPress(bool temporarily)
        {
            VRPlugin.Logger.LogDebug($"OnTriggerPress");

            // We look for a BodyPart from which grasp has started (0 index in _heldBodyParts),
            // and attach it to the collider's gameObjects.

            if (_heldChara != null)
            {
                var heldBodyParts = _heldBodyParts.Concat(_tempHeldBodyParts);
                var bodyPartsLimbs = heldBodyParts
                    .Where(b => b.name != PartName.Body && b.handler != null && b.handler.IsBusy);
                if (bodyPartsLimbs.Any())
                {
                    SetGuideObjects(false, heldBodyParts);
                    foreach (var bodyPart in bodyPartsLimbs)
                    {
                        VRPlugin.Logger.LogDebug($"OnTrigger:Attach:Grasped:{bodyPart.name} -> {bodyPart.handler.GetTrackTransform.name}");
                        AttachBodyPart(bodyPart, bodyPart.handler.GetTrackTransform);
                    }
                    ReleaseBodyParts(heldBodyParts);
                    StopGrasp();
                }
                else
                {
                    return OnTriggerExtendGrasp(temporarily);
                }
            }
            else if (_syncedChara != null)
            {
                var bodyParts = _syncedBodyParts
                    .Where(b => b.handler != null && b.handler.IsBusy);
                if (bodyParts.Any())
                {
                    foreach (var bodyPart in bodyParts)
                    {
                        VRPlugin.Logger.LogDebug($"OnTrigger:Attach:Synced:{bodyPart.name} -> {bodyPart.handler.GetTrackTransform.name}");
                        AttachBodyPart(bodyPart, bodyPart.handler.GetTrackTransform);
                    }
                    ReleaseBodyParts(bodyParts);
                    StopGrasp();
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        private bool OnTriggerExtendGrasp(bool temporarily)
        {
            // Attempts to grasp BodyPart(s) higher in hierarchy or everything if already top.
            var bodyPartList = _bodyPartsDic[_heldChara];
            var closestToCore = _heldBodyParts
                .OrderBy(bodyPart => bodyPart.name)
                .First().name;
            var parent = GetParent(closestToCore);
            VRPlugin.Logger.LogDebug($"OnTriggerExtendGrasp:Temporarily[{temporarily}]:{closestToCore} -> {parent}");

            var attachPoint = bodyPartList[(int)closestToCore].anchor;
            if (parent != PartName.Everything)
            {
                GraspBodyPart(bodyPartList[(int)parent], attachPoint);
                if (temporarily)
                    TrackOnGraspTemp(bodyPartList[(int)parent]);
                else
                {
                    TrackOnGrasp(bodyPartList[(int)parent]);
                }
            }
            else
            {
                var success = false;
                foreach (var bodyPart in bodyPartList)
                {
                    if (bodyPart.state < State.Grasped)
                    {
                        GraspBodyPart(bodyPart, attachPoint);
                        if (temporarily)
                            TrackOnGrasp(bodyPartList[(int)bodyPart.name]);
                        else
                        {
                            TrackOnGraspTemp(bodyPartList[(int)bodyPart.name]);
                        }
                        success = true;
                    }
                }
                return success;
            }
            _hand.Handler.DebugShowActive();
            return true;
        }

        internal bool OnTriggerRelease()
        {
            if (_tempHeldBodyParts.Count > 0)
            {
                ReleaseBodyParts(_tempHeldBodyParts);
                TrackOnGraspTemp(null);
                VRPlugin.Logger.LogDebug($"OnTriggerRelease");
                _hand.Handler.DebugShowActive();
            }
            return true;
        }

        internal bool OnTouchpadResetHeld()
        {
            if (_tempHeldBodyParts.Count > 0)
            {
                VRPlugin.Logger.LogDebug($"ResetHeldBodyPart[PressVersion]:[Temp]");
                ResetBodyParts(_tempHeldBodyParts, true);
                TrackOnGraspTemp(null);
            }
            if (_heldBodyParts.Count > 0)
            {
                VRPlugin.Logger.LogDebug($"ResetHeldBodyPart[PressVersion]");
                ResetBodyParts(_heldBodyParts, true);
                StopGraspTrack();
            }
            return true;
        }
        internal bool OnTouchpadResetActive(Tracker.Body trackerPart, ChaControl chara)
        {
            // We attempt to reset orientation if part was active.
            var baseName = ConvertTrackerToIK(trackerPart);
            VRPlugin.Logger.LogDebug($"ResetActiveBodyPart:{trackerPart}:{chara.name}:{baseName}");
            if (baseName != PartName.Body) 
            {
                var bodyParts = GetTargetParts(_bodyPartsDic[chara], baseName, _hand.GetAnchor.position);
                var result = false;
                foreach (var bodyPart in bodyParts)
                {
                    if (bodyPart.state > State.Transition)
                    {
                        ResetBodyPart(bodyPart, true);
                        result = true;
                    }
                }
                if (result)
                    VRPlugin.Logger.LogDebug($"ResetActiveBodyPart[ReleaseVersion]");
                return result;
            }
            else
            {
                return OnTouchpadResetEverything(chara, State.Synced);
            }
            
        }
        internal bool OnTouchpadResetEverything(ChaControl chara, State upToState = State.Synced)
        {
            var result = false;
            foreach (var bodyPart in _bodyPartsDic[chara])
            {
                if (bodyPart.state > State.Transition && bodyPart.state <= upToState)
                {
                    ResetBodyPart(bodyPart, transition: true);
                    result = true;
                }
            }
            return result;
        }
        internal bool OnMenuPress()
        {
            if (_heldBodyParts.Count != 0)
            {
                _visual.ShowAttachPoints(_heldChara, _heldBodyParts);
            }
            return true;
        }
        internal void OnGripPress(Tracker.Body trackerPart, ChaControl chara)
        {
            var bodyPartList = _bodyPartsDic[chara];
            var controller = _hand.OnGripPress();
            var bodyParts = GetTargetParts(bodyPartList, ConvertTrackerToIK(trackerPart), controller.position);
            VRPlugin.Logger.LogDebug($"OnGripPress:{trackerPart} -> {bodyParts[0].name}:totally held - {bodyParts.Count}");
            foreach (var bodyPart in bodyParts)
            {
                GraspBodyPart(bodyPart, controller);
            }
            _heldChara = chara;
            TrackOnGrasp(bodyParts);
        }
        internal void OnGripRelease()
        {
            VRPlugin.Logger.LogDebug($"OnGripPress");
            if (_heldBodyParts.Count > 0)
            {
                ReleaseBodyParts(_heldBodyParts);
                ReleaseBodyParts(_tempHeldBodyParts);
                _hand.OnGripRelease();
                StopGrasp();
            }
            _hand.Handler.DebugShowActive();
        }
        private void SetGuideObjects(bool active, IEnumerable<BodyPart> bodyPartList)
        {
            if (active)
            {
                _visual.ShowAttachPoints(_heldChara, bodyPartList);
            }
            else
            {
                _visual.HideAttachPoints(_heldChara, bodyPartList);
            }
        }
        private void ReleaseBodyParts(IEnumerable<BodyPart> bodyPartsList)
        {
            foreach (var bodyPart in bodyPartsList)
            {
                VRPlugin.Logger.LogDebug($"ReleaseBodyPart:{bodyPart.anchor.name} -> {bodyPart.origTarget.name}");

                // Attached bodyParts released one by one if they overstretch (not implemented), or by directly grabbing/resetting one.
                if (bodyPart.state != State.Attached)
                {
                    bodyPart.state = State.Active;
                    if (bodyPart.handler != null)
                    {
                        bodyPart.anchor.parent = null;
                        bodyPart.handler.transform.SetPositionAndRotation(bodyPart.anchor.transform.position, bodyPart.origTarget.rotation);
                        bodyPart.handler.Follow();
                        bodyPart.anchor.SetParent(bodyPart.handler.transform, true);
                    }
                    else
                    {
                        bodyPart.anchor.SetParent(bodyPart.origTarget, worldPositionStays: true);
                    }
                }
            }
        }
        private void ResetBodyParts(IEnumerable<BodyPart> bodyPartList, bool transition)
        {
            foreach (var bodyPart in bodyPartList)
            {
                ResetBodyPart(bodyPart, transition);
            }
        }
        private void ResetBodyPart(BodyPart bodyPart, bool transition)
        {
            bodyPart.anchor.SetParent(bodyPart.origTarget, worldPositionStays: transition);
            if (bodyPart.state == State.Attached)
                _attachedBodyParts.Remove(bodyPart);
            if (transition)
            {
                _helper.StartTransition(bodyPart);
            }
            else
            {
                bodyPart.state = State.Default;
                //bodyPart.anchor.localRotation = Quaternion.identity;
                //bodyPart.anchor.localPosition = Vector3.zero;
                if (bodyPart.chain != null) 
                    bodyPart.chain.bendConstraint.weight = 1f; 
            }
            if (bodyPart.handler != null)
            {
                EnableColliders(bodyPart);
                bodyPart.handler.gameObject.SetActive(false);
               // _hand.Handler.RemoveHandlerColliders();
            }
        }

        
        internal void OnPoseChange()
        {
            ResetBodyParts(_heldBodyParts, false);
            ResetBodyParts(_tempHeldBodyParts, false);
            ResetBodyParts(_syncedBodyParts, false);
            ResetBodyParts(_attachedBodyParts, false);
            StopGrasp();
            StopSync();
            _helper.OnPoseChange();
        }
        
        private void SyncBodyPart(BodyPart bodyPart, Transform attachPoint)
        {
            if (bodyPart.state == State.Transition)
            {
                _helper.StopTransition(bodyPart);
            }
            if (bodyPart.handler != null)
            {
                bodyPart.handler.gameObject.SetActive(false);
                EnableColliders(bodyPart);
            }
            bodyPart.state = State.Synced;
            bodyPart.anchor.SetParent(attachPoint, worldPositionStays: true); 
            bodyPart.effector.target = bodyPart.anchor;
            bodyPart.effector.positionWeight = 1f;
            bodyPart.effector.rotationWeight = 1f;
            if (bodyPart.chain != null)
            {
                bodyPart.chain.bendConstraint.weight = 0f;
            }
            VRPlugin.Logger.LogDebug($"SyncBodyPart:{bodyPart.anchor.name} -> {bodyPart.anchor.parent.name}");
        }
        private void AttachBodyPart(BodyPart bodyPart, Transform attachPoint)
        {
            if (bodyPart.handler != null)
            {
                bodyPart.handler.gameObject.SetActive(false);
                EnableColliders(bodyPart);
                //_hand.Handler.RemoveHandlerColliders();
            }
            bodyPart.state = State.Attached;
            //bodyPart.anchor.SetParent(HSceneInterpreter.lstFemale[0].objHeadBone.transform, worldPositionStays: true);
            bodyPart.anchor.parent = null;
            bodyPart.attachTarget = attachPoint;
            bodyPart.offset = attachPoint.InverseTransformPoint(bodyPart.anchor.position);
            bodyPart.effector.target = bodyPart.anchor;
            bodyPart.effector.positionWeight = 1f;
            bodyPart.effector.rotationWeight = 1f;
            if (bodyPart.chain != null)
            {
                bodyPart.chain.bendConstraint.weight = 0f;
            }
            _helper.Attach = true;
            //VRPlugin.Logger.LogDebug($"AttachBodyPart:{bodyPart.anchor.name} -> {bodyPart.anchor.parent.name}");
        }
        private void GraspBodyPart(BodyPart bodyPart, Transform attachPoint)
        {
            if (bodyPart.state == State.Transition) 
                _helper.StopTransition(bodyPart);
            if (bodyPart.state == State.Attached)
                _helper.RemoveAttach(bodyPart);
            //bodyPart.anchor.SetPositionAndRotation(bodyPart.effector.bone.position, bodyPart.effector.bone.rotation);
            if (bodyPart.handler != null)
            {
                DisableColliders(bodyPart);

                // Disabled state while assigning - source of very weird behaviors.
                bodyPart.handler.gameObject.SetActive(true);

                // In case we were parented to handler.
                bodyPart.anchor.parent = null;
                bodyPart.handler.transform.SetPositionAndRotation(bodyPart.effector.bone.position, attachPoint.rotation);
                bodyPart.handler.Follow(attachPoint);
                bodyPart.anchor.SetParent(bodyPart.handler.transform, true);
            }
            else
            {
                bodyPart.anchor.SetParent(attachPoint, worldPositionStays: true);
            }
            bodyPart.effector.target = bodyPart.anchor;
            bodyPart.effector.positionWeight = 1f;
            bodyPart.effector.rotationWeight = 1f;
            if (bodyPart.chain != null)
            {
                bodyPart.chain.bendConstraint.weight = 0f;
            }
            bodyPart.state = State.Grasped;
            VRPlugin.Logger.LogDebug($"GraspBodyPart:{bodyPart.name} -> {bodyPart.anchor.name} -> {bodyPart.anchor.parent.name}");
        }
        //private void GraspBodyPartEx(Transform attachPoint, BodyPart bodyPart)
        //{
        //    if (bodyPart.state == State.Transition) StopTransition(bodyPart);
        //    bodyPart.anchor.SetPositionAndRotation(bodyPart.effector.bone.position, bodyPart.effector.bone.rotation);
        //    bodyPart.effector.target = bodyPart.anchor;
        //    bodyPart.effector.positionWeight = 1f;
        //    bodyPart.effector.rotationWeight = 1f;
        //    if (bodyPart.chain != null) bodyPart.chain.bendConstraint.weight = 0f;
        //    bodyPart.anchor.SetParent(attachPoint, worldPositionStays: true);
        //    VRPlugin.Logger.LogDebug($"GraspBodyPart:{bodyPart.name} -> {bodyPart.anchor.name} -> {bodyPart.anchor.parent.name}");
        //    VRPlugin.Logger.LogDebug($"Distance - {Vector3.Distance(bodyPart.anchor.position, attachPoint.position)}");
        //}

        internal bool OnTouchpadSyncStart(Tracker.Body trackerPart, ChaControl chara)
        {
            var partName = ConvertTrackerToIK(trackerPart);
            // It always 
            if (partName > PartName.RightThigh && partName < PartName.UpperBody)
            {
                VRPlugin.Logger.LogDebug($"OnTouchpadSyncLimb:{trackerPart} -> {partName}");
                var bodyPart = _bodyPartsDic[chara][(int)partName];
                var controller = _hand.GetEmptyAnchor();
                SyncBodyPart(bodyPart, controller);
                var limbIndex = (int)partName - 5;
                bodyPart.anchor.transform.localPosition = _limbPosOffsets[limbIndex];
                bodyPart.anchor.transform.localRotation = _limbRotOffsets[limbIndex];
                bodyPart.chain.pull = 0.5f;
                bodyPart.state = State.Synced;
                DisableColliders(bodyPart);
                _syncedChara = chara;
                TrackOnSync(bodyPart);
                return true;
            }
            return false;
        }
        internal bool OnTouchpadSyncEnd()
        {
            if (_syncedBodyParts.Count > 0)
            {
                foreach (var bodyPart in _syncedBodyParts)
                {
                    ResetBodyParts(_syncedBodyParts, true);
                    EnableColliders(bodyPart);
                    TrackOnSync(null);
                    _hand.ChangeItem();
                }
                return true;
            }
            return false;
        }
        private void DisableColliders(BodyPart bodyPart)
        {
            // We need this so that rigidBody doesn't freak.
            // Otherwise limb is a foreign object, so we can get a GMod flashback.

            foreach (var param in bodyPart.colliderParams)
            {
                if (param.collider == null) continue;
                if (param.activeHeight == 0f)
                {
                    param.collider.enabled = false;
                    _hand.Handler.RemoveCollider(param.collider);
                }
                else
                {
                    if (param.collider is CapsuleCollider capsule)
                    {
                        capsule.height = param.activeHeight;
                    }
                }
                //VRPlugin.Logger.LogDebug($"DisableColliders:{param.collider.name}");
            }
        }
        private void EnableColliders(BodyPart bodyPart)
        {
            foreach (var param in bodyPart.colliderParams)
            {
                if (param.collider == null) continue;
                if (param.activeHeight == 0f)
                {
                    param.collider.enabled = true;
                }
                else
                {
                    if (param.collider is CapsuleCollider capsule)
                    {
                        capsule.height = param.normalHeight;
                    }
                }
            }
        }
        //private PartName ConvertToLimb(PartName partName)
        //{
        //    return partName switch
        //    {
        //        PartName.LeftShoulder  => PartName.LeftHand,
        //        PartName.RightShoulder => PartName.RightHand,
        //        PartName.LeftThigh     => PartName.LeftFoot,
        //        PartName.RightThigh    => PartName.RightFoot,
        //        PartName.Body          => PartName.UpperBody,
        //                             _ => partName
        //    };

        //}
        //private float _testNumber = 1f / 0.3f;
        //internal Vector3 HitReactionWorkaround(Vector3 vec, ChaControl chara, FullBodyBipedEffector partName)
        //{
        //    if (_init && _bodyPartsDic.ContainsKey(chara))
        //    {
        //        var bodyPart = _bodyPartsDic[chara][(int)partName];
        //        if (bodyPart.active)
        //        {
        //            var pos = (bodyPart.anchor.position - bodyPart.origTarget.position) * _testNumber;
        //            VRPlugin.Logger.LogDebug($"HitReactionFix:{vec}:{pos}");
        //            VRPlugin.Logger.LogDebug($"HitReactionFix:{bodyPart.targetBone}:{bodyPart.origTarget}");
        //            return vec + pos;
        //        }
        //    }
        //    return vec;
        //}
        private void UpdateBlackList()
        {
            _blackListDic.Clear();
            SyncBlackList(_syncedBodyParts, _syncedChara);
            SyncBlackList(_heldBodyParts, _heldChara);
            SyncBlackList(_tempHeldBodyParts, _heldChara);
        }
        private void SyncBlackList(List<BodyPart> bodyPartList, ChaControl chara)
        {
            if (chara == null || bodyPartList.Count == 0) return;

            if (!_blackListDic.ContainsKey(chara))
            {
                _blackListDic.Add(chara, new List<Tracker.Body>());
            }
            var blackList = _blackListDic[chara];
            foreach (var bodyPart in bodyPartList)
            {
                foreach (var entry in _blackListEntries[(int)bodyPart.name])
                {
                    if (!blackList.Contains(entry))
                        blackList.Add(entry);
                }
            }

        }
        private static readonly Dictionary<PartName, List<ColliderParam>> _limbColliders = new Dictionary<PartName, List<ColliderParam>>()
        {
            {
                PartName.LeftHand, new List<ColliderParam>()
                {
                    new ColliderParam
                    {
                        path = "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_L/cf_j_shoulder_L/" +
                        "cf_j_arm00_L/cf_j_forearm01_L/cf_d_forearm02_L/cf_s_forearm02_L/cf_hit_wrist_L",
                        normalHeight = 0.24f,
                        activeHeight = 0.15f
                    },
                    new ColliderParam
                    {
                        path = "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_L/cf_j_shoulder_L/cf_j_arm00_L/cf_j_forearm01_L/cf_j_hand_L/com_hit_hand_L",
                        activeHeight = 0f
                    }

                }
            },
            {
                PartName.RightHand, new List<ColliderParam>()
                {
                    new ColliderParam
                    {
                        path = "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_R/cf_j_shoulder_R/" +
                        "cf_j_arm00_R/cf_j_forearm01_R/cf_d_forearm02_R/cf_s_forearm02_R/cf_hit_wrist_R",
                        normalHeight = 0.24f,
                        activeHeight = 0.15f
                    },
                    new ColliderParam
                    {
                        path = "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_R/cf_j_shoulder_R/cf_j_arm00_R/cf_j_forearm01_R/cf_j_hand_R/com_hit_hand_R",
                        activeHeight = 0f
                    }

                }
            },
            {
                PartName.LeftFoot, new List<ColliderParam>()
                {
                    new ColliderParam
                    {
                        path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_L/cf_j_leg01_L/cf_s_leg01_L/cf_hit_leg01_L/aibu_reaction_legL",
                        normalHeight = 0.4f,
                        activeHeight = 0.35f
                    },
                    new ColliderParam
                    {
                        path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_L/cf_j_leg01_L/cf_j_leg03_L/cf_j_foot_L/cf_hit_leg02_L",
                        activeHeight = 0f
                    }

                }
            },
            {
                PartName.RightFoot, new List<ColliderParam>()
                {
                    new ColliderParam
                    {
                        path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_R/cf_j_leg01_R/cf_s_leg01_R/cf_hit_leg01_R/aibu_reaction_legR",
                        normalHeight = 0.4f,
                        activeHeight = 0.35f
                    },
                    new ColliderParam
                    {
                        path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_R/cf_j_leg01_R/cf_j_leg03_R/cf_j_foot_R/cf_hit_leg02_R",
                        activeHeight = 0f
                    }

                }
            }
        };

        private static readonly List<List<Tracker.Body>> _blackListEntries = new List<List<Tracker.Body>>()
        {
            // 0
            new List<Tracker.Body>() { Tracker.Body.None }, 
            // 1
            new List<Tracker.Body>() { Tracker.Body.HandL, Tracker.Body.ForearmL, Tracker.Body.ArmL,
                Tracker.Body.UpperBody, Tracker.Body.MuneL, Tracker.Body.MuneR },
            // 2
            new List<Tracker.Body>() { Tracker.Body.HandR, Tracker.Body.ForearmR, Tracker.Body.ArmR,
                Tracker.Body.UpperBody, Tracker.Body.MuneL, Tracker.Body.MuneR },
            // 3
            new List<Tracker.Body>() { Tracker.Body.LegL, Tracker.Body.ThighL, Tracker.Body.LowerBody,
                Tracker.Body.Asoko, Tracker.Body.Groin},
            // 4
            new List<Tracker.Body>() { Tracker.Body.LegR, Tracker.Body.ThighR, Tracker.Body.LowerBody,
                Tracker.Body.Asoko, Tracker.Body.Groin},
            // 5 
            new List<Tracker.Body>() { Tracker.Body.HandL, Tracker.Body.ForearmL, Tracker.Body.ArmL },
            // 6
            new List<Tracker.Body>() { Tracker.Body.HandR, Tracker.Body.ForearmR, Tracker.Body.ArmR },
            // 7
            new List<Tracker.Body>() { Tracker.Body.LegL },
            // 8
            new List<Tracker.Body>() { Tracker.Body.LegR },
        };

        //internal void SyncMaleHand(int index)
        //{
        //    //Restore male shoulder parameters to default as shoulder fixing will be disabled when hands are anchored to the controllers
        //    //bodyPart.parentJointBone.bone = null;
        //    //bodyPart.parentJointEffector.positionWeight = 0f;
        //}

        //private void FigureOut()
        //{
        //    //Restore male shoulder parameters to default as shoulder fixing will be disabled when hands are anchored to the controllers
        //    bodyPart.parentJointBone.bone = null;
        //    bodyPart.parentJointEffector.positionWeight = 0f;

        //    //The effector mode is for changing the way the limb behaves when not weighed in.
        //    //Free means the node is completely at the mercy of the solver. 
        //    //(If you have problems with smoothness, try changing the effector mode of the hands to MaintainAnimatedPosition or MaintainRelativePosition


        //    //MaintainRelativePositionWeight maintains the limb's position relative to the chest for the arms and hips for the legs. 
        //    // So if you pull the character from the left hand, the right arm will rotate along with the chest.
        //    //Normally you would not want to use this behaviour for the legs.
        //    ik.solver.leftHandEffector.maintainRelativePositionWeight = 1f;


        //    // The body effector is a multi-effector, meaning it also manipulates with other nodes in the solver, namely the left thigh and the right thigh
        //    // so you could move the body effector around and the thigh bones with it. If we set effectChildNodes to false, the thigh nodes will not be changed by the body effector.
        //    ik.solver.body.effectChildNodes = false;


        //    ik.solver.leftArmMapping.maintainRotationWeight = 1f; // Make the left hand maintain its rotation as animated.
        //    ik.solver.headMapping.maintainRotationWeight = 1f; // Make the head maintain its rotation as animated.

        //    // Keep the "Reach" values at 0 if you don't need them. By default they are 0.05f to improve accuracy.
        //    // Keep the Spine Twist Weight at 0 if you don't see the need for it.
        //    // Also setting the "Spine Stiffness", "Pull Body Vertical" and/or "Pull Body Horizontal" to 0 will slightly help the performance.
        //    //
        //    // Component variables:
        //    // fixTransforms - if true, will fix all the Transforms used by the solver to their initial state in each Update. This prevents potential problems with unanimated bones and animator culling with a small cost of performance
        //    // weight - the solver weight for smoothly blending out the effect of the IK
        //    // iterations - the solver iteration count. If 0, full body effect will not be calculated. This allows for very easy optimization of IK on character in the distance.

        //}
        //internal void DetachMaleHand(int index)
        //{
        //    var limbIndex = (int)(index == 0 ? LimbName.MaleLeftHand : LimbName.MaleRightHand);
        //    var limb = limbs[limbIndex];

        //    limb.Effector.target = limb.OrigTarget;
        //    limb.Anchor.SetActive(false);
        //    limb.Chain.bendConstraint.weight = 1f;
        //    limb.Chain.pull = 1f;
        //}

        //internal void UpdatePlayerIK()
        //{
        //    foreach (var hand in maleHands)
        //    {
        //        //To prevent excessive stretching or the hands being at a weird angle with the default IKs (e.g., grabing female body parts),
        //        //if rotation difference between the IK effector and original animation is beyond threshold, set IK weights to 0. 
        //        //Set IK weights to 1 if otherwise.
        //        if (!hand.Active) continue;
        //        if (Quaternion.Angle(hand.Effector.target.rotation, hand.AnimPos.rotation) > 45f)
        //        {
        //            hand.Effector.positionWeight = 0f;
        //            hand.Effector.rotationWeight = 0f;
        //        }
        //        else
        //        {
        //            hand.Effector.positionWeight = 1f;
        //            hand.Effector.rotationWeight = 1f;
        //        }
        //    }
        //}
        /// <summary>
        /// Release and attach male limbs based on the distance between the attaching target position and the default animation position
        /// </summary>
        //private void MaleIKs()
        //      {
        //          bool hideGropeHands = setFlag && hFlag.mode != HFlag.EMode.aibu && GropeHandsDisplay.Value < HideHandMode.AlwaysShow;

        //          //Algorithm for the male hands
        //          for (int i = (int)LimbName.MaleLeftHand; i <= (int)LimbName.MaleRightHand; i++)
        //          {
        //              //Assign bone to male shoulder effectors and fix it in place to prevent hands from pulling the body
        //              //Does not run if male hands are in sync with controllers to allow further movement of the hands
        //              if (setFlag)
        //              {
        //                  limbs[i].ParentJointBone.bone = limbs[i].ParentJointAnimPos;
        //                  limbs[i].ParentJointEffector.positionWeight = 1f;
        //              }
        //          }

        //          //Algorithm for the male feet
        //          for (int i = (int)LimbName.MaleLeftFoot; i <= (int)LimbName.MaleRightFoot; i++)
        //          {
        //              //Release the male feet from attachment if streched beyond threshold
        //              if (limbs[i].AnchorObj && !limbs[i].Fixed && (limbs[i].Effector.target.position - limbs[i].AnimPos.position).magnitude > 0.2f)
        //              {
        //                  FixLimbToggle(limbs[i]);
        //              }
        //              else
        //              {
        //                  limbs[i].Effector.positionWeight = 1f;
        //              }
        //          }

        //          if (setFlag)
        //          {
        //              //Fix male hips to animation position to prevent male genital from drifting due to pulling from limb chains
        //              male_hips_bd.bone = male_cf_pv_hips;
        //              maleFBBIK.solver.bodyEffector.positionWeight = 1f;
        //              maleFBBIK.solver.bodyEffector.rotationWeight = 1f;
        //          }
        //      }
    }
}
