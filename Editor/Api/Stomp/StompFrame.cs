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

using System.Collections.Generic;

namespace Prefabby
{

enum StompCommand
{
	CONNECT,
	STOMP,
	CONNECTED,
	SEND,
	SUBSCRIBE,
	UNSUBSCRIBE,
	ACK,
	NACK,
	BEGIN,
	COMMIT,
	ABORT,
	DISCONNECT,
	MESSAGE,
	RECEIPT,
	ERROR
};

class StompFrame
{

	public StompFrame(StompCommand command)
		: this(command, string.Empty)
	{
	}

	public StompFrame(StompCommand command, string body)
		: this(command, body, new Dictionary<string, string>())
	{
	}

	public StompFrame(StompCommand command, string body, Dictionary<string, string> headers)
	{
		Command = command;
		Body = body;
		Headers = headers;
	}

	public Dictionary<string, string> Headers
	{
		get;
		private set;
	}

	public string Body
	{
		get;
		private set;
	}

	public StompCommand Command
	{
		get;
		private set;
	}

	public string this[string key]
	{
		get
		{
			return Headers.ContainsKey(key) ? Headers[key] : string.Empty;
		}
		set
		{
			Headers[key] = value;
		}
	}

}

}
