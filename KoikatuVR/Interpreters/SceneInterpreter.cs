﻿using VRGIN.Core;
using UnityEngine;
using VRGIN.Controls;
using Valve.VR;

namespace KK_VR.Interpreters
{
    abstract class SceneInterpreter
    {
        public abstract void OnStart();
        public abstract void OnDisable();
        public abstract void OnUpdate();
        public virtual void OnLateUpdate()
        {

        }

        protected void AddControllerComponent<T>()
            where T: Component
        {
            VR.Mode.Left.gameObject.AddComponent<T>();
            VR.Mode.Right.gameObject.AddComponent<T>();
        }
        // For touchpad direction without click;
        public virtual bool OnButtonDown(Controller.TrackpadDirection direction)
        {
            return false;
        }
        // For touchpad direction without click;
        public virtual bool OnButtonUp(Controller.TrackpadDirection direction)
        {
            return false;
        }
        // For touchpad direction + click;
        public virtual bool OnButtonDown(Controller.TrackpadDirection direction, EVRButtonId buttonId)
        {
            return false;
        }
        // For touchpad direction + click;
        public virtual bool OnButtonUp(Controller.TrackpadDirection direction, EVRButtonId buttonId)
        {
            return false;
        }
        // For grip/trigger/(a/x).
        public virtual bool OnButtonDown(EVRButtonId buttonId)
        {
            return false;
        }
        // For grip/trigger/(a/x).
        public virtual bool OnButtonUp(EVRButtonId buttonId)
        {
            return false;
        }
        public enum Timing
        {
            Fraction,
            Half,
            Full
        }
        protected void DestroyControllerComponent<T>()
            where T: Component
        {
            var left = VR.Mode.Left.GetComponent<T>();
            if (left != null)
            {
                GameObject.Destroy(left);
            }
            var right = VR.Mode.Right.GetComponent<T>();
            if (right != null)
            {
                GameObject.Destroy(right);
            }
        }
    }
}
