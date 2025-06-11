using Assets.Game.Scripts.Gen.Models;
using Delaunay.Geo;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering;

namespace Assets.Game.Scripts.Utility
{
    internal static class Extentions
    {
        public static void ShiftBy(this List<Vector2> list, Vector2 shift)
        {
            for (int i = 0; i < list.Count; i++)
            {
                list[i] += shift;
            }
        }
        public static bool IsValid(this Vector2 vector) => float.IsNaN(vector.x) || float.IsNaN(vector.y) || float.IsInfinity(vector.x) || float.IsInfinity(vector.y);

        public static float GenNormalDist(this System.Random rnd, float min = 0, float max = 1)
        {
            return (float)((Math.Asin(2 * rnd.NextDouble() - 1) + Math.PI / 2.0) / Math.PI * (max - min) + min); //(max - min or just min???)
        }

        public static float NormalFloat(this System.Random rnd, float min = 0, float max = 1) => rnd.GenNormalDist(min, max);

        public static Vector2 NextVector2(this System.Random rnd, float dev = 1f)
        {
            return new Vector2(rnd.NextFloat(- dev, dev), rnd.NextFloat(-dev, dev));
        }

        public static Vector2 RandomDirection(this System.Random rnd)
        {
            var angle = rnd.NextFloat(360);
            var result = Vector2.one.RotateAroundPivot(Vector2.zero, angle);
            return result;
        }
        public static Vector2 RandomDirection(this System.Random rnd, float maxDev)
        {
            var angle = rnd.NextFloat(maxDev * 2) - maxDev;
            var result = Vector2.one.RotateAroundPivot(Vector2.zero, angle);
            return result;
        }
        
        public static T GetRandom<T>(this IEnumerable<T> ienum, System.Random rnd)
        {
            return ienum.ToList()[rnd.Next(ienum.Count())];
        }
        public static float NextFloat(this System.Random rnd, float min = 0, float max = 1) => (float)(rnd.NextDouble() * (max - min) + min);

        public static float NextFloat(this System.Random rnd, float max) => (float)(rnd.NextDouble() * max);
        public static bool NextBool(this System.Random rnd, float trueChance = 0.5f) => rnd.NextFloat(0, 1) <= trueChance;

        public static bool HasValue(this string str) => !string.IsNullOrEmpty(str);
        public static bool HasNoValue(this string str) => !str.HasValue();

        public static List<T> TakeRandom<T>(this IEnumerable<T> ienum, int count)
        {
            return ienum.OrderBy(x => new Guid()).Take(count).ToList();
        }

        public static List<T> TakeRandom<T>(this IEnumerable<T> ienum, System.Random rnd, int count)
        {
            return ienum.OrderBy(x => rnd.Next()).Take(count).ToList();
        }

        public static bool StrEquals(this string sourceStr, string str, bool ignoreCase = false)
        {
            return sourceStr.Equals(str, ignoreCase? StringComparison.OrdinalIgnoreCase: StringComparison.Ordinal);            
        }

        public static void SetRectTransform(this GameObject mb, GameObject parent, Vector3 position, Vector3 size, bool anchored = false, bool setDefaultAnchor = true)
        {
            mb.SetRectTransform(parent, position, anchored, setDefaultAnchor);
            mb.GetRect().sizeDelta = size;            
        }
        public static void SetRectTransform(this GameObject go, GameObject parent, Vector3 position, bool anchored = false, bool setDefaultAnchor = true)
        {
            if (setDefaultAnchor)
                go.gameObject.SetDefaultAnchor(true);
            else go.gameObject.SetBottomLefttAnchor();

            var rt = go.GetRect();
            rt.SetParent(parent.transform);
            rt.localScale = Vector3.one;

            if (anchored)
                rt.anchoredPosition = position;// + new Vector3(rt.sizeDelta.x / 2, 0);
            else rt.localPosition = position;
        }
     
        public static void ClonePositionFrom(this GameObject go, GameObject source)
        {
            go.GetRect().anchoredPosition = source.GetRect().anchoredPosition;
        }

        public static RectTransform GetRect(this GameObject go)
        {
            return go.transform as RectTransform;
        }

