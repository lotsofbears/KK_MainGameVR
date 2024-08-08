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
        private Transform _chara;
        //private ChaControl _male;
        private HFlag _hFlag;
        internal KoikatuSettings _settings;
       // private float _height;

        public void Initialize(HSceneProc proc)
        {
            Instance = this;
            var chara = Traverse.Create(proc).Field("lstFemale").GetValue<List<ChaControl>>().FirstOrDefault();
            _hFlag = Traverse.Create(proc).Field("flags").GetValue<HFlag>();
            _poi = chara.objBodyBone.transform.Find("cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_backsk_00");
            _eyes = chara.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz/cf_J_Eye_tz");
            _chara = chara.transform;
            _settings = VR.Context.Settings as KoikatuSettings;
            //_male = proc.male;
        }
        public void MoveToInH(Vector3 position = new Vector3())
        {
            VRLog.Debug($"MoveToInH {_settings.FlyInPov} {_settings.AutoEnterPov}");
            if (POV.Active)
            {
                if (_settings.FlyInPov)
                {
                    // We turn it off because lag is coming, and after that re-enable it and fly towards.
                    POV.Active = false;
                    StartCoroutine(FlyToPov());
                }
            }
            else
            {
                if (_settings.AutoEnterPov)
                {
                    var poseWithMale = false;
                    switch (_hFlag.mode)
                    {
                        case HFlag.EMode.houshi:
                        case HFlag.EMode.sonyu:
                        case HFlag.EMode.houshi3P:
                        case HFlag.EMode.sonyu3P:
                        case HFlag.EMode.houshi3PMMF:
                        case HFlag.EMode.sonyu3PMMF:
                            poseWithMale = true;
                            break;
                    }
                    if (poseWithMale)
                    {
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
        }
        //private void SetMaleHeight()
        //{
        //    var eyes = _male.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz/cf_J_Eye_tz");
        //    //var dudeTop = dude.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/a_n_headtop");

        //    //_height = dudeEyes.position.y * 0.94f - dude.transform.position.y; 
        //    _height = eyes.position.y - _male.transform.position.y;
        //    VRLog.Debug($"SetMaleHeight {_height}");
        //}
        private IEnumerator FlyToPov()
        {
            VRLog.Debug($"FlyToPov");
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
            VRLog.Debug($"FlyTowardPosition");
            var origin = VR.Camera.Origin;
            var head = VR.Camera.Head;
            var poi = _poi;
            VRMouth.NoActionAllowed = true;
            if (_eyes.position.y - _chara.position.y < 0.8f)
            {
                // Not standing position(most likely). For now we simply fly to the side.
                var leftSide = Vector3.Distance(poi.position + poi.right * 0.001f, head.position);
                var rightSide = Vector3.Distance(poi.position + poi.right * -0.001f, head.position);
                if (leftSide < rightSide)
                    position = poi.TransformPoint(new Vector3(0.3f, 0f, 0f));
                else
                    position = poi.TransformPoint(new Vector3(-0.3f, 0f, 0f));
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
            var moveSpeed = 0.5f + Vector3.Distance(head.position, position) * _settings.FlightSpeed;
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
            VRMouth.NoActionAllowed = false;
            VRLog.Debug($"EndOfFlight");
        }
        private IEnumerator FlyTowardPoi()
        {
            VRLog.Debug($"FlyTowardPoi");
            yield return null;
            yield return new WaitUntil(() => Time.deltaTime < 0.05f);
            yield return new WaitForEndOfFrame();
            Vector3 position;
            var origin = VR.Camera.Origin;
            var head = VR.Camera.Head;
            var poi = _poi;
            VRMouth.NoActionAllowed = true;
            if (_eyes.position.y - _chara.position.y < 0.8f)
            {
                // Not standing position(probably). For now we simply fly to the side.
                var leftSide = Vector3.Distance(poi.position + poi.right * 0.001f, head.position);
                var rightSide = Vector3.Distance(poi.position + poi.right * -0.001f, head.position);
                if (leftSide < rightSide)
                    position = poi.TransformPoint(new Vector3(0.3f, 0f, 0f));
                else
                    position = poi.TransformPoint(new Vector3(-0.3f, 0f, 0f));
                position.y += 0.15f;
            }
            else
            {
                // Looks close enough on Pico4, most likely a tad different on others.
                position = poi.position + poi.forward * 0.35f;
                position.y += 0.15f;

            }
            var moveSpeed = 0.5f + Vector3.Distance(head.position, position) * _settings.FlightSpeed;
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
            VRMouth.NoActionAllowed = false;
            VRLog.Debug($"EndOfFlight");
        }
    }
}
