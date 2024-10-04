using System;
using System.Collections.Generic;
using System.Linq;
using VRGIN.Core;
using UnityEngine;
using Manager;
using UnityEngine.UI;
using ADV;
using Random = UnityEngine.Random;
using RootMotion.FinalIK;
using KK_VR.Fixes;
using Valve.VR;
using static HandCtrl;
using static KK_VR.Interpreters.Extras.TalkSceneExtras;
using static VRGIN.Controls.Controller;
using KK_VR.Interpreters.Extras;
using KK_VR.Handlers;
using KK_VR.Features;
using KK_VR.Camera;

namespace KK_VR.Interpreters
{
    class TalkSceneInterpreter : SceneInterpreter
    {
        public static float talkDistance = 0.55f;
        public static float height;
        public static TalkScene talkScene;
        public static ADVScene advScene;
        internal static bool afterH;

        private static HitReaction _hitReaction;
        private readonly static List<int> lstIKEffectLateUpdate = new List<int>();
        private static bool _lateHitReaction;

        private Button _lastSelectedCategory;
        private Button _lastSelectedButton;
        private bool _talkSceneStart;
        private bool _advSceneStart;
        private bool _sittingPose;
        private bool _talkScenePreSet;
        private readonly bool[] _waitForAction = new bool[2];
        private readonly float[] _waitTimestamp = new float[2];
        private readonly float[] _waitTime = new float[2];
        private readonly TrackpadDirection[] _lastDirection = new TrackpadDirection[2];
        //private Button _previousButton;
        //private TalkSceneHandler[] _handlers;
        private readonly int[,] _modifierList = new int[2, 2];

        private bool IsADV => advScene.isActiveAndEnabled;
        private bool IsChoice => advScene.scenario.isChoice;
        enum State
        {
            Talk,
            None,
            Event
        }
        public TalkSceneInterpreter(MonoBehaviour behaviour)
        {
            if (behaviour != null)
            {
                VRPlugin.Logger.LogDebug($"TalkScene:Start:Talk");
                talkScene = (TalkScene)behaviour;
                _talkSceneStart = true;
            }
            else
            {
                VRPlugin.Logger.LogDebug($"TalkScene:Start:Adv");
                _advSceneStart = true;
            }
            advScene = Game.Instance.actScene.advScene;
            SetHeight();
        }
        private void SetHeight()
        {
            if (height == 0f && Game.Instance.actScene != null && Game.Instance.actScene.Player.chaCtrl != null)
            {
                var player = Game.Instance.actScene.Player.chaCtrl;
                height = player.objHeadBone.transform
                .Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz/cf_J_Eye_tz")
                .position.y - player.transform.position.y;
            }
        }
        //public override void OnStart()
        //{
        //    VRPlugin.Logger.LogDebug($"TalkScene:Start:Adv = {talkScene == null}");
        //    _advSceneStart = true;

