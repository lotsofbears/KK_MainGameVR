//using ADV.Commands.Object;
//using BepInEx;
//using Illusion.Game;
//using IllusionUtility.GetUtility;
//using KK_VR.Features;
//using KK_VR.Fixes;
//using KK_VR.Interpreters;
//using SceneAssist;
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using UnityEngine;
//using UnityEngine.Networking;
//using VRGIN.Core;
//using static HandCtrl;

//namespace KK_VR.Handlers
//{
//    // We adapt animated aibu items as controller models. To see why we do this in SUCH a roundabout way
//    // grab default disabled ones in HScene and scroll through their animation layers,
//    // their orientations are outright horrible for our purposes.
//    internal static class ModelHandler
//    {
//        // There currently a bug that doesn't let every second chosen 'Finger hand" to scale.
//        // Initially asset has component that does exactly this (EliminateScale), but we remove it during initialization.
//        // Yet once parented every !second! time, 'Finger hand' freezes it's own local scale at Vec.one;
//        // At that moment no components 'EliminateScale' are present in runtime, no clue what can it be.

//        private static readonly Dictionary<int, AibuItem> aibuItemList = new Dictionary<int, AibuItem>();
//        private static readonly Dictionary<int, List<ItemType>> controllersDic = new Dictionary<int, List<ItemType>>();
//        private static readonly int[]  _itemIndexes = new int[3];
//        private static readonly List<ItemType> _activeItems = new List<ItemType>();
//        private static readonly List<Transform> _sfxTransforms = new List<Transform>();
//        internal static Material GetSilhouetteMaterial => _material;
//        private static Material _material;
//        internal static bool init;

//        internal static void Init()
//        {
//            if (init) return;
//            init = true;
//            Load();
//            SetItems();
//            PopulateDic();
//        }

//        private static readonly List<AnimationParameter> defaultAnimParamList = new List<AnimationParameter>()
//        {
//            new AnimationParameter
//            {
//                // Hand
//                availableLayers = new int[] { 4, 7, 10 },
//                movingPartName = "cf_j_handroot_",
//                handlerParentName = "cf_j_handroot_",
//                positionOffset = new Vector3(0f, -0.02f, -0.07f),
//                rotationOffset = Quaternion.identity
//            },
//            new AnimationParameter
//            {
//                // Finger
//                availableLayers = new int[] { 1, 3, 9, 4, 6 },
//                movingPartName = "cf_j_handroot_",
//                handlerParentName = "cf_j_handroot_",
//                positionOffset = new Vector3(0f, -0.02f, -0.07f),
//                rotationOffset = Quaternion.identity
//            },
//            new AnimationParameter
//            {
//                // Massager
//                availableLayers = new int[] { 1, 0 },
//                movingPartName = "N_massajiki_",
//                handlerParentName = "_head_00",
//                positionOffset = new Vector3(0f, 0f, -0.05f),
//                rotationOffset = Quaternion.Euler(-90f, 180f, 0f)
//            },
//            new AnimationParameter
//            {
//                // Vibrator
//                availableLayers = new int[] { 1, 0 },
//                movingPartName = "N_vibe_Angle",
//                handlerParentName = "J_vibe_03",
//                positionOffset = new Vector3(0f, 0f, -0.1f),
//                rotationOffset = Quaternion.Euler(-90f, 180f, 0f)
//            },
//            new AnimationParameter
//            {
//                // Tongue
//                /*
//                 *  21, 16, 18, 19   - licking haphazardly
//                 *      7 - at lower angle 
//                 *      9 - at lower angle very slow
//                 *      10 - at higher angle
//                 *      12 - at higher angle very slow 
//                 *      
//                 *  13 - very high angle, flopping
//                 *  
//                 *  15 - very high angle, back forth
//                 *  1, 3, 4, 6,   - licking 
//                 */
//                availableLayers = new int[] { 1, 7, 9, 10, 12, 13, 15, 16 },
//                movingPartName = "cf_j_tang_01", // cf_j_tang_01 / cf_j_tangangle
//                handlerParentName = "cf_j_tang_03",
//                positionOffset = new Vector3(0f, -0.04f, 0.05f),
//                rotationOffset = Quaternion.identity, // Quaternion.Euler(-90f, 0f, 0f)
//            }
//        };

//        //
//        // handlerParent - a gameObject with kinematic-collider-trigger and our component to control it.
//        // anchorPoint - an extra gameObject with non-kinematic-collider to which we attach aibuItem (or empty) and then move together with controller.
//        // movingPoint - a gameObject in aibuItem's parentTree that rotates together with renderedMesh and has somehow favorable position.
//        // rootPoint - the topMost parent of aibuItem. During animations always has horrible orientation.
//        // 
//        // The relationship is
//        //                                [                               AibuItem                                ]
//        // [controller] - [anchorPoint] - [ [rootPoint] - [something] - [movingPoint] - [something/handlerParent] ]
//        // [target     <-    rigidBody]
//        // When animation changes, [movingPoint] gets drastic offset, we find that offset in relation to [rootPoint],
//        // and move [rootPoint] in a way, that allows [movingPoint] to be aligned (with predetermined offset) with [controller].
//        // This way [movingPoint] is always in predetermined place, that we maintain constantly through LateUpdate().
//        // Although after collisions offset may occur due to rigidBody, but we restore it (responsible at the moment handler component does) once not busy.
//        //

