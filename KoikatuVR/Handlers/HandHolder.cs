using BepInEx;
using KK_VR.Features;
using KK_VR.Fixes;
using KK_VR.Interpreters;
using ADV.Commands.Object;
using Illusion.Game;
using IllusionUtility.GetUtility;
using SceneAssist;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using VRGIN.Core;
using static HandCtrl;
using static RootMotion.FinalIK.RagdollUtility;
using KK_VR.Interactors;
using VRGIN.Controls;
using ADV.Commands.Base;
using KK_VR.Controls;
using RootMotion.FinalIK;

namespace KK_VR.Handlers
{
    // We adapt animated aibu items as controller models. To see why we do this in SUCH a roundabout way
    // grab default disabled ones in HScene and scroll through their animation layers,
    // their orientations are outright horrible for our purposes.
    internal class HandHolder : Holder
    {
        // There currently a bug that doesn't let every second chosen 'Finger hand" to scale.
        // Initially asset has component that does exactly this (EliminateScale), but we remove it during initialization.
        // Yet once parented every !second! time, 'Finger hand' freezes it's own local scale at Vec.one;
        // At that moment no components 'EliminateScale' are present in runtime, no clue what can it be.
        private static readonly List<HandHolder> _instances = [];
        //private static readonly Dictionary<int, AibuItem> _loadedAssetsList = [];
        private readonly List<ItemType> _itemList = [];
        //private ItemType _activeItem;
        //private Transform _controller;
        //private Transform _anchor;
        //private Rigidbody _rigidBody;
       // private AudioSource _audioSource;
        private Transform _controller;
        //private readonly Vector3[] _prevPositions = new Vector3[20];
        //private readonly Quaternion[] _prevRotations = new Quaternion[20];
        //private readonly float[] _frameCoefs = new float[19];
        //private readonly float _avgCoef = 1f / 20f;
        //private int _currentStep;
        //private bool _lag;
        private ItemLag _itemLag;
        private bool _parent;
        private HandNoise _handNoise;
        internal HandNoise Noise => _handNoise;
        internal Controller Controller { get; private set; }
        internal int Index { get; private set; }
        //internal static Material Material { get; private set; }
        //internal AudioSource AudioSource => _audioSource;
        internal ItemHandler Handler => _activeItem.handler;
        internal GraspController Grasp { get; private set; }
        //internal Transform Anchor => _anchor;
        internal SchoolTool Tool { get; private set; }
        internal static List<HandHolder> GetHands() => _instances;
        internal void Init(int index, GameObject gameObject)
        {
            _instances.Add(this);
            Index = index;
            Controller = index == 0 ? VR.Mode.Left : VR.Mode.Right;
            Tool = Controller.GetComponent<SchoolTool>();
            _controller = Controller.transform;
            if (_loadedAssetsList.Count == 0)
            {
                LoadAssets();
                HandNoise.Init();
            }
            SetItems(index, gameObject);
            Grasp = new GraspController(this);
            _handNoise = new HandNoise(gameObject.AddComponent<AudioSource>());
        }

