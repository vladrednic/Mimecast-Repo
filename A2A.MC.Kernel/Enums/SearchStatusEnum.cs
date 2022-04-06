using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Kernel.Entities {
    public enum SearchStatusEnum : byte {
        Pending = 0,
        Success = 1,
        CreateSearchFailed = 2,
        SearchCreated = 3,
        ExportCreated = 4,
        CreateExportError = 5,
        DownloadFailed = 6,
        ExportCreateFail = 7
    }
}
