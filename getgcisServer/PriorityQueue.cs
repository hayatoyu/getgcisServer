using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace getGcisServer
{
    class PriorityQueue<T>
    {
        IComparer<T> comparer;
        T[] heap;
        public int count { private set; get; }

        public PriorityQueue() : this(null) { }

        public PriorityQueue(int capacity) : this(capacity, null) { }

        public PriorityQueue(IComparer<T> comparer) : this(16, comparer) { }

        public PriorityQueue(int capacity, IComparer<T> compaere)
        {
            this.comparer = (comparer == null) ? Comparer<T>.Default : comparer;
            this.heap = new T[capacity];
        }

        public void push(T input)
        {
            if (count >= heap.Length)
                Array.Resize(ref heap, count * 2);
            heap[count] = input;
            SiftUp(count++);
        }

        public T Pop()
        {
            var v = Top();
            heap[0] = heap[--count];
            if (count > 0)
                SiftDown(0);
            return v;
        }

        public T Top()
        {
            if (count > 0)
                return heap[0];
            throw new InvalidOperationException("Priority Queue is Empty");
        }

        public bool Peep()
        {
            return count > 0;
        }

        private void SiftUp(int n)
        {
            var v = heap[n];
            for (var n2 = n / 2; (n > 0 && comparer.Compare(v, heap[n2]) > 0); n = n2, n2 /= 2)
            {
                heap[n] = heap[n2];
            }
            heap[n] = v;
        }

        private void SiftDown(int n)
        {
            var v = heap[n];
            for (var n2 = n * 2; n2 < count; n = n2, n2 *= 2)
            {
                if (n2 + 1 < count && comparer.Compare(heap[n2 + 1], heap[n2]) > 0)
                    n2++;
                if (comparer.Compare(v, heap[n2]) >= 0)
                    break;
                heap[n] = heap[n2];
            }
            heap[n] = v;
        }

    }
}
