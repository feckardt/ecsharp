/*
	VList processing library: Copyright 2008 by David Piepgrass

	This library is free software: you can redistribute it and/or modify it 
	it under the terms of the GNU Lesser General Public License as published 
	by the Free Software Foundation, either version 3 of the License, or (at 
	your option) any later version. It is provided without ANY warranties.

	If you did not receive a copy of the License with this library, you can 
	find it at http://www.gnu.org/licenses/
*/
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using NUnit.Framework;
using System.Threading;

namespace Loyc.Utilities
{
	/// <summary>
	/// RVList represents a reference to a reverse-order VList.
	/// </summary><remarks>
	/// VList is a persistent list data structure described in Phil Bagwell's 2002
	/// paper "Fast Functional Lists, Hash-Lists, Deques and Variable Length
	/// Arrays". RVList is the name I (DLP) give to a variant of this structure in
	/// which new elements are efficiently added to the end of the list rather 
	/// than to the beginning. See the remarks of <see cref="VListBlock{T}"/> for a 
	/// more detailed description.
	/// </remarks>
	public struct RVList<T> : IList<T>, ICloneable
	{
		internal VListBlock<T> _block;
		internal int _localCount;

		#region Constructors

		internal RVList(VListBlock<T> block, int localCount)
		{
			_block = block;
			_localCount = localCount;
		}
		public RVList(T firstItem)
		{
			_block = new VListBlockOfTwo<T>(firstItem);
			_localCount = 1;
		}
		public RVList(T itemZero, T itemOne)
		{
			_block = new VListBlockOfTwo<T>(itemZero, itemOne);
			_localCount = 2;
		}

		#endregion

		#region Obtaining sublists

		public RVList<T> WithoutLast(int offset)
		{
			return VListBlock<T>.SubList(_block, _localCount, offset).ToRVList();
		}
		public RVList<T> Previous
		{
			get {
				return VListBlock<T>.SubList(_block, _localCount, 1).ToRVList();
			}
		}
		public RVList<T> NextIn(RVList<T> largerList)
		{
			return VListBlock<T>.BackUpOnce(this, largerList);
		}

		#endregion

		#region Equality testing and GetHashCode()

		/// <summary>Returns whether the two list references are the same.
		/// Does not compare the contents of the lists.</summary>
		public static bool operator ==(RVList<T> lhs, RVList<T> rhs)
		{
			return lhs._localCount == rhs._localCount && lhs._block == rhs._block;
		}
		/// <summary>Returns whether the two list references are different.
		/// Does not compare the contents of the lists.</summary>
		public static bool operator !=(RVList<T> lhs, RVList<T> rhs)
		{
			return lhs._localCount != rhs._localCount || lhs._block != rhs._block;
		}
		/// <summary>Returns whether the two list references are the same.
		/// Does not compare the contents of the lists.</summary>
		public override bool Equals(object rhs_)
		{
			try {
				RVList<T> rhs = (RVList<T>)rhs_;
				return this == rhs;
			} catch {
				return false;
			}
		}
		public override int GetHashCode()
		{
			Debug.Assert((_localCount == 0) == (_block == null));
			if (_block == null)
				return 2;
			return _block.GetHashCode() ^ _localCount;
		}

		#endregion

		#region AddRange, InsertRange, RemoveRange

		public RVList<T> AddRange(RVList<T> list) { return AddRange(list, new RVList<T>()); }
		public RVList<T> AddRange(RVList<T> list, RVList<T> excludeSubList)
		{
			this = VListBlock<T>.AddRange(_block, _localCount, list.ToVList(), excludeSubList.ToVList()).ToRVList();
			return this;
		}
		public RVList<T> AddRange(IList<T> list)
		{
			this = VListBlock<T>.InsertRange(_block, _localCount, list, 0, true).ToRVList();
			return this;
		}
		public RVList<T> InsertRange(int index, IList<T> list)
		{
			this = VListBlock<T>.InsertRange(_block, _localCount, list, Count - index, true).ToRVList();
			return this;
		}
		public RVList<T> RemoveRange(int index, int count)
		{
			if (count != 0)
				this = _block.RemoveRange(_localCount, Count - (index + count), count).ToRVList();
			return this;
		}

		#endregion

		#region Other stuff

		public T Back
		{
			get {
				return _block.Front(_localCount);
			}
		}
		public bool IsEmpty
		{
			get {
				Debug.Assert((_localCount == 0) == (_block == null));
				return _block == null;
			}
		}
		public T Pop()
		{
			T item = Back;
			if (_block == null)
				throw new InvalidOperationException("Pop: The list is empty.");
			this = WithoutLast(1);
			return item;
		}
		/// <summary>Synonym for Add(); adds an item to the front of the list.</summary>
		public RVList<T> Push(T item) { return Add(item); }

