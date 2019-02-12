using Garlic;
using System.Windows;
using System.Windows.Controls;

namespace AsycnVideoConverter
{
    class Analytics
    {
        private const string DOMAIN = "http://www.visualport.kr";
        private const string GACODE = "UA-99223726-1";
        private static AnalyticsSession Session { get; } = new AnalyticsSession(DOMAIN, GACODE);

        public static IAnalyticsPageViewRequest SendFormViewReport(Window window)
        {
            string name = window.Title;
            var page = Session.CreatePageViewRequest(name, "");
            page.Track(window);
            return page;
        }

        public static void SendFiredEventReport(Window window, Button button)
        {
            var pageViewRequest = SendFormViewReport(window);
            pageViewRequest.SendEvent(button.Name, "click", "", "");
        }
        
    }
}
