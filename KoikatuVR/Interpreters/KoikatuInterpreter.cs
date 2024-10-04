using UnityEngine;
using VRGIN.Core;
using System.Collections;
using UnityEngine.SceneManagement;
using KK_VR.Features;
using KK_VR.Camera;
using Studio;
using static KK_VR.Interpreters.KoikatuInterpreter;
using System.Runtime.Remoting;
using RootMotion;
using System.Collections.Generic;
using KK_VR.Handlers;
using VRGIN.Controls;
using KK_VR.Settings;

namespace KK_VR.Interpreters
{
    class KoikatuInterpreter : GameInterpreter
    {
        public enum SceneType
        {
            None,
            OtherScene,
            ActionScene,
            TalkScene,
            HScene
            //NightMenuScene,
            //CustomScene
        }
        public static KoikatuInterpreter Instance { get; private set; }
        public static SceneType CurrentScene { get; private set; }
        public static SceneInterpreter SceneInterpreter;
        public static float DeltaTime;
        public static KoikatuSettings settings;

        private Mirror.Manager _mirrorManager;
        private int _kkapiCanvasHackWait;
        private Canvas _kkSubtitlesCaption;
        private GameObject _sceneObjCache;
        private Manager.Scene _scene;
        private Manager.Game _game;
        private static bool _deltaSet;
        private List<float> _deltaTimes = new List<float>();
        private ModelHandler _modelHandler;
        //private bool modelActive;
        protected override void OnAwake()
        {
            Instance = this;
            _scene = Manager.Scene.Instance;
            _game = Manager.Game.Instance;
            CurrentScene = SceneType.OtherScene;
            SceneInterpreter = new OtherSceneInterpreter();
            SceneManager.sceneLoaded += OnSceneLoaded;
            _mirrorManager = new Mirror.Manager();
            VR.Camera.gameObject.AddComponent<VREffector>();
            //VR.Manager.ModeInitialized += AddModels;
            settings = VR.Context.Settings as KoikatuSettings;
            Features.LoadVoice.Init();
        }
        protected override void OnUpdate()
        {
            UpdateScene();
            SceneInterpreter.OnUpdate();
            //if (!_deltaSet && !_scene.IsNowLoadingFade)
            //{
            //    if (_deltaTimes.Count < 100)
            //    {
            //        if (Time.deltaTime < 0.05f && Time.frameCount % 5 == 0)
            //        {
            //            _deltaTimes.Add(Time.deltaTime);
            //        }
            //    }
            //    else
            //    {
            //        var coef = 1f / _deltaTimes.Count;
            //        DeltaTime = 0f;
            //        foreach (var t in _deltaTimes)
            //        {
            //            DeltaTime += t * coef;
            //        }
            //        _deltaTimes.Clear();
            //        _deltaSet = true;
            //    }
            //}
        }
        public void ChangeModelItem(int index, bool increase)
        {
            _modelHandler.ChangeItem(index, increase);
        }
        public void ChangeModelLayer(int index, bool increase)
        {
            _modelHandler.ChangeLayer(index, increase);
        }
        //public void AddModels(object sender, ModeInitializedEventArgs e)
        //{
        //    _modelHandler = new ModelHandler();
        //    modelActive = true;
        //}
        protected override void OnLateUpdate()
        {
            if (_kkSubtitlesCaption != null)
            {
                FixupKkSubtitles();
            }
            SceneInterpreter.OnLateUpdate();
            if (_modelHandler != null)
                _modelHandler.OnLateUpdate();
        }
        private void FixedUpdate()
        {
            if (_modelHandler != null)
                _modelHandler.OnFixedUpdate();
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            VRLog.Info($"OnSceneLoaded {scene.name}");
            if (_modelHandler == null && scene.name.Equals("Title"))
            {
                _modelHandler = new ModelHandler();
            }
            foreach (var reflection in GameObject.FindObjectsOfType<MirrorReflection>())
            {
                _mirrorManager.Fix(reflection);
            }
            
        }

        /// <summary>
        /// Fix up scaling of subtitles added by KK_Subtitles. See
        /// https://github.com/IllusionMods/KK_Plugins/pull/91 for details.
        /// </summary>
        private void FixupKkSubtitles()
        {
            foreach (Transform child in _kkSubtitlesCaption.transform)
            {
                if (child.localScale != Vector3.one)
                {
                    VRLog.Info($"Fixing up scale for {child}");
                    child.localScale = Vector3.one;
                }
            }
        }

