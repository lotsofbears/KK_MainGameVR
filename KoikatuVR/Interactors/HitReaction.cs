using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static KK_VR.Interactors.GraspController;
using UnityEngine;

namespace KK_VR.Interactors
{
    internal class HitReaction
    {
        internal void React()
        {

        }


        internal class BodyMovement
        {
            internal BodyPart bodyPart;
            internal Transform attachPoint;
            internal int lagFloor;
            internal int lagCeiling;
            internal int lagAmount;
        }

        internal class OffsetPlayEx
        {
            internal BodyPart bodyPart;
            internal Vector3 offsetPos;
            internal Quaternion offsetRot;
            internal float timer;
            internal float waitTimer;
        }

        internal class OffsetPlay
        {
            internal OffsetPlay(BodyPart _bodyPart, Vector3 _offsetVec, Quaternion _offsetRot, bool _constrain)
            {
                bodyPart = _bodyPart;
                offsetPos = _offsetVec;
                offsetRot = _offsetRot;
                constrain = _constrain;
                coef = 0.35f / _offsetVec.magnitude;
                _bodyPart.state = State.Transition;
            }
            internal BodyPart bodyPart;
            internal Vector3 offsetPos;
            internal Quaternion offsetRot;
            internal float coef;
            internal float current;
            internal bool constrain;
        }
    }
}
