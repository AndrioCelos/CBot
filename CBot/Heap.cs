using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBot {
	public class Heap<T> {
		public IComparer<T> Comparer { get; }
		private List<T> list;
	
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

		private static int childIndex(int parentIndex) => 2 * parentIndex + 1;
		private static int parentIndex(int childIndex) => (childIndex - 1) / 2;
		private void swap(int index1, int index2) {
			T item = this.list[index1];
			this.list[index1] = this.list[index2];
			this.list[index2] = item;
		}
		private int compare(int index1, int index2) => this.Comparer.Compare(this.list[index1], this.list[index2]);

		public int Count => this.list.Count;

		public void Enqueue(T item) {
			int index = this.list.Count;
			this.list.Add(item);
			while (index > 0) {
				int parentIndex = Heap<T>.parentIndex(index);
				if (this.compare(index, parentIndex) <= 0) break;

				this.swap(index, parentIndex);
				index = parentIndex;
			}
		}

		public T Peek() => this.list[0];

		public T Dequeue() {
			var result = this.list[0];
			this.list[0] = this.list[this.list.Count - 1];

			int index = 0;
			while (true) {
				var childIndex = Heap<T>.childIndex(index);
				if (childIndex >= this.list.Count) break;

				if (this.compare(index, childIndex) < 0) {
					// Find the higher child.
					if (this.compare(childIndex + 1, childIndex) > 0) ++childIndex;
					// Swap the parent with that child.
					this.swap(index, childIndex);
					index = childIndex;
				} else if (this.compare(index, childIndex + 1) < 0) {
					this.swap(index, childIndex + 1);
					index = childIndex + 1;
				} else
					break;
			}

			return result;
		}

		public void Clear() => this.list.Clear();
	}
}