//        public class ItemType
//        {
//            public AibuItem aibuItem;
//            public ItemHandler handler;

//            public GameObject handlerParent;
//            public Transform anchorPoint;
//            public Transform rootPoint;
//            public Transform movingPoint;
//            public Quaternion rotationOffset;
//            public Vector3 positionOffset;
//            public int layer;
//            public bool enabled;
//            public int[] availableLayers;
//            public int startLayer;

//            public Rigidbody rigidBody;
//            public Transform controller;

//            public Transform audioTransform;
//            public AudioSource audioSource;
//            public ItemType(AibuItem _aibuItem, Transform _sfxTransform, Transform _controller, AnimationParameter _animParam)
//            {
//                audioTransform = _sfxTransform;
//                audioSource = audioTransform.GetComponent<AudioSource>();

//                anchorPoint = new GameObject("VRItemMover").transform;

//                if (_aibuItem != null)
//                {
//                    aibuItem = _aibuItem;
//                    rootPoint = aibuItem.obj.transform;
//                    rootPoint.transform.SetParent(anchorPoint, false);
//                }

//                anchorPoint.SetParent(VR.Manager.transform, false);
//                rigidBody = anchorPoint.gameObject.AddComponent<Rigidbody>();
//                rigidBody.useGravity = false;
//                rigidBody.freezeRotation = true;
//                controller = _controller;

//                if (_aibuItem != null && _animParam != null)
//                {
//                    positionOffset = _animParam.positionOffset;
//                    rotationOffset = _animParam.rotationOffset;
//                    availableLayers = _animParam.availableLayers;
//                    startLayer = availableLayers[availableLayers.Length - 1];
//                    movingPoint = rootPoint.GetComponentsInChildren<Transform>()
//                        .Where(t => t.name.StartsWith(_animParam.movingPartName, StringComparison.Ordinal))
//                        .FirstOrDefault();

//                    handlerParent = rootPoint.GetComponentsInChildren<Transform>()
//                        .Where(t => t.name.StartsWith(_animParam.handlerParentName, StringComparison.Ordinal)
//                        || t.name.EndsWith(_animParam.handlerParentName, StringComparison.Ordinal))
//                        .FirstOrDefault().gameObject;
//                }
//                else
//                {
//                    handlerParent = new GameObject("HandlerParent");
//                    handlerParent.transform.SetParent(anchorPoint, worldPositionStays: false);
//                }
//            }
//        }

//        public class AnimationParameter
//        {
//            //public int layerId;
//            //public Quaternion rotation;
//            //public float rotationOffsetZ;
//            public int[] availableLayers;
//            public string movingPartName;
//            public string handlerParentName;
//            public Vector3 positionOffset;
//            public Quaternion rotationOffset;
//        }

//        public static void DestroyHandlerComponent<T>()
//            where T : ItemHandler
//        {
//            for (var i = 1; i < 3; i++)
//            {
//                foreach (var item in controllersDic[i])
//                {

//                    if (item.handler != null)
//                    {
//                        GameObject.Destroy(item.handler);
//                    }
//                }
//            }
//        }
//        public static void AddHandlerComponent<T>()
//            where T : ItemHandler
//        {
//            for (var i = 1; i < 3; i++)
//            {
//                foreach (var item in controllersDic[i])
//                {
//                    if (item.handler == null)
//                    {
//                        item.handler = item.handlerParent.AddComponent<T>();
//                    }
//                    else
//                    {
//                        VRPlugin.Logger.LogWarning($"Attempt to add already existing component {typeof(T)}");
//                    }
//                }
//            }
//        }
//        /// <param name="index">0 - headset, left - 1, right - 2</param>
//        public static ItemHandler GetActiveHandler(int index)
//        {
//            //VRPlugin.Logger.LogDebug($"Model:GetHandler:{index}");
//            return controllersDic[index][_itemIndexes[index]].handler;
//            //foreach (var item in controllersDic[index])
//            //{
//            //    if (item.enabled) return item.handler;
//            //}
//            //return null;
//        }

//        public static ItemType GetItemType(MonoBehaviour monoBehaviour)
//        {
//            for (var i = 0; i < 3; i++)
//            {
//                foreach (var item in controllersDic[i])
//                {
//                    if (item.handler == monoBehaviour)
//                    {
//                        return item;
//                    }
//                }
//            }
//            return null;
//        }
//        // Straight from HandCtrl.
//        private static void Load()
//        {
//            var textAsset = GlobalMethod.LoadAllListText("h/list/", "AibuItemObject", null);
//            GlobalMethod.GetListString(textAsset, out var array);
//            for (int i = 0; i < array.GetLength(0); i++)
//            {
//                int num = 0;
//                int num2 = 0;

