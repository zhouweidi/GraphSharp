using System.Drawing;

namespace GraphSharp.Editor
{
	public interface IVisualOutPort
	{
		Bitmap UpdateVisualOutPort(Bitmap lastImage);
	}
}
