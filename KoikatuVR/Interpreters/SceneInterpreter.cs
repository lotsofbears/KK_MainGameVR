using VRGIN.Core;
using UnityEngine;
using VRGIN.Controls;
using Valve.VR;
using KK_VR.Handlers;
using KK_VR.Settings;

namespace KK_VR.Interpreters
{
    abstract class SceneInterpreter
    {
        protected KoikatuSettings _settings = VR.Context.Settings as KoikatuSettings;
        public virtual void OnStart()
        {

        }
        public virtual void OnDisable()
        {

        }
        public virtual void OnUpdate()
        {

        }
        public virtual void OnLateUpdate()
        {

        }
        //protected static T[] AddControllerComponent<T>()
        //    where T: Component
        //{
        //    // Controller indexes are 1(L) and 2(R).
        //    var components = new T[2];
        //    if (VR.Mode.Left.gameObject.GetComponent<T>() == null)
        //    {
        //        components[0] = VR.Mode.Left.gameObject.AddComponent<T>();
        //        components[1] = VR.Mode.Right.gameObject.AddComponent<T>();
        //    }
        //    else
        //    {
        //        components[0] = VR.Mode.Left.gameObject.GetComponent<T>();
        //        components[1] = VR.Mode.Right.gameObject.GetComponent<T>();
        //    }
        //    return components;
        //}
        /// <summary>
        /// For touchpad direction without click.
        /// </summary>
        public virtual bool OnDirectionDown(int index, Controller.TrackpadDirection direction)
        {
            return true;
        }
        /// <summary>
        /// For touchpad direction without click.
        /// </summary>
        public virtual void OnDirectionUp(int index, Controller.TrackpadDirection direction)
        {

        }
        /// <summary>
        /// For actual click.
        /// </summary>
        public virtual bool OnButtonDown(int index, EVRButtonId buttonId, Controller.TrackpadDirection direction)
        {
            return true;
        }
        /// <summary>
        /// For actual click.
        /// </summary>
        public virtual void OnButtonUp(int index, EVRButtonId buttonId, Controller.TrackpadDirection direction)
        {

        }
        public enum Timing
        {
            Fraction,
            Half,
            Full
        }
        public virtual void OnGripMove(int index, bool active)
        {

        }
        //protected static void DestroyControllerComponent<T>()
        //    where T: Component
        //{
        //    var left = VR.Mode.Left.GetComponent<T>();
        //    if (left != null)
        //    {
        //        GameObject.Destroy(left);
        //    }
        //    var right = VR.Mode.Right.GetComponent<T>();
        //    if (right != null)
        //    {
        //        GameObject.Destroy(right);
        //    }
        //}

        public virtual bool IsTouchpadPress(int index)
        {
            return false;
        }
        public virtual bool IsGripPress(int index)
        {
            return false;
        }
        public virtual bool IsTriggerPress(int index)
        {
            return false;
        }
    }
}
