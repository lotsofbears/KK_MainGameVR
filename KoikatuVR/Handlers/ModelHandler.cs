using ADV.Commands.Base;
using ADV.Commands.Chara;
using BepInEx;
using Illusion.Game;
using IllusionUtility.GetUtility;
using KK_VR.Features;
using KK_VR.Interpreters;
using Manager;
using RootMotion.FinalIK;
using SceneAssist;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngineInternal;
using VRGIN.Controls;
using VRGIN.Core;
using static HandCtrl;
using static UnityEngine.UI.Image;

namespace KK_VR.Handlers
{
    internal class ModelHandler
    {
        // There currently a bug that doesn't let every second chosen 'Finger hand" to scale.
        // Initially asset has component that does exactly this (EliminateScale), but we remove it during initialization.
        // Yet once parented every !second! time, 'Finger hand' freezes it's own local scale at Vec.one;
        // At that moment no components 'EliminateScale' are present in runtime, no clue what can it be.

        private static readonly Dictionary<int, AibuItem> aibuItemList = new Dictionary<int, AibuItem>();
        private static readonly Dictionary<int, List<ItemType>> controllersDic = new Dictionary<int, List<ItemType>>();
        private readonly int[]  _currentItemIndex = new int[3];
        private readonly List<ItemType> _currentlyActive = new List<ItemType>();
        private static readonly List<Transform> _sfxTransforms = new List<Transform>();

        internal ModelHandler()
        {
            Load();
            SetItems();
            PopulateDic();
        }

        // tongue { 7, 9
        private readonly List<AnimationParameter> defaultAnimParamList = new List<AnimationParameter>()
        {
            new AnimationParameter
            {
                // Hand
                offset = new Vector3(0f, -0.02f, -0.07f),
                availableLayers = new int[] { 4, 7, 10 },
                movingPartName = "cf_j_handroot_",
                handlerParentName = "cf_j_handroot_",

                rotationOffset = Quaternion.identity
            },
            new AnimationParameter
            {
                // Finger
                offset = new Vector3(0f, -0.02f, -0.07f),
                availableLayers = new int[] { 1, 3, 9, 4, 6 },
                movingPartName = "cf_j_handroot_",
                handlerParentName = "cf_j_handroot_",
                rotationOffset = Quaternion.identity
            },
            new AnimationParameter
            {
                // Massager
                offset = new Vector3(0f, 0f, -0.05f),
                availableLayers = new int[] { 0, 1 },
                movingPartName = "N_massajiki_",
                handlerParentName = "_head_00",
                rotationOffset = Quaternion.Euler(-90f, 180f, 0f)
            },
            new AnimationParameter
            {
                // Vibrator
                offset = new Vector3(0f, 0f, -0.1f),
                availableLayers = new int[] { 0, 1 },
                movingPartName = "N_vibe_Angle",
                handlerParentName = "J_vibe_03",
                rotationOffset = Quaternion.Euler(-90f, 180f, 0f)
            },
            new AnimationParameter
            {
                // Tongue
                /*
                 *  21, 16, 18, 19   - licking haphazardly
                 *      7 - at lower angle 
                 *      9 - at lower angle very slow
                 *      10 - at higher angle
                 *      12 - at higher angle very slow 
                 *      
                 *  13 - very high angle, flopping
                 *  
                 *  15 - very high angle, back forth
                 *  1, 3, 4, 6,   - licking 
                 */
                offset = new Vector3(0f, -0.04f, 0.05f),
                availableLayers = new int[] { 1, 7, 9, 10, 12, 13, 15, 16 },
                movingPartName = "cf_j_tang_01", // cf_j_tang_01 / cf_j_tangangle
                handlerParentName = "cf_j_tang_03",
                rotationOffset = Quaternion.identity, // Quaternion.Euler(-90f, 0f, 0f)
            }
        };

        public class ItemType
        {
            public AibuItem aibuItem;
            public MonoBehaviour handler;

            public GameObject handlerParent;
            public Transform anchorPoint;
            public Transform rootPoint;
            public Transform movingPoint;
            public Quaternion rotationOffset;
            public Vector3 positionOffset;
            public int layer;
            public bool enabled;
            public int[] availableLayers;

