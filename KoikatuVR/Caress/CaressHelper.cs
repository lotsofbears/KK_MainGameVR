using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static Illusion.Game.Utils.Scene;
using static ItemObject;
using Random = UnityEngine.Random;
using VRGIN.Core;
using Manager;
using Illusion.Game.Elements.EasyLoader;
using StrayTech;
using System.Reflection.Emit;
using static Valve.VR.EVRButtonId;
using VRGIN.Controls;
using Valve.VR;
using NodeCanvas.Tasks.Actions;
using static UnityStandardAssets.CinematicEffects.Bloom;
using ADV.Commands.Base;
using System.Diagnostics;
using KK_VR.Caress;
using KK_VR.Features;
using KK_VR.Settings;
using KK_VR;
using static SteamVR_Controller.ButtonMask;

namespace KK_VR.Caress
{
    /// <summary>
    /// Helps to stay where one would like to stay.
    /// </summary>
    internal class CaressHelper : MonoBehaviour
    {
        // Make proper height measurement in VRMover.
        class LickItem
        {
            public string path;
            public float itemOffsetForward;
            public float itemOffsetUp;
            public float poiOffsetUp;
            public float directionUp;
            public float directionForward;
        }

        internal static CaressHelper Instance;
        internal static Vector2 FakeDragLength;

        private bool _kissCo;
        private bool _lickCo;
        private bool _endKissCo;
        private bool _sensibleH;
        private bool _mousePressDown;
        //private float _heightDude;

        private List<Harmony> _activePatches = new List<Harmony>();
        private List<Coroutine> _activeCoroutines = new List<Coroutine>();

        private Transform _eyes;
        private Transform _head;
        private Transform _neck;
        private Transform _maleEyes;
        private Transform _shoulders;
        private Transform _latestPoi;

        private HandCtrl _handCtrl;
        private HFlag _hFlag;
        private ChaControl _chara;

        private Controller _device;
        private Controller _device1;
        private KoikatuSettings _settings;

        private Action<HandCtrl.AibuColliderKind> MoMiOnLickStart;
        private Action<HandCtrl.AibuColliderKind> MoMiOnKissStart;
        private Action MoMiOnKissEnd;

