namespace Prefabby
{

public interface ISettingsAccessor
{

	string GetApiHost();
	string GetUserId();
	bool IsShowCollaboratorSelection();
	float GetHierachyCheckDeltaTime();

}

}
