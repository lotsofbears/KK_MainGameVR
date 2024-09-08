using ADV.Commands.Base;
using IllusionUtility.GetUtility;
using KK_VR.Features;
using RootMotion.FinalIK;
using SceneAssist;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRGIN.Core;
using static HandCtrl;
using static UnityEngine.UI.Image;

namespace KK_VR.Handlers
{
    internal class ModelHandler
    {
        private static Dictionary<int, AibuItem> aibuItemList = new Dictionary<int, AibuItem>();
        private static Dictionary<int, List<ItemType>> controllersDic = new Dictionary<int, List<ItemType>>();
        private int[] _itemIndex = new int[3];

        internal ModelHandler()
        {
            Load();
            SetItems();
        }
        // God help those who decide to adjust those for different controllers.
        // A heads up => start from ground zero i.e. zero offset/rotation.
        // .. and figure out how to organize it all.
        private List<List<AnimationParameters>> defaultAnimParams = new List<List<AnimationParameters>>()
        {
            // 0 - Hand
            new List<AnimationParameters>()
            {
                new AnimationParameters
                {
                    // Base Layer
                    layerId = 0,
                    //rotation = Quaternion.Euler(0f, 0f, -30f),
                    offset = new Vector3(0f, -0.02f, -0.07f)
                },
                new AnimationParameters
                {
                    // Active
                    // Te_mune_sawari_L
                    layerId = 1,
                    //rotation = Quaternion.Euler(-90f, 0f, 180),
                    offset = new Vector3(0f, -0.02f, -0.07f)
                },
                new AnimationParameters
                {
                    // Static
                    // Te_mune_C_momu_L
                    layerId = 2,
                    //rotation = Quaternion.Euler(-90f, 0f, 180f)
                },
                new AnimationParameters
                {
                    // Semi-active
                    // Te_mune_D_momu_L
                    layerId = 3,
                    //rotation = Quaternion.Euler(-90f, 0f, 180f)
                },
                new AnimationParameters
                {
                    // Active
                    // Te_kokan_sawari_L
                    layerId = 4,
                    //rotation = Quaternion.Euler(-30f, 210f, -75f),
                    offset = new Vector3(0f, -0.02f, -0.07f),
                    rotationOffsetZ = 180f
                },
                new AnimationParameters
                {
                    // Static
                    // Te_kokan_C_kosuru_L
                    layerId = 5,
                    //rotation = Quaternion.Euler(-40f, 180f, -90f)
                },
                new AnimationParameters
                {
                    // Semi-active
                    // Te_kokan_D_kosuru_L
                    layerId = 6,
                    //rotation = Quaternion.Euler(-40f, 180f, -90f)
                },
                new AnimationParameters
                {
                    // Active
                    // Te_anal_sawari_L
                    layerId = 7,
                    //rotation = Quaternion.Euler(-60f, -15f, -160f),
                    offset = new Vector3(0.01f, -0.01f, -0.065f)
                },
                new AnimationParameters
                {
                    // Static
                    // Te_anal_C_ijiru_L
                    layerId = 8,
                    //rotation = Quaternion.Euler(-60f, -15f, -160f),
                    offset = new Vector3(0.01f, -0.01f, -0.065f)
                },
                new AnimationParameters
                {
                    // Semi-active
                    // Te_anal_D_Kosuru_L
                    layerId = 9,
                    //rotation = Quaternion.Euler(-80f, 0f, 0f)
                },
                new AnimationParameters
                {
                    // Static
                    // Te_siri_sawari_L
                    layerId = 10,
                    //rotation = Quaternion.Euler(90f, 0f, 0f),
                    offset = new Vector3(0f, -0.02f, -0.07f)
                },
                new AnimationParameters
                {
                    // Static
                    // Te_siriC_momu_L
                    layerId = 11,
                    //rotation = Quaternion.Euler(-90f, 0f, 0f)
                },
                new AnimationParameters
                {
                    // Static
                    // Te_siri_D_momu_L
                    layerId = 11,
                    //rotation = Quaternion.Euler(-90f, 0f, 0f)
                }
            },
            // 1 - Finger
            new List<AnimationParameters>
            {
                new AnimationParameters
                {
                    // Base Layer
                    layerId = 0,
                   // rotation = Quaternion.identity,
                    offset = new Vector3(0f, -0.02f, -0.07f)
                },
                new AnimationParameters
                {
                    // Active, Preparing to pull
                    // yubi_mune_sawari_L
                    layerId = 1,
                    //rotation = Quaternion.Euler(-80f, 180f, 0f)

                },
                new AnimationParameters
                {
                    // Static
                    // Yubi_mune_C_tutuku_L
                    layerId = 2,
                    //rotation = Quaternion.Euler(-80f, 180f, 0f)
                },
                new AnimationParameters
                {
                    // Semi-active, Pulling
                    // Yubi_mune_D_hipparu_L
                    layerId = 3,
                    //rotation = Quaternion.Euler(-80f, 180f, 0f)
                },
                new AnimationParameters
                {
                    // Active, preparing to stick (1 finger);
                    // Yubi_kokan_sawari_L
                    layerId = 4,
                    //rotation = Quaternion.Euler(-60f, 180f, 0f)
                },
                new AnimationParameters
                {
                    // Static
                    // yubi_kokan_C_ireru_L
                    layerId = 5,
                    //rotation = Quaternion.Euler(-60f, 180f, 0f)
                },
                new AnimationParameters
                {
                    // Semi-active, sticking (2 finger);
                    // Yubi_kokan_D_kakimawasu_L
                    layerId = 6,
                    //rotation = Quaternion.Euler(-60f, 180f, 0f)
                },
                new AnimationParameters
                {
                    // Almost static, preparing to ?? (bent finger)
                    // Yubi_anal_sawari_L
                    layerId = 7,
                    //rotation = Quaternion.Euler(-80f, 0f, 0f)
                },
                new AnimationParameters
                {
                    // Static
                    // Yubi_anal_C_ireru_L
                    layerId = 8,
                    //rotation = Quaternion.Euler(-80f, 0f, 0f)
                },
                new AnimationParameters
                {
                    // Almost static, straight finger
                    // Yubi_anal_C_kakimawasu_L
                    layerId = 9,
                    //rotation = Quaternion.Euler(-80f, 0f, 0f)
                }
            }
        };
        class ItemType
        {
            public AibuItem aibuItem;
            public Transform anchorPoint;
            public List<AnimationParameters> animParamList;
            public int layer;
            public bool enabled;
            public GameObject handlerParent;
            public Vector3 anchorOffset;

        }

