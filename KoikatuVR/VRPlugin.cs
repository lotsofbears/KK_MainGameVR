using BepInEx;
using System;
using VRGIN.Helpers;
using VRGIN.Core;
using VRGIN.Native;
using System.Collections;
using UnityEngine;
using HarmonyLib;
using System.Runtime.InteropServices;
using WindowsInput;
using KK_VR.Settings;
using KK_VR;
using KK_VR.Features;
using KK_VR.Fixes;
using BepInEx.Logging;
using KKAPI.MainGame;
using KK_VR.Interpreters;

namespace KK_VR
{
    [BepInPlugin(GUID: GUID, Name: PluginName, Version: Version)]
    [BepInProcess("Koikatu")]
    [BepInProcess("Koikatsu Party")]
    public class VRPlugin : BaseUnityPlugin
    {
        public const string GUID = "KK.VR";
        public const string PluginName = "Main Game VR";
        public const string Version = "1.2.0";

        internal static new ManualLogSource Logger;
        void Awake()
        {
            Logger = base.Logger;

            VRPlugin.Logger.LogDebug($"VRPlugin:Awake");

            bool vrDeactivated = Environment.CommandLine.Contains("--novr");
            bool vrActivated = Environment.CommandLine.Contains("--vr");

            var settings = SettingsManager.Create(Config);

            bool enabled = vrActivated || SteamVRDetector.IsRunning;

            if (enabled)
            {
                BepInExVrLogBackend.ApplyYourself();

                StartCoroutine(LoadDevice(settings));
            }

            CrossFader.Initialize(Config, enabled);
        }

        private const string DeviceOpenVR = "OpenVR";
        private const string DeviceNone = "None";

        IEnumerator LoadDevice(KoikatuSettings settings)
        {
            //yield return new WaitUntil(() => Manager.Scene.Instance != null && Manager.Scene.Instance.LoadSceneName.Equals("Title"));

            if (UnityEngine.VR.VRSettings.loadedDeviceName != DeviceOpenVR)
            {
                // 指定されたデバイスの読み込み.
                UnityEngine.VR.VRSettings.LoadDeviceByName(DeviceOpenVR);
                // 次のフレームまで待つ.
                yield return null;
            }
            // VRモードを有効にする.
            UnityEngine.VR.VRSettings.enabled = true;
            // 次のフレームまで待つ.
            //yield return null;

            // デバイスの読み込みが完了するまで待つ.
            while (UnityEngine.VR.VRSettings.loadedDeviceName != DeviceOpenVR)
            {
                yield return null;
            }

            while (true)
            {
                var rect = WindowManager.GetClientRect();
                if (rect.Right - rect.Left > 0)
                {
                    break;
                }
                //VRLog.Info("waiting for the window rect to be non-empty");
                yield return null;
            }
            //VRLog.Info("window rect is not empty!");

            new Harmony(VRPlugin.GUID).PatchAll();
            // Boot VRManager!
            VRManager.Create<KoikatuInterpreter>(new KoikatuContext(settings));
            // VRGIN doesn't update the near clip plane until a first "main" camera is created, so we set it here.
            UpdateNearClipPlane(settings);
            settings.AddListener("NearClipPlane", (_, _1) => UpdateNearClipPlane(settings));
            SetInputSimulator(settings);
            settings.AddListener("UseLegacyInputSimulator", (_, _1) => SetInputSimulator(settings));
            VR.Manager.SetMode<KoikatuStandingMode>();
            VRFade.Create();
            PrivacyScreen.Initialize();
            GraphicRaycasterPatches.Initialize();
            // It's been reported in #28 that the game window defocues when
            // the game is under heavy load. We disable window ghosting in
            // an attempt to counter this.
            NativeMethods.DisableProcessWindowsGhosting();
            //TweakShadowSettings();
            GameAPI.RegisterExtraBehaviour<InterpreterHooks>(GUID);
        }

        private void UpdateNearClipPlane(KoikatuSettings settings)
        {
            VR.Camera.gameObject.GetComponent<UnityEngine.Camera>().nearClipPlane = settings.NearClipPlane;
        }

        private void SetInputSimulator(KoikatuSettings settings)
        {
            if (settings.UseLegacyInputSimulator)
            {
                VR.Manager.Input = new InputSimulator();
            }
            else
            {
                VR.Manager.Input = new RobustInputSimulator();
            }
        }

        private void TweakShadowSettings()
        {
            // Default shadows look too wobbly in VR.
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowCascade4Split = new Vector4(0.05f, 0.1f, 0.2f);
        }
    }

    class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern void DisableProcessWindowsGhosting();
    }
}
