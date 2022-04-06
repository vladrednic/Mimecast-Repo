using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Notifications {
    public abstract class NotifyBase<TConfig> : INotify
        where TConfig : INotifyConfig {
        public NotifyBase(TConfig config) {
            Config = config;
        }

        public TConfig Config { get; private set; }

        protected abstract void SendInternal();

        public void Send() {
            SendInternal();
        }
    }
}
