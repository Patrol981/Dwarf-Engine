namespace Dwarf.Extensions.Lists;

public class PublicList<T> {
  private T[] _data;
  private int _size = 0;
  private int _capacity;

  private readonly object _lock = new object();

  public PublicList(int initialCapacity = 8) {
    if (initialCapacity < 1) initialCapacity = 1;
    _capacity = initialCapacity;
    _data = new T[initialCapacity];
  }

  public int Size { get { return _size; } }
  public bool IsEmpty { get { return _size == 0; } }

  public T GetAt(int index) {
    ThrowIfIndexOutOfRange(index);
    return _data[index];
  }

  public void SetAt(T newElement, int index) {
    ThrowIfIndexOutOfRange(index);
    _data[index] = newElement;
  }

  public void InsertAt(T newElement, int index) {
    lock (_lock) {
      ThrowIfIndexOutOfRange(index);
      if (_size == _capacity) {
        Resize();
      }

      for (int i = _size; i > index; i--) {
        _data[i] = _data[i - 1];
      }

      _data[index] = newElement;
      _size++;
    }
  }

  public void DeleteAt(int index) {
    lock (_lock) {
      ThrowIfIndexOutOfRange(index);
      for (int i = index; i < _size - 1; i++) {
        _data[i] = _data[i + 1];
      }

      _data[_size - 1] = default(T)!;
      _size--;
    }
  }

  public void Add(T newElement) {
    lock (_lock) {
      if (_size == _capacity) {
        Resize();
      }

      _data[_size] = newElement;
      _size++;
    }
  }

  public bool Contains(T value) {
    for (int i = 0; i < _size; i++) {
      T currentValue = _data[i];
      if (currentValue!.Equals(value)) {
        return true;
      }
    }
    return false;
  }

  public void Clear() {
    _data = new T[_capacity];
    _size = 0;
  }

  private void Resize() {
    lock (_lock) {
      T[] resized = new T[_capacity * 2];
      for (int i = 0; i < _capacity; i++) {
        resized[i] = _data[i];
      }
      _data = resized;
      _capacity = _capacity * 2;
    }
  }

  private void ThrowIfIndexOutOfRange(int index) {
    if (index > _size - 1 || index < 0) {
      throw new ArgumentOutOfRangeException(string.Format("The current size of the array is {0}", _size));
    }
  }

  public T[] GetData() => _data;
}