using KK_VR.Trackers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static KK_VR.Interactors.GraspController;

namespace KK_VR.Interactors
{
    internal class GraspHelper : MonoBehaviour
    {
        private bool _transition;
        private bool _animChange;
        private static GraspHelper _instance;
        private readonly List<OffsetPlay> _transitionList = new List<OffsetPlay>();
        private readonly Dictionary<ChaControl, string> _animChangeDic = new Dictionary<ChaControl, string>();
        private static Dictionary<ChaControl, List<BodyPart>> _bodyPartsDic;
        private List<BodyPart> _attachedBodyParts;
        internal bool _attach;

        internal void Init(Dictionary<ChaControl, List<BodyPart>> bodyPartsDic)
        {
            _instance = this;
            _bodyPartsDic = bodyPartsDic;
        }
        internal static void SetWorkingState(ChaControl chara)
        {
            // By default only limbs are used, the rest is limited to offset play by hitReaction.
            if (_bodyPartsDic != null && _bodyPartsDic.ContainsKey(chara))
            {
                foreach (var bodyPart in _bodyPartsDic[chara])
                {
                    bodyPart.targetBaseData.bone = bodyPart.effector.bone;
                    bodyPart.effector.target = bodyPart.anchor;
                    if (bodyPart.chain == null) continue;
                    bodyPart.chain.bendConstraint.weight = bodyPart.state == State.Default ? 1f : 0f;
                }
            }
        }

        internal static void SetDefaultState(ChaControl chara, string stateName)
        {
            if (_bodyPartsDic != null && _bodyPartsDic.ContainsKey(chara))
            {
                if (stateName != null && chara.objTop.activeSelf && chara.visibleAll)
                {
                    _instance.AnimChangeAdd(chara, stateName);
                }
                foreach (var bodyPart in _bodyPartsDic[chara])
                {
                    bodyPart.effector.target = bodyPart.origTarget;
                    if (bodyPart.chain != null)
                    {
                        bodyPart.chain.bendConstraint.weight = 1f;
                    }
                }
            }
            
        }
        private void AnimChangeAdd(ChaControl chara, string stateName)
        {
            for (var i = 5; i < 7; i++)
            {
                var bodyPart = _bodyPartsDic[chara][i];
                if (bodyPart.state == State.Active)
                {
                    //var parent = GetParent(bodyPart.name);
                    //VRPlugin.Logger.LogDebug($"AnimChange:Add:{bodyPart.name} -> {parent} -> {_bodyPartsDic[chara][(int)parent].origTarget}");
                    if (!_animChangeDic.ContainsKey(chara))
                    {
                        _animChangeDic.Add(chara, stateName);
                        _animChange = true;
                    }
                    bodyPart.anchor.SetParent(_bodyPartsDic[chara][(int)GetParent(bodyPart.name)].anchor, worldPositionStays: true);
                }
            }
        }
        private PartName GetParent(PartName partName)
        {
            return partName switch
            {
                PartName.LeftHand => PartName.LeftShoulder,
                PartName.RightHand => PartName.RightShoulder,
                PartName.LeftFoot => PartName.LeftThigh,
                PartName.RightFoot => PartName.RightThigh,
                _ => PartName.Body
            };
        }
        private void AnimChangeWait()
        {
            foreach (var kv in _animChangeDic)
            {
                VRPlugin.Logger.LogDebug($"AnimChangeWait:{kv.Key}:{kv.Value}");
                if (kv.Key.animBody.GetCurrentAnimatorStateInfo(0).IsName(kv.Value))
                {
                    AnimChangeEnd(kv.Key);
                    return;
                }
            }
        }
        internal void AddAttach(BodyPart bodyPart)
        {
            _attach = true;
            _attachedBodyParts.Add(bodyPart);
        }
        internal void RemoveAttach(BodyPart bodyPart)
        {
            if (_attachedBodyParts.Remove(bodyPart) && _attachedBodyParts.Count == 0)
            {
                _attach = false;
            }
        }
        internal void OnPoseChange()
        {
            _attach = false;
            _attachedBodyParts.Clear();
            StopTransition();
        }
        private void Update()
        {
            if (_transition) DoTransition();
            if (_animChange) AnimChangeWait();
            if (_attach)
            {
                foreach (var bodyPart in _attachedBodyParts)
                {
                    bodyPart.anchor.position = bodyPart.attachTarget.TransformPoint(bodyPart.offset);
                }
            }
        }
        private void AnimChangeEnd(ChaControl chara)
        {
            for (var i = 5; i < 7; i++)
            {
                var bodyPart = _bodyPartsDic[chara][i];
                if (bodyPart.state == State.Active)
                {
                    bodyPart.anchor.SetParent(bodyPart.origTarget, worldPositionStays: true);
                }
            }

            //    foreach (var bodyPart in _bodyPartsDic[chara])
            //{
            //    if (bodyPart.state == State.Active)
            //    {
            //        bodyPart.anchor.SetParent(bodyPart.origTarget, worldPositionStays: true);
            //        //SetEffector(bodyPart.origTarget, bodyPart);
            //    }
            //}
            _animChangeDic.Remove(chara);
            _animChange = _animChangeDic.Count != 0;
        }

        internal void StartTransition(BodyPart bodyPart)
        {
            VRPlugin.Logger.LogDebug($"Transition:Add{bodyPart.name}");
            _transitionList.Add(new OffsetPlay(bodyPart, bodyPart.anchor.localPosition, bodyPart.anchor.localRotation, bodyPart.chain != null));
            _transition = true;
        }
        private void DoTransition()
        {
            for (var i = 0; i < _transitionList.Count; i++)
            {
                var offsetPlay = _transitionList[i];
                offsetPlay.current += Time.deltaTime * offsetPlay.coef;
                if (offsetPlay.constrain)
                    offsetPlay.bodyPart.chain.bendConstraint.weight = offsetPlay.current;
                offsetPlay.bodyPart.anchor.localRotation = Quaternion.Lerp(offsetPlay.offsetRot, Quaternion.identity, offsetPlay.current);
                offsetPlay.bodyPart.anchor.localPosition = Vector3.Lerp(offsetPlay.offsetPos, Vector3.zero, offsetPlay.current);
                if (offsetPlay.current >= 1f)
                {
                    offsetPlay.current = 0f;
                    offsetPlay.bodyPart.state = State.Default;
                    _transitionList.RemoveAt(i);
                    VRPlugin.Logger.LogDebug($"Transition:Finish:{_transitionList.Count}");
                    if (_transitionList.Count == 0)
                    {
                        _transition = false;
                        return;
                    }
                    i--;
                }
            }
        }
        //private void StopTransition()
        //{
        //    VRPlugin.Logger.LogDebug($"Transition:Stop{_transitionList.Count}");
        //    _transition = false;
        //    _transitionList.Clear();
        //}
        internal void StopTransition(BodyPart bodyPart)
        {
            VRPlugin.Logger.LogDebug($"Transition:Stop{_transitionList.Count}");
            _transitionList.Remove(_transitionList
                .Where(o => o.bodyPart == bodyPart)
                .First());
            bodyPart.state = State.Active;
            if (_transitionList.Count == 0)
                _transition = false;
        }
        internal void StopTransition()
        {
            _transition = false;
            _transitionList.Clear();
        }
    }
}
