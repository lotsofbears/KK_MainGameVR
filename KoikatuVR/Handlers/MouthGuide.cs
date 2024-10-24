using KK_VR.Caress;
using KK_VR.Features;
using KK_VR.Interpreters;
using KK_VR.Settings;
using KK_VR.Trackers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using VRGIN.Core;
using static HandCtrl;

namespace KK_VR.Handlers
{
    internal class MouthGuide : Handler
    {
        internal static MouthGuide Instance => _instance;
        private static MouthGuide _instance;
        internal bool PauseInteractions
        {
            get => _pauseInteractions || _activeCo;
            set => _pauseInteractions = value;
        }
        internal Transform LookAt => _lookAt;
        private static bool _pauseInteractions;
        internal bool IsActive => _activeCo;
        private bool _activeCo;
        private bool _disengage;
        private List<HandHolder> _hands;
        private ChaControl _lastChara;
        private float _kissDistance = 0.2f;
        private bool _mousePress;
        private readonly KoikatuSettings _settings = VR.Context.Settings as KoikatuSettings;

        private bool _followRotation;

        private Transform _followAfter;
        private Vector3 _followOffsetPos;
        private Quaternion _followOffsetRot;

        private Transform _lookAt;
        private Vector3 _lookOffsetPos;
        //private Quaternion _lookOffsetRot;
        private KissHelper _kissHelper;
        private bool _aibu;

        private float _proximityTimestamp;
        private Transform _eyes;
        private Transform _shoulders;
        private bool _gripMove;

        private readonly Dictionary<ChaControl, List<Tracker.Body>> _mouthBlacklistDic = [];

        internal static void SetState(bool active)
        {
            _instance._activeCo = active;
        }
        private void Awake()
        {
            _instance = this;
            _hands = HandHolder.GetHands();
            var collider = gameObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            var rigidBody = gameObject.AddComponent<Rigidbody>();
            rigidBody.isKinematic = true;
            tracker = new Tracker();
            //var type = AccessTools.TypeByName("KK_SensibleH.Caress.MoMiController");
            //if (type != null)
            //{
            //    MoMiOnLickStart = AccessTools.MethodDelegate<Action<AibuColliderKind>>(AccessTools.FirstMethod(type, m => m.Name.Equals("OnLickStart")));
            //    MoMiOnKissStart = AccessTools.MethodDelegate<Action<AibuColliderKind>>(AccessTools.FirstMethod(type, m => m.Name.Equals("OnKissStart")));
            //    MoMiOnKissEnd = AccessTools.MethodDelegate<Action>(AccessTools.FirstMethod(type, m => m.Name.Equals("OnKissEnd")));
            //}

            _eyes = HSceneInterpreter.lstFemale[0].objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz/cf_J_Eye_tz");
            _shoulders = HSceneInterpreter.lstFemale[0].objBodyBone.transform.Find("cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_backsk_00");
            //var heroine = HSceneInterpreter.hFlag.lstHeroine[0];
            //_kissAttemptChance = 0.1f + ((int)heroine.HExperience - 1) * 0.15f + (heroine.weakPoint == 0 ? 0.1f : 0f);
            _kissHelper = new KissHelper(_eyes, _shoulders);
            _aibu = HSceneInterpreter.mode == HFlag.EMode.aibu;
            tracker.SetBlacklistDic(_mouthBlacklistDic);
        }
        internal void OnImpersonation(ChaControl chara)
        {
            _mouthBlacklistDic.Clear();
            _mouthBlacklistDic.Add(chara, [Tracker.Body.None]);
        }
        internal void OnUnImpersonation()
        {
            _mouthBlacklistDic.Clear();
        }
        private void Update()
        {
            if (_aibu && !PauseInteractions && !CrossFader.IsInTransition)
            {
                if (!HandleKissing())
                {
                    _kissHelper.AttemptProactiveKiss();
                }
            }
        }

