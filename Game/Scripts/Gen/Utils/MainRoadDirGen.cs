using Assets.Game.Scripts.Utility;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Game.Scripts.Gen.Utils
{
    public class MainRoadDirGen
    {
        public static List<Vector2> GenerateMainRoadDirections(System.Random rnd, int MainRoadsCount)
        {
            var min = -100;
            var zero = 0;
            var hundred = 100;
            var max = 200;
            var roadDirs = new List<Vector2>();

            roadDirs.Add(new Vector2(min, min));
            roadDirs.Add(new Vector2(min, zero));
            roadDirs.Add(new Vector2(min, hundred));
            roadDirs.Add(new Vector2(min, max));

            roadDirs.Add(new Vector2(zero, min));
            roadDirs.Add(new Vector2(zero, max));

            roadDirs.Add(new Vector2(hundred, min));
            roadDirs.Add(new Vector2(hundred, max));

            roadDirs.Add(new Vector2(max, min));
            roadDirs.Add(new Vector2(max, zero));
            roadDirs.Add(new Vector2(max, hundred));
            roadDirs.Add(new Vector2(max, max));

            return roadDirs.TakeRandom(rnd, MainRoadsCount);
        }
    }
}
    