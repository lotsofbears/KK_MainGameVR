using ADV.Commands.Base;
using Illusion.Component.Correct;
using KK_VR.Handlers;
using KK_VR.Interpreters;
using KK_VR.Settings;
using KK_VR.Trackers;
using RootMotion.FinalIK;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using Unity.Linq;
using UnityEngine;
using VRGIN.Core;
using static KK_VR.Interactors.GraspController;

namespace KK_VR.Interactors
{
    // Named Grasp so there is less confusion with GrabMove. 
    internal class GraspController
    {
        private readonly AnimHelper _animHelper = new();
        private readonly HandHolder _hand;
        private static GraspHelper _helper;
        private static readonly List<GraspController> _instances = [];
        private static readonly Dictionary<ChaControl, List<BodyPart>> _bodyPartsDic = [];

        private readonly Dictionary<ChaControl, List<Tracker.Body>> _blackListDic = [];
        private static readonly List<List<PartName>> _jointGroupList =
        [
            [PartName.LeftShoulder, PartName.RightShoulder],
            [PartName.LeftThigh, PartName.RightThigh]
        ];
        // Clutch.
        private GraspHelper.BaseHold _baseHold;

        private ChaControl _heldChara;
        private ChaControl _syncedChara;

        //private static readonly List<BodyPart> _attachedBodyParts = new List<BodyPart>();
        //private readonly Dictionary<ChaControl, string> _animChangeDic = new Dictionary<ChaControl, string>();
        // private readonly Dictionary<BodyPart, List<bool>> _disabledCollidersDic = new Dictionary<BodyPart, List<bool>>();
        // For Grip.
        private readonly List<BodyPart> _heldBodyParts = [];
        // For Trigger conditional long press. 
        private readonly List<BodyPart> _tempHeldBodyParts = [];
        // For Touchpad.
        private readonly List<BodyPart> _syncedBodyParts = [];

        private static readonly List<Vector3> _limbPosOffsets =
        [
            new Vector3(-0.005f, 0.015f, -0.04f),
            new Vector3(0.005f, 0.015f, -0.04f),
            Vector3.zero,
            Vector3.zero
        ];
        private static readonly List<Quaternion> _limbRotOffsets =
        [
            Quaternion.Euler(0f, 90f, 0f),
            Quaternion.Euler(0f, -90f, 0f),
            Quaternion.identity,
            Quaternion.identity
        ];

        // Add held items too once implemented. All bodyParts have black list entries, dic is sufficient.
        internal bool IsBusy => _blackListDic.Count != 0 || _baseHold != null;
        internal Dictionary<ChaControl, List<Tracker.Body>> GetBlacklistDic => _blackListDic;
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
            internal readonly PartName name;
            // Personal for each limb.
            internal readonly Transform anchor;
            internal readonly Transform afterIK;
            internal readonly Transform beforeIK;
            internal readonly IKEffector effector;
            internal readonly FBIKChain chain;
            internal readonly BaseData targetBaseData;
            internal State state;
            internal Dictionary<Collider, bool> colliders = [];
            internal BodyPartGuide guide;
            internal VisualObject visual;
            internal bool IsLimb() => name > PartName.RightThigh && name < PartName.UpperBody;

