using ADV.Commands.Base;
using IllusionUtility.GetUtility;
using Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static HandCtrl;
using Random = UnityEngine.Random;

namespace KK_VR.Interpreters
{
    internal static class TalkSceneExtras
    {
        private static Transform _dirLight;
        private static Transform _oldParent;
        internal static void RepositionDirLight(ChaControl chara)
        {
            VRPlugin.Logger.LogDebug($"RepositionDirLight:{KoikatuInterpreter.CurrentScene}");
            // It doesn't use 'activeSelf'. Somehow only 'active' changes.
            if (_dirLight == null || !_dirLight.gameObject.active)
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
            _oldParent = _dirLight.transform.parent;
            // We find rotation of vector from base of chara to the center of the scene (0,0,0).
            // Then we create rotation towards it from the chara for random degrees, and elevate it a bit.
            // And place our camera at chara head position + Vector.forward with above rotation.
            // Consistent, doesn't defy logic too often, and is much better then camera directional light, that in vr makes one question own eyes.

            var lowHeight = (chara.objHeadBone.transform.position.y - chara.transform.position.y) < 0.5f;
            var yDeviation = Random.Range(15f, 45f);
            var xDeviation = Random.Range(15f, lowHeight ? 60f : 30f);
            var lookRot = Quaternion.LookRotation(new Vector3(0f, chara.transform.position.y, 0f) - chara.transform.position);
            _dirLight.transform.SetParent(chara.transform.parent, worldPositionStays: false);
            _dirLight.position = chara.objHeadBone.transform.position + Quaternion.RotateTowards(chara.transform.rotation, lookRot, yDeviation) 
                * Quaternion.Euler(-xDeviation, 0f, 0f) * Vector3.forward;
            _dirLight.rotation = Quaternion.LookRotation((lowHeight ? chara.objBody : chara.objHeadBone).transform.position - _dirLight.position);
        }
        internal static void ReturnDirLight()
        {
            if (_oldParent == null || _dirLight == null)
            {
                VRPlugin.Logger.LogDebug($"ReturnDirLight:ButNoParent:{KoikatuInterpreter.CurrentScene}");
                return;
            }

            VRPlugin.Logger.LogDebug($"ReturnDirLight:{KoikatuInterpreter.CurrentScene}");
            _dirLight.SetParent(_oldParent, false);
        }
        private static readonly string[,] _talkCollidersArray =
        {
            {
                "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_L/cf_j_shoulder_L/cf_j_arm00_L/cf_j_forearm01_L/cf_j_hand_L",
                "communication/hit_00.unity3d",
                "com_hit_hand_L"
            },
            {
                "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_R/cf_j_shoulder_R/cf_j_arm00_R/cf_j_forearm01_R/cf_j_hand_R",
                "communication/hit_00.unity3d",
                "com_hit_hand_R"
            },
            {
                "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_j_neck/cf_j_head",
                "communication/hit_00.unity3d",
                "com_hit_head"
            }
        };
        internal static void AddTalkColliders(IEnumerable<ChaControl> charas)
        {
            // From DNSpy.

            //string[,] array = new string[3, 3];
            //array[0, 0] = "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_L/cf_j_shoulder_L/cf_j_arm00_L/cf_j_forearm01_L/cf_j_hand_L";
            //array[0, 1] = "communication/hit_00.unity3d";
            //array[0, 2] = "com_hit_hand_L";
            //array[1, 0] = "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_R/cf_j_shoulder_R/cf_j_arm00_R/cf_j_forearm01_R/cf_j_hand_R";
            //array[1, 1] = "communication/hit_00.unity3d";
            //array[1, 2] = "com_hit_hand_R";
            //array[2, 0] = "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_j_neck/cf_j_head";
            //array[2, 1] = "communication/hit_00.unity3d";
            //array[2, 2] = "com_hit_head";
            foreach (var chara in charas)
            {
                if (chara == null) continue;
                for (var i = 0; i < 3; i++)
                {
                    var target = chara.objBodyBone.transform.Find(_talkCollidersArray[i, 0]);
                    if (target.Find(_talkCollidersArray[i, 2]) == null)
                    {
                        var collider = CommonLib.LoadAsset<GameObject>(_talkCollidersArray[i, 1], _talkCollidersArray[i, 2], true, string.Empty);
                        collider.transform.SetParent(target, false);
                        //VRPlugin.Logger.LogDebug($"Extras:Colliders:Talk:Add:{target.name}");
                    }
                    //else
                    //{
                    //    VRPlugin.Logger.LogDebug($"Extras:Colliders:Talk:AlreadyHaveOne:{target.name}");
                    //}
                }
            }
            
        }
        internal static void AddHColliders(IEnumerable<ChaControl> charas)
        {
            var _strAssetFolderPath = "h/list/";
            var _file = "parent_object_base_female";
            var text = GlobalMethod.LoadAllListText(_strAssetFolderPath, _file, null);
            if (text == string.Empty) return;
            //string[,] array;
            GlobalMethod.GetListString(text, out var array);
            var length = array.GetLength(0);
            var length2 = array.GetLength(1);
            foreach (var chara in charas)
            {
                if (chara == null) continue;
                for (int i = 0; i < length; i++)
                {
                    for (int j = 0; j < length2; j += 3)
                    {
                        var parentName = array[i, j];
                        var assetName = array[i, j + 1];
                        var colliderName = array[i, j + 2];
                        if (parentName.IsNullOrEmpty() && assetName.IsNullOrEmpty() && colliderName.IsNullOrEmpty())
                        {
                            break;
                        }
                        var parent = chara.objBodyBone.transform.FindLoop(parentName);
                        if (parent.transform.Find(colliderName) != null)
                        {
                            //VRPlugin.Logger.LogDebug($"Extras:Colliders:H:AlreadyHaveOne:{colliderName}");
                            continue;
                        }
                        //else
                        //{
                        //    VRPlugin.Logger.LogDebug($"Extras:Colliders:H:Add:{colliderName}");
                        //}
                        var collider = CommonLib.LoadAsset<GameObject>(assetName, colliderName, true, string.Empty);
                        AssetBundleManager.UnloadAssetBundle(assetName, true, null, false);
                        var componentsInChildren = collider.GetComponentsInChildren<EliminateScale>(true);
                        foreach (var eliminateScale in componentsInChildren)
                        {
                            eliminateScale.chaCtrl = chara;
                        }
                        if (parent != null && collider != null)
                        {
                            collider.transform.SetParent(parent.transform, false);
                        }
                        //if (!this.dicObject.ContainsKey(text4))
                        //{
                        //    this.dicObject.Add(text4, gameObject2);
                        //}
                        //else
                        //{
                        //    UnityEngine.Object.Destroy(this.dicObject[text4]);
                        //    this.dicObject[text4] = gameObject2;
                        //}
                    }
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
