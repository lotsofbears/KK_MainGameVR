//using KK_VR;
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using UnityEngine;
//using VRGIN.Core;

//namespace KK_VR.Camera
//{
//    internal class VRMoverEx : MonoBehaviour
//    {
//        public static VRMoverEx Instance;
//        private void Awake()
//        {
//            Instance = this;
//        }
//        public void LookAtRotationY(Quaternion rotation, Action method = null, params object[] args)
//        {
//            VRPlugin.Logger.LogDebug($"VRMoverEx:LookAtRotationY");
//            StartCoroutine(RotateTowardsY(rotation, method, args));
//        }

//        private IEnumerator RotateTowardsY(Quaternion rotation, Action method = null, params object[] args)
//        {
//            yield return null;
//            yield return new WaitUntil(() => Time.deltaTime < 0.05f);
//            yield return new WaitForEndOfFrame();
//            var head = VR.Camera.Head;
//            var origin = VR.Camera.Origin;
//            var target = rotation.eulerAngles.y;
//            if (Mathf.Abs(target - origin.eulerAngles.y) > 1f)
//            {
//                Vector3 oldPos;
//                var step = Quaternion.Euler(0f, (target - origin.eulerAngles.y) * Time.deltaTime, 0f);
//                while (Mathf.Abs(target - origin.eulerAngles.y) > 0.1f)
//                {
//                    oldPos = head.position;
//                    origin.rotation *= step;
//                    origin.position += oldPos - head.position;
//                    yield return new WaitForEndOfFrame();
//                }
//            }
//            method?.DynamicInvoke(args);
//            VRPlugin.Logger.LogDebug($"VRMoverEx:LookAtRotationY:Done");
//        }
//    }
//}
