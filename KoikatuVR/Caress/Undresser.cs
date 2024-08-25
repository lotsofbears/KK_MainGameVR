﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using VRGIN.Core;
using HarmonyLib;

using static ChaFileDefine;
using KK_VR.Fixes;

namespace KK_VR.Caress
{
    /// <summary>
    /// An object responsible for determining which clothing item to remove,
    /// based on the controller position.
    /// </summary>
    class Undresser
   {
        private readonly Dictionary<Collider, Util.ValueTuple<int, InteractionBodyPart>> _knownColliders =
            new Dictionary<Collider, Util.ValueTuple<int, InteractionBodyPart>>();
        private readonly HashSet<Collider> _currentlyIntersecting = new HashSet<Collider>();

        public Undresser(HSceneProc proc)
        {
            // Populate _knownColliders.
            var lstFemale = new Traverse(proc).Field("lstFemale").GetValue<List<ChaControl>>();
            for (int i = 0; i < lstFemale.Count; i++)
            {
                var colliders = lstFemale[i].GetComponentsInChildren<Collider>(includeInactive: true);
                foreach (var collider in colliders)
                {
                    if (ColliderBodyPart(collider) is InteractionBodyPart part)
                    {
                        _knownColliders.Add(collider, Util.ValueTuple.Create(i, part));
                    }
                }
            }
        }

        public void Enter(Collider collider)
        {
            if (_knownColliders.ContainsKey(collider))
            {
                _currentlyIntersecting.Add(collider);
            }
        }

        public void Exit(Collider collider)
        {
            _currentlyIntersecting.Remove(collider);
        }

        public ClothesKind? ComputeUndressTarget(List<ChaControl> females, out int femaleIndex)
        {
            femaleIndex = 0;
            if (_currentlyIntersecting.Count == 0)
            {
                return null;
            }
            var part = (InteractionBodyPart)9999;
            foreach (var collider in _currentlyIntersecting)
            {
                var item = _knownColliders[collider];
                if (item.Field2 < part)
                {
                    femaleIndex = item.Field1;
                    part = item.Field2;
                    break;
                }
            }

            var female = females[femaleIndex];
            var targets = _itemsForPart[(int)part];
            if (part == InteractionBodyPart.Crotch && IsWearingSkirt(female))
            {
                //VRLog.Debug($"WearingSkirt");
                // Special case: if the character is wearing a skirt, allow
                // directly removing the underwear.
                targets = _skirtCrotchTargets;
            }
            //VRLog.Debug($"part = {part}");
            foreach (var target in targets)
            {
                if (!female.IsClothesStateKind((int)target.kind))
                {
                    continue;
                }
                var state = female.fileStatus.clothesState[(int)target.kind];
                if (target.min_state <= state && state <= target.max_state)
                {
                    //VRLog.Info($"toUndress: {target.kind} {state}");
                    return target.kind;
                }
            }
            return null;
        }

        private static bool IsWearingSkirt(ChaControl female)
        {
            var objBot = female.objClothes[1];
            return objBot != null && objBot.GetComponent<DynamicBone>() != null;
        }

        /// <summary>
        /// A body part the user can interact with. A more specific part gets
        /// a lower number.
        /// </summary>
        private enum InteractionBodyPart
        {
            Crotch,
            Groin,
            Breast,
            LegL,
            LegR,
            Forearm,
            UpperArm,
            Thigh,
            Torso,
        }
        private static readonly UndressTarget[][] _itemsForPart = new[]
        {
            new[] { Target(ClothesKind.bot, 0), Target(ClothesKind.panst, 0), Target(ClothesKind.shorts, 0),
                Target(ClothesKind.panst), Target(ClothesKind.shorts), Target(ClothesKind.bot)},

            new[] { Target(ClothesKind.bot), Target(ClothesKind.panst, 0), Target(ClothesKind.shorts, 0),
                Target(ClothesKind.panst), Target(ClothesKind.shorts), Target(ClothesKind.bot)},

            new[] { Target(ClothesKind.top, 0), Target(ClothesKind.bra, 0), Target(ClothesKind.top), Target(ClothesKind.bra)},
            new[] { Target(ClothesKind.socks), Target(ClothesKind.panst), Target(ClothesKind.shorts, 2, 2) },
            new[] { Target(ClothesKind.socks), Target(ClothesKind.panst) },
            new UndressTarget[] { },
            new[] { Target(ClothesKind.top) },
            new[] { Target(ClothesKind.bot, 0), Target(ClothesKind.panst, 0), Target(ClothesKind.panst), Target(ClothesKind.bot), Target(ClothesKind.socks) },
            new[] { Target(ClothesKind.top) },
        };

        private static readonly UndressTarget[] _skirtCrotchTargets =
            new[] { Target(ClothesKind.panst, 0), Target(ClothesKind.shorts, 0), Target(ClothesKind.bot, 0), Target(ClothesKind.shorts, 1) };

        private static UndressTarget Target(ClothesKind kind, int max_state = 2, int min_state = 0)
        {
            return new UndressTarget(kind, max_state, min_state);
        }

        private static InteractionBodyPart? ColliderBodyPart(Collider collider)
        {
            var name = collider.name;
            if (name.Equals("aibu_hit_kokan") ||
                name.Equals("aibu_hit_ana"))
                return InteractionBodyPart.Crotch;
            if (name.StartsWith("aibu_hit_siri", StringComparison.Ordinal) ||
                name.StartsWith("aibu_reaction_waist", StringComparison.Ordinal))
                return InteractionBodyPart.Groin;
            if (name.StartsWith("cf_hit_bust", StringComparison.Ordinal))
                return InteractionBodyPart.Breast;
            if(name.Equals("aibu_reaction_legL"))
                return InteractionBodyPart.LegL;
            if(name.Equals("aibu_reaction_legR"))
                return InteractionBodyPart.LegR;
            if(name.StartsWith("cf_hit_wrist", StringComparison.Ordinal))
                return InteractionBodyPart.Forearm;
            if(name.StartsWith("cf_hit_arm", StringComparison.Ordinal))
                return InteractionBodyPart.UpperArm;
            if(name.StartsWith("aibu_reaction_thigh", StringComparison.Ordinal))
                return InteractionBodyPart.Thigh;
            if(name.Equals("cf_hit_spine01") ||
                name.Equals("cf_hit_spine03") ||
                name.Equals("cf_hit_berry"))
                return InteractionBodyPart.Torso;
            return null;
        }

        private struct UndressTarget
        {
            public UndressTarget(ClothesKind k, int m, int mm)
            {
                kind = k;
                max_state = m;
                min_state = mm;
            }
            public ClothesKind kind;
            public int max_state;
            public int min_state;
        }
    }
}