        /// <summary>
        /// State of disengagement of camera from action.
        /// </summary>
        public bool IsEndKissCo => _endKissCo;
        private float GetFpsDelta => Time.deltaTime * 60f;
        internal bool IsTouch => _hFlag.nowAnimStateName.EndsWith("Touch", StringComparison.Ordinal);
        internal void Initialize(HSceneProc proc, Type type)
        {
            Instance = this;

            _hFlag = Traverse.Create(proc).Field("flags").GetValue<HFlag>();
            _device = FindObjectOfType<Controller>();
            _device1 = _device.Other;
            _settings = VR.Context.Settings as KoikatuSettings;
            if (type != null)
            {
                _sensibleH = true;
                //VRLog.Debug($"CaressHelper:Start[type = {type}]");
                var lickCo = AccessTools.FirstMethod(type, m => m.Name.Equals("OnLickStart"));
                var kissStart = AccessTools.FirstMethod(type, m => m.Name.Equals("OnKissStart"));
                var kissEnd = AccessTools.FirstMethod(type, m => m.Name.Equals("OnKissEnd"));
                MoMiOnLickStart = AccessTools.MethodDelegate<Action<HandCtrl.AibuColliderKind>>(lickCo);
                MoMiOnKissStart = AccessTools.MethodDelegate<Action<HandCtrl.AibuColliderKind>>(kissStart);
                MoMiOnKissEnd = AccessTools.MethodDelegate<Action>(kissEnd);
            }

            _handCtrl = Traverse.Create(proc).Field("hand").GetValue<HandCtrl>();
            _chara = Traverse.Create(proc).Field("lstFemale").GetValue<List<ChaControl>>().FirstOrDefault();
            _eyes = _chara.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz/cf_J_Eye_tz");
            _head = _chara.objBodyBone.transform.Find("cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_j_neck/cf_j_head");
            _neck = _chara.objBodyBone.transform.Find("cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_j_neck");
            _shoulders = _chara.objBodyBone.transform.Find("cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_backsk_00");

            var dude = Traverse.Create(proc).Field("male").GetValue<ChaControl>();
            _maleEyes = dude.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz");

        }
        private void OnDestroy()
        {
            //foreach (var coroutine in _activeCoroutines)
            //{
            //    if (coroutine != null)
            //        StopCoroutine(coroutine);
            //}
            StopAllCoroutines();
            foreach (var patch in _activePatches)
            {
                patch.UnpatchSelf();
            }
        }
        public bool IsGripPress() => _device.Input.GetPress(Grip) || _device1.Input.GetPress(Grip);
        public bool IsTriggerPressDown() => _device.Input.GetPressDown(Trigger) || _device1.Input.GetPressDown(Trigger);
        internal void Halt(bool disengage = true, bool haltVRMouth = true)
        {
            //VRPlugin.Logger.LogDebug($"CaressHelper:Halt\n" +
            //    $"{new StackTrace(0)}");
            StopAllCoroutines();
            //foreach (var coroutine in _activeCoroutines)
            //{
            //    if (coroutine != null)
            //        StopCoroutine(coroutine);
            //}
            foreach (var patch in _activePatches)
            {
                patch.UnpatchSelf();
            }
            _activeCoroutines.Clear();
            _activePatches.Clear();
            if (_sensibleH)
            {
                if (_kissCo || _lickCo)
                {
                    MoMiOnKissEnd();
                }
                if (_mousePressDown)
                {
                    HandCtrlHooks.InjectMouseButtonUp(0);
                    _mousePressDown = false;
                }
            }
            if (disengage)
            {
                //if (!_endKissCo)// && (_kissCo || _lickCo))
                //{
                _activeCoroutines.Add(StartCoroutine(EndKissCo()));
                // }
                // else
                // {
                //     _endKissCo = false;
                //     VRMouth.NoActionAllowed = false;
                //}
            }
            else
            {
                _endKissCo = false;
            }
            if (haltVRMouth)
            {
                VRMouth.Instance.StopLick();
                VRMouth.Instance.StopKiss();
            }
            _kissCo = false;
            _lickCo = false;
            VRMouth.NoActionAllowed = false;
        }
        internal void OnLickStart(HandCtrl.AibuColliderKind colliderKind)
        {
            VRPlugin.Logger.LogDebug($"CaressHelper:OnLickStart");
            Halt(disengage: false, haltVRMouth: false);
            if (_sensibleH)
            {
                _activeCoroutines.Add(StartCoroutine(LickCoEx(colliderKind)));
                VRMouth.NoActionAllowed = true;
            }
            _activeCoroutines.Add(StartCoroutine(AttachCo(colliderKind)));
        }

        internal void OnKissStart(HandCtrl.AibuColliderKind colliderKind)
        {
            // There are preparations to be done on SensibleH side, so we call it before click and after click.

            if (colliderKind == HandCtrl.AibuColliderKind.none)
            {
                VRPlugin.Logger.LogDebug($"CaressHelper:OnKissStart[1]");
                Halt(disengage: false, haltVRMouth: false);
                if (_sensibleH)
                {
                    MoMiOnKissStart(colliderKind);
                }
            }
            else
            {
                _kissCo = true;
                VRPlugin.Logger.LogDebug($"CaressHelper:OnKissStart[2]");
                if (_sensibleH)
                {
                    MoMiOnKissStart(colliderKind);
                    VRMouth.NoActionAllowed = true;
                }
                _activeCoroutines.Add(StartCoroutine(KissCoEx()));
            }
        }

