using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Extensions.Logging;

namespace Dwarf.Utils;

[System.Serializable]
public unsafe struct Node<T> {
  public T Data;
  public Node<T>* Next;

  public static Node<T>* CreateNode(T data) {
    Node<T> newNode = new();
    newNode.Data = data;
    newNode.Next = null;
    return &newNode;
  }

  public static void Insert(Node<T>* head, T data) {
    var newNode = CreateNode(data);
    newNode->Next = head;
    head = newNode;
  }
}

public unsafe class UnmanagedList<T> {
  private Node<T>* _head = null!;

  public void Add(T data) {
    if (_head == null) {
      _head = Node<T>.CreateNode(data);
    } else {
      Node<T>.Insert(_head, data);
    }
  }

  public T? GetAtIndex(int index) {
    int i = 0;
    var current = _head;
    while (current != null) {
      if (i == index) {
        return current->Data;
      }
      current = current->Next;
      i++;
    }
    var notFound = new Node<T>();
    return notFound.Data;
  }
}