//                int.TryParse(array[i, num++], out num2);

//                if (!aibuItemList.TryGetValue(num2, out var aibuItem))
//                {
//                    aibuItemList.Add(num2, new AibuItem());
//                    aibuItem = aibuItemList[num2];
//                }
//                aibuItem.SetID(num2);


//                var manifestName = array[i, num++];
//                var text2 = array[i, num++];
//                var assetName = array[i, num++];
//                aibuItem.SetObj(CommonLib.LoadAsset<GameObject>(text2, assetName, true, manifestName));
//                //this.flags.hashAssetBundle.Add(text2);
//                var text3 = array[i, num++];
//                var isSilhouetteChange = array[i, num++] == "1";
//                var flag = array[i, num++] == "1";
//                if (!text3.IsNullOrEmpty())
//                {
//                    aibuItem.objBody = aibuItem.obj.transform.FindLoop(text3);
//                    if (aibuItem.objBody)
//                    {
//                        aibuItem.renderBody = aibuItem.objBody.GetComponent<SkinnedMeshRenderer>();
//                        if (flag)
//                        {
//                            aibuItem.mHand = aibuItem.renderBody.material;

//                        }
//                    }
//                }
//                aibuItem.isSilhouetteChange = isSilhouetteChange;
//                text3 = array[i, num++];
//                if (!text3.IsNullOrEmpty())
//                {
//                    aibuItem.objSilhouette = aibuItem.obj.transform.FindLoop(text3);
//                    if (aibuItem.objSilhouette)
//                    {
//                        aibuItem.renderSilhouette = aibuItem.objSilhouette.GetComponent<SkinnedMeshRenderer>();
//                        aibuItem.mSilhouette = aibuItem.renderSilhouette.material;
//                        if (!_material)
//                            _material = aibuItem.renderSilhouette.material;
//                    }
//                }
//                int.TryParse(array[i, num++], out num2);
//                aibuItem.SetIdObj(num2);
//                int.TryParse(array[i, num++], out num2);
//                aibuItem.SetIdUse(num2);
//                if (aibuItem.obj)
//                {
//                    //EliminateScale[] componentsInChildren = aibuItem.obj.GetComponentsInChildren<EliminateScale>(true);
//                    //if (componentsInChildren != null && componentsInChildren.Length != 0)
//                    //{
//                    //    componentsInChildren[componentsInChildren.Length - 1].LoadList(aibuItem.id);
//                    //}
//                    var components = aibuItem.obj.transform.GetComponentsInChildren<EliminateScale>(true);
//                    foreach (var component in components)
//                    {
//                        //component.enabled = false;
//                        UnityEngine.Component.Destroy(component);
//                    }
//                    aibuItem.SetAnm(aibuItem.obj.GetComponent<Animator>());
//                    //aibuItem.obj.SetActive(false);
//                    //aibuItem.obj.transform.SetParent(VR.Manager.transform, false);
//                }
//                aibuItem.pathSEAsset = array[i, num++];
//                aibuItem.nameSEFile = array[i, num++];
//                aibuItem.saveID = int.Parse(array[i, num++]);
//                aibuItem.isVirgin = (array[i, num++] == "1");
//            }
//        }
//        private static readonly int[] _itemIDs = { 0, 2, 5, 7 };
//        private static void SetItems()
//        {
//            var controller = VR.Mode.Left.gameObject.transform;
//            var other = VR.Mode.Right.gameObject.transform;
//            for (var i = 0; i < 3; i++)
//            {
//                controllersDic.Add(i, new List<ItemType>());

//                var gameObj = new GameObject("SfxSource");
//                gameObj.transform.SetParent(VR.Manager.transform, false);
//                gameObj.AddComponent<AudioSource>();
//                _sfxTransforms.Add(gameObj.transform);
//            }
//            InitTongue();
//            for (var i = 0; i < _itemIDs.Length; i++)
//            {
//                InitItem(i, 0, controller);
//                InitItem(i, 1, other);
//            }
//            InitEmptyItem(0, controller);
//            InitEmptyItem(1, other);
//            AddDynamicBones();

//            //ActivateItem(controllersDic[0][0]);

//            ActivateItem(controllersDic[1][0]);
//            ActivateItem(controllersDic[2][0]);