        //private readonly List<AnimationParameter> defaultAnimParamList =
        //[
        //    new AnimationParameter
        //    {
        //        // Hand
        //        availableLayers = [4, 7, 10],
        //        startLayer = 10,
        //        movingPartName = "cf_j_handroot_",
        //        handlerParentName = "cf_j_handroot_",
        //        positionOffset = new Vector3(0f, -0.02f, -0.07f),
        //        rotationOffset = Quaternion.identity
        //    },
        //    new AnimationParameter
        //    {
        //        // Finger
        //        availableLayers = [1, 3, 9, 4, 6],
        //        startLayer = 9,
        //        movingPartName = "cf_j_handroot_",
        //        handlerParentName = "cf_j_handroot_",
        //        positionOffset = new Vector3(0f, -0.02f, -0.07f),
        //        rotationOffset = Quaternion.identity
        //    },
        //    new AnimationParameter
        //    {
        //        // Massager
        //        availableLayers = [0, 1],
        //        startLayer = 0,
        //        movingPartName = "N_massajiki_",
        //        handlerParentName = "_head_00",
        //        positionOffset = new Vector3(0f, 0f, -0.05f),
        //        rotationOffset = Quaternion.Euler(-90f, 180f, 0f)
        //    },
        //    new AnimationParameter
        //    {
        //        // Vibrator
        //        availableLayers = [0, 1],
        //        startLayer = 0,
        //        movingPartName = "N_vibe_Angle",
        //        handlerParentName = "J_vibe_03",
        //        positionOffset = new Vector3(0f, 0f, -0.1f),
        //        rotationOffset = Quaternion.Euler(-90f, 180f, 0f)
        //    },
        //    new AnimationParameter
        //    {
        //        // Tongue
        //        /*
        //         *  21, 16, 18, 19   - licking haphazardly
        //         *      7 - at lower angle 
        //         *      9 - at lower angle very slow
        //         *      10 - at higher angle
        //         *      12 - at higher angle very slow 
        //         *      
        //         *  13 - very high angle, flopping
        //         *  
        //         *  15 - very high angle, back forth
        //         *  1, 3, 4, 6,   - licking 
        //         */
        //        availableLayers = [1, 7, 9, 10, 12, 13, 15, 16],
        //        movingPartName = "cf_j_tang_01", // cf_j_tang_01 / cf_j_tangangle
        //        handlerParentName = "cf_j_tang_03",
        //        positionOffset = new Vector3(0f, -0.04f, 0.05f),
        //        rotationOffset = Quaternion.identity, // Quaternion.Euler(-90f, 0f, 0f)
        //    }
        //];

        //
        // handlerParent - a gameObject with kinematic-collider-trigger and our component to control it.
        // anchorPoint - an extra gameObject with non-kinematic-collider to which we attach aibuItem (or empty) and then move together with controller.
        // movingPoint - a gameObject in aibuItem's parentTree that rotates together with renderedMesh and has somehow favorable position.
        // rootPoint - the topMost parent of aibuItem. During animations always has horrible orientation.
        // 
        // The relationship is
        //                                  [                                AibuItem                                  ]
        // [controller] <- [anchorPoint] <- [ [rootPoint] <- [something] <- [movingPoint] <- [something/handlerParent] ]
        // [target]     <-   [rigidBody]
        // When animation changes, [movingPoint] gets drastic offset, we find that offset in relation to [rootPoint],
        // and move [rootPoint] in a way, that allows [movingPoint] to be aligned (with predetermined offset) with [controller].
        // This way [movingPoint] is always in predetermined place, that we maintain constantly through LateUpdate().
        // Although after collisions offset may occur due to rigidBody, but we restore it (responsible at the moment handler component does) once not busy.
        //
        // _activeItem.rootPoint.rotation = _anchor.rotation * _activeItem.rotationOffset * Quaternion.Inverse(_activeItem.movingPoint.rotation)* _activeItem.rootPoint.rotation;
        //internal class ItemType
        //{
        //    internal AibuItem aibuItem;
        //    internal ItemHandler handler;
        //    internal GameObject handlerParent;
        //    internal Transform rootPoint;
        //    internal Transform movingPoint;
        //    internal Quaternion rotationOffset;
        //    internal Vector3 positionOffset;
        //    internal int layer;
        //    internal int[] availableLayers;
        //    internal int startLayer;

        //    internal ItemType(AibuItem asset, AnimationParameter animParam)
        //    {
        //        aibuItem = asset;
        //        rootPoint = asset.obj.transform;
        //        rootPoint.transform.SetParent(VR.Manager.transform, false);
        //        positionOffset = animParam.positionOffset;
        //        rotationOffset = animParam.rotationOffset;
        //        availableLayers = animParam.availableLayers;
        //        startLayer = animParam.startLayer;
        //        movingPoint = rootPoint.GetComponentsInChildren<Transform>()
        //            .Where(t => t.name.StartsWith(animParam.movingPartName, StringComparison.Ordinal))
        //            .FirstOrDefault();
        //        handlerParent = rootPoint.GetComponentsInChildren<Transform>()
        //            .Where(t => t.name.StartsWith(animParam.handlerParentName, StringComparison.Ordinal)
        //            || t.name.EndsWith(animParam.handlerParentName, StringComparison.Ordinal))
        //            .FirstOrDefault().gameObject;
                
