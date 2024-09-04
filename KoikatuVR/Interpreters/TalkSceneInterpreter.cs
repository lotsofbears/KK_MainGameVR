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

namespace KK_VR.Interpreters
{
    class TalkSceneInterpreter : SceneInterpreter
    {
        Canvas _canvasBack;
        public static float TalkDistance = 0.55f;
        public static float Height;
        public static TalkSceneInterpreter Instance;
        public static TalkScene TalkScene
        {
            get
            {
                if (_talkScene == null)
                {
                    var scene = AdvScene.nowScene;
                    if (scene != null && scene.GetType() == typeof(TalkScene))
                    {
                        _talkScene = (TalkScene)scene;
                    }
                    else
                    {
                        _talkScene = UnityEngine.Object.FindObjectOfType<TalkScene>();
                    }
                }
                return _talkScene;
            }
        }
        public static ADVScene AdvScene;
        private static TalkScene _talkScene;
        private static HitReaction _hitReaction;
        private static List<int> lstIKEffectLateUpdate = new List<int>();

        private bool _adjustmentRequired;
        private bool _sittingPosition;
        private bool _waitForAction;
        private float _waitTimestamp;
        private float _waitTime;
        private TrackpadDirection _lastDirection;
        private Button _lastChosenButton;

        private bool IsADV => AdvScene.isActiveAndEnabled;
        private bool IsChoice => AdvScene.scenario.isChoice;
        enum State
        {
            Talk,
            None,
            Event
        }
        public override void OnDisable()
        {
            DestroyControllerComponent<Controls.TalkSceneHandler>();
            if (_canvasBack != null)
            {
                _canvasBack.enabled = true;
            }
        }
        public override void OnStart()
        {
            VRPlugin.Logger.LogDebug($"TalkScene:OnStart");
            Instance = this;
            AdvScene = Game.Instance.actScene.advScene;
            if (TalkScene == null)
                return;

            TalkScene.otherInitialize += () =>
            {
                VRPlugin.Logger.LogDebug($"TalkScene:TriggerAction:otherInitialize");
                _adjustmentRequired = true;
                _sittingPosition = (TalkScene.targetHeroine.chaCtrl.objHead.transform.position - TalkScene.targetHeroine.transform.position).y < 1f;
            };
            _canvasBack = TalkScene.canvasBack;
        }
        public override void OnUpdate()
        {
            // We don't need the background image because we directly see
            // background objects.
            if (_canvasBack != null)
            {
                _canvasBack.enabled = false;
            }
            if (_adjustmentRequired && TalkScene.cameraMap.enabled)
            {
                Adjust();
                // Don't want to init it too early, target heroine might be different/absent.
                HitReactionInitialize();
            }
            if (_waitForAction && _waitTimestamp < Time.time)
            {
                PickAction(Timing.Full);
            }
        }
        public override void OnLateUpdate()
        {
            if (lstIKEffectLateUpdate.Count != 0)
            {
                _hitReaction.ReleaseEffector();
                _hitReaction.SetEffector(lstIKEffectLateUpdate);
                lstIKEffectLateUpdate.Clear();
            }
        }
        public static void HitReactionInitialize()
        {
            if (_hitReaction == null)
            {
                // ADV scene is turned off quite often, so we can't utilized native component.

                Util.CopyComponent(AdvScene.GetComponent<HitReaction>(), TalkScene.gameObject);
                _hitReaction = TalkScene.GetComponent<HitReaction>();
            }
            _hitReaction.ik = TalkScene.targetHeroine.chaCtrl.objAnim.GetComponent<FullBodyBipedIK>();
        }

