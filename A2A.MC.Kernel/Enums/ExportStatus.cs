using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Kernel.Entities {
    public enum ExportStatus {
        /// <summary>
        /// no search defined yet
        /// </summary>
        None = -1,
        SearchPending = 0,
        SearchCreated = 1,
        ExportCompleted = 2,
        ActiveExport = 3,
        Downloadable = 4,
        Preparing = 5,
        ExportCreated = 6,
        PreparationPending = 7,
        ExportCanceled = 8
    }
}
