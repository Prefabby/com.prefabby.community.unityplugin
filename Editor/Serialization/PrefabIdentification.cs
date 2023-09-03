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
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;

namespace Prefabby
{

class PrefabIdentification
{

	private readonly EditorRestApi restApi;
	private readonly string collaborationId;
	private readonly string accessKey;

	public PrefabIdentification(EditorRestApi restApi, string collaborationId, string accessKey)
	{
		// collaborationId may be null
		Assert.IsNotNull(restApi);
		Assert.IsNotNull(accessKey);

		this.restApi = restApi;
		this.collaborationId = collaborationId;
		this.accessKey = accessKey;
	}

	public void IdentifyPrefabs(Transform transform, PrefabDictionary prefabDictionary, Action successCallback, Action<ErrorInfo> errorCallback)
	{
		Assert.IsNotNull(transform);
		Assert.IsNotNull(prefabDictionary);
		Assert.IsNotNull(successCallback);
		Assert.IsNotNull(errorCallback);

		// Build a list of relevant GOs to look at; only prefab roots are considered

		int invalidTransforms = 0;
		List<GameObject> relevantTransforms = new();

		TransformVisitor transformVisitor = new();
		transformVisitor.VisitAll(transform, (transform) => {
			GameObject go = transform.gameObject;

			if (PrefabUtility.IsAnyPrefabInstanceRoot(go))
			{
				// Is a prefab and will be checked
				relevantTransforms.Add(go);
			}
			else if (PrefabUtility.IsPartOfAnyPrefab(go))
			{
				// Is part of a prefab and will be ignored
			}
			else
			{
				Component[] components = go.GetComponents(typeof(Component));
				int numberOfUnknownComponents = components.Length - 1;
				if (numberOfUnknownComponents > 0)
				{
					// Eliminate known components
					foreach (Component component in components)
					{
						if (component.GetType() == typeof(PrefabbyAssetMarker) || component.GetType() == typeof(PrefabbyCollaborationMarker))
						{
							numberOfUnknownComponents--;
						}
					}
					// If there are still unknown ones, take note
					if (numberOfUnknownComponents > 0)
					{
						DebugUtils.Log(DebugContext.PrefabIdentification, $"GO {go.name} is not a prefab and has {numberOfUnknownComponents} unknown components!", go);
						// TODO make this a setting: should non-prefab GOs with components raise an error or should they be ignored?
						invalidTransforms++;
					}
				}
				else
				{
					relevantTransforms.Add(go);
				}
			}
		});

		// Other GOs than prefabs are not supported

		if (invalidTransforms > 0)
		{
			EditorUtils.Error("Prefabby can only be used with prefabs! Your change will not be published and will cause inconsistencies.");
			return;
		}

		// Try to identify the GOs and collect those which require identification

		List<IdentifyPrefabsRequest.IdentifyPrefabRequest> identifyPrefabRequests = new();

		foreach (GameObject go in relevantTransforms)
		{
			// Ignore "plain" objects which solely make up the hierarchy
			if (!PrefabUtility.IsAnyPrefabInstanceRoot(go))
			{
				continue;
			}

			(string path, string name) = EditorUtils.GetPathAndNameFromPrefab(go);

			PrefabDictionaryItem item = prefabDictionary.Resolve(path, name);
			if (item == null)
			{
				DebugUtils.Log(DebugContext.PrefabIdentification, $"Prefab {path}{name} isn't known yet and needs to be identified");

				identifyPrefabRequests.Add(new IdentifyPrefabsRequest.IdentifyPrefabRequest
				{
					name = name,
					path = path,
					hint = "Prefab"
				});
			}
		}

		if (identifyPrefabRequests.Count > 0)
		{
			// Try to resolve the prefab

			IdentifyPrefabsRequest identifyPrefabsRequest = new()
			{
				prefabs = identifyPrefabRequests
			};

			if (collaborationId != null)
			{
				DebugUtils.Log(DebugContext.PrefabIdentification, $"Attempting to identify {identifyPrefabRequests.Count} prefabs for collaboration {collaborationId}...");

				restApi.IdentifyPrefabsForCollaboration(
					accessKey,
					collaborationId,
					identifyPrefabsRequest,
					(List<PrefabDictionaryItem> result) => {
						foreach (PrefabDictionaryItem item in result)
						{
							prefabDictionary.Add(item);
						}

						successCallback();
					},
					errorCallback
				);
			}
			else
			{
				DebugUtils.Log(DebugContext.PrefabIdentification, $"Attempting to identify {identifyPrefabRequests.Count} prefabs to publish an asset...");

				restApi.IdentifyPrefabsForPublishing(
					accessKey,
					identifyPrefabsRequest,
					(List<PrefabDictionaryItem> result) => {
						foreach (PrefabDictionaryItem item in result)
						{
							prefabDictionary.Add(item);
						}

						successCallback();
					},
					errorCallback
				);
			}
		}
		else
		{
			successCallback();
		}
	}

}

}