        //    }
        //    internal ItemType()
        //    {
        //        positionOffset = new Vector3(0f, -0.02f, -0.07f);
        //        handlerParent = new GameObject("EmptyItem");
        //        handlerParent.transform.SetParent(VR.Manager.transform, worldPositionStays: false);
        //        rootPoint = handlerParent.transform;
        //        handlerParent.SetActive(false);
        //    }
        //}

        //internal class AnimationParameter
        //{
        //    internal int[] availableLayers;
        //    internal int startLayer;
        //    internal string movingPartName;
        //    internal string handlerParentName;
        //    internal Vector3 positionOffset;
        //    internal Quaternion rotationOffset;
        //}

        internal void UpdateHandlers<T>()
            where T : ItemHandler
        {
            if (_activeItem.handler == null || _activeItem.handler.GetType() != typeof(T))
            {
                foreach (var item in _itemList)
                {
                    if (item != null)
                        GameObject.Destroy(item.handler);

                    item.handler = item.handlerParent.AddComponent<T>();
                    item.handler.Init(this, _rigidBody);
                }
            }
        }
        internal void DestroyHandlers()
        {
            foreach (var item in _itemList)
            {
                if (item != null)
                    GameObject.Destroy(item.handler);
            }
        }
        //private static void LoadAssets()
        //{
        //    // Straight from HandCtrl.
        //    var textAsset = GlobalMethod.LoadAllListText("h/list/", "AibuItemObject", null);
        //    GlobalMethod.GetListString(textAsset, out var array);
        //    for (int i = 0; i < array.GetLength(0); i++)
        //    {
        //        int num = 0;
        //        int num2 = 0;

        //        int.TryParse(array[i, num++], out num2);

        //        if (!_loadedAssetsList.TryGetValue(num2, out var aibuItem))
        //        {
        //            _loadedAssetsList.Add(num2, new AibuItem());
        //            aibuItem = _loadedAssetsList[num2];
        //        }
        //        aibuItem.SetID(num2);


        //        var manifestName = array[i, num++];
        //        var text2 = array[i, num++];
        //        var assetName = array[i, num++];
        //        aibuItem.SetObj(CommonLib.LoadAsset<GameObject>(text2, assetName, true, manifestName));
        //        //this.flags.hashAssetBundle.Add(text2);
        //        var text3 = array[i, num++];
        //        var isSilhouetteChange = array[i, num++] == "1";
        //        var flag = array[i, num++] == "1";
        //        if (!text3.IsNullOrEmpty())
        //        {
        //            aibuItem.objBody = aibuItem.obj.transform.FindLoop(text3);
        //            if (aibuItem.objBody)
        //            {
        //                aibuItem.renderBody = aibuItem.objBody.GetComponent<SkinnedMeshRenderer>();
        //                if (flag)
        //                {
        //                    aibuItem.mHand = aibuItem.renderBody.material;