            public Rigidbody rigidBody;
            public Transform controller;

            public Transform audioTransform;
            public AudioSource audioSource;
        }

        class AnimationParameter
        {
            //public int layerId;
            //public Quaternion rotation;
            //public float rotationOffsetZ;
            public Vector3 offset;
            public int[] availableLayers;
            public string movingPartName;
            public string handlerParentName;
            public Quaternion rotationOffset;
        }

        public static void DestroyHandlerComponent<T>()
            where T : MonoBehaviour
        {
            for (var i = 1; i < 3; i++)
            {
                foreach (var item in controllersDic[i])
                {

                    if (item.handler != null)
                    {
                        GameObject.Destroy(item.handler);
                    }
                }
            }
        }
        public static void AddHandlerComponent<T>()
            where T : MonoBehaviour
        {
            for (var i = 1; i < 3; i++)
            {
                foreach (var item in controllersDic[i])
                {
                    if (item.handler == null)
                    {
                        item.handler = item.handlerParent.AddComponent<T>();
                    }
                    else
                    {
                        VRPlugin.Logger.LogError($"Attempt to add already existing component {typeof(T)}");
                    }
                }
            }
        }
        /// <param name="index">0 - headset, left - 1, right - 2</param>
        public static MonoBehaviour GetActiveHandler(int index)
        {
            //VRPlugin.Logger.LogDebug($"Model:GetHandler:{index}");
            foreach (var item in controllersDic[index])
            {
                if (item.enabled) return item.handler;
            }
            return null;
        }
        public static ItemType GetItem(MonoBehaviour monoBehaviour)
        {
            for (var i = 0; i < 3; i++)
            {
                foreach (var item in controllersDic[i])
                {
                    if (item.handler == monoBehaviour)
                    {
                        return item;
                    }
                }
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
                    //EliminateScale[] componentsInChildren = aibuItem.obj.GetComponentsInChildren<EliminateScale>(true);
                    //if (componentsInChildren != null && componentsInChildren.Length != 0)
                    //{
                    //    componentsInChildren[componentsInChildren.Length - 1].LoadList(aibuItem.id);
                    //}
                    var components = aibuItem.obj.transform.GetComponentsInChildren<EliminateScale>(true);
                    foreach (var component in components)
                    {
                        //component.enabled = false;
                        UnityEngine.Component.Destroy(component);
                    }
                    aibuItem.SetAnm(aibuItem.obj.GetComponent<Animator>());
                    //aibuItem.obj.SetActive(false);
                    //aibuItem.obj.transform.SetParent(VR.Manager.transform, false);
                }
                aibuItem.pathSEAsset = array[i, num++];
                aibuItem.nameSEFile = array[i, num++];
                aibuItem.saveID = int.Parse(array[i, num++]);
                aibuItem.isVirgin = (array[i, num++] == "1");
            }
        }
        private readonly int[] _itemIDs = { 0, 2, 5, 7 };
        private void SetItems()
        {
            var controller = VR.Mode.Left.gameObject.transform;
            var other = VR.Mode.Right.gameObject.transform;
            for (var i = 0; i < 3; i++)
            {
                controllersDic.Add(i, new List<ItemType>());

                var gameObj = new GameObject("SfxSource");
                gameObj.transform.SetParent(VR.Manager.transform, false);
                gameObj.AddComponent<AudioSource>();
                _sfxTransforms.Add(gameObj.transform);
            }
            InitTongue();
            for (var i = 0; i < _itemIDs.Length; i++)
            {
                InitHand(i, 0, controller);
                InitHand(i, 1, other);
            }
            AddDynamicBones();
            //ActivateItem(controllersDic[0][0]);
            ActivateItem(controllersDic[1][0]);
            ActivateItem(controllersDic[2][0]);

            ChangeLayer(controllerIndex: 1, increase: false, skipTransition: true);
            ChangeLayer(controllerIndex: 2, increase: false, skipTransition: true);

            VR.Mode.Right.Model.gameObject.SetActive(false);
            VR.Mode.Left.Model.gameObject.SetActive(false);
        }
        private void InitHand(int i, int index, Transform controller)
        {

            //aibuItemList[_itemIDs[i] + index].obj.transform.SetParent(VR.Camera.Origin, false);
            controllersDic[1 + index].Add(new ItemType());
            var item = controllersDic[1 + index][i];
            var animParam = defaultAnimParamList[i];
            item.aibuItem = aibuItemList[_itemIDs[i] + index];
            item.rootPoint = item.aibuItem.obj.transform;

            item.audioTransform = _sfxTransforms[1 + index];
            item.audioSource = item.audioTransform.GetComponent<AudioSource>();

            item.anchorPoint = new GameObject("ModelMover").transform;
            //item.offsetHolder = new GameObject("OffsetHolder").transform;
            item.rootPoint.transform.SetParent(item.anchorPoint, false);
            //item.offsetHolder.SetParent(item.anchorPoint, false);
            item.anchorPoint.SetParent(VR.Manager.transform, false);
            item.rigidBody = item.anchorPoint.gameObject.AddComponent<Rigidbody>();
            item.rigidBody.useGravity = false;
            item.rigidBody.freezeRotation = true;
            item.controller = controller;

            item.positionOffset = animParam.offset;
            item.rotationOffset = animParam.rotationOffset;
            item.availableLayers = animParam.availableLayers;
            item.movingPoint = item.rootPoint.GetComponentsInChildren<Transform>()
                .Where(t => t.name.StartsWith(animParam.movingPartName, StringComparison.Ordinal))
                .FirstOrDefault();

            // A part to which we attach collider.
            item.handlerParent = item.rootPoint.GetComponentsInChildren<Transform>()
                .Where(t => t.name.StartsWith(animParam.handlerParentName, StringComparison.Ordinal)
                || t.name.EndsWith(animParam.handlerParentName, StringComparison.Ordinal))
                .FirstOrDefault().gameObject;

            SetCollider(item, i);
            SetItemState(item, false);
        }
        private void InitTongue()
        {
            controllersDic[0].Add(new ItemType());
            var item = controllersDic[0][0];
            var animParam = defaultAnimParamList[4];
            item.aibuItem = aibuItemList[4];
            item.rootPoint = item.aibuItem.obj.transform;

            item.audioTransform = _sfxTransforms[0];
            item.audioSource = item.audioTransform.GetComponent<AudioSource>();

            VRPlugin.Logger.LogDebug($"InitTongue:1");
            item.anchorPoint = new GameObject("ModelMover").transform;
            item.rootPoint.transform.SetParent(item.anchorPoint, false);
            item.anchorPoint.SetParent(VR.Manager.transform, false);

            VRPlugin.Logger.LogDebug($"InitTongue:2");
            item.rigidBody = item.anchorPoint.gameObject.AddComponent<Rigidbody>();
            item.rigidBody.useGravity = false;
            item.rigidBody.freezeRotation = true;
            item.controller = VR.Camera.transform;

            item.positionOffset = animParam.offset;
            item.rotationOffset = animParam.rotationOffset;
            item.availableLayers = animParam.availableLayers;
            item.movingPoint = item.rootPoint.GetComponentsInChildren<Transform>()
                .Where(t => t.name.StartsWith(animParam.movingPartName, StringComparison.Ordinal))
                .FirstOrDefault();

            item.handlerParent = item.rootPoint.GetComponentsInChildren<Transform>()
                .Where(t => t.name.StartsWith(animParam.handlerParentName, StringComparison.Ordinal)
                || t.name.EndsWith(animParam.handlerParentName, StringComparison.Ordinal))
                .FirstOrDefault().gameObject;
            item.aibuItem.renderSilhouette.enabled = false;

            SetCollider(item, 4);
            SetItemState(item, false);
        }
        private void SetTongueCollider()
        {

        }
        private void SetCollider(ItemType item, int i)
        {
            // Apparently in this unity version, collider center uses global orientation.
            // Or perhaps built in animation is to blame?
            // Dynamic bone though uses local.

            if (i < 2)
            {
                // Hands

                // RigidBody on anchor point.
                var parent = item.anchorPoint.gameObject;
                var collider1 = parent.AddComponent<SphereCollider>();
                collider1.radius = 0.015f;

                // Trigger on moving point.
                var collider2 = item.handlerParent.AddComponent<BoxCollider>();
                collider2.size = new Vector3(0.08f, 0.05f, 0.13f);
                collider2.isTrigger = true;

                //var sphere1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                //sphere1.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);
                ////sphere1.transform.localScale = new Vector3(0.08f, 0.04f, 0.13f);
                //sphere1.transform.SetParent(item.anchorPoint, false);
                //sphere1.GetComponent<Renderer>().material.color = Color.blue;

                //collider.size = new Vector3(0.08f, 0.02f, 0.13f);
                // Implement Keep color TRACK!
                item.aibuItem.SetHandColor(new Color(0.960f, 0.887f, 0.864f, 1.000f));
                item.aibuItem.renderSilhouette.enabled = false;
            }
            else if (i == 2)
            {
                // Add 2 non kinematic jointed rigidBodies for handle and tip ?

                // Massager
                var parent = item.handlerParent;
                var collider = parent.AddComponent<CapsuleCollider>();
                collider.radius = 0.045f;
                collider.height = 0.1f;
                collider.isTrigger = true;
                var rigidBody = parent.AddComponent<Rigidbody>();
                rigidBody.isKinematic = true;
            }
            else if (i == 3)
            {
                // Vibrator
                var parent = item.handlerParent;
                var collider = parent.AddComponent<CapsuleCollider>();
                collider.radius = 0.03f;
                collider.height = 0.17f;
                collider.direction = 1;
                collider.isTrigger = true;
                var rigidBody = parent.AddComponent<Rigidbody>();
                rigidBody.isKinematic = true;
            }
            else // i == 4
            {
                var parent = item.anchorPoint.gameObject;
                var collider1 = parent.AddComponent<CapsuleCollider>();
                collider1.radius = 0.01f;
                collider1.height = 0.025f;
                collider1.direction = 2;

                //var sphere1 = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                //sphere1.transform.localScale = new Vector3(0.01f, 0.01f, 0.05f);
                ////sphere1.transform.localScale = new Vector3(0.08f, 0.04f, 0.13f);
                //sphere1.transform.SetParent(item.anchorPoint, false);
                //sphere1.GetComponent<Renderer>().material.color = Color.blue;

                // Trigger on moving point.
                var collider2 = item.handlerParent.AddComponent<BoxCollider>();
                collider2.size = new Vector3(0.08f, 0.05f, 0.13f);
                collider2.isTrigger = true;
            }
        }
        public static void SetHandColor(ChaControl chara)
        {
            // Different something (material, shader?) so the colors wont match from the get go.
            var color = chara.fileBody.skinMainColor;
            for (var i = 0; i < 4; i++)
            {
                aibuItemList[i].SetHandColor(color);
            }
        }
        private void ActivateItem(ItemType item)
        {
            item.anchorPoint.SetPositionAndRotation(item.controller.position, item.controller.rotation);
            //item.offsetHolder.localPosition = item.positionOffset;
            //item.rootPoint.localPosition = item.positionOffset; 
            SetItemState(item, true);

            // Assign this one on basis of player's character scale?
            // No clue where ChaFile hides the height.
            item.rootPoint.localScale = Divide(Vector3.Scale(Vector3.one, item.rootPoint.localScale), item.rootPoint.lossyScale);

            _currentlyActive.Add(item);
        }
        public static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        private void DeactivateItem(ItemType item)
        {
            SetItemState(item, false);

            item.aibuItem.anm.SetLayerWeight(item.layer, 0f);
            item.layer = 0;
            item.anchorPoint.SetParent(VR.Manager.transform, false);
            //item.anchorPoint.localPosition = Vector3.zero;
            _currentlyActive.Remove(item);
        }
        private void SetItemState(ItemType item, bool state)
        {
            item.enabled = state;
            item.anchorPoint.gameObject.SetActive(state);
        }

