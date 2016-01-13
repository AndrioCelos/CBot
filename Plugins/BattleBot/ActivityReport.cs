using System;
using System.Collections.ObjectModel;

namespace BattleBot {
    public class ActivityReport {
        private int[] data;
        private int[] lastData;

        public ReadOnlyCollection<int> Data { get; private set; }
        public ReadOnlyCollection<int> LastData { get; private set; }

        internal DateTime LastCheck;

        public ActivityReport() {
            this.data = new int[168];
            this.Data = new ReadOnlyCollection<int>(this.data);
            this.LastCheck = DateTime.UtcNow;
        }
        public ActivityReport(int[] data, int[] lastData, DateTime lastCheck) {
            this.data = new int[168];
            Array.Copy(data, this.data, data.Length);
            this.Data = new ReadOnlyCollection<int>(this.data);
            if (lastData != null) {
                this.lastData = new int[168];
                Array.Copy(lastData, this.lastData, lastData.Length);
                this.LastData = new ReadOnlyCollection<int>(this.lastData);
            }
            this.LastCheck = lastCheck;
        }

        internal void AddSeconds(int index, int minutes) {
            this.data[index] += minutes;
        }

        internal void RollOver() {
            this.lastData = this.data;
            this.LastData = new ReadOnlyCollection<int>(this.lastData);
            this.data = new int[168];
            this.Data = new ReadOnlyCollection<int>(this.data);
        }
    }
}