        //                }
        //            }
        //        }
        //        aibuItem.isSilhouetteChange = isSilhouetteChange;
        //        text3 = array[i, num++];
        //        if (!text3.IsNullOrEmpty())
        //        {
        //            aibuItem.objSilhouette = aibuItem.obj.transform.FindLoop(text3);
        //            if (aibuItem.objSilhouette)
        //            {
        //                aibuItem.renderSilhouette = aibuItem.objSilhouette.GetComponent<SkinnedMeshRenderer>();
        //                aibuItem.mSilhouette = aibuItem.renderSilhouette.material;
        //                aibuItem.renderSilhouette.enabled = false;  
        //                aibuItem.SetHandColor(new Color(0.960f, 0.887f, 0.864f, 1.000f));
        //                if (!Material)
        //                    Material = aibuItem.renderSilhouette.material;
        //            }
        //        }
        //        int.TryParse(array[i, num++], out num2);
        //        aibuItem.SetIdObj(num2);
        //        int.TryParse(array[i, num++], out num2);
        //        aibuItem.SetIdUse(num2);
        //        if (aibuItem.obj)
        //        {
        //            //EliminateScale[] componentsInChildren = aibuItem.obj.GetComponentsInChildren<EliminateScale>(true);
        //            //if (componentsInChildren != null && componentsInChildren.Length != 0)
        //            //{
        //            //    componentsInChildren[componentsInChildren.Length - 1].LoadList(aibuItem.id);
        //            //}
        //            var components = aibuItem.obj.transform.GetComponentsInChildren<EliminateScale>(true);
        //            foreach (var component in components)
        //            {
        //                //component.enabled = false;
        //                UnityEngine.Component.Destroy(component);
        //            }
        //            aibuItem.SetAnm(aibuItem.obj.GetComponent<Animator>());
        //            //aibuItem.obj.SetActive(false);
        //            //aibuItem.obj.transform.SetParent(VR.Manager.transform, false);
        //        }
        //        aibuItem.pathSEAsset = array[i, num++];
        //        aibuItem.nameSEFile = array[i, num++];
        //        aibuItem.saveID = int.Parse(array[i, num++]);
        //        aibuItem.isVirgin = (array[i, num++] == "1");
        //        aibuItem.obj.SetActive(false);
        //    }
        //}
        private readonly int[] _itemIDs = [0, 2, 5, 7];
        private void SetItems(int index, GameObject gameObject)
        {
            _anchor = gameObject.transform;
            _anchor.SetParent(VR.Manager.transform, false);
            _rigidBody = _anchor.gameObject.AddComponent<Rigidbody>();
            _rigidBody.useGravity = false;
            _rigidBody.freezeRotation = true;


            for (var i = 0; i < 4; i++)
            {
                InitItem(i, index);
            }
            InitEmptyItem();

            _activeItem = _itemList[0];
            AddDynamicBones();
            ActivateItem();
            Controller.Model.gameObject.SetActive(false);
        }
        private void InitItem(int i, int index)
        {
            var item = new ItemType(
                asset: _loadedAssetsList[_itemIDs[i] + index],
                animParam: _defaultAnimParamList[i]
                );
            _itemList.Add(item);
            SetCollider(item, i);
        }
        private void InitEmptyItem()
        {
            var item = new ItemType();
            _itemList.Add(item);
            SetCollider(item, -1);
        }
        //private static void InitTongue()
        //{
        //    controllersDic[0].Add(new ItemType(
        //        _aibuItem: aibuItemList[4],
        //        _sfxTransform: _sfxTransforms[0],
        //        _controller: VR.Camera.transform,
        //        _animParam: defaultAnimParamList[4]
        //        ));
        //    var item = controllersDic[0][0];


        //    SetCollider(item, 4);
        //    SetItemState(item, false);
        //}

        //private static Transform SetState(int index, Transform parent, bool detach)
        //{
        //    var handlerParent = GetCurrentItem(index).handlerParent;
        //    var collider
        //    if (detach)
        //    {

        //    }
        //    else
        //    {

        //    }
        //}

