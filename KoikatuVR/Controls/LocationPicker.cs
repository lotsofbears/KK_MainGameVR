using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRGIN.Core;
using VRGIN.Controls;
using UnityEngine;
using HarmonyLib;

namespace KK_VR.Controls
{
    /// <summary>
    /// A component to add to the controllers the ability to pick a new location in H scenes.
    /// </summary>
    class LocationPicker : ProtectedBehaviour
    {
        Controller _controller;
        LineRenderer _laser;
        bool _laserEnabled;
        H.HPointData _selection; // may be null
        Animator _selectionAnim; // may be null. Also, null if _selection is null.
        Controller.Lock _lock; // may be null. Also, null if _selection is null.


        protected override void OnAwake()
        {
            base.OnAwake();

            _controller = GetComponent<Controller>();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            // TODO: somehow arrange that this component is only enabled during location selection?
            if (Manager.Scene.Instance.NowSceneNames[0].Equals("HPointMove")
                && (_lock != null || _controller.CanAcquireFocus()))
            {
                if (!_laserEnabled)
                {
                    _laserEnabled = true;
                    _laser.gameObject.SetActive(true);
                }

                UpdateSelection();
                HandleTrigger();
            }
            else if (_laserEnabled)
            {
                CleanupSelection();
                _laserEnabled = false;
                _laser.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Update _selection, _selectionAnim and _lock depending on which
        /// location we are currently pointing at.
        /// Also triggers animation and plays sound.
        /// </summary>
        private void UpdateSelection()
        {
            var ray = new Ray(_laser.transform.position, _laser.transform.TransformDirection(Vector3.forward));
            var hit = Physics.RaycastAll(ray)
                .Where(h => h.collider.tag == "H/HPoint")
                .OrderBy(h => h.distance)
                .FirstOrDefault();

            if (hit.collider?.transform.parent.GetComponent<H.HPointData>() is H.HPointData point)
            {
                if (point != _selection)
                {
                    Select(point);
                }
            }
            else
            {
                Unselect();
            }
        }

        /// <summary>
        /// Initiate a location change if the trigger is pulled.
        /// </summary>
        private void HandleTrigger()
        {
            var device = SteamVR_Controller.Input((int)_controller.Tracking.index);
            if (_lock == null || !device.GetPressDown(Valve.VR.EVRButtonId.k_EButton_SteamVR_Trigger))
            {
                return;
            }
            var hPointMove = GameObject.FindObjectOfType<HPointMove>();
            if (hPointMove == null)
            {
                VRLog.Warn("LocationPicker: failed to find HPointMove");
                return;
            }

            var trav = new Traverse(hPointMove);
            var selection = _selection;
            var actionSelect = trav.Field<Action<H.HPointData, int>>("actionSelect").Value;
            int category = trav.Field<int>("nowCategory").Value;

            StartCoroutine(ChangeLocation(() => actionSelect(selection, category)));
        }

        private static IEnumerator ChangeLocation(Action action)
        {
            yield return null;
            action();
            Singleton<Manager.Scene>.Instance.UnLoad();
        }

        private void Unselect()
        {
            if (_selectionAnim != null)
            {
                if (_selectionAnim.GetCurrentAnimatorStateInfo(0).IsName("upidle"))
                {
                    _selectionAnim.SetTrigger("down");
                }
                else
                {
                    _selectionAnim.Play("idle");
                }

            }
            CleanupSelection();
        }

        private void CleanupSelection()
        {
            _selection = null;
            _selectionAnim = null;
            _lock?.Release();
            _lock = null;
        }

        private void Select(H.HPointData point)
        {
            Unselect();
            if (point == null)
            {
                return;
            }
            _selection = point;

            var anim = point.GetComponentInChildren<Animator>();
            if (anim == null)
            {
                return;
            }
            _selectionAnim = anim;

            if (anim.GetCurrentAnimatorStateInfo(0).IsName("idle"))
            {
                anim.SetTrigger("up");
            }
            Illusion.Game.Utils.Sound.Play(Illusion.Game.SystemSE.sel);
            _controller.TryAcquireFocus(out _lock);
        }

        // This method is called by VRGIN via SendMessage.
        private void OnRenderModelLoaded()
        {
            try
            {
                var attachPosition = _controller.FindAttachPosition("tip");

                if (!attachPosition)
                {
                    VRLog.Warn("LocationPicker: Attach position not found for laser!");
                    attachPosition = transform;
                }
                _laser = new GameObject("LocationPicker laser").AddComponent<LineRenderer>();
                _laser.transform.SetParent(attachPosition, false);
                _laser.material = new Material(Shader.Find("Sprites/Default"));
                _laser.startColor = _laser.endColor = new Color(0.21f, 0.96f, 1.00f);

                _laser.positionCount = 2;
                _laser.useWorldSpace = false;
                _laser.startWidth = _laser.endWidth = 0.002f;
                _laser.SetPosition(0, Vector3.zero);
                _laser.SetPosition(1, Vector3.forward * 20);
                _laser.gameObject.SetActive(false);
            }
            catch (Exception e)
            {
                VRLog.Error(e);
            }
        }
    }
}
