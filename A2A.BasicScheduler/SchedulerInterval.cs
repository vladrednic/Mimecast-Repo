using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.BasicScheduler {
    public class SchedulerInterval<TIntervalData>
        where TIntervalData : new() {
        public DayOfWeek? StartDay { get; set; }
        public DayOfWeek? EndDay { get; set; }

        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }

        public TIntervalData Data { get; set; }

        public bool ContainsDate(DateTime dt) {
            return false;
        }
    }
}
