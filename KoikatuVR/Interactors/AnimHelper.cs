using ADV.Commands.Camera;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KK_VR.Interactors
{
    internal class AnimHelper
    {
        private readonly List<string> _animAssets =
            [
            "h/anim/female/02_00_00.unity3d,khs_f_00",
            "h/anim/female/02_00_00.unity3d,khs_f_n00",
            "h/anim/female/02_12_00.unity3d,khs_f_n24",
            "h/anim/female/02_06_00.unity3d,khs_f_n23",
            "h/anim/female/02_00_00.unity3d,khs_f_n06",
            "h/anim/female/02_00_00.unity3d,khs_f_n16",
            "h/anim/female/02_00_00.unity3d,khs_f_n22",
            "h/anim/female/02_00_00.unity3d,khs_f_n08",
            "h/anim/female/02_00_00.unity3d,khs_f_n07",
            "h/anim/female/02_00_00.unity3d,khs_f_n20",
            "h/anim/female/02_00_00.unity3d,khs_f_02",
            "h/anim/female/02_00_00.unity3,khs_f_n02",
            "h/anim/female/02_00_00.unity3d,khs_f_11",
            "h/anim/female/02_00_00.unity3d,khs_f_n11",
            "h/anim/female/02_00_00.unity3d,khs_f_18",
            "h/anim/female/02_00_00.unity3d,khs_f_n18",
            "h/anim/female/02_00_00.unity3d,khs_f_n21",
            "h/anim/female/02_20_00.unity3d,khs_f_n28",
            "h/anim/female/02_13_00.unity3d,khs_f_n26",
            "h/anim/female/02_12_00.unity3d,khs_f_n25",
            "h/anim/female/02_13_00.unity3d,khs_f_n27",
            "h/anim/female/02_00_00.unity3d,khs_f_n04",
            "h/anim/female/02_00_00.unity3d,khs_f_n09",
            "h/anim/female/02_00_00.unity3d,khs_f_n10"

        ];
        internal void DoAnimChange(ChaControl chara)
        {
            //this.PushLimbAutoAttachButton(true);
            //if (this.femaleSpinePos == null)
            //{
            //    this.femaleSpinePos = new GameObject("femaleSpinePos");
            //}
            //this.femaleSpinePos.transform.position = this.femaleBase.transform.position;
            //this.femaleSpinePos.transform.rotation = this.femaleBase.transform.rotation;
            //SetParent.CtrlState oldState = this.currentCtrlstate;
            //this.currentCtrlstate = SetParent.CtrlState.Following;
            var animator = chara.animBody;
            var str = _animAssets[0].Split(',');
            var bundle = str[0];
            var asset = str[1];
            var runtimeAnimController = CommonLib.LoadAsset<RuntimeAnimatorController>(bundle, asset);
            var animOverrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
            foreach (var animClip in new AnimatorOverrideController(runtimeAnimController).animationClips)
            {
                animOverrideController[animClip.name] = animClip;
            }
            animOverrideController.name = runtimeAnimController.name;
            animator.runtimeAnimatorController = animOverrideController;
            AssetBundleManager.UnloadAssetBundle(bundle, true);
        }
    }
    
}
