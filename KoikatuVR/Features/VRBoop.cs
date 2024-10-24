using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using VRGIN.Core;
using VRGIN.Controls;
using System.Linq;
using System.Collections;

namespace KK_VR.Features
{
    /// <summary>
    /// Adds colliders to the controllers so you can boop things
    /// Based on a feature in KK_VREnhancement by thojmr
    /// https://github.com/thojmr/KK_VREnhancement/blob/5e46bc9a89bf2517c5482bc9df097c7f0274730f/KK_VREnhancement/VRController.Collider.cs
    /// </summary>
    public static class VRBoop
    {
        private static readonly List<DynamicBoneCollider> _colliderList = new List<DynamicBoneCollider>();
        internal struct DynBoneParam
        {
            public float m_Radius;
            public DynamicBoneCollider.Direction m_Direction;
            public float m_Height;
            public Vector3 m_Center;
        }
        internal static readonly Dictionary<string, DynBoneParam> _colliderParams = new Dictionary<string, DynBoneParam>()
        {
            {
                "cf_j_middle02_", new DynBoneParam
                {
                    m_Radius = 0.008f,
                    m_Direction = DynamicBoneCollider.Direction.X,
                    m_Height = 0.05f,
                    m_Center = new Vector3(0f, -0.0025f, 0f)
                }
            },
            {
                "cf_j_index02_", new DynBoneParam
                {
                    m_Radius = 0.007f,
                    m_Direction = DynamicBoneCollider.Direction.X,
                    m_Height = 0.05f,
                    m_Center = new Vector3(0f, -0.0015f, 0f)
                }
            },
            {
                "cf_j_ring02_", new DynBoneParam
                {
                    m_Radius = 0.0065f,
                    m_Direction = DynamicBoneCollider.Direction.X,
                    m_Height = 0.05f,
                    m_Center = new Vector3(0f, -0.001f, 0f)
                }
            },
            {
                "cf_j_thumb02_", new DynBoneParam
                {
                    m_Radius = 0.007f,
                    m_Direction = DynamicBoneCollider.Direction.X,
                    m_Height = 0.07f,
                    m_Center = new Vector3(0f, -0.001f, 0f)
                }
            },
            {
                "cf_s_hand_L", new DynBoneParam
                {
                    m_Radius = 0.017f,
                    m_Direction = DynamicBoneCollider.Direction.Z,
                    m_Height = 0.05f,
                    m_Center = new Vector3(-0.01f, -0.005f, 0.005f)
                }
            },
            {
                "cf_s_hand_R", new DynBoneParam
                {
                    m_Radius = 0.017f,
                    m_Direction = DynamicBoneCollider.Direction.Z,
                    m_Height = 0.05f,
                    m_Center = new Vector3(0.01f, -0.005f, 0.005f)
                }
            },
            {
                "_head_00", new DynBoneParam
                {
                    m_Radius = 0.03f,
                    m_Direction = DynamicBoneCollider.Direction.Y,
                    m_Height = 0.035f,
                    m_Center = new Vector3(0f, 0.025f, 0f)
                }
            },
            {
                "J_vibe_02", new DynBoneParam
                {
                    m_Radius = 0.018f,
                    m_Direction = DynamicBoneCollider.Direction.Y,
                    m_Height = 0.08f,
                    m_Center = Vector3.zero
                }
            },
            {
                "J_vibe_05", new DynBoneParam
                {
                    m_Radius = 0.018f,
                    m_Direction = DynamicBoneCollider.Direction.Y,
                    m_Height = 0.05f,
                    m_Center = Vector3.zero
                }
            },
            {
                "cf_j_tang_04", new DynBoneParam
                {
                    m_Radius = 0.005f,
                    m_Direction = DynamicBoneCollider.Direction.Z,
                    m_Height = 0.03f,
                    m_Center = Vector3.zero
                }
            }
        };
        public static void InitPatch()
        {
            // Hooks in here don't get patched by the whole assembly PatchAll since the class has no HarmonyPatch attribute
            Harmony.CreateAndPatchAll(typeof(VRBoop), typeof(VRBoop).FullName);
        }
        public static void InitDB(IEnumerable<GameObject> gameObjectList)
        {
            GetOrAttachCollider(gameObjectList);
        }
        public static void RefreshDynamicBones(bool inactive)
        {
            // Hooks don't give us BetterPenetration dynamic bones.
            var charas = UnityEngine.Object.FindObjectsOfType<ChaControl>();
            foreach (var chara in charas)
            {
                var colliderList = chara.GetComponentsInChildren<DynamicBone>();
                foreach (var collider in colliderList)
                {
                    AttachControllerColliders(GetColliderList(collider));
                }
                var colliderList01 = chara.GetComponentsInChildren<DynamicBone_Ver01>();
                foreach (var collider in colliderList01)
                {
                    AttachControllerColliders(GetColliderList(collider));
                }
                var colliderList02 = chara.GetComponentsInChildren<DynamicBone_Ver02>();
                foreach (var collider in colliderList02)
                {
                    AttachControllerColliders(GetColliderList(collider));
                }
            }

        }
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(DynamicBone), nameof(DynamicBone.SetupParticles))]
        [HarmonyPatch(typeof(DynamicBone_Ver01), nameof(DynamicBone_Ver01.SetupParticles))]
        [HarmonyPatch(typeof(DynamicBone_Ver02), nameof(DynamicBone_Ver02.SetupParticles))]
        private static void OnDynamicBoneInit(MonoBehaviour __instance)
        {
            AttachControllerColliders(__instance);
        }

        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.LoadCharaFbxDataAsync))]
        // [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.LoadCharaFbxDataNoAsync))] // unnecessary, the collider array is reset before the SetupParticles hook
        private static void OnClothesChanged(ref Action<GameObject> actObj)
        {

            // This action is called with the loaded object after the colliders on it are set up
            // This needs to be done despite the SetupParticles hook because LoadCharaFbxData resets the collider list
            actObj += newObj =>
            {
                if (newObj == null) return;
                foreach (var newBone in newObj.GetComponentsInChildren<DynamicBone>())
                {
                    var colliders = newBone.m_Colliders;
                    if (colliders != null)
                        AttachControllerColliders(colliders);
                }
            };
        }

        private static void AttachControllerColliders(MonoBehaviour dynamicBone)
        {
            var colliderList = GetColliderList(dynamicBone);
            if (colliderList == null) return;
            AttachControllerColliders(colliderList);
        }

        private static void AttachControllerColliders(List<DynamicBoneCollider> colliderList)
        {
            if (colliderList == null) throw new ArgumentNullException(nameof(colliderList));

            foreach (var ourCollider in _colliderList)
            {
                if (!colliderList.Contains(ourCollider))
                {
                    colliderList.Add(ourCollider);
                }
            }
            //if (_colliderList.Count != 0 && !colliderList.Contains(_colliderList[0]))  //!colliderList.Any(c => _colliderList.Contains(c)))
            //{
            //    _colliderList.ForEach(c => colliderList.Add(c));
            //}

            //if (_leftCollider && !colliderList.Contains(_leftCollider))
            //    colliderList.Add(_leftCollider);
            //if (_rightCollider && !colliderList.Contains(_rightCollider))
            //    colliderList.Add(_rightCollider);
            //if (_testHand &&  !colliderList.Contains(_testHand))
            //    colliderList.Add(_testHand);
        }

        private static List<DynamicBoneCollider> GetColliderList(MonoBehaviour dynamicBone)
        {
            return dynamicBone switch
            {
                DynamicBone d => d.m_Colliders,
                DynamicBone_Ver01 d => d.m_Colliders,
                DynamicBone_Ver02 d => d.Colliders,
                null => throw new ArgumentNullException(nameof(dynamicBone)),
                _ => throw new ArgumentException(@"Not a DynamicBone - " + dynamicBone.GetType(), nameof(dynamicBone)),
            };
        }
        private static void GetOrAttachCollider(IEnumerable<GameObject> gameObjectList)
        {
            if (gameObjectList == null) throw new ArgumentNullException(nameof(gameObjectList));

            //Check for existing DB collider that may have been attached earlier
            foreach (var gameObject in gameObjectList)
            {
                var existingCollider = gameObject.GetComponentInChildren<DynamicBoneCollider>();
                if (existingCollider == null)
                {
                    VRPlugin.Logger.LogDebug($"AddDB:{gameObject.name}");
                    var param = _colliderParams
                        .Where(kv => gameObject.name.StartsWith(kv.Key, StringComparison.Ordinal)
                        || gameObject.name.EndsWith(kv.Key, StringComparison.Ordinal))
                        .Select(kv => kv.Value)
                        .FirstOrDefault();
                    var colliderObject = new GameObject("DBCollider");
                    var collider = colliderObject.AddComponent<DynamicBoneCollider>();

                    collider.m_Radius = param.m_Radius;
                    collider.m_Height = param.m_Height;
                    collider.m_Center = param.m_Center;
                    collider.m_Direction = param.m_Direction;
                    colliderObject.transform.SetParent(gameObject.transform, worldPositionStays: false);
                    _colliderList.Add(collider);
                }
            }
            {
                ////Add a DB collider to the controller
                ////return AddDbCollider(controllerGameObject, colliderName);
                //return AddDbCollider(controllerGameObject, colliderName, 0.03f, 0f, new Vector3(0f, -0.015f, -0.06f));
            }

            //return existingCollider;
        }
        //private static DynamicBoneCollider AddDbCollider(GameObject controllerGameObject, string colliderName,
        //    float colliderRadius = 0.05f, float collierHeight = 0f, Vector3 colliderCenter = new Vector3(), DynamicBoneCollider.Direction colliderDirection = default)
        //{
        //    //Build the dynamic bone collider
        //    var colliderObject = new GameObject(colliderName);
        //    var collider = colliderObject.AddComponent<DynamicBoneCollider>();
        //    collider.m_Radius = colliderRadius;
        //    collider.m_Height = collierHeight;
        //    collider.m_Center = colliderCenter;
        //    collider.m_Direction = colliderDirection;
        //    colliderObject.transform.SetParent(controllerGameObject.transform, false);
        //    return collider;
        //}
    }
}