        private void SetCollider(ItemType item, int i)
        {
            // Apparently in this unity version, collider center uses global orientation.
            // Or perhaps built in animation is to blame?
            // Dynamic bone uses local though.
            var debug = KoikatuInterpreter.settings.DebugShowIK;
            if (i == -1)
            {
                // Non-kinematic rigidBody-mover.
                var collider1 = _anchor.gameObject.AddComponent<SphereCollider>();
                collider1.radius = 0.015f;
                if (debug)
                {
                    Util.CreatePrimitive(PrimitiveType.Sphere, new Vector3(0.03f, 0.03f, 0.03f), _anchor.transform, Color.yellow);
                }

                // Kinematic-trigger.
                var collider2 = item.handlerParent.AddComponent<BoxCollider>();
                collider2.size = new Vector3(0.05f, 0.04f, 0.13f);
                collider2.isTrigger = true;
                if (debug)
                {
                    Util.CreatePrimitive(PrimitiveType.Cube, new Vector3(0.05f, 0.04f, 0.13f), item.handlerParent.transform, Color.cyan, 0.25f);
                }
            }
            else if (i < 2)
            {
                // Hands
                // Trigger on moving point.
                var collider2 = item.handlerParent.AddComponent<BoxCollider>();
                collider2.size = new Vector3(0.05f, 0.04f, 0.13f);
                collider2.isTrigger = true;
                if (debug)
                {
                    Util.CreatePrimitive(PrimitiveType.Cube, new Vector3(0.05f, 0.04f, 0.13f), item.handlerParent.transform, Color.cyan, 0.25f);
                }
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
                //var rigidBody = parent.AddComponent<Rigidbody>();
                //rigidBody.isKinematic = true;
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
                //var rigidBody = parent.AddComponent<Rigidbody>();
                //rigidBody.isKinematic = true;
            }
        }
        //public static void SetHandColor(ChaControl chara)
        //{
        //    // Different something (material, shader?) so the colors wont match from the get go.
        //    var color = chara.fileBody.skinMainColor;
        //    for (var i = 0; i < 4; i++)
        //    {
        //        aibuItemList[i].SetHandColor(color);
        //    }
        //}
        private void ActivateItem()
        {
            _anchor.SetPositionAndRotation(_controller.TransformPoint(_activeItem.positionOffset), _controller.rotation);
            _activeItem.rootPoint.localScale = Util.Divide(Vector3.Scale(Vector3.one, _activeItem.rootPoint.localScale), _activeItem.rootPoint.lossyScale);
            _activeItem.rootPoint.SetParent(_anchor, false);
            _activeItem.rootPoint.gameObject.SetActive(true);
            SetStartLayer();
            // Assign this one on basis of player's character scale?
            // No clue where ChaFile hides the height.
        }
        private void DeactivateItem()
        {
            _activeItem.rootPoint.gameObject.SetActive(false); 
            _activeItem.rootPoint.SetParent(VR.Manager.transform, false);
            StopSE();
        }
        
        private void LateUpdate()
        {
            _itemLag?.SetPositionAndRotation(_controller.TransformPoint(_activeItem.positionOffset), _controller.rotation);
            if (_activeItem.movingPoint == null) return;
            _activeItem.rootPoint.rotation = _anchor.rotation * _activeItem.rotationOffset * Quaternion.Inverse(_activeItem.movingPoint.rotation) * _activeItem.rootPoint.rotation;
            _activeItem.rootPoint.position += _anchor.position - _activeItem.movingPoint.position;
        }
        private void FixedUpdate()
        {
            if (_itemLag == null)
            {
                _rigidBody.MoveRotation(_controller.rotation);
                _rigidBody.MovePosition(_controller.TransformPoint(_activeItem.positionOffset));
            }
        }

        // Due to scarcity of hotkeys, we'll go with increase only.
        internal void ChangeItem()
        {
            var index = _itemList.IndexOf(_activeItem);
            DeactivateItem();

            // Last one is an empty one for synced limbs. 
            _activeItem = _itemList[(index + 1) % (_itemList.Count - (_activeItem.aibuItem == null ? 0 : 1))];
            ActivateItem();
        }

        private void PlaySE()
        {
            var aibuItem = _activeItem.aibuItem;
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
                aibuItem.transformSound.SetParent(_activeItem.movingPoint, false);
            }
            else
            {
                aibuItem.transformSound.GetComponent<AudioSource>().Play();
            }
        }
        private void StopSE()
        {
            if (_activeItem.aibuItem != null && _activeItem.aibuItem.transformSound != null)
            {
                _activeItem.aibuItem.transformSound.GetComponent<AudioSource>().Stop();
            }
        }
        public void SetStartLayer()
        {
            if (_activeItem.aibuItem == null) return;
            _activeItem.aibuItem.anm.SetLayerWeight(_activeItem.layer, 0f);
            _activeItem.aibuItem.anm.SetLayerWeight(_activeItem.startLayer, 1f);
            _activeItem.layer = _activeItem.startLayer;
        }
        public void ChangeLayer(bool increase, bool skipTransition = false)
        {
            //TestLayer(increase, skipTransition);

            if (_activeItem.availableLayers == null) return;
            StopSE();
            var anm = _activeItem.aibuItem.anm;
            var oldLayer = _activeItem.layer;

            var oldIndex = Array.IndexOf(_activeItem.availableLayers, oldLayer);
            var newIndex = increase ? (oldIndex + 1) % _activeItem.availableLayers.Length : oldIndex <= 0 ? _activeItem.availableLayers.Length - 1 : oldIndex - 1;
            //VRPlugin.Logger.LogDebug($"oldIndex:{oldIndex}:newIndex:{newIndex}");
            var newLayer = _activeItem.availableLayers[newIndex];

            if (skipTransition)
            {
                anm.SetLayerWeight(newLayer, 1f);
                anm.SetLayerWeight(oldLayer, 0f);
                _activeItem.layer = newLayer;
            }
            else
            {
                KoikatuInterpreter.Instance.StartCoroutine(ChangeLayerCo(anm, oldLayer, newLayer));
            }

            if (newLayer != 0 && _activeItem.aibuItem.pathSEAsset != null)
            {
                PlaySE();
            }
        }

