using HarmonyLib;
using KoikatuVR.Caress;
using KoikatuVR.Settings;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text;
using UnityEngine;
using VRGIN.Core;

namespace KoikatuVR.Camera
{
    /// <summary>
    /// We fly towards adjusted positions. By flying rather then teleporting the sense of actual scene is created. No avoidance system (yet). 
    /// </summary>
    class VRMoverH : MonoBehaviour
    {
        public static VRMoverH Instance;
        private Transform _poi;
        private Transform _eyes;
        private HFlag _hFlag;

        public void Initialize(HSceneProc proc)
        {
            Instance = this;
            var chaControl = Traverse.Create(proc).Field("lstFemale").GetValue<List<ChaControl>>().FirstOrDefault();
            _hFlag = Traverse.Create(proc).Field("flags").GetValue<HFlag>();
            _poi = chaControl.objBodyBone.transform.Find("cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_backsk_00");
            _eyes = chaControl.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz/cf_J_Eye_tz");
        }
        public void MoveToInH(Vector3 position = new Vector3())
        {
            if (POV.Active)
                return;
            else
            {
                switch (_hFlag.mode)
                {
                    case HFlag.EMode.houshi:
                    case HFlag.EMode.sonyu:
                    case HFlag.EMode.houshi3P:
                    case HFlag.EMode.sonyu3P:
                    case HFlag.EMode.houshi3PMMF:
                    case HFlag.EMode.sonyu3PMMF:
                        StartCoroutine(FlyToPov());
                        return;
                }
            }
            if (position == Vector3.zero)
            {
                StartCoroutine(FlyTowardPoi());
            }
            else
            {
                StartCoroutine(FlyTowardPosition(position));
            }
        }

        private IEnumerator FlyToPov()
        {
            // We wait for the lag of position change.
            yield return null;
            yield return new WaitUntil(() => Time.deltaTime < 0.05f);
            POV.Instance.StartPov();
        }
        private IEnumerator FlyTowardPosition(Vector3 position)
        {
            yield return null;
            yield return new WaitUntil(() => Time.deltaTime < 0.05f);
            yield return new WaitForEndOfFrame();
            //VRLog.Debug($"MoveToInH {_hFlag.mode} {Time.deltaTime}");
            var origin = VR.Camera.Origin;
            var head = VR.Camera.Head;
            var poi = _poi;
            VRMouth.NoKissingAllowed = true;
            if (poi.position.y < 1f)
            {
                // Not standing position(probably). For now we simply fly to the side.
                var leftSide = Vector3.Distance(poi.position + poi.right * 0.001f, head.position);
                var rightSide = Vector3.Distance(poi.position + poi.right * -0.001f, head.position);
                if (leftSide < rightSide)
                    position = poi.position + poi.right * 0.3f;
                else
                    position = poi.position + poi.right * -0.3f;
                position.y += 0.15f;
            }
            else
            {
                // We get closer position.
                var distance = Vector3.Distance(position, poi.position);
                if (distance > 0.4f)
                {
                    position = Vector3.MoveTowards(position, poi.position, distance - 0.4f);
                }
                position.y = poi.position.y + 0.15f;

            }
            var moveSpeed = 0.5f + Vector3.Distance(head.position, position);
            var lookRotation = Quaternion.LookRotation(_eyes.position - position);
            while (true)
            {
                var distance = Vector3.Distance(head.position, position);
                var angleDelta = Quaternion.Angle(origin.rotation, lookRotation);
                var rotSpeed = angleDelta / (distance / (Time.deltaTime * moveSpeed));
                var moveTowards = Vector3.MoveTowards(head.position, position, Time.deltaTime * moveSpeed);
                origin.rotation = Quaternion.RotateTowards(origin.rotation, lookRotation, 1f * rotSpeed);
                origin.position += moveTowards - head.position;
                if (distance < 0.05f && angleDelta < 1f)
                    break;
                yield return new WaitForEndOfFrame();
            }
            VRMouth.NoKissingAllowed = false;
            //VRLog.Debug($"EndOfFlight");
        }
        private IEnumerator FlyTowardPoi()
        {
            //VRLog.Debug($"StartOfFlight");
            // My machine stutters on animation change, this is a way to wait for lag.
            // After that we don't want to be in windows after update and before animation routine.
            // My machine stutters on animation change, this is a way to wait for lag.
            // After that we don't want to be in windows after update and before animation routine.
            yield return null;
            yield return new WaitUntil(() => Time.deltaTime < 0.05f);
            yield return new WaitForEndOfFrame();
            Vector3 position;
            var origin = VR.Camera.Origin;
            var head = VR.Camera.Head;
            var poi = _poi;
            VRMouth.NoKissingAllowed = true;
            if (poi.position.y < 1f)
            {
                // Not standing position(probably). For now we simply fly to the side.
                var leftSide = Vector3.Distance(poi.position + poi.right * 0.001f, head.position);
                var rightSide = Vector3.Distance(poi.position + poi.right * -0.001f, head.position);
                if (leftSide < rightSide)
                    position = poi.position + poi.right * 0.3f;
                else
                    position = poi.position + poi.right * -0.3f;
                position.y += 0.15f;
            }
            else
            {
                // Looks close enough on Pico4, most likely a tad different for other headsets.
                position = poi.position + poi.forward * 0.35f;
                position.y += 0.15f;

            }
            var moveSpeed = 0.5f + Vector3.Distance(head.position, position);
            var lookRotation = Quaternion.LookRotation(_eyes.position - position);
            while (true)
            {
                var distance = Vector3.Distance(head.position, position);
                var angleDelta = Quaternion.Angle(origin.rotation, lookRotation);
                var rotSpeed = angleDelta / (distance / (Time.deltaTime * moveSpeed));
                var moveTowards = Vector3.MoveTowards(head.position, position, Time.deltaTime * moveSpeed);
                origin.rotation = Quaternion.RotateTowards(origin.rotation, lookRotation, 1f * rotSpeed);
                origin.position += moveTowards - head.position;
                if (distance < 0.05f && angleDelta < 1f)
                    break;
                yield return new WaitForEndOfFrame();
            }
            VRMouth.NoKissingAllowed = false;
            //VRLog.Debug($"EndOfFlight");
        }
    }
}
