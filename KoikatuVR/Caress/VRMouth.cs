using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using VRGIN.Core;
using HarmonyLib;
using Unity.Linq;
using static Illusion.Component.ShortcutKey;
using VRGIN.Controls;
using KK_VR.Caress;
using KK_VR.Features;
using KK_VR.Settings;
using KK_VR;
using KK_VR.Interpreters;
using KK_VR.Interpreters.Patches;
using Random = UnityEngine.Random;
using static KK_VR.Interpreters.HSceneInterpreter;
using KK_VR.Controls;
using KK_VR.Handlers;
using static HandCtrl;

namespace KK_VR.Caress
{
    /// <summary>
    /// A component to be attached to the VR camera during an H scene.
    /// It allows the user to kiss in H scenes by moving their head.
    /// </summary>
    internal class VRMouth : ProtectedBehaviour
    {
        /// <summary>
        /// To prevent accidental trigger when we are moving across HScene or moving to/from PoV.
        /// Or when CaressHelper takes care of the process.
        /// </summary>
        internal static bool NoActionAllowed;
        /// <summary>
        /// Indicates whether the currently running LickCo should end.
        /// null if LickCo is not running.
        /// </summary>
        private bool? _lickCoShouldEnd;
        /// <summary>
        /// Indicates whether the currently running KissCo should end.
        /// null if KissCo is not running.
        /// </summary>
        private bool? _kissCoShouldEnd;


        internal static VRMouth Instance;

        private Vector3 _headsetPosLastFrame = Vector3.zero;
        //private Transform _firstFemale;
        //private Transform _firstFemaleMouth;
        private Transform _eyes;
        private Transform _shoulders;

        private ChaControl _chara;
        private KoikatuSettings _settings;
        private VRMouthColliderObject _small;//, _large;
        private ColliderTracker _tracker;
        private CaressHelper _helper;
        //private readonly LongDistanceKissMachine _machine = new LongDistanceKissMachine();

        private bool _denial;
        private bool _sensibleH;
        private bool _inCaressMode = true;
        private float _proximityTimestamp;
        private float _kissDistance = 0.2f;
        private bool _kissAttempt;
        private float _kissAttemptChance;
        private float _kissAttemptTimestamp;

        public bool IsAction => _kissCoShouldEnd == false || _lickCoShouldEnd == false;
        public bool IsKiss => _kissCoShouldEnd == false;
        public bool IsLick => _lickCoShouldEnd == false;

