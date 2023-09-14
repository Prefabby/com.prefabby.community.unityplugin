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
using System.Collections;
using System.IO;

using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
using UnityEditor.PackageManager;
using Unity.EditorCoroutines.Editor;

namespace Prefabby
{

public class UpdateCheck
{
	private const string gitRepoUrl = "https://github.com/Prefabby/com.prefabby.unityplugin.git";
	private const string gitApiUrl = "https://api.github.com/repos/Prefabby/com.prefabby.unityplugin/commits/develop";
	private const string gitApiShaHeader = "application/vnd.github.VERSION.sha";

	public static void Run(EditorWindow owner)
	{
		// Simple way to check whether the plugin is installed through Git and compare revisions only if sensible
		string absolute = Path.GetFullPath("Packages/com.prefabby.unityplugin");
		string lastElement = absolute[(absolute.LastIndexOf(Path.DirectorySeparatorChar) + 1)..];
		if (lastElement.Contains('@'))
		{
			string installedVersion = lastElement[(lastElement.IndexOf('@') + 1)..];

			EditorCoroutineUtility.StartCoroutine(
				GetGitHubHash(
					(hash) => {
						if (hash[0..installedVersion.Length] != installedVersion)
						{
							Debug.Log("GitHub version is different from installed version, prompting for update!");
							PromptForUpdate();
						}
					},
					() => {
						Debug.LogError("Failed to determine last commit hash from GitHub repository!");
					}
				),
				owner
			);
		}
	}

	private static IEnumerator GetGitHubHash(Action<string> successCallback, Action errorCallback)
	{
		using UnityWebRequest request = UnityWebRequest.Get(gitApiUrl);
		request.SetRequestHeader("Accept", gitApiShaHeader);
		yield return request.SendWebRequest();

		if (request.result == UnityWebRequest.Result.Success)
		{
			successCallback(request.downloadHandler.text);
		}
		else
		{
			errorCallback();
		}
	}

	private static void PromptForUpdate()
	{
		if (EditorUtility.DisplayDialog(
			"Update Available",
			"There's a Prefabby update available. Do you want to update now?",
			"Yes",
			"No"
		))
		{
			Client.Add(gitRepoUrl);
		}
	}

}

}