        //private IEnumerator TryOnceInAwhile()
        //{
        //    // Having it on Update() feels awry, way too methodical/mechanical/robotic, no human lag.
        //    while (true)
        //    {
        //        if (!PauseInteractions && HSceneInterpreter.mode == HFlag.EMode.aibu)
        //        {
        //            HandleKissing();
        //            _kissHelper.ProactiveAttemptToKiss();
        //        }
        //        yield return new WaitForSeconds(0.5f);
        //    }
        //}
        private bool HandleKissing()
        {
            if (_settings.AutomaticKissing)
            {
                var head = VR.Camera.Head;
                if (Vector3.Distance(_eyes.position, head.position) < _kissDistance
                    && Quaternion.Angle(_eyes.rotation, head.rotation * _reverse) < 30f
                    //&& Vector3.Angle(head.position - _eyes.position, _eyes.forward) < 30f
                    //&& Vector3.Angle(_eyes.position - head.position, head.forward) < 30f
                    && IsKissingAllowed())
                {
                    StartKiss();
                    return true;
                }
            }
            return false;
        }
        private readonly Quaternion _reverse = Quaternion.Euler(0f, 180f, 0f);
        protected override void OnTriggerEnter(Collider other)
        {
            // Try to catch random null ref.
            if (tracker.AddCollider(other))
            {
                //if (tracker.reactionType > Tracker.ReactionType.None)
                //{
                //    DoReaction();
                //}
                var touch = tracker.colliderInfo.behavior.touch;
                if (touch != AibuColliderKind.none && !PauseInteractions)
                {
                    if (touch == AibuColliderKind.mouth)
                    {
                        StartKiss();
                    }
                    else if (touch < AibuColliderKind.reac_head)
                    {
                        StartLick(touch);
                    }
                }
            }
        }
        protected override void OnTriggerExit(Collider other)
        {
            if (tracker.AddCollider(other))
            {
                if (!IsBusy)
                {
                    HSceneInterpreter.SetSelectKindTouch(AibuColliderKind.none);
                }
            }
        }
        internal void OnGripMove(bool active)
        {
            if (_disengage)
            {
                Halt(disengage: false);
            }
            _gripMove = active;
        }
        internal void OnTriggerPress()
        {
            Halt(disengage: !_disengage);
        }
        private IEnumerator KissCo()
        {
            // Init part.
            VRPlugin.Logger.LogDebug($"KissCo:Start");
            var origin = VR.Camera.Origin;
            var head = VR.Camera.Head;
            var hand = HSceneInterpreter.handCtrl;

            var messageDelivered = false;
            hand.selectKindTouch = AibuColliderKind.mouth;
            HandCtrlHooks.InjectMouseButtonDown(0, () => messageDelivered = true);
            _mousePress = true;
            _followAfter = _eyes;
            _lookAt = _eyes;
            HSceneInterpreter.MoMiOnKissStart(AibuColliderKind.none);

            while (!messageDelivered)
            {
                hand.selectKindTouch = AibuColliderKind.mouth;
                yield return null;
            }
            DestroyGrab();
            yield return new WaitForEndOfFrame();
            HSceneInterpreter.MoMiOnKissStart(AibuColliderKind.mouth);

            // Movement part.
            // In retrospect, it's amazing that all those vec offsets work out.

            // Find desirable roll.
            //var rotDelta = Quaternion.Inverse(head.rotation * Quaternion.Euler(0f, 180f, 0f)) * _lastChara.objHeadBone.transform.rotation;
            var rollDelta = -Mathf.DeltaAngle((Quaternion.Inverse(head.rotation * Quaternion.Euler(0f, 180f, 0f)) * _lastChara.objHeadBone.transform.rotation).eulerAngles.z, 0f);

            var angleModRight = rollDelta * 0.0111f;//  /90f;
            var absModRight = Mathf.Abs(angleModRight);
            var angleModUp = 1f - absModRight;
            if (absModRight > 1f)
                angleModRight = absModRight - (angleModRight - absModRight);

            var offsetRight = angleModRight * 0.0667f; // /15f;
            var offsetForward = _settings.ProximityDuringKiss;
            var offsetUp = -0.04f - (Math.Abs(offsetRight) * 0.5f);
            var startDistance = Vector3.Distance(_eyes.position, head.position) - offsetForward;

            _followOffsetPos = new Vector3(offsetRight, offsetUp, offsetForward);
            //var fullOffsetVec = new Vector3(offsetRight, offsetUp, offsetForward);
            var rightOffsetVec = new Vector3(offsetRight, 0f, 0f);
            var oldEyesPos = _eyes.position;
            var timestamp = Time.time + 2f;
            while (timestamp > Time.time && !_gripMove)
            {
                // Position is simple MoveTowards + added delta of head movement from previous frame.
                // Rotation is LookRotation at eyes position with tailored offsets highly influenced by camera rotation.
                var moveTowards = Vector3.MoveTowards(head.position, _eyes.TransformPoint(_followOffsetPos), Time.deltaTime * 0.07f);
                var lookRotation = Quaternion.LookRotation(_eyes.TransformPoint(rightOffsetVec) - moveTowards, (_eyes.up * angleModUp) + (_eyes.right * angleModRight)); // + _eyes.forward * -0.1f);
                origin.rotation = Quaternion.RotateTowards(origin.rotation, lookRotation, Time.deltaTime * 60f);
                origin.position += moveTowards + (_eyes.position - oldEyesPos) - head.position;
                oldEyesPos = _eyes.position;
                yield return new WaitForEndOfFrame();
            }
            _followRotation = _settings.FollowRotationDuringKiss;
            _followOffsetRot = Quaternion.Inverse(_followAfter.rotation) * VR.Camera.Origin.rotation;

            while (true)
            {
                if (_gripMove)
                {
                    if (Vector3.Distance(_eyes.position, head.position) > 0.2f)
                    {
                        Halt();
                    }
                }
                else
                {
                    var moveTowards = Vector3.MoveTowards(head.position, _eyes.TransformPoint(_followOffsetPos), Time.deltaTime * 0.05f);
                    if (_followRotation)
                    {
                        origin.rotation = _eyes.rotation * _followOffsetRot;
                    }
                    origin.position += moveTowards + (_eyes.position - oldEyesPos) - head.position;
                }
                oldEyesPos = _eyes.position;
                yield return new WaitForEndOfFrame();
            }
        }
        internal void OnPoseChange()
        {
            Halt(disengage: false);
            _aibu = HSceneInterpreter.mode == HFlag.EMode.aibu;
        }
        internal void UpdateOrientationOffsets()
        {
            var head = VR.Camera.Head;
            _followRotation = _settings.FollowRotationDuringKiss;
            _followOffsetRot = Quaternion.Inverse(_followAfter.rotation) * VR.Camera.Origin.rotation;
            _followOffsetPos = _followAfter.InverseTransformPoint(head.position);
            if (_lookAt != null)
            {
                _lookOffsetPos = _lookAt.InverseTransformPoint(head.position + (head.forward * Vector3.Distance(_lookAt.position, head.position)));
            }
        }
        private void StartKiss()
        {
            Halt(disengage: false);
            _lastChara = HSceneInterpreter.lstFemale[0];
            _activeCo = true;
            StartCoroutine(KissCo());
        }
        private void StartLick(AibuColliderKind colliderKind)
        {
            if (IsLickingAllowed(colliderKind, out var layerNum))
            {
                Halt(disengage: false);
                DestroyGrab();
                _lastChara = HSceneInterpreter.lstFemale[0];
                _activeCo = true;
                HSceneInterpreter.MoMiOnLickStart(AibuColliderKind.none);
                StartCoroutine(AttachCoEx(colliderKind, layerNum));
                StartCoroutine(AttachCo(colliderKind));
            }
        }
        private bool IsLickingAllowed(AibuColliderKind colliderKind, out int layerNum)
        {
            // Still no clue what exactly it does.
            layerNum = 0;
            //if (_disengage)
            //{
            //    return false;
            //}
            var hand = HSceneInterpreter.handCtrl;
            int bodyPartId = (int)colliderKind - 2;
            var layerInfos = hand.dicAreaLayerInfos[bodyPartId];
            int clothState = hand.GetClothState(colliderKind);
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
                // No clue if i modify dic somewhere or we still need this.. so it stays.
                var chara = GetChara;
                if ((chara.IsClothes(0) && chara.fileStatus.clothesState[0] == 0) || (chara.IsClothes(2) && chara.fileStatus.clothesState[2] == 0))
                {
                    return false;
                }
            }
            if (layerInfo.plays[clothState] == -1)
            {
                return false;
            }
            var heroine = hand.flags.lstHeroine[0];
            if (hand.flags.mode != HFlag.EMode.aibu &&
                colliderKind == AibuColliderKind.anal &&
                !heroine.denial.anal &&
                heroine.hAreaExps[3] == 0f)
            {
                return false;
            }
            return true;
        }

