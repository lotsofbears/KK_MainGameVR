﻿using ADV.Commands.Base;
using IllusionUtility.GetUtility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static HandCtrl;
using Random = UnityEngine.Random;

namespace KK_VR.Interpreters.Extras
{
    internal static class TalkSceneExtras
    {
        private static Transform _dirLight;
        internal static void RepositionDirLight(ChaControl chara)
        {
            // It doesn't use 'activeSelf'. Somehow only 'active' changes.
            if (_dirLight == null || _dirLight.gameObject.active)
            {
                _dirLight = GameObject.FindObjectsOfType<Light>()
                    .Where(g => g.name.Equals("Directional Light") && g.gameObject.active)
                    .Select(g => g.transform)
                    .FirstOrDefault();
                if (_dirLight == null)
                {
                    return;
                }
            }
            // We find rotation of vector from base of chara to the center of the scene (0,0,0).
            // Then we create rotation towards it from the chara for random degrees, and elevate it a bit.
            // And place our camera at chara head position + Vector.forward with above rotation.
            // Consistent, doesn't defy logic too often, and is much better then camera directional light that in vr makes one question own eyes.
            // TODO port to KK_VR.

            var lowHeight = (chara.objHeadBone.transform.position.y - chara.transform.position.y) < 0.5f;
            var yDeviation = Random.Range(15f, 45f);
            var xDeviation = Random.Range(15f, lowHeight ? 60f : 30f);
            var lookRot = Quaternion.LookRotation(new Vector3(0f, chara.transform.position.y, 0f) - chara.transform.position);
            _dirLight.transform.SetParent(chara.transform.parent, worldPositionStays: false);
            _dirLight.position = chara.objHeadBone.transform.position + Quaternion.RotateTowards(chara.transform.rotation, lookRot, yDeviation) 
                * Quaternion.Euler(-xDeviation, 0f, 0f) * Vector3.forward;
            _dirLight.rotation = Quaternion.LookRotation((lowHeight ? chara.objBody : chara.objHeadBone).transform.position - _dirLight.position);
        }

        internal static void AddTalkColliders(ChaControl chara)
        {
            string[,] array = new string[3, 3];
            array[0, 0] = "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_L/cf_j_shoulder_L/cf_j_arm00_L/cf_j_forearm01_L/cf_j_hand_L";
            array[0, 1] = "communication/hit_00.unity3d";
            array[0, 2] = "com_hit_hand_L";
            array[1, 0] = "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_R/cf_j_shoulder_R/cf_j_arm00_R/cf_j_forearm01_R/cf_j_hand_R";
            array[1, 1] = "communication/hit_00.unity3d";
            array[1, 2] = "com_hit_hand_R";
            array[2, 0] = "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_j_neck/cf_j_head";
            array[2, 1] = "communication/hit_00.unity3d";
            array[2, 2] = "com_hit_head";

            var chaForm = chara.objBodyBone.transform;
            for (var i = 0; i < 3; i++)
            {
                var target = chaForm.Find(array[i, 0]);
                if (target.Find(array[i, 2]) == null)
                {
                    var collider = CommonLib.LoadAsset<GameObject>(array[i, 1], array[i, 2], true, string.Empty);
                    collider.transform.SetParent(target, false);
                    VRPlugin.Logger.LogDebug($"Extras:Colliders:Add:{target.name}");
                }
                else
                {
                    VRPlugin.Logger.LogDebug($"Extras:Colliders:AlreadyHaveOne:{target.name}");
                }
            }

        }

        internal static Dictionary<int, ReactionInfo> dicNowReactions = new Dictionary<int, ReactionInfo>
        {
            {
                0, new ReactionInfo
                {
                    isPlay = true,
                    weight = 0.3f,
                    lstReleaseEffector = new List<int>(),
                    lstParam = new List<ReactionParam>
                    {
                        new ReactionParam
                        {
                            id = 0,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        },
                        new ReactionParam
                        {
                            id = 1,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        }
                    }
                }
            },
            {
                1, new ReactionInfo
                {
                    isPlay = true,
                    weight = 0.3f,
                    lstReleaseEffector = new List<int>(),
                    lstParam = new List<ReactionParam>
                    {
                        new ReactionParam
                        {
                            id = 0,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        },
                        new ReactionParam
                        {
                            id = 1,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        }
                    }
                }
            },
            {
                2, new ReactionInfo
                {
                    isPlay = true,
                    weight = 0.3f,
                    lstReleaseEffector = new List<int>(),
                    lstParam = new List<ReactionParam>
                    {
                        new ReactionParam
                        {
                            id = 2,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        },
                        new ReactionParam
                        {
                            id = 3,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        }
                    }
                }
            },
            {
                3, new ReactionInfo
                {
                    isPlay = true,
                    weight = 0.3f,
                    lstReleaseEffector = new List<int>
                    {
                        0
                    },
                    lstParam = new List<ReactionParam>
                    {
                        new ReactionParam
                        {
                            id = 4,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.zero,
                                    max = Vector3.up
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        },
                        new ReactionParam
                        {
                            id = 5,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.zero,
                                    max = Vector3.up
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        }
                    }
                }
            },
            {
                4, new ReactionInfo
                {
                    isPlay = true,
                    weight = 0.3f,
                    lstReleaseEffector = new List<int>
                    {
                        1
                    },
                    lstParam = new List<ReactionParam>
                    {
                        new ReactionParam
                        {
                            id = 6,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.zero,
                                    max = Vector3.up
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        },
                        new ReactionParam
                        {
                            id = 7,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.zero,
                                    max = Vector3.up
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        }
                    }
                }
            },
            {
                5, new ReactionInfo
                {
                    isPlay = true,
                    weight = 0.3f,
                    lstReleaseEffector = new List<int>(),
                    lstParam = new List<ReactionParam>
                    {
                        new ReactionParam
                        {
                            id = 8,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = new Vector3(-1f, 0f, -1f),
                                    max = new Vector3(1f, 0f, 1f),
                                },
                                new ReactionMinMax
                                {
                                    min = new Vector3(-1f, 0f, -1f),
                                    max = new Vector3(1f, 0f, 1f),
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        },
                        new ReactionParam
                        {
                            id = 9,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = new Vector3(-1f, 0f, -1f),
                                    max = new Vector3(1f, 0f, 1f),
                                },
                                new ReactionMinMax
                                {
                                    min = new Vector3(-1f, 0f, -1f),
                                    max = new Vector3(1f, 0f, 1f),
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        }
                    }
                }
            },
            {
                6, new ReactionInfo
                {
                    isPlay = true,
                    weight = 0.3f,
                    lstReleaseEffector = new List<int>(),
                    lstParam = new List<ReactionParam>
                    {
                        new ReactionParam
                        {
                            id = 10,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = new Vector3(-1f, 0f, -1f),
                                    max = new Vector3(1f, 0f, 1f),
                                },
                                new ReactionMinMax
                                {
                                    min = new Vector3(-1f, 0f, -1f),
                                    max = new Vector3(1f, 0f, 1f),
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        },
                        new ReactionParam
                        {
                            id = 11,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = new Vector3(-1f, 0f, -1f),
                                    max = new Vector3(1f, 0f, 1f),
                                },
                                new ReactionMinMax
                                {
                                    min = new Vector3(-1f, 0f, -1f),
                                    max = new Vector3(1f, 0f, 1f),
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        }
                    }
                }
            },
        };
    }
}