        protected override void OnAwake()
        {
            base.OnAwake();
            Instance = this;
            _settings = VR.Context.Settings as KoikatuSettings;
            // Create 2 colliders, a small one for entering and a large one for exiting.
            _small = VRMouthColliderObject
                .Create("VRMouthSmall", new Vector3(0, 0, 0), new Vector3(0.05f, 0.05f, 0.07f));
            _small.TriggerEnter += HandleTriggerEnter;
            _small.TriggerExit += HandleTriggerExit;
            //_large = VRMouthColliderObject
            //    .Create("VRMouthLarge", new Vector3(0, 0, 0.05f), new Vector3(0.1f, 0.1f, 0.15f));
            //_large.TriggerExit += HandleTriggerExit;
            NoActionAllowed = false;

            //var type = AccessTools.TypeByName("KK_SensibleH.Caress.MoMiController");
            _helper = this.gameObject.AddComponent<CaressHelper>();
            _helper.Initialize();
            _tracker = new ColliderTracker();

            // Too far gone for compatibility without it.
            //if (_sensibleH)
            //{
            _chara = lstFemale[0];
            // Not so sure about rotation and position of the mouth acc, while very familiar with the eyes, so we go with them to check angle and distance.
            _eyes = _chara.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz/cf_J_Eye_tz");
            _shoulders = _chara.objBodyBone.transform.Find("cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_backsk_00");
            //}
            //_firstFemale = _chara.objTop.transform;
            //_firstFemaleMouth = _chara.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceLow_tz/a_n_mouth");
            var heroine = hFlag.lstHeroine[0];
            _kissAttemptChance = 0.1f + ((int)heroine.HExperience - 1) * 0.15f + (heroine.weakPoint == 0 ? 0.1f : 0f);
            _denial = !hFlag.isFreeH && heroine.denial.kiss == false && heroine.isGirlfriend == false;
            VRPlugin.Logger.LogDebug($"VRMouth:Awake:Finish");
        }
        private void OnDestroy()
        {
            GameObject.Destroy(_small.gameObject);
            //GameObject.Destroy(_large.gameObject);
        }
        protected override void OnUpdate()
        {
            if (_inCaressMode && !NoActionAllowed && !AttemptToKiss())// || _helper.IsEndKissCo))
            {
                HandleScoreBasedKissing();
                if (_kissAttempt && !IsKissAnim)
                {
                    _kissAttempt = false;
                    _kissDistance = 0.2f;
                }
            }
        }
        private bool AttemptToKiss()
        {
            if (_kissAttemptTimestamp < Time.time)
            {
                var headPos = VR.Camera.Head.position;
                //VRPlugin.Logger.LogDebug($"AttemptToKiss:Dist - {Vector3.Distance(_eyes.position, headPos)}" +
                //    $":Angle - {Vector3.Angle(headPos - _shoulders.position, _shoulders.forward)}" +
                //    $":DeltaAngle - {Mathf.Abs(Mathf.DeltaAngle(_shoulders.eulerAngles.y, _eyes.eulerAngles.y))}");
                if (Random.value < _kissAttemptChance
                    && IsIdleOutside
                    && Mathf.Abs(Mathf.DeltaAngle(_shoulders.eulerAngles.y, _eyes.eulerAngles.y)) < 30f
                    && Vector3.Distance(_eyes.position, headPos) < 0.55f
                    && Vector3.Angle(headPos - _shoulders.position, _shoulders.forward) < 30f)
                {
                    _kissAttempt = true;
                    _kissDistance = 0.4f;
                    LeanToKiss();
                    SetAttemptTimestamp(2f + Random.value * 2f);
                }
                else
                {
                    SetAttemptTimestamp();
                }
                return true;
            }
            return false;
        }
        private void SetAttemptTimestamp(float modifier = 1f)
        {
            _kissAttemptTimestamp = Time.time + (20f * modifier);
        }
        private void HandleScoreBasedKissing()
        {
            if (_settings.AutomaticKissing)
            {
                // At this point there is no choice but to forsake compatibility.

                //if (!_sensibleH)
                //{
                //    bool decision =
                //    _machine.Step(
                //        Time.time,
                //        _small.transform.InverseTransformPoint(_firstFemaleMouth.position),
                //        _firstFemaleMouth.InverseTransformPoint(_small.transform.position),
                //        Mathf.DeltaAngle(_firstFemale.eulerAngles.y, _firstFemaleMouth.transform.eulerAngles.y));
                //    if (decision)
                //    {
                //        StartKiss();
                //    }
                //    else
                //    {
                //        FinishKiss();
                //    }
                //}
                //else
                //{
                var head = VR.Camera.Head;
                if (!CrossFader.IsInTransition
                    && Vector3.Distance(_eyes.position, head.position) < _kissDistance
                    && Vector3.Angle(_eyes.position - head.position, head.forward) < 30f)
                {
                    if (IsKissingAllowed())
                    {
                        StartKiss();
                    }
                }
                //}
            }
        }

        private bool IsKissingAllowed()
        {
            // I'd rather not deal with cross fading animation, too much edge cases to catch. || HSceneInterpreter.IsKissAnim))
            VRPlugin.Logger.LogDebug($"VRMouth:IsKissingAllowed");
            if (_proximityTimestamp < Time.time)
            {
                if (_denial)
                {
                    if (!IsVoiceActive)
                    {
                        hFlag.voice.playVoices[0] = 103;
                        _proximityTimestamp = Time.time + 10f;
                    }
                    return false;
                }
                if (_helper.IsEndKissCo)
                {
                    // TODO evaluate more frames ? no real need for it.
                    // In case we are in state of disengagement (camera moves away), but grip was pressed and is moving us back for a consecutive one.
                    if (_headsetPosLastFrame == Vector3.zero)
                    {
                        _headsetPosLastFrame = VR.Camera.Head.position;
                        return false;
                    }
                    else
                    {
                        var pos = VR.Camera.Head.position;
                        var curDistance = Vector3.SqrMagnitude(pos - _eyes.position);
                        var lastFrameDistance = Vector3.SqrMagnitude(_headsetPosLastFrame - _eyes.position);
                        if (lastFrameDistance < curDistance)
                        {
                            _headsetPosLastFrame = pos;
                            // That is we are moving away rather then towards.
                            return false;
                        }
                        else
                        {
                            _headsetPosLastFrame = Vector3.zero;
                            return true;
                        }
                    }
                }
                return true;
            }
            return false;
        }

