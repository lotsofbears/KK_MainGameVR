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
using KKAPI.MainGame;

namespace KK_VR.Interpreters
{
    class TalkSceneInterpreter : SceneInterpreter
    {
        Canvas _canvasBack;
        public static float TalkDistance = 0.55f;
        public static float Height;
        private bool _adjustmentRequired;
        private bool _sittingPosition;
        private static TalkScene _talkScene;
        //private State _state;
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
            AddControllerComponent<Controls.TalkSceneHandler>();
            _talkScene = GetTalkScene();
            _talkScene.otherInitialize += () =>
            {
                VRPlugin.Logger.LogDebug($"TalkScene:otherInitialize");
                _adjustmentRequired = true;
                _sittingPosition = (_talkScene.targetHeroine.chaCtrl.objHead.transform.position - _talkScene.targetHeroine.transform.position).y < 1f;
            };

            _canvasBack = _talkScene.canvasBack;
        }
        public override void OnUpdate()
        {
            // We don't need the background image because we directly see
            // background objects.
            if (_canvasBack != null)
            {
                _canvasBack.enabled = false;
            }
            if (_adjustmentRequired && Singleton<Communication>.Instance.isInit && _talkScene.targetHeroine.transform.position != Vector3.zero)
            {
                Adjust();
            }
        }

        public override bool OnButtonDown(Controller.TrackpadDirection direction)
        {
            switch (direction)
            {
                case Controller.TrackpadDirection.Up:
                case Controller.TrackpadDirection.Down:
                    ScrollButtons(direction == Controller.TrackpadDirection.Down);
                    break;
                case Controller.TrackpadDirection.Left:
                    EnterState();
                    break;
                case Controller.TrackpadDirection.Right:
                    LeaveState();
                    break;
            }
            return false;
        }

        /// <summary>
        /// We wait for TalkScene to load up to the point where chara is ready, and then adjust everything.
        /// </summary>
        private void Adjust()
        {
            _adjustmentRequired = false;

            if (_talkScene == null) return;
            var head = VR.Camera.Head;
            var origin = VR.Camera.Origin;
            var heroine = _talkScene.targetHeroine.transform;
            var headsetPos = head.position;

            Height = headsetPos.y - heroine.position.y;
            headsetPos.y = heroine.position.y;
            TalkDistance = 0.4f + (_talkScene.targetHeroine.isGirlfriend ? 0f : 0.1f) + (0.1f - _talkScene.targetHeroine.intimacy * 0.001f);

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

            var name = _talkScene.targetHeroine.Name;
            foreach (var npc in actScene.npcList) 
            {
                if (npc.heroine.Name != name)
                {
                    npc.SetActive(npc.mapNo == actScene.Map.no);
                    npc.Pause(false);
                    npc.charaData.SetRoot(npc.gameObject);
                }
            }

            headsetPos.y = head.position.y;
            origin.rotation = flippedRotation;
            origin.position += headsetPos - head.position;
            VRPlugin.Logger.LogDebug($"TalkScene:Adjust:{_talkScene.targetHeroine.charaBase.motion.state}:{TalkDistance}:");
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

        private void AdjustPosition()
        {
            if (_talkScene == null) return;
            Height = VR.Camera.Head.position.y - Game.Instance.Player.transform.position.y;
            VRPlugin.Logger.LogDebug($"Interpreter:TalkScene:Height:{Height}");
            // The default camera location is a bit too far for a friendly
            // conversation.
            var heroine = _talkScene.targetHeroine;

            TalkDistance = 0.4f + (heroine.isGirlfriend ? 0f : 0.1f) + (0.15f - heroine.intimacy * 0.0015f); //  + Random.value * 0.25f;
            //TalkDistance = 0.35f + (heroine.isGirlfriend ? 0f : 0.1f) + (0.15f - (int)heroine.HExperience * 0.05f); //  + Random.value * 0.25f;
            var position = heroine.chaCtrl.objHeadBone.transform.TransformPoint(new Vector3(0f, 0f, TalkDistance));
            //var relativeHeight = heroine.transform.TransformPoint(new Vector3(0f, Height, 0f));
            position.y = Height;
            var rotation = heroine.chaCtrl.objHeadBone.transform.rotation * Quaternion.Euler(0, 180f, 0);
            VRMover.Instance.MoveTo(position, rotation, false);
            //VRMover.Instance.MoveTo(
            //    heroine.transform.TransformPoint(new Vector3(0, Height, TalkDistance)),
            //    heroine.transform.rotation * Quaternion.Euler(0, 180f, 0),
            //    false);
        }


        private TalkScene GetTalkScene()
        {
            if (_talkScene == null)
            {
                var scene = Game.Instance.actScene.advScene.nowScene;
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
        private void LeaveState()
        {
            var state = GetState();
            var buttons = GetRelevantButtons(state);
            GetSelectedButton(buttons);
            if (state != State.None)
            {
                buttons = GetRelevantButtons(State.None);
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
        private Button[] GetRelevantButtons(State state)
        {
            return state == State.None ? GetMainButtons() : GetCurrentContents(state);
        }
        private void ScrollButtons(bool increase)
        {
            var buttons = GetRelevantButtons(GetState());
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
            if (index == buttons.Length)
            {
                index = 0;
            }
            else if (index < 0)
            {
                index = buttons.Length - 1;
            }
            VRPlugin.Logger.LogDebug($"ScrollMainButtons{index}");
            buttons[index].DoStateTransition(Selectable.SelectionState.Pressed, false);
            buttons[index].name += "+";
        }
        private Button[] GetMainButtons()
        {
            return new Button[] { _talkScene.buttonTalk, _talkScene.buttonListen, _talkScene.buttonEvent };
        }
            
        private List<Button> GetADVChoices()
        {
            return Game.Instance.actScene.advScene.scenario.choices.GetComponentsInChildren<Button>()
                .Where(b => b.isActiveAndEnabled)
                .ToList();
        }
        private void EnterState()
        {
            var state = GetState();
            var buttons = GetRelevantButtons(state);
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
            button.onClick.Invoke();
        }
        private Button[] GetCurrentContents(State state)
        {
            return state == State.Talk ? _talkScene.buttonTalkContents : _talkScene.buttonEventContents;
        }
        private State GetState()
        {
            if (_talkScene.objTalkContentsRoot.activeSelf)
            {
                return State.Talk;
            }
            else if (_talkScene.objEventContentsRoot.activeSelf)
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