		/// <summary>Returns this list as a VList, which effectively reverses the
		/// order of the elements.</summary>
		/// <returns>This is a trivial operation; the VList shares the same memory.</returns>
		public static explicit operator VList<T>(RVList<T> list)
		{
			return new VList<T>(list._block, list._localCount);
		}
		/// <summary>Returns this list as a VList, which effectively reverses the
		/// order of the elements.</summary>
		/// <returns>This is a trivial operation; the VList shares the same memory.</returns>
		public VList<T> ToVList()
		{
			return new VList<T>(_block, _localCount);
		}

		public static readonly RVList<T> Empty = new RVList<T>();

		#endregion

		#region IList<T> Members

        /// <summary>Searches for the specified object and returns the zero-based
        /// index of the first occurrence (lowest index) within the entire
        /// RVList.</summary>
        /// <param name="item">Item to locate (can be null if T can be null)</param>
        /// <returns>Index of the item, or -1 if it was not found.</returns>
        /// <remarks>This method determines equality using the default equality
        /// comparer EqualityComparer.Default for T, the type of values in the list.
        ///
        /// This method performs a linear search, and is typically an O(n)
        /// operation, where n is Count. However, because the list is searched
        /// upward from index 0 to Count-1, if the list's blocks do not increase in
        /// size exponentially (due to the way that the list has been modified in
        /// the past), the search can have worse performance; the (unlikely) worst
        /// case is O(n^2). VList(of T).IndexOf() doesn't have this problem.
        /// </remarks>
		public int IndexOf(T item)
		{
			EqualityComparer<T> comparer = EqualityComparer<T>.Default;
			int i = 0;
			foreach (T candidate in this) {
				if (comparer.Equals(candidate, item))
					return i;
				i++;
			}
			return -1;
		}

		void IList<T>.Insert(int index, T item) { Insert(index, item); }
		public RVList<T> Insert(int index, T item)
		{
			_block = VListBlock<T>.Insert(_block, _localCount, item, Count - index);
			_localCount = _block.LocalCount;
			return this;
		}

		void IList<T>.RemoveAt(int index) { RemoveAt(index); }
		public RVList<T> RemoveAt(int index)
		{
			this = _block.RemoveAt(_localCount, Count - (index + 1)).ToRVList();
			return this;
		}

		public T this[int index]
		{
			get {
				if ((uint)index >= (uint)Count)
					throw new IndexOutOfRangeException();
				return _block[_localCount - (Count - index)];
			}
			set {
				this = _block.ReplaceAt(_localCount, value, Count - 1 - index).ToRVList();
			}
		}

		#endregion

		#region ICollection<T> Members

		/// <summary>Inserts an item at the back (index Count) of the RVList.</summary>
		void ICollection<T>.Add(T item) { Add(item); }
		/// <summary>Inserts an item at the back (index Count) of the RVList.</summary>
		public RVList<T> Add(T item)
		{
			_block = VListBlock<T>.Add(_block, _localCount, item);
			_localCount = _block.LocalCount;
			return this;
		}

		void ICollection<T>.Clear() { Clear(); }
		public RVList<T> Clear()
		{
			_block = null;
			_localCount = 0;
			return this;
		}

		public bool Contains(T item)
		{
			return IndexOf(item) != -1;
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			foreach (T item in this)
				array[arrayIndex++] = item;
		}

		public int Count
		{
			get {
				Debug.Assert((_localCount == 0) == (_block == null));
				if (_block == null)
					return 0;
				return _localCount + _block.PriorCount; 
			}
		}

		public bool IsReadOnly
		{
			get { return false; }
		}

		public bool Remove(T item)
		{
			int i = IndexOf(item);
			if (i == -1)
				return false;
			RemoveAt(i);
			return true;
		}

		#endregion

		#region IEnumerable<T> Members

        /// <summary>Enumerates through an RVList from index 0 up to index Count-1.
        /// </summary><remarks>
        /// Normally, enumerating the list takes O(Count) time. However, if the
        /// list's block chain does not increase in size exponentially (due to the
        /// way that the list has been modified in the past), the search can have
        /// worse performance; the worst case is O(n^2), but this is unlikely. 
        /// VList's Enumerator doesn't have this problem because it enumerates in
		/// the other direction.</remarks>
		public struct Enumerator : IEnumerator<T>
		{
			ushort _localIndex;
			ushort _localCount;
			VListBlock<T> _curBlock;
			VListBlock<T> _nextBlock;
			VList<T> _outerList;

