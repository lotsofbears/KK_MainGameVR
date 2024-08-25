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
        private Transform _firstFemale;
        private Transform _firstFemaleMouth;
        private Transform _eyes;

        private HandCtrl _hand;
        private HandCtrl _hand1;
        private ChaControl _chara;
        private KoikatuSettings _settings;
        private VRMouthColliderObject _small, _large;
        private AibuColliderTracker _aibuTracker;
        private CaressHelper _helper;
        private readonly LongDistanceKissMachine _machine = new LongDistanceKissMachine();

        private bool _denial;
        private bool _sensibleH;
        private bool _inCaressMode = true;
        private float _proximityTimestamp;

        protected override void OnAwake()
        {
            base.OnAwake();
            Instance = this;
            _settings = VR.Context.Settings as KoikatuSettings;
            // Create 2 colliders, a small one for entering and a large one for exiting.
            _small = VRMouthColliderObject
                .Create("VRMouthSmall", new Vector3(0, 0, 0), new Vector3(0.05f, 0.05f, 0.07f));
            _small.TriggerEnter += HandleTriggerEnter;
            _large = VRMouthColliderObject
                .Create("VRMouthLarge", new Vector3(0, 0, 0.05f), new Vector3(0.1f, 0.1f, 0.15f));
            _large.TriggerExit += HandleTriggerExit;
            NoActionAllowed = false;


            var hProc = GameObject.FindObjectOfType<HSceneProc>();

            if (hProc == null)
            {
                VRLog.Error("hProc is null");
            }

            var type = AccessTools.TypeByName("KK_SensibleH.Caress.MoMiController");
            _helper = this.gameObject.AddComponent<CaressHelper>();
            _helper.Initialize(hProc, type);
            _aibuTracker = new AibuColliderTracker(hProc, referencePoint: transform);

            _hand = Traverse.Create(hProc).Field("hand").GetValue<HandCtrl>();
            _hand1 = Traverse.Create(hProc).Field("hand1").GetValue<HandCtrl>();
            _chara = new Traverse(hProc).Field("lstFemale").GetValue<List<ChaControl>>().FirstOrDefault();

            _denial = IsDenial();
            _sensibleH = type != null;

            if (_sensibleH)
            {
                // Not so sure about rotation and position of the mouth acc, while very familiar with the eyes, so we go with them to check angle and distance.
                _eyes = _chara.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz/cf_J_Eye_tz");
            }
            _firstFemale = _chara.objTop.transform;
            _firstFemaleMouth = _chara.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceLow_tz/a_n_mouth");
        }
        private void OnDestroy()
        {
            GameObject.Destroy(_small.gameObject);
            GameObject.Destroy(_large.gameObject);
        }
        public bool IsAction => _kissCoShouldEnd == false || _lickCoShouldEnd == false;
        public bool IsKiss => _kissCoShouldEnd == false;
        public bool IsLick => _lickCoShouldEnd == false;
        public void StopLick()
        {
            FinishLicking();

            // Easiest way.
            _proximityTimestamp = Time.time + 1f;
        }
        public void StopKiss()
        {
            FinishKiss();
            //_proximityTimestamp = Time.time + 1f;
        }
        private bool IsDenial()
        {
            var flag = _aibuTracker.Proc.flags;
            var heroine = flag.lstHeroine[0];
            if (!flag.isFreeH && heroine.denial.kiss == false && heroine.isGirlfriend == false)
            {
                return true;
            }
            return false;
        }
        internal void OnPositionChange(HSceneProc.AnimationListInfo animationList)
        {
            _inCaressMode = animationList.mode == HFlag.EMode.aibu;
            _denial = IsDenial();
            FinishLicking();
            FinishKiss();
            _machine.Reset();
        }
        protected override void OnUpdate()
        {
            if (_inCaressMode && !NoActionAllowed)// || _helper.IsEndKissCo))
            {
                HandleScoreBasedKissing();
            }
        }
        private void HandleScoreBasedKissing()
        {
            if (_settings.AutomaticKissing)
            {
                if (!_sensibleH)
                {
                    bool decision =
                    _machine.Step(
                        Time.time,
                        _small.transform.InverseTransformPoint(_firstFemaleMouth.position),
                        _firstFemaleMouth.InverseTransformPoint(_small.transform.position),
                        Mathf.DeltaAngle(_firstFemale.eulerAngles.y, _firstFemaleMouth.transform.eulerAngles.y));
                    if (decision)
                    {
                        StartKiss();
                    }
                    else
                    {
                        FinishKiss();
                    }
                }
                else
                {
                    var head = VR.Camera.Head;
                    if (Vector3.Distance(_eyes.position, head.position) < 0.2f
                        && Vector3.Angle(_eyes.position - head.position, head.forward) < 30f)
                    {
                        if (IsKissingAllowed())
                        {
                            StartKiss();
                        }
                    }
                }
            }
        }

        public void OnDisengageStart() => _headsetPosLastFrame = Vector3.zero;
        private bool IsKissingAllowed()
        {
            if (_proximityTimestamp < Time.time && !CrossFader.IsInTransition)
            {
                if (_denial)
                {
                    if (_aibuTracker.Proc.voice.nowVoices[0].state != HVoiceCtrl.VoiceKind.voice)
                    {
                        _aibuTracker.Proc.flags.voice.playVoices[0] = 103;
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
                        //VRPlugin.Logger.LogDebug($"IsKissingAllowed[FirstFrame][False]");
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
                            //VRPlugin.Logger.LogDebug($"VRMouth:IsKissingAllowed:NextFrame[False]");
                            // That is we are moving away rather then towards.
                            return false;
                        }
                        else
                        {
                            _headsetPosLastFrame = Vector3.zero;
                            //VRPlugin.Logger.LogDebug($"VRMouth:IsKissingAllowed:NextFrame[True]");
                            return true;
                        }
                    }
                }
                //VRPlugin.Logger.LogDebug($"VRMouth:IsKissingAllowed[True]");
                return true;
            }
            //VRPlugin.Logger.LogDebug($"VRMouth:IsKissingAllowed[False]");
            return false;
        }

        private void HandleTriggerEnter(Collider other)
        {
            if (_aibuTracker.AddIfRelevant(other) && !NoActionAllowed)// || _helper.IsEndKissCo))
            {
                VRPlugin.Logger.LogDebug($"VRMouth:HandleTriggerEnter[{other}]");
                var colliderKind = _aibuTracker.GetCurrentColliderKind(out int femaleIndex);
                UpdateKissLick(colliderKind);

                if (_kissCoShouldEnd == null &&
                    HandCtrl.AibuColliderKind.reac_head <= colliderKind &&
                    _settings.AutomaticTouchingByHmd) // &&
                                                      //!CaressUtil.IsSpeaking(_aibuTracker.Proc, femaleIndex))
                {
                    _hand.Reaction(colliderKind);
                    //StartCoroutine(TriggerReactionCo(femaleIndex, colliderKind));
                }
            }
        }
        //private IEnumerator TriggerReactionCo(int femaleIndex, HandCtrl.AibuColliderKind colliderKind)
        //{
        //    var kindFields = CaressUtil.GetHands(_aibuTracker.Proc)
        //        .Select(h => new Traverse(h).Field<HandCtrl.AibuColliderKind>("selectKindTouch"))
        //        .ToList();
        //    var oldKinds = kindFields.Select(f => f.Value).ToList();
        //    CaressUtil.SetSelectKindTouch(_aibuTracker.Proc, femaleIndex, colliderKind);
        //    yield return CaressUtil.ClickCo();
        //    for (int i = 0; i < kindFields.Count(); i++)
        //    {
        //        kindFields[i].Value = oldKinds[i];
        //    }
        //}
        private void HandleTriggerExit(Collider other)
        {
            if (_aibuTracker.RemoveIfRelevant(other) && !NoActionAllowed)// || _helper.IsEndKissCo))
            {
                VRPlugin.Logger.LogDebug($"VRMouth:HandleTriggerExit[{other}]");
                var colliderKind = _aibuTracker.GetCurrentColliderKind(out int _);
                UpdateKissLick(colliderKind);
            }
        }
        private void UpdateKissLick(HandCtrl.AibuColliderKind colliderKind)
        {
            if (_settings.AutomaticKissing && !_inCaressMode && colliderKind == HandCtrl.AibuColliderKind.mouth)
            {
                StartKiss();
            }
            else if (_settings.AutomaticLicking && IsLickingOk(colliderKind, out int layerNum))
            {
                StartLicking(colliderKind, layerNum);
            }
            else
            {
                if (!_inCaressMode)
                {
                    FinishKiss();
                }
                FinishLicking();
            }
        }
        private bool IsLickingOk(HandCtrl.AibuColliderKind colliderKind, out int layerNum)
        {
            layerNum = 0;
            if (colliderKind <= HandCtrl.AibuColliderKind.mouth
                || colliderKind >= HandCtrl.AibuColliderKind.reac_head || _proximityTimestamp > Time.time)
            {
                return false;
            }

            int bodyPartId = (int)colliderKind - 2;
            var layerInfos = _hand.dicAreaLayerInfos[bodyPartId];
            int clothState = _hand.GetClothState(colliderKind);
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
            if (colliderKind == HandCtrl.AibuColliderKind.muneL || colliderKind == HandCtrl.AibuColliderKind.muneR)
            {
                if ((_chara.IsClothes(0) && _chara.fileStatus.clothesState[0] == 0) || (_chara.IsClothes(2) && _chara.fileStatus.clothesState[2] == 0))
                {
                    return false;
                }
            }
            // By default tongue is always good to go. Pointless.
            //if (layerInfo.plays[clothState] == -1)
            //{
            //    return false;
            //}
            var heroine = _hand.flags.lstHeroine[0];
            if (_hand.flags.mode != HFlag.EMode.aibu &&
                colliderKind == HandCtrl.AibuColliderKind.anal &&
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
            if (_kissCoShouldEnd != null || _hand.IsKissAction())
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

            CaressHelper.Instance.OnKissStart(HandCtrl.AibuColliderKind.none);
            var prevKindTouch = _hand.selectKindTouch;
            _hand.selectKindTouch = HandCtrl.AibuColliderKind.mouth;
            var messageDelivered = false;
            HandCtrlHooks.InjectMouseButtonDown(0, () => messageDelivered = true);
            while (!messageDelivered)
            {
                yield return null;
            }
            yield return new WaitForEndOfFrame();
            CaressHelper.Instance.OnKissStart(HandCtrl.AibuColliderKind.mouth);
            // Try to restore the old value of selectKindTouch.
            if (_hand.selectKindTouch == HandCtrl.AibuColliderKind.mouth)
            {
                _hand.selectKindTouch = prevKindTouch;
            }
            while (_kissCoShouldEnd == false && _hand.IsKissAction())
            {
                //yield return null;
                yield return new WaitForSeconds(0.2f);
            }
            HandCtrlHooks.InjectMouseButtonUp(0);
            _kissCoShouldEnd = null;
        }
        private void FinishKiss()
        {
            VRPlugin.Logger.LogDebug($"VRMouth:FinishKiss");
            if (_kissCoShouldEnd == false)
            {
                _kissCoShouldEnd = true;
            }
        }
        private void StartLicking(HandCtrl.AibuColliderKind colliderKind, int layerNum)
        {
            VRPlugin.Logger.LogDebug($"VRMouth:StartLicking");
            if (_kissCoShouldEnd != null || _lickCoShouldEnd != null)
            {
                // Already licking.
                return;
            }

            int bodyPartId = (int)colliderKind - 2;
            var usedItem = _hand.useAreaItems[bodyPartId];


            // If another item is being used on the target body part, detach it.
            if (usedItem != null && usedItem.idUse != 2)
            {
                _hand.DetachItemByUseItem(usedItem.idUse);
            }

            StartCoroutine(LickCo(colliderKind, layerNum, bodyPartId));
            CaressHelper.Instance.OnLickStart(colliderKind);
        }

        private IEnumerator LickCo(HandCtrl.AibuColliderKind colliderKind, int layerNum, int bodyPartId)
        {
            VRPlugin.Logger.LogDebug($"LickCo[Original]");
            _lickCoShouldEnd = false;

            var oldLayerNum = _hand.areaItem[bodyPartId];
            _hand.areaItem[bodyPartId] = layerNum;

            var oldKindTouch = _hand.selectKindTouch;
            _hand.selectKindTouch = colliderKind;
            while (_lickCoShouldEnd == false && _hand.areaItem[bodyPartId] == layerNum)
            {
                if (!_sensibleH)
                {
                    yield return CaressUtil.ClickCo();
                    yield return new WaitForSeconds(0.4f + UnityEngine.Random.value * 0.2f);
                }
                else
                {
                    yield return new WaitForSeconds(0.2f);
                }
            }
            _lickCoShouldEnd = null;
            _hand.selectKindTouch = oldKindTouch;
            _hand.DetachItemByUseItem(2);
            if (_hand.areaItem[bodyPartId] == layerNum)
            {
                _hand.areaItem[bodyPartId] = oldLayerNum;
            }
        }
        private void FinishLicking()
        {
            VRPlugin.Logger.LogDebug($"VRMouth:FinishLicking");
            if (_lickCoShouldEnd == false)
            {
                _lickCoShouldEnd = true;
            }
        }

        private void StopAllLicking()
        {
            VRPlugin.Logger.LogDebug($"VRMouth:StopAllLicking");
            FinishLicking();
            _hand.DetachItemByUseItem(2);
        }

        class VRMouthColliderObject : ProtectedBehaviour
        {
            public delegate void TriggerHandler(Collider other);
            public event TriggerHandler TriggerEnter;
            public event TriggerHandler TriggerExit;

            public static VRMouthColliderObject Create(string name, Vector3 center, Vector3 size)
            {
                var gameObj = new GameObject(name);
                gameObj.transform.localPosition = new Vector3(0, -0.07f, 0.02f); // (0, -0.07f, 0.02f);
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
