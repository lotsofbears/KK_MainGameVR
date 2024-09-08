using ADV.Commands.Object;
using HarmonyLib;
using KK_VR.Caress;
using KK_VR.Features;
using KK_VR.Settings;
using KK_VR;
using NodeCanvas.Tasks.Actions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using UnityEngine;
using VRGIN.Core;
using static RootMotion.FinalIK.InteractionTrigger;
using static UnityEngine.UI.Image;
using KK_VR.Interpreters;

namespace KK_VR.Camera
{
    /// <summary>
    /// We fly towards adjusted positions. By flying rather then teleporting the sense of actual scene is created. No avoidance system (yet). 
    /// </summary>
    class VRMoverH : MonoBehaviour
    {
        public static VRMoverH Instance;
        private Transform _chara;
        private Transform _eyes;
        private Transform _torso;
        private Transform _kokan;
        private PoV _pov;
        //private List<Coroutine> _activeCoroutines = new List<Coroutine>();
        internal KoikatuSettings _settings;

        public void Initialize()
        {
            Instance = this;
            _pov = PoV.Instance;
            var chara = HSceneInterpreter.lstFemale[0];
            _chara = chara.transform;
            _eyes = chara.objHeadBone.transform.Find("cf_J_N_FaceRoot/cf_J_FaceRoot/cf_J_FaceBase/cf_J_FaceUp_ty/cf_J_FaceUp_tz/cf_J_Eye_tz");
            _torso = chara.objBodyBone.transform.Find("cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03");
            _kokan = chara.objBodyBone.transform.Find("cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_d_kokan/cf_j_kokan");
            _settings = VR.Context.Settings as KoikatuSettings;
        }
        public void MoveToInH(Vector3 position, Quaternion rotation, bool actionChange, HFlag.EMode mode)
        {
            //VRPlugin.Logger.LogDebug("VRMoverH:MoveToInH");
            StopAllCoroutines();
            if (_pov != null && (_pov.Active || (_settings.AutoEnterPov && actionChange)))
            {
                _pov.DisablePov(teleport: false);
                if (mode != HFlag.EMode.aibu)
                {
                    StartCoroutine(FlyToPov());
                    return;
                }
            }
            StartCoroutine(FlyToPosition(position, rotation));
        }
        //private void HaltMovements()
        //{

        //    //foreach (var coroutine in _activeCoroutines)
        //    //{
        //    //    StopCoroutine(coroutine);
        //    //}
        //    //_activeCoroutines.Clear();
        //}
        /// <param name="method">Will invoke once upright</param>
        public void MakeUpright(Action method = null, params object[] args)
        {
            VRPlugin.Logger.LogDebug($"VRMoverH:MakeUpright");
            StartCoroutine(RotateToUpright(method, args));
        }
        