        //    //TalkScene.otherInitialize += () =>
        //    //{
        //    //    VRPlugin.Logger.LogDebug($"TalkScene:TriggerAction:otherInitialize");
        //    //    _adjustmentRequired = true;
        //    //};
        //}
        public override void OnDisable()
        {
            ModelHandler.DestroyHandlerComponent<TalkSceneHandler>();
            TalkSceneExtras.ReturnDirLight();
            VRMale.ForceShowHead = false;
        }
        public override void OnUpdate()
        {
            if (talkScene == null && (advScene == null || !advScene.isActiveAndEnabled))
            {
                KoikatuInterpreter.EndScene(KoikatuInterpreter.SceneType.TalkScene);
            }
            if (_talkSceneStart)
            {
                if (!_talkScenePreSet && talkScene.targetHeroine != null)
                {
                    _talkScenePreSet = true;
                    _sittingPose = (talkScene.targetHeroine.chaCtrl.objHead.transform.position - talkScene.targetHeroine.transform.position).y < 1f;
                }
                if (talkScene.cameraMap.enabled)
                {
                    AdjustTalkScene(); 
                }
            }
            //if (_advSceneStart
            //    //&& advScene.Scenario != null 
            //    //&& advScene.Scenario._isStartRun 
            //    && advScene.Scenario.currentChara != null
            //    && advScene.Scenario.currentChara.initialized
            //    && (advScene.Scenario.currentChara.chaCtrl.transform.position != Vector3.zero || Manager.Scene.Instance.sceneFade._Color.a < 1f))
            //if (_advSceneStart && !Manager.Scene.Instance.IsFadeNow && advScene.Scenario.currentChara.chaCtrl != null)
            if (_advSceneStart
                && advScene.Scenario.currentChara != null
                && Manager.Scene.Instance.sceneFade._Color.a < 1f)
            {
                AdjustAdvScene();
            }
            //foreach (var action in _waitForAction)
            //{
            //    if (action)
            //    {

            //    }
            //}
            if (_waitForAction[0] && _waitTimestamp[0] < Time.time)
            {
                PickAction(Timing.Full, 0);
            }
            if (_waitForAction[1] && _waitTimestamp[1] < Time.time)
            {
                PickAction(Timing.Full, 1);
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
        }
        public void OverrideAdv()
        {
            VRPlugin.Logger.LogDebug($"OverrideAdv");
            _advSceneStart = false;
            _talkSceneStart = true;
        }
        private void AdjustAdvScene()
        {
            VRPlugin.Logger.LogDebug($"AdjustAdvScene");
            _advSceneStart = false;
            var chara = advScene.Scenario.currentChara.chaCtrl;
            //VRPlugin.Logger.LogDebug($"AdjustAdvScene:{chara.transform.position}:{chara.transform.eulerAngles}:{gazeVec}:{gazeVec * talkDistance}");
            var position = VR.Camera.Origin.position;
            position.y = chara.transform.position.y;
            if (PlacePlayer(position, chara.transform.rotation * Quaternion.Euler(0f, 180f, 0f)))
                VRMover.Instance.Impersonate(Game.Instance.actScene.Player.chaCtrl);
            //PlacePlayer(chara.transform.position + (chara.transform.forward * talkDistance), chara.transform.rotation * Quaternion.Euler(0f, 180f, 0f));
            AddTalkColliders(chara);
            AddHColliders(chara);
            HitReactionInitialize(chara);

        }
        //public static void StartTalkScene(TalkScene scene)
        //{
        //    VRPlugin.Logger.LogDebug($"Interpreter:TalkScene:Start:TalkScene");
        //    TalkScene = scene;
        //}
        private void HitReactionInitialize(ChaControl chara)
        {
            if (_hitReaction == null)
            {
                // ADV scene is turned off quite often, so we can't utilized native component.
                _hitReaction = (HitReaction)Util.CopyComponent(advScene.GetComponent<HitReaction>(), Game.Instance.gameObject);
            }
            _hitReaction.ik = chara.objAnim.GetComponent<FullBodyBipedIK>();
            ColliderTracker.Initialize(chara, hScene: false);
            ModelHandler.AddHandlerComponent<TalkSceneHandler>();
        }
        private void SynchronizeClothes(ChaControl chara)
        {
            var npc = Game.Instance.actScene.npcList
                .Where(n => n.chaCtrl != null 
                && n.chaCtrl.fileParam.personality == chara.fileParam.personality
                && n.chaCtrl.fileParam.fullname.Equals(chara.fileParam.fullname))
                .Select(n => n.chaCtrl)
                .FirstOrDefault();
            if (npc == null) return;
            var cloneState = chara.fileStatus.clothesState;
            var originalState = npc.fileStatus.clothesState;
            for (var i = 0; i < cloneState.Length; i++)
            {
                // Apparently there are some hooks to show/hide accessories depending on the state on 'ClothState' methods.
                //npc.SetClothesState(i, cloneState[i], next: false);
                originalState[i] = cloneState[i];
            }
        }
        /// <param name="headset">Overrides index</param>
        private TalkSceneHandler GetHandler(int index, bool headset = false)
        {
            return (TalkSceneHandler)ModelHandler.GetActiveHandler(headset ? 0 : index + 1);
        }
        public static void HitReactionPlay(AibuColliderKind aibuKind, ChaControl chara)
        {
            VRPlugin.Logger.LogDebug($"TalkScene:Reaction:{aibuKind}:{chara}");
            var ik = chara.objAnim.GetComponent<FullBodyBipedIK>();
            if (_hitReaction.ik != ik)
            {
                _hitReaction.ik = ik;
            }
            var key = aibuKind - AibuColliderKind.reac_head;
            var index = Random.Range(0, dicNowReactions[key].lstParam.Count);
            var reactionParam = dicNowReactions[key].lstParam[index];
            var array = new Vector3[reactionParam.lstMinMax.Count];
            for (int i = 0; i < reactionParam.lstMinMax.Count; i++)
            {
                array[i] = new Vector3(Random.Range(reactionParam.lstMinMax[i].min.x, reactionParam.lstMinMax[i].max.x), 
                    Random.Range(reactionParam.lstMinMax[i].min.y, reactionParam.lstMinMax[i].max.y), 
                    Random.Range(reactionParam.lstMinMax[i].min.z, reactionParam.lstMinMax[i].max.z));
                array[i] = chara.transform.TransformDirection(array[i].normalized);
            }
            _hitReaction.weight = dicNowReactions[key].weight;
            _hitReaction.HitsEffector(reactionParam.id, array);
            lstIKEffectLateUpdate.AddRange(dicNowReactions[key].lstReleaseEffector);
            if (lstIKEffectLateUpdate.Count > 0)
            {
                _lateHitReaction = true;
            }
            Features.LoadVoice.PlayVoice(Random.value < 0.4f ? Features.LoadVoice.VoiceType.Laugh : Features.LoadVoice.VoiceType.Short, chara);
        }

        private void SetWait(int index, float duration = 1f)
        {
            _waitForAction[index] = true;
            _waitTime[index] = duration;
            _waitTimestamp[index] = Time.time + duration;
        }
        public override bool OnButtonDown(EVRButtonId buttonId, TrackpadDirection direction, int index)
        {
            VRPlugin.Logger.LogDebug($"Interpreter:ButtonDown[{buttonId}]:Index[{index}]");
            switch (buttonId)
            {
                case EVRButtonId.k_EButton_SteamVR_Trigger:
                    if (_hitReaction != null)
                    {
                        GetHandler(index).DoReaction(triggerPress: true);
                    }
                    _modifierList[index, 0]++;
                    break;
                case EVRButtonId.k_EButton_Grip:
                    _modifierList[index, 1]++;
                    //if (_modifierList[index, 0] > 0) return true;
                    break;
                case EVRButtonId.k_EButton_SteamVR_Touchpad:
                    if (_modifierList[index, 0] > 0)
                    {
                        KoikatuInterpreter.Instance.ChangeModelItem(index + 1, increase: true);
                    }
                    break;
            }
            return false;
        }
        public override bool OnButtonUp(EVRButtonId buttonId, TrackpadDirection direction, int index)
        {
            switch (buttonId)
            {
                case EVRButtonId.k_EButton_SteamVR_Trigger:
                    _modifierList[index, 0]--;
                    break;
                case EVRButtonId.k_EButton_Grip:
                    _modifierList[index, 1]--;
                    break;
            }
            return false;
        }
        public override bool OnDirectionDown(TrackpadDirection direction, int index)
        {
            VRPlugin.Logger.LogDebug($"Interpreter:DirDown[{direction}]:Index[{index}]");
            var adv = IsADV;
            _lastDirection[index] = direction;
            switch (direction)
            {
                case TrackpadDirection.Up:
                case TrackpadDirection.Down:
                    if (_hitReaction != null && GetHandler(index).DoUndress(direction == TrackpadDirection.Down, out var chara))
                    {
                        SynchronizeClothes(chara);
                    }
                    else
                    {
                        if (!adv || IsChoice)
                        {
                            ScrollButtons(direction == TrackpadDirection.Down, adv);
                        }
                        else
                        {

                        }
                    }
                    break;
                case TrackpadDirection.Left:
                case TrackpadDirection.Right:
                    //if (GetHandler(index).DoReaction(triggerPress: false))
                    //{

                    //}
                    if (_modifierList[index, 0] > 0)
                    {
                        KoikatuInterpreter.Instance.ChangeModelLayer(index + 1, direction == TrackpadDirection.Right);
                    }
                    else
                    {
                        SetWait(index);
                    }
                    break;
            }
            return false;
        }
        public override bool OnDirectionUp(TrackpadDirection direction, int index)
        {
            VRPlugin.Logger.LogDebug($"Interpreter:DirUp[{direction}]:Index[{index}]");
            _waitForAction[index] = false;
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
            return false;
        }
        private void PickAction(Timing timing, int index)
        {
            var adv = IsADV;
            _waitForAction[index] = false;
            switch (_lastDirection[index])
            {
                case TrackpadDirection.Up:
                case TrackpadDirection.Down:
                    break;
                case TrackpadDirection.Left:
                case TrackpadDirection.Right:
                    if (adv)
                    {
                        if (!IsChoice)
                        {
                            if (timing == Timing.Full)
                                SetAutoADV();
                            else
                                VR.Input.Mouse.VerticalScroll(-1);
                        }
                        else 
                            EnterState(adv);
                    }
                    else
                    {
                        if (timing == Timing.Full && ClickLastButton())
                            return;

                        if (_lastDirection[index] == TrackpadDirection.Left) 
                            EnterState(adv);
                        else
                            LeaveState(adv);
                    }
                    break;
            }
        }
        private void SetAutoADV()
        {
            advScene.Scenario.isAuto = !advScene.Scenario.isAuto;
        }
        /// <summary>
        /// We wait for the TalkScene to load up to a certain point and grab/add what we want, adjust charas/camera.
        /// </summary>
        private void AdjustTalkScene()
        {
            VRPlugin.Logger.LogDebug($"TalkScene:AdjustTalk");
            _talkSceneStart = false;
            talkScene.canvasBack.enabled = false;

            var head = VR.Camera.Head;
            var origin = VR.Camera.Origin;
            var heroine = talkScene.targetHeroine.transform;
            var headsetPos = head.position;
            var chara = talkScene.targetHeroine.chaCtrl;

            VRPlugin.Logger.LogDebug($"TalkScene:AdjustTalk:{chara.transform.rotation.eulerAngles}:{Quaternion.LookRotation(origin.position - chara.transform.position).eulerAngles}");
            headsetPos.y = heroine.position.y;
            talkDistance = 0.4f + (talkScene.targetHeroine.isGirlfriend ? 0f : 0.1f) + (0.1f - talkScene.targetHeroine.intimacy * 0.001f);

            var offset = _sittingPose || afterH ? 0.3f : 0f;
            afterH = false;

            heroine.rotation = Quaternion.LookRotation(headsetPos - heroine.position);
            var gazeVec = heroine.transform.forward;
            heroine.position += gazeVec * offset;

            headsetPos = heroine.position + (gazeVec * talkDistance);

            var reverseRot = heroine.rotation * Quaternion.Euler(0f, 180f, 0f);

            // An option to keep the head behind vr camera, allowing it to remain visible
            // so we don't see the shadow of a headless body.

            PlacePlayer(headsetPos + (KoikatuInterpreter.settings.ForceShowMaleHeadInAdv ? gazeVec * 0.15f : Vector3.zero), reverseRot);

            var actScene = Game.Instance.actScene;
            var name = talkScene.targetHeroine.Name;
            foreach (var npc in actScene.npcList) 
            {
                if (npc.heroine.Name != name)
                {
                    // TODO Don't add/stop walking/running animation, and check why async cloth load hates us for this.
                    // It hates us always nowadays.
                    // Running: Sport5, Locomotion 0
                    npc.SetActive(npc.mapNo == actScene.Map.no);
                    npc.Pause(false);
                    npc.charaData.SetRoot(npc.gameObject);
                    VRPlugin.Logger.LogDebug($"TalkScene:ExtraNPC:{npc.name}:{npc.motion.state}");
                }
            }
            headsetPos.y = head.position.y;
            origin.rotation = reverseRot;
            origin.position += headsetPos - head.position;
            AddHColliders(chara);
            HitReactionInitialize(chara);
            RepositionDirLight(chara);
        }
        private bool PlacePlayer(Vector3 position, Quaternion rotation)
        {
            if (Game.Instance.Player.chaCtrl == null)
            {
                VRPlugin.Logger.LogDebug($"No player to place");
                return false;
            }
            //if (talkScene == null)
            //{
            //    position.y = advScene.Scenario.currentChara.chaCtrl.transform.position.y;
            //}
            var player = Game.Instance.actScene.Player;
            if (player.chaCtrl.objTop.activeSelf)
            {
                VRPlugin.Logger.LogDebug($"Player is already active");
                return false;
            }

            player.SetActive(true);
            player.rotation = rotation; 
            //if (KoikatuInterpreter.settings.ForceShowMaleHeadInAdv)
            //{
            //    VRMale.ForceShowHead = true;
            //    position += player.transform.forward * -0.15f;
            //}
            player.position = position;
            VRPlugin.Logger.LogDebug($"Place player at:{player.position}:{player.eulerAngles}:{talkDistance}");
            return true;
        }
        //private void AdjustPosition()
        //{
        //    if (TalkScene == null) return;
        //    Height = VR.Camera.Head.position.y - Game.Instance.Player.transform.position.y;
        //    VRPlugin.Logger.LogDebug($"Interpreter:TalkScene:Height:{Height}");
        //    // The default camera location is a bit too far for a friendly
        //    // conversation.
        //    var heroine = TalkScene.targetHeroine;

        //    TalkDistance = 0.4f + (heroine.isGirlfriend ? 0f : 0.1f) + (0.15f - heroine.intimacy * 0.0015f); //  + Random.value * 0.25f;
        //    //TalkDistance = 0.35f + (heroine.isGirlfriend ? 0f : 0.1f) + (0.15f - (int)heroine.HExperience * 0.05f); //  + Random.value * 0.25f;
        //    var position = heroine.chaCtrl.objHeadBone.transform.TransformPoint(new Vector3(0f, 0f, TalkDistance));
        //    //var relativeHeight = heroine.transform.TransformPoint(new Vector3(0f, Height, 0f));
        //    position.y = Height;
        //    var rotation = heroine.chaCtrl.objHeadBone.transform.rotation * Quaternion.Euler(0, 180f, 0);
        //    VRMover.Instance.MoveTo(position, rotation, false);
        //    //VRMover.Instance.MoveTo(
        //    //    heroine.transform.TransformPoint(new Vector3(0, Height, TalkDistance)),
        //    //    heroine.transform.rotation * Quaternion.Euler(0, 180f, 0),
        //    //    false);
        //}
        private bool ClickLastButton()
        {
            if (_lastSelectedButton != null && _lastSelectedButton.enabled)
            {
                _lastSelectedButton.onClick.Invoke();
                return true;
            }
            return false;
        }
        private void LeaveState(bool adv)
        {
            var state = GetState();
            var buttons = GetRelevantButtons(state, adv);
            var button = GetSelectedButton(buttons, adv);
            if (adv)
            {
                button.onClick.Invoke();
            }
            else if (state != State.None)
            {
                buttons = GetRelevantButtons(State.None, adv);
                buttons[(int)state].onClick.Invoke();
            }
        }
        private Button GetSelectedButton(Button[] buttons, bool adv)
        {
            foreach (var button in buttons)
            {
                // Adv buttons are huge so they often intersect with mouse cursor and catch focus unintentionally.
                if (button.name.EndsWith("+", StringComparison.Ordinal) 
                    || (adv && button.currentSelectionState == Selectable.SelectionState.Highlighted))
                {
                    button.name = button.name.TrimEnd('+');
                    button.DoStateTransition(Selectable.SelectionState.Normal, false);
                    return button;
                }
            }
            return null;
        }
        private Button[] GetRelevantButtons(State state, bool adv)
        {
            return adv ? GetADVChoices() : state == State.None ? GetMainButtons() : GetCurrentContents(state);
        }
        private void ScrollButtons(bool increase, bool adv)
        {
            var buttons = GetRelevantButtons(GetState(), adv);
            var length = buttons.Length;
            if (length == 0)
            {
                return;
            }
            var selectedBtn = GetSelectedButton(buttons, adv);
            var index = increase ? 1 : -1;
            if (selectedBtn != null)
            {
                index += Array.IndexOf(buttons, selectedBtn);
            }
            else
            {
                index = 0;
            }
            if (index == length)
            {
                index = 0;
            }
            else if (index < 0)
            {
                index = length - 1;
            }
            MarkButton(buttons[index], adv);
        }
        private void MarkButton(Button button, bool adv)
        {
            button.DoStateTransition(adv ? Selectable.SelectionState.Highlighted : Selectable.SelectionState.Pressed, false);
            button.name += "+";
        }
        private Button[] GetMainButtons()
        {
            return new Button[] { talkScene.buttonTalk, talkScene.buttonListen, talkScene.buttonEvent };
        }
            
        private Button[] GetADVChoices()
        {
            return Game.Instance.actScene.advScene.scenario.choices.GetComponentsInChildren<Button>()
                .Where(b => b.isActiveAndEnabled)
                .ToArray();
        }
        public void ShuffleTemper(SaveData.Heroine heroine)
        {
            var temper = heroine.m_TalkTemper;
            var bias = 1f - Mathf.Clamp01(0.3f - heroine.favor * 0.001f - heroine.intimacy * 0.001f - (heroine.isGirlfriend ? 0.1f : 0f));
            var part = bias * 0.5f;
            for (int i = 0; i < temper.Length; i++)
            {
                temper[i] = GetBiasedByte(bias, part);
            }
        }
        private byte GetBiasedByte(float bias, float part)
        {
            var value = Random.value;
            if (value > bias) return 2;
            if (value < part) return 1;
            return 0;
        }
        private void EnterState(bool adv)
        {
            var state = GetState();
            var buttons = GetRelevantButtons(state, adv);
            var button = GetSelectedButton(buttons, adv);

            if (button == null)
            {
                //VRPlugin.Logger.LogDebug($"EnterState:State - {state}:NoButton");
                if (!adv)
                {
                    if (state == State.None)
                    {
                        ClickLastCategory();
                        return;
                    }
                    else if (_lastSelectedButton != null)
                    {
                        var lastSelectedButtonIndex = Array.IndexOf(buttons, _lastSelectedButton);
                        if (lastSelectedButtonIndex > -1)
                        {
                            MarkButton(buttons[lastSelectedButtonIndex], adv);
                            return;
                        }
                    }
                }
                MarkButton(buttons[Random.Range(0, buttons.Length)], adv);
                return;
            }
            //VRPlugin.Logger.LogDebug($"EnterState:State - {state}:Button - {button.name}");
            
            if (!adv)
            {
                if (state == State.None)
                    _lastSelectedCategory = button;
                else
                    _lastSelectedButton = button;

                if (state == State.Talk && Random.value < 0.5f) ShuffleTemper(talkScene.targetHeroine); 
            }
            button.onClick.Invoke();
        }
        private void ClickLastCategory()
        {
            if (_lastSelectedCategory == null)
            {
                _lastSelectedCategory = talkScene.buttonTalk;
            }
            _lastSelectedCategory.onClick.Invoke();
        }
        private Button[] GetCurrentContents(State state)
        {
            return state == State.Talk ? 
                talkScene.buttonTalkContents 
                : 
                talkScene.buttonEventContents
                .Where(b => b.isActiveAndEnabled)
                .ToArray();
        }
        private State GetState()
        {
            if (talkScene != null && talkScene.objTalkContentsRoot.activeSelf)
            {
                return State.Talk;
            }
            else if (talkScene != null && talkScene.objEventContentsRoot.activeSelf)
            {               
                return State.Event; 
            }
            else
            { 
                return State.None;
            }
        }
        
    }
}