        internal void OnLateUpdate()
        {
            foreach (var item in _currentlyActive)
            {
                HoldItem(item);
            }
        }
        // RigidBody = AnchorPoint
        internal void OnFixedUpdate()
        {
            foreach (var item in _currentlyActive)
            {
                item.rigidBody.MoveRotation(item.controller.rotation);
                //item.rigidBody.MovePosition(item.controller.position);
                item.rigidBody.MovePosition(item.controller.TransformPoint(item.positionOffset));
            }
        }
        private Vector3 _testVec = new Vector3(0f, -0.01f, 0f);
        private void HoldItem(ItemType item)
        {
            // We have an animated item, attached to 'rootPoint', on animation change it rotates heavily
            // which we compensate.

            item.rootPoint.rotation = item.anchorPoint.rotation * item.rotationOffset * Quaternion.Inverse(item.movingPoint.rotation) * item.rootPoint.rotation;
            //item.rootPoint.position = item.rootPoint.position + (item.anchorPoint.TransformPoint(item.positionOffset) - item.movingPoint.position);
            //item.rootPoint.position = item.rootPoint.position + (item.anchorPoint.position - item.movingPoint.position);
            item.rootPoint.position += item.anchorPoint.TransformPoint(_testVec) - item.movingPoint.position;

            // As I understand unity changes position of child transform(overrides suggested global position from compound assignment), when we attempt to adjust it's global rotation.
            // This renders compound PosRot assignment quite useless, as unity attempts to help us with "corrected" position, which more often then not is off.
            // This changes however when we assign !localRotation! through it.
            // Which is done quite straightforwardly, but yet to work on me. So we stick with a good old one.

            //item.rootPoint.SetPositionAndRotation(
            //    item.rootPoint.position + (item.anchorPoint.TransformPoint(item.anchorOffset) - item.movingPoint.position),
            //    item.anchorPoint.rotation * item.rotationOffset * Quaternion.Inverse(item.movingPoint.rotation) * item.rootPoint.rotation);
        }

