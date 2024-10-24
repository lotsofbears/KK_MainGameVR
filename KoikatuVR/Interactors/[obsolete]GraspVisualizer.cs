//using System;
//using System.Collections.Generic;
//using System.Linq;
//using static KK_VR.Interactors.GraspController;
//using System.Text;
//using ADV.EventCG;
//using UnityEngine;
//using KK_VR.Fixes;
//using KK_VR.Features;
//using KK_VR.Settings;
//using VRGIN.Core;

//namespace KK_VR.Interactors
//{
//    internal class GraspVisualizer
//    {
//        internal static GraspVisualizer Instance => _instance ??= new GraspVisualizer();
//        private static GraspVisualizer _instance;
//        private static List<Color> _colors = new List<Color>()
//        {
//            new Color(1f, 0f, 0, 0.2f), // Red
//            new Color(0f, 1f, 0, 0.2f), // Green
//            new Color(0f, 0f, 1, 0.2f),  // Blue
//            new Color(1, 1, 1, 0.2f)
//        };
//        private static readonly Dictionary<ChaControl, List<GameObject>> _guideObjectsDic = new Dictionary<ChaControl, List<GameObject>>();
//        private static readonly KoikatuSettings _settings = VR.Context.Settings as KoikatuSettings;
//        internal bool Active { get; private set; }
//        internal static void Init(Dictionary<ChaControl, List<BodyPart>> bodyPartDic)
//        {
//            _guideObjectsDic.Clear();
//            foreach (var kv in bodyPartDic)
//            {
//                _guideObjectsDic.Add(kv.Key, AddGuideObjects(kv.Value));
//            }
//        }
//        internal void ShowAttachPoints(ChaControl chara, IEnumerable<BodyPart> bodyParts)
//        {
//            foreach (var bodyPart in bodyParts)
//            {
//                if (bodyPart.name != PartName.Body)
//                {
//                    _guideObjectsDic[chara][(int)bodyPart.name].SetActive(true);
//                }
//            }
//            Active = true;
//        }
//        internal void ChangeColor(ChaControl chara, int index, bool active)
//        {
//            if (_guideObjectsDic.ContainsKey(chara))
//            {
//                _guideObjectsDic[chara][index].GetComponent<Renderer>().material.color = active ? _colors[1] : _colors[3];
//            }
//        }
//        internal void HideAttachPoints(ChaControl chara, IEnumerable<BodyPart> bodyParts)
//        {
//            if (!Active) return;
//            foreach (var bodyPart in bodyParts)
//            {
//                if (bodyPart.name != PartName.Body)
//                {
//                    _guideObjectsDic[chara][(int)bodyPart.name].SetActive(false);
//                }
//            }
//            Active = false;
//        }
//        //private static List<GameObject> AddGuideObjects(List<BodyPart> bodyPartList)
//        //{
//        //    var list = new List<GameObject>();
//        //    foreach (var bodyPart in bodyPartList)
//        //    {
//        //        if (bodyPart.name == PartName.Body)
//        //        {
//        //            // No need for body guide object, but indexes must align.
//        //            list.Add(null);
//        //            continue;
//        //        }
//        //        var sphere = Util.CreatePrimitive(
//        //            PrimitiveType.Sphere, 
//        //            GetColliderSize(bodyPart.name), 
//        //            bodyPart.effector.bone,
//        //            _colors[3], 
//        //            removeCollider: true);
//        //        sphere.name = "GuideObject" + bodyPart.name;
//        //        sphere.SetActive(false);
//        //        list.Add(sphere);

//        //    }
//        //    return list;
//        //}
//    }
//}
