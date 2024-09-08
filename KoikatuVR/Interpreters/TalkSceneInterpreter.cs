using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRGIN.Core;
using VRGIN.Controls;
using UnityEngine;
using HarmonyLib;
using System.Collections;
using KK_VR.Camera;
using System.Runtime.InteropServices;
using Manager;
using KK_VR.Caress;
using ADV.Commands.Camera;
using System.Diagnostics;
using KoikatuVR.Camera;
using UnityEngine.UI;
using ADV;
using Random = UnityEngine.Random;
using RootMotion.FinalIK;
using KK_VR.Fixes;
using ADV.Commands.H;
using Valve.VR;
using ADV.Commands.Base;
using static HandCtrl;
using static KK_VR.Interpreters.Extras.TalkSceneExtras;
using static VRGIN.Controls.Controller;
using KK_VR.Controls;
using KK_VR.Interpreters.Extras;
using KK_VR.Handlers;

namespace KK_VR.Interpreters
{
    // We want t
    class TalkSceneInterpreter : SceneInterpreter
    {
        public static float talkDistance = 0.55f;
        public static float height;
        public static TalkScene talkScene;
        public static ADVScene advScene;

        private static HitReaction _hitReaction;
        private readonly static List<int> lstIKEffectLateUpdate = new List<int>();
        private static bool _lateHitReaction;


        private bool _talkSceneStart;
        private bool _hitReactionInit;
        private bool _advSceneStart;
        private bool _sittingPose;
        private bool _talkScenePreSet;
        private readonly bool[] _waitForAction = new bool[2];
        private readonly float[] _waitTimestamp = new float[2];
        private readonly float[] _waitTime = new float[2];
        private readonly TrackpadDirection[] _lastDirection = new TrackpadDirection[2];
        private Button _previousButton;
        private TalkSceneHandler[] _handlers;

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
            DestroyControllerComponent<TalkSceneHandler>();
        }
        public override void OnUpdate()
        {
            // We don't need the background image because we directly see
            // background objects.
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
            if (_advSceneStart && !Manager.Scene.Instance.IsFadeNow && advScene.Scenario.currentChara != null)
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
            _advSceneStart = false;
            _talkSceneStart = true;
        }
        public void AdjustAdvScene()
        {
            _advSceneStart = false;
            AddTalkColliders(advScene.Scenario.currentChara.chaCtrl);
            HitReactionInitialize(advScene.Scenario.currentChara.chaCtrl);
        }

        //public static void StartTalkScene(TalkScene scene)
        //{
        //    VRPlugin.Logger.LogDebug($"Interpreter:TalkScene:Start:TalkScene");
        //    TalkScene = scene;
        //}
        public void HitReactionInitialize(ChaControl chara)
        {
            if (_hitReaction == null)
            {
                // ADV scene is turned off quite often, so we can't utilized native component.

                _hitReaction = (HitReaction)Util.CopyComponent(advScene.GetComponent<HitReaction>(), Game.Instance.gameObject);
            }
            _hitReaction.ik = chara.objAnim.GetComponent<FullBodyBipedIK>();
            ColliderTracker.Initialize(chara, hScene: false);
            _handlers = AddControllerComponent<TalkSceneHandler>();
            _hitReactionInit = true;
        }

