using System;
using System.Collections;
using System.Collections.Generic;

namespace GraphSharp.Editor
{
	public class Selection : IReadOnlyCollection<NodeWidget>
	{
		HashSet<NodeWidget> m_items = new HashSet<NodeWidget>();

		#region IReadOnlyCollection

		public int Count => m_items.Count;

		public IEnumerator<NodeWidget> GetEnumerator() => m_items.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => m_items.GetEnumerator();

		#endregion

		public event Action<Selection> OnChanged;

		public bool Contains(NodeWidget nodeWidget) => m_items.Contains(nodeWidget);

		public bool Clear()
		{
			if (m_items.Count == 0)
				return false;

			foreach (var item in m_items)
				item.Selected = false;

			m_items.Clear();

			OnChanged?.Invoke(this);
			return true;
		}

		public bool Add(NodeWidget nodeWidget)
		{
			if (m_items.Add(nodeWidget))
			{
				nodeWidget.Selected = true;
				OnChanged?.Invoke(this);
				return true;
			}
			else
				return false;
		}

		public bool Remove(NodeWidget nodeWidget)
		{
			if (m_items.Remove(nodeWidget))
			{
				nodeWidget.Selected = false;
				OnChanged?.Invoke(this);
				return true;
			}
			else
				return false;
		}

		public bool Set(NodeWidget nodeWidget)
		{
			if (m_items.Contains(nodeWidget))
			{
				if (m_items.Count == 1)
					return false;

				foreach (var item in m_items)
				{
					if (item != nodeWidget)
						item.Selected = false;
				}

				m_items.RemoveWhere(item => !item.Selected);
			}
			else
			{
				Clear();

				m_items.Add(nodeWidget);
				nodeWidget.Selected = true;
			}

			OnChanged?.Invoke(this);
			return true;
		}

		public bool Set(IEnumerable<NodeWidget> nodeWidgets)
		{
			foreach (var item in m_items)
				item.Selected = false;

			bool changed = false;
			foreach (var nodeWidget in nodeWidgets)
			{
				if (m_items.Add(nodeWidget))
					changed = true;

				nodeWidget.Selected = true;
			}

			int removed = m_items.RemoveWhere(item => !item.Selected);
			if (removed > 0)
				changed = true;

			if (changed)
				OnChanged?.Invoke(this);

			return changed;
		}
	}
}