        public static RectTransform GetRect(this MonoBehaviour go)
        {
            return go.transform as RectTransform;
        }

        public static void SetDefaultAnchor(this GameObject go, bool set)
        {
            if (set)
            {
                go.GetRect().anchorMin = new Vector2(0, 1);
                go.GetRect().anchorMax = new Vector2(0, 1);
                go.GetRect().pivot = new Vector2(0, 1);
            }
        }

        public static void SetBottomLefttAnchor(this GameObject go)
        {
            go.GetRect().anchorMin = new Vector2(0, 0);
            go.GetRect().anchorMax = new Vector2(0, 0);
            go.GetRect().pivot = new Vector2(0, 1);
        }

        public static string CapitalCase(this string str)
        {
            if (str == null)
                return null;
            if (str.Length > 1)
                return char.ToUpper(str[0]) + str.Substring(1);
            return str.ToUpper();
        }

        public static ushort NextUshort(this System.Random rnd, int minValue, ushort maxValue)
        {
            return (ushort)rnd.Next(minValue, maxValue);
        }

        public static byte NextByte(this System.Random rnd, byte minValue, byte maxValue)
        {
            return (byte)rnd.Next(minValue, maxValue);
        }

        public static ushort NextUshort(this System.Random rnd, ushort maxValue)
        {
            return (ushort)rnd.Next(0, maxValue);
        }

        public static string ToFixedString(this Enum enumType) => enumType.ToString().ToFixedString();
        public static string ToFixedString(this string str) => str.Replace('_', ' ');
        public static T ToEnumType<T>(this string enumStr) where T : Enum
        {
            try
            {
                return (T)Enum.Parse(typeof(T), enumStr.Replace(" ", "_"));
            }
            catch(Exception e)
            {
                Debug.LogWarning(e);
                return (T)Enum.GetValues(typeof(T)).GetEnumerator().Current;
            }
        }
        

        public static void AddOrCreate<K, V>(this Dictionary<K, V> dict, K key, V value)
        {
            if (!dict.ContainsKey(key))
            {
                dict.Add(key, value);
            }
            else
            {
                 dict[key] = value;
            }
        }
        
        public static float DistanceTo(this Vector2 v1, Vector2 v2)
        {
            return (v1 - v2).magnitude;
        }


        public static Vector3 GetDistanceVector(this Vector3 v1, Vector3 v2)
        {
            return new Vector3(v1.x - v2.x, v1.y - v2.y, v1.z - v2.z);
        }

        public static float GetDistance(this Vector3 v1, Vector3 v2)
        {
            return Mathf.Sqrt(Mathf.Pow(v1.x - v2.x, 2) + Mathf.Pow(v1.y - v2.y, 2) + Mathf.Pow(v1.z - v2.z, 2));
        }

        public static void ToggleGoActive(this MonoBehaviour go)
        {
            go.gameObject.ToggleActive();
        }

        public static void ToggleActive(this GameObject go)
        {
            go.SetActive(!go.activeSelf);
            foreach (Transform t in go.transform)
            {
                t.gameObject.SetActive(go.activeSelf);
            }
        }

        public static List<T> Except<T>(this List<T> list, params T[] items)
        {
            return list.Except(items.ToList()).ToList();
        }

        public static string Join<T>(this IEnumerable<T> str, char separator)
        {
            var singleStr = "";
            foreach (var s in str)
            {
                singleStr += s.ToString() + " ";
            }
            return singleStr.Trim().Replace(' ', separator);
        }

        public static string Join(this IEnumerable<string> str, char separator)
        {
            var singleStr = "";
            foreach (var s in str)
            {
                singleStr += s + " ";
            }
            return singleStr.Trim().Replace(' ', separator);
        }

        public static bool ContainsAll<T>(this List<T> l, params T[] points)
        {
            var result = points.All(p => l.Contains(p));
            return result;
        }

        public static bool ContainsAny<T>(this List<T> l, params T[] containedItems)
        {
            foreach (var item in containedItems)
            {
                if (l.Contains(item))
                    return true;
            }
            return false;
        }


