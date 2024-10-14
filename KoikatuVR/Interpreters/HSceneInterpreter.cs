using UnityEngine;
using VRGIN.Core;
using HarmonyLib;
using System.Collections.Generic;
using KK_VR.Camera;
using KK_VR.Features;
using System;
using Manager;
using System.Linq;
using System.Collections;
using KK_VR.Interpreters.Patches;
using KK_VR.Caress;
using Random = UnityEngine.Random;
using static HFlag;
using static HandCtrl;
using static VRGIN.Controls.Controller;
using Valve.VR;
using KK_VR.Handlers;
using KK_VR.Controls;
using RootMotion.FinalIK;
using ADV.Commands.H;
using ADV;
using KK_VR.Fixes;
using KK_VR.Interpreters.Extras;
using System.Runtime.Serialization.Formatters;
using KK_VR.Trackers;
using KK_VR.Interactors;

namespace KK_VR.Interpreters
{
    class HSceneInterpreter : SceneInterpreter
    {
        // [Obsolete description]
        // Currently available hotkeys.
        // Joystick directions (without click) for ~1 second for:
        //     Up - Everything that is related to "inside/acceleration".
        //         Varies on use circumstances, possible actions:
        //             Increase speed,
        //             Start auto motion,
        //             Insert,
        //                 Press/hold grip to go for pooper.
        //                 Press (also) trigger for an immediate no Voice insertion attempt.
        //             Swallow,
        //             Climax
        //                 Triggers male climax at ~100 excitement gauge.
        //                 During climax loop, push joystick down to swap for outside climax.
        //                 Not a kPlug's haphazard animation swap, but a simple flag change during OLoop. 
        //             
        //     Down - Everything that is related to "outside/deacceleration".
        //         Varies on use circumstances, possible actions:
        //             Decrease speed,
        //             Stop auto motion,
        //             Pull out,
        //             Vomit,
        //             Toggle condom,
        //             Initiate automatic edge
        //                 Works during active loop with grip modifier.
        //
        //     Left
        //         Outside of active loop.
        //             Pick semi-random animation.
        //                 Push joystick to the left for up to 5 second.
        //                 Apply Grip as modifier for all animations.
        //                 Apply Trigger as modifier for a particular mode.
        //                 No modifiers - 5 second and animation within the current mode.
        //                 Only Triggers - up to 5 second (immediate on 3rd press) and animation from (HFlag.EMode)(Triggers - 1).
        //                 Trigger and Grip - immediate animation from all available.
        //         During active loop.
        //             Left/Right to swap current loop (to increase/decrease intensity). Uses OLoop. 
        //
        //     Right - Enter the PointMoveScene.
        //         Push joystick to the right for up to 1 second to enter scene. 
        //         Trigger as modifier before scene starts to immediately pick random spot from random category.
        //         While in PointMoveScene.
        //             Push joystick to the right to exit, or to the left for random pick.
        //             Push joystick up/down to change pose category.
        //
        // Those HotKey hooks are flimsy at best, LF global hotkey rework.

        internal class InputWait
        {
            internal InputWait(int _index, TrackpadDirection _direction, bool _manipulateSpeed, float _duration)
            {
                index = _index;
                direction = _direction;
                manipulateSpeed = _manipulateSpeed;
                SetDuration(_duration);
            }
            internal InputWait(int _index, EVRButtonId _button, float _duration)
            {
                index = _index;
                button = _button;
                SetDuration(_duration);
            }
            private void SetDuration(float _duration)
            {
                duration = _duration;
                timestamp = Time.time;
                finish = Time.time + _duration;
            }
            internal int index;
            internal TrackpadDirection direction;
            internal EVRButtonId button;
            internal bool manipulateSpeed;
            internal float timestamp;
            internal float duration;
            internal float finish;
        }
        //private readonly VRMouth _vrMouth;
        private readonly PoV _pov;
        private readonly VRMoverH _vrMoverH;
        private readonly bool _sensibleH;
        private readonly List<InputWait> _waitList = new List<InputWait>();
        private TrackpadDirection _lastDirection;
        private HPointMove _hPointMove;

        private readonly static List<int> _lstIKEffectLateUpdate = new List<int>();
        private static bool _lateHitReaction;

        internal static HFlag hFlag;
        internal static HSprite sprite;
        internal static EMode mode;
        internal static HandCtrl handCtrl;
        internal static HandCtrl handCtrl1;
        internal static HAibu hAibu;
        internal static HVoiceCtrl hVoice;
        internal static List<HActionBase> lstProc;
        internal static List<ChaControl> lstFemale;
        internal static ChaControl male;
        private static int _backIdle;
        private static bool adjustDirLight;
        private List<HandHolder> _hands;
        // For the manual manipulation of an aibu item, so we don't lose it.
        private readonly AibuColliderKind[] _lastAibuKind = new AibuColliderKind[2];

        private readonly int[,] _modifierList = new int[2, 3];

