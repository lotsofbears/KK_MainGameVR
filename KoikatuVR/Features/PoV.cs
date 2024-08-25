using ADV.Commands.Object;
using HarmonyLib;
using Illusion.Game;
using KKAPI.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRGIN.Controls;
using VRGIN.Core;
using VRGIN.Helpers;
using WindowsInput;
using WindowsInput.Native;
using static Valve.VR.EVRButtonId;
using KKAPI;
using UniRx;
using Manager;
using KK_VR.Caress;
using KK_VR.Features;
using KK_VR.Settings;
using KK_VR;
using static SteamVR_Controller.ButtonMask;



namespace KK_VR.Features
{
    public class PoV : ProtectedBehaviour
    {

        public static PoV Instance;
        /// <summary>
        /// girlPOV is NOT set proactively, use "active" to monitor state.
        /// </summary>
        public bool GirlPOV;
        public bool Active;
        private struct PoIPatternInfo
        {
            public string teleportTo;
            public List<string> lookAt;
            public float forwardMin;
            public float forwardMax;
            public float upMin;
            public float upMax;
            public float rightMin;
            public float rightMax;
        }
        private enum POV_Mode
        {
            Eyes,

            // Okay even if there are outliers that would use this, we simply don't have a hotkey for it. Keyboard for VR? Big fat NO.
            // Fine we may add grip or trigger as modifiers for long press of touchpad(joystick). Still shady hotkey though.
            // Scroll through all modes? That's even worse then keyboard.
            Head,
            Disable
        }
        private ChaControl _target;
        private Transform _targetEyes;
        private POV_Mode povMode;
        private KoikatuSettings settings;
        private bool buttonA;
        private bool _wasAway;
        private List<ChaControl> _chaControls;
        private Controller _device;
        private Controller _device1;
        private float _moveSpeed;
        private Scene _scene;
        private bool _newAttachPoint;
        private Vector3 _offsetPosition;
        private Quaternion _offsetRotation;
        private bool _rotationFull;
        private bool _rotationZero;
        private bool _rotationRequired;
        private float _rotIntensity;
        private float _rotFootprint;
        private bool _precisionPoint;

        private HFlag _hFlag;
        private bool IsClimax => _hFlag.nowAnimStateName.EndsWith("_Loop", System.StringComparison.Ordinal);
        public void Initialize(HSceneProc proc)
        {
            Instance = this;
            settings = VR.Context.Settings as KoikatuSettings;
            //_hand = Traverse.Create(proc).Field("hand").GetValue<HandCtrl>(); 
            _hFlag = Traverse.Create(proc).Field("flags").GetValue<HFlag>();
            CrossFader.HSceneHooks.SetFlag(_hFlag);
            _chaControls = Traverse.Create(proc).Field("lstFemale").GetValue<List<ChaControl>>();
            _device = FindObjectOfType<Controller>();
            _device1 = _device.Other;
        }