        private IEnumerator ChangeLayerCo(Animator anm, int oldLayer, int newLayer)
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
            _activeItem.layer = newLayer;
        }

        /// <summary>
        /// Sets current item to an empty one and returns it's anchor.
        /// </summary>
        internal Transform GetEmptyAnchor()
        {
            DeactivateItem();
            _activeItem = _itemList[_itemList.Count - 1];
            ActivateItem();
            return _anchor;
        }
        internal void SetKinematic(bool state)
        {
            _rigidBody.isKinematic = state;
        }

        internal Transform OnGraspHold()
        {
            //_rigidBody.velocity = Vector3.zero;
             //_anchor.position += _controller.TransformPoint(_activeItem.positionOffset) - _anchor.position;

            // We adjust position after release of rigidBody, as it most likely had some velocity on it.
            if (_parent)
            {
                _parent = false;
            }
            else
            {
                // We compensate release of rigidBody's velocity by teleporting controller (target point of rigidBody).
                var pos = _anchor.position;
                _rigidBody.isKinematic = true;
                _controller.position += pos - _controller.TransformPoint(_activeItem.positionOffset);
                //_anchor.position += offsetPos - pos;
            }

            //_controller.position += pos - _anchor.position;
            _itemLag = new ItemLag(_anchor, 20);
            return _anchor;
        }
        internal void OnGraspRelease()
        {
            //_controller.position += _anchor.TransformPoint(Vector3.zero - _activeItem.positionOffset) - _controller.position;
            foreach (var inst in _instances)
            {
                if (inst.IsParent())
                {
                    inst.OnBecomingParent();
                    if (inst == this) return;
                }
            }
            _itemLag = null;
            var pos = _anchor.position;
            _rigidBody.isKinematic = false;
            _controller.position += pos - _controller.TransformPoint(_activeItem.positionOffset);
        }
        private void OnBecomingParent()
        {
            _parent = true;
            _itemLag = new ItemLag(_anchor, 10);
            _rigidBody.isKinematic = true;
        }
        private bool IsParent()
        {
            return _anchor.GetComponentsInChildren<Transform>()
                .Where(t => t.name.EndsWith("Anchor", StringComparison.Ordinal))
                .FirstOrDefault() != null;
        }

        private readonly List<string> _colliderParentListStartsWith =
            [
            "cf_j_middle02_",
            "cf_j_index02_",
            "cf_j_ring02_",
            "cf_j_thumb02_",
            "cf_s_hand_",
        ];
        private readonly List<string> _colliderParentListEndsWith =
            [
            "_head_00",
            "J_vibe_02",
            "J_vibe_05",
        ];
        private void AddDynamicBones()
        {
            var gameObjectList = new List<GameObject>();
            for (var i = 0; i < _itemList.Count - 1; i++)
            {
                var transforms = _itemList[i].aibuItem.obj.GetComponentsInChildren<Transform>(includeInactive: true)
                    .Where(t => _colliderParentListStartsWith.Any(c => t.name.StartsWith(c, StringComparison.Ordinal))
                    || _colliderParentListEndsWith.Any(c => t.name.EndsWith(c, StringComparison.Ordinal)))
                    .ToList();
                transforms?.ForEach(t => gameObjectList.Add(t.gameObject));
            }
            VRBoop.InitDB(gameObjectList);
        }
        //public void UpdateSkinColor(ChaFileControl chaFile)
        //{
        //    var color = chaFile.custom.body.skinMainColor;
        //    foreach (var item in aibuItemList.Values)
        //    {
        //        item.SetHandColor(color);
        //    }
        //}

    }
}