			public Enumerator(RVList<T> list)
				: this(new VList<T>(list._block, list._localCount), new VList<T>()) { }
			public Enumerator(RVList<T> list, RVList<T> subList)
				: this(new VList<T>(list._block, list._localCount),
					   new VList<T>(subList._block, subList._localCount)) { }
			public Enumerator(VList<T> list) : this(list, new VList<T>()) { }
			public Enumerator(VList<T> list, VList<T> subList)
			{
				_outerList = list;
				int localCount;
				_nextBlock = VListBlock<T>.FindNextBlock(ref subList, _outerList, out localCount)._block;
				_localIndex = (ushort)(checked((ushort)subList._localCount) - 1);
				_curBlock = subList._block;
				_localCount = checked((ushort)localCount);
			}

			public T Current
			{
				get { return _curBlock[_localIndex]; }
			}
			object System.Collections.IEnumerator.Current
			{
				get { return Current; }
			}
			public bool MoveNext()
			{
				if (++_localIndex >= _localCount) {
					_curBlock = _nextBlock;
					int localCount;
                    // The VList constructed here usually violates the invariant
                    // (_localCount == 0) == (_block == null), but FindNextBlock
                    // doesn't mind. It's necessary to avoid the "subList is not
                    // within list" exception in all cases.
					VList<T> subList = new VList<T>(_curBlock, 0);
					_nextBlock = VListBlock<T>.FindNextBlock(
						ref subList, _outerList, out localCount)._block;
					_localCount = checked((ushort)localCount);
					_localIndex = 0;
					return _curBlock != null;
				}
				return true;
			}
			public void Reset()
			{
				throw new NotImplementedException();
			}
			public void Dispose()
			{
			}
		}

		public Enumerator GetEnumerator()
		{
			return new Enumerator(this);
		}
		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return GetEnumerator();
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion

		#region ICloneable Members

		public RVList<T> Clone() { return this; }
		object ICloneable.Clone() { return this; }

		#endregion
	}

	[TestFixture]
	public class RVListTests
	{
		[Test]
		public void SimpleTests()
		{
			// In this simple test, I only add and remove items from the back
			// of an RVList, but forking is also tested.

			RVList<int> list = new RVList<int>();
			Assert.That(list.IsEmpty);

			// Adding to VListBlockOfTwo
			list = new RVList<int>(10, 20);
			ExpectList(list, 10, 20);

			list = new RVList<int>();
			list.Add(1);
			Assert.That(!list.IsEmpty);
			list.Add(2);
			ExpectList(list, 1, 2);

			// A fork in VListBlockOfTwo. Note that list2 will use two VListBlocks
			// here but list will only use one.
			RVList<int> list2 = list.WithoutLast(1);
			list2.Add(3);
			ExpectList(list, 1, 2);
			ExpectList(list2, 1, 3);

			// Try doubling list2
			list2.AddRange(list2);
			ExpectList(list2, 1, 3, 1, 3);

			// list now uses two arrays
			list.Add(4);
			ExpectList(list, 1, 2, 4);

			// Try doubling list using a different overload of AddRange()
			list.AddRange((IList<int>)list);
			ExpectList(list, 1, 2, 4, 1, 2, 4);
			list = list.WithoutLast(3);
			ExpectList(list, 1, 2, 4);

			// Remove(), Pop()
			Assert.AreEqual(3, list2.Pop());
			ExpectList(list2, 1, 3, 1);
			Assert.That(!list2.Remove(0));
			Assert.AreEqual(1, list2.Pop());
			Assert.That(list2.Remove(3));
			ExpectList(list2, 1);
			Assert.That(list2.Remove(1));
			ExpectList(list2);
			AssertThrows<Exception>(delegate() { list2.Pop(); });

			// Add many, SubList(). This will fill 3 arrays (sizes 8, 4, 2) and use
			// 1 element of a size-16 array. Oh, and test the enumerator.
			for (int i = 5; i <= 16; i++)
				list.Add(i);
			ExpectList(list, 1, 2, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);
			list2 = list.WithoutLast(6);
			ExpectListByEnumerator(list2, 1, 2, 4, 5, 6, 7, 8, 9, 10);
			AssertThrows<IndexOutOfRangeException>(delegate() { int i = list[-1]; });
			AssertThrows<IndexOutOfRangeException>(delegate() { int i = list[15]; });

			// IndexOf, contains
			Assert.That(list.Contains(11));
			Assert.That(!list2.Contains(11));
			Assert.That(list[list.IndexOf(2)] == 2);
			Assert.That(list[list.IndexOf(1)] == 1);
			Assert.That(list[list.IndexOf(15)] == 15);
			Assert.That(list.IndexOf(3) == -1);

			// PreviousIn(), Back
			RVList<int> list3 = list2;
			Assert.AreEqual(11, (list3 = list3.NextIn(list)).Back);
			Assert.AreEqual(12, (list3 = list3.NextIn(list)).Back);
			Assert.AreEqual(13, (list3 = list3.NextIn(list)).Back);
			Assert.AreEqual(14, (list3 = list3.NextIn(list)).Back);
			Assert.AreEqual(15, (list3 = list3.NextIn(list)).Back);
			Assert.AreEqual(16, (list3 = list3.NextIn(list)).Back);
			AssertThrows<Exception>(delegate() { list3.NextIn(list); });

			// Next
			Assert.AreEqual(10, (list3 = list3.WithoutLast(6)).Back);
			Assert.AreEqual(9, (list3 = list3.Previous).Back);
			Assert.AreEqual(8, (list3 = list3.Previous).Back);
			Assert.AreEqual(7, (list3 = list3.Previous).Back);
			Assert.AreEqual(6, (list3 = list3.Previous).Back);
			Assert.AreEqual(5, (list3 = list3.Previous).Back);
			Assert.AreEqual(4, (list3 = list3.Previous).Back);
			Assert.AreEqual(2, (list3 = list3.Previous).Back);
			Assert.AreEqual(1, (list3 = list3.Previous).Back);
			Assert.That((list3 = list3.Previous).IsEmpty);

			// list2 is still the same
			ExpectList(list2, 1, 2, 4, 5, 6, 7, 8, 9, 10);

			// ==, !=, Equals(), AddRange(a, b)
			Assert.That(!list2.Equals("hello"));
			list3 = list2;
			Assert.That(list3.Equals(list2));
			Assert.That(list3 == list2);
			// This AddRange forks the list. List2 ends up with block sizes 8 (3
			// used), 8 (3 used), 4, 2.
			list2.AddRange(list2, list2.WithoutLast(3));
			ExpectList(list2, 1, 2, 4, 5, 6, 7, 8, 9, 10, 8, 9, 10);
			Assert.That(list3 != list2);

			// List3 is a sublist of list, but list2 no longer is
			Assert.That(list3.NextIn(list).Back == 11);
			AssertThrows<InvalidOperationException>(delegate() { list2.NextIn(list); });

			list2 = list2.WithoutLast(3);
			Assert.That(list3 == list2);
		}