            internal BodyPart(PartName _name, IKEffector _effector, Transform _origTarget,
                BaseData _targetBD, FBIKChain _chain = null)
            {
                name = _name;
                effector = _effector;
                afterIK = _effector.bone;
                beforeIK = _origTarget;
                targetBaseData = _targetBD;
                chain = _chain;

                anchor = new GameObject(name + "Anchor").transform;
                anchor.SetParent(beforeIK, worldPositionStays: false);
                effector.target = anchor;

                visual = new VisualObject(this);
            }
            internal void Reset()
            {
                anchor.parent = beforeIK; // SetParent(beforeIK, false); 
                anchor.localPosition = Vector3.zero;
                anchor.localRotation = Quaternion.identity;
                //VRPlugin.Logger.LogDebug($"{name}:Reset:AnchorLocal = {anchor.localPosition},{anchor.localEulerAngles}");
                guide.Stay();
                state = State.Default; 
                if (chain != null)
                {
                    chain.bendConstraint.weight = 1f;
                }
            }
            //internal void Sync()
            //{
            //    effector.target = null;
            //    anchor.SetPositionAndRotation(afterIK.position, afterIK.rotation);
            //    effector.target = anchor;
            //}
            
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
           // visual = GraspVisualizer.Instance;
            _instances.Add(this);
        }
        internal static void Init(IEnumerable<ChaControl> charas)
        {
            _bodyPartsDic.Clear();
            foreach (var inst in _instances)
            {
                inst._blackListDic.Clear();
            }
            if (_helper == null)
            {
                _helper = charas.First().gameObject.AddComponent<GraspHelper>();
                _helper.Init(charas, _bodyPartsDic);
            }
        }
        

        private void UpdateGrasp(BodyPart bodyPart, ChaControl chara)
        {
            _heldChara = chara;
            _heldBodyParts.Add(bodyPart);
        }
        private void UpdateGrasp(IEnumerable<BodyPart> bodyPart, ChaControl chara)
        {
            _heldChara = chara;
            _heldBodyParts.AddRange(bodyPart);
        }

