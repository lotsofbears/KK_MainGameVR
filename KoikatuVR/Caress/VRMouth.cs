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

namespace KoikatuVR.Caress
{
    /// <summary>
    /// A component to be attached to the VR camera during an H scene.
    /// It allows the user to kiss in H scenes by moving their head.
    /// </summary>
    public class VRMouth : ProtectedBehaviour
    {
        public static bool NoKissingAllowed;
        /// <summary>
        /// Indicates whether the currently running LickCo should end.
        /// null if LickCo is not running.
        /// </summary>
        public static bool? _lickCoShouldEnd;
        /// <summary>
        /// Indicates whether the currently running KissCo should end.
        /// null if KissCo is not running.
        /// </summary>
        public static bool? _kissCoShouldEnd;
        private KoikatuSettings _settings;
        private AibuColliderTracker _aibuTracker;
        private Transform _firstFemale;
        private Transform _firstFemaleMouth;
        private VRMouthColliderObject _small, _large;
        private HandCtrl _hand;
        private HandCtrl _hand1;
        private HFlag _hFlag;
        private bool _inCaressMode = true;
        private readonly LongDistanceKissMachine _machine = new LongDistanceKissMachine();
        private bool _moMiActive;
        private Action<HandCtrl.AibuColliderKind> _callMoMi;


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
            var moMi = AccessTools.TypeByName("KK_SensibleH.MoMiController");
            _moMiActive = moMi != null;
            if (_moMiActive)
            {
                // True argument for kiss, False for lick.
                var methodInfo = AccessTools.FirstMethod(moMi, m => m.Name.Equals("StartVrAction"));
                _callMoMi = AccessTools.MethodDelegate<Action<HandCtrl.AibuColliderKind>>(methodInfo);
                VRLog.Debug($"[delegate = {_callMoMi}]");
            }
            _hand = Traverse.Create(hProc).Field("hand").GetValue<HandCtrl>();
            _hand1 = Traverse.Create(hProc).Field("hand1").GetValue<HandCtrl>();
            _hFlag = Traverse.Create(hProc).Field("flags").GetValue<HFlag>();

            _aibuTracker = new AibuColliderTracker(hProc, referencePoint: transform);
            var lstFemale = new Traverse(hProc).Field("lstFemale").GetValue<List<ChaControl>>();
            _firstFemale = lstFemale[0].objTop.transform;
            _firstFemaleMouth = lstFemale[0].objHeadBone.transform.Find(
                "cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceLow_tz/a_n_mouth");
        }

        private void OnDestroy()
        {
            GameObject.Destroy(_small.gameObject);
            GameObject.Destroy(_large.gameObject);
        }

        protected override void OnUpdate()
        {
            HandleScoreBasedKissing();
        }
        private void HandleScoreBasedKissing()
        {
            if (NoKissingAllowed)
                return;
            var inCaressMode = _hFlag.mode == HFlag.EMode.aibu;
            if (inCaressMode)
            {
                bool decision = _settings.AutomaticKissing &&
                    _machine.Step(
                        Time.time,
                        _small.transform.InverseTransformPoint(_firstFemaleMouth.position),
                        _firstFemaleMouth.InverseTransformPoint(_small.transform.position));
                if (decision)
                {
                    StartKiss();
                }
                else
                {
                    FinishKiss();
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
            if (_aibuTracker.AddIfRelevant(other))
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



        private IEnumerator TriggerReactionCo(int femaleIndex, HandCtrl.AibuColliderKind colliderKind)
        {
            VRLog.Debug("TriggerReactionCo[ClickCo]");
            var kindFields = CaressUtil.GetHands(_aibuTracker.Proc)
                .Select(h => new Traverse(h).Field<HandCtrl.AibuColliderKind>("selectKindTouch"))
                .ToList();
            var oldKinds = kindFields.Select(f => f.Value).ToList();
            CaressUtil.SetSelectKindTouch(_aibuTracker.Proc, femaleIndex, colliderKind);
            yield return CaressUtil.ClickCo();
            for (int i = 0; i < kindFields.Count(); i++)
            {
                kindFields[i].Value = oldKinds[i];
            }
        }
        private void HandleTriggerExit(Collider other)
        {
            if (_aibuTracker.RemoveIfRelevant(other))
            {
                var colliderKind = _aibuTracker.GetCurrentColliderKind(out int _);
                UpdateKissLick(colliderKind);
            }
        }
        private void UpdateKissLick(HandCtrl.AibuColliderKind colliderKind)
        {
            if (NoKissingAllowed)
                return;
            if (_hFlag.nowAnimStateName.EndsWith("OLoop", StringComparison.Ordinal))
            {
                FinishKiss();
                FinishLicking();
            }
            else if (_settings.AutomaticKissing && !_inCaressMode && colliderKind == HandCtrl.AibuColliderKind.mouth)
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
            if (colliderKind <= HandCtrl.AibuColliderKind.mouth ||
                HandCtrl.AibuColliderKind.reac_head <= colliderKind)
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
            if (layerInfo.plays[clothState] == -1)
            {
                return false;
            }
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
            if (_moMiActive)
                _callMoMi(HandCtrl.AibuColliderKind.mouth);
        }
        private IEnumerator KissCo()
        {
            VRLog.Debug("KissCo[MainGameVR] start");
            StopAllLicking();

            var prevKindTouch = _hand.selectKindTouch;
            _hand.selectKindTouch = HandCtrl.AibuColliderKind.mouth;
            var messageDelivered = false;
            HandCtrlHooks.InjectMouseButtonDown(0, () => messageDelivered = true);
            while (!messageDelivered)
            {
                yield return null;
            }
            yield return new WaitForEndOfFrame();

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
        }

        private IEnumerator LickCo(HandCtrl.AibuColliderKind colliderKind, int layerNum, int bodyPartId)
        {
            VRLog.Debug($"LickCo[Start]");
            _lickCoShouldEnd = false;

            var oldLayerNum = _hand.areaItem[bodyPartId];
            _hand.areaItem[bodyPartId] = layerNum;

            var oldKindTouch = _hand.selectKindTouch;
            _hand.selectKindTouch = colliderKind;
            while (_lickCoShouldEnd == false && _hand.areaItem[bodyPartId] == layerNum)
            {
                if (_moMiActive)
                {
                    _callMoMi(colliderKind);
                }
                else
                    yield return CaressUtil.ClickCo();
                yield return new WaitForSeconds(0.5f);
            }
            _lickCoShouldEnd = null;

            // Until the next fiasco.
            //if (_moMiActive)
            //{
            //    // If we do this while SensibleH spams "JudgeProc()", we'll break HandCtrl, thus we wait (should be up to 0.6 seconds) for drag to start.
            //    yield return new WaitUntil(() => _hand.ctrl != HandCtrl.Ctrl.click);
            //}
            _hand.selectKindTouch = oldKindTouch;
            _hand.DetachItemByUseItem(2);
            if (_hand.areaItem[bodyPartId] == layerNum)
            {
                _hand.areaItem[bodyPartId] = oldLayerNum;
            }

            VRLog.Debug($"LickCo[End]");
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
