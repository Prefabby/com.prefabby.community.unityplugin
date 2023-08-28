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

using UnityEngine;
using UnityEditor;
using Unity.EditorCoroutines.Editor;

namespace Prefabby
{

public class PrefabbyWindow : EditorWindow
{

	public event Action OnSettingsChanged;

	[SerializeField]
	private int activeTab = 0;

	[SerializeField]
	private BrowsingTabContent browsingTabContent;

	[SerializeField]
	private CollaborationTabContent collaborationTabContent;

	[SerializeField]
	private PublishingTabContent publishingTabContent;

	[SerializeField]
	private SettingsTabContent settingsTabContent;

	internal Settings Settings
	{
		get
		{
			return settingsTabContent.Settings;
		}
	}

	private EditorCoroutine tickCoroutine;
	private Vector2 scrollPosition;
	private Texture2D logo;
	private GUIContent titleBarContent;
	private Vector2 titleBarLogoSize;

	[MenuItem("Tools/Prefabby")]
	public static PrefabbyWindow Init()
	{
		GUIContent titleContent = new()
		{
			text = "Prefabby"
		};

		PrefabbyWindow window = GetWindow(typeof(PrefabbyWindow)) as PrefabbyWindow;
		window.titleContent = titleContent;

		return window;
	}

	public PrefabbyWindow()
	{
		browsingTabContent = new BrowsingTabContent(this);
		publishingTabContent = new PublishingTabContent(this);
		collaborationTabContent = new CollaborationTabContent(this);
		settingsTabContent = new SettingsTabContent(this);
	}

	void OnEnable()
	{
		// Settings must be enabled first, as it will load the Settings object
		settingsTabContent.OnEnable();

		browsingTabContent.OnEnable();

		collaborationTabContent.OnEnable();

		publishingTabContent.OnEnable();

		tickCoroutine = EditorCoroutineUtility.StartCoroutine(Tick(), this);

		logo = Resources.Load("PrefabbyTitle") as Texture2D;
		titleBarContent = new GUIContent($"<size=10><b>Prefabby</b> v{Constants.version} by @digitalbreed</size>", logo);
		titleBarLogoSize = new Vector2(32, 24);
	}

	void OnDisable()
	{
		browsingTabContent.OnDisable();
		collaborationTabContent.OnDisable();
		settingsTabContent.OnDisable();
		publishingTabContent.OnDisable();

		if (tickCoroutine != null)
		{
			EditorCoroutineUtility.StopCoroutine(tickCoroutine);
			tickCoroutine = null;
		}
	}

	void OnSelectionChange()
	{
		collaborationTabContent.OnSelectionChange();
	}

	void OnGUI()
	{
		GUI.InitializeIfNecessary();

		scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUI.windowMargin);

		EditorGUIUtility.SetIconSize(titleBarLogoSize);
		if (GUILayout.Button(titleBarContent, GUI.titleBarStyle, GUILayout.Height(34)))
		{
			Application.OpenURL(Settings.apiHost);
		}
		EditorGUIUtility.SetIconSize(Vector2.zero);

		EditorGUILayout.Space();

		activeTab = GUILayout.Toolbar(activeTab, new string[]{ "Browse", "Collaborate", "Publish", "Settings" }, GUILayout.Height(32));

		EditorGUILayout.Space();

		switch (activeTab)
		{
			case 0:
				browsingTabContent.OnGUI(position);
				break;
			case 1:
				collaborationTabContent.OnGUI();
				break;
			case 2:
				publishingTabContent.OnGUI();
				break;
			case 3:
				settingsTabContent.OnGUI();
				break;
		}

		EditorGUILayout.EndScrollView();
	}

	IEnumerator Tick()
	{
		// EditorCoroutines don't work with regular WaitForSeconds yields,
		// so we need to improvise a little here and manually maintain timers;
		// they're not accurate but get the job done.

		long start250ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		long start1000ms = start250ms;
		long now;

		while (true)
		{
			now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			if (now - start250ms > 250)
			{
				start250ms = now;
				collaborationTabContent.Tick250();
			}
			if (now - start1000ms > 1000)
			{
				start1000ms = now;
				collaborationTabContent.Tick1000();
			}

			yield return null;
		}
	}

	public void TriggerSettingsChanged()
	{
		DebugUtils.Log(DebugContext.Settings, "Triggering settings change...");

		OnSettingsChanged?.Invoke();
	}

	public void ChangeTab(int newTab)
	{
		activeTab = newTab;
	}

	public void SetObjectToPublish(GameObject go)
	{
		publishingTabContent.SetObjectToPublish(go);
		activeTab = 2;
	}

}

}
