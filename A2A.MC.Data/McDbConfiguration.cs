using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.SqlServer;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Data {
    public class McDbConfiguration : DbConfiguration {
        public McDbConfiguration() {
            SetProviderServices("System.Data.SqlClient", SqlProviderServices.Instance);
        }
    }
}
