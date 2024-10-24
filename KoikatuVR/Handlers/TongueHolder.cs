using KK_VR.Features;
using KK_VR.Fixes;
using Studio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using VRGIN.Controls;
using VRGIN.Core;

namespace KK_VR.Handlers
{
    internal class TongueHolder : Holder
    {
        private void SetItems(GameObject gameObject)
        {
            _anchor = gameObject.transform;
            //_anchor.SetParent(VR.Manager.transform, false);
            _rigidBody = _anchor.gameObject.AddComponent<Rigidbody>();
            _rigidBody.useGravity = false;
            _rigidBody.freezeRotation = true;
            //_audioSource = _anchor.gameObject.AddComponent<AudioSource>();

            //InitTongue();
;

            _activeItem = new ItemType(
                _loadedAssetsList[4], _defaultAnimParamList[4]
                );
            SetColliders();
            AddDynamicBones();
            ActivateItem();
        }
        private void ActivateItem()
        {
            _activeItem.rootPoint.SetParent(_anchor, false);
            _activeItem.rootPoint.gameObject.SetActive(true);
            _anchor.SetParent(VR.Camera.Head, false);
            _anchor.localPosition = _activeItem.positionOffset;
            _anchor.localScale = Util.Divide(Vector3.Scale(Vector3.one, _anchor.localScale), _anchor.lossyScale);
            //_anchor.SetPositionAndRotation(VR.Camera.Head.TransformPoint(_activeItem.positionOffset), VR.Camera.Head.rotation);
            //_activeItem.rootPoint.localScale = Util.Divide(Vector3.Scale(Vector3.one, _activeItem.rootPoint.localScale), _activeItem.rootPoint.lossyScale);
            //SetStartLayer();
        }
        private void DeactivateItem()
        {
            _activeItem.rootPoint.gameObject.SetActive(false);
            _activeItem.rootPoint.SetParent(VR.Manager.transform, false);
            //StopSE();
        }

        private void SetColliders()
        {
            // Rigid-body slave.
            var collider1 = _anchor.gameObject.AddComponent<CapsuleCollider>();
            collider1.radius = 0.01f;
            collider1.height = 0.025f;
            collider1.direction = 2;
            
            // Trigger.
            var collider2 = _activeItem.handlerParent.AddComponent<BoxCollider>();
            collider2.size = new Vector3(0.08f, 0.05f, 0.13f);
            collider2.isTrigger = true;
            _activeItem.aibuItem.renderSilhouette.enabled = false;
        }

        private void AddDynamicBones()
        {
            var gameObjectList = _activeItem.aibuItem.obj.GetComponentsInChildren<Transform>(includeInactive: true)
                .Where(t => t.name.Equals("cf_j_tang_04", StringComparison.Ordinal))
                .Select(t => t.gameObject);

            VRBoop.InitDB(gameObjectList);
        }

        //public static void TestLayer(bool increase, bool skipTransition = false)
        //{
        //    var item = controllersDic[0][0];

        //    var anm = item.aibuItem.anm;
        //    var oldLayer = item.layer;
        //    var oldIndex = Array.IndexOf(item.availableLayers, oldLayer);
        //    var newIndex = increase ? (oldIndex + 1) % item.availableLayers.Length : oldIndex <= 0 ? item.availableLayers.Length - 1 : oldIndex - 1;
        //    var newLayer = item.availableLayers[newIndex];

        //    //var newRotationOffset = newLayer == 13 || newLayer == 15 ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;// Quaternion.Euler(-90f, 0f, 0f);

        //    if (skipTransition)
        //    {
        //        anm.SetLayerWeight(newLayer, 1f);
        //        anm.SetLayerWeight(oldLayer, 0f);
        //        item.layer = newLayer;
        //    }
        //    else
        //    {
        //        KoikatuInterpreter.Instance.StartCoroutine(ChangeTongueCo(item, anm, oldLayer, newLayer));
        //    }
        //    VRPlugin.Logger.LogDebug($"TestLayer:{newLayer}");

        //}
        //private static IEnumerator ChangeTongueCo(ItemType item, Animator anm, int oldLayer, int newLayer)
        //{
        //    var timer = 0f;
        //    var stop = false;
        //    //var initRotOffset = item.rotationOffset;
        //    while (!stop)
        //    {
        //        timer += Time.deltaTime * 2f;
        //        if (timer > 1f)
        //        {
        //            timer = 1f;
        //            stop = true;
        //        }
        //        //item.rotationOffset = Quaternion.Lerp(initRotOffset, newRotationOffset, timer);
        //        anm.SetLayerWeight(newLayer, timer);
        //        anm.SetLayerWeight(oldLayer, 1f - timer);
        //        yield return null;
        //    }
        //    item.layer = newLayer;
        //}
    }
}