        public static void HitReactionPlay(AibuColliderKind aibuKind, ChaControl chara)
        {
            VRPlugin.Logger.LogDebug($"Interpreter:Reaction:{aibuKind}:{chara}");
            var ik = chara.objAnim.GetComponent<FullBodyBipedIK>();
            if (_hitReaction.ik != ik)
            {
                _hitReaction.ik = ik;
            }

            var key = aibuKind - AibuColliderKind.reac_head;
            var gameObject = chara.gameObject;
            var index = Random.Range(0, dicNowReactions[key].lstParam.Count);
            var reactionParam = dicNowReactions[key].lstParam[index];
            var array = new Vector3[reactionParam.lstMinMax.Count];
            for (int i = 0; i < reactionParam.lstMinMax.Count; i++)
            {
                array[i] = new Vector3(Random.Range(reactionParam.lstMinMax[i].min.x, reactionParam.lstMinMax[i].max.x), 
                    Random.Range(reactionParam.lstMinMax[i].min.y, reactionParam.lstMinMax[i].max.y), 
                    Random.Range(reactionParam.lstMinMax[i].min.z, reactionParam.lstMinMax[i].max.z));
                array[i] = gameObject.transform.TransformDirection(array[i].normalized);
            }
            _hitReaction.weight = dicNowReactions[key].weight;
            _hitReaction.HitsEffector(reactionParam.id, array);
            lstIKEffectLateUpdate.AddRange(dicNowReactions[key].lstReleaseEffector);
            if (lstIKEffectLateUpdate.Count > 0)
            {
                _lateHitReaction = true;
            }
            if (chara.asVoice == null)
            {
                Features.LoadVoice.Play(Random.value < 0.5f ? Features.LoadVoice.VoiceType.Laugh : Features.LoadVoice.VoiceType.Short, chara);
            }
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
                    if (_hitReactionInit)
                    {
                        _handlers[index].DoReaction(triggerPress: true);
                    }
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
                    if (!_hitReactionInit || !_handlers[index].DoUndress(direction == TrackpadDirection.Down))
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
                    if (!_hitReactionInit || !_handlers[index].DoReaction(triggerPress: false))
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
                    if (adv && !IsChoice)
                    {
                        VR.Input.Mouse.VerticalScroll(-1);
                    }
                    else
                    {
                        if (timing == Timing.Full)
                        {

                        }
                        else
                        {
                            EnterState(adv);
                        }
                    }
                    break;
                case TrackpadDirection.Right:
                    if (adv && !IsChoice)
                    {
                        if (timing == Timing.Full)
                        {
                            SetAutoADV();
                        }
                        else
                        {
                            VR.Input.Mouse.VerticalScroll(-1);
                        }
                    }
                    else
                    {
                        if (timing == Timing.Full && !adv)
                        {
                            ClickLastButton();
                        }
                        else
                        {
                            LeaveState(adv);
                        }
                    }
                    break;
            }
        }
        private void SnapshotTalkScene()
        {
            _sittingPose = (talkScene.targetHeroine.chaCtrl.objHead.transform.position - talkScene.targetHeroine.transform.position).y < 1f;
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
            _talkSceneStart = false;
            talkScene.canvasBack.enabled = false;
            var head = VR.Camera.Head;
            var origin = VR.Camera.Origin;
            var heroine = talkScene.targetHeroine.transform;
            var headsetPos = head.position;

            height = headsetPos.y - heroine.position.y;
            headsetPos.y = heroine.position.y;
            talkDistance = 0.4f + (talkScene.targetHeroine.isGirlfriend ? 0f : 0.1f) + (0.1f - talkScene.targetHeroine.intimacy * 0.001f);

            var offset = _sittingPose ? 0.3f : 0f;
            //if (_sittingAnimations.Contains(_talkScene.targetHeroine.charaBase.motion.state))
            ////(_sittingAnimations.Any(anim => _talkScene.targetHeroine.charaBase.motion.state.Equals(anim)))
            //{
            //    offset = 0.25f;
            //}

            var rotation = Quaternion.LookRotation(headsetPos - heroine.position);
            var flippedRotation = rotation * Quaternion.Euler(0f, 180f, 0f);
            var distance = Vector3.Distance(headsetPos, heroine.position);
            var vec = headsetPos - heroine.position;
            heroine.rotation = rotation;
            heroine.position += vec * (offset / distance);

            headsetPos = vec * (talkDistance / distance) + heroine.position;

            var actScene = Game.Instance.actScene;
            var player = actScene.Player;
            var eyes = player.chaCtrl.objHead.transform;
            //var eyes = player.chaCtrl.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz/cf_J_Eye_tz").position;
            //eyes.y = headsetPos.y;

            // Shouldn't this be the other way around? Yet it works only like this. 
            //var deltaBaseEyes = eyes - player.transform.position;

            var angle = Mathf.DeltaAngle(eyes.transform.rotation.eulerAngles.y, player.transform.rotation.eulerAngles.y);

            VRPlugin.Logger.LogDebug($"TalkScene:Adjust:Angle - {angle}:{eyes.transform.rotation.eulerAngles.y}:{player.transform.rotation.eulerAngles.y}");
            player.SetActive(true);
            player.rotation = flippedRotation * Quaternion.Euler(0f, angle, 0f);
            player.position = headsetPos + vec * 0.12f;

            var name = talkScene.targetHeroine.Name;
            foreach (var npc in actScene.npcList) 
            {
                if (npc.heroine.Name != name)
                {
                    // TODO Don't add/stop walking/running animation, and check why async cloth load hates us for this.
                    npc.SetActive(npc.mapNo == actScene.Map.no);
                    npc.Pause(false);
                    npc.charaData.SetRoot(npc.gameObject);
                }
            }
            headsetPos.y = head.position.y;
            origin.rotation = flippedRotation;
            origin.position += headsetPos - head.position;
            HitReactionInitialize(talkScene.targetHeroine.chaCtrl);
            RepositionDirLight(talkScene.targetHeroine.chaCtrl);
            VRPlugin.Logger.LogDebug($"Interpreter:Adjust:Talk:{talkScene.targetHeroine.charaBase.motion.state}:{talkDistance}:");
        }
        //private static readonly List<string> _sittingAnimations = new List<string>()
        //{
        //    "Reading",
        //    "Appearance6",
        //    "Game",
        //    "ChangeMind6",
        //    "ChangeMind11",
        //    "Phone3",
        //    "Phone4",
        //    "Dinner"
        //};

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
        private void ClickLastButton()
        {
            if (_previousButton != null && _previousButton.enabled)
            {
                _previousButton.onClick.Invoke();
            }
        }
        private void LeaveState(bool adv)
        {
            var state = GetState();
            var buttons = GetRelevantButtons(state, adv);
            var button = GetSelectedButton(buttons);
            if (adv)
            {
                button.onClick.Invoke();
                _previousButton = button;
            }
            else if (state != State.None)
            {
                buttons = GetRelevantButtons(State.None, adv);
                buttons[(int)state].onClick.Invoke();
            }
        }
        private Button GetSelectedButton(Button[] buttons)
        {
            foreach (var button in buttons)
            {
                if (button.name.EndsWith("+", StringComparison.Ordinal))
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
            var selectedBtn = GetSelectedButton(buttons);
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
            buttons[index].DoStateTransition(adv ? Selectable.SelectionState.Highlighted : Selectable.SelectionState.Pressed, false);
            buttons[index].name += "+";
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
        private void EnterState(bool adv)
        {
            var state = GetState();
            var buttons = GetRelevantButtons(state, adv);
            var button = GetSelectedButton(buttons);

            if (button == null)
            {
                VRPlugin.Logger.LogDebug($"EnterState:State - {state}:NoButton");
                return;
            }
            else
            {
                VRPlugin.Logger.LogDebug($"EnterState:State - {state}:Button - {button.name}");
            }
            _previousButton = button;
            button.onClick.Invoke();
        }
        private Button[] GetCurrentContents(State state)
        {
            return state == State.Talk ? talkScene.buttonTalkContents : talkScene.buttonEventContents;
        }
        private State GetState()
        {
            if (talkScene.objTalkContentsRoot.activeSelf)
            {
                return State.Talk;
            }
            else if (talkScene.objEventContentsRoot.activeSelf)
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
