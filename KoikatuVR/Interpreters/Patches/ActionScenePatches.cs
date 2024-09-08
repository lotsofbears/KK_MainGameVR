//using HarmonyLib;
//using StrayTech;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection;
//using System.Reflection.Emit;
//using System.Text;
//using UnityEngine;

//namespace KK_VR.Interpreters.Patches
//{
//    [HarmonyPatch]
//    internal class ActionScenePatches
//    {
//        public static Transform GetCamera()
//        {
//            if (ActionSceneInterpreter.FakeCamera == null)
//            {
//                return MonoBehaviourSingleton<CameraSystem>.Instance.CurrentCamera.transform;
//            }
//            return ActionSceneInterpreter.FakeCamera;
//        }

//        [HarmonyTranspiler, HarmonyPatch(typeof(ActionGame.Chara.Mover.Main), nameof(ActionGame.Chara.Mover.Main.Update))]
//        public static IEnumerable<CodeInstruction> MoverMainUpdate(IEnumerable<CodeInstruction> instructions)
//        {
//            var found = false;
//            var counter = 0;
//            var done = false;
//            VRPlugin.Logger.LogDebug($"Trans:MoverUpdate:Start");
//            foreach (var code in instructions)
//            {
//                if (!done)
//                {
//                    if (!found)
//                    {
//                        if (counter == 0)
//                        {
//                            if (code.opcode == OpCodes.Brfalse)
//                            {
//                                counter++;
//                            }
//                        }
//                        else
//                        {
//                            if (code.opcode == OpCodes.Call && code.operand is MethodInfo info
//                                && info.Name.Equals($"get_Instance"))
//                            {
//                                VRPlugin.Logger.LogDebug($"Mover:Update:{code.opcode}:{code.operand}");
//                                found = true;
//                                yield return new CodeInstruction(OpCodes.Call, AccessTools.FirstMethod(typeof(ActionScenePatches), m => m.Name.Equals(nameof(GetCamera))));
//                                continue;
//                            }
//                            else
//                            {
//                                counter = 0;
//                            }
//                        }
                            

//                    }
//                    else
//                    {
//                        VRPlugin.Logger.LogDebug($"Mover:Update:{code.opcode}:{code.operand}");
//                        counter++;
//                        if (counter == 2)
//                        {
//                            done = true;
//                        }
//                        yield return new CodeInstruction(OpCodes.Nop);
//                        continue;
//                    }
//                }
//                yield return code;
//            }
//        }
//    }
//}