        private bool IsKissingAllowed()
        {
            VRPlugin.Logger.LogDebug($"VRMouth:IsKissingAllowed");
            //if (!_disengage)
            //{
            if (!HSceneInterpreter.hFlag.isFreeH)
            {
                var heroine = Manager.Game.Instance.HeroineList
                    .Where(h => h.chaCtrl == _lastChara)
                    .FirstOrDefault();
                if (heroine != null && heroine.denial.kiss == false && heroine.isGirlfriend == false)
                {
                    if (HSceneInterpreter.IsVoiceActive)
                    {
                        HSceneInterpreter.hFlag.voice.playVoices[0] = 103;
                        _proximityTimestamp = Time.time + 10f;
                    }
                    return false;
                }
            }
            else
            {
                return true;
            }
            return true;
        }

        private IEnumerator AttachCoEx(AibuColliderKind colliderKind, int layerNum)
        {
            // We inject full synthetic click first, then wait for crossfade to end,
            // after that we inject button down and wait for an aibu item to activate, then inform SensH and we good to go.
            // Not sure if we still can get a bad state, but just in case.

            var hand = HSceneInterpreter.handCtrl;

            int bodyPartId = (int)colliderKind - 2;
            var usedItem = hand.useAreaItems[bodyPartId];
            if (usedItem != null && usedItem.idUse != 2)
            {
                hand.DetachItemByUseItem(usedItem.idUse);
            }
            hand.areaItem[bodyPartId] = layerNum;

            hand.selectKindTouch = colliderKind;
            yield return CaressUtil.ClickCo();
            yield return new WaitUntil(() => !CrossFader.IsInTransition);

            _mousePress = true;
            HandCtrlHooks.InjectMouseButtonDown(0);
            var timer = Time.time + 3f;
            while (!GameCursor.isLock || HSceneInterpreter.handCtrl.GetUseAreaItemActive() == -1)
            {
                hand.selectKindTouch = colliderKind;
                if (timer < Time.time)
                {
                    Halt();
                }
                yield return null;
            }
            HSceneInterpreter.MoMiOnLickStart(colliderKind);
            HSceneInterpreter.EnableNip(colliderKind);
        }

