using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using VRGIN.Core;
using HarmonyLib;
using KoikatuVR.Interpreters;
using KoikatuVR.Settings;
using KoikatuVR.Camera;
using Unity.Linq;
using static Illusion.Component.ShortcutKey;
using static SteamVR_Controller;
using VRGIN.Controls;

namespace KoikatuVR.Caress
{
    /// <summary>
    /// A component to be attached to the VR camera during an H scene.
    /// It allows the user to kiss in H scenes by moving their head.
    /// </summary>
    internal class VRMouth : ProtectedBehaviour
    {
        /// <summary>
        /// To prevent accidental trigger when we are moving across HScene or moving to/from PoV.
        /// Or when CaressHelper takes care of process.
        /// </summary>
        internal static bool NoActionAllowed;
        /// <summary>
        /// Indicates whether the currently running LickCo should end.
        /// null if LickCo is not running.
        /// </summary>
        internal static bool? _lickCoShouldEnd;
        /// <summary>
        /// Indicates whether the currently running KissCo should end.
        /// null if KissCo is not running.
        /// </summary>
        internal static bool? _kissCoShouldEnd;
        //internal static bool _moMiActive;

        private KoikatuSettings _settings;
        private AibuColliderTracker _aibuTracker;
        private Transform _firstFemale;
        private Transform _firstFemaleMouth;
        private Transform _eyes;
        private VRMouthColliderObject _small, _large;
        private HandCtrl _hand;
        private HandCtrl _hand1;
        private HFlag _hFlag;
        private ChaControl _chara;
        private List<ChaControl> _charas;
        private bool _inCaressMode = true;
        private readonly LongDistanceKissMachine _machine = new LongDistanceKissMachine();
        //private Action<HandCtrl.AibuColliderKind> _callMoMi;
        private CaressHelper _helper;
        private bool _sensibleH;


        protected override void OnAwake()
        {
            base.OnAwake();
            _settings = VR.Context.Settings as KoikatuSettings;
            // Create 2 colliders, a small one for entering and a large one for exiting.
            _small = VRMouthColliderObject
                .Create("VRMouthSmall", new Vector3(0, 0, 0), new Vector3(0.05f, 0.05f, 0.07f));//new Vector3(0.15f, 0.1f, 0.1f)); // (0.05f, 0.05f, 0.07f));
            _small.TriggerEnter += HandleTriggerEnter;
            _large = VRMouthColliderObject
                .Create("VRMouthLarge", new Vector3(0, 0, 0.05f), new Vector3(0.1f, 0.1f, 0.15f));
            _large.TriggerExit += HandleTriggerExit;

            var hProc = GameObject.FindObjectOfType<HSceneProc>();

            if (hProc == null)
            {
                VRLog.Error("hProc is null");
            }

            var type = AccessTools.TypeByName("KK_SensibleH.MoMiController");
            _sensibleH = type != null;
            _helper = this.gameObject.AddComponent<CaressHelper>();
            _helper.Initialize(hProc, type);

            _hand = Traverse.Create(hProc).Field("hand").GetValue<HandCtrl>();
            _hand1 = Traverse.Create(hProc).Field("hand1").GetValue<HandCtrl>();
            _hFlag = Traverse.Create(hProc).Field("flags").GetValue<HFlag>();

            _aibuTracker = new AibuColliderTracker(hProc, referencePoint: transform);
            _charas = new Traverse(hProc).Field("lstFemale").GetValue<List<ChaControl>>();
            _chara = _charas[0];
            if (_sensibleH)
            {
                // Not so sure about rotation and position of the mouth acc, while very familiar with eyes, so we go with them to check angle and distance.
                _eyes = _chara.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz/cf_J_Eye_tz");
            }

            _firstFemale = _chara.objTop.transform;
            _firstFemaleMouth = _chara.objHeadBone.transform.Find(
                "cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceLow_tz/a_n_mouth");
        }

