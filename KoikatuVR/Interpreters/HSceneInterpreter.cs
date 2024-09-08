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

namespace KK_VR.Interpreters
{
    class HSceneInterpreter : SceneInterpreter
    {
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


        //private bool _active;
        private readonly VRMouth _vrMouth;
        private readonly PoV _pov;
        private readonly VRMoverH _vrMoverH;
        private readonly bool _sensibleH;
        private readonly float[] _waitTimestamp = new float[2];
        private readonly float[] _waitTime = new float[2];
        private readonly bool[] _manipulateSpeed = new bool[2];
        private readonly bool[] _waitForAction = new bool[2];
        //private bool _addedModifier;
        private readonly TrackpadDirection[] _lastDir = new TrackpadDirection[2];
        //private State _state;
        private HPointMove _hPointMove;
        //private HSceneHandler[] _handlers;

        private readonly static List<int> lstIKEffectLateUpdate = new List<int>();
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

        private readonly int[] _modifierList = new int[2];
        public static bool IsInsertIdle => hFlag.nowAnimStateName.EndsWith("InsertIdle", StringComparison.Ordinal);
        public static bool IsIdleOutside => hFlag.nowAnimStateName.Equals("Idle");
        public static bool IsEndInside => hFlag.nowAnimStateName.EndsWith("IN_A", StringComparison.Ordinal);
        public static bool IsEndOutside => hFlag.nowAnimStateName.EndsWith("OUT_A", StringComparison.Ordinal);
        public static bool IsEndInMouth => hFlag.nowAnimStateName.StartsWith("Oral", StringComparison.Ordinal);
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

        private readonly Action<string> ClickButton;
        private readonly Action<int> ChangeLoop;
        /// <summary>
        /// -1 for all, otherwise (HFlag.EMode) 0...2 for specific, or anything higher(e.g. 3) for current EMode.
        /// </summary>
        private readonly Action<int> ChangeAnimation;
        private List<HSceneHandler> _hSceneHandlers;

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
            _vrMouth = VR.Camera.gameObject.AddComponent<VRMouth>();
            _vrMoverH = VR.Camera.gameObject.AddComponent<VRMoverH>();
            _vrMoverH.Initialize();
            HitReactionInitialize(hFlag.lstHeroine[0].chaCtrl);

            CrossFader.HSceneHooks.SetFlag(hFlag);

            var type = AccessTools.TypeByName("KK_SensibleH.AutoMode.LoopController");
            _sensibleH = type != null;

