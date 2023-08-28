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
using System.Text;

namespace Prefabby
{

public class Logger
{

	private StringBuilder builder = new StringBuilder();

	public event Action<string> OnLogChanged;

	public void Append(string str)
	{
		builder.Append(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
		builder.Append(": ");
		builder.Append(str);
		builder.Append("\n\n");

		OnLogChanged?.Invoke(builder.ToString());
	}

	public void Clear()
	{
		builder = new StringBuilder();
		OnLogChanged?.Invoke("");
	}

	public string GetValue()
	{
		return builder.ToString();
	}

}

}
