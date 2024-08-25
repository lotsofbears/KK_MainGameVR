using KK_VR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using VRGIN.Core;

namespace KK_VR.Mirror
{
    /// <summary>
    /// Mirrors in the base game look very weird in VR. This object
    /// replaces components and materials to fix this issue.
    /// </summary>
    class Manager
    {
        private Material _material;

        public void Fix(MirrorReflection refl)
        {
            if (refl.GetComponent<VRReflection>() != null)
            {
                return;
            }
            var mirror = refl.gameObject;
            GameObject.Destroy(refl);
            mirror.AddComponent<VRReflection>();
            mirror.GetComponent<Renderer>().material = Material();
        }

        private Material Material()
        {
            if (_material == null)
            {
                var shader = VRGIN.Helpers.UnityHelper.LoadFromAssetBundle<Shader>(
                    Resource.mirror_shader,
                    "Assets/MirrorReflection.shader");
                if (shader == null)
                {
                    VRLog.Error("Failed to load shader");
                }
                _material = new Material(shader);
            }
            return _material;
        }
    }
}