        class AnimationParameters
        {
            public int layerId;
            //public Quaternion rotation;
            public float rotationOffsetZ;
            public Vector3 offset;
        }
        public static void DestroyHandlerComponent<T>()
            where T : Component
        {
            for (var  i = 1; i < 3;  i++)
            {
                foreach (var item in controllersDic[i])
                {
                    var component = item.handlerParent.GetComponent<T>();
                    if (component != null)
                    {
                        GameObject.Destroy(component);
                    }
                }
            }
        }
        public static List<T> AddHandlerComponent<T>()
            where T : Component
        {
            var components = new List<T>();
            for (var i = 1; i < 3; i++)
            {
                foreach (var item in controllersDic[i])
                {
                    var component = item.handlerParent.GetComponent<T>();
                    if (component == null)
                    {
                        components.Add(item.handlerParent.AddComponent<T>());
                    }
                    else
                        throw new Exception("AddHandlerComponent:Component is already present");
                }
            }
            return components;
        }
        /// <param name="index">0 - headset, left - 1, right - 2</param>
        public static T GetActiveHandler<T>(int index)
            where T : Component
        {
            VRPlugin.Logger.LogDebug($"Model:GetHandler:{index}");
            foreach(var item in controllersDic[index])
            {
                if (item.enabled)
                    return item.handlerParent.GetComponent<T>();
            }
            return null;
        }
        // Straight from HandCtrl.
        private void Load()
        {
            var textAsset = GlobalMethod.LoadAllListText("h/list/", "AibuItemObject", null);
            GlobalMethod.GetListString(textAsset, out var array);
            for (int i = 0; i < array.GetLength(0); i++)
            {
                int num = 0;
                int num2 = 0;

                int.TryParse(array[i, num++], out num2);

                if (!aibuItemList.TryGetValue(num2, out var aibuItem))
                {
                    aibuItemList.Add(num2, new AibuItem());
                    aibuItem = aibuItemList[num2];
                }
                aibuItem.SetID(num2);


                var manifestName = array[i, num++];
                var text2 = array[i, num++];
                var assetName = array[i, num++];
                aibuItem.SetObj(CommonLib.LoadAsset<GameObject>(text2, assetName, true, manifestName));
                //this.flags.hashAssetBundle.Add(text2);
                var text3 = array[i, num++];
                var isSilhouetteChange = array[i, num++] == "1";
                var flag = array[i, num++] == "1";
                if (!text3.IsNullOrEmpty())
                {
                    aibuItem.objBody = aibuItem.obj.transform.FindLoop(text3);
                    if (aibuItem.objBody)
                    {
                        aibuItem.renderBody = aibuItem.objBody.GetComponent<SkinnedMeshRenderer>();
                        if (flag)
                        {
                            VRPlugin.Logger.LogInfo($"ModelHandler:AddMaterial:{aibuItem.objBody}");
                            aibuItem.mHand = aibuItem.renderBody.material;

                        }
                    }
                }
                aibuItem.isSilhouetteChange = isSilhouetteChange;
                text3 = array[i, num++];
                if (!text3.IsNullOrEmpty())
                {
                    aibuItem.objSilhouette = aibuItem.obj.transform.FindLoop(text3);
                    if (aibuItem.objSilhouette)
                    {
                        aibuItem.renderSilhouette = aibuItem.objSilhouette.GetComponent<SkinnedMeshRenderer>();
                        aibuItem.mSilhouette = aibuItem.renderSilhouette.material;
                    }
                }
                int.TryParse(array[i, num++], out num2);
                aibuItem.SetIdObj(num2);
                int.TryParse(array[i, num++], out num2);
                aibuItem.SetIdUse(num2);
                if (aibuItem.obj)
                {
                    EliminateScale[] componentsInChildren = aibuItem.obj.GetComponentsInChildren<EliminateScale>(true);
                    if (componentsInChildren != null && componentsInChildren.Length != 0)
                    {
                        componentsInChildren[componentsInChildren.Length - 1].LoadList(aibuItem.id);
                    }
                    aibuItem.SetAnm(aibuItem.obj.GetComponent<Animator>());
                    aibuItem.obj.SetActive(false);
                }
                aibuItem.pathSEAsset = array[i, num++];
                aibuItem.nameSEFile = array[i, num++];
                aibuItem.saveID = int.Parse(array[i, num++]);
                aibuItem.isVirgin = (array[i, num++] == "1");
            }
        }
        private void SetItems()
        {
            var controller = VR.Mode.Left.gameObject.transform;
            var other = VR.Mode.Right.gameObject.transform;
            for (var i = 0; i < 3; i++)
            {
                controllersDic.Add(i, new List<ItemType>());
            }

            //_aibuItems[4].obj.transform.SetParent(VR.Camera.Origin, false);
            //var dic = controllersDic[0];
            //dic.Add(cont)
            for (var i = 0; i < 2; i++)
            {
                InitHand(i, 0, controller);
                InitHand(i, 1, other);
            }
            AddDynamicBones();

            StartHand(0);
            StartHand(1);
        }
        private readonly int[] _itemIDs = { 0, 2 };//, 5, 7 }; 
        private void InitHand(int i, int index, Transform parent)
        {
            aibuItemList[_itemIDs[i] + index].obj.transform.SetParent(VR.Camera.Origin, false);
            controllersDic[1 + index].Add(new ItemType());
            var item = controllersDic[1 + index][i];
            item.animParamList = defaultAnimParams[i];
            item.aibuItem = aibuItemList[_itemIDs[i] + index];
            item.aibuItem.renderSilhouette.enabled = false;
            item.anchorPoint = parent;
            //item.aibuItem.obj.layer = 4;
            //var nestedObj = item.aibuItem.obj.transform.GetComponentsInChildren<Transform>();
            //foreach (var obj in nestedObj)
            //{
            //    obj.gameObject.layer = 4;
            //}
            //item.aibuItem.obj.AddComponent<Rigidbody>().isKinematic = true;
            item.aibuItem.SetHandColor(new Color(0.960f, 0.887f, 0.864f, 1.000f));
            //var colliderObject = new GameObject("VRHandCollider");
            item.handlerParent = item.aibuItem.obj.transform.GetComponentsInChildren<Transform>()
                .Where(t => t.name.StartsWith("cf_j_handroot_", StringComparison.Ordinal))
                .Select(t => t.gameObject)
                .FirstOrDefault();
            var collider = item.handlerParent.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.08f, 0.04f, 0.13f);
            collider.isTrigger = true;
            var rigidBody = item.handlerParent.AddComponent<Rigidbody>();
            rigidBody.isKinematic = true;
            //if (colliderParent == null) throw new NullReferenceException(nameof(item.aibuItem.obj));
            //colliderObject.transform.SetParent(item.handlerParent.transform, false);
        }