        public static bool IsInsertIdle(string nowAnim) => nowAnim.EndsWith("InsertIdle", StringComparison.Ordinal);
        public static bool IsIdleOutside(string nowAnim) => nowAnim.Equals("Idle");
        public static bool IsAfterClimaxInside(string nowAnim) => nowAnim.EndsWith("IN_A", StringComparison.Ordinal);
        public static bool IsAfterClimaxOutside(string nowAnim) => nowAnim.EndsWith("OUT_A", StringComparison.Ordinal);
        public static bool IsClimaxHoushiInside(string nowAnim) => nowAnim.StartsWith("Oral", StringComparison.Ordinal);
        public static bool IsAfterClimaxHoushiInside(string nowAnim) => nowAnim.Equals("Drink_A") || nowAnim.Equals("Vomit_A");
        public static bool IsFinishLoop => hFlag.finish != FinishKind.none && IsOrgasmLoop;
        public static bool IsWeakLoop => hFlag.nowAnimStateName.EndsWith("WLoop", StringComparison.Ordinal);
        public static bool IsStrongLoop => hFlag.nowAnimStateName.EndsWith("SLoop", StringComparison.Ordinal);
        public static bool IsOrgasmLoop => hFlag.nowAnimStateName.EndsWith("OLoop", StringComparison.Ordinal);
        public static bool IsKissAnim => hFlag.nowAnimStateName.StartsWith("K_", StringComparison.Ordinal);
        public static bool IsTouch => hFlag.nowAnimStateName.EndsWith("Touch", StringComparison.Ordinal);
        public HPointMove GetHPointMove => _hPointMove == null ? _hPointMove = UnityEngine.Object.FindObjectOfType<HPointMove>() : _hPointMove;
        public static int GetBackIdle => _backIdle;
        public static bool IsHPointMove => Scene.Instance.AddSceneName.Equals("HPointMove");
        public static bool IsVoiceActive => hVoice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice;
        public static bool IsHandAttached => handCtrl.useItems[0] != null || handCtrl.useItems[1] != null;
        public static bool IsHandActive => handCtrl.GetUseAreaItemActive() != -1;
        public static bool IsActionLoop
        {
            get
            {
                switch (mode)
                {
                    case EMode.aibu:
                        return handCtrl.IsKissAction() || handCtrl.IsItemTouch();
                    case EMode.houshi:
                    case EMode.sonyu:
                        return hFlag.nowAnimStateName.EndsWith("Loop", StringComparison.Ordinal);
                    default:
                        return false;
                }
            }
        }
        private bool IsWait => _waitList.Count != 0;
        private static readonly List<string> _aibuAnims = new List<string>()
        {
            "Idle",     // 0
            "M_Touch",  // 1
            "A_Touch",  // 2
            "S_Touch",  // 3
            "K_Touch"   // 4
        };

        private List<int> GetHPointCategoryList
        {
            get
            {
                var list = GetHPointMove.dicObj.Keys.ToList();
                list.Sort();
                return list;
            }
        }

        // I really hate the amount of delegates around,
        // especially given that this is basically hard dependency at this point,
        // but hey why fix something, right..
        private readonly Action<AibuColliderKind> ReleaseItem;
        // Way too much illegitimate stuff is going on in auto caress, so we can't use normal one,
        // this one among other things will refuse to do it if we are about to break the game.
        private readonly Action<AibuColliderKind> MoMiJudgeProc;
        /// <summary>
        /// Uses StartsWith to find the button, or picks any if not specified (in this case ignores fast/slow in houshi).
        /// </summary>
        private readonly Action<string> ClickButton;
        private readonly Action<int> ChangeLoop;
        /// <summary>
        /// -1 for all, otherwise (HFlag.EMode) 0...2 for specific, or anything higher(e.g. 3) for current EMode.
        /// </summary>
        private readonly Action<int> ChangeAnimation;
        private bool _manipulateSpeed;

        public HSceneInterpreter(MonoBehaviour proc)
        {
            var traverse = Traverse.Create(proc);
            hFlag = traverse.Field("flags").GetValue<HFlag>();
            sprite = traverse.Field("sprite").GetValue<HSprite>();
            handCtrl = traverse.Field("hand").GetValue<HandCtrl>();
            handCtrl1 = traverse.Field("hand1").GetValue<HandCtrl>();
            lstProc = traverse.Field("lstProc").GetValue<List<HActionBase>>();
            hVoice = traverse.Field("voice").GetValue<HVoiceCtrl>();
            lstFemale = traverse.Field("lstFemale").GetValue<List<ChaControl>>();
            male = traverse.Field("male").GetValue<ChaControl>();
            hAibu = (HAibu)lstProc[0];

            _pov = VR.Camera.gameObject.AddComponent<PoV>();
            _pov.Initialize();
            //_vrMouth = VR.Camera.gameObject.AddComponent<VRMouth>();
            _vrMoverH = VR.Camera.gameObject.AddComponent<VRMoverH>();
            _vrMoverH.Initialize();

            CrossFader.HSceneHooks.SetFlag(hFlag);

            var type = AccessTools.TypeByName("KK_SensibleH.AutoMode.LoopController");
            _sensibleH = type != null;

            if (_sensibleH)
            {
                ClickButton = AccessTools.MethodDelegate<Action<string>>(AccessTools.FirstMethod(type, m => m.Name.Equals("ClickButton")));
                ChangeLoop = AccessTools.MethodDelegate<Action<int>>(AccessTools.FirstMethod(type, m => m.Name.Equals("AlterLoop")));
                ChangeAnimation = AccessTools.MethodDelegate<Action<int>>(AccessTools.FirstMethod(type, m => m.Name.Equals("PickAnimation")));

                // There is also plenty from this type in 'CaressHelper', oh well.
                type = AccessTools.TypeByName("KK_SensibleH.Caress.MoMiController");
                ReleaseItem = AccessTools.MethodDelegate<Action<AibuColliderKind>>(AccessTools.FirstMethod(type, m => m.Name.Equals("ReleaseItem")));
                MoMiJudgeProc = AccessTools.MethodDelegate<Action<AibuColliderKind>>(AccessTools.FirstMethod(type, m => m.Name.Equals("MoMiJudgeProc")));
            }
            VRBoop.RefreshDynamicBones(true);
            var charas = new List<ChaControl>() { male };
            charas.AddRange(lstFemale);

            TalkSceneExtras.AddTalkColliders(charas);
            TalkSceneExtras.AddHColliders(charas);
            _hands = HandHolder.GetHands();
            GraspController.Init(charas);


            // Init after everything.
            HitReactionInitialize(charas);

            //ModelHandler.SetHandColor(male);

            // If disabled, camera won't know where to move.
            ((Config.EtceteraSystem)Manager.Config.Instance.xmlCtrl.datas[3]).HInitCamera = true;
        }
        public override void OnDisable()
        {
            GameObject.Destroy(_pov);
            //GameObject.Destroy(_vrMouth);
            GameObject.Destroy(_vrMoverH);
            HandHolder.DestroyHandlers();
            TalkSceneExtras.ReturnDirLight();
            TalkSceneInterpreter.afterH = true;
        }
        public override void OnUpdate()
        {
            // Exit through the title button in config doesn't trigger hook.
            if (hFlag == null) KoikatuInterpreter.EndScene(KoikatuInterpreter.SceneType.HScene);
            HandleInput();
            if (_manipulateSpeed) HandleSpeed();
        }
        private void HandleInput()
        {
            foreach (var wait in _waitList)
            {
                VRPlugin.Logger.LogDebug($"HandleInput:{wait.button}");
                if (wait.finish < Time.time)
                {
                    PickAction(wait);
                    RemoveWait(wait);
                    return;
                }
            }
        }
        private void HandleSpeed()
        {
            VRPlugin.Logger.LogDebug($"HandleSpeed");
            if (_lastDirection == TrackpadDirection.Up)
            {
                SpeedUp();
            }
            else 
            {
                SlowDown();
            }
        }
        private void RemoveWait(InputWait wait)
        {
            if (wait == null) return;
            _waitList.Remove(wait);
        }
        private void RemoveWait(int index, EVRButtonId button)
        {
            RemoveWait(_waitList
                .Where(w => w.index == index && w.button == button)
                .FirstOrDefault());
        }
        private void RemoveWait(int index, TrackpadDirection direction)
        {
            RemoveWait(_waitList
                .Where(w => w.index == index && w.direction == direction)
                .FirstOrDefault());
        }
        private void PickAction(InputWait wait)
        {
            // Main entry.
            if (wait == null) return;
            if (wait.button == EVRButtonId.k_EButton_System)
                PickDirectionAction(wait, GetTiming(wait.timestamp, wait.duration));
            else
                PickButtonAction(wait, GetTiming(wait.timestamp, wait.duration));
            RemoveWait(wait);
        }
        private void PickAction()
        {
            PickAction(
                _waitList
                .OrderByDescending(w => w.button)
                .First());
        }

