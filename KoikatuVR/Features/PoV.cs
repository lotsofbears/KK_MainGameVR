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
using KK_VR.Interpreters;



namespace KK_VR.Features
{
    public class PoV : MonoBehaviour
    {
        public static PoV Instance;
        /// <summary>
        /// girlPOV is NOT set proactively, use "active" to monitor state.
        /// </summary>
        public static bool GirlPoV;
        public static bool Active => _active;
        public static ChaControl Target => _target;
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
            Head,
            Disable
        }
        private static bool _active;
        private static ChaControl _target;
        private Transform _targetEyes;
        private POV_Mode povMode;
        private KoikatuSettings settings;
        //private bool buttonA;
        private bool _wasAway;
        private List<ChaControl> _chaControls;
        private Controller _device;
        private Controller _device1;
        private float _moveSpeed;
        private bool _newAttachPoint;
        private Vector3 _offsetVecNewAttach;
        private Quaternion _offsetRotNewAttach;
        private bool _rotationFull;
        private bool _rotationZero;
        private bool _rotationRequired;
        private float _rotIntensity;
        private float _rotAdaptSpeed;
        private float _rotStartThreshold;
        private bool _precisionPoint;
        private Vector3 _offsetVecEyes;

        private Vector3 GetEyesPosition => _targetEyes.TransformPoint(_offsetVecEyes);
        private bool IsClimax => HSceneInterpreter.hFlag.nowAnimStateName.EndsWith("_Loop", System.StringComparison.Ordinal);
        public void Initialize()
        {
            Instance = this;
            settings = VR.Context.Settings as KoikatuSettings;
            _device = FindObjectOfType<Controller>();
            _device1 = _device.Other;
        }

        // Rearrange this mess.
        //public bool IsGripPress() => _device.Input.GetPress(Grip) || _device1.Input.GetPress(Grip);
        //public bool IsGripPressUp() => _device.Input.GetPressUp(Grip) || _device1.Input.GetPressUp(Grip);
        public bool IsTouchpadPressDown() => _device.Input.GetPressDown(Touchpad) || _device1.Input.GetPressDown(Touchpad);
        //public bool IsTriggerPress() => _device.Input.GetPress(Trigger) || _device1.Input.GetPress(Trigger);
        //public bool IsTriggerPressDown() => _device.Input.GetPressDown(SteamVR_Controller.ButtonMask.Trigger) || _device1.Input.GetPressDown(SteamVR_Controller.ButtonMask.Trigger);
        private void UpdateSettings()
        {
            _rotAdaptSpeed = Mathf.Max(10f, (int)(10f * (settings.RotAdaptSpeed * 100f)) * 0.1f);
            _rotStartThreshold = (int)(10f * Mathf.Lerp(0f, 30f, settings.RotationStartThreshold)) * 0.1f;
            _offsetVecEyes = new Vector3(0f, settings.PositionOffsetY, settings.PositionOffsetZ);
            VRPlugin.Logger.LogDebug($"PoV:UpdateSettings:{_rotStartThreshold}:{_rotAdaptSpeed}");
        }
        private void SetVisibility()
        {
            if (_target != null) _target.fileStatus.visibleHeadAlways = true;
        }
        private void IncreaseRotationFootprint()
        {
            if (!_rotationFull)
            {
                _rotIntensity += Time.deltaTime * _rotAdaptSpeed;
                if (_rotIntensity > 60f)
                {
                    _rotationFull = true;
                    _rotIntensity = 60f;
                }
                _rotationZero = false;
            }
        }

        private void DecreaseRotationFootprint()
        {
            if (!_rotationZero)
            {
                _rotIntensity -= Time.deltaTime * _rotAdaptSpeed;
                if (_rotIntensity < 1f)
                {
                    _rotationZero = true;
                    _rotIntensity = 1f;
                }
                _rotationFull = false;
            }
        }

