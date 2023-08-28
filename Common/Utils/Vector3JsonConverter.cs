/*
	Prefabby Unity plugin
	Copyright (C) 2023  Matthias Gall <matt@prefabby.com>

	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Affero General Public License as
	published by the Free Software Foundation, either version 3 of the
	License, or (at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Affero General Public License for more details.

	You should have received a copy of the GNU Affero General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;

using Newtonsoft.Json;
using UnityEngine;

namespace Prefabby
{

public class Vector3JsonConverter : JsonConverter
{

	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(Vector3);
	}

	public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
	{
		var t = serializer.Deserialize(reader);
		var iv = JsonConvert.DeserializeObject<Vector3>(t.ToString());
		return iv;
	}

	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
		Vector3 v = (Vector3)value;
		writer.WriteStartObject();
		writer.WritePropertyName("x");
		writer.WriteValue(v.x);
		writer.WritePropertyName("y");
		writer.WriteValue(v.y);
		writer.WritePropertyName("z");
		writer.WriteValue(v.z);
		writer.WriteEndObject();
	}

}

}
