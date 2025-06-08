using System;
using UnityEngine;

//using System.Diagnostics;
using System.IO;

namespace Assets.Game.Scripts.Utility.NotAccessible
{

    public static class Log
    {
        static string fileName = $"log_{DateTime.Now.ToFileTime()}.txt";

        public static void Info(object text)
        {
            Debug.Log(text.ToString());
            EnterLine(text.ToString(), "INFO");
        }
        public static void Warning(object text)
        {
            Debug.LogWarning(text.ToString());
            EnterLine(text.ToString(), "WARN");
        }
        public static void Error(object text)
        {
            Debug.LogError(text.ToString());
            EnterLine(text.ToString(), "ERROR");
        }

        private static void EnterLine(string text, string type)
        {            
            StreamWriter sr = new StreamWriter(fileName, true);
            sr.WriteLine($"{DateTime.Now}   {type}  {text}");
            sr.Close();
        }
    }
}