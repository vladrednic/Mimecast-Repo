using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Kernel.Exceptions {
    public class A2ASearchCountMismatch : A2ABreakingException {
        //for now, this is a breaking exception
        //in the future, a procedure to reconcile the number of searches in Mimecast vs the searches in database should be put in place
        //when this will be implemented, the base exception should be changed from A2ABreakingException to ApplicationException!
        public A2ASearchCountMismatch(string message = null, Exception innerException = null) : base(message, innerException) { }
    }
}