        private IEnumerator LickCoEx(HandCtrl.AibuColliderKind colliderKind)
        {
            // There might be a bit of preparations to be done, so we call it before click and after click.
            MoMiOnLickStart(HandCtrl.AibuColliderKind.none);
            //yield return CaressUtil.ClickCo();
            //yield return new WaitUntil(() => !IsTouch);
            //yield return CaressUtil.ClickCo();
            //yield return new WaitForSeconds(0.2f);

            yield return CaressUtil.ClickCo();
            yield return new WaitUntil(() => !CrossFader.IsInTransition);

            //yield return CaressUtil.ClickCo();
            _mousePressDown = true;
            HandCtrlHooks.InjectMouseButtonDown(0);
            var timer = Time.time + 3f;
            while (!GameCursor.isLock || _handCtrl.actionUseItem == -1)
            {
                if (timer < Time.time)
                {
                    Halt();
                }
                yield return null;
            }
            MoMiOnLickStart(colliderKind);
        }
        /// <summary>
        /// Moves to and keeps camera around the mouth area.
        /// It's yet to be restored to work properly without SensibleH.
        /// </summary>
        private IEnumerator KissCoEx()
        {
            VRPlugin.Logger.LogDebug($"CaressHelper:KissCoEx[Start]");
            yield return new WaitForEndOfFrame();
            _kissCo = true;
            _latestPoi = _eyes;
            if (!_hFlag.nowAnimStateName.EndsWith("Loop", StringComparison.Ordinal))
            {
                // That is we don't have active sonyu(houshi?) loop. Aibu kiss will be in "Touch" at this moment.
                // And active sonyu loops have their own voices for kiss, so no need to patch drag to get voice.
                // We still patch it in idle sonyu though. 
                _activePatches.Add(Harmony.CreateAndPatchAll(typeof(PatchHandCtrlKiss)));
            }
            _activePatches.Add(Harmony.CreateAndPatchAll(typeof(PatchSteamVR)));
            var origin = VR.Camera.Origin;
            var head = VR.Camera.Head;

            // In light of recent rework, whole "FindRoll" function is busted.
            var rollDelta = FindRollDelta();
            if (Math.Abs(rollDelta) < 5f)
            {
                //var signedAngle = SignedAngle(head.position - _eyes.position, _eyes.forward, _eyes.up);
                var signedAngle = SignedAngle(head.position - _eyes.position, _eyes.forward, _eyes.up);
                if (Math.Abs(signedAngle) < 10f)
                {
                    rollDelta = 20f * (Random.value > 0.5f ? 1 : -1);
                    if (_hFlag.mode == HFlag.EMode.aibu)
                        rollDelta *= Random.value * 2f;

                    VRPlugin.Logger.LogDebug($"CaressHelper:KissCoEx[Random = {rollDelta}]");
                }
                else
                {
                    VRPlugin.Logger.LogDebug($"CaressHelper:KissCoEx[SignedAngle = {signedAngle}]");
                    rollDelta = signedAngle;
                }
            }
            else
            {
                VRPlugin.Logger.LogDebug($"CaressHelper:KissCoEx[RollDelta = {rollDelta}]");
            }


            // We look for rotations of our headset relative to the girl' head and adjust position.

            var angleModRight = rollDelta * 0.0111f;//  /90f;
            var absModRight = Mathf.Abs(angleModRight);
            var angleModUp = 1f - absModRight;
            if (absModRight > 1f)
                angleModRight = absModRight - (angleModRight - absModRight);


            var offsetRight = angleModRight * 0.0667f; // 15f; // 25f
            var offsetForward = _settings.ProximityDuringKiss;
            var offsetUp = -0.04f - (Math.Abs(offsetRight) * 0.5f);
            var startDistance = Vector3.Distance(_eyes.position, head.position) - offsetForward;
            var timer = Time.time + 3f;

            // Placeholder.
            // Change this one to something more interesting.
            FakeDragLength = Vector2.one;
            var oldEyePos = _eyes.position;
            while (timer > Time.time)
            {
                // Simple MoveTowards + added delta of head movement from previous frame.
                // With newest neck looks very good.
                // Alternative neck stays in SensibleH, functional differs way too much.
                // Without it, this method sucks, I'll bring other back in for compatibility later.

                //var adjustedEyes = _eyes.position + (_eyes.up * offsetUp) + (_eyes.right * offsetRight);
                var targetPos = _eyes.TransformPoint(new Vector3(offsetRight, offsetUp, offsetForward));// adjustedEyes + _eyes.forward * offsetForward;

                var deltaEyesPos = _eyes.position - oldEyePos;
                oldEyePos = _eyes.position;

                var moveTowards = Vector3.MoveTowards(head.position, targetPos, Time.deltaTime * 0.05f);
                var lookRotation = Quaternion.LookRotation(_eyes.TransformPoint(new Vector3(offsetRight, 0f, 0f)) - moveTowards, (_eyes.up * angleModUp) + (_eyes.right * angleModRight)); // + _eyes.forward * -0.1f);
                origin.rotation = Quaternion.RotateTowards(origin.rotation, lookRotation, Time.deltaTime * 60f);
                origin.position += moveTowards + deltaEyesPos - head.position;
                yield return new WaitForEndOfFrame();
            }

            VRPlugin.Logger.LogDebug($"CaressHelper:KissCoEx[Unpatch]");
            var lastElement = _activePatches.Count - 1;
            _activePatches[lastElement].UnpatchSelf();
            _activePatches.RemoveAt(lastElement);
            while (true)
            {
                if (IsGripPress())
                {
                    if (Vector3.Distance(_eyes.position, head.position) > 0.18f)
                    {
                        Halt();
                        yield break;
                    }
                }
                else if (IsTriggerPressDown())
                {
                    Halt();
                    yield break;
                }
                else
                {
                    //var stepMod = 0.5f + Random.value * 0.5f;
                    var deltaEyesPos = _eyes.position - oldEyePos;
                    oldEyePos = _eyes.position;
                    var targetPos = _eyes.TransformPoint(new Vector3(offsetRight, offsetUp, offsetForward));
                    var moveTowards = Vector3.MoveTowards(head.position, targetPos, Time.deltaTime * 0.05f);
                    //var lookRotation = Quaternion.LookRotation(_eyes.TransformPoint(new Vector3(offsetRight, 0f, 0f)) - moveTowards, (_eyes.up * angleModUp) + (_eyes.right * angleModRight)); // + _eyes.forward * -0.1f);
                    //origin.rotation = Quaternion.RotateTowards(origin.rotation, lookRotation, Time.deltaTime * 15f);
                    origin.position += moveTowards + (deltaEyesPos) - head.position;
                    //origin.position += head.position + (deltaEyesPos) - head.position;
                }
                yield return new WaitForEndOfFrame();
            }
        }
        /// <summary>
        /// Properly disengages the player from VR actions. Possibly leaves the player not familiar with "Grip Move" hanging, that is being in weird X-axis rotation.
        /// </summary>
        internal IEnumerator EndKissCo()
        {
            VRPlugin.Logger.LogDebug($"CaressHelper:EndKissCo[Start]");
            _endKissCo = true;
            VRMouth.Instance.OnDisengageStart();
            var origin = VR.Camera.Origin;
            var head = VR.Camera.Head;
            var pov = PoV.Instance != null && PoV.Instance.Active;
            if (IsGripPress())
            {
                yield return new WaitUntil(() => !IsGripPress());
                yield return new WaitForEndOfFrame();
            }

            // Get away first if we are too close. Different for active pov.
            var attachPosition = _latestPoi != null ? _latestPoi.position : head.position;
            if (Vector3.Distance(attachPosition, head.position) < 0.25f)
            {
                VRPlugin.Logger.LogDebug($"CaressHelper:EndKissCo[Disengage]");
                var step = Time.deltaTime * 0.15f; //0.0034f * delta;
                var upVec = Vector3.zero;
                if (pov && _maleEyes != null)
                {
                    upVec = _maleEyes.position.y - _eyes.position.y > 0.25f ? (Vector3.up * (step * 3f)) : Vector3.zero;
                }
                while (Vector3.Distance(attachPosition, head.position) < 0.3f)
                {
                    if (IsGripPress())
                    {
                        break;
                    }
                    var newPos = head.TransformPoint(0f, 0f, -step) + upVec;
                    origin.rotation = Quaternion.RotateTowards(origin.rotation, Quaternion.Euler(origin.eulerAngles.x, origin.eulerAngles.y, 0f), GetFpsDelta);
                    origin.position += newPos - head.position;
                    yield return new WaitForEndOfFrame();
                }
                //}
                //else
                //{
                //    while (Vector3.Distance(_latestPoi.position, head.position) < 0.3f)
                //    {
                //        var newPos = head.TransformPoint(0f, 0f, -step);
                //        origin.rotation = Quaternion.RotateTowards(origin.rotation, Quaternion.Euler(origin.eulerAngles.x, origin.eulerAngles.y, 0f), GetFpsDelta);
                //        origin.position += newPos - head.position;
                //        yield return new WaitForEndOfFrame();
                //    }
                //}
            }
            if (!pov)
            {
                VRPlugin.Logger.LogDebug($"CaressHelper:EndKissCo[Rotate]");
                if (Math.Abs(Mathf.DeltaAngle(origin.eulerAngles.x, 0f)) < 45f)
                {
                    while (Mathf.Abs(origin.eulerAngles.z) > 0.1f || Mathf.Abs(origin.eulerAngles.x) > 0.1f)
                    {
                        if (IsGripPress())
                        {
                            break;
                        }
                        var oldHeadPos = head.position;
                        origin.rotation = Quaternion.RotateTowards(origin.rotation, Quaternion.Euler(0f, origin.eulerAngles.y, 0f), GetFpsDelta);
                        origin.position += oldHeadPos - head.position;
                        yield return new WaitForEndOfFrame();
                    }
                }
                else
                {
                    while (true)
                    {
                        if (IsGripPress())
                        {
                            break;
                        }
                        var oldHeadPos = head.position;
                        var lookAt = Quaternion.LookRotation(attachPosition - head.position);
                        origin.rotation = Quaternion.RotateTowards(origin.rotation, Quaternion.Euler(lookAt.eulerAngles.x, origin.eulerAngles.y, 0f), GetFpsDelta);
                        origin.position += oldHeadPos - head.position;

                        if (Mathf.Abs(origin.eulerAngles.z) < 0.1f && Mathf.Abs(origin.eulerAngles.x - lookAt.eulerAngles.x) < 0.1f)
                        {
                            break;
                        }

                        yield return new WaitForEndOfFrame();
                    }
                }
            }
            VRPlugin.Logger.LogDebug($"CaressHelper:EndKissCo[End]");
            _endKissCo = false;
            _handCtrl.DetachAllItem();
        }
        /// <summary>
        /// Partner in crime of LickCo.
        /// TODO Centering of camera in sonyu, so it looks more plausible.
        /// </summary>
        private IEnumerator AttachCo(HandCtrl.AibuColliderKind colliderKind)
        {
            VRPlugin.Logger.LogDebug($"CaressHelper:AttachCo[Start]");
            // Making setting for distance of camera on this is way too much due to heavy inconsistencies in ALL positions.
            // And the process of personal adjustment wouldn't be something user friendly at all.
            _lickCo = true;
            var dic = PoI[colliderKind];
            var poi = _chara.objBodyBone.transform.Find(dic.path);

            var origin = VR.Camera.Origin;
            var head = VR.Camera.Head;

            var prevPoiPosition = poi.position;
            // Take grip away from player.
            _activePatches.Add(Harmony.CreateAndPatchAll(typeof(PatchSteamVR)));
            while (IsTouch || _handCtrl.useItems[2] == null)
            {
                // We move together with the point of interest during "Touch" animation.
                origin.position += (poi.position - prevPoiPosition) * 1.5f;
                prevPoiPosition = poi.position;
                yield return new WaitForEndOfFrame();
                if (_hFlag.isDenialvoiceWait)
                {
                    // There is a proper kill switch for bad states now, this shouldn't be necessary.
                    Halt();
                    yield break;
                }
            }
            _activePatches[_activePatches.Count - 1].UnpatchSelf();

            // Actual attachment point.
            var item = _handCtrl.useItems[2].obj.transform.Find("cf_j_tangroot");
            _latestPoi = _handCtrl.useItems[2].obj.transform.parent;

            //SensibleH.Logger.LogDebug($"AttachCo[Start] {poi.rotation.eulerAngles.x}");
            if (colliderKind == HandCtrl.AibuColliderKind.kokan && poi.rotation.eulerAngles.x > 30f && poi.rotation.eulerAngles.x < 90f)
            {
                // Checks if the girl is on all fours.
                // Special setup for it. 
                // TODO An easier way with height check?
                dic = PoI[HandCtrl.AibuColliderKind.none];
            }
            VRPlugin.Logger.LogDebug($"CaressHelper:AttachCo[type = {PoI.Where(k => k.Value == dic).FirstOrDefault().Key}");
            while (true)
            {
                if (IsTriggerPressDown())
                {
                    Halt();
                }
                var adjustedItem = item.TransformPoint(new Vector3(0f, dic.itemOffsetUp, dic.itemOffsetForward));
                var moveTo = Vector3.MoveTowards(head.position, adjustedItem, Time.deltaTime * 0.2f);
                var lookAt = Quaternion.LookRotation(poi.TransformPoint(new Vector3(0f, dic.poiOffsetUp, 0f)) - moveTo, poi.up * dic.directionUp + poi.forward * dic.directionForward);
                origin.rotation = Quaternion.RotateTowards(origin.rotation, lookAt, Time.deltaTime * 60f);
                origin.position += moveTo - head.position;
                if (Vector3.Distance(adjustedItem, head.position) < 0.002f)
                {
                    break;
                }
                yield return new WaitForEndOfFrame();
            }
            //var oldPos = Vector3.zero;
            //var touchStartFrame = false;
            while (true)
            {
                if (IsTriggerPressDown())
                {
                    break;
                }
                else if (IsGripPress())
                {
                    if (IsTriggerPressDown())
                    {
                        Halt(disengage: false);
                        yield break;
                    }
                    if (Vector3.Distance(poi.position, head.position) > 0.15f)
                    {
                        break;
                    }
                }
                else
                {
                    var targetPos = item.TransformPoint(new Vector3(0f, dic.itemOffsetUp, dic.itemOffsetForward));
                    var moveTo = Vector3.MoveTowards(head.position, targetPos, Time.deltaTime * 0.05f);
                    var lookAt = Quaternion.LookRotation(poi.TransformPoint(new Vector3(0f, dic.poiOffsetUp, 0f)) - moveTo, poi.up * dic.directionUp + poi.forward * dic.directionForward);

                    // Looks better without compensation for Touch animation.
                    //if (IsTouch)
                    //{
                    //    if (!touchStartFrame)
                    //    {
                    //        oldPos = poi.position;
                    //        touchStartFrame = true;
                    //    }
                    //    else
                    //    {
                    //        moveTo += poi.position - oldPos;
                    //        oldPos = poi.position;
                    //    }

                    //}
                    //else
                    //{
                    //    touchStartFrame = false;
                    //}

                    origin.rotation = Quaternion.RotateTowards(origin.rotation, lookAt, Time.deltaTime * 15f);
                    origin.position += moveTo - head.position;
                }
                yield return new WaitForEndOfFrame();
            }
            Halt();
            //SensibleH.Logger.LogDebug($"AttachCo[End]");
        }
        /*
         * cf_j_tangroot.transform.
         *     forward+ is (All subsequent measurements are done relative to the girl)
         *         boobs - vec.up
         *         ass - vec.down
         *         vag - vec.up
         *         anal - vec.forward 
         *     up+ is (All subsequent measurements are done relative to the girl)
         *         boobs - vec.forward
         *         ass - vec.backward
         *         vag - vec.forward
         *         anal - vec.down
         */
        private Dictionary<HandCtrl.AibuColliderKind, LickItem> PoI = new Dictionary<HandCtrl.AibuColliderKind, LickItem>()
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
                HandCtrl.AibuColliderKind.siriR, new LickItem {
                path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/aibu_hit_siri_R",
                    itemOffsetForward = -0.08f,// -0.04f
                    itemOffsetUp = 0f,//0.04f,
                    poiOffsetUp = 0.2f,
                    directionUp = 1f,
                    directionForward = 0f
                }
            },
            {
                HandCtrl.AibuColliderKind.none, new LickItem {
                path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_s_waist02/k_f_kosi02_02",
                    itemOffsetForward = -0.07f,
                    itemOffsetUp = -0.01f,
                    poiOffsetUp = 0f,
                    directionUp = 0f,
                    directionForward = -1f
                }
            },
        };
        private float FindRollDelta()
        {
            var headsetRoll = Mathf.DeltaAngle(VR.Camera.Head.eulerAngles.z, 0f);
            var headRoll = Mathf.DeltaAngle(_neck.localRotation.eulerAngles.z, 0f) + Mathf.DeltaAngle(_head.localRotation.eulerAngles.z, 0f);

            return Mathf.DeltaAngle(headsetRoll, headRoll);
        }
        private float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
        {
            // After recent rework became a local detractor.
            float unsignedAngle = Vector3.Angle(from, to);

            float cross_x = from.y * to.z - from.z * to.y;
            float cross_y = from.z * to.x - from.x * to.z;
            float cross_z = from.x * to.y - from.y * to.x;
            float sign = Mathf.Sign(axis.x * cross_x + axis.y * cross_y + axis.z * cross_z);
            return unsignedAngle * sign;
        }
    }
    /// <summary>
    /// We catch grip input for a while.
    /// </summary>
    class PatchSteamVR
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SteamVR_Controller.Device), nameof(SteamVR_Controller.Device.GetPress), new Type[] { typeof(ulong) })]
        public static bool GetPressPrefix(ulong buttonMask, ref bool __result)
        {
            if (buttonMask == 4)
            {
                __result = false;
                return false;
            }
            else
                return true;
        }
    }
    //class PatchSteamVR
    //{
    //    [HarmonyPrefix]
    //    [HarmonyPatch(typeof(DeviceLegacyAdapter), nameof(DeviceLegacyAdapter.GetPress), new Type[] { typeof(Valve.VR.EVRButtonId) })]
    //    public static bool GetPressPrefix(EVRButtonId buttonId, ref bool __result)
    //    {
    //        if (buttonId == k_EButton_Grip)
    //        {
    //            __result = false;
    //            return false;
    //        }
    //        else
    //            return true;
    //    }
    //}
    /// <summary>
    /// Because voice during kiss matters.
    /// </summary>
    class PatchHandCtrlKiss
    {
        [HarmonyTranspiler, HarmonyPatch(typeof(HandCtrl), nameof(HandCtrl.DragAction))]
        public static IEnumerable<CodeInstruction> DragActionTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var done = false;
            var found = false;
            var counter = 0;
            foreach (var code in instructions)
            {
                if (!found && code.opcode == OpCodes.Ldflda
                    && code.operand.ToString().Contains("calcDragLength"))
                {
                    found = true;
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(CaressHelper), name: "FakeDragLength"));
                    continue;
                }
                else if (!done && found)
                {
                    if (counter == 0)
                    {
                        counter++;
                        yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(HandCtrl), name: "calcDragLength"));
                        continue;
                    }
                    counter++;
                    yield return new CodeInstruction(OpCodes.Nop);
                    if (counter == 5)
                    {
                        done = true;
                    }
                    continue;
                }
                yield return code;
            }
        }
    }

}
