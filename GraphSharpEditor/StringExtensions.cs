using System.Text;

namespace GraphSharp.Editor
{
	static class StringExtensions
	{
		public static string FormatName(this string name, string trimEnd = null)
		{
			int length = name.Length;

			if (!string.IsNullOrEmpty(trimEnd) && name.EndsWith(trimEnd))
				length -= trimEnd.Length;

			var sb = new StringBuilder(name.Length);

			for (int i = 0; i < length; i++)
			{
				char c = name[i];

				if (i > 0 && char.IsUpper(c))
					sb.Append(' ');

				sb.Append(c);
			}

			return sb.ToString();
		}
	}
}
