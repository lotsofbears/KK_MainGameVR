using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRGIN.Controls;
using VRGIN.Controls.Tools;
using VRGIN.Core;
using VRGIN.Helpers;
using VRGIN.Modes;
using UnityEngine;
using KK_VR.Features;
using KK_VR.Interpreters;

namespace KK_VR
{
    class KoikatuStandingMode : StandingMode
    {
        public override IEnumerable<Type> Tools
        {
            get
            {
                return new[] { 
                    //typeof(Controls.KoikatuMenuTool), 
                    //typeof(Controls.KoikatuWarpTool), 
                    typeof(Controls.SchoolTool)};
            }
        }

        protected override IEnumerable<IShortcut> CreateShortcuts()
        {
            // Disable all VRGIN shortcuts. We'll define necessary shortcuts
            // (if any) by ourselves.
            return Enumerable.Empty<IShortcut>();
        }

        protected override void OnStart()
        {
            base.OnStart();
        }

        protected override Controller CreateLeftController()
        {
            //return AddComponents(base.CreateLeftController());

            var controller = AddComponents(base.CreateLeftController());
            controller.ToolIndex = 0;
            return controller;
        }

        protected override Controller CreateRightController()
        {
            VRPlugin.Logger.LogDebug($"CreateController");
            var controller = AddComponents(base.CreateRightController());
            controller.ToolIndex = 0;
            return controller;
        }

        private static Controller AddComponents(Controller controller)
        {
            controller.gameObject.AddComponent<Controls.LocationPicker>();
            return controller;
        }

        protected override void SyncCameras()
        {
            // Do nothing. CameraControlControl and friends take care of this.
        }

        protected override void InitializeScreenCapture()
        {
            // Don't enable CapturePanorama because it looks broken (throws an
            // exception).
        }
    }
}
