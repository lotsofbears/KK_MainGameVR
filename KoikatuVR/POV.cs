using HarmonyLib;
using Illusion.Game;
using KoikatuVR.Caress;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRGIN.Controls;
using VRGIN.Core;
using VRGIN.Helpers;
using KoikatuVR.Settings;
using static SteamVR_Controller;
using Manager;



namespace KoikatuVR
{
    public class POV : ProtectedBehaviour
    {

        public static POV Instance;
        /// <summary>
        /// girlPOV is NOT set proactively, use "active" to monitor state.
        /// </summary>
        public static bool GirlPOV;
        public static bool Active;
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
            Eyes,      // Mode1: Tracking Eye Position & Rotation
            Head,     // Mode2: Only Tracking Eye Position (Default)
            Disable // Mode3: Teleport(Jump) to next character when trigger controller
        }
        private ChaControl _target;
        private HandCtrl _hand;
        private Transform _targetEyes;
        private POV_Mode povMode;
        private KoikatuSettings settings;
        private bool buttonA;
        private bool _wasAway;
        private SteamVR_Controller.Device _device;
        private SteamVR_Controller.Device _device1;
        private Transform _poi;
        private List<ChaControl> _chaControls;
        private Controller _controller;
        private float _moveSpeed;
        private Scene _scene;
        private bool _newAttachPoint;
        private Vector3 _offsetPosition;
        private Quaternion _offsetRotation;