		private void AssertThrows<Type>(TestDelegate @delegate)
		{
			try {
				@delegate();
			} catch (Exception exc) {
				Assert.IsInstanceOf<Type>(exc);
				return;
			}
			Assert.Fail("Delegate did not throw '{0}' as expected.", typeof(Type).Name);
		}

		private static void ExpectList<T>(IList<T> list, params T[] expected)
		{
			Assert.AreEqual(expected.Length, list.Count);
			for (int i = 0; i < expected.Length; i++)
				Assert.AreEqual(expected[i], list[i]);
		}
		private static void ExpectListByEnumerator<T>(IList<T> list, params T[] expected)
		{
			Assert.AreEqual(expected.Length, list.Count);
			int i = 0;
			foreach (T item in list) {
				Assert.AreEqual(expected[i], item);
				i++;
			}
		}

		[Test]
		public void TestInsertRemove()
		{
			RVList<int> list = new RVList<int>(9);
			RVList<int> list2 = new RVList<int>(10, 11);
			list.Insert(0, 12);
			list.Insert(1, list2[1]);
			list.Insert(2, list2[0]);
			ExpectList(list, 12, 11, 10, 9);
			for (int i = 0; i < 9; i++)
				list.Insert(4, i);
			ExpectList(list, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0);

			list2 = list;
			for (int i = 1; i <= 6; i++)
				list2.RemoveAt(i);
			ExpectList(list2, 12, 10, 8, 6, 4, 2, 0);
			ExpectList(list, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0); // unchanged

			Assert.AreEqual(0, list2.Pop());
			list2.Insert(5, -2);
			ExpectList(list2, 12, 10, 8, 6, 4, -2, 2);
			list2.Insert(5, -1);
			ExpectList(list2, 12, 10, 8, 6, 4, -1, -2, 2);

			// Test changing items
			list = list2;
			for (int i = 0; i < list.Count; i++)
				list[i] = i;
			ExpectList(list, 0, 1, 2, 3, 4, 5, 6, 7);
			ExpectList(list2, 12, 10, 8, 6, 4, -1, -2, 2);

			list2.Clear();
			ExpectList(list2);
			Assert.AreEqual(5, list[5]);
		}