        private void PickAction(int index, EVRButtonId button)
        {
            if (_waitList.Count == 0) return;
            PickAction(
                _waitList
                .Where(w => w.button == button && w.index == index)
                .FirstOrDefault());
        }

        private void PickAction(int index, TrackpadDirection direction)
        {
            if (_waitList.Count == 0) return;
            PickAction(
                _waitList
                .Where(w => w.direction == direction && w.index == index)
                .FirstOrDefault());
        }

        public override void OnLateUpdate()
        {
            if (_lateHitReaction)
            {
                _lateHitReaction = false;
                _hitReaction.ReleaseEffector();
                _hitReaction.SetEffector(_lstIKEffectLateUpdate);
                _lstIKEffectLateUpdate.Clear();
            }
            if (adjustDirLight)
            {
                TalkSceneExtras.RepositionDirLight(lstFemale[0]);
                adjustDirLight = false;
            }
        }
        private void SpeedUp()
        {
            if (mode == EMode.aibu)
            {
                hFlag.SpeedUpClickAibu(Time.deltaTime, hFlag.speedMaxAibuBody, true);
            }
            else
            {
                if (hFlag.speedCalc < 1f)
                {
                    //_hFlag.SpeedUpClick(Time.deltaTime * 0.5f, 1f);
                    hFlag.speedCalc += Time.deltaTime * 0.2f;
                    if (hFlag.speedCalc > 1f)
                    {
                        hFlag.speedCalc = 1f;
                    }
                }
                else
                {
                    AttemptFinish();
                }
            }
        }
        private void SlowDown()
        {
            if (mode == EMode.aibu)
            {
                hFlag.SpeedUpClickAibu(-Time.deltaTime, hFlag.speedMaxAibuBody, true);
            }
            else
            {
                if (hFlag.speedCalc > 0f)
                {
                    //_hFlag.SpeedUpClick(-Time.deltaTime * 0.5f, 1f);

                    hFlag.speedCalc -= Time.deltaTime * 0.2f;
                    if (hFlag.speedCalc < 0f)
                    {
                        hFlag.speedCalc = 0f;
                    }
                }
                else
                {
                    AttemptStop();
                }
            }
        }
        private void AttemptFinish()
        {
            // Grab SensH ceiling.
            if (hFlag.gaugeMale == 100f)
            {
                // There will be only one finish appropriate for the current mode/setting.
                RandomButton();
                _manipulateSpeed = false;
            }
        }
        private void AttemptStop()
        {
            // Happens only when we recently pressed the button.
            _manipulateSpeed = false;
            Pull();
            
        }

        //private bool SetHand()
        //{
        //    VRPlugin.Logger.LogDebug($"Interpreter:HScene:SetHand");
        //    if (handCtrl.useItems[0] == null || handCtrl.useItems[1] == null)
        //    {
        //        var list = new List<int>();
        //        for (int i = 0; i < 6; i++)
        //        {
        //            if (handCtrl.useAreaItems[i] == null)
        //            {
        //                list.Add(i);
        //            }
        //        }
        //        list = list.OrderBy(a => Random.Range(0, 100)).ToList();
        //        var index = 0;
        //        foreach (var item in list)
        //        {
        //            VRPlugin.Logger.LogDebug($"Interpreter:HScene:SetHand:Loop:{item}");
        //            var clothState = handCtrl.GetClothState((AibuColliderKind)(item + 2));
        //            //var layerInfo = handCtrl.dicAreaLayerInfos[item][handCtrl.areaItem[item]];
        //            var layerInfo = handCtrl.dicAreaLayerInfos[item][0];
        //            if (layerInfo.plays[clothState] == -1)
        //            {
        //                continue;
        //            }
        //            index = item;
        //            break;

        //        }
        //        VRPlugin.Logger.LogDebug($"Interpreter:HScene:SetHand:Required:Choice - {index}");