        private void UpdateTempGrasp(BodyPart bodyPart)
        {
            _tempHeldBodyParts.Add(bodyPart);
        }
        private void UpdateSync(BodyPart bodyPart, ChaControl chara)
        {
            _syncedChara = chara;
            _syncedBodyParts.Add(bodyPart);
        }
        private void StopGrasp()
        {
            _heldBodyParts.Clear();
            if (_heldChara != null)
            {
                _blackListDic.Remove(_heldChara);
                _heldChara = null;
                _tempHeldBodyParts.Clear();

                UpdateBlackList();
            }
            _hand.OnGraspRelease();
        }
        private void StopTempGrasp()
        {
            _tempHeldBodyParts.Clear();
            UpdateBlackList();
        }
        private void StopSync()
        {
            _syncedBodyParts.Clear();
            if (_syncedChara != null)
            {
                _syncedChara = null;
                UpdateBlackList();
            }
        }
        private PartName ConvertTrackerToIK(Tracker.Body part)
        {
            return part switch
            {
                Tracker.Body.ArmL => PartName.LeftShoulder,
                Tracker.Body.ArmR => PartName.RightShoulder,
                Tracker.Body.MuneL or Tracker.Body.MuneR => PartName.UpperBody,
                Tracker.Body.LowerBody => PartName.Body,
                Tracker.Body.LegL => PartName.LeftFoot,
                Tracker.Body.LegR => PartName.RightFoot,
                Tracker.Body.ThighL => PartName.LeftThigh,
                Tracker.Body.ThighR => PartName.RightThigh,
                Tracker.Body.HandL or Tracker.Body.ForearmL => PartName.LeftHand,
                Tracker.Body.HandR or Tracker.Body.ForearmR => PartName.RightHand,
                Tracker.Body.Groin or Tracker.Body.Asoko => PartName.LowerBody,
                // actual UpperBody
                _ => PartName.Body,
            };
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
            // 0 - Shoulders, 1 - thighs
            return list[0] - 0.1f > list[1] ? PartName.LowerBody : PartName.UpperBody;
        }
        //private List<BodyPart> FindJoints(List<BodyPart> lstBodyPart, Vector3 pos)
        //{
        //    // Finds joint pair that was closer to the core and returns it as abnormal index for further processing.
        //    var list = new List<float>();
        //    foreach (var partNames in _jointGroupList)
        //    {
        //        // Avg distance to both joints
        //        list.Add(
        //            (Vector3.Distance(lstBodyPart[(int)partNames[0]].effector.bone.position, pos)
        //            + Vector3.Distance(lstBodyPart[(int)partNames[1]].effector.bone.position, pos))
        //            * 0.5f);
        //    }
        //    // 0 - Shoulders, 1 - thighs
        //    return FindJoint(lstBodyPart, _jointGroupList[list[0] - 0.1f > list[1] ? 1 : 0], pos);
        //}
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
                return [a < b ? partNames[0] : partNames[1]];
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
                // First we look if it's a limb and it has tracking on something.
                // If there is no track, then expand limbs we are holding.
                var heldBodyParts = _heldBodyParts.Concat(_tempHeldBodyParts);
                var bodyPartsLimbs = heldBodyParts
                    .Where(b => b.name != PartName.Body && b.guide != null && b.guide.IsBusy);
                if (bodyPartsLimbs.Any())
                {
                    foreach (var bodyPart in bodyPartsLimbs)
                    {
                        VRPlugin.Logger.LogDebug($"OnTrigger:Attach:Grasped:{bodyPart.name} -> {bodyPart.guide.GetTrackTransform.name}");
                        AttachBodyPart(bodyPart, bodyPart.guide.GetTrackTransform, bodyPart.guide.GetChara);
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
                    .Where(b => b.guide != null && b.guide.IsBusy);
                if (bodyParts.Any())
                {
                    foreach (var bodyPart in bodyParts)
                    {
                        VRPlugin.Logger.LogDebug($"OnTrigger:Attach:Synced:{bodyPart.name} -> {bodyPart.guide.GetTrackTransform.name}");
                        AttachBodyPart(bodyPart, bodyPart.guide.GetTrackTransform, bodyPart.guide.GetChara);
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
            VRPlugin.Logger.LogDebug($"OnTriggerExtendGrasp:{_heldBodyParts.Count}:{_heldChara}");
            var bodyPartList = _bodyPartsDic[_heldChara];
            var closestToCore = _heldBodyParts
                .OrderBy(bodyPart => bodyPart.name)
                .First().name;
            var nearbyPart = GetChild(closestToCore);
            if (nearbyPart == closestToCore || bodyPartList[(int)nearbyPart].state > State.Transition)
            {
                nearbyPart = GetParent(closestToCore);
            }
            VRPlugin.Logger.LogDebug($"OnTriggerExtendGrasp:Temporarily[{temporarily}]:{closestToCore} -> {nearbyPart}");

            var attachPoint = bodyPartList[(int)closestToCore].anchor;
            if (nearbyPart != PartName.Everything)
            {
                if (temporarily)
                    UpdateTempGrasp(bodyPartList[(int)nearbyPart]);
                else
                {
                    UpdateGrasp(bodyPartList[(int)nearbyPart], _heldChara);
                }
                UpdateBlackList();
                GraspBodyPart(bodyPartList[(int)nearbyPart], attachPoint);
            }
            else
            {
                ReleaseBodyParts(bodyPartList);
                HoldChara();
                //StopGrasp();
                //UpdateBlackList();
            }
            _hand.Handler.DebugShowActive();
            return true;
        }
        private void HoldChara()
        {
            _baseHold = _helper.StartBaseHold(_bodyPartsDic[_heldChara][0], _heldChara, _hand.Anchor);
        }
        internal void OnTriggerRelease()
        {
            if (_tempHeldBodyParts.Count > 0)
            {
                ReleaseBodyParts(_tempHeldBodyParts);
                StopTempGrasp();
                UpdateBlackList();
                VRPlugin.Logger.LogDebug($"OnTriggerRelease");
                _hand.Handler.DebugShowActive();
            }
        }

        internal bool OnTouchpadResetHeld()
        {
            if (_heldBodyParts.Count > 0)
            {
                VRPlugin.Logger.LogDebug($"ResetHeldBodyPart[PressVersion]:[Temp]");
                ResetBodyParts(_heldBodyParts, true);
                ResetBodyParts(_tempHeldBodyParts, true);
                StopGrasp();
                _hand.Handler.RemoveGuideObjects();
                return true;
            }
            return false;
        }
        internal bool OnTouchpadResetActive(Tracker.Body trackerPart, ChaControl chara)
        {
            // We attempt to reset orientation if part was active.
            var baseName = ConvertTrackerToIK(trackerPart);
            VRPlugin.Logger.LogDebug($"ResetActiveBodyPart:{trackerPart}:{chara.name}:{baseName}");
            if (baseName != PartName.Body) 
            {
                var bodyParts = GetTargetParts(_bodyPartsDic[chara], baseName, _hand.Anchor.position);
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
                _hand.Handler.RemoveGuideObjects();
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
            _hand.Handler.RemoveGuideObjects();
            return result;
        }
        internal bool OnMenuPress()
        {
            if (_heldBodyParts.Count != 0)
            {

            }
            else
            {
                return false;
            }
            return true;
        }
        internal void OnGripPress(Tracker.Body trackerPart, ChaControl chara)
        {
            var bodyPartList = _bodyPartsDic[chara];
            var controller = _hand.OnGraspHold();
            var bodyParts = GetTargetParts(bodyPartList, ConvertTrackerToIK(trackerPart), controller.position);
            VRPlugin.Logger.LogDebug($"OnGripPress:{trackerPart} -> {bodyParts[0].name}:totally held - {bodyParts.Count}");
            UpdateGrasp(bodyParts, chara);
            UpdateBlackList();
            foreach (var bodyPart in bodyParts)
            {
                GraspBodyPart(bodyPart, controller);
            }
        }
        internal void OnGripRelease()
        {
            VRPlugin.Logger.LogDebug($"OnGripPress");
            if (_baseHold != null )
            {
                _helper.StopBaseHold(_baseHold);
                SyncRoot(_baseHold.chara);
                _baseHold = null;
                StopGrasp();
            }
            else if (_heldBodyParts.Count > 0)
            {
                ReleaseBodyParts(_heldBodyParts);
                ReleaseBodyParts(_tempHeldBodyParts);
                StopGrasp();
            }
            _hand.Handler.DebugShowActive();
        }
        private bool AttemptToScrollBodyPart(bool increase)
        {
            // Only bodyParts directly from the tracker live at 0 index, i.e. firstly interacted with.
            var firstBodyPart = _heldBodyParts[0];
            if (firstBodyPart.name == PartName.LeftHand || firstBodyPart.name == PartName.RightHand)
            {
                _helper.ScrollHand(firstBodyPart.name, _heldChara, increase);
            }
            else
            {
                return false;
            }
            return true;
        }



        internal bool OnBusyHorizontalScroll(bool increase)
        {
            VRPlugin.Logger.LogDebug($"OnHorizontalScroll:Busy:");
            if (_baseHold != null)
            {
                _helper.StartBaseHoldScroll(_baseHold, 2, increase);
            }
            else if (!AttemptToScrollBodyPart(increase))
            {
                return false;
            }
            return true;
        }
        internal bool OnFreeHorizontalScroll(Tracker.Body trackerPart, ChaControl chara, bool increase)
        {
            VRPlugin.Logger.LogDebug($"OnHorizontalScroll:Free:{trackerPart}");
            //animHelper.DoAnimChange(chara);
            //return true;
            if (trackerPart == Tracker.Body.HandL || trackerPart == Tracker.Body.HandR)
            {
                _helper.ScrollHand((PartName)trackerPart, chara, increase);
            }
            else
            {
                return false;
            }
            return true;
        }
        internal void OnScrollRelease()
        {
            if (_baseHold != null)
            {
                _helper.StopBaseHoldScroll(_baseHold);
            }
            else
            {
                _helper.StopScroll();
            }
        }
        internal bool OnVerticalScroll(bool increase)
        {
            //if (_heldChara != null)
            //{
            //    _animHelper.DoAnimChange(_heldChara);
            //}
            //else 
            if (_baseHold != null)
            {
                _helper.StartBaseHoldScroll(_baseHold, 1, increase);
            }
            else
            {
                return false;
            }
            return true;
        }

        private void ReleaseBodyParts(IEnumerable<BodyPart> bodyPartsList)
        {
            foreach (var bodyPart in bodyPartsList)
            {
                // Attached bodyParts released one by one if they overstretch (not implemented), or by directly grabbing/resetting one.
                if (bodyPart.state != State.Attached)
                {
                    bodyPart.state = State.Active;
                    //if (bodyPart.handler != null)
                    //{
                        bodyPart.anchor.parent = null;
                        bodyPart.guide.Follow();
                        bodyPart.anchor.parent = bodyPart.guide.transform;// SetParent(bodyPart.handler.transform, true);
                    //}
                    //else
                    //{
                    //    bodyPart.anchor.parent = bodyPart.beforeIK;
                    //}
                    bodyPart.visual.Hide();
                    VRPlugin.Logger.LogDebug($"ReleaseBodyPart:{bodyPart.anchor.name} -> {bodyPart.beforeIK.name}");
                }
            }
        }
        private void SyncRoot(ChaControl chara)
        {
            // 'bodyPart.afterIK' aka 'bodyPart.effector.target.GetComponent<BaseData>().bone' aka 'cf_j_spine01' ->
            //     bone with: updateOrient = renderOrient while following anim direction
            //
            // 'bodyPart.beforeIK' -> bone with: updateOrient = animOrient != renderOrient
            //

            var bodyPart = _bodyPartsDic[chara][0];
            ReleaseAnchors(chara);
            var targetPos = bodyPart.afterIK.position;
            var charaToAnim = Quaternion.Inverse(bodyPart.beforeIK.rotation) * chara.transform.rotation;
            var charaToIK = Quaternion.Inverse(bodyPart.afterIK.rotation) * chara.transform.rotation;
            //var deltaPos = bodyPart.afterIK.position - bodyPart.beforeIK.position;
            chara.transform.rotation *= (Quaternion.Inverse(charaToIK) * charaToAnim);
            //chara.animBody.GetComponent<FullBodyBipedIK>().UpdateSolver();
            chara.transform.position += targetPos - bodyPart.beforeIK.position;
            //chara.transform.SetPositionAndRotation(chara.transform.position + (bodyPart.afterIK.position - bodyPart.beforeIK.position),
            //    chara.transform.rotation * (Quaternion.Inverse(chara2afterIK) * chara2anim));
            SetAnchors(chara);
        }
        private void ReleaseAnchors(ChaControl chara)
        {
            foreach (var bodyPart in _bodyPartsDic[chara])
            {
                bodyPart.anchor.parent = null;
            }
        }
        private void SetAnchors(ChaControl chara)
        {
            foreach (var bodyPart in _bodyPartsDic[chara])
            {
                bodyPart.anchor.parent = bodyPart.beforeIK;
            }
        }
        private void ResetBodyParts(IEnumerable<BodyPart> bodyPartList, bool transition)
        {
            foreach (var bodyPart in bodyPartList)
            {
                if (bodyPart.state == State.Default) continue;
                ResetBodyPart(bodyPart, transition);
            }
        }
        private void ResetBodyPart(BodyPart bodyPart, bool transition)
        {
            bodyPart.anchor.SetParent(bodyPart.beforeIK, worldPositionStays: transition);
            if (bodyPart.state == State.Attached)
                bodyPart.guide.Follow();
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
            bodyPart.guide.Stay();
            
        }
        internal static void OnPoseChange()
        {
            _helper.OnPoseChange();
            foreach (var inst in _instances)
            {
                inst.Reset();
            }
        }
        private void Reset()
        {
            _hand.Handler.ClearTracker();
            _baseHold = null;
            _blackListDic.Clear();
            _heldBodyParts.Clear();
            _tempHeldBodyParts.Clear();
            _syncedBodyParts.Clear();
            _heldChara = null;
            _syncedChara = null;
        }
        
        private void SyncBodyPart(BodyPart bodyPart, Transform attachPoint)
        {
            if (bodyPart.state == State.Transition)
                _helper.StopTransition(bodyPart);

            
            bodyPart.guide.Stay();
            bodyPart.guide.SetBodyPartCollidersToTrigger(true);
            bodyPart.state = State.Synced;
            bodyPart.anchor.SetParent(attachPoint, worldPositionStays: true); 
            if (bodyPart.chain != null)
                bodyPart.chain.bendConstraint.weight = 0f;
            VRPlugin.Logger.LogDebug($"SyncBodyPart:{bodyPart.anchor.name} -> {bodyPart.anchor.parent.name}");
        }
        private void AttachBodyPart(BodyPart bodyPart, Transform attachPoint, ChaControl chara)
        {
            bodyPart.visual.Hide();
            //bodyPart.guide.Stay();
            if (bodyPart.chain != null)
            {
                bodyPart.chain.bendConstraint.weight = 0f;
            }
            bodyPart.state = State.Attached;
            if (chara == null)
            {
                bodyPart.anchor.parent = attachPoint; // SetParent(attachPoint, true);
            }
            else
            {
                //bodyPart.anchor.parent = null;
                bodyPart.guide.Attach(attachPoint);
                bodyPart.anchor.parent = bodyPart.guide.transform;
                //_helper.AddAttach(bodyPart, attachPoint);
            }
            _hand.Handler.RemoveGuideObjects();
            VRPlugin.Logger.LogDebug($"AttachBodyPart:{bodyPart.anchor.name} -> {attachPoint.name}");
        }

        private void GraspBodyPart(BodyPart bodyPart, Transform attachPoint)
        {
            if (bodyPart.state == State.Transition) 
                _helper.StopTransition(bodyPart);
            //else if (bodyPart.state == State.Attached)
            //    _helper.RemoveAttach(bodyPart);
            //if (bodyPart.handler != null)
            //{
            // In case we were parented to handler.
            // And it has some unWinded rigidBody velocity i.e. offset.
            //bodyPart.anchor.parent = null;

            //bodyPart.handler.transform.SetPositionAndRotation(bodyPart.effector.bone.position, attachPoint.rotation);
            //bodyPart.handler.transform.SetPositionAndRotation(bodyPart.anchor.position, bodyPart.anchor.rotation);

            bodyPart.anchor.parent = null;
            bodyPart.guide.Follow(attachPoint, _hand);
            bodyPart.anchor.parent = bodyPart.guide.transform;// SetParent(bodyPart.handler.transform, worldPositionStays: true);
           // }
            //else
            //{
            //    bodyPart.anchor.SetParent(attachPoint, worldPositionStays: true);
            //}
            //bodyPart.effector.target = bodyPart.anchor;
            bodyPart.effector.positionWeight = 1f;
            bodyPart.effector.rotationWeight = 1f;
            if (bodyPart.chain != null)
            {
                bodyPart.chain.bendConstraint.weight = 0f;
            }
            if (KoikatuInterpreter.settings.ShowGuideObjects) bodyPart.visual.Show();
            bodyPart.state = State.Grasped;
            VRPlugin.Logger.LogDebug($"GraspBodyPart:{bodyPart.name} -> {bodyPart.anchor.name} -> {bodyPart.anchor.parent.name}");
        }
        private bool IsLimb(PartName partName) => partName > PartName.RightThigh && partName < PartName.UpperBody;
        internal bool OnTouchpadSyncStart(Tracker.Body trackerPart, ChaControl chara)
        {
            var partName = ConvertTrackerToIK(trackerPart);
            if (IsLimb(partName))
            {
                VRPlugin.Logger.LogDebug($"OnTouchpadSyncLimb:{trackerPart} -> {partName}");
                var bodyPart = _bodyPartsDic[chara][(int)partName];
                SyncBodyPart(bodyPart, _hand.GetEmptyAnchor());
                var limbIndex = (int)partName - 5;
                bodyPart.anchor.transform.localPosition = _limbPosOffsets[limbIndex];
                bodyPart.anchor.transform.localRotation = _limbRotOffsets[limbIndex];
                bodyPart.chain.pull = 0f;
                bodyPart.state = State.Synced;
                UpdateSync(bodyPart, chara);
                UpdateBlackList();
                return true;
            }
            return false;
        }

        internal bool OnTouchpadSyncEnd()
        {
            if (_syncedBodyParts.Count != 0)
            {
                ResetBodyParts(_syncedBodyParts, true);
                _hand.ChangeItem();

                StopSync();
                return true;
            }
            return false;
        }



        //private void DisableColliders(BodyPart bodyPart)
        //{
        //    // We need this so that rigidBody doesn't freak.
        //    // Otherwise limb is a foreign object, so we can get a GMod flashback.

        //    foreach (var param in bodyPart.colliderParams)
        //    {
        //        param.collider.isTrigger = true;
        //        //if (param.collider == null) continue;
        //        //if (param.activeHeight == 0f)
        //        //{
        //        //    param.collider.enabled = false;
        //        //    _hand.Handler.RemoveCollider(param.collider);
        //        //}
        //        //else
        //        //{
        //        //    if (param.collider is CapsuleCollider capsule)
        //        //    {
        //        //        capsule.height = param.activeHeight;
        //        //    }
        //        //}
        //        //VRPlugin.Logger.LogDebug($"DisableColliders:{param.collider.name}");
        //    }
        //}
        //private static void EnableColliders(BodyPart bodyPart)
        //{
        //    foreach (var param in bodyPart.colliderParams)
        //    {
        //        param.collider.isTrigger = false;
        //        //if (param.collider == null) continue;
        //        //if (param.activeHeight == 0f)
        //        //{
        //        //    param.collider.enabled = true;
        //        //}
        //        //else
        //        //{
        //        //    if (param.collider is CapsuleCollider capsule)
        //        //    {
        //        //        capsule.height = param.normalHeight;
        //        //    }
        //        //}
        //    }
        //}
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
                _blackListDic.Add(chara, []);
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
        

        private static readonly List<List<Tracker.Body>> _blackListEntries =
        [
            // 0
            [Tracker.Body.None], 
            // 1
            [ Tracker.Body.HandL, Tracker.Body.ForearmL, Tracker.Body.ArmL,
                Tracker.Body.UpperBody, Tracker.Body.MuneL, Tracker.Body.MuneR ],
            // 2
            [ Tracker.Body.HandR, Tracker.Body.ForearmR, Tracker.Body.ArmR,
                Tracker.Body.UpperBody, Tracker.Body.MuneL, Tracker.Body.MuneR ],
            // 3
            [ Tracker.Body.LegL, Tracker.Body.ThighL, Tracker.Body.LowerBody,
                Tracker.Body.Asoko, Tracker.Body.Groin],
            // 4
            [ Tracker.Body.LegR, Tracker.Body.ThighR, Tracker.Body.LowerBody,
                Tracker.Body.Asoko, Tracker.Body.Groin],
            // 5 
            [Tracker.Body.HandL, Tracker.Body.ForearmL, Tracker.Body.ArmL],
            // 6
            [Tracker.Body.HandR, Tracker.Body.ForearmR, Tracker.Body.ArmR],
            // 7
            [Tracker.Body.LegL],
            // 8
            [Tracker.Body.LegR],
        ];

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