        private void HandleTriggerEnter(Collider other)
        {
            if (_tracker.AddCollider(other) && !NoActionAllowed)// || _helper.IsEndKissCo))
            {
                var suggestedKinds = _tracker.GetSuggestedKinds();
                if (suggestedKinds[1] != AibuColliderKind.none)
                {
                    StartKissLick(suggestedKinds[1]);
                }
                //else if (shouldReact && _settings.AutomaticTouchingByHeadset)
                //{
                //    handCtrl.Reaction(colliderKind[0]);
                //}
            }
        }
        //private AibuColliderKind[] UpdateSelectKindTouch()
        //{
        //    var suggestedKinds = 
        //    handCtrl.selectKindTouch = suggestedKinds[1] == AibuColliderKind.none ? suggestedKinds[0] : suggestedKinds[1];
        //    return suggestedKinds;
        //}
        private void HandleTriggerExit(Collider other)
        {
            _tracker.RemoveCollider(other);
            //if (_tracker.RemoveCollider(other))
            //{
            //    UpdateSelectKindTouch();
            //}
            
        }
        private void StartKissLick(AibuColliderKind colliderKind)
        {
            if (_settings.AutomaticKissing && !_inCaressMode && colliderKind == AibuColliderKind.mouth)
            {
                StartKiss();
            }
            else if (_settings.AutomaticLicking && IsLickingOk(colliderKind, out int layerNum))
            {
                StartLicking(colliderKind, layerNum);
            }
        }
        private bool IsLickingOk(AibuColliderKind colliderKind, out int layerNum)
        {
            layerNum = 0;
            if (_proximityTimestamp > Time.time)
            {
                return false;
            }

            int bodyPartId = (int)colliderKind - 2;
            var layerInfos = handCtrl.dicAreaLayerInfos[bodyPartId];
            int clothState = handCtrl.GetClothState(colliderKind);
            var layerKv = layerInfos
                .Where(kv => kv.Value.useArray == 2)
                .FirstOrDefault();
            var layerInfo = layerKv.Value;
            layerNum = layerKv.Key;
            if (layerInfo == null)
            {
                VRLog.Warn("Licking not ok: no layer found");
                return false;
            }
            if (colliderKind == AibuColliderKind.muneL || colliderKind == AibuColliderKind.muneR)
            {
                // Modify dic instead.
                if ((_chara.IsClothes(0) && _chara.fileStatus.clothesState[0] == 0) || (_chara.IsClothes(2) && _chara.fileStatus.clothesState[2] == 0))
                {
                    
                    return false;
                }
            }
            if (layerInfo.plays[clothState] == -1)
            {
                return false;
            }
            var heroine = handCtrl.flags.lstHeroine[0];
            if (handCtrl.flags.mode != HFlag.EMode.aibu &&
                colliderKind == AibuColliderKind.anal &&
                !heroine.denial.anal &&
                heroine.hAreaExps[3] == 0f)
            {
                return false;
            }

            return true;
        }
        /// <summary>
        /// Attempt to start a kiss.
        /// </summary>
        private void StartKiss()
        {
            VRPlugin.Logger.LogDebug($"VRMouth:StartKiss");
            if (_kissCoShouldEnd != null || handCtrl.IsKissAction())
            {
                // Already kissing.
                return;
            }
            _kissCoShouldEnd = false;
            StartCoroutine(KissCo());
        }
        private IEnumerator KissCo()
        {
            StopAllLicking();

            CaressHelper.Instance.OnKissStart(AibuColliderKind.none);
            var prevKindTouch = handCtrl.selectKindTouch;
            handCtrl.selectKindTouch = AibuColliderKind.mouth;
            var messageDelivered = false;
            HandCtrlHooks.InjectMouseButtonDown(0, () => messageDelivered = true);
            while (!messageDelivered)
            {
                yield return null;
            }
            yield return new WaitForEndOfFrame();
            CaressHelper.Instance.OnKissStart(AibuColliderKind.mouth);
            // Try to restore the old value of selectKindTouch.
            if (handCtrl.selectKindTouch == AibuColliderKind.mouth)
            {
                handCtrl.selectKindTouch = prevKindTouch;
            }
            while (_kissCoShouldEnd == false && handCtrl.IsKissAction())
            {
                yield return new WaitForSeconds(0.2f);
            }
            HandCtrlHooks.InjectMouseButtonUp(0);
            _kissCoShouldEnd = null;
        }
        public void FinishKiss()
        {
            if (_kissCoShouldEnd == false)
            {
                _kissCoShouldEnd = true;
                SetAttemptTimestamp(1f + Random.value * 2f);
            }
        }
        private void StartLicking(HandCtrl.AibuColliderKind colliderKind, int layerNum)
        {
            if (_kissCoShouldEnd != null || _lickCoShouldEnd != null)
            {
                // Already licking.
                return;
            }

            int bodyPartId = (int)colliderKind - 2;
            var usedItem = handCtrl.useAreaItems[bodyPartId];


            // If another item is being used on the target body part, detach it.
            if (usedItem != null && usedItem.idUse != 2)
            {
                handCtrl.DetachItemByUseItem(usedItem.idUse);
            }

            StartCoroutine(LickCo(colliderKind, layerNum, bodyPartId));
            CaressHelper.Instance.OnLickStart(colliderKind);
        }

