using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenQA.Selenium {
    public static class IWebElementExtension {
        public static void SendKeysSlow(this IWebElement elem, string text, int delayMs = 100) {
            foreach (var c in text) {
                Thread.Sleep(100);
                elem.SendKeys(c.ToString());
            }
        }
    }
}
