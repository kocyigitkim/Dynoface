using System;
using System.Collections.Generic;


namespace Dynoface
{
    public class DynamicEventHandler
    {
        public delegate void EventHandler(DynamicArguments args);

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public event Action<EventHandler, Exception> Error;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private List<EventHandler> registeredDelegates = new List<EventHandler>();
        public void Add(EventHandler dlg)
        {
            if (!registeredDelegates.Contains(dlg)) registeredDelegates.Add(dlg);
        }
        public void Remove(EventHandler dlg)
        {
            var i = registeredDelegates.IndexOf(dlg);
            if (i > -1) registeredDelegates.RemoveAt(i);
        }
        public void Clear()
        {
            registeredDelegates.Clear();
        }
        public void Execute(DynamicArguments args)
        {
            foreach (var dlg in registeredDelegates)
            {
                try
                {
                    dlg?.Invoke(args);
                }
                catch (Exception err)
                {
                    Error?.Invoke(dlg, err);
                }
            }
        }

        public static DynamicEventHandler operator +(DynamicEventHandler eventHandler, EventHandler handler)
        {
            eventHandler.Add(handler);
            return eventHandler;
        }
        public static DynamicEventHandler operator -(DynamicEventHandler eventHandler, EventHandler handler)
        {
            eventHandler.Remove(handler);
            return eventHandler;
        }
    }
}
