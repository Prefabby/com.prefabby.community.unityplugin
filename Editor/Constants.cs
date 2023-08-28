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

namespace Prefabby
{

public class Constants
{

	// Should match package.json
	public const string version = "0.1.0";

	public const bool local = false;
	public const bool devel = false;

	public const string homepage = local ? "http://localhost:3000" : (devel ? "https://devel.prefabby.com" : "https://prefabby.com");
	public const string apiHost = local ? "http://localhost:8080" : homepage;

}

}
