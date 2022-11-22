using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace GraphSharp.Editor
{
	public interface IVisualNode
	{
		IVisualization CreateVisualization();
	}


	public interface IVisualization
	{
		Bitmap Draw(Node node, Bitmap lastImage);
	}


	public static class VisualNodeHelpers
	{
		public static Bitmap CreateImageAsNecessary(Bitmap lastImage, int width, int height)
		{
			if (lastImage == null || lastImage.Width != width || lastImage.Height != height)
			{
				if (lastImage != null)
					lastImage.Dispose();

				return new Bitmap(width, height, PixelFormat.Format24bppRgb);
			}
			else
				return lastImage;
		}
	}


	public abstract class VisualOutPort : IVisualization
	{
		readonly OutPort m_outPort;

		protected VisualOutPort(OutPort outPort)
		{
			if (outPort == null)
				throw new ArgumentNullException($"Invalid visual out port", nameof(outPort));

			m_outPort = outPort;
		}

		#region IVisualization

		public Bitmap Draw(Node node, Bitmap lastImage)
		{
			if (m_outPort.Owner != node)
				throw new ArgumentException($"Invalid node to the out port", nameof(node));

			if (!m_outPort.HasValue || m_outPort.Value == null)
			{
				if (lastImage != null)
					lastImage.Dispose();

				return null;
			}
			else
				return Draw(m_outPort.Value, lastImage);
		}

		#endregion

		protected abstract Bitmap Draw(object visualSource, Bitmap lastImage);
	}


	public class VisualOutPortFloat2DArray : VisualOutPort
	{
		public VisualOutPortFloat2DArray(OutPort outPort)
			: base(outPort)
		{
		}

		protected override Bitmap Draw(object visualSource, Bitmap lastImage)
		{
			return DrawFloat2DArray((float[,])visualSource, lastImage);
		}

		public static Bitmap DrawFloat2DArray(float[,] values, Bitmap lastImage)
		{
			var width = values.GetLength(0);
			var height = values.GetLength(1);
			var image = VisualNodeHelpers.CreateImageAsNecessary(lastImage, width, height);

			for (var y = 0; y < height; y++)
			{
				for (var x = 0; x < width; x++)
				{
					var value = values[x, y];
					if (value < 0 || value > 1)
						throw new Exception("Image pixel should be in range [0, 1]");

					var grayscale = (int)Math.Round(value * 255);
					var color = Color.FromArgb(grayscale, grayscale, grayscale);

					image.SetPixel(x, y, color);
				}
			}

			return image;
		}
	}
}
