using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using VRGIN.Core;
using HarmonyLib;

using static ChaFileDefine;
using KK_VR.Fixes;
using static KK_VR.Handlers.ColliderTracker;
using static KK_VR.Features.LoadVoice;
using KK_VR.Controls;
using KK_VR.Features;

namespace KK_VR.Handlers
{
    static class ClothesHandler
    {
        public static bool IsBodyPartClothed(ChaControl chara, Body part)
        {
            var array = ConvertToSlot(part);
            if (array == null) return false;
            foreach (var item in array)
            {
                if (chara.IsClothes(item)) return true;
            }
            return false;
        }
        private static int[] ConvertToSlot(Body part)
        {
            switch (part)
            {
                case Body.MuneL:
                case Body.MuneR:
                    return new int[] { 0, 2 };
                case Body.UpperBody:
                    return new int[] { 0 };
                case Body.LowerBody:
                    return new int[] { 1, 5 };
                case Body.ArmL:
                case Body.ArmR:
                    return new int[] { 0, 4 };
                case Body.Groin:
                case Body.Asoko:
                    return new int[] { 1, 3, 5 };
                case Body.Thigh:
                case Body.LegL:
                case Body.LegR:
                    return new int[] { 5, 6 };
                default:
                    return null;
            }
        }
        public static bool Undress(ChaControl chara, Body part, bool decrease)
        {

            //if (part == InteractionBodyPart.Crotch && IsWearingSkirt(female))
            //{
            //    //VRLog.Debug($"WearingSkirt");
            //    // Special case: if the character is wearing a skirt, allow
            //    // directly removing the underwear.
            //    targets = _skirtCrotchTargets;
            //}

            var targets = decrease ? UndressDic[part] : RedressDic[part];

            foreach (var target in targets)
            {
                var slot = target.slot;
                if (!chara.IsClothes(slot)
                    || (decrease && chara.fileStatus.clothesState[slot] > target.state)
                    || (!decrease && chara.fileStatus.clothesState[slot] <= target.state))
                {
                    VRPlugin.Logger.LogDebug($"Undress:Invalid:Part:[{part}]");
                    continue;
                }
                else
                {
                    VRPlugin.Logger.LogDebug($"Undress:Valid:Part[{part}]:Slot[{slot}]:State[{target.state}]");
                }
                if (slot > 6)
                {
                    // Target proper shoe slot.
                    slot = chara.fileStatus.shoesType == 0 ? 7 : 8;
                }
                else if (slot == 3 || slot > 4)
                {
                    if (decrease)
                    {
                        // Check for pants. If present override pantyhose/socks/panties with them.
                        if (chara.objClothes[1].GetComponent<DynamicBone>() == null)
                        {
                            slot = 1;
                        }
                    }
                    else 
                    {
                        if (slot != 3)
                        {
                            if (chara.fileStatus.clothesState[3] == 2)
                            {
                                // Is we decided to redress pantyhose/socks with panties hanging on the leg, remove them instead.
                                chara.fileStatus.clothesState[3] = 3;
                            }
                            else if (slot == 5 && chara.fileStatus.clothesState[3] == 1)
                            {
                                // Or put them back on if only shifted and we redress pantyhose.
                                chara.fileStatus.clothesState[3] = 0;
                            }
                        }
                        else
                        {
                            // Put panties on in one go.
                            chara.fileStatus.clothesState[3] = (byte)target.state;
                            return true;
                        }
                        
                    }
                    
                }
                if (decrease)
                {
                    chara.SetClothesStateNext(slot);
                    PlayVoice((VoiceType)UnityEngine.Random.Range(0, 2), chara);
                }
                else
                {
                    chara.SetClothesStatePrev(slot);
                }
                return true;
            }
            return false;
        }


