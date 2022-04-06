using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Kernel.Exceptions {
    public class A2ABreakingException : ApplicationException {
        public A2ABreakingException(string message = null, Exception innerException = null) : base(message, innerException) { }
    }
}
