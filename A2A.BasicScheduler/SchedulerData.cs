using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.BasicScheduler {
    public class SchedulerData<TIntervalData>
        where TIntervalData : new() {
        public string DefaultData { get; set; }
        public SchedulerInterval<TIntervalData>[] RestrictedIntervals { get; set; }

        public bool FallsInto(DateTime dt) {
            var date = dt;
            return false;
        }

        public string GetInterval(DateTime? dt) {
            if (!dt.HasValue)
                dt = DateTime.Now;
            foreach (var interval in RestrictedIntervals) {
                var contains = interval.ContainsDate(dt.Value);
                if (contains)
                    return interval.Data != null ? interval.Data.ToString() : null;
            }
            return DefaultData;
        }
    }
}