        struct SlotState
        {
            public int slot; 
            public int state;
        }
        private static readonly Dictionary<Body, List<SlotState>> UndressDic = new Dictionary<Body, List<SlotState>>
        {
            // Sequences of cloth slots and their states for each body part.
            // We check if specific cloth slot has corresponding state and toggle this slot to the next state.
            {
                Body.Asoko, new List<SlotState>
                {
                    new SlotState { slot = 1, state = 0 },
                    new SlotState { slot = 5, state = 0 },
                    new SlotState { slot = 3, state = 0 },
                    //new SlotState { slot = 5, state = 1 },
                    new SlotState { slot = 3, state = 1 }
                }
            },
            {
                Body.LowerBody, new List<SlotState>
                {
                    new SlotState { slot = 1, state = 0 },
                    new SlotState { slot = 1, state = 1 }
                }
            },
            {
                Body.UpperBody, new List<SlotState>
                {
                    new SlotState { slot = 0, state = 0 },
                    new SlotState { slot = 0, state = 1 }
                }
            },
            {
                Body.Thigh, new List<SlotState>
                {
                    new SlotState { slot = 5, state = 0 },
                    new SlotState { slot = 6, state = 0 },
                    new SlotState { slot = 1, state = 0 }
                }
            },
            {
                Body.LegL, new List<SlotState>
                {
                    new SlotState { slot = 5, state = 0 },
                    new SlotState { slot = 6, state = 0 },
                    new SlotState { slot = 7, state = 0 },
                    new SlotState { slot = 5, state = 1 },
                    new SlotState { slot = 3, state = 2 }
                }
            },
            {
                Body.LegR, new List<SlotState>
                {
                    new SlotState { slot = 5, state = 0 },
                    new SlotState { slot = 6, state = 0 },
                    new SlotState { slot = 7, state = 0 },
                    new SlotState { slot = 5, state = 1 },
                }
            },
            {
                Body.MuneL, new List<SlotState>
                {
                    new SlotState { slot = 0, state = 0 },
                    new SlotState { slot = 2, state = 0 },
                    new SlotState { slot = 0, state = 1 },
                    new SlotState { slot = 2, state = 1 },
                }
            },
            {
                Body.ArmL, new List<SlotState>
                {
                    new SlotState { slot = 0, state = 0 },
                    new SlotState { slot = 0, state = 1 },
                    new SlotState { slot = 4, state = 0 }
                }
            },
            {
                Body.HandL, new List<SlotState>
                {
                    new SlotState { slot = 4, state = 0 }
                }
            }
        }; 
        private static readonly Dictionary<Body, List<SlotState>> RedressDic = new Dictionary<Body, List<SlotState>>
        {
            // Sequences of cloth slots and their states for each body part.
            // We put specific cloth slot to corresponding state if it was in lesser one.
            {
                Body.Asoko, new List<SlotState>
                {
                    new SlotState { slot = 3, state = 0 },
                    new SlotState { slot = 5, state = 1 },
                    new SlotState { slot = 5, state = 0 },
                    new SlotState { slot = 1, state = 0 }
                }
            },
            {
                Body.LowerBody, new List<SlotState>
                {
                    //new SlotState { slot = 3, state = 0 },
                    new SlotState { slot = 5, state = 1 },
                    new SlotState { slot = 5, state = 0 },
                    new SlotState { slot = 1, state = 0 }
                }
            },
            {
                Body.UpperBody, new List<SlotState>
                {
                    new SlotState { slot = 2, state = 1 },
                    new SlotState { slot = 2, state = 0 },
                    new SlotState { slot = 0, state = 1 },
                    new SlotState { slot = 0, state = 0 },
                }
            },
            {
                Body.Thigh, new List<SlotState>
                {
                    new SlotState { slot = 5, state = 1 },
                    new SlotState { slot = 5, state = 0 },
                    new SlotState { slot = 6, state = 0 }
                }
            },
            {
                Body.LegL, new List<SlotState>
                {
                    new SlotState { slot = 5, state = 1 },
                    new SlotState { slot = 5, state = 0 },
                    new SlotState { slot = 6, state = 0 },
                    new SlotState { slot = 7, state = 0 }
                }
            },
            {
                Body.LegR, new List<SlotState>
                {
                    new SlotState { slot = 5, state = 1 },
                    new SlotState { slot = 5, state = 0 },
                    new SlotState { slot = 6, state = 0 },
                    new SlotState { slot = 7, state = 0 }
                }
            },
            {
                Body.MuneL, new List<SlotState>
                {
                    new SlotState { slot = 2, state = 1 },
                    new SlotState { slot = 2, state = 0 },
                    new SlotState { slot = 0, state = 1 },
                    new SlotState { slot = 0, state = 0 }
                }
            },
            {
                Body.ArmL, new List<SlotState>
                {
                    new SlotState { slot = 4, state = 0 },
                    new SlotState { slot = 0, state = 1 },
                    new SlotState { slot = 0, state = 0 }
                }
            },
            {
                Body.HandL, new List<SlotState>
                {
                    new SlotState { slot = 4, state = 0 }
                }
            }
        };
    }
}
