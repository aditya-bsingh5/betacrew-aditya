using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Configuration;

namespace betacrew_aditya.EventCollection
{
    public class EventCollection
    {
        public static string path = ConfigurationManager.AppSettings.Get("EVENTLOGPATH")?? "logdump.text";
        public static void Clear()
        {
            File.WriteAllText(path, String.Empty);
        }
        public static void Log(string message)
        {   
            TextWriter tsw = new StreamWriter(path, true); 
            
            //Writing text to the file.
            tsw.WriteLine(DateTime.Now.ToString() + ": " + message + "\n");
            tsw.Close();
        }
    }
}