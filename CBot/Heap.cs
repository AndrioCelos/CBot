namespace CBot;
public class Heap<T> {
	public IComparer<T> Comparer { get; }
	private readonly List<T> list;

	public Heap() : this(Comparer<T>.Default) { }
	public Heap(int capacity) : this(capacity, Comparer<T>.Default) { }
	public Heap(IEnumerable<T> collection) : this(collection, Comparer<T>.Default) { }
	public Heap(IComparer<T> comparer) {
		this.list = new List<T>();
		this.Comparer = comparer;
	}
	public Heap(int capacity, IComparer<T> comparer) {
		this.list = new List<T>(capacity);
		this.Comparer = comparer;
	}
	public Heap(IEnumerable<T> collection, IComparer<T> comparer) {
		this.list = new List<T>(collection);
		this.Comparer = comparer;
	}

	private static int GetChildIndex(int parentIndex) => 2 * parentIndex + 1;
	private static int GetParentIndex(int childIndex) => (childIndex - 1) / 2;
	private void Swap(int index1, int index2) {
		var item = this.list[index1];
		this.list[index1] = this.list[index2];
		this.list[index2] = item;
	}
	private int Compare(int index1, int index2) => this.Comparer.Compare(this.list[index1], this.list[index2]);

	public int Count => this.list.Count;

	public void Enqueue(T item) {
		int index = this.list.Count;
		this.list.Add(item);
		while (index > 0) {
			int parentIndex = Heap<T>.GetParentIndex(index);
			if (this.Compare(index, parentIndex) <= 0) break;

			this.Swap(index, parentIndex);
			index = parentIndex;
		}
	}

	public T Peek() => this.list[0];

	public T Dequeue() {
		var result = this.list[0];
		this.list[0] = this.list[^1];

		int index = 0;
		while (true) {
			var childIndex = GetChildIndex(index);
			if (childIndex >= this.list.Count) break;

			if (this.Compare(index, childIndex) < 0) {
				// Find the higher child.
				if (this.Compare(childIndex + 1, childIndex) > 0) ++childIndex;
				// Swap the parent with that child.
				this.Swap(index, childIndex);
				index = childIndex;
			} else if (this.Compare(index, childIndex + 1) < 0) {
				this.Swap(index, childIndex + 1);
				index = childIndex + 1;
			} else
				break;
		}

		return result;
	}

	public void Clear() => this.list.Clear();
}
