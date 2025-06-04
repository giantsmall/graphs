using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Analytics;

namespace Assets.Game.Scripts.Utility
{
    public static class RndFunc
    {
        public static Gender GetRandomGender()
        {
            return (Gender)new Random().Next(2);            
        }
    }
}
