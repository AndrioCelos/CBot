using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BashQuotes {
    public abstract class QuoteSource {
        public abstract string Name { get; }
        public abstract Task<SortedDictionary<int, Quote>> GetQuotes();
    }
}