        public bool IsGripPress() => _device.Input.GetPress(Grip) || _device1.Input.GetPress(Grip);
        public bool IsGripPressUp() => _device.Input.GetPressUp(Grip) || _device1.Input.GetPressUp(Grip);
        public bool IsTouchpadPressDown() => _device.Input.GetPressDown(Touchpad) || _device1.Input.GetPressDown(Touchpad);
        public bool IsTouchpadPressUp() => _device.Input.GetPressUp(Touchpad) || _device1.Input.GetPressUp(Touchpad);
        //public bool IsTriggerPressUp() => _device.Input.GetPressUp(SteamVR_Controller.ButtonMask.Trigger) || _device1.Input.GetPressUp(SteamVR_Controller.ButtonMask.Trigger);
        //public bool IsTriggerPressDown() => _device.Input.GetPressDown(SteamVR_Controller.ButtonMask.Trigger) || _device1.Input.GetPressDown(SteamVR_Controller.ButtonMask.Trigger);
        private void UpdateSettings()
        {
            _rotFootprint = (int)(10f * Mathf.Lerp(0f, 30f, settings.RotationFootprint)) * 0.1f;
            VRPlugin.Logger.LogDebug($"PoV:UpdateSettings[{_rotFootprint}]");
        }
        private void SetVisibility()
        {
            if (_target != null)
            {
                _target.fileStatus.visibleHeadAlways = true;
            }
        }
        private void IncreaseRotation()
        {
            if (!_rotationFull)
            {
                _rotIntensity += Time.deltaTime * 10f;
                if (_rotIntensity > 60f)
                {
                    _rotationFull = true;
                    _rotIntensity = 60f;
                }
                _rotationZero = false;
                //VRPlugin.Logger.LogDebug($"PoV:IncreaseRotation:[{_rotIntensity}]");
            }
        }
        private void DecreaseRotation()
        {
            if (!_rotationZero)
            {
                _rotIntensity -= Time.deltaTime * 10f;
                if (_rotIntensity < 1f)
                {
                    _rotationZero = true;
                    _rotIntensity = 1f;
                }
                _rotationFull = false;
                //VRPlugin.Logger.LogDebug($"PoV:DecreaseRotation:[{_rotIntensity}]");
            }
        }
        private void StopRotation()
        {
            _rotIntensity = 1f;
            _rotationZero = true;
            _rotationFull = false;
            //VRPlugin.Logger.LogDebug($"PoV:StopRotation[{_rotIntensity}]");
        }
        private void MoveToPos()
        {
            var origin = VR.Camera.Origin;
            if (_newAttachPoint)
            {
                if (!IsClimax)
                {
                    origin.rotation = _offsetRotation;
                    origin.position += GetEyesPosition() + _offsetPosition - VR.Camera.Head.position;
                }
            }
            else
            {
                if (IsClimax)
                {
                    if (_rotationRequired)
                    {
                        StopRotation();
                        _rotationRequired = false;
                    }
                }
                else
                {
                    var angle = Quaternion.Angle(origin.rotation, _targetEyes.rotation);
                    if (!_rotationRequired)
                    {
                        if (angle > _rotFootprint)
                        {
                            _rotationRequired = true;
                        }
                        DecreaseRotation();
                        //VRPlugin.Logger.LogDebug($"PoV:MoveToPos:NotRequired[{angle}]");

                    }
                    else
                    {
                        if (angle < _rotFootprint)
                        {
                            // Camera is close enough to the target rotation. 
                            if (!_precisionPoint)
                            {
                                //VRPlugin.Logger.LogDebug($"PoV:MoveToPos:Required:Close:NotPrecise[{angle}]");
                                if (angle < 0.25f)
                                {
                                    _precisionPoint = true;
                                }
                            }
                            else
                            {
                                //VRPlugin.Logger.LogDebug($"PoV:MoveToPos:Required:Close:Precise[{angle}]");
                                _rotationRequired = false;
                                _precisionPoint = false;
                            }
                        }
                        else
                        {
                            IncreaseRotation();
                            //VRPlugin.Logger.LogDebug($"PoV:MoveToPos:Required:Far[{angle}]");
                        }
                    }
                    if (_rotationRequired)
                    {
                        origin.rotation = Quaternion.RotateTowards(origin.rotation, _targetEyes.rotation, Time.deltaTime * _rotIntensity);
                    }
                    origin.position += GetEyesPosition() - VR.Camera.Head.position;
                }
            }
        }
        public void StartPov()
        {
            VRPlugin.Logger.LogDebug($"PoV:StartPov");
            Active = true;
            NextChara(keepChara: true);
        }
        public void CameraIsFar()
        {
            _wasAway = true;
            _moveSpeed = 0f;
            StopRotation();
        }
        public void CameraIsFarAndBusy()
        {
            CameraIsFar();
            VRMouth.NoActionAllowed = true;
        }
        public void CameraIsNear()
        {
            _wasAway = false;
            _moveSpeed = 0f;
            //StopRotation();
            if (_target.sex == 1)
            {
                GirlPOV = true;
                VRMouth.NoActionAllowed = true;
            }
            else
            {
                GirlPOV = false;
                VRMouth.NoActionAllowed = false;
            }
        }
        private void MoveToHead()
        {
            if (!settings.FlyInPov)
            {
                CameraIsNear();
                return;
            }
            var head = VR.Camera.Head;
            var origin = VR.Camera.Origin;
            var curTarget = GetEyesPosition();
            var distance = Vector3.Distance(head.position, curTarget);
            var angleDelta = Quaternion.Angle(origin.rotation, _targetEyes.rotation);
            if (_moveSpeed == 0f)
            {
                _moveSpeed = 0.5f + distance * 0.5f * settings.FlightSpeed;// 3f;
            }
            var step = Time.deltaTime * _moveSpeed;
            if (distance < step)// && angleDelta < 1f)
            {
                CameraIsNear();
            }
            var rotSpeed = angleDelta / (distance / step);
            var moveToward = Vector3.MoveTowards(head.position, curTarget, step);
            origin.rotation = Quaternion.RotateTowards(origin.rotation, _targetEyes.rotation, 1f * rotSpeed);
            origin.position += moveToward - head.position;
        }
        public void OnSpotChange()
        {
            _newAttachPoint = false;
        }
        public void OnPoseChange()
        {
            // VRMoverH does it.
            StartPov();
        }
        private void ResetRotation()
        {
            VR.Camera.Origin.rotation = Quaternion.Euler(0f, VR.Camera.Origin.rotation.eulerAngles.y, 0f);
        }
        public Vector3 GetDestination()
        {
            if (_targetEyes != null)
            {
                return GetEyesPosition();
            }
            else
            {
                return Vector3.zero;
            }
        }
        public void SetMoveSpeed(float speed) => _moveSpeed = speed;
        public Quaternion GetRotation() => _targetEyes.rotation;
        private Vector3 GetEyesPosition() => _targetEyes.TransformPoint(new Vector3(0f, settings.PositionOffsetY, settings.PositionOffsetZ));
        /// <summary>
        /// Stub.
        /// </summary>
        private void NewLookAtPoI()
        {
            if (_target == null)
                NextChara(keepChara: true);
            var chaControl = _target;
            var extraForBoobs = new Vector3();

            VRLog.Debug($"NewLookAtPoI:[{chaControl}]");
            string poiIndex = poiDic.ElementAt(Random.Range(0, poiDic.Count)).Key;

            VRLog.Debug($"poiIndex:[{poiIndex}]");

            if (chaControl.sex != 1)
            {
                chaControl = FindObjectsOfType<ChaControl>()
                    .Where(c => c.objTop.activeSelf && c.visibleAll && c.sex == 1) //!c.GetTopmostParent().name.Contains("ActionScene") && c.visibleAll)
                    .FirstOrDefault();
            }

            string teleportTo = poiDic[poiIndex].teleportTo;
            if (teleportTo.Contains("cf_j_spine03"))
            {
                // Find median value between nipples and use it as an anchor point. Otherwise the big/small breast disrespect is upon us.
                var lNip = _target.objBodyBone.transform.Descendants()
                    .Where(t => t.name.Contains("a_n_nip_L"))
                    .Select(t => t.position)
                    .FirstOrDefault();
                var rNip = _target.objBodyBone.transform.Descendants()
                    .Where(t => t.name.Contains("a_n_nip_R"))
                    .Select(t => t.position)
                    .FirstOrDefault();
                extraForBoobs = (lNip + rNip) / 2f;
            }
            Transform teleportToPosition = chaControl.transform.Descendants()
                .Where(t => t.name.Contains(teleportTo))
                .FirstOrDefault();
            VRLog.Debug($"teleportPosition:[{teleportToPosition.name}]");

            // Pick the object we will be looking at.
            string lookAtPoI = poiDic[poiIndex].lookAt.ElementAt(Random.Range(0, poiDic[poiIndex].lookAt.Count));
            Vector3 lookAtPosition = chaControl.transform.Descendants()
                .Where(t => t.name.Contains(lookAtPoI))
                .Select(t => t.position)
                .FirstOrDefault();
            VRLog.Debug($"lookAtPosition:[{lookAtPoI}]");

            var forward = Random.Range(poiDic[poiIndex].forwardMin, poiDic[poiIndex].forwardMax);
            var up = Random.Range(poiDic[poiIndex].upMin, poiDic[poiIndex].upMax);
            var right = Random.Range(poiDic[poiIndex].rightMin, poiDic[poiIndex].rightMax);
            VRLog.Debug($"forward:[{forward}], up:[{up}], right:[{right}]");

            Vector3 teleportVector = teleportTo.Contains("cf_j_spine03") ? extraForBoobs : teleportToPosition.position;
            teleportVector +=
                (teleportToPosition.forward * forward) +
                (teleportToPosition.up * up) +
                (teleportToPosition.right * right);

            VR.Camera.Origin.rotation = Quaternion.LookRotation(lookAtPosition - teleportVector);
            VR.Camera.Origin.position += teleportVector - VR.Camera.Head.position;
        }
        //private static Quaternion MakeUpright(Quaternion _rotation)
        //{
        //    return Quaternion.Euler(0f, _rotation.eulerAngles.y, 0f);
        //}
        //private static Quaternion ResetRotationZ(Quaternion _rotation)
        //{
        //    return Quaternion.Euler(_rotation.eulerAngles.x, _rotation.eulerAngles.y, 0f);
        //}
        private int GetCurrentCharaIndex(List<ChaControl> _chaControls)
        {
            if (_target != null)
            {
                for (int i = 0; i < _chaControls.Count; i++)
                {
                    if (_chaControls[i] == _target)
                    {
                        return i;
                    }
                }
            }
            return 0;
        }