            if (_sensibleH)
            {
                var methodButton = AccessTools.FirstMethod(type, m => m.Name.Equals("ActionButton"));
                var methodLoop = AccessTools.FirstMethod(type, m => m.Name.Equals("AlterLoop"));
                var methodAnimation = AccessTools.FirstMethod(type, m => m.Name.Equals("PickAnimation"));
                ClickButton = AccessTools.MethodDelegate<System.Action<string>>(methodButton);
                ChangeLoop = AccessTools.MethodDelegate<System.Action<int>>(methodLoop);
                ChangeAnimation = AccessTools.MethodDelegate<System.Action<int>>(methodAnimation);
            }
            VRBoop.RefreshDynamicBones(true);
        }
        public override void OnDisable()
        {
            GameObject.Destroy(_pov);
            GameObject.Destroy(_vrMouth);
            GameObject.Destroy(_vrMoverH);
            ModelHandler.DestroyHandlerComponent<HSceneHandler>();
            //DestroyControllerComponent<HSceneHandler>();
        }
        public override void OnUpdate()
        {
            if (_manipulateSpeed[0])
            {
                if (_lastDir[0] == TrackpadDirection.Up)
                {
                    SpeedUp(0);
                }
                else if (_lastDir[0] == TrackpadDirection.Down)
                {
                    SlowDown(0);
                }
            }
            if (_manipulateSpeed[1])
            {
                if (_lastDir[1] == TrackpadDirection.Up)
                {
                    SpeedUp(1);
                }
                else if (_lastDir[1] == TrackpadDirection.Down)
                {
                    SlowDown(1);
                }
            }
            if (_waitForAction[0] && _waitTimestamp[0] < Time.time)
            {
                PickAction(Timing.Full, 0);
            }
            if (_waitForAction[1] && _waitTimestamp[1] < Time.time)
            {
                PickAction(Timing.Full, 1);
            }
            //else
            //{
            //    // We catch modifiers before action button pressed and filter them.
            //    if (_addedModifier && !IsWait)
            //    {
            //        VRPlugin.Logger.LogDebug($"Update:ClearModifiers");
            //        _modifierList[0] = 0;
            //        _modifierList[1] = 0;
            //        _addedModifier = false;
            //    }
            //}

            // Exit through the title button in config doesn't trigger hook.
            if (hFlag == null)
            {
                KoikatuInterpreter.EndScene(KoikatuInterpreter.SceneType.HScene);
            }

        }
        public override void OnLateUpdate()
        {
            if (_lateHitReaction)
            {
                _lateHitReaction = false;
                _hitReaction.ReleaseEffector();
                _hitReaction.SetEffector(lstIKEffectLateUpdate);
                lstIKEffectLateUpdate.Clear();
            }
            if (adjustDirLight)
            {
                TalkSceneExtras.RepositionDirLight(lstFemale[0]);
                adjustDirLight = false;
            }
        }
        /// <param name="headset">Overrides index</param>
        private HSceneHandler GetHandler(int index, bool headset = false)
        {
            return ModelHandler.GetActiveHandler<HSceneHandler>(headset ? 0 : index + 1);
        }
        private void SpeedUp(int index)
        {
            if (mode == EMode.aibu)
            {
                hFlag.SpeedUpClickAibu(0.003f, hFlag.speedMaxAibuBody, true);
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
                    AttemptFinish(index);
                }
            }
        }
        private void SlowDown(int index)
        {
            if (mode == EMode.aibu)
            {
                hFlag.SpeedUpClickAibu(-0.003f, hFlag.speedMaxAibuBody, true);
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
                    AttemptStop(index);
                }
            }
        }
        private void AttemptFinish(int index)
        {
            if (hFlag.gaugeMale == 100f)
            {
                VRPlugin.Logger.LogDebug($"AttemptFinish");
                // There will be only one finish appropriate for the current mode/setting.
                RandomButton();
                _manipulateSpeed[index] = false;
            }
        }
        private void AttemptStop(int index)
        {
            // Happens only when we recently pressed the button.
            if (_waitForAction[0] || _waitForAction[1])
            {
                VRPlugin.Logger.LogDebug($"AttemptStop");
                Pull(index);
                _manipulateSpeed[index] = false;
            }
        }
        private void SetWaitTime(float duration, int index, bool action = true)
        {
            if (action)
            {
                if (IsInsertIdle)
                {
                    duration = 0.5f;
                }
                _waitForAction[index] = true;
            }
            _waitTimestamp[index] = Time.time + duration;
            _waitTime[index] = duration;
        }

        private bool SetHand()
        {
            VRPlugin.Logger.LogDebug($"Interpreter:HScene:SetHand");
            if (handCtrl.useItems[0] == null || handCtrl.useItems[1] == null)
            {
                var list = new List<int>();
                for (int i = 0; i < 6; i++)
                {
                    if (handCtrl.useAreaItems[i] == null)
                    {
                        list.Add(i);
                    }
                }
                list = list.OrderBy(a => Random.Range(0, 100)).ToList();
                var index = 0;
                foreach (var item in list)
                {
                    VRPlugin.Logger.LogDebug($"Interpreter:HScene:SetHand:Loop:{item}");
                    var clothState = handCtrl.GetClothState((AibuColliderKind)(item + 2));
                    //var layerInfo = handCtrl.dicAreaLayerInfos[item][handCtrl.areaItem[item]];
                    var layerInfo = handCtrl.dicAreaLayerInfos[item][0];
                    if (layerInfo.plays[clothState] == -1)
                    {
                        continue;
                    }
                    index = item;
                    break;

                }
                VRPlugin.Logger.LogDebug($"Interpreter:HScene:SetHand:Required:Choice - {index}");
                
                handCtrl.selectKindTouch = (AibuColliderKind)(index + 2);
                _pov.StartCoroutine(CaressUtil.ClickCo(() => handCtrl.selectKindTouch = AibuColliderKind.none));
                return false;
            }
            else
            {
                VRPlugin.Logger.LogDebug($"Interpreter:HScene:SetHand:NotRequired");
                PlayReaction();
                return true;
            }
        }
        public override bool OnDirectionUp(TrackpadDirection direction, int index)
        {
            VRPlugin.Logger.LogDebug($"OnButton:Up:{direction}");
            _waitForAction[index] = false;
            _manipulateSpeed[index] = false;
            var timing = _waitTimestamp[index] - Time.time;

            // Not interested in full wait as it performed automatically once reached via Update().
            if (timing > 0)
            {
                if (_waitTime[index] * 0.5f > timing)
                {
                    // More then a half, less then full wait case.
                    PickAction(Timing.Half, index);
                }
                else
                {
                    PickAction(Timing.Fraction, index);
                }
            }
            _modifierList[0] = 0;
            _modifierList[1] = 0;
            return false;
        }
        public override bool OnButtonDown(EVRButtonId buttonId, TrackpadDirection direction, int index)
        {
            VRPlugin.Logger.LogDebug($"OnButton:Down:{buttonId}:{direction}:Action - {_waitForAction}");
            switch (buttonId)
            {
                case EVRButtonId.k_EButton_SteamVR_Trigger:
                    VR.Input.Mouse.LeftButtonDown();
                    VRPlugin.Logger.LogInfo($"MouseClick:{handCtrl.selectKindTouch}");
                    _modifierList[0]++;
                    break;
                case EVRButtonId.k_EButton_Grip:
                    _modifierList[1]++;
                    break;
            }
            if (!_waitForAction[0] && !_waitForAction[1])
            {
                SetWaitTime(1f, index, action: false);
            }
            EvaluateModifiers(index);
            return false;
        }
        public override bool OnButtonUp(EVRButtonId buttonId, TrackpadDirection direction, int index)
        {
            switch (buttonId)
            {
                case EVRButtonId.k_EButton_SteamVR_Trigger:
                    VR.Input.Mouse.LeftButtonUp();
                    break;
            }
            return false;
        }
        public override bool OnDirectionDown(TrackpadDirection direction, int index)
        {
            VRPlugin.Logger.LogDebug($"OnButton:Down:{direction}:{IsHPointMove}:{IsActionLoop}:[{_modifierList[0]}][{_modifierList[1]}]");
            _lastDir[index] = direction;
            switch (direction)
            {
                case TrackpadDirection.Up:
                case TrackpadDirection.Down:
                    if (!GetHandler(index).DoUndress(direction == TrackpadDirection.Down))
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
                                    SetWaitTime(3f, index);
                                    _manipulateSpeed[index] = true;
                                }
                                else
                                {
                                    // Reaction/Attach hand/Start auto/Lean to kiss.
                                    SetWaitTime(1f, index);
                                }
                            }
                            else
                            {
                                _manipulateSpeed[index] = true;
                            }

                        }
                        else
                        {
                            // Attach hand/Lean to kiss.
                            SetWaitTime(0.5f, index);
                        }
                    }
                    break;
                case TrackpadDirection.Left:
                case TrackpadDirection.Right:
                    if (IsHPointMove)
                    {
                        if (direction == TrackpadDirection.Right)
                        {
                            SetWaitTime(1f, index);
                        }
                        else
                        {
                            GetHPointMove.Return();
                        }
                    }
                    else if (IsActionLoop)
                    {
                        if (mode == EMode.aibu)
                        {
                            ScrollAibuAnim(direction == TrackpadDirection.Right);
                        }
                        else
                        {
                            ChangeLoop(GetCurrentLoop(direction == TrackpadDirection.Right));
                        }
                    }
                    else
                    {
                        SetWaitTime(1f, index);
                    }
                    break;
            }
            if (_waitForAction[0] || _waitForAction[1])
            {
                return true;
            }
            else
                return false;
        }
        private IEnumerator StartItemAction(int button)
        {
            // SensibleH will overtake it by the time we release it.
            foreach (var item in handCtrl.useItems)
            {
                if (item !=  null)
                {
                    handCtrl.selectKindTouch = item.kindTouch;
                    break;
                }
            }
            if (!_pov.IsTriggerPress())
            {
                HandCtrlHooks.InjectMouseButtonDown(button);
                yield return new WaitForSeconds(1f);
                HandCtrlHooks.InjectMouseButtonUp(button);
            }
            else
            {
                yield return new WaitForSeconds(0.1f);
            }
            handCtrl.selectKindTouch = AibuColliderKind.none;
        }
        private void PickAction(Timing timing, int index)
        {
            _waitForAction[index] = false;
            _manipulateSpeed[index] = false;
            // Array[0] - Trigger modifier
            // Array[1] - Grip modifier
            VRPlugin.Logger.LogDebug($"PickAction:{_lastDir}:{timing}:[{_modifierList[0]}][{_modifierList[1]}]");
            switch (_lastDir[index])
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
                                    if (!IsHandActive)
                                    {
                                        SetHand();
                                    }
                                    break;
                                case Timing.Full:
                                    if (IsHandActive)
                                    {
                                        PlayReaction();
                                    }
                                    else if (IsHandAttached || SetHand())
                                    {
                                        _pov.StartCoroutine(StartItemAction(0));
                                    }
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
                                        PlayShort();
                                    }
                                    break;
                                case Timing.Half:
                                    // Put in denial + voice.
                                    break;
                                case Timing.Full:
                                    SetHand();
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
                                Insert(noVoice: _modifierList[0] > 0, anal: _modifierList[1] > 0, index);
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
                                    if (!IsHandActive && (_modifierList[1] > 0 || _modifierList[0] > 0))
                                    {
                                        handCtrl.DetachAllItem();
                                        hAibu.SetIdleForItem(0, true);
                                    }
                                    else
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
                                Pull(index);
                                break;
                        }
                    }
                    break;
                case TrackpadDirection.Right:
                    switch (timing)
                    {
                        case Timing.Fraction:
                        case Timing.Half:
                            PlayShort();
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
                            PlayShort();
                            break;
                        case Timing.Full:
                            if (_modifierList[1] > 0)
                            {
                                // Grip modifier, any animation goes.
                                ChangeAnimation(-1);
                            }
                            else
                            {
                                // No grip, specific or mode's default.
                                // Amount of Triggers defines mode. (HFlag.EMode)(Trigger Count - 1).
                                var triggerModifier = _modifierList[0] == 0 ? 3 : _modifierList[0] - 1;
                                ChangeAnimation(triggerModifier);
                            }
                            break;
                    }
                    break;
            }
            _lastDir[index] = TrackpadDirection.Center;
        }
        private void PlayShort(bool voiceWait = true)
        {
            if (!voiceWait || !IsVoiceActive)
            {
                hFlag.voice.playShorts[0] = Random.Range(0, 9);
            }
        }
        private IEnumerator RandomHPointMove(bool startScene)
        {
            if (startScene)
            {
                hFlag.click = ClickKind.pointmove;
                yield return new WaitUntil(() => IsHPointMove);
                //yield return null;
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
        private int GetCurrentAibuIndex()
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
            // Presence of Grip modifier allows to run kiss loop for ~5 second. During which it can stop being fake and continue indefinitely.
            // TODO Reorganize it a bit and put this as an automatic feature too in VRMouth.
            var index = GetCurrentAibuIndex() + (increase ? 1 : -1);
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
                    if (IsEndInside || IsInsertIdle || IsActionLoop)
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
                if (!increase)
                {
                    ChangeSpeed(increase);
                }
                return increase ? 1 : 0;
            }
            if (IsStrongLoop)
            {
                return increase ? 2 : 0;
            }
            // OLoop
            if (increase)
            {
                ChangeSpeed(increase);
            }
            return increase ? 2 : 1;
        }
        private void ChangeSpeed(bool increase)
        {
            hFlag.SpeedUpClick(increase ? 0.25f : -0.25f, 1f);
        }
        private void EvaluateModifiers(int index)
        {
            if (_waitForAction[index])
            {
                var number = 0;

                if (_lastDir[index] == TrackpadDirection.Left && !IsHPointMove)
                {
                    // Animation pick, default wait is long (5s).
                    // If we got the Grip modifier then no point in waiting, an extra Trigger and off we go.
                    number = _modifierList[1] > 0 ? 0 : 2;
                }
                if (_modifierList[0] > number)
                {
                    // Immediate action on the Trigger(s).
                    PickAction(Timing.Full, index);
                }
            }
        }
        private bool InsertHelper(int index)
        {
            if (IsInsertIdle || IsEndInside)
            {
                // Sonyu start auto.
                VRPlugin.Logger.LogDebug($"StartAuto");
                hFlag.click = ClickKind.modeChange;
                _manipulateSpeed[index] = true;
            }
            else if (IsEndInMouth)
            {
                hFlag.click = ClickKind.drink;
            }
            else if (mode == EMode.houshi && IsIdleOutside)
            {
                // On start of houshi pose. On consecutive action in same pose we'll handle it with button for extra voice.
                hFlag.click = ClickKind.speedup;
            }
            else
            {
                return true;
            }
            return false;
        }
        private bool PullHelper(int index)
        {
            if (IsIdleOutside || IsEndOutside)
            {
                // When outside pull back to get condom on. Extra plugin disables auto condom on denial.
                sprite.CondomClick();
            }
            else if (IsEndInMouth)
            {
                hFlag.click = ClickKind.vomit;
            }
            else if (IsFinishLoop)
            {
                hFlag.finish = FinishKind.outside;
            }
            else if (IsActionLoop)
            {
                if (mode == EMode.sonyu)
                {
                    VRPlugin.Logger.LogDebug($"StopAuto");
                    hFlag.click = ClickKind.modeChange;

                    // Will prompt the same action on the next frame.
                    _waitForAction[index] = true;
                }
                else// if (_mode == HFlag.EMode.houshi)
                {
                    lstProc[(int)hFlag.mode].MotionChange(0);
                }
            }
            else
            {
                return true;
            }
            return false;
        }
        internal static void OnPoseChange(HSceneProc.AnimationListInfo anim)
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
        }
        internal static void OnSpotChange()
        {
            adjustDirLight = true;
        }
        private void Insert(bool noVoice, bool anal, int index)
        {
            if (InsertHelper(index))
            {
                VRPlugin.Logger.LogDebug($"Insert");
                ClickButton(GetButtonName(anal, noVoice));
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
        private void Pull(int index)
        {
            if (PullHelper(index))
            {
                VRPlugin.Logger.LogDebug($"Pull");
                var name = "Pull";
                ClickButton(name);
            }
        }

        /// <summary>
        /// Empty string to click whatever is there(except houshi slow/fast), otherwise checks start of the string and clicks corresponding button.
        /// </summary>
        private void RandomButton()
        {
            ClickButton("");
        }
        private static HitReaction _hitReaction;
        public void HitReactionInitialize(ChaControl chara)
        {
            if (_hitReaction == null)
            {
                _hitReaction = handCtrl1.hitReaction;
            }
            ColliderTracker.Initialize(chara, hScene: true);
            _hSceneHandlers = ModelHandler.AddHandlerComponent<HSceneHandler>();
        }
        public static void HitReactionPlay(AibuColliderKind aibuKind, ChaControl chara)
        {
            // This roundabout way is to allow player to touch anybody present, including himself, janitor,
            // and charas from kPlug (actually don't know if they have FullBodyBipedIK or not, because we need it).

            // TODO voice is a placeHolder, in h we have a good dic lying around with the proper ones.

            VRPlugin.Logger.LogDebug($"Interpreter:Reaction:{aibuKind}:{chara}");
            _hitReaction.ik = chara.objAnim.GetComponent<FullBodyBipedIK>();

            var dic = handCtrl.dicNowReaction;
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
            lstIKEffectLateUpdate.AddRange(dic[key].lstReleaseEffector);
            if (!IsVoiceActive)
            {
                Features.LoadVoice.Play(Random.value < 0.5f ? Features.LoadVoice.VoiceType.Laugh : Features.LoadVoice.VoiceType.Short, chara);
            }
        }
    }
}
