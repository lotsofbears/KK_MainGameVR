using KK_VR.Fixes;
using KK_VR.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KK_VR.Interactors
{
    internal class LimbHandler : Handler
    {
        /// <summary>
        /// Limb index this component is associated with.
        /// </summary>
        private int _index;
        private ChaControl _master;
        private bool _wasBusy;
        private Rigidbody _rigidBody;
        private float _timer;
        private bool _unwind;

        private Transform _curTarget;
        private Transform _origTarget;
        private Vector3 _offset;
        private GraspVisualizer _visual;
        private void Awake()
        {
            _rigidBody = gameObject.AddComponent<Rigidbody>();
            _rigidBody.isKinematic = false;
            _rigidBody.freezeRotation = true;
            // ???
            _rigidBody.useGravity = false;

            // Trigger
            var collider1 = gameObject.AddComponent<SphereCollider>();
            collider1.isTrigger = true;
            collider1.radius = 0.05f;

            // RigidBody's slave.
            var collider2 = gameObject.AddComponent<SphereCollider>();
            collider2.isTrigger = false;
            collider2.radius = 0.035f;
            //Util.CreatePrimitive(PrimitiveType.Sphere, new Vector3(0.07f, 0.07f, 0.07f), transform, Color.magenta,0.25f, true);
            _visual = GraspVisualizer.Instance;
            gameObject.SetActive(false);
        }
        internal void Init(int bodyPartIndex, Transform origTarget, ChaControl chara)
        {
            _origTarget = origTarget;
            _index = bodyPartIndex;
            _master = chara;
        }
        internal void Follow(Transform target = null)
        {
            if (target == null)
            {
                _curTarget = _origTarget;
            }
            else
            {
                _curTarget = target;
                FlushBlacks();
            }
            _offset = _curTarget.InverseTransformPoint(transform.position);
        }
        private void OnCollisionEnter(Collision collision)
        {
            VRPlugin.Logger.LogDebug($"OnCollisionEnter:{collision.gameObject.name}");
        }
        private void FixedUpdate()
        {
            _rigidBody.MoveRotation(_curTarget.rotation);
            _rigidBody.MovePosition(_curTarget.TransformPoint(_offset));

        }
        protected override void OnTriggerEnter(Collider other)
        {
            if (BaseTracker.AddCollider(other))
            {
                if (!_wasBusy)
                {
                    _wasBusy = true;
                    _visual.ChangeColor(_master, _index, true);
                }
            }
        }
        protected override void OnTriggerExit(Collider other)
        {
            if (BaseTracker.RemoveCollider(other))
            {
                if (!IsBusy)
                {
                    _wasBusy = false;
                    _unwind = true;
                    _timer = 1f;
                    _visual.ChangeColor(_master, _index, false);
                }
            }
        }
        private void Update()
        {
            if (_unwind)
            {
                _timer = Mathf.Clamp01(_timer - Time.deltaTime);
                _rigidBody.velocity *= _timer;
                if (_timer == 0f)
                {
                    _unwind = false;
                }
            }
        }
    }
}
