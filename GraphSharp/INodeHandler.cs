using System.Text.Json;

namespace GraphSharp
{
	public interface INodeHandler
	{
		void OnSave(Node node, Utf8JsonWriter writer);
		void OnLoad(Node node, JsonElement element);
		void OnLink(OutPort selfOut, InPort otherIn);
		void OnUnlink(OutPort selfOut, InPort otherIn);
		void OnOutValuesChanged(Node node);
	}
}