        //        handCtrl.selectKindTouch = (AibuColliderKind)(index + 2);
        //        _pov.StartCoroutine(CaressUtil.ClickCo(() => handCtrl.selectKindTouch = AibuColliderKind.none));
        //        return false;
        //    }
        //    else
        //    {
        //        VRPlugin.Logger.LogDebug($"Interpreter:HScene:SetHand:NotRequired");
        //        PlayReaction();
        //        return true;
        //    }
        //}
        public override bool OnButtonDown(int index, EVRButtonId buttonId, TrackpadDirection direction)
        {
            switch (buttonId)
            {
                case EVRButtonId.k_EButton_SteamVR_Trigger:
                    return OnTrigger(index, press: true);
                case EVRButtonId.k_EButton_Grip:
                    return OnGrip(index, press: true) || IsWait; 
                case EVRButtonId.k_EButton_SteamVR_Touchpad:
                    return OnTouchpad(index, press: true);
                case EVRButtonId.k_EButton_ApplicationMenu:
                    return OnMenu(index, press: true);
                default:
                    return false;
            }
        }
        public override void OnButtonUp(int index, EVRButtonId buttonId, TrackpadDirection direction)
        {
            switch (buttonId)
            {
                case EVRButtonId.k_EButton_SteamVR_Trigger:
                    OnTrigger(index, press: false);
                    break;
                case EVRButtonId.k_EButton_Grip:
                    OnGrip(index, press: false);
                    break;
                case EVRButtonId.k_EButton_SteamVR_Touchpad:
                    OnTouchpad(index, press: false);
                    break;
            }
        }
        private Timing GetTiming(float timestamp, float duration)
        {
            var timing = Time.time - timestamp;
            if (timing > duration) return Timing.Full;
            if (timing > duration * 0.5f) return Timing.Half;
            return Timing.Fraction;
        }