        private void StopRotation()
        {
            _rotIntensity = 1f;
            _rotationZero = true;
            _rotationFull = false;
        }
        private void MoveToPos()
        {
            var origin = VR.Camera.Origin;
            if (_newAttachPoint)
            {
                if (!IsClimax)
                {
                    origin.rotation = _offsetRotNewAttach;
                    origin.position += _targetEyes.position + _offsetVecNewAttach - VR.Camera.Head.position;
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
                        if (angle > _rotStartThreshold)
                        {
                            _rotationRequired = true;
                        }
                        DecreaseRotationFootprint();
                    }
                    else
                    {
                        if (angle < _rotStartThreshold)
                        {
                            // Camera is close enough to the target rotation. 
                            if (!_precisionPoint)
                            {
                                if (angle < 0.25f)
                                {
                                    _precisionPoint = true;
                                }
                            }
                            else
                            {
                                _rotationRequired = false;
                                _precisionPoint = false;
                            }
                        }
                        else
                        {
                            IncreaseRotationFootprint();
                        }
                    }
                    if (_rotationRequired)
                    {
                        origin.rotation = Quaternion.RotateTowards(origin.rotation, _targetEyes.rotation, Time.deltaTime * _rotIntensity);
                    }
                    origin.position += GetEyesPosition - VR.Camera.Head.position;
                }
            }
        }
        public void StartPov()
        {
            VRPlugin.Logger.LogDebug($"PoV:StartPov");
            _active = true;
            NextChara(keepChara: true);
            if (settings.FlyInPov == KoikatuSettings.MovementTypeH.Upright)
            {
                SetCustomRotation();
            }
        }
        public void SetCustomRotation()
        {
            // Used for different camera rotation when flying to position.. i think.
            _newAttachPoint = true;
            _offsetRotNewAttach = Quaternion.Euler(0f, _targetEyes.rotation.eulerAngles.y, 0f);
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
                GirlPoV = true;
                VRMouth.NoActionAllowed = true;
            }
            else
            {
                GirlPoV = false;
                VRMouth.NoActionAllowed = false;
            }
        }
        private void MoveToHead()
        {
            if (settings.FlyInPov == KoikatuSettings.MovementTypeH.Disabled)
            {
                CameraIsNear();
                _newAttachPoint = false;
                return;
            }
            var head = VR.Camera.Head;
            var origin = VR.Camera.Origin;
            var targetPos = GetEyesPosition;
            var targetRot = _newAttachPoint ? _offsetRotNewAttach : _targetEyes.rotation;
            var distance = Vector3.Distance(head.position, targetPos);
            var angleDelta = Quaternion.Angle(origin.rotation, targetRot);
            if (_moveSpeed == 0f)
            {
                _moveSpeed = 0.5f + distance * 0.5f * settings.FlightSpeed;// 3f;
            }
            var step = Time.deltaTime * _moveSpeed;
            if (distance < step)// && angleDelta < 1f)
            {
                CameraIsNear();
                _newAttachPoint = false;
            }
            // Does quaternion lerp perform better? looks clean sure, but how it works no clue. 
            // Whatever, as they say "not broken don't fix it".
            var rotSpeed = angleDelta / (distance / step);
            var moveToward = Vector3.MoveTowards(head.position, targetPos, step);
            origin.rotation = Quaternion.RotateTowards(origin.rotation, targetRot, rotSpeed);
            origin.position += moveToward - head.position;
        }

        public void OnSpotChange()
        {
            _newAttachPoint = false;
        }
        //public void OnPoseChange()
        //{
        //    // VRMoverH does it.
        //    StartPov();
        //}
        private void ResetRotation()
        {
            var oldPos = VR.Camera.Head.position;
            VR.Camera.Origin.rotation = Quaternion.Euler(0f, VR.Camera.Origin.rotation.eulerAngles.y, 0f);
            VR.Camera.Origin.position = oldPos - VR.Camera.Head.position;
        }
        /// <summary>
        /// Stub.
        /// </summary>
        private void NewLookAtPoI()
        {
            if (_target == null)
                NextChara(keepChara: true);
            var chaControl = _target;
            var extraForBoobs = new Vector3();

            string poiIndex = poiDic.ElementAt(Random.Range(0, poiDic.Count)).Key;


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

            // Pick the object we will be looking at.
            string lookAtPoI = poiDic[poiIndex].lookAt.ElementAt(Random.Range(0, poiDic[poiIndex].lookAt.Count));
            Vector3 lookAtPosition = chaControl.transform.Descendants()
                .Where(t => t.name.Contains(lookAtPoI))
                .Select(t => t.position)
                .FirstOrDefault();

            var forward = Random.Range(poiDic[poiIndex].forwardMin, poiDic[poiIndex].forwardMax);
            var up = Random.Range(poiDic[poiIndex].upMin, poiDic[poiIndex].upMax);
            var right = Random.Range(poiDic[poiIndex].rightMin, poiDic[poiIndex].rightMax);

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
        private void DirectImpersonation(ChaControl chara)
        {
            _active = true;
            _target = chara;
            _targetEyes = _target.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz");
            CameraIsFarAndBusy();
            UpdateSettings();
        }
        private void NextChara(bool keepChara = false)
        {
            // As some may add extra characters with kPlug, we look them all up.
            var charas = FindObjectsOfType<ChaControl>()
                    .Where(c => c.objTop.activeSelf && c.visibleAll)
                    .ToList();

            if (charas.Count == 0)
            {
                DisablePov(teleport: false);
                VRLog.Warn("[PoV] Can't impersonate, everyone is dead and it's all your fault.");
                return;
            }
            var currentCharaIndex = GetCurrentCharaIndex(charas);

            // Previous target becomes visible.
            if (settings.HideHeadInPOV && !keepChara && _target != null)
                SetVisibility();

            if (keepChara)
            {
                _target = charas[currentCharaIndex];
            }
            else if (currentCharaIndex == charas.Count - 1)
            {
                if (currentCharaIndex == 0)
                {
                    // No point in switching with only one active character, disable instead.
                    povMode = POV_Mode.Disable;
                    return;
                }
                // End of the list, back to zero index.
                _target = charas[0];
            }
            else
            {
                _target = charas[currentCharaIndex + 1];
            }
            _targetEyes = _target.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz");
            CameraIsFarAndBusy();
            UpdateSettings();
        }

        private void NewPosition()
        {
            // Most likely a bad idea to kiss/lick when detached from the head but still inheriting all movements.
            CameraIsNear();
            _offsetVecNewAttach = VR.Camera.Head.position - GetEyesPosition;
            _offsetRotNewAttach = VR.Camera.Origin.rotation;
        }
        private bool _lock;
        internal void OnControllerLock(bool press)
        {
            _lock = press;
            if (press)
            {
                CameraIsFar();
            }
            else if (_newAttachPoint)
            {
                NewPosition();
            }
        }
        private void SetPoV()
        {
            if (VRMouth.IsActive || !Scene.Instance.AddSceneName.Equals("HProc")) // SceneApi.GetIsOverlap()) KKS option
            {
                // We don't want pov while kissing/licking or if config/pointmove scene pops up.
                CameraIsFar();
            }
            else if (_lock)
            {
                if (!_newAttachPoint && IsTouchpadPressDown())
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
                        VRManager.Instance.Mode.MoveToPosition(GetEyesPosition, false);
                        break;
                    case POV_Mode.Disable:
                        DisablePov();
                        break;
                }
            }
        }
        public void DisablePov(bool teleport = true)
        {
            _active = false;
            SetVisibility();
            povMode = POV_Mode.Eyes;
            if (teleport) NewLookAtPoI();
            _newAttachPoint = false;
            VRMouth.NoActionAllowed = false;
        }

        private void Update()
        {
            if (_active) SetPoV();
        }

        private void LateUpdate()
        {
            if (_active && settings.HideHeadInPOV && _target != null) HideHead();
            //if (_testParent.active) _testParent.UpdatePlayerIK();
        }

        private void HideHead()
        {
            if (_newAttachPoint || _wasAway)
            {
                var head = _target.objHead.transform;
                var wasVisible = _target.fileStatus.visibleHeadAlways;
                var headCenter = head.TransformPoint(0, 0.12f, -0.04f);
                var sqrDistance = (VR.Camera.transform.position - headCenter).sqrMagnitude;
                var visible = 0.0361f < sqrDistance; // 19 centimeters
                //bool visible = !ForceHideHead && 0.0361f < sqrDistance; // 19 centimeters 0.0451f
                _target.fileStatus.visibleHeadAlways = visible;
                if (wasVisible && !visible)
                {
                    _target.objHead.SetActive(false);
                }
            }
            else
            {
                _target.fileStatus.visibleHeadAlways = VRMouth.IsKiss;
            }
        }
        internal void HandleEnable()
        {
            if (!settings.EnablePOV) return;
            if (_newAttachPoint)
            {
                _newAttachPoint = false;
                CameraIsFarAndBusy();
            }
            else if (_active)
                NextChara();
            else
                StartPov();
        }
        internal void HandleScroll()
        {
            povMode = (POV_Mode)(((int)povMode + 1) % 3);
        }
        internal bool HandleDirect(ChaControl chara)
        {
            if (settings.EnablePOV && settings.DirectImpersonation)
            {
                if (!_active || _target != chara)
                {
                    VRPlugin.Logger.LogDebug($"PoV:HandleDirect:{chara}");
                    DirectImpersonation(chara);
                    return true;
                }
            }
            // We are ready to sync limb.
            return false;
        }
        internal void Disable()
        {
            if (!_active) return;
            if (_newAttachPoint)
            {
                _newAttachPoint = false;
                CameraIsFarAndBusy();
            }
            else
            {
                if (povMode == POV_Mode.Eyes) ResetRotation();
                povMode = POV_Mode.Disable;
            }
        }
        //private IEnumerator GetButtonA(float timer = 1f)
        //{
        //    buttonA = true;
        //    var clicks = 0;
        //    while (timer > 0f)
        //    {
        //        if (IsTouchpadPressUp())
        //        {
        //            clicks += 1;
        //            if (clicks == 2)
        //                break;
        //            timer = 0.4f;
        //        }
        //        timer -= Time.deltaTime;
        //        yield return new WaitForEndOfFrame();
        //    }
        //    if (Active && clicks == 2)
        //    {
        //        // Adding double click for non-Active state creates problems with undresser.
        //        if (_newAttachPoint)
        //        {
        //            _newAttachPoint = false;
        //            CameraIsFarAndBusy();
        //        }
        //        else
        //        {
        //            if (povMode == POV_Mode.Eyes)
        //                ResetRotation();
        //            povMode = POV_Mode.Disable;
        //            //povMode = (POV_Mode)(((int)povMode + 1) % 3);
        //        }
        //    }
        //    else if (clicks == 0)
        //    {
        //        if (_newAttachPoint)
        //        {
        //            _newAttachPoint = false;
        //            CameraIsFarAndBusy();
        //        }
        //        else if (Active)
        //        {
        //            NextChara();
        //        }
        //        else
        //        {
        //            StartPov();
        //        }

        //    }
        //    //else //click = 0
        //    //{
        //    //    povMode = (POV_Mode)(((int)povMode + 1) % 3);
        //    //}
        //    buttonA = false;
        //}
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

