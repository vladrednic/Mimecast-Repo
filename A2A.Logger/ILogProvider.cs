using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Logger {
    public interface ILogProvider {
        void Info(string text);
        void Error(string text);
        void Error(Exception ex);
    }
}
