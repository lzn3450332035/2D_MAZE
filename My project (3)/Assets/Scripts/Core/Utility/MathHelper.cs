using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    public static class MathHelper
    {
        
        public static int KnockbackHalveCeil(int original)
        {
            return (original + 2 - 1) / 2;

        }
        public static float ClampZero(float value)
        {
            return Mathf.Max(0f, value);
        }

        
    }
    
}