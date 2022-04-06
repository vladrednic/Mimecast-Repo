using A2A.BasicScheduler;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using System.Threading.Tasks;

namespace A2A.BasicCore.Service {
    public partial class AutomationService : ServiceBase {
        public AutomationService() {
            InitializeComponent();
        }

        Timer _tPulse;
        SchedulerBase<SchedulerInterval<object>> _sched;
        bool _run = true;
        object _processing = false;

        protected override void OnStart(string[] args) {
            _tPulse = new Timer(1000);
            _tPulse.Elapsed += _tPulse_Elapsed;
            _tPulse.Start();
            while (_run) {
                System.Threading.Thread.Sleep(1000);
            }
        }

        private void _tPulse_Elapsed(object sender, ElapsedEventArgs e) {
            DoTimer();
        }

        protected void DoTimer() {
            lock (_processing) {
                if ((bool)_processing) {
                    return;
                }
            }
            _processing = true;
            try {
                if (_sched == null) {
                    var json = Properties.Settings.Default.SchedulerDataJson;
                    _sched = new SchedulerBase<SchedulerInterval<object>>();
                    _sched.LoadDefinition(json);
                }
                var data = _sched.GetInterval();
            }
            finally {
                _processing = false;
            }
        }

        protected override void OnStop() {
            _run = false;
            if (_tPulse != null) {
                _tPulse.Dispose();
                _tPulse = null;
            }
        }
    }
}
