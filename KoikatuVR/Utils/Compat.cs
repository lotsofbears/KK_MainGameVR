using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace KK_VR
{
    /// <summary>
    /// Collection of compatibility methods to support different versions of the
    /// base game.
    /// </summary>
    class Compat
    {
        /// <summary>
        /// This method just returns proc.hand1. Since this field is absent in
        /// some versions of the game, isolating access here allows us to defer
        /// a MethodMissingException until this field is actually used.
        /// </summary>
        /// <param name="proc"></param>
        /// <returns>proc.hand1</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static HandCtrl HSceenProc_hand1(HSceneProc proc)
        {
            return proc.hand1;
        }

        /// <summary>
        /// The ModeChangeForce method of the CameraStateDefinitionChange class
        /// takes 1 or 2 arguments depending on the version of the base game.
        /// This method invokes whichever is available, passing true as the
        /// second argument.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="mode"></param>
        public static void CameraStateDefinitionChange_ModeChangeForce(
            ActionGame.CameraStateDefinitionChange self,
            ActionGame.CameraMode? mode)
        {
            var trav = new Traverse(self);
            var method2 = trav.Method("ModeChangeForce", new[] { typeof(ActionGame.CameraMode?), typeof(bool) });
            if (method2.MethodExists())
            {
                method2.GetValue(mode, true);
            }
            else
            {
                trav.Method("ModeChangeForce", new[] { typeof(ActionGame.CameraMode?) }).GetValue(mode);
            }
        }
    }
}
