using VRGIN.Core;
using UnityEngine;
using VRGIN.Controls;
using Valve.VR;

namespace KK_VR.Interpreters
{
    abstract class SceneInterpreter
    {
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
        protected static T[] AddControllerComponent<T>()
            where T: Component
        {
            // Controller indexes are 1(L) and 2(R).
            var components = new T[2];
            if (VR.Mode.Left.gameObject.GetComponent<T>() == null)
            {
                components[0] = VR.Mode.Left.gameObject.AddComponent<T>();
                components[1] = VR.Mode.Right.gameObject.AddComponent<T>();
            }
            else
            {
                components[0] = VR.Mode.Left.gameObject.GetComponent<T>();
                components[1] = VR.Mode.Right.gameObject.GetComponent<T>();
            }
            return components;
        }
        /// <summary>
        /// For touchpad direction without click.
        /// </summary>
        public virtual bool OnDirectionDown(Controller.TrackpadDirection direction, int index)
        {
            if (direction == Controller.TrackpadDirection.Left)
            {

                KoikatuInterpreter.Instance.ChangeModelAnim(2);
            }
            return false;
        }
        /// <summary>
        /// For touchpad direction without click.
        /// </summary>
        public virtual bool OnDirectionUp(Controller.TrackpadDirection direction, int index)
        {
            return false;
        }
        /// <summary>
        /// For actual click.
        /// </summary>
        public virtual bool OnButtonDown(EVRButtonId buttonId, Controller.TrackpadDirection direction, int index)
        {
            return false;
        }
        /// <summary>
        /// For actual click.
        /// </summary>
        public virtual bool OnButtonUp(EVRButtonId buttonId, Controller.TrackpadDirection direction, int index)
        {
            return false;
        }
        public enum Timing
        {
            Fraction,
            Half,
            Full
        }
        protected static void DestroyControllerComponent<T>()
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