        private IEnumerator AttachCo(AibuColliderKind colliderKind)
        {
            _activeCo = true;
            VRPlugin.Logger.LogDebug($"MouthGuide:AttachCo:Start");
            var origin = VR.Camera.SteamCam.origin;
            var head = VR.Camera.SteamCam.head;

            var dic = PoI[colliderKind];
            var lookAt = _lastChara.objBodyBone.transform.Find(dic.path);
            _lookAt = lookAt;
            var prevLookAt = lookAt.position;
            var hand = HSceneInterpreter.handCtrl;
            _lookOffsetPos = lookAt.InverseTransformPoint(head.position + head.forward * Vector3.Distance(lookAt.position, head.position));
            while (hand.useItems[2] == null)
            {
                // Wait for item - phase.
                // We move together with the point of interest during "Touch" animation.
                origin.position += lookAt.position - prevLookAt;// * 1.5f;
                prevLookAt = lookAt.position;
                yield return new WaitForEndOfFrame();
                if (HSceneInterpreter.hFlag.isDenialvoiceWait)
                {
                    // There is a proper kill switch for bad states now, this shouldn't be necessary.
                    Halt();
                    yield break;
                }
            }
            // Actual attachment point.
            var tongue = hand.useItems[2].obj.transform.Find("cf_j_tangroot");

            // Reference point to update offsets on demand.
            _followAfter = tongue;

            //_offsetPos = new Vector3(0f, dic.itemOffsetUp, dic.itemOffsetForward);
            //_followOffsetPos = tongue.InverseTransformPoint(head.position);

            // Use sampled offset together with custom tongue once implemented.

            _followOffsetPos = new Vector3(0f, dic.itemOffsetUp, dic.itemOffsetForward);
            _followOffsetRot = Quaternion.Inverse(tongue.transform.rotation) * head.rotation;
            //var lookAtOffset = new Vector3(0f, dic.poiOffsetUp, 0f);
            var oldTonguePos = tongue.position;
            while (true)
            {
                // Engage phase.
                // Get close to the tongue and wait for '_Touch' animation to end while also mimicking tongue movements.

                var adjTongue = tongue.TransformPoint(_followOffsetPos);
                var moveTo = Vector3.MoveTowards(head.position, adjTongue, Time.deltaTime * 0.2f);
                origin.rotation = Quaternion.RotateTowards(origin.rotation, Quaternion.LookRotation(lookAt.TransformPoint(_lookOffsetPos) - moveTo), Time.deltaTime * 45f);
                origin.position += (moveTo - head.position) + (tongue.position - oldTonguePos);
                if (_gripMove || (!HSceneInterpreter.IsTouch && Vector3.Distance(adjTongue, head.position) < 0.002f))
                {
                    break;
                }
                oldTonguePos = tongue.position;
                yield return new WaitForEndOfFrame();
            }
            while (true)
            {
                if (_gripMove)
                {

                }
                else
                {
                    var targetPos = tongue.TransformPoint(_followOffsetPos);
                    var moveTo = Vector3.MoveTowards(head.position, targetPos, Time.deltaTime * 0.05f);

                    origin.rotation = Quaternion.RotateTowards(origin.rotation, Quaternion.LookRotation(lookAt.TransformPoint(_lookOffsetPos) - moveTo), Time.deltaTime * 15f);
                    origin.position += (moveTo - head.position);
                }
                yield return new WaitForEndOfFrame();
            }
        }