        private void StartHand(int index)
        {
            var item = controllersDic[1 + index][1];
            item.enabled = true;
            var obj = item.aibuItem.obj;
            obj.transform.SetParent(item.anchorPoint, false);
            obj.transform.SetPositionAndRotation(obj.transform.position + item.animParamList[1].offset, Quaternion.identity);// item.animParamList[0].rotation);
            var movingPart = obj.transform.GetComponentsInChildren<Transform>()
                .Where(t => t.name.StartsWith(movingPartList[1], StringComparison.Ordinal))
                .FirstOrDefault();
            item.anchorOffset = item.anchorPoint.InverseTransformPoint(movingPart.position);
            obj.SetActive(true);
        }

        //private void ScrollItem(int index, bool increase)
        //{
        //    _controllerItems[index][_itemIndex[index]].obj.SetActive(false);
        //    //foreach (var item in _controllerItems[index])
        //    //{
        //    //    item.obj.SetActive(false);
        //    //}
        //    var count = _controllerItems.Count;
        //    if (increase)
        //    {
        //        _itemIndex[index] = (_itemIndex[index] + 1) % count;
        //    }
        //    else
        //    {
        //        if (_itemIndex[index] <= 0)
        //        {
        //            _itemIndex[index] = count - 1;
        //        }
        //        else
        //        {
        //            _itemIndex[index] -= 1;
        //        }
        //    }
        //    _controllerItems[index][_itemIndex[index]].obj.SetActive(true);
        //    _controllerItems[index][_itemIndex[index]].renderSilhouette.enabled = false;
        //}
        //private void StopLayers(int index)
        //{
        //    var item = _controllerItems[index][_itemIndex[index]];
        //    for (var i = 0; i < item.anm.layerCount; i++)
        //    {
        //        item.anm.SetLayerWeight(i, 0f);
        //    }
        //}
        //private int[] _testNumbers = { 0, 1, 4, 7 }; // Finger
        private int[] _testNumbers = { 0, 1, 4 }; // Hand
        private int _currentIndex = 0;