        public void Initialize(HSceneProc proc)
        {
            Instance = this;
            settings = VR.Context.Settings as KoikatuSettings;
            _hand = Traverse.Create(proc).Field("hand").GetValue<HandCtrl>();
            _chaControls = Traverse.Create(proc).Field("lstFemale").GetValue<List<ChaControl>>();
            _device = SteamVR_Controller.Input((int)VR.Mode.Right.Tracking.index);
            _device1 = SteamVR_Controller.Input((int)VR.Mode.Left.Tracking.index);
            _scene = Scene.Instance;
        }
        private void SetSettingsFalse()
        {
            settings.AutomaticKissing = false;
            settings.AutomaticTouchingByHmd = false;
        }
        private void SetSettingsTrue()
        {
            settings.AutomaticKissing = true;
            settings.AutomaticTouchingByHmd = true;
            _target.fileStatus.visibleHeadAlways = true;
        }
        private void MoveToPos()
        {
            var origin = VR.Camera.Origin;
            if (_newAttachPoint)
            {
                origin.rotation =  _offsetRotation;
                origin.position += GetEyesPosition() + _offsetPosition - VR.Camera.Head.position;
            }
            else
            {
                origin.rotation = Quaternion.RotateTowards(origin.rotation, _targetEyes.rotation, Time.deltaTime * 60f);
                origin.position += GetEyesPosition() - VR.Camera.Head.position;
            }
        }
        public void StartPov()
        {
            Active = true;
            _wasAway = true;
            NextChara(keepChara: true);
        }
        private void MoveToDesignatedHead()
        {
            var head = VR.Camera.Head;
            var origin = VR.Camera.Origin;
            var curTarPos = GetEyesPosition();
            var distance = Vector3.Distance(head.position, curTarPos);
            if (_moveSpeed == 0f)
                _moveSpeed = 0.5f + distance;// 3f;
            var angleDelta = Quaternion.Angle(origin.rotation, _targetEyes.rotation);
            var rotSpeed = angleDelta / (distance / (Time.deltaTime * _moveSpeed));
            if (distance < Time.deltaTime && angleDelta < 1f)
            {
                _wasAway = false;
                _moveSpeed = 0f;
            }
            var moveToward = Vector3.MoveTowards(head.position, curTarPos, Time.deltaTime * _moveSpeed);
            origin.rotation = Quaternion.RotateTowards(origin.rotation, _targetEyes.rotation, 1f * rotSpeed);
            origin.position += moveToward - head.position;
        }
        public void OnSpotChange()
        {
            _wasAway = true;
        }
        public void OnPoseChange()
        {
            if (!_target.visibleAll)
                Active = false;
        }
        private void ResetRotation()
        {
            VR.Camera.Origin.rotation = Quaternion.Euler(0f, VR.Camera.Origin.rotation.eulerAngles.y, 0f);
            //headY = Quaternion.identity;
        }
        private Vector3 GetEyesPosition()
        {
            return _targetEyes.position + _targetEyes.up * settings.PositionOffsetY + _targetEyes.forward * settings.PositionOffsetZ;
            //return currentTargetEyes.TransformPoint(currentTargetEyes.localPosition + new Vector3(0f, settings.HeadPosPoVY, settings.HeadPosPoVZ));
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
        private int GetCurrentCharaIndex(List<ChaControl> _chaControls)
        {
            if(_target)
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
                return;
            }
            var currentCharaIndex = GetCurrentCharaIndex(chaControls);

            // Previous target's head becomes visible on target switch.
            if (settings.HideHeadInPOV && !keepChara && _target)
                SetSettingsTrue();

            if (keepChara && chaControls[currentCharaIndex])
                _target = chaControls[currentCharaIndex];
            else if (currentCharaIndex == chaControls.Count - 1)
            {
                if (currentCharaIndex == 0)
                {
                    // No point in switching with only ONE active character, disable instead.
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

            if(_target.sex == 1)
            {
                if (settings.HideHeadInPOV)
                    SetSettingsFalse();
                GirlPOV = true;
            }
            else
            {
                GirlPOV = false;

            }
            _targetEyes = _target.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz");


        }
        private void NewPosition()
        {
            _newAttachPoint = true;
            _offsetPosition = VR.Camera.Head.position - GetEyesPosition();
            _offsetRotation = VR.Camera.Origin.rotation;
        }
        
        private void SetPOV()
        {
            if (VRMouth._kissCoShouldEnd != null || VRMouth._lickCoShouldEnd != null)
            {
                if (!_wasAway)
                    _wasAway = true;
            }
            else if (_wasAway)
            {
                if (_device.GetPress(ButtonMask.Grip))
                    return;
                else
                    MoveToDesignatedHead();
            }
            else if (_newAttachPoint && _device.GetPressUp(ButtonMask.Grip))
            {
                NewPosition();
            }
            else if (_device.GetPress(ButtonMask.Grip))
            {
                if (_device.GetPressUp(128))
                {
                    if (!_newAttachPoint)
                        _newAttachPoint = true;
                    else
                        _newAttachPoint = false;
                }
                
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
                        Active = false;
                        SetSettingsTrue();
                        povMode = POV_Mode.Eyes;
                        NewLookAtPoI();
                        break;
                }
            }
        }
        protected override void OnUpdate()
        {
            if (!settings.EnablePOV)
                return;
            if (!buttonA && ((_device.GetPressDown(128) && !_device.GetPress(ButtonMask.Grip))
                || (_device1.GetPressDown(128) && !_device1.GetPress(ButtonMask.Grip))))
            {
                if (_newAttachPoint)
                    _newAttachPoint = false;
                else
                    StartCoroutine(GetButtonA());
            }
            //else if (!buttonA && VR.Mode.Left.ToolIndex == 2 && !_device1.GetPress(ButtonMask.Grip) && _device1.GetPressDown(128))
            //{
            //    if (_iHaveDifferentKink)
            //        _iHaveDifferentKink = false;
            //    else
            //        StartCoroutine(GetButtonA());
            //}
            //if (POVConfig.TestPOVKey2.Value.IsDown())
            //{

            //}
            if (Active && !_scene.AddSceneName.StartsWith("Con") && !_scene.AddSceneName.StartsWith("HPo"))
            {
                SetPOV();
            }
            //if (test)
            //    VRLog.Debug($"{DevicePos()}");
        }
        protected override void OnLateUpdate()
        {
            if (settings.HideHeadInPOV && Active)
            {
                HideHead();
            }
        }
        private void HideHead()
        {
            // Every so often a shadow of a headless body during the kiss disturbs me deeply. So we don't hide it during kiss.
            if (_target.objTop.activeSelf && !_hand.isKiss)
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
                _target.fileStatus.visibleHeadAlways = true;
            }
        }
        private IEnumerator GetButtonA(float timer = 1f)
        {
            buttonA = true;
            var clicks = 0;
            while (timer > 0f)
            {
                if (_device.GetPressUp(128) || _device1.GetPressUp(128))
                {
                    clicks += 1;
                    if (clicks == 2)
                        break;
                    timer = 0.4f;
                }
                //else if (device.GetPressDown(ButtonMask.Trigger))
                //{
                //    // Trigger.
                // 
                //    buttonA = false;
                //    yield break;
                //}
                //else if (device.GetPressDown(ButtonMask.Grip))
                //{
                //    // Grip.
                //    Utils.Sound.Play(SystemSE.ok_l);
                //    VR.Input.Keyboard.KeyPress(VirtualKeyCode.TAB);
                //    buttonA = false;
                //    yield break;
                //}
                timer -= Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }
            Utils.Sound.Play(SystemSE.ok_l);
            if (Active && clicks == 2)
            {
                if (povMode == POV_Mode.Eyes)
                    ResetRotation();
                povMode = POV_Mode.Disable;
                //povMode = (POV_Mode)(((int)povMode + 1) % 3);
            }
            else if (clicks != 0)
            {
                if (Active)
                {
                    NextChara();
                    VRLog.Debug($"NextChara:[{povMode}]");
                }
                else
                {
                    StartPov();
                }

            }
            else //click = 0
            {
                povMode = (POV_Mode)(((int)povMode + 1) % 3);
            }
            buttonA = false;
        }
    }
}

