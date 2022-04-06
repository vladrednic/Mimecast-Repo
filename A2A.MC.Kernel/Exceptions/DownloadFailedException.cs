using A2A.MC.Kernel.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Kernel.Exceptions {
    public class DownloadFailedException : ApplicationException {
        public DownloadFailedException(SubSearchFile file, string message) : this(file, message, null) { }
        public DownloadFailedException(SubSearchFile file, string message, Exception innerException) : base(message, innerException) {
            SubSearchFile = file;
        }

        public SubSearchFile SubSearchFile { get; private set; }
    }
}
