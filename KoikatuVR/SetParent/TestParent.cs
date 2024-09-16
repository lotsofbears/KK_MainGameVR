//using Illusion.Component.Correct;
//using KK_VR.Features;
//using KK_VR.Interpreters;
//using RootMotion.FinalIK;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using UnityEngine;
//using static SetParentKK.SetParent;

//namespace KK_VR.SetParent
//{
//    internal class TestParent
//    {
//        internal Limb[] limbs = new Limb[8];
//        private GameObject playerBodyBone;
//        private Transform playerHips;
//        private BaseData playerHipsBD;
//        private FullBodyBipedIK playerIK;
//        public void InitMale()
//        {
//            var male = HSceneInterpreter.male;
//            playerBodyBone = male.objAnim;
//            playerIK = playerBodyBone.GetComponent<FullBodyBipedIK>();

//            var male_cf_n_height = playerIK.references.pelvis.parent;
//            var male_cf_pv_hand_R = male_cf_n_height.Find("cf_pv_root/cf_pv_hand_R");
//            var male_cf_pv_hand_L = male_cf_n_height.Find("cf_pv_root/cf_pv_hand_L");
//            var male_cf_pv_leg_R = male_cf_n_height.Find("cf_pv_root/cf_pv_leg_R");
//            var male_cf_pv_leg_L = male_cf_n_height.Find("cf_pv_root/cf_pv_leg_L");

//            playerHips = male_cf_n_height.Find("cf_pv_root/cf_pv_hips");
//            playerHipsBD = playerIK.solver.bodyEffector.target.GetComponent<BaseData>();

//            var male_hand_L_bd = playerIK.solver.leftHandEffector.target.GetComponent<BaseData>();
//            var male_hand_R_bd = playerIK.solver.rightHandEffector.target.GetComponent<BaseData>();
//            var male_leg_L_bd = playerIK.solver.leftFootEffector.target.GetComponent<BaseData>();
//            var male_leg_R_bd = playerIK.solver.rightFootEffector.target.GetComponent<BaseData>();

//            limbs[(int)LimbName.MaleLeftHand] = new Limb(
//                limbpart: LimbName.MaleLeftHand,
//                anchorObj: null,
//                animPos: male_cf_pv_hand_L,
//                effector: playerIK.solver.leftHandEffector,
//                origTarget: playerIK.solver.leftHandEffector.target,
//                targetBone: male_hand_L_bd,
//                chain: playerIK.solver.leftArmChain,
//                parentJointBone: playerIK.solver.leftShoulderEffector.target.GetComponent<BaseData>(),
//                parentJointEffector: playerIK.solver.leftShoulderEffector,
//                parentJointAnimPos: male_cf_n_height.Find("cf_pv_root/cf_pv_hips/cf_ik_hips/cf_kk_shoulder/cf_pv_shoulder_L"));

//            limbs[(int)LimbName.MaleRightHand] = new Limb(
//                limbpart: LimbName.MaleRightHand,
//                anchorObj: null,
//                animPos: male_cf_pv_hand_R,
//                effector: playerIK.solver.rightHandEffector,
//                origTarget: playerIK.solver.rightHandEffector.target,
//                targetBone: male_hand_R_bd,
//                chain: playerIK.solver.rightArmChain,
//                parentJointBone: playerIK.solver.rightShoulderEffector.target.GetComponent<BaseData>(),
//                parentJointEffector: playerIK.solver.rightShoulderEffector,
//                parentJointAnimPos: male_cf_n_height.Find("cf_pv_root/cf_pv_hips/cf_ik_hips/cf_kk_shoulder/cf_pv_shoulder_R"));

//            limbs[(int)LimbName.MaleLeftFoot] = new Limb(
//                limbpart: LimbName.MaleLeftFoot,
//                anchorObj: null,
//                animPos: male_cf_pv_leg_L,
//                effector: playerIK.solver.leftFootEffector,
//                origTarget: playerIK.solver.leftFootEffector.target,
//                targetBone: male_leg_L_bd);

//            limbs[(int)LimbName.MaleRightFoot] = new Limb(
//                limbpart: LimbName.MaleRightFoot,
//                anchorObj: null,
//                animPos: male_cf_pv_leg_R,
//                effector: playerIK.solver.rightFootEffector,
//                origTarget: playerIK.solver.rightFootEffector.target,
//                targetBone: male_leg_R_bd);

//        }
//    }
//}