		[Test]
		public void TestInsertRemoveRange()
		{
			RVList<int> oneTwo = new RVList<int>(1, 2);
			RVList<int> threeFour = new RVList<int>(3, 4);
			RVList<int> list = oneTwo;
			RVList<int> list2 = threeFour;

			ExpectList(list, 1, 2);
			list.InsertRange(1, threeFour);
			ExpectList(list, 1, 3, 4, 2);
			list2.InsertRange(2, oneTwo);
			ExpectList(list2, 3, 4, 1, 2);

			list.RemoveRange(1, 2);
			ExpectList(list, 1, 2);
			list2.RemoveRange(2, 2);
			ExpectList(list2, 3, 4);

			list.RemoveRange(0, 2);
			ExpectList(list);
			list2.RemoveRange(1, 1);
			ExpectList(list2, 3);

			list = oneTwo;
			list.AddRange(threeFour);
			ExpectList(list, 1, 2, 3, 4);
			list.InsertRange(1, list);
			ExpectList(list, 1, 1, 2, 3, 4, 2, 3, 4);
			list.RemoveRange(1, 1);
			list.RemoveRange(4, 3);
			ExpectList(list, 1, 2, 3, 4);

			list.RemoveRange(0, 4);
			ExpectList(list);

			list2.InsertRange(0, list);
			list2.InsertRange(1, list);
			ExpectList(list2, 3);
		}

		[Test]
		public void TestEmptyListOperations()
		{
			RVList<int> a = new RVList<int>();
			RVList<int> b = new RVList<int>();
			a.AddRange(b);
			a.InsertRange(0, b);
			a.RemoveRange(0, 0);
			Assert.That(!a.Remove(0));
			Assert.That(a.IsEmpty);

			a.Add(1);
			b.AddRange(a);
			ExpectList(b, 1);
			b.RemoveAt(0);
			Assert.That(b.IsEmpty);
			b.InsertRange(0, a);
			ExpectList(b, 1);
			b.RemoveRange(0, 1);
			Assert.That(b.IsEmpty);
			b.Insert(0, a[0]);
			ExpectList(b, 1);
			b.Remove(a.Back);
			Assert.That(b.IsEmpty);
		}

		[Test]
		void TestAddRangePair()
		{
			RVList<int> list = new RVList<int>();
			RVList<int> list2 = new RVList<int>();
			list2.AddRange(new int[] { 1, 2, 3, 4 });
			list.AddRange(list2, list2.WithoutLast(1));
			list.AddRange(list2, list2.WithoutLast(2));
			list.AddRange(list2, list2.WithoutLast(3));
			list.AddRange(list2, list2.WithoutLast(4));
			ExpectList(list, 1, 2, 3, 1, 2, 1);

			AssertThrows<InvalidOperationException>(delegate() { list2.AddRange(list2.WithoutLast(1), list2); });
			AssertThrows<InvalidOperationException>(delegate() { list2.AddRange(RVList<int>.Empty, list2); });
		}
		
		[Test]
		public void TestSublistProblem()
		{
			// This problem affects VList.PreviousIn(), RVList.NextIn(),
			// AddRange(list, excludeSubList), RVList.Enumerator when used with a
			// range.

			// Normally this works fine:
			RVList<int> subList = new RVList<int>(), list;
			subList.AddRange(new int[] { 1, 2, 3, 4, 5, 6, 7 });
			list = subList;
			list.Add(8);
			Assert.That(subList.NextIn(list).Back == 8);

			// But try it a second time and the problem arises, without some special
			// code in VListBlock<T>.FindNextBlock() that has been added to
			// compensate. I call the problem copy-causing-sharing-failure. You see,
			// right now subList is formed from three blocks: a size-8 block that
			// contains {7}, a size-4 block {3, 4, 5, 6} and a size-2 block {1, 2}.
			// But the size-8 block actually has two items {7, 8} and when we
			// attempt to add 9, a new array must be created. It might waste a lot
			// of memory to make a new block {9} that links to the size-8 block that
			// contains {7}, so instead a new size-8 block {7, 9} is created that
			// links directly to {3, 4, 5, 6}. That way, the block {7, 8} can be
			// garbage-collected if it is no longer in use. But a side effect is
			// that subList no longer appears to be a part of list. The fix is to
			// notice that list (block {7, 9}) and subList (block that contains {7})
			// have the same prior list, {3, 4, 5, 6}, and that the remaining 
			// item(s) in subList (just one item, {7}, in this case) are also
			// present in list.
			list = subList;
			list.Add(9);
			Assert.AreEqual(9, subList.NextIn(list).Back);
		}
	}
}