//            VR.Mode.Right.Model.gameObject.SetActive(false);
//            VR.Mode.Left.Model.gameObject.SetActive(false);
//        }
//        private static void InitEmptyItem(int index, Transform controller)
//        {
//            index++;
//            var list = controllersDic[index];
//            list.Add(new ItemType(
//                _aibuItem: null,
//                _sfxTransform: _sfxTransforms[index],
//                _controller: controller,
//                _animParam: null
//                ));
//            var item = list[list.Count - 1];
//            item.positionOffset = new Vector3(0f, -0.02f, -0.07f);
//            SetCollider(item, -1);
//            SetItemState(item, false);
//        }
//        private static void InitItem(int i, int index, Transform controller)
//        {
//            index++;
//            controllersDic[index].Add(new ItemType(
//                _aibuItem: aibuItemList[_itemIDs[i] + index - 1],
//                _sfxTransform: _sfxTransforms[index],
//                _controller: controller,
//                _animParam: defaultAnimParamList[i]
//                ));
//            var item = controllersDic[index][i];

//            SetCollider(item, i);
//            SetItemState(item, false);
//        }
//        private static void InitTongue()
//        {
//            controllersDic[0].Add(new ItemType(
//                _aibuItem: aibuItemList[4],
//                _sfxTransform: _sfxTransforms[0],
//                _controller: VR.Camera.transform,
//                _animParam: defaultAnimParamList[4]
//                ));
//            var item = controllersDic[0][0]; 


//            SetCollider(item, 4);
//            SetItemState(item, false);
//        }
//        private static ItemType GetCurrentItem(int index) => controllersDic[index][_itemIndexes[index]];

//        //private static Transform SetState(int index, Transform parent, bool detach)
//        //{
//        //    var handlerParent = GetCurrentItem(index).handlerParent;
//        //    var collider
//        //    if (detach)
//        //    {
                
//        //    }
//        //    else
//        //    {

//        //    }
//        //}

//        private static void SetCollider(ItemType item, int i)
//        {
//            // Apparently in this unity version, collider center uses global orientation.
//            // Or perhaps built in animation is to blame?
//            // Dynamic bone uses local though.
//            if (i == -1)
//            {
//                // Non-kinematic rigidBody collider.
//                var parent = item.anchorPoint.gameObject;
//                var collider1 = parent.AddComponent<SphereCollider>();
//                collider1.radius = 0.015f;
//                Util.SpawnPrimitive(PrimitiveType.Sphere, new Vector3(0.03f, 0.03f, 0.03f), parent.transform, Color.yellow, 0.25f);

//                // Kinematic-trigger.
//                var collider2 = item.handlerParent.AddComponent<BoxCollider>();
//                collider2.size = new Vector3(0.08f, 0.05f, 0.13f);
//                collider2.isTrigger = true;
//                Util.SpawnPrimitive(PrimitiveType.Cube, new Vector3(0.05f, 0.013f, 0.08f), item.handlerParent.transform, Color.cyan, 0.25f);

//                // RigidBody for time when we detach handler and use with SetParent.
//                var rigidBody = item.handlerParent.AddComponent<Rigidbody>();
//                rigidBody.isKinematic = true;
//                // Collider for detached state. Stays disabled outside of detached state.

//                ////////////////////////////////////////////
//                //                                        //
//                //   |TEST| REPLACE FOR COLLIDER |TEST|   //
//                //                                        //
//                ////////////////////////////////////////////

//                //var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
//                //sphere.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
//                //sphere.GetComponent<Renderer>().material.color = Color.green;
//                //var sphereCollider = sphere.GetComponent<Collider>();
//                //sphereCollider.enabled = false;
//                //sphereCollider.isTrigger = true;
//                //sphere.layer = 20;
//            }
//            else if (i < 2)
//            {
//                // Hands

//                // RigidBody on anchor point.
//                var parent = item.anchorPoint.gameObject;
//                var collider1 = parent.AddComponent<SphereCollider>();
//                collider1.radius = 0.015f;
//                Util.SpawnPrimitive(PrimitiveType.Sphere, new Vector3(0.03f, 0.03f, 0.03f), parent.transform, Color.yellow, 0.25f);

//                // Trigger on moving point.
//                var collider2 = item.handlerParent.AddComponent<BoxCollider>();
//                collider2.size = new Vector3(0.08f, 0.05f, 0.13f);
//                collider2.isTrigger = true;
//                Util.SpawnPrimitive(PrimitiveType.Cube, new Vector3(0.05f, 0.013f, 0.08f), item.handlerParent.transform, Color.cyan, 0.25f);

//                //collider.size = new Vector3(0.08f, 0.02f, 0.13f);
//                // Implement Keep color TRACK!
//                item.aibuItem.SetHandColor(new Color(0.960f, 0.887f, 0.864f, 1.000f));
//                item.aibuItem.renderSilhouette.enabled = false;
//            }
//            else if (i == 2)
//            {
//                // Add 2 non kinematic jointed rigidBodies for handle and tip ?