        public static bool ContainsList<T>(this List<T> l, IEnumerable<T> containedList)
        {
            foreach (var item in containedList)
            {
                if (!l.Contains(item))
                    return false;
            }
            return true;
        }

        public static Vector2 ToUEVector2(this System.Numerics.Vector2 v)
        {
            return new Vector2(v.X, v.Y);
        }

        public static T LastButOne<T>(this List<T> list) => list[list.Count - 2];

        public static List<T> TakeStartingFrom<T>(this List<T> list, int start)
        {
            return list.TakeRangeBetween(start, list.Count, false);
        }

        public static List<T> TakeRangeBetween<T>(this List<T> list, T obj1, T obj2, bool include = true)
        {
            var index1 = list.IndexOf(obj1);
            var index2 = list.IndexOf(obj2);

            return list.TakeRangeBetween(index1, index2, include);
        }

        public static List<T> Reversed<T>(this List<T> list)
        {
            list.Reverse();
            return list;
        }

        public static List<T> TakeReversedRangeBetween<T>(this List<T> list, T obj1, T obj2, bool include = true)
        {
            var index1 = list.IndexOf(obj1) + (include? 1: 0);
            var index2 = list.IndexOf(obj2) + (include? -1 : 0);

            var result = new List<T>();
            result.AddRange(list.TakeStartingFrom(index2));
            result.AddRange(list.Take(index1));
            return result;
        }
        
        public static List<T> TakeRangeBetween<T>(this List<T> list, int index1, int index2, bool include = true)
        {
            var start = Math.Min(index1, index2);
            var end = Math.Max(index1, index2);
            if (!include)
            {
                start += 1;
                end -= 1;
            }
            return list.GetRange(start, end - start + 1);
        }

        public static T TakeMiddleOne<T>(this List<T> list, bool left = false)
        {
            if (list.Count % 2 == 1)
                return list[list.Count / 2];
            else return list[(list.Count - 1) / 2 + (left? 0 : 1)];
        }
        public static int GetMiddleIndex<T>(this List<T> list, bool left = false)
        {
            if (list.Count % 2 == 1)
                return list.Count / 2;
            else return (list.Count - 1) / 2 + (left ? 1 : 0);
        }

        public static int GetMiddleNumber(this int start, int count)
        {
            return start + (count - start) / 2;
        }

        public static List<T> TakeLesserRangeWrapped<T>(this List<T> list, T obj1, T obj2, bool include = true)
        {
            var start = list.IndexOf(obj1);
            var end = list.IndexOf(obj2);
            return list.TakeLesserRangeWrapped(start, end, include);
        }

        public static void RemoveRandom<T>(this List<T> list, System.Random rnd)
        {
            list.RemoveAt(rnd.Next(list.Count));
        }

        public static List<T> TakeLesserRangeWrapped<T>(this List<T> list, int start, int end, bool include = true)
        {
            var diffBetweenEndStart = Math.Abs(start - end);
            var remainingAmount = list.Count - diffBetweenEndStart;

            var min = Math.Min(start, end);
            var max = Math.Max(start, end);
            try
            {
                List<T> result = null;
                if (diffBetweenEndStart < remainingAmount)
                {
                    result = list.TakeRangeBetween(min, max, include);
                }
                else
                {
                    if (!include)
                    {
                        start += 1;
                        end -= 1;
                    }

                    result = list.TakeLast(list.Count - start).ToList();
                    result.AddRange(list.Take(end + 1));
                }
                return result;
            }
            catch (Exception e)
            {
                throw new Exception("Range error");
            }
            return null;
        }

        public static List<T> TakeRangeWrapped<T>(this List<T> list, int start, int end, bool include = true)
        {
            List<T> result = null;
            if (start <= end)
            {
                result = list.TakeRangeBetween(start, end, include);
            }                
            else
            {
                if (!include)
                {
                    start++;
                    end--;
                }
                result = list.TakeLast(list.Count - start).ToList();
                result.AddRange(list.Take(end));
            }
            if (!result.Any())
            {
                throw new Exception("No points were selected");
            }
            return result;
        }

