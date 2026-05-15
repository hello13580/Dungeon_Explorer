public class Heap<T> where T : IHeapItem<T>
{
	private T[] items;

	private int currentItemCount;

	public int Count => currentItemCount;

	public Heap(int maxHeapSize)
	{
		items = new T[maxHeapSize];
	}

	public void Add(T item)
	{
		item.HeapIndex = currentItemCount;
		items[currentItemCount] = item;
		SortUp(item);
		currentItemCount++;
	}

	public T RemoveFirst()
	{
		T result = items[0];
		currentItemCount--;
		items[0] = items[currentItemCount];
		items[0].HeapIndex = 0;
		SortDown(items[0]);
		return result;
	}

	public void UpdateItem(T item)
	{
		SortUp(item);
	}

	public bool Contains(T item)
	{
		return object.Equals(items[item.HeapIndex], item);
	}

	private void SortDown(T item)
	{
		while (true)
		{
			int num = item.HeapIndex * 2 + 1;
			int num2 = item.HeapIndex * 2 + 2;
			int num3 = 0;
			if (num < currentItemCount)
			{
				num3 = num;
				if (num2 < currentItemCount && items[num].CompareTo(items[num2]) < 0)
				{
					num3 = num2;
				}
				if (item.CompareTo(items[num3]) < 0)
				{
					Swap(item, items[num3]);
					continue;
				}
				break;
			}
			break;
		}
	}

	private void SortUp(T item)
	{
		int num = (item.HeapIndex - 1) / 2;
		while (true)
		{
			T val = items[num];
			if (item.CompareTo(val) > 0)
			{
				Swap(item, val);
				num = (item.HeapIndex - 1) / 2;
				continue;
			}
			break;
		}
	}

	private void Swap(T itemA, T itemB)
	{
		items[itemA.HeapIndex] = itemB;
		items[itemB.HeapIndex] = itemA;
		int heapIndex = itemA.HeapIndex;
		itemA.HeapIndex = itemB.HeapIndex;
		itemB.HeapIndex = heapIndex;
	}
}