        private void OnDestroy()
        {
            GameObject.Destroy(_small.gameObject);
            GameObject.Destroy(_large.gameObject);

            // Shouldn't need this.
            //GameObject.Destroy(_kissHelper);
        }
        internal void OnPositionChange()
        {

        }
        protected override void OnUpdate()
        {
            if (!NoActionAllowed)
            {
                HandleScoreBasedKissing();
            }
            else if (!_helper._endKissCo && _hFlag.nowAnimStateName.EndsWith("OLoop", StringComparison.Ordinal))
            {
                _helper.Halt();
            }
        }
        private void HandleScoreBasedKissing()
        {
            var inCaressMode = _hFlag.mode == HFlag.EMode.aibu;
            if (inCaressMode)
            {
                if (_sensibleH)
                {
                    var head = VR.Camera.Head;
                    var dist = Vector3.Distance(_eyes.position, head.position);
                    var angle = Vector3.Angle(_eyes.position - head.position, head.forward);
                    if (dist < 0.2f
                        && angle < 30f)
                    {
                        VRLog.Debug($"HandleScoreBasedKissing[SensibleH] dist[{dist}] [{angle}]");
                        StartKiss();
                    }
                }
                else
                {
                    bool decision = _settings.AutomaticKissing &&
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
            }
            if (_inCaressMode & !inCaressMode)
            {
                FinishKiss();
                _machine.Reset();
            }
            _inCaressMode = inCaressMode;
        }

        private void HandleTriggerEnter(Collider other)
        {
            VRLog.Debug($"HandleTriggerEnter");
            if (_aibuTracker.AddIfRelevant(other) && !NoActionAllowed)
            {
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
        //    VRLog.Debug("TriggerReactionCo[ClickCo]");
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
            VRLog.Debug($"HandleTriggerExit");
            if (_aibuTracker.RemoveIfRelevant(other) && !NoActionAllowed)
            {
                var colliderKind = _aibuTracker.GetCurrentColliderKind(out int _);
                UpdateKissLick(colliderKind);
            }
        }
        private void UpdateKissLick(HandCtrl.AibuColliderKind colliderKind)
        {
            VRLog.Debug($"{colliderKind}");
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
                || colliderKind >= HandCtrl.AibuColliderKind.reac_head)
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

                if (_chara.fileStatus.clothesState[0] == 0 || _chara.fileStatus.clothesState[2] == 0)
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
            if (_kissCoShouldEnd != null || _hand.isKiss)
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

            CaressHelper.Instance.OnKissStart();
            var prevKindTouch = _hand.selectKindTouch;
            _hand.selectKindTouch = HandCtrl.AibuColliderKind.mouth;
            var messageDelivered = false;
            HandCtrlHooks.InjectMouseButtonDown(0, () => messageDelivered = true);
            while (!messageDelivered)
            {
                yield return null;
            }
            yield return new WaitForEndOfFrame();
            CaressHelper.Instance.OnKissStart();
            // Try to restore the old value of selectKindTouch.
            if (_hand.selectKindTouch == HandCtrl.AibuColliderKind.mouth)
            {
                _hand.selectKindTouch = prevKindTouch;
            }
            while (_kissCoShouldEnd == false && _hand.isKiss)
            {
                yield return null;
            }

            HandCtrlHooks.InjectMouseButtonUp(0);
            _kissCoShouldEnd = null;
        }
        private void FinishKiss()
        {
            if (_kissCoShouldEnd == false)
            {
                _kissCoShouldEnd = true;
            }
        }
        private void StartLicking(HandCtrl.AibuColliderKind colliderKind, int layerNum)
        {
            if (_kissCoShouldEnd != null || _lickCoShouldEnd != null)
            {
                // With unbound
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
                    yield return new WaitForSeconds(0.5f);
                }
                else
                {
                    yield return new WaitForSeconds(1f);
                }
            }
            _lickCoShouldEnd = null;

            // Still a problem, handle it.
            // Until the next fiasco.
            //if (_moMiActive)
            //{
            //    
            //}
            _hand.selectKindTouch = oldKindTouch;
            _hand.DetachItemByUseItem(2);
            if (_hand.areaItem[bodyPartId] == layerNum)
            {
                _hand.areaItem[bodyPartId] = oldLayerNum;
            }
        }
        private void FinishLicking()
        {
            if (_lickCoShouldEnd == false)
            {
                _lickCoShouldEnd = true;
            }
        }

        private void StopAllLicking()
        {
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