        public static List<T> TakeRangeWrapped<T>(this List<T> list, T obj1, T obj2, bool include = true)
        {
            var start = list.IndexOf(obj1);
            var end = list.IndexOf(obj2);
            List<T> result = null;
            if (start <= end)
                result = list.TakeRangeBetween(start, end, include);
            else
            {
                result = list.TakeLast(list.Count - start).ToList();
                result.AddRange(list.Take(end));
                
            }
            if(!result.Any())
            {

            }
            return result;
        }

        public static List<T> TakeFirstHalf<T>(this List<T> list)
        {
            return list.GetRange(0, list.Count / 2);
        }

        public static List<T> TakeSecondHalf<T>(this List<T> list)
        {
            var firstIndex = (list.Count + 1) / 2;
            var count = list.Count - firstIndex;
            return list.GetRange(firstIndex, count);
        }

        public static List<float> GetRandomSplit(this float sum, float min, float max, System.Random rnd)
        {
            var list = new List<float>();
            do
            {
                var val = Math.Min(rnd.NextFloat(min, max), sum);
                list.Add(val);
                sum -= val;
            }
            while (sum > 0);
            
            if (list.Count > 1 && list.Last() < 1 && list.Last() + list.LastButOne() < max)
            {
                var oldLast = list.Last();
                list.RemoveAt(list.Count - 1);
                list[list.Count - 1] = list[list.Count - 1] + oldLast;
            }
            return list;
        }

        public static void RotatePolygonAroundPivot(this Polygon r, Vector3 pivot, float angle)
        {
            for (int i = 0; i < r.points.Count; i++)
            {
                r.points[i].pos = r.points[i].pos.RotateAroundPivot(pivot, angle);
            }
        }

        public static Vector2 RotateAroundPivot(this Vector2 point, Vector3 pivot, float angle)
        {
            return (Quaternion.Euler(0, 0, angle) * ((Vector3)point - pivot) + pivot);
        }

        public static float Pow(this float value, float Pow) => Mathf.Pow(value, Pow);
        public static void AddItems<T>(this List<T> list, params T[] items)
        {
            list.AddRange(items);
        }

        public static T Neighbour<T>(this List<T> list, int index, int change)
        {
            var newIndex = (index + change + (change < 0? list.Count : 0)) % list.Count;
            return list[newIndex];
        }

        public static int WrapIndex<T>(this int index, int addition, List<T> list)
        {
            var wrappedIndex = index + addition;
            if (wrappedIndex >= list.Count)
            {
                wrappedIndex -= list.Count;
            }
            else if(wrappedIndex < 0)
            {
                wrappedIndex += list.Count;
            }
            return wrappedIndex;
        }

        public static T GetNeighbour<T>(this List<T> list, T point, int indexChange)
        {
            var newIndex = list.IndexOf(point).WrapIndex(indexChange, list);
            return list[newIndex];
        }

        public static T GetNeighbour<T>(this T point, int indexChange, List<T> list)
        {
            var newIndex = list.IndexOf(point).WrapIndex(indexChange, list);
            return list[newIndex];
        }

        public static int IndexDiff<T>(this List<T> list, T obj1, T obj2)
        {
            var result = Math.Abs(list.IndexOf(obj1) - list.IndexOf(obj2));
            var result2 = Math.Abs(list.Count - list.IndexOf(obj1) + list.IndexOf(obj2));
            return Math.Min(result, result2);
        }

        public static void AddRanges<T>(this List<T> list, params IEnumerable<T>[] ranges)
        {
            foreach(var range in ranges)
            {
                list.AddRange(range);
            }
        }

        public static void RemoveList<T>(this List<T> list, List<T> listToRemove)
        {
            list.RemoveAll(i => listToRemove.Contains(i));
        }

        public static List<int> DivideIntoSizes(this int sum, int min, int max, System.Random rnd)
        {
            var list = new List<int>();
            if (sum <= max)
            {
                list.Add(sum - 2);
                return list;
            }
            var remaining = sum - 2;

            do
            {
                var val = rnd.Next(min, max + 1);
                remaining -= val;
                list.Add(val);
            }
            while (remaining >= max);

            if(remaining > 0)
            {
                list.Insert(rnd.Next(list.Count), remaining);                
            }            
            return list;
        }