//                // Massager
//                var parent = item.handlerParent;
//                var collider = parent.AddComponent<CapsuleCollider>();
//                collider.radius = 0.045f;
//                collider.height = 0.1f;
//                collider.isTrigger = true;
//                var rigidBody = parent.AddComponent<Rigidbody>();
//                rigidBody.isKinematic = true;
//            }
//            else if (i == 3)
//            {
//                // Vibrator
//                var parent = item.handlerParent;
//                var collider = parent.AddComponent<CapsuleCollider>();
//                collider.radius = 0.03f;
//                collider.height = 0.17f;
//                collider.direction = 1;
//                collider.isTrigger = true;
//                var rigidBody = parent.AddComponent<Rigidbody>();
//                rigidBody.isKinematic = true;
//            }
//            else // i == 4
//            {
//                var parent = item.anchorPoint.gameObject;
//                var collider1 = parent.AddComponent<CapsuleCollider>();
//                collider1.radius = 0.01f;
//                collider1.height = 0.025f;
//                collider1.direction = 2;

//                //var sphere1 = GameObject.CreatePrimitive(PrimitiveType.Capsule);
//                //sphere1.transform.localScale = new Vector3(0.01f, 0.01f, 0.05f);
//                ////sphere1.transform.localScale = new Vector3(0.08f, 0.04f, 0.13f);
//                //sphere1.transform.SetParent(item.anchorPoint, false);
//                //sphere1.GetComponent<Renderer>().material.color = Color.blue;

//                // Trigger on moving point.
//                var collider2 = item.handlerParent.AddComponent<BoxCollider>();
//                collider2.size = new Vector3(0.08f, 0.05f, 0.13f);
//                collider2.isTrigger = true;
//                item.aibuItem.renderSilhouette.enabled = false;
//            }
//        }
//        public static void SetHandColor(ChaControl chara)
//        {
//            // Different something (material, shader?) so the colors wont match from the get go.
//            var color = chara.fileBody.skinMainColor;
//            for (var i = 0; i < 4; i++)
//            {
//                aibuItemList[i].SetHandColor(color);
//            }
//        }
//        private static void ActivateItem(ItemType item)
//        {
//            item.anchorPoint.SetPositionAndRotation(item.controller.position, item.controller.rotation);
//            SetItemState(item, true);
//            if (item.rootPoint != null)
//            {
//                // Assign this one on basis of player's character scale?
//                // No clue where ChaFile hides the height.
//                item.rootPoint.localScale = Fixes.Util.Divide(Vector3.Scale(Vector3.one, item.rootPoint.localScale), item.rootPoint.lossyScale);
//                SetStartLayer(item);
//            }
//            _activeItems.Add(item);
//        }
//        private void DeactivateItem(ItemType item)
//        {
//            SetItemState(item, false);

//            //item.aibuItem.anm.SetLayerWeight(item.layer, 0f);
//            //item.layer = 0;
//            item.anchorPoint.SetParent(VR.Manager.transform, false);
//            _activeItems.Remove(item);
//        }
//        private void SetItemState(ItemType item, bool state)
//        {
//            item.enabled = state;
//            item.anchorPoint.gameObject.SetActive(state);
//        }

//        internal void OnLateUpdate()
//        {
//            foreach (var item in _activeItems)
//            {
//                HoldItem(item);
//            }
//        }
//        // RigidBody = AnchorPoint
//        internal static void OnFixedUpdate()
//        {
//            foreach (var item in _activeItems)
//            {
//                item.rigidBody.MoveRotation(item.controller.rotation);
//                //item.rigidBody.MovePosition(item.controller.position);
//                item.rigidBody.MovePosition(item.controller.TransformPoint(item.positionOffset));
//            }
//        }
//        private static void HoldItem(ItemType item)
//        {
//            // We have an animated item, attached to 'rootPoint', on animation change it rotates heavily
//            // which we compensate.
//            if (!item.movingPoint) return;
//            item.rootPoint.rotation = item.anchorPoint.rotation * item.rotationOffset * Quaternion.Inverse(item.movingPoint.rotation) * item.rootPoint.rotation;
//            //item.rootPoint.position = item.rootPoint.position + (item.anchorPoint.TransformPoint(item.positionOffset) - item.movingPoint.position);
//            //item.rootPoint.position = item.rootPoint.position + (item.anchorPoint.position - item.movingPoint.position);
//            //item.rootPoint.position += item.anchorPoint.TransformPoint(item.positionOffset) - item.movingPoint.position;
//            item.rootPoint.position += item.anchorPoint.position - item.movingPoint.position;

//            // As I understand unity changes position of child transform(overrides suggested global position from compound assignment), when we attempt to adjust it's global rotation.
//            // This renders compound PosRot assignment quite useless, as unity attempts to help us with "corrected" position, which more often then not is off.
//            // This changes however when we assign !localRotation! through it.
//            // Which is done quite straightforwardly, but yet to work on me. So we stick with a good old one.