        // Due to scarcity of good hotkeys, we'll go with increase only.
        internal void ChangeItem(int controllerIndex, bool increase)
        {
            var index = _currentItemIndex[controllerIndex];
            var controller = controllersDic[controllerIndex];
            StopSE(controller[index]);
            DeactivateItem(controller[index]);
            if (increase)
            {
                _currentItemIndex[controllerIndex] = (index + 1) % controller.Count;
            }
            else
            {
                if (index == 0) index = controller.Count;
                _currentItemIndex[controllerIndex] = index - 1;
            }
            ActivateItem(controller[_currentItemIndex[controllerIndex]]);
        }

        private void PlaySE(ItemType item)
        {
            var aibuItem = item.aibuItem;
            if (aibuItem.pathSEAsset == null) return;

            if (aibuItem.transformSound == null)
            {
                var se = new Utils.Sound.Setting
                {
                    type = Manager.Sound.Type.GameSE3D,
                    assetBundleName = aibuItem.pathSEAsset,
                    assetName = aibuItem.nameSEFile,
                };
                aibuItem.transformSound = Utils.Sound.Play(se);
                aibuItem.transformSound.GetComponent<AudioSource>().loop = true;
                aibuItem.transformSound.SetParent(item.movingPoint, false);
            }
            else
            {
                aibuItem.transformSound.GetComponent<AudioSource>().Play();
            }
        }
        private void StopSE(ItemType item)
        {
            var aibuItem = item.aibuItem;
            if (aibuItem.transformSound == null) return;
            aibuItem.transformSound.GetComponent <AudioSource>().Stop();
        }
        public void TestLayer(bool increase, bool skipTransition = false)
        {
            var item = controllersDic[0][0];

            var anm = item.aibuItem.anm;
            var oldLayer = item.layer;
            var oldIndex = Array.IndexOf(item.availableLayers, oldLayer);
            var newIndex = increase ? (oldIndex + 1) % item.availableLayers.Length : oldIndex <= 0 ? item.availableLayers.Length - 1 : oldIndex - 1;
            var newLayer = item.availableLayers[newIndex];

            //var newRotationOffset = newLayer == 13 || newLayer == 15 ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;// Quaternion.Euler(-90f, 0f, 0f);

            if (skipTransition)
            {
                anm.SetLayerWeight(newLayer, 1f);
                anm.SetLayerWeight(oldLayer, 0f);
                item.layer = newLayer;
            }
            else
            {
                KoikatuInterpreter.Instance.StartCoroutine(ChangeTongueCo(item, anm, oldLayer, newLayer));
            }
            VRPlugin.Logger.LogDebug($"TestLayer:{newLayer}");

        }
        private IEnumerator ChangeTongueCo(ItemType item, Animator anm, int oldLayer, int newLayer)
        {
            var timer = 0f;
            var stop = false;
            //var initRotOffset = item.rotationOffset;
            while (!stop)
            {
                timer += Time.deltaTime * 2f;
                if (timer > 1f)
                {
                    timer = 1f;
                    stop = true;
                }
                //item.rotationOffset = Quaternion.Lerp(initRotOffset, newRotationOffset, timer);
                anm.SetLayerWeight(newLayer, timer);
                anm.SetLayerWeight(oldLayer, 1f - timer);
                yield return null;
            }
            item.layer = newLayer;
        }
        public void ChangeLayer(int controllerIndex, bool increase, bool skipTransition = false)
        {
            //TestLayer(increase, skipTransition);
            var itemIndex = _currentItemIndex[controllerIndex];

            var item = controllersDic[controllerIndex][itemIndex];
            StopSE(item);

            var anm = item.aibuItem.anm;
            var oldLayer = item.layer;

            var oldIndex = Array.IndexOf(item.availableLayers, oldLayer);
            var newIndex = increase ? (oldIndex + 1) % item.availableLayers.Length : oldIndex <= 0 ? item.availableLayers.Length - 1 : oldIndex - 1;
            //VRPlugin.Logger.LogDebug($"oldIndex:{oldIndex}:newIndex:{newIndex}");
            var newLayer = item.availableLayers[newIndex];

            //VRPlugin.Logger.LogDebug($"Model:Change:Layer:From[{oldLayer}]:To[{newLayer}]");

            if (skipTransition)
            {
                anm.SetLayerWeight(newLayer, 1f);
                anm.SetLayerWeight(oldLayer, 0f);
                item.layer = newLayer;
            }
            else
            {
                KoikatuInterpreter.Instance.StartCoroutine(ChangeLayerCo(item, anm, oldLayer, newLayer));
            }

            if (newLayer != 0 && item.aibuItem.pathSEAsset != null)
            {
                PlaySE(item);
            }
        }
        private IEnumerator ChangeLayerCo(ItemType item, Animator anm, int oldLayer, int newLayer)
        {
            var timer = 0f;
            var stop = false;
            while (!stop)
            {
                timer += Time.deltaTime * 2f;
                if (timer > 1f)
                {
                    timer = 1f;
                    stop = true;
                }
                anm.SetLayerWeight(newLayer, timer);
                anm.SetLayerWeight(oldLayer, 1f - timer);
                yield return null;
            }
            item.layer = newLayer;
        }
        private List<string> _colliderParentListStartsWith = new List<string>()
        {
            "cf_j_middle02_",
            "cf_j_index02_",
            "cf_j_ring02_",
            "cf_j_thumb02_",
            "cf_s_hand_",
        };
        private List<string> _colliderParentListEndsWith = new List<string>()
        {
            "_head_00",
            "J_vibe_02",
            "J_vibe_05",
            "cf_j_tang_04"
        };
        private void AddDynamicBones()
        {
            var gameObjectList = new List<GameObject>();
            for (var i = 0; i < 3; i++)
            {
                foreach (var item in controllersDic[i])
                {
                    var transforms = item.aibuItem.obj.GetComponentsInChildren<Transform>(includeInactive: true)
                        .Where(t => _colliderParentListStartsWith.Any(c => t.name.StartsWith(c, StringComparison.Ordinal)) 
                        || _colliderParentListEndsWith.Any(c => t.name.EndsWith(c, StringComparison.Ordinal)))
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

        internal static void PlaySfx(ItemType item, float volume, Transform transform, Sfx sfxType, Object objectType, Intensity intensity)
        {
            var audioSource = item.audioSource;
            if (audioSource.isPlaying) return;

            var audioClipList = sfxDic[(int)sfxType][(int)objectType][(int)intensity];
            VRPlugin.Logger.LogDebug($"AttemptToPlay:{volume}");
            if (audioClipList.Count > 0)
            {
                item.audioTransform.SetParent(transform,false);
                audioSource.volume = Mathf.Clamp01(volume);
                audioSource.pitch = 0.9f + UnityEngine.Random.value * 0.2f;
                //audioSource.pitch = 1f;
                audioSource.clip = audioClipList[UnityEngine.Random.Range(0, audioClipList.Count)];
                audioSource.Play();
            }
        }
        public enum Sfx
        {
            Tap,
            Slap,
            Traverse,
            Undress
        }
        public enum Object
        {
            Skin,
            Cloth,
            SkinCloth,
            Hair,
            Hard
        }
        public enum Intensity
        {
            // Think about:
            //     Soft as something smallish and soft and on slower side of things, like boobs or ass.
            //     Rough as something flattish and big and at times intense, like tummy or thighs.
            //     Wet as.. I yet to mix something proper for it. WIP.
            Soft,
            Rough,
            Wet
        }
        private static readonly Dictionary<int, List<List<List<AudioClip>>>> sfxDic = new Dictionary<int, List<List<List<AudioClip>>>>();
        private void InitDic()
        {
            for (var i = 0; i < Enum.GetNames(typeof(Sfx)).Length; i++)
            {
                sfxDic.Add(i, new List<List<List<AudioClip>>>());
                for (var j = 0; j < Enum.GetNames(typeof(Object)).Length; j++)
                {
                    sfxDic[i].Add(new List<List<AudioClip>>());
                    for (var k = 0; k < Enum.GetNames(typeof(Intensity)).Length; k++)
                    {
                        sfxDic[i][j].Add(new List<AudioClip>());
                    }
                }
            }
        }
        private void PopulateDic()
        {
            InitDic();
            for (var i = 0; i < sfxDic.Count; i++)
            {
                for (var j = 0; j < sfxDic[i].Count; j++)
                {
                    for (var k = 0; k < sfxDic[i][j].Count; k++)
                    {
                        var directory = BepInEx.Utility.CombinePaths(new string[]
                            {
                                Paths.PluginPath,
                                "SFX",
                                ((Sfx)i).ToString(),
                                ((Object)j).ToString(),
                                ((Intensity)k).ToString()
                            });
                        if (Directory.Exists(directory))
                        {
                            var dirInfo = new DirectoryInfo(directory);
                            var clipNames = new List<string>();
                            foreach (var file in dirInfo.GetFiles("*.wav"))
                            {
                                clipNames.Add(file.Name);
                            }
                            foreach (var file in dirInfo.GetFiles("*.ogg"))
                            {
                                clipNames.Add(file.Name);
                            }
                            //sfxDic[i][j][k] = new List<AudioClip>();
                            if (clipNames.Count == 0) continue;
                            KoikatuInterpreter.Instance.StartCoroutine(LoadAudioFile(directory, clipNames, sfxDic[i][j][k]));
                        }
                    }
                }
            }
        }

        private static IEnumerator LoadAudioFile(string path, List<string> clipNames, List<AudioClip> destination)
        {
            foreach (var name in clipNames)
            {
                UnityWebRequest audioFile;
                if (name.EndsWith(".wav"))
                {
                    audioFile = UnityWebRequest.GetAudioClip(Path.Combine(path, name), AudioType.WAV);
                }
                else
                {
                    audioFile = UnityWebRequest.GetAudioClip(Path.Combine(path, name), AudioType.OGGVORBIS);
                }
                //VRPlugin.Logger.LogDebug(Path.Combine(path, name));
                yield return audioFile.Send();//  SendWebRequest();
                if (audioFile.isError)
                {
                    VRPlugin.Logger.LogDebug(audioFile.error);
                    VRPlugin.Logger.LogDebug(Path.Combine(path, name));
                }
                else
                {
                    var clip = DownloadHandlerAudioClip.GetContent(audioFile);
                    clip.name = name;
                    destination.Add(clip);
                    VRPlugin.Logger.LogDebug($"Loaded:SFX:{name}");
                }
            }
        }
    }
}