        public static List<PtWSgmnts> GetParallelList(this List<PtWSgmnts> list, Vector2 center, float distance, int angle)
        {            
            List<PtWSgmnts> result = new();
            for (int i = 0; i < list.Count - 1; i++)
            {
                var pt = list[i + 1].pos.RotateAroundPivot(list[i].pos, angle);
                pt = list[i].pos + (pt - list[i].pos).normalized * distance;
                result.Add(new PtWSgmnts(pt));
            }
            var lastPt = list.Last().pos.RotateAroundPivot(list.LastButOne().pos, -angle);
            result.Add(new PtWSgmnts(lastPt));

            return result;
        }

        public static T TakeOpposite<T>(this List<T> list, int index)
        {
            return list[index.WrapIndex(list.Count / 2, list)];
        }

        public static T ItemBefore<T>(this List<T> list, T item) 
        {
            var itemIndex = list.IndexOf(item);
            return list[itemIndex.WrapIndex(-1, list)];
        }

        public static T ItemAfter<T>(this List<T> list, T item)
        {
            var itemIndex = list.IndexOf(item);
            return list[itemIndex.WrapIndex(1, list)];
        }        
        
        public static List<T> Shuffle<T>(this List<T> list, System.Random rnd)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var index = rnd.Next(list.Count);
                var temp = list[index];
                list[index] = list[i];
                list[i] = temp;
            }
            return list;
        }

        public static Vector2 FindCenter(this List<PtWSgmnts> points)
        {
            Vector2 pos = Vector2.zero;
            foreach (var p in points)
            {
                pos += p.pos;
            }
            return pos / points.Count;
        }

        public static Vector2 FindCenter(this List<Vector2> points)
        {
            Vector2 pos = Vector2.zero;
            foreach (var p in points)
            {
                pos += p;
            }
            return pos / points.Count;
        }

        public static float GetLongestEdgeLength(this List<Vector2> p)
        {
            var maxLen = 0f;
            for (int i = 0; i < p.Count; i++)
            {
                var prevP = p[(i - 1 + p.Count) % p.Count];

                var len = (p[i] - prevP).magnitude;
                if (len > maxLen)
                    maxLen = len;
            }
            return maxLen;
        }

        public static List<Vector2> ReorderPointsByAngleCCW(this List<Vector2> points)
        {
            var center = points.FindCenter();
            var p0 = points[0];
            return points
                .Distinct()
                .OrderBy(p => Vector2.SignedAngle(Vector2.right, p - center)) // bardziej stabilne niż `Angle()`
                .ToList();

        }

        public static List<Vector2> ReorderPointsByAngleCW(this List<Vector2> points)
        {
            var center = points.FindCenter();
            var p0 = points[0];
            return points
                .Distinct()
                .OrderBy(p => -Vector2.SignedAngle(Vector2.right, p - center)) // bardziej stabilne niż `Angle()`
                .ToList();

        }

        public static List<PtWSgmnts> ReorderPointsByAngleCW(this List<PtWSgmnts> points, Vector2 center)
        {
            var p0 = points[0];
            return points
                .Distinct()
                .OrderBy(p => -Vector2.SignedAngle(Vector2.right, p.pos - center)) // bardziej stabilne niż `Angle()`
                .ToList();

        }


        public static List<Vector2> RotateAroundCenter(this List<Vector2> polygon, float angle)
        {
            var center = polygon.FindCenter();
            for (int i = 0; i < polygon.Count; i++)
            {
                polygon[i] = polygon[i].RotateAroundPivot(center, angle);
            }
            return polygon;
        }

        public static bool ContainsPoint(this List<Vector2> polygon, Vector2 point)
        {
            int crossings = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];

                if ((a.y > point.y) != (b.y > point.y))
                {
                    float t = (point.y - a.y) / (b.y - a.y);
                    float xCross = a.x + t * (b.x - a.x);
                    if (point.x < xCross)
                        crossings++;
                }
            }
            return (crossings % 2) == 1;
        }
    }
}