        public override bool IsIgnoredCanvas(Canvas canvas)
        {
            if (PrivacyScreen.IsOwnedCanvas(canvas))
            {
                return true;
            }
            else if (canvas.name == "Canvas_BackGround")
            {
                Background.Instance.TakeCanvas(canvas);
                return true;
            }
            else if (canvas.name == "CvsMenuTree")
            {
                // Here, we attempt to avoid some unfortunate conflict with
                // KKAPI.
                //
                // In order to support plugin-defined subcategories in Maker,
                // KKAPI clones some UI elements out of CvsMenuTree when the
                // canvas is created, then uses them as templates for custom
                // UI items.
                //
                // At the same time, VRGIN attempts to claim the canvas by
                // setting its mode to ScreenSpaceCamera, which changes
                // localScale of the canvas by a factor of 100 or so. If this
                // happens between KKAPI's cloning out and cloning in, the
                // resulting UI items will have the wrong scale, 72x the correct
                // size to be precise.
                //
                // So our solution here is to hide the canvas from VRGIN for a
                // couple of frames. Crude but works.

                if (_kkapiCanvasHackWait == 0)
                {
                    _kkapiCanvasHackWait = 3;
                    return true;
                }
                else
                {
                    _kkapiCanvasHackWait -= 1;
                    return 0 < _kkapiCanvasHackWait;
                }
            }
            else if (canvas.name == "KK_Subtitles_Caption")
            {
                _kkSubtitlesCaption = canvas;
            }

            return false;
        }
        public static bool StartScene(SceneType type, MonoBehaviour behaviour = null, params object[] args)
        {
            if (CurrentScene != type)
            {
                VRPlugin.Logger.LogDebug($"Interpreter:Start:{type}");
                CurrentScene = type;
                SceneInterpreter.OnDisable();
                SceneInterpreter = CreateSceneInterpreter(type, behaviour, args);
                SceneInterpreter.OnStart();
                _deltaSet = false;
                return true;
            }
            else
            {
                VRPlugin.Logger.LogDebug($"Interpreter:AlreadyExists:{type}");
                return false;
            }
        }
        public static void EndScene(SceneType type)
        {
            if (CurrentScene == type)
            {
                StartScene(SceneType.OtherScene);
            }
            else
            {
                VRPlugin.Logger.LogDebug($"Interpreter:End:WrongScene:Current - {CurrentScene} - {type}");
            }
        }

        // 前回とSceneが変わっていれば切り替え処理をする
        private void UpdateScene()
        {
            if (CurrentScene < SceneType.TalkScene)
            {
                var sceneType = DetectScene();
                if (CurrentScene != sceneType)
                {
                    EndScene(CurrentScene);
                    StartScene(sceneType);
                }
            }
        }

        private SceneType DetectScene()
        {
            if (_game.actScene != null)
            {
                if (_game.actScene.AdvScene.isActiveAndEnabled)
                {
                    return SceneType.TalkScene;
                }
                //if (_scene.NowSceneNames.Contains("NightMenuScene"))
                //{
                //    return SceneType.NightMenuScene;
                //}
                return SceneType.ActionScene;
            }
            return SceneType.OtherScene;
        }

        //private bool SceneObjPresent(string name)
        //{
        //    if (_sceneObjCache != null && _sceneObjCache.name == name)
        //    {
        //        return true;
        //    }
        //    var obj = GameObject.Find(name);
        //    if (obj != null)
        //    {
        //        _sceneObjCache = obj;
        //        return true;
        //    }
        //    return false;
        //}

        private static SceneInterpreter CreateSceneInterpreter(SceneType type, MonoBehaviour behaviour, params object[] args)
        {
            switch(type)
            {
                case SceneType.ActionScene:
                    return new ActionSceneInterpreter();
                //case SceneType.CustomScene:
                //    return new CustomSceneInterpreter();
                //case SceneType.NightMenuScene:
                //    return new NightMenuSceneInterpreter();
                case SceneType.HScene:
                    return new HSceneInterpreter(behaviour);
                case SceneType.TalkScene:
                    return new TalkSceneInterpreter(behaviour);
                default:
                    return new OtherSceneInterpreter();
            }
        }

        protected override CameraJudgement JudgeCameraInternal(UnityEngine.Camera camera)
        {
            if (camera.CompareTag("MainCamera"))
            {
                StartCoroutine(HandleMainCameraCo(camera));
            }
            return base.JudgeCameraInternal(camera);
        }

        /// <summary>
        /// A coroutine to be called when a new main camera is detected.
        /// </summary>
        /// <param name="camera"></param>
        /// <returns></returns>
        private IEnumerator HandleMainCameraCo(UnityEngine.Camera camera)
        {
            // Unity might have messed with the camera transform for this frame,
            // so we wait for the next frame to get clean data.
            yield return null;

            if (camera.name == "ActionCamera" || camera.name == "FrontCamera")
            {
                VRLog.Info("Adding ActionCameraControl");
                camera.gameObject.AddComponent<Camera.ActionCameraControl>();
            }
            else if (camera.GetComponent<CameraControl_Ver2>() != null)
            {
                VRLog.Info("New main camera detected: moving to {0} {1}", camera.transform.position, camera.transform.eulerAngles);
                Camera.VRMover.Instance.MoveTo(camera.transform.position, camera.transform.rotation);
                VRLog.Info("moved to {0} {1}", VR.Camera.Head.position, VR.Camera.Head.eulerAngles);
                VRLog.Info("Adding CameraControlControl");
                camera.gameObject.AddComponent<Camera.CameraControlControl>();
            }
            else
            {
                VRLog.Warn($"Unknown kind of main camera was added: {camera.name}");
            }
        }

        public override bool ApplicationIsQuitting => Manager.Scene.isGameEnd;
    }
}
