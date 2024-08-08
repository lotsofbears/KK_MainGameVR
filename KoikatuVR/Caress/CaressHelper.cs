using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static Illusion.Game.Utils.Scene;
using static ItemObject;
using static SteamVR_Controller;
using Random = UnityEngine.Random;
using VRGIN.Core;
using Manager;
using Illusion.Game.Elements.EasyLoader;
using StrayTech;
using System.Reflection.Emit;

namespace KoikatuVR.Caress
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

        /// <summary>
        /// State of disengagement of camera from action.
        /// </summary>
        internal bool _endKissCo;
        internal static CaressHelper Instance;
        internal static Vector2 FakeDragLength;

        private bool _kissCo;
        private bool _lickCo;
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
        private HandCtrl _handCtrl;
        private HFlag _hFlag;
        private ChaControl _chara;
        private SteamVR_Controller.Device _device;
        private SteamVR_Controller.Device _device1;

        private Action MoMiOnLickStart;
        private Action MoMiOnKissStart;
        private Action MoMiOnKissEnd;

        private float GetFpsDelta => Time.deltaTime * 60f;
        private bool IsTouch => _hFlag.nowAnimStateName.EndsWith("Touch", StringComparison.Ordinal);
        internal void Initialize(HSceneProc proc, Type type)
        {
            Instance = this;

            _hFlag = Traverse.Create(proc).Field("flags").GetValue<HFlag>();

            //var type = AccessTools.TypeByName("KK_SensibleH.MoMiController");
            if (type != null)
            {
                _sensibleH = true;
                //VRLog.Debug($"KissHelper[Start] type[{type}]");
                var lickCo = AccessTools.FirstMethod(type, m => m.Name.Equals("OnLickStart"));
                var kissStart = AccessTools.FirstMethod(type, m => m.Name.Equals("OnKissStart"));
                var kissEnd = AccessTools.FirstMethod(type, m => m.Name.Equals("OnKissEnd"));
                MoMiOnLickStart = AccessTools.MethodDelegate<Action>(lickCo);
                MoMiOnKissStart = AccessTools.MethodDelegate<Action>(kissStart);
                MoMiOnKissEnd = AccessTools.MethodDelegate<Action>(kissEnd);
                //VRLog.Debug($"KissHelper[Start] dlgt {MoMiOnLickStart} {MoMiOnKissStart} {MoMiOnKissEnd}");
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
        private void UpdateDevices()
        {
            _device = SteamVR_Controller.Input((int)VR.Mode.Right.Tracking.index);
            _device1 = SteamVR_Controller.Input((int)VR.Mode.Left.Tracking.index);
        }
        internal void Halt(bool disengage = true)
        {
            // On Hold.
            // If we do this while SensibleH spams "JudgeProc()", we'll break HandCtrl, thus we wait (should be up to 0.6 seconds) for drag to start.
            //    yield return new WaitUntil(() => _hand.ctrl != HandCtrl.Ctrl.click);

            VRLog.Debug($"[HaltReason][Button = {UnityEngine.Input.GetMouseButtonDown(0)}] [Item = {_handCtrl.actionUseItem != -1}] [Kiss = {_handCtrl.isKiss}]");
            foreach (var coroutine in _activeCoroutines)
            {
                if (coroutine != null)
                    StopCoroutine(coroutine);
            }
            foreach (var patch in _activePatches)
            {
                patch.UnpatchSelf();
            }
            _activeCoroutines.Clear();
            _activePatches.Clear();
            if (_sensibleH)
            {
                if (_kissCo)
                {
                    MoMiOnKissEnd();
                }
                if (_mousePressDown)
                {
                    HandCtrlHooks.InjectMouseButtonUp(0);
                    _mousePressDown = false;
                }
            }
            if (disengage && (_kissCo || _lickCo))
            {
                _activeCoroutines.Add(StartCoroutine(EndKissCo()));
            }
            else
            {
                VRMouth.NoActionAllowed = false;
            }
            VRMouth._lickCoShouldEnd = null;
            VRMouth._kissCoShouldEnd = null;
            _kissCo = false;
            _lickCo = false;
        }
        internal void OnLickStart(HandCtrl.AibuColliderKind colliderKind)
        {
            if (_sensibleH)
            {
                _activeCoroutines.Add(StartCoroutine(LickCoEx()));
                VRMouth.NoActionAllowed = true;
            }
            _activeCoroutines.Add(StartCoroutine(AttachCo(colliderKind)));
        }


        internal void OnKissStart()
        {
            // There might be a bit of preparations to be done, so we call it before click and after click.
            if (!_kissCo)
            {
                if (_sensibleH)
                {
                    MoMiOnKissStart();
                }
                _kissCo = true;
            }
            else
            {
                if (_sensibleH)
                {
                    MoMiOnKissStart();
                    VRMouth.NoActionAllowed = true;
                }
                _activeCoroutines.Add(StartCoroutine(KissCoEx()));
            }
        }

        private IEnumerator LickCoEx()
        {
            // There might be a bit of preparations to be done, so we call it before click and after click.
            VRLog.Debug("LickCoEx");
            MoMiOnLickStart();
            yield return CaressUtil.ClickCo();
            yield return new WaitUntil(() => !IsTouch);
            //yield return new WaitUntil(() => _handCtrl.actionUseItem == -1);

            _mousePressDown = true;
            HandCtrlHooks.InjectMouseButtonDown(0);

            yield return new WaitUntil(() => GameCursor.isLock);
            yield return new WaitForEndOfFrame();
            MoMiOnLickStart();
        }
        /// <summary>
        /// Moves to and keeps camera around the mouth area.
        /// It's yet to be restored to work properly without SensibleH.
        /// </summary>
        private IEnumerator KissCoEx()
        {
            VRLog.Debug($"KissCo[Start]");
            yield return new WaitForEndOfFrame();
            _kissCo = true;
            _activePatches.Add(Harmony.CreateAndPatchAll(typeof(PatchHandCtrlKiss)));
            _activePatches.Add(Harmony.CreateAndPatchAll(typeof(PatchSteamVR)));
            var origin = VR.Camera.Origin;
            var head = VR.Camera.Head;


            // In light of recent rework, whole "FindRoll" function is busted.
            var rollDelta = FindRollDelta();
            if (Math.Abs(rollDelta) < 5f)
            {
                //var signedAngle = SignedAngle(head.position - _eyes.position, _eyes.forward, _eyes.up);
                var signedAngle = SignedAngle(head.position - _shoulders.position, _shoulders.forward, _shoulders.up);
                if (Math.Abs(signedAngle) < 10f)
                {
                    rollDelta = 25f * (Random.value > 0.5f ? 1 : -1);
                    if (_hFlag.mode == HFlag.EMode.aibu)
                        rollDelta *= Random.value * 2f;

                    //SensibleH.Logger.LogDebug($"KissCo[RandomRoll] Everything else is too small to consider it {rollDelta}");
                }
                else
                    rollDelta = signedAngle;
            }


            // We look for rotations of our headset relative to the girl' head and adjust position.

            var angleModRight = rollDelta * 0.0111f;//  /90f;
            var absModRight = Mathf.Abs(angleModRight);
            var angleModUp = 1f - absModRight;
            if (absModRight > 1f)
                angleModRight = absModRight - (angleModRight - absModRight);


            var offsetRight = angleModRight * 0.0667f; // 15f; // 25f
            var offsetForward = 0.09f;
            var offsetUp = -0.04f - (Math.Abs(offsetRight) * 0.5f);
            var startDistance = Vector3.Distance(_eyes.position, head.position) - offsetForward;
            var timer = Time.time + 3f;

            // Placeholder.
            // Change this one to something more interesting.
            FakeDragLength = Vector2.one * 0.5f;

            var oldEyePos = _eyes.position;
            UpdateDevices();
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

                var moveTowards = Vector3.MoveTowards(head.position, targetPos, Time.deltaTime * 0.07f);
                var lookRotation = Quaternion.LookRotation(_eyes.TransformPoint(new Vector3(offsetRight, 0f, 0f)) - moveTowards, (_eyes.up * angleModUp) + (_eyes.right * angleModRight)); // + _eyes.forward * -0.1f);
                origin.rotation = Quaternion.RotateTowards(origin.rotation, lookRotation, Time.deltaTime * 90f);
                origin.position += moveTowards + deltaEyesPos - head.position;
                yield return new WaitForEndOfFrame();
            }
            //SensibleH.Logger.LogDebug($"KissCo[UnPatch]");
            var lastElement = _activePatches.Count - 1;
            _activePatches[lastElement].UnpatchSelf();
            _activePatches.RemoveAt(lastElement);
            while (true)
            {
                if (_device.GetPress(ButtonMask.Grip) || _device1.GetPress(ButtonMask.Grip))
                {
                    if (Vector3.Distance(_eyes.position, head.position) > 0.2f)
                    {
                        Halt();
                        yield break;
                    }
                }
                else if (_device.GetPressUp(ButtonMask.Trigger) || _device1.GetPressUp(ButtonMask.Trigger))
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
                    var lookRotation = Quaternion.LookRotation(_eyes.TransformPoint(new Vector3(offsetRight, 0f, 0f)) - moveTowards, (_eyes.up * angleModUp) + (_eyes.right * angleModRight)); // + _eyes.forward * -0.1f);
                    origin.rotation = Quaternion.RotateTowards(origin.rotation, lookRotation, Time.deltaTime * 15f);
                    origin.position += moveTowards + deltaEyesPos - head.position;
                }
                yield return new WaitForEndOfFrame();
            }
        }
        /// <summary>
        /// Properly disengages the player from VR actions. Possible leaves the player not familiar with "Grip Move" hanging, that is being in weird X-axis rotation.
        /// </summary>
        internal IEnumerator EndKissCo()
        {
            VRLog.Debug($"EndKissCo[Start]");
            _endKissCo = true;
            var origin = VR.Camera.Origin;
            var head = VR.Camera.Head;
            var pov = POV.Instance != null && POV.Active;
            UpdateDevices();
            if (_device.GetPress(ButtonMask.Grip) || _device1.GetPress(ButtonMask.Grip))
            {
                yield return new WaitUntil(() => !_device.GetPress(ButtonMask.Grip) && !_device1.GetPress(ButtonMask.Grip));
                yield return new WaitForEndOfFrame();
            }
            else
            {
                yield return new WaitForEndOfFrame();
            }
            // Get away first if we are too close. Different for active pov.
            // We only really account for kiss on this one, lick cases don't really care about disengage.
            if (Vector3.Distance(_eyes.position, head.position) < 0.25f)
            {
                //SensibleH.Logger.LogDebug($"EndKissCo[MoveCameraAway][pov = {pov}]");
                var step = Time.deltaTime * 0.13f; //0.0034f * delta;
                if (pov && _maleEyes != null)
                {
                    //SensibleH.Logger.LogDebug($"EndKissCo[PoV]");
                    var upVec = _maleEyes.position.y - _eyes.position.y > 0.3f ? (Vector3.up * (step * 3f)) : Vector3.zero;
                    while (VRMouth._kissCoShouldEnd == false) // _handCtrl.isKiss
                    {
                        var newPos = head.position + (head.forward * -step) + upVec;
                        origin.rotation = Quaternion.RotateTowards(origin.rotation, Quaternion.Euler(origin.eulerAngles.x, origin.eulerAngles.y, 0f), GetFpsDelta);
                        origin.position += newPos - head.position;
                        yield return new WaitForEndOfFrame();
                    }
                }
                else
                {
                    while (Vector3.Distance(_eyes.position, head.position) < 0.3f)
                    {
                        var newPos = head.position + (head.forward * -step);
                        origin.rotation = Quaternion.RotateTowards(origin.rotation, Quaternion.Euler(origin.eulerAngles.x, origin.eulerAngles.y, 0f), GetFpsDelta);
                        origin.position += newPos - head.position;
                        yield return new WaitForEndOfFrame();
                    }
                }
            }
            if (!pov)
            {
                // We return back Roll and Pitch if latter is not too big.
                // Otherwise it's most likely desirable, so we go for girl's eyes (other pois would be a welcome addition).
                if (Math.Abs(Mathf.DeltaAngle(origin.eulerAngles.x, 0f)) < 50f)
                {
                    while ((int)origin.eulerAngles.z != 0 || (int)origin.eulerAngles.x != 0)
                    {
                        if (!_device.GetPress(ButtonMask.Grip) && !_device1.GetPress(ButtonMask.Grip))
                        {
                            var oldHeadPos = head.position;
                            origin.rotation = Quaternion.RotateTowards(origin.rotation, Quaternion.Euler(0f, origin.eulerAngles.y, 0f), GetFpsDelta);
                            origin.position += oldHeadPos - head.position;
                        }
                        yield return new WaitForEndOfFrame();
                    }
                }
                else
                {
                    while (true)
                    {
                        if (!_device.GetPress(ButtonMask.Grip) && !_device1.GetPress(ButtonMask.Grip))
                        {
                            var oldHeadPos = head.position;
                            var lookAt = Quaternion.LookRotation(_eyes.position - head.position);
                            origin.rotation = Quaternion.RotateTowards(origin.rotation, Quaternion.Euler(lookAt.eulerAngles.x, origin.eulerAngles.y, 0f), GetFpsDelta);
                            origin.position += oldHeadPos - head.position;

                            if ((int)origin.eulerAngles.z == 0 && (int)origin.eulerAngles.x == (int)lookAt.eulerAngles.x)
                            {
                                break;
                            }
                        }
                        yield return new WaitForEndOfFrame();
                    }
                }

            }
            if (Random.value < 0.5f)
            {
                _hFlag.click = HFlag.ClickKind.de_muneL;
            }
            VRLog.Debug($"EndKissCo[End] x = {origin.eulerAngles.x} z = {origin.eulerAngles.z}");
            _endKissCo = false;
            _handCtrl.DetachAllItem();
            VRMouth.NoActionAllowed = false;
        }
        /// <summary>
        /// TODO Centering of camera in sonyu, so it looks more plausible.
        /// </summary> 
        /// <summary>
        /// Partner in crime of LickCo.
        /// </summary>
        private IEnumerator AttachCo(HandCtrl.AibuColliderKind colliderKind)
        {
            //SensibleH.Logger.LogDebug($"AttachCo[Start]");
            // We don't always use default parents.
            _lickCo = true;
            var dic = PoI[colliderKind];
            var poi = _chara.objBodyBone.transform.Find(dic.path);

            var origin = VR.Camera.Origin;
            var head = VR.Camera.Head;

            var prevPoiPosition = poi.position;

            while (IsTouch || _handCtrl.useItems[2] == null)
            {
                // We move together with the point of interest during "Touch" animation.
                origin.position += poi.position - prevPoiPosition;
                prevPoiPosition = poi.position;
                yield return new WaitForEndOfFrame();
            }

            // Actual attachment point.
            var item = _handCtrl.useItems[2].obj.transform.Find("cf_j_tangroot");
            //SensibleH.Logger.LogDebug($"AttachCo[Start] {poi.rotation.eulerAngles.x}");
            if (poi.rotation.eulerAngles.x > 30f && poi.rotation.eulerAngles.x < 90f)
            {
                // Checks if the girl is on all fours.
                // Special setup for it. 
                dic = PoI[HandCtrl.AibuColliderKind.none];
            }
            UpdateDevices();
            // Probably due to rework, because it starts to slack way too much with BIG breasts.
            while (true)
            {
                if (_device.GetPressDown(ButtonMask.Trigger) || _device1.GetPressDown(ButtonMask.Trigger))
                {
                    //SensibleH.Logger.LogDebug($"AttachCo[PrematureEnd] no transform/triggers");
                    Halt();
                }
                //SensibleH.Logger.LogDebug($"AttachCo[MoveToItem]");

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
            while (true)
            {
                if (_device.GetPressDown(ButtonMask.Trigger) || _device1.GetPressDown(ButtonMask.Trigger)) //_handCtrl.useItems[2] == null || 
                {
                    break;
                }
                else if (_device.GetPress(ButtonMask.Grip) || _device1.GetPress(ButtonMask.Grip))
                {
                    if (Vector3.Distance(poi.position, head.position) > 0.15f)
                        break;
                }
                else
                {
                    var targetPos = item.TransformPoint(new Vector3(0f, dic.itemOffsetUp, dic.itemOffsetForward));
                    var moveTo = Vector3.MoveTowards(head.position, targetPos, Time.deltaTime * 0.05f);
                    var lookAt = Quaternion.LookRotation(poi.TransformPoint(new Vector3(0f, dic.poiOffsetUp, 0f)) - moveTo, poi.up * dic.directionUp + poi.forward * dic.directionForward);
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
                itemOffsetUp = 0f,//-0.04f, 
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
                itemOffsetForward = -0.05f,//-0.06f, 
                itemOffsetUp = -0.08f, // -0.06f
                    poiOffsetUp = 0f,
                directionUp = 1f,
                directionForward = 0f
                }
            },
            {
                HandCtrl.AibuColliderKind.siriL, new LickItem {
                path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/aibu_hit_siri_L",
                itemOffsetForward = -0.04f, // -0.06f
                itemOffsetUp = 0.04f,
                    poiOffsetUp = 0.2f,
                directionUp = 1f,
                directionForward = 0f
                }
            },
            {
                HandCtrl.AibuColliderKind.siriR, new LickItem {
                path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/aibu_hit_siri_R",
                itemOffsetForward = -0.04f, // -0.06f
                    itemOffsetUp = 0.04f,
                    poiOffsetUp = 0.2f,
                directionUp = 1f,
                directionForward = 0f
                }
            },
            {
                HandCtrl.AibuColliderKind.none, new LickItem {
                path = "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_s_waist02/k_f_kosi02_02",
                itemOffsetForward = -0.07f, // -0.01
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
            // This one brings little to no benefit with current neck states of the kiss.
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
            //VRLog.Error($"PatchHandCtrlKiss");
            //var code = new List<CodeInstruction>(instructions);
            //for (var i = 0; i < code.Count; i++)
            //{
            //    if (code[i].opcode == OpCodes.Ldflda &&
            //        code[i].operand.ToString().Contains("calcDragLength"))
            //    {
            //        code[i].opcode = OpCodes.Ldsfld;
            //        code[i].operand = AccessTools.Field(typeof(CaressHelper), name: "FakeDragLength"); ;
            //        code[i + 1].opcode = OpCodes.Ldc_R4;
            //        code[i + 1].operand = 3f;
            //        code[i + 2].opcode = OpCodes.Call;
            //        code[i + 2].operand = AccessTools.FirstMethod(typeof(Vector2), method => method.Name.Equals("op_Multiply"));
            //        code[i + 3].opcode = OpCodes.Stfld;
            //        code[i + 3].operand = AccessTools.Field(typeof(HandCtrl), name: "calcDragLength");
            //        code[i + 4].opcode = OpCodes.Nop;
            //        code[i + 4].operand = null;
            //        code[i + 5].opcode = OpCodes.Nop;
            //        code[i + 5].operand = null;
            //        break;
            //    }
            //}
            //return code.AsEnumerable();
        }
    }

}