        private void NextChara(bool keepChara = false)
        {
            // As some may add extra characters with kPlug, we look them all up.
            var chaControls = FindObjectsOfType<ChaControl>()
                    .Where(c => c.objTop.activeSelf && c.visibleAll)
                    .ToList();

            if (chaControls.Count == 0)
            {
                Active = false;
                VRLog.Warn("[PoV] Can't impersonate, everyone is dead and it's all your fault.");
                return;
            }
            var currentCharaIndex = GetCurrentCharaIndex(chaControls);

            // Previous target becomes visible.
            if (settings.HideHeadInPOV && !keepChara && _target != null)
                SetVisibility();

            if (keepChara && chaControls[currentCharaIndex])
                _target = chaControls[currentCharaIndex];
            else if (currentCharaIndex == chaControls.Count - 1)
            {
                if (currentCharaIndex == 0)
                {
                    // No point in switching with only one active character, disable instead.
                    povMode = POV_Mode.Disable;
                    return;
                }
                // End of the list, back to zero index.
                _target = chaControls[0];
            }
            else
            {
                _target = chaControls[currentCharaIndex + 1];
            }
            _targetEyes = _target.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz");
            CameraIsFarAndBusy();
            UpdateSettings();
        }
        private void NewPosition()
        {
            // Most likely a bad idea to kiss/lick when detached from the head but still inheriting all movements.
            CameraIsNear();
            _offsetPosition = VR.Camera.Head.position - GetEyesPosition();
            _offsetRotation = VR.Camera.Origin.rotation;
        }