        private List<string> movingPartList = new List<string>()
        {
            // StartsWith.

            "cf_j_handroot_",
            "cf_j_handroot_"
        };
        /*"cf_j_handroot_"
         * cf_j_handangle_
         * cf_s_hand_
         */
        public IEnumerator AnimChange(int index)
        {
            var itemIndex = 1;

            var item = controllersDic[index][itemIndex];
            var aibuItem = item.aibuItem;
            var anchor = item.anchorPoint;
            var movingPart = aibuItem.obj.transform.GetComponentsInChildren<Transform>()
                .Where(t => t.name.StartsWith(movingPartList[itemIndex], StringComparison.Ordinal))
                .FirstOrDefault();

            var oldLayer = item.layer;
            var oldStatic = oldLayer == 0 ? 0 : oldLayer + 1;
            //var oldOffset = item.animParamList[oldLayer].offset;
            _currentIndex++;
            var newLayer = _testNumbers[_currentIndex % _testNumbers.Length];
            var newStatic = newLayer == 0 ? 0 : newLayer + 1;
            //var newOffset = item.animParamList[newLayer].offset;
            var timer = 0f;
            var stop = false;
            while (!stop)
            {
                // There is a small position glitch when transition finishes if we adjust position after setting the final layer weights.
                // No clue why, but atleast it can be helped.

                timer += Time.deltaTime;
                if (timer > 1f)
                {
                    timer = 1f;
                    stop = true;
                }
                //var angleDelta = Quaternion.Angle(movingPart.rotation, targetRot);
                //var rotSpeed = angleDelta / ((1f - timer) / step);


                aibuItem.anm.SetLayerWeight(oldLayer, 1f - timer);
                aibuItem.anm.SetLayerWeight(newLayer, timer);

                //var targetRot = item.anchorPoint.rotation * item.animParamList[newLayer].rotation;

                //var targetRotEx = Quaternion.AngleAxis(Mathf.Lerp(item.animParamList[oldLayer].rotationOffsetZ, item.animParamList[newLayer].rotationOffsetZ, timer),
                //    anchor.up);
                // item.anchorPoint.rotation * item.animParamList[newLayer].rotation;

                //var deltaRot = anchor.rotation * Quaternion.Inverse(movingPart.rotation);

                //aibuItem.obj.transform.rotation = Quaternion.RotateTowards(aibuItem.obj.transform.rotation, targetRot, rotSpeed);

                //aibuItem.obj.transform.SetPositionAndRotation(anchor.position +  - movingPart.position, //
                //    Quaternion.Lerp(aibuItem.obj.transform.rotation, targetRot, timer));
                //aibuItem.obj.transform.rotation = Quaternion.Lerp(aibuItem.obj.transform.rotation, targetRot, timer);
                //aibuItem.obj.transform.position += anchor.TransformPoint(newOffset * timer + oldOffset * (1f - timer)) - movingPart.position;
                //aibuItem.obj.transform.SetPositionAndRotation(aibuItem.obj.transform.position + (anchor.position - movingPart.position), //(anchor.TransformPoint(Vector3.Lerp(oldOffset, newOffset, timer))
                //    deltaRot * aibuItem.obj.transform.rotation);
                aibuItem.obj.transform.rotation = anchor.rotation * Quaternion.Inverse(movingPart.rotation) * aibuItem.obj.transform.rotation;
                aibuItem.obj.transform.position += anchor.TransformPoint(item.anchorOffset) - movingPart.position;

                yield return null;
            }
            //aibuItem.obj.transform.SetPositionAndRotation(aibuItem.obj.transform.position + (anchor.TransformPoint(item.anchorOffset) - movingPart.position),
            //        (anchor.rotation * Quaternion.Inverse(movingPart.rotation)) * aibuItem.obj.transform.rotation);

            item.layer = newLayer;

        }
        private List<string> _colliderParentList = new List<string>()
        {
            "cf_j_middle02_",
            "cf_j_index02_",
            "cf_j_ring02_",
            "cf_j_thumb02_",
            "cf_s_hand_"
        };
        private void AddDynamicBones()
        {
            var gameObjectList = new List<GameObject>();
            for (var i = 1; i < 3; i++)
            {
                foreach (var item in controllersDic[i])
                {
                    var transforms = item.aibuItem.obj.GetComponentsInChildren<Transform>(includeInactive: true)
                        .Where(t => _colliderParentList.Any(c =>  t.name.StartsWith(c)))
                        .ToList();
                    transforms?.ForEach(t => gameObjectList.Add(t.gameObject));
                }
            }
            VRBoop.Initialize(gameObjectList);
        }
        public void UpdateSkinColor(ChaFileControl chaFile)
        {
            var color = chaFile.custom.body.skinMainColor;
            foreach (var item in aibuItemList.Values)
            {
                item.SetHandColor(color);
            }
        }
    }
}
