using UnityEngine;
using VRGIN.Core;
using WindowsInput.Native;
using StrayTech;
using KK_VR.Settings;
using KK_VR.Features;
using KK_VR.Camera;

namespace KK_VR.Interpreters
{
    class ActionSceneInterpreter : SceneInterpreter
    {
        private KoikatuSettings _Settings;
        private ActionScene _ActionScene;

        private GameObject _Map;
        private GameObject _CameraSystem;
        private Transform _eyes;
        private bool _NeedsResetCamera;
        private bool _IsStanding = true;
        internal bool _Walking = false;
        private bool _Dashing = false; // ダッシュ時は_Walkingと両方trueになる

        public override void OnStart()
        {
            VRLog.Info("ActionScene OnStart");

            _Settings = (VR.Context.Settings as KoikatuSettings);
            _ActionScene = GameObject.FindObjectOfType<ActionScene>();

            ResetState();
            HoldCamera();
            var height = VR.Camera.Head.position.y - _ActionScene.Player.chaCtrl.transform.position.y;
            VRPlugin.Logger.LogWarning($"Interpreter:Action:Start:{height}");
        }

        public override void OnDisable()
        {
            VRLog.Info("ActionScene OnDisable");

            ResetState();
            ReleaseCamera();
        }

        private void ResetState()
        {
            VRLog.Info("ActionScene ResetState");

            StandUp();
            StopWalking();
            _NeedsResetCamera = false;
        }

        private void ResetCamera()
        {
            var pl = _ActionScene.Player?.chaCtrl.objTop;

            if (pl != null && pl.activeSelf)
            {
                _CameraSystem = MonoBehaviourSingleton<CameraSystem>.Instance.gameObject;

                // トイレなどでFPS視点になっている場合にTPS視点に戻す
                Compat.CameraStateDefinitionChange_ModeChangeForce(
                    _CameraSystem.GetComponent<ActionGame.CameraStateDefinitionChange>(),
                    (ActionGame.CameraMode?) ActionGame.CameraMode.TPS);
                //scene.GetComponent<ActionScene>().isCursorLock = false;

                // カメラをプレイヤーの位置に移動
                MoveCameraToPlayer();

                _NeedsResetCamera = false;
                VRLog.Info("ResetCamera succeeded");
            }
        }

        private void HoldCamera()
        {
            VRLog.Info("ActionScene HoldCamera");

            _CameraSystem = MonoBehaviourSingleton<CameraSystem>.Instance.gameObject;

            if (_CameraSystem != null)
            {
                _CameraSystem.SetActive(false);

                VRLog.Info("succeeded");
            }
        }

        private void ReleaseCamera()
        {
            VRLog.Info("ActionScene ReleaseCamera");

            if (_CameraSystem != null)
            {
                _CameraSystem.SetActive(true);

                VRLog.Info("succeeded");
            }
        }

        public override void OnUpdate()
        {
            GameObject map = _ActionScene.Map.mapRoot?.gameObject;

            if (map != _Map)
            {

                VRLog.Info("! map changed.");

                ResetState();
                _Map = map;
                _NeedsResetCamera = true;
            }

            if (_Walking)
            {
                MoveCameraToPlayer(true, true);
            }

            if (_NeedsResetCamera)
            {
                ResetCamera();
            }

            UpdateCrouch();
        }

        private void UpdateCrouch()
        {
            var pl = _ActionScene.Player?.chaCtrl.objTop;

            if (_Settings.CrouchByHMDPos && pl?.activeInHierarchy == true)
            {
                var cam = VR.Camera.transform;
                var delta_y = cam.position.y - pl.transform.position.y;

                if (_IsStanding && delta_y < _Settings.CrouchThreshold)
                {
                    Crouch();
                }
                else if (!_IsStanding && delta_y > _Settings.StandUpThreshold)
                {
                    StandUp();
                }
            }
        }

        public void MoveCameraToPlayer(bool onlyPosition = false, bool quiet = false)
        {

            //var playerHead = player.chaCtrl.objHead.transform;
            var headCam = VR.Camera.transform;


            var pos = GetEyesPosition();
            if (!_Settings.UsingHeadPos)
            {
                var player = _ActionScene.Player;
                pos.y = player.position.y + (_IsStanding ? _Settings.StandingCameraPos : _Settings.CrouchingCameraPos);
            }

            VRMover.Instance.MoveTo(
                //pos + cf * 0.23f, // 首が見えるとうざいのでほんの少し前目にする
                pos,
                onlyPosition ? headCam.rotation : _eyes.rotation,
                false,
                quiet);
        }

        private Vector3 GetEyesPosition()
        {
            if (_eyes == null)
            {
                _eyes = _ActionScene.Player.chaCtrl.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz/cf_J_Eye_tz");
            }
            return _eyes.TransformPoint(0f, _Settings.PositionOffsetY, _Settings.PositionOffsetZ);
        }
        public void MovePlayerToCamera(bool onlyRotation = false)
        {
            var player = _ActionScene.Player;
            var head = VR.Camera.Head;

            var vec = player.position - GetEyesPosition();
            if (!_Settings.UsingHeadPos)
            {
                var attachPoint = player.position;
                attachPoint.y = _IsStanding ? _Settings.StandingCameraPos : _Settings.CrouchingCameraPos;
                vec = player.position - attachPoint;
            }
            var rot = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
            player.rotation = rot;
            player.position = head.position + vec;
        }

        public void Crouch()
        {
            if (_IsStanding)
            {
                _IsStanding = false;
                VR.Input.Keyboard.KeyDown(VirtualKeyCode.VK_Z);
            }
        }

        public void StandUp()
        {
            if (!_IsStanding)
            {
                _IsStanding = true;
                VR.Input.Keyboard.KeyUp(VirtualKeyCode.VK_Z);
            }
        }

        public void StartWalking(bool dash = false)
        {
            MovePlayerToCamera(true);

            if (!dash)
            {
                VR.Input.Keyboard.KeyDown(VirtualKeyCode.LSHIFT);
                _Dashing = true;
            }

            VR.Input.Mouse.LeftButtonDown();
            _Walking = true;
            // Force hide the protagonist's head while walking, so that it
            // remains hidden when the game lags.
            VRMale.ForceHideHead = true;
        }

        public void StopWalking()
        {
            VR.Input.Mouse.LeftButtonUp();

            if (_Dashing)
            {
                VR.Input.Keyboard.KeyUp(VirtualKeyCode.LSHIFT);
                _Dashing = false;
            }

            _Walking = false;
            VRMale.ForceHideHead = false;
        }
    }
}
