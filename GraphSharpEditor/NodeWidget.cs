using System;
using System.Drawing;

namespace GraphSharp.Editor
{
	public partial class NodeWidget
	{
		public GraphView View { get; private set; }
		public Node Node { get; private set; }
		public string Name { get; private set; }

		public Point Location { get; internal set; }
		public bool Selected { get; internal set; }
		Drawing m_drawing;
		Bitmap m_visualImage;

		internal NodeWidget(GraphView view, Node node)
		{
			View = view;
			Node = node;
			Name = node.GetType().Name.FormatName("Node");

			Node.UserData = this;

			Node.OnLink += OnLinkChanged;
			Node.OnUnlink += OnLinkChanged;

			if (node is IVisualOutPort)
				Node.OnOutValuesChanged += OnUpdateVisualImage;

			if (View.AutoEvaluation)
				TryEvalute();
		}

		void OnUpdateVisualImage(Node node)
		{
			if (node != Node)
				throw new Exception($"Unexcepted node instance '{node}'");

			var visual = (IVisualOutPort)Node;
			m_visualImage = visual.UpdateVisualOutPort(m_visualImage);
		}

		void OnLinkChanged(OutPort selfOut, InPort otherIn)
		{
			if (View.AutoEvaluation)
			{
				var nodeWidget = (NodeWidget)otherIn.Owner.UserData;
				if (nodeWidget != null)
					nodeWidget.TryEvalute();
			}
		}

		void TryEvalute()
		{
			try
			{
				Node.Evaluate();
			}
			catch
			{
				m_visualImage?.Dispose();
				m_visualImage = null;
			}

			foreach (var port in Node.OutPorts)
			{
				foreach (var ep in port.EndPorts)
				{
					var nodeWidget = (NodeWidget)ep.Owner.UserData;
					nodeWidget.TryEvalute();
				}
			}
		}

		#region Draw

		internal void PrepareDrawing(Graphics g)
		{
			if (m_drawing == null)
				m_drawing = new Drawing(this, g);
		}

		public void DrawLinks(Graphics g)
		{
			m_drawing.DrawLinks(g);
		}

		public void DrawNode(Graphics g)
		{
			m_drawing.DrawNode(g);
		}

		#endregion

		#region Helper

		internal bool HitTest(Point location) => m_drawing.Rect.Contains(location);

		internal bool Intersect(Rectangle rect) => m_drawing.Rect.IntersectsWith(rect);

		internal enum PinType
		{
			In, Out, All
		}

		internal Port PinHitTest(Point location, PinType pinType, out PointF pinLocation) => m_drawing.PinHitTest(location, pinType, out pinLocation);

		#endregion
	}
}
