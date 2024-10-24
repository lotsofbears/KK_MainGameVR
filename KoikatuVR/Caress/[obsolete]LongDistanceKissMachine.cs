//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using UnityEngine;

//using VRGIN.Core;

//namespace KK_VR.Caress
//{
//    /// <summary>
//    /// A state machine for starting and finishing a kiss in the caress mode.
//    ///
//    /// This requires a special treatment because the female leans forward after
//    /// a kiss is started. This means a kiss should start with some distance.
//    /// </summary>
//    public class LongDistanceKissMachine
//    {
//        float? _startTime; // null if not kissing
//        bool _prevEntryConditionMet = true;

//        public bool Step(
//            float currentTime,
//            Vector3 femaleFromHmd,
//            Vector3 hmdFromFemale,
//            float femaleFaceAngleY)
//        {
//            bool entryConditionMet = EntryScore(femaleFromHmd, hmdFromFemale, femaleFaceAngleY) < 0;
//            bool result;
//            if (_startTime is float startTime)
//            {
//                var duration = currentTime - startTime;
//                var maxDistance = Mathf.Max(0.10f, 0.55f - 0.4f * duration);
//                result = hmdFromFemale.sqrMagnitude < maxDistance * maxDistance;
//            }
//            else
//            {
//                result = entryConditionMet && !_prevEntryConditionMet;
//            }

//            _prevEntryConditionMet = entryConditionMet;

//            if (result)
//            {
//                _startTime = _startTime ?? currentTime;
//            }
//            else if (_startTime != null)
//            {
//                _startTime = null;
//            }
//            return result;
//        }

//        public void Reset()
//        {
//            _startTime = null;
//            _prevEntryConditionMet = true;
//        }

//        private static float EntryScore(Vector3 femaleFromHmd, Vector3 hmdFromFemale, float femaleFaceAngle)
//        {
//            var total = OneSidedScore(femaleFromHmd) + OneSidedScore(hmdFromFemale) + 0.1f * Mathf.Abs(femaleFaceAngle);
//            return total - 2.0f;
//        }

//        private static float OneSidedScore(Vector3 rel)
//        {
//            rel.z = 0.4f * (rel.z - 0.1f);
//            return 500f * rel.sqrMagnitude;
//        }
//    }
//}