        private void SetPOV()
        {
            if (VRMouth.Instance.IsAction || SceneApi.GetIsOverlap())//!Scene.AddSceneName.Equals("HProc"))
            {
                // We don't want pov while kissing/licking or if config/pointmove scene pops up.
                CameraIsFar();

            }
            else if (_newAttachPoint && (_device.Input.GetPressUp(k_EButton_Grip) || _device1.Input.GetPressUp(k_EButton_Grip)))
            {
                NewPosition();
            }
            else if (_device.Input.GetPress(k_EButton_Grip) || _device1.Input.GetPress(k_EButton_Grip))
            {
                CameraIsFar();

                if (_device.Input.GetPressDown(k_EButton_SteamVR_Touchpad) || _device1.Input.GetPressDown(k_EButton_SteamVR_Touchpad))
                {
                    _newAttachPoint = true;
                }

            }
            else if (_wasAway)
            {
                MoveToHead();
            }
            else
            {
                switch (povMode)
                {
                    case POV_Mode.Eyes:
                        MoveToPos();
                        break;
                    case POV_Mode.Head:
                        VRManager.Instance.Mode.MoveToPosition(GetEyesPosition(), false);
                        break;
                    case POV_Mode.Disable:
                        DisablePov();
                        break;
                }
            }
        }