        internal IEnumerator DisengageCo()
        {
            VRPlugin.Logger.LogDebug($"Mouth:Disengage:Start");

            _activeCo = true;
            _disengage = true;
            var origin = VR.Camera.Origin;
            var head = VR.Camera.Head;
            yield return new WaitUntil(() => !_gripMove);
            yield return new WaitForEndOfFrame();

            // Get away first if we are too close. With active pov, look for impersonation target also.
            var lookAt = (_lookAt == null ? head : _lookAt).position;
            if (Vector3.Distance(lookAt, head.position) < 0.25f)
            {
                var addUp = 0f;
                if (PoV.Active)
                {
                    // We find height relationship between PoV target's eyes and partner's,
                    // to determine if we want to move up before flying to head, so that camera doesn't clip body parts.
                    // i.e. if our impersonation target is higher, we go considerably Up first, otherwise we go straight towards.
                    addUp = PoV.Target.objHeadBone.transform.position.y - _lastChara.objHeadBone.transform.position.y > 0.25f ? (1f * (Time.deltaTime * 0.5f)) : 0f;
                }
                while (Vector3.Distance(lookAt, head.position) < 0.3f)
                {
                    var newPos = head.TransformPoint(0f, addUp, -(Time.deltaTime * 0.15f));
                    origin.rotation = Quaternion.RotateTowards(origin.rotation, Quaternion.Euler(origin.eulerAngles.x, origin.eulerAngles.y, 0f), Time.deltaTime * 45f);
                    origin.position += newPos - head.position;
                    yield return new WaitForEndOfFrame();
                }
            }
            if (PoV.Active)
            {

            }
            else
            {
                // Return X && Z rotations to ~ 0.
                VRPlugin.Logger.LogDebug($"MouthGuide:Disengage:Rotate");
                if (Math.Abs(Mathf.DeltaAngle(origin.eulerAngles.x, 0f)) < 45f)
                {
                    // What unity's internal workings deem equal, script doesn't.
                    while (Mathf.Abs(origin.eulerAngles.z) > 0.1f || Mathf.Abs(origin.eulerAngles.x) > 0.1f)
                    {
                        var oldHeadPos = head.position;
                        origin.rotation = Quaternion.RotateTowards(origin.rotation, Quaternion.Euler(0f, origin.eulerAngles.y, 0f), Time.deltaTime * 45f);
                        origin.position += oldHeadPos - head.position;
                        yield return new WaitForEndOfFrame();
                    }
                }
                else
                {
                    // Return Z rotations to ~ 0. If X is too high then it's probably desirable.
                    while (Mathf.Abs(origin.eulerAngles.z) > 0.1f)
                    {
                        var oldHeadPos = head.position;
                        var lookRot = Quaternion.LookRotation(lookAt - head.position);
                        origin.rotation = Quaternion.RotateTowards(origin.rotation, Quaternion.Euler(lookRot.eulerAngles.x, origin.eulerAngles.y, 0f), Time.deltaTime * 45f);
                        origin.position += oldHeadPos - head.position;
                        //if (Mathf.Abs(origin.eulerAngles.z) < 0.1f) // && Mathf.Abs(origin.eulerAngles.x - lookRot.eulerAngles.x) < 0.1f)
                        //{
                        //    break;
                        //}
                        yield return new WaitForEndOfFrame();
                    }
                }
            }
            VRPlugin.Logger.LogDebug($"MouthGuide:Disengage:End");
            _activeCo = false;
            _disengage = false;
        }
        private void DoReaction()
        {

        }
        internal void Halt(bool disengage = true)
        {
            VRPlugin.Logger.LogDebug($"MouthGuide:Halt:Disengage = {disengage}");//\n{new StackTrace(0)}");

            if (_activeCo)
            {
                StopAllCoroutines();
                HSceneInterpreter.MoMiOnKissEnd();
                _activeCo = false;
                _disengage = false;
                _followRotation = false;
                HSceneInterpreter.handCtrl.DetachItemByUseItem(2);
                HSceneInterpreter.handCtrl.selectKindTouch = AibuColliderKind.none;
                UnlazyGripMove();
            }
            if (_mousePress)
            {
                HandCtrlHooks.InjectMouseButtonUp(0);
                _mousePress = false;
            }
            if (disengage)
            {
                StartCoroutine(DisengageCo());
            }
        }

