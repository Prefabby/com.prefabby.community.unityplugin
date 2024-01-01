/*
	Prefabby Unity plugin
    Copyright (C) 2023-2024  Matthias Gall <matt@prefabby.com>

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
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Prefabby
{

class StompFrameSerializer
{

	public string Serialize(StompFrame frame)
	{
		StringBuilder builder = new();
		builder.Append(frame.Command.ToString());
		builder.Append("\n");

		foreach (KeyValuePair<string, string> header in frame.Headers)
		{
			builder.Append($"{header.Key}:{header.Value}\n");
		}

		builder.Append("\n");
		builder.Append(frame.Body);
		builder.Append('\0');

		return builder.ToString();
	}

	public StompFrame Deserialize(string frame)
	{
		StringReader reader = new(frame);

		StompCommand command = Enum.Parse<StompCommand>(reader.ReadLine());

		Dictionary<string, string> headers = new();
		string header = reader.ReadLine();
		while (!string.IsNullOrEmpty(header))
		{
			string[] parts = header.Split(':');
			if (parts.Length == 2)
			{
				headers[parts[0].Trim()] = parts[1].Trim();
			}
			else
			{
				CommonUtils.LogError("STOMP message has invalid format: header doesn't consist of two parts");
			}
			header = reader.ReadLine();
		}

		string body = reader.ReadToEnd().TrimEnd('\0', '\r', '\n');

		return new StompFrame(command, body, headers);
	}

}

}
