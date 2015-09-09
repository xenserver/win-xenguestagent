using System;
using System.Diagnostics;

namespace xenwinsvc
{
    public class EventLogger
    {
        static EventLog el;
        static EventLogger()
        {
            el = new EventLog();
            el.Source = "XenGuestAgent";
            if (!EventLog.SourceExists("XenGuestAgent"))
            {
                //Create New Log       
                EventLog.CreateEventSource("XenGuestAgent", "XenGuestAgentLog");
            }
        }

        public static void addEvent(string message)
        {
            //Log Information  
            el.WriteEntry(message, EventLogEntryType.Information);
        }

        public static void addException(string message)
        {
            //Log Exception  
            el.WriteEntry(message, EventLogEntryType.Error);
        }

        public static void addWarning(string message)
        {
            //Log Warning  
            el.WriteEntry(message, EventLogEntryType.Warning);
        }
    }
}
