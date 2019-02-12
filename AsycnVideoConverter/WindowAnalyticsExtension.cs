using Garlic;
using System;
using System.Collections.Generic;
using System.Windows;

namespace AsycnVideoConverter
{
    public static class WindowAnalyticsExtension
    {
        private static readonly IDictionary<Window, EventHandler> windowDictionary = new Dictionary<Window, EventHandler>();

        public static void Track(this IAnalyticsPageViewRequest request, Window window)
        {
            EventHandler eventHandler = new EventHandler((s, e) => ScreenActivated(request));
            windowDictionary.Add(window, eventHandler);
            window.Activated += eventHandler;
        }

        public static void UnTrack(this IAnalyticsPageViewRequest request, Window window)
        {
            window.Activated -= windowDictionary[window];
            windowDictionary.Remove(window);
        }

        private static void ScreenActivated(IAnalyticsPageViewRequest request)
        {
            request.Send();
        }
    }
}