        public void DisablePov(bool teleport = true)
        {
            Active = false;
            SetVisibility();
            povMode = POV_Mode.Eyes;
            if (teleport)
            {
                NewLookAtPoI();
            }
            _newAttachPoint = false;
            VRMouth.NoActionAllowed = false;
        }
        protected override void OnUpdate()
        {
            if (!settings.EnablePOV)
            {
                return;
            }
            if (!buttonA && IsTouchpadPressDown() && !IsGripPress())
            {
                StartCoroutine(GetButtonA());
            }
            if (Active) //!_scene.AddSceneName.StartsWith("Con", System.StringComparison.Ordinal) && !_scene.AddSceneName.StartsWith("HPo", System.StringComparison.Ordinal))
            {
                SetPOV();
            }
        }
        protected override void OnLateUpdate()
        {
            if (settings.HideHeadInPOV && Active && _target != null)
            {
                HideHead();
            }
        }
        private void HideHead()
        {
            // We hide it lazily by default, and start proper check if we use custom position or currently moving to impersonate.
            // Every so often a shadow of a headless body during the kiss disturbs me deeply. So we don't hide it during kiss.
            if (_newAttachPoint || _wasAway)
            {
                var head = _target.objHead.transform;
                var wasVisible = _target.fileStatus.visibleHeadAlways;
                var headCenter = head.TransformPoint(0, 0.12f, -0.04f);
                var sqrDistance = (VR.Camera.transform.position - headCenter).sqrMagnitude;
                bool visible = 0.0361f < sqrDistance; // 19 centimeters
                //bool visible = !ForceHideHead && 0.0361f < sqrDistance; // 19 centimeters 0.0451f
                _target.fileStatus.visibleHeadAlways = visible;
                if (wasVisible && !visible)
                {
                    _target.objHead.SetActive(false);
                }
            }
            else
            {
                _target.fileStatus.visibleHeadAlways = VRMouth.Instance.IsKiss;
            }
        }
        private IEnumerator GetButtonA(float timer = 1f)
        {
            buttonA = true;
            var clicks = 0;
            while (timer > 0f)
            {
                if (IsTouchpadPressUp())
                {
                    clicks += 1;
                    if (clicks == 2)
                        break;
                    timer = 0.4f;
                }
                timer -= Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }
            if (Active && clicks == 2)
            {
                // Adding double click for non-Active state creates problems with undresser.
                if (_newAttachPoint)
                {
                    _newAttachPoint = false;
                    CameraIsFarAndBusy();
                }
                else
                {
                    if (povMode == POV_Mode.Eyes)
                        ResetRotation();
                    povMode = POV_Mode.Disable;
                    //povMode = (POV_Mode)(((int)povMode + 1) % 3);
                }
            }
            else if (clicks == 0)
            {
                if (_newAttachPoint)
                {
                    _newAttachPoint = false;
                    CameraIsFarAndBusy();
                }
                else if (Active)
                {
                    NextChara();
                }
                else
                {
                    StartPov();
                }

            }
            //else //click = 0
            //{
            //    povMode = (POV_Mode)(((int)povMode + 1) % 3);
            //}
            buttonA = false;
        }
        private Dictionary<string, PoIPatternInfo> poiDicDev = new Dictionary<string, PoIPatternInfo>()
        {

            {
                "NavelUpFront",  // Upfront
                new PoIPatternInfo {
                    teleportTo = "cf_j_spine01",
                    lookAt = new List<string> {
                        "cf_j_spine03",
                        "cf_j_spine01",
                        "cf_j_spine02"
                    },
                    forwardMin = 0.05f,
                    forwardMax = 0.15f,
                    upMin = -0.1f,
                    upMax = 0.1f,
                    rightMin = -0.1f,
                    rightMax = 0.1f
                }
            },
            {
                "NavelLeftSide",
                new PoIPatternInfo {
                    teleportTo = "cf_j_spine01",
                    lookAt = new List<string> {
                        "cf_j_spine03",
                        "cf_j_spine01",
                        "cf_j_spine02"
                    },
                    forwardMin = -0.1f,
                    forwardMax = 0.1f,
                    upMin = -0.1f,
                    upMax = 0.1f,
                    rightMin = -0.15f,
                    rightMax = -0.25f
                }
            },
            {
                "NavelRightSide",
                new PoIPatternInfo {
                    teleportTo = "cf_j_spine01",
                    lookAt = new List<string> {
                        "cf_j_spine03",
                        "cf_j_spine01",
                        "cf_j_spine02"
                    },
                    forwardMin = -0.1f,
                    forwardMax = 0.1f,
                    upMin = -0.1f,
                    upMax = 0.1f,
                    rightMin = 0.15f,
                    rightMax = 0.25f
                }
            }
        };
        private Dictionary<string, PoIPatternInfo> poiDic = new Dictionary<string, PoIPatternInfo>()
        {
            {
                "FaceUpFront",  // Upfront
                new PoIPatternInfo {
                    teleportTo = "cf_J_FaceUp_tz",
                    lookAt = new List<string> {
                        "cf_J_FaceUp_tz",
                        "cf_j_neck"
                    },
                    forwardMin = 0.15f,
                    forwardMax = 0.3f,
                    upMin = -0.05f,
                    upMax = 0.05f,
                    rightMin = -0.2f,
                    rightMax = 0.2f
                }
            },
            {
                "FaceLeftSide",
                new PoIPatternInfo {
                    teleportTo = "cf_J_FaceUp_tz",
                    lookAt = new List<string> {
                        "cf_J_FaceUp_tz",
                        "cf_j_neck"
                    },
                    forwardMin = 0.1f,
                    forwardMax = 0.2f,
                    upMin = -0.05f,
                    upMax = 0.05f,
                    rightMin = -0.15f,
                    rightMax = -0.3f
                }
            },
            {
                "FaceRightSide",  // Right
                new PoIPatternInfo {
                    teleportTo = "cf_J_FaceUp_tz",
                    lookAt = new List<string> {
                        "cf_J_FaceUp_tz",
                        "cf_j_neck"
                    },
                    forwardMin = 0.1f,
                    forwardMax = 0.2f,
                    upMin = -0.05f,
                    upMax = 0.05f,
                    rightMin = 0.15f,
                    rightMax = 0.3f
                }
            },
            {
                "NeckUpFront",  // Upfront
                new PoIPatternInfo {
                    teleportTo = "cf_j_neck",
                    lookAt = new List<string> {
                        "cf_J_FaceUp_tz",
                        "cf_j_spine03"
                    },
                    forwardMin = 0.2f,
                    forwardMax = 0.3f,
                    upMin = -0.05f,
                    upMax = 0.05f,
                    rightMin = -0.2f,
                    rightMax = 0.2f
                }
            },
            {
                "NeckLeftSide",
                new PoIPatternInfo {
                    teleportTo = "cf_j_neck",
                    lookAt = new List<string> {
                        "cf_J_FaceUp_tz",
                        "cf_j_spine03"
                    },
                    forwardMin = 0.1f,
                    forwardMax = 0.2f,
                    upMin = -0.05f,
                    upMax = 0.05f,
                    rightMin = -0.15f,
                    rightMax = -0.25f
                }
            },
            {
                "NeckRightSide",
                new PoIPatternInfo {
                    teleportTo = "cf_j_neck",
                    lookAt = new List<string> {
                        "cf_J_FaceUp_tz",
                        "cf_j_spine03"
                    },
                    forwardMin = 0.1f,
                    forwardMax = 0.2f,
                    upMin = -0.05f,
                    upMax = 0.05f,
                    rightMin = 0.15f,
                    rightMax = 0.25f
                }
            },
            {
                "BreastUpFront",  // Upfront
                new PoIPatternInfo {
                    teleportTo = "cf_j_spine03",
                    lookAt = new List<string> {
                        "cf_J_FaceUp_tz",
                        "cf_j_neck",
                        "cf_j_spine03"
                    },
                    forwardMin = 0.05f,
                    forwardMax = 0.15f,
                    upMin = -0.1f,
                    upMax = 0.1f,
                    rightMin = -0.1f,
                    rightMax = 0.1f
                }
            },
            {
                "BreastLeftSide",
                new PoIPatternInfo {
                    teleportTo = "cf_j_spine03",
                    lookAt = new List<string> {
                        "cf_J_FaceUp_tz",
                        "cf_j_neck",
                        "cf_j_spine03"
                    },
                    forwardMin = -0.1f,
                    forwardMax = 0.1f,
                    upMin = -0.1f,
                    upMax = 0.1f,
                    rightMin = -0.15f,
                    rightMax = -0.25f
                }
            },
            {
                "BreastRightSide",
                new PoIPatternInfo {
                    teleportTo = "cf_j_spine03",
                    lookAt = new List<string> {
                        "cf_J_FaceUp_tz",
                        "cf_j_neck",
                        "cf_j_spine03"
                    },
                    forwardMin = -0.1f,
                    forwardMax = 0.1f,
                    upMin = -0.1f,
                    upMax = 0.1f,
                    rightMin = 0.15f,
                    rightMax = 0.25f
                }
            },
            {
                "NavelUpFront",  // Upfront
                new PoIPatternInfo {
                    teleportTo = "cf_j_spine01",
                    lookAt = new List<string> {
                        "cf_j_spine03",
                        "cf_j_spine01",
                        "cf_j_spine02"
                    },
                    forwardMin = 0.05f,
                    forwardMax = 0.15f,
                    upMin = -0.1f,
                    upMax = 0.1f,
                    rightMin = -0.1f,
                    rightMax = 0.1f
                }
            },
            {
                "NavelLeftSide",
                new PoIPatternInfo {
                    teleportTo = "cf_j_spine01",
                    lookAt = new List<string> {
                        "cf_j_spine03",
                        "cf_j_spine01",
                        "cf_j_spine02"
                    },
                    forwardMin = -0.1f,
                    forwardMax = 0.1f,
                    upMin = -0.1f,
                    upMax = 0.1f,
                    rightMin = -0.15f,
                    rightMax = -0.25f
                }
            },
            {
                "NavelRightSide",
                new PoIPatternInfo {
                    teleportTo = "cf_j_spine01",
                    lookAt = new List<string> {
                        "cf_j_spine03",
                        "cf_j_spine01",
                        "cf_j_spine02"
                    },
                    forwardMin = -0.1f,
                    forwardMax = 0.1f,
                    upMin = -0.1f,
                    upMax = 0.1f,
                    rightMin = 0.15f,
                    rightMax = 0.25f
                }
            }
        };

    }
}

