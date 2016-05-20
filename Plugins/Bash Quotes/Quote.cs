namespace BashQuotes {
    public struct Quote {
        public string Text;
        public int Rating;

        public Quote(string text) {
            this.Text = text;
            this.Rating = 0;
        }
        public Quote(string text, int rating) {
            this.Text = text;
            this.Rating = rating;
        }
    }
}
