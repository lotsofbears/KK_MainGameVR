using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRGIN.Controls;
using UnityEngine;

namespace KK_VR.Controls
{
    class KoikatuMenuTool : VRGIN.Controls.Tools.MenuTool
    {
        public override List<HelpText> GetHelpTexts()
        {
            return new List<HelpText>(new HelpText[] {
                ToolUtil.HelpTrackpadCenter(Owner, "Tap to click"),
                ToolUtil.HelpTrackpadRight(Owner, "Slide to move cursor"),
                ToolUtil.HelpTrigger(Owner, "Click"),
                ToolUtil.HelpGrip(Owner, "Take/release screen"),
            }.Where(x => x != null));
        }
    }
}