        public override void OnControllerLock(int index, bool isLock)
        {
            if (isLock)
            {
                _modifierList[index, 0] = 0;
                _modifierList[index, 2] = 0;
                _pov.OnControllerLock(isLock);
                //_vrMouth.OnControllerLock(isLock);
                _hands[index].Grasp.OnGripRelease();
            }
            else
            {
                _modifierList[index, 1] = 0;

                // If second controller isn't locked.
                if (_modifierList[(index == 0) ? 1 : 0, 1] == 0)
                {
                    _pov.OnControllerLock(isLock);
                    //_vrMouth.OnControllerLock(isLock);
                }
            }
        }
        private bool IsTriggerPress(int index) => _modifierList[index, 0] == 1;
        private void AddWait(int index, EVRButtonId button, float duration)
        {
            _waitList.Add(new InputWait(index, button, duration));
        }
        private void AddWait(int index, TrackpadDirection direction, bool manipulateSpeed, float duration)
        {
            _waitList.Add(new InputWait(index, direction, manipulateSpeed, duration));
        }
        private HSceneHandler GetHandler(int index) => (HSceneHandler)_hands[index].Handler;
        private bool OnTrigger(int index, bool press)
        {
            // With present 'Direction' 'Wait' (no buttons pressed) trigger simply finishes 'Wait' and prompts the action,
            // but if button is present, it in addition also offers alternative mode. Currently TouchpadPress only.
            var handler = GetHandler(index);
            var grasp = _hands[index].Grasp;
            if (press)
            {
                _modifierList[index, 0] = 1;
                if (handler.IsAibuManual)
                {
                    MoMiJudgeProc(_lastAibuKind[index]);
                }
                else if (!IsTouchpadPress(index) && IsWait)
                {
                    PickAction();
                }
                else if (handler.IsBusy)
                {
                    if (IsTouchpadPress(index) && grasp.OnTouchpadResetEverything(handler.GetChara))
                    {
                        // Touchpad pressed + trigger = total reset of tracked character.
                        RemoveWait(index, EVRButtonId.k_EButton_SteamVR_Touchpad);
                    }
                    else if (grasp.IsBusy)
                    {
                        VRPlugin.Logger.LogDebug($"OnTrigger:Wait:GraspBusy");
                        AddWait(index, EVRButtonId.k_EButton_SteamVR_Trigger, 0.35f);
                    }
                    else
                    {
                        // If nothing is being held/synced, send synthetic click.
                        handler.TriggerPress();
                    }
                }
                else if (grasp.IsBusy)
                {

                    VRPlugin.Logger.LogDebug($"OnTrigger:Wait:GraspBusy");
                    AddWait(index, EVRButtonId.k_EButton_SteamVR_Trigger, 0.35f);

                }
                return true;
            }
            else
            {
                _modifierList[index, 0] = 0;
                handler.TriggerRelease();
                if (!_hands[index].Grasp.OnTriggerRelease())
                {
                    PickAction(index, EVRButtonId.k_EButton_SteamVR_Trigger);
                }
            }
            return false;
        }
        private bool IsGripPress(int index) => _modifierList[index, 1] == 1;
        private void EnableNip(AibuColliderKind colliderKind)
        {
            if (colliderKind == AibuColliderKind.muneL || colliderKind == AibuColliderKind.muneR)
            {
                var number = colliderKind == AibuColliderKind.muneL ? 0 : 1;
                handCtrl.female.DisableShapeNip(number, false);
                handCtrl.female.DisableShapeBodyID(number, ChaFileDefine.cf_ShapeMaskNipStand, false);
                //if (number == 1)
                //{
                //    handCtrl.female.DisableShapeBust(number, false);
                //}
            }
        }
        internal static void ShowAibuHand(AibuColliderKind colliderKind, bool show)
        {
            handCtrl.useAreaItems[(int)colliderKind - 2].objBody.GetComponent<Renderer>().enabled = show;
        }
        internal void ToggleAibuHandVisibility(AibuColliderKind colliderKind)
        {
            var renderer = handCtrl.useAreaItems[(int)colliderKind - 2].objBody.GetComponent<Renderer>();
            renderer.enabled = !renderer.enabled;
            EnableNip(colliderKind);
        }
        private bool OnMenu(int index, bool press)
        {
            if (press)
            {
                return _hands[index].Grasp.OnMenuPress();
            }
            return false;
        }
        private bool OnGrip(int index, bool press)
        {
            var handler = GetHandler(index);
            //VRPlugin.Logger.LogDebug($"OnGrip:{handler.IsBusy}");
            if (press)
            {
                _modifierList[index, 1] = 1;
                if (handler.IsBusy)
                {
                    handler.UpdateTracker();
                    if (handCtrl.GetUseAreaItemActive() != -1 && handler.IsAibuItemPresent(out var touch))
                    {
                        handler.StartMovingAibuItem(touch);
                        _lastAibuKind[index] = touch;
                        ReleaseItem(touch);
                        EnableNip(touch);
                        if (_settings.HideHandOnUserInput != Settings.KoikatuSettings.HandType.None)
                        {
                            if (_settings.HideHandOnUserInput > Settings.KoikatuSettings.HandType.ControllerItem)
                            {
                                ShowAibuHand(touch, false);
                            }
                            if (_settings.HideHandOnUserInput != Settings.KoikatuSettings.HandType.CaressItem)
                            {
                                _hands[index].SetItemRenderer(false);
                            }
                        }
                    }
                    else
                    {
                        //if (!handler.InBlack)
                        //{
                        // We grasped something, don't start GrabMove.
                        _hands[index].Grasp.OnGripPress(handler.GetTrackPartName(), handler.GetChara);
                        //}
                    }
                    return true;
                }
            }
            else
            {
                _modifierList[index, 1] = 0;
                handler.StopMovingAibuItem();
                _hands[index].Grasp.OnGripRelease();
                _hands[index].SetItemRenderer(true);
            }
            return false;
        }
        private bool IsTouchpadPress(int index) => _modifierList[index, 2] == 1;
        private bool OnTouchpad(int index, bool press)
        {
            if (press)
            {
                _modifierList[index, 2] = 1;
                if (IsTriggerPress(index))
                {
                    _hands[index].ChangeItem();
                }
                else if (!_hands[index].Grasp.OnTouchpadResetHeld())
                {
                    _waitList.Add(new InputWait(index, EVRButtonId.k_EButton_SteamVR_Touchpad, 0.4f));
                }
            }
            else
            {
                _modifierList[index, 2] = 0;
                PickAction(index, EVRButtonId.k_EButton_SteamVR_Touchpad);
            }
            return false;
        }
        public override void OnDirectionUp(int index, TrackpadDirection direction)
        {
            if (IsWait)
                PickAction(index, direction);
            else
            {
                _manipulateSpeed = false;
            }

        }
        private TrackpadDirection SwapSides(TrackpadDirection direction)
        {
            return direction switch
            {
                TrackpadDirection.Left => TrackpadDirection.Right,
                TrackpadDirection.Right => TrackpadDirection.Left,
                _ => direction
            };

        }
        public override bool OnDirectionDown(int index, TrackpadDirection direction)
        {
            ///////////////////////////////////////////////////////
            ///                                                 ///
            ///   A New Headache has arrived -> _grasp.IsBusy   ///
            ///                                                 ///
            ///////////////////////////////////////////////////////
            var wait = 0f;
            var speed = false;
            var handler = GetHandler(index);

            // We respect lefties now.
            direction = SwapSides(direction);
            switch (direction)
            {
                case TrackpadDirection.Up:
                case TrackpadDirection.Down:
                    if (!handler.DoUndress(direction == TrackpadDirection.Down))
                    {
                        if (IsHPointMove)
                        {
                            MoveCategory(direction == TrackpadDirection.Down);
                        }
                        else if (IsActionLoop)
                        {
                            if (mode == EMode.aibu)
                            {
                                if (IsHandActive)
                                {
                                    // Reaction if too long, speed meanwhile.
                                    wait = 3f;
                                    speed = true;
                                }
                                else
                                {
                                    // Reaction/Lean to kiss.
                                    wait = 1f;
                                }
                            }
                            else
                            {
                                speed = true;
                            }
                        }
                        else
                        {
                            // ?? is this.
                            wait = 0.5f;
                        }
                    }
                    break;
                case TrackpadDirection.Left:
                case TrackpadDirection.Right:
                    if (IsTriggerPress(index)) 
                    {
                        _hands[index].ChangeLayer(direction == TrackpadDirection.Right);
                    }
                    else if (IsHPointMove)
                    {
                        if (direction == TrackpadDirection.Right)
                            wait = 1f;
                        else
                            GetHPointMove.Return();
                    }
                    else if (IsActionLoop)
                    {
                        if (mode == EMode.aibu)
                        {
                            if (handler.IsAibuItemPresent(out var touch))
                            {
                                if (IsHandActive)
                                {
                                    ToggleAibuHandVisibility(touch);
                                }
                                else
                                {
                                    SetSelectKindTouch(touch);
                                    VR.Input.Mouse.VerticalScroll(direction == TrackpadDirection.Right ? -1 : 1);
                                }
                            }
                            else
                                ScrollAibuAnim(direction == TrackpadDirection.Right);
                        }
                        else
                            ChangeLoop(GetCurrentLoop(direction == TrackpadDirection.Right));
                    }
                    else
                        wait = 1f;
                    break;
            }
            _manipulateSpeed = speed;
            _lastDirection = direction;
            if (wait != 0f)
            {
                _waitList.Add(new InputWait(index, direction, speed, wait));
                return true;
            }
            else
                return false;
        }
        //private IEnumerator StartItemAction(int button)
        //{
        //    // SensibleH will overtake it by the time we release it.
        //    foreach (var item in handCtrl.useItems)
        //    {
        //        if (item !=  null)
        //        {
        //            handCtrl.selectKindTouch = item.kindTouch;
        //            break;
        //        }
        //    }
        //    if (!_pov.IsTriggerPress())
        //    {
        //        HandCtrlHooks.InjectMouseButtonDown(button);
        //        yield return new WaitForSeconds(1f);
        //        HandCtrlHooks.InjectMouseButtonUp(button);
        //    }
        //    else
        //    {
        //        yield return new WaitForSeconds(0.1f);
        //    }
        //    handCtrl.selectKindTouch = AibuColliderKind.none;
        //}
        private void PickButtonAction(InputWait wait, Timing timing)
        {
            VRPlugin.Logger.LogDebug($"PickButtonAction:{wait.button}");
            var handler = _hands[wait.index].Handler;
            var grasp = _hands[wait.index].Grasp;
            switch (wait.button)
            {
                case EVRButtonId.k_EButton_SteamVR_Touchpad:
                    if (timing == Timing.Full)
                    {
                        if (handler.IsBusy)
                        {
                            handler.UpdateTracker(tryToAvoid: PoV.Active ? PoV.Target : null);

                            // We attempt to reset active body part (held parts reset on press);
                            if (!grasp.OnTouchpadResetActive(handler.GetTrackPartName(), handler.GetChara))
                            {
                                // We update tracker to remove bias from PoV target we set beforehand.
                                handler.UpdateTracker();

                                // We attempt to impersonate, false if already impersonating/or setting.
                                var chara = handler.GetChara;
                                if (!_pov.HandleDirect(chara))
                                {
                                    if (PoV.Active && PoV.Target == chara)
                                    {
                                        grasp.OnTouchpadSyncStart(handler.GetTrackPartName(), handler.GetChara);

                                        //handler.FlushBlack();
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (!_hands[wait.index].Grasp.OnTouchpadSyncEnd())
                            {
                                VRPlugin.Logger.LogDebug($"PoV:Handle:Enable:{handler.GetChara}");
                                _pov.HandleEnable();
                            }
                        }
                    }
                    break;
                case EVRButtonId.k_EButton_SteamVR_Trigger:
                    if (grasp.IsBusy)
                    {
                        grasp.OnTriggerPress(temporarily: timing == Timing.Full);
                    }
                    break;
            }
        }
        private void PickDirectionAction(InputWait wait, Timing timing)
        {
            _manipulateSpeed = false;
            VRPlugin.Logger.LogDebug($"PickDirectionAction:{wait.direction}:{timing}");
            switch (wait.direction)
            {
                case TrackpadDirection.Up:
                    if (mode == EMode.aibu)
                    {
                        if (IsActionLoop)
                        {
                            switch (timing)
                            {
                                case Timing.Fraction:
                                    if (!IsHandActive && IsHandAttached)
                                    {
                                        PlayReaction();
                                    }
                                    break;
                                case Timing.Half:
                                    //if (!IsHandActive)
                                    //{
                                    //    SetHand();
                                    //}
                                    break;
                                case Timing.Full:
                                    //if (IsHandActive)
                                    //{
                                    //    PlayReaction();
                                    //}
                                    //else if (IsHandAttached || SetHand())
                                    //{
                                    //    _pov.StartCoroutine(StartItemAction(0));
                                    //}
                                    break;
                            }
                        }
                        else // Non-action Aibu mode.
                        {
                            switch (timing)
                            {
                                case Timing.Fraction:
                                    if (Random.value < 0.5f)
                                    {
                                        PlayShort(lstFemale[0]);
                                    }
                                    break;
                                case Timing.Half:
                                    // Put in denial + voice.
                                    break;
                                case Timing.Full:
                                    //SetHand();
                                    break;
                            }
                        }
                    }
                    else // Non-Aibu mode.
                    {
                        switch (timing)
                        {
                            case Timing.Fraction:
                            case Timing.Half:
                                PlayReaction();
                                break;
                            case Timing.Full:
                                Insert(noVoice: IsTriggerPress(wait.index), anal: IsTouchpadPress(wait.index));
                                break;
                        }
                    }
                    break;
                case TrackpadDirection.Down:
                    if (mode == EMode.aibu)
                    {
                        if (IsActionLoop)
                        {
                            switch (timing)
                            {
                                case Timing.Fraction:
                                case Timing.Half:
                                    break;
                                case Timing.Full:
                                    //if (!IsHandActive && (_modifierList[index, 1] > 0 || _modifierList[index, 0] > 0))
                                    //{
                                    //    handCtrl.DetachAllItem();
                                    //    hAibu.SetIdleForItem(0, true);
                                    //}
                                    //else
                                    {
                                        LeanToKiss();
                                    }
                                    break;
                            }
                        }
                        else // Non-action Aibu mode.
                        {
                            switch (timing)
                            {
                                case Timing.Fraction:
                                case Timing.Half:
                                    break;
                                case Timing.Full:
                                    LeanToKiss();
                                    break;
                            }
                        }
                    }
                    else // Non-Aibu mode.
                    {
                        switch (timing)
                        {
                            case Timing.Fraction:
                            case Timing.Half:
                                PlayReaction();
                                break;
                            case Timing.Full:
                                Pull();
                                break;
                        }
                    }
                    break;
                case TrackpadDirection.Right:
                    switch (timing)
                    {
                        case Timing.Fraction:
                        case Timing.Half:
                            PlayShort(lstFemale[0]);
                            break;
                        case Timing.Full:
                            if (!IsHPointMove)
                            {
                                hFlag.click = ClickKind.pointmove;
                            }
                            else
                            {
                                _pov.StartCoroutine(RandomHPointMove(startScene: false));
                            }
                            break;
                    }
                    break;
                case TrackpadDirection.Left:
                    switch (timing)
                    {
                        case Timing.Fraction:
                        case Timing.Half:
                            PlayShort(lstFemale[0]);
                            break;
                        case Timing.Full:
                            if (IsTouchpadPress(wait.index))
                            {
                                // Any animation goes.
                                ChangeAnimation(-1);
                            }
                            else
                            {
                                // SameMode.
                                ChangeAnimation(3);
                            }
                            break;
                    }
                    break;
            }
        }

        public static bool PlayShort(ChaControl chara, bool voiceWait = true)
        {
            if (lstFemale.Contains(chara))
            {
                if (!voiceWait || !IsVoiceActive)
                {
                    hFlag.voice.playShorts[lstFemale.IndexOf(chara)] = Random.Range(0, 9);
                }
                return true;
            }
            else
            {
                Features.LoadVoice.PlayVoice(Features.LoadVoice.VoiceType.Short, chara, voiceWait);
            }
            return false;
        }
        private IEnumerator RandomHPointMove(bool startScene)
        {
            if (startScene)
            {
                hFlag.click = ClickKind.pointmove;
                yield return new WaitUntil(() => IsHPointMove);
            }
            var hPoint = GetHPointMove;
            var key = hPoint.dicObj.ElementAt(Random.Range(0, hPoint.dicObj.Count)).Key;
            ChangeCategory(GetHPointCategoryList.IndexOf(key));
            yield return null;
            var dicList = hPoint.dicObj[hPoint.nowCategory];
            var hPointData = dicList[Random.Range(0, dicList.Count)].GetComponent<H.HPointData>();
            hPoint.actionSelect(hPointData, hPoint.nowCategory);
            Singleton<Scene>.Instance.UnLoad();

        }
        internal static void SetSelectKindTouch(AibuColliderKind colliderKind)
        {
            handCtrl.selectKindTouch = colliderKind;
        }
        private int GetCurrentBackIdleIndex()
        {
            var anim = _aibuAnims.Where(anim => anim.StartsWith(hFlag.nowAnimStateName.Remove(2), StringComparison.Ordinal)).FirstOrDefault();
            var index = _aibuAnims.IndexOf(anim);
            _backIdle = index == 4 ? 0 : index;
            return index;
        }
        public static void LeanToKiss()
        {
            HScenePatches.HoldKissLoop();
            CaressHelper.Instance.OnFakeKiss();
            SetPlay(_aibuAnims[4]);
        }
        private void ScrollAibuAnim(bool increase)
        {
            var index = GetCurrentBackIdleIndex() + (increase ? 1 : -1);
            if (index > 3)
            {
                index = 1;
            }
            else if (index < 1)
            {
                index = 3;
            }
            _pov.StartCoroutine(PlayAnimOverTime(index));

            VRPlugin.Logger.LogDebug($"PlayAibuAnim:{_aibuAnims[index]}:{index}");
        }
        private void PlayReaction()
        {
            var nowAnim = hFlag.nowAnimStateName;
            switch (mode)
            {
                case EMode.houshi:
                    if (IsActionLoop)
                    {
                        if (hFlag.nowAnimationInfo.kindHoushi == 1)
                        {
                            handCtrl.Reaction(AibuColliderKind.reac_head);
                        }
                        else if (hFlag.nowAnimationInfo.kindHoushi == 2)
                        {
                            handCtrl.Reaction(AibuColliderKind.reac_bodyup);
                        }
                        else
                        {
                            handCtrl.Reaction(AibuColliderKind.reac_armR);
                        }
                    }
                    else
                    {
                        goto default;
                    }
                    break;
                case EMode.sonyu:
                    if (IsAfterClimaxInside(nowAnim) || IsInsertIdle(nowAnim) || IsActionLoop)
                    {
                        handCtrl.Reaction(AibuColliderKind.reac_bodydown);
                    }
                    else
                    {
                        goto default;
                    }
                    break;
                default:
                    var items = handCtrl.GetUseItemNumber();
                    var count = items.Count;
                    if (count != 0)
                    {
                        var item = items[Random.Range(0, count)];
                        handCtrl.Reaction(handCtrl.useItems[item].kindTouch < AibuColliderKind.kokan ? AibuColliderKind.reac_bodyup : AibuColliderKind.reac_bodydown);
                    }
                    break;

            }

        }
        private IEnumerator PlayAnimOverTime(int index)
        {
            PlayReaction();
            yield return new WaitForSeconds(0.25f);
            hAibu.backIdle = -1;
            HScenePatches.suppressSetIdle = true;
            SetPlay(_aibuAnims[index]);
        }
        public static void SetPlay(string animation)
        {
            lstProc[(int)hFlag.mode].SetPlay(animation, true);
        }
        private void MoveCategory(bool increase)
        {
            var list = GetHPointCategoryList;
            var index = list.IndexOf(GetHPointMove.nowCategory);
            if (increase)
            {
                if (index == list.Count - 1)
                {
                    index = 0;
                }
                else
                {
                    index++;
                }
            }
            else
            {
                if (index == 0)
                {
                    index = list.Count - 1;
                }
                else
                {
                    index--;
                }    
            }
            ChangeCategory(index);
        }
        private void ChangeCategory(int index)
        {
            var list = GetHPointCategoryList;
            GetHPointMove.SelectPointVisible(list[index], true);
            GetHPointMove.nowCategory = list[index];
        }
        private int GetCurrentLoop(bool increase)
        {
            if (IsWeakLoop)
            {
                return increase ? 1 : 0;
            }
            if (IsStrongLoop)
            {
                return increase ? 2 : 0;
            }
            // OLoop
            return increase ? 2 : 1;
        }
        private bool InsertHelper()
        {
            var nowAnim = hFlag.nowAnimStateName;
            if (mode == EMode.sonyu)
            {
                VRPlugin.Logger.LogDebug($"InsertHelper[1]");
                if (IsInsertIdle(nowAnim) || IsAfterClimaxInside(nowAnim))
                {
                    VRPlugin.Logger.LogDebug($"InsertHelper[1][1]");
                    // Sonyu start auto.
                    hFlag.click = ClickKind.modeChange;
                    WaitFrame(1);
                }
                else
                {
                    VRPlugin.Logger.LogDebug($"InsertHelper[0]");
                    return true;
                }
            }
            else if (mode == EMode.houshi)
            {
                VRPlugin.Logger.LogDebug($"InsertHelper[2]");
                if (IsClimaxHoushiInside(nowAnim))
                {
                    VRPlugin.Logger.LogDebug($"InsertHelper[2][1]");
                    hFlag.click = ClickKind.drink;
                }
                else if (IsIdleOutside(nowAnim))
                {
                    VRPlugin.Logger.LogDebug($"InsertHelper[2][2]");
                    // Start houshi after pose change/long pause after finish.
                    hFlag.click = ClickKind.speedup;
                }
                else if (IsAfterClimaxHoushiInside(nowAnim) || IsAfterClimaxOutside(nowAnim))
                {
                    VRPlugin.Logger.LogDebug($"InsertHelper[2][3]");
                    // Restart houshi.
                    RandomButton();
                }
                else
                {
                    VRPlugin.Logger.LogDebug($"InsertHelper[0]");
                    return true;
                }
            }
            else
            {
                VRPlugin.Logger.LogDebug($"InsertHelper[0]");
                return true;
            }

            return false;
        }
        private int _frameWait;
        private bool PullHelper()
        {
            var nowAnim = hFlag.nowAnimStateName;
            
            if (mode == EMode.sonyu)
            {
                if (IsIdleOutside(nowAnim) || IsAfterClimaxOutside(nowAnim))
                {
                    // When outside pull back to get condom on. Extra plugin disables auto condom on denial.
                    VRPlugin.Logger.LogDebug($"Pull:CondomClick");
                    sprite.CondomClick();
                }
                else if (IsFinishLoop)
                {
                    VRPlugin.Logger.LogDebug($"Pull:Outside");
                    hFlag.finish = FinishKind.outside;
                }
                else if (IsActionLoop)
                {
                    VRPlugin.Logger.LogDebug($"Pull:StopAuto");
                    hFlag.click = ClickKind.modeChange;
                    WaitFrame(3);
                }
                else 
                { 
                    return true; 
                }
            }
            else if (mode == EMode.houshi)
            {
                if (IsClimaxHoushiInside(nowAnim))
                {
                    hFlag.click = ClickKind.vomit;
                }
                else if (IsActionLoop)
                {
                    lstProc[(int)hFlag.mode].MotionChange(0);
                }
                else
                {
                    return true;
                }
            }
            return false;
        }
        
        internal void OnPoseChange(HSceneProc.AnimationListInfo anim)
        {
            switch (anim.mode)
            {
                case EMode.houshi:
                case EMode.houshi3P:
                case EMode.houshi3PMMF:
                    mode = EMode.houshi;
                    break;
                case EMode.sonyu:
                case EMode.sonyu3P:
                case EMode.sonyu3PMMF:
                    mode = EMode.sonyu;
                    break;
                default:
                    mode = anim.mode;
                    break;
            }
            adjustDirLight = true;
            foreach (var hand in _hands)
            {
                hand.Grasp.OnPoseChange();
            }
        }
        internal static void OnSpotChange()
        {
            adjustDirLight = true;
        }
        private void Insert(bool noVoice, bool anal)
        {
            if (!IsFrameWait() && InsertHelper())
            {
                VRPlugin.Logger.LogDebug($"Insert");
                ClickButton(GetButtonName(anal, hFlag.isDenialvoiceWait || noVoice));
            }
        }
        private string GetButtonName(bool anal, bool noVoice)
        {
            string name;
            switch (mode)
            {
                case EMode.sonyu:
                    name = "Insert";
                    if (anal)
                    {
                        name += "Anal";
                    }
                    if (noVoice)
                    {
                        name += "_novoice";
                    }
                    break;
                default:
                    name = "";
                    break;
            }
            VRPlugin.Logger.LogDebug($"GetButtonName:{name}");
            return name;
        }
        private IEnumerator DoAfter(float wait, Action action, params object[] args)
        {
            wait += Time.time;
            yield return new WaitUntil(() => wait > Time.time);
            action.DynamicInvoke(args);
        }
        private void Pull()
        {
            if (!IsFrameWait() && PullHelper())
            {
                VRPlugin.Logger.LogDebug($"Pull:Pull");
                ClickButton("Pull");
            }
        }
        private bool IsFrameWait()
        {
            if (_frameWait != 0)
            {
                VRPlugin.Logger.LogDebug($"FrameWait");
                if (!CrossFader.IsInTransition)
                {
                    _frameWait--;
                }
                _manipulateSpeed = true;
                return true;
            }
            return false;
        }
        private void WaitFrame(int count)
        {
            _frameWait = count;
            _manipulateSpeed = true;
        }
        /// <summary>
        /// Empty string to click whatever is there(except houshi slow/fast), otherwise checks start of the string and clicks corresponding button.
        /// </summary>
        private void RandomButton()
        {
            ClickButton("");
        }
        private static HitReaction _hitReaction;
        public void HitReactionInitialize(IEnumerable<ChaControl> charas)
        {
            if (_hitReaction == null)
            {
                _hitReaction = handCtrl1.hitReaction;
            }
            ControllerTracker.Initialize(charas);
            HandHolder.UpdateHandlers<HSceneHandler>();
        }
        public static void HitReactionPlay(AibuColliderKind aibuKind, ChaControl chara)
        {
            // This roundabout way is to allow player to touch anybody present, including himself, janitor,
            // and charas from kPlug (actually don't know if they have FullBodyBipedIK or not, because we need it).

            // TODO voice is a placeHolder, in h we have a good dic lying around with the proper ones.

            VRPlugin.Logger.LogDebug($"HScene:Reaction:{aibuKind}:{chara}");
            _hitReaction.ik = chara.objAnim.GetComponent<FullBodyBipedIK>();

            var dic = handCtrl.dicNowReaction;
            if (dic.Count == 0)
            {
                dic = TalkSceneExtras.dicNowReactions;
            }
            var key = aibuKind - AibuColliderKind.reac_head;
            var index = Random.Range(0, dic[key].lstParam.Count);
            var reactionParam = dic[key].lstParam[index];
            var array = new Vector3[reactionParam.lstMinMax.Count];
            for (int i = 0; i < reactionParam.lstMinMax.Count; i++)
            {
                array[i] = new Vector3(Random.Range(reactionParam.lstMinMax[i].min.x, reactionParam.lstMinMax[i].max.x),
                    Random.Range(reactionParam.lstMinMax[i].min.y, reactionParam.lstMinMax[i].max.y),
                    Random.Range(reactionParam.lstMinMax[i].min.z, reactionParam.lstMinMax[i].max.z));
                array[i] = chara.transform.TransformDirection(array[i].normalized);
            }
            _hitReaction.weight = dic[key].weight;
            _hitReaction.HitsEffector(reactionParam.id, array);
            _lateHitReaction = true;
            _lstIKEffectLateUpdate.AddRange(dic[key].lstReleaseEffector);
            
            PlayShort(chara, voiceWait: false);
        }
    }
}
