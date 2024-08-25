using VRGIN.Core;
using UnityEngine;

namespace KK_VR.Interpreters
{
    abstract class SceneInterpreter
    {
        public abstract void OnStart();
        public abstract void OnDisable();
        public abstract void OnUpdate();

        protected void AddControllerComponent<T>()
            where T: Component
        {
            VR.Mode.Left.gameObject.AddComponent<T>();
            VR.Mode.Right.gameObject.AddComponent<T>();
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
