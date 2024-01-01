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

namespace Prefabby
{

class AssetInfo
{

	public string id;
	public DateTime creationDateTime;
	public string creatorId;
	public string creatorDisplayName;
	public string title;
	public string description;
	public string representation;
	public SerializedTree content;
	public PrefabDictionary prefabDictionary;
	public int version;

	public string previewImageUrl;

	public List<ArtPackHint> requiredArtPacks;

}

}