//            //item.rootPoint.SetPositionAndRotation(
//            //    item.rootPoint.position + (item.anchorPoint.TransformPoint(item.anchorOffset) - item.movingPoint.position),
//            //    item.anchorPoint.rotation * item.rotationOffset * Quaternion.Inverse(item.movingPoint.rotation) * item.rootPoint.rotation);
//        }

//        // Due to scarcity of good hotkeys, we'll go with increase only.
//        internal static void ChangeItem(int controllerIndex)
//        {
//            controllerIndex++;
//            var currentIndex = _itemIndexes[controllerIndex];
//            var controllerItemList = controllersDic[controllerIndex];
//            var currentItem = controllerItemList[currentIndex];
//            StopSE(currentItem);
//            DeactivateItem(currentItem);
//            //if (increase)
//            //{
//            // Last one is reserved, we don't scroll it.
//            _itemIndexes[controllerIndex] = (currentIndex + 1) % (controllerItemList.Count - 1);
//            //}
//            //else
//            //{
//            //    // Last one is reserved, we don't scroll it.
//            //    if (currentIndex == 0) currentIndex = controllerItemList.Count - 1;
//            //    _currentItemIndex[controllerIndex] = currentIndex - 1;
//            //}
//            ActivateItem(controllerItemList[_itemIndexes[controllerIndex]]);
//        }

//        private static void PlaySE(ItemType item)
//        {
//            var aibuItem = item.aibuItem;
//            if (aibuItem.pathSEAsset == null) return;

//            if (aibuItem.transformSound == null)
//            {
//                var se = new Utils.Sound.Setting
//                {
//                    type = Manager.Sound.Type.GameSE3D,
//                    assetBundleName = aibuItem.pathSEAsset,
//                    assetName = aibuItem.nameSEFile,
//                };
//                aibuItem.transformSound = Utils.Sound.Play(se);
//                aibuItem.transformSound.GetComponent<AudioSource>().loop = true;
//                aibuItem.transformSound.SetParent(item.movingPoint, false);
//            }
//            else
//            {
//                aibuItem.transformSound.GetComponent<AudioSource>().Play();
//            }
//        }
//        private static void StopSE(ItemType item)
//        {
//            if (item.aibuItem == null || item.aibuItem.transformSound == null) return;
//            item.aibuItem.transformSound.GetComponent<AudioSource>().Stop();
//        }
//        public static void TestLayer(bool increase, bool skipTransition = false)
//        {
//            var item = controllersDic[0][0];

//            var anm = item.aibuItem.anm;
//            var oldLayer = item.layer;
//            var oldIndex = Array.IndexOf(item.availableLayers, oldLayer);
//            var newIndex = increase ? (oldIndex + 1) % item.availableLayers.Length : oldIndex <= 0 ? item.availableLayers.Length - 1 : oldIndex - 1;
//            var newLayer = item.availableLayers[newIndex];

//            //var newRotationOffset = newLayer == 13 || newLayer == 15 ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;// Quaternion.Euler(-90f, 0f, 0f);

//            if (skipTransition)
//            {
//                anm.SetLayerWeight(newLayer, 1f);
//                anm.SetLayerWeight(oldLayer, 0f);
//                item.layer = newLayer;
//            }
//            else
//            {
//                KoikatuInterpreter.Instance.StartCoroutine(ChangeTongueCo(item, anm, oldLayer, newLayer));
//            }
//            VRPlugin.Logger.LogDebug($"TestLayer:{newLayer}");

//        }
//        private static IEnumerator ChangeTongueCo(ItemType item, Animator anm, int oldLayer, int newLayer)
//        {
//            var timer = 0f;
//            var stop = false;
//            //var initRotOffset = item.rotationOffset;
//            while (!stop)
//            {
//                timer += Time.deltaTime * 2f;
//                if (timer > 1f)
//                {
//                    timer = 1f;
//                    stop = true;
//                }
//                //item.rotationOffset = Quaternion.Lerp(initRotOffset, newRotationOffset, timer);
//                anm.SetLayerWeight(newLayer, timer);
//                anm.SetLayerWeight(oldLayer, 1f - timer);
//                yield return null;
//            }
//            item.layer = newLayer;
//        }
//        public static void SetStartLayer(ItemType item)
//        {
//            var anm = item.aibuItem.anm;

//            anm.SetLayerWeight(item.startLayer, 1f);
//            anm.SetLayerWeight(item.layer, 0f);
//            item.layer = item.startLayer;
//        }
//        public static void ChangeLayer(int controllerIndex, bool increase, bool skipTransition = false)
//        {
//            controllerIndex++;
//            //TestLayer(increase, skipTransition);
//            var itemIndex = _itemIndexes[controllerIndex];

//            var item = controllersDic[controllerIndex][itemIndex];
//            if (item.availableLayers == null) return;
//            StopSE(item);

//            var anm = item.aibuItem.anm;
//            var oldLayer = item.layer;

