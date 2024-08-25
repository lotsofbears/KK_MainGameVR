using UnityEngine;
using VRGIN.Core;
using HarmonyLib;
using System.Collections.Generic;
using KK_VR.Camera;
using KK_VR.Features;

namespace KK_VR.Interpreters
{
    class HSceneInterpreter : SceneInterpreter
    {
        private bool _active;
        private HSceneProc _proc;
        private Caress.VRMouth _vrMouth;
        private PoV _pov;
        private VRMoverH _vrMoverH;

        public override void OnStart()
        {
        }

        public override void OnDisable()
        {
            Deactivate();
        }

        public override void OnUpdate()
        {
            if (_active && (!_proc || !_proc.enabled))
            {
                // The HProc scene is over, but there may be one more coming.
                Deactivate();
            }

            if (!_active &&
                Manager.Scene.GetRootComponent<HSceneProc>("HProc") is HSceneProc proc &&
                proc.enabled)
            {
                _pov = VR.Camera.gameObject.AddComponent<PoV>();
                _pov.Initialize(proc);
                _vrMouth = VR.Camera.gameObject.AddComponent<Caress.VRMouth>();
                _vrMoverH = VR.Camera.gameObject.AddComponent<VRMoverH>();
                _vrMoverH.Initialize(proc);
                AddControllerComponent<Caress.CaressController>();
                _proc = proc;
                _active = true;
            }
        }

        private void Deactivate()
        {
            if (_active)
            {
                GameObject.Destroy(_pov);
                GameObject.Destroy(_vrMouth);
                GameObject.Destroy(_vrMoverH);
                DestroyControllerComponent<Caress.CaressController>();
                _proc = null;
                _active = false;
            }
        }

    }
}