        private IEnumerator LickCo(HandCtrl.AibuColliderKind colliderKind, int layerNum, int bodyPartId)
        {
            _lickCoShouldEnd = false;

            var oldLayerNum = handCtrl.areaItem[bodyPartId];
            handCtrl.areaItem[bodyPartId] = layerNum;

            var oldKindTouch = handCtrl.selectKindTouch;
            handCtrl.selectKindTouch = colliderKind;
            while (_lickCoShouldEnd == false && handCtrl.areaItem[bodyPartId] == layerNum)
            {
                if (!_sensibleH)
                {
                    yield return CaressUtil.ClickCo();
                    yield return new WaitForSeconds(0.4f + Random.value * 0.2f);
                }
                else
                {
                    yield return new WaitForSeconds(0.2f);
                }
            }
            _lickCoShouldEnd = null;
            handCtrl.selectKindTouch = oldKindTouch;
            handCtrl.DetachItemByUseItem(2);
            if (handCtrl.areaItem[bodyPartId] == layerNum)
            {
                handCtrl.areaItem[bodyPartId] = oldLayerNum;
            }
        }
        public void FinishLicking()
        {
            if (_lickCoShouldEnd == false)
            {
                _lickCoShouldEnd = true;
                _proximityTimestamp = Time.time + 1f;
            }
        }
        private void StopAllLicking()
        {
            FinishLicking();
            handCtrl.DetachItemByUseItem(2);
        }

        internal void OnPositionChange(HSceneProc.AnimationListInfo animationList)
        {
            _inCaressMode = animationList.mode == HFlag.EMode.aibu;
            FinishLicking();
            FinishKiss();
            //_machine.Reset();
        }

        public void OnDisengageStart() => _headsetPosLastFrame = Vector3.zero;

        class VRMouthColliderObject : ProtectedBehaviour
        {
            public delegate void TriggerHandler(Collider other);
            public event TriggerHandler TriggerEnter;
            public event TriggerHandler TriggerExit;

            public static VRMouthColliderObject Create(string name, Vector3 center, Vector3 size)
            {
                var gameObj = new GameObject(name);
                gameObj.transform.localPosition = new Vector3(0, -0.07f, 0.02f);
                gameObj.transform.SetParent(VR.Camera.transform, false);

                var collider = gameObj.AddComponent<BoxCollider>();
                collider.size = size;
                collider.center = center;
                collider.isTrigger = true;

                gameObj.AddComponent<Rigidbody>().isKinematic = true;
                return gameObj.AddComponent<VRMouthColliderObject>();
            }

            protected void OnTriggerEnter(Collider other)
            {
                try
                {
                    TriggerEnter?.Invoke(other);
                }
                catch (Exception e)
                {
                    VRLog.Error(e);
                }
            }

            protected void OnTriggerExit(Collider other)
            {
                try
                {
                    TriggerExit?.Invoke(other);
                }
                catch (Exception e)
                {
                    VRLog.Error(e);
                }
            }
        }
    }
}

// Notes on some fields in HandCtrl:
//
// areaItem[p] : The layer num of the item to be used on the body part p
// useAreaItems[p] : The item currently being used on the body part p
// useItems[s] : The item currently in the slot s
// dicAreaLayerInfos[p][l].useArray : The slot to be used when the layer l is used on the body part p.
//     3 means either slot 0 (left hand) or 1 (right hand).
// item.idUse: The slot to be used for the item.