//            var oldIndex = Array.IndexOf(item.availableLayers, oldLayer);
//            var newIndex = increase ? (oldIndex + 1) % item.availableLayers.Length : oldIndex <= 0 ? item.availableLayers.Length - 1 : oldIndex - 1;
//            //VRPlugin.Logger.LogDebug($"oldIndex:{oldIndex}:newIndex:{newIndex}");
//            var newLayer = item.availableLayers[newIndex];

//            //VRPlugin.Logger.LogDebug($"Model:Change:Layer:From[{oldLayer}]:To[{newLayer}]");

//            if (skipTransition)
//            {
//                anm.SetLayerWeight(newLayer, 1f);
//                anm.SetLayerWeight(oldLayer, 0f);
//                item.layer = newLayer;
//            }
//            else
//            {
//                KoikatuInterpreter.Instance.StartCoroutine(ChangeLayerCo(item, anm, oldLayer, newLayer));
//            }

//            if (newLayer != 0 && item.aibuItem.pathSEAsset != null)
//            {
//                PlaySE(item);
//            }
//        }
//        private static IEnumerator ChangeLayerCo(ItemType item, Animator anm, int oldLayer, int newLayer)
//        {
//            var timer = 0f;
//            var stop = false;
//            while (!stop)
//            {
//                timer += Time.deltaTime * 2f;
//                if (timer > 1f)
//                {
//                    timer = 1f;
//                    stop = true;
//                }
//                anm.SetLayerWeight(newLayer, timer);
//                anm.SetLayerWeight(oldLayer, 1f - timer);
//                yield return null;
//            }
//            item.layer = newLayer;
//        }
//        /// <summary>
//        /// Sets current item to an empty one and returns it's anchor.
//        /// </summary>
//        public static Transform GetEmptyAnchor(int index)
//        {
//            // Actual 0 is a headset, for convenience/clarity we use 0..1 input where headset is not applicable. 
//            // Headset is basically reserved for the tongue which is quite far on my priority list or from those methods.
//            index++;

//            var itemIndex = _itemIndexes[index];
//            var itemTypeList = controllersDic[index];
//            DeactivateItem(itemTypeList[itemIndex]);
//            itemIndex = itemTypeList.Count - 1;
//            _itemIndexes[index] = itemIndex;
//            var item = itemTypeList[itemIndex];
//            ActivateItem(item);
//            //item.rigidBody.isKinematic = false;
//            return item.anchorPoint;
//        }

//        /// <param name="press">OnButtonDown - true, OnButtonUp - false.</param>
//        public static Transform OnGrip(int index, bool press)
//        {
//            var item = GetCurrentItem(++index);
//            item.rigidBody.isKinematic = press;
//            return item.anchorPoint;
//        }
//        public static Transform GetAnchor(int index)
//        {
//            return GetCurrentItem(++index).anchorPoint;
//        }
//        private static List<string> _colliderParentListStartsWith = new List<string>()
//        {
//            "cf_j_middle02_",
//            "cf_j_index02_",
//            "cf_j_ring02_",
//            "cf_j_thumb02_",
//            "cf_s_hand_",
//        };
//        private static List<string> _colliderParentListEndsWith = new List<string>()
//        {
//            "_head_00",
//            "J_vibe_02",
//            "J_vibe_05",
//            "cf_j_tang_04"
//        };
//        private static void AddDynamicBones()
//        {
//            var gameObjectList = new List<GameObject>();
//            for (var i = 0; i < 3; i++)
//            {
//                // Last item in controllers (not headset) is fake, skip.
//                for (var j = 0; j < controllersDic[i].Count - (i == 0 ? 0 : 1); j++)
//                {
//                    var transforms = controllersDic[i][j].aibuItem.obj.GetComponentsInChildren<Transform>(includeInactive: true)
//                        .Where(t => _colliderParentListStartsWith.Any(c => t.name.StartsWith(c, StringComparison.Ordinal))
//                        || _colliderParentListEndsWith.Any(c => t.name.EndsWith(c, StringComparison.Ordinal)))
//                        .ToList();
//                    transforms?.ForEach(t => gameObjectList.Add(t.gameObject));
//                }
//                //foreach (var item in controllersDic[i])
//                //{
//                //    var transforms = item.aibuItem.obj.GetComponentsInChildren<Transform>(includeInactive: true)
//                //        .Where(t => _colliderParentListStartsWith.Any(c => t.name.StartsWith(c, StringComparison.Ordinal)) 
//                //        || _colliderParentListEndsWith.Any(c => t.name.EndsWith(c, StringComparison.Ordinal)))
//                //        .ToList();
//                //    transforms?.ForEach(t => gameObjectList.Add(t.gameObject));
//                //}
//            }
//            VRBoop.Initialize(gameObjectList);
//        }
//        public static void UpdateSkinColor(ChaFileControl chaFile)
//        {
//            var color = chaFile.custom.body.skinMainColor;
//            foreach (var item in aibuItemList.Values)
//            {
//                item.SetHandColor(color);
//            }
//        }