        public static void HitReactionPlay(AibuColliderKind aibuKind, ChaControl chara)
        {
            VRPlugin.Logger.LogDebug($"TalkScene:Interpreter:Reaction:{aibuKind}");
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

            if (chara.asVoice == null)
            {
                // Find actual experience ?
                Features.LoadVoice.Play(Random.value < 0.5f ? Features.LoadVoice.VoiceType.Laugh : Features.LoadVoice.VoiceType.Short, chara, SaveData.Heroine.HExperienceKind.慣れ);
            }
        }
        private void SetWait(float duration = 1f)
        {
            _waitForAction = true;
            _waitTime = duration;
            _waitTimestamp = Time.time + duration;
        }
        public override bool OnButtonDown(TrackpadDirection direction)
        {
            var adv = IsADV;
            _lastDirection = direction;
            switch (direction)
            {
                case TrackpadDirection.Up:
                case TrackpadDirection.Down:
                    if (!adv || IsChoice)
                    {
                        ScrollButtons(direction == TrackpadDirection.Down, adv);
                    }
                    else
                    {

                    }
                    break;
                case TrackpadDirection.Left:
                case TrackpadDirection.Right:
                    SetWait();
                    break;
            }
            return false;
        }
        public override bool OnButtonUp(TrackpadDirection direction)
        {
            _waitForAction = false;
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
        private void PickAction(Timing timing)
        {
            var adv = IsADV;
            _waitForAction = false;
            switch (_lastDirection)
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
        private void SetAutoADV()
        {
            AdvScene.Scenario.isAuto = !AdvScene.Scenario.isAuto;
        }
        /// <summary>
        /// We wait for TalkScene to load up to the point where chara is ready, and then adjust everything.
        /// </summary>
        private void Adjust()
        {
            _adjustmentRequired = false;
            if (TalkScene == null) return;

            AddControllerComponent<Controls.TalkSceneHandler>();


            var head = VR.Camera.Head;
            var origin = VR.Camera.Origin;
            var heroine = TalkScene.targetHeroine.transform;
            var headsetPos = head.position;

            Height = headsetPos.y - heroine.position.y;
            headsetPos.y = heroine.position.y;
            TalkDistance = 0.4f + (TalkScene.targetHeroine.isGirlfriend ? 0f : 0.1f) + (0.1f - TalkScene.targetHeroine.intimacy * 0.001f);

            var offset = _sittingPosition ? 0.3f : 0f;
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

            headsetPos = vec * (TalkDistance / distance) + heroine.position;

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

            var name = TalkScene.targetHeroine.Name;
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

            // Enable all the colliders.
            var colliders = TalkScene.targetHeroine.chaCtrl.GetComponentsInChildren<Collider>(includeInactive: true);
            foreach (var collider in colliders)
            {
                if (!collider.enabled)
                {
                    collider.enabled = true;
                    collider.gameObject.layer = 10;
                    collider.gameObject.SetActive(true);
                }
            }

            headsetPos.y = head.position.y;
            origin.rotation = flippedRotation;
            origin.position += headsetPos - head.position;
            VRPlugin.Logger.LogDebug($"TalkScene:Adjust:{TalkScene.targetHeroine.charaBase.motion.state}:{TalkDistance}:");
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
            if (_lastChosenButton != null && _lastChosenButton.enabled)
            {
                _lastChosenButton.onClick.Invoke();
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
                _lastChosenButton = button;
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
                VRPlugin.Logger.LogDebug($"ScrollMainButtons:NotFound:Pressed");
            }
            if (index == length)
            {
                index = 0;
            }
            else if (index < 0)
            {
                index = length - 1;
            }
            VRPlugin.Logger.LogDebug($"ScrollButtons:Index - {index}");
            buttons[index].DoStateTransition(adv ? Selectable.SelectionState.Highlighted : Selectable.SelectionState.Pressed, false);
            buttons[index].name += "+";
        }
        private Button[] GetMainButtons()
        {
            return new Button[] { TalkScene.buttonTalk, TalkScene.buttonListen, TalkScene.buttonEvent };
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
            _lastChosenButton = button;
            button.onClick.Invoke();
        }
        private Button[] GetCurrentContents(State state)
        {
            return state == State.Talk ? TalkScene.buttonTalkContents : TalkScene.buttonEventContents;
        }
        private State GetState()
        {
            if (TalkScene.objTalkContentsRoot.activeSelf)
            {
                return State.Talk;
            }
            else if (TalkScene.objEventContentsRoot.activeSelf)
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