        private IEnumerator RotateToUpright(Action method = null, params object[] args)
        {
            yield return null;
            yield return new WaitUntil(() => Time.deltaTime < 0.05f);
            yield return new WaitForEndOfFrame();
            var head = VR.Camera.Head;
            var origin = VR.Camera.Origin;
            if (origin.eulerAngles.x != 0f || origin.eulerAngles.z != 0f)
            {
                var uprightRot = Quaternion.Euler(0f, origin.eulerAngles.y, 0f);
                Vector3 oldPos;
                while (Mathf.Abs(origin.eulerAngles.x) > 0.1f || Mathf.Abs(origin.eulerAngles.x) > 0.1f)
                {
                    oldPos = head.position;
                    origin.rotation = Quaternion.RotateTowards(origin.rotation, uprightRot, Time.deltaTime * 120f);
                    origin.position += oldPos - head.position;
                    yield return new WaitForEndOfFrame();
                }
                oldPos = head.position;
                origin.rotation = uprightRot;
                origin.position += oldPos - head.position;
            }
            method?.DynamicInvoke(args);
            VRPlugin.Logger.LogDebug($"VRMoverH:MakeUpright:Done");
        }
        private IEnumerator FlyToPov()
        {
            VRPlugin.Logger.LogDebug($"VRMoverH:FlyToPov");
            // We wait for the lag of position change.
            yield return null;
            yield return new WaitUntil(() => Time.deltaTime < 0.05f);
            //if (actionChange)
            //{
            //    yield return new WaitForEndOfFrame();
            //    var destination = _pov.GetDestination();
            //    if (destination != Vector3.zero)
            //    {
            //        var head = VR.Camera.Head;
            //        var origin = VR.Camera.Origin;
            //        var distance = Vector3.Distance(destination, head.position);
            //        var target = destination + _pov.GetRotation() * (Vector3.forward * 0.4f);
            //        var rotation = Quaternion.LookRotation(target - head.position);
            //        if (distance > 2f && Quaternion.Angle(origin.rotation, rotation) > 30f)
            //        {
            //            VRPlugin.Logger.LogDebug($"VRMoverH:FlyToPov:MovementOverride");
            //            var moveSpeed = 0.5f + distance * 0.5f * _settings.FlightSpeed;
            //            var halfDist = distance * 0.5f;
            //            while (true)
            //            {
            //                var angleDelta = Mathf.Clamp(Quaternion.Angle(origin.rotation, rotation) - 30f, 0f, 180f);
            //                if (angleDelta == 0f)
            //                {
            //                    break;
            //                }
            //                distance = Vector3.Distance(destination, head.position) - halfDist;
            //                var step = Time.deltaTime * moveSpeed;
            //                var moveTowards = Vector3.MoveTowards(head.position, destination, step);
            //                var rotSpeed = angleDelta / (distance / step);
            //                origin.rotation = Quaternion.RotateTowards(origin.rotation, rotation, 1f * rotSpeed);
            //                origin.position += moveTowards - head.position;
            //                yield return new WaitForEndOfFrame();
            //            }
            //            while (true)
            //            {
            //                distance = Vector3.Distance(destination, head.position);
            //                var step = Time.deltaTime * moveSpeed;
            //                var angleDelta = Quaternion.Angle(origin.rotation, rotation);
            //                var moveTowards = Vector3.MoveTowards(head.position, destination, step);
            //                var rotSpeed = angleDelta / (distance / step);
            //                origin.rotation = Quaternion.RotateTowards(origin.rotation, rotation, 1f * rotSpeed);
            //                origin.position += moveTowards - head.position;
            //                yield return new WaitForEndOfFrame();
            //                if (distance < step)
            //                {
            //                    break;
            //                }
            //            }
            //        }
            //    }
            //}
            //VRPlugin.Logger.LogDebug($"VRMoverH:FlyToPov:Done");
            _pov.StartPov();
        }
        private IEnumerator FlyToPosition(Vector3 position, Quaternion rotation)
        {
            yield return null;
            yield return new WaitUntil(() => Time.deltaTime < 0.05f);
            yield return new WaitForEndOfFrame();
            VRLog.Debug($"VRMoverH:FlyToPosition[{VR.Camera.transform.position}]");
            var origin = VR.Camera.Origin;
            var head = VR.Camera.Head;
            VRMouth.NoActionAllowed = true;
            var height = _eyes.transform.position.y;

            if (height - _chara.transform.position.y > 1f)
            {
                VRLog.Debug($"VRMoverH:FlyToPosition[height is high, resetting rotation]");
                // Upright (probably) position, some of them have weird rotations.
                rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
                if (position.y < height)
                {
                    VRLog.Debug($"VRMoverH:FlyToPosition[height is low, meeting eye level]");
                    position.y = height;
                }

            }
            else
            {
                position.y += 0.2f;
                VRLog.Debug($"VRMoverH:FlyToPosition[height is low, increasing a bit]");
            }

            //else
            //{
            //    

            //}
            //var dic = new Dictionary<float, Vector3>()
            //{
            //    {Vector3.Distance(position, _kokan.position), _kokan.position },
            //    {Vector3.Distance(position, _torso.position), _torso.position },
            //    {Vector3.Distance(position, _eyes.position), _eyes.position }
            //};

            var distKokan = Vector3.Distance(position, _kokan.position);
            var distTorso = Vector3.Distance(position, _torso.position);
            var distEyes = Vector3.Distance(position, _eyes.position);
            var proximity = Mathf.Min(distEyes, distTorso, distKokan);// dic.Min(k => k.Key);
            if (proximity > 0.4f)
            {
                // We are moving.. somewhere, maybe we'll get closer. Changing rotation dulls it move often then not.

                VRLog.Debug($"VRMoverH:FlyToPosition[not close enough, moving forward for {proximity - 0.4f}]");
                position += rotation * Vector3.forward * (proximity - 0.4f);

            }
            var moveSpeed = 0.5f + Vector3.Distance(head.position, position) * _settings.FlightSpeed;
            //var halfDistance = Vector3.Distance(head.position, position) * 0.5f;
            //var auxRot = Quaternion.LookRotation(position - head.position);
            //while (true)
            //{
            //    var moveTowards = Vector3.MoveTowards(head.position, position, Time.deltaTime * moveSpeed);
            //    origin.rotation = Quaternion.RotateTowards(origin.rotation, auxRot, Time.deltaTime * 60f);
            //    origin.position += moveTowards - head.position;
            //    if (Vector3.Distance(head.position, position) < halfDistance)
            //    {
            //        break;
            //    }
            //}
            while (true)
            {
                var distance = Vector3.Distance(head.position, position);
                var angleDelta = Quaternion.Angle(origin.rotation, rotation);
                var step = Time.deltaTime * moveSpeed;
                var rotSpeed = angleDelta / (distance / step);
                var moveTowards = Vector3.MoveTowards(head.position, position, step);
                origin.rotation = Quaternion.RotateTowards(origin.rotation, rotation, rotSpeed);
                origin.position += moveTowards - head.position;
                if (distance < step && angleDelta < 1f)
                {
                    break;
                }
                yield return new WaitForEndOfFrame();
            }
            VRMouth.NoActionAllowed = false;
            VRLog.Debug($"EndOfFlight");
        }
    }
}
