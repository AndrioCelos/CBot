using System;
using System.Collections.Generic;

namespace UNO {
    public class GameRecord {
        public int version = 1;

        public DateTime time;
        public TimeSpan duration;
        public List<object> shuffles;

        public class Shuffle {
            public string[] cards;
            public object random;
            public string signature;
        }
        
        public class FailedShuffle {
            public string[] cards;
            public string error;
        }
    }
}