//        internal static void PlaySfx(ItemType item, float volume, Transform transform, Sfx sfxType, Object objectType, Intensity intensity)
//        {
//            var audioSource = item.audioSource;
//            if (audioSource.isPlaying) return;

//            var audioClipList = sfxDic[(int)sfxType][(int)objectType][(int)intensity];
//            VRPlugin.Logger.LogDebug($"AttemptToPlay:{volume}");
//            if (audioClipList.Count > 0)
//            {
//                item.audioTransform.SetParent(transform,false);
//                audioSource.volume = Mathf.Clamp01(volume);
//                audioSource.pitch = 0.9f + UnityEngine.Random.value * 0.2f;
//                //audioSource.pitch = 1f;
//                audioSource.clip = audioClipList[UnityEngine.Random.Range(0, audioClipList.Count)];
//                audioSource.Play();
//            }
//        }
//        public enum Sfx
//        {
//            Tap,
//            Slap,
//            Traverse,
//            Undress
//        }
//        public enum Object
//        {
//            Skin,
//            Cloth,
//            SkinCloth,
//            Hair,
//            Hard
//        }
//        public enum Intensity
//        {
//            // Think about:
//            //     Soft as something smallish and soft and on slower side of things, like boobs or ass.
//            //     Rough as something flattish and big and at times intense, like tummy or thighs.
//            //     Wet as.. I yet to mix something proper for it. WIP.
//            Soft,
//            Rough,
//            Wet
//        }
//        private static readonly Dictionary<int, List<List<List<AudioClip>>>> sfxDic = new Dictionary<int, List<List<List<AudioClip>>>>();
//        private static void InitDic()
//        {
//            for (var i = 0; i < Enum.GetNames(typeof(Sfx)).Length; i++)
//            {
//                sfxDic.Add(i, new List<List<List<AudioClip>>>());
//                for (var j = 0; j < Enum.GetNames(typeof(Object)).Length; j++)
//                {
//                    sfxDic[i].Add(new List<List<AudioClip>>());
//                    for (var k = 0; k < Enum.GetNames(typeof(Intensity)).Length; k++)
//                    {
//                        sfxDic[i][j].Add(new List<AudioClip>());
//                    }
//                }
//            }
//        }
//        private static void PopulateDic()
//        {
//            InitDic();
//            for (var i = 0; i < sfxDic.Count; i++)
//            {
//                for (var j = 0; j < sfxDic[i].Count; j++)
//                {
//                    for (var k = 0; k < sfxDic[i][j].Count; k++)
//                    {
//                        var directory = BepInEx.Utility.CombinePaths(new string[]
//                            {
//                                Paths.PluginPath,
//                                "SFX",
//                                ((Sfx)i).ToString(),
//                                ((Object)j).ToString(),
//                                ((Intensity)k).ToString()
//                            });
//                        if (Directory.Exists(directory))
//                        {
//                            var dirInfo = new DirectoryInfo(directory);
//                            var clipNames = new List<string>();
//                            foreach (var file in dirInfo.GetFiles("*.wav"))
//                            {
//                                clipNames.Add(file.Name);
//                            }
//                            foreach (var file in dirInfo.GetFiles("*.ogg"))
//                            {
//                                clipNames.Add(file.Name);
//                            }
//                            //sfxDic[i][j][k] = new List<AudioClip>();
//                            if (clipNames.Count == 0) continue;
//                            KoikatuInterpreter.Instance.StartCoroutine(LoadAudioFile(directory, clipNames, sfxDic[i][j][k]));
//                        }
//                    }
//                }
//            }
//        }

//        private static IEnumerator LoadAudioFile(string path, List<string> clipNames, List<AudioClip> destination)
//        {
//            foreach (var name in clipNames)
//            {
//                UnityWebRequest audioFile;
//                if (name.EndsWith(".wav"))
//                {
//                    audioFile = UnityWebRequest.GetAudioClip(Path.Combine(path, name), AudioType.WAV);
//                }
//                else
//                {
//                    audioFile = UnityWebRequest.GetAudioClip(Path.Combine(path, name), AudioType.OGGVORBIS);
//                }
//                //VRPlugin.Logger.LogDebug(Path.Combine(path, name));
//                yield return audioFile.Send();//  SendWebRequest();
//                if (audioFile.isError)
//                {
//                    VRPlugin.Logger.LogDebug(audioFile.error);
//                    VRPlugin.Logger.LogDebug(Path.Combine(path, name));
//                }
//                else
//                {
//                    var clip = DownloadHandlerAudioClip.GetContent(audioFile);
//                    clip.name = name;
//                    destination.Add(clip);
//                    VRPlugin.Logger.LogDebug($"Loaded:SFX:{name}");
//                }
//            }
//        }
//    }
//}
