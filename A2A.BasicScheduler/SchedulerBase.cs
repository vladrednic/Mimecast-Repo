using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.BasicScheduler {
    public class SchedulerBase<TIntervalData>
        where TIntervalData : new() {

        public SchedulerData<object> SchedulerData { get; set; }

        public virtual void LoadDefinition(string text) {
            var value = JsonConvert.DeserializeObject<SchedulerData<object>>(text);
            SchedulerData = value;
        }
        public virtual string SaveDefinition() {
            if (SchedulerData == null) return null;
            var value = JsonConvert.SerializeObject(SchedulerData);
            return value;
        }

        public virtual string GetInterval(DateTime? dt = null) {
            if (!dt.HasValue)
                dt = DateTime.Now;

            return SchedulerData.GetInterval(dt);
        }
    }
}