        private void DestroyGrab()
        {
            foreach (var hand in _hands)
            {
                hand.Tool.DestroyGrab();
            }
        }
        private void UnlazyGripMove()
        {
            foreach (var hand in _hands)
            {
                hand.Tool.UnlazyGripMove();
            }
        }


        // About to obsolete in favor of dynamic offsets and bootleg tongue.
        class LickItem
        {
            public string path;
            public float itemOffsetForward;
            public float itemOffsetUp;
            public float poiOffsetUp;
            public float directionUp;
            public float directionForward;
        }
        //private readonly List<string> _lookAtList =
        //    [

        //    ]

        private readonly Dictionary<AibuColliderKind, LickItem> PoI = new()
        {
            // There are inconsistencies depending on the pose. Not fixed: ass, anal.
            {
                HandCtrl.AibuColliderKind.muneL, new LickItem {
                path = "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_bust00/cf_s_bust00_L/cf_d_bust01_L" +
                    "/cf_j_bust01_L/cf_d_bust02_L/cf_j_bust02_L/cf_d_bust03_L/cf_j_bust03_L/cf_s_bust03_L/k_f_mune03L_02",
                    itemOffsetForward = 0.08f,
                    itemOffsetUp = 0f,
                    poiOffsetUp = 0.05f,
                    directionUp = 1f,
                    directionForward = 0f
                }
            },
            {
                HandCtrl.AibuColliderKind.muneR, new LickItem {
                path = "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_bust00/cf_s_bust00_R/cf_d_bust01_R" +
                    "/cf_j_bust01_R/cf_d_bust02_R/cf_j_bust02_R/cf_d_bust03_R/cf_j_bust03_R/cf_s_bust03_R/k_f_mune03R_02",
                    itemOffsetForward = 0.08f,
                    itemOffsetUp = 0f,
                    poiOffsetUp = 0.05f,
                    directionUp = 1f,
                    directionForward = 0f
                }
            },
            {
                HandCtrl.AibuColliderKind.kokan, new LickItem {
                path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_s_waist02/k_f_kosi02_02",
                    itemOffsetForward = 0.06f,
                    itemOffsetUp = 0.03f,
                    poiOffsetUp = 0f,
                    directionUp = 0.5f,
                    directionForward = 0.5f
                }
            },
            {
                HandCtrl.AibuColliderKind.anal, new LickItem {
                path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_s_waist02/k_f_kosi02_02",
                    itemOffsetForward = -0.05f,// -0.05f,
                    itemOffsetUp = -0.08f,
                    poiOffsetUp = 0f,
                    directionUp = 1f,
                    directionForward = 0f
                }
            },
            {
                HandCtrl.AibuColliderKind.siriL, new LickItem {
                path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/aibu_hit_siri_L",
                    itemOffsetForward = -0.08f, // -0.04f
                    itemOffsetUp = 0f,//0.04f,
                    poiOffsetUp = 0.2f,
                    directionUp = 1f,
                    directionForward = 0f
                }
            },
            {
                AibuColliderKind.siriR, new LickItem {
                path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/aibu_hit_siri_R",
                    itemOffsetForward = -0.08f,// -0.04f
                    itemOffsetUp = 0f,//0.04f,
                    poiOffsetUp = 0.2f,
                    directionUp = 1f,
                    directionForward = 0f
                }
            },
            {
                AibuColliderKind.none, new LickItem {
                path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_s_waist02/k_f_kosi02_02",
                    itemOffsetForward = -0.07f,
                    itemOffsetUp = -0.01f,
                    poiOffsetUp = 0f,
                    directionUp = 0f,
                    directionForward = -1f
                }
            },
        };

    }
}
