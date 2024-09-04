using UnityEngine;
using VRGIN.Core;
using HarmonyLib;
using System.Collections.Generic;
using KK_VR.Camera;
using KK_VR.Features;
using System;
using static SteamVR_Events;
using Manager;
using System.Linq;
using System.Collections;
using KK_VR.Interpreters.Patches;
using KKAPI.Utilities;
using KK_VR.Caress;
using Random = UnityEngine.Random;
using static HFlag;
using static HandCtrl;
using ADV.Commands.Chara;
using Illusion.Extensions;
using VRGIN.Controls;
using static VRGIN.Controls.Controller;
using static SteamVR_Controller;
using Valve.VR;
using KK_VR.Controls;

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


        private bool _active;
        private HSceneProc _proc;
        private Caress.VRMouth _vrMouth;
        private PoV _pov;
        private VRMoverH _vrMoverH;
        private bool _sensibleH;
        private float _waitTimestamp;
        private float _waitTime;
        private bool _manipulateSpeed;
        private bool _waitForAction;
        private bool _addedModifier;
        private TrackpadDirection _lastDir;
        //private State _state;
        private HPointMove _hPointMove;
        private int _backIdle;

        internal static HSceneInterpreter Instance;
        internal static HFlag hFlag;
        internal static HSprite sprite;
        internal static HFlag.EMode mode;
        internal static HandCtrl handCtrl;
        internal static HAibu hAibu;
        internal static HVoiceCtrl hVoice;
        internal static List<HActionBase> lstProc;

        private List<EVRButtonId> _modifierList = new List<EVRButtonId>();
        public static bool IsActive => Instance != null && Instance._active;
        public static bool IsInsertIdle => hFlag.nowAnimStateName.EndsWith("InsertIdle", StringComparison.Ordinal);
        public static bool IsIdleOutside => hFlag.nowAnimStateName.Equals("Idle");
        public static bool IsEndInside => hFlag.nowAnimStateName.EndsWith("IN_A", StringComparison.Ordinal);
        public static bool IsEndOutside => hFlag.nowAnimStateName.EndsWith("OUT_A", StringComparison.Ordinal);
        public static bool IsEndInMouth => hFlag.nowAnimStateName.StartsWith("Oral", StringComparison.Ordinal);
        public static bool IsFinishLoop => hFlag.finish != HFlag.FinishKind.none && IsOrgasmLoop;
        public static bool IsWeakLoop => hFlag.nowAnimStateName.EndsWith("WLoop", StringComparison.Ordinal);
        public static bool IsStrongLoop => hFlag.nowAnimStateName.EndsWith("SLoop", StringComparison.Ordinal);
        public static bool IsOrgasmLoop => hFlag.nowAnimStateName.EndsWith("OLoop", StringComparison.Ordinal);
        public static bool IsKissAnim => hFlag.nowAnimStateName.StartsWith("K_", StringComparison.Ordinal);
        public HPointMove GetHPointMove => _hPointMove == null ? _hPointMove = UnityEngine.Object.FindObjectOfType<HPointMove>() : _hPointMove;
        public static int GetBackIdle => Instance._backIdle;
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
        private List<string> _aibuAnims = new List<string>()
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
        private bool IsWait => _waitTimestamp > Time.time;

        private System.Action<string> ClickButton;
        private System.Action<int> ChangeLoop;
        /// <summary>
        /// -1 for all, otherwise (HFlag.EMode) 0...2 for specific, or anything higher(e.g. 3) for current EMode.
        /// </summary>
        private System.Action<int> ChangeAnimation;

        public override void OnStart()
        {
            Instance = this;
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
        }

        public override void OnDisable()
        {
            Deactivate();
        }
        public override void OnUpdate()
        {
            if (_active)
            {
                if (!_proc || !_proc.enabled)
                {
                    // The HProc scene is over, but there may be one more coming.
                    Deactivate();
                    return;
                }
                if (_manipulateSpeed)
                {
                    if (_lastDir == TrackpadDirection.Up)
                    {
                        SpeedUp();
                    }
                    else if (_lastDir == TrackpadDirection.Down)
                    {
                        SlowDown();
                    }
                }
                if (_waitForAction)
                {
                    if (!IsWait)
                    {
                        PickAction(Timing.Full);
                    }
                }
                else
                {
                    // We catch modifiers before action button pressed and filter them.
                    if (_addedModifier && !IsWait)
                    {
                        VRPlugin.Logger.LogDebug($"Update:ClearModifiers");
                        _modifierList.Clear();
                        _addedModifier = false;
                    }
                }
            }

            if (!_active &&
                Scene.GetRootComponent<HSceneProc>("HProc") is HSceneProc proc &&
                proc.enabled)
            {
                _proc = proc;
                _active = true;
                var traverse = Traverse.Create(proc);
                hFlag = traverse.Field("flags").GetValue<HFlag>();
                sprite = traverse.Field("sprite").GetValue<HSprite>();
                handCtrl = traverse.Field("hand").GetValue<HandCtrl>();
                lstProc = traverse.Field("lstProc").GetValue<List<HActionBase>>();
                hVoice = traverse.Field("voice").GetValue<HVoiceCtrl>();
                hAibu = (HAibu)lstProc[0];

                _pov = VR.Camera.gameObject.AddComponent<PoV>();
                _pov.Initialize(proc);
                _vrMouth = VR.Camera.gameObject.AddComponent<Caress.VRMouth>();
                _vrMoverH = VR.Camera.gameObject.AddComponent<VRMoverH>();
                _vrMoverH.Initialize(proc);
                AddControllerComponent<CaressController>();

                CrossFader.HSceneHooks.SetFlag(hFlag);
            }
        }
        private void SpeedUp()
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
                    AttemptFinish();
                }
            }
        }
        private void SlowDown()
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
                    AttemptStop();
                }
            }
        }
        private void AttemptFinish()
        {
            if (hFlag.gaugeMale == 100f)
            {
                VRPlugin.Logger.LogDebug($"AttemptFinish");
                // There will be only one finish appropriate for the current mode/setting.
                RandomButton();
                _manipulateSpeed = false;
            }
        }
        private void AttemptStop()
        {
            // Happens only when we recently pressed the button.
            if (IsWait)
            {
                VRPlugin.Logger.LogDebug($"AttemptStop");
                Pull();
                _manipulateSpeed = false;
            }
        }
        private void SetWaitTime(float duration, bool action = true)
        {
            if (action)
            {
                if (IsInsertIdle)
                {
                    duration = 0.5f;
                }
                _waitForAction = true;
            }
            _waitTimestamp = Time.time + duration;
            _waitTime = duration;
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
        public override bool OnButtonUp(TrackpadDirection direction)
        {
            VRPlugin.Logger.LogDebug($"OnButton:Up:{direction}");
            _waitForAction = false;
            _manipulateSpeed = false;
            var timing = _waitTimestamp - Time.time;

            // Not interested in full wait as it performed automatically once reached via Update().
            if (timing > 0)
            {
                if (_waitTime * 0.5f > timing)
                {
                    // More then a half, less then full wait case.
                    PickAction(Timing.Half);
                }
                else
                {
                    PickAction(Timing.Fraction);
                }
            }
            return false;
        }
        public override bool OnButtonDown(TrackpadDirection direction, EVRButtonId buttonId)
        {
            VRPlugin.Logger.LogDebug($"OnButton:Down:{direction}:{buttonId}");
            if (direction == TrackpadDirection.Center)
            {

            }
            return false;
        }
        public override bool OnButtonDown(EVRButtonId buttonId)
        {
            VRPlugin.Logger.LogDebug($"OnButton:Down:{buttonId}:Action - {_waitForAction}");
            _modifierList.Add(buttonId);
            _addedModifier = true;
            if (!_waitForAction)
            {
                SetWaitTime(1f, action: false);
            }
            EvaluateModifiers();
            return _waitForAction;
        }

        public override bool OnButtonDown(TrackpadDirection direction)
        {
            var array = EvaluateModifiers();
            VRPlugin.Logger.LogDebug($"OnButton:Down:{direction}:{IsHPointMove}:{IsActionLoop}:[{array[0]}][{array[1]}]");
            _lastDir = direction;

            if (IsHPointMove)
            {
                switch (direction)
                {
                    case TrackpadDirection.Up:
                    case TrackpadDirection.Down:
                        MoveCategory(direction == TrackpadDirection.Down);
                        break;
                    case TrackpadDirection.Left:
                        GetHPointMove.Return();
                        break;
                    case TrackpadDirection.Right:
                        SetWaitTime(1f);
                        break;
                }
            }
            else if (IsActionLoop)
            {
                if (mode == EMode.aibu)
                {
                    switch (direction)
                    {
                        case TrackpadDirection.Up:
                            if (IsHandActive)
                            {
                                // Reaction if too long, speed meanwhile.
                                SetWaitTime(3f);
                                _manipulateSpeed = true;
                            }
                            else
                            {
                                // Reaction/Attach hand/Start auto
                                SetWaitTime(1f);
                            }
                            break;
                        case TrackpadDirection.Down:
                            if (IsHandActive)
                            {
                                // Lean to kiss if too long, speed meanwhile.
                                SetWaitTime(3f);
                                _manipulateSpeed = true;
                            }
                            else
                            {
                                // Lean to kiss.
                                SetWaitTime(1f);
                            }
                            break;
                        case TrackpadDirection.Left:
                        case TrackpadDirection.Right:
                            ScrollAibuAnim(direction == TrackpadDirection.Right);
                            break;
                    }
                }
                else // Non-Aibu mode.
                {
                    switch (direction)
                    {
                        case TrackpadDirection.Up:
                        case TrackpadDirection.Down:
                            _manipulateSpeed = true;
                            break;
                        case TrackpadDirection.Left:
                        case TrackpadDirection.Right:
                            ChangeLoop(GetCurrentLoop(direction == TrackpadDirection.Right));
                            break;

                            // No respect for lefties.
                            // In all honesty though this has to be done on fundamental level in settings and then a lot of functional of the whole plugin reworked/adjusted. 
                            // But who will do it? There is like no (legit) lefties among code monkeys, and this whole work is voluntary, so..
                    }
                }
            }
            else
            {
                if (mode == EMode.aibu)
                {
                    switch (direction)
                    {
                        case TrackpadDirection.Up:
                            // Attach hand
                            SetWaitTime(0.5f);
                            break;
                        case TrackpadDirection.Down:
                            // Lean to kiss
                            SetWaitTime(0.5f);
                            break;
                        case TrackpadDirection.Left:
                        case TrackpadDirection.Right:
                            // PointMove/ChangeAnim
                            SetWaitTime(1f);
                            break;
                    }
                }
                else
                    SetWaitTime(1f);
            }
            if (_waitForAction)
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
        private void PickAction(Timing timing)
        {
            _waitForAction = false;
            _manipulateSpeed = false;
            // Array[0] - Trigger modifier
            // Array[1] - Grip modifier
            var array = EvaluateModifiers();
            VRPlugin.Logger.LogDebug($"PickAction:{_lastDir}:{timing}:[{array[0]}][{array[1]}]");
            switch (_lastDir)
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
                                Insert(noVoice: array[0] > 0, anal: array[1] > 0);
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
                                    if (!IsHandActive && (array[1] > 0 || array[0] > 0))
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
                            if (array[1] > 0)
                            {
                                // Grip modifier, any animation goes.
                                ChangeAnimation(-1);
                            }
                            else
                            {
                                // No grip, specific or mode's default.
                                // Amount of Triggers defines mode. (HFlag.EMode)(Trigger Count - 1).
                                var triggerModifier = array[0] == 0 ? 3 : array[0] - 1;
                                ChangeAnimation(triggerModifier);
                            }
                            break;
                    }
                    break;
            }
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
                hFlag.click = HFlag.ClickKind.pointmove;
                yield return new WaitUntil(() => IsHPointMove);
                //yield return null;
            }
            var hPoint = GetHPointMove;
            var key = hPoint.dicObj.ElementAt(UnityEngine.Random.Range(0, hPoint.dicObj.Count)).Key;
            ChangeCategory(GetHPointCategoryList.IndexOf(key));
            yield return null;
            var dicList = hPoint.dicObj[hPoint.nowCategory];
            var hPointData = dicList[UnityEngine.Random.Range(0, dicList.Count)].GetComponent<H.HPointData>();
            hPoint.actionSelect(hPointData, hPoint.nowCategory);
            Singleton<Manager.Scene>.Instance.UnLoad();

        }
        private int GetCurrentAibuIndex()
        {
            var anim = _aibuAnims.Where(anim => anim.StartsWith(hFlag.nowAnimStateName.Remove(2), StringComparison.Ordinal)).FirstOrDefault();
            var index = _aibuAnims.IndexOf(anim);
            _backIdle = index == 4 ? 0 : index;
            return index;
        }
        internal void LeanToKiss()
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
        internal void SetPlay(string animation)
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
        private int[] EvaluateModifiers()
        {
            var array = new int[2];
            foreach (var modifier in _modifierList )
            {
                if (modifier == EVRButtonId.k_EButton_SteamVR_Trigger)
                {
                    array[0]++;
                }
                else
                {
                    array[1]++;
                }
            }
            if (_waitForAction)
            {
                var number = 0;

                if (_lastDir == TrackpadDirection.Left && !IsHPointMove)
                {
                    // Animation pick, default wait is long (5s).
                    // If we got the Grip modifier then no point in waiting, an extra Trigger and off we go.
                    number = array[1] > 0 ? 0 : 2;
                }
                if (array[0] > number)
                {
                    // Immediate action on the Trigger(s).
                    PickAction(Timing.Full);
                }
            }
            return array;
        }
        private void Deactivate()
        {
            if (_active)
            {
                GameObject.Destroy(_pov);
                GameObject.Destroy(_vrMouth);
                GameObject.Destroy(_vrMoverH);
                DestroyControllerComponent<Caress.CaressController>();
                _proc = null;
                _active = false;
            }
        }
        private bool InsertHelper()
        {
            if (IsInsertIdle || IsEndInside)
            {
                // Sonyu start auto.
                VRPlugin.Logger.LogDebug($"StartAuto");
                hFlag.click = HFlag.ClickKind.modeChange;
                _manipulateSpeed = true;
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
        private bool PullHelper()
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
                    _waitForAction = true;
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

        }
        private void Insert(bool noVoice, bool anal)
        {
            if (InsertHelper())
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
        private void Pull()
        {
            if (PullHelper())
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

    }
}
